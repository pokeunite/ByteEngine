using System.Numerics;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Import-time, mesh-independent analysis of a humanoid skeleton. The graph and
/// inverse bind pose carry the main evidence; bone spelling only resolves side
/// ambiguity and boosts confidence after a structurally valid assignment.
/// </summary>
public sealed class HumanoidMappingResult
{
    public required HumanoidBoneMap Mapping { get; init; }
    public IReadOnlyDictionary<HumanoidBone, float> BoneConfidence { get; init; } =
        new Dictionary<HumanoidBone, float>();
    public IReadOnlyList<HumanoidBone> NeedsReview { get; init; } = Array.Empty<HumanoidBone>();
    public float OverallConfidence { get; init; }
    public bool IsHumanoid => Mapping.MissingRequiredBones().Count == 0 && OverallConfidence >= .65f;
}

public static class HumanoidSkeletonAnalyzer
{
    private sealed class Node
    {
        public required int Index;
        public required string Name;
        public required int Parent;
        public required Vector3 Position;
        public List<int> Children { get; } = new();
    }

    public static HumanoidMappingResult Analyze(SkeletonAsset? skeleton)
    {
        HumanoidBoneMap map = new();
        Dictionary<HumanoidBone, float> confidence = new();
        if (skeleton?.Bones is not { Count: > 0 } bones)
            return Result(map, confidence);

        // FBX transform wrappers remain in the imported asset for playback,
        // but must not count as anatomical joints during mapping.
        int[] retained = Enumerable.Range(0, bones.Count)
            .Where(i => !bones[i].Name.Contains("$AssimpFbx$", StringComparison.Ordinal))
            .ToArray();
        if (retained.Length == 0) return Result(map, confidence);
        Dictionary<int, int> denseByOriginal = retained.Select((index, dense) => (index, dense))
            .ToDictionary(pair => pair.index, pair => pair.dense);
        Node[] nodes = new Node[retained.Length];
        for (int dense = 0; dense < retained.Length; dense++)
        {
            int original = retained[dense];
            if (!Matrix4x4.Invert(bones[original].BindPose, out Matrix4x4 bind) ||
                !Finite(bind.Translation))
                return Result(map, confidence);
            int parent = bones[original].ParentIndex;
            HashSet<int> seen = new();
            while (parent >= 0 && parent < bones.Count && seen.Add(parent) &&
                !denseByOriginal.ContainsKey(parent))
                parent = bones[parent].ParentIndex;
            nodes[dense] = new Node { Index = dense, Name = bones[original].Name,
                Parent = denseByOriginal.TryGetValue(parent, out int mappedParent) ? mappedParent : -1,
                Position = bind.Translation };
        }
        for (int i = 0; i < nodes.Length; i++)
            if (nodes[i].Parent >= 0 && nodes[i].Parent < nodes.Length &&
                nodes[i].Parent != i)
                nodes[nodes[i].Parent].Children.Add(i);

        float size = BoundingSize(nodes);
        if (size < 1e-5f) return Result(map, confidence);

        int hips = -1, spineStart = -1, legA = -1, legB = -1;
        float best = 0;
        foreach (Node node in nodes)
        {
            if (node.Children.Count < 3) continue;
            foreach (int a in node.Children)
            foreach (int b in node.Children)
            foreach (int up in node.Children)
            {
                if (a >= b || up == a || up == b) continue;
                Vector3 va = nodes[a].Position - node.Position;
                Vector3 vb = nodes[b].Position - node.Position;
                Vector3 vu = nodes[up].Position - node.Position;
                if (va.Length() < size * .025f || vb.Length() < size * .025f ||
                    vu.Length() < size * .025f) continue;
                if ((va + vb).Length() < size * .01f) continue;
                Vector3 down = Vector3.Normalize(va + vb);
                Vector3 torso = Vector3.Normalize(vu);
                float opposition = -Vector3.Dot(down, torso);
                float symmetry = 1 - MathF.Abs(va.Length() - vb.Length()) /
                    MathF.Max(va.Length(), vb.Length());
                float spread = (nodes[a].Position - nodes[b].Position).Length() / size;
                float score = opposition * 2 + symmetry + MathF.Min(spread, 1) +
                    (HasMajorFork(nodes, up, size) ? 3f : 0f);
                if (opposition < .35f || symmetry < .35f || score <= best) continue;
                best = score; hips = node.Index; legA = a; legB = b; spineStart = up;
            }
        }
        if (hips < 0) return Result(map, confidence);
        Vector3 upAxis = Vector3.Normalize(nodes[spineStart].Position - nodes[hips].Position);
        List<int> leftLegPath = LongestPath(nodes, legA, size);
        List<int> rightLegPath = LongestPath(nodes, legB, size);
        if (leftLegPath.Count < 3 || rightLegPath.Count < 3)
            return Result(map, confidence);

        // Foot travel supplies a rotation-invariant forward cue. An entirely
        // symmetric skeleton cannot determine anatomical left from geometry.
        Vector3 forward = Vector3.Zero;
        foreach (List<int> path in new[] { leftLegPath, rightLegPath })
        {
            Vector3 footDirection = nodes[path[2]].Position - nodes[path[1]].Position;
            forward += footDirection - Vector3.Dot(footDirection, upAxis) * upAxis;
        }
        bool hasForward = forward.Length() > size * .025f;
        if (hasForward) forward = Vector3.Normalize(forward);
        Vector3 rightAxis = hasForward ? Vector3.Normalize(Vector3.Cross(forward, upAxis)) :
            Vector3.Normalize(nodes[legB].Position - nodes[legA].Position);

        // Explicit side spelling is only used to orient the pair, never to
        // choose an anatomically invalid branch.
        int sideA = SideHint(nodes[legA].Name);
        int sideB = SideHint(nodes[legB].Name);
        if (sideA != 0 && sideB == -sideA)
        {
            float aSide = Vector3.Dot(nodes[legA].Position - nodes[legB].Position, rightAxis);
            if ((sideA > 0 && aSide < 0) || (sideA < 0 && aSide > 0))
                rightAxis = -rightAxis;
        }
        else if (Vector3.Dot(nodes[legB].Position - nodes[legA].Position, rightAxis) < 0)
            rightAxis = -rightAxis;

        bool aIsLeft = Vector3.Dot(nodes[legA].Position - nodes[legB].Position, rightAxis) < 0;
        List<int> leftLeg = aIsLeft ? leftLegPath : rightLegPath;
        List<int> rightLeg = aIsLeft ? rightLegPath : leftLegPath;
        Assign(HumanoidBone.Hips, hips, .96f);
        if (nodes[hips].Parent >= 0) Assign(HumanoidBone.Root, nodes[hips].Parent, .76f);
        AssignLeg(leftLeg, true);
        AssignLeg(rightLeg, false);

        List<int> torsoPath = new() { spineStart };
        int current = spineStart;
        int branch = -1, armA = -1, armB = -1;
        for (int depth = 0; depth < nodes.Length; depth++)
        {
            List<int> children = nodes[current].Children
                .Where(i => Vector3.Distance(nodes[i].Position, nodes[current].Position) > size * .01f)
                .ToList();
            int central = children.OrderByDescending(i =>
                Vector3.Dot(nodes[i].Position - nodes[current].Position, upAxis)).FirstOrDefault(-1);
            List<int> lateral = children.Where(i => i != central &&
                MathF.Abs(Vector3.Dot(nodes[i].Position - nodes[current].Position, rightAxis)) >
                size * .02f).ToList();
            if (lateral.Count >= 2 && branch < 0)
            {
                var pair = (from a in lateral from b in lateral where a < b
                    let sideAValue = Vector3.Dot(nodes[a].Position - nodes[current].Position, rightAxis)
                    let sideBValue = Vector3.Dot(nodes[b].Position - nodes[current].Position, rightAxis)
                    where sideAValue * sideBValue < 0
                    orderby MathF.Min(MathF.Abs(sideAValue), MathF.Abs(sideBValue)) descending
                    select (a, b)).FirstOrDefault();
                if (pair != default) { branch = current; armA = pair.a; armB = pair.b; }
            }
            if (central < 0 || central == current ||
                Vector3.Dot(nodes[central].Position - nodes[current].Position, upAxis) <
                    -size * .015f) break;
            Vector3 nextDirection = Vector3.Normalize(nodes[central].Position - nodes[current].Position);
            if (Vector3.Dot(nextDirection, upAxis) < .72f) break;
            torsoPath.Add(central);
            current = central;
        }
        if (branch < 0 || torsoPath.Count < 2) return Result(map, confidence);
        Assign(HumanoidBone.Spine, spineStart, .90f);
        int branchPosition = torsoPath.IndexOf(branch);
        if (branchPosition > 0) Assign(HumanoidBone.Chest, branch, .87f);
        if (branchPosition > 2) Assign(HumanoidBone.UpperChest,
            torsoPath[Math.Max(1, branchPosition - 1)], .70f);
        Assign(HumanoidBone.Head, torsoPath[^1], .86f);
        if (torsoPath.Count > branchPosition + 2)
            Assign(HumanoidBone.Neck, torsoPath[^2], .82f);

        List<int> armPathA = LongestPath(nodes, armA, size);
        List<int> armPathB = LongestPath(nodes, armB, size);
        if (armPathA.Count >= 3 && armPathB.Count >= 3)
        {
            bool armAIsLeft = Vector3.Dot(nodes[armA].Position - nodes[armB].Position, rightAxis) < 0;
            AssignArm(armAIsLeft ? armPathA : armPathB, true);
            AssignArm(armAIsLeft ? armPathB : armPathA, false);
        }
        return Result(map, confidence);

        void Assign(HumanoidBone role, int index, float score)
        {
            if (index < 0 || index >= nodes.Length || map.Bones.Values.Contains(nodes[index].Name))
                return;
            map.SetBone(role, nodes[index].Name);
            confidence[role] = score;
        }
        void AssignLeg(List<int> path, bool left)
        {
            HumanoidBone upper = left ? HumanoidBone.LeftUpperLeg : HumanoidBone.RightUpperLeg;
            HumanoidBone lower = left ? HumanoidBone.LeftLowerLeg : HumanoidBone.RightLowerLeg;
            HumanoidBone foot = left ? HumanoidBone.LeftFoot : HumanoidBone.RightFoot;
            float sideConfidence = hasForward || SideHint(nodes[path[0]].Name) != 0 ? .91f : .68f;
            Assign(upper, path[0], sideConfidence);
            Assign(lower, path[1], sideConfidence);
            Assign(foot, path[2], sideConfidence);
            if (path.Count > 3) Assign(left ? HumanoidBone.LeftToes : HumanoidBone.RightToes,
                path[3], .67f);
        }
        void AssignArm(List<int> path, bool left)
        {
            // A short first edge is a clavicle; a long first edge is upper arm.
            float first = Vector3.Distance(nodes[branch].Position, nodes[path[0]].Position);
            float second = Vector3.Distance(nodes[path[0]].Position, nodes[path[1]].Position);
            bool shoulder = path.Count >= 4 && (first < second * .65f ||
                (Vector3.Distance(nodes[path[1]].Position, nodes[path[2]].Position) >
                    second * 1.55f && first < second));
            int start = shoulder ? 1 : 0;
            if (path.Count < start + 3) return;
            float sideConfidence = hasForward || SideHint(nodes[path[0]].Name) != 0 ? .88f : .66f;
            if (shoulder) Assign(left ? HumanoidBone.LeftShoulder : HumanoidBone.RightShoulder,
                path[0], .80f);
            Assign(left ? HumanoidBone.LeftUpperArm : HumanoidBone.RightUpperArm,
                path[start], sideConfidence);
            Assign(left ? HumanoidBone.LeftLowerArm : HumanoidBone.RightLowerArm,
                path[start + 1], sideConfidence);
            Assign(left ? HumanoidBone.LeftHand : HumanoidBone.RightHand,
                path[start + 2], sideConfidence);
        }
    }

    private static bool HasMajorFork(Node[] nodes, int start, float size)
    {
        HashSet<int> seen = new();
        Stack<int> pending = new();
        pending.Push(start);
        while (pending.Count > 0)
        {
            int index = pending.Pop();
            if (!seen.Add(index)) continue;
            if (nodes[index].Children.Count(child =>
                    Vector3.Distance(nodes[child].Position, nodes[index].Position) > size * .02f) >= 3)
                return true;
            foreach (int child in nodes[index].Children) pending.Push(child);
        }
        return false;
    }
    private static List<int> LongestPath(Node[] nodes, int start, float size)
    {
        List<int> result = new() { start };
        HashSet<int> seen = new() { start };
        while (result.Count < nodes.Length)
        {
            int parent = result[^1];
            int next = nodes[parent].Children.Where(i => !seen.Contains(i))
                .OrderByDescending(i => Reach(nodes, i, size, new HashSet<int>(seen)))
                .FirstOrDefault(-1);
            if (next < 0 || Vector3.Distance(nodes[next].Position, nodes[parent].Position) <
                size * .006f) break;
            result.Add(next); seen.Add(next);
        }
        return result;
    }

    private static float Reach(Node[] nodes, int node, float size, HashSet<int> seen)
    {
        if (!seen.Add(node)) return 0;
        float best = 0;
        foreach (int child in nodes[node].Children)
            best = MathF.Max(best, Reach(nodes, child, size, new HashSet<int>(seen)));
        return best + (nodes[node].Parent >= 0 &&
            nodes[node].Parent < nodes.Length
            ? Vector3.Distance(nodes[node].Position, nodes[nodes[node].Parent].Position) : 0);
    }

    private static HumanoidMappingResult Result(HumanoidBoneMap map,
        Dictionary<HumanoidBone, float> confidence)
    {
        HumanoidBone[] missing = map.MissingRequiredBones().ToArray();
        float overall = HumanoidBoneCatalog.Required.Average(role =>
            confidence.TryGetValue(role, out float value) ? value : 0f);
        return new HumanoidMappingResult
        {
            Mapping = map,
            BoneConfidence = confidence,
            NeedsReview = HumanoidBoneCatalog.Required.Where(role =>
                !confidence.TryGetValue(role, out float value) || value < .75f).ToArray(),
            OverallConfidence = overall
        };
    }

    private static float BoundingSize(Node[] nodes)
    {
        Vector3 min = nodes[0].Position, max = min;
        foreach (Node node in nodes) { min = Vector3.Min(min, node.Position); max = Vector3.Max(max, node.Position); }
        return (max - min).Length();
    }

    private static int SideHint(string name)
    {
        string value = name.ToLowerInvariant();
        if (value.Contains("left") || value.EndsWith(".l") || value.EndsWith("_l") || value.StartsWith("l_") ||
            value.Contains("_l_")) return -1;
        if (value.Contains("right") || value.EndsWith(".r") || value.EndsWith("_r") || value.StartsWith("r_") ||
            value.Contains("_r_")) return 1;
        return 0;
    }

    private static bool Finite(Vector3 p) =>
        float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}


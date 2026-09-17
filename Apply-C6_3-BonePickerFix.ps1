param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath($PSScriptRoot)
$path = Join-Path $root "Editor\ByteEngine.Editor\ComponentPropertyRenderer.cs"

if (-not (Test-Path -LiteralPath $path)) {
    throw "Could not find ComponentPropertyRenderer.cs. Put this script in C:\Users\codex\ByteEngine."
}

$text = [System.IO.File]::ReadAllText($path)

# Back up once.
$backup = "$path.c63.bak"
if (-not (Test-Path -LiteralPath $backup)) {
    Copy-Item -LiteralPath $path -Destination $backup
}

# 1) Pass EditorProjectContext into DrawBoneSelector.
$callPattern = @'
DrawBoneSelector\(
\s*boneSocket,
\s*label,
\s*ref boneName\)
'@

$callReplacement = @'
DrawBoneSelector(
                        boneSocket,
                        project,
                        label,
                        ref boneName)
'@

if ($text -notmatch 'DrawBoneSelector\(\s*boneSocket,\s*project,') {
    $matches = [regex]::Matches(
        $text,
        $callPattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    if ($matches.Count -ne 1) {
        throw "Could not safely patch the DrawBoneSelector call. Expected 1 match, found $($matches.Count)."
    }

    $text = [regex]::Replace(
        $text,
        $callPattern,
        $callReplacement,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
}

# 2) Replace the complete DrawBoneSelector method.
$methodPattern = @'
private static bool DrawBoneSelector\(
.*?
(?=\s*private static bool DrawAnimationClipSelector\()
'@

$newMethod = @'
private static bool DrawBoneSelector(
        BoneSocket3D socket,
        EditorProjectContext project,
        string label,
        ref string boneName)
    {
        IReadOnlyList<string> bones =
            GetSocketBoneNames(
                socket,
                project);

        string currentBoneName =
            boneName;

        bool currentExists =
            bones.Any(
                candidate =>
                    string.Equals(
                        candidate,
                        currentBoneName,
                        StringComparison.OrdinalIgnoreCase));

        string preview =
            string.IsNullOrWhiteSpace(
                boneName)
                ? "Select Bone..."
                : currentExists
                    ? boneName
                    : $"{boneName} (Missing)";

        bool changed =
            false;

        if (!ImGui.BeginCombo(
                label,
                preview))
        {
            return false;
        }

        if (ImGui.Selectable(
                "None",
                string.IsNullOrWhiteSpace(
                    boneName)))
        {
            boneName =
                string.Empty;

            changed =
                true;
        }

        if (bones.Count == 0)
        {
            ImGui.TextDisabled(
                "No skeleton bones found for this character/model.");
        }
        else
        {
            foreach (string bone
                     in bones)
            {
                bool selected =
                    string.Equals(
                        bone,
                        boneName,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        bone,
                        selected))
                {
                    boneName =
                        bone;

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.EndCombo();

        return changed;
    }

    private static IReadOnlyList<string> GetSocketBoneNames(
        BoneSocket3D socket,
        EditorProjectContext project)
    {
        /*
         * Runtime/Play mode: prefer the live renderer because its skeleton is
         * exactly the one BoneSocket3D will bind to.
         */
        SkeletalMeshRenderer? renderer =
            socket.ResolveSourceRenderer();

        if (renderer != null)
        {
            string[] liveBones =
                renderer.BoneNames
                    .Where(
                        name =>
                            !string.IsNullOrWhiteSpace(
                                name))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (liveBones.Length > 0)
            {
                return liveBones;
            }
        }

        /*
         * Edit mode: no SkeletalMeshRenderer is required. Find the nearest
         * character AnimationController and reuse the same animated-model
         * discovery path used by the animation clip picker.
         */
        for (GameObject? current = socket.GameObject;
             current != null;
             current = current.Parent)
        {
            AnimationController? controller =
                current.GetComponent<AnimationController>();

            if (controller == null)
            {
                continue;
            }

            if (AnimationClipDiscovery.TryGetModel(
                    controller,
                    project,
                    out ModelAsset? model,
                    out _) &&
                model?.Skeleton != null)
            {
                string[] modelBones =
                    model.Skeleton.Bones
                        .Select(
                            bone =>
                                bone.Name)
                        .Where(
                            name =>
                                !string.IsNullOrWhiteSpace(
                                    name))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                if (modelBones.Length > 0)
                {
                    return modelBones;
                }
            }
        }

        /*
         * Fallback for a model hierarchy that has no AnimationController yet.
         * Walk upward from the socket and inspect sibling/descendant visual
         * branches for a ModelHierarchyInstance with a skeleton.
         */
        GameObject branchToSkip =
            socket.GameObject;

        for (GameObject? ancestor = socket.GameObject.Parent;
             ancestor != null;
             ancestor = ancestor.Parent)
        {
            if (TryFindModelBones(
                    ancestor,
                    branchToSkip,
                    project,
                    out IReadOnlyList<string> modelBones))
            {
                return modelBones;
            }

            branchToSkip =
                ancestor;
        }

        return Array.Empty<string>();
    }

    private static bool TryFindModelBones(
        GameObject root,
        GameObject branchToSkip,
        EditorProjectContext project,
        out IReadOnlyList<string> bones)
    {
        foreach (GameObject candidate
                 in SelfAndDescendantsForSocket(
                     root,
                     branchToSkip))
        {
            ModelHierarchyInstance? instance =
                candidate.GetComponent<ModelHierarchyInstance>();

            if (instance == null ||
                instance.Model.IsEmpty)
            {
                continue;
            }

            try
            {
                ModelAsset model =
                    project.Assets.LoadModel(
                        instance.Model);

                if (model.Skeleton == null)
                {
                    continue;
                }

                string[] names =
                    model.Skeleton.Bones
                        .Select(
                            bone =>
                                bone.Name)
                        .Where(
                            name =>
                                !string.IsNullOrWhiteSpace(
                                    name))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                if (names.Length == 0)
                {
                    continue;
                }

                bones =
                    names;

                return true;
            }
            catch
            {
                // Keep searching other visual/model branches.
            }
        }

        bones =
            Array.Empty<string>();

        return false;
    }

    private static IEnumerable<GameObject> SelfAndDescendantsForSocket(
        GameObject root,
        GameObject branchToSkip)
    {
        if (!ReferenceEquals(
                root,
                branchToSkip))
        {
            yield return root;
        }

        foreach (GameObject child
                 in root.Children)
        {
            if (ReferenceEquals(
                    child,
                    branchToSkip))
            {
                continue;
            }

            foreach (GameObject candidate
                     in SelfAndDescendantsForSocket(
                         child,
                         branchToSkip))
            {
                yield return candidate;
            }
        }
    }

'@

$methodMatches = [regex]::Matches(
    $text,
    $methodPattern,
    [System.Text.RegularExpressions.RegexOptions]::Singleline
)

if ($methodMatches.Count -ne 1) {
    throw "Could not safely replace DrawBoneSelector. Expected 1 method block, found $($methodMatches.Count)."
}

$text = [regex]::Replace(
    $text,
    $methodPattern,
    $newMethod,
    [System.Text.RegularExpressions.RegexOptions]::Singleline
)

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($path, $text, $utf8)

Write-Host ""
Write-Host "C6.3 Bone Picker fix applied." -ForegroundColor Green
Write-Host "BoneSocket3D can now populate bones from the model asset in Edit mode." -ForegroundColor Green
Write-Host ""
Write-Host "Now run:" -ForegroundColor Yellow
Write-Host "powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1" -ForegroundColor Cyan
Write-Host ""

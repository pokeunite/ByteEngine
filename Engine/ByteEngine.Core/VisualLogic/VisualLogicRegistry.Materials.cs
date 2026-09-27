using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterMaterials(VisualLogicRegistry registry)
    {
        static void Add(VisualLogicRegistry registry, string id, string name,
            Action<VisualInstruction, EventExecutionContext> execute) =>
            registry.RegisterAction(new VisualActionDefinition
            {
                Id = id, Category = "Materials", DisplayName = name, Execute = execute
            });
        Add(registry, "material.setMaterial", "Set Material", SetMaterial);
        Add(registry, "material.setBaseColor", "Set Base Color", (i, c) =>
        {
            MaterialOverrideState? state = ResolveMaterialOverrides(i, c);
            if (state == null) return;
            Vector3 rgb = EventValueResolver.GetVector3(i, "color", c, Vector3.One);
            float alpha = (float)EventValueResolver.GetNumber(i, "alpha", c, 1);
            state.BaseColor = new Vector4(rgb, Math.Clamp(alpha, 0f, 1f));
        });
        Add(registry, "material.setMetallic", "Set Metallic", (i, c) =>
        {
            MaterialOverrideState? state = ResolveMaterialOverrides(i, c);
            if (state != null) state.Metallic =
                Math.Clamp((float)EventValueResolver.GetNumber(i, "value", c, 0), 0f, 1f);
        });
        Add(registry, "material.setRoughness", "Set Roughness", (i, c) =>
        {
            MaterialOverrideState? state = ResolveMaterialOverrides(i, c);
            if (state != null) state.Roughness =
                Math.Clamp((float)EventValueResolver.GetNumber(i, "value", c, 1), .04f, 1f);
        });
        Add(registry, "material.setEmissionColor", "Set Emission Color", (i, c) =>
        {
            MaterialOverrideState? state = ResolveMaterialOverrides(i, c);
            if (state != null) state.EmissionColor =
                EventValueResolver.GetVector3(i, "color", c, Vector3.One);
        });
        Add(registry, "material.setEmissionIntensity", "Set Emission Intensity", (i, c) =>
        {
            MaterialOverrideState? state = ResolveMaterialOverrides(i, c);
            if (state != null) state.EmissionIntensity =
                Math.Max(0f, (float)EventValueResolver.GetNumber(i, "value", c, 1));
        });
        Add(registry, "material.setTexture", "Set Texture", SetMaterialTexture);
        Add(registry, "material.resetOverrides", "Reset Material Overrides", (i, c) =>
            ResolveMaterialOverrides(i, c)?.Reset());
    }

    private static void SetMaterial(VisualInstruction instruction, EventExecutionContext context)
    {
        AssetReference reference = ParseMaterialReference(
            EventValueResolver.GetString(instruction, "material", context));
        if (reference.IsEmpty) return;
        if (!AnimationRuntimeAssets.TryGet(out AssetManager? assets) || assets == null) return;
        try { assets.LoadMaterial(reference); }
        catch { return; }
        if (!TryResolveMaterialRenderer(instruction, context,
            out MeshRenderer? mesh, out SkeletalMeshRenderer? skeletal, out int slot)) return;
        if (mesh != null) mesh.MaterialAssetReference = reference;
        else if (skeletal != null)
        {
            while (skeletal.MaterialAssetSlots.Count <= slot)
                skeletal.MaterialAssetSlots.Add(AssetReference.Empty);
            skeletal.MaterialAssetSlots[slot] = reference;
        }
    }

    private static void SetMaterialTexture(VisualInstruction instruction, EventExecutionContext context)
    {
        MaterialOverrideState? state = ResolveMaterialOverrides(instruction, context);
        if (state == null) return;
        string slotName = EventValueResolver.GetString(instruction, "textureSlot", context, "BaseColor");
        if (!Enum.TryParse(slotName, true, out MaterialTextureSlot slot)) return;
        string token = EventValueResolver.GetString(instruction, "texture", context);
        if (string.IsNullOrWhiteSpace(token))
        { state.SetTexture(slot, AssetReference.Empty); return; }
        AssetReference reference = ParseMaterialReference(token);
        if (!reference.IsEmpty) state.SetTexture(slot, reference);
    }

    private static AssetReference ParseMaterialReference(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return AssetReference.Empty;
        if (Guid.TryParse(token, out Guid guid)) return new AssetReference(guid);
        if (Path.IsPathRooted(token)) return AssetReference.Empty;
        try { return new AssetReference(token); }
        catch { return AssetReference.Empty; }
    }

    private static MaterialOverrideState? ResolveMaterialOverrides(
        VisualInstruction instruction, EventExecutionContext context)
    {
        if (!TryResolveMaterialRenderer(instruction, context,
            out MeshRenderer? mesh, out SkeletalMeshRenderer? skeletal, out int slot)) return null;
        return mesh?.MaterialOverrides ?? skeletal?.GetMaterialOverrides(slot);
    }

    private static bool TryResolveMaterialRenderer(VisualInstruction instruction,
        EventExecutionContext context, out MeshRenderer? mesh,
        out SkeletalMeshRenderer? skeletal, out int slot)
    {
        mesh = null;
        skeletal = null;
        slot = Math.Max(0, (int)EventValueResolver.GetNumber(instruction, "slot", context, 0));
        GameObject? target = ResolveObjectTarget(instruction, context, false);
        if (target == null) return false;
        // A skeletal renderer owns its mesh slots. Imported static models instead
        // expose one MeshRenderer on each child, in hierarchy order.
        skeletal = FindSkeletalRenderer(target);
        if (skeletal != null) return true;
        int remaining = slot;
        mesh = FindStaticRenderer(target, ref remaining);
        slot = 0; // The selected static renderer has a single local material slot.
        return mesh != null;
    }

    private static SkeletalMeshRenderer? FindSkeletalRenderer(GameObject object3D)
    {
        SkeletalMeshRenderer? renderer = object3D.GetComponent<SkeletalMeshRenderer>();
        if (renderer != null) return renderer;
        foreach (GameObject child in object3D.Children)
        {
            renderer = FindSkeletalRenderer(child);
            if (renderer != null) return renderer;
        }
        return null;
    }

    private static MeshRenderer? FindStaticRenderer(GameObject object3D, ref int slot)
    {
        MeshRenderer? renderer = object3D.GetComponent<MeshRenderer>();
        if (renderer != null && slot-- == 0) return renderer;
        foreach (GameObject child in object3D.Children)
        {
            renderer = FindStaticRenderer(child, ref slot);
            if (renderer != null) return renderer;
        }
        return null;
    }
}

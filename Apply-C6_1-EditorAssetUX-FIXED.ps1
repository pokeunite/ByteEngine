param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Root = [System.IO.Path]::GetFullPath($PSScriptRoot)

if (-not (Test-Path -LiteralPath (Join-Path $Root "ByteEngine.sln"))) {
    throw "Put this script in C:\Users\codex\ByteEngine and run it from the ByteEngine repo root."
}

function Read-Text([string]$Path) {
    return [System.IO.File]::ReadAllText($Path)
}

function Write-Text([string]$Path, [string]$Text) {
    $Utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $Utf8)
}

function Save-Backup([string]$Path) {
    $Backup = "$Path.c61.bak"
    if (-not (Test-Path -LiteralPath $Backup)) {
        Copy-Item -LiteralPath $Path -Destination $Backup
    }
}

function Replace-RegexOnce {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Pattern,
        [Parameter(Mandatory=$true)][string]$Replacement,
        [Parameter(Mandatory=$true)][string]$Description,
        [string]$AlreadyMarker = ""
    )

    $Text = Read-Text $Path

    if ($AlreadyMarker -and $Text.Contains($AlreadyMarker)) {
        Write-Host "Already applied: $Description" -ForegroundColor DarkGreen
        return
    }

    $Matches = [regex]::Matches(
        $Text,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    if ($Matches.Count -ne 1) {
        throw "Could not safely apply '$Description'. Expected 1 source match, found $($Matches.Count) in: $Path"
    }

    Save-Backup $Path

    $Text = [regex]::Replace(
        $Text,
        $Pattern,
        $Replacement,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    Write-Text $Path $Text
    Write-Host "Applied: $Description" -ForegroundColor Green
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor DarkCyan
Write-Host " ByteEngine C6.1 - Asset UX + Bone Picker FIXED" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor DarkCyan
Write-Host ""

# ---------------------------------------------------------------------------
# AssetDragDrop.cs
# The failed first patch may already have overwritten this file. This write is
# intentional and idempotent.
# ---------------------------------------------------------------------------

$AssetDragDropPath = Join-Path $Root "Editor\ByteEngine.Editor\AssetDragDrop.cs"

$AssetDragDropSource = @'
using System.Runtime.InteropServices;

using ByteEngine.Core.Scene;

using ImGuiNET;

namespace ByteEngine.Editor;

internal static class AssetDragDrop
{
    private const string PayloadType = "BYTEENGINE_ASSET_GUID";

    private static GameObject? _pendingObjectSelection;
    private static double _pendingObjectSelectionTime;

    /// <summary>
    /// Remembers the object that was selected immediately before an Asset
    /// Browser click. If that click turns into a drag, Set() restores the
    /// object so the Inspector remains a valid drop target.
    /// </summary>
    public static void RememberObjectSelection(
        GameObject? gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        _pendingObjectSelection =
            gameObject;

        _pendingObjectSelectionTime =
            ImGui.GetTime();
    }

    public static unsafe void Set(Guid guid)
    {
        RestoreObjectSelectionForDrag();

        Span<byte> bytes =
            stackalloc byte[16];

        guid.TryWriteBytes(bytes);

        fixed (byte* pointer = bytes)
        {
            ImGui.SetDragDropPayload(
                PayloadType,
                (nint)pointer,
                16,
                ImGuiCond.Once);
        }
    }

    public static unsafe Guid? Accept()
    {
        ImGuiPayloadPtr payload =
            ImGui.AcceptDragDropPayload(
                PayloadType);

        if (payload.NativePtr == null)
        {
            return null;
        }

        if (payload.Data == IntPtr.Zero ||
            payload.DataSize != 16 ||
            !payload.Delivery)
        {
            return null;
        }

        byte[] bytes =
            new byte[16];

        Marshal.Copy(
            payload.Data,
            bytes,
            0,
            bytes.Length);

        return new Guid(bytes);
    }

    private static void RestoreObjectSelectionForDrag()
    {
        GameObject? remembered =
            _pendingObjectSelection;

        _pendingObjectSelection =
            null;

        if (remembered == null ||
            ImGui.GetTime() -
            _pendingObjectSelectionTime >
            1.5)
        {
            return;
        }

        EditorState? state =
            EditorState.Active;

        if (state == null ||
            remembered.Scene == null ||
            (!ReferenceEquals(
                 remembered.Scene,
                 state.EditorScene) &&
             !ReferenceEquals(
                 remembered.Scene,
                 state.RuntimeScene)))
        {
            return;
        }

        state.Selection.Set(
            remembered);

        state.SelectedAssetId =
            null;

        state.SelectedAssetPath =
            null;

        state.SelectedModelAnimationKey =
            null;
    }
}
'@

Save-Backup $AssetDragDropPath
Write-Text $AssetDragDropPath $AssetDragDropSource
Write-Host "Applied: drag assets without losing the selected GameObject Inspector" -ForegroundColor Green

# ---------------------------------------------------------------------------
# AssetsPanel.cs
# ---------------------------------------------------------------------------

$AssetsPanelPath = Join-Path $Root "Editor\ByteEngine.Editor\Panels\AssetsPanel.cs"

Replace-RegexOnce `
    -Path $AssetsPanelPath `
    -Pattern '(public\s+bool\s+IsOpen\s*\{\s*get;\s*set;\s*\}\s*=\s*true\s*;)' `
    -Replacement @'
$1

    /// <summary>
    /// Folder currently displayed by the Asset Browser. External OS file drops
    /// import here when this path is inside the project's Assets directory.
    /// </summary>
    public string CurrentImportDirectory =>
        _currentDirectory;
'@ `
    -Description "expose current Asset Browser folder for imports" `
    -AlreadyMarker "public string CurrentImportDirectory"

# In SyncPrimaryAssetSelection(), remember the selected object immediately
# before that function clears it for normal asset selection.
$AssetsText = Read-Text $AssetsPanelPath
if (-not $AssetsText.Contains("AssetDragDrop.RememberObjectSelection(")) {
    $FunctionStart = $AssetsText.IndexOf(
        "private void SyncPrimaryAssetSelection(EditorState state)",
        [StringComparison]::Ordinal)

    if ($FunctionStart -lt 0) {
        throw "Could not find SyncPrimaryAssetSelection(EditorState state) in AssetsPanel.cs."
    }

    $ClearIndex = $AssetsText.IndexOf(
        "state.SelectedObject =",
        $FunctionStart,
        [StringComparison]::Ordinal)

    if ($ClearIndex -lt 0) {
        throw "Could not find SelectedObject clearing inside SyncPrimaryAssetSelection()."
    }

    Save-Backup $AssetsPanelPath

    $IndentStart = $AssetsText.LastIndexOf("`n", $ClearIndex)
    $IndentStart = if ($IndentStart -ge 0) { $IndentStart + 1 } else { 0 }
    $Indent = $AssetsText.Substring($IndentStart, $ClearIndex - $IndentStart)

    $Insert = @"
${Indent}AssetDragDrop.RememberObjectSelection(
${Indent}    state.SelectedObject);

"@

    $AssetsText =
        $AssetsText.Insert(
            $ClearIndex,
            $Insert)

    Write-Text $AssetsPanelPath $AssetsText
    Write-Host "Applied: remember GameObject before Asset Browser selection clears it" -ForegroundColor Green
}
else {
    Write-Host "Already applied: remember GameObject before Asset Browser selection clears it" -ForegroundColor DarkGreen
}

# ---------------------------------------------------------------------------
# EditorApplication.cs - OS drop uses current Asset Browser directory.
# ---------------------------------------------------------------------------

$EditorApplicationPath = Join-Path $Root "Editor\ByteEngine.Editor\EditorApplication.cs"

Replace-RegexOnce `
    -Path $EditorApplicationPath `
    -Pattern 'IReadOnlyList<AssetRecord>\s+imported\s*=\s*new\s+ExternalAssetImporter\(_projectContext,\s*_log\)\.Import\(e\.FileNames\)\s*;' `
    -Replacement @'
string? destinationDirectory =
            _assets?.CurrentImportDirectory;

        IReadOnlyList<AssetRecord> imported =
            new ExternalAssetImporter(
                _projectContext,
                _log)
            .Import(
                e.FileNames,
                destinationDirectory);
'@ `
    -Description "import external files into the currently open Asset Browser folder" `
    -AlreadyMarker "_assets?.CurrentImportDirectory"

# Do not wipe the GameObject selection after an OS import.
Replace-RegexOnce `
    -Path $EditorApplicationPath `
    -Pattern 'AssetRecord\?\s+selected\s*=\s*imported\.FirstOrDefault\(\)\s*;\s*if\s*\(selected\s*!=\s*null\)\s*\{\s*_state\.SelectedObject\s*=\s*null\s*;\s*_state\.SelectedAssetId\s*=\s*selected\.Guid\s*;\s*_state\.SelectedAssetPath\s*=\s*selected\.ProjectPath\s*;\s*\}' `
    -Replacement @'
AssetRecord? selected =
            imported.FirstOrDefault();

        if (selected != null &&
            _state.SelectedObject == null)
        {
            _state.SelectedAssetId =
                selected.Guid;

            _state.SelectedAssetPath =
                selected.ProjectPath;
        }
'@ `
    -Description "preserve selected GameObject while importing external assets" `
    -AlreadyMarker "_state.SelectedObject == null"

# ---------------------------------------------------------------------------
# ComponentPropertyRenderer.cs
# ---------------------------------------------------------------------------

$PropertyRendererPath = Join-Path $Root "Editor\ByteEngine.Editor\ComponentPropertyRenderer.cs"

# Replace AssetReference field with picker + drag/drop.
Replace-RegexOnce `
    -Path $PropertyRendererPath `
    -Pattern 'else\s+if\s*\(descriptor\.Property\.PropertyType\s*==\s*typeof\(AssetReference\)\)\s*\{.*?\}\s*else\s+if\s*\(descriptor\.Property\.PropertyType\s*==\s*typeof\(InputActionReference\)\)' `
    -Replacement @'
else if (descriptor.Property.PropertyType == typeof(AssetReference))
            {
                AssetReference reference =
                    before as AssetReference ??
                    AssetReference.Empty;

                if (project != null)
                {
                    edited =
                        DrawAssetReferenceSelector(
                            component,
                            descriptor,
                            project,
                            label,
                            ref reference);

                    after =
                        reference;
                }
                else
                {
                    ImGui.TextDisabled(
                        $"{descriptor.Metadata.DisplayName}: {reference}");
                }
            }
            else if (descriptor.Property.PropertyType == typeof(InputActionReference))
'@ `
    -Description "add project asset picker to AssetReference component fields" `
    -AlreadyMarker "DrawAssetReferenceSelector("

# Insert BoneSocket selector before AnimationController string handling.
Replace-RegexOnce `
    -Path $PropertyRendererPath `
    -Pattern 'else\s+if\s*\(component\s+is\s+AnimationController\s+clipController\s*&&' `
    -Replacement @'
else if (component is BoneSocket3D boneSocket &&
                     descriptor.Property.Name == nameof(BoneSocket3D.BoneName) &&
                     descriptor.Property.PropertyType == typeof(string))
            {
                string boneName =
                    before?.ToString() ??
                    string.Empty;

                edited =
                    DrawBoneSelector(
                        boneSocket,
                        label,
                        ref boneName);

                after =
                    boneName;
            }
            else if (component is AnimationController clipController &&
'@ `
    -Description "replace manual BoneSocket3D bone text with a bone dropdown" `
    -AlreadyMarker "component is BoneSocket3D boneSocket"

# Add helper methods directly before existing DrawAnimationClipSelector().
$Helpers = @'
    private static bool DrawAssetReferenceSelector(
        Component component,
        ComponentPropertyDescriptor descriptor,
        EditorProjectContext project,
        string label,
        ref AssetReference reference)
    {
        AssetType? expectedType =
            GetExpectedAssetType(
                component,
                descriptor.Property.Name);

        AssetRecord? current =
            reference.IsEmpty
                ? null
                : project.AssetDatabase.Resolve(
                    reference);

        string preview =
            current?.ProjectPath ??
            reference.CachedProjectPath ??
            "None";

        bool changed =
            false;

        if (ImGui.BeginCombo(
                label,
                preview))
        {
            if (ImGui.Selectable(
                    "None",
                    reference.IsEmpty))
            {
                reference =
                    AssetReference.Empty;

                changed =
                    true;
            }

            AssetRecord[] candidates =
                project.AssetDatabase.Assets
                    .Where(
                        asset =>
                            expectedType == null ||
                            asset.Type == expectedType.Value)
                    .OrderBy(
                        asset =>
                            asset.ProjectPath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (candidates.Length == 0)
            {
                ImGui.TextDisabled(
                    expectedType.HasValue
                        ? $"No {expectedType.Value} assets found."
                        : "No compatible assets found.");
            }

            foreach (AssetRecord asset
                     in candidates)
            {
                bool selected =
                    asset.Guid != Guid.Empty &&
                    asset.Guid == reference.Guid;

                if (ImGui.Selectable(
                        $"{asset.ProjectPath}##asset-picker:{descriptor.Property.Name}:{asset.Guid}",
                        selected))
                {
                    reference =
                        new AssetReference(
                            asset.Guid,
                            asset.ProjectPath);

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        // Keep drag/drop as well as the picker.
        if (ImGui.BeginDragDropTarget())
        {
            Guid? id =
                AssetDragDrop.Accept();

            if (id.HasValue &&
                project.AssetDatabase.TryGetAsset(
                    id.Value,
                    out AssetRecord? dropped) &&
                dropped != null &&
                (!expectedType.HasValue ||
                 dropped.Type == expectedType.Value))
            {
                reference =
                    new AssetReference(
                        dropped.Guid,
                        dropped.ProjectPath);

                changed =
                    true;
            }

            ImGui.EndDragDropTarget();
        }

        if (!reference.IsEmpty)
        {
            ImGui.SameLine();

            if (ImGui.SmallButton(
                    $"Clear##asset-picker-clear:{descriptor.Property.Name}"))
            {
                reference =
                    AssetReference.Empty;

                changed =
                    true;
            }
        }

        return changed;
    }

    private static AssetType? GetExpectedAssetType(
        Component component,
        string propertyName)
    {
        if (component is SpriteRenderer &&
            propertyName ==
                nameof(SpriteRenderer.TextureReference))
        {
            return AssetType.Texture2D;
        }

        if (component is AudioSource3D &&
            propertyName ==
                nameof(AudioSource3D.ClipReference))
        {
            return AssetType.AudioClip;
        }

        if (component is SkyEnvironment &&
            propertyName ==
                nameof(SkyEnvironment.EnvironmentMapReference))
        {
            return AssetType.Texture2D;
        }

        if (component is ModelHierarchyInstance &&
            propertyName ==
                nameof(ModelHierarchyInstance.Model))
        {
            return AssetType.Model3D;
        }

        if (component is SkeletalMeshRenderer &&
            propertyName ==
                nameof(SkeletalMeshRenderer.Model))
        {
            return AssetType.Model3D;
        }

        return null;
    }

    private static bool DrawBoneSelector(
        BoneSocket3D socket,
        string label,
        ref string boneName)
    {
        SkeletalMeshRenderer? renderer =
            socket.ResolveSourceRenderer();

        IReadOnlyList<string> bones =
            renderer?.BoneNames ??
            Array.Empty<string>();

        bool currentExists =
            bones.Any(
                candidate =>
                    string.Equals(
                        candidate,
                        boneName,
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

        if (renderer == null)
        {
            ImGui.TextDisabled(
                "No Skeletal Mesh Renderer found for this socket.");
        }
        else if (bones.Count == 0)
        {
            ImGui.TextDisabled(
                "The resolved skeletal model contains no bones.");
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

'@

$PropertyText = Read-Text $PropertyRendererPath

if (-not $PropertyText.Contains("private static bool DrawAssetReferenceSelector(")) {
    $Anchor = "    private static bool DrawAnimationClipSelector("
    $AnchorIndex = $PropertyText.IndexOf(
        $Anchor,
        [StringComparison]::Ordinal)

    if ($AnchorIndex -lt 0) {
        throw "Could not find DrawAnimationClipSelector() helper anchor in ComponentPropertyRenderer.cs."
    }

    Save-Backup $PropertyRendererPath
    $PropertyText =
        $PropertyText.Insert(
            $AnchorIndex,
            $Helpers)

    Write-Text $PropertyRendererPath $PropertyText
    Write-Host "Applied: asset picker and bone picker helper methods" -ForegroundColor Green
}
else {
    Write-Host "Already applied: asset picker and bone picker helper methods" -ForegroundColor DarkGreen
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor DarkGreen
Write-Host " C6.1 PATCH APPLIED" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor DarkGreen
Write-Host ""
Write-Host "Now rebuild + refresh Dist with:" -ForegroundColor Yellow
Write-Host "powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1" -ForegroundColor Cyan
Write-Host ""

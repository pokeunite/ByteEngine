param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath($PSScriptRoot)

if (-not (Test-Path -LiteralPath (Join-Path $root "ByteEngine.sln"))) {
    throw "Put this script in C:\Users\codex\ByteEngine and run it from the ByteEngine repo root."
}

function Read-Text([string]$Path) {
    [System.IO.File]::ReadAllText($Path)
}

function Write-Text([string]$Path, [string]$Text) {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}

function Backup-Once([string]$Path) {
    $backup = "$Path.c7.bak"
    if ((Test-Path -LiteralPath $Path) -and -not (Test-Path -LiteralPath $backup)) {
        Copy-Item -LiteralPath $Path -Destination $backup
    }
}

function Replace-Once {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Old,
        [Parameter(Mandatory=$true)][string]$New,
        [Parameter(Mandatory=$true)][string]$Description,
        [string]$AlreadyMarker = ""
    )

    $text = Read-Text $Path

    if ($AlreadyMarker -and $text.Contains($AlreadyMarker)) {
        Write-Host "Already applied: $Description" -ForegroundColor DarkGreen
        return
    }

    $count = ([regex]::Matches($text, [regex]::Escape($Old))).Count
    if ($count -ne 1) {
        throw "Could not safely apply '$Description'. Expected exactly 1 match in '$Path', found $count."
    }

    Backup-Once $Path
    Write-Text $Path ($text.Replace($Old, $New))
    Write-Host "Applied: $Description" -ForegroundColor Green
}

function Replace-RegexOnce {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Pattern,
        [Parameter(Mandatory=$true)][string]$Replacement,
        [Parameter(Mandatory=$true)][string]$Description,
        [string]$AlreadyMarker = ""
    )

    $text = Read-Text $Path

    if ($AlreadyMarker -and $text.Contains($AlreadyMarker)) {
        Write-Host "Already applied: $Description" -ForegroundColor DarkGreen
        return
    }

    $regex = [regex]::new(
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    $matches = $regex.Matches($text)
    if ($matches.Count -ne 1) {
        throw "Could not safely apply '$Description'. Expected exactly 1 structural match in '$Path', found $($matches.Count)."
    }

    Backup-Once $Path
    Write-Text $Path ($regex.Replace($text, $Replacement, 1))
    Write-Host "Applied: $Description" -ForegroundColor Green
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor DarkCyan
Write-Host " ByteEngine v0.11-C7A - Explicit Root Motion Modes" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor DarkCyan
Write-Host ""

$modePath = Join-Path $root "Engine\ByteEngine.Core\Animation\RootMotionMode.cs"
$rendererPath = Join-Path $root "Engine\ByteEngine.Core\Graphics\3D\SkeletalMeshRenderer.cs"
$controllerPath = Join-Path $root "Engine\ByteEngine.Core\Animation\AnimationController.cs"
$serializationPath = Join-Path $root "Engine\ByteEngine.Core\Animation\AnimationSerializationRegistrar.cs"

foreach ($path in @($rendererPath, $controllerPath, $serializationPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing expected source file: $path"
    }
}

# ---------------------------------------------------------------------------
# 1. RootMotionMode enum
# ---------------------------------------------------------------------------

$modeSource = @'
namespace ByteEngine.Core.Animation;

/// <summary>
/// Controls how translation authored on an animation's skeleton root is used.
///
/// InPlace keeps the existing ByteEngine behaviour: locomotion travel is
/// removed from the rendered pose and gameplay/CharacterController3D remains
/// authoritative for world movement.
///
/// ApplyHorizontal removes horizontal travel from the rendered pose and applies
/// that X/Z travel to the owning character GameObject.
///
/// ApplyFull removes and applies horizontal plus vertical root translation.
/// </summary>
public enum RootMotionMode
{
    InPlace,
    ApplyHorizontal,
    ApplyFull
}
'@

if (-not (Test-Path -LiteralPath $modePath)) {
    Write-Text $modePath $modeSource
    Write-Host "Created: RootMotionMode.cs" -ForegroundColor Green
}
else {
    $existingMode = Read-Text $modePath
    if (-not $existingMode.Contains("public enum RootMotionMode")) {
        throw "RootMotionMode.cs already exists but does not contain the expected enum."
    }
    Write-Host "Already applied: RootMotionMode.cs" -ForegroundColor DarkGreen
}

# ---------------------------------------------------------------------------
# 2. SkeletalMeshRenderer root-motion state + public API
# ---------------------------------------------------------------------------

Replace-Once `
    -Path $rendererPath `
    -Old @'
    private Vector3 _modelSpaceRootTravel;

    private Vector3[] _basePositions = Array.Empty<Vector3>();
'@ `
    -New @'
    private Vector3 _modelSpaceRootTravel;
    private Vector3 _rootMotionDelta;
    private Vector3 _lastRootMotionTravel;
    private ImportedAnimation? _lastRootMotionAnimation;
    private float _lastRootMotionTime;
    private bool _rootMotionSampleValid;
    private bool _rootMotionStepPending;
    private RootMotionMode _rootMotionMode = RootMotionMode.InPlace;

    private Vector3[] _basePositions = Array.Empty<Vector3>();
'@ `
    -Description "add root-motion runtime tracking fields" `
    -AlreadyMarker "_rootMotionStepPending"

Replace-Once `
    -Path $rendererPath `
    -Old @'
    public float TransitionDuration { get; set; } = 0.15f;

    public string CurrentAnimation =>
'@ `
    -New @'
    public float TransitionDuration { get; set; } = 0.15f;

    /// <summary>
    /// Controls whether extracted animation-root translation is kept in-place
    /// or applied to the owning character in world space.
    /// </summary>
    public RootMotionMode RootMotionMode
    {
        get => _rootMotionMode;
        set
        {
            if (_rootMotionMode == value)
            {
                return;
            }

            _rootMotionMode = value;
            ResetRootMotionTracking();
            _poseDirty = true;
        }
    }

    /// <summary>
    /// World-space root-motion delta applied during the most recent animation
    /// step. Zero for InPlace, paused clips, or frames with no extracted travel.
    /// </summary>
    public Vector3 RootMotionDelta =>
        _rootMotionDelta;

    /*
     * AnimationController sets these internally so only one renderer moves a
     * multi-renderer character while every renderer still receives identical
     * pose compensation.
     */
    internal GameObject? RootMotionTargetOverride { get; set; }
    internal bool RootMotionAuthority { get; set; } = true;

    public string CurrentAnimation =>
'@ `
    -Description "add RootMotionMode and diagnostics API" `
    -AlreadyMarker "public RootMotionMode RootMotionMode"

# Mark a root-motion step when playback time really advances.
Replace-Once `
    -Path $rendererPath `
    -Old @'
            _poseDirty = true;
        }

        if (_poseDirty)
'@ `
    -New @'
            _rootMotionStepPending = true;
            _poseDirty = true;
        }

        if (_poseDirty)
'@ `
    -Description "mark playback frames that can apply root motion" `
    -AlreadyMarker "_rootMotionStepPending = true;"

# Reset tracking when switching clips.
Replace-Once `
    -Path $rendererPath `
    -Old @'
        _currentAnimation = animation;
        _currentTime = 0.0f;
        _currentLoop = requestedLoop;
        Loop = requestedLoop;
        IsPlaying = true;
        _poseDirty = true;

        UpdatePoseAndMeshes();
'@ `
    -New @'
        _currentAnimation = animation;
        _currentTime = 0.0f;
        _currentLoop = requestedLoop;
        Loop = requestedLoop;
        IsPlaying = true;
        _poseDirty = true;
        ResetRootMotionTracking();

        UpdatePoseAndMeshes();
'@ `
    -Description "reset root-motion baseline when changing clips" `
    -AlreadyMarker "ResetRootMotionTracking();`r`n`r`n        UpdatePoseAndMeshes();"

# Reset tracking on Stop().
Replace-Once `
    -Path $rendererPath `
    -Old @'
        _transitionElapsed = 0.0f;
        _activeTransitionDuration = 0.0f;
        _poseDirty = true;

        UpdatePoseAndMeshes();
'@ `
    -New @'
        _transitionElapsed = 0.0f;
        _activeTransitionDuration = 0.0f;
        _poseDirty = true;
        ResetRootMotionTracking();

        UpdatePoseAndMeshes();
'@ `
    -Description "reset root-motion baseline when animation stops"

# Reset tracking when runtime skeleton/model data is rebuilt.
Replace-Once `
    -Path $rendererPath `
    -Old @'
        _rootMotionRanges.Clear();
        _modelSpaceRootTravel =
            Vector3.Zero;
    }
'@ `
    -New @'
        _rootMotionRanges.Clear();
        _modelSpaceRootTravel =
            Vector3.Zero;
        ResetRootMotionTracking();
    }
'@ `
    -Description "reset root-motion tracking when model runtime is rebuilt" `
    -AlreadyMarker "_modelSpaceRootTravel =`r`n            Vector3.Zero;`r`n        ResetRootMotionTracking();"

# Apply extracted delta immediately after computing the corrected pose travel.
Replace-Once `
    -Path $rendererPath `
    -Old @'
        _currentPoseGlobals = globals;
        _currentRootMotionCorrection = rootMotionCorrection;

        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
'@ `
    -New @'
        _currentPoseGlobals = globals;
        _currentRootMotionCorrection = rootMotionCorrection;

        ApplyRootMotionDelta();

        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
'@ `
    -Description "apply extracted root motion to the owning character" `
    -AlreadyMarker "ApplyRootMotionDelta();"

# Replace the old hard-coded horizontal compensation implementation.
$rootMotionPattern = @'
    /// <summary>
    /// Produces an in-place locomotion correction.*?
    private RootMotionRange GetRootMotionRange\(
'@

$rootMotionReplacement = @'
    /// <summary>
    /// Extracts root translation from the fully evaluated imported hierarchy.
    /// The rendered pose is corrected by the same amount that is optionally
    /// applied to the character GameObject, preventing double movement.
    /// </summary>
    private Matrix4x4 BuildModelSpaceRootMotionCorrection()
    {
        _modelSpaceRootTravel =
            Vector3.Zero;

        if (_currentAnimation == null ||
            _rootMotionChainIndices.Length == 0)
        {
            return Matrix4x4.Identity;
        }

        bool includeVertical =
            RootMotionMode == RootMotionMode.ApplyFull;

        Vector3 currentTravel =
            CalculateClipRootTravel(
                _currentAnimation,
                _currentTime,
                includeVertical);

        float blend =
            _previousAnimation == null ||
            _activeTransitionDuration <= 0.000001f
                ? 1.0f
                : Math.Clamp(
                    _transitionElapsed /
                    _activeTransitionDuration,
                    0.0f,
                    1.0f);

        if (_previousAnimation != null &&
            blend < 1.0f)
        {
            Vector3 previousTravel =
                CalculateClipRootTravel(
                    _previousAnimation,
                    _previousTime,
                    includeVertical);

            _modelSpaceRootTravel =
                Vector3.Lerp(
                    previousTravel,
                    currentTravel,
                    blend);
        }
        else
        {
            _modelSpaceRootTravel =
                currentTravel;
        }

        return Matrix4x4.CreateTranslation(
            -_modelSpaceRootTravel.X,
            includeVertical
                ? -_modelSpaceRootTravel.Y
                : 0.0f,
            -_modelSpaceRootTravel.Z);
    }

    private Vector3 CalculateClipRootTravel(
        ImportedAnimation animation,
        float time,
        bool includeVertical)
    {
        if (animation.Duration <= 0.000001f ||
            _rootMotionChainIndices.Length == 0)
        {
            return Vector3.Zero;
        }

        RootMotionRange range =
            GetRootMotionRange(
                animation);

        Vector3 current =
            SampleRootModelPosition(
                animation,
                time);

        Vector3 displacement =
            current -
            range.Start;

        Vector3 result =
            Vector3.Zero;

        Vector3 netHorizontal =
            range.End -
            range.Start;

        netHorizontal.Y =
            0.0f;

        float netLengthSquared =
            netHorizontal.LengthSquared();

        if (netLengthSquared >
            0.0000001f)
        {
            Vector3 direction =
                netHorizontal /
                MathF.Sqrt(
                    netLengthSquared);

            Vector3 horizontalDisplacement =
                displacement;

            horizontalDisplacement.Y =
                0.0f;

            /*
             * Extract progress along the actual locomotion path while retaining
             * perpendicular authored body sway inside the skeletal pose.
             */
            float distanceAlongTravel =
                Vector3.Dot(
                    horizontalDisplacement,
                    direction);

            result =
                direction *
                distanceAlongTravel;
        }

        if (includeVertical)
        {
            result.Y =
                displacement.Y;
        }

        return result;
    }

    private void ApplyRootMotionDelta()
    {
        _rootMotionDelta =
            Vector3.Zero;

        if (_currentAnimation == null)
        {
            ResetRootMotionTracking();
            return;
        }

        bool includeVertical =
            RootMotionMode == RootMotionMode.ApplyFull;

        /*
         * Always maintain a baseline, even in InPlace mode. Switching modes at
         * runtime therefore starts from the current pose instead of teleporting
         * by all root travel accumulated since clip start.
         */
        if (!_rootMotionSampleValid ||
            !ReferenceEquals(
                _lastRootMotionAnimation,
                _currentAnimation))
        {
            StoreRootMotionBaseline();
            _rootMotionStepPending = false;
            return;
        }

        if (!_rootMotionStepPending ||
            RootMotionMode == RootMotionMode.InPlace ||
            !RootMotionAuthority)
        {
            StoreRootMotionBaseline();
            _rootMotionStepPending = false;
            return;
        }

        Vector3 delta;

        /*
         * Looping clips wrap currentTime back to the beginning. Add the tail of
         * the previous loop and the head of the new loop instead of interpreting
         * the wrap as a large backwards movement.
         */
        bool wrapped =
            _currentLoop &&
            _previousAnimation == null &&
            _currentTime + 0.000001f <
            _lastRootMotionTime;

        if (wrapped)
        {
            Vector3 endTravel =
                CalculateClipRootTravel(
                    _currentAnimation,
                    Math.Max(
                        _currentAnimation.Duration,
                        0.0f),
                    includeVertical);

            delta =
                (endTravel -
                 _lastRootMotionTravel) +
                _modelSpaceRootTravel;
        }
        else
        {
            delta =
                _modelSpaceRootTravel -
                _lastRootMotionTravel;
        }

        if (RootMotionMode ==
            RootMotionMode.ApplyHorizontal)
        {
            delta.Y =
                0.0f;
        }

        if (IsFinite(delta) &&
            delta.LengthSquared() >
            0.0000000001f)
        {
            GameObject target =
                ResolveRootMotionTarget();

            Vector3 worldDelta =
                Vector3.Transform(
                    delta,
                    target.Transform.WorldRotation);

            if (IsFinite(worldDelta))
            {
                target.Transform.WorldPosition +=
                    worldDelta;

                _rootMotionDelta =
                    worldDelta;
            }
        }

        StoreRootMotionBaseline();
        _rootMotionStepPending = false;
    }

    private GameObject ResolveRootMotionTarget()
    {
        if (RootMotionTargetOverride != null)
        {
            return RootMotionTargetOverride;
        }

        /*
         * A standalone SkeletalMeshRenderer can still use root motion. Prefer
         * the nearest AnimationController ancestor when one exists; otherwise
         * move the renderer's own GameObject.
         */
        for (GameObject? current = GameObject;
             current != null;
             current = current.Parent)
        {
            if (current.GetComponent<AnimationController>() != null)
            {
                return current;
            }
        }

        return GameObject;
    }

    private void StoreRootMotionBaseline()
    {
        _lastRootMotionAnimation =
            _currentAnimation;

        _lastRootMotionTravel =
            _modelSpaceRootTravel;

        _lastRootMotionTime =
            _currentTime;

        _rootMotionSampleValid =
            _currentAnimation != null;
    }

    private void ResetRootMotionTracking()
    {
        _rootMotionDelta =
            Vector3.Zero;

        _lastRootMotionTravel =
            Vector3.Zero;

        _lastRootMotionAnimation =
            null;

        _lastRootMotionTime =
            0.0f;

        _rootMotionSampleValid =
            false;

        _rootMotionStepPending =
            false;
    }

    private static bool IsFinite(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private RootMotionRange GetRootMotionRange(
'@

Replace-RegexOnce `
    -Path $rendererPath `
    -Pattern $rootMotionPattern `
    -Replacement $rootMotionReplacement `
    -Description "replace hard-coded in-place compensation with explicit root-motion modes" `
    -AlreadyMarker "private void ApplyRootMotionDelta()"

# Reset diagnostics on renderer disposal.
Replace-Once `
    -Path $rendererPath `
    -Old @'
        _runtimeMeshes.Clear();
        _currentPoseGlobals = Array.Empty<Matrix4x4>();
        _currentRootMotionCorrection = Matrix4x4.Identity;
    }
'@ `
    -New @'
        _runtimeMeshes.Clear();
        _currentPoseGlobals = Array.Empty<Matrix4x4>();
        _currentRootMotionCorrection = Matrix4x4.Identity;
        ResetRootMotionTracking();
    }
'@ `
    -Description "reset root-motion state when renderer resources are disposed" `
    -AlreadyMarker "_currentRootMotionCorrection = Matrix4x4.Identity;`r`n        ResetRootMotionTracking();"

# ---------------------------------------------------------------------------
# 3. AnimationController exposes and propagates the authoring mode.
# ---------------------------------------------------------------------------

Replace-Once `
    -Path $controllerPath `
    -Old @'
    public float PlaybackSpeed { get; set; } = 1.0f;

    public LocomotionState State { get; private set; }
'@ `
    -New @'
    public float PlaybackSpeed { get; set; } = 1.0f;

    /// <summary>
    /// Root animation translation policy for all skeletal renderers driven by
    /// this character. InPlace preserves CharacterController3D authority.
    /// </summary>
    public RootMotionMode RootMotionMode { get; set; } =
        RootMotionMode.InPlace;

    public LocomotionState State { get; private set; }
'@ `
    -Description "add AnimationController Root Motion Mode" `
    -AlreadyMarker "public RootMotionMode RootMotionMode"

Replace-Once `
    -Path $controllerPath `
    -Old @'
            renderer.TransitionDuration =
                Math.Max(TransitionDuration, 0.0f);
        }
'@ `
    -New @'
            renderer.TransitionDuration =
                Math.Max(TransitionDuration, 0.0f);

            renderer.RootMotionMode =
                RootMotionMode;
        }
'@ `
    -Description "propagate root-motion mode during controller updates" `
    -AlreadyMarker "renderer.RootMotionMode =`r`n                RootMotionMode;"

Replace-Once `
    -Path $controllerPath `
    -Old @'
                        TransitionDuration =
                            Math.Max(TransitionDuration, 0.0f),
                        Speed =
                            Math.Max(PlaybackSpeed, 0.0f)
                    });
'@ `
    -New @'
                        TransitionDuration =
                            Math.Max(TransitionDuration, 0.0f),
                        Speed =
                            Math.Max(PlaybackSpeed, 0.0f),
                        RootMotionMode =
                            RootMotionMode
                    });
'@ `
    -Description "initialize generated skeletal renderers with controller root-motion mode" `
    -AlreadyMarker "RootMotionMode =`r`n                            RootMotionMode"

Replace-Once `
    -Path $controllerPath `
    -Old @'
    private void AddRenderer(
        SkeletalMeshRenderer renderer)
    {
        if (!_renderers.Contains(renderer))
        {
            _renderers.Add(renderer);
        }
    }
'@ `
    -New @'
    private void AddRenderer(
        SkeletalMeshRenderer renderer)
    {
        if (_renderers.Contains(renderer))
        {
            return;
        }

        /*
         * Every renderer uses the same extraction/correction mode, but only
         * the first renderer is allowed to move the character. This prevents
         * double root motion on characters with multiple skinned meshes.
         */
        renderer.RootMotionMode =
            RootMotionMode;

        renderer.RootMotionTargetOverride =
            GameObject;

        renderer.RootMotionAuthority =
            _renderers.Count == 0;

        _renderers.Add(renderer);
    }
'@ `
    -Description "make one skeletal renderer authoritative for character root motion" `
    -AlreadyMarker "renderer.RootMotionAuthority"

# ---------------------------------------------------------------------------
# 4. Persist both controller and standalone-renderer RootMotionMode.
# ---------------------------------------------------------------------------

Replace-Once `
    -Path $serializationPath `
    -Old @'
                            ["playbackSpeed"] =
                                controller.PlaybackSpeed
'@ `
    -New @'
                            ["playbackSpeed"] =
                                controller.PlaybackSpeed,
                            ["rootMotionMode"] =
                                controller.RootMotionMode.ToString()
'@ `
    -Description "serialize AnimationController root-motion mode" `
    -AlreadyMarker '["rootMotionMode"] =`r`n                                controller.RootMotionMode.ToString()'

Replace-Once `
    -Path $serializationPath `
    -Old @'
                PlaybackSpeed =
                    Float(
                        data,
                        "playbackSpeed",
                        1.0f)
            };
'@ `
    -New @'
                PlaybackSpeed =
                    Float(
                        data,
                        "playbackSpeed",
                        1.0f),
                RootMotionMode =
                    ReadRootMotionMode(
                        data)
            };
'@ `
    -Description "deserialize AnimationController root-motion mode"

Replace-Once `
    -Path $serializationPath `
    -Old @'
                            ["transitionDuration"] =
                                renderer.TransitionDuration
'@ `
    -New @'
                            ["transitionDuration"] =
                                renderer.TransitionDuration,
                            ["rootMotionMode"] =
                                renderer.RootMotionMode.ToString()
'@ `
    -Description "serialize SkeletalMeshRenderer root-motion mode"

Replace-Once `
    -Path $serializationPath `
    -Old @'
                    TransitionDuration =
                        Float(
                            data,
                            "transitionDuration",
                            0.15f)
                };
'@ `
    -New @'
                    TransitionDuration =
                        Float(
                            data,
                            "transitionDuration",
                            0.15f),
                    RootMotionMode =
                        ReadRootMotionMode(
                            data)
                };
'@ `
    -Description "deserialize SkeletalMeshRenderer root-motion mode"

Replace-Once `
    -Path $serializationPath `
    -Old @'
    private static float Float(
        ComponentData data,
        string key,
        float fallback) =>
        data.Properties[key]?
            .GetValue<float>() ??
        fallback;
}
'@ `
    -New @'
    private static float Float(
        ComponentData data,
        string key,
        float fallback) =>
        data.Properties[key]?
            .GetValue<float>() ??
        fallback;

    private static RootMotionMode ReadRootMotionMode(
        ComponentData data)
    {
        string? value =
            data.Properties["rootMotionMode"]?
                .GetValue<string>();

        return Enum.TryParse(
                value,
                ignoreCase: true,
                out RootMotionMode mode)
            ? mode
            : RootMotionMode.InPlace;
    }
}
'@ `
    -Description "add backward-compatible root-motion enum deserialization" `
    -AlreadyMarker "private static RootMotionMode ReadRootMotionMode("

Write-Host ""
Write-Host "==================================================" -ForegroundColor DarkGreen
Write-Host " C7A ROOT MOTION PATCH APPLIED" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor DarkGreen
Write-Host ""
Write-Host "Default remains InPlace, so existing characters keep current behaviour." -ForegroundColor Gray
Write-Host ""
Write-Host "Now run the standard ByteEngine build/Dist refresh:" -ForegroundColor Yellow
Write-Host "powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1" -ForegroundColor Cyan
Write-Host ""

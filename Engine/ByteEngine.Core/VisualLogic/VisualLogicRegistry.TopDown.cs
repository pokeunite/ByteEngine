using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterTopDown(VisualLogicRegistry r)
    {
        RegisterDash(r);
        const string controller = "Top Down / Controller", camera = "Top Down / Camera";
        var target = ObjectArg();
        Add(r, "topDown.setInput", controller, "Enable / disable top-down input", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownController3D>() is {} control) control.InputEnabled = B(i,c,"enabled",true); }, target, BoolArg("enabled","Input enabled",true));
        var facing = TextArg("mode","Facing mode","Cursor") with { Choices = ["Cursor","MovementUntilAiming","Movement","Independent"] };
        Add(r, "topDown.setFacing", controller, "Set top-down facing mode", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownController3D>() is {} control && Enum.TryParse<TopDownFacing>(S(i,c,"mode","Cursor"), true, out var mode)) control.Facing = mode; }, target, facing);
        Add(r, "topDown.setAiming", controller, "Set top-down aiming request", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownController3D>() is {} control) control.AimRequested = B(i,c,"enabled",true); }, target, BoolArg("enabled","Aiming",true));
        Test(r, "topDown.isFacingCursor", controller, "Top-down character is facing the cursor", (i,c) =>
            ResolveObjectTarget(i,c)?.GetComponent<TopDownController3D>() is { Enabled:true, IsFacingCursor:true }, target);
        Add(r, "topDown.setCameraRelative", controller, "Set camera-relative movement", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownController3D>() is {} control) control.CameraRelativeMovement = B(i,c,"enabled",true); }, target, BoolArg("enabled","Camera-relative movement",true));
        Test(r, "topDown.hasCursorAim", controller, "Top-down cursor has a valid world point", (i,c) => ResolveObjectTarget(i,c)?.GetComponent<TopDownController3D>() is { Enabled:true, HasCursorAim:true }, target);
        Add(r, "topDown.storeAimPoint", controller, "Store cursor aim world position", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownController3D>() is { HasCursorAim:true } control) c.Self.Variables.Set(S(i,c,"variable","AimPoint"),VariableValue.FromVector3(control.AimWorldPoint)); }, target, TextArg("variable","Output variable","AimPoint"));
        Add(r, "animation.setTurnInPlace", "Animation", "Enable / disable turn-in-place animations", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<AnimationController>() is {} animation) animation.TurnInPlaceEnabled = B(i,c,"enabled",true); }, target, BoolArg("enabled","Enabled",true));
        Test(r, "animation.isTurning", "Animation", "Character is playing a turn animation", (i,c) =>
            ResolveObjectTarget(i,c)?.GetComponent<AnimationController>() is {} animation && animation.CurrentTurn.Length > 0, target);
        Add(r, "characterDebug.setVisible", "Character Debug", "Show / hide movement debug HUD", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<CharacterMotionDebugHud3D>() is {} hud) hud.Visible = B(i,c,"enabled",true); }, target, BoolArg("enabled","Visible",true));
        Add(r, "topDownCamera.setTarget", camera, "Set top-down camera target", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownCamera3D>() is {} rig) { rig.TargetId = Other(i,c,"followTarget")?.Id ?? Guid.Empty; } }, target, ObjectArg("followTarget","Follow object"));
        Add(r, "topDownCamera.setLookAhead", camera, "Enable / disable cursor camera look-ahead", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownCamera3D>() is {} rig) { rig.CursorLookAhead = B(i,c,"enabled",true); rig.LookAheadDistance = (float)Math.Max(0,N(i,c,"distance",4)); } }, target, BoolArg("enabled","Look-ahead enabled",true), NumberArg("distance","Maximum look-ahead distance",4));
        Add(r, "topDownCamera.setZoom", camera, "Set top-down camera zoom", (i,c) =>
        { if (ResolveObjectTarget(i,c)?.GetComponent<TopDownCamera3D>() is {} rig) { float zoom=(float)N(i,c,"zoom",22); if(rig.Orthographic)rig.OrthographicWidth=zoom;else rig.Distance=zoom; } }, target, NumberArg("zoom","View width / perspective distance",22));
        Add(r, "topDownCamera.snap", camera, "Snap top-down camera to target", (i,c) => ResolveObjectTarget(i,c)?.GetComponent<TopDownCamera3D>()?.SnapToTarget(), target);
        Add(r, "topDownCamera.isometric", camera, "Use true isometric view", (i,c) => ResolveObjectTarget(i,c)?.GetComponent<TopDownCamera3D>()?.SetIsometricView(), target);
    }
}

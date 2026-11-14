using System.Numerics;
using System.Reflection;

using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Classification;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed partial class EventWorkspacePanel
{
    private void DrawInstructionArguments(
        VisualInstruction instruction,
        EditorState? state)
    {
        var pluginArguments = _registry.GetArguments(instruction.Id);
        if (instruction.Id.StartsWith("vfx.", StringComparison.Ordinal))
        {
            if (instruction.Id is "vfx.spawn" or "vfx.impact" or "vfx.spawnOnObject" or "vfx.rayImpact")
            {
                DrawMaterialAssetArgument(instruction, "effect", "VFX Effect", AssetType.VfxEffect);
                if(instruction.Id=="vfx.spawnOnObject")
                {
                    DrawObjectTargetArgument(instruction,"object","Source Object",state);
                    DrawValueArgument(instruction,"socket","Socket Name (optional)",VariableType.String,EventValue.String(""),state,false);
                    DrawValueArgument(instruction,"follow","Follow Object / Socket",VariableType.Boolean,EventValue.Boolean(false),state,false);
                }
                else if(instruction.Id!="vfx.rayImpact")
                    DrawValueArgument(instruction, "position", "World Position", VariableType.Vector3,
                        EventValue.Vector3(Vector3.Zero), state, false);
                if(instruction.Id=="vfx.impact")
                    DrawValueArgument(instruction,"normal","Hit Normal (world)",VariableType.Vector3,EventValue.Vector3(Vector3.UnitY),state,false);
                if(instruction.Id=="vfx.rayImpact") ImGui.TextWrapped("Use after Cast Ray in the same event execution. Spawns at its hit point, facing the hit normal. A miss does nothing.");
                DrawValueArgument(instruction, "size", "Size Multiplier", VariableType.Number,
                    EventValue.Number(1), state, false);
                return;
            }
            DrawObjectTargetArgument(instruction, "target", "VFX Object", state);
            foreach (var argument in pluginArguments)
                DrawValueArgument(instruction, argument.Name, argument.DisplayName,
                    argument.DefaultValue.Type, argument.CreateDefault(), state, false);
            return;
        }
        if (pluginArguments.Count > 0)
        {
            foreach (var argument in pluginArguments)
                DrawValueArgument(instruction, argument.Name, argument.DisplayName,
                    argument.DefaultValue.Type, argument.CreateDefault(), state, false);
            return;
        }
        switch (instruction.Id)
        {
            case "flow.branch":
                DrawValueArgument(
                    instruction,
                    "condition",
                    "Condition",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        false),
                    state,
                    false);

                ImGui.Separator();

                ImGui.TextColored(
                    TrueExecutionWireColor,
                    "TRUE output");

                ImGui.TextColored(
                    FalseExecutionWireColor,
                    "FALSE output");
                break;

            case "time.startTimer":
                DrawValueArgument(instruction, "name", "Timer Name", VariableType.String,
                    EventValue.String("Timer"), state, false);
                DrawValueArgument(instruction, "duration", "Duration (seconds)", VariableType.Number,
                    EventValue.Number(1.0), state, false);
                break;
            case "time.stopTimer":
            case "time.timerFinished":
            case "time.timerRunning":
                DrawValueArgument(instruction, "name", "Timer Name", VariableType.String,
                    EventValue.String("Timer"), state, false);
                break;

            case "input.mouseHeld":
            case "input.mousePressed":
            case "input.mouseReleased":
                DrawMouseButtonArgument(
                    instruction,
                    "button",
                    "Mouse Button",
                    MouseButton.Left);
                break;

            case "logic.and":
                ImGui.TextDisabled(
                    "TRUE when every connected input is TRUE.");

                ImGui.TextDisabled(
                    "Connect Condition outputs to the left pin.");
                break;

            case "logic.or":
                ImGui.TextDisabled(
                    "TRUE when any connected input is TRUE.");

                ImGui.TextDisabled(
                    "Connect Condition outputs to the left pin.");
                break;

            case "input.keyHeld":
            case "input.keyPressed":
            case "input.keyReleased":
                DrawKeyArgument(
                    instruction,
                    "key",
                    "Key",
                    Key.W);
                break;

            case "input.actionHeld":
            case "input.actionPressed":
            case "input.actionReleased":
                DrawInputActionArgument(instruction, "action", "Input Action", "Jump");
                break;

            case "input.axisGreater":
            case "input.axisLess":
            case "input.vectorLengthGreater":
                DrawInputActionArgument(instruction, "action", "Input Action", "Move");
                DrawValueArgument(instruction, "value", "Value", VariableType.Number,
                    EventValue.Number(instruction.Id == "input.axisLess" ? -.5 : .5), state, false);
                break;

            case "physics.lineTrace":
            case "physics.lineTraceHitsAnything":
                DrawLineTraceArguments(
                    instruction,
                    state);
                break;

            case "physics.castRayToCursor":
            case "physics.castRayInDirection":
            case "physics.castRay":
            case "physics.rayHitsAnything":
                DrawRaycastArguments(
                    instruction,
                    state);
                break;
            case "physics.lastRayHit":
            case "physics.lastRayMissed":
                ImGui.TextDisabled("Reads the latest raycast in this Event Sheet update.");
                break;
            case "physics.lastRayHitObject":
                DrawObjectTargetArgument(instruction, "target", "Object To Compare", state);
                break;
            case "physics.saveRayHit":
                DrawValueArgument(instruction, "prefix", "Self Variable Prefix", VariableType.String,
                    EventValue.String("Ray"), state, false);
                ImGui.TextDisabled("Writes Hit, ObjectId, Point, Normal, and Distance variables.");
                break;
            case "scene.load":
                DrawValueArgument(instruction,"path","Scene Path",VariableType.String,EventValue.String("Scenes/Workshop.bytescene"),state,false);break;
            case "ui.setText":
            case "ui.setTextFromHealth":
            case "ui.setButtonLabel":
            case "ui.setBarValue":
            case "ui.setBarMaximum":
            case "ui.setBarFromHealth":
            case "ui.show":
            case "ui.hide":
            case "ui.setButtonEnabled":
            case "ui.buttonClicked":
            case "ui.buttonHovered":
            case "ui.isVisible":
            case "ui.barBelow":
            case "ui.barAbove":
            case "ui.setImage":
            case "ui.focus":
            case "ui.buttonFocused":
            case "ui.buttonEnabled":
            case "ui.setLanguage":
            case "ui.languageIs":
            case "ui.setTextKey":
            case "ui.setLabelKey":
            case "ui.playAnimation":
            case "ui.stopAnimation":
            case "ui.animationPlaying":
                DrawUiArguments(instruction, state);
                break;
            case "material.setMaterial":
            case "material.setBaseColor":
            case "material.setMetallic":
            case "material.setRoughness":
            case "material.setEmissionColor":
            case "material.setEmissionIntensity":
            case "material.setTexture":
            case "material.resetOverrides":
                DrawMaterialActionArguments(instruction, state);
                break;
            case "combat.canFire":
            case "combat.fireWeapon":
            case "health.isDead":
            case "enemyAI.isIdle":
            case "waves.isIdle":
            case "waves.isRunning":
            case "waves.isSpawning":
            case "waves.isWaitingForClear":
            case "waves.isIntermission":
            case "waves.isCompleted":
            case "waves.isFailed":
            case "waves.waveStarted":
            case "waves.waveCleared":
            case "waves.completedThisFrame":
            case "waves.failedThisFrame":
            case "waves.noEnemiesRemain":
            case "waves.start":
            case "waves.stop":
            case "waves.restart":
            case "enemyAI.isChasing":
            case "enemyAI.isAttacking":
            case "enemyAI.attackFired":
            case "projectile.fire":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                break;
            case "health.percentAtMost":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawValueArgument(instruction, "value", "Health Fraction (0-1)", VariableType.Number,
                    EventValue.Number(.25), state, false);
                break;
            case "health.damage":
            case "health.heal":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawValueArgument(instruction, "amount", "Amount", VariableType.Number,
                    EventValue.Number(20), state, false);
                break;
            case "combat.damageLastRayHit":
                DrawValueArgument(instruction, "amount", "Damage", VariableType.Number,
                    EventValue.Number(20), state, false);
                break;

            case "attachment.attachToSocket":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                DrawObjectTargetArgument(instruction, "parent", "Parent", state);
                DrawAttachmentSocketArgument(instruction, state);
                DrawAttachmentRuleArgument(instruction, "locationRule", "Location Rule", "SnapToTarget");
                DrawAttachmentRuleArgument(instruction, "rotationRule", "Rotation Rule", "SnapToTarget");
                DrawAttachmentRuleArgument(instruction, "scaleRule", "Scale Rule", "KeepRelative");
                if (ImGui.TreeNode($"Offsets (optional)##{instruction.InstanceId}"))
                {
                    DrawValueArgument(instruction, "positionOffset", "Position Offset", VariableType.Vector3,
                        EventValue.Vector3(Vector3.Zero), state, false);
                    DrawValueArgument(instruction, "rotationOffset", "Rotation Offset", VariableType.Vector3,
                        EventValue.Vector3(Vector3.Zero), state, false);
                    DrawValueArgument(instruction, "scaleMultiplier", "Scale Multiplier", VariableType.Vector3,
                        EventValue.Vector3(Vector3.One), state, false);
                    ImGui.TreePop();
                }
                break;
            case "attachment.detach":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                DrawValueArgument(instruction, "keepWorld", "Keep World", VariableType.Boolean, EventValue.Boolean(true), state, false);
                break;
            case "attachment.isAttached":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                break;
            case "attachment.isAttachedToSocket":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                DrawObjectTargetArgument(instruction, "parent", "Parent", state);
                DrawAttachmentSocketArgument(instruction, state);
                break;
            case "animation.eventFired":
                DrawAnimationSignalCondition(
                    instruction,
                    state,
                    "event",
                    "Event",
                    includePayload: true);
                break;

            case "animation.windowEntered":
            case "animation.windowExited":
                DrawAnimationSignalCondition(
                    instruction,
                    state,
                    "window",
                    "Window",
                    includePayload: true);
                break;

            case "animation.windowActive":
                DrawAnimationSignalCondition(
                    instruction,
                    state,
                    "window",
                    "Window",
                    includePayload: false);
                break;
            case "animation.pause":
            case "animation.resume":
            case "animation.stop":
            case "animation.isPlaying":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                break;

            case "animation.play":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawAnimationClipArgument(
                    instruction,
                    "clip",
                    "Clip",
                    state);

                DrawValueArgument(
                    instruction,
                    "loop",
                    "Loop",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "animation.playAction":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawAnimationClipArgument(
                    instruction,
                    "clip",
                    "Action Clip",
                    state);

                DrawValueArgument(
                    instruction,
                    "retrigger",
                    "Retrigger Same Action",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        false),
                    state,
                    false);

                DrawValueArgument(
                    instruction,
                    "interruptCurrent",
                    "Interrupt Current Action",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "animation.playNamedAction":
                DrawObjectTargetArgument(instruction,"target","Target Object",state); DrawAnimationActionArgument(instruction,state);
                DrawValueArgument(instruction,"retrigger","Retrigger",VariableType.Boolean,EventValue.Boolean(false),state,false);
                DrawValueArgument(instruction,"queueIfBlocked","Queue If Blocked",VariableType.Boolean,EventValue.Boolean(false),state,false);
                DrawValueArgument(instruction,"forceInterrupt","Force Interrupt",VariableType.Boolean,EventValue.Boolean(false),state,false); break;
            case "animation.queueNamedAction":
            case "animation.currentActionIs":
                DrawObjectTargetArgument(instruction,"target","Target Object",state); DrawAnimationActionArgument(instruction,state); break;
            case "animation.queueCombo":
            case "animation.cancelAction":
            case "animation.actionPlaying":
                DrawObjectTargetArgument(instruction,"target","Target Object",state); break;
            case "animation.triggerAction":
                DrawObjectTargetArgument(instruction, "target", "Target Object", state);
                DrawAnimationActionArgument(instruction, state, "Start Action");
                break;
            case "animation.setSpeed":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "speed",
                    "Speed",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "animation.setTransitionDuration":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "duration",
                    "Transition Duration",
                    VariableType.Number,
                    EventValue.Number(
                        0.15),
                    state,
                    false);
                break;

            case "animation.setLayerWeight":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                DrawAnimationLayerArgument(
                    instruction,
                    state);
                DrawValueArgument(
                    instruction,
                    "weight",
                    "Weight (0 - 1)",
                    VariableType.Number,
                    EventValue.Number(1.0),
                    state,
                    false);
                break;

            case "animation.enableLayer":
            case "animation.disableLayer":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                DrawAnimationLayerArgument(
                    instruction,
                    state);
                break;

            case "animation.currentClipIs":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawAnimationClipArgument(
                    instruction,
                    "clip",
                    "Clip",
                    state);
                break;

            case "animation.currentStateIs":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "state",
                    "State",
                    VariableType.String,
                    EventValue.String(
                        "Idle"),
                    state,
                    false);
                break;

            case "object.exists":
            case "object.isActive":
            case "object.destroy":
            case "audio.play":
            case "audio.pause":
            case "audio.stop":
            case "audio.isPlaying":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                break;

            case "audio.playClip":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                DrawAudioClipArgument(
                    instruction);
                break;
            case "audio.setVolume":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "volume",
                    "Volume",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "audio.setPitch":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "pitch",
                    "Pitch",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "audio.setLoop":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "loop",
                    "Loop",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "object.hasTag":
            case "object.doesNotHaveTag":
            case "object.addTag":
            case "object.removeTag":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawTagArgument(instruction, "tag", "Tag", "Enemy");
                break;

            case "object.withTagExists":
                DrawTagArgument(instruction, "tag", "Tag", "Enemy");
                break;

            case "object.isOnLayer":
            case "object.setLayer":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawLayerArgument(instruction, "layer", "Layer", "Default");
                break;

            case "object.setActive":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "active",
                    "Active",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "object.spawnEmpty":
                DrawValueArgument(
                    instruction,
                    "name",
                    "Object Name",
                    VariableType.String,
                    EventValue.String(
                        "GameObject"),
                    state,
                    false);

                DrawValueArgument(
                    instruction,
                    "position",
                    "Spawn Position",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "object.spawnBlueprint":
                DrawBlueprintAssetArgument(
                    instruction,
                    "blueprint",
                    "Blueprint Asset");

                DrawValueArgument(
                    instruction,
                    "position",
                    "Spawn Position",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "character.moveForward":
            case "character.moveRight":
                DrawValueArgument(
                    instruction,
                    "amount",
                    "Amount",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "character.setVelocity":
                DrawValueArgument(
                    instruction,
                    "velocity",
                    "Velocity",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "character.addImpulse":
                DrawValueArgument(
                    instruction,
                    "impulse",
                    "Impulse",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "transform.setPosition":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "position",
                    "Position",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "transform.move":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Amount",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "transform.setX":
            case "transform.setY":
            case "transform.setZ":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "value",
                    "Value",
                    VariableType.Number,
                    EventValue.Number(0.0),
                    state,
                    false);
                break;

            case "transform.setRotation":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "rotation",
                    "Rotation (Degrees)",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "transform.rotateBy":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Degrees",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "transform.setScale":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "scale",
                    "Scale",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.One),
                    state,
                    false);
                break;

            case "variable.compare":
                DrawValueArgument(
                    instruction,
                    "left",
                    "Left",
                    null,
                    EventValue.Number(
                        0.0),
                    state,
                    false);

                DrawComparisonOperator(
                    instruction);

                DrawValueArgument(
                    instruction,
                    "right",
                    "Right",
                    null,
                    EventValue.Number(
                        0.0),
                    state,
                    false);
                break;

            case "variable.set":
                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    null,
                    state);

                VariableType? selectedType =
                    TryGetSelectedTargetVariableType(instruction, state);
                DrawValueArgument(
                    instruction,
                    "value",
                    "Value",
                    selectedType,
                    selectedType == VariableType.Boolean
                        ? EventValue.Boolean(false)
                        : EventValue.Number(0.0),
                    state,
                    false);
                break;

            case "variable.add":
            case "variable.subtract":
                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    VariableType.Number,
                    state);

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Amount",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "variable.toggle":
                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    VariableType.Boolean,
                    state);
                break;
        }
    }

    // ========================================================
    // RAYCAST AUTHORING
    // ========================================================

    private void DrawLineTraceArguments(
        VisualInstruction instruction,
        EditorState? state)
    {
        string startMode =
            DrawLineTraceModeArgument(
                instruction,
                "startMode",
                "Start From",
                "WorldPosition",
                new[]
                {
                    ("Object", "Object"),
                    ("WorldPosition", "World Position")
                });

        if (startMode.Equals(
                "Object",
                StringComparison.OrdinalIgnoreCase))
        {
            DrawObjectTargetArgument(
                instruction,
                "startObject",
                "Start Object",
                state);

            ImGui.TextDisabled(
                "Uses the object's first collider centre; falls back to its transform position. Select a muzzle Empty for an exact origin.");
        }
        else
        {
            DrawValueArgument(
                instruction,
                "start",
                "Start World Position",
                VariableType.Vector3,
                EventValue.Vector3(Vector3.Zero),
                state,
                false);
        }

        ImGui.Spacing();

        string endMode =
            DrawLineTraceModeArgument(
                instruction,
                "endMode",
                "End At",
                "WorldPosition",
                new[]
                {
                    ("Object", "Object"),
                    ("Direction", "Direction + Distance"),
                    ("Cursor", "Mouse Cursor"),
                    ("WorldPosition", "World Position")
                });

        if (endMode.Equals(
                "Object",
                StringComparison.OrdinalIgnoreCase))
        {
            DrawObjectTargetArgument(
                instruction,
                "endObject",
                "Target Object",
                state);

            ImGui.TextDisabled(
                "Aims at the target collider centre; falls back to its transform position.");
        }
        else if (endMode.Equals(
                     "Direction",
                     StringComparison.OrdinalIgnoreCase))
        {
            string directionMode =
                DrawLineTraceDirectionArgument(
                    instruction);

            if (directionMode.Equals(
                    "CustomLocal",
                    StringComparison.OrdinalIgnoreCase))
            {
                DrawValueArgument(
                    instruction,
                    "traceDirection",
                    "Custom Local Direction",
                    VariableType.Vector3,
                    EventValue.Vector3(new Vector3(0f, 0f, -1f)),
                    state,
                    false);
            }
            else if (directionMode.Equals(
                         "CustomWorld",
                         StringComparison.OrdinalIgnoreCase))
            {
                DrawValueArgument(
                    instruction,
                    "traceDirection",
                    "Custom World Direction",
                    VariableType.Vector3,
                    EventValue.Vector3(new Vector3(0f, 0f, -1f)),
                    state,
                    false);
            }

            DrawCompactRayValueArgument(
                instruction,
                "distance",
                "Trace Distance",
                VariableType.Number,
                EventValue.Number(100),
                state);
        }
        else if (endMode.Equals(
                     "Cursor",
                     StringComparison.OrdinalIgnoreCase))
        {
            DrawCompactRayValueArgument(
                instruction,
                "distance",
                "Maximum Distance",
                VariableType.Number,
                EventValue.Number(100),
                state);

            ImGui.TextDisabled(
                "The cursor chooses the aim point; Maximum Distance clamps the actual Start -> End trace.");
        }
        else
        {
            DrawValueArgument(
                instruction,
                "end",
                "End World Position",
                VariableType.Vector3,
                EventValue.Vector3(new Vector3(0f, 0f, -10f)),
                state,
                false);
        }

        DrawRayCollisionLayerArgument(
            instruction,
            state);

        DrawCompactRayValueArgument(
            instruction,
            "ignoreSelf",
            "Ignore Start Hierarchy",
            VariableType.Boolean,
            EventValue.Boolean(true),
            state);

        DrawCompactRayValueArgument(
            instruction,
            "includeTriggers",
            "Include Triggers",
            VariableType.Boolean,
            EventValue.Boolean(false),
            state);

        DrawCompactRayValueArgument(
            instruction,
            "drawDebug",
            "Draw Debug Trace",
            VariableType.Boolean,
            EventValue.Boolean(false),
            state);

        if (ImGui.TreeNode(
                $"Advanced Trace Settings##{instruction.InstanceId}"))
        {
            DrawValueArgument(
                instruction,
                "debugDuration",
                "Debug Duration (seconds)",
                VariableType.Number,
                EventValue.Number(.25),
                state,
                false);

            ImGui.TreePop();
        }

        string summary =
            endMode switch
            {
                "Object" => "Start -> target object",
                "Direction" => "Start -> direction x distance",
                "Cursor" => "Start -> collider under cursor (distance limited)",
                _ => "Start -> world position"
            };

        ImGui.TextDisabled(summary);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "ByteEngine resolves these simple inputs to one finite Start -> End segment, then performs one physics line trace. " +
                "The physics primitive stays universal; object, direction and cursor modes are only convenient ways to resolve End.");
        }
    }

    private string DrawLineTraceModeArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        string legacyFallback,
        IReadOnlyList<(string Mode, string Label)> options)
    {
        string current =
            instruction.Arguments.TryGetValue(
                    argumentName,
                    out EventValue? value) &&
                value != null &&
                value.Kind == EventValueKind.Constant &&
                value.Constant.Type == VariableType.String &&
                !string.IsNullOrWhiteSpace(value.Constant.String)
                    ? value.Constant.String
                    : legacyFallback;

        string preview =
            options.FirstOrDefault(
                option =>
                    option.Mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase)).Label
            ?? current;

        ImGui.PushID(
            $"LineTraceMode:{argumentName}");

        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(-1.0f);

        if (ImGui.BeginCombo(
                "##Mode",
                preview))
        {
            foreach ((string mode, string optionLabel) in options)
            {
                bool selected =
                    mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        optionLabel,
                        selected))
                {
                    RecordHistory(
                        $"Change Line Trace {label}");

                    instruction.Arguments[argumentName] =
                        EventValue.String(mode);

                    current =
                        mode;

                    _dirty =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.PopID();

        return current;
    }

    private string DrawLineTraceDirectionArgument(
        VisualInstruction instruction)
    {
        string current =
            instruction.Arguments.TryGetValue(
                    "traceDirectionMode",
                    out EventValue? value) &&
                value != null &&
                value.Kind == EventValueKind.Constant &&
                value.Constant.Type == VariableType.String &&
                !string.IsNullOrWhiteSpace(value.Constant.String)
                    ? value.Constant.String
                    : "Forward";

        (string Mode, string Label)[] options =
        {
            ("Forward", "Forward"),
            ("Back", "Back"),
            ("Right", "Right"),
            ("Left", "Left"),
            ("Up", "Up"),
            ("Down", "Down"),
            ("CustomLocal", "Custom Local Direction"),
            ("CustomWorld", "Custom World Direction")
        };

        string preview =
            options.FirstOrDefault(
                option =>
                    option.Mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase)).Label
            ?? current;

        ImGui.PushID("LineTraceDirection");
        ImGui.TextDisabled("Direction");
        ImGui.SetNextItemWidth(-1.0f);

        if (ImGui.BeginCombo(
                "##Direction",
                preview))
        {
            foreach ((string mode, string optionLabel) in options)
            {
                bool selected =
                    mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        optionLabel,
                        selected))
                {
                    RecordHistory("Change Line Trace Direction");
                    instruction.Arguments["traceDirectionMode"] =
                        EventValue.String(mode);
                    current = mode;
                    _dirty = true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.PopID();
        return current;
    }

    private void DrawRaycastArguments(
        VisualInstruction instruction,
        EditorState? state)
    {
        DrawObjectTargetArgument(
            instruction,
            "source",
            "Ray Origin Object",
            state);

        DrawRayMuzzlePointArgument(
            instruction,
            state);

        bool cursorAction = instruction.Id.Equals(
            "physics.castRayToCursor",
            StringComparison.OrdinalIgnoreCase);
        bool directionAction = instruction.Id.Equals(
            "physics.castRayInDirection",
            StringComparison.OrdinalIgnoreCase);

        string aimMode;
        if (cursorAction)
        {
            aimMode = "TopDownCursor";
            ImGui.TextWrapped("Shoots from the origin object or muzzle to the collider directly under the mouse cursor.");
        }
        else if (directionAction)
        {
            aimMode = "MuzzleDirection";
            ImGui.TextWrapped("Shoots from the origin object or muzzle in the selected direction.");
        }
        else
        {
            aimMode = DrawRayAimModeArgument(instruction);
            if (aimMode == "TopDownCursor") DrawTopDownAimStyleArgument(instruction);
        }
        string directionMode = aimMode == "MuzzleDirection"
            ? DrawRayDirectionModeArgument(instruction) : string.Empty;

        if (string.Equals(
                directionMode,
                "CustomLocal",
                StringComparison.OrdinalIgnoreCase))
        {
            DrawValueArgument(
                instruction,
                "direction",
                "Custom Local Direction",
                VariableType.Vector3,
                EventValue.Vector3(
                    new Vector3(
                        0f,
                        0f,
                        -1f)),
                state,
                false);
        }
        else if (string.Equals(
                     directionMode,
                     "CustomWorld",
                     StringComparison.OrdinalIgnoreCase))
        {
            DrawValueArgument(
                instruction,
                "direction",
                "Custom World Direction",
                VariableType.Vector3,
                EventValue.Vector3(
                    new Vector3(
                        0f,
                        0f,
                        -1f)),
                state,
                false);
        }

        DrawCompactRayValueArgument(instruction, "distance", "Maximum Distance",
            VariableType.Number, EventValue.Number(100), state);
        DrawRayCollisionLayerArgument(instruction, state);
        DrawCompactRayValueArgument(instruction, "includeTriggers", "Include Triggers",
            VariableType.Boolean, EventValue.Boolean(false), state);
        DrawCompactRayValueArgument(instruction, "drawDebug", "Draw Debug Ray",
            VariableType.Boolean, EventValue.Boolean(false), state);

        if (ImGui.TreeNode($"Advanced Ray Settings##{instruction.InstanceId}"))
        {
            DrawValueArgument(instruction, "originOffset", "Muzzle Local Offset",
                VariableType.Vector3, EventValue.Vector3(Vector3.Zero), state, false);
            DrawValueArgument(instruction, "debugDuration", "Debug Duration (seconds)",
                VariableType.Number, EventValue.Number(.25), state, false);
            ImGui.TreePop();
        }

        ImGui.TextDisabled(cursorAction
            ? "Origin object / muzzle -> collider under cursor"
            : directionAction
                ? "Origin object / muzzle -> selected direction"
                : "Camera target -> muzzle hit");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(cursorAction
                ? "The visible cursor selects a collider. The ray starts at the chosen object or muzzle and ends at that exact hit point."
                : directionAction
                    ? "The ray starts at the chosen object or muzzle and follows the selected local or world direction."
                    : "Camera selects the aim point. The muzzle ray determines the real hit. Cyan = camera; red = hit; green = miss.");
    }

    private void DrawCompactRayValueArgument(VisualInstruction instruction, string argumentName,
        string label, VariableType type, EventValue fallback, EditorState? state)
    {
        if (!instruction.Arguments.TryGetValue(argumentName, out EventValue? value) || value == null)
            instruction.Arguments[argumentName] = value = fallback;
        if (value.Kind != EventValueKind.Constant || value.Constant.Type != type)
        {
            DrawValueArgument(instruction, argumentName, label, type, fallback, state, false);
            return;
        }

        ImGui.PushID(argumentName);
        if (type == VariableType.Boolean)
        {
            bool checkedValue = value.Constant.Boolean;
            if (ImGui.Checkbox(label, ref checkedValue))
            {
                instruction.Arguments[argumentName] = EventValue.Boolean(checkedValue);
                _dirty = true;
            }
            ImGui.SameLine();
        }
        else
        {
            ImGui.TextDisabled(label);
            ImGui.SameLine();
        }

        if (ImGui.SmallButton("Reference"))
        {
            RecordHistory("Use Ray Value Reference");
            instruction.Arguments[argumentName] = new EventValue { Kind = EventValueKind.Reference };
            _dirty = true;
        }

        if (type == VariableType.Number)
        {
            float number = (float)value.Constant.Number;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.DragFloat("##Value", ref number, .1f, .01f, 100000f))
            {
                instruction.Arguments[argumentName] = EventValue.Number(MathF.Max(.01f, number));
                _dirty = true;
            }
        }
        ImGui.PopID();
    }

    private string DrawRayAimModeArgument(VisualInstruction instruction)
    {
        string current = instruction.Arguments.TryGetValue("aimMode", out EventValue? value) &&
            value.Kind == EventValueKind.Constant && value.Constant.Type == VariableType.String
                ? value.Constant.String : "MuzzleDirection";
        (string Mode, string Label)[] options =
        [
            ("TopDownCursor", "Top Down Cursor"),
            ("ThirdPersonCrosshair", "Third Person Crosshair"),
            ("FirstPersonCrosshair", "First Person Crosshair"),
            ("MuzzleDirection", "Muzzle Direction")
        ];
        string preview = options.FirstOrDefault(item => item.Mode == current).Label ?? "Muzzle Direction";
        ImGui.PushID("RayAimMode");
        ImGui.TextDisabled("Aim Mode");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##AimMode", preview))
        {
            foreach ((string mode, string label) in options)
            {
                bool selected = current == mode;
                if (ImGui.Selectable(label, selected))
                {
                    RecordHistory("Change Ray Aim Mode");
                    instruction.Arguments["aimMode"] = EventValue.String(mode);
                    current = mode;
                    _dirty = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
        return current;
    }

    private void DrawTopDownAimStyleArgument(VisualInstruction instruction)
    {
        string current = instruction.Arguments.TryGetValue("topDownAimStyle", out EventValue? value) &&
            value.Kind == EventValueKind.Constant && value.Constant.Type == VariableType.String
                ? value.Constant.String : "Exact3D";
        ImGui.PushID("TopDownAimStyle");
        ImGui.TextDisabled("Top Down Aim Style");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##AimStyle", current == "Planar" ? "Horizontal / Planar" : "Exact 3D Target"))
        {
            foreach ((string mode, string label) in new[]
                     { ("Exact3D", "Exact 3D Target"), ("Planar", "Horizontal / Planar") })
            {
                bool selected = current == mode;
                if (ImGui.Selectable(label, selected))
                {
                    RecordHistory("Change Top Down Aim Style");
                    instruction.Arguments["topDownAimStyle"] = EventValue.String(mode);
                    current = mode;
                    _dirty = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
    }


    private void DrawRayCollisionLayerArgument(VisualInstruction instruction, EditorState? state)
    {
        ClassificationSettings? settings = EditorProjectContext.Active?.Project.Classification;
        if (settings == null ||
            (instruction.Arguments.TryGetValue("layer", out EventValue? existing) &&
             (existing.Kind != EventValueKind.Constant || existing.Constant.Type != VariableType.Number)))
        {
            DrawValueArgument(instruction, "layer", "Collision Layer (-1 = All)",
                VariableType.Number, EventValue.Number(-1), state, false);
            return;
        }

        int current = instruction.Arguments.TryGetValue("layer", out EventValue? value)
            ? (int)value.Constant.Number : -1;
        string preview = current == -1 ? "All" : settings.FindLayer(current)?.Name ?? $"Layer {current}";
        ImGui.PushID("RayCollisionLayer");
        ImGui.TextDisabled("Collision Layer");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##CollisionLayer", preview))
        {
            if (ImGui.Selectable("All", current == -1))
            {
                RecordHistory("Change Ray Collision Layer");
                instruction.Arguments["layer"] = EventValue.Number(-1);
                _dirty = true;
            }
            foreach (ObjectLayerDefinition layer in settings.Layers.OrderBy(item => item.Index))
            {
                if (ImGui.Selectable($"{layer.Index}  {layer.Name}##RayLayer{layer.Index}",
                        current == layer.Index))
                {
                    RecordHistory("Change Ray Collision Layer");
                    instruction.Arguments["layer"] = EventValue.Number(layer.Index);
                    _dirty = true;
                }
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
    }

    private void DrawRayMuzzlePointArgument(
        VisualInstruction instruction,
        EditorState? state)
    {
        string currentPath =
            instruction.Arguments.TryGetValue(
                "muzzlePath",
                out EventValue? existing) &&
            existing != null &&
            existing.Kind == EventValueKind.Constant &&
            existing.Constant.Type == VariableType.String
                ? existing.Constant.String
                : string.Empty;

        GameObject? owner =
            state != null
                ? ResolveEditorObjectArgument(
                    instruction,
                    "source",
                    state)
                : null;

        ImGui.PushID(
            "RayMuzzlePoint");

        ImGui.TextDisabled(
            "Muzzle Point");

        if (owner == null)
        {
            ImGui.TextDisabled(
                "Ray Owner is unresolved. Runtime will use the Ray Owner as the muzzle.");

            if (!string.IsNullOrWhiteSpace(
                    currentPath))
            {
                ImGui.TextDisabled(
                    $"Saved child path: {currentPath}");
            }

            ImGui.PopID();
            return;
        }

        GameObject? selectedObject =
            ResolveEditorRayMuzzlePath(
                owner,
                currentPath);

        string preview =
            string.IsNullOrWhiteSpace(
                currentPath)
                ? $"Use Ray Owner ({owner.Name})"
                : selectedObject != null
                    ? currentPath
                    : currentPath + " (Missing)";

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##MuzzlePoint",
                preview))
        {
            bool useOwner =
                string.IsNullOrWhiteSpace(
                    currentPath);

            if (ImGui.Selectable(
                    $"Use Ray Owner ({owner.Name})",
                    useOwner))
            {
                RecordHistory(
                    "Change Ray Muzzle Point");

                instruction.Arguments["muzzlePath"] =
                    EventValue.String(
                        string.Empty);

                currentPath =
                    string.Empty;

                selectedObject =
                    owner;

                _dirty =
                    true;
            }

            (GameObject Object, string Path)[] candidates =
                EnumerateRayMuzzleCandidates(
                    owner)
                .ToArray();

            if (candidates.Length > 0)
            {
                ImGui.Separator();
            }

            foreach ((GameObject Object, string Path) candidate
                     in candidates)
            {
                bool selected =
                    string.Equals(
                        currentPath,
                        candidate.Path,
                        StringComparison.OrdinalIgnoreCase);

                bool isEmpty =
                    candidate.Object.Components.Count == 0;

                string emptyHint =
                    isEmpty
                        ? "  [Empty]"
                        : string.Empty;

                if (ImGui.Selectable(
                        $"{candidate.Path}{emptyHint}##rayMuzzle:{candidate.Object.Id}",
                        selected))
                {
                    RecordHistory(
                        "Change Ray Muzzle Point");

                    instruction.Arguments["muzzlePath"] =
                        EventValue.String(
                            candidate.Path);

                    currentPath =
                        candidate.Path;

                    selectedObject =
                        candidate.Object;

                    _dirty =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            if (candidates.Length == 0)
            {
                ImGui.TextDisabled(
                    "This Ray Owner has no child objects. Add an Empty under the gun first.");
            }

            ImGui.EndCombo();
        }

        if (!string.IsNullOrWhiteSpace(
                currentPath) &&
            selectedObject == null)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    .72f,
                    .28f,
                    1.0f),
                "The saved Muzzle Point no longer exists under the Ray Owner.");
        }
        else if (selectedObject != null &&
                 !ReferenceEquals(
                     selectedObject,
                     owner) &&
                 selectedObject.Components.Count > 0)
        {
            ImGui.TextDisabled(
                "Tip: a component-free Empty is recommended for a clean muzzle transform.");
        }

        ImGui.PopID();
    }

    private string DrawRayDirectionModeArgument(
        VisualInstruction instruction)
    {
        string current =
            InferRayDirectionMode(
                instruction);

        (string Mode, string Label)[] options =
        {
            ("Forward", "Muzzle Forward"),
            ("Back", "Muzzle Back"),
            ("Right", "Muzzle Right"),
            ("Left", "Muzzle Left"),
            ("Up", "Muzzle Up"),
            ("Down", "Muzzle Down"),
            ("CustomLocal", "Custom Local Direction"),
            ("CustomWorld", "Custom World Direction")
        };

        string preview =
            options
                .FirstOrDefault(
                    item =>
                        string.Equals(
                            item.Mode,
                            current,
                            StringComparison.OrdinalIgnoreCase))
                .Label
            ?? current;

        ImGui.PushID(
            "RayDirectionMode");

        ImGui.TextDisabled(
            "Direction");

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##DirectionMode",
                preview))
        {
            foreach ((string Mode, string Label) option
                     in options)
            {
                bool selected =
                    string.Equals(
                        current,
                        option.Mode,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        option.Label,
                        selected))
                {
                    SetRayDirectionMode(
                        instruction,
                        option.Mode);

                    current =
                        option.Mode;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        string hint =
            current switch
            {
                "Forward" =>
                    "Uses the Muzzle Point's local Forward axis (-Z).",

                "Back" =>
                    "Uses the opposite of the Muzzle Point's Forward axis.",

                "Right" =>
                    "Uses the Muzzle Point's local Right axis (+X).",

                "Left" =>
                    "Uses the opposite of the Muzzle Point's Right axis.",

                "Up" =>
                    "Uses the Muzzle Point's local Up axis (+Y).",

                "Down" =>
                    "Uses the opposite of the Muzzle Point's Up axis.",

                "CustomWorld" =>
                    "Uses an absolute world-space vector. It does not rotate with the Muzzle Point.",

                _ =>
                    "Uses a custom vector in the Muzzle Point's local space."
            };

        ImGui.TextWrapped(
            hint);

        ImGui.PopID();

        return current;
    }

    private void SetRayDirectionMode(
        VisualInstruction instruction,
        string mode)
    {
        RecordHistory(
            "Change Ray Direction");

        instruction.Arguments["directionMode"] =
            EventValue.String(
                mode);

        switch (mode)
        {
            case "Forward":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            0f,
                            -1f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Back":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            0f,
                            1f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Right":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            1f,
                            0f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Left":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            -1f,
                            0f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Up":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            1f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Down":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            -1f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "CustomWorld":
                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        true);
                break;

            default:
                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;
        }

        _dirty =
            true;
    }

    private static string InferRayDirectionMode(
        VisualInstruction instruction)
    {
        if (instruction.Arguments.TryGetValue(
                "directionMode",
                out EventValue? modeValue) &&
            modeValue != null &&
            modeValue.Kind == EventValueKind.Constant &&
            modeValue.Constant.Type == VariableType.String)
        {
            string normalized =
                NormalizeRayDirectionMode(
                    modeValue.Constant.String);

            if (!string.IsNullOrWhiteSpace(
                    normalized))
            {
                return normalized;
            }
        }

        bool legacyWorldSpace =
            instruction.Arguments.TryGetValue(
                "worldSpace",
                out EventValue? worldValue) &&
            worldValue != null &&
            worldValue.Kind == EventValueKind.Constant &&
            worldValue.Constant.Type == VariableType.Boolean &&
            worldValue.Constant.Boolean;

        if (legacyWorldSpace)
        {
            return "CustomWorld";
        }

        if (!instruction.Arguments.TryGetValue(
                "direction",
                out EventValue? directionValue) ||
            directionValue == null ||
            directionValue.Kind != EventValueKind.Constant ||
            directionValue.Constant.Type != VariableType.Vector3)
        {
            return "CustomLocal";
        }

        Vector3 direction =
            directionValue.Constant.Vector3;

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    0f,
                    -1f)))
        {
            return "Forward";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    0f,
                    1f)))
        {
            return "Back";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    1f,
                    0f,
                    0f)))
        {
            return "Right";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    -1f,
                    0f,
                    0f)))
        {
            return "Left";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    1f,
                    0f)))
        {
            return "Up";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    -1f,
                    0f)))
        {
            return "Down";
        }

        return "CustomLocal";
    }

    private static string NormalizeRayDirectionMode(
        string value)
    {
        return value
            .Trim()
            .Replace(
                " ",
                string.Empty)
            .ToLowerInvariant() switch
        {
            "forward" or
            "muzzleforward" =>
                "Forward",

            "back" or
            "muzzleback" =>
                "Back",

            "right" or
            "muzzleright" =>
                "Right",

            "left" or
            "muzzleleft" =>
                "Left",

            "up" or
            "muzzleup" =>
                "Up",

            "down" or
            "muzzledown" =>
                "Down",

            "customlocal" or
            "customlocaldirection" =>
                "CustomLocal",

            "customworld" or
            "customworlddirection" =>
                "CustomWorld",

            _ =>
                string.Empty
        };
    }

    private static bool RayDirectionNearlyEquals(
        Vector3 left,
        Vector3 right)
    {
        return Vector3.DistanceSquared(
                   left,
                   right) <=
               .000001f;
    }

    private static IEnumerable<(GameObject Object, string Path)>
        EnumerateRayMuzzleCandidates(
            GameObject owner)
    {
        return EnumerateRayMuzzleCandidates(
            owner,
            string.Empty);
    }

    private static IEnumerable<(GameObject Object, string Path)>
        EnumerateRayMuzzleCandidates(
            GameObject parent,
            string prefix)
    {
        foreach (GameObject child
                 in parent.Children
                     .OrderBy(
                         item =>
                             item.Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            string path =
                string.IsNullOrWhiteSpace(
                    prefix)
                    ? child.Name
                    : $"{prefix}/{child.Name}";

            yield return
                (
                    child,
                    path
                );

            foreach ((GameObject Object, string Path) descendant
                     in EnumerateRayMuzzleCandidates(
                         child,
                         path))
            {
                yield return
                    descendant;
            }
        }
    }

    private static GameObject? ResolveEditorRayMuzzlePath(
        GameObject owner,
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return owner;
        }

        GameObject current =
            owner;

        foreach (string segment
                 in path.Split(
                     '/',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            GameObject? next =
                current.Children.FirstOrDefault(
                    child =>
                        string.Equals(
                            child.Name,
                            segment,
                            StringComparison.OrdinalIgnoreCase));

            if (next == null)
            {
                return null;
            }

            current =
                next;
        }

        return current;
    }

    //
    // ========================================================
    // ANIMATION SIGNAL AUTHORING
    // ========================================================

    private void DrawAnimationSignalCondition(
        VisualInstruction instruction,
        EditorState? state,
        string signalArgument,
        string signalLabel,
        bool includePayload)
    {
        DrawObjectTargetArgument(instruction, "target", "Target Object", state);

        GameObject? target =
            state != null
                ? ResolveAnimationTargetForEditor(instruction, state)
                : null;

        if (state == null || _project == null || target == null)
        {
            DrawAnimationSignalManualFallback(
                instruction, signalArgument, signalLabel, includePayload, state,
                "Target cannot be resolved in the current authoring context.");
            return;
        }

        AnimationController? controller =
            AnimationController.FindForObject(target);

        if (controller == null)
        {
            DrawDisabledAnimationSignalFields(
                instruction, signalArgument, signalLabel, includePayload, state,
                "Target has no AnimationController.");
            return;
        }

        string cacheKey =
            AnimationSignalAuthoringResolver.CreateCacheKey(target, controller);

        if (!_animationSignalCache.TryGetValue(
                instruction.InstanceId,
                out AnimationSignalCacheEntry? cached) ||
            !string.Equals(cached.Key, cacheKey, StringComparison.Ordinal))
        {
            cached = new AnimationSignalCacheEntry(
                cacheKey,
                AnimationSignalAuthoringResolver.Resolve(target, _project));

            _animationSignalCache[instruction.InstanceId] = cached;
        }

        AnimationSignalAuthoringResult resolved = cached.Result;

        if (!resolved.HasSelectors || resolved.Model == null)
        {
            DrawAnimationSignalManualFallback(
                instruction, signalArgument, signalLabel, includePayload, state,
                resolved.Message);
            return;
        }

        string clipValue =
            DrawAnimationSignalClipPicker(instruction, resolved.Model);

        ImportedAnimation? selectedClip =
            string.IsNullOrWhiteSpace(clipValue)
                ? null
                : AnimationSignalAuthoringResolver.FindClip(
                    resolved.Model, clipValue);

        if (!string.IsNullOrWhiteSpace(clipValue) &&
            selectedClip == null)
        {
            DrawStaleAnimationValueWarning(
                "clip", clipValue, "selected animation model");
        }

        DrawAnimationSignalMetadataPicker(
            instruction, signalArgument, signalLabel, selectedClip);

        if (includePayload)
        {
            DrawValueArgument(
                instruction, "payload", "Payload (blank = Any)",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }
    }

    private void DrawAnimationSignalManualFallback(
        VisualInstruction instruction,
        string signalArgument,
        string signalLabel,
        bool includePayload,
        EditorState? state,
        string message)
    {
        DrawValueArgument(
            instruction, "clip", "Animation Clip (manual)",
            VariableType.String, EventValue.String(string.Empty),
            state, false);
        DrawValueArgument(
            instruction, signalArgument, $"{signalLabel} (manual)",
            VariableType.String, EventValue.String(string.Empty),
            state, false);

        if (includePayload)
        {
            DrawValueArgument(
                instruction, "payload", "Payload (blank = Any)",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }

        ImGui.TextWrapped(
            $"{message} Manual filters will still work at runtime.");
    }

    private void DrawDisabledAnimationSignalFields(
        VisualInstruction instruction,
        string signalArgument,
        string signalLabel,
        bool includePayload,
        EditorState? state,
        string message)
    {
        ImGui.BeginDisabled();
        try
        {
            DrawValueArgument(
                instruction, "clip", "Animation Clip",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
            DrawValueArgument(
                instruction, signalArgument, signalLabel,
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }
        finally
        {
            ImGui.EndDisabled();
        }

        if (includePayload)
        {
            DrawValueArgument(
                instruction, "payload", "Payload (blank = Any)",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }

        ImGui.TextColored(
            new Vector4(1.0f, 0.72f, 0.28f, 1.0f),
            message);
    }

    private string DrawAnimationSignalClipPicker(
        VisualInstruction instruction,
        ModelAsset model)
    {
        EventValue value = EnsureAnimationSignalString(instruction, "clip");

        if (value.Kind != EventValueKind.Constant)
        {
            DrawValueArgument(
                instruction, "clip", "Animation Clip",
                VariableType.String, EventValue.String(string.Empty),
                null, false);
            ImGui.TextDisabled(
                "Dynamic clip reference; metadata choices are unavailable.");
            return string.Empty;
        }

        string current = value.Constant.String;
        ImportedAnimation? selected =
            string.IsNullOrWhiteSpace(current)
                ? null
                : AnimationSignalAuthoringResolver.FindClip(model, current);
        string preview =
            string.IsNullOrWhiteSpace(current)
                ? "Any Clip"
                : selected?.Name ?? current;

        ImGui.TextDisabled("Animation Clip");
        ImGui.SetNextItemWidth(-1.0f);

        if (ImGui.BeginCombo("##AnimationSignalClip", preview))
        {
            if (ImGui.Selectable(
                    "Any Clip",
                    string.IsNullOrWhiteSpace(current)))
            {
                SetAnimationSignalString(
                    instruction, "clip", string.Empty,
                    "Change Animation Clip");
                current = string.Empty;
            }

            foreach (ImportedAnimation animation in model.Animations)
            {
                if (string.IsNullOrWhiteSpace(animation.Key) &&
                    string.IsNullOrWhiteSpace(animation.Name))
                {
                    continue;
                }

                string stored =
                    string.IsNullOrWhiteSpace(animation.Key)
                        ? animation.Name
                        : animation.Key;
                bool isSelected =
                    string.Equals(current, animation.Key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current, animation.Name, StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        $"{animation.Name}##clip:{animation.Key}",
                        isSelected))
                {
                    SetAnimationSignalString(
                        instruction, "clip", stored,
                        "Change Animation Clip");
                    current = stored;
                }

                DrawFullValueTooltip(animation.Name);
            }

            ImGui.EndCombo();
        }

        DrawFullValueTooltip(preview);
        return current;
    }

    private void DrawAnimationSignalMetadataPicker(
        VisualInstruction instruction,
        string argumentName,
        string label,
        ImportedAnimation? selectedClip)
    {
        EventValue value =
            EnsureAnimationSignalString(instruction, argumentName);

        if (value.Kind != EventValueKind.Constant)
        {
            DrawValueArgument(
                instruction, argumentName, label,
                VariableType.String, EventValue.String(string.Empty),
                null, false);
            return;
        }

        string current = value.Constant.String;
        bool isEvent =
            string.Equals(argumentName, "event", StringComparison.Ordinal);
        string anyLabel = isEvent ? "Any Event" : "Any Window";
        string preview =
            string.IsNullOrWhiteSpace(current) ? anyLabel : current;

        ImGui.TextDisabled(label);
        ImGui.BeginDisabled(selectedClip == null);
        try
        {
            ImGui.SetNextItemWidth(-1.0f);

            if (ImGui.BeginCombo(
                    $"##AnimationSignal:{argumentName}",
                    preview))
            {
                if (ImGui.Selectable(
                        anyLabel,
                        string.IsNullOrWhiteSpace(current)))
                {
                    SetAnimationSignalString(
                        instruction, argumentName, string.Empty,
                        $"Change Animation {label}");
                    current = string.Empty;
                }

                if (selectedClip != null && isEvent)
                {
                    foreach (AnimationEventMarker marker
                             in selectedClip.Events.OrderBy(item => item.Time))
                    {
                        string display =
                            $"{marker.Name}    {marker.Time:0.###}s";

                        if (ImGui.Selectable(
                                $"{display}##event:{marker.Id}",
                                string.Equals(
                                    current, marker.Name,
                                    StringComparison.OrdinalIgnoreCase)))
                        {
                            SetAnimationSignalString(
                                instruction, argumentName, marker.Name,
                                "Change Animation Event");
                            current = marker.Name;
                        }

                        DrawFullValueTooltip(display);
                    }
                }
                else if (selectedClip != null)
                {
                    foreach (AnimationWindow window
                             in selectedClip.Windows.OrderBy(item => item.StartTime))
                    {
                        string display =
                            $"{window.Name}    {window.StartTime:0.###} - {window.EndTime:0.###}s";

                        if (ImGui.Selectable(
                                $"{display}##window:{window.Id}",
                                string.Equals(
                                    current, window.Name,
                                    StringComparison.OrdinalIgnoreCase)))
                        {
                            SetAnimationSignalString(
                                instruction, argumentName, window.Name,
                                "Change Animation Window");
                            current = window.Name;
                        }

                        DrawFullValueTooltip(display);
                    }
                }

                ImGui.EndCombo();
            }
        }
        finally
        {
            ImGui.EndDisabled();
        }

        DrawFullValueTooltip(preview);

        if (selectedClip == null)
        {
            ImGui.TextDisabled(
                "Select a concrete Animation Clip to browse metadata.");
            return;
        }

        bool found =
            isEvent
                ? AnimationSignalAuthoringResolver.ContainsEvent(
                    selectedClip, current)
                : AnimationSignalAuthoringResolver.ContainsWindow(
                    selectedClip, current);

        if (!found)
        {
            DrawStaleAnimationValueWarning(
                label.ToLowerInvariant(), current, "selected clip");
        }
    }

    private EventValue EnsureAnimationSignalString(
        VisualInstruction instruction,
        string argumentName)
    {
        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) ||
            value == null)
        {
            value = EventValue.String(string.Empty);
            instruction.Arguments[argumentName] = value;
        }

        if (value.Kind == EventValueKind.Constant &&
            value.Constant.Type != VariableType.String)
        {
            value = EventValue.String(string.Empty);
            instruction.Arguments[argumentName] = value;
        }

        return value;
    }

    private void SetAnimationSignalString(
        VisualInstruction instruction,
        string argumentName,
        string value,
        string historyLabel)
    {
        RecordHistory(historyLabel);
        instruction.Arguments[argumentName] = EventValue.String(value);
        _dirty = true;
    }

    private static void DrawStaleAnimationValueWarning(
        string valueKind,
        string value,
        string owner)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ImGui.TextColored(
                new Vector4(1.0f, 0.72f, 0.28f, 1.0f),
                $"{value} - Not found on {owner} ({valueKind}).");
        }
    }

    private static void DrawFullValueTooltip(string value)
    {
        if (ImGui.IsItemHovered() &&
            !string.IsNullOrWhiteSpace(value))
        {
            ImGui.SetTooltip(value);
        }
    }

    // ========================================================
    // ANIMATION CLIP / PROFILE PICKERS
    // ========================================================

    private void DrawAnimationLayerArgument(
        VisualInstruction instruction,
        EditorState? state)
    {
        const string label = "Layer";
        GameObject? target =
            state != null
                ? ResolveAnimationTargetForEditor(instruction, state)
                : null;
        AnimationController? controller =
            AnimationController.FindForObject(target);

        if (_project == null ||
            controller == null ||
            controller.AnimationProfile.IsEmpty)
        {
            DrawValueArgument(
                instruction,
                "layer",
                label + " (manual)",
                VariableType.String,
                EventValue.String(string.Empty),
                state,
                false);
            return;
        }

        AnimationProfile profile;
        try
        {
            profile = _project.Assets.LoadAnimationProfile(controller.AnimationProfile);
        }
        catch
        {
            DrawValueArgument(
                instruction,
                "layer",
                label + " (manual)",
                VariableType.String,
                EventValue.String(string.Empty),
                state,
                false);
            return;
        }

        EventValue value = EnsureAnimationSignalString(instruction, "layer");
        if (value.Kind != EventValueKind.Constant)
        {
            DrawValueArgument(
                instruction,
                "layer",
                label,
                VariableType.String,
                EventValue.String(string.Empty),
                state,
                false);
            return;
        }

        string current = value.Constant.String;
        AnimationLayerProfile[] layers =
            profile.Layers
                .Where(layer => !string.IsNullOrWhiteSpace(layer.Name))
                .ToArray();

        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(-1.0f);

        string preview = string.IsNullOrWhiteSpace(current)
            ? "Select Layer"
            : current;

        if (ImGui.BeginCombo("##AnimationLayer", preview))
        {
            if (ImGui.Selectable("<None>", string.IsNullOrWhiteSpace(current)))
            {
                SetAnimationSignalString(
                    instruction,
                    "layer",
                    string.Empty,
                    "Change Animation Layer");
                current = string.Empty;
            }

            if (layers.Length > 0)
            {
                ImGui.Separator();
            }

            for (int i = 0; i < layers.Length; i++)
            {
                AnimationLayerProfile layer = layers[i];
                bool selected = string.Equals(
                    current,
                    layer.Name,
                    StringComparison.OrdinalIgnoreCase);
                string status = layer.Enabled ? string.Empty : "  (Default Off)";

                if (ImGui.Selectable(
                        $"{layer.Name}{status}##animationLayer:{i}",
                        selected))
                {
                    SetAnimationSignalString(
                        instruction,
                        "layer",
                        layer.Name,
                        "Change Animation Layer");
                    current = layer.Name;
                }

                if (ImGui.IsItemHovered())
                {
                    string clip = string.IsNullOrWhiteSpace(layer.Clip)
                        ? "<No Clip>"
                        : layer.Clip;
                    ImGui.SetTooltip(
                        $"Clip: {clip}\nMode: {layer.BlendMode}\nMask: {layer.Mask.Kind}\nBlend In: {layer.BlendIn:0.###}s\nBlend Out: {layer.BlendOut:0.###}s");
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            if (layers.Length == 0)
            {
                ImGui.TextDisabled("The target Animation Profile has no animation layers.");
            }

            ImGui.EndCombo();
        }

        if (!string.IsNullOrWhiteSpace(current) &&
            !layers.Any(layer => string.Equals(
                layer.Name,
                current,
                StringComparison.OrdinalIgnoreCase)))
        {
            DrawStaleAnimationValueWarning(
                "layer",
                current,
                "Animation Profile");
        }
    }

}

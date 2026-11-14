using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;
internal sealed partial class EventWorkspacePanel
{
    private Guid? _listeningKeyInstruction;
    private string _listeningKeyArgument = "";
    private int _keyListenFrame = -1;
    private bool IsCapturingKey => _listeningKeyInstruction != null || _keyListenFrame == ImGui.GetFrameCount();
    internal static VisualInstruction CreateInstructionDefaults(string id, VisualLogicRegistry registry)
    {
        var instruction =
            new VisualInstruction
            {
                Id = id
            };
        switch (id)
        {
            case "time.startTimer":
                instruction.Arguments["name"] = EventValue.String("Timer");
                instruction.Arguments["duration"] = EventValue.Number(1.0);
                break;
            case "time.stopTimer":
            case "time.timerFinished":
            case "time.timerRunning":
                instruction.Arguments["name"] = EventValue.String("Timer");
                break;

            case "input.mouseHeld":
            case "input.mousePressed":
            case "input.mouseReleased":
                instruction.Arguments["button"] =
                    EventValue.String(
                        "Left");
                break;

            case "flow.branch":
                instruction.Arguments["condition"] =
                    EventValue.Boolean(
                        false);
                break;

            case "input.keyHeld":
            case "input.keyPressed":
            case "input.keyReleased":
                instruction.Arguments["key"] =
                    EventValue.String(
                        "W");
                break;

            case "input.actionHeld":
            case "input.actionPressed":
            case "input.actionReleased":
                instruction.Arguments["action"] = EventValue.String(DefaultActionToken("Jump"));
                break;

            case "input.axisGreater":
            case "input.vectorLengthGreater":
                instruction.Arguments["action"] = EventValue.String(DefaultActionToken("Move"));
                instruction.Arguments["value"] = EventValue.Number(.5);
                break;

            case "input.axisLess":
                instruction.Arguments["action"] = EventValue.String(DefaultActionToken("Move"));
                instruction.Arguments["value"] = EventValue.Number(-.5);
                break;

            case "physics.lineTrace":
            case "physics.lineTraceHitsAnything":
                // New traces default to a useful object + direction workflow.
                // start/end are kept too so Event Sheets created by the first
                // universal-trace pass remain loadable without migration.
                instruction.Arguments["startMode"] = EventValue.String("Object");
                instruction.Arguments["startObject"] = EventValue.String("Self");
                instruction.Arguments["start"] = EventValue.Vector3(Vector3.Zero);
                instruction.Arguments["endMode"] = EventValue.String("Direction");
                instruction.Arguments["endObject"] = EventValue.String("Self");
                instruction.Arguments["end"] = EventValue.Vector3(new Vector3(0f, 0f, -10f));
                instruction.Arguments["traceDirectionMode"] = EventValue.String("Forward");
                instruction.Arguments["traceDirection"] = EventValue.Vector3(new Vector3(0f, 0f, -1f));
                instruction.Arguments["distance"] = EventValue.Number(100);
                instruction.Arguments["layer"] = EventValue.Number(-1);
                instruction.Arguments["ignoreSelf"] = EventValue.Boolean(true);
                instruction.Arguments["includeTriggers"] = EventValue.Boolean(false);
                instruction.Arguments["drawDebug"] = EventValue.Boolean(false);
                instruction.Arguments["debugDuration"] = EventValue.Number(.25);
                break;

            case "physics.castRayToCursor":
            case "physics.castRayInDirection":
            case "physics.castRay":
            case "physics.rayHitsAnything":
                instruction.Arguments["source"] = EventValue.String("Self");
                instruction.Arguments["muzzlePath"] = EventValue.String(string.Empty);
                instruction.Arguments["aimMode"] = EventValue.String(
                    id == "physics.castRayToCursor" ? "TopDownCursor" : "MuzzleDirection");
                instruction.Arguments["topDownAimStyle"] = EventValue.String("Exact3D");
                instruction.Arguments["directionMode"] = EventValue.String("Forward");
                instruction.Arguments["originOffset"] = EventValue.Vector3(Vector3.Zero);

                /*
                 * Keep the legacy direction/worldSpace values serialized too.
                 * This preserves compatibility with existing Event Sheets and
                 * older runtime data while the editor presents presets.
                 */
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            0f,
                            -1f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);

                instruction.Arguments["distance"] = EventValue.Number(100);
                instruction.Arguments["layer"] = EventValue.Number(-1);
                instruction.Arguments["includeTriggers"] = EventValue.Boolean(false);
                instruction.Arguments["drawDebug"] = EventValue.Boolean(false);
                instruction.Arguments["debugDuration"] = EventValue.Number(.25);
                break;
            case "scene.load":
                instruction.Arguments["path"]=EventValue.String("Scenes/Workshop.bytescene");break;
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
                instruction.Arguments["target"] = EventValue.String("Self");
                if (id is "ui.setText" or "ui.setButtonLabel")
                    instruction.Arguments["text"] = EventValue.String(string.Empty);
                if (id is "ui.setBarValue" or "ui.setBarMaximum" or "ui.barBelow" or "ui.barAbove")
                    instruction.Arguments["value"] = EventValue.Number(100);
                if (id == "ui.setImage")
                    instruction.Arguments["image"] = EventValue.String(string.Empty);
                if (id is "ui.setBarFromHealth" or "ui.setTextFromHealth")
                    instruction.Arguments["source"] = EventValue.String("Self");
                if (id == "ui.setTextFromHealth")
                {
                    instruction.Arguments["prefix"] = EventValue.String("HP ");
                    instruction.Arguments["prefixKey"] = EventValue.String(string.Empty);
                }
                if (id == "ui.setButtonEnabled")
                    instruction.Arguments["enabled"] = EventValue.Boolean(true);
                if (id is "ui.setLanguage" or "ui.languageIs")
                    instruction.Arguments["language"] = EventValue.String("en");
                if (id is "ui.setTextKey" or "ui.setLabelKey")
                    instruction.Arguments["key"] = EventValue.String("menu.play");
                break;
            case "material.setMaterial":
            case "material.setBaseColor":
            case "material.setMetallic":
            case "material.setRoughness":
            case "material.setEmissionColor":
            case "material.setEmissionIntensity":
            case "material.setTexture":
            case "material.resetOverrides":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["slot"] = EventValue.Number(0);
                if (id == "material.setMaterial")
                    instruction.Arguments["material"] = EventValue.String(string.Empty);
                if (id == "material.setTexture")
                {
                    instruction.Arguments["textureSlot"] = EventValue.String("BaseColor");
                    instruction.Arguments["texture"] = EventValue.String(string.Empty);
                }
                if (id is "material.setBaseColor" or "material.setEmissionColor")
                    instruction.Arguments["color"] = EventValue.Vector3(Vector3.One);
                if (id == "material.setBaseColor")
                    instruction.Arguments["alpha"] = EventValue.Number(1);
                if (id is "material.setMetallic" or "material.setRoughness" or
                    "material.setEmissionIntensity")
                    instruction.Arguments["value"] = EventValue.Number(1);
                break;
            case "combat.fireWeapon":
            case "combat.canFire":
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
            case "physics.lastRayHitObject":
            case "projectile.fire":
                instruction.Arguments["target"] = EventValue.String("Self");
                break;
            case "health.percentAtMost":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["value"] = EventValue.Number(.25);
                break;
            case "health.damage":
            case "health.heal":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["amount"] = EventValue.Number(20);
                break;
            case "combat.damageLastRayHit":
                instruction.Arguments["amount"] = EventValue.Number(20);
                break;
            case "physics.saveRayHit":
                instruction.Arguments["prefix"] = EventValue.String("Ray");
                break;

            case "attachment.attachToSocket":
                instruction.Arguments["object"] = EventValue.String("Self");
                instruction.Arguments["parent"] = EventValue.String("Self");
                instruction.Arguments["socket"] = EventValue.String(string.Empty);
                instruction.Arguments["locationRule"] = EventValue.String("SnapToTarget");
                instruction.Arguments["rotationRule"] = EventValue.String("SnapToTarget");
                instruction.Arguments["scaleRule"] = EventValue.String("KeepRelative");
                instruction.Arguments["positionOffset"] = EventValue.Vector3(Vector3.Zero);
                instruction.Arguments["rotationOffset"] = EventValue.Vector3(Vector3.Zero);
                instruction.Arguments["scaleMultiplier"] = EventValue.Vector3(Vector3.One);
                break;
            case "attachment.detach":
                instruction.Arguments["object"] = EventValue.String("Self");
                instruction.Arguments["keepWorld"] = EventValue.Boolean(true);
                break;
            case "attachment.isAttached":
                instruction.Arguments["object"] = EventValue.String("Self");
                break;
            case "attachment.isAttachedToSocket":
                instruction.Arguments["object"] = EventValue.String("Self");
                instruction.Arguments["parent"] = EventValue.String("Self");
                instruction.Arguments["socket"] = EventValue.String(string.Empty);
                break;
            case "animation.eventFired":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["event"] = EventValue.String(string.Empty);
                instruction.Arguments["clip"] = EventValue.String(string.Empty);
                instruction.Arguments["payload"] = EventValue.String(string.Empty);
                break;

            case "animation.windowEntered":
            case "animation.windowExited":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["window"] = EventValue.String(string.Empty);
                instruction.Arguments["clip"] = EventValue.String(string.Empty);
                instruction.Arguments["payload"] = EventValue.String(string.Empty);
                break;

            case "animation.windowActive":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["window"] = EventValue.String(string.Empty);
                instruction.Arguments["clip"] = EventValue.String(string.Empty);
                break;
            case "animation.pause":
            case "animation.resume":
            case "animation.stop":
            case "animation.isPlaying":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                break;

            case "animation.play":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clip"] =
                    EventValue.String(
                        string.Empty);
                instruction.Arguments["loop"] =
                    EventValue.Boolean(
                        true);
                break;

            case "animation.playAction":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clip"] =
                    EventValue.String(
                        string.Empty);
                instruction.Arguments["retrigger"] =
                    EventValue.Boolean(
                        false);
                instruction.Arguments["interruptCurrent"] =
                    EventValue.Boolean(
                        true);
                break;

            case "animation.playNamedAction":
                instruction.Arguments["target"]=EventValue.String("Self"); instruction.Arguments["action"]=EventValue.String(string.Empty);
                instruction.Arguments["retrigger"]=EventValue.Boolean(false); instruction.Arguments["queueIfBlocked"]=EventValue.Boolean(false); instruction.Arguments["forceInterrupt"]=EventValue.Boolean(false); break;
            case "animation.queueNamedAction":
            case "animation.currentActionIs":
                instruction.Arguments["target"]=EventValue.String("Self"); instruction.Arguments["action"]=EventValue.String(string.Empty); break;
            case "animation.queueCombo":
            case "animation.cancelAction":
            case "animation.actionPlaying":
                instruction.Arguments["target"]=EventValue.String("Self"); break;
            case "animation.triggerAction":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["action"] = EventValue.String(string.Empty);
                break;
            case "animation.setSpeed":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["speed"] =
                    EventValue.Number(
                        1.0);
                break;

            case "animation.setTransitionDuration":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["duration"] =
                    EventValue.Number(
                        0.15);
                break;

            case "animation.setLayerWeight":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["layer"] = EventValue.String(string.Empty);
                instruction.Arguments["weight"] = EventValue.Number(1.0);
                break;

            case "animation.enableLayer":
            case "animation.disableLayer":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["layer"] = EventValue.String(string.Empty);
                break;

            case "animation.currentClipIs":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clip"] =
                    EventValue.String(
                        string.Empty);
                break;

            case "animation.currentStateIs":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["state"] =
                    EventValue.String(
                        "Idle");
                break;

            case "object.exists":
            case "object.isActive":
            case "object.destroy":
            case "audio.play":
            case "audio.pause":
            case "audio.stop":
            case "audio.isPlaying":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                break;

            case "audio.playClip":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clipGuid"] =
                    EventValue.String(
                        string.Empty);
                instruction.Arguments["clipPath"] =
                    EventValue.String(
                        string.Empty);
                break;
            case "audio.setVolume":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["volume"] =
                    EventValue.Number(
                        1.0);
                break;

            case "audio.setPitch":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["pitch"] =
                    EventValue.Number(
                        1.0);
                break;

            case "audio.setLoop":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["loop"] =
                    EventValue.Boolean(
                        true);
                break;

            case "object.hasTag":
            case "object.doesNotHaveTag":
            case "object.addTag":
            case "object.removeTag":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["tag"] = EventValue.String(DefaultTagToken("Enemy"));
                break;

            case "object.withTagExists":
                instruction.Arguments["tag"] = EventValue.String(DefaultTagToken("Enemy"));
                break;

            case "object.isOnLayer":
            case "object.setLayer":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["layer"] = EventValue.Number(DefaultLayerIndex("Default"));
                break;

            case "object.setActive":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["active"] =
                    EventValue.Boolean(
                        true);
                break;

            case "object.spawnEmpty":
                instruction.Arguments["name"] =
                    EventValue.String(
                        "GameObject");

                instruction.Arguments["position"] =
                    EventValue.FromReference(
                        new VariableReference
                        {
                            Scope =
                                VariableScope.Component,

                            ComponentType =
                                "Transform",

                            MemberName =
                                "WorldPosition"
                        });
                break;

            case "object.spawnBlueprint":
                instruction.Arguments["blueprint"] =
                    EventValue.String(
                        string.Empty);

                instruction.Arguments["position"] =
                    EventValue.FromReference(
                        new VariableReference
                        {
                            Scope =
                                VariableScope.Component,

                            ComponentType =
                                "Transform",

                            MemberName =
                                "WorldPosition"
                        });
                break;

            case "character.moveForward":
            case "character.moveRight":
                instruction.Arguments["amount"] =
                    EventValue.Number(
                        1.0);
                break;

            case "character.setVelocity":
                instruction.Arguments["velocity"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "character.addImpulse":
                instruction.Arguments["impulse"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.setPosition":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["position"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.move":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["amount"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.setX":
            case "transform.setY":
            case "transform.setZ":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["value"] =
                    EventValue.Number(
                        0.0);
                break;

            case "transform.setRotation":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["rotation"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.rotateBy":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["amount"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.setScale":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["scale"] =
                    EventValue.Vector3(
                        Vector3.One);
                break;

            case "variable.compare":
                instruction.Arguments["left"] =
                    EventValue.Number(
                        0.0);
                instruction.Arguments["operator"] =
                    EventValue.String(
                        "==");
                instruction.Arguments["right"] =
                    EventValue.Number(
                        0.0);
                break;

            case "variable.set":
                instruction.Arguments["value"] =
                    EventValue.Number(
                        0.0);
                break;

            case "variable.add":
            case "variable.subtract":
                instruction.Arguments["amount"] =
                    EventValue.Number(
                        1.0);
                break;
        }

        foreach (var argument in registry.GetArguments(id))
            instruction.Arguments.TryAdd(argument.Name, argument.CreateDefault());
        return instruction;
    }

    // ========================================================
    // ARGUMENT UI
    // ========================================================


    private void DrawKeyArgument(VisualInstruction instruction, string argumentName, string label, Key fallback)
    {
        if (!instruction.Arguments.TryGetValue(argumentName, out EventValue? value) || value == null)
            instruction.Arguments[argumentName] = value = EventValue.String(fallback.ToString());
        string current = value.Kind == EventValueKind.Constant && value.Constant.Type == VariableType.String
            ? value.Constant.String : fallback.ToString();
        bool listening = _listeningKeyInstruction == instruction.InstanceId && _listeningKeyArgument == argumentName;
        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(Math.Max(80f, ImGui.GetContentRegionAvail().X - 100f));
        if (ImGui.BeginCombo("##Key" + argumentName, current))
        {
            foreach (Key key in Enum.GetValues<Key>())
            {
                string name = key.ToString();
                bool selected = string.Equals(current, name, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(name, selected))
                {
                    instruction.Arguments[argumentName] = EventValue.String(name);
                    _listeningKeyInstruction = null; _dirty = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button((listening ? "Cancel" : "Listen") + "##Listen" + argumentName))
        {
            _listeningKeyInstruction = listening ? null : instruction.InstanceId;
            _listeningKeyArgument = argumentName;
            _keyListenFrame = ImGui.GetFrameCount();
        }
        if (listening)
        {
            ImGui.TextDisabled("Press a key. Escape cancels.");
            if (ImGui.GetFrameCount() > _keyListenFrame && EditorKeyCapture.TryGetPressed(out Key pressed))
            {
                if (pressed != Key.Escape)
                {
                    instruction.Arguments[argumentName] = EventValue.String(pressed.ToString());
                    _dirty = true;
                }
                _listeningKeyInstruction = null;
                _keyListenFrame = ImGui.GetFrameCount();
            }
        }
    }

}

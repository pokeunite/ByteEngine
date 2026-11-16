using System.Numerics;
using System.Reflection;

using ByteEngine.Core;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor.Panels;

using ImGuiNET;

namespace ByteEngine.Tests;

internal static class EventAuthoringControlsTests
{
    internal static void Run()
    {
        nint previous = ImGui.GetCurrentContext();
        nint context = ImGui.CreateContext();
        string log = Path.Combine(Path.GetTempPath(), "ByteEngine-authoring-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            ImGui.SetCurrentContext(context);
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(1280, 720);
            io.DeltaTime = 1f / 60;
            io.Fonts.GetTexDataAsRGBA32(out nint pixels, out int width, out int height);
            var panel = new EventWorkspacePanel(null!);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(EventWorkspacePanel);
            var rule = new EventRuleDefinition();

            void Frame(Action draw)
            {
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(new Vector2(10, 10));
                ImGui.SetNextWindowSize(new Vector2(650, 600));
                ImGui.Begin("Authoring controls test");
                draw();
                ImGui.End();
                ImGui.Render();
            }

            foreach (bool condition in new[] { true, false })
            {
                string popup = condition ? "Add Condition" : "Add Action";
                string method = condition ? "DrawConditionPicker" : "DrawActionPicker";
                var items = condition ? rule.Conditions : rule.Actions;
                Frame(() =>
                {
                    ImGui.OpenPopup(popup);
                    type.GetMethod(method, flags)!.Invoke(panel, [rule, items]);
                });
                type.GetField(condition ? "_conditionMenuSearch" : "_actionMenuSearch", flags)!.SetValue(panel, "no_such_instruction_8234");
                Frame(() =>
                {
                    ImGui.LogToFile(0, log);
                    type.GetMethod(method, flags)!.Invoke(panel, [rule, items]);
                    ImGui.LogFinish();
                });
                Check(File.ReadAllText(log).Contains("No matching results"), popup + " invokes its search control");
                File.Delete(log);
                Frame(() =>
                {
                    if (ImGui.BeginPopup(popup))
                    {
                        ImGui.CloseCurrentPopup();
                        ImGui.EndPopup();
                    }
                });
            }

            foreach (string id in new[] { "input.mouseHeld", "input.mousePressed", "input.mouseReleased" })
            {
                var instruction = new VisualInstruction { Id = id };
                instruction.Arguments["button"] = EventValue.String("Left");
                Vector2 minimum = default, maximum = default;
                void Draw()
                {
                    type.GetMethod("DrawInstructionArguments", flags)!.Invoke(panel, [instruction, null]);
                    minimum = ImGui.GetItemRectMin();
                    maximum = ImGui.GetItemRectMax();
                }
                Frame(Draw);
                Vector2 button = (minimum + maximum) / 2;
                io.AddMousePosEvent(button.X, button.Y);
                io.AddMouseButtonEvent(0, true);
                Frame(Draw);
                io.AddMouseButtonEvent(0, false);
                Frame(() =>
                {
                    ImGui.LogToFile(0, log);
                    Draw();
                    ImGui.LogFinish();
                });
                string text = File.ReadAllText(log);
                Check(text.Contains("Right") && text.Contains("Middle"), id + " opens supported mouse-button options");
                File.Delete(log);
                Check(instruction.Arguments["button"].Constant.String == "Left", "Opening dropdown preserves existing value");
                io.AddMousePosEvent(1000, 650);
                io.AddMouseButtonEvent(0, true);
                Frame(Draw);
                io.AddMouseButtonEvent(0, false);
                Frame(Draw);
            }
            Console.WriteLine("PASS event authoring controls");
        }
        finally
        {
            if (File.Exists(log)) File.Delete(log);
            ImGui.DestroyContext(context);
            ImGui.SetCurrentContext(previous);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}


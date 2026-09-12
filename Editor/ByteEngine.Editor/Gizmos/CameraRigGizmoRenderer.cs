using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Gizmos;

internal static class CameraRigGizmoRenderer
{
    public static void Draw(Scene scene, EditorCamera3D editorCamera, Vector2 minimum, Vector2 size)
    {
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint boomColor = ImGui.GetColorU32(new Vector4(.25f, .8f, 1f, .95f));
        uint cameraColor = ImGui.GetColorU32(new Vector4(1f, .78f, .2f, .95f));

        foreach (GameObject gameObject in scene.GameObjects.Where(item => item.ActiveInHierarchy))
        {
            CameraBoom3D? boom = gameObject.GetComponent<CameraBoom3D>();
            if (boom != null && boom.Enabled)
            {
                Vector3 pivot = gameObject.Transform.WorldPosition + Vector3.UnitY * boom.PivotHeight;
                float yaw = boom.Yaw * MathF.PI / 180f;
                Vector3 right = new(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
                Vector3 socket = pivot + CameraBoom3D.CalculateOrbitVector(boom.Yaw, boom.Pitch, boom.ArmLength) +
                    right * boom.ShoulderOffset;
                Vector2 pivotScreen = Gizmo3DController.Project(pivot, editorCamera, minimum, size);
                Vector2 socketScreen = Gizmo3DController.Project(socket, editorCamera, minimum, size);
                draw.AddLine(pivotScreen, socketScreen, boomColor, 2f);
                draw.AddCircleFilled(pivotScreen, 4f, boomColor);
                draw.AddCircle(socketScreen, 6f, boomColor, 16, 2f);
            }

            Camera3D? camera = gameObject.GetComponent<Camera3D>();
            if (camera == null || !camera.Enabled) continue;
            Vector3 position = gameObject.Transform.WorldPosition;
            Vector2 cameraScreen = Gizmo3DController.Project(position, editorCamera, minimum, size);
            Vector2 forwardScreen = Gizmo3DController.Project(position + gameObject.Transform.Forward, editorCamera, minimum, size);
            draw.AddCircleFilled(cameraScreen, 5f, cameraColor);
            draw.AddLine(cameraScreen, forwardScreen, cameraColor, 2f);
        }
    }
}

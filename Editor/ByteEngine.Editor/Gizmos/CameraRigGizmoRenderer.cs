using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Gizmos;

internal static class CameraRigGizmoRenderer
{
    public static void Draw(
        Scene scene,
        EditorCamera3D editorCamera,
        Vector2 minimum,
        Vector2 size,
        GameObject? selectedObject = null,
        Component? selectedComponent = null)
    {
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint boomColor = ImGui.GetColorU32(new Vector4(.25f, .8f, 1f, .35f));
        uint cameraColor = ImGui.GetColorU32(new Vector4(1f, .78f, .2f, .35f));

        foreach (GameObject gameObject in scene.GameObjects.Where(item => item.ActiveInHierarchy))
        {
            CameraBoom3D? boom = gameObject.GetComponent<CameraBoom3D>();
            if (boom != null && boom.Enabled)
            {
                bool boomSelected = ReferenceEquals(selectedObject, gameObject) &&
                    (selectedComponent == null || ReferenceEquals(selectedComponent, boom));
                uint boomDrawColor = boomSelected ? ImGui.GetColorU32(new Vector4(.35f, 1f, 1f, 1f)) : boomColor;
                float boomThickness = boomSelected ? 4f : 2f;
                Vector3 pivot = gameObject.Transform.WorldPosition + Vector3.UnitY * boom.PivotHeight;
                PlayerController3D? controller = gameObject.GetComponent<PlayerController3D>();
                float boomYaw = boom.UseControlRotation && controller != null ? controller.ControlYaw : boom.Yaw;
                float boomPitch = boom.UseControlRotation && controller != null ? controller.ControlPitch : boom.Pitch;
                float yaw = boomYaw * MathF.PI / 180f;
                Vector3 right = new(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
                Vector3 socket = pivot + CameraBoom3D.CalculateOrbitVector(boomYaw, boomPitch, boom.ArmLength) +
                    right * boom.ShoulderOffset;
                Vector2 pivotScreen = Gizmo3DController.Project(pivot, editorCamera, minimum, size);
                Vector2 socketScreen = Gizmo3DController.Project(socket, editorCamera, minimum, size);
                draw.AddLine(pivotScreen, socketScreen, boomDrawColor, boomThickness);
                draw.AddCircleFilled(pivotScreen, boomSelected ? 6f : 4f, boomDrawColor);
                draw.AddCircle(socketScreen, boomSelected ? 8f : 6f, boomDrawColor, 16, boomThickness);
            }

            Camera3D? camera = gameObject.GetComponent<Camera3D>();
            if (camera == null || !camera.Enabled) continue;
            Vector3 position = gameObject.Transform.WorldPosition;
            Vector3 forward = gameObject.Transform.Forward;
            Vector3 rightVector = gameObject.Transform.Right;
            Vector3 up = gameObject.Transform.Up;
            bool selected = ReferenceEquals(selectedObject, gameObject) &&
                (selectedComponent == null || ReferenceEquals(selectedComponent, camera));
            uint color = selected ? ImGui.GetColorU32(new Vector4(1f, .95f, .35f, 1f)) : cameraColor;
            float thickness = selected ? 3f : 2f;

            Vector3[] body =
            {
                position - rightVector * .22f - up * .14f,
                position + rightVector * .22f - up * .14f,
                position + rightVector * .22f + up * .14f,
                position - rightVector * .22f + up * .14f
            };
            DrawLoop(draw, body, editorCamera, minimum, size, color, thickness);

            Vector3 lens = position + forward * .28f;
            Vector2 positionScreen = Gizmo3DController.Project(position, editorCamera, minimum, size);
            Vector2 lensScreen = Gizmo3DController.Project(lens, editorCamera, minimum, size);
            Vector2 arrowScreen = Gizmo3DController.Project(position + forward * .8f, editorCamera, minimum, size);
            draw.AddCircle(lensScreen, selected ? 6f : 4f, color, 16, thickness);
            draw.AddLine(positionScreen, arrowScreen, color, thickness);
            Vector2 arrowDirection = arrowScreen - positionScreen;
            if (arrowDirection.LengthSquared() > 1f)
            {
                Vector2 direction = Vector2.Normalize(arrowDirection);
                Vector2 perpendicular = new(-direction.Y, direction.X);
                draw.AddTriangleFilled(
                    arrowScreen,
                    arrowScreen - direction * 10f + perpendicular * 5f,
                    arrowScreen - direction * 10f - perpendicular * 5f,
                    color);
            }

            float halfHeight = MathF.Tan(camera.FieldOfView * MathF.PI / 360f) * 1.1f;
            float halfWidth = halfHeight * 1.6f;
            Vector3 center = position + forward * 1.1f;
            Vector3[] frustum =
            {
                center - rightVector * halfWidth - up * halfHeight,
                center + rightVector * halfWidth - up * halfHeight,
                center + rightVector * halfWidth + up * halfHeight,
                center - rightVector * halfWidth + up * halfHeight
            };
            DrawLoop(draw, frustum, editorCamera, minimum, size, color, thickness);
            foreach (Vector3 corner in frustum)
                draw.AddLine(positionScreen, Gizmo3DController.Project(corner, editorCamera, minimum, size), color, thickness);
        }
    }

    private static void DrawLoop(ImDrawListPtr draw, IReadOnlyList<Vector3> points,
        EditorCamera3D camera, Vector2 minimum, Vector2 size, uint color, float thickness)
    {
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 start = Gizmo3DController.Project(points[index], camera, minimum, size);
            Vector2 end = Gizmo3DController.Project(points[(index + 1) % points.Count], camera, minimum, size);
            draw.AddLine(start, end, color, thickness);
        }
    }
}

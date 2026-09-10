using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Panels;

internal static class ProjectTemplateFactory
{
    public static Scene Create(ProjectTemplate template) => template switch
    {
        ProjectTemplate.Clean => new Scene("Main"),
        ProjectTemplate.Starter3D => CreateStarter3D(),
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    private static Scene CreateStarter3D()
    {
        var scene = new Scene("Main");

        GameObject camera = scene.CreateGameObject("Main Camera");
        camera.Transform.LocalPosition = new Vector3(0f, 2f, 6f);
        camera.Transform.EulerAngles = new Vector3(-18f, 0f, 0f);
        camera.AddComponent(new Camera3D());

        GameObject cube = scene.CreateGameObject("Cube");
        cube.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Cube,
            Material = new Material { BaseColor = new Vector4(.25f, .58f, 1f, 1f) }
        });

        GameObject ground = scene.CreateGameObject("Ground");
        ground.Transform.LocalPosition = new Vector3(0f, -1f, 0f);
        ground.Transform.LocalScale = new Vector3(10f, 1f, 10f);
        ground.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Plane,
            Material = new Material { BaseColor = new Vector4(.32f, .38f, .32f, 1f) }
        });
        ground.AddComponent(new BoxCollider3D { Size = new Vector3(1f, .05f, 1f) });
        ground.AddComponent(new GroundSurface());

        GameObject light = scene.CreateGameObject("Directional Light");
        light.Transform.EulerAngles = new Vector3(45f, -35f, 0f);
        light.AddComponent(new DirectionalLight { Intensity = 1.2f });

        return scene;
    }
}

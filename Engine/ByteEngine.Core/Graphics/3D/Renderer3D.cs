using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class Renderer3D : IDisposable
{
    private readonly Dictionary<PrimitiveMeshType, Mesh> _primitives = new();
    private Shader3D? _shader;

    internal void Initialize() => _shader ??= new Shader3D();

    public Mesh GetPrimitive(PrimitiveMeshType type)
    {
        if (!_primitives.TryGetValue(type, out Mesh? mesh))
        {
            mesh = PrimitiveMesh.Create(type);
            _primitives[type] = mesh;
        }

        return mesh;
    }

    public void Draw(
        Mesh mesh,
        Material material,
        Matrix4x4 model,
        Matrix4x4 view,
        Matrix4x4 projection,
        Vector3 lightDirection,
        Vector3 lightColor,
        float intensity,
        float ambientIntensity)
    {
        Initialize();
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);

        _shader!.Use();
        _shader.SetMatrix("uModel", model);
        _shader.SetMatrix("uView", view);
        _shader.SetMatrix("uProjection", projection);
        _shader.SetVector4("uBaseColor", material.BaseColor);
        _shader.SetVector3("uLightDirection", lightDirection);
        _shader.SetVector3("uLightColor", lightColor);
        _shader.SetFloat("uLightIntensity", intensity);
        _shader.SetFloat("uAmbientIntensity", ambientIntensity);

        if (material.MainTexture != null)
        {
            material.MainTexture.Bind(0);
            _shader.SetInt("uTexture", 0);
            _shader.SetInt("uUseTexture", 1);
        }
        else
        {
            _shader.SetInt("uUseTexture", 0);
        }

        mesh.Bind();
        GL.DrawElements(BeginMode.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        foreach (Mesh mesh in _primitives.Values) mesh.Dispose();
        _primitives.Clear();
        _shader?.Dispose();
        _shader = null;
    }
}

using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class Mesh : IDisposable
{
    private float[] _vertices;
    private uint[] _indices;

    private int _vertexArray;
    private int _vertexBuffer;
    private int _elementBuffer;

    public int IndexCount =>
        _indices.Length;

    public int VertexCount =>
        _vertices.Length /
        8;

    public bool IsUploaded =>
        _vertexArray !=
        0;

    /// <summary>
    /// Local-space bounds calculated from the mesh position vertices.
    /// </summary>
    public BoundingBox3D LocalBounds { get; private set; }

    public Mesh(
        float[] vertices,
        uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(
            vertices);

        ArgumentNullException.ThrowIfNull(
            indices);

        if (vertices.Length %
            8 !=
            0)
        {
            throw new ArgumentException(
                "Vertices must use position/normal/UV layout.",
                nameof(vertices));
        }

        _vertices =
            vertices;

        _indices =
            indices;

        LocalBounds =
            BoundingBox3D.FromInterleavedVertices(
                _vertices);
    }

    internal void Bind()
    {
        if (!IsUploaded)
        {
            Upload();
        }

        GL.BindVertexArray(
            _vertexArray);
    }

    internal void ReplaceData(
        float[] vertices,
        uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(
            vertices);

        ArgumentNullException.ThrowIfNull(
            indices);

        if (vertices.Length %
            8 !=
            0)
        {
            throw new ArgumentException(
                "Vertices must use position/normal/UV layout.",
                nameof(vertices));
        }

        DisposeGpuResources();

        _vertices =
            vertices;

        _indices =
            indices;

        LocalBounds =
            BoundingBox3D.FromInterleavedVertices(
                _vertices);
    }

    private void Upload()
    {
        _vertexArray =
            GL.GenVertexArray();

        _vertexBuffer =
            GL.GenBuffer();

        _elementBuffer =
            GL.GenBuffer();

        GL.BindVertexArray(
            _vertexArray);

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            _vertexBuffer);

        GL.BufferData(
            BufferTarget.ArrayBuffer,
            _vertices.Length *
            sizeof(float),
            _vertices,
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(
            BufferTarget.ElementArrayBuffer,
            _elementBuffer);

        GL.BufferData(
            BufferTarget.ElementArrayBuffer,
            _indices.Length *
            sizeof(uint),
            _indices,
            BufferUsageHint.StaticDraw);

        int stride =
            8 *
            sizeof(float);

        GL.EnableVertexAttribArray(
            0);

        GL.VertexAttribPointer(
            0,
            3,
            VertexAttribPointerType.Float,
            false,
            stride,
            0);

        GL.EnableVertexAttribArray(
            1);

        GL.VertexAttribPointer(
            1,
            3,
            VertexAttribPointerType.Float,
            false,
            stride,
            3 *
            sizeof(float));

        GL.EnableVertexAttribArray(
            2);

        GL.VertexAttribPointer(
            2,
            2,
            VertexAttribPointerType.Float,
            false,
            stride,
            6 *
            sizeof(float));

        GL.BindVertexArray(
            0);
    }

    public void Dispose()
    {
        DisposeGpuResources();
    }

    private void DisposeGpuResources()
    {
        if (_elementBuffer !=
            0)
        {
            GL.DeleteBuffer(
                _elementBuffer);
        }

        if (_vertexBuffer !=
            0)
        {
            GL.DeleteBuffer(
                _vertexBuffer);
        }

        if (_vertexArray !=
            0)
        {
            GL.DeleteVertexArray(
                _vertexArray);
        }

        _elementBuffer =
            0;

        _vertexBuffer =
            0;

        _vertexArray =
            0;
    }
}

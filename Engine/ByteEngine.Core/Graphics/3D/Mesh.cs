using System.Numerics;

using ByteEngine.Core.Graphics;

using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class Mesh : IDisposable
{
    private float[] _vertices;
    private uint[] _indices;

    private readonly bool _dynamicVertices;
    private bool _vertexDataDirty;

    /*
     * OpenGL buffer objects are shared by the native asset-editor contexts
     * because those windows are created with SharedContext = main.Context.
     *
     * Vertex-array objects are NOT shared between OpenGL contexts. Keeping a
     * single VAO id here caused meshes uploaded/rendered in one ByteEngine
     * window to disappear in another window. Keep one VAO per logical context
     * while sharing the VBO/EBO.
     */
    private readonly Dictionary<int, VertexArrayState> _vertexArrays =
        new();

    private int _vertexBuffer;
    private int _elementBuffer;
    private int _bufferGeneration;

    public int IndexCount =>
        _indices.Length;

    public int VertexCount =>
        _vertices.Length /
        8;

    public bool IsUploaded =>
        _vertexBuffer !=
        0 &&
        _elementBuffer !=
        0;

    /// <summary>
    /// Local-space bounds calculated from the mesh position vertices.
    /// </summary>
    public BoundingBox3D LocalBounds { get; private set; }

    public Mesh(
        float[] vertices,
        uint[] indices,
        bool dynamicVertices = false)
    {
        ArgumentNullException.ThrowIfNull(
            vertices);

        ArgumentNullException.ThrowIfNull(
            indices);

        ValidateVertices(
            vertices);

        _vertices =
            vertices;

        _indices =
            indices;

        _dynamicVertices =
            dynamicVertices;

        LocalBounds =
            BoundingBox3D.FromInterleavedVertices(
                _vertices);
    }

    internal void Bind()
    {
        if (!IsUploaded)
        {
            UploadBuffers();
        }
        else if (_vertexDataDirty)
        {
            UploadVertexChanges();
        }

        BindVertexArrayForCurrentContext();
    }

    /// <summary>
    /// Efficient same-layout vertex update for dynamic meshes.
    ///
    /// The shared VBO is refreshed the next time the mesh is bound. Context
    /// specific VAOs remain valid because the buffer object and vertex layout
    /// are unchanged.
    /// </summary>
    internal void UpdateVertices(
        float[] vertices,
        bool updateBounds = true)
    {
        ArgumentNullException.ThrowIfNull(
            vertices);

        ValidateVertices(
            vertices);

        if (vertices.Length !=
            _vertices.Length)
        {
            throw new ArgumentException(
                "Dynamic vertex updates must preserve the existing vertex count.",
                nameof(vertices));
        }

        if (!ReferenceEquals(
                vertices,
                _vertices))
        {
            Array.Copy(
                vertices,
                _vertices,
                vertices.Length);
        }

        if (updateBounds)
        {
            LocalBounds =
                BoundingBox3D.FromInterleavedVertices(
                    _vertices);
        }

        _vertexDataDirty =
            true;
    }

    internal void ReplaceData(
        float[] vertices,
        uint[] indices)
    {
        ArgumentNullException.ThrowIfNull(
            vertices);

        ArgumentNullException.ThrowIfNull(
            indices);

        ValidateVertices(
            vertices);

        /*
         * Delete only the shareable buffer objects here. Existing VAOs belong
         * to their individual contexts and cannot safely be deleted from an
         * arbitrary current context. Their generation will no longer match and
         * each context replaces its own VAO the next time it binds this mesh.
         */
        ReleaseSharedBuffers();

        _vertices =
            vertices;

        _indices =
            indices;

        _vertexDataDirty =
            false;

        LocalBounds =
            BoundingBox3D.FromInterleavedVertices(
                _vertices);
    }

    private void UploadBuffers()
    {
        _vertexBuffer =
            GL.GenBuffer();

        _elementBuffer =
            GL.GenBuffer();

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            _vertexBuffer);

        GL.BufferData(
            BufferTarget.ArrayBuffer,
            _vertices.Length *
            sizeof(float),
            _vertices,
            _dynamicVertices
                ? BufferUsageHint.DynamicDraw
                : BufferUsageHint.StaticDraw);

        /*
         * ELEMENT_ARRAY_BUFFER binding is VAO state in the core profile.
         * Upload index data through COPY_WRITE_BUFFER so buffer creation does
         * not accidentally depend on whichever context-local VAO is active.
         */
        GL.BindBuffer(
            BufferTarget.CopyWriteBuffer,
            _elementBuffer);

        GL.BufferData(
            BufferTarget.CopyWriteBuffer,
            _indices.Length *
            sizeof(uint),
            _indices,
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(
            BufferTarget.CopyWriteBuffer,
            0);

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            0);

        _bufferGeneration++;

        _vertexDataDirty =
            false;
    }

    private void BindVertexArrayForCurrentContext()
    {
        int contextId =
            GraphicsContextScope.CurrentContextId;

        if (_vertexArrays.TryGetValue(
                contextId,
                out VertexArrayState existing) &&
            existing.BufferGeneration ==
            _bufferGeneration &&
            existing.VertexArray !=
            0)
        {
            GL.BindVertexArray(
                existing.VertexArray);

            return;
        }

        /*
         * If this logical context has an older VAO, the matching OpenGL
         * context is current here, so deleting that one VAO is safe.
         */
        if (existing.VertexArray !=
            0)
        {
            GL.DeleteVertexArray(
                existing.VertexArray);
        }

        int vertexArray =
            GL.GenVertexArray();

        GL.BindVertexArray(
            vertexArray);

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            _vertexBuffer);

        GL.BindBuffer(
            BufferTarget.ElementArrayBuffer,
            _elementBuffer);

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

        _vertexArrays[
            contextId] =
            new VertexArrayState(
                vertexArray,
                _bufferGeneration);
    }

    private void UploadVertexChanges()
    {
        if (_vertexBuffer ==
            0)
        {
            return;
        }

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            _vertexBuffer);

        /*
         * Orphan the previous dynamic storage before uploading the new frame.
         * Because VBOs are shared by the ByteEngine native-window share group,
         * one upload updates the data seen by every context-specific VAO.
         */
        GL.BufferData(
            BufferTarget.ArrayBuffer,
            _vertices.Length *
            sizeof(float),
            IntPtr.Zero,
            BufferUsageHint.DynamicDraw);

        GL.BufferSubData(
            BufferTarget.ArrayBuffer,
            IntPtr.Zero,
            _vertices.Length *
            sizeof(float),
            _vertices);

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            0);

        _vertexDataDirty =
            false;
    }

    private static void ValidateVertices(
        float[] vertices)
    {
        if (vertices.Length %
            8 !=
            0)
        {
            throw new ArgumentException(
                "Vertices must use position/normal/UV layout.",
                nameof(vertices));
        }
    }

    public void Dispose()
    {
        DisposeGpuResources();
    }

    private void ReleaseSharedBuffers()
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

        _elementBuffer =
            0;

        _vertexBuffer =
            0;

        _vertexDataDirty =
            false;
    }

    private void DisposeGpuResources()
    {
        /*
         * A VAO can only be deleted safely from the context that owns it.
         * Delete the current context's VAO now. VAOs owned by other contexts
         * are intentionally left to that OpenGL context's teardown; clearing
         * the managed cache prevents a disposed Mesh from reusing them.
         */
        int currentContextId =
            GraphicsContextScope.CurrentContextId;

        if (_vertexArrays.TryGetValue(
                currentContextId,
                out VertexArrayState current) &&
            current.VertexArray !=
            0)
        {
            GL.DeleteVertexArray(
                current.VertexArray);
        }

        _vertexArrays.Clear();

        ReleaseSharedBuffers();
    }

    private readonly record struct VertexArrayState(
        int VertexArray,
        int BufferGeneration);
}

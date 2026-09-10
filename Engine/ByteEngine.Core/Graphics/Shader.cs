using OpenTK.Graphics.OpenGL4;

using Matrix4 = OpenTK.Mathematics.Matrix4;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace ByteEngine.Core.Graphics;

internal sealed class Shader : IDisposable
{
    private readonly int _programHandle;

    private readonly Dictionary<string, int> _uniformLocations =
        new();

    public Shader(
        string vertexSource,
        string fragmentSource)
    {
        int vertexShader =
            CompileShader(
                ShaderType.VertexShader,
                vertexSource
            );

        int fragmentShader =
            CompileShader(
                ShaderType.FragmentShader,
                fragmentSource
            );

        _programHandle =
            GL.CreateProgram();

        GL.AttachShader(
            _programHandle,
            vertexShader
        );

        GL.AttachShader(
            _programHandle,
            fragmentShader
        );

        GL.LinkProgram(
            _programHandle
        );

        GL.GetProgram(
            _programHandle,
            GetProgramParameterName.LinkStatus,
            out int success
        );

        if (success == 0)
        {
            string error =
                GL.GetProgramInfoLog(
                    _programHandle
                );

            GL.DeleteShader(
                vertexShader
            );

            GL.DeleteShader(
                fragmentShader
            );

            GL.DeleteProgram(
                _programHandle
            );

            throw new Exception(
                $"ByteEngine shader failed to link:\n{error}"
            );
        }

        GL.DetachShader(
            _programHandle,
            vertexShader
        );

        GL.DetachShader(
            _programHandle,
            fragmentShader
        );

        GL.DeleteShader(
            vertexShader
        );

        GL.DeleteShader(
            fragmentShader
        );
    }

    private static int CompileShader(
        ShaderType type,
        string source)
    {
        int shader =
            GL.CreateShader(type);

        GL.ShaderSource(
            shader,
            source
        );

        GL.CompileShader(
            shader
        );

        GL.GetShader(
            shader,
            ShaderParameter.CompileStatus,
            out int success
        );

        if (success == 0)
        {
            string error =
                GL.GetShaderInfoLog(
                    shader
                );

            GL.DeleteShader(
                shader
            );

            throw new Exception(
                $"ByteEngine {type} failed to compile:\n{error}"
            );
        }

        return shader;
    }

    public void Use()
    {
        GL.UseProgram(
            _programHandle
        );
    }

    private int GetUniformLocation(
        string name)
    {
        if (_uniformLocations.TryGetValue(
                name,
                out int location))
        {
            return location;
        }

        location =
            GL.GetUniformLocation(
                _programHandle,
                name
            );

        _uniformLocations[name] =
            location;

        return location;
    }

    public void SetInt(
        string name,
        int value)
    {
        GL.Uniform1(
            GetUniformLocation(name),
            value
        );
    }

    public void SetFloat(
        string name,
        float value)
    {
        GL.Uniform1(
            GetUniformLocation(name),
            value
        );
    }

    public void SetVector2(
        string name,
        Vector2 value)
    {
        GL.Uniform2(
            GetUniformLocation(name),
            value.X,
            value.Y
        );
    }

    public void SetVector4(
        string name,
        Vector4 value)
    {
        GL.Uniform4(
            GetUniformLocation(name),
            value.X,
            value.Y,
            value.Z,
            value.W
        );
    }

    public void SetMatrix4(
        string name,
        Matrix4 value)
    {
        int location =
            GetUniformLocation(name);

        GL.UniformMatrix4(
            location,
            false,
            ref value
        );
    }

    public void Dispose()
    {
        GL.DeleteProgram(
            _programHandle
        );
    }
}
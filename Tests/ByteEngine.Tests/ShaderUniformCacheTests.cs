using ByteEngine.Core.Graphics;
using OpenTK.Graphics.OpenGL4;
using Matrix4 = OpenTK.Mathematics.Matrix4;
namespace ByteEngine.Tests;
internal static class ShaderUniformCacheTests
{
    public static void Run()
    {
        const string vertex = "#version 330 core\nuniform mat4 m; void main(){gl_Position=m*vec4(0,0,0,1);}";
        const string fragment = "#version 330 core\nuniform float f; uniform int i; uniform vec4 v; out vec4 c; void main(){c=v*(f+float(i));}";
        using var first=new Shader(vertex,fragment);
        using var second=new Shader(vertex,fragment);
        foreach(var shader in new[]{first,second,first})
        {
            shader.Use();int program=GL.GetInteger(GetPName.CurrentProgram);
            foreach(float value in new[]{1f,1f,3f,1f})
            {
                shader.SetFloat("f",value);shader.SetInt("i",(int)value);
                shader.SetVector4("v",new(value,2,3,4));
                shader.SetMatrix4("m",Matrix4.CreateTranslation(value,2,3));
                GL.GetUniform(program,GL.GetUniformLocation(program,"f"),out float actual);
                if(actual!=value)throw new Exception("Cached uniform did not follow changed/revisited values.");
                GL.GetUniform(program,GL.GetUniformLocation(program,"i"),out int integer);
                if(integer!=(int)value)throw new Exception("Integer shader cache was incorrect.");
                var vector=new float[4];GL.GetUniform(program,GL.GetUniformLocation(program,"v"),vector);
                if(vector[0]!=value||vector[3]!=4)throw new Exception("Vector shader cache was incorrect.");
                var matrix=new float[16];GL.GetUniform(program,GL.GetUniformLocation(program,"m"),matrix);
                if(matrix[12]!=value||matrix[13]!=2||matrix[14]!=3)throw new Exception("Matrix shader cache was incorrect.");
                shader.SetFloat("absent",value);
            }
        }
        if(GL.GetError()!=ErrorCode.NoError)throw new Exception("Shader cache generated an OpenGL error.");
        GL.UseProgram(0);
        Console.WriteLine("PASS: native shader uniform caching, changed/revisited values and independent programs.");
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Tests;
internal static class ShaderPortabilityTests
{
 public static void Run()
 {
  // Some desktop drivers accept reserved identifiers that NVIDIA correctly rejects.
  var shader=typeof(Renderer3D).Assembly.GetType("ByteEngine.Core.Graphics.ThreeD.Shader3D")!;
  string reserved="packed|input|output|common|partition|active|filter|resource|namespace|using|row_major";
  foreach(var field in shader.GetFields(BindingFlags.Static|BindingFlags.NonPublic))
  {
   if(!field.IsLiteral||field.FieldType!=typeof(string)||field.GetRawConstantValue() is not string source||!source.Contains("#version"))continue;
   var match=Regex.Match(source,@"\b(?:float|int|uint|bool|[biu]?vec[234]|mat[234])\s+(?:"+reserved+@")\b");
   if(match.Success)throw new Exception(field.Name+" uses reserved GLSL identifier: "+match.Value);
  }
  Console.WriteLine("PASS desktop shader reserved-identifier portability checks");
 }
}

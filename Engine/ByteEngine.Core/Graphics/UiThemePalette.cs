using System.Numerics;
using System.Text.Json;
namespace ByteEngine.Core.Graphics;
/// <summary>Reusable .uitheme JSON asset; local widget colours remain available through an empty ThemeKey.</summary>
public sealed class UiThemePalette
{
 public Vector4 Primary {get;set;}=new(.95f,.65f,.22f,1);
 public Vector4 Secondary {get;set;}=new(.09f,.1f,.095f,1);
 public Vector4 Danger {get;set;}=new(.65f,.18f,.12f,1);
 public Vector4 Label {get;set;}=new(.88f,.87f,.83f,1);
 public Vector4 PrimaryLabel {get;set;}=new(.06f,.06f,.045f,1);
 public Vector4 Disabled {get;set;}=new(.16f,.17f,.16f,.7f);
 private static readonly JsonSerializerOptions Options=new(){IncludeFields=true,PropertyNameCaseInsensitive=true,WriteIndented=true};
 public static UiThemePalette Load(string path)=>JsonSerializer.Deserialize<UiThemePalette>(File.ReadAllText(path),Options)??throw new InvalidDataException("Invalid UI theme");
 public void Save(string path)=>File.WriteAllText(path,JsonSerializer.Serialize(this,Options));
}

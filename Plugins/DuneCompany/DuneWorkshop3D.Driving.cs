using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 static IEnumerable<GameObject> Descendants(GameObject root){foreach(var c in root.Children){yield return c;foreach(var d in Descendants(c))yield return d;}}
}

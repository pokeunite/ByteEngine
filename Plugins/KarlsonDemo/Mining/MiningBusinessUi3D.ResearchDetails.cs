using System.Numerics;
using ByteEngine.Core;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 int descriptionScroll;
 public string[] ResearchDescriptionLines(string description)
 {
  var result=new List<string>();string line="";
  foreach(string word in description.Split(' ',StringSplitOptions.RemoveEmptyEntries)){if(line.Length>0&&line.Length+word.Length+1>45){result.Add(line);line="";}line+=(line.Length>0?" ":"")+word;}
  if(line.Length>0)result.Add(line);return result.ToArray();
 }
 void ScrollResearchDescription(Vector2 mouse,float wheel)
 {
  if(mouse.X<96||mouse.X>560||mouse.Y<550||mouse.Y>650||Trade?.Research.FirstOrDefault(n=>n.Id==SelectedResearchId) is not {} chosen)return;
  descriptionScroll=Math.Clamp(descriptionScroll-(int)MathF.Sign(wheel),0,Math.Max(0,ResearchDescriptionLines(chosen.Description).Length-3));
 }
 void DrawResearchDescription(BusinessPainter p,string description)
 {
  var lines=ResearchDescriptionLines(description);descriptionScroll=Math.Clamp(descriptionScroll,0,Math.Max(0,lines.Length-3));
  for(int i=0;i<3&&i+descriptionScroll<lines.Length;i++)p.T(lines[i+descriptionScroll],96,558+i*23,17,MenuMuted);
  if(lines.Length>3)p.T($"Scroll here for more  /  {descriptionScroll+1}-{Math.Min(lines.Length,descriptionScroll+3)} of {lines.Length}",96,637,12,Accent);
 }
}

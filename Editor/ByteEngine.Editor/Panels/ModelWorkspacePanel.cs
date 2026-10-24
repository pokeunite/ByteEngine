using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;
internal sealed class ModelWorkspacePanel : IDisposable
{
 readonly EditorDocumentManager documents; readonly SkeletalSocketPreview preview=new(); readonly ModelImporterInspector importer=new();
 readonly EditorProjectContext project; readonly AssetRecord asset; ModelAsset model; readonly AssetReference reference; readonly EditorDocumentId id;
 List<SkeletalSocketDefinition> points; Guid selected; string search=""; string status="";
 public ModelWorkspacePanel(EditorDocumentManager documents,EditorProjectContext project,AssetRecord asset)
 {
  this.documents=documents;this.project=project;this.asset=asset;reference=new(asset.Guid,asset.ProjectPath);model=project.Assets.LoadModel(reference);points=model.Sockets.Select(s=>s.Clone()).ToList();selected=points.FirstOrDefault()?.Id??Guid.Empty;
  id=new(EditorDocumentType.Model,asset.Guid.ToString("N"));documents.RegisterOrFocus(new EditorDocument(id,model.Name,()=>{},()=>documents.Unregister(id)));
 }
 void Persist(){try{ModelSocketMetadataStore.Save(project.ProjectRoot,model.Guid,points);model.ReplaceSockets(points);status="Attachment points saved";}catch(Exception e){status=e.Message;}}
 public void Draw(Renderer2D renderer,Renderer3D renderer3D,int width,int height)
 {
  bool open=true;ImGui.Begin("Model: "+model.Name+"###ModelWorkspace",ref open);
  if(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))documents.Activate(id);
  ImGui.TextDisabled($"{model.Meshes.Count} meshes   {model.Materials.Count} materials   {model.Skeleton?.Bones.Count??0} bones   {model.Animations.Count} animations");
  ImGui.TextWrapped("Add an attachment point, select it, and move its gizmo onto the barrel tip. Root points work without bones. Changes save automatically.");
  if(ImGui.BeginTable("ModelColumns",3,ImGuiTableFlags.Resizable|ImGuiTableFlags.BordersInnerV))
  {
   ImGui.TableSetupColumn("Model",ImGuiTableColumnFlags.WidthFixed,230);ImGui.TableSetupColumn("Preview",ImGuiTableColumnFlags.WidthStretch,2);ImGui.TableSetupColumn("Details",ImGuiTableColumnFlags.WidthFixed,300);ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);
   ImGui.SeparatorText("ATTACHMENT POINTS");
   if(ImGui.Button("+ Add empty point")){string name="Muzzle";int suffix=2;while(points.Any(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))name="Point "+suffix++;var point=new SkeletalSocketDefinition{Name=name};points.Add(point);selected=point.Id;Persist();}
   foreach(var point in points)if(ImGui.Selectable(point.Name+"##"+point.Id,point.Id==selected))selected=point.Id;
   ImGui.SeparatorText("MODEL CONTENTS");ImGui.InputTextWithHint("##modelSearch","Search bones / nodes",ref search,128);
   if(ImGui.TreeNode("Bones")){if(model.Skeleton==null)ImGui.TextDisabled("No bones - use a root point");else foreach(var bone in model.Skeleton.Bones.Where(b=>b.Name.Contains(search,StringComparison.OrdinalIgnoreCase)))ImGui.TextUnformatted(bone.Name);ImGui.TreePop();}
   if(ImGui.TreeNode("Nodes")){foreach(var node in model.Nodes.Where(n=>n.Name.Contains(search,StringComparison.OrdinalIgnoreCase)))ImGui.TextUnformatted(node.Name);ImGui.TreePop();}
   if(ImGui.TreeNode("Animations")){foreach(var a in model.Animations)ImGui.TextUnformatted(a.Name);ImGui.TextDisabled("Play clips using the preview selector");ImGui.TreePop();}
   if(ImGui.TreeNode("Materials")){foreach(var m in model.Materials)ImGui.TextUnformatted(m.Name);ImGui.TreePop();}
   ImGui.TableSetColumnIndex(1);preview.Draw(project,reference,model,points,ref selected,renderer,renderer3D,width,height,Persist);
   ImGui.TableSetColumnIndex(2);ImGui.SeparatorText("SELECTED POINT");var chosen=points.FirstOrDefault(p=>p.Id==selected);
   if(chosen!=null)
   {
    string name=chosen.Name;if(ImGui.InputText("Name",ref name,128)&&!string.IsNullOrWhiteSpace(name)&&!points.Any(p=>p!=chosen&&p.Name.Equals(name,StringComparison.OrdinalIgnoreCase))){chosen.Name=name;Persist();}
    if(ImGui.BeginCombo("Parent",string.IsNullOrEmpty(chosen.BoneName)?"Model root (no bone)":chosen.BoneName)){if(ImGui.Selectable("Model root (no bone)",chosen.BoneName=="")){chosen.BoneName="";Persist();}if(model.Skeleton!=null)foreach(var bone in model.Skeleton.Bones)if(ImGui.Selectable(bone.Name,chosen.BoneName==bone.Name)){chosen.BoneName=bone.Name;Persist();}ImGui.EndCombo();}
    var position=chosen.PositionOffset;if(ImGui.DragFloat3("Position",ref position,.001f)){chosen.PositionOffset=position;Persist();}
    var rotation=chosen.RotationOffsetDegrees;if(ImGui.DragFloat3("Rotation",ref rotation,.5f)){chosen.RotationOffsetDegrees=rotation;Persist();}
    var scale=chosen.Scale;if(ImGui.DragFloat3("Scale",ref scale,.01f)){chosen.Scale=Vector3.Max(scale,new(.0001f));Persist();}
    ImGui.TextWrapped("Weapon Definition: select Named Model Point and choose this point. Alternatively, choose a scene Empty in Muzzle Object.");
    if(ImGui.Button("Duplicate")){var clone=chosen.Clone(false);clone.Name=chosen.Name+" copy";while(points.Any(p=>p.Name.Equals(clone.Name,StringComparison.OrdinalIgnoreCase)))clone.Name+=" copy";points.Add(clone);selected=clone.Id;Persist();}
    ImGui.SameLine();if(ImGui.Button("Delete point")){points.Remove(chosen);selected=Guid.Empty;Persist();}
   }
   ImGui.SeparatorText("IMPORT PROPERTIES");if(importer.Draw(project,asset,model,null)){model=project.Assets.LoadModel(reference);points=model.Sockets.Select(p=>p.Clone()).ToList();preview.Reset();}ImGui.TextWrapped(status);ImGui.EndTable();
  }
  ImGui.End();if(!open)documents.Unregister(id);
 }
 public void Dispose()=>preview.Dispose();
}

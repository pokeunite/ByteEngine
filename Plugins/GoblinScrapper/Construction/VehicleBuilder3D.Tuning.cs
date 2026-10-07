using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace GoblinScrapper.Construction;
public sealed partial class VehicleBuilder3D {
 private Dictionary<string,float>? _copiedTuning;private string? _copiedTuningFile;
 private int _editTuning=-1;private string _tuningInput="";private bool _replaceTuningInput;
 private readonly List<UiWidget> _tuningInputs=new();
 private bool _tuneMode;private int _tunedBlock=-1,_dragTuning=-1;
 private UiWidget? _tuneToolButton;
 private GameObject? _tuningPanel;private UiText? _tuningTitle;
 private readonly List<(UiText Label,UiWidget Track,UiWidget Fill)> _tuningRows=new();
 public bool TuneBlock(int id) {
  if(!Building||Assembly==null||!Assembly.Parts.ContainsKey(id)||id==0)return false;
  CancelMove();_movePick=_copyPick=false;SetEraseTool(false);_tuneMode=true;_tunedBlock=id;_selectedBlock=id;RefreshTuningPanel();return true;
 }
 public bool SetBlockTuning(int id,string key,float value) {
  if(!Building||Assembly==null||!Assembly.Parts.TryGetValue(id,out var p))return false;
  if(!BlockTuning.Settings(Assembly.Catalog[p.File].ReferenceId).Any(s=>s.Key==key)||!float.IsFinite(value))return false;
  RememberAssembly();Assembly.Parts[id]=BlockTuning.Set(p,Assembly.Catalog[p.File].ReferenceId,key,value);RefreshTuningPanel();return true;
 }
 private void BeginTuning(){CancelMove();SetEraseTool(false);_movePick=_copyPick=false;_tuneMode=true;_tunedBlock=-1;RefreshTuningPanel();_message="TUNE: click a placed block. Escape exits.";}
 private void CloseTuning(){_tuneMode=false;_dragTuning=-1;_editTuning=-1;if(_tuningPanel!=null)_tuningPanel.Active=false;}
 private Vector2 TuningPointer(){float scale=Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720);var insets=_canvas!.GetComponent<UiCanvas>()!.SafeAreaInsets;return Input.GameViewPointerNormalized*Input.GameViewSize/Math.Max(.1f,scale)-new Vector2(insets.X,insets.Y);}
 private void CreateTuningPanel(){
  _tuningPanel=HudObject("Block tuning panel",true);_tuningPanel.BindSceneComponent(new UiWidget{Offset=new(928,78),Size=new(340,482),Color=WorkshopInk,OrderInLayer=30});
  void Parent(GameObject o,int order){if(_authoredObjects.Contains(o))return;o.SetParent(_tuningPanel,false);if(o.GetComponent<UiWidget>() is {} w){w.Offset-=new Vector2(928,78);w.OrderInLayer=order;}if(o.GetComponent<UiText>() is {} t){t.Offset-=new Vector2(928,78);t.OrderInLayer=order;}}
  _tuningTitle=Text("Tuning heading","BLOCK TUNING",new(944,92),20,true);Parent(_tuningTitle.GameObject,32);
  var close=Button("X",new(1223,88),new(30,28),CloseTuning,true);Parent(close.GameObject,32);
  for(int i=0;i<6;i++){
   int row=i;float y=135+i*57;var label=Text("Tuning value "+i,"",new(944,y),14,true);Parent(label.GameObject,32);
   var track=Button("",new(944,y+23),new(304,18),()=>StartTuningDrag(row),true);Parent(track.GameObject,32);track.Color=new(.15f,.2f,.17f,1);
   var fill=HudObject("Tuning slider fill "+i,true).BindSceneComponent(new UiWidget{Kind=UiWidgetKind.ProgressBar,Label="",Offset=new(944,y+23),Size=new(304,18),Maximum=1,FillColor=WorkshopGold,Color=Vector4.Zero,OrderInLayer=33});Parent(fill.GameObject,33);
   var value=Button("",new(1158,y-3),new(90,22),()=>BeginTuningInput(row),true);Parent(value.GameObject,34);value.FontSize=13;_tuningInputs.Add(value);
   _tuningRows.Add((label,track,fill));
  }
  var reset=Button("Reset block",new(944,494),new(145,32),()=>ResetBlockTuning(false),true);Parent(reset.GameObject,32);
  var same=Button("Apply to same type",new(1098,494),new(150,32),()=>ResetBlockTuning(true),true);Parent(same.GameObject,32);
  var hint=Text("Tuning help","Drag bars or click values to type. Enter applies / Esc cancels.",new(944,535),11,true);hint.WrapWidth=310;Parent(hint.GameObject,32);_tuningPanel.Active=false;
 }
 private BlockSetting[] SelectedTuningSettings()=>Assembly?.Parts.TryGetValue(_tunedBlock,out var p)==true?BlockTuning.Settings(Assembly.Catalog[p.File].ReferenceId):[];
 private void RefreshTuningPanel(){
  if(_tuningPanel==null)return;_tuningPanel.Active=Building&&_tuneMode&&Assembly?.Parts.ContainsKey(_tunedBlock)==true;if(!_tuningPanel.Active)return;
  var p=Assembly!.Parts[_tunedBlock];var settings=SelectedTuningSettings();_tuningTitle!.Text=Assembly.Catalog[p.File].Label+" #"+p.Id;
  for(int i=0;i<_tuningRows.Count;i++){var r=_tuningRows[i];bool show=i<settings.Length;r.Label.GameObject.Active=r.Track.GameObject.Active=r.Fill.GameObject.Active=show;if(!show){_tuningInputs[i].GameObject.Active=false;continue;}var s=settings[i];float v=BlockTuning.Value(p,Assembly.Catalog[p.File].ReferenceId,s.Key);r.Label.Text=s.Label;_tuningInputs[i].GameObject.Active=!s.Toggle;_tuningInputs[i].Label=_editTuning==i?_tuningInput+"|":v.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);_tuningInputs[i].Color=_editTuning==i?new Vector4(.18f,.3f,.2f,1):WorkshopInk;r.Label.Text=s.Label+(s.Toggle?": ":" ")+ (s.Toggle?(v>.5f?"ON":"OFF"):s.Unit);r.Fill.Value=(v-s.Min)/(s.Max-s.Min);}
  if(settings.Length==0){var r=_tuningRows[0];r.Label.GameObject.Active=true;r.Label.Text="This block has no adjustable settings.";}
 }
 private void StartTuningDrag(int row){CommitTuningInput();var settings=SelectedTuningSettings();if(row>=settings.Length)return;RememberAssembly();var s=settings[row];var p=Assembly!.Parts[_tunedBlock];if(s.Toggle){Assembly.Parts[p.Id]=BlockTuning.Set(p,Assembly.Catalog[p.File].ReferenceId,s.Key,1-BlockTuning.Value(p,Assembly.Catalog[p.File].ReferenceId,s.Key));RefreshTuningPanel();}else{_dragTuning=row;UpdateTuningDrag();}}
 private void UpdateTuningDrag(){if(_dragTuning<0)return;var settings=SelectedTuningSettings();if(_dragTuning>=settings.Length){_dragTuning=-1;return;}var s=settings[_dragTuning];var p=Assembly!.Parts[_tunedBlock];var track=_tuningRows[_dragTuning].Track;var rect=UiLayout.Resolve(track.GameObject,track.Anchor,track.Offset,track.Size,Input.GameViewSize);float t=Math.Clamp((Input.GameViewMousePosition.X-rect.Position.X)/Math.Max(1,rect.Size.X),0,1);float value=MathF.Round((s.Min+t*(s.Max-s.Min))*100)/100;Assembly.Parts[p.Id]=BlockTuning.Set(p,Assembly.Catalog[p.File].ReferenceId,s.Key,value);RefreshTuningPanel();if(!Input.IsMouseButtonDownForUi(MouseButton.Left))_dragTuning=-1;}
 private void BeginTuningInput(int row){CommitTuningInput();var settings=SelectedTuningSettings();if(row>=settings.Length||settings[row].Toggle)return;_dragTuning=-1;_editTuning=row;var p=Assembly!.Parts[_tunedBlock];_tuningInput=BlockTuning.Value(p,Assembly.Catalog[p.File].ReferenceId,settings[row].Key).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);_replaceTuningInput=true;}
 private void CommitTuningInput(){int row=_editTuning;_editTuning=-1;if(row<0)return;var settings=SelectedTuningSettings();if(row<settings.Length&&float.TryParse(_tuningInput.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float value)&&float.IsFinite(value))SetBlockTuning(_tunedBlock,settings[row].Key,value);}
 private bool UpdateTuningInput(){if(_editTuning<0)return false;if(Input.IsKeyPressed(Key.Escape)){_editTuning=-1;RefreshTuningPanel();return true;}if(Input.IsKeyPressed(Key.Enter)||Input.IsKeyPressed(Key.KeyPadEnter)){CommitTuningInput();RefreshTuningPanel();return true;}
  if((Input.IsKeyDown(Key.LeftControl)||Input.IsKeyDown(Key.RightControl))&&Input.IsKeyPressed(Key.A))_replaceTuningInput=true;
  if(Input.IsKeyPressed(Key.Backspace)||Input.IsKeyPressed(Key.Delete)){_tuningInput=_replaceTuningInput?"":_tuningInput.Length>0?_tuningInput[..^1]:"";_replaceTuningInput=false;}
  string typed="";for(int i=0;i<10;i++)if(Input.IsKeyPressed((Key)((int)Key.D0+i))||Input.IsKeyPressed((Key)((int)Key.KeyPad0+i)))typed+=i.ToString();
  if(Input.IsKeyPressed(Key.Period)||Input.IsKeyPressed(Key.Comma)||Input.IsKeyPressed(Key.KeyPadDecimal))typed+=".";
  if(Input.IsKeyPressed(Key.Minus)||Input.IsKeyPressed(Key.KeyPadSubtract))typed+="-";
  if(typed.Length>0){if(_replaceTuningInput)_tuningInput="";_replaceTuningInput=false;if(_tuningInput.Length<16)_tuningInput+=typed;}RefreshTuningPanel();return true;
 }
 private void ResetBlockTuning(bool apply){var a=Assembly;if(a==null||!a.Parts.TryGetValue(_tunedBlock,out var source))return;RememberAssembly();if(apply){foreach(var p in a.Parts.Values.Where(p=>p.File==source.File).ToArray())a.Parts[p.Id]=p with {Tuning=source.Tuning==null?null:new(source.Tuning)};}else a.Parts[source.Id]=source with {Tuning=null};RefreshTuningPanel();}
}

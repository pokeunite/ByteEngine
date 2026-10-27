using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
internal static class MiningUiAuthor
{
 public static void Build(Scene s,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/mining-ui-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  string oldEvents=Program.Root+"/Assets/Events/Mining/Mining business controls.byteevents";if(File.Exists(oldEvents))File.Copy(oldEvents,backup+"/Mining business controls.byteevents");
  foreach(var region in s.GameObjects.SelectMany(o=>o.Components).OfType<ByteEngine.Core.Navigation.NavigationRegion3D>())region.EndpointSearchRadius=Math.Max(3,region.EndpointSearchRadius);
  var op=s.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var company=op.GameObject;
  foreach(string name in new[]{"Mining HUD","Mining business interface"})if(s.FindGameObject(name) is {} old)s.DestroyGameObject(old);
  if(s.FindGameObject("HUD canvas") is {} oldHud)oldHud.Active=false;
  var trade=company.GetComponent<MiningTrade3D>()??company.AddComponent(new MiningTrade3D{OperationObjectId=company.Id});
  var canvas=s.CreateGameObject("Mining business interface");canvas.AddComponent(new UiCanvas{ReferenceResolution=new(1280,720)});
  AssetReference Font(string name){string path="Assets/UI/Mining/Fonts/"+name;var id=Guid.NewGuid();File.WriteAllText(Program.Root+"/"+path+".meta",JsonSerializer.Serialize(new{guid=id,type="Font"}));return new(id,path);}
  var view=canvas.AddComponent(new MiningBusinessUi3D{OperationObjectId=company.Id,WorkshopObjectId=company.Id,AmmoMachineObjectId=(s.FindGameObject("Ammo press station")??s.FindGameObject("Ammo press body"))!.Id,RefineryMachineObjectId=(s.FindGameObject("Refinery station")??s.FindGameObject("Refinery body"))!.Id,TerminalObjectId=(s.FindGameObject("Company tent computer interaction")??s.FindGameObject("Galactic market console"))!.Id,HeadingFont=Font("BarlowCondensed-Bold.ttf"),BodyFont=Font("BarlowCondensed-Medium.ttf")});
  var blocker=s.CreateGameObject("Modal input shield");blocker.SetParent(canvas,false);blocker.AddComponent(new UiWidget{Kind=UiWidgetKind.Panel,StretchHorizontal=true,StretchVertical=true,Offset=Vector2.Zero,Color=Vector4.Zero,BlocksWorldPointer=true,OrderInLayer=800});blocker.Active=false;view.ModalBlockerObjectId=blocker.Id;
  AssetReference Art(string name){string path="Assets/UI/Mining/Artwork/"+name;using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));return new(meta.RootElement.GetProperty("guid").GetGuid(),path);}
  view.MachineIllustration=Art("press-model.png");view.OreIllustration=Art("ore.png");view.DrillIllustration=Art("drill-model.png");view.RefineryIllustration=Art("refinery-model.png");view.TentIllustration=Art("tent-model.png");
  var module=new EventModuleDefinition{Name="Mining interface - machine interactions and transactions"};
  VisualInstruction Action(string action)=>new(){Id="bytebard.karlsondemo.mining."+action,Arguments=new(){["target"]=EventValue.String("id:"+canvas.Id)}};
  GameObject Button(string name,string action,float x,float y,float w,float h)
  {
   var o=s.CreateGameObject(name);o.SetParent(canvas,false);o.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Anchor=UiAnchor.Center,Offset=new(x+w/2-290,y+h/2-330),Size=new(w,h),Label="",Color=Vector4.Zero,HoverColor=Vector4.Zero,PressedColor=Vector4.Zero,DisabledColor=Vector4.Zero,BlocksWorldPointer=true,ActivateWithSpace=false,Visible=false,OrderInLayer=850});
   module.Rules.Add(new(){DisplayName=name,Conditions=[new(){Id="ui.buttonClicked",Arguments=new(){["target"]=EventValue.String("id:"+o.Id)}}],Actions=[Action(action)]});return o;
  }
  view.CloseButtonObjectId=Button("Close machine panel","closePanel",400,585,156,52).Id;
  view.CloseXButtonObjectId=Button("Close panel X","closePanel",526,12,42,42).Id;
  view.Row0ButtonObjectId=Button("Buy output or current machine upgrade","buyRow0",440,218,98,60).Id;
  view.Row1ButtonObjectId=Button("Buy ammo storage upgrade","buyRow1",440,330,98,60).Id;
  view.Row2ButtonObjectId=Button("Buy production efficiency upgrade","buyRow2",440,442,98,60).Id;
  view.PrimaryButtonObjectId=Button("Machine main action or sell selected cargo","primary",24,585,360,52).Id;
  view.SecondaryButtonObjectId=Button("Return to terminal","secondary",400,585,156,52).Id;


  for(int i=0;i<5;i++)view.DefenceButtonObjectIds.Add(Button("Place company defence - "+(MiningDefenseKind)i,"selectDefence"+i,0,0,200,64).Id);
  view.CreditsButtonObjectId=Button("Read audio source credits","openAudioCredits",0,0,105,34).Id;
  view.Contract0ButtonObjectId=Button("Select shallow extraction contract","selectContract0",0,0,100,100).Id;
  view.Contract1ButtonObjectId=Button("Select deep vein contract","selectContract1",0,0,100,100).Id;
  view.Contract2ButtonObjectId=Button("Select overdrive contract","selectContract2",0,0,100,100).Id;
  view.RawButtonObjectId=Button("Select raw ore","selectRaw",24,225,86,28).Id;
  view.RefinedButtonObjectId=Button("Select refined ore","selectRefined",118,225,108,28).Id;
  var slider=s.CreateGameObject("Sale quantity slider");slider.SetParent(canvas,false);slider.AddComponent(new UiWidget{Kind=UiWidgetKind.Slider,Anchor=UiAnchor.Center,Offset=new(132,198),Size=new(236,32),Maximum=100,Value=20,Color=Vector4.Zero,FillColor=Vector4.Zero,Visible=false,BlocksWorldPointer=true,OrderInLayer=850});view.QuantitySliderObjectId=slider.Id;
  var nodes=JsonSerializer.Deserialize<CompanyUpgrade[]>(trade.ResearchDefinitionsJson)??[];
  for(int i=0;i<nodes.Length;i++)view.ResearchButtonObjectIds.Add(Button("Select research - "+nodes[i].Name,"selectResearch"+i,nodes[i].X-63,nodes[i].Y-29,126,58).Id);
  void Key(string key,string action,string label)=>module.Rules.Add(new(){DisplayName=label,Conditions=[new(){Id="input.keyPressed",Arguments=new(){["key"]=EventValue.String(key)}}],Actions=[Action(action)]});
  for(int i=0;i<5;i++)Key("F"+(i+1),"selectDefence"+i,"F"+(i+1)+" - place "+(MiningDefenseKind)i);
  Key("E","openNearby","E - interact with the nearest machine");Key("Escape","closePanel","Escape - close panel");
  Key("Enter","confirm","Enter - confirm selected upgrade");
  string path="Assets/Events/Mining/Mining business controls.byteevents";new EventModuleSerializer().Save(module,Program.Root+"/"+path);File.WriteAllText(Program.Root+"/"+path+".meta",JsonSerializer.Serialize(new{guid=module.Id,type="EventModule"}));
  foreach(var old in company.Components.OfType<EventModuleComponent>().ToArray())company.RemoveComponent(old);
  canvas.AddComponent(new EventModuleComponent()).AddModuleReference(new(module.Id,path));
  if(s.FindGameObject(op.PlayerObjectId)?.GetComponent<MiningThirdPerson3D>() is {} thirdPerson)thirdPerson.InterfaceObjectId=canvas.Id;
  runtime.Serializer.Save(s,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("MINING UI INSTALLED; scene backup "+backup);
 }
}

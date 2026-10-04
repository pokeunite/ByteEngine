using ByteEngine.Core.Scene;
using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;

using ByteEngine.Core.Assets;

using ByteEngine.Core.Graphics;

namespace GoblinScrapper.Construction;

public sealed partial class VehicleBuilder3D

{

    private string PreviewMountFaceLabel(){
        if(_catalog==null)return "none";var sockets=_catalog[SelectedPart].Sockets;if(_ownSocket>=sockets.Length)return "none";
        var n=sockets[_ownSocket].Normal;
        return n.Y>.9f?"top":n.Y<-.9f?"bottom":n.X>.9f?"right side":n.X<-.9f?"left side":n.Z>.9f?"rear end":"front end";
    }
    private static readonly Vector4 WorkshopInk=new(.055f,.073f,.07f,.95f),WorkshopGold=new(.68f,.81f,.35f,1),WorkshopMuted=new(.60f,.68f,.64f,1);

    private readonly List<UiWidget> _freeCardBorders=new();

    private UiText? _partPurpose,_connectionFeedback,_machineCount,_freeDriveLabel;

    private UiWidget? _previewAction,_placeToolButton,_eraseToolButton;

    private UiWidget Image(string name,string path,Vector2 offset,Vector2 size,bool buildOnly=false,Vector4? tint=null)=>HudObject(name,buildOnly).AddComponent(new UiWidget {Kind=UiWidgetKind.Image,ImageReference=new AssetReference("Assets/GarageUI/"+path),Offset=offset,Size=size,Color=tint??Vector4.One,OrderInLayer=7});

    private void Rule(string name,Vector2 position,Vector2 size,Vector4 color,bool buildOnly=false)=>HudObject(name,buildOnly).AddComponent(new UiWidget{Offset=position,Size=size,Color=color,OrderInLayer=3});

    private UiWidget ToolButton(string name,string icon,Vector2 position,Action click)

    {

        var button=Button("",position,new(34,34),click,false);button.GameObject.Name=name;button.Color=WorkshopInk;button.HoverColor=new(.24f,.23f,.18f,1);button.PressedColor=WorkshopGold;

        Image(name+" glyph","icons/"+icon+".png",position+new Vector2(7),new(20),false,new(.82f,.81f,.75f,1));return button;

    }

    private readonly Dictionary<UiWidget,(string Title,string Body)> _workshopTips=new();
    private GameObject? _tooltipPanel;
    private UiText? _tooltipTitle,_tooltipBody;
    private bool _movePick,_copyPick;
    private UiWidget WorkshopTool(string name,string icon,float x,Action click,string help,bool buildOnly=true)
    {
        var button=Button("",new(x,5),new(38,38),click,buildOnly);button.GameObject.Name=name;button.Color=WorkshopInk;button.HoverColor=new(.19f,.23f,.22f,1);button.PressedColor=WorkshopGold;
        Image(name+" glyph","workshop/"+icon+".png",new(x+9,14),new(20),buildOnly,new(.88f,.9f,.86f,1));_workshopTips[button]=(name,help);return button;
    }
    private void CreateFreeHud()
    {
        _canvas=Own("Goblin Scraper workshop HUD");_canvas.AddComponent(new UiCanvas {ReferenceResolution=new(1280,720)});
        Rule("Toolbar",Vector2.Zero,new(1280,48),WorkshopInk);
        Rule("Toolbar edge",new(0,47),new(1280,1),new(.25f,.3f,.27f,1));
        _driveButton=WorkshopTool("Run / build","play",8,ToggleDrive,"B � run the machine or return to building.",false);
        _driveButton.Color=new(.28f,.4f,.2f,1);
        _freeDriveLabel=Text("Simulation mode","BUILD",new(57,9),22);_freeDriveLabel.Color=WorkshopGold;
        Text("Workshop title","GOBLIN SCRAPER",new(136,13),18).Color=new(.65f,.7f,.65f,1);
        Rule("History divider",new(289,10),new(1,28),new(.25f,.3f,.27f,1));
        _undoButton=WorkshopTool("Undo","undo-2",301,()=>UndoBuild(),"Ctrl+Z � undo the last build change.");
        _redoButton=WorkshopTool("Redo","redo-2",343,()=>RedoBuild(),"Ctrl+Y � redo a build change.");
        WorkshopTool("Save machine","save",394,SaveBuild,"F5 � save your current machine.");
        WorkshopTool("Load machine","folder-open",436,LoadBuild,"F9 � load the saved machine.");
        Rule("Tools divider",new(485,10),new(1,28),new(.25f,.3f,.27f,1));
        _placeToolButton=WorkshopTool("Place blocks","box",496,()=>{_movePick=_copyPick=false;SetEraseTool(false);},"Choose a part below, then click a connector to place it.");
        WorkshopTool("Move branch","move",538,()=>{SetEraseTool(false);_movePick=true;_copyPick=false;},"Click a placed block, then choose its new connector. Escape cancels.");
        WorkshopTool("Rotate mount","rotate-cw",580,()=>{_pendingMountTurns++;_message="Rotate queued. Point at a connector.";},"R - turn 90 degrees. Click this tool, then point at a connector.");
        WorkshopTool("Change mount face","arrow-left-right",622,CyclePreviewMountFace,"T � change attachment face. Tab � cycle every socket.");
        WorkshopTool("Copy part","copy",664,()=>{SetEraseTool(false);_copyPick=true;_movePick=false;},"Click a block to select another copy of that part.");
        _eraseToolButton=WorkshopTool("Erase blocks","trash-2",706,()=>{_movePick=_copyPick=false;SetEraseTool(true);},"Click a block or brace to erase it. X erases under the pointer. Ctrl+Z restores it.");
        WorkshopTool("Recover machine","rotate-cw",766,ResetPosition,"Recover the machine to its starting position.",false);
        _machineCount=Text("Machine count","",new(874,15),14);_machineCount.Color=WorkshopMuted;
        _status=Text("Machine status","",new(1075,15),14);_status.Color=WorkshopMuted;
        Rule("Part tray",new(0,620),new(1280,100),WorkshopInk,true);
        Rule("Tray edge",new(0,620),new(1280,1),new(.25f,.3f,.27f,1),true);
        string[] icons=["box","cog","swords","wrench","plane"];
        for(int c=0;c<CategoryNames.Length;c++){
            int category=c;float x=8+c*42;
            var tab=Button("",new(x,639),new(38,55),()=>{_category=category;_palettePage=0;SelectPart(Categories[category][0]);},true);tab.Color=WorkshopInk;tab.HoverColor=new(.18f,.23f,.2f,1);_categoryButtons.Add(tab);
            Image("Category "+c,"workshop/"+icons[c]+".png",new(x+8,655),new(22),true,WorkshopMuted);_workshopTips[tab]=(CategoryNames[c],"Browse "+CategoryNames[c].ToLowerInvariant()+" blocks.");
        }
        Rule("Categories divider",new(222,634),new(1,68),new(.25f,.3f,.27f,1),true);
        for(int i=0;i<6;i++){
            int slot=i;float x=238+i*106;
            var border=HudObject("Part selection "+i,true).AddComponent(new UiWidget{Offset=new(x,632),Size=new(100,76),Color=Vector4.Zero,OrderInLayer=3});_freeCardBorders.Add(border);
            var card=Button("",new(x+1,633),new(98,74),()=>{int index=_palettePage*6+slot;if(index<Categories[_category].Length)SelectPart(Categories[_category][index]);},true);card.Color=WorkshopInk;card.HoverColor=new(.2f,.25f,.21f,1);_palette.Add(card);
            _cardImages.Add(Image("Part "+i,"cutouts/"+BuilderPartFiles[0]+".png",new(x+14,630),new(72,48),true));
            _cardLabels.Add(Text("Part name "+i,"",new(x+5,682),11,true));_cardLabels[^1].WrapWidth=92;
        }
        Button("<",new(880,634),new(26,34),()=>ChangePalette(-1),true).Color=WorkshopInk;
        Button(">",new(880,674),new(26,34),()=>ChangePalette(1),true).Color=WorkshopInk;
        Rule("Details divider",new(918,634),new(1,68),new(.25f,.3f,.27f,1),true);
        _selection=Text("Selected block","",new(936,632),19,true);_selection.Color=WorkshopGold;
        _partPurpose=Text("Block function","",new(936,655),12,true);_partPurpose.WrapWidth=320;_partPurpose.Color=WorkshopMuted;
        _connectionFeedback=Text("Placement instruction","",new(22,573),14,true);_connectionFeedback.WrapWidth=1100;
        _previewAction=Image("Placement status","workshop/check.png",new(22,598),new(14),true,WorkshopGold);
        _hint=Text("Build controls","",new(44,598),12);_hint.Color=WorkshopMuted;
        _tooltipPanel=HudObject("Context tooltip",true);
        _tooltipPanel.AddComponent(new UiWidget{Offset=new(936,482),Size=new(324,123),Color=WorkshopInk,OrderInLayer=20});
        var titleObj=HudObject("Tooltip heading",true);titleObj.SetParent(_tooltipPanel,false);
        _tooltipTitle=titleObj.AddComponent(new UiText{Offset=new(15,11),FontSize=18,Color=WorkshopGold,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),OrderInLayer=22});
        var bodyObj=HudObject("Tooltip explanation",true);bodyObj.SetParent(_tooltipPanel,false);
        _tooltipBody=bodyObj.AddComponent(new UiText{Offset=new(15,37),FontSize=13,WrapWidth=288,Color=new(.85f,.88f,.84f,1),FontReference=new("Assets/GarageUI/fonts/Barlow-Regular.ttf"),OrderInLayer=22});
        _tooltipPanel.Active=false;RefreshFreePalette();
    }

    private void RefreshFreePalette()

    {

        for(int c=0;c<_categoryButtons.Count;c++)_categoryButtons[c].Color=c==_category?new(.19f,.29f,.20f,1):WorkshopInk;

        for(int i=0;i<_palette.Count;i++)

        {

            int item=_palettePage*6+i;bool visible=item<Categories[_category].Length;

            _palette[i].GameObject.Active=visible;_cardImages[i].GameObject.Active=visible;_cardLabels[i].GameObject.Active=visible;_freeCardBorders[i].GameObject.Active=visible;

            if(!visible)continue;int part=Categories[_category][item];_freeCardBorders[i].Color=part==_part?WorkshopGold:Vector4.Zero;

            _cardImages[i].ImageReference=new("Assets/GarageUI/cutouts/"+BuilderPartFiles[part]+".png");_cardLabels[i].Text=BuilderPartNames[part];_workshopTips[_palette[i]]=(BuilderPartNames[part],PartRole(BuilderPartFiles[part]));

        }

    }

    private string PartRole(string file)=>_catalog?.Standard==true?_catalog[file].Description+"\n"+StandardControls(_catalog[file].ReferenceId):file switch

    {

        "scrap_wheel_large" or "scrap_wheel_small"=>"Fits beam slots and axle hubs. A steering pivot turns its connected branch.",

        "scrap_axle_2m"=>"Carries wheel hubs. Connect through a steering joint to turn.",

        "scrap_steering_pivot"=>"Physically turns its connected branch with A / D. Required for A/D steering.",

        "scrap_suspension_piston"=>"Moving support. Its child branch follows compression.",

        "scrap_engine_block"=>"Powers the machine. More engines offset heavier builds.",

        "scrap_cab_shell"=>"Driver controls. Required for test driving.",

        "scrap_track_pod"=>"Tracked grip and steering. Use two; replaces wheels.",

        "scrap_beam_1m" or "scrap_beam_2m" or "scrap_frame_long"=>"Extends the structure. Build branches in any direction.",

        "scrap_corner_joint" or "scrap_t_connector"=>"Branches the frame across multiple mounting faces.",

        "scrap_weapon_mount" or "scrap_swivel_turret" or "scrap_lift_mast"=>"Moving mount. Connected payload follows its mechanism. G activates.",

        "scrap_armor_plate"=>"Structural cover. Adds weight; damage protection awaits combat.",

        "scrap_ram_wedge" or "scrap_forked_ram"=>"Rigid ram structure. Damage awaits combat.",

        "scrap_outrigger_arm"=>"Adjustable support branch. G activates.",

        _=>"Animated weapon. F activates; damage awaits combat."

    };

    private static string StandardControls(int id)=>id switch

    {

        2 or 46=>"W/S motor  |  Release to brake  |  Steering requires a steering block",

        13 or 28 or 79 or 95=>"A/D turns the connected output.",

        9 or 12 or 18=>"G extends/contracts the connected output.",

        4 or 30=>"G releases the connection/payload.",

        27 or 77=>"G closes/releases the grabber.",

        11 or 53 or 61=>"F fires a physical projectile with recoil.",

        23 or 54 or 59=>"F detonates/launches  |  bombs react to ground contact.",

        14=>"W/S thrust along the rotor axis. Rotate to choose direction.",

        17 or 22 or 39 or 48=>"Hold F to drive the rotor.",

        26 or 55=>"Passive propeller: attach to a powered spinning output.",

        21 or 56 or 62=>"Hold F for the directional effect.",

        97=>"G deploys/retracts the canopy.",

        7=>"Click two connectors to brace them. Escape cancels.",
        _=>"Passive physical part. Its placement and orientation matter."

    };

    private void RefreshFreeHud()
    {
        if(_status==null||Assembly==null)return;
        _placeToolButton!.Color=!_eraseMode&&!_movePick&&!_copyPick?new(.19f,.29f,.20f,1):WorkshopInk;
        _eraseToolButton!.Color=_eraseMode?new(.52f,.15f,.12f,1):WorkshopInk;
        _undoButton!.Interactable=_assemblyUndo.Count>0;_redoButton!.Interactable=_assemblyRedo.Count>0;
        _status.Text=Building?"WORKSHOP":"SPEED  "+Speed.ToString("0.0")+" m/s";
        _machineCount!.Text=(Assembly.Parts.Count+Assembly.Braces.Count)+" BLOCKS   /   "+Assembly.TotalMass.ToString("0.0")+" kg";
        _selection!.Text=BuilderPartNames[_part];_partPurpose!.Text=PartRole(SelectedPart);
        _connectionFeedback!.Text=_movePick?"MOVE: click a block to pick up its branch":_copyPick?"COPY: click a placed block":_eraseMode?_message:BraceSelected?_message+(_placementIssue!=""?"  |  "+_placementIssue:""):_candidateVisible?(_placementIssue==""?"Click to connect   /   Mount: "+PreviewMountFaceLabel():_placementIssue):_message;
        _connectionFeedback.Color=_candidateVisible&&_placementIssue!=""?new(.95f,.49f,.35f,1):new(.87f,.9f,.83f,1);
        _previewAction!.ImageReference=new("Assets/GarageUI/workshop/"+(_placementIssue==""?"check":"info")+".png");
        _hint!.Text=Building?"R turn   T face   Tab socket   RMB orbit   MMB pan   Scroll zoom   X erase   Ctrl+Z undo":"W/S drive   A/D steer   Shift drift   F weapons   G mechanisms";
        _hint.Offset=Building?new(44,598):new(22,680);
        _freeDriveLabel!.Text=Building?"BUILD":"RUNNING";_driveButton!.Interactable=!Building||Assembly.CanDrive;
        _tooltipPanel!.Active=false;
        if(Building)foreach(var tip in _workshopTips)if(tip.Key.GameObject.ActiveInHierarchy&&tip.Key.IsHovered){_tooltipTitle!.Text=tip.Value.Title;_tooltipBody!.Text=tip.Value.Body;_tooltipPanel.Active=true;break;}
    }
}

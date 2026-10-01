using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
namespace ByteEngine.Core.Construction;
public sealed partial class VehicleBuilder3D
{
    private static readonly Vector4 WorkshopInk=new(.075f,.078f,.073f,.98f),WorkshopGold=new(.78f,.58f,.18f,1),WorkshopMuted=new(.67f,.66f,.60f,1);
    private readonly List<UiWidget> _freeCardBorders=new();
    private UiText? _partPurpose,_connectionFeedback,_machineCount,_freeDriveLabel;
    private UiWidget? _previewAction,_placeToolButton,_eraseToolButton;
    private UiWidget Image(string name,string path,Vector2 offset,Vector2 size,bool buildOnly=false,Vector4? tint=null)=>HudObject(name,buildOnly).AddComponent(new UiWidget {Kind=UiWidgetKind.Image,ImageReference=new AssetReference("Assets/GarageUI/"+path),Offset=offset,Size=size,Color=tint??Vector4.One,OrderInLayer=7});
    private void Rule(string name,Vector2 position,Vector2 size,Vector4 color,bool buildOnly=false)=>HudObject(name,buildOnly).AddComponent(new UiWidget{Offset=position,Size=size,Color=color,OrderInLayer=3});
    private UiWidget ToolButton(string name,string icon,Vector2 position,Action click)
    {
        var button=Button("",position,new(34,34),click,false);button.GameObject.Name=name;button.Color=Vector4.Zero;button.HoverColor=new(.24f,.23f,.18f,1);button.PressedColor=WorkshopGold;
        Image(name+" glyph","icons/"+icon+".png",position+new Vector2(7),new(20),false,new(.82f,.81f,.75f,1));return button;
    }
    private void CreateFreeHud()
    {
        _canvas=Own("Goblin Scraper workshop HUD");_canvas.AddComponent(new UiCanvas {ReferenceResolution=new(1280,720)});
        Rule("Top steel bar",Vector2.Zero,new(1280,48),WorkshopInk);
        Rule("Toolbar brass seam",new(0,47),new(1280,1),new(.35f,.29f,.15f,1));
        Image("Workshop emblem","icons/wrench.png",new(18,12),new(23),false,WorkshopGold);
        var title=Text("Workshop title","GOBLIN",new(52,8),27);title.FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf");
        var title2=Text("Workshop title accent","SCRAPER",new(127,8),27);title2.FontReference=title.FontReference;title2.Color=WorkshopGold;
        Text("Current mode","BUILD",new(513,13),18).FontReference=title.FontReference;
        Image("Build mode glyph","icons/wrench.png",new(485,14),new(19),false,WorkshopGold);
        Rule("Build mode underline",new(476,45),new(115,3),WorkshopGold);
        _undoButton=ToolButton("Undo","rewind",new(797,7),()=>UndoBuild());
        _redoButton=ToolButton("Redo","fastForward",new(834,7),()=>RedoBuild());
        ToolButton("Save","save",new(880,7),SaveBuild);ToolButton("Load","open",new(917,7),LoadBuild);ToolButton("Recover","return",new(954,7),ResetPosition);
        _driveButton=Button("TEST DRIVE   [B]",new(1083,8),new(178,32),ToggleDrive,false);_driveButton.Color=WorkshopGold;_driveButton.HoverColor=new(.92f,.70f,.28f,1);_driveButton.PressedColor=new(.66f,.46f,.12f,1);_driveButton.FontSize=19;
        _freeDriveLabel=Text("Test drive label","TEST DRIVE  [B]",new(1110,13),19);_freeDriveLabel.FontReference=title.FontReference;_freeDriveLabel.Color=new(.075f,.07f,.045f,1);_driveButton.Label="";
        Rule("Chassis inspector",new(18,70),new(171,204),WorkshopInk,true);
        Text("Master block label","YOUR MACHINE",new(29,80),13,true).Color=WorkshopMuted;
        Text("Root information","One master block.\nBuild your own frame.",new(29,103),14,true).WrapWidth=145;
        _status=Text("Connected machine requirements","",new(29,144),14);_status.WrapWidth=145;
        _machineCount=Text("Machine part count","",new(29,213),12,true);_machineCount.Color=WorkshopMuted;
        _placeToolButton=Button("PLACE",new(29,240),new(70,23),()=>SetEraseTool(false),true);
        _eraseToolButton=Button("ERASE",new(105,240),new(71,23),()=>SetEraseTool(true),true);
        _placeToolButton.HoverColor=new(.48f,.37f,.17f,1);_eraseToolButton.HoverColor=new(.72f,.22f,.12f,1);
        Rule("Parts dock",new(0,551),new(1280,169),WorkshopInk,true);
        Rule("Dock brass seam",new(0,551),new(1280,1),new(.35f,.29f,.15f,1),true);
        string[] icons=["menuGrid","gear","target","wrench","target"];
        for(int c=0;c<CategoryNames.Length;c++)
        {
            int category=c;float x=18+c*146;
            var tab=Button(CategoryNames[c],new(x+27,559),new(114,28),()=>{_category=category;_palettePage=0;SelectPart(Categories[category][0]);},true);tab.FontSize=17;tab.Color=Vector4.Zero;tab.HoverColor=new(.2f,.2f,.17f,1);_categoryButtons.Add(tab);
            Image("Category glyph "+c,"icons/"+icons[c]+".png",new(x+3,565),new(17),true,WorkshopMuted);
        }
        Button("<",new(1193,559),new(28,28),()=>ChangePalette(-1),true).Color=Vector4.Zero;
        Button(">",new(1231,559),new(28,28),()=>ChangePalette(1),true).Color=Vector4.Zero;
        Rule("Dock divider",new(18,591),new(1244,1),new(.25f,.25f,.22f,1),true);
        for(int i=0;i<6;i++)
        {
            int slot=i;float x=18+i*153;
            var border=HudObject("Part selection border "+i,true).AddComponent(new UiWidget {Offset=new(x,599),Size=new(145,96),Color=WorkshopGold,OrderInLayer=3});_freeCardBorders.Add(border);
            var card=Button("",new(x+2,601),new(141,92),()=>{int index=_palettePage*6+slot;if(index<Categories[_category].Length)SelectPart(Categories[_category][index]);},true);card.Color=WorkshopInk;card.HoverColor=new(.18f,.18f,.15f,1);card.PressedColor=new(.26f,.24f,.16f,1);_palette.Add(card);
            _cardImages.Add(Image("Part cutout "+i,"cutouts/"+BuilderPartFiles[0]+".png",new(x+24,602),new(96,67),true));
            _cardLabels.Add(Text("Part name "+i,"",new(x+10,673),14,true));
        }
        _selection=Text("Current part","",new(954,605),18,true);_selection.FontReference=title.FontReference;
        _partPurpose=Text("Part role","",new(954,630),13,true);_partPurpose.WrapWidth=290;_partPurpose.Color=WorkshopMuted;
        Rule("Context feedback strip",new(204,498),new(930,36),new(.075f,.078f,.073f,.92f),true);
        _connectionFeedback=Text("Placement feedback","",new(228,506),14,true);_connectionFeedback.WrapWidth=900;
        _previewAction=Image("Placement state icon","icons/checkmark.png",new(204,508),new(17),true,WorkshopGold);
        _hint=Text("Interaction hints","",new(696,702),12);_hint.Color=WorkshopMuted;
        Image("Mouse orbit prompt","prompts/mouse_middle.png",new(603,700),new(17),true);
        Image("Mouse zoom prompt","prompts/mouse_scroll.png",new(630,700),new(17),true);
        RefreshFreePalette();
    }
    private void RefreshFreePalette()
    {
        for(int c=0;c<_categoryButtons.Count;c++)_categoryButtons[c].Color=c==_category?new(.24f,.21f,.13f,1):Vector4.Zero;
        for(int i=0;i<_palette.Count;i++)
        {
            int item=_palettePage*6+i;bool visible=item<Categories[_category].Length;
            _palette[i].GameObject.Active=visible;_cardImages[i].GameObject.Active=visible;_cardLabels[i].GameObject.Active=visible;_freeCardBorders[i].GameObject.Active=visible;
            if(!visible)continue;int part=Categories[_category][item];_freeCardBorders[i].Color=part==_part?WorkshopGold:new(.22f,.22f,.19f,.6f);
            _cardImages[i].ImageReference=new("Assets/GarageUI/cutouts/"+BuilderPartFiles[part]+".png");_cardLabels[i].Text=BuilderPartNames[part];
        }
    }
    private string PartRole(string file)=>_catalog?.Standard==true?_catalog[file].Description+"\n"+StandardControls(_catalog[file].ReferenceId):file switch
    {
        "scrap_wheel_large" or "scrap_wheel_small"=>"Fits beam slots and axle hubs. A steering pivot turns its connected branch.",
        "scrap_axle_2m"=>"Carries wheel hubs. Connect through a steering joint to turn.",
        "scrap_steering_pivot"=>"Physically turns its connected branch with A / D. Optional: wheels can skid steer.",
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
        2 or 46=>"W/S motor  |  A/D differential steering  |  Space brake",
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
        _=>"Passive physical part. Its placement and orientation matter."
    };
    private void RefreshFreeHud()
    {
        if(_status==null||Assembly==null)return;
        _placeToolButton!.Color=!_eraseMode?new(.37f,.29f,.12f,1):new(.14f,.15f,.14f,1);_eraseToolButton!.Color=_eraseMode?new(.65f,.17f,.10f,1):new(.25f,.15f,.12f,1);
        _undoButton!.Interactable=_assemblyUndo.Count>0;_redoButton!.Interactable=_assemblyRedo.Count>0;
        bool engine=Assembly.Parts.Values.Any(p=>p.File=="scrap_engine_block"&&Assembly.IsConnected(p.Id)),cab=Assembly.Parts.Values.Any(p=>p.File=="scrap_cab_shell"&&Assembly.IsConnected(p.Id));
        _status.Text=_catalog?.Standard==true?(Building?"BUILD YOUR MACHINE\nNo engine or cab required":Speed.ToString("0.0")+" m/s"):Building?(Assembly.CanDrive?"READY TO DRIVE":"ASSEMBLY IN PROGRESS")+"\nEngine "+(engine?"connected":"missing")+"\nCab "+(cab?"connected":"missing"):(Velocity.Length()*3.6f).ToString("0")+" km/h"+(IsDrifting?"\nDRIFT":"");
        _status.Offset=Building?new(29,144):new(24,70);_status.WrapWidth=Building?145:280;
        _machineCount!.Text=Assembly.Parts.Count+(Assembly.Parts.Count==1?" part":" parts")+" / "+Assembly.TotalMass.ToString("0")+" kg";
        _selection!.Text=BuilderPartNames[_part];_partPurpose!.Text=PartRole(SelectedPart);
        _connectionFeedback!.Text=_eraseMode?_message:_candidateVisible?(_placementIssue==""?"Click to connect   |   R rotate   |   Tab attachment   |   F flip":_placementIssue):_message+"   |   "+Assembly.DriveRequirement;
        _connectionFeedback.Color=_candidateVisible&&_placementIssue!=""?new(.92f,.47f,.32f,1):new(.87f,.83f,.72f,1);
        _previewAction!.ImageReference=new("Assets/GarageUI/icons/"+(_placementIssue==""?"checkmark":"information")+".png");
        _hint!.Text=Building?"RMB orbit   MMB pan   Scroll zoom   M move   C copy   X erase   Ctrl+Z undo":"W/S drive   A/D steer   Shift drift   F weapons   G mechanisms";
        _hint.Offset=Building?new(696,702):new(530,682);
        _freeDriveLabel!.Text=Building?"TEST DRIVE  [B]":"BACK TO BUILD  [B]";_driveButton!.Label="";_driveButton.Interactable=!Building||Assembly.CanDrive;
    }
}

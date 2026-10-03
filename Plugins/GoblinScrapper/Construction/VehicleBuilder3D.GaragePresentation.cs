using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;

using ByteEngine.Core.Assets;

using ByteEngine.Core.Graphics;

using ByteEngine.Core.Graphics.ThreeD;

using ByteEngine.Core.Scene;



namespace GoblinScrapper.Construction;

public sealed partial class VehicleBuilder3D

{

    private static readonly int[][] LegacyCategories = [[11,12,13,14,4,5],[0,1,2,3,25,9,10],[6,7,16,17,18,19,20,21,22,23],[8,24,26,27]];

    private static readonly string[] LegacyCategoryNames=["STRUCTURE","DRIVE","WEAPONS","MECHANISMS"];

    private string[] BuilderPartFiles => _catalog?.Standard==true?_catalog.Parts.Keys.Where(f=>f!=VehicleAssembly.MasterBlock).ToArray():VehicleBuildLayout.PartFiles;

    private string[] BuilderPartNames => _catalog?.Standard==true?BuilderPartFiles.Select(f=>_catalog[f].Label).ToArray():VehicleBuildLayout.PartNames;

    private string[] CategoryNames => _catalog?.Standard==true?["STRUCTURE","DRIVE","WEAPONS","MECHANISMS","FLIGHT"]:LegacyCategoryNames;

    private int[][] Categories => _catalog?.Standard==true?CategoryNames.Select(g=>BuilderPartFiles.Select((f,i)=>(f,i)).Where(p=>(_catalog[p.f].Group=="Mechanics"?"MECHANISMS":_catalog[p.f].Group).Equals(g,StringComparison.OrdinalIgnoreCase)).Select(p=>p.i).ToArray()).ToArray():LegacyCategories;

    private readonly List<UiWidget> _cardImages=new();

    private readonly List<UiText> _cardLabels=new();

    private readonly List<UiWidget> _categoryButtons=new();

    private readonly Stack<string> _undo=new(),_redo=new();

    private bool _restoring;

    private int _category;

    private float _zoom=7.5f,_elevation=.58f;

    private UiWidget? _shortBase,_longBase,_undoButton,_redoButton;

    private UiText? _partDetails;

    private readonly List<GameObject> _axleBridges=new();

    public bool SetChassis(string chassis)

    {

        if(FreeBuilding)return ReplaceAssemblyRoot(chassis);

        if(!Building || chassis is not ("scrap_frame_2x1" or "scrap_frame_long") || Assets==null) return false;

        if(Layout.Chassis==chassis) return true;

        Assets.LoadModel(new AssetReference(PartsDirectory+"/"+chassis+".glb"));

        RecordEdit(); Layout.SetChassis(chassis);

        var previous=GameObject.Children.FirstOrDefault(o=>o.Name=="Chassis");

        if(previous!=null) GameObject.Scene!.DestroyGameObject(previous);

        CreateModelVisual(GameObject.Scene!,Assets,PartsDirectory+"/"+chassis+".glb",GameObject,"Chassis");

        foreach(var pair in _visuals) ApplyPlacement(pair.Value,pair.Key,Layout.Parts[pair.Key]);

        UpdateAxleBridges();

        _zoom=Layout.LongChassis ? 9 : 7.5f;

        RefreshGhost(); FollowRoofPayload(); RefreshHud();

        _message=Layout.LongChassis ? "Long chassis selected. All mounts and attached parts moved with it." : "Short chassis selected.";

        return true;

    }

    private void RecordEdit()

    {

        if(_restoring) return;

        // Bound history so imported model data is never retained by undo.

        if(_undo.Count>=64) _undo.Clear();

        _undo.Push(Layout.ToJson()); _redo.Clear();

    }

    private void RestoreLayout(VehicleBuildLayout layout)

    {

        _restoring=true;

        try

        {

            foreach(int mount in Layout.Parts.Keys.ToArray()) RemovePart(mount);

            SetChassis(layout.Chassis);

            foreach(var pair in layout.Parts) AttachPart(pair.Key,pair.Value);

            RefreshGhost(); FollowRoofPayload(); RefreshHud();

        }

        finally { _restoring=false; }

    }

    public bool UndoBuild()

    {

        if(FreeBuilding)return UndoAssembly(false);

        if(!Building || !_undo.TryPop(out var snapshot)) return false;

        _redo.Push(Layout.ToJson()); RestoreLayout(VehicleBuildLayout.FromJson(snapshot)); _message="Build change undone."; return true;

    }

    public bool RedoBuild()

    {

        if(FreeBuilding)return UndoAssembly(true);

        if(!Building || !_redo.TryPop(out var snapshot)) return false;

        _undo.Push(Layout.ToJson()); RestoreLayout(VehicleBuildLayout.FromJson(snapshot)); _message="Build change restored."; return true;

    }

    private bool PlaceAtSelected()

    {

        string issue=Layout.PlacementIssue(_mount,SelectedPart);

        if(issue=="") return AttachPart(_mount,SelectedPart);

        if(!issue.StartsWith("Mount occupied") || Layout.Parts[_mount]==SelectedPart) return false;

        Assets!.LoadModel(new AssetReference(PartsDirectory+"/"+SelectedPart+".glb"));

        RecordEdit(); _restoring=true;

        try { RemovePart(_mount); return AttachPart(_mount,SelectedPart); }

        finally { _restoring=false; }

    }

    private bool PointerOnHud()

    {

        Vector2 normalized=Input.GameViewPointerNormalized;

        // Match the scaled canvas, including letterboxing on taller displays.

        float scale=Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720);

        var insets=_canvas!.GetComponent<UiCanvas>()!.SafeAreaInsets;

        Vector2 point=normalized*Input.GameViewSize/Math.Max(.1f,scale)-new Vector2(insets.X,insets.Y);

        if(FreeBuilding)return point.Y<48 || point.Y>540 || (point.X<198 && point.Y<278);

        return point.Y<70 || point.Y>520 || (point.X<266 && point.Y<350) || (point.X>996 && point.Y<320);

    }

    internal void AdaptHudViewport(Vector2 size)

    {

        if(_canvas==null || size.X<160 || size.Y<90) return;

        float scale=Math.Min(size.X/1280,size.Y/720);

        Vector2 margin=Vector2.Max(Vector2.Zero,(size/scale-new Vector2(1280,720))*.5f);

        _canvas.GetComponent<UiCanvas>()!.SafeAreaInsets=new(margin.X,margin.Y,margin.X,margin.Y);

    }

    private void HoverMount()

    {

        var ray=_camera!.ScreenPointToRay(Input.GameViewPointerNormalized,Input.GameViewSize.X/Math.Max(1,Input.GameViewSize.Y));

        int hovered=-1; float nearest=float.MaxValue;

        for(int i=0;i<_markers.Length;i++)

        {

            if(!_markers[i].Active) continue;

            Vector3 delta=_markers[i].Transform.WorldPosition-ray.Origin;float distance=Vector3.Dot(delta,ray.Direction);

            if(distance>0 && distance<nearest && (delta-ray.Direction*distance).Length()<.27f) { nearest=distance;hovered=i; }

        }

        if(hovered>=0 && hovered!=_mount) { _mount=hovered;RefreshGhost(); }

    }

    private void TintGhost(string issue)

    {

        if(_ghost==null) return;

        Vector4 color=issue=="" ? new(.86f,.67f,.26f,.30f) : issue.StartsWith("Mount occupied") ? new(1,.68f,.2f,.40f) : new(1,.22f,.17f,.40f);

        void Visit(GameObject obj)

        { if(obj.GetComponent<MeshRenderer>() is {} mesh) mesh.Material.BaseColor=color; foreach(var child in obj.Children) Visit(child); }

        Visit(_ghost);

    }

    private void Panel(string name,Vector2 position,Vector2 size,bool buildOnly=false)

    {

        HudObject(name+" rim",buildOnly).AddComponent(new UiWidget {Offset=position-Vector2.One,Size=size+new Vector2(2),Color=new(.35f,.39f,.30f,.85f),OrderInLayer=1});

        HudObject(name,buildOnly).AddComponent(new UiWidget {Offset=position,Size=size,Color=new(.065f,.085f,.075f,.96f),OrderInLayer=2});

    }

    private void CreateHud()

    {

        _canvas=Own("Goblin Scraper garage HUD");_canvas.AddComponent(new UiCanvas {ReferenceResolution=new(1280,720)});

        Panel("Toolbar",new(0,0),new(1280,66));

        Text("Title","GOBLIN SCRAPER",new(22,19),22);

        Text("Workshop subtitle","THE SCRAPYARD",new(244,25),13);

        _undoButton=Button("Undo",new(422,15),new(68,35),()=>UndoBuild(),true);

        _redoButton=Button("Redo",new(496,15),new(68,35),()=>RedoBuild(),true);

        Button("Save",new(724,15),new(66,35),SaveBuild,false);

        Button("Load",new(796,15),new(66,35),LoadBuild,false);

        Button("Recover",new(868,15),new(84,35),ResetPosition,false);

        _driveButton=Button("TEST DRIVE  [B]",new(1044,12),new(212,42),ToggleDrive,false);

        Panel("Machine card",new(24,96),new(238,240),true);

        Text("Base heading","CHASSIS",new(40,112),13,true);

        _shortBase=Button("Short base",new(40,140),new(100,36),()=>SetChassis("scrap_frame_2x1"),true);

        _longBase=Button("Long base",new(146,140),new(100,36),()=>SetChassis("scrap_frame_long"),true);

        _status=Text("Vehicle status","",new(40,194),16);

        _status.WrapWidth=205;

        Text("Machine rule","Wheels OR tracks\nEngine + cab to drive",new(40,276),14,true);

        Panel("Selection card",new(1012,96),new(244,214),true);

        Text("Selection heading","SELECTED PART",new(1028,112),12,true);

        _selection=Text("Selected mount","",new(1028,140),18,true);_selection.WrapWidth=208;

        _partDetails=Text("Part effect","",new(1028,190),14,true);_partDetails.WrapWidth=208;

        Button("Place / replace",new(1028,251),new(132,35),PlaceSelected,true);

        Button("Remove",new(1166,251),new(74,35),RemoveSelected,true);

        Panel("Parts dock",new(24,532),new(1232,166),true);

        for(int c=0;c<4;c++)

        { int category=c; _categoryButtons.Add(Button(CategoryNames[c],new(40+c*162,544),new(154,30),()=>{_category=category;_palettePage=0;SelectPart(Categories[category][0]);},true)); }

        Button("<",new(1164,544),new(32,30),()=>ChangePalette(-1),true);

        Button(">",new(1202,544),new(32,30),()=>ChangePalette(1),true);

        for(int i=0;i<6;i++)

        {

            int slot=i;float x=40+i*198;

            _palette.Add(Button("",new(x,584),new(186,96),()=>{int index=_palettePage*6+slot;if(index<Categories[_category].Length) SelectPart(Categories[_category][index]);},true));

            _cardImages.Add(HudObject("Part thumbnail "+i,true).AddComponent(new UiWidget {Kind=UiWidgetKind.Image,Offset=new(x+43,588),Size=new(100,64),Color=Vector4.One,OrderInLayer=6}));

            _cardLabels.Add(Text("Part label "+i,"",new(x+12,658),14,true));

        }

        _hint=Text("Garage controls","",new(286,490),14);_hint.WrapWidth=700;

        RefreshPalette();

    }

    private void ChangePalette(int direction)

    {

        int pages=(Categories[_category].Length+5)/6;

        _palettePage=(_palettePage+direction+pages)%pages;SelectPart(Categories[_category][_palettePage*6]);

    }

    private void RefreshPalette()

    {

        if(FreeBuilding){RefreshFreePalette();return;}

        for(int c=0;c<_categoryButtons.Count;c++) _categoryButtons[c].Color=c==_category ? new(.31f,.43f,.26f,1) : new(.13f,.18f,.15f,1);

        for(int i=0;i<_palette.Count;i++)

        {

            int item=_palettePage*6+i;bool visible=item<Categories[_category].Length;

            _palette[i].GameObject.Active=visible;_cardImages[i].GameObject.Active=visible;_cardLabels[i].GameObject.Active=visible;

            if(!visible) continue;

            int part=Categories[_category][item];_palette[i].Color=part==_part ? new(.35f,.45f,.27f,1) : new(.13f,.18f,.15f,1);

            _cardImages[i].ImageReference=new AssetReference("Assets/GarageUI/parts/"+BuilderPartFiles[part]+".png");

            _cardLabels[i].Text=BuilderPartNames[part];

        }

    }

    private void RefreshHud()

    {

        if(FreeBuilding){RefreshFreeHud();return;}

        if(_status==null) return;

        _shortBase!.Color=Layout.LongChassis ? new(.13f,.18f,.15f,1) : new(.31f,.43f,.26f,1);

        _longBase!.Color=Layout.LongChassis ? new(.31f,.43f,.26f,1) : new(.13f,.18f,.15f,1);

        _undoButton!.Interactable=_undo.Count>0;_redoButton!.Interactable=_redo.Count>0;

        _status.Text=Building ? Layout.Parts.Count+" PARTS\n"+Layout.DriveRequirement : (Velocity.Length()*3.6f).ToString("0")+" km/h"+(IsDrifting ? "\nDRIFT "+DriftAngle.ToString("0")+" degrees" : "");

        _status.Offset=Building ? new(40,194) : new(24,92);_status.WrapWidth=Building ? 205 : 300;

        _selection!.Text=BuilderPartNames[_part];

        string issue=Layout.PlacementIssue(_mount,SelectedPart);

        _partDetails!.Text=Layout.ActiveMounts[_mount].Name+"\n"+(issue=="" ? "Empty mount - ready to place" : issue);

        _hint!.Text=Building ? _message+"\nMMB orbit  /  scroll zoom  /  click mount to place  /  Ctrl+Z undo" : "W/S drive  /  A/D steer  /  Shift drift  /  F weapons  /  G mechanisms";

        _hint.Offset=Building ? new(286,490) : new(24,662);

        _driveButton!.Label=Building ? "TEST DRIVE  [B]" : "BACK TO BUILD  [B]";

        _driveButton.Interactable=!Building || Layout.CanDrive;

    }

}

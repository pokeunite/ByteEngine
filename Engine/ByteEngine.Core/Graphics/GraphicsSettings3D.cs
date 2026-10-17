using System.Text.Json;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>Reusable runtime quality controls. Optional per-game preferences survive scene resets
/// and application restarts; no quality setting changes physics or simulation frequency.</summary>
public sealed class GraphicsSettings3D : Component
{
    public string SettingsKey { get; set; } = "";
    public GraphicsQuality Quality { get; set; } = GraphicsQuality.High;
    private float scale=1;
    public float RenderScale { get=>scale; set=>scale=Math.Clamp(float.IsFinite(value)?value:1,.5f,1); }
    public bool PreservePostEffects { get; set; }
    public string LastError { get; private set; } = "";
    SkyEnvironment? sky;GraphicsQuality? applied;float appliedScale;string? path;
    record Preferences(GraphicsQuality Quality,float RenderScale);
    protected override void OnStart()
    {
        if(OperatingSystem.IsBrowser())Quality=BrowserPresentationSettings.Quality;
        sky=GameObject.Scene!.Environments.FirstOrDefault(e=>e.Enabled&&e.GameObject.ActiveInHierarchy);
        if(SettingsKey.Length>0){
            string key=new(SettingsKey.Where(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_').ToArray());
            if(key.Length>0){path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ByteEngine","Graphics",key+".json");
                try{if(File.Exists(path)&&JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) is {} p&&Enum.IsDefined(p.Quality)){Quality=p.Quality;RenderScale=p.RenderScale;}}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){LastError=ex.Message;}
            }
        }
        Apply(false);
    }
    protected override void OnUpdate(){if(applied!=Quality||appliedScale!=RenderScale)Apply(true);}
    void Apply(bool save)
    {
        if(!Enum.IsDefined(Quality))Quality=GraphicsQuality.High;
        if(sky!=null){float ao=sky.AmbientOcclusion,bloom=sky.Bloom;sky.Quality=Quality;if(PreservePostEffects){sky.AmbientOcclusion=ao;sky.Bloom=bloom;}sky.RenderScale=RenderScale;}
        if(OperatingSystem.IsBrowser()){BrowserPresentationSettings.Quality=Quality;if(RenderScale<.99f||(applied.HasValue&&appliedScale!=RenderScale))BrowserPresentationSettings.WorldResolution=RenderScale<.63f?"50%":RenderScale<.9f?"75%":"Auto";}
        applied=Quality;appliedScale=RenderScale;
        if(save&&path!=null)try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new Preferences(Quality,RenderScale)));File.Move(temp,path,true);LastError="";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){LastError=ex.Message;}
    }
}


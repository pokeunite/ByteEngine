using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class BrowserFrame
{
    public BrowserSky? sky { get; set; }
    public float exposure { get; set; } = 1;
    public int pendingTextures { get; set; }
    public double loopMs { get; set; }
    public float ambient { get; set; }
    public float[] vp { get; set; } = [];
    public float[] eye { get; set; } = [];
    public float[] background { get; set; } = [];
    public BrowserLight[] lights { get; set; } = [];
    public BrowserLight[] points { get; set; } = [];
    public List<BrowserMeshUpload> uploads { get; set; } = [];
    public List<BrowserTextureUpload> textures { get; set; } = [];
    public List<BrowserDraw> draws { get; set; } = [];
    public List<BrowserUi> ui { get; set; } = [];
    public int[] deadMeshes { get; set; } = [];
    public int[] deadTextures { get; set; } = [];
    public JsonElement audio { get; set; }
}

internal sealed class BrowserSky
{
 public int texture { get; set; }
 public float[] inverseVp { get; set; } = [];
 public float rotation { get; set; }
 public float intensity { get; set; }
 public bool hdr { get; set; }
 public float[] zenith { get; set; } = [];
 public float[] horizon { get; set; } = [];
 public float[] ground { get; set; } = [];
 public float sharpness { get; set; }
}
internal sealed class BrowserLight
{
    public float[] direction { get; set; } = [];
    public float[] color { get; set; } = [];
    public float[] position { get; set; } = [];
    public float range { get; set; }
}

internal sealed class BrowserMeshUpload
{
    public int id { get; set; }
    public int vertices { get; set; }
    public int indices { get; set; }
}

internal sealed class BrowserTextureUpload
{
    public string source { get; set; } = "";
    public int id { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int w { get; set; }
    public int h { get; set; }
    public int pixels { get; set; }
    public bool allocate { get; set; }
    public bool partial { get; set; }
    public bool nearest { get; set; }
    public bool hdr { get; set; }
}

internal sealed class BrowserDraw
{
    public float[] instances {get;set;}=[];
    public int id { get; set; }
    public int heightField { get; set; }
    public int texture { get; set; }
    public int normal { get; set; }
    public int metallicTexture { get; set; }
    public int roughnessTexture { get; set; }
    public int aoTexture { get; set; }
    public int packedTexture { get; set; }
    public int emissionTexture { get; set; }
    public float[] model { get; set; } = [];
    public float[] color { get; set; } = [];
    public float[] heightUv { get; set; } = [];
    public float[] heightSize { get; set; } = [];
    public float[] emission { get; set; } = [];
    public float[] tiling { get; set; } = [];
    public float[] offset { get; set; } = [];
    public float heightRange { get; set; }
    public float heightBias { get; set; }
    public float normalStrength { get; set; }
    public float metallic { get; set; }
    public float roughness { get; set; }
    public float ao { get; set; }
    public float cutoff { get; set; }
    public int[] channels { get; set; } = [];
    public bool directX { get; set; }
    public bool packed { get; set; }
    public bool unlit { get; set; }
    public bool srgb { get; set; }
    public bool depth { get; set; }
    public bool write { get; set; }
    public string blend { get; set; } = "";
    public string cull { get; set; } = "";
    public string front { get; set; } = "";
}

internal sealed class BrowserUi
{
    public string kind { get; set; } = "";
    public int texture { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public float w { get; set; }
    public float h { get; set; }
    public float sx { get; set; }
    public float sy { get; set; }
    public float sw { get; set; }
    public float sh { get; set; }
    public float[] color { get; set; } = [];
}

[JsonSerializable(typeof(BrowserFrame))]
internal partial class BrowserJsonContext : JsonSerializerContext { }

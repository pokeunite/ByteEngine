using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public enum SpeedUnit { KilometresPerHour, MilesPerHour, MetresPerSecond }

/// <summary>Reusable speed readout with optional authored image frames. Input is always metres/second.</summary>
public sealed class Speedometer : Component
{
    public SpeedUnit Unit { get; set; }
    public float MaximumSpeed { get; set; } = 120;
    public string DialPathPrefix { get; set; } = "";
    public string DialPathSuffix { get; set; } = ".png";
    public int DialFrameCount { get; set; } = 61;
    public string ReadoutObject { get; set; } = "";
    public string TargetObject { get; set; } = "";
    public bool MeasureTargetMotion { get; set; }
    public int DecimalPlaces { get; set; }
    private float _metresPerSecond;
    private Vector3? _previous;
    private int _frame = -1;
    public void SetSpeed(float metresPerSecond) => _metresPerSecond = float.IsFinite(metresPerSecond) ? Math.Abs(metresPerSecond) : 0;
    public float DisplaySpeed => _metresPerSecond * (Unit == SpeedUnit.KilometresPerHour ? 3.6f : Unit == SpeedUnit.MilesPerHour ? 2.2369363f : 1);
    public override int UpdateOrder => 95;
    protected override void OnUpdate()
    {
        if (MeasureTargetMotion)
        {
            var target = GameObject.Scene?.FindGameObject(TargetObject);
            if (target == null) { _previous = null; SetSpeed(0); }
            else { var position = target.Transform.WorldPosition; if (_previous is {} last && Time.DeltaTime > 0) SetSpeed(Vector3.Distance(position, last) / (float)Time.DeltaTime); _previous = position; }
        }
        Refresh();
    }
    public void Refresh()
    {
        var text = ReadoutObject.Length == 0 ? GameObject.GetComponent<UiText>() : GameObject.Scene?.FindGameObject(ReadoutObject)?.GetComponent<UiText>();
        if (text != null) text.Text = DisplaySpeed.ToString("F" + Math.Clamp(DecimalPlaces, 0, 3), System.Globalization.CultureInfo.InvariantCulture) + (Unit == SpeedUnit.KilometresPerHour ? " km/h" : Unit == SpeedUnit.MilesPerHour ? " mph" : " m/s");
        int frame = (int)MathF.Round(Math.Clamp(DisplaySpeed / Math.Max(.001f, MaximumSpeed), 0, 1) * (Math.Clamp(DialFrameCount, 1, 10000) - 1));
        if (frame != _frame && DialPathPrefix.Length > 0 && GameObject.GetComponent<UiWidget>() is {} dial)
        { dial.ImageReference = new AssetReference(DialPathPrefix + frame.ToString("00") + DialPathSuffix); _frame = frame; }
    }
    protected override void OnStop() { _previous = null; _frame = -1; }
}

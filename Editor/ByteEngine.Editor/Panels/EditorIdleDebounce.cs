namespace ByteEngine.Editor.Panels;

internal sealed class EditorIdleDebounce
{
    private double _idleSince = double.NaN;
    public bool Ready(double now, bool dirty, bool interacting)
    {
        if (!dirty || interacting)
        {
            _idleSince = double.NaN;
            return false;
        }
        if (double.IsNaN(_idleSince)) _idleSince = now;
        return now - _idleSince >= .5;
    }
}

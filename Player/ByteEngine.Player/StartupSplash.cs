using System.Reflection;
namespace ByteEngine.Player;

// A separate UI thread keeps the embedded animation moving while content loads.
internal sealed class StartupSplash : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private Form? _window;
    private int _disposed;
    public StartupSplash()
    {
        _thread = new Thread(Show) { IsBackground = true, Name = "ByteEngine startup" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }
    private void Show()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ByteEngine.Startup.gif")!;
            using var image = Image.FromStream(stream);
            using var window = new Form
            {
                FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterScreen,
                ClientSize = new Size(640, 360), BackColor = Color.FromArgb(16,22,30),
                ShowInTaskbar = false, Text = "ByteEngine"
            };
            window.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = image, SizeMode = PictureBoxSizeMode.Zoom });
            _window = window;
            window.Shown += (_, _) => _ready.Set();
            Application.Run(window);
        }
        catch (Exception error) { ByteEngine.Core.Diagnostics.CrashDebugLog.Write("Startup animation unavailable: " + error.Message); }
        finally { _ready.Set(); _window = null; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        var window = _window;
        try { if (window is { IsDisposed: false, IsHandleCreated: true }) window.BeginInvoke(() => window.Close()); }
        catch (InvalidOperationException) { }
        if (Thread.CurrentThread != _thread) _thread.Join(2000);
        _ready.Dispose();
    }
}

using System.Drawing;
using System.Windows.Forms;

using ByteEngine.Core.Runtime;

namespace ByteEngine.Player;

internal sealed class VideoRecorderDialog : Form
{
    internal VideoRecorderDialog(VideoRecorder recorder)
    {
        Text = "Video Recorder";
        ClientSize = new(590, 375);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        var help = new Label { Text = "F9: start / stop recording     Shift+F9: settings\nMP4 / H.264 • 30 fps • up to 1080p • AAC playback audio", Location = new(16, 12), Size = new(555, 44) };
        var folder = new TextBox { Text = recorder.Settings.OutputDirectory, Location = new(16, 78), Width = 555, ReadOnly = true };
        var browse = new Button { Text = "Choose folder…", Location = new(16, 110), Width = 150 };
        var defaults = new Button { Text = "Windows Videos", Location = new(176, 110), Width = 150 };
        var quality = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new(110, 151), Width = 330 };
        foreach (var value in Enum.GetValues<VideoRecordingQuality>()) quality.Items.Add(VideoRecordingProfile.For(value).Label);
        quality.SelectedIndex = (int)recorder.Settings.Quality;
        var qualityHint = new Label { Text = "Higher quality uses more space. Resolution follows the rendered view.", Location = new(16, 182), Size = new(555, 28) };
        var status = new Label { Text = recorder.Message, Location = new(16, 226), Size = new(555, 42) };
        var audio = new CheckBox { Text = "Record default Windows playback audio (includes other apps; no microphone)", Checked = recorder.Settings.IncludeAudio, Location = new(16, 279), Size = new(555, 28) };
        var done = new Button { Text = "Done", DialogResult = DialogResult.OK, Location = new(466, 334), Width = 105 };
        browse.Enabled = defaults.Enabled = !recorder.IsActive && recorder.State != VideoRecorderState.Saving;
        audio.Enabled = browse.Enabled;
        quality.Enabled = browse.Enabled;
        if (!browse.Enabled) status.Text = "Stop recording and wait for saving before changing the folder.";
        void SaveFolder(string path)
        {
            status.Text = recorder.Settings.TrySave(path, out string error) ? "Output folder saved." : "Folder error: " + error;
            folder.Text = recorder.Settings.OutputDirectory;
        }
        browse.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = "Video output folder", UseDescriptionForTitle = true, SelectedPath = folder.Text };
            if (picker.ShowDialog(this) == DialogResult.OK) SaveFolder(picker.SelectedPath);
        };
        defaults.Click += (_, _) => SaveFolder(VideoRecorderSettings.DefaultOutputDirectory);
        bool restoringQuality = false;
        quality.SelectedIndexChanged += (_, _) =>
        {
            if (restoringQuality) return;
            var previous = recorder.Settings.Quality;
            recorder.Settings.Quality = (VideoRecordingQuality)quality.SelectedIndex;
            if (!recorder.Settings.TrySave(recorder.Settings.OutputDirectory, out string error))
            {
                recorder.Settings.Quality = previous;
                restoringQuality = true;
                quality.SelectedIndex = (int)previous;
                restoringQuality = false;
                status.Text = "Settings error: " + error;
            }
        };
        audio.CheckedChanged += (_, _) =>
        {
            recorder.Settings.IncludeAudio = audio.Checked;
            if (!recorder.Settings.TrySave(recorder.Settings.OutputDirectory, out string error)) status.Text = "Settings error: " + error;
        };
        Controls.AddRange([help, new Label { Text = "Output folder", Location = new(16, 57), AutoSize = true }, folder, browse, defaults,
            new Label { Text = "Video quality", Location = new(16, 154), AutoSize = true }, quality, qualityHint, status, audio, done]);
        AcceptButton = CancelButton = done;
    }
}

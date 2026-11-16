using System.Diagnostics;
using System.Windows.Forms;

using ByteEngine.Core.Runtime;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class VideoRecorderPanel
{
    internal bool IsOpen;
    private string _error = string.Empty;
    private static readonly string[] QualityLabels = Enum.GetValues<VideoRecordingQuality>().Select(value => VideoRecordingProfile.For(value).Label).ToArray();

    internal void DrawMenu(VideoRecorder recorder, Action toggle)
    {
        if (!ImGui.BeginMenu("Recorder")) return;
        if (ImGui.MenuItem(recorder.IsActive ? "Stop recording" : "Start recording", "F9", false, recorder.State != VideoRecorderState.Saving)) toggle();
        if (ImGui.MenuItem("Recorder settings", "Shift+F9")) IsOpen = true;
        ImGui.TextDisabled(recorder.State.ToString());
        ImGui.EndMenu();
    }

    internal void Draw(VideoRecorder recorder, Action toggle)
    {
        if (!IsOpen) return;
        ImGui.SetNextWindowSize(new(640, 450), ImGuiCond.FirstUseEver);
        bool visible = ImGui.Begin("Video Recorder", ref IsOpen);
        if (visible)
        {
            ImGui.Text("Game View video | MP4 / H.264 | up to 1080p | 30 fps");
            ImGui.TextDisabled("F9 starts/stops; Shift+F9 opens these settings.");
            ImGui.TextWrapped("Audio captures the default Windows playback device, including other apps. Microphone is not recorded.");
            ImGui.Separator();
            ImGui.Text("Output folder");
            ImGui.TextWrapped(recorder.Settings.OutputDirectory);
            ImGui.BeginDisabled(recorder.IsActive || recorder.State == VideoRecorderState.Saving);
            int quality = (int)recorder.Settings.Quality;
            ImGui.SetNextItemWidth(330);
            if (ImGui.Combo("Video quality", ref quality, QualityLabels, QualityLabels.Length))
            {
                var previous = recorder.Settings.Quality;
                recorder.Settings.Quality = (VideoRecordingQuality)quality;
                if (!recorder.Settings.TrySave(recorder.Settings.OutputDirectory, out _error)) recorder.Settings.Quality = previous;
            }
            ImGui.TextWrapped("Higher quality uses more space. Game View records at project output resolution, capped by the preset, even when the panel is small.");
            bool audio = recorder.Settings.IncludeAudio;
            if (ImGui.Checkbox("Record playback audio (AAC)", ref audio))
            {
                recorder.Settings.IncludeAudio = audio;
                if (!recorder.Settings.TrySave(recorder.Settings.OutputDirectory, out _error)) recorder.Settings.IncludeAudio = !audio;
            }
            if (ImGui.Button("Choose folder..."))
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "Choose video recording output folder", UseDescriptionForTitle = true,
                    SelectedPath = recorder.Settings.OutputDirectory, ShowNewFolderButton = true
                };
                if (dialog.ShowDialog() == DialogResult.OK) recorder.Settings.TrySave(dialog.SelectedPath, out _error);
            }
            ImGui.SameLine();
            if (ImGui.Button("Use Windows Videos")) recorder.Settings.TrySave(VideoRecorderSettings.DefaultOutputDirectory, out _error);
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Open folder"))
            {
                try
                {
                    Directory.CreateDirectory(recorder.Settings.OutputDirectory);
                    Process.Start(new ProcessStartInfo(recorder.Settings.OutputDirectory) { UseShellExecute = true });
                }
                catch (Exception e) { _error = e.Message; }
            }
            ImGui.Spacing();
            ImGui.BeginDisabled(recorder.State == VideoRecorderState.Saving);
            if (ImGui.Button(recorder.IsActive ? "Stop recording (F9)" : "Start recording (F9)")) toggle();
            ImGui.EndDisabled();
            ImGui.TextWrapped(recorder.Message);
            if (recorder.IsActive) ImGui.Text($"{recorder.Elapsed:hh\\:mm\\:ss} | {recorder.Width} x {recorder.Height} | skipped frames: {recorder.DroppedFrames}");
            if (_error.Length > 0) ImGui.TextWrapped("Folder error: " + _error);
        }
        ImGui.End();
    }
}

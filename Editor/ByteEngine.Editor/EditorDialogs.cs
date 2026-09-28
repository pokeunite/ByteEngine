using System.Windows.Forms;

namespace ByteEngine.Editor;

internal static class EditorDialogs
{
    public static string? ChooseProjectDirectory(string? initialDirectory = null)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Choose where ByteEngine should create the project folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    public static string? ChooseNewProject()
    {
        using SaveFileDialog dialog =
            new()
            {
                Title =
                    "Create ByteEngine Project",
                Filter =
                    "ByteEngine Project (*.byteproject)|*.byteproject",
                DefaultExt =
                    "byteproject",
                AddExtension =
                    true,
                FileName =
                    "MyGame.byteproject",
                InitialDirectory =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments
                    )
            };

        return dialog.ShowDialog() ==
               DialogResult.OK
            ? dialog.FileName
            : null;
    }

    public static string? ChooseProject()
    {
        using OpenFileDialog dialog =
            new()
            {
                Title =
                    "Open ByteEngine Project",
                Filter =
                    "ByteEngine Project (*.byteproject)|*.byteproject",
                CheckFileExists =
                    true
            };

        return dialog.ShowDialog() ==
               DialogResult.OK
            ? dialog.FileName
            : null;
    }

    public static string[] ChooseGlbFiles()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Import glTF Binary Models",
            Filter = "glTF Binary Models (*.glb)|*.glb",
            Multiselect = true,
            CheckFileExists = true
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : Array.Empty<string>();
    }

    public static string? ChooseGltfPackageFolder()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Import a glTF package folder (all files): include the .gltf, .bin buffers and textures",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    public static string[] ChooseFontFiles()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Import Font Files",
            Filter = "Font Files (*.ttf;*.otf;*.fnt)|*.ttf;*.otf;*.fnt|All Files (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : Array.Empty<string>();
    }

    public static string? ChooseScene(
        string initialDirectory)
    {
        using OpenFileDialog dialog =
            new()
            {
                Title =
                    "Open ByteEngine Scene",
                Filter =
                    "ByteEngine Scene (*.bytescene)|*.bytescene",
                CheckFileExists =
                    true,
                InitialDirectory =
                    initialDirectory
            };

        return dialog.ShowDialog() ==
               DialogResult.OK
            ? dialog.FileName
            : null;
    }

    public static string? ChooseSceneSavePath(
        string initialDirectory,
        string suggestedName)
    {
        using SaveFileDialog dialog =
            new()
            {
                Title =
                    "Save ByteEngine Scene",
                Filter =
                    "ByteEngine Scene (*.bytescene)|*.bytescene",
                DefaultExt =
                    "bytescene",
                AddExtension =
                    true,
                InitialDirectory =
                    initialDirectory,
                FileName =
                    suggestedName
            };

        return dialog.ShowDialog() ==
               DialogResult.OK
            ? dialog.FileName
            : null;
    }

    public static void ShowError(
        string title,
        string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        );
    }
}

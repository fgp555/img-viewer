using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace IMGViewer;

public partial class CaptureSettingsWindow : Window
{
    public CaptureSettingsWindow()
    {
        InitializeComponent();
        FolderPath.Text = CapturePreferences.CaptureFolder;
    }

    private void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Seleccionar carpeta de capturas" };
        if (Directory.Exists(FolderPath.Text))
        {
            dialog.InitialDirectory = FolderPath.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            UpdateFolder(dialog.FolderName);
        }
    }

    private void ResetFolder_Click(object sender, RoutedEventArgs e) => UpdateFolder(null);

    private void UpdateFolder(string? folder)
    {
        try
        {
            CapturePreferences.SetFolder(folder);
            FolderPath.Text = CapturePreferences.CaptureFolder;
            SettingsStatus.Text = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SettingsStatus.Text = "No se pudo guardar la carpeta de capturas.";
        }
    }
}

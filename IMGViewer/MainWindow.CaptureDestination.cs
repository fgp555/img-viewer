using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
namespace IMGViewer;
public partial class MainWindow
{
    private string? captureExportPath;
    private string? destinationFile;
    private string destinationFolder = "";
    private void UpdateCaptureDestination()
    {
        if (CaptureDestinationText is null) return;
        destinationFile = captureExportPath ?? CurrentGridImage;
        destinationFolder = destinationFile is null ? CapturePreferences.CaptureFolder : Path.GetDirectoryName(destinationFile)!;
        bool defaultFolder = StringComparer.OrdinalIgnoreCase.Equals(Path.TrimEndingDirectorySeparator(destinationFolder),
            Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));
        string displayFolder = defaultFolder ? "Imágenes" : destinationFolder;
        if (destinationFile is null)
        {
            CaptureDestinationText.Text = $"Destino: {displayFolder} · {CapturePreferences.FormatExtension.TrimStart('.').ToUpperInvariant()}";
            CaptureDestinationButton.ToolTip = $"{(defaultFolder ? "Carpeta predeterminada" : "Carpeta personalizada")}: {destinationFolder}\nFormato: {CapturePreferences.FormatExtension.TrimStart('.').ToUpperInvariant()}\nClic para abrir la carpeta. Guardar o Ctrl + S permite confirmar la ubicación.";
        }
        else
        {
            string label = activeCapture is not null || captureExportPath is not null ? "Guardada" : "Ubicación";
            CaptureDestinationText.Text = $"{label}: {Path.Combine(displayFolder,Path.GetFileName(destinationFile))}";
            CaptureDestinationButton.ToolTip = $"{destinationFile}\nClic para abrir la carpeta y seleccionar el archivo.";
        }
        ImageInfo.Foreground = HasUnsavedCapture ? Brushes.Orange : Brushes.LightGray;
    }
    private void CaptureDestination_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (destinationFile is not null && File.Exists(destinationFile))
            {
                var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                info.ArgumentList.Add("/select,"); info.ArgumentList.Add(destinationFile);
                Process.Start(info);
            }
            else if (Directory.Exists(destinationFolder))
                Process.Start(new ProcessStartInfo(destinationFolder) { UseShellExecute = true });
            else StatusText.Text = "La carpeta de destino todavía no existe. Se creará al guardar.";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        { StatusText.Text = "No se pudo abrir la ubicación de guardado."; }
    }
}

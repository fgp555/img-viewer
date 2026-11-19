using System.Windows.Controls;
namespace IMGViewer;
public partial class CaptureSettingsWindow
{
    private void FloatingCaptureEnabled_Click(object sender,System.Windows.RoutedEventArgs e)
    {
        try {CapturePreferences.ShowFloatingCaptureButton=FloatingCaptureEnabled.IsChecked==true;(Owner as MainWindow)?.RefreshFloatingCaptureButton();}
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {FloatingCaptureEnabled.IsChecked=CapturePreferences.ShowFloatingCaptureButton;SettingsStatus.Text="No se pudo guardar la opción del botón flotante.";}
    }
    private void CaptureResultPicker_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(CaptureResultPicker.SelectedIndex<0)return;
        try {CapturePreferences.ShowThumbnail=CaptureResultPicker.SelectedIndex==0;}
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException) {SettingsStatus.Text="No se pudo guardar la opción.";}
    }
    private void CaptureThumbnailLocation_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(CaptureThumbnailLocation.SelectedIndex<0)return;
        try {CapturePreferences.ThumbnailOnPrimaryMonitor=CaptureThumbnailLocation.SelectedIndex==0;}
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException) {SettingsStatus.Text="No se pudo guardar la ubicación de la miniatura.";}
    }
    private void CaptureThumbnailDelay_SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(CaptureThumbnailDelay.SelectedItem is not int seconds)return;
        try {CapturePreferences.ThumbnailSeconds=seconds;}
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException) {SettingsStatus.Text="No se pudo guardar la duración.";}
    }
}

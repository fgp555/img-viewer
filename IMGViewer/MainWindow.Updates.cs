using IMGViewer.Services.Updates;
using System.Windows;
namespace IMGViewer;
public partial class MainWindow
{
    internal void BindUpdates(UpdateCoordinator coordinator) => UpdateNoticeButton.DataContext = coordinator;
    private void UpdateNotice_Click(object sender,RoutedEventArgs e)
    {
        var settings = new CaptureSettingsWindow { Owner = this };
        settings.ShowUpdates(); settings.ShowDialog();
    }
    internal bool ConfirmUpdateRestart() =>
        !(HasPendingChanges || inlineCropPending || captureHistory.Count > 0) ||
        ViewerDialog.Show(this,"La aplicación se reiniciará. Las capturas pendientes se conservarán como borradores. Los cambios sin guardar en otras imágenes se perderán. ¿Continuar?","Actualizar IMG Viewer",MessageBoxButton.YesNo,defaultResult:MessageBoxResult.No) == MessageBoxResult.Yes;
}

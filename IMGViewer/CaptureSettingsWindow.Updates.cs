using System.Windows;
using IMGViewer.Services.Updates;
namespace IMGViewer;
public partial class CaptureSettingsWindow
{
    internal void ShowUpdates() => SettingsNavigation.SelectedIndex = 5;
    private async void UpdateAction_Click(object sender,RoutedEventArgs e)
    {
        if(Application.Current is not App app) return;
        if(app.Updates.State == UpdateState.Ready && Owner is MainWindow viewer && !viewer.ConfirmUpdateRestart()) return;
        if(await app.Updates.PerformAction()) app.ExitViewer();
    }
    private void CancelUpdate_Click(object sender,RoutedEventArgs e) => (Application.Current as App)?.Updates.Cancel();
}

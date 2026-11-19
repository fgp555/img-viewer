using System.Windows;
namespace IMGViewer;
public partial class MainWindow
{
    private FloatingCaptureButtonWindow? floatingCaptureButton;
    private bool floatingCaptureShuttingDown;
    internal void RefreshFloatingCaptureButton()
    {
        if(floatingCaptureShuttingDown||Dispatcher.HasShutdownStarted)return;
        try
        {
            if(!CapturePreferences.ShowFloatingCaptureButton)
            {floatingCaptureButton?.Close();floatingCaptureButton=null;return;}
            if(floatingCaptureButton is null)
            {
                var button=new FloatingCaptureButtonWindow(CaptureScreen,()=>{
                    if(Application.Current is App app)app.ShowViewer();else{Show();Activate();}
                },()=>{
                    try {CapturePreferences.ShowFloatingCaptureButton=false;RefreshFloatingCaptureButton();}
                    catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException){StatusText.Text="No se pudo ocultar el botón de captura.";}
                },CapturePreferences.FloatingCaptureButtonPosition,point=>CapturePreferences.FloatingCaptureButtonPosition=point,Resources);
                floatingCaptureButton=button;
                button.Closed+=(_,_)=>{if(ReferenceEquals(floatingCaptureButton,button))floatingCaptureButton=null;};
            }
            if(!captureInProgress&&!floatingCaptureButton.IsVisible)floatingCaptureButton.Show();
        }
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {StatusText.Text="No se pudo actualizar el botón flotante de captura.";}
    }
    private void CloseFloatingCaptureButton()
    {
        floatingCaptureShuttingDown=true;floatingCaptureButton?.Close();floatingCaptureButton=null;
    }
}

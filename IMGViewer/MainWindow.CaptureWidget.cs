using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private CaptureThumbnailWindow? captureWidget;
    private SessionCapture? widgetCapture;
    private void ShowCaptureWidget(BitmapSource bitmap, DateTime timestamp, Point captureOrigin)
    {
        var capture = new SessionCapture { Name="Captura "+timestamp.ToString("HH:mm:ss"), Created=timestamp, Photo=bitmap, Preview=bitmap, SavedPhoto=bitmap, RotationBase=bitmap };
        captureHistory.Insert(0,capture);
        PersistCaptureDraft(capture);
        CaptureHistoryList.ItemsSource=captureHistory;
        string SaveCapture()
        {
            if(capture.FilePath is { } saved && File.Exists(saved)) return saved;
            string path=SaveCaptureFile(capture.Preview,CapturePreferences.CaptureFolder,timestamp);
            capture.FilePath=path;
            DeleteCaptureDraft(capture,false);
            if(ReferenceEquals(activeCapture,capture)) { folderImages=[path];currentIndex=0;captureExportPath=path;UpdateMetadataFooter(); }
            return path;
        }
        string? autoError=null;
        if(CapturePreferences.AutoSave)
        {
            try { SaveCapture(); }
            catch(Exception ex) when(IsBatchImageError(ex)) { autoError="No se pudo guardar automÃ¡ticamente."; }
        }
        widgetCapture=capture;
        captureWidget=new CaptureThumbnailWindow(bitmap,SaveCapture,()=> {
            ShowSessionCapture(capture);
            historyView=true;galleryOpen=true;UpdateHistoryView();
            viewerBackgroundMode=0;UpdateBackgroundToggle();
            PhotoViewport.Background=(System.Windows.Media.Brush)FindResource("ViewerBackground");
            SetZenMode(false);toolsOpen=true;ArrowTool.IsChecked=true;ApplyWorkspaceLayout();EnableInlineCrop();
            if(Application.Current is App app) app.ShowViewer(); else { Show();Activate(); }
        }, autoError, captureOrigin);
        var widget=captureWidget;
        EventHandler closeWithViewer=(_,_)=>widget.Close();
        widget.Closed+=(_,_)=>{Closed-=closeWithViewer;if(ReferenceEquals(captureWidget,widget)){captureWidget=null;widgetCapture=null;}};
        Closed+=closeWithViewer;
        widget.Show();
    }
}

using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace IMGViewer;
public partial class MainWindow
{
    private int imageOpenVersion;
    private bool deferImageDetails;
    internal async void OpenExplorerImage(string path)
    {
        int version=++imageOpenVersion;
        deferImageDetails=false;
        var elapsed=Stopwatch.StartNew();
        try
        {
            path=Path.GetFullPath(path);
            if(!IsSupportedImage(path)) { StatusText.Text="Formato de imagen no habilitado."; return; }
            BitmapSource bitmap=await DecodeExplorerImage(path);
            if(version!=imageOpenVersion) return;
            Debug.WriteLine($"IMGViewer open: decode {elapsed.ElapsedMilliseconds} ms");
            RememberActiveCapture(); activeCapture=null; captureExportPath=null;
            deferImageDetails=true;
            ApplyDecodedPhoto(path,bitmap);
            CompletePhotoOpen(path,true,[path]);
            // Render the selected image before enumerating the folder or opening EXIF streams.
            await Dispatcher.InvokeAsync(() => { },DispatcherPriority.ApplicationIdle);
            if(version!=imageOpenVersion) return;
            Debug.WriteLine($"IMGViewer open: image presented {elapsed.ElapsedMilliseconds} ms");
            var details=await Task.Run(() => {
                var files=GetFolderImages(path);
                int index=Array.FindIndex(files,file=>StringComparer.OrdinalIgnoreCase.Equals(file,path));
                string info=DescribePhotoFile(path,$"{index+1} / {files.Length}",bitmap);
                return (Files:files,Index:index,Info:info);
            });
            if(version!=imageOpenVersion) return;
            folderImages=details.Files; currentIndex=details.Index;
            deferImageDetails=false;
            ImageInfo.Text=details.Info;
            UpdateCaptureDestination(); UpdateToolbarActions(); UpdateSlideShowAvailability();
            RefreshGallery();
            Debug.WriteLine($"IMGViewer open: details ready {elapsed.ElapsedMilliseconds} ms");
        }
        catch(Exception ex) when(IsBatchImageError(ex) || ex is System.Security.SecurityException)
        {
            if(version!=imageOpenVersion) return;
            deferImageDetails=false;
            StatusText.Text=$"No se pudo abrir {Path.GetFileName(path)}.";
            Debug.WriteLine($"IMGViewer open failed: {ex.Message}");
        }
    }
    private static Task<BitmapSource> DecodeExplorerImage(string path,int maximumSide=0)
    {
        if(!IsSvg(path)) return Task.Run(() => ReadPhotoBitmap(path,maximumSide));
        // SVG rendering uses WPF visuals: keep it on a worker STA, then transfer a frozen bitmap.
        var result=new TaskCompletionSource<BitmapSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker=new Thread(() => {
            try { result.SetResult(ReadPhotoBitmap(path,maximumSide)); }
            catch(Exception ex) { result.SetException(ex); }
        }) { IsBackground=true, Name="IMGViewer SVG decode" };
        worker.SetApartmentState(ApartmentState.STA); worker.Start();
        return result.Task;
    }
}

using System.IO;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private bool quickWebPInProgress;
    private async void QuickConvertToWebP()
    {
        if(quickWebPInProgress || gridBatchInProgress)return;
        bool grid=IsFolderGridOpen;
        string[] paths=grid?SelectedGridPaths():[];
        if(grid && paths.Length==0 || !grid && Photo.Source is not BitmapSource)return;
        EndInlineText(true);
        if(inlineCropPending){ApplyInlineCrop();if(inlineCropPending)return;}
        string? current=CurrentGridImage;
        BitmapSource? edited=Photo.Source is BitmapSource photo && (!grid || HasPendingChanges)
            ?FrozenDraftBitmap(FlattenMarkers(photo)):null;
        string fallbackFolder=CapturePreferences.CaptureFolder;
        string captureName="screen-"+(activeCapture?.Created??DateTime.Now).ToString("yyMMdd-HHmmss")+".webp";
        string gridFolder=GridFolderPath.Text;
        quickWebPInProgress=true;
        if(grid){gridBatchInProgress=true;GridActions.IsEnabled=GridScrollArea.IsEnabled=false;UpdateToolbarActions();UpdateSelectAllState();}
        StatusText.Text="Convirtiendo a WebP…";
        int saved=0,failed=0;string? last=null;
        try
        {
            foreach(string? path in grid?paths:new string?[]{current})
            {
                try
                {
                    BitmapSource bitmap=edited is not null && (!grid || StringComparer.OrdinalIgnoreCase.Equals(path,current))
                        ?edited:await DecodeExplorerImage(path!);
                    string folder=path is null?fallbackFolder:Path.GetDirectoryName(path)!;
                    string name=path is null?captureName:Path.GetFileNameWithoutExtension(path)+".webp";
                    last=await Task.Run(()=>WriteUniquePhoto(bitmap,folder,name));saved++;
                }
                catch(Exception ex) when(IsBatchImageError(ex)){failed++;}
            }
            gallerySignature=null;RefreshGallery(true);
            if(grid && IsFolderGridOpen && StringComparer.OrdinalIgnoreCase.Equals(GridFolderPath.Text,gridFolder))LoadFolderGrid(gridFolder);
            if(!grid && current is not null && StringComparer.OrdinalIgnoreCase.Equals(CurrentGridImage,current))
            {
                folderImages=GetFolderImages(current);
                currentIndex=Array.FindIndex(folderImages,file=>StringComparer.OrdinalIgnoreCase.Equals(file,current));
                UpdateMetadataFooter();UpdateSlideShowAvailability();
            }
            string message=saved==1 && failed==0?"WebP guardado: "+last:$"WebP: {saved} copia(s) · {failed} con error.";
            if(grid)SetGridStatus(message);else StatusText.Text=message;
        }
        finally
        {
            quickWebPInProgress=false;
            if(grid){gridBatchInProgress=false;GridActions.IsEnabled=GridScrollArea.IsEnabled=true;UpdateToolbarActions();UpdateSelectAllState();}
        }
    }
}

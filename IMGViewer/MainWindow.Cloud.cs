using IMGViewer.Cloud;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private R2Settings cloudSettings=new();
    private bool cloudView,cloudBusy;
    private string? cloudContinuation;
    private CancellationTokenSource? cloudOperation,cloudThumbnails;
    private readonly ObservableCollection<CloudImage> cloudImages=[];
    private readonly SemaphoreSlim cloudThumbnailGate=new(3);
    private List<CloudUpload> cloudFailed=[];
    private sealed record CloudUpload(string Name,string? Path,BitmapSource? Bitmap);
    private sealed class CloudImage(R2Object item):INotifyPropertyChanged
    {
        public R2Object Item {get;}=item;
        public string Name=>Item.Name;
        private BitmapSource? thumbnail;
        public BitmapSource? Thumbnail {get=>thumbnail;set{thumbnail=value;PropertyChanged?.Invoke(this,new(nameof(Thumbnail)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private void InitializeCloud()
    {
        CloudList.ItemsSource=cloudImages;
        CloudList.ItemContainerStyle=GalleryList.ItemContainerStyle;
        RefreshCloudConfiguration();
        Closed+=(_,_)=>{cloudOperation?.Cancel();cloudThumbnails?.Cancel();};
    }
    internal void RefreshCloudConfiguration()
    {
        cloudOperation?.Cancel();cloudThumbnails?.Cancel();cloudImages.Clear();cloudContinuation=null;cloudFailed.Clear();
        try {cloudSettings=R2Preferences.Load();if(cloudSettings.Enabled)cloudSettings=cloudSettings.Validate();}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException or ArgumentException)
        {cloudSettings=new();StatusText.Text="No se pudo recuperar Nube. Revisa la conexión en Editar → Configuración → Nube.";}
        if(!cloudSettings.Enabled)cloudView=false;
        GalleryCloudTab.Visibility=cloudSettings.Enabled?Visibility.Visible:Visibility.Collapsed;
        CloudUploadButton.Visibility=CloudCompactUploadButton.Visibility=cloudSettings.Enabled?Visibility.Visible:Visibility.Collapsed;
        CloudMessage.Text="Actualiza para cargar las imágenes de tu carpeta remota.";
        UpdateCloudActions();UpdateHistoryView();
    }
    private void UpdateCloudActions()
    {
        if(CloudUploadButton is null)return;
        bool image=Photo.Source is BitmapSource;
        bool canUpload=cloudSettings.Enabled&&!cloudBusy&&(IsFolderGridOpen?SelectedGridPaths().Length>0&&!gridBatchInProgress:image);
        CloudUploadButton.IsEnabled=CloudCompactUploadButton.IsEnabled=canUpload;
        bool selected=cloudSettings.Enabled&&!cloudBusy&&CloudList.SelectedItem is CloudImage;
        CloudOpenButton.IsEnabled=CloudDownloadButton.IsEnabled=CloudLinkButton.IsEnabled=CloudDeleteButton.IsEnabled=selected;
        CloudCancelButton.Visibility=cloudBusy?Visibility.Visible:Visibility.Collapsed;
        CloudProgress.Visibility=cloudBusy?Visibility.Visible:Visibility.Collapsed;
        CloudRetryButton.Visibility=cloudFailed.Count>0&&!cloudBusy?Visibility.Visible:Visibility.Collapsed;
        CloudMoreButton.Visibility=cloudContinuation is not null?Visibility.Visible:Visibility.Collapsed;CloudMoreButton.IsEnabled=!cloudBusy;
    }
    private static string CloudError(Exception error)=>error switch {
        OperationCanceledException=>"Operación cancelada.",
        HttpRequestException {StatusCode: { } code}=>$"R2 respondió HTTP {(int)code}. Revisa el bucket y los permisos de la conexión.",
        HttpRequestException=>"No se pudo conectar con R2. Comprueba tu conexión e inténtalo de nuevo.",
        ArgumentException=>error.Message,
        InvalidOperationException=>error.Message,
        _=>"No se pudo completar la operación. Revisa la conexión, el archivo y el espacio disponible."
    };
    private async Task RunCloudOperation(Func<R2Client,CancellationToken,Task> operation)
    {
        if(cloudBusy || !cloudSettings.Enabled)return;
        var cancellation=new CancellationTokenSource();cloudOperation=cancellation;cloudBusy=true;CloudProgress.Value=0;CloudProgress.IsIndeterminate=true;UpdateCloudActions();
        try {await operation(new R2Client(cloudSettings),cancellation.Token);}
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or System.Xml.XmlException or System.Runtime.InteropServices.ExternalException)
        {CloudMessage.Text=StatusText.Text=CloudError(ex);}
        finally {cloudBusy=false;if(ReferenceEquals(cloudOperation,cancellation))cloudOperation=null;cancellation.Dispose();UpdateCloudActions();}
    }
    private async Task LoadCloud(bool more=false)
    {
        if(cloudBusy)return;
        await RunCloudOperation(async(client,token)=>{
            if(!more){cloudThumbnails?.Cancel();cloudImages.Clear();cloudContinuation=null;}
            CloudMessage.Text="Cargando Nube…";
            var page=await client.List(more?cloudContinuation:null,token);token.ThrowIfCancellationRequested();
            var added=new List<CloudImage>();
            foreach(var item in page.Items.OrderByDescending(x=>x.Modified))if(!cloudImages.Any(x=>x.Item.Key==item.Key)){var photo=new CloudImage(item);cloudImages.Add(photo);added.Add(photo);}
            cloudContinuation=page.Next;
            CloudMessage.Text=cloudImages.Count==0?"No hay imágenes en esta carpeta remota.":$"{cloudImages.Count} imágenes · doble clic para abrir";
            cloudThumbnails??=new CancellationTokenSource();
            if(cloudThumbnails.IsCancellationRequested){cloudThumbnails.Dispose();cloudThumbnails=new CancellationTokenSource();}

        });
    }
    private void CloudThumbnail_Updated(object sender,DataTransferEventArgs e)
    {
        if(sender is Image {Parent:Grid card})UpdateGalleryCard(card);
    }
    private void CloudThumbnail_Loaded(object sender,RoutedEventArgs e)=>LoadVisibleCloudThumbnail((Image)sender);
    private void CloudThumbnail_Changed(object sender,DependencyPropertyChangedEventArgs e)
    {
        var image=(Image)sender;CancelCloudThumbnail(image);if(image.IsLoaded)LoadVisibleCloudThumbnail(image);
    }
    private void CloudThumbnail_Unloaded(object sender,RoutedEventArgs e)=>CancelCloudThumbnail((Image)sender);
    private static void CancelCloudThumbnail(Image image)
    {
        if(image.Tag is CancellationTokenSource cancellation){cancellation.Cancel();image.Tag=null;}
    }
    private async void LoadVisibleCloudThumbnail(Image image)
    {
        if(image.DataContext is not CloudImage item || item.Thumbnail is not null || item.Item.Size>16*1024*1024 || image.Tag is CancellationTokenSource || !cloudSettings.Enabled)return;
        cloudThumbnails??=new CancellationTokenSource();
        if(cloudThumbnails.IsCancellationRequested){cloudThumbnails.Dispose();cloudThumbnails=new CancellationTokenSource();}
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cloudThumbnails.Token);image.Tag=cancellation;
        var token=cancellation.Token;bool entered=false;
        try
        {
            await cloudThumbnailGate.WaitAsync(token);entered=true;
            var client=new R2Client(cloudSettings);var bytes=await client.Get(item.Item.Key,token);
            var bitmap=await DecodeCloudBytes(bytes,item.Name,token,true);
            if(!token.IsCancellationRequested&&ReferenceEquals(image.DataContext,item))item.Thumbnail=bitmap;
        }
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or System.Runtime.InteropServices.ExternalException) { }
        finally {if(entered)cloudThumbnailGate.Release();if(ReferenceEquals(image.Tag,cancellation))image.Tag=null;}
    }
    private static async Task<BitmapSource> DecodeCloudBytes(byte[] bytes,string name,CancellationToken token,bool thumbnail=false)
    {
        string extension=Path.GetExtension(name).ToLowerInvariant();
        if(!R2Client.IsImage(name))throw new ArgumentException("Formato de imagen no disponible.");
        string temporary=Path.Combine(Path.GetTempPath(),"IMGViewer-cloud-"+Guid.NewGuid().ToString("N")+extension);
        try
        {
            await File.WriteAllBytesAsync(temporary,bytes,token);
            var bitmap=await DecodeExplorerImage(temporary,thumbnail?320:0);token.ThrowIfCancellationRequested();
            if(!thumbnail || bitmap.PixelWidth<=320)return bitmap;
            return await Task.Run(()=>{var scaled=new TransformedBitmap(bitmap,new System.Windows.Media.ScaleTransform(320.0/bitmap.PixelWidth,320.0/bitmap.PixelWidth));scaled.Freeze();return (BitmapSource)scaled;},token);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private async void CloudRefresh_Click(object sender,RoutedEventArgs e)=>await LoadCloud();
    private async void CloudMore_Click(object sender,RoutedEventArgs e)=>await LoadCloud(true);
    private void CloudCancel_Click(object sender,RoutedEventArgs e){cloudOperation?.Cancel();cloudThumbnails?.Cancel();}
    private void CloudSelection_Changed(object sender,SelectionChangedEventArgs e)=>UpdateCloudActions();
    private bool HandleCloudKey(KeyEventArgs e)
    {
        if(!CloudList.IsKeyboardFocusWithin)return false;
        if(e.Key==Key.Enter && Keyboard.Modifiers==ModifierKeys.None)
        {e.Handled=true;if(!e.IsRepeat)_=OpenSelectedCloud();}
        else if(e.Key==Key.C && Keyboard.Modifiers==ModifierKeys.Control)
        {e.Handled=true;if(!e.IsRepeat)CloudLink_Click(this,new RoutedEventArgs());}
        else if(e.Key==Key.Delete && Keyboard.Modifiers==ModifierKeys.None)
        {e.Handled=true;if(!e.IsRepeat)CloudDelete_Click(this,new RoutedEventArgs());}
        else if(e.Key==Key.Escape && Keyboard.Modifiers==ModifierKeys.None)
        {e.Handled=true;Keyboard.Focus(this);}
        // Native list navigation must not navigate the local folder behind this pane.
        return true;
    }
    private async void GalleryCloud_Click(object sender,RoutedEventArgs e)
    {
        if(!cloudSettings.Enabled)return;cloudView=true;UpdateHistoryView();if(cloudImages.Count==0)await LoadCloud();
    }
    private async void CloudList_DoubleClick(object sender,MouseButtonEventArgs e)
    {
        if(GalleryAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null){e.Handled=true;await OpenSelectedCloud();}
    }
    private async void CloudOpen_Click(object sender,RoutedEventArgs e)=>await OpenSelectedCloud();
    private async Task OpenSelectedCloud()
    {
        if(CloudList.SelectedItem is not CloudImage image || cloudBusy || !ConfirmWorkspaceNavigation())return;
        int version=++imageOpenVersion;
        await RunCloudOperation(async(client,token)=>{
            CloudMessage.Text="Abriendo imagen…";
            var bitmap=await DecodeCloudBytes(await client.Get(image.Item.Key,token),image.Name,token);
            if(version!=imageOpenVersion)return;
            RememberActiveCapture();activeCapture=null;OpenCapture(bitmap);AddCaptureToHistory(DateTime.Now);cloudView=true;UpdateHistoryView();
            CloudMessage.Text="Imagen abierta. Guardar crea una copia local; no modifica la imagen de Nube.";
            StatusText.Text="Nube · "+image.Name;
        });
    }
    private async void CloudDownload_Click(object sender,RoutedEventArgs e)
    {
        if(CloudList.SelectedItem is not CloudImage image||cloudBusy)return;
        string name=new(image.Name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
        var dialog=new SaveFileDialog {Title="Descargar imagen de Nube",FileName=name,InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),Filter="Imagen|*"+Path.GetExtension(name),AddExtension=true,DefaultExt=Path.GetExtension(name)};
        if(dialog.ShowDialog(this)!=true)return;
        await RunCloudOperation(async(client,token)=>{
            CloudMessage.Text="Descargando…";var bytes=await client.Get(image.Item.Key,token);
            string temp=dialog.FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {await File.WriteAllBytesAsync(temp,bytes,token);token.ThrowIfCancellationRequested();File.Move(temp,dialog.FileName,true);}
            finally{if(File.Exists(temp))File.Delete(temp);}
            CloudMessage.Text="Descargada en "+dialog.FileName;
        });
    }
    private void CloudLink_Click(object sender,RoutedEventArgs e)
    {
        if(CloudList.SelectedItem is not CloudImage image||cloudBusy)return;
        try {Clipboard.SetText(new R2Client(cloudSettings).Link(image.Item.Key));CloudMessage.Text=cloudSettings.PublicUrl.Length==0?"Enlace copiado · válido durante una hora.":"Enlace público copiado.";}
        catch(Exception ex) when(ex is ArgumentException or System.Runtime.InteropServices.ExternalException){CloudMessage.Text="No se pudo copiar el enlace.";}
    }
    private async void CloudDelete_Click(object sender,RoutedEventArgs e)
    {
        if(CloudList.SelectedItem is not CloudImage image||cloudBusy)return;
        if(ViewerDialog.Show(this,$"¿Eliminar {image.Name} de Nube? Esta acción no se puede deshacer. Tus archivos locales se conservarán.","Eliminar de Nube",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        await RunCloudOperation(async(client,token)=>{await client.Delete(image.Item.Key,token);cloudImages.Remove(image);CloudMessage.Text="Imagen eliminada de Nube. La copia local se conserva.";});
    }
    private List<CloudUpload> SelectedCloudUploads()
    {
        if(IsFolderGridOpen)return SelectedGridPaths().Select(path=>
            StringComparer.OrdinalIgnoreCase.Equals(path,CurrentGridImage)&&HasPendingChanges&&Photo.Source is BitmapSource edited
                ?new CloudUpload(Path.GetFileNameWithoutExtension(path)+".png",null,FrozenDraftBitmap(FlattenMarkers(edited)))
                :new CloudUpload(Path.GetFileName(path),path,null)).ToList();
        if(Photo.Source is not BitmapSource bitmap)return [];
        EndInlineText(true);
        string? path=CurrentGridImage;
        if(!HasPendingChanges && path is not null && File.Exists(path))return [new(Path.GetFileName(path),path,null)];
        string name=path is null?"screen-"+(activeCapture?.Created??DateTime.Now).ToString("yyMMdd-HHmmss"):Path.GetFileNameWithoutExtension(path);
        return [new(name+".png",null,FrozenDraftBitmap(FlattenMarkers(bitmap)))];
    }
    private async void CloudUpload_Click(object sender,RoutedEventArgs e)=>await UploadToCloud(SelectedCloudUploads());
    private async void CloudRetry_Click(object sender,RoutedEventArgs e)=>await UploadToCloud(cloudFailed.ToList());
    private async Task UploadToCloud(List<CloudUpload> uploads)
    {
        if(cloudBusy || !cloudSettings.Enabled || uploads.Count==0)return;
        cloudView=true;galleryOpen=true;UpdateHistoryView();ApplyWorkspaceLayout();
        await RunCloudOperation(async(client,token)=>{
            cloudFailed=[];int completed=0;string? lastKey=null;string? failure=null;
            for(int i=0;i<uploads.Count;i++)
            {
                var upload=uploads[i];
                try
                {
                    token.ThrowIfCancellationRequested();CloudMessage.Text=$"Subiendo {i+1} / {uploads.Count} · {upload.Name}";
                    CloudProgress.IsIndeterminate=false;CloudProgress.Value=0;
                    byte[] bytes=await Task.Run(()=>{
                        if(upload.Path is not null){if(new FileInfo(upload.Path).Length>R2Client.MaximumImageBytes)throw new InvalidOperationException("La imagen supera los 128 MB.");return File.ReadAllBytes(upload.Path);}
                        using var stream=new MemoryStream();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(upload.Bitmap!));encoder.Save(stream);return stream.ToArray();
                    },token);
                    lastKey=await client.Upload(bytes,upload.Name,new Progress<double>(value=>CloudProgress.Value=value*100),token);completed++;
                    var item=new CloudImage(new(lastKey,bytes.LongLength,DateTimeOffset.UtcNow));
                    if(upload.Bitmap is { } original)
                    {
                        item.Thumbnail=await Task.Run(()=>{
                            double scale=Math.Min(1,320.0/Math.Max(original.PixelWidth,original.PixelHeight));
                            var preview=new TransformedBitmap(original,new System.Windows.Media.ScaleTransform(scale,scale));preview.Freeze();return (BitmapSource)preview;
                        });
                    }
                    cloudImages.Insert(0,item);
                }
                catch(OperationCanceledException){failure="Subida cancelada.";cloudFailed.AddRange(uploads.Skip(i));break;}
                catch(Exception ex) when(ex is HttpRequestException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException){cloudFailed.Add(upload);failure=CloudError(ex);}
            }
            if(client.Settings!=cloudSettings){cloudFailed.Clear();return;}
            CloudMessage.Text=StatusText.Text=$"Subidas: {completed} · pendientes: {cloudFailed.Count}."+(failure is null?"":" "+failure);
            if(completed>0&&client.Settings.CopyLink&&lastKey is not null)
            {
                try {Clipboard.SetText(client.Link(lastKey));CloudMessage.Text+=client.Settings.PublicUrl.Length==0?" Enlace de la última imagen copiado (1 hora).":" Enlace de la última imagen copiado.";}
                catch(System.Runtime.InteropServices.ExternalException){CloudMessage.Text+=" No se pudo copiar el enlace.";}
            }
        });
    }
}

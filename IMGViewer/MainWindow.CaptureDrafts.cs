using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace IMGViewer;
public partial class MainWindow
{
    private static readonly string CaptureDraftFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IMGViewer","Drafts");
    private Task draftWrites=Task.CompletedTask;
    private readonly DispatcherTimer draftSaveTimer=new() {Interval=TimeSpan.FromMilliseconds(350)};
    private readonly HashSet<Guid> removedDrafts=[];
    private bool discardRecoveredDrafts;
    private sealed record DraftMark(string Geometry,string Color,double Opacity,double Width,bool Contrast,ArrowShape? Curve,Rect PatchBounds,double ShadowIntensity,bool HasPatch);
    private sealed record DraftData(int Version,Guid Id,string Name,DateTime Created,string? FilePath,int Rotation,bool PixelEdit,bool HasSaved,bool HasRotationBase,DraftMark[] Marks);
    private sealed record DraftSnapshot(DraftData Data,BitmapSource Photo,BitmapSource Preview,BitmapSource? Saved,BitmapSource? RotationBase,BitmapSource?[] Patches);
    private static BitmapSource FrozenDraftBitmap(BitmapSource bitmap)
    {
        if(bitmap.IsFrozen)return bitmap;
        var frozen=bitmap.Clone();frozen.Freeze();return frozen;
    }
    private void QueueDraftOperation(Action operation)
    {
        draftWrites=draftWrites.ContinueWith(_=> {
            try {operation();}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.Security.SecurityException)
            { Dispatcher.BeginInvoke(()=> {if(!Dispatcher.HasShutdownStarted)StatusText.Text="No se pudo actualizar el borrador recuperable. Guarda la captura para conservarla.";}); }
        },TaskScheduler.Default);
    }
    private void ScheduleCaptureDraftSave()
    {
        if(activeCapture is null || switchingCapture)return;
        draftSaveTimer.Stop();draftSaveTimer.Start();
    }
    private void PersistCaptureDraft(SessionCapture capture)
    {
        if(removedDrafts.Contains(capture.DraftId))return;
        if(capture.FilePath is not null && !capture.PixelEdit && capture.Rotation==0 && capture.Marks.Count==0) {DeleteCaptureDraft(capture,false);return;}
        var marks=capture.Marks.Select(mark=>new DraftMark(mark.Geometry.ToString(CultureInfo.InvariantCulture),
            ((SolidColorBrush)mark.Color).Color.ToString(CultureInfo.InvariantCulture),mark.Color.Opacity,mark.Width,mark.Contrast,mark.Curve,mark.PatchBounds,mark.ShadowIntensity,mark.Patch is not null)).ToArray();
        var data=new DraftData(1,capture.DraftId,capture.Name,capture.Created,capture.FilePath,capture.Rotation,capture.PixelEdit,capture.SavedPhoto is not null,capture.RotationBase is not null,marks);
        var snapshot=new DraftSnapshot(data,FrozenDraftBitmap(capture.Photo),FrozenDraftBitmap(capture.Preview),capture.SavedPhoto is null?null:FrozenDraftBitmap(capture.SavedPhoto),
            capture.RotationBase is null?null:FrozenDraftBitmap(capture.RotationBase),capture.Marks.Select(m=>m.Patch is null?null:FrozenDraftBitmap(m.Patch)).ToArray());
        QueueDraftOperation(()=>WriteCaptureDraft(CaptureDraftFolder,snapshot));
    }
    private static void WriteCaptureDraft(string folder,DraftSnapshot snapshot)
    {
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,snapshot.Data.Id.ToString("N")+".draft");
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                using(var zip=new ZipArchive(file,ZipArchiveMode.Create,true))
                {
                    using(var state=zip.CreateEntry("state.json").Open())JsonSerializer.Serialize(state,snapshot.Data);
                    void Image(string name,BitmapSource bitmap) {using var entry=zip.CreateEntry(name,CompressionLevel.NoCompression).Open();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var buffer=new MemoryStream();encoder.Save(buffer);buffer.Position=0;buffer.CopyTo(entry);}
                    Image("photo.png",snapshot.Photo);Image("preview.png",snapshot.Preview);
                    if(snapshot.Saved is not null)Image("saved.png",snapshot.Saved);
                    if(snapshot.RotationBase is not null)Image("rotation.png",snapshot.RotationBase);
                    for(int i=0;i<snapshot.Patches.Length;i++)if(snapshot.Patches[i] is { } patch)Image($"patch-{i}.png",patch);
                }
                file.Flush(true);
            }
            File.Move(temporary,path,true);
        }
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static SessionCapture ReadCaptureDraft(string path)
    {
        using var zip=ZipFile.OpenRead(path);
        using var state=zip.GetEntry("state.json")!.Open();
        var data=JsonSerializer.Deserialize<DraftData>(state)??throw new InvalidDataException("Borrador vacío.");
        if(data.Version!=1 || Path.GetFileNameWithoutExtension(path)!=data.Id.ToString("N"))throw new InvalidDataException("Borrador no compatible.");
        BitmapSource Image(string name) {using var stream=(zip.GetEntry(name)??throw new InvalidDataException("Imagen ausente.")).Open();using var buffer=new MemoryStream();stream.CopyTo(buffer);buffer.Position=0;var image=BitmapFrame.Create(buffer,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);image.Freeze();return image;}
        var capture=new SessionCapture {DraftId=data.Id,Created=data.Created,Name=data.Name,FilePath=data.FilePath,Photo=Image("photo.png"),Preview=Image("preview.png"),SavedPhoto=data.HasSaved?Image("saved.png"):null,RotationBase=data.HasRotationBase?Image("rotation.png"):null,Rotation=data.Rotation,PixelEdit=data.PixelEdit};
        for(int i=0;i<data.Marks.Length;i++)
        {
            var mark=data.Marks[i];var geometry=Geometry.Parse(mark.Geometry);geometry.Freeze();
            var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(mark.Color)) {Opacity=mark.Opacity};brush.Freeze();
            capture.Marks.Add(new StyledMarker(geometry,brush,mark.Width,mark.Contrast,mark.Curve,mark.HasPatch?Image($"patch-{i}.png"):null,mark.PatchBounds,mark.ShadowIntensity));
        }
        return capture;
    }
    private void DeleteCaptureDraft(SessionCapture capture,bool removeFromHistory=true)
    {
        if(removeFromHistory)removedDrafts.Add(capture.DraftId);
        string path=Path.Combine(CaptureDraftFolder,capture.DraftId.ToString("N")+".draft");
        QueueDraftOperation(()=>File.Delete(path));
    }
    internal async Task RestoreCaptureDraftsAsync()
    {
        int openingVersion=imageOpenVersion;
        var recovered=await Task.Run(()=> {
            var captures=new List<SessionCapture>();int errors=0;
            try {
                if(Directory.Exists(CaptureDraftFolder))foreach(string path in Directory.EnumerateFiles(CaptureDraftFolder,"*.draft"))
                    try {captures.Add(ReadCaptureDraft(path));}
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NotSupportedException or System.FormatException or NullReferenceException) {errors++;}
            } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {errors++;}
            return (captures,errors);
        });
        if(discardRecoveredDrafts || Dispatcher.HasShutdownStarted)return;
        foreach(var capture in recovered.captures.OrderByDescending(c=>c.Created))
            if(!removedDrafts.Contains(capture.DraftId) && !captureHistory.Any(c=>c.DraftId==capture.DraftId))captureHistory.Add(capture);
        CaptureHistoryList.ItemsSource=captureHistory;
        if(imageOpenVersion==openingVersion && Photo.Source is null && captureHistory.Count>0) {ShowSessionCapture(captureHistory[0]);historyView=true;galleryOpen=true;UpdateHistoryView();ApplyWorkspaceLayout();}
        if(recovered.errors>0)StatusText.Text="Algunos borradores no pudieron recuperarse. Los archivos se conservaron.";
    }
    internal void FlushCaptureDrafts()
    {
        EndInlineText(true);
        draftSaveTimer.Stop();RememberActiveCapture();
        // Worker jobs never wait on the dispatcher, so shutdown can drain the queue.
        draftWrites.GetAwaiter().GetResult();
    }
}

using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IMGViewer;
internal static class Program
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private static readonly Type Type=typeof(MainWindow);
    private static int checks;
    private static object? Call(MainWindow? window,string name,params object?[] args)=>Type.GetMethod(name,Flags)!.Invoke(window,args);
    private static object? Field(MainWindow window,string name)=>Type.GetField(name,Flags)!.GetValue(window);
    private static void Set(MainWindow window,string name,object? value)=>Type.GetField(name,Flags)!.SetValue(window,value);
    private static void Check(bool value,string error){if(!value)throw new Exception(error);checks++;}
    private static BitmapSource Bitmap(int width,int height,byte blue,byte green,byte red)
    {
        var pixels=new byte[width*height*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=blue;pixels[i+1]=green;pixels[i+2]=red;pixels[i+3]=255;}
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);bitmap.Freeze();return bitmap;
    }
    private static bool Near(Rect a,Rect b)=>Math.Abs(a.X-b.X)<.001&&Math.Abs(a.Y-b.Y)<.001&&Math.Abs(a.Width-b.Width)<.001&&Math.Abs(a.Height-b.Height)<.001;
    private static Rect Bounds(object marker)=>(Rect)marker.GetType().GetProperty("PatchBounds")!.GetValue(marker)!;
    private static byte[] Pixel(BitmapSource bitmap,int x,int y){var bytes=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),bytes,4,0);return bytes;}
    [STAThread] private static int Main()
    {
        string folder=Path.Combine(Path.GetTempPath(),"IMGViewer-composition-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};MainWindow? window=null;int result=1;
        try
        {
            string original=Path.Combine(folder,"base.png");var baseBitmap=Bitmap(400,300,255,0,0);
            using(var file=File.Create(original)){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(baseBitmap));encoder.Save(file);}
            byte[] before=File.ReadAllBytes(original);
            window=new MainWindow {Width=900,Height=650};Set(window,"restoredStartupPlacement",true);Set(window,"galleryOpen",false);Set(window,"toolsOpen",false);
            Call(window,"OpenImage",original,true);app.MainWindow=window;
            window.ContentRendered+=(_,_)=>{
                try
                {
                    var arrowTool=(RadioButton)Field(window,"ArrowTool")!;var rectangleTool=(RadioButton)Field(window,"RectangleTool")!;
                    var widthSlider=(Slider)Field(window,"MarkerWidthSlider")!;
                    arrowTool.IsChecked=true;Check(widthSlider.Value==60,"Arrow default width changed");
                    double arrowWidth=(double)Call(window,"MarkerStroke",baseBitmap)!;
                    rectangleTool.IsChecked=true;Check(widthSlider.Value==25,"Rectangle default width incorrect");
                    Check((double)Call(window,"MarkerStroke",baseBitmap)!<arrowWidth/2,"Rectangle should be thinner than arrow");
                    widthSlider.Value=32;arrowTool.IsChecked=true;Check(widthSlider.Value==60,"Rectangle change modified arrow");
                    widthSlider.Value=72;rectangleTool.IsChecked=true;Check(widthSlider.Value==32,"Rectangle width was not remembered");
                    arrowTool.IsChecked=true;Check(widthSlider.Value==72,"Arrow width was not remembered");
                    var dragType=Nullable.GetUnderlyingType(Type.GetField("markerDrag",Flags)!.FieldType)!;
                    var rectangleKind=Enum.Parse(Type.GetNestedType("MarkerKind",BindingFlags.NonPublic)!,"Rectangle");
                    Set(window,"markerDrag",Activator.CreateInstance(dragType,rectangleKind,new Point(0,0)));
                    double shortcutWidth=(double)Call(window,"MarkerStroke",baseBitmap)!;
                    Set(window,"markerDrag",null);rectangleTool.IsChecked=true;
                    Check(Math.Abs(shortcutWidth-(double)Call(window,"MarkerStroke",baseBitmap)!)<0.00001,"Modifier rectangle did not use independent width");
                    widthSlider.Value=25;arrowTool.IsChecked=true;widthSlider.Value=60;Call(window,"SelectWindowMoveTool");
                    Call(window,"AddPastedImage",Bitmap(100,80,0,0,255));window.UpdateLayout();
                    var markers=(IList)Field(window,"markers")!;Check(markers.Count==1,"Paste did not add a layer");
                    var actualBase=(BitmapSource)((Image)Field(window,"Photo")!).Source; double sx=actualBase.Width/400,sy=actualBase.Height/300; Rect initial=Bounds(markers[0]!);Check(Near(initial,new Rect(150*sx,110*sy,100*sx,80*sy)),"Initial paste placement incorrect: "+initial);
                    var flattened=(BitmapSource)Call(window,"FlattenMarkers",actualBase)!;
                    Check(Pixel(flattened,200,150)[2]==255,"Pasted image absent from export");Check(Pixel(flattened,10,10)[0]==255,"Base image was not preserved");
                    var zoom=(ScaleTransform)Field(window,"PhotoZoom")!;zoom.ScaleX=zoom.ScaleY=2;Call(window,"SetPhotoPan",0.0,0.0);window.UpdateLayout();
                    var full=(Rect)Call(window,"PastedImageViewBounds",new Rect(0,0,actualBase.Width,actualBase.Height))!;
                    var bodies=(IDictionary)Field(window,"pastedImageBodies")!;var body=(Thumb)bodies[0]!;
                    Call(window,"BeginPastedImageGesture",0,body);Call(window,"ChangePastedImage",0,null,40*full.Width/400,30*full.Height/300);Call(window,"CompletePastedImageGesture",false);
                    Check(Near(Bounds(markers[0]!),new Rect(190*sx,140*sy,100*sx,80*sy)),"Move did not map viewport delta through zoom");
                    Call(window,"BeginPastedImageGesture",0,body);Call(window,"ChangePastedImage",0,"se",40*full.Width/400,32*full.Height/300);Call(window,"CompletePastedImageGesture",false);
                    var enlarged=Bounds(markers[0]!);Check(Math.Abs(enlarged.Width-140*sx)<0.001&&Math.Abs(enlarged.Height-112*sy)<0.001,"Resize did not preserve aspect ratio");
                    Call(window,"UndoWorkspace");Check(Near(Bounds(markers[0]!),new Rect(190*sx,140*sy,100*sx,80*sy)),"Undo resize failed");Call(window,"UndoWorkspace");Check(Near(Bounds(markers[0]!),initial),"Undo move failed");
                    Call(window,"BeginPastedImageGesture",0,body);Call(window,"ChangePastedImage",0,null,5000.0,5000.0);Call(window,"CompletePastedImageGesture",true);Check(Near(Bounds(markers[0]!),initial),"Canceled move changed layer");
                    Call(window,"AddPastedImage",Bitmap(60,60,0,255,0));Check(markers.Count==2,"Second paste missing");
                    flattened=(BitmapSource)Call(window,"FlattenMarkers",actualBase)!;Check(Pixel(flattened,200,150)[1]==255,"Image stacking order incorrect");
                    Call(window,"UndoWorkspace");Check(markers.Count==1,"Undo second paste failed");
                    var model=Activator.CreateInstance(Type.GetNestedType("SessionCapture",BindingFlags.NonPublic)!,true)!;var modelType=model.GetType();
                    modelType.GetProperty("Photo")!.SetValue(model,actualBase);modelType.GetProperty("Preview")!.SetValue(model,Call(window,"FlattenMarkers",actualBase));
                    modelType.GetField("SavedPhoto")!.SetValue(model,actualBase);modelType.GetField("RotationBase")!.SetValue(model,actualBase);
                    var savedMarks=(IList)modelType.GetField("Marks")!.GetValue(model)!;savedMarks.Add(markers[0]);
                    var snapshot=Call(null,"CreateCaptureDraftSnapshot",model)!;Call(null,"WriteCaptureDraft",folder,snapshot);
                    string draft=Directory.GetFiles(folder,"*.draft").Single();var restored=Call(null,"ReadCaptureDraft",draft)!;
                    var restoredMarks=(IList)modelType.GetField("Marks")!.GetValue(restored)!;var restoredMark=restoredMarks[0]!;
                    Check((bool)restoredMark.GetType().GetProperty("IsPastedImage")!.GetValue(restoredMark)!,"Draft lost editable image type");Check(Near(Bounds(restoredMark),initial),"Draft lost layer placement");
                    var restoredPatch=(BitmapSource)restoredMark.GetType().GetProperty("Patch")!.GetValue(restoredMark)!;Check(Pixel(restoredPatch,10,10)[2]==255,"Draft lost pasted pixels");
                    // Older drafts have raster blur patches without an editable-image flag.
                    using(var zip=ZipFile.Open(draft,ZipArchiveMode.Update))
                    {
                        var entry=zip.GetEntry("state.json")!;JsonNode state;using(var reader=new StreamReader(entry.Open()))state=JsonNode.Parse(reader.ReadToEnd())!;
                        foreach(var mark in state["Marks"]!.AsArray())mark!.AsObject().Remove("IsPastedImage");entry.Delete();using var writer=new StreamWriter(zip.CreateEntry("state.json").Open());writer.Write(state.ToJsonString());
                    }
                    restored=Call(null,"ReadCaptureDraft",draft)!;restoredMarks=(IList)modelType.GetField("Marks")!.GetValue(restored)!;restoredMark=restoredMarks[0]!;
                    Check(!(bool)restoredMark.GetType().GetProperty("IsPastedImage")!.GetValue(restoredMark)!,"Older raster patches incorrectly became image layers");
                    foreach(string side in new[]{"right","left","top","bottom"})
                    {
                        Call(window,"PlacePastedImage",0,side);window.UpdateLayout();
                        var expanded=(BitmapSource)((Image)Field(window,"Photo")!).Source;
                        bool horizontal=side is "right" or "left";
                        Check(expanded.PixelWidth==(horizontal?500:400)&&expanded.PixelHeight==(horizontal?300:380),"Incorrect expanded canvas: "+side+" "+expanded.PixelWidth+"x"+expanded.PixelHeight);
                        var composite=(BitmapSource)Call(window,"FlattenMarkers",expanded)!;
                        int redX=side=="left"?50:side=="right"?450:50;
                        int redY=side=="top"?40:side=="bottom"?340:40;
                        Check(Pixel(composite,redX,redY)[2]==255,"Missing placed pixels "+side);
                        int emptyX=side=="left"?5:expanded.PixelWidth-5,emptyY=side=="top"?5:expanded.PixelHeight-5; Check(Pixel(composite,emptyX,emptyY)[3]==0,"Empty area should be transparent "+side);
                        string jpg=Path.Combine(folder,"composition.jpg");Call(null,"WritePhoto",composite,jpg,true,true);
                        var decoded=BitmapFrame.Create(new Uri(jpg),BitmapCreateOptions.IgnoreImageCache,BitmapCacheOption.OnLoad);
                        var converted=new FormatConvertedBitmap(decoded,PixelFormats.Bgra32,null,0);
                        var white=Pixel(converted,emptyX,emptyY);
                        Check(white[0]>245&&white[1]>245&&white[2]>245,"JPEG background not white "+side);
                        Call(window,"UndoWorkspace");window.UpdateLayout();
                        Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,actualBase)&&Near(Bounds(markers[0]!),initial),"Undo expansion did not restore both canvas and placement "+side);
                    }
                    zoom.ScaleX=zoom.ScaleY=1;Call(window,"SetPhotoPan",0.0,0.0);window.UpdateLayout();
                    full=(Rect)Call(window,"PastedImageViewBounds",new Rect(0,0,actualBase.Width,actualBase.Height))!;
                    Call(window,"BeginPastedImageGesture",0,body);Call(window,"ChangePastedImage",0,null,400*full.Width/400,0.0);Call(window,"CompletePastedImageGesture",false);
                    Check(((BitmapSource)((Image)Field(window,"Photo")!).Source).PixelWidth==650,"Drag outside did not expand on release");
                    Call(window,"UndoWorkspace");Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,actualBase),"Undo outside drag failed");
                    Set(window,"selectedPastedImage",0);
                    Check((bool)Call(window,"DeleteSelectedPastedImage")!,"Delete selected image failed");
                    Check(markers.Count==0&&((Canvas)Field(window,"PastedImagesCanvas")!).Visibility==Visibility.Collapsed,"Deleted layer or handles remained");
                    Call(window,"UndoWorkspace");Check(markers.Count==1&&Near(Bounds(markers[0]!),initial),"Undo sole-layer deletion failed");
                    Call(window,"AddPastedImage",Bitmap(60,60,0,255,0));var upper=markers[1];
                    Set(window,"selectedPastedImage",0);Call(window,"DeleteSelectedPastedImage");
                    Check(markers.Count==1&&ReferenceEquals(markers[0],upper),"Deleting lower image removed wrong layer");
                    Set(window,"selectedPastedImage",0);Call(window,"DeleteSelectedPastedImage");Check(markers.Count==0,"Multiple deletion failed");
                    Call(window,"UndoWorkspace");Check(markers.Count==1&&ReferenceEquals(markers[0],upper),"Undo latest deletion failed");
                    Call(window,"UndoWorkspace");Check(markers.Count==2&&ReferenceEquals(markers[1],upper),"Undo previous deletion did not restore order");
                    Call(window,"UndoWorkspace");Check(markers.Count==1,"Undo paste after restoring deletions failed");
                    Set(window,"selectedPastedImage",-1);Check(!(bool)Call(window,"DeleteSelectedPastedImage")!,"Delete without selection should not remove image");
                    Call(window,"UndoWorkspace");Check(markers.Count==0,"Undo first pasted image failed");Check(((Canvas)Field(window,"PastedImagesCanvas")!).Visibility==Visibility.Collapsed,"Selection handles survived undo");
                    // History navigation must preserve editable layers and never write user drafts.
                    var removedDrafts=(HashSet<Guid>)Field(window,"removedDrafts")!;
                    Guid Id(object capture)=>(Guid)modelType.GetProperty("DraftId")!.GetValue(capture)!;
                    removedDrafts.Add(Id(model));
                    var other=Activator.CreateInstance(modelType,true)!;removedDrafts.Add(Id(other));
                    modelType.GetProperty("Photo")!.SetValue(other,baseBitmap);modelType.GetProperty("Preview")!.SetValue(other,baseBitmap);
                    modelType.GetField("SavedPhoto")!.SetValue(other,baseBitmap);modelType.GetField("RotationBase")!.SetValue(other,baseBitmap);
                    Call(window,"ShowSessionCapture",model);window.UpdateLayout();
                    Check(markers.Count==1&&(bool)markers[0]!.GetType().GetProperty("IsPastedImage")!.GetValue(markers[0])!,"History did not restore editable layer");
                    Call(window,"ShowSessionCapture",other);Call(window,"ShowSessionCapture",model);window.UpdateLayout();
                    Check(markers.Count==1&&Near(Bounds(markers[0]!),initial),"Switching captures lost layer placement");
                    bodies=(IDictionary)Field(window,"pastedImageBodies")!;Check(bodies.Count==1,"History did not rebuild layer handles");
                    Call(window,"PlacePastedImage",0,"right");var historyCanvas=((Image)Field(window,"Photo")!).Source;
                    Call(window,"ShowSessionCapture",other);Call(window,"ShowSessionCapture",model);window.UpdateLayout();
                    Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,historyCanvas)&&markers.Count==1,"History lost expanded editable canvas");
                    Call(window,"ClearWorkspaceImage");
                    Check(((Image)Field(window,"Photo")!).Source is null&&!((bool)modelType.GetProperty("IsActive")!.GetValue(model)!),"Close left capture active");
                    Call(window,"ShowSessionCapture",model);window.UpdateLayout();
                    Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,historyCanvas)&&markers.Count==1,"Cannot reopen composition after closing workspace");
                    Set(window,"selectedPastedImage",0);Check((bool)Call(window,"DeleteSelectedPastedImage")!,"Recovered image is not editable");Call(window,"UndoWorkspace");
                    Check(markers.Count==1,"Undo editing recovered image failed");
                    // Regression: old code could leave an active capture with an empty workspace.
                    ((Image)Field(window,"Photo")!).Source=null;((FrameworkElement)Field(window,"EmptyState")!).Visibility=Visibility.Visible;
                    Call(window,"ShowSessionCapture",model);Check(((Image)Field(window,"Photo")!).Source is not null&&markers.Count==1,"Stale active capture prevented recovery");
                    Type.GetProperty("activeCapture",Flags)!.SetValue(window,null);
                    Check(File.ReadAllBytes(original).SequenceEqual(before),"Composition modified original file");
                    result=0;Console.WriteLine($"PASS: {checks} composition checks — placement, export pixels, zoom-aware move, proportional resize, transform/paste undo, cancel, stacking and editable draft recovery. Clipboard and user drafts unchanged.");
                }
                catch(Exception error){Console.Error.WriteLine(error);}
                finally{Set(window,"restoredStartupPlacement",false);window.Close();app.Shutdown();}
            };
            app.Run(window);
        }
        catch(Exception error){Console.Error.WriteLine(error);if(window is not null){Set(window,"restoredStartupPlacement",false);window.Close();}}
        finally
        {
            foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);
        }
        return result;
    }
}

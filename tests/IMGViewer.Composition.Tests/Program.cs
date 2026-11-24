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
                    var initialBrush=(LinearGradientBrush)Field(window,"markerBrush")!;
                    Check(initialBrush.GradientStops[0].Color==(Color)ColorConverter.ConvertFromString("#8B5CF6")&&initialBrush.GradientStops[1].Color==(Color)ColorConverter.ConvertFromString("#F36DAE"),"Default violet gradient incorrect");
                    var swatches=((System.Windows.Controls.WrapPanel)Field(window,"MarkerColors")!).Children.OfType<Button>().ToArray();
                    Check(swatches.Length==8,"Gradient/solid choices missing");
                    foreach(string key in new[]{"coral","ocean","gold","violet"})
                    {
                        var swatch=swatches.Single(button=>(string)button.Tag==key);Call(window,"MarkerColor_Click",swatch,new RoutedEventArgs());
                        Check(Field(window,"markerBrush") is LinearGradientBrush {IsFrozen:true},"Gradient selection failed: "+key);
                        Check(swatch.Content is TextBlock,"Gradient selection check missing: "+key);
                    }
                    Call(window,"MarkerColor_Click",swatches.Single(button=>(string)button.Tag=="#F24F57"),new RoutedEventArgs());
                    Check(Field(window,"markerBrush") is SolidColorBrush,"Solid color option broken");
                    Call(window,"MarkerColor_Click",swatches.Single(button=>(string)button.Tag=="violet"),new RoutedEventArgs());
                    var gradientGeometry=new RectangleGeometry(new Rect(20,20,300,180));gradientGeometry.Freeze();
                    var gradientMarker=Activator.CreateInstance(Type.GetNestedType("StyledMarker",BindingFlags.NonPublic)!,new object?[]{gradientGeometry,Field(window,"markerBrush"),4.0,false,null,null,new Rect(),0.0,false,null})!;
                    var arrowDragType=Nullable.GetUnderlyingType(Type.GetField("markerDrag",Flags)!.FieldType)!;
                    var arrowKind=Enum.Parse(Type.GetNestedType("MarkerKind",BindingFlags.NonPublic)!,"Arrow");
                    Set(window,"markerDrag",Activator.CreateInstance(arrowDragType,arrowKind,new Point(300,40)));Set(window,"markerEnd",new Point(40,180));
                    var directed=(LinearGradientBrush)Call(window,"MarkerPaint",gradientGeometry)!;
                    Check(directed.StartPoint.X>directed.EndPoint.X&&directed.StartPoint.Y<directed.EndPoint.Y,"Arrow gradient did not follow tail to tip");
                    Check(directed.IsFrozen&&directed.MappingMode==BrushMappingMode.RelativeToBoundingBox,"Arrow gradient should remain relative and frozen");
                    Set(window,"markerDrag",null);
                    var gradientDraft=Call(null,"DraftBrushGradient",Field(window,"markerBrush"))!;
                    Check(gradientDraft is not null,"Gradient draft data missing");
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
                    widthSlider.Value=25;arrowTool.IsChecked=true;widthSlider.Value=60;Call(window,"SelectElementMoveTool");
                    var photoElement=(Image)Field(window,"Photo")!;
                    System.Windows.Input.MouseButtonEventArgs Button(System.Windows.Input.MouseButton button)=>new(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,button){RoutedEvent=System.Windows.Input.Mouse.MouseDownEvent,Source=photoElement};
                    Call(window,"SelectElementMoveTool");
                    Call(window,"OnPreviewMouseDown",Button(System.Windows.Input.MouseButton.Left));
                    Check(Field(window,"viewerPressStart") is null,"Left click should not prepare window movement");
                    double originalZoom=((ScaleTransform)Field(window,"PhotoZoom")!).ScaleX;
                    Call(window,"OnPreviewMouseDown",Button(System.Windows.Input.MouseButton.Middle));
                    Check(Field(window,"viewerPressStart") is Point&&Field(window,"photoDragPosition") is null,"Middle click must prepare window movement, not image pan");
                    Call(window,"OnPreviewMouseUp",Button(System.Windows.Input.MouseButton.Middle));
                    Check(((ScaleTransform)Field(window,"PhotoZoom")!).ScaleX==originalZoom,"Middle click changed zoom");
                    ((ScaleTransform)Field(window,"PhotoZoom")!).ScaleX=((ScaleTransform)Field(window,"PhotoZoom")!).ScaleY=2;
                    Call(window,"Photo_MouseLeftButtonDown",photoElement,Button(System.Windows.Input.MouseButton.Left));
                    Check(Field(window,"photoDragPosition") is Point&&Field(window,"windowDragOffset") is null,"Left click with zoom did not start image panning");
                    Call(window,"OnPreviewMouseUp",Button(System.Windows.Input.MouseButton.Left));
                    Check(Field(window,"photoDragPosition") is null,"Left image pan was not released");
                    arrowTool.IsChecked=true;Call(window,"Photo_MouseLeftButtonDown",photoElement,Button(System.Windows.Input.MouseButton.Left));
                    Check(Field(window,"markerDrag") is not null&&Field(window,"photoDragPosition") is null,"Markup tool lost left-button priority");
                    window.ReleaseMouseCapture();Call(window,"SelectElementMoveTool");Call(window,"ResetPhotoView");
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
                    var gradientModel=Activator.CreateInstance(modelType,true)!;
                    modelType.GetProperty("Photo")!.SetValue(gradientModel,actualBase);modelType.GetProperty("Preview")!.SetValue(gradientModel,actualBase);
                    ((IList)modelType.GetField("Marks")!.GetValue(gradientModel)!).Add(gradientMarker);
                    var gradientSnapshot=Call(null,"CreateCaptureDraftSnapshot",gradientModel)!;Call(null,"WriteCaptureDraft",folder,gradientSnapshot);
                    string gradientFile=Directory.GetFiles(folder,"*.draft").Single(path=>path!=draft);
                    var gradientRecovered=Call(null,"ReadCaptureDraft",gradientFile)!;
                    var gradientRecoveredMark=((IList)modelType.GetField("Marks")!.GetValue(gradientRecovered)!)[0]!;
                    var recoveredBrush=(LinearGradientBrush)gradientRecoveredMark.GetType().GetProperty("Color")!.GetValue(gradientRecoveredMark)!;
                    Check(recoveredBrush.IsFrozen&&recoveredBrush.GradientStops.Count==2&&recoveredBrush.GradientStops[1].Color==initialBrush.GradientStops[1].Color,"Gradient lost on draft recovery");
                    Check(recoveredBrush.StartPoint==new Point(0,0)&&recoveredBrush.EndPoint==new Point(1,1),"Recovered gradient direction changed");
                    // Verify the same flattening used by clipboard/save includes both ends of the gradient.
                    markers.Add(gradientMarker);var gradientExport=(BitmapSource)Call(window,"FlattenMarkers",actualBase)!;
                    var gradientStart=Pixel(gradientExport,40,35);var gradientEnd=Pixel(gradientExport,290,180);
                    Check(!gradientStart.SequenceEqual(gradientEnd),"Export flattened gradient to a solid color");
                    markers.RemoveAt(markers.Count-1);
                    // Older drafts have raster blur patches without an editable-image flag.
                    using(var zip=ZipFile.Open(draft,ZipArchiveMode.Update))
                    {
                        var entry=zip.GetEntry("state.json")!;JsonNode state;using(var reader=new StreamReader(entry.Open()))state=JsonNode.Parse(reader.ReadToEnd())!;
                        foreach(var mark in state["Marks"]!.AsArray()){mark!.AsObject().Remove("IsPastedImage");mark.AsObject().Remove("Gradient");}entry.Delete();using var writer=new StreamWriter(zip.CreateEntry("state.json").Open());writer.Write(state.ToJsonString());
                    }
                    restored=Call(null,"ReadCaptureDraft",draft)!;restoredMarks=(IList)modelType.GetField("Marks")!.GetValue(restored)!;restoredMark=restoredMarks[0]!;
                    Check(!(bool)restoredMark.GetType().GetProperty("IsPastedImage")!.GetValue(restoredMark)!,"Older raster patches incorrectly became image layers");
                    Check(restoredMark.GetType().GetProperty("Color")!.GetValue(restoredMark) is SolidColorBrush,"Legacy solid-color draft compatibility broken");
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
                    // Selection tool moves vector/text elements while keeping images editable.
                    var rectangleGeometry=(Geometry)Call(null,"CreateRectangleMark",new Rect(40,40,160,100),5.0)!;
                    var arrowGeometry=(Geometry)Call(null,"CreateArrowMark",new Point(100,80),new Point(260,130),8.0)!;
                    var textGeometry=new FormattedText("Texto",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),24,Brushes.White,1).BuildGeometry(new Point(40,100));textGeometry.Freeze();
                    foreach(var elementGeometry in new[]{rectangleGeometry,arrowGeometry,textGeometry})
                    {
                        Call(window,"SelectElementMoveTool");
                        var element=Activator.CreateInstance(Type.GetNestedType("StyledMarker",BindingFlags.NonPublic)!,new object?[]{elementGeometry,initialBrush,5.0,false,null,null,new Rect(),0.0,false,null})!;
                        markers.Add(element);Call(window,"RefreshMarkerOverlay",(object?)null);window.UpdateLayout();
                        var elementBody=(Thumb)((IDictionary)Field(window,"pastedImageBodies")!)[0]!;elementBody.ApplyTemplate();
                        Check(VisualTreeHelper.GetChild(elementBody,0) is System.Windows.Shapes.Path,"Vector element must use a shape hit target");
                        if(ReferenceEquals(elementGeometry,rectangleGeometry))Check(!elementGeometry.FillContains(new Point(120,90)),"Rectangle interior should allow image pan");
                        var initialBounds=elementGeometry.Bounds;
                        var fullView=(Rect)Call(window,"PastedImageViewBounds",new Rect(0,0,actualBase.Width,actualBase.Height))!;
                        Call(window,"BeginPastedImageGesture",0,elementBody);
                        Call(window,"ChangePastedImage",0,null,20*fullView.Width/actualBase.Width,14*fullView.Height/actualBase.Height);
                        Call(window,"CompletePastedImageGesture",false);
                        var moved=markers[0]!;var movedGeometry=(Geometry)moved.GetType().GetProperty("Geometry")!.GetValue(moved)!;
                        Check(Near(movedGeometry.Bounds,new Rect(initialBounds.X+20,initialBounds.Y+14,initialBounds.Width,initialBounds.Height)),"Vector/text movement incorrect");
                        var corners=(IDictionary)Field(window,"pastedImageCorners")!;
                        Check(corners.Values.Cast<Thumb>().All(handle=>handle.Visibility==Visibility.Visible),"Selected markers should show resize handles");
                        var movedModel=Activator.CreateInstance(modelType,true)!;
                        modelType.GetProperty("Photo")!.SetValue(movedModel,actualBase);modelType.GetProperty("Preview")!.SetValue(movedModel,actualBase);
                        ((IList)modelType.GetField("Marks")!.GetValue(movedModel)!).Add(moved);
                        var movedSnapshot=Call(null,"CreateCaptureDraftSnapshot",movedModel)!;Call(null,"WriteCaptureDraft",folder,movedSnapshot);
                        string movedFile=Path.Combine(folder,((Guid)modelType.GetProperty("DraftId")!.GetValue(movedModel)!).ToString("N")+".draft");
                        var recoveredModel=Call(null,"ReadCaptureDraft",movedFile)!;
                        var recoveredElement=((IList)modelType.GetField("Marks")!.GetValue(recoveredModel)!)[0]!;
                        Check(Near(((Geometry)recoveredElement.GetType().GetProperty("Geometry")!.GetValue(recoveredElement)!).Bounds,movedGeometry.Bounds),"Moved element position lost on draft recovery");
                        Check((bool)Call(window,"DeleteSelectedPastedImage")!&&markers.Count==0,"Delete selected vector/text failed");
                        Call(window,"UndoWorkspace");Check(markers.Count==1&&ReferenceEquals(markers[0],moved),"Undo vector/text delete failed");
                        Call(window,"UndoWorkspace");Check(ReferenceEquals(markers[0],element),"Undo vector/text move failed");
                        Call(window,"BeginPastedImageGesture",0,elementBody);Call(window,"ChangePastedImage",0,null,12.0,12.0);Call(window,"CompletePastedImageGesture",true);
                        Check(ReferenceEquals(markers[0],element),"Cancel vector/text move failed");
                        Call(window,"UndoWorkspace");Check(markers.Count==0,"Undo original vector/text insertion failed");
                    }
                    // New editable element operations: metadata, properties, redo and duplication.
                    Call(window,"ClearMarkers");Call(window,"SelectElementMoveTool");
                    Call(window,"BeginInlineText",new Point(40,80),(double?)180);
                    ((TextBox)Field(window,"inlineText")!).Text="Texto inicial";
                    Call(window,"EndInlineText",true);
                    Check(markers.Count==1,"Text insertion failed");
                    object DefinitionOf(object mark)=>mark.GetType().GetProperty("Definition")!.GetValue(mark)!;
                    string Content(object mark)=>(string)DefinitionOf(mark).GetType().GetProperty("Text")!.GetValue(DefinitionOf(mark))!;
                    Check(Content(markers[0]!)=="Texto inicial","Text metadata missing");
                    Set(window,"selectedPastedImage",0);Call(window,"EditSelectedText");
                    Check(((TextBox)Field(window,"inlineText")!).Text=="Texto inicial","Reediting did not restore content");
                    ((TextBox)Field(window,"inlineText")!).Text="Texto corregido";Call(window,"EndInlineText",true);
                    Check(markers.Count==1&&Content(markers[0]!)=="Texto corregido","Text reedit duplicated or lost content");
                    Call(window,"UndoWorkspace");Check(Content(markers[0]!)=="Texto inicial","Text undo failed");
                    Call(window,"RedoWorkspace");Check(Content(markers[0]!)=="Texto corregido","Text redo failed");
                    Set(window,"selectedPastedImage",0);Call(window,"EditSelectedElement","shadow",40.0);
                    Check((double)markers[0]!.GetType().GetProperty("ShadowIntensity")!.GetValue(markers[0])! == .4,"Selected property failed");
                    Call(window,"UndoWorkspace");Check((bool)Type.GetProperty("CanRedoEdit",Flags)!.GetValue(window)!,"Property redo unavailable");
                    Call(window,"RedoWorkspace");Set(window,"selectedPastedImage",0);
                    Call(window,"DuplicateSelectedElement");Check(markers.Count==2&&Content(markers[1]!)=="Texto corregido","Duplicate lost editable metadata");
                    Call(window,"UndoWorkspace");Check(markers.Count==1,"Duplicate undo failed");
                    Call(window,"RedoWorkspace");Check(markers.Count==2,"Duplicate redo failed");
                    Set(window,"selectedPastedImage",0);Call(window,"EditSelectedElement","shadow",20.0);
                    Check(!(bool)Type.GetProperty("CanRedoEdit",Flags)!.GetValue(window)!,"New edit retained redo branch");
                    var textModel=Activator.CreateInstance(modelType,true)!;
                    modelType.GetProperty("Photo")!.SetValue(textModel,actualBase);modelType.GetProperty("Preview")!.SetValue(textModel,actualBase);
                    ((IList)modelType.GetField("Marks")!.GetValue(textModel)!).Add(markers[0]);
                    Call(null,"WriteCaptureDraft",folder,Call(null,"CreateCaptureDraftSnapshot",textModel)!);
                    string textFile=Path.Combine(folder,((Guid)modelType.GetProperty("DraftId")!.GetValue(textModel)!).ToString("N")+".draft");
                    var textRecovered=Call(null,"ReadCaptureDraft",textFile)!;
                    Check(Content(((IList)modelType.GetField("Marks")!.GetValue(textRecovered)!)[0]!)=="Texto corregido","Draft lost editable text");
                    Set(window,"selectedPastedImage",0);Call(window,"RefreshMarkerOverlay",(object?)null);window.UpdateLayout();
                    var resizeBody=(Thumb)((IDictionary)Field(window,"pastedImageBodies")!)[0]!;
                    var resizeBefore=(Geometry)markers[0]!.GetType().GetProperty("Geometry")!.GetValue(markers[0])!;
                    Call(window,"BeginPastedImageGesture",0,resizeBody);
                    Call(window,"ResizeMarker",0,"se",20.0,15.0);Call(window,"CompletePastedImageGesture",false);
                    Check(((Geometry)markers[0]!.GetType().GetProperty("Geometry")!.GetValue(markers[0])!).Bounds.Width>resizeBefore.Bounds.Width,"Marker resize failed");
                    Call(window,"UndoWorkspace");Check(Near(((Geometry)markers[0]!.GetType().GetProperty("Geometry")!.GetValue(markers[0])!).Bounds,resizeBefore.Bounds),"Marker resize undo failed");
                    var ellipse=(Geometry)Call(null,"CreateEllipseMark",new Rect(30,30,100,70),4.0)!;
                    Check(!ellipse.FillContains(new Point(80,65))&&ellipse.Bounds.Width>100,"Ellipse must be a hollow outline");
                    Check((Point)Call(null,"ConstrainCircle",new Point(100,100),new Point(40,70))! == new Point(70,70),"Shift circle constraint failed");
                    Call(window,"ClearMarkers");
                    // Arrow endpoint editing must rebuild, never anisotropically stretch its head.
                    var arrowDef=System.Text.Json.JsonSerializer.Deserialize(
                        System.Text.Json.JsonSerializer.Serialize(new {Kind=0,Bounds=new Rect(70,70,160,80),Start=new Point(70,70),End=new Point(230,150),Control=new Point(150,110),Form=0,ScaleX=1.0,ScaleY=1.0}),
                        Type.GetNestedType("ElementDefinition",BindingFlags.NonPublic)!)!;
                    var arrowElement=Activator.CreateInstance(Type.GetNestedType("StyledMarker",BindingFlags.NonPublic)!,new object?[]{arrowGeometry,initialBrush,8.0,false,null,null,new Rect(),.2,false,arrowDef})!;
                    markers.Add(arrowElement);Set(window,"selectedPastedImage",0);Call(window,"RefreshMarkerOverlay",(object?)null);window.UpdateLayout();
                    var arrowBody=(Thumb)((IDictionary)Field(window,"pastedImageBodies")!)[0]!;
                    Call(window,"BeginPastedImageGesture",0,arrowBody);
                    var arrowHandles=(IDictionary)Field(window,"pastedImageCorners")!;
                    Check(((Thumb)arrowHandles["nw"]!).Visibility==Visibility.Visible&&((Thumb)arrowHandles["se"]!).Visibility==Visibility.Visible&&((Thumb)arrowHandles["ne"]!).Visibility==Visibility.Collapsed,"Arrow must show only endpoint handles");
                    Call(window,"ResizeArrowEndpoint",0,false,40.0,-30.0);Call(window,"CompletePastedImageGesture",false);
                    var arrowAfter=markers[0]!;var editedDef=DefinitionOf(arrowAfter);
                    Check((Point)editedDef.GetType().GetProperty("End")!.GetValue(editedDef)! ==new Point(270,120),"Arrow tip endpoint incorrect");
                    Check((double)arrowAfter.GetType().GetProperty("Width")!.GetValue(arrowAfter)! ==8.0,"Endpoint edit changed stroke width");
                    var expectedArrow=(Geometry)Call(null,"CreateArrowMark",new Point(70,70),new Point(270,120),8.0)!;
                    Check(Near(((Geometry)arrowAfter.GetType().GetProperty("Geometry")!.GetValue(arrowAfter)!).Bounds,expectedArrow.Bounds),"Arrow silhouette was stretched instead of rebuilt");
                    Call(window,"UndoWorkspace");Check(ReferenceEquals(markers[0],arrowElement),"Arrow endpoint undo failed");
                    Call(window,"RedoWorkspace");Check(ReferenceEquals(markers[0],arrowAfter),"Arrow endpoint redo failed");
                    Call(window,"BeginPastedImageGesture",0,arrowBody);Call(window,"ResizeArrowEndpoint",0,true,10.0,10.0);Call(window,"CompletePastedImageGesture",true);
                    Check(ReferenceEquals(markers[0],arrowAfter),"Arrow endpoint cancellation failed");
                    Call(window,"ClearMarkers");
                    // Crop stays pending after dragging; explicit accept/Enter commits, cancel is non-destructive.
                    var cropBase=((Image)Field(window,"Photo")!).Source;
                    Call(window,"EnableInlineCrop");
                    var cropActions=(Border)Field(window,"inlineCropActions")!;
                    Check(cropActions.IsVisible&&((StackPanel)cropActions.Child).Children.Count==2,"Crop confirm/cancel actions missing");
                    Set(window,"inlineCropBounds",new Rect(10,20,120,90));Set(window,"inlineCropPending",true);
                    Call(window,"PreviewGestureCrop");
                    Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,cropBase)&&(bool)Field(window,"inlineCropPending")!,"Gesture selection must wait for confirmation");
                    Call(window,"InlineCrop_DragCompleted",new Thumb(),new DragCompletedEventArgs(0,0,false));
                    Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,cropBase),"Crop committed before confirmation");
                    ((Button)((StackPanel)cropActions.Child).Children[1]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,cropBase)&&Field(window,"inlineCropBitmap") is null,"Cancel modified image");
                    Call(window,"EnableInlineCrop");Set(window,"inlineCropBounds",new Rect(10,20,120,90));Set(window,"inlineCropPending",true);
                    System.Windows.Input.Keyboard.Focus(window);
                    var enter=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,System.Windows.Input.Key.Enter) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};
                    Check((bool)Call(window,"HandleInlineCropKey",enter)!&&enter.Handled,"Enter did not accept crop");
                    var cropped=(BitmapSource)((Image)Field(window,"Photo")!).Source;
                    Check(cropped.PixelWidth==120&&cropped.PixelHeight==90&&Field(window,"inlineCropBitmap") is null,"Accepted crop failed");
                    Call(window,"UndoWorkspace");Check(ReferenceEquals(((Image)Field(window,"Photo")!).Source,cropBase),"Confirmed crop undo failed");
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

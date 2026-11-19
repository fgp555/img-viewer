using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMGViewer;

public partial class MainWindow
{
    private enum MarkerKind { Arrow, Rectangle, Freehand, Text, Blur, Crop }
    private enum ArrowForm { Classic, Tapered, Curved }
    private ArrowForm? dragArrowForm;
    private sealed record ArrowShape(Point Start, Point End, Point Control);

    private sealed record StyledMarker(Geometry Geometry, Brush Color, double Width, bool Contrast, ArrowShape? Curve = null, BitmapSource? Patch = null, Rect PatchBounds = default, double ShadowIntensity = 0);
    private readonly List<StyledMarker> markers = [];
    private (MarkerKind Kind, Point Start)? markerDrag;
    private readonly List<Point> freehandPoints = [];
    private Point stabilizedFreehandPoint;
    private double freehandStabilizerRadius;
    private readonly List<double> freehandPressure = [];
    private readonly System.Diagnostics.Stopwatch freehandClock = new();
    private double previousFreehandTime;
    private double filteredBrushPressure = 0.7;
    private bool freehandBrush;

    private bool TryStartMarker(MouseButtonEventArgs e)
    {
        MarkerKind? kind = Keyboard.Modifiers switch
        {
            ModifierKeys.Control | ModifierKeys.Alt => MarkerKind.Crop,
            ModifierKeys.Control | ModifierKeys.Shift => MarkerKind.Arrow,
            ModifierKeys.Alt | ModifierKeys.Shift => MarkerKind.Freehand,
            ModifierKeys.Alt => MarkerKind.Arrow,
            ModifierKeys.Shift => MarkerKind.Rectangle,
            ModifierKeys.None when !zenMode => activeMarkerTool,
            _ => null
        };

        if (kind is null || Photo.Source is not BitmapSource bitmap ||
            photoDragPosition.HasValue || windowDragOffset.HasValue ||
            Photo.ActualWidth <= 0 || Photo.ActualHeight <= 0)
        {
            return false;
        }

        if (CaptureMouse())
        {
            dragArrowForm = kind == MarkerKind.Arrow
                ? Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
                    ? ArrowForm.Curved : (ArrowForm)(ArrowFormPicker?.SelectedIndex ?? 0)
                : null;
            freehandPoints.Clear();
            freehandPressure.Clear(); freehandPressure.Add(0.7);
            freehandClock.Restart(); previousFreehandTime = 0; filteredBrushPressure = 0.7;
            freehandBrush = FreehandFinishPicker?.SelectedIndex == 1;
            freehandPoints.Add(GetMarkerPoint(e, bitmap));
            stabilizedFreehandPoint = freehandPoints[0];
            // Keep the leash distance in screen units, independent of image resolution and zoom.
            double screenScale = Photo.ActualWidth / bitmap.Width * Math.Max(0.001, PhotoZoom.ScaleX);
            freehandStabilizerRadius = (FreehandStabilizerSlider?.Value ?? 12) / Math.Max(0.001, screenScale);
            SetSlideShowPlaying(false);
            markerDrag = (kind.Value, GetMarkerPoint(e, bitmap));
            Cursor = Cursors.Cross;
            ForceCursor = true;
        }

        return true;
    }

    private Point GetMarkerPoint(MouseEventArgs e, BitmapSource bitmap)
    {
        Point point = e.GetPosition(Photo);
        return new Point(
            Math.Clamp(point.X / Photo.ActualWidth, 0, 1) * bitmap.Width,
            Math.Clamp(point.Y / Photo.ActualHeight, 0, 1) * bitmap.Height);
    }

    private Geometry? GetMarkerGeometry(MouseEventArgs e, BitmapSource bitmap)
    {
        if (markerDrag is not { } drag)
        {
            return null;
        }

        Point end = GetMarkerPoint(e, bitmap);
        if (drag.Kind == MarkerKind.Freehand)
        {
            end = StabilizeFreehandPoint(stabilizedFreehandPoint, end, freehandStabilizerRadius);
            stabilizedFreehandPoint = end;
            double freehandStroke = MarkerStroke(bitmap);
            double sampleDistance = Math.Max(0.6, freehandStroke * 0.12);
            if (freehandPoints.Count == 0 || (end - freehandPoints[^1]).Length >= sampleDistance)
            {
                double now = freehandClock.Elapsed.TotalSeconds;
                double scale = Photo.ActualWidth / bitmap.Width * PhotoZoom.ScaleX;
                double speed = freehandPoints.Count == 0 ? 0 : (end-freehandPoints[^1]).Length * scale / Math.Max(0.008, now-previousFreehandTime);
                double target = 0.3 + 0.9 / (1 + speed / 320);
                filteredBrushPressure += (target-filteredBrushPressure)*0.25;
                freehandPoints.Add(end); freehandPressure.Add(filteredBrushPressure); previousFreehandTime=now;
            }
            return freehandBrush ? CreateBrushFreehand(freehandPoints, freehandPressure, freehandStroke)
                : CreateSmoothFreehand(freehandPoints, end, freehandStroke);
        }
        if (drag.Kind is MarkerKind.Blur or MarkerKind.Text or MarkerKind.Crop) return new RectangleGeometry(new Rect(drag.Start, end));
        Vector direction = end - drag.Start;
        double stroke = MarkerStroke(bitmap);
        if (direction.Length < stroke)
        {
            return null;
        }

        if (drag.Kind == MarkerKind.Rectangle)
        {
            return Math.Abs(direction.X) >= stroke && Math.Abs(direction.Y) >= stroke
                ? CreateRectangleMark(new Rect(drag.Start, end), stroke)
                : null;
        }

        return SelectedArrowForm == ArrowForm.Classic ? CreateArrowMark(drag.Start, end, stroke) : CreateTaperedArrow(drag.Start, end, DefaultCurveControl(drag.Start, end, SelectedArrowForm == ArrowForm.Curved), stroke);
    }

    private static Geometry CreateBrushFreehand(IReadOnlyList<Point> points, IReadOnlyList<double> pressure, double stroke)
    {
        if (points.Count < 2) return Geometry.Empty;
        var smooth=points.ToArray();
        for(int pass=0;pass<2;pass++)
        {
            var previous=smooth.ToArray();
            for(int i=1;i<smooth.Length-1;i++) smooth[i]=new Point((previous[i-1].X+2*previous[i].X+previous[i+1].X)/4,
                (previous[i-1].Y+2*previous[i].Y+previous[i+1].Y)/4);
        }
        var samples=new List<Point>(); var widths=new List<double>();
        double total=0;
        for(int i=1;i<smooth.Length;i++) total+=(smooth[i]-smooth[i-1]).Length;
        double travelled=0;
        double taperLength=Math.Min(total*0.24,stroke*4);
        for(int i=0;i<smooth.Length-1;i++)
        {
            Point a=smooth[i], b=smooth[i+1];
            Point before=i>0?smooth[i-1]:a, after=i+2<smooth.Length?smooth[i+2]:b;
            double segment=(b-a).Length;
            int steps=Math.Clamp((int)Math.Ceiling(segment/Math.Max(0.7,stroke*0.2)),1,64);
            for(int j=0;j<steps;j++)
            {
                double t=(double)j/steps, t2=t*t,t3=t2*t;
                Point point=new(0.5*((2*a.X)+(-before.X+b.X)*t+(2*before.X-5*a.X+4*b.X-after.X)*t2+(-before.X+3*a.X-3*b.X+after.X)*t3),
                    0.5*((2*a.Y)+(-before.Y+b.Y)*t+(2*before.Y-5*a.Y+4*b.Y-after.Y)*t2+(-before.Y+3*a.Y-3*b.Y+after.Y)*t3));
                double distance=travelled+segment*t;
                double taper=Math.Min(1,Math.Min(distance,total-distance)/Math.Max(0.1,taperLength));
                taper=0.12+0.88*Math.Sin(Math.Clamp(taper,0,1)*Math.PI/2);
                double weight=pressure[Math.Min(i,pressure.Count-1)]*(1-t)+pressure[Math.Min(i+1,pressure.Count-1)]*t;
                samples.Add(point); widths.Add(Math.Max(0.2,stroke*weight*taper/2));
            }
            travelled+=segment;
        }
        samples.Add(smooth[^1]); widths.Add(Math.Max(0.2,stroke*0.06));
        var group=new GeometryGroup { FillRule=FillRule.Nonzero };
        for(int i=0;i<samples.Count;i++)
        {
            group.Children.Add(new EllipseGeometry(samples[i],widths[i],widths[i]));
            if(i==0) continue;
            Vector direction=samples[i]-samples[i-1];
            if(direction.Length<0.001) continue;
            direction.Normalize(); Vector normal=new(-direction.Y,direction.X);
            var strip=new StreamGeometry();
            using(var context=strip.Open())
            {
                context.BeginFigure(samples[i-1]+normal*widths[i-1],true,true);
                context.LineTo(samples[i-1]-normal*widths[i-1],true,false);
                context.LineTo(samples[i]-normal*widths[i],true,false);
                context.LineTo(samples[i]+normal*widths[i],true,false);
            }
            group.Children.Add(strip);
        }
        group.Freeze(); return group;
    }

    private static Point StabilizeFreehandPoint(Point previous, Point pointer, double radius)
    {
        if (radius <= 0) return pointer;
        Vector delta = pointer - previous;
        double distance = delta.Length;
        // The stroke follows only when the pointer pulls the virtual leash taut.
        return distance <= radius ? previous : previous + delta * ((distance - radius) / distance);
    }

    private static Geometry CreateSmoothFreehand(IReadOnlyList<Point> samples, Point end, double stroke)
    {
        if (samples.Count == 0) return Geometry.Empty;
        var raw = samples.ToList();
        if ((raw[^1] - end).Length > 0.001) raw.Add(end);
        // Arc-length resampling prevents slow pointer movement from dominating the fit.
        double length = 0;
        for (int i = 1; i < raw.Count; i++) length += (raw[i] - raw[i-1]).Length;
        if (length < 0.001) return new EllipseGeometry(raw[0], stroke/2, stroke/2);
        double spacing = Math.Max(Math.Clamp(stroke * 0.85, 2, 6), length / 4096);
        var points = new List<Point> { raw[0] };
        double remaining = spacing;
        for (int i = 1; i < raw.Count; i++)
        {
            Point start = raw[i-1];
            Vector segment = raw[i] - start;
            double distance = segment.Length;
            if (distance < 0.001) continue;
            Vector direction = segment / distance;
            while (distance >= remaining)
            {
                start += direction * remaining;
                points.Add(start);
                distance -= remaining;
                remaining = spacing;
            }
            remaining -= distance;
        }
        if ((points[^1] - end).Length < spacing * 0.3 && points.Count > 1) points[^1] = end;
        else if ((points[^1] - end).Length > 0.001) points.Add(end);

        // Two small spatial filters remove jitter; endpoints remain exact.
        for (int pass = 0; pass < 2; pass++)
        {
            var previous = points.ToArray();
            for (int i = 1; i < points.Count-1; i++)
            {
                Point a = previous[Math.Max(0,i-2)], b = previous[i-1], c = previous[i],
                    d = previous[i+1], e = previous[Math.Min(previous.Length-1,i+2)];
                points[i] = new Point((a.X+4*b.X+6*c.X+4*d.X+e.X)/16,
                    (a.Y+4*b.Y+6*c.Y+4*d.Y+e.Y)/16);
            }
        }
        var path = new StreamGeometry();
        using (var context = path.Open())
        {
            context.BeginFigure(points[0], false, false);
            for (int i=0; i<points.Count-1; i++)
            {
                Point start=points[i], finish=points[i+1];
                Point before=i>0 ? points[i-1] : start;
                Point after=i+2<points.Count ? points[i+2] : finish;
                Vector first=(finish-before)/6, second=(after-start)/6;
                double limit=(finish-start).Length*0.45;
                if(first.Length>limit && first.Length>0) first*=limit/first.Length;
                if(second.Length>limit && second.Length>0) second*=limit/second.Length;
                context.BezierTo(start+first,finish-second,finish,true,true);
            }
        }
        var result = path.GetWidenedPathGeometry(new Pen(Brushes.Black, stroke)
        { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round },
            0.1, ToleranceType.Absolute);
        result.Freeze();
        return result;
    }

    private static Geometry CreateRectangleMark(Rect bounds, double stroke)
    {
        double radius = Math.Min(stroke * 1.5, Math.Min(bounds.Width, bounds.Height) * 0.12);
        var outline = new RectangleGeometry(bounds, radius, radius);
        return outline.GetWidenedPathGeometry(new Pen(Brushes.Black, stroke) { LineJoin = PenLineJoin.Round });
    }

    private static Geometry CreateArrowMark(Point start, Point end, double stroke)
    {
        Vector direction = end - start;
        double length = direction.Length;
        if (length < 0.001) return Geometry.Empty;
        double headLength = Math.Min(length * 0.45, stroke * 3.4);
        double headWidth = headLength * (1.6 / 3.4);
        direction.Normalize();
        Vector perpendicular = new(-direction.Y, direction.X);
        Point neck = end - direction * headLength;
        // Overlap the rounded shaft and triangular head to avoid a seam at export.
        var shaft = new LineGeometry(start, neck + direction * Math.Min(stroke, headLength * 0.25))
            .GetWidenedPathGeometry(new Pen(Brushes.Black, stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        var head = new StreamGeometry();
        using (var context = head.Open())
        {
            context.BeginFigure(end, true, true);
            context.LineTo(neck + perpendicular * headWidth, true, false);
            context.LineTo(neck - perpendicular * headWidth, true, false);
        }
        var result = Geometry.Combine(shaft, head, GeometryCombineMode.Union, null);
        result.Freeze();
        return result;
    }

    private ArrowForm SelectedArrowForm => dragArrowForm ?? (ArrowForm)(ArrowFormPicker?.SelectedIndex ?? 0);
    private static Point DefaultCurveControl(Point start, Point end, bool curve)
    {
        Vector delta = end - start;
        return start + delta * 0.5 + (curve ? new Vector(-delta.Y, delta.X) * 0.32 : new Vector());
    }
    private static Geometry CreateTaperedArrow(Point start, Point end, Point control, double stroke)
    {
        Vector chord = end - start;
        double length = chord.Length;
        if (length < 0.01) return Geometry.Empty;
        double headLength = Math.Min(length * 0.35, Math.Max(stroke * 5, length * 0.12));
        double neckT = Math.Clamp(1 - headLength / length, 0.55, 0.95);
        Point At(double t) => new((1-t)*(1-t)*start.X + 2*(1-t)*t*control.X + t*t*end.X,
            (1-t)*(1-t)*start.Y + 2*(1-t)*t*control.Y + t*t*end.Y);
        Vector Normal(double t)
        {
            Vector tangent = (control-start)*(2*(1-t)) + (end-control)*(2*t);
            if (tangent.Length < 0.001) tangent = chord;
            tangent.Normalize(); return new Vector(-tangent.Y,tangent.X);
        }
        var left = new List<Point>(); var right = new List<Point>();
        const int segments = 48;
        for (int i=0;i<=segments;i++)
        {
            double t = neckT*i/segments;
            double halfWidth = stroke * (0.42 + 0.83 * Math.Pow(t/neckT,0.85));
            left.Add(At(t)+Normal(t)*halfWidth);
            right.Add(At(t)-Normal(t)*halfWidth);
        }
        Point neck = At(neckT); Vector normal = Normal(neckT);
        double wing = Math.Max(stroke*2.2,headLength*0.45);
        Vector tangent = new(normal.Y, -normal.X);
        // Keep a shallow harpoon notch, with softened barbs and a flat-cut tail.
        Point barbBase = neck - tangent * (headLength * 0.18);
        var geometry = new StreamGeometry();
        using(var context=geometry.Open())
        {
            void RoundedBarb(Point previous, Point corner, Point next)
            {
                Vector incoming = previous - corner, outgoing = next - corner;
                double radius = Math.Min(stroke * 0.7, Math.Min(incoming.Length, outgoing.Length) * 0.22);
                incoming.Normalize(); outgoing.Normalize();
                context.LineTo(corner + incoming * radius, true, false);
                context.QuadraticBezierTo(corner, corner + outgoing * radius, true, false);
            }
            context.BeginFigure(left[0],true,true);
            context.PolyLineTo(left.Skip(1).ToArray(),true,false);
            RoundedBarb(left[^1],barbBase+normal*wing,end);
            context.LineTo(end,true,false);
            RoundedBarb(end,barbBase-normal*wing,right[^1]);
            right.Reverse(); context.PolyLineTo(right,true,false);
        }
        geometry.Freeze(); return geometry;
    }

    private void CurveHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (markers.Count == 0 || markers[^1].Curve is not { } curve) return;
        Point pointer = Mouse.GetPosition(Photo);
        Point control = new(pointer.X / Photo.ActualWidth * ((BitmapSource)Photo.Source).Width,
            pointer.Y / Photo.ActualHeight * ((BitmapSource)Photo.Source).Height);
        // The handle lies on the curve at t=.5, not on the off-curve Bezier control.
        control = new Point(2*control.X - (curve.Start.X+curve.End.X)*0.5,
            2*control.Y - (curve.Start.Y+curve.End.Y)*0.5);
        var updated = curve with { Control = control };
        var marker=markers[^1];
        markers[^1]=marker with { Geometry=CreateTaperedArrow(updated.Start,updated.End,updated.Control,marker.Width), Curve=updated };
        RefreshMarkerOverlay(); UpdatePendingSaveIndicator();
    }
    private void UpdateCurveHandle()
    {
        if (CurveHandle is null) return;
        if (zenMode || windowedMode.HasValue || IsFolderGridOpen || markers.Count==0 || markers[^1].Curve is not { } curve || Photo.Source is not BitmapSource bitmap)
        { CurveHandle.Visibility=Visibility.Collapsed; return; }
        Point mid = new((curve.Start.X+2*curve.Control.X+curve.End.X)/4,
            (curve.Start.Y+2*curve.Control.Y+curve.End.Y)/4);
        Point screen=Photo.TranslatePoint(new Point(mid.X/bitmap.Width*Photo.ActualWidth,mid.Y/bitmap.Height*Photo.ActualHeight),PhotoViewport);
        System.Windows.Controls.Canvas.SetLeft(CurveHandle,screen.X-7);
        System.Windows.Controls.Canvas.SetTop(CurveHandle,screen.Y-7);
        CurveHandle.Visibility=Visibility.Visible;
    }

    private bool MarkerContrast => MarkerStylePicker?.SelectedIndex == 1;


    private void UpdateMarkerPreview(MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ReleaseMouseCapture();
            return;
        }

        if (Photo.Source is BitmapSource bitmap)
        {
            RefreshMarkerOverlay(GetMarkerGeometry(e, bitmap));
        }
    }

    private void CompleteMarker(MouseButtonEventArgs e)
    {
        if (markerDrag is { Kind: MarkerKind.Crop } crop && Photo.Source is BitmapSource cropBitmap)
        {
            Point end = GetMarkerPoint(e, cropBitmap);
            Rect selection = new(crop.Start, end);
            ReleaseMouseCapture();
            if (selection.Width * Photo.ActualWidth / cropBitmap.Width < SystemParameters.MinimumHorizontalDragDistance ||
                selection.Height * Photo.ActualHeight / cropBitmap.Height < SystemParameters.MinimumVerticalDragDistance) return;
            EnableInlineCrop();
            inlineCropBounds = new Rect(selection.X * cropBitmap.PixelWidth / cropBitmap.Width,
                selection.Y * cropBitmap.PixelHeight / cropBitmap.Height,
                selection.Width * cropBitmap.PixelWidth / cropBitmap.Width,
                selection.Height * cropBitmap.PixelHeight / cropBitmap.Height);
            inlineCropPending = true;
            ScheduleGestureCrop();
            return;
        }
        if (markerDrag is { Kind: MarkerKind.Text } textDrag && Photo.Source is BitmapSource textBitmap)
        {
            Point end = GetMarkerPoint(e, textBitmap);
            Rect bounds = new(textDrag.Start, end);
            bool fixedWidth = bounds.Width * Photo.ActualWidth / textBitmap.Width >= SystemParameters.MinimumHorizontalDragDistance;
            Point origin = fixedWidth ? bounds.TopLeft : textDrag.Start;
            ReleaseMouseCapture();
            BeginInlineText(origin, fixedWidth ? bounds.Width : null);
            return;
        }
        if (Photo.Source is BitmapSource bitmap && GetMarkerGeometry(e, bitmap) is { } geometry)
        {
            if (markerDrag is { Kind: MarkerKind.Blur } blur)
            {
                Rect bounds = new(blur.Start, GetMarkerPoint(e, bitmap));
                if (bounds.Width >= 2 && bounds.Height >= 2)
                {
                    var patch = CreateBlurPatch(FlattenMarkers(bitmap), bounds);
                    markers.Add(new StyledMarker(geometry, markerBrush, MarkerStroke(bitmap), false, Patch: patch, PatchBounds: bounds));
                }
                ReleaseMouseCapture(); UpdatePendingSaveIndicator(); return;
            }
            ArrowShape? curve = markerDrag is { Kind: MarkerKind.Arrow } drag && SelectedArrowForm == ArrowForm.Curved
                ? new ArrowShape(drag.Start, GetMarkerPoint(e, bitmap), DefaultCurveControl(drag.Start, GetMarkerPoint(e, bitmap), true)) : null;
            markers.Add(new StyledMarker(geometry, markerBrush, MarkerStroke(bitmap), MarkerContrast, curve, ShadowIntensity: markerDrag is { Kind: MarkerKind.Arrow } && SelectedArrowForm == ArrowForm.Classic ? (ArrowShadowSlider?.Value ?? 20) / 100 : 0));
        }

        ReleaseMouseCapture();
        UpdatePendingSaveIndicator();
    }

    private void UndoMarker()
    {
        if (markers.Count > 0)
        {
            markers.RemoveAt(markers.Count - 1);
            RefreshMarkerOverlay();
            UpdatePendingSaveIndicator();
        }
    }

    private void ClearMarkers()
    {
        if(!preserveCropUndo) cropUndo.Clear();
        EndInlineText(false);
        markers.Clear();
        if (CurveHandle is not null) CurveHandle.Visibility = Visibility.Collapsed;
        MarkerOverlay.Source = null;
    }

    private void RefreshMarkerOverlay(Geometry? preview = null)
    {
        UpdateCurveHandle();
        if (Photo.Source is not BitmapSource bitmap || (markers.Count == 0 && preview is null))
        {
            MarkerOverlay.Source = null;
            return;
        }

        var group = new DrawingGroup();
        using (DrawingContext drawing = group.Open())
        {
            drawing.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, bitmap.Width, bitmap.Height));
            DrawMarkers(drawing, bitmap, preview);
        }

        MarkerOverlay.Source = new DrawingImage(group);
    }

    private void DrawMarkers(DrawingContext drawing, BitmapSource bitmap, Geometry? preview)
    {
        drawing.PushClip(new RectangleGeometry(new Rect(0, 0, bitmap.Width, bitmap.Height)));
        foreach (StyledMarker marker in markers) DrawStyledMark(drawing, marker);
        if (preview is not null && markerDrag is { Kind: MarkerKind.Blur or MarkerKind.Text or MarkerKind.Crop })
            drawing.DrawGeometry(new SolidColorBrush(Color.FromArgb(60, 100, 160, 230)), new Pen(Brushes.White, 1), preview);
        else if (preview is not null)
            DrawStyledMark(drawing, new StyledMarker(preview, markerBrush, MarkerStroke(bitmap), MarkerContrast, ShadowIntensity: markerDrag is { Kind: MarkerKind.Arrow } && SelectedArrowForm == ArrowForm.Classic ? (ArrowShadowSlider?.Value ?? 20) / 100 : 0));
        drawing.Pop();
    }

    private static void DrawStyledMark(DrawingContext drawing, StyledMarker marker)
    {
        if (marker.Patch is not null) { drawing.DrawImage(marker.Patch, marker.PatchBounds); return; }
        if (marker.ShadowIntensity > 0)
        {
            // Render the same soft shadow in the preview, clipboard and saved bitmap.
            drawing.PushTransform(new TranslateTransform(marker.Width * 0.18, marker.Width * 0.35));
            var shadow = new SolidColorBrush(Color.FromArgb((byte)(30 * marker.ShadowIntensity), 0, 0, 0));
            for (int layer = 6; layer >= 1; layer--)
                drawing.DrawGeometry(null, new Pen(shadow, marker.Width * layer * 0.22) { LineJoin = PenLineJoin.Round }, marker.Geometry);
            drawing.DrawGeometry(new SolidColorBrush(Color.FromArgb((byte)(110 * marker.ShadowIntensity), 0, 0, 0)), null, marker.Geometry);
            drawing.Pop();
        }
        if (marker.Contrast)
        {
            // Two neutral outlines stay readable against both white UI and dark photos.
            drawing.DrawGeometry(null, new Pen(Brushes.Black, Math.Max(1.5, marker.Width * 0.85)) { LineJoin = PenLineJoin.Round }, marker.Geometry);
            drawing.DrawGeometry(null, new Pen(Brushes.White, Math.Max(0.75, marker.Width * 0.4)) { LineJoin = PenLineJoin.Round }, marker.Geometry);
        }
        drawing.DrawGeometry(marker.Color, null, marker.Geometry);
    }

    private double MarkerStroke(BitmapSource bitmap) =>
        Math.Max(1, Math.Max(1, (MarkerWidthSlider?.Value ?? 60) * 0.16) * Math.Min(bitmap.Width, bitmap.Height) / 700);

    private BitmapSource FlattenMarkers(BitmapSource bitmap)
    {
        if (markers.Count == 0)
        {
            return bitmap;
        }

        var visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawImage(bitmap, new Rect(0, 0, bitmap.Width, bitmap.Height));
            DrawMarkers(drawing, bitmap, null);
        }

        var result = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight,
            bitmap.DpiX, bitmap.DpiY, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace IMGViewer;
public partial class MainWindow
{
    private DispatcherTimer? galleryScrollTimer;
    private ScrollViewer? galleryAnimatedScroller;
    private double galleryScrollTarget;
    private static T? GalleryChild<T>(DependencyObject? node, string? name = null) where T : FrameworkElement
    {
        if (node is null) return null;
        if (node is T match && (name is null || match.Name == name)) return match;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            if(GalleryChild<T>(VisualTreeHelper.GetChild(node,i),name) is { } child) return child;
        return null;
    }
    private void StopGalleryScroll()
    {
        galleryScrollTimer?.Stop(); galleryScrollTimer=null; galleryAnimatedScroller=null;
    }
    private void Gallery_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ListBox list || e.Delta == 0 || SystemParameters.WheelScrollLines == 0 || GalleryChild<ScrollViewer>(list) is not { } scroll) return;
        e.Handled=true;
        double basis=ReferenceEquals(galleryAnimatedScroller,scroll)?galleryScrollTarget:scroll.VerticalOffset;
        double step=SystemParameters.WheelScrollLines < 0 ? scroll.ViewportHeight : SystemParameters.WheelScrollLines*18;
        AnimateGalleryScroll(scroll,basis-e.Delta/120.0*step);
    }
    private void AnimateGalleryScroll(ScrollViewer scroll, double target)
    {
        double start=scroll.VerticalOffset;
        StopGalleryScroll();
        galleryScrollTarget=Math.Clamp(target,0,scroll.ScrollableHeight);
        if(!SystemParameters.ClientAreaAnimation) { scroll.ScrollToVerticalOffset(galleryScrollTarget); return; }
        galleryAnimatedScroller=scroll;
        var clock=Stopwatch.StartNew();
        var timer=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(16) };
        galleryScrollTimer=timer;
        timer.Tick += (_,_) => {
            if (!IsVisible || !scroll.IsVisible) { StopGalleryScroll(); return; }
            double progress=Math.Min(1,clock.Elapsed.TotalMilliseconds/150);
            double eased=1-Math.Pow(1-progress,3);
            scroll.ScrollToVerticalOffset(start+(galleryScrollTarget-start)*eased);
            if(progress>=1) StopGalleryScroll();
        };
        timer.Start();
    }
    private void RevealGalleryPhoto(GalleryPhoto photo)
    {
        Dispatcher.InvokeAsync(() => {
            if(galleryRenamePhoto is not null || !GalleryList.IsVisible || !ReferenceEquals(GalleryList.SelectedItem,photo)) return;
            if(GalleryChild<ScrollViewer>(GalleryList) is not { } scroll) return;
            if(GalleryList.ItemContainerGenerator.ContainerFromItem(photo) is not FrameworkElement tile)
            { StopGalleryScroll(); GalleryList.ScrollIntoView(photo); return; }
            Rect bounds=tile.TransformToAncestor(scroll).TransformBounds(new Rect(tile.RenderSize));
            double target=scroll.VerticalOffset;
            if(bounds.Height>scroll.ViewportHeight) { if(bounds.Top<0 || bounds.Top>scroll.ViewportHeight) target+=bounds.Top; }
            else if(bounds.Top<0) target+=bounds.Top;
            else if(bounds.Bottom>scroll.ViewportHeight) target+=bounds.Bottom-scroll.ViewportHeight;
            if(Math.Abs(target-scroll.VerticalOffset)>0.5) AnimateGalleryScroll(scroll,target);
        },DispatcherPriority.Loaded);
    }
}

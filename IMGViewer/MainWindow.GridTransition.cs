using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace IMGViewer;

public partial class MainWindow
{
    private static readonly TimeSpan GridOpenDuration = TimeSpan.FromMilliseconds(200);

    private Rect GetGridTransitionStart(Button tile)
    {
        if (!SystemParameters.ClientAreaAnimation || ActiveGridScroller is not { } scroller)
        {
            return Rect.Empty;
        }

        var visual = tile.Template.FindName("Tile", tile) as FrameworkElement ?? tile;
        Rect start = visual.TransformToAncestor(PhotoViewport).TransformBounds(new Rect(visual.RenderSize));
        Rect viewport = scroller.TransformToAncestor(PhotoViewport).TransformBounds(new Rect(scroller.RenderSize));
        start.Intersect(viewport);
        return start;
    }

    private void AnimateGridPhoto(Rect start)
    {
        if (start.IsEmpty || start.Width < 1 || start.Height < 1 || Photo.Source is null)
        {
            return;
        }

        PhotoViewport.UpdateLayout();
        Rect end = Photo.TransformToAncestor(PhotoViewport).TransformBounds(new Rect(Photo.RenderSize));
        end.Intersect(new Rect(PhotoViewport.RenderSize));
        if (end.IsEmpty || end.Width < 1 || end.Height < 1)
        {
            return;
        }

        GridOpenImage.Source = Photo.Source;
        GridOpenImage.Visibility = Visibility.Visible;
        Canvas.SetLeft(GridOpenImage, end.Left);
        Canvas.SetTop(GridOpenImage, end.Top);
        GridOpenImage.Width = end.Width;
        GridOpenImage.Height = end.Height;

        GridOpenImage.BeginAnimation(Canvas.LeftProperty, GridOpenAnimation(start.Left, end.Left));
        GridOpenImage.BeginAnimation(Canvas.TopProperty, GridOpenAnimation(start.Top, end.Top));
        GridOpenImage.BeginAnimation(WidthProperty, GridOpenAnimation(start.Width, end.Width));
        GridOpenImage.BeginAnimation(HeightProperty, GridOpenAnimation(start.Height, end.Height));

        var reveal = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        reveal.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        reveal.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))));
        reveal.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(GridOpenDuration)));
        Photo.BeginAnimation(OpacityProperty, reveal);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(70))
        {
            BeginTime = TimeSpan.FromMilliseconds(130),
            FillBehavior = FillBehavior.Stop
        };
        fade.Completed += (_, _) => StopGridPhotoAnimation();
        GridOpenImage.BeginAnimation(OpacityProperty, fade);
    }

    private static DoubleAnimation GridOpenAnimation(double from, double to) => new(from, to, GridOpenDuration)
    {
        FillBehavior = FillBehavior.Stop,
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };

    private void StopGridPhotoAnimation()
    {
        if (GridOpenImage.Visibility != Visibility.Visible)
        {
            return;
        }

        GridOpenImage.Visibility = Visibility.Collapsed;
        GridOpenImage.Source = null;
        GridOpenImage.BeginAnimation(Canvas.LeftProperty, null);
        GridOpenImage.BeginAnimation(Canvas.TopProperty, null);
        GridOpenImage.BeginAnimation(WidthProperty, null);
        GridOpenImage.BeginAnimation(HeightProperty, null);
        GridOpenImage.BeginAnimation(OpacityProperty, null);
        Photo.BeginAnimation(OpacityProperty, null);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace IMGViewer;

public partial class MainWindow
{
    private bool showingCaptureFrame;

    private void ShowCaptureFrame()
    {
        if (!showingCaptureFrame)
        {
            showingCaptureFrame = true;
            LayoutUpdated += CaptureFrame_LayoutUpdated;
        }

        UpdateCaptureFrame();
        if (SystemParameters.ClientAreaAnimation)
        {
            var pulse = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.55, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350))));
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(CaptureFrame.Opacity, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));
            CaptureFrame.BeginAnimation(OpacityProperty, pulse);
        }
    }

    private void HideCaptureFrame()
    {
        if (!showingCaptureFrame)
        {
            return;
        }

        showingCaptureFrame = false;
        LayoutUpdated -= CaptureFrame_LayoutUpdated;
        CaptureFrame.BeginAnimation(OpacityProperty, null);
        CaptureFrame.Visibility = Visibility.Collapsed;
    }

    private void CaptureFrame_LayoutUpdated(object? sender, EventArgs e) => UpdateCaptureFrame();

    private void UpdateCaptureFrame()
    {
        if (!showingCaptureFrame || Photo.Source is null)
        {
            return;
        }

        Rect bounds = Photo.TransformToAncestor(PhotoViewport)
            .TransformBounds(new Rect(Photo.RenderSize));
        bounds.Intersect(new Rect(PhotoViewport.RenderSize));
        if (bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1)
        {
            CaptureFrame.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(CaptureFrame, bounds.Left);
        Canvas.SetTop(CaptureFrame, bounds.Top);
        CaptureFrame.Width = bounds.Width;
        CaptureFrame.Height = bounds.Height;
        CaptureFrame.Visibility = Visibility.Visible;
    }
}

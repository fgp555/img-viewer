using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace IMGViewer;
public partial class WebPOptionsControl : UserControl
{
    private bool refreshing = true;
    public WebPOptionsControl()
    {
        InitializeComponent();
        Refresh();
        IsVisibleChanged += (_,_) => { if (IsVisible) Refresh(); };
    }
    private void Refresh()
    {
        refreshing = true;
        try { Quality.Value = ViewerPreferences.WebPQuality; Lossless.IsChecked = ViewerPreferences.WebPLossless; UpdateLabels(); }
        finally { refreshing = false; }
    }
    private void UpdateLabels()
    {
        QualityValue.Text = $"{Quality.Value:0}%";
        QualityPanel.Visibility = Lossless.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        LosslessHint.Visibility = Lossless.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Quality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (refreshing) return;
        try { ViewerPreferences.WebPQuality = (int)Quality.Value; Status.Text = ""; UpdateLabels(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Refresh(); Status.Text = "No se pudo guardar la calidad WebP."; }
    }
    private void Lossless_Click(object sender, RoutedEventArgs e)
    {
        try { ViewerPreferences.WebPLossless = Lossless.IsChecked == true; Status.Text = ""; UpdateLabels(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Refresh(); Status.Text = "No se pudo guardar la compresión WebP."; }
    }
}

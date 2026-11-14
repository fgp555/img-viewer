using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace IMGViewer;

public partial class MainWindow
{
    private async void ConvertSelectedGridPhotos()
    {
        string[] paths = SelectedGridPaths();
        if (paths.Length == 0 || gridBatchInProgress) return;
        string folder = GridFolderPath.Text;
        var dialog = ViewerDialog.Create(this, "Convertir imágenes", 440);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = $"Convertir {paths.Length} imagen(es)", FontSize = 18, Margin = new Thickness(0,0,0,14) });
        panel.Children.Add(new TextBlock { Text = "Formato de las copias", Margin = new Thickness(0,0,0,8) });
        var format = new ComboBox { ItemsSource = new[] { "PNG", "JPG", "WebP", "BMP" }, SelectedIndex = 0, Margin = new Thickness(0,0,0,14) };
        panel.Children.Add(format);
        var webpOptions = new WebPOptionsControl { Visibility = Visibility.Collapsed, Margin = new Thickness(0,0,0,14) };
        format.SelectionChanged += (_,_) => webpOptions.Visibility = format.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        panel.Children.Add(webpOptions);
        panel.Children.Add(new TextBlock { Text = "Se guardará una copia junto a cada original, con nombre nuevo. Los originales se conservan. JPG no admite transparencia; se usará fondo blanco.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new Separator { Margin = new Thickness(0,18,0,12) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(0,0,8,0) };
        var apply = new Button { Content = "Convertir copias", IsDefault = true };
        cancel.Click += (_,_) => dialog.DialogResult = false;
        apply.Click += (_,_) => dialog.DialogResult = true;
        buttons.Children.Add(cancel); buttons.Children.Add(apply); panel.Children.Add(buttons); dialog.Content = panel;
        if (dialog.ShowDialog() != true) return;
        string extension = new[] { ".png", ".jpg", ".webp", ".bmp" }[format.SelectedIndex];
        gridBatchInProgress = true;
        GridActions.IsEnabled = GridScrollArea.IsEnabled = false;
        UpdateToolbarActions(); UpdateSelectAllState();
        SetGridStatus("Convirtiendo imágenes…");
        try
        {
            var result = await Task.Run(() => ConvertGridCopies(paths, extension));
            gallerySignature = null;
            RefreshGallery(true);
            if (IsFolderGridOpen && StringComparer.OrdinalIgnoreCase.Equals(GridFolderPath.Text, folder)) LoadFolderGrid(folder);
            SetGridStatus($"{result.Saved} copia(s) convertida(s) · {result.Failed} con error.");
        }
        finally
        {
            gridBatchInProgress = false;
            GridActions.IsEnabled = GridScrollArea.IsEnabled = true;
            UpdateToolbarActions(); UpdateSelectAllState();
        }
    }

    private static (int Saved, int Failed) ConvertGridCopies(IEnumerable<string> paths, string extension)
    {
        int saved = 0, failed = 0;
        foreach (string path in paths)
        {
            try
            {
                var bitmap = ReadPhotoBitmap(path);
                if (extension == ".jpg")
                {
                    var visual = new System.Windows.Media.DrawingVisual();
                    using (var drawing = visual.RenderOpen())
                    {
                        var bounds = new Rect(0, 0, bitmap.Width, bitmap.Height);
                        drawing.DrawRectangle(System.Windows.Media.Brushes.White, null, bounds);
                        drawing.DrawImage(bitmap, bounds);
                    }
                    var flattened = new System.Windows.Media.Imaging.RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, bitmap.DpiX, bitmap.DpiY, System.Windows.Media.PixelFormats.Pbgra32);
                    flattened.Render(visual); flattened.Freeze(); bitmap = flattened;
                }
                WriteUniquePhoto(bitmap, Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-convertida" + extension);
                saved++;
            }
            catch (Exception ex) when (IsBatchImageError(ex)) { failed++; }
        }
        return (saved, failed);
    }
}

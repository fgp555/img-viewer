using System.Windows;
using System.Windows.Controls;

namespace IMGViewer;

public partial class ResizeImageWindow : Window
{
    private readonly int originalWidth;
    private readonly int originalHeight;
    private readonly int batchCount;
    private bool updatingFields;

    internal ImageResizeRequest Request { get; private set; }

    internal (int Width, int Height) TargetSize { get; private set; }

    internal ResizeImageWindow(int width, int height, int batchCount = 0)
    {
        originalWidth = width;
        originalHeight = height;
        this.batchCount = batchCount;
        InitializeComponent();
        if (batchCount > 0)
        {
            Title = "Redimensionar y guardar copias";
            ApplyButton.Content = "Guardar copias";
        }

        SetRequest(new ImageResizeRequest(ImageResizeMode.Scale, 1));
        WidthField.TextChanged += SizeField_TextChanged;
        HeightField.TextChanged += SizeField_TextChanged;
    }

    private void HalfSize_Click(object sender, RoutedEventArgs e) => SetRequest(new(ImageResizeMode.Scale, 0.5));

    private void DoubleSize_Click(object sender, RoutedEventArgs e) => SetRequest(new(ImageResizeMode.Scale, 2));

    private void FitSize_Click(object sender, RoutedEventArgs e) => SetRequest(new(ImageResizeMode.MaxSide, 1000));

    private void SetRequest(ImageResizeRequest request, TextBox? editedField = null)
    {
        Request = request;
        var (width, height) = request.GetSize(originalWidth, originalHeight);
        updatingFields = true;
        if (editedField != WidthField)
        {
            WidthField.Text = width.ToString();
        }

        if (editedField != HeightField)
        {
            HeightField.Text = height.ToString();
        }

        updatingFields = false;
        UpdatePreview(width, height);
    }

    private void SizeField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (updatingFields || sender is not TextBox field)
        {
            return;
        }

        if (!int.TryParse(field.Text, out int pixels) || pixels < 1)
        {
            SizePreview.Text = "Introduce una medida en píxeles mayor que cero.";
            ApplyButton.IsEnabled = false;
            return;
        }

        var mode = field == WidthField ? ImageResizeMode.Width : ImageResizeMode.Height;
        SetRequest(new ImageResizeRequest(mode, pixels), field);
    }

    private void UpdatePreview(int width, int height)
    {
        bool allowed = ImageResizeRequest.IsAllowed(width, height);
        ApplyButton.IsEnabled = allowed || batchCount > 0;
        TargetSize = (width, height);
        string size = allowed
            ? $"{originalWidth} × {originalHeight} → {width} × {height} px"
            : "El resultado supera 16 384 px por lado o 100 megapíxeles.";
        SizePreview.Text = batchCount > 0
            ? $"Ejemplo para {batchCount} imagen(es): {size}\nLas copias se guardan en la misma carpeta."
            : size;
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}

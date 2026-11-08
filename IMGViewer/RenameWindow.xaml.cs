using System.IO;
using System.Windows;

namespace IMGViewer;

public partial class RenameWindow : Window
{
    private readonly string originalPath;

    internal string? RenamedPath { get; private set; }

    internal RenameWindow(string path)
    {
        originalPath = path;
        InitializeComponent();
        FileName.Text = Path.GetFileNameWithoutExtension(path);
        Extension.Text = Path.GetExtension(path);
        Loaded += (_, _) =>
        {
            FileName.Focus();
            FileName.SelectAll();
        };
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        string name = FileName.Text.Trim();
        if (name.Length == 0 || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ErrorText.Text = "Escribe un nombre de archivo válido.";
            return;
        }

        string destination = Path.Combine(Path.GetDirectoryName(originalPath)!, name + Extension.Text);
        if (StringComparer.Ordinal.Equals(originalPath, destination))
        {
            DialogResult = false;
            return;
        }

        try
        {
            File.Move(originalPath, destination);
            RenamedPath = destination;
            DialogResult = true;
        }
        catch (IOException) when (!StringComparer.OrdinalIgnoreCase.Equals(originalPath, destination) && Path.Exists(destination))
        {
            ErrorText.Text = "Ya existe un archivo o una carpeta con ese nombre.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ErrorText.Text = "No se pudo renombrar el archivo.";
        }
    }
}

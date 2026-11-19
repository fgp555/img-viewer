using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
namespace IMGViewer;
public partial class MainWindow
{
    private System.Windows.Threading.DispatcherTimer? dimensionsCopyFeedback;
    private void SetImageInfo(string text)
    {
        dimensionsCopyFeedback?.Stop();
        dimensionsCopyFeedback=null;
        ImageInfo.Inlines.Clear();
        if(Photo.Source is not BitmapSource bitmap){ImageInfo.Text=text;return;}
        string dimensions=$"{bitmap.PixelWidth} × {bitmap.PixelHeight} px";
        int index=text.IndexOf(dimensions,StringComparison.Ordinal);
        if(index<0){ImageInfo.Text=text;return;}
        ImageInfo.Inlines.Add(new Run(text[..index]));
        var dimensionLabel=new Run(dimensions);
        var link=new Hyperlink(dimensionLabel) {
            Foreground=ImageInfo.Foreground,TextDecorations=null,
            ToolTip="Copiar dimensiones",Cursor=System.Windows.Input.Cursors.Hand
        };
        System.Windows.Automation.AutomationProperties.SetName(link,"Copiar dimensiones de la imagen");
        link.Click+=(_,_)=>{
            if(Photo.Source is not BitmapSource current)return;
            try
            {
                Clipboard.SetText(current.PixelWidth.ToString(CultureInfo.InvariantCulture)+"x"+current.PixelHeight.ToString(CultureInfo.InvariantCulture));
                dimensionsCopyFeedback?.Stop();
                dimensionLabel.Text="Copiado…";
                var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromSeconds(5)};
                dimensionsCopyFeedback=timer;
                timer.Tick+=(_,_)=>{
                    timer.Stop();
                    if(ReferenceEquals(dimensionsCopyFeedback,timer))
                    {dimensionLabel.Text=dimensions;dimensionsCopyFeedback=null;}
                };
                timer.Start();
            }
            catch(System.Runtime.InteropServices.ExternalException){StatusText.Text="No se pudieron copiar las dimensiones. Inténtalo de nuevo.";}
        };
        ImageInfo.Inlines.Add(link);
        ImageInfo.Inlines.Add(new Run(text[(index+dimensions.Length)..]));
    }
}

using IMGViewer.Cloud;
using System.Windows;
namespace IMGViewer;
public partial class CaptureSettingsWindow
{
    private CancellationTokenSource? cloudTest;
    private void InitializeCloudSettings()
    {
        try
        {
            var settings=R2Preferences.Load();CloudEnabled.IsChecked=settings.Enabled;CloudEndpoint.Text=settings.Endpoint;CloudBucket.Text=settings.Bucket;
            CloudAccess.Text=settings.AccessKey;CloudSecret.Password=settings.SecretKey;CloudPrefix.Text=settings.Prefix;CloudPublicUrl.Text=settings.PublicUrl;CloudCopyLink.IsChecked=settings.CopyLink;
        }
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException)
        {CloudStatus.Text="No se pudo recuperar la conexión protegida. Vuelve a introducir tus datos.";}
        Closed+=(_,_)=>{cloudTest?.Cancel();cloudTest?.Dispose();};
    }
    private void CloudSecret_PasswordChanged(object sender,RoutedEventArgs e)
    {
        if(CloudSecretHint is null)return;
        using var secret=CloudSecret.SecurePassword;
        CloudSecretHint.Visibility=secret.Length==0?Visibility.Visible:Visibility.Collapsed;
    }
    private R2Settings ReadCloudForm()=>new(CloudEnabled.IsChecked==true,CloudEndpoint.Text,CloudBucket.Text,CloudAccess.Text,CloudSecret.Password,CloudPrefix.Text,CloudPublicUrl.Text,CloudCopyLink.IsChecked==true);
    private void SaveCloud_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            var settings=ReadCloudForm();
            if(settings.Enabled || settings.Endpoint.Length>0)settings=settings.Validate();
            R2Preferences.Save(settings);
            (Owner as MainWindow)?.RefreshCloudConfiguration();
            CloudStatus.Text=settings.Enabled?"Nube activada. Puedes subir imágenes manualmente desde el visor o la cuadrícula.":"Nube desactivada. Las imágenes remotas se conservan.";
        }
        catch(Exception ex) when(ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {CloudStatus.Text=ex is ArgumentException?ex.Message:"No se pudo guardar la conexión protegida.";}
    }
    private async void TestCloud_Click(object sender,RoutedEventArgs e)
    {
        cloudTest?.Cancel();cloudTest?.Dispose();cloudTest=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        TestCloudButton.IsEnabled=false;CloudStatus.Text="Comprobando conexión…";
        try
        {
            var client=new R2Client(ReadCloudForm());await client.List(null,cloudTest.Token);
            CloudStatus.Text="Conexión correcta: se puede leer la carpeta remota. Para subir y eliminar, el token necesita Object Read & Write. Pulsa Guardar conexión para aplicar.";
        }
        catch(Exception ex) when(ex is ArgumentException or System.Net.Http.HttpRequestException or OperationCanceledException or System.Xml.XmlException)
        {CloudStatus.Text=ex is ArgumentException?ex.Message:ex is OperationCanceledException?"La comprobación se canceló o agotó el tiempo de espera.":"No se pudo conectar. Revisa endpoint, bucket, credenciales y permisos.";}
        finally {TestCloudButton.IsEnabled=true;}
    }
}

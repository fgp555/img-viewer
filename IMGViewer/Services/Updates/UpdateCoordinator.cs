using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace IMGViewer.Services.Updates;

public enum UpdateState { Idle, Checking, Available, Downloading, Preparing, Ready, Current, Error }

public sealed class UpdateCoordinator : INotifyPropertyChanged, IDisposable
{
    private readonly UpdateFeedClient feed;
    private CancellationTokenSource? operation;
    private readonly CancellationTokenSource lifetime = new();
    private UpdateManifest? manifest;
    private string? job;
    private UpdateState state;
    private string status = "Busca una nueva versiÃ³n de IMG Viewer.";
    private double progress;
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Enabled => UpdateInstaller.IsPublishedExecutable && UpdateInstaller.UsesExternalRuntime;
    public string EditionLabel => "Requiere .NET 10 Desktop Runtime";
    public string InstalledLabel => "VersiÃ³n " + UpdateManifest.CurrentVersionText;
    public UpdateState State => state;
    public string Status => status;
    public string Notes => manifest?.ReleaseNotes ?? "";
    public double Progress => progress;
    public bool IsWorking => state is UpdateState.Checking or UpdateState.Downloading or UpdateState.Preparing;
    public bool IsIndeterminate => state is UpdateState.Checking or UpdateState.Preparing;
    public bool CanAct => Enabled && !IsWorking;
    public bool ShowBanner => state is UpdateState.Available or UpdateState.Downloading or UpdateState.Preparing or UpdateState.Ready;
    public string BannerText => state == UpdateState.Ready ? "ActualizaciÃ³n lista para instalar" : state == UpdateState.Downloading ? "Descargando actualizaciÃ³nâ€¦" : "Nueva versiÃ³n disponible: " + manifest?.Version;
    public string ActionText => state switch
    {
        UpdateState.Ready => "Actualizar y reiniciar",
        UpdateState.Available => "Descargar actualizaciÃ³n",
        UpdateState.Checking => "Buscandoâ€¦",
        UpdateState.Downloading => "Descargandoâ€¦",
        UpdateState.Preparing => "Preparandoâ€¦",
        _ => "Buscar actualizaciones",
    };
    public UpdateCoordinator(UpdateFeedClient? feed = null) { this.feed = feed ?? new(); }

    public async Task Check()
    {
        if (!Enabled || IsWorking || state == UpdateState.Ready) return;
        Begin(UpdateState.Checking, "Buscando actualizacionesâ€¦");
        try
        {
            var candidate = await feed.Check(operation!.Token);
            manifest = candidate.IsNewerThan(UpdateManifest.CurrentVersion) ? candidate : null;
            SetState(manifest is null ? UpdateState.Current : UpdateState.Available,
                manifest is null ? "Ya tienes la Ãºltima versiÃ³n disponible." : "Disponible la versiÃ³n " + candidate.Version + ".");
        }
        catch (OperationCanceledException) { SetState(UpdateState.Idle, "ComprobaciÃ³n cancelada o sin respuesta. Puedes intentarlo de nuevo."); }
        catch (Exception error) { Fail("No se pudo comprobar la actualizaciÃ³n. " + error.Message); }
    }

    public async Task<bool> PerformAction()
    {
        if (!CanAct) return false;
        if (state == UpdateState.Ready && manifest is not null && job is not null)
        {
            Begin(UpdateState.Preparing, "Preparando el reinicioâ€¦");
            try { await UpdateInstaller.Prepare(job, manifest, operation!.Token); return true; }
            catch (Exception error) { SetState(UpdateState.Ready, "No se pudo instalar. " + error.Message); return false; }
        }
        if (state != UpdateState.Available || manifest is null) { await Check(); return false; }
        Begin(UpdateState.Downloading, "Descargando y verificando la actualizaciÃ³nâ€¦");
        try
        {
            job = UpdateInstaller.NewJob();
            await feed.Download(manifest, Path.Combine(job, "download.exe"), new Progress<double>(value => { progress = value; Notify(nameof(Progress)); }), operation!.Token);
            SetState(UpdateState.Ready, "Descarga verificada. La app se cerrarÃ¡ al pulsar Actualizar y reiniciar.");
        }
        catch (OperationCanceledException) { SetState(UpdateState.Available, "Descarga cancelada. Puedes volver a intentarlo."); }
        catch (Exception error) { Fail("No se pudo descargar. " + error.Message); }
        return false;
    }
    public void Cancel() => operation?.Cancel();
    private void Begin(UpdateState next, string text)
    {
        operation?.Dispose(); operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); progress = 0; SetState(next, text);
    }
    private void Fail(string text) { manifest = null; SetState(UpdateState.Error, text); }
    private void SetState(UpdateState value, string text)
    {
        state = value; status = text;
        foreach (var property in new[] { nameof(State), nameof(Status), nameof(Notes), nameof(Progress), nameof(IsWorking), nameof(IsIndeterminate), nameof(CanAct), nameof(ShowBanner), nameof(BannerText), nameof(ActionText) }) Notify(property);
    }
    private void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    public void Dispose() { lifetime.Cancel(); operation?.Dispose(); lifetime.Dispose(); feed.Dispose(); }
}

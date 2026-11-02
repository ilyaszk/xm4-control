using System.ComponentModel;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;
using Windows.System;

namespace Xm4Control.App.Views;

/// <summary>
/// Fenêtre principale : une page web locale (dossier Web) affichée par WebView2, reliée au <see cref="MainViewModel"/>
/// par <see cref="WebBridge"/>. Détruite à la fermeture, l'appli la recrée à la demande.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    // Hôte virtuel des fichiers de la page : jamais résolu sur le réseau.
    private const string HostName = "app.xm4";
    private static readonly string[] ExternalHosts = ["github.com", "sketchfab.com"];

    private readonly MainViewModel _viewModel;
    private bool _ready;
    private bool _postPending;
    private string? _pendingPage;

    internal SettingsWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Ui.IconPath);
        // Appli non empaquetée : chemin absolu plutôt qu'une URI ms-appx.
        AppTitleBar.IconSource = new ImageIconSource { ImageSource = new BitmapImage(new Uri(Ui.IconPath)) };
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 760;
            presenter.PreferredMinimumHeight = 560;
        }

        var scale = Ui.Scale(this);
        var size = new SizeInt32((int)(1180 * scale), (int)(800 * scale));
        var area = DisplayArea.Primary.WorkArea;
        AppWindow.MoveAndResize(new RectInt32(
            area.X + (area.Width - size.Width) / 2,
            area.Y + (area.Height - size.Height) / 2,
            size.Width,
            size.Height));

        _viewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            Web.Close();
        };
        _ = InitializeWebAsync();
    }

    /// <summary>Affiche la fenêtre, sur la page demandée ("eq"…) ou celle déjà ouverte.</summary>
    public void ShowPage(string? page)
    {
        if (page is not null)
        {
            _pendingPage = page;
            SchedulePost();
        }
        Activate();
        Ui.BringToFront(this);
    }

    private async Task InitializeWebAsync()
    {
        var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xm4Control", "WebView2");
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, dataFolder, new CoreWebView2EnvironmentOptions());
        await Web.EnsureCoreWebView2Async(environment);
        var core = Web.CoreWebView2;

        // Une fenêtre d'appli, pas un navigateur.
        var settings = core.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
#if !DEBUG
        settings.AreDevToolsEnabled = false;
#endif

        core.SetVirtualHostNameToFolderMapping(HostName, Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith($"https://{HostName}/", StringComparison.Ordinal)) e.Cancel = true;
        };
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += OnWebMessage;
        core.Navigate($"https://{HostName}/index.html");
    }

    private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.WebMessageAsJson;
        switch (WebBridge.Apply(_viewModel, json))
        {
            case "ready":
                _ready = true;
                SchedulePost();
                break;
            case "powerOff":
                _ = _viewModel.PowerOffAsync();
                break;
            case "quit":
                App.Current.Quit();
                break;
            case "open":
                OpenExternal(json);
                break;
        }
    }

    /// <summary>Liens « À propos » : ouverts dans le navigateur par défaut, seulement vers les sites prévus.</summary>
    private static void OpenExternal(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (Uri.TryCreate(doc.RootElement.GetProperty("value").GetString(), UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && ExternalHosts.Contains(uri.Host))
            _ = Launcher.LaunchUriAsync(uri);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => SchedulePost();

    // Plusieurs notifications d'affilée (réglages, état du casque) : un seul envoi.
    private void SchedulePost()
    {
        if (!_ready || _postPending) return;
        _postPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _postPending = false;
            if (Web.CoreWebView2 is null) return;
            Web.CoreWebView2.PostWebMessageAsJson(WebBridge.Serialize(_viewModel, _pendingPage));
            _pendingPage = null;
        });
    }
}

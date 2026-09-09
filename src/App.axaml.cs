using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ProgramacioDocent.Data;
using ProgramacioDocent.Services;
using ProgramacioDocent.ViewModels;
using ProgramacioDocent.Views;

namespace ProgramacioDocent;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                LogService.Info("Inicialitzant base de dades...");
                var db = new Database();
                db.Inicialitza();
                LogService.Info($"Base de dades OK. Dades a: {PathService.GetDataDirectory()}");

                var horariService = new HorariService(db);
                var notesService = new NotesService(db);
                var calendariService = new CalendariService(db);
                var configService = new ConfigService(db);
                var informeService = new InformeService(db);
                var backupService = new BackupService(db);

                var config = configService.Carrega();

                // Aplica el tema desat abans de mostrar la finestra (evita el "flaix").
                AplicaTema(config.Tema);

                // Còpia de seguretat automàtica en arrencar (best-effort, no bloqueja).
                try { backupService.CreaCopia("arrencada"); }
                catch (Exception exb) { LogService.Error("Backup d'arrencada", exb); }

                LogService.Info("Construint la finestra principal...");
                var mainVm = new MainWindowViewModel(
                    horariService, notesService, calendariService,
                    configService, informeService, backupService, db);

                // Quan l'usuari canvia el tema des de Configuració, s'aplica a l'instant.
                mainVm.TemaCanviat += AplicaTema;

                desktop.MainWindow = new MainWindow { DataContext = mainVm };
                LogService.Info("Finestra principal creada correctament.");
            }
            catch (Exception ex)
            {
                // Registra l'error d'inicialització i mostra una finestra de
                // diagnòstic en lloc de tancar-se en silenci.
                LogService.Error("OnFrameworkInitializationCompleted", ex);
                desktop.MainWindow = CreaFinestraError(ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreaFinestraError(Exception ex)
    {
        string ruta = LogService.RutaActual ?? "(no s'ha pogut determinar)";

        return new Window
        {
            Title = "Programació Docent — Error d'inici",
            Width = 640,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new ScrollViewer
            {
                Padding = new Thickness(20),
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "No s'ha pogut iniciar l'aplicació",
                            FontSize = 18, FontWeight = FontWeight.Bold
                        },
                        new TextBlock
                        {
                            Text = "S'ha produït un error en arrencar. Els detalls s'han desat a:",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock { Text = ruta, FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.Wrap },
                        new SelectableTextBlock
                        {
                            Text = ex.ToString(),
                            FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            }
        };
    }

    // Tradueix la preferència ('Sistema'|'Clar'|'Fosc') a un ThemeVariant d'Avalonia.
    public static void AplicaTema(string? tema)
    {
        var variant = tema switch
        {
            "Clar" => ThemeVariant.Light,
            "Fosc" => ThemeVariant.Dark,
            _ => ThemeVariant.Default // segueix el sistema
        };
        if (Current != null)
            Current.RequestedThemeVariant = variant;
    }
}

using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            // Inicialitza la base de dades (WAL + migracions + dades inicials).
            var db = new Database();
            db.Inicialitza();

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
            try { backupService.CreaCopia("arrencada"); } catch { /* ignora */ }

            var mainVm = new MainWindowViewModel(
                horariService, notesService, calendariService,
                configService, informeService, backupService, db);

            // Quan l'usuari canvia el tema des de Configuració, s'aplica a l'instant.
            mainVm.TemaCanviat += AplicaTema;

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
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

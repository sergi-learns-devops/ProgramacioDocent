using System;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using ProgramacioDocent.Services;

namespace ProgramacioDocent;

internal sealed class Program
{
    // Punt d'entrada. No fer servir cap API d'Avalonia abans d'AppMain.
    [STAThread]
    public static void Main(string[] args)
    {
        // Marcador d'inici i registre d'excepcions no controlades: és el PRIMER
        // que fem, perquè si no apareix ni aquesta línia al log, el procés mor
        // abans d'executar el nostre codi (runtime .NET, antivirus, AppLocker...).
        LogService.Info($"Inici de l'aplicació. Versió {ObteVersio()}. BaseDir={AppContext.BaseDirectory}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogService.Error("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogService.Error("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        try
        {
            // Estableix el català com a idioma de tota l'aplicació.
            var cultura = new CultureInfo("ca-ES");
            CultureInfo.DefaultThreadCurrentCulture = cultura;
            CultureInfo.DefaultThreadCurrentUICulture = cultura;
            Thread.CurrentThread.CurrentCulture = cultura;
            Thread.CurrentThread.CurrentUICulture = cultura;

            // Llicència Community de QuestPDF (ús gratuït).
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            LogService.Info("Tancament normal de l'aplicació.");
        }
        catch (Exception ex)
        {
            LogService.Error("Main", ex);
            throw;
        }
    }

    private static string ObteVersio()
    {
        try
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v?.ToString() ?? "desconeguda";
        }
        catch { return "desconeguda"; }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

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
    {
        var builder = AppBuilder.Configure<App>();

        // Alguns equips (drivers de GPU antics, màquines virtuals o entorns
        // capats) fan caure l'aplicació de manera NATIVA en renderitzar amb
        // acceleració per maquinari: el procés mor sense que cap handler de .NET
        // ho pugui registrar (per això el crash no deixa rastre al log).
        //
        // Per a aquests casos oferim un "mode segur" que força el render per
        // programari (CPU). S'activa SENSE recompilar de dues maneres:
        //   1) Variable d'entorn:  PROGRAMACIODOCENT_SOFTWARE_RENDER=1
        //   2) Un fitxer buit anomenat 'render-software.txt' al costat de l'exe.
        if (VolRenderProgramari())
        {
            LogService.Info("Render per PROGRAMARI (mode segur, sense GPU).");
            builder = builder.UsePlatformDetect()
                .With(new Win32PlatformOptions
                {
                    // Sense backends de GPU: força el render per CPU (Skia software).
                    RenderingMode = new[] { Win32RenderingMode.Software }
                });
        }
        else
        {
            LogService.Info("Render per GPU (per defecte).");
            builder = builder.UsePlatformDetect();
        }

        return builder
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }

    // Decideix si cal forçar el render per programari (mode segur).
    private static bool VolRenderProgramari()
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("PROGRAMACIODOCENT_SOFTWARE_RENDER");
            if (!string.IsNullOrEmpty(env) &&
                (env == "1" || env.Equals("true", StringComparison.OrdinalIgnoreCase)))
                return true;

            var marcador = System.IO.Path.Combine(AppContext.BaseDirectory, "render-software.txt");
            if (System.IO.File.Exists(marcador))
                return true;
        }
        catch (Exception ex)
        {
            LogService.Error("VolRenderProgramari", ex);
        }
        return false;
    }
}

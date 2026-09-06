using System;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;

namespace ProgramacioDocent;

internal sealed class Program
{
    // Punt d'entrada. No fer servir cap API d'Avalonia abans d'AppMain.
    [STAThread]
    public static void Main(string[] args)
    {
        // Estableix el català com a idioma de tota l'aplicació.
        var cultura = new CultureInfo("ca-ES");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;

        // Llicència Community de QuestPDF (ús gratuït).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Registre bàsic d'errors d'arrencada per a diagnòstic en equips capats.
            try
            {
                var log = System.IO.Path.Combine(
                    Services.PathService.GetDataDirectory(), "error-arrencada.log");
                System.IO.File.AppendAllText(log,
                    $"[{DateTime.Now:O}] {ex}\n\n");
            }
            catch
            {
                // Si no es pot escriure el log, no hi ha res més a fer.
            }
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

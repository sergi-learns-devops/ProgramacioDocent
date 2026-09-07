using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ProgramacioDocent.ViewModels;

namespace ProgramacioDocent.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.ObreUrlDemanada += ObreFitxer;
    }

    // Clic sobre una classe de la graella: obre l'editor de notes.
    private void OnClasseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control ctrl
            && ctrl.DataContext is ClasseCellaViewModel cella
            && DataContext is MainWindowViewModel vm)
        {
            vm.ObreNota(cella);
        }
    }

    // Genera l'informe i intenta obrir-lo amb l'aplicació per defecte.
    private void OnGeneraInforme(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var ruta = vm.GeneraInforme();
        if (!string.IsNullOrEmpty(ruta))
            ObreFitxer(ruta);
    }

    private static void ObreFitxer(string ruta)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", ruta);
            }
            else
            {
                Process.Start("xdg-open", ruta);
            }
        }
        catch
        {
            // Si no es pot obrir automàticament, el missatge de la UI ja indica la ruta.
        }
    }
}

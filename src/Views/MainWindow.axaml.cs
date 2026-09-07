using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ProgramacioDocent.ViewModels;

namespace ProgramacioDocent.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // Redibuixa el calendari quan canvia el tema (colors dels tokens).
        ActualThemeVariantChanged += (_, _) => RenderCalendari();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (_vm != null)
            {
                _vm.ObreUrlDemanada -= ObreFitxer;
                _vm.CalendariActualitzat -= RenderCalendari;
            }
            _vm = vm;
            vm.ObreUrlDemanada += ObreFitxer;
            vm.CalendariActualitzat += RenderCalendari;
            RenderCalendari();
        }
    }

    // Obté un pinzell dels recursos del tema (tokens definits a App.axaml).
    private IBrush Brush(string clau)
        => this.TryFindResource(clau, ActualThemeVariant, out var r) && r is IBrush b
            ? b : Brushes.Transparent;

    // Construeix la graella tipus calendari a partir de les dades del ViewModel.
    private void RenderCalendari()
    {
        var host = this.FindControl<Grid>("CalendariHost");
        if (host == null || _vm == null) return;

        host.Children.Clear();
        host.ColumnDefinitions.Clear();
        host.RowDefinitions.Clear();

        int files = _vm.NombreFiles;
        double alcada = _vm.AlcadaFila;
        if (files <= 0) return;

        // Columnes: eix d'hores (56px) + 5 dies (proporcionals).
        host.ColumnDefinitions.Add(new ColumnDefinition(56, GridUnitType.Pixel));
        for (int c = 0; c < 5; c++)
            host.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        // Files de 30 min.
        for (int r = 0; r < files; r++)
            host.RowDefinitions.Add(new RowDefinition(alcada, GridUnitType.Pixel));

        var vorera = Brush("BrushBorder");
        var surface = Brush("BrushSurface");

        // 1) Fons de cada columna de dia (avui ressaltat, festiu atenuat).
        foreach (var col in _vm.ColumnesDia)
        {
            var fons = new Border
            {
                Background = col.EsAvui ? Brush("BrushTodayColumn")
                            : col.EsFestiu ? Brush("BrushHolidayColumn")
                            : surface,
                BorderBrush = vorera,
                BorderThickness = new Thickness(0.5, 0, 0.5, 0)
            };
            Grid.SetColumn(fons, col.Columna); // 1..5
            Grid.SetRow(fons, 0);
            Grid.SetRowSpan(fons, files);
            host.Children.Add(fons);
        }

        // 2) Línies horitzontals de cada hora + etiqueta a l'eix.
        foreach (var h in _vm.EtiquetesHora)
        {
            var linia = new Border
            {
                BorderBrush = vorera,
                BorderThickness = new Thickness(0, 0.8, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(linia, 1);
            Grid.SetColumnSpan(linia, 5);
            Grid.SetRow(linia, h.Fila);
            host.Children.Add(linia);

            var etiqueta = new TextBlock
            {
                Text = h.Text,
                FontSize = 11,
                Margin = new Thickness(4, 1, 4, 0),
                Foreground = Brush("BrushTextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(etiqueta, 0);
            Grid.SetRow(etiqueta, h.Fila);
            host.Children.Add(etiqueta);
        }

        // 3) Blocs de classe posicionats (columna = dia, fila = hora, span = durada).
        foreach (var bloc in _vm.BlocsCalendari)
        {
            var contingut = new StackPanel { Spacing = 1 };

            var titolRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            if (bloc.TeNota)
            {
                titolRow.Children.Add(new Ellipse
                {
                    Width = 8, Height = 8,
                    Fill = Brush("BrushSuccess"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            titolRow.Children.Add(new TextBlock
            {
                Text = bloc.NomAssignatura,
                FontWeight = FontWeight.Bold,
                Foreground = bloc.TextBrush,
                TextWrapping = TextWrapping.Wrap
            });
            contingut.Children.Add(titolRow);

            if (!string.IsNullOrWhiteSpace(bloc.Detall))
            {
                contingut.Children.Add(new TextBlock
                {
                    Text = bloc.Detall,
                    FontSize = 11,
                    Foreground = bloc.TextDetallBrush,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var targeta = new Border
            {
                Background = bloc.FonsBrush,
                BorderBrush = vorera,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4),
                Child = contingut
            };

            var boto = new Button
            {
                Padding = new Thickness(0),
                Margin = new Thickness(2, 1),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = targeta,
                Tag = bloc
            };
            boto.Click += OnBlocClick;

            Grid.SetColumn(boto, bloc.Columna); // 1..5
            Grid.SetRow(boto, bloc.Fila);
            Grid.SetRowSpan(boto, bloc.FilesSpan);
            host.Children.Add(boto);
        }
    }

    private void OnBlocClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is BlocCalendariVm bloc && _vm != null)
            _vm.ObreNota(bloc);
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

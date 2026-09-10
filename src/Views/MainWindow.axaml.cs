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
        ConfiguraDivisorPanell();
    }

    // Configura el divisor arrossegable del panell dret: aplica l'amplada desada
    // i la persisteix quan l'usuari acaba d'arrossegar.
    private void ConfiguraDivisorPanell()
    {
        var splitter = this.FindControl<GridSplitter>("DivisorPanellDret");
        if (splitter != null)
            splitter.DragCompleted += (_, _) => DesaAmpladaColumna();
    }

    // Columna del panell dret dins del Grid pare (índex 2).
    private ColumnDefinition? ColumnaPanell()
    {
        var grid = this.FindControl<Grid>("GraellaAmbPanell");
        if (grid != null && grid.ColumnDefinitions.Count > 2)
            return grid.ColumnDefinitions[2];
        return null;
    }

    // Llegeix l'amplada actual de la columna del panell i la desa al ViewModel.
    private void DesaAmpladaColumna()
    {
        if (_vm == null) return;
        var col = ColumnaPanell();
        if (col != null && col.Width.IsAbsolute && col.Width.Value > 0)
            _vm.DesaAmpladaPanell(col.Width.Value);
    }

    // Aplica l'amplada de la columna del panell dret segons el mode actual:
    // l'amplada desada si estem en mode Dret, o 0 (col·lapsada) en cas contrari.
    // Fixa també un mínim (260 px) perquè el contingut no col·lapsi en arrossegar,
    // i un màxim (640 px) coherent amb el rang desat.
    private void AplicaAmpladaDesada()
    {
        if (_vm == null) return;
        var col = ColumnaPanell();
        if (col == null) return;
        if (_vm.EsModeDret)
        {
            col.MinWidth = 260;
            col.MaxWidth = 640;
            col.Width = new GridLength(_vm.AmpladaPanellDret, GridUnitType.Pixel);
        }
        else
        {
            // Col·lapsada completament quan no s'usa el panell dret.
            col.MinWidth = 0;
            col.MaxWidth = double.PositiveInfinity;
            col.Width = new GridLength(0, GridUnitType.Pixel);
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (_vm != null)
            {
                _vm.ObreUrlDemanada -= ObreFitxer;
                _vm.CalendariActualitzat -= RenderCalendari;
                _vm.PropertyChanged -= OnVmPropertyChanged;
            }
            _vm = vm;
            vm.ObreUrlDemanada += ObreFitxer;
            vm.CalendariActualitzat += RenderCalendari;
            vm.PropertyChanged += OnVmPropertyChanged;
            AplicaAmpladaDesada();
            RenderCalendari();
        }
    }

    // Quan canvia el mode de posició de notes, reajusta l'amplada de la columna
    // del panell dret (aplica l'amplada desada o la col·lapsa a 0).
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.EsModeDret) ||
            e.PropertyName == nameof(MainWindowViewModel.PosicioEditorNotes))
        {
            AplicaAmpladaDesada();
        }
    }

    // Obté un pinzell dels recursos del tema (tokens definits a App.axaml).
    private IBrush Brush(string clau)
        => this.TryFindResource(clau, ActualThemeVariant, out var r) && r is IBrush b
            ? b : Brushes.Transparent;

    // Construeix la graella tipus calendari a partir de les dades del ViewModel.
    private void RenderCalendari()
    {
        try { RenderCalendariIntern(); }
        catch (Exception ex) { ProgramacioDocent.Services.LogService.Error("RenderCalendari", ex); }
    }

    private void RenderCalendariIntern()
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

        // 1) Fons de cada columna de dia (avui ressaltat, festiu atenuat) i
        //    cel·les buides clicables per crear una classe nova.
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

            // Botó transparent per cada fila (30 min) per obrir el pop-up de nova classe.
            for (int r = 0; r < files; r++)
            {
                int minuts = _vm.MinutBase + r * 30;
                var cel = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Tag = new int[] { col.Columna, minuts }
                };
                cel.Click += OnCelBuidaClick;
                Grid.SetColumn(cel, col.Columna);
                Grid.SetRow(cel, r);
                host.Children.Add(cel);
            }
        }

        // 2) Línies horitzontals de cada hora + etiqueta a l'eix.
        foreach (var h in _vm.EtiquetesHora)
        {
            var linia = new Border
            {
                BorderBrush = vorera,
                BorderThickness = new Thickness(0, 0.8, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
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

            // Botó petit d'edició a la cantonada (dia/hora/assignatura).
            var botoEdita = new Button
            {
                Content = "✎",
                FontSize = 11,
                Padding = new Thickness(4, 0),
                Margin = new Thickness(0, 2, 4, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = bloc.TextBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Tag = bloc
            };
            ToolTip.SetTip(botoEdita, "Edita o elimina la classe");
            botoEdita.Click += OnEditaBlocClick;

            var capa = new Grid { Margin = new Thickness(2, 1) };
            capa.Children.Add(boto);
            capa.Children.Add(botoEdita);

            Grid.SetColumn(capa, bloc.Columna); // 1..5
            Grid.SetRow(capa, bloc.Fila);
            Grid.SetRowSpan(capa, bloc.FilesSpan);
            host.Children.Add(capa);
        }
    }

    private void OnBlocClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is BlocCalendariVm bloc && _vm != null)
            _vm.ObreNota(bloc);
    }

    private void OnEditaBlocClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is BlocCalendariVm bloc && _vm != null)
            _vm.ObrePopupEditaClasse(bloc);
    }

    private void OnCelBuidaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is int[] info && info.Length == 2 && _vm != null)
            _vm.ObrePopupNovaClasse(info[0], info[1]);
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

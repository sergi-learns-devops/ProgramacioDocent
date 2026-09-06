using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProgramacioDocent.Models;
using ProgramacioDocent.Services;

namespace ProgramacioDocent.ViewModels;

// ViewModel principal de l'aplicació. Gestiona la graella d'horari, la
// navegació entre setmanes, l'edició de notes, l'assistent inicial i els informes.
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HorariService _horari;
    private readonly NotesService _notes;
    private readonly CalendariService _calendari;
    private readonly ConfigService _config;
    private readonly InformeService _informe;
    private static readonly CultureInfo Ca = new("ca-ES");

    private Configuracio _configuracio;
    private int _versioActiva;
    private List<FranjaHorari> _franjes = new();
    private List<ClasseHorari> _classes = new();

    // Setmana actualment visible (dilluns).
    [ObservableProperty] private DateTime _setmanaActual;
    [ObservableProperty] private string _titolSetmana = string.Empty;
    [ObservableProperty] private string _infoDia = string.Empty;

    // Assistent inicial.
    [ObservableProperty] private bool _mostraAssistent;

    // Editor de notes.
    [ObservableProperty] private bool _mostraEditorNota;
    [ObservableProperty] private string _textNota = string.Empty;
    [ObservableProperty] private string _titolNota = string.Empty;
    [ObservableProperty] private string _subtitolNota = string.Empty;
    private ClasseHorari? _classeSeleccionada;
    private DateTime _diaSeleccionat;

    // Graella: files (franjes) amb 5 columnes (dilluns-divendres).
    public ObservableCollection<FilaHorariViewModel> FilesHorari { get; } = new();

    // Informes.
    public ObservableCollection<string> FormatsInforme { get; } = new() { "PDF", "XLSX", "CSV" };
    public ObservableCollection<string> PeriodesInforme { get; } = new()
        { "Setmana actual", "Mes actual", "Trimestre actual", "Tot el curs" };
    [ObservableProperty] private string _formatSeleccionat = "PDF";
    [ObservableProperty] private string _periodeSeleccionat = "Mes actual";
    [ObservableProperty] private string _missatgeInforme = string.Empty;

    // Assistent: dades introduïdes.
    public ObservableCollection<FranjaEditVm> FranjesAssistent { get; } = new();
    public ObservableCollection<AssignaturaEditVm> AssignaturesAssistent { get; } = new();

    public string RutaDades => PathService.GetDataDirectory();

    public MainWindowViewModel(
        HorariService horari, NotesService notes, CalendariService calendari,
        ConfigService config, InformeService informe)
    {
        _horari = horari;
        _notes = notes;
        _calendari = calendari;
        _config = config;
        _informe = informe;

        _configuracio = _config.Carrega();
        _formatSeleccionat = _configuracio.FormatInformePreferit;

        // Setmana inicial: la d'inici de curs o la setmana actual si el curs ja ha començat.
        var avui = DateTime.Today;
        var referencia = avui < _configuracio.DataIniciCurs ? _configuracio.DataIniciCurs : avui;
        _setmanaActual = CalendariService.DillunsDeLaSetmana(referencia);

        if (!_configuracio.AssistentCompletat)
        {
            IniciaAssistent();
        }
        else
        {
            CarregaHorari();
            RefrescaGraella();
        }
    }

    // ---------------- Assistent inicial ----------------

    private void IniciaAssistent()
    {
        MostraAssistent = true;
        // Franjes per defecte proposades (el professor les pot editar/eliminar).
        FranjesAssistent.Clear();
        var predef = new[]
        {
            ("08:00", "09:00"), ("09:00", "10:00"), ("10:00", "11:00"),
            ("11:30", "12:30"), ("12:30", "13:30"), ("15:00", "16:00"), ("16:00", "17:00")
        };
        foreach (var (i, f) in predef)
            FranjesAssistent.Add(new FranjaEditVm { HoraInici = i, HoraFi = f });

        AssignaturesAssistent.Clear();
        AssignaturesAssistent.Add(new AssignaturaEditVm { Nom = "", Curs = "", Color = "#4F86C6" });
    }

    [RelayCommand]
    private void AfegeixFranjaAssistent()
        => FranjesAssistent.Add(new FranjaEditVm { HoraInici = "17:00", HoraFi = "18:00" });

    [RelayCommand]
    private void EliminaFranjaAssistent(FranjaEditVm f)
        => FranjesAssistent.Remove(f);

    [RelayCommand]
    private void AfegeixAssignaturaAssistent()
    {
        var colors = new[] { "#4F86C6", "#E06C75", "#98C379", "#E5C07B", "#C678DD", "#56B6C2", "#D19A66" };
        var color = colors[AssignaturesAssistent.Count % colors.Length];
        AssignaturesAssistent.Add(new AssignaturaEditVm { Nom = "", Curs = "", Color = color });
    }

    [RelayCommand]
    private void EliminaAssignaturaAssistent(AssignaturaEditVm a)
        => AssignaturesAssistent.Remove(a);

    [RelayCommand]
    private void CompletaAssistent()
    {
        // Valida franjes.
        var franjesValides = new List<(TimeOnly, TimeOnly)>();
        foreach (var f in FranjesAssistent)
        {
            if (TimeOnly.TryParse(f.HoraInici, out var hi) && TimeOnly.TryParse(f.HoraFi, out var hf) && hf > hi)
                franjesValides.Add((hi, hf));
        }
        var assignaturesValides = AssignaturesAssistent
            .Where(a => !string.IsNullOrWhiteSpace(a.Nom)).ToList();

        if (franjesValides.Count == 0 || assignaturesValides.Count == 0)
        {
            // No es pot completar sense almenys una franja i una assignatura.
            MissatgeAssistent = "Cal definir com a mínim una franja horària i una assignatura.";
            return;
        }

        // Desa franjes.
        int ordre = 1;
        foreach (var (hi, hf) in franjesValides.OrderBy(x => x.Item1))
            _horari.AfegeixFranja(new FranjaHorari { Ordre = ordre++, HoraInici = hi, HoraFi = hf });

        // Desa assignatures.
        foreach (var a in assignaturesValides)
            _horari.AfegeixAssignatura(new Assignatura { Nom = a.Nom, Curs = a.Curs, Color = a.Color });

        // Crea i activa la versió d'horari inicial.
        var versio = _horari.CreaVersio("Horari inicial del curs", _configuracio.DataIniciCurs);
        _horari.ActivaVersio(versio, _configuracio.DataIniciCurs);

        _configuracio.AssistentCompletat = true;
        _configuracio.VersioHorariActivaId = versio;
        _config.Desa(_configuracio);

        MostraAssistent = false;
        MissatgeAssistent = string.Empty;
        CarregaHorari();
        RefrescaGraella();
    }

    [ObservableProperty] private string _missatgeAssistent = string.Empty;

    // ---------------- Càrrega d'horari ----------------

    private void CarregaHorari()
    {
        _versioActiva = _horari.VersioVigent(SetmanaActual);
        if (_versioActiva == 0)
            _versioActiva = _configuracio.VersioHorariActivaId;
        _franjes = _horari.ObteFranjes();
        _classes = _horari.ObteClasses(_versioActiva);
    }

    // Reconstrueix la graella per a la setmana visible.
    private void RefrescaGraella()
    {
        FilesHorari.Clear();

        foreach (var franja in _franjes.OrderBy(f => f.Ordre))
        {
            var fila = new FilaHorariViewModel(franja.Etiqueta);
            for (int dia = 1; dia <= 5; dia++)
            {
                var classe = _classes.FirstOrDefault(c => c.DiaSetmana == dia && c.FranjaId == franja.Id);
                if (classe != null)
                {
                    var cella = new ClasseCellaViewModel(classe);
                    var diaData = SetmanaActual.AddDays(dia - 1);
                    var nota = _notes.ObteNota(classe.Id, SetmanaActual);
                    cella.TeNota = nota != null && !string.IsNullOrWhiteSpace(nota.Text);
                    fila.Celles.Add(cella);
                }
                else
                {
                    fila.Celles.Add(null);
                }
            }
            FilesHorari.Add(fila);
        }

        ActualitzaTitolSetmana();
    }

    private void ActualitzaTitolSetmana()
    {
        var divendres = SetmanaActual.AddDays(4);
        TitolSetmana = $"Setmana del {SetmanaActual:dd/MM/yyyy} al {divendres:dd/MM/yyyy}";

        // Indica festius de la setmana.
        var avisos = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var dia = SetmanaActual.AddDays(i);
            var festiu = _calendari.FestiuDe(dia);
            if (festiu != null)
                avisos.Add($"{dia:dddd d}: {festiu.Nom}");
        }
        InfoDia = avisos.Count > 0
            ? "Dies no lectius aquesta setmana → " + string.Join(" · ", avisos)
            : "Tots els dies d'aquesta setmana són lectius.";
    }

    // ---------------- Navegació de setmanes ----------------

    [RelayCommand]
    private void SetmanaAnterior()
    {
        SetmanaActual = SetmanaActual.AddDays(-7);
        CarregaHorari();
        RefrescaGraella();
    }

    [RelayCommand]
    private void SetmanaSeguent()
    {
        SetmanaActual = SetmanaActual.AddDays(7);
        CarregaHorari();
        RefrescaGraella();
    }

    [RelayCommand]
    private void SetmanaAvui()
    {
        var avui = DateTime.Today;
        var referencia = avui < _configuracio.DataIniciCurs ? _configuracio.DataIniciCurs : avui;
        SetmanaActual = CalendariService.DillunsDeLaSetmana(referencia);
        CarregaHorari();
        RefrescaGraella();
    }

    // ---------------- Editor de notes ----------------

    // S'invoca en fer clic sobre una classe de la graella.
    public void ObreNota(ClasseCellaViewModel cella)
    {
        var classe = cella.Classe;
        // Determina el dia concret (columna) de la classe dins la setmana.
        _diaSeleccionat = SetmanaActual.AddDays(classe.DiaSetmana - 1);
        _classeSeleccionada = classe;

        var nota = _notes.ObteNota(classe.Id, SetmanaActual);
        TextNota = nota?.Text ?? string.Empty;

        TitolNota = classe.Assignatura?.Nom ?? "Classe";
        SubtitolNota =
            $"{_diaSeleccionat:dddd d 'de' MMMM} · {classe.Franja?.Etiqueta}" +
            (string.IsNullOrWhiteSpace(classe.Assignatura?.Curs) ? "" : $" · {classe.Assignatura!.Curs}");

        // Avís si el dia és festiu.
        var festiu = _calendari.FestiuDe(_diaSeleccionat);
        if (festiu != null)
            SubtitolNota += $"  ⚠ {festiu.Nom} (dia no lectiu)";

        MostraEditorNota = true;
    }

    [RelayCommand]
    private void DesaNota()
    {
        if (_classeSeleccionada == null) return;
        _notes.DesaNota(_classeSeleccionada.Id, SetmanaActual, TextNota ?? string.Empty);
        MostraEditorNota = false;
        RefrescaGraella();
    }

    [RelayCommand]
    private void CancelaNota()
    {
        MostraEditorNota = false;
    }

    // ---------------- Informes ----------------

    partial void OnFormatSeleccionatChanged(string value)
    {
        _configuracio.FormatInformePreferit = value;
        _config.Desa(_configuracio);
    }

    // Calcula l'interval [desde, fins] segons el període triat.
    private (DateTime desde, DateTime fins) IntervalPeriode()
    {
        var avui = SetmanaActual;
        switch (PeriodeSeleccionat)
        {
            case "Setmana actual":
                return (SetmanaActual, SetmanaActual.AddDays(6));
            case "Mes actual":
            {
                var ini = new DateTime(avui.Year, avui.Month, 1);
                var fi = ini.AddMonths(1).AddDays(-1);
                return (ini, fi);
            }
            case "Trimestre actual":
            {
                int q = (avui.Month - 1) / 3;
                var ini = new DateTime(avui.Year, q * 3 + 1, 1);
                var fi = ini.AddMonths(3).AddDays(-1);
                return (ini, fi);
            }
            default: // Tot el curs
                return (_configuracio.DataIniciCurs, _configuracio.DataFiCurs);
        }
    }

    // Genera l'informe. Retorna el camí del fitxer perquè la vista pugui obrir-lo.
    public string? GeneraInforme()
    {
        var format = FormatSeleccionat switch
        {
            "XLSX" => InformeService.Format.XLSX,
            "CSV" => InformeService.Format.CSV,
            _ => InformeService.Format.PDF
        };
        var (desde, fins) = IntervalPeriode();

        var carpeta = Path.Combine(PathService.GetDataDirectory(), "informes");
        Directory.CreateDirectory(carpeta);
        var nom = $"informe_{desde:yyyyMMdd}_{fins:yyyyMMdd}.{InformeService.ExtensioPer(format)}";
        var ruta = Path.Combine(carpeta, nom);

        try
        {
            _informe.Genera(format, desde, fins, _configuracio.CursEscolar, ruta);
            MissatgeInforme = $"Informe generat: {ruta}";
            return ruta;
        }
        catch (Exception ex)
        {
            MissatgeInforme = "Error en generar l'informe: " + ex.Message;
            return null;
        }
    }
}

// Fila de la graella d'horari: etiqueta de la franja + 5 cel·les (dilluns-divendres).
public class FilaHorariViewModel : ViewModelBase
{
    public string Franja { get; }
    public ObservableCollection<ClasseCellaViewModel?> Celles { get; } = new();
    public FilaHorariViewModel(string franja) => Franja = franja;
}

// Model editable de franja per a l'assistent.
public partial class FranjaEditVm : ViewModelBase
{
    [ObservableProperty] private string _horaInici = "08:00";
    [ObservableProperty] private string _horaFi = "09:00";
}

// Model editable d'assignatura per a l'assistent.
public partial class AssignaturaEditVm : ViewModelBase
{
    [ObservableProperty] private string _nom = string.Empty;
    [ObservableProperty] private string _curs = string.Empty;
    [ObservableProperty] private string _color = "#4F86C6";
}

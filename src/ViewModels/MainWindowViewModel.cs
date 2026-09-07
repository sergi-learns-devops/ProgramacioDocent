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
    private readonly BackupService _backup;
    private static readonly CultureInfo Ca = new("ca-ES");

    // S'emet quan l'usuari canvia el tema, perquè App l'apliqui a l'instant.
    public event Action<string>? TemaCanviat;

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
        ConfigService config, InformeService informe, BackupService backup)
    {
        _horari = horari;
        _notes = notes;
        _calendari = calendari;
        _config = config;
        _informe = informe;
        _backup = backup;

        _configuracio = _config.Carrega();
        _formatSeleccionat = _configuracio.FormatInformePreferit;

        // Configuració: tema i perfil.
        _temaSeleccionat = _configuracio.Tema;
        _profNom = _configuracio.ProfNom;
        _profCognoms = _configuracio.ProfCognoms;
        _profCentre = _configuracio.ProfCentre;
        _profDepartament = _configuracio.ProfDepartament;
        _profEmail = _configuracio.ProfEmail;

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

        RefrescaCopies();
    }

    // ---------------- Assistent inicial ----------------

    // Pas de l'assistent: 1 = definir franjes i assignatures; 2 = col·locar classes.
    [ObservableProperty] private int _passAssistent = 1;
    public bool EsPas1 => PassAssistent == 1;
    public bool EsPas2 => PassAssistent == 2;
    partial void OnPassAssistentChanged(int value)
    {
        OnPropertyChanged(nameof(EsPas1));
        OnPropertyChanged(nameof(EsPas2));
    }

    private void IniciaAssistent()
    {
        MostraAssistent = true;
        PassAssistent = 1;
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
    {
        // Hora dinàmica: la nova franja comença quan acaba l'última definida,
        // i proposa una durada d'una hora.
        string iniciNou = "09:00";
        string fiNou = "10:00";
        var ultima = FranjesAssistent.LastOrDefault();
        if (ultima != null && TimeOnly.TryParse(ultima.HoraFi, out var fiUltima))
        {
            iniciNou = fiUltima.ToString("HH:mm");
            fiNou = fiUltima.AddHours(1).ToString("HH:mm");
        }
        FranjesAssistent.Add(new FranjaEditVm { HoraInici = iniciNou, HoraFi = fiNou });
    }

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

    // Pas 1 -> Pas 2: valida i desa franjes + assignatures + versió, i prepara
    // les llistes per col·locar classes a la graella (per dia).
    [RelayCommand]
    private void ContinuaAssistent()
    {
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
            MissatgeAssistent = "Cal definir com a mínim una franja horària i una assignatura.";
            return;
        }

        // Desa franjes (ordenades per hora d'inici).
        int ordre = 1;
        foreach (var (hi, hf) in franjesValides.OrderBy(x => x.Item1))
            _horari.AfegeixFranja(new FranjaHorari { Ordre = ordre++, HoraInici = hi, HoraFi = hf });

        // Desa assignatures.
        foreach (var a in assignaturesValides)
            _horari.AfegeixAssignatura(new Assignatura { Nom = a.Nom, Curs = a.Curs, Color = a.Color });

        // Crea i activa la versió d'horari inicial.
        var versio = _horari.CreaVersio("Horari inicial del curs", _configuracio.DataIniciCurs);
        _horari.ActivaVersio(versio, _configuracio.DataIniciCurs);
        _versioActiva = versio;
        _configuracio.VersioHorariActivaId = versio;

        // Carrega les franjes i assignatures desades per poder col·locar classes.
        _franjes = _horari.ObteFranjes();
        RefrescaConfigHorari();
        NovaClasseAssignatura = AssignaturesConfig.FirstOrDefault();
        NovaClasseFranja = FranjesConfig.FirstOrDefault();

        MissatgeAssistent = string.Empty;
        PassAssistent = 2;
    }

    // Afegeix una classe a la graella durant l'assistent (pas 2).
    [RelayCommand]
    private void AfegeixClasseAssistent()
    {
        if (NovaClasseAssignatura == null || NovaClasseFranja == null)
        {
            MissatgeAssistent = "Selecciona dia, assignatura i franja.";
            return;
        }
        _horari.AfegeixClasse(new ClasseHorari
        {
            VersioHorariId = _versioActiva,
            DiaSetmana = NovaClasseDia,
            FranjaId = NovaClasseFranja.Id,
            AssignaturaId = NovaClasseAssignatura.Id,
            Grup = NovaClasseGrup?.Trim() ?? "",
            Aula = NovaClasseAula?.Trim() ?? ""
        });
        NovaClasseGrup = string.Empty;
        NovaClasseAula = string.Empty;
        RefrescaConfigHorari();
        MissatgeAssistent = $"Classe afegida: {NomDia(NovaClasseDia)} · {NovaClasseAssignatura.Nom}";
    }

    [RelayCommand]
    private void EliminaClasseAssistent(ClasseHorari classe)
    {
        if (classe == null) return;
        _horari.EliminaClasse(classe.Id);
        RefrescaConfigHorari();
    }

    // Pas 2 -> finalitza: marca l'assistent com a completat i mostra la graella.
    [RelayCommand]
    private void CompletaAssistent()
    {
        _configuracio.AssistentCompletat = true;
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
        RefrescaConfigHorari();
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

    // ================= CONFIGURACIÓ =================

    // ---- Tema (mode clar/fosc) ----
    public ObservableCollection<string> OpcionsTema { get; } = new() { "Sistema", "Clar", "Fosc" };
    [ObservableProperty] private string _temaSeleccionat = "Sistema";

    partial void OnTemaSeleccionatChanged(string value)
    {
        _configuracio.Tema = value;
        _config.Desa(_configuracio);
        TemaCanviat?.Invoke(value); // App l'aplica a l'instant
    }

    // ---- Perfil del professor ----
    [ObservableProperty] private string _profNom = string.Empty;
    [ObservableProperty] private string _profCognoms = string.Empty;
    [ObservableProperty] private string _profCentre = string.Empty;
    [ObservableProperty] private string _profDepartament = string.Empty;
    [ObservableProperty] private string _profEmail = string.Empty;
    [ObservableProperty] private string _missatgePerfil = string.Empty;

    [RelayCommand]
    private void DesaPerfil()
    {
        _configuracio.ProfNom = ProfNom?.Trim() ?? string.Empty;
        _configuracio.ProfCognoms = ProfCognoms?.Trim() ?? string.Empty;
        _configuracio.ProfCentre = ProfCentre?.Trim() ?? string.Empty;
        _configuracio.ProfDepartament = ProfDepartament?.Trim() ?? string.Empty;
        _configuracio.ProfEmail = ProfEmail?.Trim() ?? string.Empty;
        _config.Desa(_configuracio);
        MissatgePerfil = "Perfil desat correctament.";
    }

    // ---- Còpies de seguretat ----
    public ObservableCollection<CopiaVm> Copies { get; } = new();
    [ObservableProperty] private CopiaVm? _copiaSeleccionada;
    [ObservableProperty] private string _missatgeBackup = string.Empty;

    private void RefrescaCopies()
    {
        Copies.Clear();
        foreach (var f in _backup.LlistaCopies())
            Copies.Add(new CopiaVm(f.FullName, f.Name, f.CreationTime, f.Length));
    }

    [RelayCommand]
    private void CreaCopiaSeguretat()
    {
        try
        {
            var ruta = _backup.CreaCopia("manual");
            RefrescaCopies();
            MissatgeBackup = "Còpia creada: " + Path.GetFileName(ruta);
        }
        catch (Exception ex)
        {
            MissatgeBackup = "Error en crear la còpia: " + ex.Message;
        }
    }

    [RelayCommand]
    private void RestauraCopiaSeguretat()
    {
        if (CopiaSeleccionada == null)
        {
            MissatgeBackup = "Selecciona primer una còpia de la llista.";
            return;
        }

        var (ok, missatge) = _backup.Restaura(CopiaSeleccionada.RutaCompleta);
        MissatgeBackup = missatge;

        if (ok)
        {
            // Recarrega les dades des de la còpia restaurada.
            _configuracio = _config.Carrega();
            CarregaHorari();
            RefrescaGraella();
            RefrescaCopies();
        }
    }

    // ---- Edició d'horari ----
    public ObservableCollection<Assignatura> AssignaturesConfig { get; } = new();
    public ObservableCollection<FranjaHorari> FranjesConfig { get; } = new();
    public ObservableCollection<ClasseHorari> ClassesConfig { get; } = new();

    // Camps per afegir una assignatura nova.
    [ObservableProperty] private string _novaAssignaturaNom = string.Empty;
    [ObservableProperty] private string _novaAssignaturaCurs = string.Empty;
    // Camps per afegir una franja nova.
    [ObservableProperty] private string _novaFranjaInici = "08:00";
    [ObservableProperty] private string _novaFranjaFi = "09:00";
    // Camps per afegir una classe a la graella.
    public ObservableCollection<int> DiesSetmanaOpcions { get; } = new() { 1, 2, 3, 4, 5 };
    [ObservableProperty] private int _novaClasseDia = 1;
    [ObservableProperty] private Assignatura? _novaClasseAssignatura;
    [ObservableProperty] private FranjaHorari? _novaClasseFranja;
    [ObservableProperty] private string _novaClasseGrup = string.Empty;
    [ObservableProperty] private string _novaClasseAula = string.Empty;
    [ObservableProperty] private string _missatgeHorari = string.Empty;

    private static readonly string[] NomsDies =
        { "", "Dilluns", "Dimarts", "Dimecres", "Dijous", "Divendres" };
    public static string NomDia(int dia) => dia >= 1 && dia <= 5 ? NomsDies[dia] : "";

    public void RefrescaConfigHorari()
    {
        AssignaturesConfig.Clear();
        foreach (var a in _horari.ObteAssignatures()) AssignaturesConfig.Add(a);
        FranjesConfig.Clear();
        foreach (var f in _horari.ObteFranjes()) FranjesConfig.Add(f);
        ClassesConfig.Clear();
        foreach (var c in _horari.ObteClasses(_versioActiva)) ClassesConfig.Add(c);
    }

    [RelayCommand]
    private void AfegeixAssignaturaConfig()
    {
        if (string.IsNullOrWhiteSpace(NovaAssignaturaNom)) { MissatgeHorari = "Cal un nom d'assignatura."; return; }
        var colors = new[] { "#4F86C6", "#E06C75", "#98C379", "#E5C07B", "#C678DD", "#56B6C2", "#D19A66" };
        var color = colors[AssignaturesConfig.Count % colors.Length];
        _horari.AfegeixAssignatura(new Assignatura { Nom = NovaAssignaturaNom.Trim(), Curs = NovaAssignaturaCurs?.Trim() ?? "", Color = color });
        NovaAssignaturaNom = string.Empty;
        NovaAssignaturaCurs = string.Empty;
        RefrescaConfigHorari();
        MissatgeHorari = "Assignatura afegida.";
    }

    [RelayCommand]
    private void AfegeixFranjaConfig()
    {
        if (!TimeOnly.TryParse(NovaFranjaInici, out var hi) || !TimeOnly.TryParse(NovaFranjaFi, out var hf) || hf <= hi)
        {
            MissatgeHorari = "Franja no vàlida (hora d'inici < hora de fi, format HH:mm).";
            return;
        }
        var ordre = FranjesConfig.Count + 1;
        _horari.AfegeixFranja(new FranjaHorari { Ordre = ordre, HoraInici = hi, HoraFi = hf });
        RefrescaConfigHorari();
        MissatgeHorari = "Franja afegida.";
    }

    [RelayCommand]
    private void AfegeixClasseConfig()
    {
        if (NovaClasseAssignatura == null || NovaClasseFranja == null)
        {
            MissatgeHorari = "Selecciona assignatura i franja.";
            return;
        }
        _horari.AfegeixClasse(new ClasseHorari
        {
            VersioHorariId = _versioActiva,
            DiaSetmana = NovaClasseDia,
            FranjaId = NovaClasseFranja.Id,
            AssignaturaId = NovaClasseAssignatura.Id,
            Grup = NovaClasseGrup?.Trim() ?? "",
            Aula = NovaClasseAula?.Trim() ?? ""
        });
        NovaClasseGrup = string.Empty;
        NovaClasseAula = string.Empty;
        RefrescaConfigHorari();
        RefrescaGraella();
        MissatgeHorari = "Classe afegida a l'horari.";
    }

    [RelayCommand]
    private void EliminaClasseConfig(ClasseHorari classe)
    {
        if (classe == null) return;
        _horari.EliminaClasse(classe.Id);
        RefrescaConfigHorari();
        RefrescaGraella();
        MissatgeHorari = "Classe eliminada.";
    }
}

// Representa una còpia de seguretat a la llista de la UI.
public class CopiaVm
{
    public string RutaCompleta { get; }
    public string Nom { get; }
    public DateTime Data { get; }
    public long Bytes { get; }

    public CopiaVm(string ruta, string nom, DateTime data, long bytes)
    {
        RutaCompleta = ruta;
        Nom = nom;
        Data = data;
        Bytes = bytes;
    }

    public string Descripcio => $"{Data:dd/MM/yyyy HH:mm}  ·  {Math.Round(Bytes / 1024.0)} KB  ·  {Nom}";
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

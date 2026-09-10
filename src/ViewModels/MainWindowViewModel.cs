using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly Data.Database _db;
    private static readonly CultureInfo Ca = new("ca-ES");

    // S'emet quan l'usuari canvia el tema, perquè App l'apliqui a l'instant.
    public event Action<string>? TemaCanviat;

    private Configuracio _configuracio;
    private int _versioActiva;
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
    // Feedback temporal "Desat ✓" després de desar una nota.
    [ObservableProperty] private bool _notaDesadaRecentment;
    private System.Threading.Timer? _timerDesat;
    private ClasseHorari? _classeSeleccionada;
    private DateTime _diaSeleccionat;

    // Quan canvia si hi ha una nota oberta, recalcula els estats derivats dels panells.
    partial void OnMostraEditorNotaChanged(bool value)
    {
        OnPropertyChanged(nameof(MostraOverlayModal));
        OnPropertyChanged(nameof(MostraEstatBuitPanell));
    }

    // Informes.
    public ObservableCollection<string> FormatsInforme { get; } = new() { "PDF", "XLSX", "CSV" };
    public ObservableCollection<string> PeriodesInforme { get; } = new()
        { "Setmana actual", "Mes actual", "Trimestre actual", "Tot el curs" };
    [ObservableProperty] private string _formatSeleccionat = "PDF";
    [ObservableProperty] private string _periodeSeleccionat = "Mes actual";
    [ObservableProperty] private string _missatgeInforme = string.Empty;

    // Assistent: dades introduïdes.
    public ObservableCollection<AssignaturaEditVm> AssignaturesAssistent { get; } = new();

    public string RutaDades => PathService.GetDataDirectory();

    public MainWindowViewModel(
        HorariService horari, NotesService notes, CalendariService calendari,
        ConfigService config, InformeService informe, BackupService backup,
        Data.Database db)
    {
        _horari = horari;
        _notes = notes;
        _calendari = calendari;
        _config = config;
        _informe = informe;
        _backup = backup;
        _db = db;

        _configuracio = _config.Carrega();
        _formatSeleccionat = _configuracio.FormatInformePreferit;

        // Configuració: tema i perfil.
        _temaSeleccionat = _configuracio.Tema;
        _posicioEditorNotes = _configuracio.PosicioEditorNotes;
        _profNom = _configuracio.ProfNom;
        _profCognoms = _configuracio.ProfCognoms;
        _profCentre = _configuracio.ProfCentre;
        _profDepartament = _configuracio.ProfDepartament;
        _profEmail = _configuracio.ProfEmail;

        // Setmana inicial: la d'inici de curs o la setmana actual si el curs ja ha començat.
        var avui = DateTime.Today;
        var referencia = avui < _configuracio.DataIniciCurs ? _configuracio.DataIniciCurs : avui;
        _setmanaActual = CalendariService.DillunsDeLaSetmana(referencia);

        // Carrega sempre les classes i calcula el calendari (encara que es mostri
        // l'assistent), perquè la graella de fons tingui valors vàlids.
        CarregaHorari();
        RefrescaGraella();

        if (!_configuracio.AssistentCompletat)
            IniciaAssistent();

        RefrescaCopies();
        RefrescaDiesLliure();
    }

    // ---------------- Assistent inicial (només assignatures) ----------------

    private void IniciaAssistent()
    {
        MostraAssistent = true;
        AssignaturesAssistent.Clear();
        AssignaturesAssistent.Add(new AssignaturaEditVm { Nom = "", Curs = "", Color = "#4F86C6" });
        MissatgeAssistent = string.Empty;
    }

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

    // Completa l'assistent: desa les assignatures, crea la versió d'horari inicial
    // i mostra el calendari (buit) perquè el professor hi afegeixi les classes.
    [RelayCommand]
    private void CompletaAssistent()
    {
        var assignaturesValides = AssignaturesAssistent
            .Where(a => !string.IsNullOrWhiteSpace(a.Nom)).ToList();

        if (assignaturesValides.Count == 0)
        {
            MissatgeAssistent = "Cal definir com a mínim una assignatura.";
            return;
        }

        foreach (var a in assignaturesValides)
            _horari.AfegeixAssignatura(new Assignatura { Nom = a.Nom.Trim(), Curs = a.Curs?.Trim() ?? "", Color = a.Color });

        var versio = _horari.CreaVersio("Horari inicial del curs", _configuracio.DataIniciCurs);
        _horari.ActivaVersio(versio, _configuracio.DataIniciCurs);
        _versioActiva = versio;
        _configuracio.VersioHorariActivaId = versio;
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
        _classes = _horari.ObteClasses(_versioActiva);
        RefrescaConfigHorari();
    }

    // Reconstrueix les vistes per a la setmana visible.
    private void RefrescaGraella()
    {
        RefrescaCapceleraDies();
        RefrescaCalendari();
        ActualitzaTitolSetmana();
    }

    // ===== Layout tipus Google Calendar =====
    public ObservableCollection<BlocCalendariVm> BlocsCalendari { get; } = new();
    public ObservableCollection<ColumnaDiaVm> ColumnesDia { get; } = new();
    public ObservableCollection<EtiquetaHoraVm> EtiquetesHora { get; } = new();

    // Alçada d'una fila de 30 min (px). L'usa el XAML per dimensionar la graella.
    public double AlcadaFila => 30;
    [ObservableProperty] private int _nombreFiles = 20; // per defecte 10h * 2

    private const int MinutsPerFila = 30;
    private int _minutBase; // minut del dia on comença la graella (p. ex. 08:00 -> 480)
    public int MinutBase => _minutBase;

    // Calcula la posició de cada classe segons la seva hora, per a la vista calendari.
    private void RefrescaCalendari()
    {
        BlocsCalendari.Clear();
        ColumnesDia.Clear();
        EtiquetesHora.Clear();

        // Rang horari: de l'inici més matiner al final més tardà de les classes.
        // Si no hi ha classes, mostrem un rang per defecte (08:00–18:00) perquè
        // el professor pugui clicar per crear la primera classe.
        int minInici, maxFi;
        if (_classes.Count == 0)
        {
            minInici = 8 * 60;
            maxFi = 18 * 60;
        }
        else
        {
            minInici = _classes.Min(c => c.HoraInici.Hour * 60 + c.HoraInici.Minute);
            maxFi = _classes.Max(c => c.HoraFi.Hour * 60 + c.HoraFi.Minute);
            // Marge perquè sempre hi hagi almenys una fila buida a sota per clicar.
            minInici = Math.Min(minInici, 8 * 60);
            maxFi = Math.Max(maxFi, minInici + 60);
        }

        // Arrodonim l'inici cap avall a la mitja hora i el final cap amunt.
        _minutBase = (minInici / MinutsPerFila) * MinutsPerFila;
        int finalArrod = ((maxFi + MinutsPerFila - 1) / MinutsPerFila) * MinutsPerFila;
        NombreFiles = Math.Max(1, (finalArrod - _minutBase) / MinutsPerFila);

        // Eix d'hores: una etiqueta a cada hora en punt.
        for (int m = _minutBase; m < finalArrod; m += MinutsPerFila)
        {
            if (m % 60 == 0)
            {
                int fila = (m - _minutBase) / MinutsPerFila;
                EtiquetesHora.Add(new EtiquetaHoraVm(fila, $"{m / 60:00}:00"));
            }
        }

        // Columnes (fons): dia actual i festius.
        for (int dia = 1; dia <= 5; dia++)
        {
            var data = SetmanaActual.AddDays(dia - 1);
            bool esAvui = data.Date == DateTime.Today;
            bool esFestiu = _calendari.FestiuDe(data) != null;
            ColumnesDia.Add(new ColumnaDiaVm(dia, esAvui, esFestiu));
        }

        // Blocs de classe posicionats per hora d'inici i durada.
        foreach (var classe in _classes)
        {
            int ini = classe.HoraInici.Hour * 60 + classe.HoraInici.Minute;
            int fi = classe.HoraFi.Hour * 60 + classe.HoraFi.Minute;

            int fila = (ini - _minutBase) / MinutsPerFila;
            int span = Math.Max(1, (fi - ini) / MinutsPerFila);
            if (fila < 0) fila = 0;

            var bloc = new BlocCalendariVm(classe, fila, span, classe.DiaSetmana);
            var nota = _notes.ObteNota(classe.Id, SetmanaActual);
            bloc.TeNota = nota != null && !string.IsNullOrWhiteSpace(nota.Text);
            BlocsCalendari.Add(bloc);
        }

        CalendariActualitzat?.Invoke();
    }

    // S'emet quan cal redibuixar la graella de calendari (canvi de setmana, etc.).
    public event Action? CalendariActualitzat;

    // Capçalera de dies tipus calendari: nom + data, marca del dia actual i festius.
    public ObservableCollection<DiaCapceleraVm> DiesCapcalera { get; } = new();

    private void RefrescaCapceleraDies()
    {
        DiesCapcalera.Clear();
        var noms = new[] { "Dilluns", "Dimarts", "Dimecres", "Dijous", "Divendres" };
        for (int i = 0; i < 5; i++)
        {
            var data = SetmanaActual.AddDays(i);
            var festiu = _calendari.FestiuDe(data);
            DiesCapcalera.Add(new DiaCapceleraVm(
                noms[i],
                data,
                esAvui: data.Date == DateTime.Today,
                festiu: festiu?.Nom));
        }
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

    // S'invoca en fer clic sobre un bloc de la vista calendari.
    public void ObreNota(BlocCalendariVm bloc) => ObreNotaClasse(bloc.Classe);

    // Lògica comuna d'obertura de l'editor de notes per a una classe.
    private void ObreNotaClasse(ClasseHorari classe)
    {
        // Determina el dia concret (columna) de la classe dins la setmana.
        _diaSeleccionat = SetmanaActual.AddDays(classe.DiaSetmana - 1);
        _classeSeleccionada = classe;

        var nota = _notes.ObteNota(classe.Id, SetmanaActual);
        TextNota = nota?.Text ?? string.Empty;

        TitolNota = classe.Assignatura?.Nom ?? "Classe";
        SubtitolNota =
            $"{_diaSeleccionat:dddd d 'de' MMMM} · {classe.Etiqueta}" +
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
        RefrescaGraella();

        // En mode Modal tanquem la finestra. En un panell fix el deixem obert
        // perquè el professor pugui seguir editant; mostrem feedback "Desat ✓".
        if (EsModeModal)
        {
            MostraEditorNota = false;
        }
        else
        {
            // La graella s'ha refrescat i pot haver perdut la referència de
            // selecció visual; recarreguem la nota per mantenir l'editor coherent.
            MostraFeedbackDesat();
        }
    }

    // Mostra "Desat ✓" durant uns segons (sense bloquejar).
    private void MostraFeedbackDesat()
    {
        NotaDesadaRecentment = true;
        _timerDesat?.Dispose();
        _timerDesat = new System.Threading.Timer(_ =>
        {
            // Torna al fil d'UI per canviar la propietat enllaçada.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => NotaDesadaRecentment = false);
        }, null, 2000, System.Threading.Timeout.Infinite);
    }

    [RelayCommand]
    private void CancelaNota()
    {
        MostraEditorNota = false;
        if (MostraPanellFix) _classeSeleccionada = null;
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

    // ---- Posició de l'editor de notes (preferència global) ----
    // Valor persistit: 'Modal' | 'Inferior' | 'Dret'.
    [ObservableProperty] private string _posicioEditorNotes = "Modal";

    partial void OnPosicioEditorNotesChanged(string value)
    {
        _configuracio.PosicioEditorNotes = value;
        _config.Desa(_configuracio);
        // Notifica els booleans derivats perquè la vista actualitzi els panells.
        OnPropertyChanged(nameof(EsModeModal));
        OnPropertyChanged(nameof(EsModeInferior));
        OnPropertyChanged(nameof(EsModeDret));
        OnPropertyChanged(nameof(MostraPanellFix));
        OnPropertyChanged(nameof(MostraOverlayModal));
        OnPropertyChanged(nameof(MostraEstatBuitPanell));
        // Sincronitza els booleans dels RadioButton (patró radio-enum).
        OnPropertyChanged(nameof(ModeNotaModal));
        OnPropertyChanged(nameof(ModeNotaInferior));
        OnPropertyChanged(nameof(ModeNotaDret));
    }

    // Booleans derivats per condicionar el layout a la vista.
    public bool EsModeModal => PosicioEditorNotes == "Modal";
    public bool EsModeInferior => PosicioEditorNotes == "Inferior";
    public bool EsModeDret => PosicioEditorNotes == "Dret";

    // Un panell fix (inferior o dret) està actiu.
    public bool MostraPanellFix => EsModeInferior || EsModeDret;
    // L'overlay modal només es mostra en mode Modal i amb una nota oberta.
    public bool MostraOverlayModal => EsModeModal && MostraEditorNota;
    // En un panell fix, si no hi ha cap nota oberta, es mostra l'estat buit.
    public bool MostraEstatBuitPanell => MostraPanellFix && !MostraEditorNota;

    // Selecció de mode per als RadioButton (patró radio-enum: assignar true fixa el valor).
    public bool ModeNotaModal
    {
        get => EsModeModal;
        set { if (value) PosicioEditorNotes = "Modal"; }
    }
    public bool ModeNotaInferior
    {
        get => EsModeInferior;
        set { if (value) PosicioEditorNotes = "Inferior"; }
    }
    public bool ModeNotaDret
    {
        get => EsModeDret;
        set { if (value) PosicioEditorNotes = "Dret"; }
    }

    // Tanca l'editor en mode panell fix (torna a l'estat buit sense desar).
    [RelayCommand]
    private void TancaPanellNota()
    {
        MostraEditorNota = false;
        _classeSeleccionada = null;
    }

    // Amplada actual del panell dret (px). La vista la llegeix a l'arrencada i la
    // desa quan l'usuari acaba d'arrossegar el divisor.
    public int AmpladaPanellDret => _configuracio.AmpladaPanellDret;

    // Desa la nova amplada del panell dret (cridada per la vista en soltar el divisor).
    public void DesaAmpladaPanell(double px)
    {
        int valor = (int)Math.Round(px);
        if (valor == _configuracio.AmpladaPanellDret) return;
        _configuracio.AmpladaPanellDret = valor; // ConfigService la limita al rang vàlid
        _config.Desa(_configuracio);
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

    // ---- Gestió d'assignatures (a Configuració) ----
    public ObservableCollection<Assignatura> AssignaturesConfig { get; } = new();
    public ObservableCollection<ClasseHorari> ClassesConfig { get; } = new();
    public ObservableCollection<int> DiesSetmanaOpcions { get; } = new() { 1, 2, 3, 4, 5 };

    [ObservableProperty] private string _novaAssignaturaNom = string.Empty;
    [ObservableProperty] private string _novaAssignaturaCurs = string.Empty;
    [ObservableProperty] private string _missatgeHorari = string.Empty;

    private static readonly string[] NomsDies =
        { "", "Dilluns", "Dimarts", "Dimecres", "Dijous", "Divendres" };
    public static string NomDia(int dia) => dia >= 1 && dia <= 5 ? NomsDies[dia] : "";

    public void RefrescaConfigHorari()
    {
        AssignaturesConfig.Clear();
        foreach (var a in _horari.ObteAssignatures()) AssignaturesConfig.Add(a);
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
    private void EliminaClasseConfig(ClasseHorari classe)
    {
        if (classe == null) return;
        _horari.EliminaClasse(classe.Id);
        RefrescaConfigHorari();
        RefrescaGraella();
        MissatgeHorari = "Classe eliminada.";
    }

    // ---- Pop-up de classe (crear/editar en fer clic al calendari) ----
    [ObservableProperty] private bool _mostraPopupClasse;
    [ObservableProperty] private bool _popupEsEdicio;
    [ObservableProperty] private int _popupDia = 1;
    [ObservableProperty] private string _popupHoraInici = "09:00";
    [ObservableProperty] private string _popupHoraFi = "10:00";
    [ObservableProperty] private Assignatura? _popupAssignatura;
    [ObservableProperty] private string _popupGrup = string.Empty;
    [ObservableProperty] private string _popupAula = string.Empty;
    [ObservableProperty] private string _popupTitol = string.Empty;
    [ObservableProperty] private string _missatgePopup = string.Empty;
    public ObservableCollection<Assignatura> PopupAssignatures { get; } = new();
    private int _popupClasseId;

    private void CarregaAssignaturesPopup()
    {
        PopupAssignatures.Clear();
        foreach (var a in _horari.ObteAssignatures()) PopupAssignatures.Add(a);
    }

    // Obre el pop-up per crear una classe nova (des d'un buit del calendari).
    public void ObrePopupNovaClasse(int dia, int horaIniciMinuts)
    {
        CarregaAssignaturesPopup();
        if (PopupAssignatures.Count == 0)
        {
            MissatgeHorari = "Primer defineix alguna assignatura a Configuració.";
            return;
        }
        _popupClasseId = 0;
        PopupEsEdicio = false;
        PopupTitol = "Nova classe";
        PopupDia = dia >= 1 && dia <= 5 ? dia : 1;
        var ini = new TimeOnly(horaIniciMinuts / 60, horaIniciMinuts % 60);
        PopupHoraInici = ini.ToString("HH:mm");
        PopupHoraFi = ini.AddHours(1).ToString("HH:mm");
        PopupAssignatura = PopupAssignatures.FirstOrDefault();
        PopupGrup = string.Empty;
        PopupAula = string.Empty;
        MissatgePopup = string.Empty;
        MostraPopupClasse = true;
    }

    // Obre el pop-up per editar una classe existent (des d'un bloc del calendari).
    public void ObrePopupEditaClasse(BlocCalendariVm bloc)
    {
        CarregaAssignaturesPopup();
        var c = bloc.Classe;
        _popupClasseId = c.Id;
        PopupEsEdicio = true;
        PopupTitol = "Edita la classe";
        PopupDia = c.DiaSetmana;
        PopupHoraInici = c.HoraInici.ToString("HH:mm");
        PopupHoraFi = c.HoraFi.ToString("HH:mm");
        PopupAssignatura = PopupAssignatures.FirstOrDefault(a => a.Id == c.AssignaturaId) ?? PopupAssignatures.FirstOrDefault();
        PopupGrup = c.Grup;
        PopupAula = c.Aula;
        MissatgePopup = string.Empty;
        MostraPopupClasse = true;
    }

    [RelayCommand]
    private void CancelaPopupClasse() => MostraPopupClasse = false;

    [RelayCommand]
    private void DesaClassePopup()
    {
        if (PopupAssignatura == null)
        {
            MissatgePopup = "Selecciona una assignatura.";
            return;
        }
        if (!TimeOnly.TryParse(PopupHoraInici, out var hi) || !TimeOnly.TryParse(PopupHoraFi, out var hf) || hf <= hi)
        {
            MissatgePopup = "Horari no vàlid (inici < fi, format HH:mm).";
            return;
        }

        var classe = new ClasseHorari
        {
            Id = _popupClasseId,
            VersioHorariId = _versioActiva,
            DiaSetmana = PopupDia,
            HoraInici = hi,
            HoraFi = hf,
            AssignaturaId = PopupAssignatura.Id,
            Grup = PopupGrup?.Trim() ?? "",
            Aula = PopupAula?.Trim() ?? ""
        };

        if (PopupEsEdicio) _horari.ActualitzaClasse(classe);
        else _horari.AfegeixClasse(classe);

        MostraPopupClasse = false;
        CarregaHorari();
        RefrescaGraella();
    }

    [RelayCommand]
    private void EliminaClassePopup()
    {
        if (_popupClasseId > 0)
            _horari.EliminaClasse(_popupClasseId);
        MostraPopupClasse = false;
        CarregaHorari();
        RefrescaGraella();
    }

    // ---- Dies de lliure disposició (a Configuració) ----
    public ObservableCollection<Festiu> DiesLliure { get; } = new();
    // Data en text 'dd/MM/yyyy'. Fem servir un TextBox en lloc del DatePicker
    // perquè el DatePicker d'Avalonia 11.2 pot fer caure l'aplicació en
    // renderitzar-se (problema conegut del control) en alguns equips Windows.
    [ObservableProperty] private string _novaDataLliure = DateTime.Today.ToString("dd/MM/yyyy");
    [ObservableProperty] private string _descripcioLliure = string.Empty;
    [ObservableProperty] private string _missatgeLliure = string.Empty;

    public void RefrescaDiesLliure()
    {
        DiesLliure.Clear();
        foreach (var f in _calendari.ObteFestius().Where(f => f.Tipus == "LliureDisposicio").OrderBy(f => f.Data))
            DiesLliure.Add(f);
    }

    [RelayCommand]
    private void AfegeixDiaLliure()
    {
        if (!DateTime.TryParseExact(NovaDataLliure?.Trim(), "dd/MM/yyyy",
                Ca, System.Globalization.DateTimeStyles.None, out var data))
        {
            MissatgeLliure = "Data no vàlida. Fes servir el format dd/mm/aaaa.";
            return;
        }
        _calendari.AfegeixDiaLliure(data.Date, DescripcioLliure);
        DescripcioLliure = string.Empty;
        RefrescaDiesLliure();
        RefrescaGraella();
        MissatgeLliure = "Dia de lliure disposició afegit.";
    }

    [RelayCommand]
    private void EliminaDiaLliure(Festiu f)
    {
        if (f == null) return;
        _calendari.EliminaFestiu(f.Id);
        RefrescaDiesLliure();
        RefrescaGraella();
        MissatgeLliure = "Dia eliminat.";
    }

    // ---- Actualitzacions (actualitzador assistit) ----
    private readonly UpdateService _update = new();

    // S'emet perquè la vista obri una URL al navegador per defecte.
    public event Action<string>? ObreUrlDemanada;

    public string VersioActualApp => "v" + UpdateService.VersioActual();
    [ObservableProperty] private bool _comprovantActualitzacio;
    [ObservableProperty] private string _missatgeActualitzacio = string.Empty;
    [ObservableProperty] private string _notesActualitzacio = string.Empty;
    [ObservableProperty] private bool _hiHaActualitzacio;
    private string? _urlDescarrega;
    private string? _urlPaginaRelease;

    [RelayCommand]
    private async Task ComprovaActualitzacions()
    {
        if (ComprovantActualitzacio) return;
        ComprovantActualitzacio = true;
        HiHaActualitzacio = false;
        NotesActualitzacio = string.Empty;
        MissatgeActualitzacio = "Comprovant si hi ha actualitzacions…";

        var r = await _update.ComprovaAsync();

        switch (r.Estat)
        {
            case UpdateService.Estat.AlDia:
                MissatgeActualitzacio = $"Ja tens l'última versió (v{r.VersioActual}).";
                break;
            case UpdateService.Estat.HiHaActualitzacio:
                HiHaActualitzacio = true;
                _urlDescarrega = r.UrlZip;
                _urlPaginaRelease = r.UrlPaginaRelease;
                MissatgeActualitzacio =
                    $"Hi ha una versió nova disponible: v{r.VersioNova} (tens la v{r.VersioActual}).";
                NotesActualitzacio = string.IsNullOrWhiteSpace(r.Notes) ? string.Empty : r.Notes!;
                break;
            case UpdateService.Estat.SenseConnexio:
                MissatgeActualitzacio =
                    "No s'ha pogut comprovar: " + r.MissatgeError +
                    " Pots descarregar l'última versió manualment quan tinguis connexió.";
                break;
            default:
                MissatgeActualitzacio = "No s'ha pogut comprovar: " + r.MissatgeError;
                break;
        }

        ComprovantActualitzacio = false;
    }

    [RelayCommand]
    private void BaixaActualitzacio()
    {
        // Obre la descàrrega del ZIP si existeix; si no, la pàgina de la release.
        var url = _urlDescarrega ?? _urlPaginaRelease;
        if (!string.IsNullOrEmpty(url))
            ObreUrlDemanada?.Invoke(url);
    }

    // ---- Esborrar dades (amb confirmació i còpia de seguretat prèvia) ----
    [ObservableProperty] private bool _mostraConfirmacio;
    [ObservableProperty] private string _titolConfirmacio = string.Empty;
    [ObservableProperty] private string _textConfirmacio = string.Empty;
    [ObservableProperty] private string _missatgeEsborrat = string.Empty;
    private Action? _accioConfirmada;

    private void DemanaConfirmacio(string titol, string text, Action accio)
    {
        TitolConfirmacio = titol;
        TextConfirmacio = text;
        _accioConfirmada = accio;
        MostraConfirmacio = true;
    }

    [RelayCommand]
    private void CancelaConfirmacio()
    {
        MostraConfirmacio = false;
        _accioConfirmada = null;
    }

    [RelayCommand]
    private void ConfirmaAccio()
    {
        MostraConfirmacio = false;
        var accio = _accioConfirmada;
        _accioConfirmada = null;
        accio?.Invoke();
    }

    // Fa una còpia de seguretat abans d'una operació destructiva (best-effort).
    private void BackupPreviBorrat(string sufix)
    {
        try { _backup.CreaCopia(sufix); RefrescaCopies(); } catch { /* continua igualment */ }
    }

    [RelayCommand]
    private void BuidaHorari()
    {
        DemanaConfirmacio(
            "Buidar l'horari",
            "S'esborraran totes les assignatures, franjes i classes de l'horari. " +
            "ATENCIÓ: també s'esborraran les NOTES associades a aquestes classes. " +
            "Es farà una còpia de seguretat abans. Vols continuar?",
            () =>
            {
                BackupPreviBorrat("abans_buidar_horari");
                _horari.BuidaHorari();
                _configuracio.AssistentCompletat = false;
                _configuracio.VersioHorariActivaId = 0;
                _config.Desa(_configuracio);
                MissatgeEsborrat = "Horari buidat. En reiniciar l'aplicació, o ara mateix, es tornarà a mostrar l'assistent.";
                // Rellança l'assistent per definir l'horari de nou.
                IniciaAssistent();
            });
    }

    [RelayCommand]
    private void EsborraNotes()
    {
        DemanaConfirmacio(
            "Esborrar totes les notes",
            "S'esborraran TOTES les notes de totes les classes i setmanes. " +
            "L'horari es conserva. Es farà una còpia de seguretat abans. Vols continuar?",
            () =>
            {
                BackupPreviBorrat("abans_esborrar_notes");
                _notes.EsborraTotesLesNotes();
                CarregaHorari();
                RefrescaGraella();
                MissatgeEsborrat = "S'han esborrat totes les notes.";
            });
    }

    [RelayCommand]
    private void RestableixTot()
    {
        DemanaConfirmacio(
            "Restablir-ho tot",
            "S'esborraran l'horari i TOTES les notes, i es tornarà a l'assistent inicial. " +
            "Es conserven el calendari de festius, el tema i el perfil. " +
            "Es farà una còpia de seguretat abans. Vols continuar?",
            () =>
            {
                BackupPreviBorrat("abans_restablir_tot");
                _db.RestableixDades();
                _configuracio = _config.Carrega();
                MissatgeEsborrat = "Dades restablertes.";
                IniciaAssistent();
            });
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

// Capçalera d'un dia de la setmana (nom + data, marca d'avui i festiu).
public class DiaCapceleraVm : ViewModelBase
{
    public string Nom { get; }
    public DateTime Data { get; }
    public bool EsAvui { get; }
    public string? Festiu { get; }

    public DiaCapceleraVm(string nom, DateTime data, bool esAvui, string? festiu)
    {
        Nom = nom;
        Data = data;
        EsAvui = esAvui;
        Festiu = festiu;
    }

    public string DataText => Data.ToString("dd/MM");
    public bool EsFestiu => !string.IsNullOrEmpty(Festiu);
    // Text de la capçalera: nom + data; si és festiu, ho indica.
    public string Titol => EsFestiu ? $"{Nom} {DataText}" : $"{Nom} {DataText}";
    public string SubTitol => EsFestiu ? Festiu! : (EsAvui ? "Avui" : "");
}

// Model editable d'assignatura per a l'assistent.
public partial class AssignaturaEditVm : ViewModelBase
{
    [ObservableProperty] private string _nom = string.Empty;
    [ObservableProperty] private string _curs = string.Empty;
    [ObservableProperty] private string _color = "#4F86C6";
}

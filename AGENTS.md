# AGENTS.md — Context per a agents (Programació Docent)

Aquest fitxer dona context a un agent d'IA (Kiro u altres) perquè pugui continuar
el desenvolupament d'aquest projecte. Escrit en català; el codi i la UI també.

## Què és

Aplicació d'escriptori per al professorat de Catalunya per gestionar l'horari de
classes, prendre notes setmanals per classe i generar informes per assignatura.

- **100 % local i offline**, un sol usuari per equip.
- **Portable per a Windows 64 bits**: `.exe` autocontingut, sense instal·lació ni
  permisos d'administrador. Pensada per a equips de centres educatius, sovint
  **capats/restringits** (antivirus, AppLocker, sense .NET preinstal·lat).
- Tota la interfície **en català**.

## Stack

- **.NET 8** + **Avalonia UI 11.2** (MVVM amb CommunityToolkit.Mvvm).
- **SQLite** (Microsoft.Data.Sqlite) — base de dades local a `dades/dades.db`.
- **QuestPDF** (informes PDF) i **ClosedXML** (XLSX); CSV natiu.
- Motor gràfic **Skia** (NO depèn de WebView2).

## Com compilar i executar (entorn de desenvolupament)

Requereix el **.NET 8 SDK**.

```powershell
# Compilar i executar en local (Windows)
dotnet run --project src/ProgramacioDocent.csproj

# Compilar en Release
dotnet build src/ProgramacioDocent.csproj -c Release

# Generar el portable win-x64 (o executar publish-portable.ps1)
dotnet publish src/ProgramacioDocent.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false `
  -p:PublishReadyToRun=true -o publish/ProgramacioDocent
```

> **Nota macOS/Linux**: es pot fer cross-compile a win-x64 amb el mateix comandament.

## Distribució i versionat

- El `.exe` es genera via **GitHub Actions** (`.github/workflows/release.yml`) en
  fer **push d'un tag `vX.Y.Z`**, i es publica com a **Release** (ZIP descarregable).
- **Versionat semàntic (SemVer)**: `MAJOR.MINOR.PATCH`.
  - PATCH = correccions de bugs. MINOR = funcions noves compatibles. MAJOR = canvis grans.
  - La versió del `.exe` la fixa el workflow a partir del tag (no cal tocar el codi).
- **El repositori és públic** (necessari perquè el comprovador d'actualitzacions
  llegeixi l'API de releases sense token). Owner/repo:
  `sergi-learns-devops/ProgramacioDocent`.

## Arquitectura del codi (`src/`)

```
src/
├── Program.cs            # Punt d'entrada; LogService + handlers d'excepcions globals
├── App.axaml(.cs)        # Init: BD, serveis, tema, finestra; handler del Dispatcher UI
├── ViewLocator.cs
├── Models/Models.cs      # Configuracio, Assignatura, VersioHorari, ClasseHorari, Festiu
├── Data/
│   ├── Database.cs       # Connexió SQLite (WAL, Pooling=false), Inicialitza, integritat, RestableixDades
│   ├── Migracions.cs     # Migracions versionades (PRAGMA user_version). ACTUAL: v3
│   └── CalendariCatalunya.cs  # Seed de festius del curs 2026-2027
├── Services/
│   ├── PathService.cs    # Ubicació de dades (portable o %LOCALAPPDATA%)
│   ├── LogService.cs     # Log robust multi-ubicació (error-arrencada.log)
│   ├── ConfigService.cs  # Llegeix/desa Configuracio (tema, perfil, etc.)
│   ├── HorariService.cs  # Assignatures, versions, classes (amb hora pròpia)
│   ├── NotesService.cs   # Notes de text lliure per classe/setmana; dades d'informe
│   ├── CalendariService.cs # Festius i dies de lliure disposició
│   ├── InformeService.cs # Informes PDF/XLSX/CSV per assignatura
│   ├── BackupService.cs  # Còpies de seguretat (VACUUM INTO) + restauració
│   ├── UpdateService.cs  # Comprovador d'actualitzacions (GitHub Releases)
│   └── ContrastHelper.cs # Color de text llegible sobre el color d'assignatura
├── ViewModels/
│   ├── MainWindowViewModel.cs  # ViewModel principal (gran): horari, notes, informes,
│   │                           # assistent, configuració, backups, actualitzacions, esborrat
│   └── CalendariVms.cs   # BlocCalendariVm, ColumnaDiaVm, EtiquetaHoraVm
└── Views/
    ├── MainWindow.axaml(.cs)   # UI principal; el calendari es renderitza al codi darrere
    └── DiaConverter.cs         # 1..5 -> Dilluns..Divendres
```

## Model de dades (esquema actual, migració v3)

- `Configuracio`: curs, dates, format informe, tema (Sistema/Clar/Fosc), perfil del professor.
- `Assignatura`: Nom, Curs, Color.
- `VersioHorari`: permet versionar l'horari sense perdre notes.
- `ClasseHorari`: VersioHorariId, DiaSetmana (1-5), **HoraInici/HoraFi (TEXT HH:mm)**,
  AssignaturaId, Grup, Aula. (Ja NO existeix `FranjaHorari`.)
- `NotaSetmanal`: ClasseHorariId, DataDilluns, Text.
- `Festiu`: Data, Nom, Tipus ('Festiu' | 'Vacances' | 'LliureDisposicio').

## Flux funcional

1. **Assistent inicial** (un pas): el professor només defineix les **assignatures**.
2. **Horari** (vista tipus Google Calendar): eix de temps vertical; **clic en un
   buit** obre un pop-up per crear una classe (dia + hora inici/fi + assignatura +
   grup + aula); **clic a la icona ✎** d'un bloc l'edita/elimina; **clic al bloc**
   obre les notes (text lliure per setmana). Columna d'avui ressaltada; festius i
   dies de lliure disposició atenuats.
3. **Informes**: per assignatura, format elegible (PDF/XLSX/CSV) i període.
4. **Configuració**: tema clar/fosc, perfil, assignatures, dies de lliure
   disposició, còpies de seguretat, actualitzacions i esborrat de dades.

## Convencions

- **UI i codi en català** (comentaris, noms de mètodes de domini, textos).
- **Colors**: SEMPRE amb `DynamicResource` als tokens definits a `App.axaml`
  (ThemeDictionaries Light/Dark). MAI colors literals a les vistes.
- El **calendari** es construeix al codi darrere (`MainWindow.axaml.cs`,
  `RenderCalendari`) amb Grid.Row/RowSpan/Column, no amb un control natiu.
- **Evitar `DatePicker`/`TimePicker` d'Avalonia 11.2**: tenen bugs de crash en
  render a Windows. Fer servir TextBox amb format (`HH:mm`, `dd/MM/yyyy`) i validar.
- Tota operació destructiva (esborrar/restaurar) fa **còpia de seguretat prèvia**.

## Diagnòstic

- Si l'app es tanca sola, mira el **log**: `error-arrencada.log`. Ubicacions
  (la primera escrivible): al costat de l'`.exe` a `dades/`, o
  `%LOCALAPPDATA%\ProgramacioDocent`, o el perfil de l'usuari, o `%TEMP%\ProgramacioDocent`.
- El log escriu un marcador d'inici i les fases d'arrencada. Els handlers globals
  (AppDomain, TaskScheduler i **Dispatcher.UIThread**) registren qualsevol excepció,
  inclosos els errors de render en canviar de pestanya.
- **Crash sense rastre al log = fallada NATIVA (GPU o stack overflow).** Si l'app
  es tanca de cop i al log NO hi ha cap ERROR, no és una excepció de .NET (els
  handlers gestionats no la poden capturar). Dues causes possibles:
  - **Stack overflow (exit code -1073741571 / 0xC00000FD)**: recursió de layout.
    **Causa coneguda en aquest projecte**: `<Run Text="{Binding ...}"/>` inline
    dins d'un `TextBlock` amb **compiled bindings** (el projecte té
    `AvaloniaUseCompiledBindingsByDefault=true`). Materialitzar aquests `Run` en un
    `ItemsControl` amb dades feia caure la pestanya Configuració sense deixar log.
    **Solució aplicada**: no usar `<Run>` amb binding; exposar una propietat de
    text al model (p. ex. `Assignatura.NomICurs`, `ClasseHorari.ResumConfig`,
    `Festiu.ResumLliure`) i enllaçar-la amb `TextBlock Text="{Binding ...}"`.
  - **Fallada de GPU**: drivers antics, VM, entorns capats. Solució: mode segur
    (render per programari), descrit a sota.
- **Mode segur (render per programari, CPU):** força el render sense GPU, sense
  recompilar, de dues maneres:
  1. Variable d'entorn: `PROGRAMACIODOCENT_SOFTWARE_RENDER=1`.
  2. Crear un fitxer buit `render-software.txt` al costat de `ProgramacioDocent.exe`.
  `Program.cs` (`BuildAvaloniaApp` → `VolRenderProgramari`) ho detecta i aplica
  `Win32PlatformOptions { RenderingMode = [Software] }`. El log indica quin mode
  s'ha usat: "Render per GPU" o "Render per PROGRAMARI (mode segur, sense GPU)".

## Estat i tasques pendents

- **Fet**: fonaments de dades (WAL, migracions, backups), tema clar/fosc amb
  tokens, vista de calendari, actualitzador assistit, esborrat de dades,
  redisseny del model (classes amb hora pròpia), assistent només d'assignatures,
  dies de lliure disposició, logging robust.
- **Pendent (proposat)**:
  - **Notes amb posició configurable**: que el professor triï a Configuració on
    apareix l'editor de notes (panell inferior, panell dret, o diàleg modal com ara).
  - Millores visuals menors: línia de l'hora actual, zebra, hover als blocs.
  - (Roadmap complet al README, secció "Full de ruta".)

## Nota per a l'agent

- Compila i **verifica** (build + prova la lògica) després de cada canvi.
- L'usuari gestiona el **git** (commits, tags, releases) personalment: no facis
  push ni creïs tags si no t'ho demana explícitament.

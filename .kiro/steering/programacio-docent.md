# Programació Docent — Steering

Projecte: app d'escriptori .NET 8 + Avalonia (MVVM), SQLite local, per a
professorat de Catalunya. Tota la UI i el codi **en català**. Windows portable.

Consulta `AGENTS.md` a l'arrel del repositori per al context complet (arquitectura,
model de dades, flux funcional, com compilar i diagnosticar).

## Regles clau

- **Idioma**: interfície i comentaris en català.
- **Colors**: sempre `DynamicResource` als tokens de tema (`App.axaml`), mai literals.
- **Evita `DatePicker`/`TimePicker`** d'Avalonia 11.2 (bugs de crash en render a
  Windows). Fes servir TextBox amb format i validació.
- **Git el gestiona l'usuari**: no facis commits, push ni tags sense petició explícita.
- **Verifica** sempre amb `dotnet build -c Release` després de cada canvi.
- Operacions destructives → còpia de seguretat prèvia (BackupService).

## Compilar / executar

```powershell
dotnet run --project src/ProgramacioDocent.csproj
dotnet build src/ProgramacioDocent.csproj -c Release
```

## Diagnòstic de tancaments

Mira `error-arrencada.log` (al costat de l'exe a `dades/`, o `%LOCALAPPDATA%\ProgramacioDocent`).

## Pendent

Notes amb posició configurable (panell inferior/dret/modal); millores visuals menors.

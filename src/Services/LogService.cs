using System;
using System.Collections.Generic;
using System.IO;

namespace ProgramacioDocent.Services;

// Servei de registre (log) robust i independent, pensat per diagnosticar
// tancaments inesperats en equips capats.
//
// Punts clau del disseny:
//  - NO depèn de PathService: prova diverses ubicacions candidates i escriu a la
//    primera on pugui, de manera que un problema de permisos en una carpeta no
//    impedeixi registrar l'error.
//  - Registra un marcador d'"inici" molt aviat: si al log no hi ha ni tan sols
//    aquesta línia, vol dir que el procés mor abans d'executar el nostre codi
//    (p. ex. el runtime .NET o un antivirus/AppLocker), no la nostra lògica.
//  - Mai llança excepcions.
public static class LogService
{
    private const string NomFitxer = "error-arrencada.log";
    private static string? _rutaLog;
    private static readonly object _pany = new();

    // Ubicacions candidates on intentar escriure el log, en ordre de preferència.
    private static IEnumerable<string> Candidates()
    {
        // 1) Al costat de l'executable (mode portable).
        string? baseDir = null;
        try { baseDir = AppContext.BaseDirectory; } catch { }
        if (!string.IsNullOrEmpty(baseDir))
        {
            yield return Path.Combine(baseDir!, "dades");
            yield return baseDir!;
        }

        // 2) %LOCALAPPDATA%\ProgramacioDocent
        string? local = null;
        try { local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } catch { }
        if (!string.IsNullOrEmpty(local))
            yield return Path.Combine(local!, "ProgramacioDocent");

        // 3) Carpeta de perfil de l'usuari.
        string? perfil = null;
        try { perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } catch { }
        if (!string.IsNullOrEmpty(perfil))
            yield return Path.Combine(perfil!, "ProgramacioDocent");

        // 4) Carpeta temporal del sistema (últim recurs).
        string? temp = null;
        try { temp = Path.GetTempPath(); } catch { }
        if (!string.IsNullOrEmpty(temp))
            yield return Path.Combine(temp!, "ProgramacioDocent");
    }

    // Determina (una sola vegada) la primera ubicació on es pot escriure.
    private static string? ResolRutaLog()
    {
        if (_rutaLog != null) return _rutaLog;

        foreach (var carpeta in Candidates())
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                var prova = Path.Combine(carpeta, ".prova_log");
                File.WriteAllText(prova, "ok");
                File.Delete(prova);
                _rutaLog = Path.Combine(carpeta, NomFitxer);
                return _rutaLog;
            }
            catch
            {
                // Prova la següent ubicació.
            }
        }
        return null; // Cap ubicació escrivible.
    }

    // Ruta on s'escriu el log (o null si no s'ha pogut determinar).
    public static string? RutaActual => ResolRutaLog();

    // Escriu una línia informativa al log.
    public static void Info(string missatge) => Escriu("INFO", missatge, null);

    // Escriu un error (amb l'excepció completa) al log.
    public static void Error(string origen, Exception? ex) => Escriu("ERROR", origen, ex);

    private static void Escriu(string nivell, string text, Exception? ex)
    {
        try
        {
            var ruta = ResolRutaLog();
            if (ruta == null) return;

            var linia = ex == null
                ? $"[{DateTime.Now:O}] {nivell} {text}\n"
                : $"[{DateTime.Now:O}] {nivell} ({text}) {ex}\n\n";

            lock (_pany)
            {
                File.AppendAllText(ruta, linia);
            }
        }
        catch
        {
            // El logging mai ha de fer caure l'aplicació.
        }
    }
}

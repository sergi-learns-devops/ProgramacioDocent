using System;
using System.IO;

namespace ProgramacioDocent.Services;

// Determina on desar les dades. En equips capats, la carpeta de l'executable
// pot ser de només lectura (USB protegit, unitat de xarxa, Program Files).
// En aquest cas es fa servir %LOCALAPPDATA%\ProgramacioDocent.
public static class PathService
{
    private const string NomCarpeta = "ProgramacioDocent";
    private const string NomBaseDades = "dades.db";
    private static string? _dataDir;

    public static string GetDataDirectory()
    {
        if (_dataDir != null)
            return _dataDir;

        // 1) Intenta desar les dades al costat de l'executable (mode portable).
        var dirExe = AppContext.BaseDirectory;
        var carpetaPortable = Path.Combine(dirExe, "dades");

        if (EsEscrivible(carpetaPortable))
        {
            _dataDir = carpetaPortable;
            return _dataDir;
        }

        // 2) Fallback: %LOCALAPPDATA%\ProgramacioDocent
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var carpetaLocal = Path.Combine(local, NomCarpeta);
        Directory.CreateDirectory(carpetaLocal);
        _dataDir = carpetaLocal;
        return _dataDir;
    }

    public static string GetDatabasePath()
        => Path.Combine(GetDataDirectory(), NomBaseDades);

    // Comprova si es pot escriure a la carpeta indicada (la crea si cal).
    private static bool EsEscrivible(string carpeta)
    {
        try
        {
            Directory.CreateDirectory(carpeta);
            var provaFitxer = Path.Combine(carpeta, ".prova_escriptura");
            File.WriteAllText(provaFitxer, "ok");
            File.Delete(provaFitxer);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

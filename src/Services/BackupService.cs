using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using ProgramacioDocent.Data;

namespace ProgramacioDocent.Services;

// Gestiona les còpies de seguretat de la base de dades.
//
// Fem servir 'VACUUM INTO', que crea una còpia neta i consolidada de la base de
// dades en un únic fitxer (segur amb WAL, sense necessitat de tancar l'app).
// Es manté una rotació de les últimes N còpies.
public class BackupService
{
    private readonly Database _db;
    private const int MaxCopies = 10;

    public BackupService(Database db) => _db = db;

    public string CarpetaBackups
    {
        get
        {
            var carpeta = Path.Combine(PathService.GetDataDirectory(), "backups");
            Directory.CreateDirectory(carpeta);
            return carpeta;
        }
    }

    // Crea una còpia de seguretat amb marca de temps. Retorna el camí del fitxer.
    public string CreaCopia(string sufix = "")
    {
        var marca = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var nom = string.IsNullOrWhiteSpace(sufix)
            ? $"backup_{marca}.db"
            : $"backup_{marca}_{sufix}.db";
        var desti = Path.Combine(CarpetaBackups, nom);

        using (var conn = _db.ObreConnexio())
        using (var cmd = conn.CreateCommand())
        {
            // El paràmetre no es pot passar amb placeholder en VACUUM INTO;
            // el camí prové de dades internes (marca de temps), no d'entrada
            // d'usuari. Escapem les cometes simples per seguretat.
            var rutaSegura = desti.Replace("'", "''");
            cmd.CommandText = $"VACUUM INTO '{rutaSegura}';";
            cmd.ExecuteNonQuery();
        }

        RotaCopies();
        return desti;
    }

    // Manté només les últimes MaxCopies còpies (esborra les més antigues).
    private void RotaCopies()
    {
        var fitxers = new DirectoryInfo(CarpetaBackups)
            .GetFiles("backup_*.db")
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        foreach (var vell in fitxers.Skip(MaxCopies))
        {
            try { vell.Delete(); } catch { /* ignora errors d'esborrat */ }
        }
    }

    public List<FileInfo> LlistaCopies()
        => new DirectoryInfo(CarpetaBackups)
            .GetFiles("backup_*.db")
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

    // Restaura una còpia de seguretat. Abans, valida la integritat del fitxer i
    // fa una còpia de seguretat de l'estat actual (per si de cas).
    // Retorna (èxit, missatge).
    public (bool ok, string missatge) Restaura(string rutaCopia)
    {
        if (string.IsNullOrWhiteSpace(rutaCopia))
            return (false, "Ruta de còpia no vàlida.");

        // Defensa en profunditat: canonicalitza la ruta i comprova que la còpia
        // es troba dins de la carpeta de còpies permesa (evita path traversal si
        // en el futur la ruta prové d'una entrada externa).
        string rutaCanonica;
        try { rutaCanonica = Path.GetFullPath(rutaCopia); }
        catch { return (false, "Ruta de còpia no vàlida."); }

        var carpetaPermesa = Path.GetFullPath(CarpetaBackups);
        if (!rutaCanonica.StartsWith(carpetaPermesa, StringComparison.OrdinalIgnoreCase))
            return (false, "La còpia ha d'estar dins de la carpeta de còpies de seguretat.");

        if (!File.Exists(rutaCanonica))
            return (false, "El fitxer de còpia no existeix.");
        rutaCopia = rutaCanonica;

        // 1) Validar la integritat de la còpia abans de restaurar-la.
        if (!ValidaIntegritat(rutaCopia, out var errorIntegritat))
            return (false, "La còpia no és vàlida: " + errorIntegritat);

        // 2) Còpia de seguretat de l'estat actual abans de sobreescriure.
        try
        {
            // Consolida el WAL al fitxer principal abans de la còpia preventiva.
            using (var conn = _db.ObreConnexio())
            using (var cp = conn.CreateCommand())
            {
                cp.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                cp.ExecuteNonQuery();
            }
            CreaCopia("abans_de_restaurar");
        }
        catch
        {
            // Si no es pot fer la còpia prèvia, continuem igualment amb la restauració.
        }

        // 3) Sobreescriure el fitxer de base de dades actiu de manera atòmica.
        //    Cal alliberar qualsevol connexió/pool obert de SQLite.
        try
        {
            SqliteConnection.ClearAllPools();
            var destinacio = _db.RutaFitxer;

            // En mode WAL poden existir fitxers -wal i -shm; els eliminem perquè
            // no barregin dades de l'estat anterior amb la còpia restaurada.
            EsborraSiExisteix(destinacio + "-wal");
            EsborraSiExisteix(destinacio + "-shm");

            // Restauració atòmica: copiem a un fitxer temporal i el movem a sobre.
            // Així, si la còpia falla a mig camí, la BD activa no queda corrompuda.
            var temporal = destinacio + ".restaurant.tmp";
            EsborraSiExisteix(temporal);
            File.Copy(rutaCopia, temporal, overwrite: true);
            File.Move(temporal, destinacio, overwrite: true);

            return (true, "Còpia restaurada correctament.");
        }
        catch (Exception ex)
        {
            return (false, "Error en restaurar: " + ex.Message);
        }
    }

    // Valida la integritat d'un fitxer de base de dades SQLite.
    private static bool ValidaIntegritat(string rutaFitxer, out string error)
    {
        error = string.Empty;
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = rutaFitxer,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var conn = new SqliteConnection(cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var resultat = cmd.ExecuteScalar() as string;
            if (!string.Equals(resultat, "ok", StringComparison.OrdinalIgnoreCase))
            {
                error = resultat ?? "resultat desconegut";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void EsborraSiExisteix(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* ignora */ }
    }
}

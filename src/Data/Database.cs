using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using ProgramacioDocent.Services;

namespace ProgramacioDocent.Data;

// Gestiona la connexió i l'esquema de la base de dades SQLite local.
public class Database
{
    private readonly string _connectionString;

    public Database()
    {
        var ruta = PathService.GetDatabasePath();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = ruta,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // App d'un sol usuari: desactivem el pool de connexions perquè les
            // operacions a nivell de fitxer (com restaurar una còpia) tinguin
            // efecte immediat sense connexions residuals que mantinguin el fitxer.
            Pooling = false
        }.ToString();
    }

    public string RutaFitxer => PathService.GetDatabasePath();

    public SqliteConnection ObreConnexio()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            // foreign_keys s'ha d'activar a cada connexió.
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    // Inicialitza la base de dades: activa WAL, aplica migracions i precarrega dades.
    public void Inicialitza()
    {
        using var conn = ObreConnexio();

        // WAL (Write-Ahead Logging): millora la robustesa davant tancaments
        // bruscos i el rendiment de lectura/escriptura simultànies. És un ajust
        // persistent a la base de dades (n'hi ha prou d'aplicar-lo un cop).
        using (var wal = conn.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode = WAL;";
            wal.ExecuteNonQuery();
        }

        // Aplica les migracions d'esquema pendents (crea/actualitza taules).
        Migracions.Aplica(conn);

        // Dades inicials.
        AsseguraConfiguracio(conn);
        PrecarregaCalendari(conn);
    }

    private void AsseguraConfiguracio(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Configuracio;";
        var n = Convert.ToInt64(check.ExecuteScalar());
        if (n > 0) return;

        using var ins = conn.CreateCommand();
        ins.CommandText = @"
INSERT INTO Configuracio (CursEscolar, DataIniciCurs, DataFiCurs, FormatInformePreferit, AssistentCompletat, VersioHorariActivaId)
VALUES ('2026-2027', '2026-09-08', '2027-06-21', 'PDF', 0, 0);";
        ins.ExecuteNonQuery();
    }

    // Precarrega els festius del curs 2026-2027 (dades oficials verificades).
    private void PrecarregaCalendari(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Festiu;";
        var n = Convert.ToInt64(check.ExecuteScalar());
        if (n > 0) return;

        var festius = CalendariCatalunya.Curs2026_2027();

        using var tx = conn.BeginTransaction();
        foreach (var (data, nom, tipus) in festius)
        {
            using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText =
                "INSERT OR IGNORE INTO Festiu (Data, Nom, Tipus) VALUES ($d, $n, $t);";
            ins.Parameters.AddWithValue("$d", data.ToString("yyyy-MM-dd"));
            ins.Parameters.AddWithValue("$n", nom);
            ins.Parameters.AddWithValue("$t", tipus);
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // Comprova la integritat de la base de dades. Retorna true si tot és correcte.
    public bool ComprovaIntegritat()
    {
        using var conn = ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var resultat = cmd.ExecuteScalar() as string;
        return string.Equals(resultat, "ok", StringComparison.OrdinalIgnoreCase);
    }

    // Restableix totes les dades de l'usuari: esborra notes, classes, franjes,
    // assignatures i versions, i torna a deixar l'assistent inicial pendent.
    // CONSERVA el calendari de festius i la configuració (tema, perfil).
    // Fer sempre una còpia de seguretat abans.
    public void RestableixDades()
    {
        using var conn = ObreConnexio();
        using var tx = conn.BeginTransaction();
        foreach (var taula in new[] { "NotaSetmanal", "ClasseHorari", "FranjaHorari", "Assignatura", "VersioHorari" })
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"DELETE FROM {taula};";
            cmd.ExecuteNonQuery();
        }
        using (var cfg = conn.CreateCommand())
        {
            cfg.Transaction = tx;
            cfg.CommandText = "UPDATE Configuracio SET AssistentCompletat = 0, VersioHorariActivaId = 0;";
            cfg.ExecuteNonQuery();
        }
        tx.Commit();
    }
}

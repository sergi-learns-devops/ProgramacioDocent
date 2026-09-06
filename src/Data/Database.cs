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
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection ObreConnexio()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    // Crea les taules si no existeixen i precarrega el calendari.
    public void Inicialitza()
    {
        using var conn = ObreConnexio();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Configuracio (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CursEscolar TEXT NOT NULL,
    DataIniciCurs TEXT NOT NULL,
    DataFiCurs TEXT NOT NULL,
    FormatInformePreferit TEXT NOT NULL DEFAULT 'PDF',
    AssistentCompletat INTEGER NOT NULL DEFAULT 0,
    VersioHorariActivaId INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Assignatura (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Nom TEXT NOT NULL,
    Curs TEXT NOT NULL DEFAULT '',
    Color TEXT NOT NULL DEFAULT '#4F86C6'
);

CREATE TABLE IF NOT EXISTS FranjaHorari (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Ordre INTEGER NOT NULL,
    HoraInici TEXT NOT NULL,
    HoraFi TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS VersioHorari (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Descripcio TEXT NOT NULL DEFAULT '',
    DataInici TEXT NOT NULL,
    DataFi TEXT,
    Activa INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS ClasseHorari (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    VersioHorariId INTEGER NOT NULL,
    DiaSetmana INTEGER NOT NULL,
    FranjaId INTEGER NOT NULL,
    AssignaturaId INTEGER NOT NULL,
    Grup TEXT NOT NULL DEFAULT '',
    Aula TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (VersioHorariId) REFERENCES VersioHorari(Id) ON DELETE CASCADE,
    FOREIGN KEY (FranjaId) REFERENCES FranjaHorari(Id) ON DELETE CASCADE,
    FOREIGN KEY (AssignaturaId) REFERENCES Assignatura(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS NotaSetmanal (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ClasseHorariId INTEGER NOT NULL,
    DataDilluns TEXT NOT NULL,
    Text TEXT NOT NULL DEFAULT '',
    DataCreacio TEXT NOT NULL,
    DataModificacio TEXT NOT NULL,
    FOREIGN KEY (ClasseHorariId) REFERENCES ClasseHorari(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Festiu (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Data TEXT NOT NULL,
    Nom TEXT NOT NULL,
    Tipus TEXT NOT NULL DEFAULT 'Festiu'
);

CREATE INDEX IF NOT EXISTS IX_NotaSetmanal_Classe_Data
    ON NotaSetmanal(ClasseHorariId, DataDilluns);
CREATE INDEX IF NOT EXISTS IX_ClasseHorari_Versio
    ON ClasseHorari(VersioHorariId);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Festiu_Data ON Festiu(Data);
";
            cmd.ExecuteNonQuery();
        }

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
}

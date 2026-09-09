using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace ProgramacioDocent.Data;

// Sistema de migracions d'esquema versionades.
//
// Cada migració té un número de versió incremental i un bloc SQL. En obrir la
// base de dades, s'apliquen en ordre totes les migracions amb versió superior a
// la 'PRAGMA user_version' actual, cadascuna dins d'una transacció (rollback
// automàtic si falla). Això permet afegir taules o columnes en versions futures
// SENSE perdre les dades dels usuaris que actualitzin l'aplicació.
public static class Migracions
{
    // Llista ordenada de migracions. Per afegir-ne de noves, incrementa el número
    // i afegeix-la al final. MAI modifiquis una migració ja publicada.
    public static readonly IReadOnlyList<(int Versio, string Nom, string Sql)> Totes = new List<(int, string, string)>
    {
        (1, "Esquema inicial", @"
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
"),

        (2, "Perfil del professor i preferència de tema", @"
-- Preferència de tema visual: 'Sistema' | 'Clar' | 'Fosc'
ALTER TABLE Configuracio ADD COLUMN Tema TEXT NOT NULL DEFAULT 'Sistema';

-- Dades bàsiques del perfil del professor (opcionals).
ALTER TABLE Configuracio ADD COLUMN ProfNom TEXT NOT NULL DEFAULT '';
ALTER TABLE Configuracio ADD COLUMN ProfCognoms TEXT NOT NULL DEFAULT '';
ALTER TABLE Configuracio ADD COLUMN ProfCentre TEXT NOT NULL DEFAULT '';
ALTER TABLE Configuracio ADD COLUMN ProfDepartament TEXT NOT NULL DEFAULT '';
ALTER TABLE Configuracio ADD COLUMN ProfEmail TEXT NOT NULL DEFAULT '';
"),

        (3, "Les classes tenen hora pròpia (s'elimina el concepte de franja)", @"
-- Nou model: cada classe porta la seva HoraInici i HoraFi directament,
-- en lloc de referenciar una franja predefinida.
-- Es recrea ClasseHorari amb el nou esquema i s'eliminen les dades antigues
-- d'horari (les notes també, per la clau forana). El calendari de festius i la
-- configuració es conserven.
DROP TABLE IF EXISTS ClasseHorari;
DROP TABLE IF EXISTS FranjaHorari;

CREATE TABLE ClasseHorari (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    VersioHorariId INTEGER NOT NULL,
    DiaSetmana INTEGER NOT NULL,          -- 1 = dilluns ... 5 = divendres
    HoraInici TEXT NOT NULL,              -- 'HH:mm'
    HoraFi TEXT NOT NULL,                 -- 'HH:mm'
    AssignaturaId INTEGER NOT NULL,
    Grup TEXT NOT NULL DEFAULT '',
    Aula TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (VersioHorariId) REFERENCES VersioHorari(Id) ON DELETE CASCADE,
    FOREIGN KEY (AssignaturaId) REFERENCES Assignatura(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_ClasseHorari_Versio ON ClasseHorari(VersioHorariId);
"),
    };

    // Aplica totes les migracions pendents. Retorna la versió final assolida.
    public static int Aplica(SqliteConnection conn)
    {
        int versioActual = LlegeixUserVersion(conn);
        int versioFinal = versioActual;

        foreach (var (versio, _, sql) in Totes)
        {
            if (versio <= versioActual)
                continue;

            using var tx = conn.BeginTransaction();
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }

                // PRAGMA user_version no admet paràmetres; el número prové d'una
                // constant interna (no d'entrada d'usuari), així que és segur.
                using (var pv = conn.CreateCommand())
                {
                    pv.Transaction = tx;
                    pv.CommandText = $"PRAGMA user_version = {versio};";
                    pv.ExecuteNonQuery();
                }

                tx.Commit();
                versioFinal = versio;
            }
            catch
            {
                tx.Rollback();
                throw; // Deixa que l'arrencada registri l'error; la BD queda intacta.
            }
        }

        return versioFinal;
    }

    private static int LlegeixUserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using ProgramacioDocent.Data;
using ProgramacioDocent.Models;

namespace ProgramacioDocent.Services;

// Gestiona l'horari: assignatures, franjes, versions i classes de la graella.
public class HorariService
{
    private readonly Database _db;

    public HorariService(Database db) => _db = db;

    // ---------- Assignatures ----------

    public List<Assignatura> ObteAssignatures()
    {
        var llista = new List<Assignatura>();
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nom, Curs, Color FROM Assignatura ORDER BY Nom;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            llista.Add(new Assignatura
            {
                Id = r.GetInt32(0),
                Nom = r.GetString(1),
                Curs = r.GetString(2),
                Color = r.GetString(3)
            });
        }
        return llista;
    }

    public int AfegeixAssignatura(Assignatura a)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Assignatura (Nom, Curs, Color) VALUES ($n, $c, $col); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", a.Nom);
        cmd.Parameters.AddWithValue("$c", a.Curs);
        cmd.Parameters.AddWithValue("$col", a.Color);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ---------- Franjes ----------

    // ---------- Versions d'horari ----------

    public int CreaVersio(string descripcio, DateTime dataInici)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO VersioHorari (Descripcio, DataInici, Activa) VALUES ($d, $i, 0); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$d", descripcio);
        cmd.Parameters.AddWithValue("$i", dataInici.ToString("yyyy-MM-dd"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // Activa una versió d'horari i tanca (data fi) l'anterior activa.
    public void ActivaVersio(int versioId, DateTime dataInici)
    {
        using var conn = _db.ObreConnexio();
        using var tx = conn.BeginTransaction();

        using (var tanca = conn.CreateCommand())
        {
            tanca.Transaction = tx;
            tanca.CommandText =
                "UPDATE VersioHorari SET Activa = 0, DataFi = $fi WHERE Activa = 1 AND Id <> $id;";
            tanca.Parameters.AddWithValue("$fi", dataInici.AddDays(-1).ToString("yyyy-MM-dd"));
            tanca.Parameters.AddWithValue("$id", versioId);
            tanca.ExecuteNonQuery();
        }

        using (var activa = conn.CreateCommand())
        {
            activa.Transaction = tx;
            activa.CommandText = "UPDATE VersioHorari SET Activa = 1, DataFi = NULL WHERE Id = $id;";
            activa.Parameters.AddWithValue("$id", versioId);
            activa.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public List<VersioHorari> ObteVersions()
    {
        var llista = new List<VersioHorari>();
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Descripcio, DataInici, DataFi, Activa FROM VersioHorari ORDER BY DataInici;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            llista.Add(new VersioHorari
            {
                Id = r.GetInt32(0),
                Descripcio = r.GetString(1),
                DataInici = DateTime.Parse(r.GetString(2)),
                DataFi = r.IsDBNull(3) ? null : DateTime.Parse(r.GetString(3)),
                Activa = r.GetInt32(4) == 1
            });
        }
        return llista;
    }

    // Retorna la versió d'horari vigent per a una data concreta.
    public int VersioVigent(DateTime data)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Id FROM VersioHorari
WHERE DataInici <= $d AND (DataFi IS NULL OR DataFi >= $d)
ORDER BY DataInici DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$d", data.ToString("yyyy-MM-dd"));
        var res = cmd.ExecuteScalar();
        if (res != null && res != DBNull.Value)
            return Convert.ToInt32(res);

        // Si cap versió cobreix la data, torna l'activa.
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT Id FROM VersioHorari WHERE Activa = 1 LIMIT 1;";
        var res2 = cmd2.ExecuteScalar();
        return res2 != null && res2 != DBNull.Value ? Convert.ToInt32(res2) : 0;
    }

    // ---------- Classes de la graella ----------

    public int AfegeixClasse(ClasseHorari c)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO ClasseHorari (VersioHorariId, DiaSetmana, HoraInici, HoraFi, AssignaturaId, Grup, Aula)
VALUES ($v, $d, $hi, $hf, $a, $g, $au); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$v", c.VersioHorariId);
        cmd.Parameters.AddWithValue("$d", c.DiaSetmana);
        cmd.Parameters.AddWithValue("$hi", c.HoraInici.ToString("HH:mm"));
        cmd.Parameters.AddWithValue("$hf", c.HoraFi.ToString("HH:mm"));
        cmd.Parameters.AddWithValue("$a", c.AssignaturaId);
        cmd.Parameters.AddWithValue("$g", c.Grup);
        cmd.Parameters.AddWithValue("$au", c.Aula);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // Actualitza una classe existent (hora, assignatura, grup, aula).
    public void ActualitzaClasse(ClasseHorari c)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE ClasseHorari SET DiaSetmana=$d, HoraInici=$hi, HoraFi=$hf,
    AssignaturaId=$a, Grup=$g, Aula=$au WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$d", c.DiaSetmana);
        cmd.Parameters.AddWithValue("$hi", c.HoraInici.ToString("HH:mm"));
        cmd.Parameters.AddWithValue("$hf", c.HoraFi.ToString("HH:mm"));
        cmd.Parameters.AddWithValue("$a", c.AssignaturaId);
        cmd.Parameters.AddWithValue("$g", c.Grup);
        cmd.Parameters.AddWithValue("$au", c.Aula);
        cmd.Parameters.AddWithValue("$id", c.Id);
        cmd.ExecuteNonQuery();
    }

    public void EliminaClasse(int id)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ClasseHorari WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // Buida completament l'horari: elimina classes, assignatures i versions.
    // ATENCIÓ: en esborrar les classes, les notes associades també s'eliminen
    // per la clau forana ON DELETE CASCADE. Fer sempre una còpia de seguretat abans.
    public void BuidaHorari()
    {
        using var conn = _db.ObreConnexio();
        using var tx = conn.BeginTransaction();
        foreach (var taula in new[] { "ClasseHorari", "Assignatura", "VersioHorari" })
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"DELETE FROM {taula};";
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // Retorna totes les classes d'una versió d'horari, amb l'assignatura.
    public List<ClasseHorari> ObteClasses(int versioId)
    {
        var llista = new List<ClasseHorari>();
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT c.Id, c.VersioHorariId, c.DiaSetmana, c.HoraInici, c.HoraFi, c.AssignaturaId, c.Grup, c.Aula,
       a.Nom, a.Curs, a.Color
FROM ClasseHorari c
JOIN Assignatura a ON a.Id = c.AssignaturaId
WHERE c.VersioHorariId = $v
ORDER BY c.DiaSetmana, c.HoraInici;";
        cmd.Parameters.AddWithValue("$v", versioId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            llista.Add(new ClasseHorari
            {
                Id = r.GetInt32(0),
                VersioHorariId = r.GetInt32(1),
                DiaSetmana = r.GetInt32(2),
                HoraInici = TimeOnly.Parse(r.GetString(3)),
                HoraFi = TimeOnly.Parse(r.GetString(4)),
                AssignaturaId = r.GetInt32(5),
                Grup = r.GetString(6),
                Aula = r.GetString(7),
                Assignatura = new Assignatura
                {
                    Id = r.GetInt32(5),
                    Nom = r.GetString(8),
                    Curs = r.GetString(9),
                    Color = r.GetString(10)
                }
            });
        }
        return llista;
    }
}

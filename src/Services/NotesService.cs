using System;
using System.Collections.Generic;
using ProgramacioDocent.Data;
using ProgramacioDocent.Models;

namespace ProgramacioDocent.Services;

// Gestiona les notes de text lliure per classe i setmana.
public class NotesService
{
    private readonly Database _db;

    public NotesService(Database db) => _db = db;

    // Recupera la nota d'una classe per a una setmana concreta (o null).
    public NotaSetmanal? ObteNota(int classeHorariId, DateTime dataDilluns)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Id, ClasseHorariId, DataDilluns, Text, DataCreacio, DataModificacio
FROM NotaSetmanal WHERE ClasseHorariId = $c AND DataDilluns = $d LIMIT 1;";
        cmd.Parameters.AddWithValue("$c", classeHorariId);
        cmd.Parameters.AddWithValue("$d", dataDilluns.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new NotaSetmanal
        {
            Id = r.GetInt32(0),
            ClasseHorariId = r.GetInt32(1),
            DataDilluns = DateTime.Parse(r.GetString(2)),
            Text = r.GetString(3),
            DataCreacio = DateTime.Parse(r.GetString(4)),
            DataModificacio = DateTime.Parse(r.GetString(5))
        };
    }

    // Desa (crea o actualitza) la nota d'una classe per a una setmana.
    public void DesaNota(int classeHorariId, DateTime dataDilluns, string text)
    {
        var ara = DateTime.Now;
        var existent = ObteNota(classeHorariId, dataDilluns);

        using var conn = _db.ObreConnexio();

        if (existent == null)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO NotaSetmanal (ClasseHorariId, DataDilluns, Text, DataCreacio, DataModificacio)
VALUES ($c, $d, $t, $cr, $mo);";
            cmd.Parameters.AddWithValue("$c", classeHorariId);
            cmd.Parameters.AddWithValue("$d", dataDilluns.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$t", text ?? string.Empty);
            cmd.Parameters.AddWithValue("$cr", ara.ToString("O"));
            cmd.Parameters.AddWithValue("$mo", ara.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE NotaSetmanal SET Text = $t, DataModificacio = $mo WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$t", text ?? string.Empty);
            cmd.Parameters.AddWithValue("$mo", ara.ToString("O"));
            cmd.Parameters.AddWithValue("$id", existent.Id);
            cmd.ExecuteNonQuery();
        }
    }

    // Esborra TOTES les notes, conservant l'horari (franjes, assignatures, classes).
    // Fer sempre una còpia de seguretat abans.
    public void EsborraTotesLesNotes()
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM NotaSetmanal;";
        cmd.ExecuteNonQuery();
    }

    // Fila d'informe: nota amb dades d'assignatura i data, per als reports.
    public record FilaInforme(
        string Assignatura, string Curs, DateTime DataDilluns,
        int DiaSetmana, string Franja, string Text);

    // Recupera totes les notes dins d'un interval de dates, per a informes.
    public List<FilaInforme> ObteNotesPerInterval(DateTime desde, DateTime fins)
    {
        var llista = new List<FilaInforme>();
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT a.Nom, a.Curs, n.DataDilluns, c.DiaSetmana,
       f.HoraInici || ' - ' || f.HoraFi AS Franja, n.Text
FROM NotaSetmanal n
JOIN ClasseHorari c ON c.Id = n.ClasseHorariId
JOIN Assignatura a ON a.Id = c.AssignaturaId
JOIN FranjaHorari f ON f.Id = c.FranjaId
WHERE n.DataDilluns >= $desde AND n.DataDilluns <= $fins
  AND TRIM(n.Text) <> ''
ORDER BY a.Nom, n.DataDilluns, c.DiaSetmana, f.Ordre;";
        cmd.Parameters.AddWithValue("$desde", desde.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$fins", fins.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            llista.Add(new FilaInforme(
                r.GetString(0),
                r.GetString(1),
                DateTime.Parse(r.GetString(2)),
                r.GetInt32(3),
                r.GetString(4),
                r.GetString(5)));
        }
        return llista;
    }
}

using System;
using ProgramacioDocent.Data;
using ProgramacioDocent.Models;

namespace ProgramacioDocent.Services;

// Llegeix i desa la configuració general (un únic registre).
public class ConfigService
{
    private readonly Database _db;

    public ConfigService(Database db) => _db = db;

    public Configuracio Carrega()
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT Id, CursEscolar, DataIniciCurs, DataFiCurs, FormatInformePreferit,
       AssistentCompletat, VersioHorariActivaId
FROM Configuracio LIMIT 1;";
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return new Configuracio();

        return new Configuracio
        {
            Id = r.GetInt32(0),
            CursEscolar = r.GetString(1),
            DataIniciCurs = DateTime.Parse(r.GetString(2)),
            DataFiCurs = DateTime.Parse(r.GetString(3)),
            FormatInformePreferit = r.GetString(4),
            AssistentCompletat = r.GetInt32(5) == 1,
            VersioHorariActivaId = r.GetInt32(6)
        };
    }

    public void Desa(Configuracio c)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE Configuracio SET
    CursEscolar = $curs,
    DataIniciCurs = $ini,
    DataFiCurs = $fi,
    FormatInformePreferit = $format,
    AssistentCompletat = $assist,
    VersioHorariActivaId = $versio
WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$curs", c.CursEscolar);
        cmd.Parameters.AddWithValue("$ini", c.DataIniciCurs.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$fi", c.DataFiCurs.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$format", c.FormatInformePreferit);
        cmd.Parameters.AddWithValue("$assist", c.AssistentCompletat ? 1 : 0);
        cmd.Parameters.AddWithValue("$versio", c.VersioHorariActivaId);
        cmd.Parameters.AddWithValue("$id", c.Id);
        cmd.ExecuteNonQuery();
    }
}

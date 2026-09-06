using System;
using System.Collections.Generic;
using ProgramacioDocent.Data;
using ProgramacioDocent.Models;

namespace ProgramacioDocent.Services;

// Gestiona el calendari escolar: festius, vacances i dies de lliure disposició.
public class CalendariService
{
    private readonly Database _db;

    public CalendariService(Database db) => _db = db;

    public List<Festiu> ObteFestius()
    {
        var llista = new List<Festiu>();
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Data, Nom, Tipus FROM Festiu ORDER BY Data;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            llista.Add(new Festiu
            {
                Id = r.GetInt32(0),
                Data = DateTime.Parse(r.GetString(1)),
                Nom = r.GetString(2),
                Tipus = r.GetString(3)
            });
        }
        return llista;
    }

    public Festiu? FestiuDe(DateTime dia)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Data, Nom, Tipus FROM Festiu WHERE Data = $d LIMIT 1;";
        cmd.Parameters.AddWithValue("$d", dia.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Festiu
        {
            Id = r.GetInt32(0),
            Data = DateTime.Parse(r.GetString(1)),
            Nom = r.GetString(2),
            Tipus = r.GetString(3)
        };
    }

    public bool EsLectiu(DateTime dia)
    {
        if (dia.DayOfWeek == DayOfWeek.Saturday || dia.DayOfWeek == DayOfWeek.Sunday)
            return false;
        return FestiuDe(dia) == null;
    }

    // Afegeix un dia de lliure disposició (o qualsevol festiu propi del centre).
    public void AfegeixDiaLliure(DateTime dia, string descripcio)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT OR REPLACE INTO Festiu (Data, Nom, Tipus) VALUES ($d, $n, 'LliureDisposicio');";
        cmd.Parameters.AddWithValue("$d", dia.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$n", string.IsNullOrWhiteSpace(descripcio) ? "Dia de lliure disposició" : descripcio);
        cmd.ExecuteNonQuery();
    }

    public void EliminaFestiu(int id)
    {
        using var conn = _db.ObreConnexio();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Festiu WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // Retorna el dilluns de la setmana que conté la data indicada.
    public static DateTime DillunsDeLaSetmana(DateTime data)
    {
        int diff = ((int)data.DayOfWeek + 6) % 7; // dilluns = 0
        return data.Date.AddDays(-diff);
    }
}

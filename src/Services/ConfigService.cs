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
       AssistentCompletat, VersioHorariActivaId,
       Tema, ProfNom, ProfCognoms, ProfCentre, ProfDepartament, ProfEmail,
       PosicioEditorNotes, AmpladaPanellDret
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
            VersioHorariActivaId = r.GetInt32(6),
            Tema = r.GetString(7),
            ProfNom = r.GetString(8),
            ProfCognoms = r.GetString(9),
            ProfCentre = r.GetString(10),
            ProfDepartament = r.GetString(11),
            ProfEmail = r.GetString(12),
            PosicioEditorNotes = NormalitzaPosicio(r.IsDBNull(13) ? null : r.GetString(13)),
            AmpladaPanellDret = ClampAmplada(r.IsDBNull(14) ? 320 : r.GetInt32(14))
        };
    }

    // Valida la preferència de posició; qualsevol valor no reconegut torna a 'Modal'.
    private static string NormalitzaPosicio(string? valor) => valor switch
    {
        "Inferior" => "Inferior",
        "Dret" => "Dret",
        _ => "Modal"
    };

    // Limita l'amplada del panell dret a un rang raonable (evita valors absurds).
    private static int ClampAmplada(int px) => Math.Clamp(px, 240, 640);

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
    VersioHorariActivaId = $versio,
    Tema = $tema,
    ProfNom = $pnom,
    ProfCognoms = $pcognoms,
    ProfCentre = $pcentre,
    ProfDepartament = $pdept,
    ProfEmail = $pemail,
    PosicioEditorNotes = $posicio,
    AmpladaPanellDret = $amplada
WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$curs", c.CursEscolar);
        cmd.Parameters.AddWithValue("$ini", c.DataIniciCurs.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$fi", c.DataFiCurs.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$format", c.FormatInformePreferit);
        cmd.Parameters.AddWithValue("$assist", c.AssistentCompletat ? 1 : 0);
        cmd.Parameters.AddWithValue("$versio", c.VersioHorariActivaId);
        cmd.Parameters.AddWithValue("$tema", c.Tema ?? "Sistema");
        cmd.Parameters.AddWithValue("$pnom", c.ProfNom ?? string.Empty);
        cmd.Parameters.AddWithValue("$pcognoms", c.ProfCognoms ?? string.Empty);
        cmd.Parameters.AddWithValue("$pcentre", c.ProfCentre ?? string.Empty);
        cmd.Parameters.AddWithValue("$pdept", c.ProfDepartament ?? string.Empty);
        cmd.Parameters.AddWithValue("$pemail", c.ProfEmail ?? string.Empty);
        cmd.Parameters.AddWithValue("$posicio", NormalitzaPosicio(c.PosicioEditorNotes));
        cmd.Parameters.AddWithValue("$amplada", ClampAmplada(c.AmpladaPanellDret));
        cmd.Parameters.AddWithValue("$id", c.Id);
        cmd.ExecuteNonQuery();
    }
}

using System;
using System.Collections.Generic;

namespace ProgramacioDocent.Data;

// Dades oficials verificades del calendari escolar de Catalunya, curs 2026-2027.
// Fonts: Generalitat de Catalunya (calendari escolar) i calendari laboral 2026/2027.
//   - Inici de curs: 8/9/2026 (2n cicle Infantil, Primària, ESO, Batxillerat).
//   - Fi de curs: ~21/6/2027.
//   - Diada Nacional de Catalunya: 11/9/2026 (festiu).
//   - Festius: 12/10/2026, 8/12/2026, 25/12/2026, 26/12/2026.
//   - Vacances de Nadal: 22/12/2026 a 7/1/2027 (ambdós inclosos).
//   - Vacances de Setmana Santa: 20/3/2027 a 29/3/2027 (ambdós inclosos).
//   - Festius 2027 dins el curs: 1/5/2027 (Festa del Treball), 24/6/2027 (Sant Joan, fora de curs lectiu).
// Els dies de lliure disposició (fins a 4) els fixa cada centre; el professor
// els pot afegir manualment des de l'aplicació.
public static class CalendariCatalunya
{
    public static List<(DateTime Data, string Nom, string Tipus)> Curs2026_2027()
    {
        var llista = new List<(DateTime, string, string)>();

        // Festius puntuals dins el curs.
        llista.Add((new DateTime(2026, 9, 11), "Diada Nacional de Catalunya", "Festiu"));
        llista.Add((new DateTime(2026, 10, 12), "Festa Nacional d'Espanya", "Festiu"));
        llista.Add((new DateTime(2026, 12, 8), "La Immaculada", "Festiu"));
        llista.Add((new DateTime(2027, 5, 1), "Festa del Treball", "Festiu"));

        // Vacances de Nadal: 22/12/2026 a 7/1/2027 (inclou 25 i 26 de desembre).
        AfegeixInterval(llista, new DateTime(2026, 12, 22), new DateTime(2027, 1, 7),
            "Vacances de Nadal", "Vacances");

        // Vacances de Setmana Santa: 20/3/2027 a 29/3/2027.
        AfegeixInterval(llista, new DateTime(2027, 3, 20), new DateTime(2027, 3, 29),
            "Vacances de Setmana Santa", "Vacances");

        return llista;
    }

    private static void AfegeixInterval(
        List<(DateTime, string, string)> llista,
        DateTime inici, DateTime fi, string nom, string tipus)
    {
        for (var d = inici; d <= fi; d = d.AddDays(1))
        {
            // Només marquem els dies laborables (dilluns-divendres) com a no lectius,
            // ja que els caps de setmana no són dies de classe.
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                continue;
            llista.Add((d, nom, tipus));
        }
    }
}

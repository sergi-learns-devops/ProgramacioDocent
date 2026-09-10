using System;

namespace ProgramacioDocent.Models;

// Configuració general de l'aplicació (un únic registre).
public class Configuracio
{
    public int Id { get; set; }
    public string CursEscolar { get; set; } = "2026-2027";
    public DateTime DataIniciCurs { get; set; } = new(2026, 9, 8);
    public DateTime DataFiCurs { get; set; } = new(2027, 6, 21);
    public string FormatInformePreferit { get; set; } = "PDF"; // PDF | XLSX | CSV
    public bool AssistentCompletat { get; set; }
    public int VersioHorariActivaId { get; set; }

    // Preferència de tema visual: 'Sistema' | 'Clar' | 'Fosc'.
    public string Tema { get; set; } = "Sistema";

    // Preferència global d'on apareix l'editor de notes:
    // 'Modal' (finestra flotant) | 'Inferior' (panell a sota) | 'Dret' (panell a la dreta).
    public string PosicioEditorNotes { get; set; } = "Modal";

    // Amplada (px) del panell de notes quan està a la dreta, ajustable amb el divisor.
    public int AmpladaPanellDret { get; set; } = 320;

    // Perfil del professor (informació bàsica, opcional).
    public string ProfNom { get; set; } = string.Empty;
    public string ProfCognoms { get; set; } = string.Empty;
    public string ProfCentre { get; set; } = string.Empty;
    public string ProfDepartament { get; set; } = string.Empty;
    public string ProfEmail { get; set; } = string.Empty;
}

// Assignatura o matèria que imparteix el professor.
public class Assignatura
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Curs { get; set; } = string.Empty; // p.ex. "1r ESO A"
    public string Color { get; set; } = "#4F86C6";   // color per a la graella

    // Text per a la llista de Configuració (nom i, si n'hi ha, curs).
    public string NomICurs => string.IsNullOrWhiteSpace(Curs) ? Nom : $"{Nom} ({Curs})";
}

// Franja horària definida pel professor (p.ex. 08:00–09:00).
// (Model conservat només per compatibilitat; ja no s'utilitza al nou disseny.)

// Versió de l'horari. Permet editar l'horari en el futur (reducció de jornada,
// canvi de curs, etc.) sense trencar les notes ja preses amb la versió anterior.
public class VersioHorari
{
    public int Id { get; set; }
    public string Descripcio { get; set; } = string.Empty;
    public DateTime DataInici { get; set; }
    public DateTime? DataFi { get; set; }
    public bool Activa { get; set; }
}

// Una classe concreta dins la graella: dia + hora + assignatura.
public class ClasseHorari
{
    public int Id { get; set; }
    public int VersioHorariId { get; set; }
    public int DiaSetmana { get; set; } // 1 = dilluns ... 5 = divendres
    public TimeOnly HoraInici { get; set; }
    public TimeOnly HoraFi { get; set; }
    public int AssignaturaId { get; set; }
    public string Grup { get; set; } = string.Empty;
    public string Aula { get; set; } = string.Empty;

    // Camp desnormalitzat per a la UI (no es guarda a la BD).
    public Assignatura? Assignatura { get; set; }

    // Etiqueta de la franja horària d'aquesta classe.
    public string Etiqueta => $"{HoraInici:HH\\:mm} - {HoraFi:HH\\:mm}";

    // Resum per a la llista de Configuració: dia · assignatura · franja.
    public string ResumConfig
    {
        get
        {
            string[] dies = { "", "Dilluns", "Dimarts", "Dimecres", "Dijous", "Divendres" };
            var dia = DiaSetmana >= 1 && DiaSetmana <= 5 ? dies[DiaSetmana] : "";
            var nom = Assignatura?.Nom ?? "";
            return $"{dia} · {nom} · {Etiqueta}";
        }
    }
}

// Nota de text lliure associada a una classe i a una setmana concreta.
public class NotaSetmanal
{
    public int Id { get; set; }
    public int ClasseHorariId { get; set; }
    public DateTime DataDilluns { get; set; } // dilluns de la setmana
    public string Text { get; set; } = string.Empty;
    public DateTime DataCreacio { get; set; }
    public DateTime DataModificacio { get; set; }
}

// Dia festiu del calendari escolar de Catalunya.
public class Festiu
{
    public int Id { get; set; }
    public DateTime Data { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Tipus { get; set; } = "Festiu"; // Festiu | Vacances | LliureDisposicio

    // Resum per a la llista de dies de lliure disposició a Configuració.
    public string ResumLliure => $"{Data:dd/MM/yyyy} · {Nom}";
}

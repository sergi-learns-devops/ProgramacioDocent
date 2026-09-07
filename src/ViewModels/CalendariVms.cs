using System;
using Avalonia.Media;
using ProgramacioDocent.Models;
using ProgramacioDocent.Services;

namespace ProgramacioDocent.ViewModels;

// Bloc de classe posicionat a la graella tipus calendari.
// La posició i l'alçada es calculen a partir de l'hora d'inici i la durada.
public class BlocCalendariVm : ViewModelBase
{
    public ClasseHorari Classe { get; }

    // Posició a la graella (files de 30 min).
    public int Fila { get; }        // fila d'inici (Grid.Row)
    public int FilesSpan { get; }   // nombre de files que ocupa (Grid.RowSpan)
    public int Columna { get; }     // dia 1..5 -> columna 1..5 (Grid.Column)

    public bool TeNota { get; set; }

    public BlocCalendariVm(ClasseHorari classe, int fila, int filesSpan, int columna)
    {
        Classe = classe;
        Fila = fila;
        FilesSpan = filesSpan;
        Columna = columna;
    }

    public string NomAssignatura => Classe.Assignatura?.Nom ?? "";
    public string Franja => Classe.Franja?.Etiqueta ?? "";
    public string Detall
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(Classe.Grup)) parts.Add(Classe.Grup);
            if (!string.IsNullOrWhiteSpace(Classe.Aula)) parts.Add("Aula " + Classe.Aula);
            return string.Join(" · ", parts);
        }
    }

    public string Color => Classe.Assignatura?.Color ?? "#4F86C6";
    public IBrush FonsBrush => new SolidColorBrush(Avalonia.Media.Color.Parse(Color));
    public IBrush TextBrush => ContrastHelper.BrushTextSobre(Color);
    public IBrush TextDetallBrush => new SolidColorBrush(ContrastHelper.TextSobre(Color), 0.82);
}

// Columna d'un dia a la graella de calendari (fons segons avui/festiu).
public class ColumnaDiaVm : ViewModelBase
{
    public int Columna { get; }      // 1..5
    public bool EsAvui { get; }
    public bool EsFestiu { get; }

    public ColumnaDiaVm(int columna, bool esAvui, bool esFestiu)
    {
        Columna = columna;
        EsAvui = esAvui;
        EsFestiu = esFestiu;
    }
}

// Etiqueta d'hora de l'eix vertical esquerre.
public class EtiquetaHoraVm : ViewModelBase
{
    public int Fila { get; }
    public string Text { get; }
    public EtiquetaHoraVm(int fila, string text) { Fila = fila; Text = text; }
}

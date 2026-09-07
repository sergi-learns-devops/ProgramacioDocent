using ProgramacioDocent.Models;

namespace ProgramacioDocent.ViewModels;

// Representa una cel·la de la graella d'horari (una classe en un dia i franja).
public class ClasseCellaViewModel : ViewModelBase
{
    public ClasseHorari Classe { get; }

    public ClasseCellaViewModel(ClasseHorari classe)
    {
        Classe = classe;
    }

    public string NomAssignatura => Classe.Assignatura?.Nom ?? "";
    public string Detall
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(Classe.Assignatura?.Curs)) parts.Add(Classe.Assignatura!.Curs);
            if (!string.IsNullOrWhiteSpace(Classe.Grup)) parts.Add(Classe.Grup);
            if (!string.IsNullOrWhiteSpace(Classe.Aula)) parts.Add("Aula " + Classe.Aula);
            return string.Join(" · ", parts);
        }
    }
    public string Color => Classe.Assignatura?.Color ?? "#4F86C6";
    public bool TeNota { get; set; }

    // Color de fons del bloc (color de l'assignatura).
    public Avalonia.Media.IBrush FonsBrush
        => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(Color));

    // Color de text llegible sobre el fons (blanc o fosc segons luminància),
    // perquè el nom de l'assignatura es vegi bé en qualsevol color i tema.
    public Avalonia.Media.IBrush TextBrush
        => ProgramacioDocent.Services.ContrastHelper.BrushTextSobre(Color);

    // Variant més tènue del text per als detalls.
    public Avalonia.Media.IBrush TextDetallBrush
    {
        get
        {
            var c = ProgramacioDocent.Services.ContrastHelper.TextSobre(Color);
            return new Avalonia.Media.SolidColorBrush(c, 0.82);
        }
    }
}

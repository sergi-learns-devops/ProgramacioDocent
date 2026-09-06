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
}

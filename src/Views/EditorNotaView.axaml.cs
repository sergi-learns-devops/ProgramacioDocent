using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProgramacioDocent.Views;

// Contingut reutilitzable de l'editor de notes (títol, subtítol, text i botons).
// S'incrusta a la finestra flotant modal i als panells fixos (inferior/dret).
public partial class EditorNotaView : UserControl
{
    public EditorNotaView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

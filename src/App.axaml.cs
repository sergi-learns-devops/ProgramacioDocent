using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ProgramacioDocent.Data;
using ProgramacioDocent.Services;
using ProgramacioDocent.ViewModels;
using ProgramacioDocent.Views;

namespace ProgramacioDocent;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Inicialitza la base de dades i les dades del calendari.
            var db = new Database();
            db.Inicialitza();

            var horariService = new HorariService(db);
            var notesService = new NotesService(db);
            var calendariService = new CalendariService(db);
            var configService = new ConfigService(db);
            var informeService = new InformeService(db);

            var mainVm = new MainWindowViewModel(
                horariService, notesService, calendariService, configService, informeService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

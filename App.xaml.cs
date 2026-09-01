using System.Windows;

namespace KioskMeet
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            var controlBar = new ControlBarWindow(mainWindow);

            mainWindow.ControlBar = controlBar;
            MainWindow = mainWindow;

            mainWindow.Show();
            controlBar.Show();
        }
    }
}

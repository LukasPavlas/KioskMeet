using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace KioskMeet
{
    /// <summary>
    /// Malá vždy-navrch lišta v pravém horním rohu obrazovky s tlačítky
    /// Domů / Restart / Ukončit. Je to samostatné okno (ne součást
    /// MainWindow ani MeetWindow), takže zůstává viditelné i během
    /// probíhající schůzky nebo nad nativní appkou Teams/Zoom.
    /// </summary>
    public partial class ControlBarWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private DispatcherTimer? _keepOnTopTimer;

        public ControlBarWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;

            Loaded += (s, e) =>
            {
                PositionTopRight();
                StartKeepOnTopWatch();
            };
        }

        private void PositionTopRight()
        {
            Left = SystemParameters.PrimaryScreenWidth - ActualWidth - 20;
            Top = 20;
        }

        /// <summary>
        /// I když je toto okno Topmost, jiná Topmost okna (např. MeetWindow
        /// během schůzky) se mohou po aktivaci dostat "nad" něj - Windows
        /// řadí topmost okna mezi sebou podle toho, které bylo naposledy
        /// aktivováno. Pravidelné krátké vypnutí/zapnutí Topmost lištu
        /// spolehlivě vrátí navrch i nad jiná topmost okna.
        /// </summary>
        private void StartKeepOnTopWatch()
        {
            _keepOnTopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _keepOnTopTimer.Tick += (s, e) => ReassertTopmost();
            _keepOnTopTimer.Start();
        }

        public void ReassertTopmost()
        {
            if (!IsLoaded) return;
            Topmost = false;
            Topmost = true;
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.BringToFront();
            ReassertTopmost();
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                this,
                "Kiosk se restartuje. Pokud právě probíhá schůzka, bude ukončena.\n\nPokračovat?",
                "Restartovat kiosk",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(exePath);
                }
            }
            catch
            {
                // Pokud se nepovede spustit nová instance, appka se aspoň
                // bezpečně ukončí - je pak potřeba ji spustit ručně.
            }

            Application.Current.Shutdown();
        }

        public void RequestExit()
        {
            var result = MessageBox.Show(
                this,
                "Ukončení kiosk režimu se nedoporučuje - kiosk pak nebude dostupný pro další "
                + "uživatele, dokud ho někdo znovu ručně nespustí.\n\nOpravdu chcete pokračovat?",
                "Ukončit kiosk režim",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            Application.Current.Shutdown();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e) => RequestExit();
    }
}

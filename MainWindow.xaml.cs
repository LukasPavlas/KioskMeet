using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace KioskMeet
{
    public partial class MainWindow : Window
    {
        private MeetWindow? _meetWindow;
        private DispatcherTimer? _deviceCheckTimer;

        /// <summary>
        /// Referenci nastavuje App.xaml.cs po vytvoření obou oken.
        /// Používá se pro udržení ovládací lišty navrch i nad oknem
        /// se schůzkou.
        /// </summary>
        public ControlBarWindow? ControlBar { get; set; }

        private static readonly SolidColorBrush StatusOkBrush = new(Color.FromRgb(0x6f, 0xbf, 0x5e));
        private static readonly SolidColorBrush StatusWarnBrush = new(Color.FromRgb(0xf5, 0xa6, 0x23));
        private static readonly SolidColorBrush StatusErrorBrush = new(Color.FromRgb(0xe0, 0x5c, 0x5c));

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await MainWebView.EnsureCoreWebView2Async(null);

            // Zákaz kontextového menu, DevTools a akcelerátorů (F12, Ctrl+N, Ctrl+T...)
            MainWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            MainWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            MainWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
            MainWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);

            MainWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            StartDeviceMonitoring();
        }

        private void StartDeviceMonitoring()
        {
            CheckDevices();

            _deviceCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _deviceCheckTimer.Tick += (s, e) => CheckDevices();
            _deviceCheckTimer.Start();
        }

        private void CheckDevices()
        {
            // ===== Kamera =====
            var camera = CameraDeviceHelper.CheckCamera();

            if (camera.Found && camera.Ok)
            {
                CameraDot.Fill = StatusOkBrush;
                CameraStatusText.Text = "Kamera: OK";
            }
            else if (camera.Found && !camera.Ok)
            {
                CameraDot.Fill = StatusWarnBrush;
                CameraStatusText.Text = "Kamera: nalezena, hlásí problém";
            }
            else
            {
                CameraDot.Fill = StatusErrorBrush;
                CameraStatusText.Text = "Kamera: nenalezena";
            }

            // ===== Jabra 810 =====
            var audio = AudioDeviceHelper.CheckAndFixJabraDefault();

            if (!audio.Found)
            {
                JabraDot.Fill = StatusErrorBrush;
                JabraStatusText.Text = "Jabra 810: nenalezena";
            }
            else if (audio.IsDefaultPlayback && audio.IsDefaultRecording)
            {
                JabraDot.Fill = StatusOkBrush;
                JabraStatusText.Text = "Jabra 810: OK (výchozí)";
            }
            else
            {
                JabraDot.Fill = StatusWarnBrush;
                JabraStatusText.Text = "Jabra 810: připojena, není výchozí";
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string? message = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(message)) return;

            // Očekávaný formát z index.html:
            // {"type":"join","service":"meet"|"teams"|"zoom","url":"https://...",
            //  "autofillId":"...", "autofillPasscode":"..."}  <- poslední dvě jen pro Teams "ID" režim
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "join")
                {
                    return;
                }

                string service = root.TryGetProperty("service", out var svcProp)
                    ? svcProp.GetString() ?? "meet"
                    : "meet";

                string? url = root.TryGetProperty("url", out var urlProp)
                    ? urlProp.GetString()
                    : null;

                string? autofillId = root.TryGetProperty("autofillId", out var idProp)
                    ? idProp.GetString()
                    : null;

                string? autofillPasscode = root.TryGetProperty("autofillPasscode", out var pwdProp)
                    ? pwdProp.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(url))
                {
                    OpenMeetWindow(service, url, autofillId, autofillPasscode);
                }
            }
            catch (JsonException)
            {
                // Poškozená/neznámá zpráva - ignorovat
            }
        }

        private void OpenMeetWindow(string service, string url, string? autofillId = null, string? autofillPasscode = null)
        {
            // Pokud už jedno okno se schůzkou běží, zavřít ho a nahradit novým
            if (_meetWindow != null)
            {
                _meetWindow.MeetingEnded -= MeetWindow_MeetingEnded;
                _meetWindow.Close();
                _meetWindow = null;
            }

            // Kiosk okno musí dočasně přestat být "vždy navrch",
            // jinak by okno se schůzkou zůstalo skryté pod ním.
            Topmost = false;

            _meetWindow = new MeetWindow(service, url, autofillId, autofillPasscode);
            _meetWindow.MeetingEnded += MeetWindow_MeetingEnded;
            _meetWindow.Show();
            _meetWindow.Activate();

            // Ovládací lišta (Domů/Restart/Ukončit) musí zůstat navrch
            // i nad nově otevřeným oknem schůzky.
            ControlBar?.ReassertTopmost();
        }

        private void MeetWindow_MeetingEnded(object? sender, EventArgs e)
        {
            _meetWindow = null;

            // Vrátit kiosk okno zpět navrch a do popředí
            Topmost = true;
            Activate();
            Focus();
            MainWebView.CoreWebView2?.PostWebMessageAsString("ended");
        }

        /// <summary>
        /// Vrátí kiosk (obrazovku pro připojení ke schůzce) do popředí,
        /// i když právě běží schůzka v samostatném okně. Schůzku
        /// nezavírá - jen ji dočasně "podleze" (Home tlačítko).
        /// </summary>
        public void BringToFront()
        {
            if (_meetWindow != null)
            {
                _meetWindow.Topmost = false;
            }

            Topmost = true;
            Activate();
            Focus();

            ControlBar?.ReassertTopmost();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Skrytá zkratka pro ukončení celé kiosk aplikace: Ctrl + Alt + X
            // (stejné potvrzení jako tlačítko "Ukončit" v ovládací liště)
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

            if (ctrl && alt && e.Key == Key.X)
            {
                ControlBar?.RequestExit();
                return;
            }

            // Zablokovat Alt+F4, aby appku nešlo zavřít omylem
            if (e.SystemKey == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                e.Handled = true;
            }
        }
    }
}


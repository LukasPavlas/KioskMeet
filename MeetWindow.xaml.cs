using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace KioskMeet
{
    public partial class MeetWindow : Window
    {
        public event EventHandler? MeetingEnded;

        private readonly string _service;
        private readonly string _targetUrl;
        private readonly string? _autofillId;
        private readonly string? _autofillPasscode;
        private bool _autofillDone = false;
        private bool _endedRaised = false;
        private bool _nativeProcessSeen = false;
        private DispatcherTimer? _nativeWatchTimer;

        // Domény, na kterých je v rámci dané služby běžné se pohybovat
        // (vlastní schůzka + přihlašovací obrazovky). Jakmile appka
        // zaznamená navigaci mimo tento seznam, považuje to za konec
        // schůzky (typicky přesměrování na marketing/domovskou stránku).
        private static readonly Dictionary<string, string[]> AllowedHostsByService = new()
        {
            ["meet"] = new[]
            {
                "meet.google.com", "accounts.google.com",
                "www.gstatic.com", "ssl.gstatic.com", "fonts.gstatic.com"
            },
            ["teams"] = new[]
            {
                "teams.microsoft.com", "teams.live.com",
                "www.microsoft.com", // univerzální stránka "Join a meeting" pro připojení podle ID
                "login.microsoftonline.com", "login.live.com",
                "aadcdn.msftauth.net", "statics.teams.cdn.office.net",
                "res.cdn.office.net"
            },
            ["zoom"] = new[]
            {
                "zoom.us", "applications.zoom.us"
            }
        };

        // Názvy procesů nativní desktopové appky pro danou službu.
        // Google Meet nativní appku nemá, proto zde není.
        // Pozn.: nový Teams klient běží pod "ms-teams", starší pod "Teams".
        private static readonly Dictionary<string, string[]> NativeProcessNamesByService = new()
        {
            ["teams"] = new[] { "ms-teams", "Teams" },
            ["zoom"] = new[] { "Zoom" }
        };

        /// <param name="service">"meet" | "teams" | "zoom"</param>
        /// <param name="targetUrl">Cílová URL, na kterou appka naviguje</param>
        /// <param name="autofillId">
        /// Volitelné: číslo schůzky pro Teams "Join a meeting" stránku -
        /// pokud je vyplněné, appka po načtení stránky automaticky
        /// vyplní a odešle formulář (uživatel nemusí nic přepisovat).
        /// </param>
        /// <param name="autofillPasscode">Volitelné heslo ke schůzce (Teams)</param>
        public MeetWindow(string service, string targetUrl, string? autofillId = null, string? autofillPasscode = null)
        {
            InitializeComponent();
            _service = service;
            _targetUrl = targetUrl;
            _autofillId = autofillId;
            _autofillPasscode = autofillPasscode;

            Loaded += MeetWindow_Loaded;
            Closed += (s, e) =>
            {
                StopNativeWatch();
                RaiseEndedOnce();
            };
        }

        private async void MeetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await MeetWebView.EnsureCoreWebView2Async(null);

            MeetWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            MeetWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            MeetWebView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
            MeetWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            MeetWebView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
            MeetWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            MeetWebView.CoreWebView2.Navigate(_targetUrl);
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Automatické vyplnění ID/hesla se pokusí spustit jen jednou,
            // a jen na univerzální Teams stránce "Join a meeting".
            if (_autofillDone || string.IsNullOrEmpty(_autofillId)) return;
            if (MeetWebView.CoreWebView2 == null) return;

            Uri uri;
            try
            {
                uri = new Uri(MeetWebView.CoreWebView2.Source);
            }
            catch
            {
                return;
            }

            if (uri.Host != "www.microsoft.com") return;

            _autofillDone = true;

            // Záloha pro případ, že by se automatické vyplnění nepovedlo
            // (Microsoft může kdykoliv změnit strukturu stránky): hodnoty
            // se zkopírují do schránky a zobrazí se v panelu, aby je
            // uživatel mohl během pár vteřin ručně vložit.
            try
            {
                string clipboardText = !string.IsNullOrEmpty(_autofillPasscode)
                    ? $"{_autofillId}\t{_autofillPasscode}"
                    : _autofillId ?? "";

                if (!string.IsNullOrEmpty(clipboardText))
                {
                    System.Windows.Clipboard.SetText(clipboardText);
                }
            }
            catch
            {
                // Schránka nedostupná - není kritické, hodnoty jsou i tak vidět v panelu.
            }

            AutofillIdText.Text = "ID schůzky: " + _autofillId;
            AutofillPasscodeText.Text = string.IsNullOrEmpty(_autofillPasscode)
                ? ""
                : "Heslo: " + _autofillPasscode;
            AutofillInfoPanel.Visibility = Visibility.Visible;

            var hidePanelTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(14) };
            hidePanelTimer.Tick += (s2, e2) =>
            {
                AutofillInfoPanel.Visibility = Visibility.Collapsed;
                ((DispatcherTimer)s2!).Stop();
            };
            hidePanelTimer.Start();

            string idJson = JsonSerializer.Serialize(_autofillId);
            string pwdJson = JsonSerializer.Serialize(_autofillPasscode ?? "");

            // Pole na stránce se hledají podle placeholderu/aria-labelu
            // ("Enter meeting ID" / "Enter meeting Passcode"), protože
            // Microsoft nezveřejňuje stabilní ID elementů. Skript se
            // opakovaně pokouší (stránka je React SPA a pole se mohou
            // vykreslit s malým zpožděním), a pokud selže, uživatel
            // prostě zadá ID/heslo ručně - stránka zůstává funkční.
            string script = $@"
            (function() {{
                function fillAndSubmit() {{
                    try {{
                        var idInput = document.querySelector('input[placeholder*=""meeting ID"" i], input[aria-label*=""meeting ID"" i]');
                        var pwdInput = document.querySelector('input[placeholder*=""Passcode"" i], input[aria-label*=""Passcode"" i]');

                        if (idInput) {{
                            idInput.focus();
                            idInput.value = {idJson};
                            idInput.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        }}
                        if (pwdInput && {pwdJson}) {{
                            pwdInput.focus();
                            pwdInput.value = {pwdJson};
                            pwdInput.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        }}

                        var btn = Array.from(document.querySelectorAll('button')).find(function (b) {{
                            return /join a meeting/i.test(b.textContent || '');
                        }});

                        if (idInput && btn) {{
                            setTimeout(function () {{ btn.click(); }}, 300);
                            return true;
                        }}
                    }} catch (e) {{ /* ignorovat - zkusí se to znovu */ }}
                    return false;
                }}

                var tries = 0;
                var timer = setInterval(function () {{
                    tries++;
                    if (fillAndSubmit() || tries > 20) clearInterval(timer);
                }}, 300);
            }})();";

            try
            {
                await MeetWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch
            {
                // Automatické vyplnění selhalo - formulář na stránce
                // zůstává funkční, uživatel může ID/heslo zadat ručně.
            }
        }

        private void CoreWebView2_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            // Toto okno vždy naviguje jen na námi sestavenou URL důvěryhodné
            // schůzkové služby (Meet/Teams/Zoom), takže je bezpečné rovnou
            // povolit kameru a mikrofon bez vyskakovacího dotazu prohlížeče -
            // v kiosk režimu by na něj stejně nebyl nikdo, kdo by klikl.
            if (e.PermissionKind == CoreWebView2PermissionKind.Camera ||
                e.PermissionKind == CoreWebView2PermissionKind.Microphone)
            {
                e.State = CoreWebView2PermissionState.Allow;
            }
            else
            {
                // Ostatní oprávnění (notifikace, poloha...) v kiosku nejsou
                // potřeba - zamítnout, ať se nezobrazují žádné dialogy.
                e.State = CoreWebView2PermissionState.Deny;
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            Uri uri;
            try
            {
                uri = new Uri(e.Uri);
            }
            catch
            {
                return;
            }

            bool isWebScheme = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            if (isWebScheme) return;

            // Nestandardní schéma (msteams:, zoommtg:, ...) = stránka se
            // pokouší otevřít nativní desktopovou appku. Předáme to
            // Windows shellu - pokud appka není nainstalovaná, nic se
            // nestane a uživatel pokračuje na webové verzi schůzky.
            e.Cancel = true;

            try
            {
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });

                // Kiosk okno s webovou stránkou už nemusí být navrch -
                // ať je vidět skutečná nativní appka.
                Topmost = false;

                StartNativeWatch();
            }
            catch
            {
                // Appka není nainstalovaná / chybí registrace protokolu.
                // Necháváme uživatele na webové stránce - Teams i Zoom
                // v tom případě samy nabídnou pokračování v prohlížeči.
            }
        }

        private void StartNativeWatch()
        {
            if (!NativeProcessNamesByService.TryGetValue(_service, out var names)) return;

            _nativeProcessSeen = false;
            _nativeWatchTimer?.Stop();
            _nativeWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _nativeWatchTimer.Tick += (s, e) =>
            {
                bool running = false;
                foreach (var name in names)
                {
                    if (Process.GetProcessesByName(name).Length > 0)
                    {
                        running = true;
                        break;
                    }
                }

                if (running)
                {
                    _nativeProcessSeen = true;
                }
                else if (_nativeProcessSeen)
                {
                    // Appka běžela a teď proces zmizel -> schůzka skončila
                    StopNativeWatch();
                    Close();
                }
            };
            _nativeWatchTimer.Start();
        }

        private void StopNativeWatch()
        {
            _nativeWatchTimer?.Stop();
            _nativeWatchTimer = null;
        }

        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (MeetWebView.CoreWebView2 == null) return;

            Uri uri;
            try
            {
                uri = new Uri(MeetWebView.CoreWebView2.Source);
            }
            catch
            {
                return;
            }

            var allowedHosts = AllowedHostsByService.TryGetValue(_service, out var hosts)
                ? hosts
                : Array.Empty<string>();

            bool allowed = Array.IndexOf(allowedHosts, uri.Host) >= 0;

            if (!allowed)
            {
                Dispatcher.Invoke(Close);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void RaiseEndedOnce()
        {
            if (_endedRaised) return;
            _endedRaised = true;
            MeetingEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}


using System;
using System.IO;
using System.Text.Json;

namespace KioskMeet
{
    /// <summary>
    /// Jednoduchá konfigurace vzhledu appky (logo, název, barvy...),
    /// načítaná z wwwroot/config.json. Stejný soubor čte i JS ve
    /// wwwroot/index.html (přes fetch), takže stačí upravit jeden
    /// soubor a projeví se to na obou stranách appky.
    /// </summary>
    public class AppConfig
    {
        public string AppName { get; set; } = "Meeting Kiosk";
        public string WindowTitle { get; set; } = "Meeting Kiosk";
        public string Tagline { get; set; } = "Připojení na online schůzku";

        // Cesta k obrázku loga, relativní vůči složce wwwroot
        // (např. "assets/logo.png"). Když je prázdná, appka místo
        // obrázku zobrazí textové logo (AppName/LogoAltText).
        public string LogoPath { get; set; } = "";
        public string LogoAltText { get; set; } = "MK";

        public string AccentColor { get; set; } = "#f5a623";
        public string AccentColorDark { get; set; } = "#f07d1a";

        public string HelpContactText { get; set; } = "V případě potíží kontaktujte IT oddělení.";

        // Podle jaké části názvu appka hledá preferované audio zařízení
        // (mikrofon/reproduktor), které má nastavit jako výchozí -
        // viz AudioDeviceHelper.cs. AudioDeviceLabel je jen popisek
        // zobrazený ve stavovém panelu.
        public string AudioDeviceNameContains { get; set; } = "Jabra";
        public string AudioDeviceLabel { get; set; } = "Jabra 810";
    }

    public static class ConfigLoader
    {
        public static AppConfig Load()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "config.json");

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var config = JsonSerializer.Deserialize<AppConfig>(json, options);

                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch
            {
                // config.json chybí nebo je poškozený (neplatné JSON) -
                // appka v tom případě jede s výchozími hodnotami níže,
                // ať kiosk nespadne jen kvůli překlepu v konfiguraci.
            }

            return new AppConfig();
        }
    }
}

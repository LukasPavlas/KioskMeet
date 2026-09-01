using System;
using System.Management;

namespace KioskMeet
{
    /// <summary>
    /// Kontroluje přes WMI, zda je v systému aktivní a funkční kamera
    /// (USB webkamera se hlásí jako PNP zařízení třídy "Camera" nebo "Image").
    /// </summary>
    public static class CameraDeviceHelper
    {
        public class CameraStatus
        {
            public bool Found;
            public bool Ok;
            public string? Name;
        }

        public static CameraStatus CheckCamera()
        {
            var status = new CameraStatus();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Status, PNPClass FROM Win32_PnPEntity " +
                    "WHERE PNPClass = 'Camera' OR PNPClass = 'Image'");

                foreach (ManagementObject device in searcher.Get())
                {
                    var name = device["Name"]?.ToString();
                    var devStatus = device["Status"]?.ToString();

                    if (string.IsNullOrEmpty(name)) continue;

                    status.Found = true;
                    status.Name = name;
                    status.Ok = string.Equals(devStatus, "OK", StringComparison.OrdinalIgnoreCase);

                    // Jakmile najdeme jednu funkční kameru, není potřeba
                    // procházet další - stačí, že alespoň jedna funguje.
                    if (status.Ok) break;
                }
            }
            catch
            {
                // WMI dotaz selhal (např. služba WMI neběží) - appka to
                // zobrazí jako "kamera nenalezena", i když fyzicky
                // připojená být může.
                status.Found = false;
            }

            return status;
        }
    }
}

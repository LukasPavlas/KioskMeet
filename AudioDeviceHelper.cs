using System;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace KioskMeet
{
    /// <summary>
    /// Kontroluje, zda je připojené preferované audio zařízení (mikrofon
    /// i reproduktor), a pokud ano, snaží se ho nastavit jako výchozí pro
    /// Windows. Hledaný název zařízení se předává jako parametr - viz
    /// config.json (AudioDeviceNameContains) a ConfigLoader.cs.
    /// </summary>
    public static class AudioDeviceHelper
    {
        public class DeviceStatus
        {
            public bool Found;
            public bool IsDefaultPlayback;
            public bool IsDefaultRecording;
            public string? DeviceName;
        }

        public static DeviceStatus CheckAndFixDefault(string deviceNameContains)
        {
            var status = new DeviceStatus();

            if (string.IsNullOrWhiteSpace(deviceNameContains))
            {
                return status;
            }

            try
            {
                using var enumerator = new MMDeviceEnumerator();

                var playbackDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
                var captureDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

                var jabraPlayback = playbackDevices.FirstOrDefault(d =>
                    d.FriendlyName.Contains(deviceNameContains, StringComparison.OrdinalIgnoreCase));
                var jabraCapture = captureDevices.FirstOrDefault(d =>
                    d.FriendlyName.Contains(deviceNameContains, StringComparison.OrdinalIgnoreCase));

                if (jabraPlayback == null && jabraCapture == null)
                {
                    status.Found = false;
                    return status;
                }

                status.Found = true;
                status.DeviceName = jabraPlayback?.FriendlyName ?? jabraCapture?.FriendlyName;

                var currentDefaultPlayback = SafeGetDefault(enumerator, DataFlow.Render, Role.Multimedia);
                var currentDefaultCapture = SafeGetDefault(enumerator, DataFlow.Capture, Role.Multimedia);

                status.IsDefaultPlayback = jabraPlayback != null && currentDefaultPlayback?.ID == jabraPlayback.ID;
                status.IsDefaultRecording = jabraCapture != null && currentDefaultCapture?.ID == jabraCapture.ID;

                // Pokud Jabra ještě není výchozí, appka se ji pokusí nastavit
                // (pro roli Console, Multimedia i Communications).
                if ((jabraPlayback != null && !status.IsDefaultPlayback) ||
                    (jabraCapture != null && !status.IsDefaultRecording))
                {
                    try
                    {
                        var policyConfig = (IPolicyConfig)new PolicyConfigClient();

                        if (jabraPlayback != null && !status.IsDefaultPlayback)
                        {
                            policyConfig.SetDefaultEndpoint(jabraPlayback.ID, ERole.eConsole);
                            policyConfig.SetDefaultEndpoint(jabraPlayback.ID, ERole.eMultimedia);
                            policyConfig.SetDefaultEndpoint(jabraPlayback.ID, ERole.eCommunications);
                            status.IsDefaultPlayback = true;
                        }

                        if (jabraCapture != null && !status.IsDefaultRecording)
                        {
                            policyConfig.SetDefaultEndpoint(jabraCapture.ID, ERole.eConsole);
                            policyConfig.SetDefaultEndpoint(jabraCapture.ID, ERole.eMultimedia);
                            policyConfig.SetDefaultEndpoint(jabraCapture.ID, ERole.eCommunications);
                            status.IsDefaultRecording = true;
                        }
                    }
                    catch
                    {
                        // Nastavení výchozího zařízení přes nedokumentované COM
                        // rozhraní selhalo (může se lišit dle verze/buildu
                        // Windows). Appka jen nahlásí aktuální stav - je pak
                        // potřeba nastavit Jabru jako výchozí ručně přes
                        // Nastavení zvuku ve Windows.
                    }
                }
            }
            catch
            {
                // Enumerace audio zařízení selhala (např. Windows Audio
                // služba neběží) - vrátit "nenalezeno", appka to zobrazí
                // jako chybu ve stavovém panelu.
                status.Found = false;
            }

            return status;
        }

        private static MMDevice? SafeGetDefault(MMDeviceEnumerator enumerator, DataFlow flow, Role role)
        {
            try
            {
                return enumerator.GetDefaultAudioEndpoint(flow, role);
            }
            catch
            {
                return null;
            }
        }
    }

    // ===== Nedokumentované COM rozhraní pro nastavení výchozího audio zařízení =====
    //
    // Windows oficiálně nevystavuje veřejné API pro programové nastavení
    // výchozího přehrávacího/nahrávacího zařízení - proto se používá stejné
    // COM rozhraní jako v nástrojích typu EarTrumpet nebo AudioSwitcher.
    // Funguje spolehlivě na Windows 10/11, ale jako nedokumentované API se
    // teoreticky může chovat jinak na některých buildech - proto je volání
    // vždy obalené v try/catch (viz výše) s tichým selháním.

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string pszDeviceName, IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
        [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(string pszDeviceName, bool bDefault, out long hnsDefaultDevicePeriod, out long hnsMinimumDevicePeriod);
        [PreserveSig] int SetProcessingPeriod(string pszDeviceName, long hnsDevicePeriod);
        [PreserveSig] int GetShareMode(string pszDeviceName, IntPtr pMode);
        [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint(string pszDeviceName, ERole role);
        [PreserveSig] int SetEndpointVisibility(string pszDeviceName, bool bVisible);
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    internal class PolicyConfigClient
    {
    }

    internal enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }
}

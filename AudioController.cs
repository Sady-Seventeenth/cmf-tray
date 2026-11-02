using System;
using System.Runtime.InteropServices;

namespace CmfTray;

public static class AudioController
{
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object pInterface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorCom { }

    public static bool IsMuted()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice dev) != 0 || dev == null)
                return false;

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref iid, 1, IntPtr.Zero, out object ep) != 0 || ep == null)
            {
                Marshal.ReleaseComObject(dev);
                Marshal.ReleaseComObject(enumerator);
                return false;
            }

            var vol = (IAudioEndpointVolume)ep;
            vol.GetMute(out bool muted);
            Marshal.ReleaseComObject(vol);
            Marshal.ReleaseComObject(dev);
            Marshal.ReleaseComObject(enumerator);
            return muted;
        }
        catch
        {
            return false;
        }
    }

    public static void SetMute(bool mute)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice dev) == 0 && dev != null)
            {
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                if (dev.Activate(ref iid, 1, IntPtr.Zero, out object ep) == 0 && ep != null)
                {
                    var vol = (IAudioEndpointVolume)ep;
                    Guid g = Guid.Empty;
                    vol.SetMute(mute, ref g);
                    Marshal.ReleaseComObject(vol);
                }
                Marshal.ReleaseComObject(dev);
            }
            Marshal.ReleaseComObject(enumerator);
        }
        catch { }
    }
}

using System.Runtime.InteropServices;
using BluetoothAudioSwitch.Interop;

namespace AudioSwitch.Tray;

internal static class OutputVolume
{
    internal sealed record State(int Percent, bool Muted);

    internal static State Read() => Access(null, null);
    internal static State Set(int? percent = null, bool? muted = null) => Access(percent, muted);

    internal static State ToggleMute() => Access(null, null, toggleMute: true);

    private static State Access(int? percent, bool? muted, bool toggleMute = false)
    {
        var enumerator = Audio.Create();
        IMMDevice? device = null;
        object? control = null;
        try
        {
            // Resolve the current multimedia output for every gesture and refresh.
            enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, 1, out device);
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 0x17, IntPtr.Zero, out control);
            var volume = (IAudioEndpointVolume)control;
            var context = Guid.Empty;
            if (percent.HasValue)
                volume.SetMasterVolumeLevelScalar(Math.Clamp(percent.Value, 0, 100) / 100f, ref context);
            if (toggleMute)
            {
                volume.GetMute(out bool currentMute);
                muted = !currentMute;
            }
            if (muted.HasValue) volume.SetMute(muted.Value, ref context);
            volume.GetMasterVolumeLevelScalar(out float level);
            volume.GetMute(out bool isMuted);
            return new State((int)Math.Round(level * 100), isMuted);
        }
        finally
        {
            Audio.Release(control);
            Audio.Release(device);
            Audio.Release(enumerator);
        }
    }
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr notify);
    void UnregisterControlChangeNotify(IntPtr notify);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float level, ref Guid context);
    void SetMasterVolumeLevelScalar(float level, ref Guid context);
    void GetMasterVolumeLevel(out float level);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint channel, float level, ref Guid context);
    void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
    void GetChannelVolumeLevel(uint channel, out float level);
    void GetChannelVolumeLevelScalar(uint channel, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
}

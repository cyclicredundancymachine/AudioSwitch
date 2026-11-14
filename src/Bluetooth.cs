using System.Runtime.InteropServices;
using BluetoothAudioSwitch.Interop;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Devices.Bluetooth;

namespace AudioSwitch;

internal record PairedAudio(string Id, string Name, Guid Container, bool Connected, List<Endpoint> Endpoints);

internal static class Bluetooth
{
    internal static async Task<List<PairedAudio>> Paired()
    {
        var endpoints = Audio.Endpoints();
        var devices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true),
            new[] { "System.Devices.Aep.ContainerId", "System.Devices.Aep.IsConnected" }, DeviceInformationKind.AssociationEndpoint);
        var result = new List<PairedAudio>();
        foreach (var device in devices)
        {
            if (!device.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var value) || value is not Guid container) continue;
            var matching = endpoints.Where(e => e.Container == container).ToList();
            if (matching.Count == 0) continue;
            bool connected = device.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var state) && state is true;
            result.Add(new PairedAudio(device.Id, device.Name, container, connected, matching));
        }
        return result;
    }

    internal static async Task SetRadio(string id, bool enabled)
    {
        if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) throw new InvalidOperationException("Windows denied radio-control access.");
        var radio = await Radio.FromIdAsync(id);
        if (radio == null || radio.Kind != RadioKind.Bluetooth) throw new InvalidOperationException("Bluetooth radio not found.");
        var target = enabled ? RadioState.On : RadioState.Off;
        if (await radio.SetStateAsync(target) != RadioAccessStatus.Allowed) throw new InvalidOperationException("Windows rejected the radio change.");
        for (int i = 0; i < 20 && radio.State != target; i++) await Task.Delay(250);
        if (radio.State != target) throw new TimeoutException("Radio state did not verify.");
    }

    internal static void Request(Endpoint endpoint, bool connect)
    {
        if (endpoint.Filter == null) throw new NotSupportedException("Playback driver exposes no connection filter.");
        var enumerator = Audio.Create(); IMMDevice? filter = null; object? control = null;
        try
        {
            enumerator.GetDevice(endpoint.Filter, out filter);
            var iid = ComIds.KsControlIid;
            filter.Activate(ref iid, ComIds.ClsCtxAll, IntPtr.Zero, out control);
            var property = new KsProperty { Set = KsBluetoothAudio.PropertySet, Id = connect ? 0u : 1u, Flags = 1 };
            int hr = ((IKsControl)control).KsProperty(ref property, (uint)Marshal.SizeOf<KsProperty>(), IntPtr.Zero, 0, out _);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally { Audio.Release(control); Audio.Release(filter); Audio.Release(enumerator); }
    }

    internal static async Task<string> ConnectAndSwitch(string pairedId, string? endpointId = null)
    {
        var device = (await Paired()).SingleOrDefault(d => d.Id == pairedId) ?? throw new InvalidOperationException("Paired audio device not found. Pair in Windows first.");
        if (!(await Radio.GetRadiosAsync()).Any(r => r.Kind == RadioKind.Bluetooth && r.State == RadioState.On))
            throw new InvalidOperationException("Bluetooth is off. Run radio-on with the chosen radio ID, then retry Connect.");
        var candidates = device.Endpoints.Where(e => (e.State is DeviceState.Active or DeviceState.Unplugged) && (endpointId == null || e.Id == endpointId)).ToList();
        if (candidates.Count != 1) throw new NotSupportedException("Endpoint mapping is ambiguous or unavailable. No connection or output change attempted. See inspect output.");
        if (candidates[0].State != DeviceState.Active) Request(candidates[0], true);
        for (int i = 0; i < 60; i++)
        {
            var active = Audio.Endpoints().Where(e => e.Container == device.Container && e.Id == candidates[0].Id && e.State == DeviceState.Active).ToList();
            if (active.Count == 1) { Audio.Switch(active[0].Id); return active[0].Id; }
            if (active.Count > 1) throw new InvalidOperationException("Multiple playback endpoints became active. Select the intended output explicitly.");
            await Task.Delay(500);
        }
        throw new TimeoutException("Connection request sent, but no matching active playback endpoint appeared within 30 seconds. Output was not changed.");
    }
}

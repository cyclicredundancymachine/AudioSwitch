using System.Runtime.InteropServices;
using BluetoothAudioSwitch.Interop;

namespace AudioSwitch;

internal record Endpoint(string Id, string Name, Guid? Container, DeviceState State, string? Filter);

internal static class Audio
{
    internal static IMMDeviceEnumerator Create() => (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(ComIds.MMDeviceEnumeratorClsid, true)!)!;
    internal static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

    internal static string? Default(EDataFlow flow, int role)
    {
        var enumerator = Create();
        IMMDevice? device = null;
        try { enumerator.GetDefaultAudioEndpoint(flow, role, out device); device.GetId(out var id); return id; }
        catch (COMException e) when (e.HResult == unchecked((int)0x80070490)) { return null; }
        finally { Release(device); Release(enumerator); }
    }

    internal static string?[] Defaults() => [Default(EDataFlow.Render, 0), Default(EDataFlow.Render, 1), Default(EDataFlow.Render, 2), Default(EDataFlow.Capture, 0), Default(EDataFlow.Capture, 1), Default(EDataFlow.Capture, 2)];

    internal static List<Endpoint> Endpoints()
    {
        var result = new List<Endpoint>();
        var enumerator = Create();
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator.EnumAudioEndpoints(EDataFlow.Render, (DeviceState)15, out collection);
            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                collection.Item(i, out var device);
                IPropertyStore? store = null;
                object? topologyObject = null;
                IConnector? connector = null;
                try
                {
                    device.GetId(out var id); device.GetState(out var state);
                    device.OpenPropertyStore(0, out store);
                    string name = Read(store, PropertyKeys.DeviceFriendlyName, v => v.AsString()) ?? id;
                    Guid? container = Read(store, PropertyKeys.DeviceContainerId, v => v.AsGuid());
                    string? filter = null;
                    try
                    {
                        var iid = ComIds.DeviceTopologyIid;
                        device.Activate(ref iid, ComIds.ClsCtxAll, IntPtr.Zero, out topologyObject);
                        ((IDeviceTopology)topologyObject).GetConnector(0, out connector);
                        connector.GetDeviceIdConnectedTo(out filter);
                    }
                    catch (COMException) { }
                    result.Add(new Endpoint(id, name, container, state, filter));
                }
                finally { Release(connector); Release(topologyObject); Release(store); Release(device); }
            }
            return result;
        }
        finally { Release(collection); Release(enumerator); }
    }

    private static T? Read<T>(IPropertyStore store, PropertyKey key, Func<PropVariant, T?> reader)
    {
        try
        {
            store.GetValue(ref key, out var value);
            try { return reader(value); } finally { NativeMethods.PropVariantClear(ref value); }
        }
        catch (COMException) { return default; }
    }

    internal static void Switch(string endpointId)
    {
        if (!Endpoints().Any(e => e.Id == endpointId && e.State == DeviceState.Active))
            throw new InvalidOperationException("Selected playback endpoint is not active.");
        var before = Defaults();
        object policy = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"), true)!)!;
        try { Marshal.ThrowExceptionForHR(((IPolicyConfig)policy).SetDefaultEndpoint(endpointId, 1)); }
        finally { Release(policy); }
        var after = Defaults();
        if (after[1] != endpointId) throw new InvalidOperationException("Multimedia output change did not verify.");
        // Windows may couple console playback to multimedia. Protect communications and all capture roles.
        if (Enumerable.Range(2, 4).Any(i => before[i] != after[i]))
            throw new InvalidOperationException("A protected default changed during this operation. No additional defaults were written. Inspect diagnostics.");
    }
}

// Undocumented Windows policy interface, isolated here. Only multimedia role 1 is written.
[ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    void GetMixFormat(); void GetDeviceFormat(); void ResetDeviceFormat(); void SetDeviceFormat();
    void GetProcessingPeriod(); void SetProcessingPeriod(); void GetShareMode(); void SetShareMode();
    void GetPropertyValue(); void SetPropertyValue();
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    void SetEndpointVisibility();
}

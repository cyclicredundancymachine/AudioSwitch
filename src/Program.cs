using System.Text.Json;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using AudioSwitch;

try
{
    string command = args.FirstOrDefault() ?? "inspect";
    object result;
    switch (command)
    {
        case "inspect":
            var radios = new List<object>();
            foreach (var info in await DeviceInformation.FindAllAsync(Radio.GetDeviceSelector()))
            {
                var radio = await Radio.FromIdAsync(info.Id);
                if (radio?.Kind == RadioKind.Bluetooth) radios.Add(new { info.Id, radio.Name, State = radio.State.ToString() });
            }
            result = new { Time = DateTimeOffset.Now, OS = Environment.OSVersion.ToString(), Elevated = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator), Radios = radios, Defaults = Audio.Defaults(), Endpoints = Audio.Endpoints(), PairedAudio = await Bluetooth.Paired() };
            break;
        case "radio-on" when args.Length == 2:
        case "radio-off" when args.Length == 2:
            await Bluetooth.SetRadio(args[1], command == "radio-on"); result = new { Verified = true }; break;
        case "switch" when args.Length == 2:
            Audio.Switch(args[1]); result = new { Verified = true, Endpoint = args[1] }; break;
        case "connect" when args.Length is 2 or 3:
            result = new { Verified = true, Endpoint = await Bluetooth.ConnectAndSwitch(args[1], args.Length == 3 ? args[2] : null) }; break;
        case "disconnect" when args.Length == 2:
            var paired = (await Bluetooth.Paired()).Single(d => d.Id == args[1]);
            var endpoints = paired.Endpoints.Where(e => e.State == BluetoothAudioSwitch.Interop.DeviceState.Active).ToList();
            if (endpoints.Count != 1) throw new InvalidOperationException("Expected exactly one active playback endpoint.");
            Bluetooth.Request(endpoints[0], false);
            bool disconnected = false;
            for (int i = 0; i < 60; i++)
            {
                if (!Audio.Endpoints().Any(e => e.Container == paired.Container && e.State == BluetoothAudioSwitch.Interop.DeviceState.Active)) { disconnected = true; break; }
                await Task.Delay(500);
            }
            if (!disconnected) throw new TimeoutException("Driver accepted the request, but playback disconnection did not verify.");
            result = new { Verified = true }; break;
        default:
            throw new ArgumentException("Commands: inspect | radio-on RADIO_ID | radio-off RADIO_ID | connect PAIRED_ID [ENDPOINT_ID] | disconnect PAIRED_ID | switch ACTIVE_ENDPOINT_ID");
    }
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAILED 0x{ex.HResult:X8}: {ex.Message}");
    return 1;
}

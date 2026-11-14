# API decisions and limitations

## Supported Windows interfaces

- WinRT Radio enumeration, RequestAccessAsync, SetStateAsync, and observed State: Bluetooth radio control. Hardware switches, policy, and adapter availability can prevent success.
- WinRT BluetoothDevice paired association-endpoint selector: existing classic Bluetooth devices only. Container IDs correlate paired devices to Core Audio render endpoints; names are never used as identity.
- Core Audio IMMDeviceEnumerator, property stores, and IDeviceTopology: enumerate playback endpoints and inspect state/default roles. Missing or ambiguous associations fail closed. Paired audio devices without matching installed endpoints are currently omitted; LE Audio coverage is unverified.

## Driver-dependent / undocumented dependencies

Actual audio connection uses IKsControl with KSPROPSETID_BtAudio, one-shot reconnect/disconnect properties (0/1, GET flag). The playback topology supplies the filter ID. This established implementation technique is driver-dependent and is not a universal public WinRT headset-connect contract. HRESULT success alone is not proof: the prototype waits for the exact correlated endpoint to become active before switching. Disconnection waits for all associated render endpoints to become inactive. Devices with multiple active profiles currently require further disconnect handling.

The synchronous driver call itself has no cancellation or timeout. The following endpoint activation wait is bounded to 30 seconds. The tray app isolates device changes in a worker process with a 45-second deadline for connect and disconnect, and 12 seconds for other commands. A timeout triggers a state refresh.

Default-output selection uses the undocumented IPolicyConfig COM interface, isolated in Audio.cs. Only the multimedia render role is written. Windows on the tested laptop couples the console playback role to multimedia. Communications playback and all capture roles are checked for changes; the app does not write them. External Windows changes may still occur, especially when a headset disconnects. Existing apps may retain explicit device selections or streams.

## Rejected approaches

- AudioPlaybackConnection receives Bluetooth audio from another device into the PC; it does not connect a playback headset. [Microsoft documentation](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/enable-remote-audio-playback).
- BluetoothSetServiceState changes service enablement and may involve drivers; it is not a reliable audio-profile Connect operation. [Microsoft documentation](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/nf-bluetoothapis-bluetoothsetservicestate).
- No driver modification, service restart, pairing, discovery, registry manipulation, or device-name matching.

## Code provenance

CoreAudio.cs is reused from [chrispoulter/bluetooth-audio-switch](https://github.com/chrispoulter/bluetooth-audio-switch), commit `0e1407c265ceb4379768d7b67c861385e957a85f`, under MIT; see THIRD-PARTY-LICENSE.txt. Connection research also examined [ToothTray](https://github.com/m2jean/ToothTray). Other prototype files implement render-only operations, explicit selection, and verification locally.

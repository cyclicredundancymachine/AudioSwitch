# AudioSwitch

## Tray app

Open dist\AudioSwitchTray.exe. It opens the popup and adds AudioSwitch to the Windows notification area. Close the popup to keep AudioSwitch in the tray. Double-click its icon to reopen it. Hover or single-click for status. If the icon is hidden, look under the arrow beside the clock. Use Exit to quit. Run with --minimized to start with only the tray icon.

Select a playback output directly, or use Connect under Paired Bluetooth audio to connect and select a paired device. Device state refreshes inside the app every two seconds while open and every five seconds in the tray. Sound settings opens Windows Settings. Help opens the short README.

To share AudioSwitch, copy the whole folder to a compatible Windows x64 PC. Keep AudioSwitchTray.exe, AudioSwitch.exe, README.txt, and THIRD-PARTY-LICENSE.txt together. Open AudioSwitchTray.exe. Device preferences and sign-in startup are set separately per PC. The tray app calls the latter as its device helper. Both are self-contained Windows x64 builds. Build the helper using the command below, then publish the tray from the project directory:

    dotnet publish tray\AudioSwitchTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist

Copy README.txt and THIRD-PARTY-LICENSE.txt into dist after publishing. The remaining commands are for the diagnostic helper.

## Run

The self-contained x64 executable is `dist\AudioSwitch.exe`. It needs neither a .NET installation nor administrator privileges for normal use. Run it in PowerShell; double-clicking a console prototype will not provide a persistent interface.

```powershell
.\dist\AudioSwitch.exe inspect
.\dist\AudioSwitch.exe radio-on 'RADIO_ID'
.\dist\AudioSwitch.exe radio-off 'RADIO_ID'
.\dist\AudioSwitch.exe connect 'PAIRED_ID' 'ENDPOINT_ID'
.\dist\AudioSwitch.exe switch 'ACTIVE_ENDPOINT_ID'
.\dist\AudioSwitch.exe disconnect 'PAIRED_ID'
```

Copy the exact IDs from `inspect`. The endpoint argument to `connect` is optional only when the paired device has one eligible render endpoint. Selecting an endpoint belonging to a different device is rejected. Initial pairing remains in Windows Settings. Radio changes and inspection never call the output setter. Only explicit `connect` and `switch` commands can select an output.

`Verified: true` means the requested state was read back, not merely that an API accepted the request. Exit code 1 and a message indicate failure. A driver request can fail or block; this prototype is not yet suitable as a background service.

Inspection reports all render endpoints, including inactive ones. State values: 1 active, 2 disabled, 4 absent, 8 unplugged. Defaults array order: playback console, playback multimedia, playback communications, recording console, recording multimedia, recording communications.

## Build

Install the .NET 10 SDK. From this directory:

```powershell
dotnet publish src\AudioSwitch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

First build requires access to Microsoft's package feeds. Keep THIRD-PARTY-LICENSE.txt with any redistributed executable. Target API floor is Windows 10 build 19041; this is not a claim of tested Windows 10 compatibility. Only this Windows 11 x64 laptop has been exercised.

See [API_NOTES.md](API_NOTES.md) for dependencies and [HARDWARE_TESTS.md](HARDWARE_TESTS.md) for manual verification.

For maintenance instructions, see [AGENTS.md](AGENTS.md). Keep the device helper beside the tray executable.

Use **Device list** to manage visibility. Uncheck a device and save to hide it. Check it again and save to restore it. Hiding a paired device also hides its playback choices. The current-output header still shows the actual Windows output. Visibility is saved per Windows user and does not change pairing.
The headphones tray icon is blue when Bluetooth is on and red when it is off. Hover or single-click for Bluetooth state and the default playback output.

# Working on AudioSwitch

- Build both projects on Windows x64 with the .NET 10 SDK. See README.md for the publish commands. Keep the worker EXE beside the tray EXE.
- `tray/Program.cs` owns the UI and worker lifetime. `src/Program.cs` handles explicit commands. `src/Bluetooth.cs` handles radio and connect/disconnect operations. `src/Audio.cs` handles endpoint discovery and multimedia output selection.
- `tray/OutputVolume.cs` owns current-output master volume and mute. Keep refresh read-only, resolve the current output for each gesture and read back changes. Preserve display scaling and compact rows in the common build.
- Keep discovery read-only. Only explicit Connect and Use actions may change the default multimedia output. Keep genuine device IDs and the container correlation checks. Preserve the worker timeout and verify changes by reading the Windows state back.
- Coordinate hardware tests with the user. Start with `inspect`, then verify playback changes and restore the original output after testing. Follow HARDWARE_TESTS.md and record only observed results.
- Update README.txt when the UI changes. Keep the third-party license with redistributed builds and exclude user device IDs, local diagnostics and binaries from Git.

- VERSION is the release version. Build-Release.ps1 creates Setup, portable ZIP and SHA256SUMS.txt using Inno Setup 7; see INSTALL.md. Run release scripts with Windows PowerShell 5.1. Test clean install, upgrade and uninstall before publishing. Keep installed Help synchronized with README.txt.
- Never publish local diagnostics, device identifiers, user configuration, credentials or the internal handoff. Check tracked history before changing repository visibility. Release publication and visibility follow the user's current authorization.

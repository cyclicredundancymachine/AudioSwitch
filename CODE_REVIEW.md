# AudioSwitch code review and verification

Review date: November 27, 2026. Version: 1.1.0.

## Fixed defects

- P1: Unbounded in-process inspection could block timeout recovery and leave device controls disabled indefinitely. Inspection now uses the helper's 12-second deadline; stale results are rejected after a newer operation.
- P2: Mute could invert an outdated polled flag after an external change or output switch. It now reads and toggles the current endpoint's live state and reads the result back.
- P2: A periodic read could reposition the slider during a mouse drag. Refresh waits while the slider owns mouse capture.

## Interop and behavior reviewed

Reviewed Core Audio enumerator, property store, topology, connector and endpoint-volume method order, HRESULT handling, BOOL marshaling, x64 PROPVARIANT layout and COM release paths. No confirmed ABI defect was found in the methods called. Optional topology failures retain usable playback endpoints. Exact endpoint/container identity and ambiguity checks remain intact.

Only explicit Connect and Use actions write the multimedia default. Communications and recording defaults are checked for changes but are not rolled back over independent Windows changes. Windows can couple console playback to multimedia. PolicyConfig is undocumented; Bluetooth IKsControl requests remain driver-dependent.

A volume gesture targets the endpoint resolved at its start. A concurrent external default-output change is not atomic with that gesture; readback stays on the same endpoint. Synchronous volume COM calls can still block the UI if a driver hangs. The worker deadline does not cover volume.

## Observed checks

- Both Windows x64 projects published successfully as compressed self-contained executables.
- Read-only inspection found playback endpoints, paired devices and a default output on Windows 11, ASUS ROG Strix G16 G614PW.
- Real volume write/read, live mute toggle and restoration of original values passed.
- A deliberately hung helper was terminated after 12.1 seconds. Real inspection, snapshot deserialization and rendering passed afterward.
- The compressed worker switched from an already-connected Bluetooth headset to Realtek speakers, then Connect selected the still-active headset. Fresh inspection verified both multimedia defaults; communications and recording defaults stayed unchanged. The original output was restored and the headset remained active.
- An unavailable, powered-off headset reached the expected connection timeout. This is not evidence of a connection defect.
- The popup was inspected on Strix with compact rows, volume/mute and all footer buttons visible.

Detailed device identifiers and local diagnostics are intentionally excluded from the repository and packages.

## Remaining coverage and limitations

Audible playback, reconnect from a disconnected powered-on headset, explicit disconnect, Flow scaling, high scaling and mixed-monitor DPI still need broader verification. Radio-control requests were denied in the test environment and left radio state unchanged. Sandboxed build restores reported an unavailable vulnerability feed. Subsequent network-enabled NuGet vulnerability queries completed successfully with no vulnerabilities reported for listed dependencies; this does not audit all embedded runtime code.

The single supported package is compressed self-contained Windows x64, distributed both as Setup and portable ZIP with identical payload files. See INSTALL.md, HARDWARE_TESTS.md and RELEASE_NOTES.md.

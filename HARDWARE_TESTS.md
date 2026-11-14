# Hardware test checklist

Save `inspect` output before and after tests. Defaults indices 2–5 must not be written by AudioSwitch. Windows can independently change defaults; record those changes separately. Use exact IDs from inspection. Do not use a microphone/call-dependent headset for disruptive disconnect testing during a call.

- Detect radio and paired audio devices; compare with Windows Settings.
- Switch to active internal speakers, verify multimedia ID, then restore the original output. Verify communications and capture defaults are unchanged.
- With a powered-on, disconnected paired headset, run `connect PAIRED_ID ENDPOINT_ID`. Require observed active endpoint and matching multimedia default, not just accepted driver request. Test actual audible playback manually.
- Request an unavailable headset; verify useful failure and no app-requested output change.
- Disconnect a connected test headset; require all associated render endpoints to become inactive. Record independent Windows fallback separately.
- With Bluetooth off, Connect must report that Bluetooth needs enabling. Enable with `radio-on`, confirm state, and verify AudioSwitch does not invoke output selection. Test off/on only when interruption of other Bluetooth peripherals is acceptable.
- Connect independently through Windows; inspection must observe without writing output defaults. Startup/inspection must never restore output automatically.
- Reject an invalid endpoint ID, an inactive endpoint, and an endpoint belonging to a different paired device; defaults must remain unchanged.
- Repeat on another Windows 11 PC and Windows 10 where feasible, with classic Bluetooth, LE Audio, USB, and HDMI outputs. Do not infer compatibility from the build target.

Phase 1 local evidence is in the ignored evidence directory because it contains device identifiers. Automated application logic tests and complete distribution packaging remain Phase 4 work.

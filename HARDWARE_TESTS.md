# Hardware test checklist

Save `inspect` output before and after tests. Defaults indices 2–5 must not be written by AudioSwitch. Windows can independently change defaults; record those changes separately. Use exact IDs from inspection. Do not use a microphone/call-dependent headset for disruptive disconnect testing during a call.

- Detect radio and paired audio devices; compare with Windows Settings.
- Verify volume and Mute/Unmute against Windows, switch outputs and repeat, then restore the original values. Current local evidence covers reading volume and writing the same values back. Audible changes and behavior across devices remain manual checks.
- Check compact rows, footer wrapping, resizing and full-name tooltips at different display scaling. The user reported the earlier Flow clipping improved. The latest controls still need a visual check there.
- Switch to active internal speakers, verify multimedia ID, then restore the original output. Verify communications and capture defaults are unchanged.
- With a powered-on, disconnected paired headset, run `connect PAIRED_ID ENDPOINT_ID`. Require observed active endpoint and matching multimedia default, not just accepted driver request. Test actual audible playback manually.
- Request an unavailable headset; verify useful failure and no app-requested output change.
- Disconnect a connected test headset; require all associated render endpoints to become inactive. Record independent Windows fallback separately.
- With Bluetooth off, Connect must report that Bluetooth needs enabling. Enable with `radio-on`, confirm state, and verify AudioSwitch does not invoke output selection. Test off/on only when interruption of other Bluetooth peripherals is acceptable.
- Connect independently through Windows; inspection must observe without writing output defaults. Startup/inspection must never restore output automatically.
- Reject an invalid endpoint ID, an inactive endpoint, and an endpoint belonging to a different paired device; defaults must remain unchanged.
- Repeat on another Windows 11 PC and Windows 10 where feasible, with classic Bluetooth, LE Audio, USB, and HDMI outputs. Do not infer compatibility from the build target.

Keep device-specific diagnostics outside version control. The shared Windows x64 portable package contains both executables, README.txt and THIRD-PARTY-LICENSE.txt. Packaging is implemented. Hardware coverage remains limited to checks actually performed.

November 27 review follow-up: the compressed candidate passed worker inspection,
volume write/read and restoration, live mute toggle, and a deliberately hung
helper test (terminated at 12.1 seconds, followed by successful inspection).
The popup opened on STRIX after stopping the installed tray because the
single-instance lock prevents parallel candidates. Flow, mixed-monitor DPI and
audible playback remain required. Only one active endpoint was available in the
initial inspection; the later switching test below used two. Sandboxed radio control
was denied and left state unchanged. See CODE_REVIEW.md for subsequent evidence.

The November 27 tester follow-up confirms that switching between his Bluetooth
headset and speaker on the fly worked in the app he tested. This is user-verified
switching evidence; the exact build was not identified. The installed tray had
been restored before this report, so compressed-candidate coverage remains
separate. The tester also confirmed that no headsets were powered on during the agent's
connection test: its timeout was expected, not a demonstrated connection defect.

Subsequent agent verification with the already-connected XO-BS37: the compressed
candidate worker switched from the headset to Realtek speakers, then `connect`
selected the headset again. Fresh inspections verified both multimedia defaults;
communications and recording defaults stayed unchanged. Final state matched the
original output, with the headset still active and the radio unchanged. This
passes active-output switching and Connect's already-active path. Reconnecting a
disconnected headset, explicit disconnect and audible playback remain untested
by the agent. No headset disconnect was issued in this test.

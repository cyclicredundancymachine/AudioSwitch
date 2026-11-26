# AudioSwitch 1.1.0

- Added current-output volume and live Mute/Unmute controls.
- Preserved compact device rows, resizable layout, tooltips and per-monitor DPI awareness.
- Bounded inspection and timeout recovery in the worker; retained exact-device mapping and protected-default checks.
- Deferred volume refresh during dragging and fixed stale mute toggles.
- Added a per-user Windows x64 installer, portable ZIP and SHA-256 checksums. Both packages use the same compressed self-contained binaries and include Help and the third-party MIT notice.

Verified: worker inspection, volume/mute readback and restoration, 12-second hung-worker recovery, popup layout on Strix, and switching between an already-connected Bluetooth headset and speakers with protected defaults unchanged. Reconnect from disconnected state, audible playback, Flow/mixed-DPI and broader device compatibility remain unverified. See CODE_REVIEW.md and HARDWARE_TESTS.md. Installers are not code-signed.

Installer verification on November 27, 2026: clean install, same-version upgrade and uninstall passed; installed payload hashes matched the build, and uninstall preserved unrelated source files. In-place upgrade into the existing app folder passed, installed Help matched source, and user configuration/restore records were unchanged. Both updated tray apps restarted successfully. AudioSwitch retained read-only inspection; Laptop Power's embedded-resource verifier passed.

The original application is now MIT licensed. LICENSE is included in Setup and the portable ZIP; existing third-party notices are retained.

# Changelog

## 1.0.1 - 2026-10-24

- Fix the right-click tray menu and restore access to Exit with a NotifyIcon-owned menu.
- Add one Start menu shortcut so Windows Search can find the app, updating it without duplication.
- Open Settings in the existing process when launched again from Start or the executable.
- Apply Start with Windows immediately using the per-user Startup folder and migrate the old Run entry.
- Reduce private RAM use by disabling compression of bundled .NET libraries. Keep compression in the download ZIP instead.
- Create the overlay only when needed, suppress unchanged Sonar readings, and avoid copying socket message buffers.

The executable is larger on disk. A short local sample measured about 21 MiB private memory before opening Settings and 91 MiB with Settings open; results vary. Startup registration, shortcut targeting, and the tray menu were verified. Sign-in execution requires a subsequent Windows sign-in.

## 1.0.0 — 2026-10-24

First public release of ChatMix Overlay for Windows 11 x64.

- Read-only Sonar integration with live ChatMix wheel events and automatic reconnection.
- Compact, click-through Game/Chat overlay with smooth movement and automatic dismissal.
- Light, Dark, and Follow Windows appearances.
- Position, monitor, duration, reverse-direction, and optional Windows startup settings.
- Custom tray icon: left-click opens Settings; right-click opens the menu.
- Portable, self-contained executable; no separate .NET installation required.

Physical wheel detection was verified with an Arctis Nova 7. Other headset models, mixed-DPI configurations, and individual games require testing. Exclusive fullscreen is not guaranteed. The executable is unsigned.

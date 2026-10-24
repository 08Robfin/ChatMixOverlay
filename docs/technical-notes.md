# Sonar observation research — 2026-10-24

## Findings on this machine

Read `%ProgramData%\SteelSeries\GG\coreProps.json`, using `ggEncryptedAddress` for GG Core discovery. A GET to its HTTPS `/subApps` returned `subApps.sonar.metadata.webServerAddress`. This session's Sonar port was 49985; it is dynamic and is never hard-coded in the application.

GET `/chatMix` returned 404. GET `/v1/chatMix` returned:

```json
{"balance":0.009999997,"state":"finiteWheel","id":"00000000-0000-0000-0000-000000000000"}
```

The observer tries the versioned route first, falling back only on HTTP 404. It rejects malformed/out-of-range balances instead of making up a center reading. The id is unused.

A WebSocket upgrade on `/sock` succeeded. Physical wheel testing emitted this message shape, repeatedly across the full range:

```json
{"event":"EVENT_SONAR_CHATMIX_DATA","data":{"balance":-1.0,"state":"finiteWheel","id":"00000000-0000-0000-0000-000000000000"}}
```

A timestamped diagnostic capture was inspected during development; temporary captures are not distributed. No audio parameters were written during the test. Actual physical wheel movement was confirmed by the user; which direction moved first was recalled with uncertainty. Negative-as-Game is supported by the upstream example and consistent with that recollection, but should be deliberately checked in the overlay.

## Integration choice

Use a custom, small read-only observer with built-in .NET HTTP and WebSocket clients. Only two HTTP resources are read during discovery and one during steady state. The socket receiver handles fragmented messages, bounds message size, filters for the observed ChatMix event and reconnects independently. The HTTP loop rediscovers after failures. All discovered URLs are restricted to IPv4 loopback; redirects and proxies are disabled. Self-signed certificate acceptance is confined to the loopback HTTP client, never installed globally.

A 350ms state refresh covers Sonar UI updates and socket loss. It is required because upstream research reports that software API writes can bypass socket broadcasts. This software-slider behavior has not been independently exercised here. Polling is serialized, and backs off when Sonar is unavailable. No unsupported subscription messages or speculative IPC interfaces are used.

## Alternatives investigated

- **Open source Sonar clients:** SteelSeries-NET-API documents direct ChatMix reading, events and the negative Game direction. GGSonarRev independently lists the older ChatMix route. Their differing route versions demonstrate why live endpoint verification matters. This application does not embed or depend on either library.
- **USB HID:** HeadsetControl implements SteelSeries Nova protocol helpers including ChatMix. Direct hardware access is possible in that ecosystem, but device support and behavior with GG sharing the interface vary. No HID access was attempted because Sonar already exposes the authoritative state, and hardware values alone do not establish Sonar's active mix.
- **Windows audio notifications:** WASAPI endpoint callbacks expose volume/mute changes, not a semantic Sonar Game/Chat balance. Inferring balance from two endpoint levels is ambiguous because independent user channel volumes also affect them. No inference-based fallback is implemented.
- **Official support:** SteelSeries support pages have differing statements about Nova 7 wheel/software control across models/setup. Live testing on this installation is stronger evidence of this specific integration than assuming a uniform product behavior.

## Sources

- [SteelSeries-NET-API](https://github.com/DataNext27/SteelSeries-NET-API): API example labels -0.3 as toward Game.
- [Architecture and protocol findings](https://github.com/DataNext27/SteelSeries-NET-API/blob/main/docs/ARCHITECTURE.md): discovery, socket, refresh rationale, version migration.
- [Socket listener source](https://github.com/DataNext27/SteelSeries-NET-API/blob/main/SteelSeriesAPI/Sonar/Events/SonarEventListener.cs): framing and ChatMix envelope.
- [GGSonarRev](https://github.com/PrzemekkkYT/GGSonarRev): earlier route inventory.
- [HeadsetControl device development](https://github.com/Sapd/HeadsetControl/blob/master/docs/ADDING_A_DEVICE.md): Nova HID helper capabilities.
- [Microsoft endpoint callback documentation](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolumecallback): scope of endpoint volume/mute notifications.
- [SteelSeries ChatMix setup](https://support.steelseries.com/hc/en-us/articles/16956381998989-What-is-ChatMix-and-how-to-set-it-up).
- [SteelSeries wheel troubleshooting](https://support.steelseries.com/hc/en-us/articles/24744331054989-My-Nova-7X-Nova-7X-Gen-2-ChatMix-wheel-is-not-working).

These interfaces are undocumented by SteelSeries and may change. On an incompatible payload the application logs/retries rather than changing audio or fabricating state. No packet sniffing, administrator requirement, audio control, or GG modifications are needed.


## Performance and acceptance limits

A five-second idle sample during development recorded 0.0 CPU seconds, approximately 126 MB private memory and 236 MB working set. This is not a gaming benchmark. WPF and the bundled runtime have a material baseline memory cost. Mixed-DPI transitions, sign-in startup and per-game behavior need hands-on testing. Light/dark layouts were rendered and inspected, and live wheel events were verified on the development machine with an Arctis Nova 7.

## 1.0.1 desktop integration and memory

- Start search: one stable `ChatMix Overlay.lnk` in the current-user Programs directory, using `--settings`. Target and arguments are compared before saving, so unchanged launches do not rewrite it.
- Startup: a separate stable shortcut in the current-user Startup folder, using `--background`. The checkbox applies immediately. The legacy `HKCU\...\Run\ChatMixOverlay` entry is removed after migration, avoiding a second autostart. Windows-level startup disabling is deliberately not overwritten. A live check returned one `Win32_StartupCommand` with name `ChatMix Overlay`, location `Startup`.
- Single instance: a current-user-only named pipe forwards Settings/Exit requests to the running instance. Repeated background startup does not open Settings.
- Tray menu: a NotifyIcon-owned ContextMenuStrip replaces the broken hidden-window WPF popup. The user confirmed that right-click displays the menu. Graceful shutdown was tested through the same Application.Shutdown action used by Exit.
- Memory: a matched local comparison of compressed/uncompressed 1.0.1 builds recorded about 180/91 MiB private memory after opening Settings, and 100/21 MiB beforehand. Managed live memory was about 8 MiB with Settings open; the remainder includes runtime, native UI, framework code and other allocations. No forced working-set trimming or periodic full GC is used. The executable grows to about 162 MiB, while the distribution ZIP remains compressed.
- Additional reductions: lazy overlay creation, deduplication before UI dispatch, parsing socket buffers directly, and GC memory-conservation mode.

These short observations are not a long-running leak or gaming benchmark. Actual sign-in launch, Windows Search indexing delay, and other Windows installations still need broader testing.

References: [Microsoft single-file deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/) explains that compressed bundled assemblies are decompressed into memory. [Microsoft startup documentation](https://support.microsoft.com/en-au/windows/experience/startup-boot/configure-startup-applications-in-windows) describes the per-user Startup folder and startup management.

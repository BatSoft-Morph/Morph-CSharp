# MAUI conversion of the demos

Converting the WinForms demo UIs (`MorphDemos/`) to .NET MAUI. In progress.

## Decisions (Peter, 2026-07-05)

- **Convert the demos to MAUI**, following the existing `Morph.Manager` MAUI project as the
  template (MauiProgram / App / single page / `Platforms/` / `Resources/`).
- **Platforms:** client apps target **Windows + Android**; **server apps target Windows only**.
  Rejected "Windows only" and "all platforms": servers register a service with a *local*
  daemon so they are inherently desktop; iOS/MacCatalyst need a Mac to build and can't be
  verified here.
- **Clients are pure Morph-library clients (no daemon).** A client app uses **only** the
  `Morph` library: it registers the link types and sets the worker-thread count itself (once,
  at startup) and relies on the default local `IDSeed` for IDs. It does **not** reference
  `Morph.Daemon.Client`. This is the library's intended daemon-less-device pattern, shown by
  the original Xamarin `Clique.Droid` / `BookingClientAndroid` demos. **Server apps** keep
  `Morph.Daemon.Client` / `MorphManager`, since they register their service with a (local)
  daemon. (The split applies on Windows too: the MAUI client is a pure client on both its
  platforms; only the WinForms PC client — legacy — bootstraps via `MorphManager`.)
- **Non-destructive:** MAUI apps are added **alongside** the WinForms projects (suffix
  `.Maui`), not replacing them. The user retires the WinForms versions when ready.
- **Shared demo libraries** (`Basic`, `Booking`, `CliqueInterface`, `MorphDemoSync`) are
  retargeted `net48 → netstandard2.0` so both WinForms and MAUI can reference them (they are
  pure Morph logic, no UI).
- **Sequence:** pilot with Basic first (done), then Booking, then Clique.

## Conversion recipe (established by the Basic pilot — reuse for Booking & Clique)

1. **Off-UI-thread Morph calls (client).** Every proxy call runs via `Task.Run(...)`, with UI
   reads taken *before* and UI writes done *after* the `await`. This is mandatory, not polish:
   Android throws `NetworkOnMainThreadException` for network on the UI thread.
2. **Server UI callbacks marshal to the UI thread.** Morph invokes the server's `…UI`
   interface on a background thread; WinForms used `Control.Invoke`, MAUI uses
   `MainThread.InvokeOnMainThreadAsync(func).GetAwaiter().GetResult()` (guarded by
   `MainThread.IsMainThread`).
3. **Client startup uses the `Morph` library directly, not `MorphManager`.** Register the link
   types (`End`/`Message`/`Data`/`Internet`/`Service`/`Servlet`/`Member`) and call
   `ActionHandler.SetThreadCount(2)` **once** at app startup (`MauiProgram.CreateMauiApp`) —
   this is pure-local (no network), so it needs no `Task.Run`. Only the connect itself
   (`MorphApartmentProxy.ViaString`, step 1) goes on a background thread. Shutdown is
   `SetThreadCount(0)` + `Connections.CloseAll()` on window teardown. (Server apps still call
   `MorphManager.Startup`/`Shutdown`.)
4. Use `DisplayAlertAsync`, not the obsolete `DisplayAlert`.
5. Csproj: `UseMaui`, `SingleProject`; multi-target clients use
   `<TargetFrameworks>net10.0-android;net10.0-windows10.0.19041.0</TargetFrameworks>` with
   `SupportedOSPlatformVersion` conditioned per platform; Android needs `INTERNET` permission
   in `Platforms/Android/AndroidManifest.xml`.

## Status

- **Basic — done.** `BasicServer.Maui` (Windows, uses `MorphManager`), `BasicClient.Maui`
  (Windows + Android, **pure client** — `Morph` library only, no `Morph.Daemon.Client`); both
  build clean (0 warnings), added to `Basic.sln`; WinForms Basic still builds. The dead
  `Morph.Daemon.Client` reference was also dropped from the shared `Basic` library.
- **Booking — todo.** Adds a `TreeView` → `CollectionView` (server shows a live registration tree).
- **Clique — todo.** `ListView` → `CollectionView` chat UI, built fresh as a MAUI app. (The old
  Xamarin `Clique.Droid` has been **deleted** — see Backlog item 7 — so there is nothing to fold in.)

## Status of the Android runtime path

The earlier "Android can't complete a call" note was a **mis-wiring**, not a library limitation:
the demo wrongly bootstrapped through `MorphManager` (the PC/daemon path), which eagerly
connects to a local loopback daemon Android doesn't have. `BasicClient.Maui` now follows the
proven pure-client pattern (per `Clique.Droid`), so that architectural blocker is gone.
**Still pending:** an on-device/emulator run against a live `Basic` server to confirm the round
trip end-to-end — the change is build-verified but not yet runtime-tested on Android.

# MAUI conversion of the demos

Converting the WinForms demo UIs (`MorphDemos/`) to .NET MAUI. All three demos (Basic, Booking,
Clique) are now converted and build-verified (0 warnings, both TFMs); live/on-device runtime
testing remains.

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
- **Sequence:** pilot with Basic first, then Booking, then Clique. (Doneness tracked under *Status*.)

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
   `SetThreadCount(0)` + `Connections.CloseAll()` on window teardown — a client that holds a
   server-tracked session also signs off first (see Booking under *Status*). (Server apps still
   call `MorphManager.Startup`/`Shutdown`.)
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
- **Booking — done.** `BookingServer.Maui` (Windows, `MorphManager` + `StartServiceSessioned`; the
  WinForms `treeBooking` TreeView is now a grouped `CollectionView`), `BookingClient.Maui`
  (Windows + Android, pure client). `Booking` lib retargeted to netstandard2.0; both added to
  `Booking.sln`; WinForms Booking still builds; whole `Booking.sln` builds 0/0. Notes: the pure
  client gained a **Host** entry (default `127.0.0.1`) because `ViaString` needs a target address
  (the WinForms client hard-wired the local daemon via `ViaLocal`); the server's Morph-logic
  classes (`BookingObjects`/`Factories`/`Server.cs`) are copied into `BookingServer.Maui` rather
  than shared, matching the WinForms server's own-assembly layout. On window teardown the client
  **signs off** (`MainPage.SignOff()`, invoked from `App.CreateWindow`'s `window.Destroying` before
  the `SetThreadCount(0)`/`CloseAll` teardown): it releases a held booking, then disposes the
  server apartment proxy, which sends the Morph `End` link that disposes the server's session
  apartment (`BookingRegistrationSession`) — so the server's client count decrements and it shuts
  down when the last client leaves. A socket close alone does **not** achieve this: apartments are
  not owned by connections, so `Connections.CloseAll()` closes the transport but never disposes the
  server apartment (mirrors the WinForms client's `FormClosing`). Both sign-off steps are
  best-effort so a hung server can't block the window from closing.
- **Clique — done.** `Clique.Maui` (Windows + Android) — a **daemon-less peer** built fresh (Morph
  library only, replicating the deleted Xamarin `Clique.Droid`: link types + `SetThreadCount` +
  `MorphServices.Register` via `MorphApartmentFactoryShared`). WinForms `lstFriends` ListView →
  `CollectionView`. `CliqueInterface` retargeted to netstandard2.0; added to `Clique.sln`;
  `Clique.Win` still builds; whole `Clique.sln` builds 0/0.

### Clique known limitation — no unsolicited inbound connections (decision pending)
A daemon-less Clique peer starts **no listener** (`MorphServices.Register` only registers the
service locally; the `MorphPort` listener is started only by the daemon). So a MAUI Clique instance
**cannot accept an inbound connection from a peer it has not itself dialled** — but chat still works
bidirectionally over the connection the *initiator* opened (the other peer's diplomat callbacks
route back over that same socket). Inherited from `Clique.Droid`, not a regression. To let a MAUI
peer also *accept* inbound connections, add `ListenerManager.Obtain(LinkInternet.MorphPort).StartAll();`
after `SetThreadCount(2)` in `MauiProgram` (and `…Find(…).StopAll();` on teardown). Left out to stay
faithful to the original — **awaiting Peter's decision** on whether MAUI Clique peers should listen.

## Status of the Android runtime path

**Still pending for all three demos:** an on-device/emulator run against a live server to confirm
the round trip end-to-end. The conversions are build-verified (0 warnings, both TFMs) but not yet
runtime-tested on Android. (The earlier "Android can't complete a call" scare was a mis-wiring — the
demo bootstrapped via `MorphManager`; the pure-client rule under *Decisions* above resolved it.)

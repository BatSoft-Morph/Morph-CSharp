# Morph C# — backlog and known issues

Deferred work and open questions raised while fixing defects. These are **not** decided
designs; they are notes so the context is not lost. The protocol source of truth remains
`Morph-Definition/Protocol/Morph Protocol.xlsx`.

## To do

### 1. Multidimensional arrays
`Params.ValueCodec.EncodeArray` currently throws for any array whose `Rank != 1`.
Morph represents a multidimensional array as **nested arrays** — one array `ValueType`
byte per dimension — so an N-dimensional CLR array should encode as N nested single-
dimension arrays (and `DecodeArray` should rebuild it). Implement this instead of
rejecting. (Not explicitly stated in the spreadsheet; confirmed by Peter, 2026-07-05.)

### 2. `MorphApartmentSession.GenerateReturnPath` — null client path
`GenerateReturnPath` does `_path.Clone()` directly, so it raises a `NullReferenceException`
if `_path` was never set. (A temporary `EMorph` guard was added and then removed by Peter,
2026-07-05 — the guard only masked the symptom.) The real question stands: **should this
state be reachable at all?** A session apartment being asked to build a reply before its
client path was ever set may be an upstream bug; investigate the caller chain.

## Open design questions (awaiting decision)

### 3. Sequencing subsystem needs a design pass
Point-fixes were applied (no more deadlock/NRE/leak), but two behaviours are unconfirmed:
- `SequenceReceiver.Stop(int index)` for the `IsLast` flag is implemented as a graceful
  drain-then-stop; the intended semantics were never specified.
- A message sent while `SequenceID == 0` (before the Start handshake completes) makes the
  receiving side create a **new** receiver per message.
Decide whether to do a full design pass on sequencing.

### 4. Remote `End` on a shared apartment
A client disposing its proxy sends an `End` link that the server actions as
`apartment.Dispose()`. For a **shared** apartment that tears it down for every client.
The redundant-proxy auto-trigger was fixed, but the policy question stands: should `End`
dispose only **session** apartments, and be ignored for shared ones?

### 5. `MorphManager` static constructor connects to the daemon (PC robustness only)
`MorphManager`'s static constructor creates `MorphManagerApartmentItems` → `DaemonClient` →
`ViaEndPoint`, which connects to the loopback daemon eagerly. If the daemon is down at first
touch, the type initializer throws and the class is poisoned for the process lifetime.
Consider moving the connecting initialization out of the static constructor into an
explicit/lazy step. This affects **PC apps only** — `MorphManager` (and `Morph.Daemon.Client`)
is for devices that have a daemon. A daemon-less device (Android/iOS) must not use it at all;
it uses the `Morph` library directly (register link types + `SetThreadCount`, default local
`IDSeed`). See the resolved mobile-client note below — the previously-recorded "mobile clients
are blocked" claim was a demo mis-wiring, not a library gap.

### 6. Stale daemon files
`Morph.Daemon/LinkType.LinkService.cs` and `Morph.Daemon/RunningService.cs` are not in
the csproj and describe an older architecture. Decide: delete, or keep for reference.

### 9. `ViaString` with a blank address resolves to localhost
With the `StringParser` crash fixed (see "Resolved this session"), a blank address now falls
through to `Dns.GetHostEntry("")`, which .NET resolves to **localhost** — so Connect silently
targets the local machine instead of erroring. Decide where to guard a blank address: demo-side
(each `…Connect_Click` checks its field) or, cleaner, core-side (`MorphApartmentProxy.Resolve`
rejects a blank address with a clear `EMorph`). Peter to choose; I lean core-side.

### 10. Should MAUI Clique peers accept inbound connections?
A daemon-less Clique peer starts no `ListenerManager`, so it cannot receive an *unsolicited*
inbound connection (chat still works over the connection the initiator opens). Inherited from
`Clique.Droid`. Adding `ListenerManager.Obtain(LinkInternet.MorphPort).StartAll()` in `MauiProgram`
would enable inbound. Left out to stay faithful — decision pending. Details in `Maui-Demos.md`.

### 11. Minor known issues (low priority)
- **WinForms `BookingServer` runs headless** — its `Program.Main` has `Application.Run(new
  BookingServerForm())` commented out, so it registers the service and stays alive on foreground
  worker threads but shows **no window**. It looks like it failed to launch; it hasn't. (This
  misled the "why doesn't the server show in the Manager" diagnosis — the real cause there was
  running a stale pre-migration exe.)
- **`Morph.Daemon/ServiceDaemon.cd`** class diagram still shows `DaemonStartup` without the new
  `parameters` field — cosmetic (a VS diagram, not compiled).
- **`Test.Bat.Library.Settings`** has ~3 failing tests from a hard-coded `C:\Temp\Settings.xml`
  path — environmental, pre-existing, unrelated to this session.

## Tooling and project format

All legacy `.csproj` files were migrated to SDK-style (target frameworks unchanged: net48
libraries stay net48; MSTest v1 test projects → `Microsoft.NET.Test.Sdk` + MSTest v3).
Outstanding from that migration:

### 7. Xamarin Android demo projects — deleted (resolved)
`MorphDemos/Booking/BookingClientAndroid` and `MorphDemos/Clique/Clique.Droid` were
discontinued-Xamarin projects that could not build under the current SDK (their
`Novell.MonoDroid.CSharp.targets` import is gone), so they broke `Booking.sln`/`Clique.sln`.
Per Peter's decision (2026-07-05) both project entries were removed from the solutions and the
folders deleted; the MAUI client conversions replace them. See "Resolved this session".

### 8. Output-path and platform layout — resolved
Both halves are now done (see "Resolved this session"): the flat output layout is restored
(`Directory.Build.props`), and `Basic.sln`'s x86 pin on `BasicClient`/`BasicServer` was
unified to Any CPU so every project builds to `bin\<Configuration>\` uniformly.

## Verified non-issues (do not re-flag)

From the multi-agent defect review, these were investigated and are **correct as written**:
- `Internet.Connection.Write` ignoring `Socket.Send`'s return value — the socket is blocking,
  so `Send` transmits the full count before returning.
- `MorphDaemonService.DoStop` — `ListenerManager.Find(int)` returns a non-null (possibly
  empty) `Listeners`, so it cannot NRE there.
- Any "Release / apartment needs a connection-ownership check" finding — apartments are **not**
  owned by connections (see the `morph-apartments-not-owned-by-connections` memory).

## Resolved this session
- Date/time conversion replaced with `System.Xml.XmlConvert` (`Lib.Conversion`).
- `HostURI` DNS resolution added (`LinkInternet.URIToAddress`, used by IPv4/IPv6 readers).
- All projects migrated to SDK-style; `Morph.Daemon` in particular (fixes IDE namespace
  resolution against the SDK-style `Morph` core).
- Basic demo converted to MAUI (see `Maui-Demos.md`).
- MAUI `BasicClient` reworked as a **pure client**: it uses the `Morph` library directly
  (registers link types + `ActionHandler.SetThreadCount` once in `MauiProgram`, default local
  `IDSeed`) and no longer references `Morph.Daemon.Client`. This is the library's intended
  daemon-less-device pattern (as the original Xamarin `Clique.Droid` / `BookingClientAndroid`
  demos show); the earlier "Android can't complete a call" note was a mis-wiring, not a library
  limitation. The dead `Morph.Daemon.Client` reference was also removed from the shared `Basic`
  library. (Build-verified on both TFMs; on-device runtime test still pending — see
  `Maui-Demos.md`.)
- Flat output layout restored: `Morph-CSharp/Directory.Build.props` sets
  `AppendTargetFrameworkToOutputPath=false`, so classic projects build to `bin\Debug\` (not
  `bin\Debug\net48\`). The three MAUI projects (`Morph.Manager`, `BasicClient.Maui`,
  `BasicServer.Maui`) override it back to `true` — they multi-target / carry a runtime-identifier
  folder and would collide under a flat path. All `bin`/`obj` were cleaned first, removing the
  stale pre-migration exes that had been sitting in the old `bin\Debug\` roots.
- `Basic.sln` x86 pin removed: `BasicClient`/`BasicServer` were mapped to `Debug|x86` in the
  solution (csprojs were already AnyCPU); the mappings were rewritten to Any CPU so they build to
  `bin\Debug\` like every other project. Every runnable exe now lands at `bin\<Configuration>\`
  (Debug by default, `bin\Release\` when built Release) with no TFM or platform subfolder — except
  the MAUI apps, which keep `bin\<Configuration>\<tfm>\<rid>\` by necessity.
- `Booking.sln` exes now build: `BookingClient`/`BookingServer` had the **same** defect as Basic —
  mapped to `Debug|x86` with **no `Debug|Any CPU.Build.0`**, so the solution skipped them entirely
  under its default Any CPU configuration (only `Booking.dll` built). Rewritten to Any CPU with
  `Build.0`; both now build to `bin\Debug\`.
- `Clique.sln` given the same exe-build fix: `Clique.Win` was likewise `Debug|x86` with no
  `Debug|Any CPU.Build.0`; rewritten to Any CPU so it builds to `bin\Debug\`.
- Dead Xamarin projects deleted (item 7): `BookingClientAndroid` and `Clique.Droid` entries were
  removed from `Booking.sln`/`Clique.sln` and their folders deleted. Both solutions now build
  green (0 warnings): `Booking.sln` → `BookingServer.exe` + `BookingClient.exe`; `Clique.sln` →
  `Clique.Win.exe`, all at `bin\Debug\`.
- **Startup-service persistence moved from the Manager to the daemon** (design bug fix, Peter
  2026-07-06). Previously `Morph.Manager` owned the list in `Morph.Manager.json` (added by the
  earlier "Fable 5" commit `64fe434`) and the daemon held startups only in memory — so the
  always-on daemon depended on the occasional UI for its own recovery, and lost all startups on
  restart. **Rejected** that ownership. Now the **daemon owns and persists** it (`Morph.Daemon/
  StartupStore.cs`): it loads on `DoStart` (registering each so services launch on demand) and
  rewrites on every `StartupImpl.Add`/`Remove`. Store is JSON — **Debug** build: beside the daemon
  exe (`bin\Debug\Morph.Daemon.json`); **Release** build: `%ProgramData%\Morph\Morph.Daemon.json` (chosen
  via `#if DEBUG`). The Manager is now a thin UI over the daemon's `Morph.Startup` service
  (`Morph.Manager/Services/StartupStore.cs` deleted; `StartupsViewModel` lists/adds/removes only
  through the daemon). The wire `DaemonStartup` struct gained a `parameters` field (both ends) so
  the daemon's `ListServices` is a faithful source — that missing field was the only reason the
  Manager had kept its own copy. `System.Text.Json` added to the net48 daemon (runtime-verified).
  Design principle affirmed: **Morph.Manager is UI only; the daemon owns all persistent state.**
  Lineage: the *original* daemon persisted startups in the registry at
  `HKEY_CURRENT_USER\Software\Morph\Startups` (per-service `Filename`/`Parameters`/`Timeout`),
  added at `3572558` and removed at `64fe434`. The new JSON store keeps the **identical fields**,
  so nothing is lost; it also fixes the old per-user `HKCU` scope (wrong for a service) by using
  machine-wide `%ProgramData%\Morph\` in Release. No registry remnants remain (verified).
  (Store file is `Morph.Daemon.json`.)
- Booking and Clique demos converted to MAUI (see `Maui-Demos.md`): `BookingServer.Maui` (Windows,
  MorphManager + `StartServiceSessioned`, TreeView → grouped `CollectionView`) + `BookingClient.Maui`
  (Windows+Android, pure client); `Clique.Maui` (Windows+Android, daemon-less peer, ListView →
  `CollectionView`). Shared libs `Booking` and `CliqueInterface` retargeted `net48 → netstandard2.0`.
  `Booking.sln` and `Clique.sln` build green end-to-end (0/0); WinForms and Basic still build.
  Build-verified only; on-device runtime pending. Open follow-ups: Clique inbound listener (item 10);
  Booking server Morph-logic duplicated into `.Maui` rather than shared.
- **`ViaString` no longer throws on a blank/invalid address** (core bug, Peter 2026-07-06). The
  best-effort IPv4 parser (`MorphApartmentProxy.ResolveIPv4`) is meant to return null so `Resolve`
  falls back to DNS, but `StringParser.ReadChars`/`ReadChar` threw `StringParserException("End of
  string…")` at end-of-string — so an empty (or trailing-dot) address crashed Connect in every demo
  instead of resolving. Fixed at the parser: those two *try-to-read* methods now return null/false at
  end-of-string (used **only** by the IPv4 resolver, so zero blast radius). Regression tests added
  (`StringParserTests`, 4). Not a regression from this session — `ReadChars` had thrown at end since
  the original commit. Follow-up: blank-address behaviour (item 9).

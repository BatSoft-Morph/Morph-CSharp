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

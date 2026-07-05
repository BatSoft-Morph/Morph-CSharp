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
The method now guards `_path == null` with a thrown `EMorph` (the original would have
raised a `NullReferenceException`). The real question: **should this state be reachable
at all?** A session apartment being asked to build a reply before its client path was
ever set may be an upstream bug. Investigate the caller chain rather than treating the
guard as the fix.

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

### 5. `MorphManager` static constructor connects to the daemon
The static constructor creates `MorphManagerApartmentItems` → `DaemonClient` →
`ViaEndPoint`, which connects. If the daemon is down at first touch, the type initializer
throws and the class is poisoned for the process lifetime. Consider moving the connecting
initialization out of the static constructor into an explicit/lazy step.

### 6. Stale daemon files
`Morph.Daemon/LinkType.LinkService.cs` and `Morph.Daemon/RunningService.cs` are not in
the csproj and describe an older architecture. Decide: delete, or keep for reference.

## Resolved this session
- Date/time conversion replaced with `System.Xml.XmlConvert` (`Lib.Conversion`).
- `HostURI` DNS resolution added (`LinkInternet.URIToAddress`, used by IPv4/IPv6 readers).

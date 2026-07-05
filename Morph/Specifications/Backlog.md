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

### 3. Date/time conversion — prefer a standard routine
`Lib.Conversion.DateTimeToStr` / `StrToDateTime` are hand-rolled (and were the source of
several bugs). `System.Xml.XmlConvert` already implements the xmlschema date/time format
the spec references, and `SimpleType` already uses `XmlConvert` for `TimeSpan`/duration.
Consider replacing both methods with `XmlConvert`, **keeping the Morph rules**: a local
time carries no zone suffix, an instant carries `Z`, and timezone offsets are not emitted
("not supported, for the sake of simple Morph implementations"). Note that the current
offset-parsing branch handles a case the spec says is unsupported.

### 4. `HostURI` — no DNS resolution
`Internet.LinkInternetIPv4/IPv6.ReadNew` parse the string host with `IPAddress.Parse`,
which accepts only a literal IP address — it does **not** do a DNS lookup. If the
`HostURI` field can ever carry a domain name, this needs `Dns.GetHostAddresses(...)`.
Clarify whether the client always resolves to an IP literal before a host reaches the
wire (client-side `MorphApartmentProxy.Resolve` already does DNS), or whether domain
names travel in `HostURI`.

## Open design questions (awaiting decision)

### 5. Sequencing subsystem needs a design pass
Point-fixes were applied (no more deadlock/NRE/leak), but two behaviours are unconfirmed:
- `SequenceReceiver.Stop(int index)` for the `IsLast` flag is implemented as a graceful
  drain-then-stop; the intended semantics were never specified.
- A message sent while `SequenceID == 0` (before the Start handshake completes) makes the
  receiving side create a **new** receiver per message.
Decide whether to do a full design pass on sequencing.

### 6. Remote `End` on a shared apartment
A client disposing its proxy sends an `End` link that the server actions as
`apartment.Dispose()`. For a **shared** apartment that tears it down for every client.
The redundant-proxy auto-trigger was fixed, but the policy question stands: should `End`
dispose only **session** apartments, and be ignored for shared ones?

### 7. `MorphManager` static constructor connects to the daemon
The static constructor creates `MorphManagerApartmentItems` → `DaemonClient` →
`ViaEndPoint`, which connects. If the daemon is down at first touch, the type initializer
throws and the class is poisoned for the process lifetime. Consider moving the connecting
initialization out of the static constructor into an explicit/lazy step.

### 8. Stale daemon files
`Morph.Daemon/LinkType.LinkService.cs` and `Morph.Daemon/RunningService.cs` are not in
the csproj and describe an older architecture. They held the only prior copy of the
access-control logic (now implemented in the compiled `RegisteredServices.Daemon.cs`).
Decide: delete, or keep for reference.

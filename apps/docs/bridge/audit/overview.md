# Bridge Audit Infrastructure

`Sunfish.Kernel.Audit.InMemoryAuditTrail` provides synchronous audit emission for Bridge v1. Route handlers call `_auditTrail.AppendAsync(record, ct)`; a failed audit emission rejects the inbound event with **500 + audit-emission-failed** (W#32 both-or-neither).

> **v1 known limitation:** Bridge audit infrastructure is in-memory only; restarts lose history. Persistent audit storage is deferred to a follow-up ADR (~ADR 0076 area).

## DI registration

```csharp
services.AddSingleton<IAuditTrail, InMemoryAuditTrail>();
```

No event stream fanout in v1 — audit records flow directly to `IAuditTrail.AppendAsync`.

## Audit record shape

`TenantId` is sourced from the pairing-token JWT's `tenantId` claim (W#23 P0 pairing surface). See ADR 0049 for the canonical `AuditRecord` schema.

## What it gives you

| Type | Role |
|---|---|
| `IAuditTrail` | Append-only audit record sink per ADR 0049. |
| `InMemoryAuditTrail` | v1 implementation — in-memory, volatile, no retention limit. |

## See also

- ADR 0049 — audit-trail substrate (canonical `AuditRecord` schema + conventions)
- W#23 P4.5 hand-off — `property-ios-field-app-stage06-p4-5-audit-unblock-addendum.md`
- ~ADR 0076 — Bridge Audit Infrastructure (persistent) — deferred follow-up

# iOS Inspections Walkthrough Capture Flow

**Workstream:** W#23.3 | **Status:** Phases 1–3 shipped; Phase 4 = this document

---

## Overview

The iOS field-capture app implements a structured inspection walkthrough for property field
inspectors. The flow covers scheduling, in-progress checklist responses, deficiency recording,
equipment condition assessment, and completion — all via an offline-first event queue that
syncs to the Bridge when connectivity is available.

---

## Event types

All field actions emit events through `EventQueueServicing`. Events are serialized with
`JsonCanonical` (canonical JSON per `Sunfish.Foundation.Crypto.CanonicalJson`), wrapped in
`EventEnvelope`, and persisted to the GRDB `event_queue` table for sync to Bridge.

| Swift EventType | Bridge handler | Payload fields |
|---|---|---|
| `InspectionStarted` | `HandleInspectionStartedAsync` | `inspectionId`, `propertyId`, `templateId?` |
| `ChecklistResponseRecorded` | `HandleChecklistResponseRecordedAsync` | `inspectionId`, `itemId`, `response`, `note?` |
| `DeficiencyRecorded` | `HandleDeficiencyRecordedAsync` | `inspectionId`, `itemId`, `description`, `severity`, `photoRef?` |
| `EquipmentConditionRecorded` | `HandleEquipmentConditionRecordedAsync` | `inspectionId`, `equipmentId`, `rating`, `note?`, `photoRef?` |
| `InspectionCompleted` | `HandleInspectionCompletedAsync` | `inspectionId`, `completedAt` |
| `InspectionScheduled` | pending (halt-condition HC-TemplateId) | `propertyId`, `inspectorName`, `scheduledFor` |

---

## Checklist response semantics

`ChecklistResponse` values (wire strings): `pass`, `fail`, `na`.

- `pass` — item checked, no deficiency
- `fail` — item has a deficiency; "Add Deficiency" link appears in `InspectionDetailView`
- `na` — not applicable for this property (e.g., pool inspection on a property with no pool)

All three count as "responded" for the progress bar (`respondedItems / totalItems`). The
"Complete Inspection" button activates when all items are responded.

---

## Deficiency severity guide

`DeficiencySeverity` values (wire strings, must be case-exact): `Low`, `Medium`, `High`, `Critical`.

| Severity | Examples |
|---|---|
| `Low` | Cosmetic issue, minor wear, no urgency |
| `Medium` | Functional but degraded; schedule repair within 30 days |
| `High` | Safety or habitability risk; repair within 7 days |
| `Critical` | Immediate danger; requires same-day or emergency response |

---

## Equipment condition rating guide

`ConditionRating` values (wire strings, must be case-exact): `Good`, `Fair`, `Poor`, `Failed`.

| Rating | Description |
|---|---|
| `Good` | Fully operational; no maintenance action needed |
| `Fair` | Functional with minor wear; routine maintenance scheduled |
| `Poor` | Degraded performance; maintenance required soon |
| `Failed` | Not operational; replacement or immediate repair needed |

---

## Offline behavior

### Inspection list

`InspectionListView` fetches from `GET /api/v1/field/inspections`. On success, it writes the
result to the GRDB `inspections` table (Phase 3 V2Migration). On fetch failure:

1. If the GRDB `inspections` table has rows, the cached list is displayed.
2. An offline banner appears (`wifi.slash` icon): "Showing cached inspections"
3. If `cached_at > 24h`, the banner turns orange with a `exclamationmark.triangle.fill` icon
   and reads "Cached data is over 24h old"

### Event queue

All captured events (checklist responses, deficiencies, equipment conditions, etc.) are
written to the GRDB `event_queue` table immediately at capture time — the network is not
required. The sync engine drains the queue in the background when connectivity is restored.

### Photo attachments

Photos (deficiencies, equipment condition) are stored in the `BlobStore`
(content-addressed local file store) before the event envelope is queued. The `blobRef`
field in the event payload is the SHA-256 hex content address. On sync, the blob is
uploaded via `POST /api/v1/field/blob/{sha256}` before the event is dispatched.

---

## Cross-tenant security

All inspection event handlers enforce a cross-tenant guard:

```
existing.UnitId.Authority != envelope.TenantId.Value → 400 "inspection-not-found"
```

This is a uniform 404-sentinel per ADR 0092 §A3: a cross-tenant access attempt returns the
same error as a missing inspection, preventing tenant enumeration.

---

## Screen flow

```
InspectionListView
  └─ [tap scheduled] → InspectionDetailView
        ├─ "Start Inspection" → emits InspectionStarted
        ├─ Checklist items → emit ChecklistResponseRecorded
        │     └─ [fail] → RecordDeficiencySheet → emits DeficiencyRecorded
        ├─ Equipment section → EquipmentConditionView
        │     └─ [Assess] → EquipmentConditionSheet → emits EquipmentConditionRecorded
        └─ "Complete Inspection" → emits InspectionCompleted
  └─ [tap "+"] → NewInspectionSheet → emits InspectionScheduled (Phase 3)
```

---

## Related

- Hand-off: `icm/_state/handoffs/property-ios-field-app-stage06-w23-3-inspections-handoff.md`
- Bridge handlers: `signal-bridge/Sunfish.Bridge/Field/InspectionEventHandler.cs`
- Bridge DTOs: `signal-bridge/Sunfish.Bridge/Field/InspectionFieldDtos.cs`
- Domain service: `packages/blocks-inspections/Services/IInspectionsService.cs`
- ADR 0092: cross-tenant guard and error-response discipline

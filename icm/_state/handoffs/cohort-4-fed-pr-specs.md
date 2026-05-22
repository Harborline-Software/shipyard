# Cohort-4 FED PR-by-PR detail specs

**Authored by:** ONR (V9 batch item #1)
**Requester:** Admiral (per `admiral-directive-2026-05-22T15-35Z` item V9 #1; carryover from V7 #1 + V8 #1)
**Authored at:** 2026-05-22T15-45Z
**Workstream:** W#78 cohort-4 (audit-trail viewer; ADR 0094 substrate; ADR 0093 Stage-05 first pilot)

---

## 1. Purpose + scope

This document decomposes cohort-4 FED-side delivery into PR-by-PR acceptance criteria.
Cohort-4 is the ADR 0093 Stage-05 Adversarial Review **first pilot**, so its retro
(per V7 #5 scaffold) compares against cohorts 1/2/3 baseline.

**Cohort-4 FED state at V9 #1 authoring time (2026-05-22T15:45Z):**

- **sunfish#58** — `audit-events.ts` TypeScript type stubs — OPEN (gates sunfish#59 rebind)
- **sunfish#59** — `AuditEventsPage` skeleton + `/audit-trail` route — Ready (DRAFT-ahead;
  pair-gated on Engineer PR 0; pattern-009 PAIR pattern; merge gate held)
- **sunfish#61** — `ErrorCard.tsx` + `LoadingState.tsx` shared components — OPEN
- **Bridge endpoint** — NOT YET OPEN (gated on shipyard ADR 0094 Step 1 substrate PR)
- **shipyard ADR 0094 Step 1 substrate** — NOT YET OPEN (Engineer's W#78 queue position; per V9 #1 R1 informational flag)

**Council verdicts on sunfish#59 (DRAFT-ahead skeleton):**

- **sec-eng SPOT-CHECK** (2026-05-22T14:45Z): **AMBER** — A1 + A2 forward to FED PR 1
- **frontend-architect SPOT-CHECK** (2026-05-22T12:13Z): **GREEN** — 7 nits forward to FED PR 1
- **Combined posture**: PAIR DRAFT-ahead working as designed; merge gate held;
  acceptance-criteria forward-watched into FED PR 1.

---

## 2. PR sequencing

The pattern-009 PAIR pattern requires Bridge + Frontend halves merge together
(or the live-rebind frontend half merges paired with the Bridge endpoint PR).
Cohort-4 sequence:

```
1. shipyard ADR 0094 Step 1 substrate PR    [Engineer; ~2-3 days; NOT OPEN]
   └──┐
2. signal-bridge Engineer PR 0               [Engineer; ~1-2 days; NOT OPEN]
      └─consumes IAuditEventReader────┐
3. sunfish#58 audit-events.ts          [FED; OPEN; can merge anytime]
                                       │
4. sunfish FED PR 1 (live-rebind)      [FED; depends on 1+2+3; this spec]
   └─pair with #2 via auto-merge fire─┘
5. sunfish#59 (DRAFT-ahead, current)   [FED; auto-merge fires when 1+2+3+4 land]
6. (Optional) sunfish FED PR 2         [FED; detail-page polish; §6 below]
```

**Merge gate posture per pattern-009:**
- sunfish#59 auto-merge stays disarmed until steps 1+2+3+4 ship
- Once Engineer PR 0 ships paired with FED PR 1 (live-rebind), sunfish#59
  flips auto-merge ON
- FED PR 2 (optional polish) ships on its own cadence post-pair-merge

---

## 3. FED PR 1 — Live API rebind + cursor pagination + filter wiring

**Branch suggestion:** `feat/cohort4-audit-events-live-rebind`
**Estimated lines:** ~600-900 (3-4 files; tests included)
**Estimated effort:** 2-3 days FED time
**Council SPOT-CHECK on Ready-flip:** sec-eng (mandatory; A1 + A2 close gate) +
frontend-architect (mandatory; nits 1+4 close gate)

### 3.1 Files touched

| File | Action | Notes |
|---|---|---|
| `sunfish/apps/web/src/pages/audit-trail/AuditEventsPage.tsx` | rewrite (live data) | Remove `MOCK_EVENTS`; consume `useAuditEvents` hook |
| `sunfish/apps/web/src/api/audit-events.ts` | extend | Add `useAuditEvents` TanStack hook + cursor pagination state |
| `sunfish/apps/web/src/pages/audit-trail/AuditEventDetailPage.tsx` | extend | Live-rebind from stub; structured render of TBV 5-field payload |
| `sunfish/apps/web/src/pages/audit-trail/__tests__/AuditEventsPage.test.tsx` | new | ~10 RTL tests per §3.4 |
| `sunfish/apps/web/src/pages/audit-trail/__tests__/AuditEventDetailPage.test.tsx` | new | ~6 RTL tests per §3.4 |
| `sunfish/apps/web/src/pages/audit-trail/components/SignatureBadge.tsx` | delete | Replaced by `<Badge variant>` from `@/components/ui/badge` per nit 1 |

### 3.2 `useAuditEvents` hook spec

Located in `apps/web/src/api/audit-events.ts`:

```typescript
import { useInfiniteQuery } from '@tanstack/react-query';
import type { AuditEventSummary, AuditEventListResponse, AuditEventFilters } from './audit-events.types';

export function useAuditEvents(filters: AuditEventFilters) {
  return useInfiniteQuery({
    queryKey: ['audit-events', filters],
    queryFn: async ({ pageParam }) => {
      const params = new URLSearchParams();
      if (filters.from) params.append('from', filters.from);
      if (filters.to) params.append('to', filters.to);
      if (filters.eventType) params.append('event_type', filters.eventType);
      if (filters.correlationId) params.append('correlation_id', filters.correlationId);
      if (filters.severity) params.append('severity', filters.severity);  // A2 — new param
      if (pageParam) params.append('cursor', pageParam);

      const response = await fetch(`/api/v1/audit-events?${params}`, {
        credentials: 'include',
        headers: { 'Accept': 'application/json' },
      });

      // G1 — 400 tenant_changed → reload + reset cursor; do NOT keep failed cursor
      if (response.status === 400) {
        const body = await response.json();
        if (body.error === 'tenant_changed_reload_page') {
          throw new TenantChangedError();
        }
      }
      if (!response.ok) throw new Error(`Audit events fetch failed: ${response.status}`);
      return response.json() as Promise<AuditEventListResponse>;
    },
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.next_cursor ?? undefined,
    // G1 — cursor never logged
    meta: { logCursor: false },
  });
}

export class TenantChangedError extends Error {
  readonly cause = 'tenant_changed';
}
```

**A2 — Severity filter wiring:** The `severity` query-string param is NEW (added by
this PR to address A2). Wire contract:
- Param values: `Security` | `Financial` | `Messaging` | `Authentication` | `All`
- Server filters event_type prefix-match; client passes opaque string
- Type extension in `audit-events.types.ts`: add `severity?: 'Security' | 'Financial' | 'Messaging' | 'Authentication'`

**G1 — Cursor discipline (sec-eng GREEN-w-note):**
- Cursor passed verbatim as query-string param; no URL re-encoding, no JSON-parse
- `meta: { logCursor: false }` flag in TanStack query meta; companion logger
  middleware to skip cursor field if/when telemetry sink is added
- On TenantChangedError: reset pagination state to page 1; refetch with no cursor

### 3.3 Page rewrite spec — `AuditEventsPage.tsx`

```tsx
import { useState } from 'react';
import { useAuditEvents, TenantChangedError } from '@/api/audit-events';
import { Badge } from '@/components/ui/badge';
import { ErrorCard } from '@/components/ErrorCard';
import { LoadingState } from '@/components/LoadingState';

export function AuditEventsPage() {
  const [filters, setFilters] = useState<AuditEventFilters>({
    from: undefined,
    to: undefined,
    eventType: undefined,
    correlationId: undefined,
    severity: undefined,  // A2 — NEW
  });

  const { data, error, isLoading, fetchNextPage, hasNextPage, refetch } = useAuditEvents(filters);

  // G1 — TenantChangedError handling
  if (error instanceof TenantChangedError) {
    // Reset cursor state; full page reload to re-establish tenant context
    setFilters({ ...filters });  // forces refetch
    return <LoadingState message="Tenant changed. Reloading..." />;
  }

  if (isLoading) return <LoadingState message="Loading audit events..." />;
  if (error) return <ErrorCard error={error} retry={refetch} />;

  const events = data?.pages.flatMap(page => page.events) ?? [];

  return (
    <div className="space-y-4">
      <FilterBar filters={filters} onChange={setFilters} />
      <EventsTable events={events} />
      {hasNextPage && (
        <button onClick={() => fetchNextPage()} className="...">Load more</button>
      )}
      {/* Nit 2: hide button when next_cursor === null */}
    </div>
  );
}

function EventsTable({ events }: { events: AuditEventSummary[] }) {
  return (
    <table aria-label="Audit events" className="w-full">
      <thead>
        <tr>
          <th className="px-4 py-3 text-xs uppercase tracking-wide">Audit ID</th>
          <th>Occurred</th>
          <th>Event Type</th>
          <th>Actor</th>
          <th>Signature</th>
        </tr>
      </thead>
      <tbody>
        {events.map(ev => <EventRow key={ev.audit_id} event={ev} />)}
      </tbody>
    </table>
  );
}

function EventRow({ event }: { event: AuditEventSummary }) {
  // A2 — Severity coloring based on event_type prefix
  const severity = event.event_type.split('.')[0];
  const isCritical = severity === 'Security' && (
    event.event_type === 'Security.TenantBoundaryViolation' ||
    event.event_type === 'Security.AuthenticationFailed'
  );

  const rowClass = isCritical
    ? 'bg-red-50 hover:bg-red-100 cursor-pointer'   // A2 — red-tinted
    : 'hover:bg-gray-50 cursor-pointer';
  const cellClass = isCritical
    ? 'font-mono text-red-700'   // A2 — red event-type cell text
    : 'font-mono text-gray-700';

  return (
    <tr
      tabIndex={0}
      role="button"
      onClick={() => navigate(`/audit-trail/${event.audit_id}`)}
      onKeyDown={(e) => e.key === 'Enter' || e.key === ' '
        ? navigate(`/audit-trail/${event.audit_id}`)
        : null}
      aria-label={`Audit event ${event.audit_id.slice(-8)} — ${event.event_type}`}
      className={rowClass}
    >
      <td className="px-4 py-3">{event.audit_id.slice(-8).toUpperCase()}</td>
      <td>{formatRelative(event.occurred_at)}</td>
      <td className={cellClass}>
        {event.event_type}
        {isCritical && <Badge variant="destructive" className="ml-2 text-xs">HIGH</Badge>}
      </td>
      <td>{event.actor}</td>
      <td><SignatureBadgeUsingBadge state={event.signature_state} /></td>
    </tr>
  );
}

// Nit 1 — Use canonical Badge from @/components/ui/badge
function SignatureBadgeUsingBadge({ state }: { state: SignatureState }) {
  switch (state) {
    case 'Verified':
      return <Badge variant="success">✓ Verified</Badge>;
    case 'VerificationFailed':
      return <Badge variant="destructive">⚠ Failed</Badge>;
    case 'NotSigned':
      return <Badge variant="outline">— Not signed</Badge>;
  }
}
```

### 3.4 Filter bar spec — `FilterBar.tsx`

```tsx
// Nit 4 — Wire htmlFor/id on label+input pairs for screen-reader association
// Nit 5 — Remove no-op "Filter" button (filter state is reactive)

function FilterBar({ filters, onChange }: { filters: AuditEventFilters; onChange: (f: AuditEventFilters) => void }) {
  return (
    <div className="flex flex-wrap gap-2 items-end p-4 bg-gray-50 rounded">
      <div>
        <label htmlFor="audit-filter-from" className="text-xs">From</label>
        <input
          id="audit-filter-from"
          type="date"
          value={filters.from ?? ''}
          onChange={(e) => onChange({ ...filters, from: e.target.value || undefined })}
        />
      </div>
      <div>
        <label htmlFor="audit-filter-to" className="text-xs">To</label>
        <input
          id="audit-filter-to"
          type="date"
          value={filters.to ?? ''}
          onChange={(e) => onChange({ ...filters, to: e.target.value || undefined })}
        />
      </div>
      <div>
        <label htmlFor="audit-filter-event-type" className="text-xs">Event type</label>
        <select
          id="audit-filter-event-type"
          value={filters.eventType ?? ''}
          onChange={(e) => onChange({ ...filters, eventType: e.target.value || undefined })}
        >
          <option value="">All event types</option>
          {KNOWN_AUDIT_EVENT_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
        </select>
      </div>
      <div>
        {/* A2 — NEW: Severity filter */}
        <label htmlFor="audit-filter-severity" className="text-xs">Severity</label>
        <select
          id="audit-filter-severity"
          value={filters.severity ?? ''}
          onChange={(e) => onChange({ ...filters, severity: e.target.value as AuditEventFilters['severity'] || undefined })}
        >
          <option value="">All severities</option>
          <option value="Security">Security only</option>
          <option value="Financial">Financial only</option>
          <option value="Messaging">Messaging only</option>
          <option value="Authentication">Authentication only</option>
        </select>
      </div>
      <div>
        <label htmlFor="audit-filter-correlation" className="text-xs">Correlation ID</label>
        <input
          id="audit-filter-correlation"
          type="text"
          placeholder="UUID..."
          value={filters.correlationId ?? ''}
          onChange={(e) => onChange({ ...filters, correlationId: e.target.value || undefined })}
        />
      </div>
      {/* Nit 5 — no-op Filter button REMOVED (filter state is reactive via onChange) */}
      <button onClick={() => onChange({})}>Clear</button>
    </div>
  );
}
```

### 3.5 Detail-page spec — `AuditEventDetailPage.tsx` rewrite

```tsx
import { Link, useParams } from 'react-router';
import { useAuditEventDetail } from '@/api/audit-events';
import { Badge } from '@/components/ui/badge';

// A1 — Structured render of 5-field TBV payload (NOT raw JSON dump)
// A1 — Defense-in-depth tenant-assertion against active ITenantContext
// Nit 7 — Use <Link> instead of <button onClick={navigate}>

export function AuditEventDetailPage() {
  const { auditId } = useParams<{ auditId: string }>();
  const { data: detail, error, isLoading } = useAuditEventDetail(auditId!);

  if (isLoading) return <LoadingState message="Loading audit event..." />;
  if (error) return <ErrorCard error={error} />;
  if (!detail) return null;

  // A1 — Defense-in-depth client-side tenant-assertion
  // The server IS the security boundary; this client guard surfaces a regression
  // before it hits demo. Console warning (not crash).
  const activeTenant = useActiveTenant();
  if (detail.tenant_id !== activeTenant.id) {
    console.warn(
      'Audit event tenant_id mismatch — server bug suspected',
      { eventTenant: detail.tenant_id, activeTenant: activeTenant.id }
    );
    return <ErrorCard message="Audit event unavailable" />;  // Degraded fallback
  }

  return (
    <div className="space-y-4">
      <Link to="/audit-trail" className="text-sm text-blue-600">
        ← Audit trail
      </Link>
      <h1>{detail.event_type}</h1>
      <dl>
        <dt>Audit ID</dt><dd className="font-mono">{detail.audit_id}</dd>
        <dt>Occurred at</dt><dd>{detail.occurred_at}</dd>
        <dt>Actor</dt><dd>{detail.actor}</dd>
        <dt>Signature</dt><dd><SignatureBadge state={detail.signature_state} /></dd>
        <dt>Correlation ID</dt><dd className="font-mono">{detail.correlation_id}</dd>
      </dl>

      {/* A1 — Structured render per event type */}
      {detail.event_type === 'Security.TenantBoundaryViolation' && (
        <TenantBoundaryViolationPayload payload={detail.payload} />
      )}
      {detail.event_type === 'Security.AuthenticationFailed' && (
        <AuthenticationFailedPayload payload={detail.payload} />
      )}
      {/* etc., one render component per known event type */}
      {!isKnownEventType(detail.event_type) && (
        <UnknownPayloadRender payload={detail.payload} />
      )}
    </div>
  );
}

// A1 — Labeled field-name + value pairs (NOT raw JSON dump)
// Each field flows through known render path; future field addition (e.g., 6th leak) is visible at code review
function TenantBoundaryViolationPayload({ payload }: { payload: Record<string, unknown> }) {
  return (
    <div className="bg-red-50 border border-red-200 rounded p-4">
      <h2 className="text-red-800 font-semibold">Cross-tenant boundary violation</h2>
      <dl className="mt-2 space-y-1">
        <dt>Entity type</dt><dd>{String(payload.entity_type ?? '—')}</dd>
        <dt>Entity ID</dt><dd className="font-mono">{String(payload.entity_id ?? '—')}</dd>
        <dt>Requested tenant</dt><dd className="font-mono">{String(payload.requested_tenant ?? '—')}</dd>
        <dt>Actual tenant</dt><dd className="font-mono">{String(payload.actual_tenant ?? '—')}</dd>
        <dt>Correlation ID</dt><dd className="font-mono">{String(payload.correlation_id ?? '—')}</dd>
      </dl>
    </div>
  );
}
```

### 3.6 Test expectations — `AuditEventsPage.test.tsx`

Minimum 10 React Testing Library tests:

| # | Test | Closes |
|---|---|---|
| 1 | `renders loading state` | (baseline) |
| 2 | `renders empty-state when no events` | (baseline) |
| 3 | `renders event rows from useAuditEvents data` | (baseline) |
| 4 | `Security.TenantBoundaryViolation row renders with red-tinted styling + HIGH badge` | A2 |
| 5 | `Financial.InvoicePosted row renders with default styling` | A2 |
| 6 | `severity filter narrows visible events to Security only` | A2 |
| 7 | `clicking event row navigates to /audit-trail/:auditId` | (baseline) |
| 8 | `next_cursor=null hides Load more button` | Nit 2 |
| 9 | `400 tenant_changed_reload_page response resets cursor + refetches` | G1 |
| 10 | `cursor is not logged to console` (mock console.log + assert no cursor strings) | G1 |
| 11 | `htmlFor on filter labels matches input id` (a11y) | Nit 4 |
| 12 | `no decorative no-op Filter button in DOM` | Nit 5 |

### 3.7 Test expectations — `AuditEventDetailPage.test.tsx`

Minimum 6 RTL tests:

| # | Test | Closes |
|---|---|---|
| 1 | `renders TenantBoundaryViolation 5-field payload as labeled list (not raw JSON)` | A1 |
| 2 | `payload field names match canonical ADR 0094 contract: entity_type, entity_id, requested_tenant, actual_tenant, correlation_id` | A1 |
| 3 | `tenant_id mismatch triggers console.warn + degraded fallback (does NOT render)` | A1 |
| 4 | `back link uses <Link to="..."> not <button>` | Nit 7 |
| 5 | `unknown event type falls back to UnknownPayloadRender` | (baseline) |
| 6 | `AuditTrailPage_CursorTenantMismatch_400_TriggersReload` | A1 |

### 3.8 PR description acceptance criteria (FED authors)

Cohort-4 FED PR 1 description MUST include:

```markdown
## Closes acceptance criteria from sunfish#59 council verdicts

### sec-eng (council-verdict-2026-05-22T1445Z)
- [x] A1 — TenantBoundaryViolation structured render with labeled fields
- [x] A1 — Client-side tenant-assertion (defense-in-depth) with console.warn + degraded fallback
- [x] A1 — Test: AuditTrailPage_CursorTenantMismatch_400_TriggersReload
- [x] A2 — Row-level severity coloring for Security.* events
- [x] A2 — Severity filter dropdown (Security/Financial/Messaging/Authentication)
- [x] A2 — Filter button removed (state is reactive via onChange) per Nit 5
- [x] G1 — Cursor never logged (meta.logCursor=false; verified by test)
- [x] G1 — Cursor passed verbatim as query-string (no URL re-encoding)
- [x] G1 — 400 tenant_changed resets to page 1; failed cursor discarded

### frontend-architect (council-verdict-2026-05-22T1213Z)
- [x] Nit 1 — Inline SignatureBadge replaced with <Badge variant="success|destructive|outline">
- [x] Nit 2 — Load more button hidden when next_cursor === null
- [x] Nit 4 — htmlFor/id wired on all filter label+input pairs
- [x] Nit 5 — No-op Filter button removed
- [x] Nit 7 — Detail-page <button onClick={navigate}> replaced with <Link to="/audit-trail">
- [ ] Nit 3 — Sticky thead (deferred; page_size 50 doesn't justify yet)
- [ ] Nit 6 — i18n string extraction (deferred until apps/web i18n foundation lands)
```

### 3.9 Pattern conformance

- **pattern-009** (Bridge endpoint + frontend rebind pair) — formal — this PR's
  raison d'être; pair-merge with Engineer PR 0
- **pattern-009-cohort-4-audit-pair** (candidate; cohort-4 qualifying instance) —
  if this is the 3rd qualifying instance, ratification may follow

### 3.10 Pre-flight checks before Ready-flip

Per fleet-conventions §SPOT-CHECK + cohort-4 hand-off §3:

- [ ] sunfish#58 MERGED (audit-events.ts types live)
- [ ] sunfish#61 MERGED (ErrorCard + LoadingState live; Nit 4 ErrorCard consumption viable)
- [ ] Engineer PR 0 OPEN + CI-green (Bridge endpoint family live)
- [ ] shipyard ADR 0094 Step 1 substrate PR MERGED (IAuditEventReader live)
- [ ] All 12 + 6 = 18 RTL tests passing locally
- [ ] PR description includes acceptance criteria checklist per §3.8
- [ ] pattern-009 PAIR SPOT-CHECK MANDATORY: sec-eng + frontend-architect dispatched within 30 min

---

## 4. FED PR 2 — Detail page polish (OPTIONAL; ships post-pair-merge)

**Branch suggestion:** `feat/cohort4-audit-detail-polish`
**Estimated lines:** ~150-200
**Estimated effort:** 0.5-1 day FED time
**Council SPOT-CHECK on Ready-flip:** sec-eng (A1 deep-dive on PII masking) + frontend-architect (UX polish)
**Optional:** YES — items deferred from FED PR 1 may not justify a separate PR

### 4.1 Scope

Per cohort-4 hand-off §6.1, FED PR 2 was originally scoped as the standalone detail-page PR. Since FED PR 1 (this V9 #1 spec) now consumes the detail-page rewrite for A1 closure, FED PR 2 is reduced to:

- **`[Pii]` field masking pass** (cohort-4 hand-off §6.1; forward-watched in FED PR 1)
- **Print stylesheet** (frontend-architect Nit 10 — AMBER-light deferred from FED PR 1)
- **Severity-prefix tag** as a separate column or filter chip (V3 #8 follow-on if not addressed in FED PR 1)

### 4.2 `[Pii]` field masking spec

```tsx
// In TenantBoundaryViolationPayload component (and any future payload renderer):
// Default unmask; show masked label + reveal button gated on operator role

function MaskedField({ label, value, isPii }: { label: string; value: string; isPii: boolean }) {
  const [revealed, setRevealed] = useState(false);
  if (!isPii || revealed) return <dd>{value}</dd>;
  return (
    <dd>
      <span className="text-gray-400">●●●●●●</span>
      <button onClick={() => setRevealed(true)}>Reveal</button>
    </dd>
  );
}

// Server-side tagging via response field convention:
// payload.entity_id_masked: boolean   (server signals which fields are PII-tagged)
```

**Forward-watch:** Server-side tagging convention is per ADR 0049 / 0094; if the
server doesn't tag yet, default-unmask + log to fleet cerebrum as a substrate gap.

### 4.3 Print stylesheet

```css
/* apps/web/src/styles/print.css (NEW) */
@media print {
  nav, .filter-bar, button[data-export-csv] { display: none; }
  tr.cursor-pointer { cursor: default; }
  tr.hover\:bg-gray-50:hover { background: transparent; }
  .signature-badge { print-color-adjust: exact; }
}
```

### 4.4 Test expectations

- `[Pii]` masking: 3 RTL tests (default-masked, reveal-button-toggles, server-untagged-default-unmask)
- Print stylesheet: visual snapshot test (Playwright + media emulation) — `mediaType: 'print'`

---

## 5. Bridge endpoint dependency — Engineer PR 0

Pre-condition for FED PR 1 Ready-flip. Not authored by FED; ONR notes Engineer's
queue position for Admiral routing visibility (per R1 informational flag in
sec-eng SPOT-CHECK verdict).

### 5.1 Engineer queue position

Per ADR 0094 Step 1 + cohort-4 hand-off §4:

1. **shipyard ADR 0094 Step 1 PR** — `IAuditEventReader` + 3 supporting types +
   `InMemoryAuditEventReader` to `packages/kernel-audit/` (~280 LOC)
2. **signal-bridge Engineer PR 0** — Bridge endpoint family
   `GET /api/v1/audit-events` + `GET /api/v1/audit-events/{auditId}` +
   `GET /api/v1/audit-events/export.csv` (CSV deferred to Engineer PR 1)

### 5.2 Bridge endpoint shape (Engineer authors — ONR informational)

Per cohort-4 hand-off §4.2-§4.5:

- **Request**: `from`, `to`, `event_type`, `correlation_id`, `severity` (A2 NEW),
  `cursor`
- **Response**: `{ events: AuditEventSummary[], next_cursor: string | null }`
- **Cursor**: opaque base64-encoded value signed by `IOperationSigner` (Ed25519);
  carries tenant_id_signature per Decision 2
- **400 tenant_changed_reload_page**: when cursor's tenant_id_signature does
  not match active `ITenantContext.TenantId` (Decision 5)
- **Page size**: default 50

### 5.3 Bridge tests (Engineer authors — ONR informational)

Per cohort-4 hand-off §4.6, ≥10 new Bridge tests. ONR forward-watches:

- `GET /api/v1/audit-events` filtered by `severity=Security` returns only Security.* events (A2 server-side enforcement)
- `GET /api/v1/audit-events` with cross-tenant cursor returns 400 + `tenant_changed_reload_page` body
- `GET /api/v1/audit-events/{auditId}` for cross-tenant audit_id returns 404 (uniform-404 invariant per ADR 0092 §A3)

---

## 6. Test coverage matrix — cumulative FED + Bridge

When all PRs land, the cumulative cohort-4 test coverage matrix:

| Test name | PR | Closes | Layer |
|---|---|---|---|
| `useAuditEvents fetches with no filters` | FED PR 1 | baseline | hook |
| `useAuditEvents respects cursor pagination` | FED PR 1 | baseline | hook |
| `useAuditEvents 400 tenant_changed → TenantChangedError` | FED PR 1 | G1 | hook |
| `useAuditEvents cursor not in console.log` | FED PR 1 | G1 | hook |
| `AuditEventsPage Security.TBV row red-tinted + HIGH badge` | FED PR 1 | A2 | page |
| `AuditEventsPage severity filter narrows results` | FED PR 1 | A2 | page |
| `AuditEventsPage filter button removed` | FED PR 1 | Nit 5 | page |
| `AuditEventsPage htmlFor wires labels to inputs` | FED PR 1 | Nit 4 | page |
| `AuditEventsPage Load more hidden when next_cursor=null` | FED PR 1 | Nit 2 | page |
| `AuditEventDetailPage TBV structured render with 5 fields` | FED PR 1 | A1 | page |
| `AuditEventDetailPage tenant-mismatch triggers console.warn + degraded` | FED PR 1 | A1 | page |
| `AuditEventDetailPage <Link> back navigation` | FED PR 1 | Nit 7 | page |
| `AuditTrailPage_CursorTenantMismatch_400_TriggersReload` | FED PR 1 | A1 | integration |
| `Bridge GET /api/v1/audit-events?severity=Security filter enforcement` | Engineer PR 0 | A2 | Bridge |
| `Bridge cross-tenant cursor returns 400 tenant_changed` | Engineer PR 0 | G1 | Bridge |
| `Bridge GET /api/v1/audit-events/{auditId} cross-tenant returns 404` | Engineer PR 0 | A1 | Bridge |
| `IAuditEventReader.GetByTenantAsync filters tuple-compare cursor` | shipyard substrate | (Step 1) | substrate |
| `IAuditEventReader.GetByIdAsync cross-tenant returns null` | shipyard substrate | (Step 1) | substrate |
| `[Pii] masked field default-masked` | FED PR 2 | (forward) | page |
| `[Pii] masked field reveal button toggles` | FED PR 2 | (forward) | page |
| Print stylesheet hides nav + filter bar | FED PR 2 | (Nit 10) | page |

**Cumulative test count target:** ~21 new tests across substrate + Bridge + FED layers.

---

## 7. SPOT-CHECK dispatch cadence

Per fleet-conventions §SPOT-CHECK + ADR 0093 + V8 #5 DEFER protocol:

| Event | Dispatch | Verdict gate |
|---|---|---|
| sunfish#59 Ready-flip | sec-eng + frontend-architect | DONE (AMBER + GREEN) |
| FED PR 1 Ready-flip | sec-eng (A1 + A2 deep) + frontend-architect (Nit 1 + 4 verify) | GREEN required for auto-merge |
| FED PR 2 Ready-flip (optional) | sec-eng (PII masking) + frontend-architect (print) | GREEN required |
| Engineer PR 0 Ready-flip | sec-eng (cursor signing + cross-tenant 400) + .NET-architect | GREEN required |
| shipyard substrate Step 1 Ready-flip | .NET-architect | GREEN required |

**Admiral SLA:** 30 min from Ready-flip to council dispatch per fleet-conventions
§SPOT-CHECK dispatch SLA. Council verdict cadence ~30-60 min median.

---

## 8. Pattern emergence forward-watches

Cohort-4 may surface pattern candidates worth tracking:

1. **pattern-tenant-id-signed-opaque-cursor** (candidate) — IOperationSigner-signed
   opaque cursor with embedded tenant_id_signature. If 2nd-instance emerges (e.g.,
   work-orders or financial-ledger pagination), formalize.
2. **pattern-uniform-404-cross-tenant** (potential candidate) — `GetByIdAsync`
   returns null on cross-tenant; consumer renders 404. ADR 0092 §A3 codifies but
   pattern catalog doesn't yet name this. If 3rd-instance emerges, draft candidate.
3. **pattern-defense-in-depth-tenant-assert-client-side** (potential candidate) —
   A1 client-side tenant-assertion against active ITenantContext. If applied to
   2+ cohorts, formalize.
4. **pattern-severity-event-prefix-coloring** (potential candidate) — A2 row-level
   severity coloring keyed off event_type prefix. If applied to non-audit views
   (e.g., notifications, messages), formalize.

ONR tracks via cohort-4 retrospective per V7 #5 scaffold.

---

## 9. Risk + mitigation

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Engineer queue blocks FED PR 1 (substrate + Bridge not ready) | HIGH | HIGH (FED idle on critical-path) | Engineer ADR 0094 Step 1 + Bridge PR 0 should ship in series; FED can author PR 1 against Engineer's draft Bridge PR (typespec only) |
| sec-eng SPOT-CHECK on FED PR 1 finds new AMBER beyond A1+A2 | MEDIUM | LOW (1 fold cycle) | Adversarial Brief (cohort-4 hand-off §2 already exists; Stage-05 pilot precedent) reduces this |
| sunfish#58 type stubs drift from final Bridge response shape | MEDIUM | MEDIUM (FED PR 1 type errors) | Engineer + FED coordinate via beacon during Bridge PR authoring; finalize types in sunfish#58 before FED PR 1 |
| pattern-009 PAIR auto-merge fires before BOTH halves land | LOW (gate is held per FED status beacon) | HIGH (production-broken cohort) | Auto-merge stays disarmed on sunfish#59 until Engineer PR 0 ships; documented in FED status beacon |
| Cursor signing scope mismatch (Engineer signs different scope than FED expects) | LOW | MEDIUM | Coordination via beacon; Bridge PR includes integration test asserting roundtrip |
| Defense-in-depth tenant-assertion (A1) false-positives in tenant-switch flow | LOW | MEDIUM (degraded fallback for legitimate flows) | useActiveTenant() returns live tenant; TanStack queryKey invalidates on tenant switch; A1 assert reads post-invalidation tenant |

---

## 10. Decisions surfaced (route to Admiral via inbox)

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **FED PR 2 standalone vs fold into PR 1?** ONR recommends: spec PR 2 as separate
   (per §4); FED's call to fold if scope is light.
2. **Severity filter wire-contract** — `severity` as event_type prefix-match server-side?
   Recommended: server filters by `LIKE '<prefix>.%'` semantically; client passes opaque string.
3. **pattern-tenant-id-signed-opaque-cursor candidate** — register as candidate now,
   or wait for 2nd-instance emergence? ONR recommends register-now per V8 #6 cadence.
4. **A1 client-side tenant-assertion** — accept as cohort-4 pattern, or defer to
   substrate-level guard? ONR recommends accept as cohort-4 pattern; substrate
   guard later if 2nd-instance pattern emerges.
5. **FED PR 2 timing** — ships immediately after FED PR 1 pair-merge, OR deferred
   to cohort-5+ work cycle? ONR recommends immediately; cohort-4 closure depends on it.

---

## 11. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T15-35Z` item V9 #1
2. `shipyard/icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md` (MERGED at shipyard#81)
3. `coordination/inbox/council-verdict-2026-05-22T1445Z-security-engineering-sunfish-59-cohort-4-pair-spot-check.md` (sec-eng AMBER)
4. `coordination/inbox/council-verdict-2026-05-22T1213Z-frontend-architect-sunfish-59-cohort-4-pair-spot-check.md` (frontend-architect GREEN + 7 nits)
5. ADR 0093 (Stage-05 Adversarial Review Protocol; first pilot cohort)
6. ADR 0094 (IAuditEventReader; Step 1 substrate)
7. ADR 0091 R2 + ADR 0092 R2 (ITenantContext divergence + substrate tenant-keyed)
8. ADR 0046 + ADR 0049 (IOperationSigner + audit substrate)
9. pattern-009 (Bridge endpoint + frontend rebind pair; formal)
10. V8 #5 DEFER verdict spec (shipyard#114) — sec-eng-council 4th verdict
11. fleet-conventions §SPOT-CHECK dispatch SLA
12. `feedback_pattern009_scope` memory (pattern-009 SPOT-CHECK on NEW routes)
13. cerebrum.md R1 (Pattern-009 SPOT-CHECK ordering — cohort-5+ amendment planned)

---

## 12. What ONR does next

V9 #1 deliverable complete. Per Admiral V9 directive sequencing: interleaves V9 #2
(cohort-10 metrics additions — CONDITIONAL on QM V5 #3 landing), V9 #3 (pattern
catalog 2026-05-23 follow-up snapshot — CONDITIONAL on QM V5 #1 + PAO #116
landing), V9 #4 (onboarding-ladder sub-cohorts scaffold).

ONR proceeds to V9 #4 (onboarding follow-up; not conditional). #2 + #3 fired if
upstream prerequisites land during this session.

— ONR, 2026-05-22T15:45Z

# Component Reuse Audit — Cohort-2

This audit catalogs which existing components cohort-2 pages reuse, which new components are needed (none for cohort-2), and which gaps warrant promotion to `@sunfish/ui-react` after the cohort lands.

## Existing components reused

### From `@sunfish/ui-react`

| Component | Used by | Usage notes |
|---|---|---|
| `<Card>`, `<CardHeader>`, `<CardContent>`, `<CardTitle>`, `<CardFooter>` | All 3 pages | Standard Shadcn-style card primitive; reused as-is |

### From `@/components/`

| Component | Used by | Usage notes |
|---|---|---|
| `AuthRoleGate` | RentCollectionPage (NEW) | Wrap the form in `<AuthRoleGate allow={['owner', 'manager']}>` — gating rent collection to owner/manager roles. Cohort-1 MaintenancePage precedent (line 134). |

### Tailwind utility-class patterns (no component primitive)

These are inline conventions repeated across pages. See [`tokens.md`](./tokens.md) for the full inventory.

| Pattern | Pages |
|---|---|
| Status pill (`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium` + semantic color) | AccountingPage (days-due) — and MaintenancePage (work-order status) |
| Data table (`rounded-lg border border-gray-200` + `<thead>` gray-50 + hover-on-row) | LeaseDetailPage, AccountingPage — and MaintenancePage |
| Error surface (`border-red-200 bg-red-50 text-red-700`) | All 3 |
| Success/confirmation surface (`border-green-200 bg-green-50 text-green-800`) | RentCollectionPage |
| Primary button (`bg-blue-600 hover:bg-blue-700 text-white text-sm rounded`) | All 3 |
| Secondary button (`border-gray-300 text-gray-700 hover:bg-gray-50 text-sm rounded`) | RentCollectionPage |
| Form field (`border-gray-300 rounded-md focus:ring-blue-500 px-3 py-2 text-sm`) | RentCollectionPage |

## New components needed for cohort-2

**None.** All cohort-2 work composes existing primitives.

## Missing primitives — promote to `@sunfish/ui-react` after cohort-2

These patterns now have ≥2 instances in shipped code (cohort-1 + cohort-2). Recommend promotion in cohort-3 cleanup or as a standalone shipyard PR:

### 1. `<StatusPill>` — semantic state pill

**Repeated in:** MaintenancePage (work-order status), AccountingPage (aging bucket).

**Proposed API:**

```typescript
<StatusPill kind="workOrderStatus" value={wo.status} />
<StatusPill kind="agingBucket" days={daysDue} />
```

**Implementation:** internal `Record<KindName, Record<ValueName, ClassName>>` lookup; renders a `<span>` with the base classes + the looked-up color classes.

**Promotion priority:** medium. Two instances; a third (cohort-3 reports) likely.

### 2. `<DataTable>` — list-of-records table

**Repeated in:** LeaseDetailPage (payments), AccountingPage (outstanding), MaintenancePage (work orders).

**Proposed API:**

```typescript
<DataTable
  columns={[
    { key: 'id', label: 'ID', render: (row) => <code>{row.id.slice(-8)}</code> },
    { key: 'status', label: 'Status', render: (row) => <StatusPill ... /> },
    ...
  ]}
  rows={data?.items ?? []}
  loading={isPending}
  error={error}
  empty={<EmptyState text="..." />}
/>
```

**Implementation:** wraps `<table>` + header + body + loading skeleton + empty state + error surface in one component.

**Promotion priority:** high. Three current instances; cohort-3 reports will add 2+ more.

### 3. `<ErrorSurface>` — actionable error message

**Repeated in:** All 3 cohort-2 pages (4 variants on RentCollectionPage alone), plus MaintenancePage.

**Proposed API:**

```typescript
<ErrorSurface
  variant="retryable" | "reload" | "redirect" | "info"
  title="Couldn't load payment history"
  body="We couldn't fetch this lease's payment history. Try again in a moment."
  action={{ label: 'Try again', onClick: () => refetch() }}
/>
```

**Implementation:** internal map of variant → icon + button-class; renders the standard red-surface card.

**Promotion priority:** high. RentCollectionPage's 4 error states + LeaseDetailPage + AccountingPage = 6+ instances in cohort-2 alone.

### 4. `<ConfirmationSurface>` — success-state acknowledgment

**Repeated in:** RentCollectionPage, MaintenancePage (create-success).

**Proposed API:**

```typescript
<ConfirmationSurface
  title="Payment recorded"
  body={<>${amount} recorded for {tenantName} (ref: {paymentId}).</>}
  auditTrailNote="An audit-trail entry has been emitted."  // pattern-010 signature
  actions={[
    { label: 'Record another', onClick: reset, variant: 'primary' },
    { label: 'View lease history', to: `/leases/${leaseId}#payments`, variant: 'secondary' },
  ]}
/>
```

**Implementation:** green-surface card with title (semibold + check icon) + body + optional audit-trail note (slate-700 with separator above) + action row.

**Promotion priority:** medium. Two instances; the `auditTrailNote` slot makes pattern-010 portable.

## Recommendation

Promote `<DataTable>` and `<ErrorSurface>` in **cohort-3 cleanup PR** (separate from W#75 PRs). Drives the cohort-3 reports cluster from a higher-level abstraction; refactors cohort-1 MaintenancePage + cohort-2 pages to use the new primitives in a follow-up PR.

`<StatusPill>` and `<ConfirmationSurface>` are lower-priority — promote when a clean refactor opportunity surfaces.

— PAO, 2026-05-19

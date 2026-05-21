# Mobile-first UX — cohort candidate architecture research (2026-05-21)

**Authored by:** ONR (V5 batch item #7)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #7)
**Authored at:** 2026-05-21T14-48Z

---

## Scope

V2 #6 cohort-4 scope survey listed "Mobile-first UX wave" (candidate C5). Now survey what mobile UX architecture would entail. Decision dimensions: PWA vs MAUI iOS vs deferred.

---

## TL;DR

1. **Three mobile UX paths exist for Sunfish:**
   - **Path A — Progressive Web App (PWA):** existing React app (`sunfish/apps/web/`) gains responsive design + offline + add-to-home-screen support. Browser-based; no native install.
   - **Path B — .NET MAUI iOS:** parallels the W#23.3 iOS field-app work (`anchor-mobile-ios`). Native install via App Store.
   - **Path C — Tauri mobile:** Tauri 2.0 added experimental mobile support (Android + iOS). Same React app, native shell.

2. **Tauri 2.x mobile support is limited** — Android stable; iOS still preview / requires extensive plugin work; not production-ready as of January 2026 cutoff.

3. **W#23.3 iOS (MAUI) is a SEPARATE app, not the Sunfish ERP main app.** It's the field-app for property inspections; uses `.NET MAUI` with platform-native iOS APIs. Distinct codebase from `sunfish/apps/web/` React app.

4. **ONR recommends Path A (PWA) for Sunfish ERP mobile.** Lowest engineering cost (~10-15h FED); reuses existing React codebase; covers Surface Pro + iPad + iPhone via responsive design + offline service worker. Defers Path B (MAUI parallel app) until customer signal demands native install (e.g., App Store distribution requirement).

5. **PWA scope (cohort-5/6 candidacy):**
   - Responsive design audit + remediation across cohort-1/2/3/4 pages
   - Touch-target sizing (≥44px per Apple HIG)
   - Offline service worker (cohort-3 reports can be cached; cohort-2 financial pages need offline-write queue per W#60 P3 model)
   - Add-to-home-screen manifest
   - Mobile-specific UX (swipe gestures; mobile nav; bottom-sheet modals)
   - Form factor: Surface Pro (already MVP target per W#60 P3) + iPad + iPhone

6. **Substrate gap:** none significant. PWA service worker + manifest are pure FED + browser-features.

7. **Cohort-5/6 candidacy:** scores 7/12 per V5 #6 candidate matrix. Higher than ERPNext migration (5/12); lower than ARR/MRR (9/12). Cohort-6 candidate if cohort-5 = C1 ARR/MRR.

---

## 1. Path A — Progressive Web App (PWA)

### 1.1 Scope

- **Responsive design audit:** sweep cohort-1/2/3/4 pages for breakpoint correctness (≥768px tablet; ≥375px mobile)
- **Touch-target remediation:** buttons + interactive elements meet 44×44px minimum
- **Service worker:** offline page caching + offline-write queue for cohort-2 financial pages (mirrors W#60 P3 Tauri offline pattern)
- **Web app manifest:** add-to-home-screen support; icon set + theme color
- **Mobile navigation:** bottom-nav on phone; sidebar on tablet+
- **Forms-on-mobile:** touch-friendly input + keyboard handling

### 1.2 Engineer scope

- ~1h: service worker scaffolding + cache strategy (network-first for read; offline-queue for write)
- ~1h: web app manifest + icons (FED + PAO collaboration; Yeoman renders icons)

### 1.3 FED scope (~10-15h)

| PR | Subject | Effort |
|---|---|---|
| PR 1 | Responsive design sweep across cohort-1 pages (Properties + Leases + Maintenance) | 2-3h |
| PR 2 | Responsive design sweep across cohort-2 pages (Accounting + LeaseDetail + RentCollection) | 2-3h |
| PR 3 | Responsive design sweep across cohort-3 pages (RentRoll + PL + TrialBalance + ArAging) | 2-3h |
| PR 4 | Responsive design sweep across cohort-4 page (AuditTrail) | 1h |
| PR 5 | Service worker + offline-write queue + manifest | 2-3h |
| PR 6 | Mobile-nav + bottom-sheet modals + touch-target audit | 2-3h |

**Total Path A:** ~10-15h FED + ~2h Engineer = ~12-17h.

### 1.4 Pros

- Lowest engineering cost
- Reuses entire React codebase
- Single deployment surface (web app served via Bridge static)
- Offline parity with W#60 P3 Tauri model
- Covers Surface Pro + iPad + iPhone uniformly

### 1.5 Cons

- No native App Store distribution
- iOS Safari has PWA limitations (no push notifications until iOS 16.4+; no background sync)
- Add-to-home-screen UX is OS-specific (Android more polished than iOS)

---

## 2. Path B — .NET MAUI iOS

### 2.1 Scope

Parallel iOS app paralleling W#23.3 field-app. Native install via App Store.

### 2.2 Engineer scope (~30-50h)

- New `sunfish/apps/mobile-ios/` project
- .NET MAUI scaffold (similar to `anchor-mobile-ios/` per W#23.3)
- API client (consumes Bridge endpoints; mirrors `sunfish/apps/web/src/api/` TypeScript clients in C#)
- Page implementations parallel to React pages — DUPLICATED work
- iOS-specific features (push notifications; biometric auth; deep links)

### 2.3 Pros

- Native iOS install via App Store
- Push notifications + background sync
- Biometric auth (FaceID / TouchID)
- Better iOS UX

### 2.4 Cons

- HIGH engineering cost (~30-50h Engineer; not FED-leveraged)
- DUPLICATE codebase (C# MAUI parallel to TypeScript React)
- Two deployment pipelines (web + App Store)
- App Store review cycles + iOS code-signing infrastructure
- iOS-only; Android would need separate app

### 2.5 ONR's read

Path B is HIGH-EFFORT + DEMAND-DRIVEN. Defer until customer signal (e.g., "we need App Store distribution for our property management chain"). Until then, Path A PWA covers the form factors.

---

## 3. Path C — Tauri Mobile

### 3.1 Status as of January 2026 cutoff

Tauri 2.0 added experimental mobile support:
- Android: stable since Tauri 2.1
- iOS: preview / requires plugins for native APIs (not production-ready)

### 3.2 ONR's read

NOT VIABLE for Sunfish ERP MVP. iOS path is too immature. Wait for Tauri 2.3+ or 3.0 stability before reconsidering.

---

## 4. ONR recommendation

**Path A (PWA) for cohort-5/6 candidacy.**

- Lowest engineering cost
- Reuses existing React codebase
- Covers all current form factors (Surface Pro + iPad + iPhone) via responsive design
- Defer Path B (MAUI parallel app) until customer signal demands App Store distribution
- Defer Path C (Tauri mobile) until upstream stability matures

**Cohort-5/6 candidacy score: 7/12** per V5 #6 candidate matrix (above ERPNext at 5/12; below ARR/MRR at 9/12).

---

## 5. Cohort-5/6 sequencing

If cohort-5 anchor = C1 (ARR/MRR reporting) per V5 #6 ranking:

**Cohort-6 candidates re-ranked:**
1. C7 AP Aging page (8/12; substrate gap is small; closes reports cluster narrative)
2. **C5 Mobile-first UX PWA (7/12; ONR's research output here)**
3. C2 Multi-tenant admin (6/12; production OIDC needed)
4. C4 ERPNext migration (5/12; demand-driven)

**ONR's cohort-6 recommendation: C7 AP Aging** (small substrate; closes reports). Cohort-7 candidate: C5 Mobile-first UX PWA.

Alternative: if C7 AP Aging cartridge already shipped by cohort-5 close (post-Engineer ApAgingSummaryCartridge work), elevate C5 Mobile-first to cohort-6 directly.

---

## 6. Open questions for CIC (via Admiral routing)

1. **Path A vs Path B for mobile UX** — ONR recommends Path A (PWA); Path B (MAUI parallel) deferred. Confirm.
2. **Cohort-5/6/7 sequencing** — ONR recommends C1 → C7 → C5. Confirm.
3. **Tauri mobile re-evaluation timeline** — Tauri 2.3+ stable; consider at next-after-cohort-5 dispatch?

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z` item #7
2. V2 #6 cohort-4 scope survey (shipyard#74) — C5 mobile-first candidate framing
3. V5 #6 ERPNext migration scoping (shipyard#93) — candidate matrix scoring template
4. W#23.3 `anchor-mobile-ios/` MAUI project (per memory + active workstream) — Path B precedent
5. Tauri 2.0+ mobile support documentation (January 2026 cutoff)

---

## 8. What ONR does next

V5 #7 deliverable complete. Files `onr-status-*-v5-item-7-mobile-first-ux-complete.md`. Proceeds to V5 #1 (cohort-5 + cohort-6 surveys — PRIMARY V5 deliverable; 2 distinct PRs).

— ONR, 2026-05-21T14:48Z

# BizTracker — Handoff for the remaining phases (8 → 15)

Updated at the end of the session that built Phases 6 and 7 (2026-10-06).
Read **CLAUDE.md** and **WORK_ORDER.md** first, then this whole file. **CLAUDE.md wins** if anything here conflicts.

---

## 1. Where the project is

| Phase | Status | Commit |
|---|---|---|
| 1 Setup | ✅ done | `3882454 Setup: project scaffold` |
| 2 Database | ✅ done | `183db02 Database: integrated biztracker_db` |
| 3 Models / repositories / services | ✅ done | `7d1648c Phase 3: Models, repositories, core services` |
| 4 Login, roles, main shell, theme | ✅ done | `e367333 Phase 4: Login, roles, main shell, theme` |
| 5 Business Permit module | ✅ done | `d0a7b7c Phase 5: Business Permit module` |
| 6 Health Certificates module | ✅ done | `e924977 Phase 6: Health Certificates module` |
| 7 Sanitary Permit module | ✅ done | `774951f Phase 7: Sanitary Permit module` |
| 8 Real Property Tax | ⏳ next | — |
| 9 Annual Inspection | ⏳ | — |
| 10 Construction Permit | ⏳ | — |
| 11 Dashboard | ⏳ | — |
| 12 Notifications / alerts | ⏳ | — |
| 13 Permit vault + printable documents | ⏳ | — |
| 14 Admin tools | ⏳ | — |
| 15 Testing, cleanup, demo prep | ⏳ | — |

- GitHub: `https://github.com/fannaaa-intel/biztracker.git`, branch **`main`**, remote `origin` already set. `git push` after every phase commit.
- Home folder `C:\Users\DELL` is ALSO a git repo — always run git inside `C:\Users\DELL\source\repos\BizTracker`.
- Last regression (end of Phase 7), all green:
  DataTests 106/106 · BusinessPermitTests 68/68 · HealthCertificateTests 62/62 · SanitaryPermitTests 65/65 · UiTests 140/140 · **overflow issues 0**.
- The user's real `biztracker_db` was **not** re-imported after the Phase 7 seed changes (new setting `sp_passing_score`, sanitary
  requirement rows for permits 2 and 3). The code falls back to 75 if the setting is missing. Ask before re-importing.

---

## 2. The user's standing rules (keep them exactly)

**Workflow**
1. Follow **CLAUDE.md** strictly. If something is unclear or conflicts with CLAUDE.md, **ask** — do not guess.
2. **One phase at a time.** Before coding a phase, show a short plan (files to create/change). Then build it.
3. After coding: `dotnet build` with **0 errors, 0 warnings**.
4. Then run the **regression** (section 6) and verify the phase's "Check" from WORK_ORDER.md. **Open and look at the screenshots
   yourself** (maximized + 1100×680, staff + owner, every tab and dialog). Fix every functional failure, every overflow issue and every
   visual problem you see, then run the regression again until it is fully green.
5. Report **in the simplest form**: a small results table, what was done, bugs found and fixed, **what's left**, exactly what to click to test.
6. **STOP** and wait for **"next"**. If the user reports a bug, fix it first.
7. On "next": commit `Phase X: <name>` and `git push` to `main`, then continue with the next phase.
   - If the user asks for several phases in one go (e.g. "do phase 8 to 10"): still do them one at a time — plan, build, regression,
     look at screenshots, **commit + push each phase separately** — and give one combined report at the end. If the user says "stop on
     phase X", finish that phase's regression, commit + push it, report, and stop.
   - The user writes short messages while you work (e.g. "what phase are you building?"). Answer briefly, then continue.
8. Seed data changes go in **`database/biztracker_db.sql`** (single source of truth). Tell the user before re-importing.
   **Never drop/re-import the user's `biztracker_db` without asking** — tests use the throw-away `biztracker_test` copy.
9. Don't write handoff documents unless the user asks.

**Access (decided by the user: "follow the claude.md")**
- Dashboard is visible ONLY to Admin, BPLO and Owner (exactly the CLAUDE.md roles table). Health / Assessor / Building / Inspector land on
  their own module. Already implemented in `AccessService`.
- Owner = read-only for their own business; they may **upload requirements and file applications** (CLAUDE.md), nothing else.

**UI / UX (the user asks for this every time — non-negotiable)**
- **Modern, quality UI/UX** on every screen, same look as Business Permits / Health Certificates / Sanitary Permits. Reuse the UI kit (section 4).
- Main window opens **maximized**.
- **NO visible scrollbars anywhere**, especially when the window shrinks. Grids: `ModernGrid` + `UiHelper.StyleGrid` (wheel/keyboard scroll).
  Lists: `WheelScrollPanel` (automatic "Scroll for more ▾" strip).
- **Nothing may overflow or be cut** at the minimum window size **1100×680** or maximized: no cut text, no controls outside their parent.
  Use AutoEllipsis + tooltips, responsive grid columns (hide less important ones when narrow), short button labels when narrow.
- **ALL messages use the custom animated `ModernMessageBox`** — never `MessageBox.Show`. Call
  `UiHelper.ShowInfo / ShowSuccess / ShowWarning / ShowError / Confirm(message, title, yesText, noText, danger:=True)`.
  `danger:=True` for deletes/deactivations. `ShowSuccess` after important actions (filed, issued, paid, approved, deleted…), and say what
  happens next ("Next: upload the requirements…").
- Disabled buttons must look disabled (automatic with `UiHelper.Style*Button` / `ModuleToolbar.AddAction`). Give disabled buttons a tooltip
  that explains WHY (`toolbar.SetTip(btn, "...")`).
- Empty states everywhere ("No properties yet. Click ""Add Property""…"), helpful notes in the detail tabs (what is blocking, what to do next).

---

## 3. Architecture already in place (reuse it — do not reinvent)

**Data/** (the ONLY place with SQL; parameterized; `Using` blocks)
- `Db.vb` — `Db.P("@x", value)` (always), `ExecuteNonQuery`, `ExecuteScalar`, `GetDataTable`, `ExecuteInsert` (returns new id),
  `RunInTransaction(Sub(conn, tx) ...)` + `TxExecute` / `TxScalar`, `NullIfEmpty`. Throw `BusinessRuleException("msg")` inside a transaction to
  roll back and show the message. DB errors show a friendly ModernMessageBox and return -1 / Nothing / empty table (so a console test that
  hits a DB error will hang on the modal box — avoid deliberately failing SQL in tests).
- `DataRowExtensions.vb` — `row.GetInt / GetNullableInt / GetString / GetDecimal / GetDate / GetNullableDate / GetBool`.
- Repositories (all `Shared`):
  - `BusinessRepository`, `UserRepository`
  - `BusinessPermitRepository` (+ `GetEndorsements`, `SetEndorsement(permitId, office, status, userId, remarks)`)
  - `RequirementRepository` (`GetByModule(businessId, module, relatedId)`, `Insert`, `SaveUpload`, `SetVerification`)
  - `SanitaryRepository` (`GetByBusinessId`, `GetById`, `Insert`, `Update`, `UpdateStatus`)
  - `EmployeeRepository`, `HealthCertificateRepository` (`GetLatestByBusinessId`, `GetByEmployeeId`, `Insert`, **`InsertMany`** (transaction))
  - `PropertyRepository` (`GetAll/GetById/GetByBusinessId/Insert/Update` — no delete)
  - `RptRepository` (assessments: `GetAssessmentsByPropertyId/ByBusinessId`, `InsertAssessment`; payments: `GetPaymentsByAssessmentId/ByBusinessId`, `InsertPayment`)
  - `InspectionRepository` (`Insert` auto-creates the 4 department items, `Update`, `GetItems`, `UpdateItem`)
  - `ConstructionRepository` (`Insert` auto-creates the 4 clearances, `Update`, `GetClearances`, `UpdateClearance`)
  - `NotificationRepository` (`InsertIfNew` dedupes, `GetUnreadCount`, `MarkRead`, `MarkAllRead`), `SettingsRepository` (cached
    `GetValue/GetInt/GetDecimal/Update/Reload`), `AuditLogRepository`, `ReferenceNoRepository` (`GetLastSequence(prefix, year)`).
  - Add missing repository methods when a phase needs them (e.g. a delete or an "exists" check) — SQL stays in Data/.

**Models/** — one class per table (`properties` → `RealProperty`). `Constants.vb` holds the exact ENUM strings:
`Roles`, `ModuleNames`, `AuditActions`, `EndorsementOffices`, `InspectionDepartments`, `ClearanceTypes`.

**Services/**
- `Session` (CurrentUser, UserId, FullName, Role, BusinessId, IsOwner, IsAdmin, IsStaff).
- `AccessService` — `CanAccess(screen)`, `GetScreens(role)`, **`CanManage(screen)`** (Admin + the module's office: BPLO / Health / Assessor /
  Building / Inspector), **`CanSeeBusiness(businessId)`** (Owner only their own). **Enforce both inside the services**, not only in the UI.
- `StatusService` — computed statuses from `Date.Today` + `expiry_warning_days`: `GetExpiryStatus`, `CountByExpiryStatus`, `GetDueStatus`
  (Paid / Overdue / Due Soon / Not Yet Due), `GetQuarterDueDate`, `GetQuarterStatus`, `IsOverdue`, `DaysUntil`, `WarningDays`,
  `GetHealthCertificateExpiry`, `GetSanitaryPermitExpiry`.
- `ReferenceNoService` — `GetNext(prefix, year)`: BP, MP, SP, HC, INS, AIC, CP, OR; `FormatNumber`; `GetNextClearanceNo(type)`: LC / FSEC / BLDG / OCC.
- `AuditService` — `LogInsert / LogUpdate / LogDelete / LogStatusChange / Log(action, table, id, details)`. Every insert/update/delete/status change logs.
- `AuthService`, `RequirementService` (upload copies to `uploads/<business_id>/`, PDF/JPG/PNG ≤ 10 MB, open, verify/reject).
- Module services (the **pattern**): `BusinessPermitService`, `HealthCertificateService`, `SanitaryPermitService`. Each has: status constants +
  `StatusFlow`, `GetNextStatus`, `GetTrackerStep`, `IsFinal`, `Validate…()` returning field→message dictionaries, `Get…Blocker()` returning
  "why not" text or Nothing, actions returning an **error message or Nothing**, owner/role checks inside, audit logging.
- **`BusinessPermitService.AutoEndorse(businessId, office, remarks)`** — cross-module endorsement. Sets the office's endorsement on the
  business's latest non-Rejected, not-yet-Issued application to Endorsed (skips if none or already Endorsed), logs it, returns the reference
  no. or Nothing. Used by Sanitary (Phase 7). **Use it in Phases 8–10:**
  - RPT: all due quarters paid → `EndorsementOffices.RPT`
  - Inspection: Fire department Passed → `EndorsementOffices.Fire`
  - Construction: Locational clearance Approved → `EndorsementOffices.Zoning`
  After it endorses, mention it in the success message ("The RPT endorsement of BP-2027-00001 is now Endorsed.").

**UI kit (Helpers/, Forms/, Modules/)**
- `Theme.vb` (+ `Icons` glyphs, Segoe MDL2 Assets), `Theme.StatusColor/StatusSoftColor(status)` (green/amber/red/gray groups — add new status
  words to `StatusGroup` if a new status should be colored, e.g. "Overdue" is red, "Due Soon" amber, "Not Yet Due" gray).
- `ModernControls.vb`: `RoundedPanel` (card), `NavButton`, `IconButton`, `Avatar`, `StatusBadge`.
- `LayoutControls.vb`: `StepTracker` (`Steps`, `CurrentStep`: n = all done, -1 = stopped), `SegmentedTabs` (`Tabs`, `SelectedIndex`),
  `WheelScrollPanel` (`SetRows`), `StatCard` (`SetValue(value, status, note)`; **compact mode** hides the icon automatically when narrow, so
  4–5 cards fit at 1100×680), **`ValidityBar`** (`SetValue(caption, fraction 0..1, status, note)` — colored "days left" bar).
- **`ModuleToolbar.vb`** (new, Phase 7) — the module toolbar: business ComboBox for staff / fixed label for Owner, remembers the last business
  per screen, right-aligned actions with full/short labels. Usage:
  ```vb
  Private ReadOnly toolbar As New ModuleToolbar("rpt")
  btnAdd = toolbar.AddAction("Add Property", "Add", "primary", AddressOf Add_Click, canManage)   ' left-to-right order
  AddHandler toolbar.BusinessChanged, Sub() LoadData(Nothing)
  ' OnLoad: toolbar.LoadBusinesses()     current business: toolbar.Business
  toolbar.SetTip(btnAdd, "why it is disabled")
  ```
- **`DetailRows.vb`** (new, Phase 7) — row builders for detail tabs (put them in a `WheelScrollPanel`):
  `InfoRow(tips, cap1, val1, cap2, val2)`, `ListRow(tips, title, detail, status, {New RowLink("Approve", Theme.SidebarBlue, Sub() ...)}, detailColor)`,
  `NoteRow(tips, text, color)`, `HeadingRow(text)`. Links run deferred (BeginInvoke) because the action usually rebuilds the list.
- `ModernGrid.vb` + `UiHelper.StyleGrid(grid, "Status")` + `UiHelper.AddGridColumn(grid, name, HEADER, property, fillWeight, minWidth)` —
  badge-painted status column, no scrollbars. Set `grid.MultiSelect = True` after StyleGrid if a module needs multi-select (Health Certificates does).
- `UiHelper`: `StylePrimaryButton / StyleSecondaryButton / StyleDangerButton`, `StyleComboBox`, **`CreateComboBox(combo, height)`** (bordered box
  so a dropdown lines up with FixedSingle text boxes in dialogs — always use it in dialogs), `CreateInputBox(textBox, placeholder, glyph)`
  (search boxes), `FormatMoney` (₱ 1,234.00), `FormatDate` (Oct 6, 2026 / "—"), message helpers.
- `ModernDialog` — base of every dialog: `SetTitle`, `AddField(caption, input, left, top, width)` → error label, `AddInfo(panel, caption, value, …)`,
  `AddFooterButton(text, "primary"|"danger"|"secondary")` (add the main action first), `Tips`. Pattern: light-blue summary box at top, fields
  with red error labels under them that clear on change, validation through the service's `Validate…`, save on a **copy**, `DialogResult.OK`.
  `PromptDialog.Ask(owner, title, message, caption, required, okText, okStyle)` for reasons.
- **`WorkflowDialog`** (new, Phase 7) — generic "Update Status / Advance": Current → Next badges, green ready / amber blocked box, Advance +
  optional Reject (asks a reason) buttons. `New WorkflowDialog(title, subtitle, current, next, blocker, readyMessage, Function() Service.Advance(x),
  Function(reason) Service.Reject(x, reason), "Reject Application", "Issue Permit")`.
- `ModuleView` base (override `Subtitle`, `RefreshData` for F5). `RequirementsPanel.LoadFor(businessId, ModuleNames.X, relatedId, AppScreen.X,
  allowUpload, allowVerify)` + `RequirementsChanged` event.
- `Modules/PlaceholderViews.vb` — remove a module's placeholder class when its real view is built. Left: `DashboardView`, `RealPropertyTaxView`,
  `AnnualInspectionView`, `ConstructionPermitView`, `SettingsView`.
- `MainForm` — `navItems` maps `AppScreen` → view factory (class names already match); bell uses `NotificationRepository.GetUnreadCount`
  (`BtnBell_Click` is the Phase 12 placeholder).

**Templates to copy for a new module**
- **`Modules/SanitaryPermitView.vb`** is the best template now (uses `ModuleToolbar`, `DetailRows`, `WorkflowDialog`, `RequirementsPanel`).
  `HealthCertificateView` shows 5 cards, multi-select, `ValidityBar` and a "to-do" tab. `BusinessPermitView` is the original.
- Structure: root `TableLayoutPanel` (toolbar 56 / cards 110 / main 100%), cards row, main row = list card (title + search + `ModernGrid`,
  empty-state label) + detail card (`detailContent` hidden when nothing selected → `lblDetailEmpty`; header with reference + `StatusBadge` +
  info line, `StepTracker`, `SegmentedTabs`, tab panels). `loading` flag while binding; selection from **`grid.SelectedRows`**; dialogs get a
  **fresh copy** from the repository; reload keeping the selection after every change; responsive columns in `FitGridColumns`.
- Services: copy `SanitaryPermitService.vb`. Dialogs: copy `SanitaryPermitDialog.vb` / `SanitaryInspectionDialog.vb` / `EmployeeDialog.vb`.

**Gotchas already hit (avoid them)**
- VB is case-insensitive and members shadow: don't name locals `name`, `left`, `width`, `text`, `single`, `p` (inside `Db`) —
  `For Each name In …` inside a control is a compile error (Control.Name). A handler called `Add_Click` clashes with the `Click` event
  (warning BC40014) — use `AddProperty_Click` etc.
- `": "c` is not a char constant — use strings.
- Never set `Anchor = Right` on a control inside a panel that is resized later — position it in the parent's `Resize` handler.
- Grid: guarded scroll helpers only (never set `FirstDisplayedScrollingRowIndex` yourself). Reset `SmoothingMode` after anti-aliased painting.
- **Grid column minimum widths** (the overflow scanner checks every cell): dates need **112**, status pills need text width + 36
  (**125** for "Expiring Soon", **130** for "For Inspection"/"For Re-inspection"), money ~110. Give the list card enough share
  (Health uses 58/42) and hide columns by width in `FitGridColumns`.
- Hidden docked controls are not laid out — call `PerformLayout()` after showing a tab.
- Disposing a LinkLabel inside its own click event crashes — `DetailRows.ListRow` already defers with BeginInvoke.
- Tab labels that may not fit at 1100×680 (e.g. "Renew Now (2)") — shorten in the tabs' `Resize` (see `HealthCertificateView.SetTabLabels`).
- Bash heredocs containing `'` (VB comments) break — write files with the Write tool (python one-off edits are fine).
- Tests: compare decimals with `.ToString("0.00")`; dates by value.
- Adding a row to `settings` in the seed → update `DataTests` "settings count" (now **14**).
- UI screenshots are taken with `CopyFromScreen`: if the user is using the PC (Alt+Tab, other windows) a shot can show another window. The
  overflow scan is still valid; re-run the regression if you need clean shots.

---

## 4. Seed data facts (today-relative; re-import refreshes them)

- Businesses: 1 Cristan's Bakeshop (food; owner user `owner`), 2 Santos General Merchandise (non-food), 3 Kusina ni Aling Rosa (food).
  Users (password `password123`): admin, bplo, health (Dr. Paolo Agustin), assessor (Ramon Villanueva), building (Engr. Teresa Castillo),
  inspector (Insp. Mark Del Rosario), owner (Cristan Dela Cruz → business 1).
- Business permits: 1 BP-Y-00001 Issued (MP-Y-00001); 2 BP-(Y+1)-00001 bakeshop **Under Review** (endorsements: Barangay/Sanitary/RPT/Zoning
  Endorsed, **Fire Pending**); 3, 4 Issued; 5 BP-(Y+1)-00002 Kusina **Submitted** (all 5 endorsements **Pending**).
- Health certs (bakeshop 10 active): 8 Valid, 1 Expiring Soon (Ben Reyes +20 d, Non-Food), 1 Expired (Lilah Espergal −5 d, Food Handler).
  Santos 2 valid. Kusina 3: 2 valid + Noel Ramos (Food Handler) expired −40 d.
- Sanitary: SP-Y-00001 bakeshop Issued, SP-Y-00002 Santos Issued (both valid until Dec 31), SP-Y-00003 Kusina **For Inspection**
  (prerequisites verified). Issuing it needs a score ≥ 75 AND Noel Ramos renewed. Setting `sp_passing_score` = 75.
- **RPT** (rates: basic 1% + SEF 1%; penalty 2%/month, max 72%):
  - Property 1 bakeshop PIN 015-06-001-01-001, TD-2024-10021, Land, **₱40,000 → ₱800/yr** (₱200/quarter); property 2 bakeshop Building
    **₱15,000 → ₱300/yr**. Every quarter already due is PAID (OR-Y-000xx, received by assessor); Q4 (Dec 31) not yet due.
  - Property 3 Kusina Land **₱25,000 → ₱500/yr**: only **Q1 paid**; Q2 (Jun 30) and Q3 (Sep 30) are **Overdue** today (Oct 6); Q4 not yet due.
  - Assessments exist for the current year only (UNIQUE property_id + tax_year).
  - Bakeshop BP 2 RPT endorsement is already Endorsed; Kusina BP 5 RPT is Pending → paying Q2 + Q3 must endorse it.
- **Inspections**: INS-Y-00001 bakeshop **For Re-inspection** (Structural/Electrical/Mechanical Passed, **Fire Failed**: "Emergency Exit Door #2…",
  re-inspection +30 days); INS-Y-00002 Santos **Completed**, Passed, AIC-Y-00002; INS-Y-00003 Kusina **Scheduled** +14 days, all 4 Pending.
  Bakeshop BP 2 Fire endorsement Pending → passing Fire on INS-1 must make it "4 of 4", Completed, certificate AIC-Y-00003, and endorse Fire.
- **Construction**: CP-Y-00001 bakeshop "Warehouse Renovation & Extension", **Building Permit** stage, Active, ₱850,000; clearances:
  Locational **Approved** (LC-Y-00001), FSEC Pending, Building Pending, Occupancy Pending. Requirements (module "Construction Permit",
  related 1): 3 Verified + "Plumbing / Sanitary Plans" Submitted.
- Notifications table is empty on purpose (Phase 12 fills it). Settings keys: lgu_name, province, expiry_warning_days, bp_renewal_deadline,
  bp_surcharge_rate, bp_interest_rate_monthly, bp_interest_max_months, rpt_basic_rate, rpt_sef_rate, rpt_penalty_rate_monthly, rpt_penalty_max,
  hc_validity_months, **sp_passing_score**, upload_folder.

---

## 5. Remaining phases — detailed notes (WORK_ORDER.md has the official spec + "Check")

### Phase 8 — Real Property Tax (`RealPropertyTaxView`, `AppScreen.RealPropertyTax`, managers **Assessor + Admin**; Owner read-only)
**Service `RptService`**
- `GetLedger(businessId, taxYear)` → one row per property: property + assessment (or Nothing) + payments + per-quarter info
  (amount = `total_due / 4` rounded, due date `StatusService.GetQuarterDueDate`, status `GetQuarterStatus(year, q, isPaid)` →
  Paid / Overdue / Due Soon / Not Yet Due, penalty if unpaid & overdue).
- `ComputeTax(assessedValue)` → basic = value × `rpt_basic_rate`, SEF = value × `rpt_sef_rate`, total = basic + SEF (₱40,000 → ₱400 + ₱400 = **₱800**).
- `ComputePenalty(quarterAmount, dueDate, payDate)` → `rpt_penalty_rate_monthly` × months late (a fraction of a month counts as a month,
  same counting as `BusinessPermitService.ComputeLateCharges`), capped at `rpt_penalty_max` (72%). Paying on/before the due date = 0.
  Example: Kusina Q2 ₱125 due Jun 30 paid Oct 6 → 4 months → 8% → ₱10.00; Q3 due Sep 30 → 1 month → ₱2.50.
- `ValidateProperty` (PIN required + unique, TD no required + unique, location required, type/classification from the ENUMs, assessed value > 0),
  `AddProperty`, `UpdateProperty` (add `PropertyRepository` exists checks if needed), audit.
- `GenerateAssessment(propertyId, taxYear)` — blocked if one already exists for that year (UNIQUE) or year outside current−1 … current+1; audit.
- `RecordPayment(assessmentId, quarter, payDate)` — only unpaid quarters, quarters must be paid **in order** (no Q3 before Q2), payDate ≤ today
  and in/after the tax year start, amount = quarter amount, penalty computed, **OR no = `GetNext("OR")`**, `received_by = Session.UserId`;
  audit. After saving: if the business has **no unpaid quarter whose due date has passed** (all due quarters paid) →
  `BusinessPermitService.AutoEndorse(businessId, EndorsementOffices.RPT, "RPT dues paid as of <date>")`.
- `GetTotalDue(businessId, year)` = sum of unpaid quarter amounts of the year's assessments (WORK_ORDER: "Total due equals the sum of unpaid
  assessments") + `GetOverdueAmount` (unpaid + past due, with penalties). `GetTaxClearanceStatus` = **"Issued"** only when nothing past due
  is unpaid, otherwise "Not Issued" (show why: "2 overdue quarters").
**Screen**
- Cards (4): **Total Due** (₱ unpaid this year, note "₱x overdue" red badge Overdue / Paid), **Overdue** (count of overdue quarters + amount),
  **Tax Clearance** (Issued / Not Issued badge), **Assessed Value** (sum of properties, note "N properties").
- List card "Property Ledger": grid PIN, TD NO., LOCATION, TYPE, ASSESSED VALUE, Q1, Q2, Q3, Q4 (each quarter column painted as a status pill
  — give StyleGrid the status column name of one column only, so either paint quarter columns via a small custom `CellPainting` that reuses
  the same pill drawing, or show text "Paid"/"Overdue" and color the cell text with `Theme.StatusColor`). Overdue rows highlighted
  (light red `StatusRedSoft` row back color). Year selector (current year default; prev/next) optional but nice.
- Detail card (selected property): header PIN + status (worst quarter status), info line (TD no · type · classification · location);
  tabs **Quarters** (4 `ListRow`s: "Q2 · due Jun 30, 2026", amount + penalty, badge, link "Record Payment" for Assessor on the next payable
  quarter; paid rows show OR no + date), **Assessment** (InfoRows: assessed value, basic, SEF, annual total, paid so far, balance; link/button
  "Generate <year> Assessment" when missing), **Important Dates** (Q1–Q4 deadlines with days left / overdue, Jan 31 early-payment note optional).
- Toolbar: Add Property (primary), Edit Property, Generate Assessment, Record Payment — Assessor/Admin only; Owner sees no buttons.
- Dialogs: `PropertyDialog` (PIN, TD no, location, type + classification via `UiHelper.CreateComboBox`, assessed value, live tax preview
  "Annual tax ₱800.00 (basic ₱400 + SEF ₱400)"), `RptPaymentDialog` (property + quarter summary, payment date, live preview: amount,
  months late, penalty, total, OR no preview), assessment can be a `UiHelper.Confirm` with the computed amounts.
- Success messages: "OR-2026-00041 recorded: ₱135.00 (₱125.00 + ₱10.00 penalty)." + endorsement note when AutoEndorse returned a reference.
**Tests `RptTests`**: tax math (₱40,000 → ₱800), penalty months/cap, ledger statuses for seed (bakeshop all due paid, Kusina Q2/Q3 Overdue),
total due = sum of unpaid, clearance Not Issued for Kusina / Issued for bakeshop, pay out of order blocked, pay Q2 then Q3 → clearance Issued +
**BP 5 RPT endorsement Endorsed**, duplicate assessment blocked, PIN/TD uniqueness, owner/other-role blocked, audit rows. UiTests: `DeepCheck("rpt", AppScreen.RealPropertyTax, {"assessor", "owner"}, …)`
(card values), Kusina selected, every dialog, validation errors.

### Phase 9 — Annual Inspection (`AnnualInspectionView`, managers **Inspector + Admin**)
**Service `InspectionService`**
- Statuses: Scheduled → In Progress → (For Re-inspection) → Completed, or Cancelled. Overall result Pending / Passed / Failed.
- `ScheduleInspection(businessId, scheduleDateTime)` — one active (not Completed/Cancelled) inspection per business per year; date ≥ today;
  reference `GetNext("INS")`; `InspectionRepository.Insert` creates the 4 items; audit. Staff only.
- `RecordResult(item, result Passed/Failed, findings, inspectorName)` — findings required when Failed; inspector required; sets inspected_at = now;
  then recompute the inspection: any Pending → "In Progress"; all done and any Failed → "For Re-inspection" (overall Failed, require/offer a
  re-inspection date); all 4 Passed → "Completed", overall Passed, **certificate `GetNext("AIC")`**. When Fire becomes Passed →
  `AutoEndorse(businessId, EndorsementOffices.Fire, "Fire safety inspection passed (INS-…)")`.
- `ScheduleReinspection(inspection, dateTime)` (date > today), `Cancel(inspection, reason)`, `GetPassedCount` → "3 of 4 Depts Passed".
- A failed item can be re-recorded as Passed at re-inspection (keep findings history in the text, e.g. append "Re-inspected …: Passed").
**Screen**
- Cards: **Schedule** (next date/time, badge Scheduled/Re-inspection, days until), **Endorsement** ("3 of 4 Depts Passed", badge),
  **Certificate** (AIC no. or "Locked — all 4 departments must pass").
- List: inspections (reference, year, schedule, status, result). Detail: header + `StepTracker` (Scheduled → Inspection → Re-inspection → Certified)
  + tabs **Checklist** (4 `ListRow`s: department, inspector · date · findings, badge Passed/Failed/Pending, links "Pass" / "Fail…" for Inspector),
  **Deficiencies** (failed items' findings as notes + re-inspection date + link "Schedule re-inspection"), **Certificate** (number, issued date,
  "Print" placeholder for Phase 13 — disabled until Completed, with tooltip).
- Dialogs: `ScheduleInspectionDialog` (date + time pickers), `InspectionResultDialog` (department, result segmented/combobox, inspector, findings).
**Tests `InspectionTests`**: seed counts (bakeshop 3 of 4), pass Fire on INS-1 → 4 of 4, Completed, AIC-Y-00003, **BP 2 Fire endorsement
Endorsed** (WORK_ORDER check), fail needs findings, schedule rules, cancel, owner/other-role blocked, audit. UiTests DeepCheck with {"inspector", "owner"}.

### Phase 10 — Construction Permit (`ConstructionPermitView`, managers **Building + Admin**; Owner can file a New Project + upload)
**Service `ConstructionService`**
- Stages: Locational → Building Permit → Construction → Occupancy → Completed (`StepTracker`). Status Active / On Hold / Completed / Rejected.
- Required clearance per stage advance: Locational → Building Permit needs **Locational Approved**; Building Permit → Construction needs
  **Building Approved**; Construction → Occupancy: no clearance (construction finished; maybe require all requirements Verified);
  Occupancy → Completed needs **Occupancy Approved**. Only the NEXT stage (no skipping — "Try to skip a stage — it must be blocked").
- Clearances: `Approve(clearance)` / `Reject(clearance, reason)` (Building role). Rules: **Building cannot be approved until FSEC is Approved**;
  a clearance can only be approved when its stage is reached (Locational at Locational; FSEC + Building at Building Permit; Occupancy at Occupancy).
  Approve sets `clearance_no = GetNextClearanceNo(type)`, approved_by/at; audit. **Locational Approved → `AutoEndorse(businessId, Zoning, …)`**.
- `FileProject` (title required, type ENUM, estimated cost > 0 optional, reference `GetNext("CP")`), default requirements (Architectural &
  Structural Plans, Electrical Plans, Plumbing / Sanitary Plans, Lot Title & Tax Declaration), put On Hold / Resume, Reject project with reason.
**Screen**: cards (Current Stage, Clearances "1 of 4 approved", Estimated Cost); project list; detail with stage tracker + tabs **Clearances**
(4 ListRows with Approve / Reject links + blocking notes like "Approve FSEC first"), **Documents** (`RequirementsPanel`, module "Construction Permit"),
**Details**; toolbar New Project, Edit, Advance Stage (WorkflowDialog), Put On Hold.
**Tests `ConstructionTests`**: skip blocked, Building blocked before FSEC, approve FSEC then Building → advance to Construction, Locational approve
on a new project → Zoning endorsement, owner can file but not approve, audit. UiTests DeepCheck with {"building", "owner"}.

### Phase 11 — Dashboard (`DashboardView`; Admin, BPLO, Owner only)
- "Welcome, <name>" and "**Action Required: N items need attention**" where N is computed from the same services: expiring/expired health certs,
  overdue RPT quarters, pending endorsements, failed inspection items, permits (business + sanitary) expiring within 30 days. List the items
  (clickable rows that open the module).
- Six module cards (Business Permit, Sanitary, RPT, Health Certificates, Annual Inspection, Construction) with live status badge, key reference
  and an "Open" button. MainForm needs a way to navigate: add `Public Event NavigateRequested(screen As AppScreen)` on `ModuleView` (or a
  shared `MainForm.Navigate(screen)`), handled by MainForm.
- Owner: their business. Staff (Admin/BPLO): counts across ALL businesses (pending applications per module) + a business selector optional.
- **Every number must equal the module screens** — compute via the module services, never separate SQL counts. Test it in UiTests by opening
  the dashboard and each module and comparing card values.

### Phase 12 — Notifications
- `AlertService.GenerateAlerts()` after login (LoginForm success → before MainForm): scan expiries/dues within `expiry_warning_days` + overdue
  items (health certs, sanitary + business permit validity, RPT quarters, re-inspection dates) → `NotificationRepository.InsertIfNew` (UNIQUE
  business/module/related/due_date prevents duplicates). Priority: Urgent = expired/overdue, Warning = within warning days.
- Bell (already in MainForm, badge = unread) → modern popup panel (no scrollbars — WheelScrollPanel), mark read / mark all read, clicking a
  notification opens the module (uses the Phase 11 navigation). Owner sees only their business; staff see the modules they can access.
- Pop-up summary after login when urgent items exist (ModernMessageBox or a modern panel). Check: log in twice → no duplicates.

### Phase 13 — Permit vault + printable documents
- `ReportService` fills HTML templates in `Templates/` (copy to output dir in the .vbproj) and opens the default browser. Templates: Mayor's/Business
  Permit, Sanitary Permit, Health Certificate (card size), Tax Order of Payment, RPT Official Receipt / Tax Clearance, Annual Inspection
  Certificate, Building/Occupancy clearance — LGU header from settings (`lgu_name`, `province`), reference no, dates, verification code
  (e.g. hash of reference + date). HTML-encode every value. Only Issued documents print. Log `PRINT` to audit.
- **Ask the user**: "Permit Vault" as a Dashboard area or its own sidebar item? (Adding a sidebar item changes the CLAUDE.md access table → ask.)
- "Verify Document" screen: enter a reference no → valid / expired / not found.

### Phase 14 — Admin tools (replace `SettingsView` placeholder; Admin only)
- Tabs: **Users** (add, edit role, reset password via `AuthService.HashPassword`, activate/deactivate — cannot deactivate yourself; Owner users
  need a business), **Businesses** (add/edit/deactivate = soft delete, create the Owner account), **Settings** (edit values with validation per key,
  `SettingsRepository.Update` + `Reload`), **Audit Log** (filters: date range, user, table; grid), **Backup** (`C:\xampp\mysql\bin\mysqldump.exe -u root
  biztracker_db` → SaveFileDialog folder; show success/failure).
- Check: create a new business + owner account and log in as that owner.

### Phase 15 — Testing, cleanup, demo prep
- Review every form (validation, friendly errors, tab order, no SQL in forms, Using blocks, parameterized SQL). Every grid has search; resizing never
  breaks layout. Run the full regression.
- `README.md` (requirements, XAMPP setup, import SQL, run, seeded logins, features) and `docs/TEST_CHECKLIST.md` (manual script per role with expected results).

---

## 6. Regression tests (run after EVERY phase)

Folder (outside the repo, kept on purpose): `C:\Users\DELL\source\repos\BizTracker_regression\`

```
bash /c/Users/DELL/source/repos/BizTracker_regression/run_regression.sh
```
It builds the app, creates the throw-away `biztracker_test` from `database/biztracker_db.sql` (fresh copy **before each suite**), runs the
console suites, then `UiTests`, prints a summary and drops `biztracker_test`. The real `biztracker_db` is never touched. UI windows flash on
screen for a few minutes (ask the user not to use the PC meanwhile if clean screenshots matter). Takes ~5–8 minutes — use a 900000 ms timeout.

Suites now: `DataTests` (106), `BusinessPermitTests` (68), `HealthCertificateTests` (62), `SanitaryPermitTests` (65), `UiTests` (140).
Screenshots + `results.txt` in `shots/`.

**For each new phase**
1. Add a `<Module>Tests` console project: copy the folder `SanitaryPermitTests` (App.config points to `biztracker_test`; .vbproj references the
   app; change `RootNamespace`), write checks for every service rule + the WORK_ORDER "Check" + owner/other-role restrictions + audit rows.
   Add the suite name to the `for suite in …` line of `run_regression.sh`.
2. Extend `UiTests/Program.vb` (before the `' ---------- Dialogs ----------` block) with:
   `DeepCheck("rpt", AppScreen.RealPropertyTax, {"assessor", "owner"}, Sub(u, size, view) Check(..., CardValues(view), "...") End Sub)` —
   it opens the module maximized and at 1100×680 for each user, selects every grid row, visits every tab, runs the overflow scanner and saves
   screenshots. Then switch to a second business through the toolbar ComboBox (see the Kusina block), and `CheckDialog("dlg_x", New XDialog(...))`
   for every dialog, plus one dialog with validation errors shown.
3. The overflow scanner reports: controls outside parents, clipped labels/buttons, cut grid cells/headers, visible scrollbars, WheelScrollPanel
   without hint, disabled buttons that look enabled. **Target: 0.**
4. **Look at the screenshots** (Read the PNGs) — maximized and min, staff and owner, each tab and dialog — and fix anything ugly
   (misaligned inputs, empty-looking panels, unclear labels, cut text the scanner missed in painted controls).

---

## 7. How to run the app
```
dotnet run --project src/BizTracker      (XAMPP MySQL must be running)
```
Logins (password `password123`): admin, bplo, health, assessor, building, inspector, owner.

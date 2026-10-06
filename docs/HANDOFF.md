# BizTracker — Handoff for the remaining phases (6 → 15)

Written at the end of the session that built Phases 1–5 (2026-10-06).
Read **CLAUDE.md** and **WORK_ORDER.md** first, then this file. CLAUDE.md wins if anything here conflicts.

---

## 1. Where the project is

| Phase | Status | Commit |
|---|---|---|
| 1 Setup | ✅ done | `3882454 Setup: project scaffold` |
| 2 Database | ✅ done | `183db02 Database: integrated biztracker_db` |
| 3 Models / repositories / services | ✅ done | `7d1648c Phase 3: Models, repositories, core services` |
| 4 Login, roles, main shell, theme | ✅ done | `e367333 Phase 4: Login, roles, main shell, theme` |
| 5 Business Permit module | ✅ done, regression green, **NOT committed yet** | — |
| 6–15 | ⏳ to do | — |

**First thing in the new session:** confirm `git status` shows the Phase 5 files. When the user says **"next"**, commit them as
`Phase 5: Business Permit module` (include `docs/HANDOFF.md`) and push, then start Phase 6.

- GitHub: `https://github.com/fannaaa-intel/biztracker.git`, branch `main`, remote `origin` already set. Push after every phase commit (`git push`).
- Home folder `C:\Users\DELL` is ALSO a git repo — always run git inside `C:\Users\DELL\source\repos\BizTracker`.
- Last regression (end of Phase 5): DataTests 106/106, BusinessPermitTests 68/68, UiTests 95/95, overflow issues 0.

---

## 2. The user's standing rules (keep them exactly)

**Workflow**
1. Follow **CLAUDE.md** strictly. If something is unclear or conflicts with CLAUDE.md, **ask** — do not guess.
2. **One phase at a time.** Before coding, show a short plan (files to create/change). Then build it.
3. After coding: `dotnet build` with **0 errors, 0 warnings**.
4. Then run the **regression** (section 5): data + service + UI overflow scan, and the phase's "Check" from WORK_ORDER.md as far as it can be verified.
5. Then report **in the simplest form**: a small results table, what was done, bugs found and fixed, **what's left**, and exactly what to click to test.
6. **STOP** and wait for the user to say **"next"**. If they report a bug, fix it first.
7. On "next": commit `Phase X: <name>` and `git push`, then continue with the next phase.
8. Seed data changes go in **`database/biztracker_db.sql`** (the single source of truth; there is no `biztracker_seed.sql`). Tell the user before re-importing. **Never drop/re-import the user's `biztracker_db` without asking** — tests use the `biztracker_test` copy.
9. Don't write handoff documents unless the user asks.

**Access (decided by the user: "follow the claude.md")**
- Dashboard is visible ONLY to Admin, BPLO and Owner (exactly the CLAUDE.md roles table). Health / Assessor / Building / Inspector land on their own module. Already implemented in `AccessService`.

**UI / UX (user asked repeatedly — non-negotiable)**
- **Modern, quality UI/UX** on every screen. Use the existing UI kit (section 4) so every module looks the same as Business Permits.
- Main window opens **maximized** (full screen).
- **NO visible scrollbars anywhere**, especially when the window shrinks. Lists scroll with the mouse wheel only
  (`ModernGrid` + `UiHelper.StyleGrid`, `WheelScrollPanel` with its automatic "Scroll for more ▾" strip).
- **Nothing may overflow** at the minimum window size **1100×680** or when maximized: no cut text, no controls outside their parent.
  Use AutoEllipsis + tooltips, responsive grid columns (hide less important columns when narrow), short button labels when narrow.
- **ALL messages use the custom animated `ModernMessageBox`** — never `MessageBox.Show`. Call
  `UiHelper.ShowInfo / ShowSuccess / ShowWarning / ShowError / Confirm(message, title, yesText, noText, danger:=True)`.
  Use `danger:=True` for deletes. Show `ShowSuccess` after important actions (filed, issued, deleted, paid…).
- Disabled buttons must look disabled (automatic when styled with `UiHelper.Style*Button`).

---

## 3. Architecture already in place (reuse it — do not reinvent)

**Data/** (no SQL anywhere else)
- `Db.vb` — `Db.P("@x", value)` (always use this, never `New MySqlParameter("@x", 0)`), `ExecuteNonQuery`, `ExecuteScalar`,
  `GetDataTable`, `ExecuteInsert` (returns new id), `RunInTransaction(Sub(conn, tx) ...)` + `TxExecute` / `TxScalar`,
  `NullIfEmpty`. Throw `BusinessRuleException("message")` inside a transaction to roll back and show the message.
  DB errors show a friendly ModernMessageBox and return -1 / Nothing / empty table.
- `DataRowExtensions.vb` — `row.GetInt / GetNullableInt / GetString / GetDecimal / GetDate / GetNullableDate / GetBool`.
- Repositories (all `Shared`): Business, User, BusinessPermit (+ endorsements: `GetEndorsements`, `SetEndorsement(permitId, office, status, userId, remarks)`),
  Requirement (`GetByModule(businessId, module, relatedId)`, `SaveUpload`, `SetVerification`), Sanitary, Employee, HealthCertificate (`GetLatestByBusinessId`),
  Property, Rpt (assessments + payments), Inspection (+ `GetItems`, `UpdateItem`; Insert auto-creates 4 items), Construction (+ `GetClearances`,
  `UpdateClearance`; Insert auto-creates 4 clearances), Notification (`InsertIfNew` dedupes, `GetUnreadCount`, `MarkRead`, `MarkAllRead`),
  Settings (cached; `GetValue/GetInt/GetDecimal/Update/Reload`), AuditLog, ReferenceNo.

**Models/** — one class per table (`properties` → `RealProperty`). `Constants.vb` holds exact ENUM strings:
`Roles`, `ModuleNames`, `AuditActions`, `EndorsementOffices`, `InspectionDepartments`, `ClearanceTypes`.

**Services/**
- `Session` (CurrentUser, Role, BusinessId, IsOwner, IsAdmin, IsStaff).
- `AccessService` — `CanAccess(screen)`, `GetScreens(role)`, **`CanManage(screen)`** (who processes a module: Admin + BPLO/Health/Assessor/Building/Inspector per module),
  **`CanSeeBusiness(businessId)`** (Owner only their own). Enforce these in services too, not only in the UI.
- `StatusService` — computed statuses from `Date.Today` + `expiry_warning_days`: `GetExpiryStatus`, `CountByExpiryStatus`, `GetDueStatus`,
  `GetQuarterDueDate`, `GetQuarterStatus`, `IsOverdue`, `DaysUntil`, `GetHealthCertificateExpiry` (issue + `hc_validity_months`), `GetSanitaryPermitExpiry` (Dec 31).
- `ReferenceNoService` — `GetNext(prefix, year)`: BP, MP, SP, HC, INS, AIC, CP, OR; `GetNextClearanceNo(type)`: LC / FSEC / BLDG / OCC.
- `AuditService` — `LogInsert / LogUpdate / LogDelete / LogStatusChange / Log(action,...)`; every insert/update/delete/status change must log.
- `AuthService` (BCrypt, 5 fails → 60 s lock, logs logins). `RequirementService` (upload copies to `uploads/<business_id>/`, PDF/JPG/PNG ≤ 10 MB, open, verify/reject).
- `BusinessPermitService` — the **pattern** for module services: workflow constants, `GetNextStatus`, `GetTrackerStep`, `Validate()` returning
  field→message dictionary, `GetAdvanceBlocker()`, `Advance()`, `Reject()`, `SetEndorsement()`, late charges. Owner rules enforced inside.

**UI kit (Helpers/, Forms/, Modules/)**
- `Theme.vb` (+ `Icons` glyphs, Segoe MDL2 Assets), status colors/soft colors via `Theme.StatusColor/StatusSoftColor`.
- `ModernControls.vb`: `RoundedPanel` (card), `NavButton`, `IconButton` (badge count, `CircleColor`), `Avatar`, `StatusBadge`.
- `LayoutControls.vb`: `StepTracker`, `SegmentedTabs`, `WheelScrollPanel` (`SetRows`), `StatCard` (`SetValue(value, status, note)`).
- `ModernGrid.vb` + `UiHelper.StyleGrid(grid, "Status")` + `UiHelper.AddGridColumn(...)` — badge-painted status column, no scrollbars, wheel/keyboard scroll.
- `UiHelper`: `StylePrimaryButton / StyleSecondaryButton / StyleDangerButton`, `StyleComboBox`, `CreateInputBox(textBox, placeholder, glyph)`,
  `FormatMoney` (₱ 1,234.00), `FormatDate` (Oct 6, 2026 / "—"), message helpers.
- `ModernDialog` (base for every Add/Edit dialog: `SetTitle`, `AddField(caption, input, left, top, width)` → error label, `AddInfo`, `AddFooterButton(text, "primary"|"danger"|"secondary")`).
  `PromptDialog.Ask(owner, title, message, caption, required, okText, okStyle)` for reasons. `ModernMessageBox` (do not call directly; use UiHelper).
- `ModuleView` base (override `Subtitle`, `RefreshData` for F5). `RequirementsPanel.LoadFor(businessId, ModuleNames.X, relatedId, AppScreen.X, allowUpload, allowVerify)`.
- `Modules/PlaceholderViews.vb` — remove a module's placeholder class when its real view is built (Phase 5 removed BusinessPermitView's).
- `MainForm` — `navItems` list maps `AppScreen` → view factory; bell uses `NotificationRepository.GetUnreadCount` (`BtnBell_Click` is the Phase 12 placeholder).

**`Modules/BusinessPermitView.vb` is the template for Phases 6–10.** Copy its structure:
toolbar (business ComboBox for staff via `StyleComboBox` / fixed label for Owner + right-aligned actions with full/short labels in `FitToolbar`),
row of 3 `StatCard`s, main row 44% list card (title + search + `ModernGrid`) / 56% detail card (`detailContent` hidden when nothing selected,
header + `StepTracker` + `SegmentedTabs` + tab panels), `loading` flag while binding, selection read from **`grid.SelectedRows`**, dialogs get a
fresh copy from the repository, reload keeping selection after every change, responsive columns in `FitGridColumns`.

**Gotchas already hit (avoid them)**
- VB is case-insensitive: a local `p` collides with `Db.P` inside `Db`; a local `left`/`width` shadows Form properties.
- Never set `Anchor = Right` (or Left|Right) on a control inside a panel that is resized later — it drifts off-screen. Position it in the parent's `Resize` handler.
- Grid: setting `FirstDisplayedScrollingRowIndex` before the grid has room throws — use the existing guarded helpers. Reset `SmoothingMode` after anti-aliased painting (dark lines bug).
- Hidden docked controls are not laid out — call `PerformLayout()` after showing a tab.
- Bash heredocs containing `'` (VB comments) break — write files with the Write tool.
- Tests: compare decimals with `.ToString("0.00")`, not `800D` vs `"800.00"`.

**Cross-module endorsements (Phases 7–10).** Use the business's latest non-Rejected application:
`BusinessPermitService.GetLatestApplication(businessId)` then `BusinessPermitRepository.SetEndorsement(permit.PermitId, EndorsementOffices.X, "Endorsed", Session.UserId, remarks)`
(skip if none or already Issued). Log to audit.
- Sanitary permit Issued → `Sanitary`. All due RPT quarters paid → `RPT`. Fire inspection Passed → `Fire`. Locational clearance Approved → `Zoning`.

---

## 4. Seed data facts (today-relative; re-import refreshes them)

- Businesses: 1 Cristan's Bakeshop (owner user `owner`), 2 Santos General Merchandise, 3 Kusina ni Aling Rosa. All passwords `password123`.
- Permits: 1 BP-YYYY-00001 Issued (MP-YYYY-00001), 2 BP-(YYYY+1)-00001 Under Review (Fire endorsement Pending, 2 docs Pending Upload),
  3/4 Issued, 5 BP-(YYYY+1)-00002 Submitted.
- Health certs (bakeshop, 10 active employees): 8 Valid, **1 Expiring Soon (Ben Reyes, +20 days)**, **1 Expired (Lilah Espergal, −5 days)**; Kusina 1 expired.
- Sanitary: 1 & 2 Issued (valid Dec 31), 3 Kusina "For Inspection". Sanitary requirements for permit 1 already verified.
- RPT: bakeshop properties ₱40,000 (₱800/yr) and ₱15,000 (₱300/yr), quarters already due are paid; Kusina ₱25,000 only Q1 paid → later due quarters **Overdue**.
- Inspections: 1 bakeshop "For Re-inspection", Structural/Electrical/Mechanical Passed, **Fire Failed**, re-inspection +30 days; 2 Santos Completed (AIC-YYYY-00002); 3 Kusina Scheduled +14 days.
- Construction: CP-YYYY-00001 bakeshop at "Building Permit" stage; Locational Approved (LC-YYYY-00001); FSEC, Building, Occupancy Pending.
- Notifications table is empty on purpose (Phase 12 fills it). Settings keys: lgu_name, province, expiry_warning_days, bp_renewal_deadline,
  bp_surcharge_rate, bp_interest_rate_monthly, bp_interest_max_months, rpt_basic_rate, rpt_sef_rate, rpt_penalty_rate_monthly, rpt_penalty_max,
  hc_validity_months, upload_folder.

---

## 5. Regression tests (run after every phase)

Folder (outside the repo, kept on purpose): `C:\Users\DELL\source\repos\BizTracker_regression\`

```
bash /c/Users/DELL/source/repos/BizTracker_regression/run_regression.sh
```
It builds the app, creates the throw-away `biztracker_test` from `database/biztracker_db.sql`, runs:
- `DataTests` — repositories, StatusService, ReferenceNoService, AuditService (106 checks).
- `BusinessPermitTests` — Phase 5 workflow, rules, owner restrictions, late charges, uploads (68 checks).
- `UiTests` — logs in every role, opens every page **maximized and at 1100×680**, long-name stress test, Business Permits every tab,
  every dialog and message box; **overflow scanner** reports controls outside parents, clipped labels/buttons, cut grid text,
  visible scrollbars, WheelScrollPanel without hint, disabled buttons that look enabled; screenshots in `shots/` (95 checks).
then drops `biztracker_test`. The real `biztracker_db` is never touched. UI windows flash on screen for ~2 minutes.

**For each new phase:** add a `<Module>Tests` console project (copy `BusinessPermitTests`) for the service rules + the WORK_ORDER "Check",
and extend `UiTests/Program.vb` with a deep check of the new view (copy the "Business Permits deep check" block: every tab, both sizes,
staff + owner, its dialogs). Add the new suite to `run_regression.sh`. Look at a few screenshots yourself before reporting.

---

## 6. Remaining phases — notes per phase (follow WORK_ORDER.md for the full spec)

**Phase 6 — Health Certificates** (`HealthCertificateView`, screen `AppScreen.HealthCertificates`, managers: Health + Admin; Owner read-only)
- Cards: Total Personnel, Valid, Expiring (30 days), Expired, Compliance % (Valid/Total). Bakeshop must show **10 / 8 / 1 / 1 / 80%**.
- "Renew Now" list (expiring + expired). Grid: name, position, category, certificate no, issue, expiry, computed status (badge).
- Add/Edit employee, Deactivate (soft), Issue/Renew certificate (expiry = `StatusService.GetHealthCertificateExpiry`, number `GetNext("HC")`),
  Renew Selected (this grid may use `MultiSelect = True`). All counts via StatusService; renewing the expired one updates counts immediately.

**Phase 7 — Sanitary Permit** (`SanitaryPermitView`, managers Health + Admin)
- Statuses: Submitted → Lab Analysis → For Inspection → Issued (or Rejected). Tracker: Intake → Lab Analysis → On-site Inspection → Permit Issued.
- Inspection score (0–100) + findings + inspector (Health role records). Prerequisites via `RequirementsPanel` (module "Sanitary Permit", related = sanitary_id):
  Microbiological Water Test, Physico-Chemical Analysis, Pest Control Certificate. Staff health-card sync card from HealthCertificate data.
- Expiry Dec 31 of issue year. **On Issued → Sanitary endorsement** on the business permit (see section 3).

**Phase 8 — Real Property Tax** (`RealPropertyTaxView`, managers Assessor + Admin)
- Ledger grid: PIN, TD no, location, type, assessed value, Q1–Q4 status (`StatusService.GetQuarterStatus`). Add/Edit property.
- Generate assessment for a year: basic = `rpt_basic_rate`, SEF = `rpt_sef_rate` (₱40,000 → ₱800). Record quarterly payment: OR auto (`GetNext("OR")`),
  penalty 2%/month (`rpt_penalty_rate_monthly`, cap `rpt_penalty_max` = 72%) when late. Total-due card, overdue highlight, important dates.
- "Tax Clearance" Issued only when nothing due is unpaid. **All due quarters paid → RPT endorsement.**

**Phase 9 — Annual Inspection** (`AnnualInspectionView`, managers Inspector + Admin)
- Schedule card, 4-department checklist (result + findings + inspector), deficiency notes, re-inspection scheduling, "3 of 4 Depts Passed".
- Completed only when all 4 Passed → certificate enabled (`GetNext("AIC")`). **Fire Passed → Fire endorsement.** Check: passing Fire → 4 of 4.

**Phase 10 — Construction Permit** (`ConstructionPermitView`, managers Building + Admin)
- Project list, New Project, stage pipeline (Locational → Building Permit → Construction → Occupancy → Completed) with `StepTracker`,
  clearances grid (Locational, FSEC, Building, Occupancy) approve/reject (number via `GetNextClearanceNo`), blueprints via `RequirementsPanel`.
- A stage advances only if its required clearance is Approved; **Building cannot be approved until FSEC is Approved**. Skipping a stage must be blocked.
  **Locational Approved → Zoning endorsement.**

**Phase 11 — Dashboard** (`DashboardView`; visible to Admin, BPLO, Owner only)
- "Welcome, <name>", "Action Required: N items" (expiring/expired health certs, overdue RPT, pending endorsements, failed inspection items, permit expiring ≤30 days).
- Six module cards with live status + key reference + button that opens the module (MainForm must expose a way to navigate, e.g. an event).
- Staff: counts across ALL businesses. Every number must equal the module screens. No hard-coded values.

**Phase 12 — Notifications**
- `AlertService.GenerateAlerts()` after login: scan expiries/dues within `expiry_warning_days` + overdue; `NotificationRepository.InsertIfNew` (no duplicates).
- Bell (already in MainForm) → list popup (modern, no scrollbars), mark read / mark all read, click opens the module.
  Pop-up summary after login when urgent items exist (use ModernMessageBox or a modern panel).

**Phase 13 — Permit vault + printable documents**
- `ReportService` fills HTML templates in `Templates/` and opens the default browser. Templates: Mayor's/Business Permit, Sanitary Permit,
  Health Certificate (card size), Tax Order of Payment, RPT OR / Tax Clearance, Annual Inspection Certificate, Building/Occupancy clearance —
  LGU header from settings, reference no, dates, verification code. Only Issued documents print. Log `PRINT` to audit.
- "Permit Vault" (Dashboard area or its own sidebar item — **ask the user which** before building) + "Verify Document" screen.

**Phase 14 — Admin tools** (replace `SettingsView` placeholder; Admin only)
- User Management (add, edit role, reset password via `AuthService.HashPassword`, activate/deactivate, cannot deactivate yourself).
- Business registry (add/edit/deactivate, create Owner account). Settings editor with validation (`SettingsRepository.Update`).
- Audit Log viewer with filters (date range, user, table). Backup: `C:\xampp\mysql\bin\mysqldump.exe -u root biztracker_db` → chosen folder.
- Check: create a new business + owner account and log in as that owner.

**Phase 15 — Testing, cleanup, demo prep**
- Review every form (validation, friendly errors, tab order, no SQL in forms, Using blocks, parameterized SQL). Every grid has search; resizing never breaks layout.
- `README.md` (requirements, XAMPP setup, import SQL, run, seeded logins, features) and `docs/TEST_CHECKLIST.md` (manual script per role).

---

## 7. How to run the app
```
dotnet run --project src/BizTracker      (XAMPP MySQL must be running)
```
Logins (password `password123`): admin, bplo, health, assessor, building, inspector, owner.

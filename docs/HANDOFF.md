# BizTracker — Handoff for the remaining phases (11 → 15)

Updated at the end of the session that built Phases 8, 9 and 10 (2026-10-06).
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
| 8 Real Property Tax | ✅ done | `6dfd7d7 Phase 8: Real Property Tax` |
| 9 Annual Inspection | ✅ done | `3f3bd6d Phase 9: Annual Inspection` |
| 10 Construction Permit | ✅ done | `60fcecb Phase 10: Construction Permit` |
| 11 Dashboard | ⏳ **next** | — |
| 12 Notifications / alerts | ⏳ | — |
| 13 Permit vault + printable documents | ⏳ (ask the user where the vault goes) | — |
| 14 Admin tools | ⏳ | — |
| 15 Testing, cleanup, demo prep | ⏳ | — |

- GitHub: `https://github.com/fannaaa-intel/biztracker.git`, branch **`main`**, remote `origin` already set. `git push` after every phase commit.
- Home folder `C:\Users\DELL` is ALSO a git repo — always run git inside `C:\Users\DELL\source\repos\BizTracker`.
- Last regression (end of Phase 10), all green:
  DataTests 106 · BusinessPermitTests 68 · HealthCertificateTests 62 · SanitaryPermitTests 65 · RptTests 118 · InspectionTests 89 ·
  ConstructionTests 91 · UiTests 259 · **overflow issues 0**.
- The user's real `biztracker_db` was **not** re-imported since the Phase 7 seed changes (setting `sp_passing_score`, sanitary requirement
  rows for permits 2 and 3). Phases 8–10 needed **no** seed changes. The code falls back to 75 if `sp_passing_score` is missing.
  **Ask before re-importing.**
- **The dev PC now runs at 150 % display scaling** (single 1920×1080 monitor; UiTests logs `DPI scale: 1.50`). See section 3 "DPI".

---

## 2. The user's standing rules (keep them exactly)

**Workflow (latest instructions from the user)**
1. Follow **CLAUDE.md** strictly. If something is unclear or conflicts with CLAUDE.md, **ask** — do not guess.
   (Phase 13: **ask where the Permit Vault goes** — Dashboard area or its own sidebar item — before building it.)
2. **One phase at a time.** Before coding a phase, show a short plan (files to create/change), then build it.
3. After coding: `dotnet build` with **0 errors, 0 warnings**.
4. Run the **regression** (section 6, timeout 900000). Add a `<Module>Tests` suite for new logic and a UI deep check of the new screen
   (DeepCheck + CheckDialog, both sizes, staff + owner, every tab and dialog). Verify the phase's "Check" from WORK_ORDER.md.
   **Open and look at the screenshots yourself** and fix every functional failure, overflow issue and visual problem; re-run until all green
   with 0 overflow issues.
5. Then **commit `Phase X: <name>` and push to origin main**.
6. Report **in the simplest form**: results table, what was done, bugs found and fixed, **what's left**, exactly what to click to test.
7. **STOP** and wait for the user to reply **"next"** (they often ask "Did you run the regression? If yes continue" — answer yes/no
   truthfully, then continue). If they report a bug, fix it first. If they ask for several phases at once, still do them one by one
   (plan, build, regression, screenshots, commit + push each) and give one combined report. "Stop on phase X" = finish that phase's
   regression, commit + push, report, stop. Short questions while you work: answer briefly and continue.
8. Seed data changes go in **`database/biztracker_db.sql`**. **Never drop/re-import the user's `biztracker_db` without asking** —
   tests use the throw-away `biztracker_test` copy.
9. Don't write handoff documents unless the user asks.

**Access (decided by the user: "follow the claude.md")**
- Dashboard is visible ONLY to Admin, BPLO and Owner (exactly the CLAUDE.md roles table). Health / Assessor / Building / Inspector land on
  their own module. Already implemented in `AccessService`.
- Owner = read-only for their own business; they may **upload requirements and file applications** (CLAUDE.md), nothing else.
  (Owner can file: business permit, sanitary permit, construction project; can upload requirements/technical documents.)

**UI / UX (the user asks for this every time — non-negotiable)**
- **Modern, quality UI/UX** on every screen, same look as Business Permits / Sanitary / RPT / Inspections / Construction. Reuse the UI kit.
- Main window opens **maximized**.
- **NO visible scrollbars anywhere.** Grids: `ModernGrid` + `UiHelper.StyleGrid`. Lists: `WheelScrollPanel` ("Scroll for more ▾" strip).
- **Nothing may overflow or be cut** at the minimum window (1100×680 logical) or maximized — **at 150 % scaling too**. AutoEllipsis +
  tooltips, responsive grid columns, short button labels when narrow, short card notes (cards are narrow at 150 %).
- **ALL messages use the animated `ModernMessageBox`** — never `MessageBox.Show`. `UiHelper.ShowInfo / ShowSuccess / ShowWarning /
  ShowError / Confirm(message, title, yesText, noText, danger:=True)`. Success messages say what to do next.
- Disabled buttons look disabled (automatic) and have a tooltip explaining WHY (`toolbar.SetTip`).
- Every list has a helpful empty state; detail tabs have notes saying what is blocking / what to do next.

---

## 3. Architecture already in place (reuse it — do not reinvent)

**Data/** (the ONLY place with SQL; parameterized; `Using` blocks)
- `Db.vb` — `Db.P("@x", value)`, `ExecuteNonQuery`, `ExecuteScalar`, `GetDataTable`, `ExecuteInsert` (new id), `RunInTransaction` +
  `TxExecute` / `TxScalar`, `NullIfEmpty`. DB errors show a friendly box and return -1 / Nothing / empty (a console test hitting a DB error
  hangs on the modal box — avoid failing SQL in tests).
- Repositories (all `Shared`): Business, User, BusinessPermit (+ `GetEndorsements`, `SetEndorsement`), Requirement (`GetByModule`,
  `Insert`, `SaveUpload`, `SetVerification`), Sanitary, Employee, HealthCertificate, **Property** (+ `PinExists`, `TdNoExists` — Phase 8),
  **Rpt** (assessments + payments), **Inspection** (`Insert` creates 4 items, `GetItems`, `UpdateItem`), **Construction** (`Insert`
  creates 4 clearances, `GetClearances`, `UpdateClearance`), Notification (`InsertIfNew`, `GetUnreadCount`, `MarkRead`, `MarkAllRead`),
  Settings (cached), AuditLog (`GetRecent`), ReferenceNo.

**Services/** — each module service: status constants, `Validate…` (field→message dictionary), `Get…Blocker` ("why not" text or
Nothing), actions returning an **error message or Nothing** (or an outcome object), role checks inside (`AccessService.CanManage(screen)`,
`CanSeeBusiness(id)`), audit logging.
- `BusinessPermitService` (+ **`AutoEndorse(businessId, office, remarks)`** → reference no. or Nothing; `GetLatestApplication`).
- `HealthCertificateService` (`GetRoster` → rows with computed Status), `SanitaryPermitService` (`GetCurrentPermit`, `GetDisplayStatus`,
  `GetHealthCardSync`).
- **`RptService`** (Phase 8): `ComputeTax`, `GetQuarterAmount` (Q4 takes the centavo remainder), `GetMonthsLate`, `ComputePenalty`
  (2 %/month, part of a month counts, cap 72 %), `GetLedger(businessId, year)` → `RptLedgerRow` (RealProperty, Assessment, Quarters as
  `RptQuarter` with Status/Penalty, `Balance`, `NextPayable`, `WorstStatus`), `GetAllQuarters`, `GetTotalDue(biz, year)`,
  `GetOverdueQuarters(biz)` (all years), `GetOverdueAmount`, `GetTaxClearanceStatus` ("Issued"/"Not Issued"), `GetTaxClearanceNote`,
  `ValidateProperty/AddProperty/UpdateProperty`, `GetAssessmentBlocker/GenerateAssessment` (years current−1…current+1),
  `GetPaymentBlocker/ValidatePayment/RecordPayment` → `RptPaymentResult` (Payment, EndorsedReference). All due quarters paid → AutoEndorse RPT.
- **`InspectionService`** (Phase 9): statuses Scheduled / In Progress / For Re-inspection / Completed / Cancelled; `GetInspections`,
  `GetItems`, `GetCurrentInspection` (newest not cancelled), `GetPassedCount`, `GetEndorsementText` ("3 of 4 Depts Passed"),
  `GetNextVisit`, `GetCertificateBlocker`, `GetScheduleBlocker`, `ValidateSchedule` (not past, 7 AM–5 PM, ≤12 months),
  `ScheduleInspection`, `Reschedule`, `ScheduleReinspection`, `Cancel(reason)`, `GetRecordBlocker` (not before the schedule date),
  `GetRecordableItems`, `ValidateResult`, `RecordResult` → `InspectionResultOutcome` (NewStatus, CertificateNo AIC-…, EndorsedReference).
  Fire Passed → AutoEndorse Fire. Failed findings kept as "Earlier (date): …" history.
- **`ConstructionService`** (Phase 10): Stages Locational → Building Permit → Construction → Occupancy → Completed (no skipping);
  status Active / On Hold / Completed / Rejected; `GetProjects`, `GetClearances`, `GetDocuments`, `GetClearanceSummary` ("1 of 4 approved"),
  `ValidateProject`, `FileProject` (owner allowed; 4 default documents), `UpdateProject`, `GetApproveBlocker` (stage reached; Building needs
  FSEC Approved + all documents Verified), `ApproveClearance` → `ClearanceOutcome` (ClearanceNo, EndorsedReference; Locational → AutoEndorse
  Zoning), `GetRejectBlocker/RejectClearance`, `GetAdvanceBlocker/Advance`, `GetReadyMessage`, `PutOnHold`, `ResumeProject`
  (`Resume` is a VB keyword), `RejectProject`.
- `StatusService`, `ReferenceNoService` (BP, MP, SP, HC, INS, AIC, CP, OR + clearance LC/FSEC/BLDG/OCC), `AuditService`, `AuthService`,
  `RequirementService`, `AccessService`, `Session`.

**UI kit (Helpers/, Forms/, Modules/)**
- `Theme.vb` (+ `StatusGroup` — add new status words there; Phase 8–10 added "not issued", "re-inspection" red).
- `ModernControls.vb` (RoundedPanel, NavButton, IconButton, Avatar, StatusBadge), `LayoutControls.vb` (StepTracker — min height from font,
  compact font for a long single word; SegmentedTabs — min height; WheelScrollPanel; StatCard; ValidityBar).
- `ModuleToolbar.vb` — business ComboBox / owner label + actions. `AddAction(full, short, style, handler, visible)`, `SetTip`,
  **`SetActionText(btn, full, short)`** (Phase 10, e.g. Put On Hold ↔ Resume Project).
- `DetailRows.vb` — `InfoRow`, `ListRow(tips, title, detail, status, links, detailColor)`, `NoteRow`, `HeadingRow` (DPI-scaled).
- `ModernGrid` + `UiHelper.StyleGrid(grid, "Status")` + `AddGridColumn`. RPT paints its own Q1–Q4 chips in a `CellPainting` handler added
  after StyleGrid (see `RealPropertyTaxView`).
- `UiHelper`: buttons, `CreateComboBox` (dialogs), `CreateInputBox` (search), `FormatMoney`, `FormatDate`, messages, **`Dpi(n)` /
  `DpiScale`**, **`DisableMnemonics(root)`** (called from ModuleView, ModernDialog, MainForm, LoginForm — so "&" in data shows).
- `ModernDialog` (scales itself once in OnLoad — see DPI), `PromptDialog.Ask(...)`, **`WorkflowDialog`** (current → next, blocker box,
  advance + optional reject), `RequirementsPanel.LoadFor(...)` (reviewer of a Submitted file gets Verify/Reject, not Replace).
- Module views to copy: **`RealPropertyTaxView`, `AnnualInspectionView`, `ConstructionPermitView`** (newest, DPI-correct), `SanitaryPermitView`.
  Structure: root TableLayoutPanel (toolbar 56 / cards 110 / main) → cards row (3–4 StatCards) → list card (header `Dpi(48)` with title +
  search `Dpi(38)`, ModernGrid, empty-state label) + detail card (header `Dpi(52)`, StepTracker, SegmentedTabs, WheelScrollPanels).
  `loading` flag while binding; selection from `grid.SelectedRows`; dialogs get a fresh copy from the repository; reload keeping selection.
- `Modules/PlaceholderViews.vb` — only `DashboardView` and `SettingsView` are left.
- `MainForm` — `navItems` maps `AppScreen` → view factory; bell uses `NotificationRepository.GetUnreadCount` (`BtnBell_Click` is the
  Phase 12 placeholder). **No navigation API yet** — Phase 11 must add one (see section 5).

**DPI (150 % scaling) — must follow**
- WinForms does NOT auto-scale the module views. Every pixel size you set in a view/row in code → wrap with **`Dpi(n)`** (header heights,
  search box heights, badge heights, grid MinimumWidth, label bounds set in Resize handlers).
- Dialogs: build at 100 % coordinates; `ModernDialog.OnLoad` scales them once (`Scale(DpiScale)`). Footer button widths are measured back in
  100 % units. Don't call Dpi() inside dialogs.
- Child layout inside a container that gets scaled: use the container's **`Layout`** event, not `Resize` (Resize runs before children are
  scaled → double-scaled, overflowing children).
- Card notes must be short (cards are ~250 px wide at 150 % min size). The overflow scanner does NOT catch AutoEllipsis cuts — **look at
  the screenshots**.

**Gotchas already hit (avoid them)**
- VB reserved words as identifiers: `when`, `resume`, `err`, `text`/`name`/`left`/`width` (shadow Control members). Handler names like
  `Add_Click` clash with events — use `AddProperty_Click`.
- `List(Of T).Count(Function …)` resolves to the `Count` property → use `.AsEnumerable().Count(…)` or `.Where(…).Count()`.
- Bash heredocs with `'` break — use the Write tool; python one-off edits are fine (beware `\\` in python strings).
- Grid min widths: dates 112, status pills = text + 36 ("For Re-inspection" 140); long free text (titles) → hide the column when it does
  not fit (see `ConstructionPermitView.FitGridColumns`), never let it cut.
- Hidden docked controls are not laid out — `PerformLayout()` after showing a tab. Disposing a LinkLabel inside its click → ListRow defers.
- Tests: compare decimals with `.ToString("0.00")`; dates by value; adding a settings row → update DataTests "settings count" (now 14).
- Session-relative reference numbers in tests: seed max OR = 00031, INS = 00003, AIC = 00002, CP = 00001, LC = 00001.

---

## 4. Seed data facts (today-relative; re-import refreshes them)

- Businesses: 1 Cristan's Bakeshop (food; owner user `owner`), 2 Santos General Merchandise (non-food), 3 Kusina ni Aling Rosa (food).
  Users (password `password123`): admin, bplo, health (Dr. Paolo Agustin), assessor (Ramon Villanueva), building (Engr. Teresa Castillo),
  inspector (Insp. Mark Del Rosario), owner (Cristan Dela Cruz → business 1).
- Business permits: 1 BP-Y-00001 Issued; **2 BP-(Y+1)-00001 bakeshop Under Review** (Barangay/Sanitary/RPT/Zoning Endorsed, **Fire Pending**);
  3, 4 Issued; **5 BP-(Y+1)-00002 Kusina Submitted** (all 5 endorsements Pending).
- Health certs: bakeshop 10 active → 8 Valid, 1 Expiring Soon (Ben Reyes +20 d), 1 Expired (Lilah Espergal −5 d, Food Handler).
  Santos 2 valid. Kusina 3: 2 valid + Noel Ramos (Food Handler) expired −40 d.
- Sanitary: SP-Y-00001 bakeshop Issued, SP-Y-00002 Santos Issued (valid until Dec 31), SP-Y-00003 Kusina **For Inspection**.
- RPT: bakeshop Land ₱40,000 (₱800/yr) + Building ₱15,000 (₱300/yr), every due quarter paid; Kusina Land ₱25,000 (₱500/yr) only Q1 paid →
  Q2/Q3 **Overdue** (today Oct 6), Q4 not yet due. Kusina cards: Total Due ₱375, Overdue ₱262.50 (2 quarters), Tax Clearance Not Issued.
- Inspections: INS-Y-00001 bakeshop **For Re-inspection** (3 of 4, Fire Failed, re-inspection +30 d); INS-Y-00002 Santos Completed
  AIC-Y-00002; INS-Y-00003 Kusina Scheduled +14 d (results can't be recorded before that date).
- Construction: CP-Y-00001 bakeshop "Warehouse Renovation & Extension", Building Permit stage, ₱850,000; Locational Approved LC-Y-00001,
  FSEC/Building/Occupancy Pending; documents 3 Verified + "Plumbing / Sanitary Plans" Submitted. Kusina and Santos have no projects.
- Notifications table is empty on purpose (Phase 12 fills it). 14 settings keys incl. `sp_passing_score`.

---

## 5. Remaining phases — notes (WORK_ORDER.md has the official spec + "Check")

### Phase 11 — Dashboard (`DashboardView`; Admin, BPLO, Owner only)
- Replace the `DashboardView` placeholder (move it to `Modules/DashboardView.vb`).
- "Welcome, <name>" and "**Action Required: N items need attention**", N computed from the module services (no separate SQL counts):
  expiring/expired health certs (`HealthCertificateService.GetRoster`), overdue RPT quarters (`RptService.GetOverdueQuarters`), pending
  endorsements (`BusinessPermitRepository.GetEndorsements` of the latest non-final application), failed inspection items
  (`InspectionService`), business/sanitary permits expiring within `expiry_warning_days` (`StatusService.GetExpiryStatus`). List the items
  as clickable rows (WheelScrollPanel) that open the module.
- Six module cards (Business Permit, Sanitary, RPT, Health Certificates, Annual Inspection, Construction): live status badge, key reference,
  "Open" button. **Navigation**: add `Public Event NavigateRequested(screen As AppScreen)` on `ModuleView` (or `MainForm.Navigate(screen)`),
  handled by MainForm (Phase 12 notifications will reuse it).
- Owner: their own business. Admin/BPLO: totals across ALL businesses (pending applications per module) — optionally a business selector.
- **Every number must equal the module screens** — WORK_ORDER check. Test in UiTests by opening the dashboard and each module and comparing
  card values (use `CardValues`). Add a `DashboardTests` console suite for the counting logic if it lives in a service (e.g. `DashboardService`).
- Note: BPLO cannot open the other modules (sidebar) — on their dashboard, module cards for screens they cannot access show info only (no
  "Open", or a disabled one with a tooltip). Check `AccessService.CanAccess`.

### Phase 12 — Notifications
- `AlertService.GenerateAlerts()` after login: expiries/dues within `expiry_warning_days` + overdue items (health certs, sanitary + business
  permit validity, RPT quarters, re-inspection dates) → `NotificationRepository.InsertIfNew` (UNIQUE business/module/related/due_date).
  Priority: Urgent = expired/overdue, Warning = within warning days.
- Bell (MainForm) → modern popup (WheelScrollPanel, no scrollbars), mark read / mark all read, click opens the module (Phase 11 navigation).
  Owner sees only their business; staff see the modules they can access.
- Pop-up summary after login when urgent items exist. Check: log in twice → no duplicates.

### Phase 13 — Permit vault + printable documents
- `ReportService` fills HTML templates in `Templates/` (copy to output in the .vbproj) and opens the default browser. Templates: Mayor's/
  Business Permit, Sanitary Permit, Health Certificate (card), Tax Order of Payment, RPT Official Receipt / Tax Clearance, Annual Inspection
  Certificate, Building/Occupancy clearance — LGU header from settings, reference no, dates, verification code. HTML-encode every value.
  Only Issued documents print. Log `PRINT` to audit.
- **Ask the user**: Permit Vault as a Dashboard area or its own sidebar item? (A sidebar item changes the CLAUDE.md access table → ask.)
- "Verify Document" screen: reference no → valid / expired / not found.

### Phase 14 — Admin tools (replace `SettingsView`; Admin only)
- Tabs: Users (add, edit role, reset password, activate/deactivate — not yourself; Owner users need a business), Businesses (add/edit/
  deactivate = soft delete, create the Owner account), Settings (validated edit, `SettingsRepository.Update` + `Reload`), Audit Log (filters
  date range, user, table), Backup (`C:\xampp\mysql\bin\mysqldump.exe -u root biztracker_db` → SaveFileDialog).
- Check: create a new business + owner account and log in as that owner.

### Phase 15 — Testing, cleanup, demo prep
- Review every form (validation, friendly errors, tab order, no SQL in forms, Using blocks, parameterized SQL); every grid has search;
  resizing never breaks layout. Full regression.
- `README.md` and `docs/TEST_CHECKLIST.md` (manual script per role with expected results).

---

## 6. Regression tests (run after EVERY phase)

Folder (outside the repo, on purpose): `C:\Users\DELL\source\repos\BizTracker_regression\`

```
bash /c/Users/DELL/source/repos/BizTracker_regression/run_regression.sh      (timeout 900000 — takes ~7–9 min)
bash /c/Users/DELL/source/repos/BizTracker_regression/run_ui_only.sh         (only UiTests, for quick UI iterations)
```
Builds the app, creates the throw-away `biztracker_test` from `database/biztracker_db.sql` (fresh before each suite), runs the console
suites (`for suite in DataTests BusinessPermitTests HealthCertificateTests SanitaryPermitTests RptTests InspectionTests ConstructionTests`),
then `UiTests`, prints a summary and drops `biztracker_test`. The real `biztracker_db` is never touched. Screenshots + `results.txt` in `shots/`.

**For each new phase**
1. New console suite: copy a suite folder (e.g. `ConstructionTests`: App.config → biztracker_test, .vbproj referencing the app, change
   `RootNamespace`), test every service rule + the WORK_ORDER "Check" + owner/other-role restrictions + audit rows. Add it to the
   `for suite in …` line.
2. Extend `UiTests/Program.vb` **before the `' ---------- Dialogs ----------` block**: `DeepCheck(prefix, AppScreen.X, {"staffUser", "owner"},
   Sub(u, size, view) Check(..., CardValues(view), "...") End Sub)`, a second-business block (switch the toolbar ComboBox), `CheckDialog`
   for every dialog, plus one dialog with validation errors shown.
3. Overflow scanner target: **0** (controls outside parents, clipped labels/buttons, cut grid cells/headers, visible scrollbars, hint-less
   WheelScrollPanel, disabled buttons that look enabled).
4. **Look at the screenshots** (Read the PNGs). Notes: restored (min-size) MainForm shots are rendered with `DrawToBitmap` (CopyFromScreen
   captured VS Code instead) — in those shots the combo boxes look classic and the "Scroll for more" strip / placeholders may be missing
   (z-order artifact), that is not a bug. Maximized shots and dialogs use CopyFromScreen. A Windows "Low Disk Space" toast may cover the
   bottom-right corner.

---

## 7. How to run the app
```
dotnet run --project src/BizTracker      (XAMPP MySQL must be running)
```
Logins (password `password123`): admin, bplo, health, assessor, building, inspector, owner.

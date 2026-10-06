# BizTracker — Handoff for the remaining phases (14 → 15)

Updated at the end of the session that built Phases 11, 12 and 13 (2026-10-06).
Read **CLAUDE.md** and **WORK_ORDER.md** first, then this whole file. **CLAUDE.md wins** if anything here conflicts.

---

## 1. Where the project is

| Phase | Status | Commit |
|---|---|---|
| 1–10 (setup → Construction Permit) | ✅ done | `60fcecb Phase 10: Construction Permit` and earlier |
| 11 Dashboard | ✅ done | `e9feed3 Phase 11: Dashboard` |
| 12 Notifications / alerts | ✅ done | `95ecadd Phase 12: Notifications / alerts` |
| 13 Permit vault + printable documents | ✅ done | `9443685 Phase 13: Permit vault + printable documents` |
| 14 Admin tools | ⏳ **next** | — |
| 15 Testing, cleanup, demo prep | ⏳ | — |

- GitHub: `https://github.com/fannaaa-intel/biztracker.git`, branch **`main`**, remote `origin` set. `git push` after every phase commit.
- Home folder `C:\Users\DELL` is ALSO a git repo — always run git inside `C:\Users\DELL\source\repos\BizTracker`.
- Last regression (end of Phase 13), all green:
  DataTests 106 · BusinessPermitTests 68 · HealthCertificateTests 62 · SanitaryPermitTests 65 · RptTests 118 · InspectionTests 89 ·
  ConstructionTests 91 · DashboardTests 96 · AlertTests 65 · DocumentTests 111 · UiTests 396 · **overflow issues 0**.
- The user's real `biztracker_db` was **not** re-imported since the Phase 7 seed changes. Phases 8–13 needed **no** seed changes.
  **Ask before re-importing.** Notifications in the real DB are created at the user's first login (AlertService).
- **Display scaling:** the user says the PC runs at 150 %, but during Phases 11–13 it was actually **100 %** (UiTests logs
  `DPI scale: 1.00`; screenshots are 1920 px wide with a 260 px sidebar). Phases 11–13 were verified at 100 % only. Always check the
  `INFO DPI scale:` line in `shots/results.txt` and tell the user which scaling was tested. If they switch back to 150 %, re-run the
  regression and fix anything new (all new controls compute sizes from `DeviceDpi` / `Dpi()`, so it should hold).
- **MySQL port clash (seen in Phase 11):** a Windows service `MySQL80` (MySQL 8.0) can grab port 3306; then XAMPP MariaDB cannot start
  and the mysql client fails with `caching_sha2_password could not be loaded` (the app/test then hangs on a DB error box). Check with
  `netstat -ano | findstr :3306` and `Get-Service MySQL80`. Do not stop services yourself — ask the user (they start MySQL in XAMPP).

---

## 2. The user's standing rules (keep them exactly)

**Workflow**
1. Follow **CLAUDE.md** strictly. If something is unclear or conflicts with CLAUDE.md, **ask** — do not guess.
2. **One phase at a time.** Before coding a phase, show a short plan (files to create/change), then build it.
3. After coding: `dotnet build` with **0 errors, 0 warnings**.
4. Run the **regression** (section 6, timeout 900000). Add a `<Module>Tests` suite for new logic and a UI deep check of the new screen
   (DeepCheck + CheckDialog, both sizes, staff + owner where relevant, every tab and dialog). Verify the phase's "Check" from WORK_ORDER.md.
   **Open and look at the screenshots yourself** and fix every functional failure, overflow issue and visual problem; re-run until all green
   with 0 overflow issues.
5. Then **commit `Phase X: <name>` and push to origin main**.
6. Report **in the simplest form**: results table, what was done, bugs found and fixed, **what's left**, exactly what to click to test.
7. **STOP** and wait for the user to reply **"next"** ("Next proceed" = go). If they report a bug, fix it first. If they ask for several
   phases at once, still do them one by one (plan, build, regression, screenshots, commit + push each) and give one combined report.
   "Stop on phase X" = finish that phase's regression, commit + push, report, stop. Short questions while you work: answer briefly and continue.
8. Seed data changes go in **`database/biztracker_db.sql`**. **Never drop/re-import the user's `biztracker_db` without asking** —
   tests use the throw-away `biztracker_test` copy.
9. Don't write handoff documents unless the user asks.

**Access (CLAUDE.md roles table — updated in Phase 13)**
- Dashboard: Admin, BPLO, Owner. **Permit Vault** (own sidebar item, section "RECORDS"): Admin, BPLO, Owner (user's decision).
  Settings / admin tools: Admin only. Health / Assessor / Building / Inspector land on their own module.
- Owner = read-only for their own business; may **upload requirements and file applications** only.

**UI / UX (non-negotiable, asked every time)**
- Modern UI, same look as the existing modules; reuse the UI kit. Main window opens **maximized**.
- **NO visible scrollbars anywhere.** Grids: `ModernGrid` + `UiHelper.StyleGrid`. Lists: `WheelScrollPanel` ("Scroll for more ▾").
- **Nothing may overflow or be cut** at 1100×680 or maximized (at 150 % too). AutoEllipsis + tooltips, responsive grid columns
  (hide a column instead of cutting it — see `FitGridColumns` in ConstructionPermitView / RealPropertyTaxView / PermitVaultView),
  short button labels when narrow, short card notes. The overflow scanner does NOT see owner-drawn text or AutoEllipsis cuts — **look at the screenshots**.
- **ALL messages use the animated `ModernMessageBox`** via `UiHelper.ShowInfo / ShowSuccess / ShowWarning / ShowError /
  Confirm(message, title, yesText, noText, danger:=…, warning:=…)` — never `MessageBox.Show`. Success messages say what to do next.
- Disabled buttons look disabled (automatic) and have a tooltip explaining WHY (`toolbar.SetTip`). Every list has a helpful empty state.

---

## 3. Architecture already in place (reuse it — do not reinvent)

**Data/** (the ONLY place with SQL; parameterized; `Using` blocks) — `Db.vb` (`P`, `ExecuteNonQuery`, `ExecuteScalar`, `GetDataTable`,
`ExecuteInsert`, `RunInTransaction`, `NullIfEmpty`; DB errors show a friendly box — a console test hitting a DB error hangs on it).
Repositories (all `Shared`): Business (`GetAll(includeInactive)`, `GetById`, `Insert`, `Update`, `SoftDelete`), User (`GetAll`, `GetById`,
`GetByUsername`, `GetByBusinessId`, `Insert`, `Update`, `UpdatePassword`, `SetActive`, `UpdateLastLogin`), BusinessPermit (+ endorsements),
Requirement, Sanitary, Employee, HealthCertificate, Property, Rpt, Inspection, Construction, **Notification** (now JOINs `businesses`
for `BusinessName` and skips inactive businesses; `Upsert` = insert or refresh, escalation to Urgent makes it unread; `GetById`),
Settings (cached; `GetValue/GetInt/GetDecimal/Update` + reload), AuditLog (`Insert`, `GetRecent(limit)` — Phase 14 needs a filtered query), ReferenceNo.

**Services/** — module services with status constants, `Validate…`, `Get…Blocker`, actions returning an error message or Nothing,
role checks inside (`AccessService.CanManage(screen)`, `CanSeeBusiness(id)`, `CanAccess(screen)`), audit logging.
- BusinessPermitService, HealthCertificateService, SanitaryPermitService, RptService, InspectionService, ConstructionService (see earlier phases).
- **DashboardService** (Phase 11): `CanView`, `GetBusinesses` (owner's own / all active), `GetActionItems()` → `ActionItem` (BusinessId,
  Screen, ModuleName, RelatedId, Title, Detail, ShortDetail, Status, IsUrgent, DueDate), `GetModuleSummaries()` → `ModuleSummary`
  (Value, Status, Note, Detail, PendingCount, Progress/ProgressText/ProgressNote), `GetActionHeadline`, `DaysText`, `ShortDaysText`.
- **AlertService** (Phase 12): `GenerateAlerts()` → `AlertRunResult` (Added/Updated/Resolved) — health certs, Mayor's / sanitary permit
  validity, RPT quarters (overdue = Urgent, due soon = Warning), inspection / re-inspection visits (Info / Warning); resolved alerts are
  marked read. `BuildAlerts(b)`, `GetNotifications(unreadOnly)` (owner = own business, staff = modules they can open), `GetUnreadCount`,
  `GetUrgentUnread`, `GetUrgentSummary`, `GetUrgentTitle`, `MarkRead`, `MarkAllRead`, `ScreenFor(moduleName)`, `DueText`.
- **DocumentService** (Phase 13): `CanView`, `GetDocuments(businessId)` → `IssuedDocument` (Kind from `DocumentKinds`, Title, ReferenceNo,
  Holder, IssueDate, ValidUntil, Status, RelatedId, TableName, Screen, Fields, Rows), `CountByStatus`, `GetPrintBlocker` (expired =
  no print), `GetVerificationCode` (SHA-256, "XXXXX-XXXXX"), `FindByReference`, `Verify(ref, code)` → `VerifyResult`.
  Tax clearance number = `TC-YYYY-<business_id 5 digits>` (not stored).
- **ReportService** (Phase 13): `BuildHtml(doc)` fills `Templates/_layout.html` + `_header.html` + `_footer.html` + one template per kind
  (`{{key}}` values HTML-encoded, `{{rows}}` table rows), `Print(doc, openInBrowser)` → `PrintResult` (saves to
  `%TEMP%\BizTracker\Documents\<ref>.html`, opens the default browser, logs `PRINT`). Templates are copied to the output folder by the .vbproj.
- `StatusService`, `ReferenceNoService`, `AuditService` (`Log`, `LogInsert/Update/Delete/StatusChange/Login/Logout`), `AuthService`
  (`Login`, `Logout`, `HashPassword` (BCrypt), lockout after 5 failures), `RequirementService`, `AccessService`, `Session`.

**UI kit**
- `Theme.vb` (+ StatusGroup words), `ModernControls.vb` (RoundedPanel, NavButton, IconButton, Avatar, StatusBadge), `LayoutControls.vb`
  (StepTracker, SegmentedTabs, WheelScrollPanel, StatCard, ValidityBar), **`DashboardControls.vb`** (WelcomeBanner, ModuleCard,
  ProgressStrip, ActionRow — owner-drawn, DPI from `DeviceDpi`), `ModuleToolbar.vb`, `DetailRows.vb` (InfoRow, ListRow, NoteRow, HeadingRow),
  `ModernGrid` + `UiHelper.StyleGrid` + `AddGridColumn`, `UiHelper` (Dpi, messages, `CreateComboBox`, `CreateInputBox`, `FormatMoney/Date`).
- `ModernDialog` (+ `PromptDialog.Ask`), `WorkflowDialog`, `RequirementsPanel`, **`NotificationPopup`** (bell drop-down, borderless form),
  **`VerifyDocumentDialog`**. Module views to copy: **`PermitVaultView`**, `ConstructionPermitView`, `RealPropertyTaxView`, `AnnualInspectionView`.
- **Navigation API (Phase 11):** a view calls `RequestNavigate(screen, businessId)` (event `ModuleView.NavigateRequested`); MainForm
  (`NavigateTo`) opens the screen deferred and the target toolbar selects that business (`ModuleView.SetPendingBusiness` /
  `TakePendingBusiness`, honored by ModuleToolbar, BusinessPermitView, HealthCertificateView).
- **MainForm:** `New MainForm(openedAfterLogin:=True)` from Program.vb generates alerts and shows the urgent summary (tests use the default
  `New MainForm()` so no modal box appears). `OpenNotifications()` opens the bell popup. Sidebar sections OVERVIEW / SERVICES / RECORDS /
  ADMINISTRATION; rows switch to compact heights automatically when the window is short (`SetCompactNav`). Ctrl+1–9 shortcuts.
- `Modules/PlaceholderViews.vb` — only **`SettingsView`** is left (Phase 14 replaces it; move it to `Modules/SettingsView.vb` or an
  `AdminView` and delete the placeholder file / `ShowPlaceholder` if unused).

**DPI (must follow)**
- Module views are NOT auto-scaled: every pixel size set in a view/row in code → `Dpi(n)`. Owner-drawn controls compute from `DeviceDpi`.
- Dialogs: build at 100 % coordinates; `ModernDialog.OnLoad` scales them once. Don't call Dpi() inside dialogs.
- Child layout inside a scaled container: use the **`Layout`** event (or `OnLayout`), not `Resize`.

**Gotchas already hit (avoid them)**
- VB reserved words / Control member names as identifiers (`when`, `resume`, `err`, `text`, `name`, `left`, `width`, **`Scale`** —
  a property named Scale on a Control gives warning BC40004). Handler names like `Add_Click` clash with events.
- `List(Of T).Count(Function …)` → use `.AsEnumerable().Count(…)` or `.Where(…).Count()`. `String.Join("|", {ints})` is ambiguous in VB.
- Bash heredocs with `'` or python strings containing VB `'''` comments break — write a python script / text block with the Write tool.
- Disposing a control inside its own click → defer with `BeginInvoke` (ListRow links, ActionRow, NotificationRow already do).
- Grid min widths: dates 112, references 125–135 ("AIC-2026-00002" needs 129), status pills = text + 36. Long free text → hide the column.
- UiTests: a TopMost MainForm covers a ModernMessageBox (use non-TopMost when capturing one); wait until `box.Opacity = 1` before a screenshot.
  Restored (min-size) MainForm shots use `DrawToBitmap` (classic combos, no "Scroll for more" strip, no placeholders — not a bug).
- Tests: decimals with `.ToString("0.00")`; dates by value; adding a settings row → update DataTests "settings count" (now 14);
  a new AppScreen → update UiTests "Admin screens" count (now 9) and the role screen lists.
- Session-relative numbers in tests: seed max OR = 00031, INS = 00003, AIC = 00002, CP = 00001, LC = 00001, HC (Y) = 00008.

---

## 4. Seed data facts (today-relative; re-import refreshes them)

- Businesses: 1 Cristan's Bakeshop (food; owner user `owner`, user_id 7), 2 Santos General Merchandise, 3 Kusina ni Aling Rosa.
  Users (password `password123`): admin (1), bplo (2, Liza Ramirez), health (3), assessor (4), building (5), inspector (6), owner (7).
- Business permits: 1 MP-Y-00001 Issued; **2 BP-(Y+1)-00001 bakeshop Under Review** (Fire Pending); 3, 4 Issued; **5 BP-(Y+1)-00002 Kusina Submitted**.
- Health certs: bakeshop 10 → 8 Valid, Ben Reyes Expiring (+20 d), Lilah Espergal Expired (−5 d). Santos 2 valid. Kusina 3: Noel Ramos expired (−40 d).
- Sanitary: SP-Y-00001 / 00002 Issued, SP-Y-00003 Kusina For Inspection. RPT: bakeshop paid through Q3; Kusina Q2/Q3 overdue (₱262.50).
- Inspections: INS-Y-00001 bakeshop For Re-inspection (+30 d), INS-Y-00002 Santos Completed AIC-Y-00002, INS-Y-00003 Kusina Scheduled +14 d.
- Construction: CP-Y-00001 bakeshop, Building Permit stage, LC-Y-00001 approved.
- Dashboard: owner 4 action items; admin/BPLO 12. Alerts after login: 7 (bakeshop 3, Kusina 4). Vault: bakeshop 21 documents
  (19 valid / 1 expiring / 1 expired), Santos 6, Kusina 6. 14 settings keys incl. `sp_passing_score`.

---

## 5. Remaining phases — notes (WORK_ORDER.md has the official spec + "Check")

### Phase 14 — Admin tools (replace `SettingsView`; Admin only; sidebar item "Settings" under ADMINISTRATION)
- One screen with `SegmentedTabs`: **Users | Businesses | Settings | Audit Log | Backup** (same look as the modules: toolbar actions
  per tab, list card + detail card, ModernGrid + search box above every grid). New `AdminService` (or `UserService` + `BusinessService`
  + `SettingsService`) with validation dictionaries and role checks (`Session.IsAdmin`) + audit logging; dialogs on `ModernDialog`.
- **Users:** list (username, name, role, business, active, last login), Add, Edit role/name, Reset password (BCrypt via
  `AuthService.HashPassword`; show the temporary password once), Activate/Deactivate — **cannot deactivate yourself** (and keep at least one
  active Admin). Owner users must have a business; staff must not. Username unique.
- **Businesses:** registry (add / edit / deactivate = soft delete `is_active = 0`, never hard delete; FKs are RESTRICT) and
  **"Create Owner Account"** for a business (one owner per business — check `UserRepository.GetByBusinessId`).
- **Settings:** edit the `settings` table with validation per key (numbers / rates 0–1 / `MM-DD` date / text), `SettingsRepository.Update`
  + cache reload; audit UPDATE with old → new value.
- **Audit Log:** viewer with filters (date range, user, table, action) — add a parameterized filtered query to `AuditLogRepository`.
- **Backup:** `C:\xampp\mysql\bin\mysqldump.exe -u root biztracker_db` → `SaveFileDialog` (.sql). Run it from a service with `Process`
  (redirect stdout to the file), friendly error if the exe is missing. Tests must back up `biztracker_test`, never prompt.
- Check: create a new business + owner account from scratch and log in as that owner (test it in an `AdminTests` suite and UiTests).

### Phase 15 — Testing, cleanup, demo prep
- Review every form (validation, friendly errors, tab order, no SQL in forms, Using blocks, parameterized SQL); every grid has search;
  resizing never breaks layout. Remove leftovers (`ShowPlaceholder`, `PlaceholderViews.vb` if empty, unused code). Full regression.
- `README.md` (requirements, setup: XAMPP + import `database/biztracker_db.sql` + run, seeded logins, features) and
  `docs/TEST_CHECKLIST.md` (manual script per role with expected results — use the seed facts above).

---

## 6. Regression tests (run after EVERY phase)

Folder (outside the repo, on purpose): `C:\Users\DELL\source\repos\BizTracker_regression\`

```
bash /c/Users/DELL/source/repos/BizTracker_regression/run_regression.sh      (timeout 900000 — takes ~10–13 min)
bash /c/Users/DELL/source/repos/BizTracker_regression/run_ui_only.sh         (only UiTests)
bash /c/Users/DELL/source/repos/BizTracker_regression/dash_quick.sh          (UiTests "dash"  mode → shots_dash/)
bash /c/Users/DELL/source/repos/BizTracker_regression/notif_quick.sh         (UiTests "notif" mode → shots_notif/)
bash /c/Users/DELL/source/repos/BizTracker_regression/vault_quick.sh         (UiTests "vault" mode → shots_vault/)
```
`run_regression.sh` builds the app, creates the throw-away `biztracker_test` from `database/biztracker_db.sql` (fresh before each suite),
runs `for suite in DataTests BusinessPermitTests HealthCertificateTests SanitaryPermitTests RptTests InspectionTests ConstructionTests
DashboardTests AlertTests DocumentTests`, then `UiTests`, prints a summary and drops `biztracker_test`. Screenshots + `results.txt` in `shots/`.

**For each new phase**
1. New console suite: copy a suite folder (App.config → biztracker_test, .vbproj referencing the app, change `RootNamespace`), test every
   service rule + the WORK_ORDER "Check" + role restrictions + audit rows. Add it to the `for suite in …` line.
2. UiTests: add a `Sub <Name>Checks()` (see `DashboardChecks`, `NotificationChecks`, `VaultChecks`), call it before the
   `' ---------- Dialogs ----------` block, and add a quick mode (`args(1) = "admin"` etc.) plus a `<name>_quick.sh` copied from `vault_quick.sh`.
   Use `DeepCheck(prefix, AppScreen.X, users, extra)`, `CheckDialog`, a validation-errors dialog shot, `CardValues(view)`.
3. Overflow scanner target **0**. 4. **Look at the screenshots** (Read the PNGs) — at both sizes. A Windows Alt+Tab overlay or tooltip in a
   shot is a capture artifact, not a bug.

---

## 7. How to run the app
```
dotnet run --project src/BizTracker      (XAMPP MySQL must be running on port 3306)
```
Logins (password `password123`): admin, bplo, health, assessor, building, inspector, owner.

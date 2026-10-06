# BizTracker — Work Order for Claude Code

How to use this file:
- Put `CLAUDE.md` and `WORK_ORDER.md` in your project root folder (`BizTracker/`).
- Do ONE phase at a time. Copy the prompt into Claude Code, let it finish, then do the "Check" yourself.
- After each phase passes the check: `git add . && git commit -m "Phase X done"`.
- Type `/clear` in Claude Code before starting a new phase (CLAUDE.md is reloaded automatically).
- For big phases, press Shift+Tab to use Plan Mode first, review the plan, then approve.

---

## Phase 0 — Setup (you do this manually, no Claude Code)
1. Install XAMPP → open XAMPP Control Panel → Start **Apache** and **MySQL**.
2. Install the **.NET 8 SDK** and **Visual Studio 2022** (workload: ".NET desktop development"). VS is for the form designer and debugging.
3. Install Git. Create folder `BizTracker/`, put CLAUDE.md and WORK_ORDER.md inside, run `git init`.
4. Open PowerShell in `BizTracker/` and run `claude`. (Run it on Windows itself, not WSL — WinForms only runs on Windows.)

---

## Phase 1 — Project scaffold + DB connection
```
Read CLAUDE.md. Do Phase 1 only:
- Create the folder structure from CLAUDE.md.
- Create a .NET 8 VB.NET Windows Forms project at src/BizTracker (target net8.0-windows).
- Add NuGet packages MySql.Data and BCrypt.Net-Next.
- Add App.config with the connection string for biztracker_db.
- Create Data/Db.vb with shared helpers: GetConnection, ExecuteNonQuery, ExecuteScalar,
  GetDataTable, all accepting parameters as a Dictionary or MySqlParameter array.
- Add a temporary button on the startup form that tests the connection and shows a MessageBox.
- Add .gitignore for bin/, obj/, .vs/, uploads/.
- Run dotnet build and fix errors.
```
**Check:** App runs, button says connected (after Phase 2 creates the DB, or create an empty `biztracker_db` in phpMyAdmin first).

---

## Phase 2 — Database schema + seed data
```
Read CLAUDE.md. Do Phase 2 only:
- Write database/biztracker_schema.sql: DROP/CREATE DATABASE biztracker_db, all tables from
  CLAUDE.md with primary keys, foreign keys (ON DELETE RESTRICT), ENUMs for status fields,
  indexes on foreign keys and reference_no, InnoDB, utf8mb4.
- Write database/biztracker_seed.sql with ONE consistent story:
  * Business: "Cristan's Bakeshop", food business, Aparri, Cagayan.
  * Users: one per role (admin, bplo, health, assessor, building, inspector, owner linked to the
    bakeshop). All passwords "password123" stored as BCrypt hashes.
  * 1 business permit under review with endorsements (Barangay endorsed, Sanitary endorsed,
    RPT endorsed, Fire pending, Zoning endorsed).
  * 1 active sanitary permit.
  * 10 employees with health certificates: 8 valid, 1 expiring in 20 days, 1 expired 5 days ago.
  * 2 properties with current-year assessments; Q1-Q3 paid, Q4 not yet due.
  * 1 annual inspection scheduled 30 days from today, Structural/Electrical/Mechanical passed, Fire pending.
  * 1 construction project at "Building Permit" stage with Locational clearance approved.
  * settings rows for expiry_warning_days=30, surcharge and interest rates, RPT rates.
  Use dates relative to CURDATE() so alerts always work.
- Also write a short database/README.md: how to import both files in phpMyAdmin.
```
**Check:** Import both files in phpMyAdmin with no errors. Open a few tables and confirm the data makes sense.

---

## Phase 3 — Models, repositories, core services
```
Read CLAUDE.md. Do Phase 3 only:
- Create a Model class for every table in Models/.
- Create Repository classes in Data/ (BusinessRepository, BusinessPermitRepository,
  RequirementRepository, SanitaryRepository, EmployeeRepository, HealthCertificateRepository,
  PropertyRepository, RptRepository, InspectionRepository, ConstructionRepository,
  NotificationRepository, UserRepository, SettingsRepository) with GetAll, GetById,
  GetByBusinessId, Insert, Update, and SoftDelete/Delete where appropriate. Parameterized SQL only.
- Create Services: Session (current user + role + business_id), StatusService
  (computes Valid/Expiring Soon/Expired and Overdue from dates and settings),
  ReferenceNoService, AuditService.
- Build and fix errors.
```
**Check:** Builds clean. Ask Claude Code to add a temporary test button that prints the count of employees with "Expiring Soon" — it should say 1.

---

## Phase 4 — Login, roles, main shell, theme
```
Read CLAUDE.md. Do Phase 4 only:
- Helpers/Theme.vb with colors/fonts from CLAUDE.md.
- LoginForm: username, password, show/hide password, login button. Verify with BCrypt via
  AuthService. Lock out after 5 failed attempts for 1 minute. Log logins to audit_log.
- MainForm: left sidebar (Dashboard, Business Permits, Sanitary Permits, Real Property Tax,
  Health Certificates, Annual Inspections, Construction Permit, Settings), top bar with
  logged-in name/role, notification bell placeholder, and logout.
- Hide sidebar items the role cannot access (see roles table in CLAUDE.md).
- Create an empty placeholder UserControl for each module in Modules/ and load it into the
  content panel when clicked. Highlight the active sidebar item.
- Program.vb starts at LoginForm. Remove the Phase 1 test button.
```
**Check:** Log in as each seeded user. Owner and Health should see different sidebars. Logout returns to login.

---

## Phase 5 — Business Permit module (the template for all modules)
```
Read CLAUDE.md. Do Phase 5 only. Build Modules/BusinessPermitView:
- Top: business selector (staff roles) or fixed business (Owner).
- Card: current permit reference, status badge, valid until.
- DataGridView of permit applications with search box.
- Buttons: New Application, Renew Permit, View/Edit, Update Status (staff only), Delete (Admin only, only if status = Submitted).
- Add/Edit dialog form with validation (required fields, valid year).
- Status flow per CLAUDE.md; Update Status only allows the next valid status.
- Filing progress tracker (Submission → Document Review → Multi-Agency Clearance → Final Assessment → Issuance).
- Clearance endorsements panel (Barangay, Sanitary, RPT, Fire, Zoning) — staff can mark Endorsed/Rejected.
- Requirements panel: shared reusable UserControl "RequirementsPanel" that lists documents,
  lets Owner upload (OpenFileDialog → copy into uploads/<business_id>/), and staff Verify/Reject.
  Make it reusable for other modules.
- Late renewal computation: if renewed after Jan 20, show surcharge + interest using settings.
- Log all changes to audit_log.
Keep this module clean — later modules will copy its structure.
```
**Check:** Create, edit, advance status to Issued, upload a file, verify it. Test the Owner can't change status.

---

## Phase 6 — Health Certificates module
```
Read CLAUDE.md. Do Phase 6 only. Follow the same structure as BusinessPermitView.
Build Modules/HealthCertificateView:
- Summary cards: Total Personnel, Valid, Expiring (30 days), Expired, Compliance % (Valid / Total).
- "Renew Now" list of expiring and expired staff.
- Grid of employees: name, position, category, certificate no, issue date, expiry date, computed status (colored).
- Add/Edit employee, Deactivate employee, Issue/Renew certificate (auto expiry = issue + 1 year,
  auto certificate_no), Renew Selected (multi-select).
- All counts computed from the database via StatusService.
```
**Check:** With seed data, cards show 10 / 8 / 1 / 1 and 80%. Renewing the expired one changes counts immediately.

---

## Phase 7 — Sanitary Permit module
```
Read CLAUDE.md. Do Phase 7 only. Same structure as BusinessPermitView.
Build Modules/SanitaryPermitView: permit card, status/expiry, workflow tracker
(Intake → Lab Analysis → On-site Inspection → Permit Issued), inspection score + findings
(Health role can record), prerequisites list using RequirementsPanel (water test,
physico-chemical analysis, pest control certificate), a staff health-card sync card that reads
counts from HealthCertificate data, Add/Edit/Issue/Renew. Expiry = Dec 31 of issue year.
When a sanitary permit is Issued, set the Sanitary endorsement on the current business permit to Endorsed.
```
**Check:** Issuing a sanitary permit updates the Business Permit endorsement panel.

---

## Phase 8 — Real Property Tax module
```
Read CLAUDE.md. Do Phase 8 only. Same structure as BusinessPermitView.
Build Modules/RealPropertyTaxView: property ledger grid (PIN, TD no, location, type,
assessed value, payment status per quarter), Add/Edit property, Generate assessment for a
year (basic + SEF from settings), Record quarterly payment (OR no auto-generated, penalty
computed if late), total due card, overdue highlight, important dates panel.
"Tax Clearance" status only shows Issued when there is no unpaid due amount.
When all due quarters are paid, set the RPT endorsement on the business permit to Endorsed.
```
**Check:** Total due equals the sum of unpaid assessments. Assessed ₱40,000 → annual tax ₱800 (2%).

---

## Phase 9 — Annual Inspection module
```
Read CLAUDE.md. Do Phase 9 only. Same structure as BusinessPermitView.
Build Modules/AnnualInspectionView: schedule card, department checklist
(Structural, Electrical, Mechanical, Fire) with result + findings + inspector, deficiency notes,
re-inspection scheduling, endorsement count ("3 of 4 Depts Passed"). Inspector role records
results. Inspection is Completed only when all 4 passed; then enable the certificate.
When Fire passes, set the Fire endorsement on the business permit to Endorsed.
```
**Check:** Passing Fire makes it 4 of 4, unlocks the certificate, and updates the BP endorsement.

---

## Phase 10 — Construction Permit module
```
Read CLAUDE.md. Do Phase 10 only. Same structure as BusinessPermitView.
Build Modules/ConstructionPermitView: project list, New Project, stage pipeline
(Locational → Building Permit → Construction → Occupancy → Completed), clearances grid
(Locational, Building, Occupancy, FSEC), approve/reject per clearance (Building role),
blueprints/technical docs via RequirementsPanel. A stage can only advance if its required
clearance is approved. Building permit cannot be approved until FSEC is approved.
When Locational is approved, set the Zoning endorsement on the business permit to Endorsed.
```
**Check:** Try to skip a stage — it must be blocked.

---

## Phase 11 — Dashboard
```
Read CLAUDE.md. Do Phase 11 only. Build Modules/DashboardView:
- "Welcome, <name>" and "Action Required: N items need attention" where N is computed
  (expiring/expired health certs, overdue RPT, pending endorsements, failed inspection items,
  permit expiring within 30 days).
- Six cards (one per module) showing live status, key reference, and a button that opens that module.
- For staff roles: summary counts across ALL businesses (pending applications per module).
- Everything must come from the repositories/StatusService — no hard-coded values.
```
**Check:** Every number on the dashboard matches the module screens exactly.

---

## Phase 12 — Notifications / alerts
```
Read CLAUDE.md. Do Phase 12 only.
- AlertService.GenerateAlerts(): runs after login; scans all expiry/due dates within
  expiry_warning_days and overdue items; inserts notifications (no duplicates for the same item and due date).
- Bell icon in the top bar with unread count; clicking opens a list; mark as read / mark all read;
  clicking a notification opens the related module.
- Pop-up summary after login if there are urgent (expired/overdue) items.
```
**Check:** Log in as owner → bell shows the expiring health cert and other alerts. Log in again → no duplicates.

---

## Phase 13 — Permit vault + printable documents
```
Read CLAUDE.md. Do Phase 13 only.
- ReportService: fill HTML templates in Templates/ and open in the default browser for printing/PDF.
- Templates: Mayor's/Business Permit, Sanitary Permit, Health Certificate (card size),
  Tax Order of Payment, RPT Official Receipt / Tax Clearance, Annual Inspection Certificate,
  Building/Occupancy clearance. Include LGU header, reference no, dates, and a verification code.
- Add a "Permit Vault" area (Dashboard or its own sidebar item) listing all issued documents
  with Print/Download buttons. Only Issued documents can be printed.
- Add a "Verify Document" screen: enter a reference no and show whether it is valid.
```
**Check:** Print each document type; reference numbers match the database.

---

## Phase 14 — Admin tools
```
Read CLAUDE.md. Do Phase 14 only (Admin role).
- User Management: list, add, edit role, reset password, activate/deactivate (cannot deactivate yourself).
- Business registry: add/edit/deactivate businesses; create the Owner account for a business.
- Settings screen: edit settings table values with validation.
- Audit Log viewer with filters (date range, user, table).
- Backup button: run mysqldump from XAMPP path and save a .sql file to a chosen folder.
```
**Check:** Create a new business + owner account from scratch and log in as that owner.

---

## Phase 15 — Testing, cleanup, demo prep
```
Read CLAUDE.md. Do Phase 15 only.
- Review every form for: input validation, friendly error messages, tab order,
  no SQL in forms, Using blocks, parameterized queries. Fix what you find.
- Make sure every grid has search and that window resizing doesn't break layouts.
- Write README.md: requirements, setup steps (XAMPP, import SQL, run), seeded logins, and features list.
- Write docs/TEST_CHECKLIST.md: a manual test script per role with expected results.
```
**Check:** Follow TEST_CHECKLIST.md yourself for every role. Then take screenshots for the paper.

---

## After the system is done
Update the paper to match: Scope & Limitations, Technology Constraint, SDLC justification,
Expected Outcome, Gantt chart, DFD (Level 0 + Level 1 with all external entities and labeled flows),
Use Case diagram (all roles), ERD (new — export from phpMyAdmin Designer), and new screenshots.

**Minimum demo if time runs out:** Phases 1–6 + Phase 11 (Dashboard).

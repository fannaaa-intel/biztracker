# BizTracker — Business Permit and Regulatory Compliance System

A Windows desktop app for a Philippine municipality (LGU) that tracks six regulatory services for local businesses
in one place. It follows RA 7160 (Local Government Code) and RA 11032 (Ease of Doing Business).

| # | Service | Office |
|---|---|---|
| 1 | Business Permit (New / Renewal) | BPLO |
| 2 | Sanitary Permit | Municipal Health Office |
| 3 | Real Property Tax (RPT) | Assessor / Treasurer |
| 4 | Health Certificates (Food Handler / Non-Food) | Municipal Health Office |
| 5 | Annual Inspection (Structural, Electrical, Mechanical, Fire) | Joint Inspection Team |
| 6 | Construction Permit (Locational/Zoning → Building → Occupancy) | OBO / MPDO |

Built with VB.NET, Windows Forms (.NET 8) and MySQL (MariaDB via XAMPP).

---

## Requirements

| What | Version / notes |
|---|---|
| Windows | 10 or 11 (Windows Forms runs on Windows only) |
| .NET 8 SDK | <https://dotnet.microsoft.com/download/dotnet/8.0>. Check with `dotnet --version` (8.x). |
| XAMPP | Includes MySQL (MariaDB 10.4), phpMyAdmin and `mysqldump` (used by the Backup screen). Install it in `C:\xampp`. |
| Web browser | Any. Printable documents open in your default browser, where you can print or save them as PDF. |
| Visual Studio 2022 (optional) | Workload ".NET desktop development", if you want to debug. |

NuGet packages (restored automatically on the first build): `MySql.Data` and `BCrypt.Net-Next`.

---

## Setup

### 1. Start MySQL
1. Open the **XAMPP Control Panel**.
2. Click **Start** next to **Apache** and **MySQL**. Apache is needed only for phpMyAdmin.

> If MySQL will not start, another MySQL server may be using port 3306 (for example a Windows service called `MySQL80`).
> Stop that service in *Services* (`services.msc`), then start MySQL in XAMPP again.

### 2. Create the database
1. Go to <http://localhost/phpmyadmin>.
2. Click **Import**, choose **`database/biztracker_db.sql`** and click **Import**. You can also paste the file into the **SQL** tab.
3. This creates the database `biztracker_db` with all 18 tables and the sample data.

Command-line alternative (Git Bash):
```
/c/xampp/mysql/bin/mysql.exe -u root < database/biztracker_db.sql
```

> Importing the file again **resets everything** to the sample data. Sample dates are relative to the import day,
> so re-import on the morning of a demo to get fresh "Expiring Soon" and "Overdue" examples.
> See [database/README.md](database/README.md) for the table list and the sample story.

### 3. Run the app
From the project folder:
```
dotnet run --project src/BizTracker
```
Or open `BizTracker.sln` in Visual Studio and press **F5**.

The connection string is in **`src/BizTracker/App.config`** (the only place it lives):
`server=localhost;port=3306;database=biztracker_db;uid=root;pwd=;`. Change it there if your MySQL user or password is different.

---

## Sample logins

Every password is **`password123`**.

| Username | Role | What they can open |
|---|---|---|
| `admin` | Admin | Everything, plus **Settings** (users, business registry, settings, audit log, backup) |
| `bplo` | BPLO | Dashboard, Business Permits, Permit Vault (all businesses) |
| `health` | Health | Sanitary Permits, Health Certificates |
| `assessor` | Assessor | Real Property Tax |
| `building` | Building | Construction Permit |
| `inspector` | Inspector | Annual Inspections |
| `owner` | Owner of *Cristan's Bakeshop* | Read-only view of their own business: all six modules, Dashboard and Permit Vault. Can upload requirements and file applications. |

After 5 wrong passwords a username is locked for 1 minute.

---

## Features

**Everywhere**
- Role-based sidebar: each role sees only the screens it may open. Owners see only their own business.
- Live data on every screen. Date-based statuses (Valid / Expiring Soon / Expired, Overdue) are computed from today's date.
  The "Expiring Soon" window is 30 days and can be changed in Settings.
- Search box above every list, color-coded status badges (green = valid/paid/approved, amber = pending/expiring, red = expired/overdue/rejected).
- Reference numbers in the format `PREFIX-YYYY-00001` (BP, MP, SP, HC, INS, AIC, CP, LC, OR...).
- Every important change is written to the audit log.
- Shortcuts: **Ctrl+1–9** switch screens, **F5** refreshes the current screen.

**Dashboard** (Admin, BPLO, Owner)
- "Action Required" list: expiring or expired health certificates, overdue RPT, pending endorsements, failed inspection items, and permits expiring within 30 days.
- One card per service with its live status. Click **Open** to go to that module.

**Notifications**
- The bell in the top bar shows the number of unread alerts. They are generated after each sign-in, without duplicates.
- A summary pops up after sign-in when something is urgent. Click an alert to open its module.

**Business Permits**
- New application and renewal, with a status flow: Submitted → Under Review → For Assessment → Assessed → Paid → Issued (or Rejected).
- Five clearance endorsements (Barangay, Sanitary, RPT, Fire, Zoning), plus a requirements checklist with file upload and verification.
- Late renewal after January 20 adds a 25% surcharge and 2% interest per month (maximum 36 months).

**Sanitary Permits**
- Intake → Lab Analysis → On-site Inspection → Permit Issued.
- Inspection score and findings, prerequisite documents, and a staff health-card summary.
- Valid until December 31. Issuing the permit endorses Sanitary on the business permit.

**Real Property Tax**
- Property ledger.
- Yearly assessment: basic 1% + SEF 1% of the assessed value.
- Quarterly payments with an Official Receipt number, and a 2% per month late penalty (maximum 72%).
- Tax clearance. When every due quarter is paid, RPT is endorsed on the business permit.

**Health Certificates**
- Personnel list with Valid / Expiring Soon / Expired, the compliance %, and a "Renew Now" list.
- Issue or renew a certificate (valid 1 year), or renew several at once.

**Annual Inspections**
- Schedule an inspection.
- Results per department (Structural, Electrical, Mechanical, Fire), deficiencies and re-inspection.
- The certificate unlocks when all 4 departments pass. Passing Fire endorses Fire on the business permit.

**Construction Permit**
- Locational → Building Permit → Construction → Occupancy → Completed. A stage can't be skipped.
- Clearances: Locational, FSEC, Building and Occupancy. Building needs FSEC first.
- Technical documents. Approving the Locational clearance endorses Zoning.

**Permit Vault** (Admin, BPLO, Owner)
- Every issued document, ready to print: Mayor's Permit, Sanitary Permit, Health Certificate, Tax Order of Payment,
  RPT Official Receipt and Tax Clearance, Annual Inspection Certificate, Building/Occupancy clearances.
- Documents open as a printable page in the browser, with the LGU header and a verification code.
- **Verify Document**: enter a reference number and code to check whether a document is genuine and still valid.

**Settings** (Admin only)
- **Users:** add accounts, edit name and role, reset passwords (a temporary password is shown once), and activate/deactivate.
  You cannot deactivate yourself, and at least one active Admin always remains.
- **Businesses:** register, edit, deactivate/reactivate (a soft delete that also turns off the owner's login),
  and **Create Owner Account** (one per business).
- **Settings:** rates, deadlines and names, each checked by type before saving.
- **Audit Log:** who did what and when, filtered by date range, user, table and action.
- **Backup:** saves the whole database as a `.sql` file (restore it with phpMyAdmin → Import).

---

## Project structure

```
BizTracker/
├─ README.md                  this file
├─ CLAUDE.md, WORK_ORDER.md   project rules and build plan
├─ database/
│  ├─ biztracker_db.sql       database + 18 tables + sample data (source of truth)
│  └─ README.md               import / reset steps, table list, sample story
├─ docs/
│  └─ TEST_CHECKLIST.md       manual test script per role
├─ src/BizTracker/
│  ├─ App.config              connection string
│  ├─ Data/                   Db helper + repositories (the only place with SQL; parameterized queries)
│  ├─ Models/                 one class per table
│  ├─ Services/               business rules, role checks, status computation, audit log, reports, backup
│  ├─ Forms/                  login, main window, dialogs
│  ├─ Modules/                one screen per sidebar item
│  ├─ Helpers/                theme (colors/fonts), shared UI controls
│  └─ Templates/              HTML templates for the printable documents
└─ uploads/                   uploaded requirement files, stored as uploads/<business_id>/ (not in git)
```

---

## Testing

A manual test script for every role is in **[docs/TEST_CHECKLIST.md](docs/TEST_CHECKLIST.md)**.
Re-import `database/biztracker_db.sql` before you start, so the numbers match.

## Troubleshooting

| Message | Fix |
|---|---|
| "Cannot connect to the MySQL server" | Start **MySQL** in the XAMPP Control Panel (and see the port 3306 note above). |
| "The database 'biztracker_db' does not exist yet" | Import `database/biztracker_db.sql` (Setup step 2). |
| A document does not open | Make sure a default web browser is set in Windows. |
| "mysqldump.exe was not found" (Backup) | Install XAMPP in `C:\xampp`. |

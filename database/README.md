# BizTracker Database

Everything lives in one file: **`biztracker_db.sql`**. It creates the database, all 18 tables, and the sample data.
This file is the source of truth for the schema.

- Server: XAMPP MySQL (MariaDB) on `localhost:3306`
- Database: `biztracker_db` (utf8mb4, InnoDB)
- Login: user `root`, empty password (matches `src/BizTracker/App.config`)

## Import (first time)
1. Open the **XAMPP Control Panel** and start **Apache** and **MySQL**.
2. Go to <http://localhost/phpmyadmin>.
3. Click the **SQL** tab at the top. You don't need to select a database first.
4. Open `biztracker_db.sql` in a text editor, copy everything, paste it into the box, and click **Go**.
   You can also use the **Import** tab: choose the file, then click **Import**.
5. At the bottom you should see a quick-check result listing the bakeshop's employees with
   8 `Valid`, 1 `Expiring Soon` and 1 `Expired`.

## Reset to fresh sample data
Run the same file again with the same steps. It drops all 18 tables and recreates them, which **deletes all data**
(including anything entered through the app), then re-seeds them.

Seed dates are relative to the day you import (`CURDATE()`), so "Expiring Soon" and "Overdue" alerts always
show up during a demo. **Re-import the file on the morning of a demo** so the dates are fresh.

Command-line alternative (Git Bash):
```
/c/xampp/mysql/bin/mysql.exe -u root < database/biztracker_db.sql
```

## Tables
| # | Table | What it stores |
|---|---|---|
| 1 | `businesses` | Registered businesses (name, owner, address, barangay, line of business, food/non-food). Soft delete via `is_active`. |
| 2 | `users` | Login accounts with a BCrypt password hash and role. Owner accounts are linked to one business. |
| 3 | `business_permits` | Mayor's/Business Permit applications (New/Renewal) per year, with status flow, assessment, surcharge and interest. |
| 4 | `clearance_endorsements` | The five office endorsements (Barangay, Sanitary, RPT, Fire, Zoning) for each business permit application. |
| 5 | `requirements` | Document checklist shared by all six modules. Uploaded files are stored as relative paths. |
| 6 | `sanitary_permits` | Sanitary permit applications, inspection score/findings, and validity (expires Dec 31). |
| 7 | `employees` | Staff of each business (Food Handler / Non-Food). |
| 8 | `health_certificates` | Health certificates per employee (valid 1 year). Status is computed from `expiry_date`. |
| 9 | `properties` | Real properties (land, building or machinery) with PIN, Tax Declaration no. and assessed value. |
| 10 | `rpt_assessments` | Yearly RPT assessment per property: basic tax 1% + SEF 1%. |
| 11 | `rpt_payments` | Quarterly RPT payments with Official Receipt no. and late penalty. |
| 12 | `inspections` | Annual joint inspection per business: schedule, re-inspection, overall result, certificate no. |
| 13 | `inspection_items` | Per-department result (Structural, Electrical, Mechanical, Fire) for each inspection. |
| 14 | `construction_projects` | Construction permit projects and their current stage (Locational → Building Permit → Construction → Occupancy → Completed). |
| 15 | `construction_clearances` | Locational, FSEC, Building and Occupancy clearances for each project. |
| 16 | `notifications` | Expiry and overdue alerts per business. Empty at first; the app's AlertService fills it. |
| 17 | `settings` | Rates and options (warning days, surcharge, interest, RPT rates, etc.) so they can change without code edits. |
| 18 | `audit_log` | Who did what and when (logins, inserts, updates, status changes, uploads, prints). |

## Seeded logins
All passwords are **`password123`**.

| Username | Role | Full name | Business |
|---|---|---|---|
| `admin` | Admin | System Administrator | — |
| `bplo` | BPLO | Liza Ramirez | — |
| `health` | Health | Dr. Paolo Agustin | — |
| `assessor` | Assessor | Ramon Villanueva | — |
| `building` | Building | Engr. Teresa Castillo | — |
| `inspector` | Inspector | Insp. Mark Del Rosario | — |
| `owner` | Owner | Cristan Dela Cruz | Cristan's Bakeshop (business_id 1) |

## The sample story
The main business is **Cristan's Bakeshop** (Centro 01, Aparri, Cagayan):
- **Business permit:** the current-year permit is Issued. Next year's early renewal is *Under Review*, with every endorsement in except **Fire (Pending)**.
- **Sanitary permit:** the current-year Food permit is Issued (score 96).
- **Health certificates:** 10 employees. 8 are valid, **Ben Reyes expires in 20 days**, and **Lilah Espergal expired 5 days ago**.
- **Real property tax:** 2 properties (land ₱40,000 and building ₱15,000 assessed). Every quarter that is already due has been paid.
- **Annual inspection:** Structural, Electrical and Mechanical passed. **Fire failed** (exit door #2), with a re-inspection in 30 days.
- **Construction permit:** "Warehouse Renovation & Extension" is at the *Building Permit* stage. Locational is approved; FSEC, Building and Occupancy are pending.

Two other businesses make the staff dashboards realistic:
- **Santos General Merchandise:** everything compliant.
- **Kusina ni Aling Rosa:** a renewal *Submitted*, 1 expired health certificate, RPT quarters overdue after Q1, a sanitary permit *For Inspection*, and an inspection scheduled in 14 days.

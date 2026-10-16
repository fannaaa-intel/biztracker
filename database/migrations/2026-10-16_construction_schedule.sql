-- =====================================================================
--  Migration: Construction project estimated start / end dates (Revision 4A)   2026-10-16
--  For an EXISTING biztracker_db. Keeps all data. Safe to run more than once.
--  Run it after the 2026-10-15_* migrations.
--
--  phpMyAdmin: click the SQL tab, paste this whole file, click Go.
--  Command line (Git Bash):
--    /c/xampp/mysql/bin/mysql.exe -u root < database/migrations/2026-10-16_construction_schedule.sql
--
--  Existing projects get no dates (the app shows "No Schedule" until staff enter them).
--  The schedule status (Not Started / On Schedule / Behind Schedule) is computed by the app, not stored.
--  The last result is a check: projects whose end date is before their start date -> must be 0.
-- =====================================================================

USE biztracker_db;

ALTER TABLE construction_projects
  ADD COLUMN IF NOT EXISTS est_start_date DATE NULL AFTER date_filed,
  ADD COLUMN IF NOT EXISTS est_end_date   DATE NULL AFTER est_start_date;

ALTER TABLE construction_projects
  ADD CONSTRAINT IF NOT EXISTS chk_cp_schedule
  CHECK (est_start_date IS NULL OR est_end_date IS NULL OR est_end_date >= est_start_date);

SELECT COUNT(*) AS bad_schedules
FROM construction_projects
WHERE est_start_date IS NOT NULL AND est_end_date IS NOT NULL AND est_end_date < est_start_date;

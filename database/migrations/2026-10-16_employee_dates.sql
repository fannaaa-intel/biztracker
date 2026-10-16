-- =====================================================================
--  Migration: Employee hire / separation dates (Revision 4B)   2026-10-16
--  For an EXISTING biztracker_db. Keeps all data. Safe to run more than once.
--
--  phpMyAdmin: click the SQL tab, paste this whole file, click Go.
--  Command line (Git Bash):
--    /c/xampp/mysql/bin/mysql.exe -u root < database/migrations/2026-10-16_employee_dates.sql
--
--  Existing employees get no dates ("Not recorded" in the app until staff enter them).
--  is_active stays the "still working" flag: 0 = separated.
--  The last result is a summary per business: active / separated employees.
-- =====================================================================

USE biztracker_db;

ALTER TABLE employees
  ADD COLUMN IF NOT EXISTS date_hired     DATE NULL AFTER created_at,
  ADD COLUMN IF NOT EXISTS date_separated DATE NULL AFTER date_hired;

ALTER TABLE employees
  ADD CONSTRAINT IF NOT EXISTS chk_emp_dates
  CHECK (date_hired IS NULL OR date_separated IS NULL OR date_separated >= date_hired);

SELECT b.business_name, SUM(e.is_active = 1) AS active_employees, SUM(e.is_active = 0) AS separated_employees
FROM businesses b JOIN employees e ON e.business_id = b.business_id
GROUP BY b.business_id, b.business_name
ORDER BY b.business_name;

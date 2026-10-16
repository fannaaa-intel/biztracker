-- =====================================================================
--  Migration: Terms and Conditions acceptance on login (Revision 4E)   2026-10-17
--  For an EXISTING biztracker_db. Keeps all data. Safe to run more than once.
--
--  phpMyAdmin: click the SQL tab, paste this whole file, click Go.
--  Command line (Git Bash):
--    /c/xampp/mysql/bin/mysql.exe -u root < database/migrations/2026-10-17_terms_acceptance.sql
--
--  Existing users have not accepted yet: each one sees the Terms screen once, on the next login.
--  The last result lists the current terms version and how many users still have to accept it.
-- =====================================================================

USE biztracker_db;

ALTER TABLE users
  ADD COLUMN IF NOT EXISTS terms_accepted_version VARCHAR(20) NULL AFTER created_at,
  ADD COLUMN IF NOT EXISTS terms_accepted_at      DATETIME    NULL AFTER terms_accepted_version;

INSERT IGNORE INTO settings (setting_key, setting_value, description) VALUES
('terms_version', '1.0', 'Version of the Terms and Conditions; change it to make every user accept again');

SELECT s.setting_value AS terms_version,
       SUM(u.terms_accepted_version IS NULL OR u.terms_accepted_version <> s.setting_value) AS users_to_accept
FROM settings s CROSS JOIN users u
WHERE s.setting_key = 'terms_version' AND u.is_active = 1
GROUP BY s.setting_value;

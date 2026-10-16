-- =====================================================================
--  Migration: RA 11032 processing time limits for the Admin queue (Revision 4C)   2026-10-16
--  For an EXISTING biztracker_db. Keeps all data (existing values are not overwritten). Safe to run more than once.
--
--  phpMyAdmin: click the SQL tab, paste this whole file, click Go.
--  Command line (Git Bash):
--    /c/xampp/mysql/bin/mysql.exe -u root < database/migrations/2026-10-16_processing_times.sql
--
--  Adds three settings (working days): simple 3, complex 7, highly technical 20.
--  The last result lists them.
-- =====================================================================

USE biztracker_db;

INSERT IGNORE INTO settings (setting_key, setting_value, description) VALUES
('arta_simple_days',    '3',  'RA 11032 processing time, simple transactions (working days)'),
('arta_complex_days',   '7',  'RA 11032 processing time, complex transactions (working days)'),
('arta_technical_days', '20', 'RA 11032 processing time, highly technical transactions (working days)');

SELECT setting_key, setting_value FROM settings WHERE setting_key LIKE 'arta\_%' ORDER BY setting_key;

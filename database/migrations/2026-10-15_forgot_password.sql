-- =====================================================================
--  Migration: Forgot Password (Revision 1)          2026-10-15
--  For an EXISTING biztracker_db. Keeps all data.
--
--  phpMyAdmin: click the SQL tab, paste this whole file, click Go.
--  Command line (Git Bash):
--    /c/xampp/mysql/bin/mysql.exe -u root < database/migrations/2026-10-15_forgot_password.sql
--
--  Safe to run more than once: every step checks first ("IF NOT EXISTS",
--  "only where email IS NULL"), and no step can hit the UNIQUE e-mail rule.
--  The last result lists the accounts that still have NO e-mail, with the reason.
--  Fix those in the app: Settings > Users > Edit User > E-mail.
-- =====================================================================

USE biztracker_db;

-- 1. users.email (nullable for now, so existing accounts keep working)
ALTER TABLE users
  ADD COLUMN IF NOT EXISTS email VARCHAR(150) NULL AFTER full_name;
ALTER TABLE users
  ADD UNIQUE KEY IF NOT EXISTS email (email);

-- 2. password_resets: one row per 6-digit code (only its BCrypt hash is stored)
CREATE TABLE IF NOT EXISTS password_resets (
  reset_id    INT AUTO_INCREMENT PRIMARY KEY,
  user_id     INT NOT NULL,
  code_hash   VARCHAR(100) NOT NULL,
  expires_at  DATETIME NOT NULL,
  attempts    TINYINT UNSIGNED NOT NULL DEFAULT 0,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  used_at     DATETIME NULL,
  INDEX idx_pr_user (user_id, created_at),
  CONSTRAINT fk_pr_user FOREIGN KEY (user_id)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- 3. Seeded staff accounts get <username>@example.com (never a real inbox),
--    unless another account already uses that address.
UPDATE users u
SET u.email = CONCAT(u.username, '@example.com')
WHERE u.email IS NULL
  AND u.role <> 'Owner'
  AND u.username IN ('admin', 'bplo', 'health', 'assessor', 'building', 'inspector')
  AND NOT EXISTS (SELECT 1 FROM (SELECT email FROM users WHERE email IS NOT NULL) used
                  WHERE used.email = CONCAT(u.username, '@example.com'));

-- 4. Owner accounts get their business e-mail (trimmed, lower case), but only when
--    - no other account already has that address, and
--    - no other owner account without an e-mail wants the same address
--      (co-owners, or one owner of several businesses). Those stay NULL.
UPDATE users u
JOIN businesses b ON b.business_id = u.business_id
SET u.email = LOWER(TRIM(b.email))
WHERE u.role = 'Owner' AND u.email IS NULL AND TRIM(COALESCE(b.email, '')) <> ''
  AND NOT EXISTS (SELECT 1 FROM (SELECT email FROM users WHERE email IS NOT NULL) used
                  WHERE used.email = LOWER(TRIM(b.email)))
  AND (SELECT COUNT(*) FROM (SELECT o.user_id, LOWER(TRIM(ob.email)) AS email
                             FROM users o JOIN businesses ob ON ob.business_id = o.business_id
                             WHERE o.role = 'Owner' AND o.email IS NULL) wanted
       WHERE wanted.email = LOWER(TRIM(b.email))) = 1;

-- 5. Report: accounts that still have no e-mail (they cannot use Forgot Password yet).
--    An empty result means every account has an e-mail.
SELECT u.user_id, u.username, u.full_name, u.role,
       b.business_name,
       LOWER(TRIM(b.email)) AS business_email,
       CASE
         WHEN u.role <> 'Owner' THEN 'Staff account: no e-mail on file'
         WHEN TRIM(COALESCE(b.email, '')) = '' THEN 'The business has no e-mail'
         WHEN EXISTS (SELECT 1 FROM users x WHERE x.email = LOWER(TRIM(b.email)))
           THEN CONCAT('E-mail already used by account "',
                       (SELECT x.username FROM users x WHERE x.email = LOWER(TRIM(b.email)) LIMIT 1), '"')
         ELSE 'Several owner accounts share this business e-mail'
       END AS reason_skipped
FROM users u
LEFT JOIN businesses b ON b.business_id = u.business_id
WHERE u.email IS NULL
ORDER BY u.role, u.username;

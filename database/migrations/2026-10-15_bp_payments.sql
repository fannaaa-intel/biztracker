-- =====================================================================
--  Migration: Business tax walk-in payments + shared OR registry (Revision 2B)   2026-10-15
--  For an EXISTING biztracker_db. Keeps all data.
--  Recommended order:  2026-10-15_forgot_password.sql
--                   -> 2026-10-15_rpt_partial_payments.sql
--                   -> 2026-10-15_bp_payments.sql   (this file)
--  This file only reads rpt_payments.or_no / payment_id, so it also works if the RPT migration
--  has not been run yet (run that one afterwards). Safe to run more than once.
--
--  phpMyAdmin: click the SQL tab, paste this whole file, click Go.
--  Command line (Git Bash):
--    /c/xampp/mysql/bin/mysql.exe -u root < database/migrations/2026-10-15_bp_payments.sql
--
--  The last two results are checks:
--    1. duplicate OR numbers across RPT and business tax payments -> must be EMPTY
--    2. how many permits were marked "paid before payment recording" (legacy_paid = 1)
-- =====================================================================

USE biztracker_db;

-- 1. Legacy flag: Paid / Issued before payments were recorded (no payment rows, counts as fully paid)
ALTER TABLE business_permits
  ADD COLUMN IF NOT EXISTS legacy_paid TINYINT(1) NOT NULL DEFAULT 0 AFTER interest;

-- 2. Business tax payments (several per application = partial payments; voided rows are kept)
CREATE TABLE IF NOT EXISTS business_permit_payments (
  payment_id      INT AUTO_INCREMENT PRIMARY KEY,
  permit_id       INT NOT NULL,
  amount_paid     DECIMAL(14,2) NOT NULL,
  surcharge       DECIMAL(12,2) NOT NULL DEFAULT 0,
  interest        DECIMAL(12,2) NOT NULL DEFAULT 0,
  payment_method  ENUM('Cash','Check','GCash','Bank Transfer') NOT NULL DEFAULT 'Cash',
  reference_no    VARCHAR(50) NULL,
  or_no           VARCHAR(30) NOT NULL UNIQUE,
  payment_date    DATE NOT NULL,
  received_by     INT NULL,
  remarks         VARCHAR(255) NULL,
  voided_at       DATETIME NULL,
  voided_by       INT NULL,
  void_reason     VARCHAR(255) NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_bpp_permit (permit_id),
  INDEX idx_bpp_user (received_by),
  INDEX idx_bpp_voided_by (voided_by),
  CONSTRAINT fk_bpp_permit FOREIGN KEY (permit_id)
    REFERENCES business_permits(permit_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_bpp_user FOREIGN KEY (received_by)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_bpp_voided_by FOREIGN KEY (voided_by)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- 3. Shared OR registry: one AF 51 booklet for RPT and business tax, so an OR number is unique across both
CREATE TABLE IF NOT EXISTS official_receipts (
  receipt_id  INT AUTO_INCREMENT PRIMARY KEY,
  or_no       VARCHAR(30) NOT NULL UNIQUE,
  source      ENUM('RPT','BP') NOT NULL,
  payment_id  INT NOT NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_or_source (source, payment_id)
) ENGINE=InnoDB;

-- 4. Register every OR number already used (voided ones too), oldest first. INSERT IGNORE skips numbers
--    already registered, so running this again adds nothing twice.
INSERT IGNORE INTO official_receipts (or_no, source, payment_id, created_at)
SELECT or_no, 'RPT', payment_id, created_at FROM rpt_payments ORDER BY payment_id;
INSERT IGNORE INTO official_receipts (or_no, source, payment_id, created_at)
SELECT or_no, 'BP', payment_id, created_at FROM business_permit_payments ORDER BY payment_id;

-- 5. Permits already Paid / Issued with no payment rows at all (not even voided ones) were paid before
--    payment recording started: mark them legacy so they count as fully paid. No fake payment rows.
UPDATE business_permits p
SET p.legacy_paid = 1
WHERE p.status IN ('Paid', 'Issued') AND p.legacy_paid = 0
  AND NOT EXISTS (SELECT 1 FROM business_permit_payments x WHERE x.permit_id = p.permit_id);

-- 6. Checks
-- 6a. OR numbers used by more than one payment (RPT + business tax). Must be EMPTY.
SELECT or_no, COUNT(*) AS times_used
FROM (SELECT or_no FROM rpt_payments UNION ALL SELECT or_no FROM business_permit_payments) used
GROUP BY or_no
HAVING COUNT(*) > 1;

-- 6b. Permits treated as "paid before payment recording".
SELECT COUNT(*) AS legacy_paid_permits, GROUP_CONCAT(reference_no ORDER BY reference_no SEPARATOR ', ') AS permits
FROM business_permits
WHERE legacy_paid = 1;

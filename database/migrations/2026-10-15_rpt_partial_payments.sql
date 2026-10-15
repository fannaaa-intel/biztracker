-- =====================================================================
--  Migration: RPT walk-in payments with partial payments (Revision 2A)   2026-10-15
--  For an EXISTING biztracker_db. Keeps all data. Run AFTER 2026-10-15_forgot_password.sql.
--
--  phpMyAdmin: click the SQL tab, paste this whole file, click Go.
--  Command line (Git Bash):
--    /c/xampp/mysql/bin/mysql.exe -u root < database/migrations/2026-10-15_rpt_partial_payments.sql
--
--  Safe to run more than once: every step uses IF NOT EXISTS / IF EXISTS.
--  Existing payments keep their amounts and become method 'Cash':
--    amount_paid = the part applied to the quarter's tax (principal)
--    penalty     = the part applied to the late-payment penalty
--  The last result is a check: it must say 0 problem quarters.
-- =====================================================================

USE biztracker_db;

-- 1. A quarter may now have several payments (partial payments).
--    The foreign key on assessment_id needs an index, so add a normal one BEFORE dropping the unique key.
ALTER TABLE rpt_payments
  ADD INDEX IF NOT EXISTS idx_pay_assessment (assessment_id, quarter);
ALTER TABLE rpt_payments
  DROP INDEX IF EXISTS uq_pay_quarter;

-- 2. Payment method, reference no. and remarks (existing payments become 'Cash')
ALTER TABLE rpt_payments
  ADD COLUMN IF NOT EXISTS payment_method ENUM('Cash','Check','GCash','Bank Transfer') NOT NULL DEFAULT 'Cash' AFTER penalty,
  ADD COLUMN IF NOT EXISTS reference_no VARCHAR(50) NULL AFTER payment_method,
  ADD COLUMN IF NOT EXISTS remarks VARCHAR(255) NULL AFTER received_by;

-- 3. Voiding (mistake correction): voided payments are kept but no longer count
ALTER TABLE rpt_payments
  ADD COLUMN IF NOT EXISTS voided_at DATETIME NULL AFTER remarks,
  ADD COLUMN IF NOT EXISTS voided_by INT NULL AFTER voided_at,
  ADD COLUMN IF NOT EXISTS void_reason VARCHAR(255) NULL AFTER voided_by,
  ADD INDEX IF NOT EXISTS idx_pay_voided_by (voided_by);
ALTER TABLE rpt_payments
  ADD CONSTRAINT fk_pay_voided_by FOREIGN KEY IF NOT EXISTS (voided_by)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE;

-- 4. Check: no quarter may have more principal paid than its share of the annual tax.
--    (Q1-Q3 = total_due / 4 rounded; Q4 takes the centavo remainder.) Expect 0.
SELECT COUNT(*) AS problem_quarters
FROM (
  SELECT p.assessment_id, p.quarter, SUM(p.amount_paid) AS principal_paid,
         IF(p.quarter = 4, a.total_due - 3 * ROUND(a.total_due / 4, 2), ROUND(a.total_due / 4, 2)) AS quarter_amount
  FROM rpt_payments p
  JOIN rpt_assessments a ON a.assessment_id = p.assessment_id
  WHERE p.voided_at IS NULL
  GROUP BY p.assessment_id, p.quarter, a.total_due
  HAVING principal_paid > quarter_amount
) over_paid;

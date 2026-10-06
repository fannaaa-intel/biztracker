-- =====================================================================
--  BizTracker: Business Permit and Regulatory Compliance System
--  Database: biztracker_db   (MySQL / MariaDB via XAMPP)
--  Paste this whole file into phpMyAdmin > SQL tab and click Go.
--  Safe to re-run: it drops and recreates all tables.
--  All seeded users have the password:  password123
--  Seed dates are relative to CURDATE() so alerts always work in demos.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS biztracker_db
  CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
USE biztracker_db;

SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS audit_log, notifications, settings,
  construction_clearances, construction_projects,
  inspection_items, inspections,
  rpt_payments, rpt_assessments, properties,
  health_certificates, employees, sanitary_permits,
  requirements, clearance_endorsements, business_permits,
  users, businesses;
SET FOREIGN_KEY_CHECKS = 1;

-- =====================================================================
--  1. TABLES
-- =====================================================================

CREATE TABLE businesses (
  business_id       INT AUTO_INCREMENT PRIMARY KEY,
  business_name     VARCHAR(150) NOT NULL,
  owner_name        VARCHAR(150) NOT NULL,
  address           VARCHAR(255) NOT NULL,
  barangay          VARCHAR(100) NOT NULL,
  business_type     ENUM('Sole Proprietorship','Partnership','Corporation','Cooperative') NOT NULL DEFAULT 'Sole Proprietorship',
  line_of_business  VARCHAR(150) NOT NULL,
  is_food_business  TINYINT(1) NOT NULL DEFAULT 0,
  dti_sec_no        VARCHAR(50),
  tin               VARCHAR(20),
  contact_no        VARCHAR(20),
  email             VARCHAR(150),
  is_active         TINYINT(1) NOT NULL DEFAULT 1,
  created_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE users (
  user_id        INT AUTO_INCREMENT PRIMARY KEY,
  username       VARCHAR(50)  NOT NULL UNIQUE,
  password_hash  VARCHAR(100) NOT NULL,
  full_name      VARCHAR(150) NOT NULL,
  role           ENUM('Admin','BPLO','Health','Assessor','Building','Inspector','Owner') NOT NULL,
  business_id    INT NULL,                       -- only for Owner accounts
  is_active      TINYINT(1) NOT NULL DEFAULT 1,
  last_login     DATETIME NULL,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_users_business (business_id),
  CONSTRAINT fk_users_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ---------- Business Permit ----------
CREATE TABLE business_permits (
  permit_id         INT AUTO_INCREMENT PRIMARY KEY,
  business_id       INT NOT NULL,
  reference_no      VARCHAR(30) NOT NULL UNIQUE,
  application_type  ENUM('New','Renewal') NOT NULL,
  permit_year       YEAR NOT NULL,
  status            ENUM('Submitted','Under Review','For Assessment','Assessed','Paid','Issued','Rejected') NOT NULL DEFAULT 'Submitted',
  gross_receipts    DECIMAL(14,2) NOT NULL DEFAULT 0,
  assessed_amount   DECIMAL(12,2) NOT NULL DEFAULT 0,
  surcharge         DECIMAL(12,2) NOT NULL DEFAULT 0,
  interest          DECIMAL(12,2) NOT NULL DEFAULT 0,
  mayors_permit_no  VARCHAR(30) NULL UNIQUE,
  date_filed        DATE NOT NULL,
  date_issued       DATE NULL,
  valid_until       DATE NULL,
  remarks           VARCHAR(255),
  created_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_bp_business (business_id),
  CONSTRAINT fk_bp_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE clearance_endorsements (
  endorsement_id  INT AUTO_INCREMENT PRIMARY KEY,
  permit_id       INT NOT NULL,
  office          ENUM('Barangay','Sanitary','RPT','Fire','Zoning') NOT NULL,
  status          ENUM('Pending','Endorsed','Rejected') NOT NULL DEFAULT 'Pending',
  endorsed_by     INT NULL,
  endorsed_at     DATETIME NULL,
  remarks         VARCHAR(255),
  UNIQUE KEY uq_endorse (permit_id, office),
  INDEX idx_ce_user (endorsed_by),
  CONSTRAINT fk_ce_permit FOREIGN KEY (permit_id)
    REFERENCES business_permits(permit_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_ce_user FOREIGN KEY (endorsed_by)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- Shared document checklist for all six modules
-- (related_id = permit_id / sanitary_id / project_id ... depending on module)
CREATE TABLE requirements (
  requirement_id  INT AUTO_INCREMENT PRIMARY KEY,
  business_id     INT NOT NULL,
  module          ENUM('Business Permit','Sanitary Permit','Health Certificate','Real Property Tax','Annual Inspection','Construction Permit') NOT NULL,
  related_id      INT NULL,
  document_name   VARCHAR(150) NOT NULL,
  file_path       VARCHAR(255) NULL,
  status          ENUM('Pending Upload','Submitted','Verified','Rejected') NOT NULL DEFAULT 'Pending Upload',
  uploaded_at     DATETIME NULL,
  verified_by     INT NULL,
  verified_at     DATETIME NULL,
  remarks         VARCHAR(255),
  INDEX idx_req_business (business_id),
  INDEX idx_req_module (module, related_id),
  INDEX idx_req_user (verified_by),
  CONSTRAINT fk_req_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_req_user FOREIGN KEY (verified_by)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ---------- Sanitary Permit ----------
CREATE TABLE sanitary_permits (
  sanitary_id       INT AUTO_INCREMENT PRIMARY KEY,
  business_id       INT NOT NULL,
  permit_no         VARCHAR(30) NOT NULL UNIQUE,
  permit_year       YEAR NOT NULL,
  category          ENUM('Food','Non-Food') NOT NULL,
  status            ENUM('Submitted','Lab Analysis','For Inspection','Issued','Rejected') NOT NULL DEFAULT 'Submitted',
  inspection_date   DATE NULL,
  inspection_score  TINYINT UNSIGNED NULL,
  inspector_name    VARCHAR(150) NULL,
  findings          TEXT NULL,
  date_filed        DATE NOT NULL,
  date_issued       DATE NULL,
  valid_until       DATE NULL,        -- Dec 31 of the year issued
  created_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_sp_business (business_id),
  CONSTRAINT chk_sp_score CHECK (inspection_score IS NULL OR inspection_score <= 100),
  CONSTRAINT fk_sp_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ---------- Health Certificates ----------
CREATE TABLE employees (
  employee_id  INT AUTO_INCREMENT PRIMARY KEY,
  business_id  INT NOT NULL,
  full_name    VARCHAR(150) NOT NULL,
  position     VARCHAR(100) NOT NULL,
  category     ENUM('Food Handler','Non-Food') NOT NULL,
  is_active    TINYINT(1) NOT NULL DEFAULT 1,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_emp_business (business_id),
  CONSTRAINT fk_emp_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- Status (Valid / Expiring Soon / Expired) is COMPUTED from expiry_date, not stored.
CREATE TABLE health_certificates (
  cert_id         INT AUTO_INCREMENT PRIMARY KEY,
  employee_id     INT NOT NULL,
  certificate_no  VARCHAR(30) NOT NULL UNIQUE,
  issue_date      DATE NOT NULL,
  expiry_date     DATE NOT NULL,
  issued_by       VARCHAR(150) NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_hc_employee (employee_id),
  INDEX idx_hc_expiry (expiry_date),
  CONSTRAINT chk_hc_dates CHECK (expiry_date > issue_date),
  CONSTRAINT fk_hc_employee FOREIGN KEY (employee_id)
    REFERENCES employees(employee_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ---------- Real Property Tax ----------
CREATE TABLE properties (
  property_id     INT AUTO_INCREMENT PRIMARY KEY,
  business_id     INT NOT NULL,
  pin             VARCHAR(30) NOT NULL UNIQUE,   -- Property Index Number
  td_no           VARCHAR(30) NOT NULL UNIQUE,   -- Tax Declaration No.
  location        VARCHAR(255) NOT NULL,
  property_type   ENUM('Land','Building','Machinery') NOT NULL,
  classification  ENUM('Commercial','Residential','Industrial','Agricultural') NOT NULL DEFAULT 'Commercial',
  assessed_value  DECIMAL(14,2) NOT NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_prop_business (business_id),
  CONSTRAINT fk_prop_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE rpt_assessments (
  assessment_id  INT AUTO_INCREMENT PRIMARY KEY,
  property_id    INT NOT NULL,
  tax_year       YEAR NOT NULL,
  basic_tax      DECIMAL(12,2) NOT NULL,   -- 1% of assessed value
  sef_tax        DECIMAL(12,2) NOT NULL,   -- 1% Special Education Fund
  total_due      DECIMAL(12,2) NOT NULL,   -- annual total
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_assess (property_id, tax_year),
  CONSTRAINT fk_ra_property FOREIGN KEY (property_id)
    REFERENCES properties(property_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE rpt_payments (
  payment_id     INT AUTO_INCREMENT PRIMARY KEY,
  assessment_id  INT NOT NULL,
  quarter        TINYINT UNSIGNED NOT NULL,
  amount_paid    DECIMAL(12,2) NOT NULL,
  penalty        DECIMAL(12,2) NOT NULL DEFAULT 0,
  or_no          VARCHAR(30) NOT NULL UNIQUE,     -- Official Receipt No.
  payment_date   DATE NOT NULL,
  received_by    INT NULL,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_pay_quarter (assessment_id, quarter),
  INDEX idx_pay_user (received_by),
  CONSTRAINT chk_quarter CHECK (quarter BETWEEN 1 AND 4),
  CONSTRAINT fk_pay_assessment FOREIGN KEY (assessment_id)
    REFERENCES rpt_assessments(assessment_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_pay_user FOREIGN KEY (received_by)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ---------- Annual Inspection ----------
CREATE TABLE inspections (
  inspection_id      INT AUTO_INCREMENT PRIMARY KEY,
  business_id        INT NOT NULL,
  reference_no       VARCHAR(30) NOT NULL UNIQUE,
  inspection_year    YEAR NOT NULL,
  schedule_date      DATETIME NOT NULL,
  reinspection_date  DATETIME NULL,
  status             ENUM('Scheduled','In Progress','For Re-inspection','Completed','Cancelled') NOT NULL DEFAULT 'Scheduled',
  overall_result     ENUM('Pending','Passed','Failed') NOT NULL DEFAULT 'Pending',
  certificate_no     VARCHAR(30) NULL UNIQUE,
  created_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_insp_business (business_id),
  CONSTRAINT fk_insp_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE inspection_items (
  item_id         INT AUTO_INCREMENT PRIMARY KEY,
  inspection_id   INT NOT NULL,
  department      ENUM('Structural','Electrical','Mechanical','Fire') NOT NULL,
  result          ENUM('Pending','Passed','Failed') NOT NULL DEFAULT 'Pending',
  findings        TEXT NULL,
  inspector_name  VARCHAR(150) NULL,
  inspected_at    DATETIME NULL,
  UNIQUE KEY uq_item (inspection_id, department),
  CONSTRAINT fk_item_inspection FOREIGN KEY (inspection_id)
    REFERENCES inspections(inspection_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ---------- Construction Permit ----------
CREATE TABLE construction_projects (
  project_id     INT AUTO_INCREMENT PRIMARY KEY,
  business_id    INT NOT NULL,
  reference_no   VARCHAR(30) NOT NULL UNIQUE,
  project_title  VARCHAR(200) NOT NULL,
  project_type   ENUM('New Construction','Renovation','Extension','Renovation & Extension','Demolition') NOT NULL,
  current_stage  ENUM('Locational','Building Permit','Construction','Occupancy','Completed') NOT NULL DEFAULT 'Locational',
  status         ENUM('Active','On Hold','Completed','Rejected') NOT NULL DEFAULT 'Active',
  estimated_cost DECIMAL(14,2) NULL,
  date_filed     DATE NOT NULL,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_cp_business (business_id),
  CONSTRAINT fk_cp_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE construction_clearances (
  clearance_id    INT AUTO_INCREMENT PRIMARY KEY,
  project_id      INT NOT NULL,
  clearance_type  ENUM('Locational','FSEC','Building','Occupancy') NOT NULL,
  clearance_no    VARCHAR(30) NULL UNIQUE,
  status          ENUM('Pending','Approved','Rejected') NOT NULL DEFAULT 'Pending',
  approved_by     INT NULL,
  approved_at     DATETIME NULL,
  remarks         VARCHAR(255),
  UNIQUE KEY uq_clearance (project_id, clearance_type),
  INDEX idx_cc_user (approved_by),
  CONSTRAINT fk_cc_project FOREIGN KEY (project_id)
    REFERENCES construction_projects(project_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_cc_user FOREIGN KEY (approved_by)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ---------- Notifications, Settings, Audit ----------
CREATE TABLE notifications (
  notification_id  INT AUTO_INCREMENT PRIMARY KEY,
  business_id      INT NOT NULL,
  module           ENUM('Business Permit','Sanitary Permit','Health Certificate','Real Property Tax','Annual Inspection','Construction Permit') NOT NULL,
  related_id       INT NOT NULL,
  priority         ENUM('Info','Warning','Urgent') NOT NULL DEFAULT 'Warning',
  message          VARCHAR(255) NOT NULL,
  due_date         DATE NOT NULL,
  is_read          TINYINT(1) NOT NULL DEFAULT 0,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_notif (business_id, module, related_id, due_date),   -- prevents duplicate alerts
  CONSTRAINT fk_notif_business FOREIGN KEY (business_id)
    REFERENCES businesses(business_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE settings (
  setting_key    VARCHAR(50) PRIMARY KEY,
  setting_value  VARCHAR(255) NOT NULL,
  description    VARCHAR(255)
) ENGINE=InnoDB;

CREATE TABLE audit_log (
  log_id      INT AUTO_INCREMENT PRIMARY KEY,
  user_id     INT NULL,
  action      ENUM('LOGIN','LOGOUT','INSERT','UPDATE','DELETE','STATUS_CHANGE','UPLOAD','PRINT') NOT NULL,
  table_name  VARCHAR(50) NULL,
  record_id   INT NULL,
  details     VARCHAR(255),
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_audit_user (user_id),
  INDEX idx_audit_date (created_at),
  CONSTRAINT fk_audit_user FOREIGN KEY (user_id)
    REFERENCES users(user_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- =====================================================================
--  2. SEED DATA
-- =====================================================================

SET @Y   = YEAR(CURDATE());
SET @JAN1 = MAKEDATE(@Y, 1);
SET @HASH = '$2b$11$FLcfTNp4xUWq4whcxJWNeecOL8GJOjSKY8bhXLV9kEdcqaAyx/6bq';  -- password123

-- ---------- Settings ----------
INSERT INTO settings (setting_key, setting_value, description) VALUES
('lgu_name',                 'Municipality of Aparri', 'LGU name printed on documents'),
('province',                 'Cagayan',                'Province'),
('expiry_warning_days',      '30',    'Days before expiry to mark as Expiring Soon'),
('bp_renewal_deadline',      '01-20', 'Business permit renewal deadline (MM-DD)'),
('bp_surcharge_rate',        '0.25',  'Late renewal surcharge (RA 7160 Sec. 168)'),
('bp_interest_rate_monthly', '0.02',  'Monthly interest on unpaid business tax'),
('bp_interest_max_months',   '36',    'Max months of interest'),
('rpt_basic_rate',           '0.01',  'RPT basic tax rate (municipality)'),
('rpt_sef_rate',             '0.01',  'Special Education Fund rate'),
('rpt_penalty_rate_monthly', '0.02',  'RPT late payment penalty per month'),
('rpt_penalty_max',          '0.72',  'RPT max penalty (36 months)'),
('hc_validity_months',       '12',    'Health certificate validity in months'),
('upload_folder',            'uploads', 'Folder for uploaded requirement files');

-- ---------- Businesses ----------
INSERT INTO businesses (business_id, business_name, owner_name, address, barangay, business_type, line_of_business, is_food_business, dti_sec_no, tin, contact_no, email) VALUES
(1, 'Cristan''s Bakeshop',          'Cristan Dela Cruz', '12 Rizal St.',       'Centro 01',  'Sole Proprietorship', 'Bakery and Pastry Shop', 1, 'DTI-3456789', '123-456-789-000', '09171234567', 'cristan.bakeshop@email.com'),
(2, 'Santos General Merchandise',   'Maria Santos',      '45 Bonifacio St.',   'Centro 03',  'Sole Proprietorship', 'Retail - General Merchandise', 0, 'DTI-4567890', '234-567-890-000', '09182345678', 'santos.gm@email.com'),
(3, 'Kusina ni Aling Rosa',         'Rosa Mendoza',      '8 Maharlika Hwy.',   'Macanaya',   'Sole Proprietorship', 'Restaurant / Eatery', 1, 'DTI-5678901', '345-678-901-000', '09193456789', 'kusina.rosa@email.com');

-- ---------- Users (password for all: password123) ----------
INSERT INTO users (user_id, username, password_hash, full_name, role, business_id) VALUES
(1, 'admin',     @HASH, 'System Administrator',     'Admin',     NULL),
(2, 'bplo',      @HASH, 'Liza Ramirez',             'BPLO',      NULL),
(3, 'health',    @HASH, 'Dr. Paolo Agustin',        'Health',    NULL),
(4, 'assessor',  @HASH, 'Ramon Villanueva',         'Assessor',  NULL),
(5, 'building',  @HASH, 'Engr. Teresa Castillo',    'Building',  NULL),
(6, 'inspector', @HASH, 'Insp. Mark Del Rosario',   'Inspector', NULL),
(7, 'owner',     @HASH, 'Cristan Dela Cruz',        'Owner',     1);

-- ---------- Business Permits ----------
-- Bakeshop: current year Issued, next year renewal Under Review (filed early)
INSERT INTO business_permits (permit_id, business_id, reference_no, application_type, permit_year, status, gross_receipts, assessed_amount, mayors_permit_no, date_filed, date_issued, valid_until, remarks) VALUES
(1, 1, CONCAT('BP-', @Y, '-00001'),   'Renewal', @Y,   'Issued',       1850000, 18500, CONCAT('MP-', @Y, '-00001'), @JAN1 + INTERVAL 5 DAY,  @JAN1 + INTERVAL 17 DAY, MAKEDATE(@Y, 1) + INTERVAL 1 YEAR - INTERVAL 1 DAY, 'Renewed on time'),
(2, 1, CONCAT('BP-', @Y+1, '-00001'), 'Renewal', @Y+1, 'Under Review', 2100000, 0,     NULL,                        CURDATE() - INTERVAL 7 DAY, NULL, NULL, 'Early renewal filing'),
(3, 2, CONCAT('BP-', @Y, '-00002'),   'Renewal', @Y,   'Issued',       950000,  9500,  CONCAT('MP-', @Y, '-00002'), @JAN1 + INTERVAL 8 DAY,  @JAN1 + INTERVAL 19 DAY, MAKEDATE(@Y, 1) + INTERVAL 1 YEAR - INTERVAL 1 DAY, NULL),
(4, 3, CONCAT('BP-', @Y, '-00003'),   'New',     @Y,   'Issued',       600000,  6000,  CONCAT('MP-', @Y, '-00003'), @JAN1 + INTERVAL 40 DAY, @JAN1 + INTERVAL 55 DAY, MAKEDATE(@Y, 1) + INTERVAL 1 YEAR - INTERVAL 1 DAY, NULL),
(5, 3, CONCAT('BP-', @Y+1, '-00002'), 'Renewal', @Y+1, 'Submitted',    800000,  0,     NULL,                        CURDATE() - INTERVAL 2 DAY, NULL, NULL, 'Awaiting document review');

-- Endorsements for the bakeshop's renewal (permit 2): Fire pending
INSERT INTO clearance_endorsements (permit_id, office, status, endorsed_by, endorsed_at, remarks) VALUES
(2, 'Barangay', 'Endorsed', 2,    CURDATE() - INTERVAL 6 DAY, 'Barangay clearance verified'),
(2, 'Sanitary', 'Endorsed', 3,    CURDATE() - INTERVAL 5 DAY, 'Valid sanitary permit on file'),
(2, 'RPT',      'Endorsed', 4,    CURDATE() - INTERVAL 5 DAY, 'RPT dues settled as of filing'),
(2, 'Fire',     'Pending',  NULL, NULL,                       'Awaiting FSIC renewal'),
(2, 'Zoning',   'Endorsed', 5,    CURDATE() - INTERVAL 4 DAY, 'Conforms to commercial zone');

-- Endorsements for the restaurant's renewal (permit 5): all pending
INSERT INTO clearance_endorsements (permit_id, office, status) VALUES
(5, 'Barangay', 'Pending'), (5, 'Sanitary', 'Pending'), (5, 'RPT', 'Pending'),
(5, 'Fire', 'Pending'), (5, 'Zoning', 'Pending');

-- Endorsements for issued permits (all endorsed)
INSERT INTO clearance_endorsements (permit_id, office, status, endorsed_by, endorsed_at)
SELECT p.permit_id, o.office, 'Endorsed', 2, p.date_issued - INTERVAL 2 DAY
FROM business_permits p
CROSS JOIN (SELECT 'Barangay' office UNION ALL SELECT 'Sanitary' UNION ALL SELECT 'RPT'
            UNION ALL SELECT 'Fire' UNION ALL SELECT 'Zoning') o
WHERE p.status = 'Issued';

-- ---------- Requirements ----------
INSERT INTO requirements (business_id, module, related_id, document_name, file_path, status, uploaded_at, verified_by, verified_at) VALUES
(1, 'Business Permit', 2, 'DTI/SEC Registration',              'uploads/1/dti_registration.pdf', 'Verified',       CURDATE() - INTERVAL 7 DAY, 2, CURDATE() - INTERVAL 6 DAY),
(1, 'Business Permit', 2, 'Gross Receipts Declaration',        'uploads/1/gross_receipts.pdf',   'Verified',       CURDATE() - INTERVAL 7 DAY, 2, CURDATE() - INTERVAL 6 DAY),
(1, 'Business Permit', 2, 'Lease Contract',                    'uploads/1/lease_contract.pdf',   'Verified',       CURDATE() - INTERVAL 7 DAY, 2, CURDATE() - INTERVAL 6 DAY),
(1, 'Business Permit', 2, 'Occupancy Permit',                  NULL,                             'Pending Upload', NULL, NULL, NULL),
(1, 'Business Permit', 2, 'Fire Safety Inspection Certificate (FSIC)', NULL,                     'Pending Upload', NULL, NULL, NULL),
(1, 'Sanitary Permit', 1, 'Microbiological Water Test',        'uploads/1/water_test.pdf',       'Verified',       @JAN1 + INTERVAL 3 DAY, 3, @JAN1 + INTERVAL 4 DAY),
(1, 'Sanitary Permit', 1, 'Physico-Chemical Analysis',         'uploads/1/physchem.pdf',         'Verified',       @JAN1 + INTERVAL 3 DAY, 3, @JAN1 + INTERVAL 4 DAY),
(1, 'Sanitary Permit', 1, 'Pest Control Certificate',          'uploads/1/pest_control.pdf',     'Verified',       @JAN1 + INTERVAL 3 DAY, 3, @JAN1 + INTERVAL 4 DAY),
(1, 'Construction Permit', 1, 'Architectural & Structural Plans', 'uploads/1/arch_struct_plans.pdf', 'Verified', CURDATE() - INTERVAL 40 DAY, 5, CURDATE() - INTERVAL 35 DAY),
(1, 'Construction Permit', 1, 'Electrical Plans',              'uploads/1/electrical_plans.pdf', 'Verified',       CURDATE() - INTERVAL 40 DAY, 5, CURDATE() - INTERVAL 35 DAY),
(1, 'Construction Permit', 1, 'Plumbing / Sanitary Plans',     'uploads/1/plumbing_plans.pdf',   'Submitted',      CURDATE() - INTERVAL 40 DAY, NULL, NULL),
(1, 'Construction Permit', 1, 'Lot Title & Tax Declaration',   'uploads/1/title_td.pdf',         'Verified',       CURDATE() - INTERVAL 40 DAY, 5, CURDATE() - INTERVAL 35 DAY),
(3, 'Business Permit', 5, 'DTI/SEC Registration',              'uploads/3/dti_registration.pdf', 'Submitted',      CURDATE() - INTERVAL 2 DAY, NULL, NULL),
(3, 'Business Permit', 5, 'Gross Receipts Declaration',        NULL,                             'Pending Upload', NULL, NULL, NULL);

-- ---------- Sanitary Permits ----------
INSERT INTO sanitary_permits (sanitary_id, business_id, permit_no, permit_year, category, status, inspection_date, inspection_score, inspector_name, findings, date_filed, date_issued, valid_until) VALUES
(1, 1, CONCAT('SP-', @Y, '-00001'), @Y, 'Food', 'Issued', @JAN1 + INTERVAL 9 DAY, 96, 'Sanitary Insp. R. Ramos',
   'Potable water meets PNSDW standards; functional grease trap; valid pest control contract; all food handlers with health cards.',
   @JAN1 + INTERVAL 2 DAY, @JAN1 + INTERVAL 12 DAY, MAKEDATE(@Y, 1) + INTERVAL 1 YEAR - INTERVAL 1 DAY),
(2, 2, CONCAT('SP-', @Y, '-00002'), @Y, 'Non-Food', 'Issued', @JAN1 + INTERVAL 11 DAY, 92, 'Sanitary Insp. R. Ramos',
   'Clean premises; proper waste segregation.',
   @JAN1 + INTERVAL 4 DAY, @JAN1 + INTERVAL 14 DAY, MAKEDATE(@Y, 1) + INTERVAL 1 YEAR - INTERVAL 1 DAY),
(3, 3, CONCAT('SP-', @Y, '-00003'), @Y, 'Food', 'For Inspection', NULL, NULL, NULL, NULL,
   CURDATE() - INTERVAL 10 DAY, NULL, NULL);

-- ---------- Employees ----------
INSERT INTO employees (employee_id, business_id, full_name, position, category) VALUES
(1,  1, 'Aira Agbanglo',        'Baker',            'Food Handler'),
(2,  1, 'Christopher Sadi',     'Cashier',          'Non-Food'),
(3,  1, 'Lilah Espergal',       'Packer',           'Food Handler'),
(4,  1, 'Juan Pascual',         'Delivery Driver',  'Non-Food'),
(5,  1, 'Ben Reyes',            'Security Guard',   'Non-Food'),
(6,  1, 'Jasmine Tolentino',    'Pastry Chef',      'Food Handler'),
(7,  1, 'Mark Anthony Galang',  'Baker Assistant',  'Food Handler'),
(8,  1, 'Grace Bautista',       'Service Crew',     'Food Handler'),
(9,  1, 'Rico Manalo',          'Utility Staff',    'Non-Food'),
(10, 1, 'Elena Soriano',        'Store Supervisor', 'Non-Food'),
(11, 2, 'Joy Cabrera',          'Sales Clerk',      'Non-Food'),
(12, 2, 'Arnel Santos',         'Stockman',         'Non-Food'),
(13, 3, 'Rosa Mendoza',         'Head Cook',        'Food Handler'),
(14, 3, 'Danica Flores',        'Server',           'Food Handler'),
(15, 3, 'Noel Ramos',           'Dishwasher',       'Food Handler');

-- ---------- Health Certificates ----------
-- Bakeshop: 8 valid, 1 expiring in 20 days (Ben Reyes), 1 expired 5 days ago (Lilah Espergal)
INSERT INTO health_certificates (employee_id, certificate_no, issue_date, expiry_date, issued_by) VALUES
(1,  CONCAT('HC-', @Y, '-00001'), CURDATE() - INTERVAL 120 DAY, CURDATE() - INTERVAL 120 DAY + INTERVAL 1 YEAR, 'Dr. Paolo Agustin'),
(2,  CONCAT('HC-', @Y, '-00002'), CURDATE() - INTERVAL 150 DAY, CURDATE() - INTERVAL 150 DAY + INTERVAL 1 YEAR, 'Dr. Paolo Agustin'),
(3,  CONCAT('HC-', @Y-1, '-00031'), CURDATE() - INTERVAL 5 DAY - INTERVAL 1 YEAR, CURDATE() - INTERVAL 5 DAY,   'Dr. Paolo Agustin'),
(4,  CONCAT('HC-', @Y, '-00003'), CURDATE() - INTERVAL 90 DAY,  CURDATE() - INTERVAL 90 DAY + INTERVAL 1 YEAR,  'Dr. Paolo Agustin'),
(5,  CONCAT('HC-', @Y-1, '-00032'), CURDATE() + INTERVAL 20 DAY - INTERVAL 1 YEAR, CURDATE() + INTERVAL 20 DAY, 'Dr. Paolo Agustin'),
(6,  CONCAT('HC-', @Y, '-00004'), CURDATE() - INTERVAL 60 DAY,  CURDATE() - INTERVAL 60 DAY + INTERVAL 1 YEAR,  'Dr. Paolo Agustin'),
(7,  CONCAT('HC-', @Y, '-00005'), CURDATE() - INTERVAL 200 DAY, CURDATE() - INTERVAL 200 DAY + INTERVAL 1 YEAR, 'Dr. Paolo Agustin'),
(8,  CONCAT('HC-', @Y, '-00006'), CURDATE() - INTERVAL 30 DAY,  CURDATE() - INTERVAL 30 DAY + INTERVAL 1 YEAR,  'Dr. Paolo Agustin'),
(9,  CONCAT('HC-', @Y, '-00007'), CURDATE() - INTERVAL 180 DAY, CURDATE() - INTERVAL 180 DAY + INTERVAL 1 YEAR, 'Dr. Paolo Agustin'),
(10, CONCAT('HC-', @Y, '-00008'), CURDATE() - INTERVAL 100 DAY, CURDATE() - INTERVAL 100 DAY + INTERVAL 1 YEAR, 'Dr. Paolo Agustin'),
-- Store: both valid
(11, CONCAT('HC-', @Y, '-00009'), CURDATE() - INTERVAL 80 DAY,  CURDATE() - INTERVAL 80 DAY + INTERVAL 1 YEAR,  'Dr. Paolo Agustin'),
(12, CONCAT('HC-', @Y, '-00010'), CURDATE() - INTERVAL 70 DAY,  CURDATE() - INTERVAL 70 DAY + INTERVAL 1 YEAR,  'Dr. Paolo Agustin'),
-- Restaurant: 2 valid, 1 expired 40 days ago
(13, CONCAT('HC-', @Y, '-00011'), CURDATE() - INTERVAL 45 DAY,  CURDATE() - INTERVAL 45 DAY + INTERVAL 1 YEAR,  'Dr. Paolo Agustin'),
(14, CONCAT('HC-', @Y, '-00012'), CURDATE() - INTERVAL 45 DAY,  CURDATE() - INTERVAL 45 DAY + INTERVAL 1 YEAR,  'Dr. Paolo Agustin'),
(15, CONCAT('HC-', @Y-1, '-00040'), CURDATE() - INTERVAL 40 DAY - INTERVAL 1 YEAR, CURDATE() - INTERVAL 40 DAY, 'Dr. Paolo Agustin');

-- ---------- Properties ----------
INSERT INTO properties (property_id, business_id, pin, td_no, location, property_type, classification, assessed_value) VALUES
(1, 1, '015-06-001-01-001', 'TD-2024-10021', '12 Rizal St., Centro 01, Aparri, Cagayan', 'Land',     'Commercial', 40000.00),
(2, 1, '015-06-001-01-002', 'TD-2024-10022', '12 Rizal St., Centro 01, Aparri, Cagayan', 'Building', 'Commercial', 15000.00),
(3, 3, '015-06-014-02-007', 'TD-2024-20107', '8 Maharlika Hwy., Macanaya, Aparri, Cagayan', 'Land',  'Commercial', 25000.00);

-- Current-year assessments: basic 1% + SEF 1% of assessed value
INSERT INTO rpt_assessments (assessment_id, property_id, tax_year, basic_tax, sef_tax, total_due)
SELECT property_id, property_id, @Y,
       ROUND(assessed_value * 0.01, 2), ROUND(assessed_value * 0.01, 2), ROUND(assessed_value * 0.02, 2)
FROM properties;

-- Quarter due dates: Mar 31, Jun 30, Sep 30, Dec 31.
-- Bakeshop (properties 1-2): every quarter already due is PAID on time.
INSERT INTO rpt_payments (assessment_id, quarter, amount_paid, penalty, or_no, payment_date, received_by)
SELECT a.assessment_id, q.q, ROUND(a.total_due / 4, 2), 0,
       CONCAT('OR-', @Y, '-', LPAD(a.assessment_id * 10 + q.q, 5, '0')),
       (MAKEDATE(@Y, 1) + INTERVAL (q.q * 3) MONTH - INTERVAL 1 DAY) - INTERVAL 10 DAY,
       4
FROM rpt_assessments a
JOIN (SELECT 1 q UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4) q
WHERE a.property_id IN (1, 2)
  AND (MAKEDATE(@Y, 1) + INTERVAL (q.q * 3) MONTH - INTERVAL 1 DAY) < CURDATE();

-- Restaurant (property 3): only Q1 paid, so any later due quarters show as OVERDUE.
INSERT INTO rpt_payments (assessment_id, quarter, amount_paid, penalty, or_no, payment_date, received_by)
SELECT a.assessment_id, 1, ROUND(a.total_due / 4, 2), 0,
       CONCAT('OR-', @Y, '-', LPAD(a.assessment_id * 10 + 1, 5, '0')),
       MAKEDATE(@Y, 1) + INTERVAL 80 DAY, 4
FROM rpt_assessments a
WHERE a.property_id = 3
  AND (MAKEDATE(@Y, 1) + INTERVAL 3 MONTH - INTERVAL 1 DAY) < CURDATE();

-- ---------- Annual Inspection ----------
-- Bakeshop: inspected 10 days ago, Fire failed (exit door), re-inspection in 30 days
INSERT INTO inspections (inspection_id, business_id, reference_no, inspection_year, schedule_date, reinspection_date, status, overall_result) VALUES
(1, 1, CONCAT('INS-', @Y, '-00001'), @Y, TIMESTAMP(CURDATE() - INTERVAL 10 DAY, '09:30:00'), TIMESTAMP(CURDATE() + INTERVAL 30 DAY, '09:00:00'), 'For Re-inspection', 'Pending'),
(2, 2, CONCAT('INS-', @Y, '-00002'), @Y, TIMESTAMP(@JAN1 + INTERVAL 60 DAY, '10:00:00'), NULL, 'Completed', 'Passed'),
(3, 3, CONCAT('INS-', @Y, '-00003'), @Y, TIMESTAMP(CURDATE() + INTERVAL 14 DAY, '13:30:00'), NULL, 'Scheduled', 'Pending');

UPDATE inspections SET certificate_no = CONCAT('AIC-', @Y, '-00002') WHERE inspection_id = 2;

INSERT INTO inspection_items (inspection_id, department, result, findings, inspector_name, inspected_at) VALUES
(1, 'Structural', 'Passed', 'Structural integrity verified; no unauthorized modifications.', 'Engr. A. Lopez',       TIMESTAMP(CURDATE() - INTERVAL 10 DAY, '09:45:00')),
(1, 'Electrical', 'Passed', 'Breakers load-tested; wiring conforms to Philippine Electrical Code.', 'Engr. J. Tan', TIMESTAMP(CURDATE() - INTERVAL 10 DAY, '10:15:00')),
(1, 'Mechanical', 'Passed', 'Exhaust and ventilation compliant.', 'Engr. M. Cruz',                                  TIMESTAMP(CURDATE() - INTERVAL 10 DAY, '10:45:00')),
(1, 'Fire',       'Failed', 'Emergency Exit Door #2: panic bar needs repair; clear stored crates from exit path.', 'Insp. M. Del Rosario (BFP)', TIMESTAMP(CURDATE() - INTERVAL 10 DAY, '11:15:00')),
(2, 'Structural', 'Passed', 'Compliant.', 'Engr. A. Lopez',  TIMESTAMP(@JAN1 + INTERVAL 60 DAY, '10:15:00')),
(2, 'Electrical', 'Passed', 'Compliant.', 'Engr. J. Tan',    TIMESTAMP(@JAN1 + INTERVAL 60 DAY, '10:40:00')),
(2, 'Mechanical', 'Passed', 'Compliant.', 'Engr. M. Cruz',   TIMESTAMP(@JAN1 + INTERVAL 60 DAY, '11:00:00')),
(2, 'Fire',       'Passed', 'Extinguishers and signage compliant.', 'Insp. M. Del Rosario (BFP)', TIMESTAMP(@JAN1 + INTERVAL 60 DAY, '11:30:00')),
(3, 'Structural', 'Pending', NULL, NULL, NULL),
(3, 'Electrical', 'Pending', NULL, NULL, NULL),
(3, 'Mechanical', 'Pending', NULL, NULL, NULL),
(3, 'Fire',       'Pending', NULL, NULL, NULL);

-- ---------- Construction Permit ----------
INSERT INTO construction_projects (project_id, business_id, reference_no, project_title, project_type, current_stage, status, estimated_cost, date_filed) VALUES
(1, 1, CONCAT('CP-', @Y, '-00001'), 'Warehouse Renovation & Extension', 'Renovation & Extension', 'Building Permit', 'Active', 850000.00, CURDATE() - INTERVAL 40 DAY);

INSERT INTO construction_clearances (project_id, clearance_type, clearance_no, status, approved_by, approved_at, remarks) VALUES
(1, 'Locational', CONCAT('LC-', @Y, '-00001'), 'Approved', 5,    CURDATE() - INTERVAL 25 DAY, 'Conforms to CLUP commercial zone'),
(1, 'FSEC',       NULL,                         'Pending',  NULL, NULL, 'For BFP evaluation'),
(1, 'Building',   NULL,                         'Pending',  NULL, NULL, 'Under OBO technical review'),
(1, 'Occupancy',  NULL,                         'Pending',  NULL, NULL, NULL);

-- ---------- Audit Log ----------
INSERT INTO audit_log (user_id, action, table_name, record_id, details, created_at) VALUES
(7, 'INSERT',        'business_permits',        2, 'Owner filed early renewal',            CURDATE() - INTERVAL 7 DAY),
(2, 'STATUS_CHANGE', 'business_permits',        2, 'Submitted -> Under Review',            CURDATE() - INTERVAL 6 DAY),
(6, 'UPDATE',        'inspection_items',        4, 'Fire inspection Failed: exit door #2', CURDATE() - INTERVAL 10 DAY),
(5, 'STATUS_CHANGE', 'construction_clearances', 1, 'Locational clearance Approved',        CURDATE() - INTERVAL 25 DAY);

-- Notifications are left empty on purpose: the app's AlertService generates them.

-- =====================================================================
--  3. QUICK CHECK (results show at the bottom in phpMyAdmin)
-- =====================================================================
SELECT e.full_name, hc.expiry_date,
  CASE WHEN hc.expiry_date < CURDATE() THEN 'Expired'
       WHEN hc.expiry_date <= CURDATE() + INTERVAL 30 DAY THEN 'Expiring Soon'
       ELSE 'Valid' END AS computed_status
FROM employees e JOIN health_certificates hc ON hc.employee_id = e.employee_id
WHERE e.business_id = 1
ORDER BY hc.expiry_date;

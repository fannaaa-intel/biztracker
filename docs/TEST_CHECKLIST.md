# BizTracker — Manual Test Checklist

Use this script to test the system role by role, for example before a defense or to take screenshots for the paper.
Tick each box when the result matches.

## Before you start
1. Start **MySQL** (and Apache) in the XAMPP Control Panel.
2. **Re-import `database/biztracker_db.sql`** (phpMyAdmin → Import) so the sample data and dates are fresh.
   The expected numbers below assume a fresh import made **today**.
   - "Y" means the current year.
   - "+20 d" means 20 days from today.
3. Run the app: `dotnet run --project src/BizTracker`.
4. Every password is `password123`.
5. Follow the roles in the order below. Some steps change data that later steps rely on.

**Things to check on every screen:**
- No scrollbars: lists scroll with the mouse wheel and show "Scroll for more ▾".
- No text is cut off.
- Disabled buttons are gray, and hovering over one explains why.
- Every message uses the app's own animated message box.

Also try the window both maximized and restored to its smallest size.

---

## 1. Login screen

| # | Action | Expected | ✓ |
|---|---|---|---|
| 1.1 | Click **Sign in** with empty fields | "Please enter your username and password." | ☐ |
| 1.2 | `admin` / `wrong` four times | After the 4th try the message warns "1 attempt left before a 1-minute lock." | ☐ |
| 1.3 | Wrong password a 5th time | "Too many failed attempts. Please try again in 60 seconds." Even the right password is refused until then. | ☐ |
| 1.4 | Tick **Show password** | The password becomes readable | ☐ |
| 1.5 | Press **Tab** from the username | Focus moves to the password, then to the next controls in order | ☐ |

---

## 2. Owner — `owner` (Cristan's Bakeshop)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 2.1 | Sign in | An urgent-alert summary appears ("1 urgent alert needs attention": Lilah Espergal's expired certificate). Click **Later**. | ☐ |
| 2.2 | Look at the sidebar | Dashboard, the six services and Permit Vault. **No Settings.** | ☐ |
| 2.3 | Dashboard | "Welcome, Cristan Dela Cruz" and **"Action Required: 4 items need attention"**: Lilah expired, Ben Reyes expiring, Fire endorsement pending, Fire inspection failed | ☐ |
| 2.4 | Bell in the top bar | **3** unread alerts for the bakeshop only | ☐ |
| 2.5 | Click Lilah's alert | Health Certificates opens. The bell drops to 2. | ☐ |
| 2.6 | Health Certificates cards | Total **10**, Valid **8**, Expiring Soon **1**, Expired **1**, Compliance **80%** | ☐ |
| 2.7 | Health Certificates buttons | No add/edit/renew buttons (read-only) | ☐ |
| 2.8 | Business Permits | The business name is fixed (no selector). The list has 2 applications. BP-(Y+1)-00001 is **Under Review**. | ☐ |
| 2.9 | Endorsements tab | Barangay, Sanitary, RPT and Zoning are Endorsed. **Fire is Pending.** There are no Endorse/Reject links. | ☐ |
| 2.10 | Requirements tab → **Upload** on a pending document | A file picker opens. After choosing a file, the document shows **Submitted**. | ☐ |
| 2.11 | Business Permits buttons | **Update Status** and **Delete** are not shown. **New Application** / **Renew Permit** are available. | ☐ |
| 2.12 | Real Property Tax | 2 properties (₱ 55,000.00 assessed). Q1–Q3 paid, nothing overdue, Tax Clearance **Issued**. | ☐ |
| 2.13 | Annual Inspections | **3 of 4 passed**. Fire failed (exit door #2). Re-inspection is +30 d. Certificate is **Locked**. | ☐ |
| 2.14 | Construction Permit | CP-Y-00001 at the **Building Permit** stage, 1 of 4 clearances approved. Owner cannot approve. | ☐ |
| 2.15 | Permit Vault | **21** documents: 19 valid, 1 expiring, 1 expired | ☐ |
| 2.16 | Select Lilah's (expired) certificate | **Print Document** is disabled with a reason | ☐ |
| 2.17 | Select MP-Y-00001 → **Print Document** | The Mayor's Permit opens in the browser with the LGU header, reference number and verification code | ☐ |
| 2.18 | **Verify Document** → enter MP-Y-00001 and the code from 2.17 | Result: **Valid** | ☐ |
| 2.19 | Log out (power icon) | A confirmation appears, then you return to the sign-in screen | ☐ |

---

## 3. BPLO — `bplo` (Liza Ramirez)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 3.1 | Sidebar | Dashboard, Business Permits, Permit Vault only | ☐ |
| 3.2 | Dashboard | **"Action Required: 12 items need attention"** across the bakeshop and Kusina. Santos has none. Only the Business Permits card's **Open** button is enabled. | ☐ |
| 3.3 | Business Permits → choose **Kusina ni Aling Rosa** | BP-(Y+1)-00002 is **Submitted** | ☐ |
| 3.4 | **Update Status** | Only the next step (**Under Review**) is offered. After confirming, the status changes. | ☐ |
| 3.5 | Bakeshop BP-(Y+1)-00001 → **Update Status** | Blocked: waiting for the **Fire** endorsement | ☐ |
| 3.6 | Endorsements tab → Endorse / Reject | Links work. Reject asks for a reason. | ☐ |
| 3.7 | **Renew Permit** for Santos | The dialog shows the renewal fields. Saving with an empty amount shows a red "Enter a valid amount" message. | ☐ |
| 3.8 | Search box above the list | Typing filters the rows. A search with no match shows "No … match your search." | ☐ |
| 3.9 | Permit Vault → Santos | 6 documents, including the Annual Inspection Certificate AIC-Y-00002 | ☐ |

---

## 4. Health — `health` (Dr. Paolo Agustin)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 4.1 | Sidebar | Sanitary Permits and Health Certificates only. Opens on Sanitary Permits. | ☐ |
| 4.2 | Health Certificates (bakeshop) | 10 / 8 / 1 / 1 / 80%. **Renew Now (2)** tab. | ☐ |
| 4.3 | Select **Lilah Espergal** → **Issue / Renew** | A new HC-Y-… certificate is issued, valid for 1 year. The cards change to Valid **9**, Expired **0**, **90%**. | ☐ |
| 4.4 | **Add Employee** with an empty name | Red "Enter the employee's full name.", nothing saved | ☐ |
| 4.5 | Sanitary Permits → **Kusina** | SP-Y-00003 is **For Inspection** | ☐ |
| 4.6 | **Record Inspection** with score 90 | Saved, and the permit can be issued. When issued, the Sanitary endorsement on Kusina's business permit becomes **Endorsed**. | ☐ |

---

## 5. Assessor — `assessor` (Ramon Villanueva)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 5.1 | Real Property Tax → **Kusina** | **Overdue ₱ 262.50** (Q2 and Q3) shown in red. Tax Clearance **Not Issued**. | ☐ |
| 5.2 | **Record Payment** (Q2) | An OR-Y-… number is generated automatically and a 2%/month late penalty is added | ☐ |
| 5.3 | Pay Q3 too | Nothing is overdue any more. Tax Clearance **Issued**. The RPT endorsement on Kusina's application is set to Endorsed. | ☐ |
| 5.4 | **Add Property** with PIN `015-06-001-01-099`, TD `TD-2026-99999`, assessed value 40000 | Preview: annual tax **₱ 800.00** (1% basic + 1% SEF) | ☐ |
| 5.5 | **Generate Assessment** for the new property | The assessment is created for this year (₱ 200.00 per quarter) | ☐ |

---

## 6. Inspector — `inspector` (Insp. Mark Del Rosario)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 6.1 | Annual Inspections (bakeshop) | **3 of 4 passed**, Fire failed, certificate **Locked** | ☐ |
| 6.2 | **Record Result** → Fire → **Passed** | **4 of 4 passed**. The inspection is Completed and the certificate AIC-Y-… unlocks. The **Fire** endorsement on BP-(Y+1)-00001 becomes Endorsed. | ☐ |
| 6.3 | Record a **Failed** result without findings | Red "Describe the deficiency found (required when Failed)." | ☐ |
| 6.4 | Kusina | INS-Y-00003 scheduled +14 d, **0 of 4 passed** | ☐ |

---

## 7. Building — `building` (Engr. Teresa Castillo)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 7.1 | Construction Permit (bakeshop) | Stage **Building Permit**. Locational approved. "Approve the FSEC first." shown for Building. | ☐ |
| 7.2 | **Advance Stage** | Blocked: the Building clearance must be approved first (stages cannot be skipped) | ☐ |
| 7.3 | Approve **FSEC**, verify the documents, then approve **Building** | Each step is allowed only in this order | ☐ |
| 7.4 | Kusina → **New Project** with an empty title | Red "Enter the project title." | ☐ |

---

## 8. Admin — `admin` (System Administrator)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 8.1 | Sign in | Summary "4 urgent alerts need attention" and the bell shows **7** alerts. These are the fresh-import numbers; after sections 4–6 fixed some items, there are fewer. | ☐ |
| 8.2 | Sidebar | Every screen, including **Settings** under ADMINISTRATION | ☐ |
| 8.3 | Settings → **Users** tab | 7 accounts. Cards show Active Users 7 and Active Businesses 3. | ☐ |
| 8.4 | Select **admin** | **Deactivate** is gray and its tooltip says you cannot deactivate your own account | ☐ |
| 8.5 | **Add User** → clear every field, Role = Owner → **Add User** | Red messages under username, full name, business and password | ☐ |
| 8.6 | **Businesses** tab → **Add Business**. Fill in name, owner, line of business, address and barangay → **Add Business**. | A new row appears, and you're asked "Create Owner Account?" | ☐ |
| 8.7 | **Create Owner Account** → keep the suggested username, note the temporary password → **Create Account** | The password is shown once (and copied to the clipboard). The registry's LOGIN column shows the username. | ☐ |
| 8.8 | Log out and sign in as the new owner with that password | The dashboard shows only the new business ("All clear"). There is no Settings item. | ☐ |
| 8.9 | Sign in as admin → Businesses → select the new business → **Deactivate** | Confirmation, then the status is **Inactive**. The business disappears from the module lists and its owner cannot sign in. | ☐ |
| 8.10 | **Reactivate** | Status Active. The owner can sign in again. | ☐ |
| 8.11 | Users → select the new owner → **Reset Password** | Confirmation, then a new temporary password. The old one no longer works. | ☐ |
| 8.12 | **Settings** tab → double-click **Expiring Soon warning** → type `400` → **Save Value** | Red "Enter a number from 1 to 365." | ☐ |
| 8.13 | Type `45` → Save | "Expiring Soon warning is now 45 days." Change it back to `30` afterwards. | ☐ |
| 8.14 | **Late renewal surcharge** → type `25%` | Saved as `0.25 (25%)` | ☐ |
| 8.15 | **Audit Log** tab | Today's actions are at the top (logins, the new business, the owner account, settings changes...) | ☐ |
| 8.16 | Set **User** = bplo, or **Table** = settings | The list filters immediately. "Clear Filters" restores the last 30 days. | ☐ |
| 8.17 | Set **From** after **To** | Red message: the "From" date is after the "To" date | ☐ |
| 8.18 | **Backup** tab → **Back Up Now** → choose a folder → Save | "The database was saved to …". The history lists the file. **Show file** opens Explorer. The "Last Backup" card shows today. | ☐ |
| 8.19 | Business Permits → **New Application** for any business, then select it (status Submitted) → **Delete** | Asks for confirmation, then removes it. Delete is shown only to Admin, and is gray for applications past Submitted (e.g. BP-(Y+1)-00002 after step 3.4). | ☐ |
| 8.20 | Press **F5** on any screen | The data reloads | ☐ |
| 8.21 | **Ctrl+1 … Ctrl+9** | Switch between screens | ☐ |

---

## 9. Window size and look (any role)

| # | Action | Expected | ✓ |
|---|---|---|---|
| 9.1 | Restore the window to its smallest size (1100 × 680) and visit every screen | Nothing overlaps or is cut off. Long lists show "Scroll for more ▾". Less important table columns hide instead of cutting text. Buttons switch to short labels. | ☐ |
| 9.2 | Maximize again | The layout stretches to fill the screen | ☐ |
| 9.3 | Open any dialog and press **Tab** repeatedly | Focus moves through the fields from top to bottom and left to right, then to the buttons | ☐ |
| 9.4 | Stop MySQL in XAMPP, then press F5 | A friendly "Cannot connect to the MySQL server" message appears. The app does not crash. Start MySQL again afterwards. | ☐ |

When you're done, re-import `database/biztracker_db.sql` to return to the clean sample data.

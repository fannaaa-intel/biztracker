#!/usr/bin/env bash
# =====================================================================
#  Migration regression test (Revisions 1, 2A, 2B, 4A)
#
#  What it checks:
#    1. The database as it was BEFORE Revision 1 (biztracker_db.sql from git, commit 5a97a84^)
#       can be upgraded with the migrations in database/migrations/ without errors.
#    2. The migrations are safe to run twice (the second run must not fail or change anything).
#    3. The final checks pass: 0 problem quarters, no duplicate OR numbers, 0 bad construction schedules.
#    4. The upgraded schema is IDENTICAL to a fresh import of today's database/biztracker_db.sql
#       (columns, types, defaults, indexes, foreign keys).
#
#  It NEVER touches your real biztracker_db: it works in two scratch databases
#  (biztracker_migtest_old / biztracker_migtest_new) and drops them at the end.
#
#  Run from the project folder (Git Bash, XAMPP MySQL started):
#    bash tests/migrations/run_migration_test.sh
#  Exit code 0 = PASS, 1 = FAIL.
# =====================================================================
set -u

MYSQL="${MYSQL:-/c/xampp/mysql/bin/mysql.exe}"
DB_ARGS=(-u root -h 127.0.0.1)
OLD_DB=biztracker_migtest_old
NEW_DB=biztracker_migtest_new
BEFORE_REVISION_1=5a97a84^          # last commit before the Forgot Password revision
MIGRATIONS=(2026-10-15_forgot_password 2026-10-15_rpt_partial_payments 2026-10-15_bp_payments
            2026-10-16_construction_schedule)

cd "$(git rev-parse --show-toplevel)" || exit 1
WORK=$(mktemp -d)
FAILED=0

fail() { echo "FAIL: $*"; FAILED=1; }
sql()  { "$MYSQL" "${DB_ARGS[@]}" "$@"; }
# Point a SQL script at a scratch database instead of biztracker_db
retarget() { sed "s/\bbiztracker_db\b/$1/g"; }

cleanup() {
    sql -e "DROP DATABASE IF EXISTS $OLD_DB; DROP DATABASE IF EXISTS $NEW_DB;" 2>/dev/null
    rm -rf "$WORK"
}
trap cleanup EXIT

sql -e "SELECT 1" >/dev/null || { echo "FAIL: cannot connect to MySQL. Start it in the XAMPP Control Panel."; exit 1; }

echo "== 1. Build the pre-Revision-1 database ($OLD_DB)"
git show "$BEFORE_REVISION_1:database/biztracker_db.sql" | retarget $OLD_DB | sql > /dev/null || { fail "old schema import"; exit 1; }

echo "== 2. Run the migrations (twice, to prove they are safe to re-run)"
for run in 1 2; do
    for m in "${MIGRATIONS[@]}"; do
        retarget $OLD_DB < "database/migrations/$m.sql" | sql --batch > "$WORK/$m.run$run.txt" 2>&1 \
            || { fail "$m (run $run)"; cat "$WORK/$m.run$run.txt"; exit 1; }
    done
done
for m in "${MIGRATIONS[@]}"; do
    cmp -s "$WORK/$m.run1.txt" "$WORK/$m.run2.txt" || fail "$m gave different results on the second run"
done

echo "== 3. Final checks"
echo "-- accounts without an e-mail:"; cat "$WORK/2026-10-15_forgot_password.run2.txt"
grep -qx "problem_quarters" "$WORK/2026-10-15_rpt_partial_payments.run2.txt" &&
grep -qx "0" "$WORK/2026-10-15_rpt_partial_payments.run2.txt" || fail "RPT check: problem_quarters is not 0"
grep -q "times_used" "$WORK/2026-10-15_bp_payments.run2.txt" && fail "duplicate OR numbers found"
grep -qx "bad_schedules" "$WORK/2026-10-16_construction_schedule.run2.txt" &&
grep -qx "0" "$WORK/2026-10-16_construction_schedule.run2.txt" || fail "construction check: bad_schedules is not 0"
echo "-- RPT / business tax checks:"; cat "$WORK/2026-10-15_rpt_partial_payments.run2.txt" "$WORK/2026-10-15_bp_payments.run2.txt"

echo "== 4. Compare with a fresh database/biztracker_db.sql ($NEW_DB)"
retarget $NEW_DB < database/biztracker_db.sql | sql > /dev/null || { fail "current schema import"; exit 1; }

# One line per column / index column / foreign key, without the database name, sorted.
schema_of() {
    sql --batch --skip-column-names -e "
      SELECT CONCAT('COLUMN ', TABLE_NAME, '.', COLUMN_NAME, ' #', ORDINAL_POSITION, ' ', COLUMN_TYPE, ' null=', IS_NULLABLE,
                    ' default=', IFNULL(COLUMN_DEFAULT, 'NONE'), ' ', EXTRA)
        FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = '$1'
      UNION ALL
      SELECT CONCAT('INDEX ', TABLE_NAME, ' ', IF(NON_UNIQUE = 0, 'UNIQUE ', ''), '(', GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX), ')')
        FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = '$1'
       GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE
      UNION ALL
      SELECT CONCAT('FK ', k.TABLE_NAME, '.', k.COLUMN_NAME, ' -> ', k.REFERENCED_TABLE_NAME, '.', k.REFERENCED_COLUMN_NAME,
                    ' delete=', r.DELETE_RULE, ' update=', r.UPDATE_RULE)
        FROM information_schema.KEY_COLUMN_USAGE k
        JOIN information_schema.REFERENTIAL_CONSTRAINTS r
          ON r.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA AND r.CONSTRAINT_NAME = k.CONSTRAINT_NAME
       WHERE k.TABLE_SCHEMA = '$1'
      UNION ALL
      SELECT CONCAT('TABLE ', TABLE_NAME, ' ', ENGINE) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '$1'" | sort
}
schema_of $OLD_DB > "$WORK/migrated.txt"
schema_of $NEW_DB > "$WORK/fresh.txt"
if diff -u "$WORK/fresh.txt" "$WORK/migrated.txt" > "$WORK/schema.diff"; then
    echo "Schema identical ($(wc -l < "$WORK/fresh.txt") lines compared)."
else
    fail "migrated schema differs from biztracker_db.sql ('-' = fresh import, '+' = migrated):"
    cat "$WORK/schema.diff"
fi

echo
if [ $FAILED -eq 0 ]; then echo "MIGRATION TEST: PASS"; else echo "MIGRATION TEST: FAIL"; fi
exit $FAILED

-- Reverses 20261017001446_AddRoleAssignments.sql: drops the view, then the
-- triggers and their functions. The migration drops role_assignments next, and
-- with it every assignment, which holds only emails and roles that an
-- Administrator can re-enter. role_assignments is new in this migration, so a
-- rollback returns to the schema as it was before the table existed.
DROP VIEW active_role_assignments;
DROP TRIGGER role_assignments_never_truncated ON role_assignments;
DROP FUNCTION refuse_truncate_of_role_assignments();
DROP TRIGGER role_assignments_immutable ON role_assignments;
DROP FUNCTION enforce_role_assignments_immutability();

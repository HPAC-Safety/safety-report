-- Role assignments (issue #909, ADR-0199): who holds Safety Officer or
-- Administrator, by email. The table has just been created by the migration.
--
-- A row is never deleted and never rewritten. The one change it allows is the
-- revoke of an active row: revoked_at and revoked_by_subject set together,
-- once (CON-DP-017). A refusal names the table and, for an update, the column,
-- never a value: the email is personal data and must not reach a log.
--
-- There is no session setting, role, or flag that bypasses the trigger. Like the
-- reporter's account triggers (ADR-0178), a migration that must change a row
-- disables it inside its own transaction and enables it again before it ends.
CREATE FUNCTION enforce_role_assignments_immutability() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
DECLARE
    changed_column text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'role_assignments rows are never deleted' USING ERRCODE = '23000';
    END IF;

    IF OLD.revoked_at IS NOT NULL THEN
        RAISE EXCEPTION 'role_assignments rows cannot be changed once revoked' USING ERRCODE = '23000';
    END IF;

    FOREACH changed_column IN ARRAY ARRAY ['id', 'email', 'role', 'granted_at', 'granted_by_subject']
        LOOP
            IF to_jsonb(NEW) -> changed_column IS DISTINCT FROM to_jsonb(OLD) -> changed_column THEN
                RAISE EXCEPTION 'role_assignments.% cannot be changed', changed_column
                    USING ERRCODE = '23000';
            END IF;
        END LOOP;

    -- Active before, so the only legal change sets the revoke pair. The table's
    -- check constraint already forces the two columns to be set together.
    RETURN NEW;
END
$$;

CREATE TRIGGER role_assignments_immutable
    BEFORE UPDATE OR DELETE
    ON role_assignments
    FOR EACH ROW
EXECUTE FUNCTION enforce_role_assignments_immutability();

-- TRUNCATE removes every row below any row trigger, so it is refused on its own.
CREATE FUNCTION refuse_truncate_of_role_assignments() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
BEGIN
    RAISE EXCEPTION 'role_assignments rows are never deleted' USING ERRCODE = '23000';
END
$$;

CREATE TRIGGER role_assignments_never_truncated
    BEFORE TRUNCATE
    ON role_assignments
    FOR EACH STATEMENT
EXECUTE FUNCTION refuse_truncate_of_role_assignments();

-- The assignments that still grant a role: what the role resolver and the
-- manage-roles list read. Who granted and who revoked stay in the table.
CREATE VIEW active_role_assignments AS
SELECT assignment.id,
       assignment.email,
       assignment.role,
       assignment.granted_at
FROM role_assignments AS assignment
WHERE assignment.revoked_at IS NULL;

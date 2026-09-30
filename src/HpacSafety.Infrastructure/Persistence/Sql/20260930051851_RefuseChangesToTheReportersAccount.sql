-- The database refuses a change to the reporter's account and to a summary
-- revision (issue #669, ADR-0178). Immutability used to hold only in the
-- domain (`private init`, no mutator); a stray UPDATE, a migration, or a
-- raw-SQL path could still rewrite what a reporter said. These four
-- column-scoped BEFORE UPDATE OR DELETE triggers make Postgres refuse it too.
--
-- Each function lists the columns it locks, and the trigger fires only for an
-- UPDATE that names one of them, so a write to a metadata column (status,
-- hidden_at, ...) never pays for the check. An UPDATE that names a locked
-- column but leaves its value as it was is not a change and passes. A
-- refusal names the table and column, never a value: a value is report
-- content. No row of these tables is ever deleted (AGENTS.md invariant 8), so
-- DELETE is refused outright; retirement is the `deleted` stamp.
--
-- There is no session setting, role, or flag that bypasses them. A migration
-- that must change a locked column disables the trigger inside its own
-- transaction and turns it back on before it ends, and argues that in its own
-- ADR:
--
--     ALTER TABLE report_answers DISABLE TRIGGER report_answers_immutable;
--     ... the one change ...
--     ALTER TABLE report_answers ENABLE TRIGGER report_answers_immutable;

-- report_answers: every reporter column is locked. The second language is
-- written once, by the Worker (ADR-0174), and `deleted` is stamped once.
CREATE FUNCTION enforce_report_answers_immutability() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
DECLARE
    changed_column text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'report_answers rows are never deleted' USING ERRCODE = '23000';
    END IF;

    FOREACH changed_column IN ARRAY ARRAY [
        'id', 'report_id', 'question_id', 'question_revision_id', 'question_key', 'is_private',
        'value', 'value_boolean', 'choice_id', 'locale', 'translation_mode', 'answered_at']
        LOOP
            IF to_jsonb(NEW) -> changed_column IS DISTINCT FROM to_jsonb(OLD) -> changed_column THEN
                RAISE EXCEPTION 'report_answers.% cannot be changed after submission', changed_column
                    USING ERRCODE = '23000';
            END IF;
        END LOOP;

    FOREACH changed_column IN ARRAY ARRAY ['translated_value', 'translation_source', 'deleted']
        LOOP
            IF to_jsonb(OLD) -> changed_column <> 'null'::jsonb
                AND to_jsonb(NEW) -> changed_column IS DISTINCT FROM to_jsonb(OLD) -> changed_column THEN
                RAISE EXCEPTION 'report_answers.% is set once and cannot be changed again', changed_column
                    USING ERRCODE = '23000';
            END IF;
        END LOOP;

    RETURN NEW;
END
$$;

CREATE TRIGGER report_answers_immutable
    BEFORE UPDATE OF id, report_id, question_id, question_revision_id, question_key, is_private,
        value, value_boolean, choice_id, locale, translation_mode, answered_at,
        translated_value, translation_source, deleted
        OR DELETE
    ON report_answers
    FOR EACH ROW
EXECUTE FUNCTION enforce_report_answers_immutability();

-- report_files: what arrived is locked. What the Worker and a reviewer record
-- about it (the derivative, validation, a processing failure, hidden, deleted)
-- stays writable.
CREATE FUNCTION enforce_report_files_immutability() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
DECLARE
    changed_column text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'report_files rows are never deleted' USING ERRCODE = '23000';
    END IF;

    FOREACH changed_column IN ARRAY ARRAY [
        'id', 'report_id', 'report_answer_id', 'kind', 'blob_key', 'original_file_name',
        'content_type', 'byte_size', 'uploaded_at']
        LOOP
            IF to_jsonb(NEW) -> changed_column IS DISTINCT FROM to_jsonb(OLD) -> changed_column THEN
                RAISE EXCEPTION 'report_files.% cannot be changed after upload', changed_column
                    USING ERRCODE = '23000';
            END IF;
        END LOOP;

    RETURN NEW;
END
$$;

CREATE TRIGGER report_files_immutable
    BEFORE UPDATE OF id, report_id, report_answer_id, kind, blob_key, original_file_name,
        content_type, byte_size, uploaded_at
        OR DELETE
    ON report_files
    FOR EACH ROW
EXECUTE FUNCTION enforce_report_files_immutability();

-- reports: the language it was written in, when it was submitted, and the
-- consent answers are locked. Status, published_at, the unpublish note, the
-- summary error, and `deleted` stay writable.
CREATE FUNCTION enforce_reports_immutability() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
DECLARE
    changed_column text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'reports rows are never deleted' USING ERRCODE = '23000';
    END IF;

    FOREACH changed_column IN ARRAY ARRAY [
        'id', 'language', 'submitted_at', 'consent_publish', 'consent_media', 'consent_documents']
        LOOP
            IF to_jsonb(NEW) -> changed_column IS DISTINCT FROM to_jsonb(OLD) -> changed_column THEN
                RAISE EXCEPTION 'reports.% cannot be changed after submission', changed_column
                    USING ERRCODE = '23000';
            END IF;
        END LOOP;

    RETURN NEW;
END
$$;

CREATE TRIGGER reports_immutable
    BEFORE UPDATE OF id, language, submitted_at, consent_publish, consent_media, consent_documents
        OR DELETE
    ON reports
    FOR EACH ROW
EXECUTE FUNCTION enforce_reports_immutability();

-- summary_revisions (ADR-0177): the text, its sources, its provenance, its
-- author, and its place in the sequence are locked. Approval may be set and
-- cleared again (Unpublish clears the latest revision's approval), so
-- approved_at and approved_by_subject stay writable; `deleted` is stamped once.
CREATE FUNCTION enforce_summary_revisions_immutability() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
DECLARE
    changed_column text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'summary_revisions rows are never deleted' USING ERRCODE = '23000';
    END IF;

    FOREACH changed_column IN ARRAY ARRAY [
        'id', 'summary_id', 'sequence', 'ai_summary_en', 'ai_summary_fr', 'source_en', 'source_fr',
        'model', 'prompt_version', 'author_subject', 'created_at', 'restored_from_id']
        LOOP
            IF to_jsonb(NEW) -> changed_column IS DISTINCT FROM to_jsonb(OLD) -> changed_column THEN
                RAISE EXCEPTION 'summary_revisions.% cannot be changed after it is saved', changed_column
                    USING ERRCODE = '23000';
            END IF;
        END LOOP;

    IF OLD.deleted IS NOT NULL AND NEW.deleted IS DISTINCT FROM OLD.deleted THEN
        RAISE EXCEPTION 'summary_revisions.deleted is set once and cannot be changed again'
            USING ERRCODE = '23000';
    END IF;

    RETURN NEW;
END
$$;

CREATE TRIGGER summary_revisions_immutable
    BEFORE UPDATE OF id, summary_id, sequence, ai_summary_en, ai_summary_fr, source_en, source_fr,
        model, prompt_version, author_subject, created_at, restored_from_id, deleted
        OR DELETE
    ON summary_revisions
    FOR EACH ROW
EXECUTE FUNCTION enforce_summary_revisions_immutability();

-- TRUNCATE removes every row at once, below any row trigger, so each of the four
-- tables also refuses it with a statement-level trigger (owner, 2026-09-30). A
-- TRUNCATE ... CASCADE from another table fires these too, for every table it
-- would empty. A migration that must truncate disables the trigger in its own
-- transaction, like any other.
CREATE FUNCTION refuse_truncate_of_reporters_account() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
BEGIN
    RAISE EXCEPTION '% rows are never deleted', TG_TABLE_NAME USING ERRCODE = '23000';
END
$$;

CREATE TRIGGER report_answers_never_truncated
    BEFORE TRUNCATE ON report_answers
    FOR EACH STATEMENT
EXECUTE FUNCTION refuse_truncate_of_reporters_account();

CREATE TRIGGER report_files_never_truncated
    BEFORE TRUNCATE ON report_files
    FOR EACH STATEMENT
EXECUTE FUNCTION refuse_truncate_of_reporters_account();

CREATE TRIGGER reports_never_truncated
    BEFORE TRUNCATE ON reports
    FOR EACH STATEMENT
EXECUTE FUNCTION refuse_truncate_of_reporters_account();

CREATE TRIGGER summary_revisions_never_truncated
    BEFORE TRUNCATE ON summary_revisions
    FOR EACH STATEMENT
EXECUTE FUNCTION refuse_truncate_of_reporters_account();

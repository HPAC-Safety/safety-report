-- ADR-0130: every yes/no and checkbox answer, on live and soft-deleted reports
-- alike, becomes a boolean. yes and oui are true, no and non are false, and a
-- skipped answer stays skipped. The word, its fixed counterpart, and the
-- `fixed` mode go; the answer's locale still records the reporter's language.
--
-- A boolean answer holding anything else stops the migration before anything
-- is converted. The error gives a count, never a value: a value is report
-- content.
DO
$$
    DECLARE
        unreadable integer;
    BEGIN
        SELECT count(*)
        INTO unreadable
        FROM report_answers AS answer
                 JOIN question_revisions AS revision ON revision.id = answer.question_revision_id
        WHERE revision.type IN ('yes_no', 'checkbox')
          AND answer.value IS NOT NULL
          AND answer.value NOT IN ('yes', 'no', 'oui', 'non');

        IF unreadable > 0 THEN
            RAISE EXCEPTION 'StoreYesOrNoAsABoolean: % yes/no or checkbox answer(s) are not one of the four words; nothing was converted.', unreadable;
        END IF;
    END
$$;

UPDATE report_answers AS answer
SET value_boolean      = CASE WHEN answer.value IS NULL THEN NULL ELSE answer.value IN ('yes', 'oui') END,
    value              = NULL,
    translated_value   = NULL,
    translation_source = NULL,
    translation_mode   = 'none'
FROM question_revisions AS revision
WHERE revision.id = answer.question_revision_id
  AND revision.type IN ('yes_no', 'checkbox');

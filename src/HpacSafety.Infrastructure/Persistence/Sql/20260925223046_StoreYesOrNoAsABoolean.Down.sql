-- Restores ADR-0127's word form: each boolean becomes the word in its answer's
-- locale, with the other language's word as its fixed counterpart. A skipped
-- answer stays skipped, in mode `fixed` as ADR-0127 recorded it.
UPDATE report_answers AS answer
SET value              = CASE
                             WHEN answer.value_boolean IS NULL THEN NULL
                             WHEN answer.locale = 'fr-CA' THEN CASE WHEN answer.value_boolean THEN 'oui' ELSE 'non' END
                             ELSE CASE WHEN answer.value_boolean THEN 'yes' ELSE 'no' END
                         END,
    translated_value   = CASE
                             WHEN answer.value_boolean IS NULL THEN NULL
                             WHEN answer.locale = 'fr-CA' THEN CASE WHEN answer.value_boolean THEN 'yes' ELSE 'no' END
                             ELSE CASE WHEN answer.value_boolean THEN 'oui' ELSE 'non' END
                         END,
    translation_source = CASE WHEN answer.value_boolean IS NULL THEN NULL ELSE 'fixed' END,
    translation_mode   = 'fixed'
FROM question_revisions AS revision
WHERE revision.id = answer.question_revision_id
  AND revision.type IN ('yes_no', 'checkbox');

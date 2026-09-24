-- ADR-0112: only free text marked as needing translation is machine-translated.
-- Long text starts out marked; every other type (including short text) does not.
UPDATE question_revisions
SET is_translatable = true
WHERE type = 'long_text';

-- Each existing answer takes the mode its revision implies. Answers already
-- given to select and type-ahead questions keep 'machine': their stored second
-- language, if any, came from the Worker, not from a choice. Everything else
-- (short text, email, phone, date, time, number, yes/no, checkbox, file) is
-- 'none', the column default, so any second language it holds is never shown.
UPDATE report_answers AS answer
SET translation_mode = 'machine'
FROM question_revisions AS revision
WHERE revision.id = answer.question_revision_id
  AND revision.type IN ('long_text', 'single_select', 'multi_select', 'autocomplete');

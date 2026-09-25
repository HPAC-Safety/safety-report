-- Restores the translation queue as CreateAdminQueueViews defined it, before
-- the choice reference it filters on is dropped. The linked answers keep the
-- labels they always stored, and the removed choices step 2 of the Up script
-- made stay, unreferenced.
CREATE OR REPLACE VIEW answers_awaiting_translation AS
SELECT answer.id,
       answer.question_key,
       answer.value,
       answer.locale,
       answer.answered_at
FROM report_answers AS answer
WHERE answer.deleted IS NULL
  AND answer.value IS NOT NULL
  AND answer.translated_value IS NULL
  AND answer.translation_mode = 'machine';

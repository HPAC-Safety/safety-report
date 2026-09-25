-- Puts back each revision's required option code from the choice it names,
-- before depends_on_choice_id is dropped. A condition that followed a
-- replacement reads the code of the choice it named, as it did before.
UPDATE question_revisions AS revision
SET depends_on_option_code = choice.code
FROM question_choices AS choice
WHERE choice.id = revision.depends_on_choice_id;

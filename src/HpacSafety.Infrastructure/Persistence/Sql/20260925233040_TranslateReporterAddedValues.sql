-- ADR-0129: each language of a choice records how it was produced. Every label
-- written before this was written by a person — an Administrator, or the
-- reporter who typed it — so it is 'human'. A missing language has no source.
UPDATE question_choices
SET label_en_source = CASE WHEN label_en IS NULL THEN NULL ELSE 'human' END,
    label_fr_source = CASE WHEN label_fr IS NULL THEN NULL ELSE 'human' END;

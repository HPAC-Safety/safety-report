-- ADR-0108: every existing summary language was written by the Worker's model
-- call (the column default), except a pair a reviewer wrote by hand after a
-- failed summarization, which carries 'manual' as its model.
UPDATE summaries
SET source_en = 'human',
    source_fr = 'human'
WHERE model = 'manual';

-- Removes a trailing colon from every stored question label, in place
-- (issue #697, ADR-0181).
--
-- A label is now stored without its closing punctuation: the interface adds the
-- colon after an answerable question's label in the reader's locale (`Label:` in
-- English, `Label :` in French), and a summary heading is the bare label. The seed
-- wrote `Description:` and `Description :`, and an administrator may have typed
-- the same.
--
-- This rewrites stored revision text, which no other migration does. It is the
-- one carved exception to "an answered question forks instead of being revised"
-- (ADR-0071, AGENTS.md invariant 1), argued in ADR-0181: it changes presentation
-- only, creates no revision, fork, or row, deletes none, and leaves every answer
-- naming the same revision. question_revisions is not one of the four tables
-- ADR-0178 locks, so no trigger is bypassed.
--
-- The colon may follow a plain space, a no-break space, or a narrow no-break
-- space (French typography). Everything from the last word's end to the end of
-- the label is removed; a label with no trailing colon is not touched.
UPDATE question_revisions
SET label_en = CASE
                   WHEN label_en ~ ':[[:space:]  ]*$'
                       THEN regexp_replace(label_en, '[[:space:]  ]*:[[:space:]  ]*$', '')
                   ELSE label_en
    END,
    label_fr = CASE
                   WHEN label_fr ~ ':[[:space:]  ]*$'
                       THEN regexp_replace(label_fr, '[[:space:]  ]*:[[:space:]  ]*$', '')
                   ELSE label_fr
    END
WHERE label_en ~ ':[[:space:]  ]*$'
   OR label_fr ~ ':[[:space:]  ]*$';

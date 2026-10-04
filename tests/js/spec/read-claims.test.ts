import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { changedScenarios, scenarioTexts } from '../../../tools/spec/read-claims.ts'

const FEATURE = `Feature: Media
  Files a reporter attaches.

  Background:
    Given a member is signed in

  @REQ-MED-001
  Scenario: A file is kept
    A description of the rule.
    Given a file
    Then it is kept

  Rule: Videos

    Background:
      Given a video

    @REQ-MED-002 @ui
    Scenario Outline: A <kind> is remuxed
      Then it is remuxed

      @slow
      Examples:
        | kind |
        | mp4  |
`

describe('scenarioTexts', () => {
	it('holds each claim with its tags, steps, examples, and the Backgrounds that run before it', () => {
		const texts = scenarioTexts(FEATURE)
		assert.deepEqual([...texts.keys()], ['REQ-MED-001', 'REQ-MED-002'])
		assert.equal(texts.get('REQ-MED-001'), ['Given a member is signed in', '@REQ-MED-001', 'Scenario: A file is kept', 'A description of the rule.', 'Given a file', 'Then it is kept'].join('\n'))
		assert.equal(
			texts.get('REQ-MED-002'),
			['Given a member is signed in', 'Given a video', '@REQ-MED-002 @ui', 'Scenario Outline: A <kind> is remuxed', 'Then it is remuxed', '@slow', 'Examples:', '| kind |', '| mp4 |'].join('\n'),
		)
	})
})

describe('changedScenarios', () => {
	it('finds nothing in whitespace, a comment, or the feature description', () => {
		const edited = `# a comment\n${FEATURE.replace('Files a reporter attaches.', 'Files, reworded.').replace(/ {4}Then it is kept/, '\tThen   it is kept\n')}`
		assert.deepEqual(changedScenarios(FEATURE, edited), [])
	})

	it('finds a changed step, tag, or example row', () => {
		assert.deepEqual(changedScenarios(FEATURE, FEATURE.replace('Then it is kept', 'Then it is kept forever')), ['REQ-MED-001'])
		assert.deepEqual(changedScenarios(FEATURE, FEATURE.replace('@REQ-MED-002 @ui', '@REQ-MED-002 @ui @ignore')), ['REQ-MED-002'])
		assert.deepEqual(changedScenarios(FEATURE, FEATURE.replace('| mp4  |', '| mov |')), ['REQ-MED-002'])
	})

	it('finds every claim a changed Background runs before', () => {
		assert.deepEqual(changedScenarios(FEATURE, FEATURE.replace('Given a member is signed in', 'Given an administrator is signed in')), ['REQ-MED-001', 'REQ-MED-002'])
		assert.deepEqual(changedScenarios(FEATURE, FEATURE.replace('Given a video', 'Given a long video')), ['REQ-MED-002'])
	})

	it('reads a stray tag at the end of the file as the start of nothing', () => {
		assert.deepEqual([...scenarioTexts(`${FEATURE}\n  @wip\n`).keys()], ['REQ-MED-001', 'REQ-MED-002'])
	})

	it('finds an added and a removed scenario', () => {
		assert.deepEqual(changedScenarios('', FEATURE), ['REQ-MED-001', 'REQ-MED-002'])
		assert.deepEqual(changedScenarios(FEATURE, ''), ['REQ-MED-001', 'REQ-MED-002'])
	})
})

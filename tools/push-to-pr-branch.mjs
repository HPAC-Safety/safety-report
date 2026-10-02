#!/usr/bin/env node
// Temporary: main's i18n-translate.yml runs on pull_request_target from the base
// branch and still calls this old path against the pull request that moved the
// script to tools/github/push-to-pr-branch.mjs (#778). Delete once that pull request has merged.
// Importing runs it: the moved script's command guard matches this path too.
import './github/push-to-pr-branch.mjs'

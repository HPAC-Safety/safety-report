// What a workflow step's script needs from GitHub Actions and the shell, in one
// place, so each script under tools/ stays a set of pure functions plus a thin
// main() (ADR-0189).
//
// Every effect goes through an object a test can replace: `exec` runs a
// command, `env` is the environment, and the output, summary, and annotation
// helpers write where Actions reads them. A script takes `{ exec, env, log }`
// as its main() argument and defaults each to the real one.
import { spawnSync } from 'node:child_process'
import { appendFileSync, realpathSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

/** An environment: `process.env`, or what a test hands a script. */
export type Env = Readonly<Record<string, string | undefined>>

export interface ExecOptions {
	cwd?: string
	env?: Env
	input?: string
	check?: boolean
	inherit?: boolean
}

export interface ExecResult {
	status: number
	stdout: string
	stderr: string
}

/** The shape of `exec`, so a script can take a stand-in for it. */
export type Exec = (command: string, args?: readonly string[], options?: ExecOptions) => ExecResult

export type AnnotationLevel = 'error' | 'warning' | 'notice'

/**
 * Runs a command and returns `{ status, stdout, stderr }`, trimmed. Never
 * throws on a non-zero exit; `check: true` throws with the command's stderr.
 * `input` is written to its stdin; `inherit: true` streams its output instead
 * of capturing it.
 */
export function exec(command: string, args: readonly string[] = [], { cwd, env, input, check = false, inherit = false }: ExecOptions = {}): ExecResult {
	const result = spawnSync(command, [...args], {
		cwd,
		env: env ? { ...process.env, ...env } : process.env,
		input,
		encoding: 'utf8',
		stdio: inherit ? ['pipe', 'inherit', 'inherit'] : 'pipe',
		maxBuffer: 64 * 1024 * 1024,
	})
	if (result.error) throw result.error
	const outcome = { status: result.status ?? 1, stdout: trimmed(result.stdout), stderr: trimmed(result.stderr) }
	if (check && outcome.status !== 0) {
		throw new Error(`${command} ${args.join(' ')} exited ${outcome.status}${outcome.stderr ? `: ${outcome.stderr}` : ''}`)
	}
	return outcome
}

/** The captured stream, trimmed; `null` when `inherit` sent it to the terminal instead. */
function trimmed(stream: string | null | undefined): string {
	return (stream ?? '').trim()
}

/** Like exec with `check: true`, returning only stdout. */
export function run(command: string, args: readonly string[] = [], options: ExecOptions = {}): string {
	return exec(command, args, { ...options, check: true }).stdout
}

/** Appends `name=value` to $GITHUB_OUTPUT; a multi-line value uses a heredoc delimiter. */
export function setOutput(name: string, value: string | number | boolean, env: Env = process.env): void {
	const text = String(value)
	const line = text.includes('\n') ? `${name}<<__OUTPUT__\n${text}\n__OUTPUT__\n` : `${name}=${text}\n`
	if (env.GITHUB_OUTPUT) appendFileSync(env.GITHUB_OUTPUT, line)
	else process.stdout.write(line)
}

/** Appends a line to $GITHUB_ENV, for a later step's environment. */
export function setEnv(name: string, value: string | number | boolean, env: Env = process.env): void {
	if (env.GITHUB_ENV) appendFileSync(env.GITHUB_ENV, `${name}=${value}\n`)
}

/** Appends markdown to $GITHUB_STEP_SUMMARY, or prints it when there is none. */
export function appendSummary(markdown: string, env: Env = process.env): void {
	const text = markdown.endsWith('\n') ? markdown : `${markdown}\n`
	if (env.GITHUB_STEP_SUMMARY) appendFileSync(env.GITHUB_STEP_SUMMARY, text)
	else process.stdout.write(text)
}

/** The `::error::`/`::warning::`/`::notice::` line Actions turns into an annotation. */
export function annotation(level: AnnotationLevel, message: string, { file, line, title }: { file?: string; line?: number | string; title?: string } = {}): string {
	const props = [file && `file=${file}`, line && `line=${String(line)}`, title && `title=${title}`].filter(Boolean).join(',')
	return `::${level}${props ? ` ${props}` : ''}::${message}`
}

/** Reads a required environment variable, or throws naming it. */
export function required(env: Env, name: string): string {
	const value = env[name]
	if (value === undefined || value === '') throw new Error(`${name} is not set`)
	return value
}

/** The message of a thrown value: an Error's message, else the value as text. */
export function errorMessage(error: unknown): string {
	return error instanceof Error ? error.message : String(error)
}

/** Whether a module is being run as the command, rather than imported by a test. */
export function isMain(metaUrl: string): boolean {
	if (!process.argv[1]) return false
	return realpathSync(fileURLToPath(metaUrl)) === realpathSync(path.resolve(process.argv[1]))
}

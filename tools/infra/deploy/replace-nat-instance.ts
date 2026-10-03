#!/usr/bin/env node
// Replaces the NAT instance by starting an instance refresh on its Auto
// Scaling group and waiting for it (deploy-environment.yml).
//
// The NAT instance is the one resource this system ever deletes and recreates,
// every release (CON-INF-013). `nat_autoscaling_group_arn` is #465's actual
// output (network.tf, the fck_nat module) — an ARN, not a name, because that is
// what the module publishes; the ASG APIs take the name, so it is parsed off
// the ARN's last path segment
// (arn:...:autoScalingGroup:<id>:autoScalingGroupName/<name>) rather than a
// second output existing for a value one string op already gets.
import { type Exec, exec as realExec, isMain } from '../../lib/actions.ts'

export const POLL_SECONDS = 15
export const FAILED_STATUSES = ['Failed', 'Cancelled', 'RollbackFailed', 'RollbackSuccessful']

// Everything after the last slash (the shell's `${asg_arn##*/}`).
export function groupName(arn: string): string {
	return arn.slice(arn.lastIndexOf('/') + 1)
}

/** What one poll of the refresh status means: 'done', 'failed', or 'wait'. */
export function classify(status: string): 'done' | 'failed' | 'wait' {
	if (status === 'Successful') return 'done'
	if (FAILED_STATUSES.includes(status)) return 'failed'
	return 'wait'
}

const realSleep = (seconds: number): void => {
	Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, seconds * 1000)
}

export function main({
	exec = realExec,
	log = console.log,
	sleep = realSleep,
}: { exec?: Exec; log?: (line: string) => void; sleep?: (seconds: number) => void } = {}): number {
	const arn = exec('terraform', ['-chdir=infra', 'output', '-raw', 'nat_autoscaling_group_arn'], { check: true }).stdout
	const name = groupName(arn)
	const refreshId = exec(
		'aws',
		[
			'autoscaling',
			'start-instance-refresh',
			'--auto-scaling-group-name',
			name,
			'--preferences',
			'{"MinHealthyPercentage":0,"InstanceWarmup":60}',
			'--query',
			'InstanceRefreshId',
			'--output',
			'text',
		],
		{ check: true },
	).stdout
	log(`Started instance refresh ${refreshId} on ${name}.`)
	for (;;) {
		const status = exec(
			'aws',
			[
				'autoscaling',
				'describe-instance-refreshes',
				'--auto-scaling-group-name',
				name,
				'--instance-refresh-ids',
				refreshId,
				'--query',
				'InstanceRefreshes[0].Status',
				'--output',
				'text',
			],
			{ check: true },
		).stdout
		const outcome = classify(status)
		if (outcome === 'done') {
			log('NAT instance replaced.')
			return 0
		}
		if (outcome === 'failed') {
			log(`::error::NAT instance refresh ended as ${status}.`)
			return 1
		}
		sleep(POLL_SECONDS)
	}
}

if (isMain(import.meta.url)) process.exit(main())

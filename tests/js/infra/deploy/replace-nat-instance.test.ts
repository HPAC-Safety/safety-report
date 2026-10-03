import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { classify, groupName, main } from '../../../../tools/infra/deploy/replace-nat-instance.ts'
import { fakeExec } from '../fake-exec.ts'

describe('groupName', () => {
	it('takes the last path segment of the ARN', () => {
		assert.equal(groupName('arn:aws:autoscaling:r:1:autoScalingGroup:id:autoScalingGroupName/nat-asg'), 'nat-asg')
	})
})

describe('classify', () => {
	it('knows done, failed, and in-progress statuses', () => {
		assert.equal(classify('Successful'), 'done')
		for (const status of ['Failed', 'Cancelled', 'RollbackFailed', 'RollbackSuccessful']) assert.equal(classify(status), 'failed')
		for (const status of ['Pending', 'InProgress', 'None']) assert.equal(classify(status), 'wait')
	})
})

describe('replace-nat-instance', () => {
	const arn = 'arn:aws:autoscaling:r:1:autoScalingGroup:id:autoScalingGroupName/nat-asg'
	const start = 'aws autoscaling start-instance-refresh --auto-scaling-group-name nat-asg --preferences {"MinHealthyPercentage":0,"InstanceWarmup":60} --query InstanceRefreshId --output text'
	const describeLine = 'aws autoscaling describe-instance-refreshes --auto-scaling-group-name nat-asg --instance-refresh-ids ir-1 --query InstanceRefreshes[0].Status --output text'
	const outputLine = 'terraform -chdir=infra output -raw nat_autoscaling_group_arn'

	function scenario(statuses: string[]) {
		const queue = [...statuses]
		return fakeExec({
			[outputLine]: { stdout: arn },
			[start]: { stdout: 'ir-1' },
			[describeLine]: () => ({ stdout: queue.shift() ?? '' }),
		})
	}

	it('polls every 15 seconds until the refresh succeeds', () => {
		const { exec, calls } = scenario(['Pending', 'InProgress', 'Successful'])
		const logs: string[] = []
		const sleeps: number[] = []
		assert.equal(main({ exec, log: (m) => logs.push(m), sleep: (s) => sleeps.push(s) }), 0)
		assert.deepEqual(sleeps, [15, 15])
		assert.equal(calls.filter((c) => c === describeLine).length, 3)
		assert.deepEqual(logs, ['Started instance refresh ir-1 on nat-asg.', 'NAT instance replaced.'])
	})

	it('fails with the status when the refresh ends badly', () => {
		const { exec } = scenario(['Cancelled'])
		const logs: string[] = []
		assert.equal(main({ exec, log: (m) => logs.push(m), sleep() {} }), 1)
		assert.equal(logs.at(-1), '::error::NAT instance refresh ended as Cancelled.')
	})
})

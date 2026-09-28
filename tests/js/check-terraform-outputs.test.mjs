import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import {
	main,
	parseOutputs,
	problems,
	referencedDeployVariableKeys,
	referencedOutputNames,
	referencedSiteUrlsKeys,
} from '../../tools/check-terraform-outputs.mjs'

const OUTPUTS_HCL = [
	'output "deploy_variables" {',
	'  description = "..."',
	'  value = {',
	'    ECR_REPOSITORY_API    = aws_ecr_repository.this["api"].name',
	'    LAMBDA_FUNCTION_API   = aws_lambda_function.api.function_name',
	'    S3_BUCKET_SITE        = aws_s3_bucket.site.id',
	'  }',
	'}',
	'',
	'output "site_urls" {',
	'  value = {',
	'    public = "https://example.test/"',
	'    admin  = "https://example.test/admin/"',
	'  }',
	'}',
	'',
	'output "nat_autoscaling_group_name" {',
	'  value = aws_autoscaling_group.nat.name',
	'}',
	'',
	'output "dns_records_to_publish" {',
	'  value = {',
	'    aliases = [',
	'      {',
	'        type  = "CNAME"',
	'        name  = "example"',
	'        value = "somewhere"',
	'      },',
	'    ]',
	'  }',
	'}',
].join('\n')

/** Runs `main` with console output captured, restoring it afterwards even on failure. */
function runMain(input) {
	const output = { log: [], error: [] }
	const original = { log: console.log, error: console.error }
	console.log = (...args) => output.log.push(args.join(' '))
	console.error = (...args) => output.error.push(args.join(' '))
	try {
		return { code: main(input), output }
	} finally {
		console.log = original.log
		console.error = original.error
	}
}

describe('parseOutputs', () => {
	it('names every top-level output block', () => {
		const outputs = parseOutputs(OUTPUTS_HCL)
		assert.deepEqual(
			[...outputs.keys()].sort(),
			['deploy_variables', 'dns_records_to_publish', 'nat_autoscaling_group_name', 'site_urls'].sort(),
		)
	})

	it('collects the shallow keys of a value map, and nothing nested inside a list of objects', () => {
		const outputs = parseOutputs(OUTPUTS_HCL)
		assert.deepEqual([...outputs.get('deploy_variables')].sort(), ['ECR_REPOSITORY_API', 'LAMBDA_FUNCTION_API', 'S3_BUCKET_SITE'])
		assert.deepEqual([...outputs.get('site_urls')].sort(), ['admin', 'public'])
		// aliases is the one shallow key; type/name/value inside its list are not.
		assert.deepEqual([...outputs.get('dns_records_to_publish')], ['aliases'])
	})

	it('is empty for an output whose value is not an object literal', () => {
		const outputs = parseOutputs(OUTPUTS_HCL)
		assert.deepEqual([...outputs.get('nat_autoscaling_group_name')], [])
	})
})

describe('referencedOutputNames', () => {
	it('finds a -raw and a -json reference', () => {
		const text = [
			'          asg_name=$(terraform -chdir=infra output -raw nat_autoscaling_group_name)',
			'          terraform -chdir=infra output -json deploy_variables > /tmp/x.json',
		].join('\n')
		assert.deepEqual([...referencedOutputNames(text)].sort(), ['deploy_variables', 'nat_autoscaling_group_name'])
	})
})

describe('referencedDeployVariableKeys', () => {
	it('finds every steps.tf.outputs.KEY reference', () => {
		const text = '${{ steps.tf.outputs.ECR_REGISTRY }}/${{ steps.tf.outputs.ECR_REPOSITORY_API }}'
		assert.deepEqual([...referencedDeployVariableKeys(text)].sort(), ['ECR_REGISTRY', 'ECR_REPOSITORY_API'])
	})

	it('finds nothing when the file never reads steps.tf.outputs', () => {
		assert.deepEqual([...referencedDeployVariableKeys('no such reference here')], [])
	})
})

describe('referencedSiteUrlsKeys', () => {
	it('finds a jq key read only once site_urls was actually read as JSON', () => {
		const text = [
			"          echo \"public_url=$(terraform -chdir=infra output -json site_urls | jq -r '.public')\" >> \"$GITHUB_OUTPUT\"",
		].join('\n')
		assert.deepEqual([...referencedSiteUrlsKeys(text)], ['public'])
	})

	it('ignores an unrelated jq read when site_urls was never read as JSON', () => {
		const text = "echo \"$(terraform -chdir=infra output -json deploy_variables | jq -r '.public')\""
		assert.deepEqual([...referencedSiteUrlsKeys(text)], [])
	})
})

describe('problems', () => {
	it('is empty when every reference matches a declared output and key', () => {
		const workflows = [
			{
				path: '.github/workflows/example.yml',
				text: [
					'terraform -chdir=infra output -raw nat_autoscaling_group_name',
					'terraform -chdir=infra output -json deploy_variables',
					'${{ steps.tf.outputs.LAMBDA_FUNCTION_API }}',
					"terraform -chdir=infra output -json site_urls | jq -r '.public'",
				].join('\n'),
			},
		]
		assert.deepEqual(problems({ outputsHcl: OUTPUTS_HCL, workflows }), [])
	})

	it('reports an output name the workflow reads but outputs.tf never declares', () => {
		const workflows = [
			{
				path: '.github/workflows/example.yml',
				text: 'terraform -chdir=infra output -raw secret_id_gemini_api_key',
			},
		]
		const found = problems({ outputsHcl: OUTPUTS_HCL, workflows })
		assert.equal(found.length, 1)
		assert.match(found[0], /secret_id_gemini_api_key/)
	})

	it('reports a deploy_variables key the workflow reads but the output never declares', () => {
		const workflows = [{ path: '.github/workflows/example.yml', text: '${{ steps.tf.outputs.NOT_A_REAL_KEY }}' }]
		const found = problems({ outputsHcl: OUTPUTS_HCL, workflows })
		assert.equal(found.length, 1)
		assert.match(found[0], /NOT_A_REAL_KEY/)
		assert.match(found[0], /deploy_variables/)
	})

	it('reports a site_urls key the workflow reads but the output never declares', () => {
		const workflows = [
			{
				path: '.github/workflows/example.yml',
				text: "terraform -chdir=infra output -json site_urls | jq -r '.staging_only_field'",
			},
		]
		const found = problems({ outputsHcl: OUTPUTS_HCL, workflows })
		assert.equal(found.length, 1)
		assert.match(found[0], /staging_only_field/)
		assert.match(found[0], /site_urls/)
	})
})

describe('main', () => {
	it('exits 0 and logs a notice when nothing is wrong', () => {
		const { code, output } = runMain({ outputsHcl: OUTPUTS_HCL, workflows: [] })
		assert.equal(code, 0)
		assert.match(output.log[0], /::notice::/)
	})

	it('exits 1 and prints one ::error:: per problem, plus guidance', () => {
		const workflows = [{ path: '.github/workflows/example.yml', text: 'terraform -chdir=infra output -raw not_declared' }]
		const { code, output } = runMain({ outputsHcl: OUTPUTS_HCL, workflows })
		assert.equal(code, 1)
		assert.equal(output.error.filter((line) => line.startsWith('::error')).length, 1)
		assert.ok(output.error.some((line) => line.includes('Add the output')))
	})
})

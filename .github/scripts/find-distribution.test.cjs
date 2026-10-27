const test = require('node:test');
const assert = require('node:assert/strict');
const find = require('./find-distribution.cjs');

function fixture({ runs = [], artifacts = {}, jobs = {} } = {}) {
    const outputs = {};
    const github = {
        rest: { actions: { listWorkflowRuns: 'runs', listWorkflowRunArtifacts: 'artifacts', listJobsForWorkflowRun: 'jobs' } },
        paginate: async (method, args) => method === 'runs' ? runs : (method === 'artifacts' ? artifacts : jobs)[args.run_id] || []
    };
    const core = { setOutput: (key, value) => { outputs[key] = value; }, info: () => {} };
    return { github, core, context: { repo: { owner: 'owner', repo: 'repo' }, sha: 'a'.repeat(40), runId: 99 }, outputs };
}

async function identity(rid) {
    const input = fixture();
    await find({ ...input, rid });
    return input.outputs['artifact-name'];
}

test('artifact recipe identity is stable and RID-specific', async () => {
    const windows = await identity('win-x64');
    const linux = await identity('linux-x64');
    assert.match(windows, /^Memoria-FF8-win-x64-[0-9a-f]{16}$/);
    assert.notEqual(windows, linux);
    assert.equal(linux, await identity('linux-x64'));
});

test('a successful Windows artifact never satisfies Linux reuse', async () => {
    const input = fixture({ runs: [{ id: 7, conclusion: 'success' }], artifacts: { 7: [{ name: await identity('win-x64'), expired: false }] }, jobs: { 7: [{ steps: [{ name: 'Validated package receipt win-x64', conclusion: 'success' }] }] } });
    await find({ ...input, rid: 'linux-x64' });
    assert.equal(input.outputs.reuse, 'false');
    assert.equal(input.outputs['run-id'], '99');
});

test('publication failure still reuses each tested platform payload', async () => {
    const input = fixture({ runs: [{ id: 7, conclusion: 'failure' }], artifacts: { 7: [{ name: await identity('osx-x64'), expired: false }] } });
    await find({ ...input, rid: 'osx-x64' });
    assert.equal(input.outputs.reuse, 'true');
    assert.equal(input.outputs['run-id'], '7');
});

test('expired tested artifacts require recovery rather than rebuild', async () => {
    const input = fixture({ runs: [{ id: 7 }], artifacts: { 7: [{ name: await identity('linux-x64'), expired: true }] } });
    await assert.rejects(find({ ...input, rid: 'linux-x64' }), /expired or was deleted/);
});

test('successful platform receipt with failed artifact upload requires recovery', async () => {
    const input = fixture({ runs: [{ id: 7 }], jobs: { 7: [{ steps: [{ name: 'Validated package receipt linux-x64', conclusion: 'success' }] }] } });
    await assert.rejects(find({ ...input, rid: 'linux-x64' }), /refusing to rebuild/);
});

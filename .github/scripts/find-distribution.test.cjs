const test = require('node:test');
const assert = require('node:assert/strict');
const find = require('./find-distribution.cjs');

const source = 'a'.repeat(40);
const rids = ['win-x64', 'linux-x64', 'osx-x64', 'osx-arm64'];
function trustedRun(id = 7, overrides = {}) {
    return { id, event: 'push', head_branch: 'main', head_sha: source, repository: { full_name: 'owner/repo' }, ...overrides };
}

function fixture({ runs = [], artifacts = {}, jobs = {} } = {}) {
    const outputs = {};
    const github = {
        rest: { actions: { listWorkflowRuns: 'runs', listWorkflowRunArtifacts: 'artifacts', listJobsForWorkflowRun: 'jobs' } },
        paginate: async (method, args) => method === 'runs' ? runs : (method === 'artifacts' ? artifacts : jobs)[args.run_id] || []
    };
    const core = { setOutput: (key, value) => { outputs[key] = value; }, info: () => {} };
    return { github, core, context: { repo: { owner: 'owner', repo: 'repo' }, sha: source, runId: 99, ref: 'refs/heads/main', eventName: 'push' }, outputs };
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

test('main and tag builds reuse the same immutable payload in both directions for every RID', async () => {
    for (const rid of rids) {
        const name = await identity(rid);
        for (const [branch, ref] of [['main', 'refs/tags/v1.2.3'], ['v1.2.3', 'refs/heads/main']]) {
            const input = fixture({ runs: [trustedRun(7, { head_branch: branch })], artifacts: { 7: [{ name, expired: false }] } });
            input.context.ref = ref;
            await find({ ...input, rid });
            assert.equal(input.outputs['artifact-name'], name);
            assert.equal(input.outputs.reuse, 'true');
            assert.equal(input.outputs['run-id'], '7');
        }
    }
});

test('a successful Windows artifact never satisfies Linux reuse', async () => {
    const input = fixture({ runs: [trustedRun(7, { conclusion: 'success' })], artifacts: { 7: [{ name: await identity('win-x64'), expired: false }] }, jobs: { 7: [{ steps: [{ name: 'Validated package receipt win-x64', conclusion: 'success' }] }] } });
    await find({ ...input, rid: 'linux-x64' });
    assert.equal(input.outputs.reuse, 'false');
    assert.equal(input.outputs['run-id'], '99');
});

test('publication failure still reuses each tested platform payload', async () => {
    const input = fixture({ runs: [trustedRun(7, { conclusion: 'failure' })], artifacts: { 7: [{ name: await identity('osx-x64'), expired: false }] } });
    await find({ ...input, rid: 'osx-x64' });
    assert.equal(input.outputs.reuse, 'true');
    assert.equal(input.outputs['run-id'], '7');
});

test('expired tested artifacts require recovery rather than rebuild', async () => {
    const input = fixture({ runs: [trustedRun()], artifacts: { 7: [{ name: await identity('linux-x64'), expired: true }] } });
    await assert.rejects(find({ ...input, rid: 'linux-x64' }), /expired or was deleted/);
});

test('successful platform receipt with failed artifact upload requires recovery', async () => {
    const input = fixture({ runs: [trustedRun()], jobs: { 7: [{ steps: [{ name: 'Validated package receipt linux-x64', conclusion: 'success' }] }] } });
    await assert.rejects(find({ ...input, rid: 'linux-x64' }), /refusing to rebuild/);
});


test('Apple Silicon receipts are distinct from Intel and reusable independently', async () => {
    const intel = await identity('osx-x64');
    const arm = await identity('osx-arm64');
    assert.match(arm, /^Memoria-FF8-osx-arm64-[0-9a-f]{16}$/);
    assert.notEqual(arm, intel);
    const input = fixture({ runs: [trustedRun()], artifacts: { 7: [{ name: intel, expired: false }] } });
    await find({ ...input, rid: 'osx-arm64' });
    assert.equal(input.outputs.reuse, 'false');
    const matching = fixture({ runs: [trustedRun(8)], artifacts: { 8: [{ name: arm, expired: false }] } });
    await find({ ...matching, rid: 'osx-arm64' });
    assert.equal(matching.outputs.reuse, 'true');
    assert.equal(matching.outputs['run-id'], '8');
});


test('active trusted runs with uploaded receipts can be reused', async () => {
    const input = fixture({ runs: [trustedRun(99, { status: 'in_progress', conclusion: null })], artifacts: { 99: [{ name: await identity('osx-arm64'), expired: false }] } });
    await find({ ...input, rid: 'osx-arm64' });
    assert.equal(input.outputs.reuse, 'true');
    assert.equal(input.outputs['run-id'], '99');
});

test('untrusted runs cannot supply artifacts or block trusted builds', async () => {
    const name = await identity('linux-x64');
    for (const overrides of [
        { event: 'pull_request', head_branch: 'main' },
        { event: 'pull_request_target' },
        { head_branch: 'feature' },
        { head_branch: 'v-invalid' },
        { head_sha: 'b'.repeat(40) },
        { repository: { full_name: 'other/repo' } }
    ]) {
        const input = fixture({ runs: [trustedRun(7, overrides)], artifacts: { 7: [{ name, expired: false }] }, jobs: { 7: [{ steps: [{ name: 'Validated package receipt linux-x64', conclusion: 'success' }] }] } });
        await find({ ...input, rid: 'linux-x64' });
        assert.equal(input.outputs.reuse, 'false', JSON.stringify(overrides));
    }
});

test('pull requests may reuse trusted dispatch artifacts without trusting PR artifacts', async () => {
    const name = await identity('linux-x64');
    const input = fixture({ runs: [trustedRun(7, { event: 'pull_request' }), trustedRun(8, { event: 'workflow_dispatch' })], artifacts: { 7: [{ name, expired: false }], 8: [{ name, expired: false }] } });
    input.context.eventName = 'pull_request';
    input.context.ref = 'refs/pull/123/merge';
    await find({ ...input, rid: 'linux-x64' });
    assert.equal(input.outputs.reuse, 'true');
    assert.equal(input.outputs['run-id'], '8');
});

test('publication matrix covers four native RIDs with macOS 26 architecture-specific hosts', () => {
    const fs = require('node:fs');
    const workflow = fs.readFileSync('.github/workflows/build.yml', 'utf8');
    const rows = [...workflow.matchAll(/- os: ([^\r\n]+)\r?\n +rid: ([^\r\n]+)/g)];
    assert.deepEqual(rows.map(row => row[2]).sort(), ['linux-x64', 'osx-arm64', 'osx-x64', 'win-x64']);
    assert.equal(rows.find(row => row[2] === 'osx-arm64')[1], 'macos-26');
    assert.equal(rows.find(row => row[2] === 'osx-x64')[1], 'macos-26-intel');
    assert.match(workflow, /Verify the native macOS build host/);
    assert.match(workflow, /Publication\.Tests\.ps1/);
});

test('source and RID packaging queues feed a publication job without compilation or restamping', () => {
    const workflow = require('node:fs').readFileSync('.github/workflows/build.yml', 'utf8');
    const packageJob = workflow.split(/^  package:\r?$/m)[1].split(/^  publish:\r?$/m)[0];
    const publishJob = workflow.split(/^  publish:\r?$/m)[1];
    const lock = packageJob.match(/^    concurrency:\r?\n((?:      [^\r\n]*\r?\n)+)/m)?.[1];
    assert.ok(lock);
    assert.match(lock, /group: desktop-distribution-.*\$\{\{ github\.sha \}\}-\$\{\{ matrix\.rid \}\}/);
    assert.doesNotMatch(lock, /github\.ref/);
    assert.match(lock, /^      cancel-in-progress: false\r?$/m);
    assert.match(lock, /^      queue: max\r?$/m);
    assert.match(publishJob, /^      queue: max\r?$/m);
    assert.doesNotMatch(publishJob, /setup-dotnet|Test-InstalledSdk|\bdotnet\b|Build\.ps1|Set-DistributionVersion|--set-version/);
    assert.match(publishJob, /Publish-Distributions\.ps1/);
    assert.match(packageJob, /\$version = 'source'/);
});

test('pre-build validations run in dedicated bounded steps and respect artifact reuse', () => {
    const workflow = require('node:fs').readFileSync('.github/workflows/build.yml', 'utf8');
    const steps = workflow.split(/^      - /m).slice(1);
    const validations = [
        ['node --test .github/scripts/*.test.cjs', 2],
        ['./tools/linux-testing/tests/Orchestration.Tests.ps1', 3],
        ['./tools/packaging/tests/Packaging.Tests.ps1', 5],
        ['./tools/packaging/tests/Publication.Tests.ps1', 2],
        ['./tools/packaging/tests/MacSigning.Tests.ps1', 8],
        ['./tools/Get-PlatformInventory.ps1 -Check', 2]
    ];
    for (const [command, timeout] of validations) {
        const matches = steps.filter(step => step.includes(command));
        assert.equal(matches.length, 1, `${command} must run exactly once`);
        const step = matches[0];
        assert.match(step, /^name: .+/);
        assert.match(step, /^        if: steps\.distribution\.outputs\.reuse != 'true'\r?$/m);
        assert.match(step, new RegExp(`^        timeout-minutes: ${timeout}\\r?$`, 'm'));
        assert.match(step, /^        shell: pwsh\r?$/m);
        const run = step.match(/^        run: \|\r?\n((?:          [^\r\n]*\r?\n)+)/m);
        assert.ok(run, `${command} must have a run block`);
        assert.deepEqual(run[1].trim().split(/\r?\n/).map(line => line.trim()), [command], `${command} must be the only command in its step`);
    }
});

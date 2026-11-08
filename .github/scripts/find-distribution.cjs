// Called under a source/RID-specific packaging lock. A receipt is a build success,
// independent of whether publishing the release later succeeds.
const fs = require('node:fs');
const crypto = require('node:crypto');
module.exports = async function findDistribution({ github, context, core, rid }) {
    if (!['win-x64', 'linux-x64', 'osx-x64', 'osx-arm64'].includes(rid)) {
        throw new Error('A supported RID is required for distribution reuse.');
    }
    const recipe = crypto.createHash('sha256');
    recipe.update('source-payload-v1');
    for (const file of ['Build.ps1', 'global.json', 'Directory.Packages.props', 'tools/New-DistributionManifest.ps1', 'tools/packaging/MacBundleLauncher.targets', 'tools/Memoria.FF8.Distribution.Check/MacBundleSigning.cs', 'src/Memoria.FF8.Platform/MacCodeSigning.cs']) {
        recipe.update(file);
        recipe.update(fs.readFileSync(file));
    }
    const identity = recipe.digest('hex');
    const name = `Memoria-FF8-${rid}-${identity.slice(0, 16)}`;
    core.setOutput('artifact-name', name);
    core.setOutput('recipe', identity);
    const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
        ...context.repo, workflow_id: 'build.yml', head_sha: context.sha, per_page: 100
    });
    let previouslyBuilt = false;
    for (const run of runs) {
        const repository = run.repository?.full_name;
        if (run.head_sha !== context.sha || !['push', 'workflow_dispatch'].includes(run.event) || !(run.head_branch === 'main' || /^v[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?$/.test(run.head_branch || '')) || (repository && repository.toLowerCase() !== `${context.repo.owner}/${context.repo.repo}`.toLowerCase())) {
            continue;
        }
        const artifacts = await github.paginate(github.rest.actions.listWorkflowRunArtifacts, {
            ...context.repo, run_id: run.id, per_page: 100
        });
        for (const artifact of artifacts) {
            if (artifact.name !== name) {
                continue;
            }
            previouslyBuilt = true;
            if (artifact.expired) {
                continue;
            }
            core.setOutput('run-id', String(run.id));
            core.setOutput('reuse', 'true');
            core.info(`Reusing tested ${context.sha}/${rid} recipe ${identity} from run ${run.id}.`);
            return;
        }
        if (run.id !== context.runId || run.run_attempt > 1) {
            const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
                ...context.repo, run_id: run.id, filter: 'all', per_page: 100
            });
            previouslyBuilt ||= jobs.some(job => job.steps?.some(step => step.name === `Validated package receipt ${rid}` && step.conclusion === 'success'));
        }
    }
    if (previouslyBuilt) {
        throw new Error(`The tested distribution for ${context.sha}/${rid} expired or was deleted. Restore the original tested artifact; refusing to rebuild this source/RID.`);
    }
    core.setOutput('run-id', String(context.runId));
    core.setOutput('reuse', 'false');
};

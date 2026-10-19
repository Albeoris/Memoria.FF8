// Called while the package job holds the commit-specific concurrency lock.
module.exports = async function findDistribution({ github, context, core }) {
    const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
        ...context.repo,
        workflow_id: 'build.yml',
        head_sha: context.sha,
        per_page: 100
    });
    let previouslyBuilt = false;
    for (const run of runs) {
        const artifacts = await github.paginate(github.rest.actions.listWorkflowRunArtifacts, {
            ...context.repo,
            run_id: run.id,
            per_page: 100
        });
        for (const artifact of artifacts) {
            if (artifact.name !== 'Memoria-FF8-win-x64') {
                continue;
            }
            previouslyBuilt = true;
            if (artifact.expired) {
                continue;
            }
            core.setOutput('run-id', String(run.id));
            core.setOutput('reuse', 'true');
            core.info(`Reusing tested distribution for ${context.sha} from run ${run.id}.`);
            return;
        }
        previouslyBuilt ||= run.conclusion === 'success';
        // A completed build can outlive a failed upload or release publication.
        // Include earlier attempts when this workflow run is being retried.
        if (run.id !== context.runId || run.run_attempt > 1) {
            const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
                ...context.repo,
                run_id: run.id,
                filter: 'all',
                per_page: 100
            });
            previouslyBuilt ||= jobs.some(job => job.steps?.some(step => step.name === 'Build and test with the installed .NET 10 SDK' && step.conclusion === 'success'));
        }
    }
    if (previouslyBuilt) {
        throw new Error(`The distribution for ${context.sha} has expired or was deleted. Restore the original tested ZIP; refusing to rebuild this commit.`);
    }
    core.setOutput('run-id', String(context.runId));
    core.setOutput('reuse', 'false');
};

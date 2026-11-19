using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private sealed record NativePreparation(bool Success, NativeTextureMods Textures, IReadOnlyList<ModDiagnostic> Diagnostics);

    private sealed class CandidatePreparation : IDisposable
    {
        public required ModCatalogSnapshot Catalog { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required Task<NativePreparation> Native { get; init; }
        public required IReadOnlyList<TextureBuildContext> Contexts { get; init; }
        public ResourceService.PreparationJob? Resources { get; set; }
        public Func<ResourceService.PreparedResources, ModPreparationResult>? Finish { get; set; }
        public bool PublicationPrepared { get; set; }
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }
            Cancellation.Cancel();
            Resources?.Dispose();
            Resources = null;
            Finish = null;
            if (Native.IsCompletedSuccessfully)
            {
                Native.Result.Textures.ReleasePreparationPixels();
            }
            else
            {
                _ = Native.ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        task.Result.Textures.ReleasePreparationPixels();
                    }
                    else
                    {
                        _ = task.Exception;
                    }
                }, TaskScheduler.Default);
            }
            Cancellation.Dispose();
        }
    }

    private CandidatePreparation? candidatePreparation;

    private void CompleteStartupPreparation(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Pump(force: true) || !Mods.IsReloadPending || Mods.View.Outcome is ModUpdateOutcome.Failed or ModUpdateOutcome.Blocked or ModUpdateOutcome.ReviewRequired)
            {
                return;
            }
            if (cancellationToken.WaitHandle.WaitOne(5))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private CandidatePreparation GetCandidatePreparation(ModCatalogSnapshot catalog)
    {
        if (candidatePreparation is { } current && ReferenceEquals(current.Catalog, catalog))
        {
            return current;
        }
        candidatePreparation?.Dispose();
        NativeTextureMods previous = nativeTextureMods;
        IReadOnlyList<TextureBuildContext> contexts = Resources.CaptureTextureContextsForPreparation();
        NativeTextureRequest[] requests = contexts.Select(NativeTextureRequest.FromContext).ToArray();
        CancellationTokenSource cancellation = new();
        CancellationToken token = cancellation.Token;
        Task<NativePreparation> native = catalog.Files.Any(file => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("textures/", StringComparison.OrdinalIgnoreCase)) ? Task.Run(() =>
        {
            bool valid = NativeTextureMods.TryPrepare(catalog, out NativeTextureMods textures, out IReadOnlyList<ModDiagnostic> diagnostics, token, previous, requests);
            return new NativePreparation(valid, textures, diagnostics);
        }, token) : Task.FromResult(new NativePreparation(true, NativeTextureMods.Empty, []));
        candidatePreparation = new() { Catalog = catalog, Cancellation = cancellation, Native = native, Contexts = contexts };
        return candidatePreparation;
    }

    private ModPreparationResult DeferredCandidate(CandidatePreparation candidate) => new(false) { Deferred = true, Discard = () => DiscardCandidate(candidate) };

    private void DiscardCandidate(CandidatePreparation candidate)
    {
        if (ReferenceEquals(candidatePreparation, candidate))
        {
            candidatePreparation = null;
        }
        candidate.Dispose();
    }

    private ModPreparationResult AdvanceCandidate(CandidatePreparation candidate)
    {
        ResourceService.PreparationJob job = candidate.Resources!;
        if (job.IsStale)
        {
            DiscardCandidate(candidate);
            return new(false) { Deferred = true };
        }
        try
        {
            if (!job.Advance(TimeSpan.FromMilliseconds(4), maximumUnits: 64, cancellationToken: candidate.Cancellation.Token))
            {
                return DeferredCandidate(candidate);
            }
            ResourceService.PreparedResources prepared = job.Prepared!;
            ModPreparationResult result = candidate.Finish!(prepared);
            if (job.IsStale)
            {
                result.Discard?.Invoke();
                DiscardCandidate(candidate);
                return new(false) { Deferred = true };
            }
            candidate.Native.Result.Textures.ReleasePreparationPixels();
            candidate.PublicationPrepared = true;
            job.Dispose();
            candidate.Resources = null;
            candidate.Finish = null;
            return result with
            {
                Commit = () =>
                {
                    result.Commit?.Invoke();
                    DiscardCandidate(candidate);
                },
                Discard = () =>
                {
                    result.Discard?.Invoke();
                    DiscardCandidate(candidate);
                }
            };
        }
        catch (InvalidOperationException) when (job.IsStale)
        {
            DiscardCandidate(candidate);
            return new(false) { Deferred = true };
        }
        catch
        {
            DiscardCandidate(candidate);
            throw;
        }
    }
}

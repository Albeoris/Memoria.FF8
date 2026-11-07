using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly EffectPreviewDrainObservationBudget effectPreviewDrainObservations = new();

    private void ObserveEffectPreviewDrain(BattleSession active, bool blocked, bool timeout)
    {
        if (effectPreview?.SourceSnapshot is null || effectPreviewExpectedEffect is not (77 or 101))
        {
            return;
        }
        uint? returned = active.NativeGraphics?.OriginalEffect?.ReturnedDescriptor;
        string? boundary = effectPreviewDrainObservations.Observe(returned is > 0, returned == 0, blocked, timeout);
        if (boundary is null)
        {
            return;
        }
        try
        {
            string directory = effectPreview.Presentation?.ReceiptDirectory ?? options.ControlDirectory!;
            object observation = new { schema_version = 1, boundary, effect = effectPreviewExpectedEffect, source_sha256 = effectPreview.SourceSha256, drain_blockers = active.ObserveDiagnosticDrainBlockers(), owners = active.ObserveDiagnosticEffectPreviewOwners() };
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(observation, DiagnosticBattleJson);
            if (bytes.Length > 32768)
            {
                throw new InvalidDataException("The bounded component drain observation exceeded its scalar report budget.");
            }
            using FileStream file = new(Path.Combine(directory, "component-drain-" + boundary + ".json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            file.Write(bytes);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or JsonException or InvalidOperationException or IndexOutOfRangeException or OverflowException)
        {
            Console.Error.WriteLine("Component drain observation unavailable: " + error.GetType().Name);
        }
    }
}

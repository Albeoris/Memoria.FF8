using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.BattleEffects;

public static class PilotSlowReplay
{
    public static PilotManifest Prepare(PilotSlowEffectsReport query, string caseId, string executable, string outputRoot)
    {
        EvidenceStore.Id(caseId);
        PilotSlowCase[] matches = query.Effects.SelectMany(effect => effect.Cases).Where(item => item.Case.Id == caseId).ToArray();
        if (matches.Length != 1)
        {
            throw new ArgumentException("Select exactly one scenario from the slow-effect query result.", nameof(caseId));
        }
        PilotSlowCase selected = matches[0];
        PrivateEvidencePath.Validate(Path.GetDirectoryName(selected.SourceManifestPath)!);
        byte[] source = ReadBounded(selected.SourceManifestPath);
        if (Hash(source) != selected.SourceManifestSha256)
        {
            throw new InvalidDataException("The original scenario manifest changed after the slow-effect query.");
        }
        PilotManifest previous = JsonSerializer.Deserialize<PilotManifest>(source, RecordJson.Options) ?? throw new InvalidDataException("The original manifest is empty.");
        PilotCase[] originals = previous.Cases.Where(item => item.Id == caseId && item.Status == "RUNNABLE" && item.EffectId == selected.Case.EffectId && item.RecipeSha256 == selected.Case.RecipeSha256).ToArray();
        if (originals.Length != 1 || selected.Case.Status != "RUNNABLE" || selected.Case.Recipe is null || selected.Case.RecipeSha256 is null)
        {
            throw new InvalidDataException("The replay scenario differs from its original invocation and recipe identity.");
        }
        PrivateEvidencePath.Validate(Path.GetDirectoryName(selected.Case.Recipe)!);
        if (Hash(ReadBounded(selected.Case.Recipe)) != selected.Case.RecipeSha256)
        {
            throw new InvalidDataException("The replay recipe changed after its measured invocation.");
        }
        string host = Path.GetFullPath(executable);
        string workingDirectory = Path.GetDirectoryName(host)!;
        PilotManifest replay = previous with { Executable = host, WorkingDirectory = workingDirectory, OutputRoot = Path.GetFullPath(outputRoot), Cases = [selected.Case], BuildFingerprint = null };
        PilotRunner.ValidateManifest(replay);
        return replay with { BuildFingerprint = EvidenceStore.HashRecord(EvidenceStore.DescribeFiles(workingDirectory)) };
    }

    private static byte[] ReadBounded(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 4 * 1024 * 1024)
        {
            throw new InvalidDataException("Replay inputs must be bounded nonempty records.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

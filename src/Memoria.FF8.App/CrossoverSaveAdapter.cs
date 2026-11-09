using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class CrossoverSaveAdapter
{
    internal const string Ff8Codec = "memoria-ff8-gameplay-components-v1";

    internal static CombinedSessionSave? ReadEnvelope(string path)
    {
        using FileStream source = File.OpenRead(path);
        return CombinedSessionCodec.HasSignature(source) ? CombinedSessionCodec.Read(source) : null;
    }

    internal static GameplaySaveComponentDocument ReadFf8(CombinedSessionSave save)
    {
        if (save.Ff8PayloadFormat != Ff8Codec || save.Ff8Payload is not { Length: > 0 } payload)
        {
            throw new NotSupportedException("This crossover save does not contain a supported FFVIII continuation.");
        }
        using MemoryStream source = new(payload, false);
        return GameplaySaveCodec.Read(source);
    }

    internal static SaveSummary? ReadSummary(string path)
    {
        CombinedSessionSave? save = ReadEnvelope(path);
        if (save is null)
        {
            return GameSession.ReadSummary(path);
        }
        using MemoryStream source = new(save.Ff8Payload ?? throw new InvalidDataException("A player slot requires an FFVIII continuation."), false);
        ReadFf8(save);
        return GameplaySaveCodec.ReadSummary(source);
    }
}

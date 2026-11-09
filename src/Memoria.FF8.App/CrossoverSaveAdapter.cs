using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class CrossoverSaveAdapter
{
    internal const string Ff8Codec = "memoria-ff8-gameplay-components-v1";

    internal static bool RequiresCurrentFf8(CombinedSessionSave save)
    {
        return save.ActiveGame == SessionGame.Ff7 && save.Ff8Payload is null && save.Ff8PayloadFormat is null;
    }

    internal static (CombinedSessionSave Save, T Target) PrepareFf7OnlyLoad<T>(CombinedSessionSave save, WorldSwitchAvailability availability, Func<byte[]> captureFf8, Func<CombinedSessionSave, T> prepareTarget) where T : class
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(captureFf8);
        ArgumentNullException.ThrowIfNull(prepareTarget);
        save.Validate();
        if (!availability.Allowed)
        {
            throw new InvalidOperationException(availability.Reason ?? "Reach an FFVIII save-enabled boundary before loading this FFVII-only save.");
        }
        if (!RequiresCurrentFf8(save))
        {
            throw new InvalidOperationException("Only an FFVII-only save can retain the current FFVIII world.");
        }
        byte[] payload = captureFf8();
        CombinedSessionSave source = save with { ActiveGame = SessionGame.Ff8, Ff8Payload = payload, Ff8PayloadFormat = Ff8Codec };
        _ = ReadFf8(source);
        WorldSwitch transaction = new(source);
        T prepared = default!;
        transaction.ToFf7(availability, payload, Ff8Codec, () => throw new InvalidDataException("The selected save has no FFVII continuation."), target =>
        {
            prepared = prepareTarget(source with { ActiveGame = SessionGame.Ff7, Ff7 = target });
            ArgumentNullException.ThrowIfNull(prepared);
        });
        return (transaction.State, prepared);
    }

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

    internal static void EnsureOriginalExportAllowed(string path)
    {
        if (ReadEnvelope(path) is not null)
        {
            throw new NotSupportedException("Combined world saves require Memoria format. Original FFVIII saves cannot preserve FFVII progress and appearance mappings.");
        }
    }

    internal static SaveSummary? ReadSummary(string path)
    {
        CombinedSessionSave? save = ReadEnvelope(path);
        if (save is null)
        {
            return GameSession.ReadSummary(path);
        }
        if (save.ActiveGame != SessionGame.Ff8)
        {
            throw new NotSupportedException("An active FFVII save requires its own game summary.");
        }
        using MemoryStream source = new(save.Ff8Payload ?? throw new InvalidDataException("A player slot requires an FFVIII continuation."), false);
        ReadFf8(save);
        return GameplaySaveCodec.ReadSummary(source);
    }
}

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    public ImmutableArray<NativeBattlePrimitiveReference> PresentationPackets { get; init; } = [];
}

/// <summary>Accepted physical append references, independent of whether an emitter captured geometry.</summary>
internal static class NativeBattleEffectPresentationPackets
{
    internal static ImmutableArray<NativeBattlePrimitiveReference> Record(ImmutableArray<NativeBattlePrimitiveReference> packets, string bank, byte buffer, int start, NativeBattleGfGeometryBatch batch)
    {
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (AcceptedReference(bank, buffer, start, attempt) is { } reference)
            {
                packets = packets.Add(reference);
            }
        }
        return packets;
    }

    internal sealed class Collector
    {
        private readonly List<NativeBattlePrimitiveReference> packets = [];

        internal void Record(string bank, byte buffer, int start, NativeBattleGfGeometryBatch batch)
        {
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                if (AcceptedReference(bank, buffer, start, attempt) is { } reference)
                {
                    packets.Add(reference);
                }
            }
        }

        internal ImmutableArray<NativeBattlePrimitiveReference> Capture() => packets.ToImmutableArray();
    }

    private static NativeBattlePrimitiveReference? AcceptedReference(string bank, byte buffer, int start, NativeBattleGfGeometryAttempt attempt)
    {
        return attempt.Primitive is null ? null : new(bank, buffer, checked(start + attempt.ByteOffsetRelative));
    }
}

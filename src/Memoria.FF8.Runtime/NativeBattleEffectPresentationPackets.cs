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
            if (attempt.Primitive is not null)
            {
                packets = packets.Add(new(bank, buffer, checked(start + attempt.ByteOffsetRelative)));
            }
        }
        return packets;
    }
}

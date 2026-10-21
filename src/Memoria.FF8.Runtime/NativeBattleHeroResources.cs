using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHeroResources(ushort Effect, NativeBattleGfAnimationSource Main, NativeBattleGfAnimationSource Orbit, ImmutableArray<byte> Fan, uint Sound)
{
    public void Validate()
    {
        NativeBattleHeroProfile profile = NativeBattleHeroProfile.ForEffect(Effect);
        RequireSource(Main, profile.MainSource, 0x888, 4, 40);
        RequireSource(Orbit, profile.OrbitSource, 0x7BC, 3, 18);
        if (Fan.IsDefault || Fan.Length != 0x2B8 || BinaryPrimitives.ReadInt32LittleEndian(Fan.AsSpan().Slice(4)) != 28 || Sound != NativeBattleHeroProfile.Sound)
        {
            throw new InvalidDataException("The Hero fan requires its complete original geometry resource.");
        }
    }

    private static void RequireSource(NativeBattleGfAnimationSource source, uint address, int length, int nodes, int duration)
    {
        if (source is null || source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("The Hero animation requires its complete original source and address.");
        }
        int directory = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
        if (directory < 0 || directory > length - 8 || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(directory)) != nodes || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(directory + 4)) != duration)
        {
            throw new InvalidDataException("The Hero animation directory has incompatible nodes or duration.");
        }
    }
}

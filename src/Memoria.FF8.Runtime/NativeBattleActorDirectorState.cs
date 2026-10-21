using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleActorDirectorState([property: JsonRequired] ushort Effect, [property: JsonRequired] ImmutableArray<byte> Shared84, [property: JsonRequired] NativeBattleCuragaState Generic)
{
    public int? PrimaryDrawCursor { get; init; }
    public int? SecondaryDrawCursor { get; init; }

    public static NativeBattleActorDirectorState FromColdProcess(ushort effect)
    {
        _ = NativeBattleActorDirectorProfile.ForEffect(effect);
        return new(effect, new byte[84].ToImmutableArray(), new(0, null, null, 0, null) { Counter26D76C0 = 0, Counter26D82E8 = 0, Counter26D82D8 = 0, Counter26D75C4 = 0, PassCount26D82EA = 0, PassCount26D82DA = 0 });
    }

    public void Validate()
    {
        _ = NativeBattleActorDirectorProfile.ForEffect(Effect);
        if (Shared84.IsDefault || Shared84.Length != 84 || Generic is null)
        {
            throw new InvalidDataException("The actor director requires its complete retained shared bytes and node workspace.");
        }
        Generic.CopiedCamera?.Validate();
    }

    public short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Shared84.AsSpan().Slice(offset, 2));
    public int Dword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Shared84.AsSpan().Slice(offset, 4));

    public NativeBattleActorDirectorState WithWord(int offset, int value)
    {
        byte[] bytes = Shared84.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), unchecked((short)value));
        return this with { Shared84 = bytes.ToImmutableArray() };
    }

    public NativeBattleActorDirectorState WithDword(int offset, int value)
    {
        byte[] bytes = Shared84.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        return this with { Shared84 = bytes.ToImmutableArray() };
    }
}

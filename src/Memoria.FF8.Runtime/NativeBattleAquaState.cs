using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAquaState([property: JsonRequired] ushort Effect, [property: JsonRequired] ImmutableArray<byte> Shared84, [property: JsonRequired] ImmutableArray<byte>? WaveMesh)
{
    public int? Source { get; init; }
    public int? PrimaryDrawCursor { get; init; }
    public int? SecondaryDrawCursor { get; init; }
    public int PrimaryBuffer { get; init; }
    public int AlternateBuffer { get; init; }
    public int PrimaryLimit { get; init; }
    public int AlternateLimit { get; init; }
    public int BufferEnd { get; init; }
    public int ResetCounter { get; init; }
    public int ResetWaveCounter { get; init; }

    public static NativeBattleAquaState FromColdProcess(ushort effect, ImmutableArray<byte>? waveMesh = null)
    {
        NativeBattleAquaState state = new(effect, new byte[84].ToImmutableArray(), waveMesh);
        state.Validate();
        return state;
    }

    public void Validate()
    {
        _ = NativeBattleAquaProfile.ForEffect(Effect);
        if (Shared84.IsDefault || Shared84.Length != 84 || WaveMesh is { } mesh && (mesh.IsDefault || mesh.Length != 10600))
        {
            throw new InvalidDataException("The Aqua module requires its complete shared state and retained wave mesh.");
        }
    }

    public short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Shared84.AsSpan().Slice(offset, 2));
    public int Dword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Shared84.AsSpan().Slice(offset, 4));

    public NativeBattleAquaState WithWord(int offset, int value)
    {
        byte[] bytes = Shared84.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), unchecked((short)value));
        return this with { Shared84 = bytes.ToImmutableArray() };
    }

    public NativeBattleAquaState WithDword(int offset, int value)
    {
        byte[] bytes = Shared84.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        return this with { Shared84 = bytes.ToImmutableArray() };
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAquaState? Aqua55
    {
        get => effectState.Aqua55;
        init => effectState = effectState with { Aqua55 = value };
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAquaState? Aqua72
    {
        get => effectState.Aqua72;
        init => effectState = effectState with { Aqua72 = value };
    }
}

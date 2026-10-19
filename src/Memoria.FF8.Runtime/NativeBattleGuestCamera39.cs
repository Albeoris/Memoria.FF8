using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSharedCustomMatrix([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known)
{
    public void Validate()
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != 32 || Known.Length != 32)
        {
            throw new InvalidDataException("Native custom matrix requires its retained physical bytes and knowledge mask.");
        }
    }

    public NativeBattleActionMatrix ReadMatrix50D790()
    {
        Validate();
        if (Known.Take(18).Any(value => !value) || Known.Skip(20).Any(value => !value))
        {
            throw new NotSupportedException("Native camera custom matrix requires its actual rotation and translation fields.");
        }
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(index * 2, 2))).ToImmutableArray();
        return new(rotation, BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(20)), BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(24)), BinaryPrimitives.ReadInt32LittleEndian(Bytes.AsSpan().Slice(28)), 4096);
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSharedCustomMatrix? SharedCustomMatrix { get; init; }
}

public static class NativeBattleGuestCamera39
{
    public static NativeBattleSharedCustomMatrix CaptureMode5(NativeBattleWaterExecutionFrame frame)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0xF1FF) != 0x5039)
        {
            throw new NotSupportedException("Native guest custom camera capture requires its actual mode-five command.");
        }
        (byte[] bytes, bool[] known) = frame.Core.ReadSnapshot(0x2797968, 32);
        for (int lane = 0; lane < 3; lane++)
        {
            short position = BinaryPrimitives.ReadInt16LittleEndian(ReadSlot(frame, 0x94 + lane * 2, 2));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20 + lane * 4, 4), position);
            Array.Fill(known, true, 20 + lane * 4, 4);
        }
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}

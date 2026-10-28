using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDarkMist13Resources(ImmutableArray<byte> Swirl, ImmutableArray<byte> Ring, ImmutableArray<byte> Cloud, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<NativeBattleVector> StreamVectors, NativeBattleEffectHeapAllocation Camera, uint Sound);

public sealed record NativeBattleDarkMist13State([property: JsonRequired] ImmutableArray<byte>? Swirl, [property: JsonRequired] NativeBattleEffectArena ActorWork, [property: JsonRequired] short OverlayPhase, [property: JsonRequired] short OverlayAmplitude)
{
    public static NativeBattleDarkMist13State FromColdProcess() => new(null, NativeBattleEffectArena.Clear(), 0, 0);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDarkMist13State? DarkMist13
    {
        get => effectState.DarkMist13;
        init => effectState = effectState with { DarkMist13 = value };
    }
}

public static partial class NativeBattleDarkMist13
{
    public static ImmutableArray<byte> Warp8BE7C0(ImmutableArray<byte> ring, ImmutableArray<byte> mesh, int angle)
    {
        byte[] output = mesh.ToArray();
        int count = BinaryPrimitives.ReadInt32LittleEndian(ring.AsSpan().Slice(4));
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> vertex = ring.AsSpan().Slice(8 + index * 8, 8);
            short x = BinaryPrimitives.ReadInt16LittleEndian(vertex);
            short y = BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(2));
            int phase = unchecked(BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(4)) + angle) & 4095;
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(8 + index * 8), unchecked((short)(x + Math.Abs((int)x) * NativeBattleCameraMath.Cosine(phase) / 4096)));
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(10 + index * 8), unchecked((short)(y + Math.Abs((int)y) * NativeBattleCameraMath.Sine(phase) / 4096)));
        }
        return output.ToImmutableArray();
    }

    public static ImmutableArray<byte> Scroll8BE6B0(ImmutableArray<byte> mesh)
    {
        byte[] output = mesh.ToArray();
        int start = BinaryPrimitives.ReadInt32LittleEndian(output) + 0x1C;
        int count = BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(start));
        for (int index = 0; index < count; index++)
        {
            int position = start + 4 + index * 36;
            int[] offsets = [position + 13, position + 17, position + 21, position + 23];
            bool wrap = offsets.Any(offset => output[offset] < 4);
            foreach (int offset in offsets)
            {
                output[offset] = unchecked((byte)((output[offset] - 4 + (wrap ? 64 : 0)) & 127));
            }
        }
        return output.ToImmutableArray();
    }

    public static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    public static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4));
    public static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    public static NativeBattleModulePoolTask Phase(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    public static NativeBattleModulePoolTask Done(NativeBattleModulePoolTask task) => Phase(task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 1)));
}

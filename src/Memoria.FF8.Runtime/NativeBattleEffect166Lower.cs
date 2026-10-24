using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Lower
{
    public static NativeBattleEffect166Frame Invoke(NativeBattleEffect166Frame frame, uint function, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        if (function != 0x5E5700)
        {
            throw new NotSupportedException("Effect166 direct lower call is unsupported.");
        }
        return inputs.Execute(frame, new NativeBattleEffect166Capture5700(NativeBattleEffect166Capture.Prepare(frame.State.ReadView(0x23C3248, 8))));
    }

    public static NativeBattleEffect166Step Advance(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            0x5E5980 or 0x5E5AB0 or 0x5E5C30 or 0x5E8330 or 0x5E8450 or 0x5E8590 or 0x5E86D0 or 0x5E9250 => AdvanceStatic(frame, address, resources, inputs),
            0x5E5D50 => InitializePath(frame, address, resources, inputs),
            0x5E6030 => AdvanceBeam(frame, address, resources, inputs),
            0x5E7AE0 => AdvanceHistory(frame, address, resources, inputs),
            0x5E7ED0 => AdvanceRibbon(frame, address, resources, inputs),
            0x5E8870 => AdvanceSprites(frame, address, resources, inputs),
            0x5E8A80 => AdvanceDebris(frame, address, resources, inputs),
            0x5E8DA0 => AdvanceLift(frame, address, inputs),
            0x5E8EF0 => AdvanceTurn(frame, address, inputs),
            _ => throw new NotSupportedException("Effect166 callback leaves its selected lower graph.")
        };
    }

    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static uint Path(NativeBattleModulePoolTask task) => checked(0x23C1428u + (uint)(Signed(task, 14) * 820));
    private static bool Paused(NativeBattleEffect166Frame frame, NativeBattleEffect166Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    private static int Roll(ref NativeBattleEffect166Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }
    private static NativeBattleEffect166Frame Deliver(NativeBattleEffect166Frame frame, NativeBattleGfGeometryBatch batch, NativeBattleEffect166Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect166DrawBatch(batch, 0x23C3274));
    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset = 0)
    {
        (byte[] data, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new([.. data], [.. known]);
    }
    private static NativeBattleEffectArena Copy(NativeBattleEffectArena target, int offset, NativeBattleEffectArena source, int sourceOffset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, sourceOffset, target, offset, length);
    private static NativeBattleEffect166Step Clock(NativeBattleEffect166Frame frame, uint address, NativeBattleModulePoolTask task, int end)
    {
        short age = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)age))), age >= end ? 2 : 0);
    }
}

using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect174Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect174Lower
{
    private const uint L174Cursor = 0x2397270;
    private const uint L174Records = 0x2394240;

    public static NativeBattleEffect174Step Advance(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs) => Read(frame, address).Callback switch
    {
        0x5D4CB0 or 0x5D6AA0 => AdvanceParticles(frame, address, resources, inputs),
        0x5D4ED0 => AdvanceHistory(frame, address, resources, inputs),
        0x5D51C0 or 0x5D52F0 or 0x5D5560 or 0x5D5690 or 0x5D7090 => AdvanceMesh(frame, address, resources, inputs),
        0x5D5430 => AdvanceCameraSprite(frame, address, resources, inputs),
        0x5D64E0 => AdvanceBody(frame, address, inputs),
        0x5D6DA0 or 0x5D6F50 or 0x5D7220 => AdvanceActorServices(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect174 requires its selected lower callback.")
    };

    private static NativeBattleEffect174Step L174Age(NativeBattleEffect174Frame frame, uint address, int terminal, NativeBattleEffect174Inputs inputs)
    {
        if (L174Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short next = unchecked((short)(task.ReadWord(12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next >= terminal ? 2 : 0);
    }

    private static bool L174Paused(NativeBattleEffect174Frame frame, NativeBattleEffect174Inputs inputs) => (inputs.ReadFlags(frame) & 0x201) != 0;
    private static uint L174U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short L174S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena L174Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena L174Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleVector L174Vector(NativeBattleEffectArena arena) => new(L174S(arena, 0), L174S(arena, 2), L174S(arena, 4));
    private static int L174Random(ref NativeBattleEffect174Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static NativeBattleEffect174Frame L174WriteModel(NativeBattleEffect174Frame frame, int actor, int offset, NativeBattleEffectArena data, int length, NativeBattleEffect174Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, offset, data, length));
}

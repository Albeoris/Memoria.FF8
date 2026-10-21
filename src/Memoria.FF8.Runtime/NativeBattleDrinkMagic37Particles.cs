using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDrinkMagic37Particles
{
    public static NativeBattleDrawStock15Jitter InitializeTrajectory(NativeBattleModulePoolTask task, ImmutableArray<byte> capturedAnchor8, NativeCrtRandomState random)
    {
        if (task.Callback != 0x85E930 || capturedAnchor8.IsDefault || capturedAnchor8.Length != 8)
        {
            throw new InvalidDataException("Drink Magic trajectory requires its actual task and retained eight-byte caster anchor.");
        }
        random = random.Draw(out ushort first);
        task = task.WriteDword(0x4C, 0x15D92F4).WriteWord(0x52, 5).WriteWord(0x54, 0).WriteWord(0x126, (ushort)(first & 0xFFF)).WriteDword(0x88, task.ReadDword(0x1C)).WriteDword(0x8C, task.ReadDword(0x20));
        task = task.WriteDword(0x80, BinaryPrimitives.ReadUInt32LittleEndian(capturedAnchor8.AsSpan())).WriteDword(0x84, BinaryPrimitives.ReadUInt32LittleEndian(capturedAnchor8.AsSpan().Slice(4)));
        NativeBattleVector direction = new(unchecked((short)(Word(task, 0x80) - Word(task, 0x88))), unchecked((short)(Word(task, 0x82) - Word(task, 0x8A))), unchecked((short)(Word(task, 0x84) - Word(task, 0x8C))));
        NativeBattleActionMatrix basis = NativeBattleDrawStock15Particles.LookBasis8DDA50(direction);
        random = random.Draw(out ushort second);
        short angle = (short)((second & 0x3FF) + ((task.ReadByte(0x120) & 1) != 0 ? 0x400 : 0x800));
        NativeBattleDrawStock15Rotate rotate = new(basis, 'Z', angle) { Padding18 = NativeBattleDrawStock15Particles.ReadPadding(task, 0x72) };
        basis = NativeBattleCuragaMatrices.RotateZ8DD960(basis, angle);
        for (int lane = 0; lane < 9; lane++)
        {
            task = task.WriteWord(0x60 + lane * 2, unchecked((ushort)basis.Rotation[lane]));
        }
        return new(task, random, [new NativeBattleDrawStock15LookBasis(direction, new(0, 4096, 0)), rotate]);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset)
    {
        return unchecked((short)task.ReadWord(offset));
    }
}

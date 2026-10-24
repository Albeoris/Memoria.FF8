using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect80Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Particles
{
    public static NativeBattleEffect80Step Advance7AE7D0(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        frame = AdvanceSpritePhase(frame, address, false);
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteDword(0x1C, task.ReadDword(0x5C)).WriteDword(0x20, task.ReadDword(0x60)));
        return Finish(NativeBattleEffect80Sprite.DrawTask(frame, address, resources, inputs), address);
    }

    public static NativeBattleEffect80Step Advance7AF030(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        frame = AdvanceSpritePhase(frame, address, true);
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x42, unchecked((ushort)(task.ReadWord(0x42) + 0x40)));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, unchecked((short)task.ReadWord(0x44)));
        matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, unchecked((short)task.ReadWord(0x40)));
        matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, unchecked((short)task.ReadWord(0x42)));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(unchecked((short)task.ReadWord(0x9E)), 0, 0));
        uint target = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0x157B2E0, 4).AsSpan());
        task = task.WriteWord(0x1C, unchecked((ushort)(frame.State.ReadWord(target + 0x10) + offset.X))).WriteWord(0x1E, unchecked((ushort)(frame.State.ReadWord(target + 0x12) + offset.Y))).WriteWord(0x20, unchecked((ushort)(frame.State.ReadWord(target + 0x14) + offset.Z)));
        frame = Write(frame, address, task);
        return Finish(NativeBattleEffect80Sprite.DrawTask(frame, address, resources, inputs), address);
    }

    private static NativeBattleEffect80Frame AdvanceSpritePhase(NativeBattleEffect80Frame frame, uint address, bool orbit)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                task = task.WriteDword(0x4C, orbit ? 0x157B2ECu : 0x157B4E8u).WriteWord(0x52, orbit ? (ushort)18 : (ushort)11);
                if (orbit)
                {
                    NativeCrtRandomState random = frame.Random;
                    for (int index = 0; index < 3; index++)
                    {
                        random = random.Draw(out ushort value);
                        task = task.WriteWord(0x40 + index * 2, (ushort)(value & 0xFFF));
                    }
                    random = random.Draw(out ushort radius);
                    task = task.WriteWord(0x9E, (ushort)((radius & 0xFF) + 0x180));
                    frame = frame with { Random = random };
                }
                else
                {
                    task = task.WriteWord(0x56, 0xFF80);
                }
                return NextPhase(Write(frame, address, task), address);
            case 1:
                short next = unchecked((short)(task.ReadWord(0x50) + 1));
                short last = unchecked((short)task.ReadWord(0x52));
                task = task.WriteWord(0x50, unchecked((ushort)next));
                if (next > last)
                {
                    task = task.WriteWord(0x50, unchecked((ushort)last)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                    return NextPhase(Write(frame, address, task), address);
                }
                return Write(frame, address, task);
            case 2:
                return frame;
            default:
                throw new InvalidDataException("Effect80 particle has an unsupported native phase.");
        }
    }
}

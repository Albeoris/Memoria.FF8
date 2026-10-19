using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWendigoController
{
    private sealed partial class Execution
    {
        private bool PassStep()
        {
            int offset = Word(Ball + 0xD6) * 10;
            short Field(int member) => BinaryPrimitives.ReadInt16LittleEndian(resources.PassScript.AsSpan(offset + member, 2));
            int kicker = Field(8);
            if (kicker is >= 1 and <= 4)
            {
                int actor = Word(Ball + (kicker is 1 or 3 ? 0xDAu : 0xDCu));
                Sequence(actor, kicker <= 2 ? (byte)4 : (byte)6);
                if (kicker <= 2)
                {
                    Model(Model(actor) with { Rotation14 = kicker == 1 ? unchecked((short)0xFC00) : (short)0x400 });
                }
            }
            switch (Field(0))
            {
                case 0:
                    Dword(Ball + 0xAC, Dword(Ball + 0xB4));
                    Dword(Ball + 0xB0, Dword(Ball + 0xB8));
                    break;
                case 2:
                    Dword(Ball + 0xAC, Dword(Ball + 0xBC));
                    Dword(Ball + 0xB0, Dword(Ball + 0xC0));
                    break;
                case 3:
                case 4:
                    Word(Ball + 0xAC, (Word(Ball + 0xB4) + Word(Ball + 0xBC)) / 2);
                    Word(Ball + 0xAE, Field(6));
                    Word(Ball + 0xB0, (Word(Ball + 0xB8) + Word(Ball + 0xC0)) / 2);
                    break;
                case 127:
                    return true;
            }
            short duration = Field(2);
            Word(Ball + 0xD2, duration);
            for (uint axis = 0; axis < 6; axis += 2)
            {
                Word(Ball + 0x94 + axis, (Word(Ball + 0xAC + axis) - Word(Ball + 0x7C + axis)) / duration);
            }
            Word(Ball + 0xD6, Word(Ball + 0xD6) + 1);
            return false;
        }
        private void KickEffects(uint address)
        {
            int age = Signed(Read(address), 0x24);
            if (age > 76)
            {
                return;
            }
            short Field(int member) => BinaryPrimitives.ReadInt16LittleEndian(resources.KickScript.AsSpan(age * 14 + member, 2));
            if (Field(4) == 1)
            {
                Word(Ball + 0xDE, 1);
            }
            Word(Ball + 0xA8, Field(6));
            if (Field(8) == 0)
            {
                return;
            }
            NativeBattleGfAnimationSource source = resources.KickPrim ?? throw new NotSupportedException("Native Shoot requires its kick animation layout.");
            uint child = SpawnPrim(address, 0x846350, source, 0x84, 1, 0);
            NativeBattleVector p = Position(Ball + 0x7C);
            NativeBattleModulePoolTask task = Vector(Read(child), 0x1C, p).WriteWord(0x22, unchecked((ushort)Word(Ball + 0x82))).WriteWord(0x64, unchecked((ushort)Field(10)));
            NativeBattleActionMatrix placement = Identity() with { X = p.X, Y = p.Y, Z = p.Z };
            placement = NativeBattleCuragaMatrices.RotateX8DD7E0(placement, Signed(task, 0x60));
            placement = NativeBattleCuragaMatrices.RotateZ8DD960(placement, Field(10));
            placement = NativeBattlePoseMatrices.Scale56BEF0(placement, Field(8), Field(8), Field(8));
            Store(child, TaskMatrix(task, 0x30, placement));
            uint burst = Append(3, 0x844C50, TaskSize, address);
            Store(burst, TaskMatrix(Vector(Read(burst), 0x1C, p).WriteWord(0x22, task.ReadWord(0x22)).WriteWord(0x1FA, unchecked((ushort)Field(12))), 0x58, placement));
        }
    }
}

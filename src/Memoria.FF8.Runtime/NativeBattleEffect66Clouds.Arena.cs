using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Clouds
{
    private static NativeBattleEffect66Step AdvanceArenaSparks(NativeBattleEffect66Frame frame, uint callback, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        bool burst = callback == 0x627890;
        uint pool = frame.State.ReadDword(burst ? InitializedBase : InitializedBase + 4);
        int slots = burst ? 400 : 800;
        (frame, uint outerAddress) = Allocate(frame, 72);
        NativeBattleEffectArena outer = Context(frame, outerAddress, 72);
        int age = unchecked((short)Read(frame, task).ReadWord(12));
        if (burst && !Paused(frame, inputs) && age <= 308)
        {
            outer = NativeBattleEffectNodeContextMemory.Copy(ReadBytes(frame, task + 0x10, 8), 0, outer, 0, 8);
            for (int birth = 0; birth < 8; birth++)
            {
                uint address = Free(frame, pool, 400);
                if (address == 0)
                {
                    break;
                }
                NativeBattleEffectArena row = Word(Put(ReadBytes(frame, address, 24), 0, 1), 4, 0);
                frame = Random(frame, out int random);
                row = Word(row, 6, random % 3072 + 256);
                row = NativeBattleEffectNodeContextMemory.Copy(outer, 0, row, 8, 8);
                frame = Random(frame, out random);
                row = Word(row, 8, S16(row, 8) + random % 10000 - 5000);
                frame = Random(frame, out random);
                row = Word(row, 10, -2000 - random % 1800);
                frame = Random(frame, out random);
                row = Word(row, 12, S16(row, 12) + random % 10000 - 5000);
                frame = Random(frame, out random);
                int x = random % 1536 + 768;
                frame = Random(frame, out random);
                int y = random % 1280 + 1024;
                frame = Random(frame, out random);
                int z = random % 1792 + 512;
                var unit = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                outer = Put(Put(Put(outer, 0x28, unit.X), 0x2C, unit.Y), 0x30, unit.Z);
                frame = Random(frame, out random);
                int speed = random % 55 + 10;
                row = Word(Word(Word(row, 16, unchecked(unit.X * speed) >> 12), 18, unchecked(unit.Y * speed) >> 12), 20, unchecked(unit.Z * speed) >> 12);
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        outer = NativeBattleEffectNodeContextMemory.Copy(PackedArena(view), 0, outer, 8, 32);
        frame = frame with { Memory = NativeBattleLightBackTransform.LoadBone(frame.Memory, view) };
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xDE477Cu)).Write(0x24, BitConverter.GetBytes((short)8));
        for (int index = 0; index < slots; index++)
        {
            uint address = checked(pool + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, address, 24);
            if ((U32(row, 0) & 1) == 0)
            {
                continue;
            }
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, Point(row, 8), OptionalWord(row, 14));
            frame = frame with { Memory = NativeBattleLightBackTransform.Transform(Publish(frame, registers).Memory, 0) };
            registers = Registers(frame);
            short scale = S16(row, 6);
            NativeBattleActionMatrix matrix = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value, 4096);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, 0) };
            frame = Publish(frame, registers);
            sprite = Word(sprite, 4, burst ? S16(row, 4) >> 2 : S16(row, 4));
            frame = PublishContext(PublishContext(frame, outerAddress, outer, 72), spriteAddress, sprite, 180);
            frame = NativeBattleEffect66Drawing.DrawSprite(frame, spriteAddress, 2, 0x1D8E054, resources, inputs, NativeBattleEffect66VisualIdentity.Point(frame, address, U32(sprite, 0)));
            sprite = Context(frame, spriteAddress, 180);
            if (!Paused(frame, inputs))
            {
                row = ReadBytes(frame, address, 24);
                if (S16(sprite, 0x28) < 0)
                {
                    row = Put(row, 0, 0);
                }
                else
                {
                    row = Word(row, 4, S16(row, 4) + 1);
                    if (burst)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            row = Word(row, 8 + axis * 2, S16(row, 8 + axis * 2) + S16(row, 16 + axis * 2));
                        }
                    }
                    else
                    {
                        row = Word(row, 10, S16(row, 10) + S16(row, 18));
                    }
                }
                frame = WriteBytes(frame, address, row, 24);
            }
        }
        frame = PublishContext(frame, outerAddress, outer, 72);
        frame = Release(Release(frame, 180), 72);
        if (burst)
        {
            return FinishTimed(frame, task, 308, pool, 400, 1, inputs);
        }
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int next = unchecked((short)(Read(frame, task).ReadWord(12) + 1));
        frame = Write(frame, task, Read(frame, task).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next >= 152 ? 2 : 0);
    }
}

using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Step AdvanceParticle664D20(NativeBattleEffect314Frame frame, uint address, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        uint cursor = Read(frame, address).ReadDword(0x18);
        uint boneRow = Read(frame, address).ReadDword(0x14);
        while (true)
        {
            ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(cursor, 2));
            cursor += 2;
            switch (opcode)
            {
                case 0x1002:
                case 0x1003:
                case 0x1012:
                {
                    int target = opcode == 0x1012 ? 0x24 : 0x1C;
                    for (int lane = 0; lane < 3; lane++)
                    {
                        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(cursor, 2));
                        cursor += 2;
                        NativeBattleModulePoolTask task = Read(frame, address);
                        if (opcode == 0x1003)
                        {
                            value = unchecked((ushort)(value + task.ReadWord(target + lane * 2)));
                        }
                        frame = Write(frame, address, task.WriteWord(target + lane * 2, value));
                    }
                    break;
                }
                case 0x1004:
                {
                    NativeBattleModulePoolTask task = Read(frame, address);
                    NativeBattleEffectArena row = ReadBytes(frame, boneRow, 16);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        row = row.Write(lane * 2, BitConverter.GetBytes(unchecked((ushort)(U16(row, lane * 2) + task.ReadWord(0x1C + lane * 2)))));
                    }
                    frame = WriteBytes(frame, boneRow, row, 16);
                    uint model = task.ReadDword(0xC);
                    int bone = unchecked((int)(boneRow - task.ReadDword(0x10))) >> 4;
                    uint matrix = inputs.ResolveBoneRecord(frame, model, bone);
                    NativeBattleEffectArena translation = inputs.ReadArena(frame, matrix + 20, 12);
                    NativeBattleActionMatrix world = inputs.ReadModel(frame, model).GeometryWorldMatrix ?? throw new NotSupportedException("Effect314 particle requires its current caster world matrix.");
                    (int _, int y, int _) = NativeBattleGfMatrices.RotateLong56C600(world, I32(translation, 0), I32(translation, 4), I32(translation, 8));
                    if (unchecked(y + I16(row, 2)) > Signed(task, 0x2C))
                    {
                        short velocity = Signed(task, 0x1E);
                        task = task.WriteWord(0x1E, unchecked((ushort)((velocity >> 2) - velocity)));
                        bool first = task.ReadByte(0x2E) == 0;
                        frame = Write(frame, address, first ? task.WriteByte(0x2E, 1) : task);
                        if (first)
                        {
                            frame = EmitParticleBurst(frame, inputs, model, bone, row);
                        }
                    }
                    break;
                }
                case 0x1014:
                {
                    NativeBattleModulePoolTask task = Read(frame, address);
                    NativeBattleEffectArena row = ReadBytes(frame, boneRow, 16);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        row = row.Write(8 + lane * 2, BitConverter.GetBytes(unchecked((ushort)(U16(row, 8 + lane * 2) + task.ReadWord(0x24 + lane * 2)))));
                    }
                    frame = WriteBytes(frame, boneRow, row, 16);
                    break;
                }
                case 0x10FF:
                    return new(frame, 2);
                default:
                    throw new NotSupportedException("Effect314 particle reached an unsupported original command.");
            }
        }
    }

    private static NativeBattleEffect314Frame EmitParticleBurst(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint model, int bone, NativeBattleEffectArena row)
    {
        uint main = frame.State.ReadDword(WorkGlobal) + MainOffset;
        ushort Draw()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        for (int index = 0; index < 16; index++)
        {
            (frame, uint spark) = AllocateSpark6643D0(frame, main);
            if (spark == 0)
            {
                break;
            }
            int angle = Draw() & 0xFFF;
            frame = inputs.Execute(frame, new NativeBattleEffect314BonePoint502170(model, bone, angle, spark));
            NativeBattleEffectArena record = ReadBytes(frame, spark, 16);
            record = record.Write(0, BitConverter.GetBytes(unchecked((short)(I16(record, 0) + I16(row, 0) + (Draw() & 0x7FF) - 0x400))));
            record = record.Write(2, BitConverter.GetBytes(unchecked((short)(I16(record, 2) + I16(row, 2)))));
            record = record.Write(4, BitConverter.GetBytes(unchecked((short)(I16(record, 4) + I16(row, 4) + (Draw() & 0x7FF) - 0x400))));
            record = record.Write(6, BitConverter.GetBytes((short)12)).Write(8, BitConverter.GetBytes((short)((Draw() & 0x7FF) + 0xC00)));
            record = record.Write(12, [unchecked((byte)((Draw() & 7) - 4))]);
            record = record.Write(14, [unchecked((byte)((Draw() & 7) - 4))]);
            record = record.Write(13, [unchecked((byte)(-8 - (Draw() & 7)))]).Write(15, [0]);
            frame = WriteBytes(frame, spark, record, 16);
        }
        return frame;
    }

    private static short I16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static int I32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
}

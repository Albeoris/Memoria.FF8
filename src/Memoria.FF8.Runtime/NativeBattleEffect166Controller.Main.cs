using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect166Controller
{
    public static NativeBattleEffect166Step AdvanceMain5E4F60(NativeBattleEffect166Frame frame, uint address, NativeBattleEffect166Resources resources, NativeBattleEffect166Inputs inputs)
    {
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadWorld(frame, caster).ReadMatrix(), inputs.ReadCurrentBoneMatrix32(frame, caster, 37).ReadMatrix());
        NativeBattleVector offset = Vector(NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(CenterOffsetAddress, 8)));
        NativeBattleVector anchor = NativeBattleGfMatrices.RotateShort56C4F0(composed, offset);
        anchor = new(unchecked((short)(anchor.X + composed.X)), unchecked((short)(anchor.Y + composed.Y)), unchecked((short)(anchor.Z + composed.Z)));
        frame = WriteBytes(frame, AnchorAddress, PutPoint(ReadBytes(frame, AnchorAddress, 8), 0, anchor), 8);
        NativeBattleVector target = Vector(ReadBytes(frame, TargetPointAddress, 8));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(target.X - anchor.X, target.Y - anchor.Y, target.Z - anchor.Z);
        byte[] alignment = resources.ReadStatic(AlignmentAddress, 12);
        NativeBattleModuleAxisAngle axis = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(alignment), BinaryPrimitives.ReadInt32LittleEndian(alignment.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(alignment.AsSpan(8)), direction.X, direction.Y, direction.Z);
        NativeBattleEffectArena matrix = ReadBytes(frame, MatrixAddress, 32);
        var rotation = NativeBattleModuleAlignment.Rotation5714F0(axis.Angle, axis.Axis);
        for (int index = 0; index < 9; index++)
        {
            matrix = matrix.Write(index * 2, BitConverter.GetBytes(rotation[index]));
        }
        matrix = matrix.Write(20, BitConverter.GetBytes((int)anchor.X)).Write(24, BitConverter.GetBytes((int)anchor.Y)).Write(28, BitConverter.GetBytes((int)anchor.Z));
        frame = WriteBytes(frame, MatrixAddress, matrix, 32);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect166TargetCenter5020A0(frame.Effect.Parameters!.TargetMask7A, TargetPointAddress));
            frame = frame with { State = frame.State.WriteDword(CueGlobal, 0) };
        }
        if (age == 43)
        {
            frame = NativeBattleEffect166Lower.Invoke(frame, 0x5E5700, resources, inputs);
        }
        if (age == 1)
        {
            frame = Birth(frame, 0x5E5980, task => task.WriteWord(0x1C, 0x6800).WriteWord(0x1E, 0x14CC));
        }
        if (age == 8)
        {
            (frame, int firstAngle) = Random(frame, 4096);
            (frame, int velocity) = Random(frame, 40);
            frame = Birth(frame, 0x5E5AB0, task => task.WriteWord(0x18, (ushort)firstAngle).WriteWord(0x1A, (ushort)(velocity + 20)).WriteWord(0x1C, 8192));
            (frame, int secondAngle) = Random(frame, 4096);
            frame = Birth(frame, 0x5E5AB0, task => task.WriteWord(0x18, (ushort)secondAngle).WriteWord(0x1A, unchecked((ushort)-firstAngle)).WriteWord(0x1C, 8192));
        }
        if (age >= 6 && age <= 30 && age % 6 == 2)
        {
            frame = Birth(frame, 0x5E5C30, task => task.WriteWord(0x1C, 0x340).WriteWord(0x1E, 0x340));
        }
        if (age == 8)
        {
            for (int index = 0; index < 6; index++)
            {
                int selected = index;
                frame = Birth(frame, 0x5E5D50, task => task.WriteWord(14, (ushort)selected));
            }
        }
        if (age == 44)
        {
            frame = BirthAtCenter(frame, 0x5E8450, task => task.WriteWord(0x1C, 0).WriteWord(0x1E, 512));
            frame = BirthAtCenter(frame, 0x5E8330, task => task.WriteWord(0x1C, 0).WriteWord(0x1E, 512));
        }
        if (age == 54)
        {
            frame = BirthAtCenter(frame, 0x5E8590, task => task.WriteWord(0x1C, 4096).WriteWord(0x20, 0x555).WriteWord(0x22, 0x555));
            for (int index = 0; index < 16; index++)
            {
                (frame, int delay) = Random(frame, 4);
                (frame, int x) = Random(frame, 5600);
                (frame, int z) = Random(frame, 2800);
                (frame, int angle) = Random(frame, 4096);
                (frame, int scale) = Random(frame, 6144);
                (frame, int speed) = Random(frame, 10);
                NativeBattleEffectArena point = ReadBytes(frame, TargetPointAddress, 8);
                point = point.Write(0, BitConverter.GetBytes(unchecked((short)(Vector(point).X + x - 2800))));
                point = point.Write(4, BitConverter.GetBytes(unchecked((short)(Vector(point).Z + z - 1400))));
                int bornDelay = delay + index;
                frame = Birth(frame, 0x5E86D0, task => CopyTask(task, 0x10, point, 8).WriteWord(14, (ushort)bornDelay).WriteWord(0x18, (ushort)angle).WriteWord(0x1A, (ushort)(angle & 1)).WriteWord(0x1C, (ushort)(scale + 7168)).WriteWord(0x20, 0x4000).WriteWord(0x22, (ushort)(speed + 6)));
            }
        }
        if (age == 55)
        {
            frame = BirthAtCenter(frame, 0x5E8870, task => task);
            frame = BirthAtCenter(frame, 0x5E8A80, task => task);
        }
        if (age == 6 || age == 51)
        {
            frame = inputs.Execute(frame, age == 6 ? new NativeBattleEffect166Flash5712C0(2, 1, 10, 255) : new NativeBattleEffect166Flash5712C0(0, 2, 4, 255));
        }
        if (age == 44 || age == 54)
        {
            for (int actor = 0; actor < 3; actor++)
            {
                if ((inputs.ReadModel(frame, actor).Flags0 & 2) == 0)
                {
                    continue;
                }
                int contact = ContactIndex(frame, actor, inputs);
                if (age == 44)
                {
                    int selectedActor = actor;
                    frame = Birth(frame, 0x5E8DA0, task => task.WriteWord(14, (ushort)selectedActor).WriteWord(0x1A, unchecked((ushort)contact)).WriteWord(0x20, (ushort)(contact >= 0 ? 500 : 250)));
                }
                else
                {
                    (frame, int scale) = Random(frame, 3);
                    (frame, int displacement) = Random(frame, 1500);
                    int y = -3000 - displacement;
                    if (contact < 0)
                    {
                        y >>= 1;
                    }
                    int selectedActor = actor;
                    frame = Birth(frame, 0x5E8EF0, task => task.WriteWord(14, (ushort)selectedActor).WriteWord(0x18, (ushort)((scale + 1) << 12)).WriteWord(0x1A, unchecked((ushort)contact)).WriteWord(0x20, unchecked((ushort)y)));
                }
            }
        }
        if (age < 8)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect166Stage5713E0(age << 8, 0));
        }
        else if (age >= 27 && age < 35)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect166 requires its current primitive process.");
            NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect166 requires its current far color.");
            far = far with { FarRed = 48u << 4, FarGreen = 16u << 4, FarBlue = 16u << 4 };
            NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect166 requires current color registers."), 0, (uint)((age - 27) << 9), far, inputs.Colors);
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far }, ScriptedColorRegisters = result.Registers }, State = frame.State.WriteDword(CueGlobal, result.PackedRgb) };
            frame = inputs.Execute(frame, new NativeBattleEffect166Stage5713E0((age - 11) << 7, result.PackedRgb));
        }
        else if (age >= 90)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect166Stage5713E0(((98 - age) * 3) << 7, 0x101030));
        }
        if (age == 1 || age == 48)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect166Sound501330(age == 1 ? 0xD7803Cu : 0xD78040u, 0, 128));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 98 ? 2 : 0);
    }

    private static NativeBattleEffect166Frame Birth(NativeBattleEffect166Frame frame, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect166 requires its selected child allocation.");
        }
        return Write(frame, address, initialize(Read(frame, address).WriteWord(12, 0)));
    }

    private static NativeBattleEffect166Frame BirthAtCenter(NativeBattleEffect166Frame frame, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        NativeBattleEffectArena point = ReadBytes(frame, TargetPointAddress, 8);
        return Birth(frame, callback, task => initialize(CopyTask(task, 0x10, point, 8)));
    }

    private static (NativeBattleEffect166Frame Frame, int Value) Random(NativeBattleEffect166Frame frame, int modulus)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        return (frame with { Random = random }, value % modulus);
    }

    private static int ContactIndex(NativeBattleEffect166Frame frame, int actor, NativeBattleEffect166Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[frame.Effect.Parameters!.HeaderIndex7C];
        int count = header.ReadByte(0x10);
        if (count == 0)
        {
            return -1;
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect166 requires its current contact rows.");
        for (int index = 0; index < count; index++)
        {
            if (packets.ReadContact(first + index).Primary.ActorIndex == actor)
            {
                return index;
            }
        }
        return -1;
    }
}

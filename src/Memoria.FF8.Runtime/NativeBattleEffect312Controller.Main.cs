using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Controller
{
    public static NativeBattleEffect312Step AdvanceMain59CC60(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        uint modelAddress = checked(0x1D972C0u + frame.State.ReadDword(CasterGlobal) * 0x9Cu);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadWorld(frame, modelAddress).ReadMatrix(), inputs.ReadBoneMatrix59D3C0(frame, modelAddress, 10).ReadMatrix());
        NativeBattleVector offset = Vector(NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(CenterOffsetAddress, 6)));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(composed, offset);
        NativeBattleVector anchor = new(unchecked((short)(rotated.X + composed.X)), unchecked((short)(rotated.Y + composed.Y)), unchecked((short)(rotated.Z + composed.Z)));
        frame = WriteBytes(frame, AnchorAddress, PutPoint(ReadBytes(frame, AnchorAddress, 8), 0, anchor), 8);
        NativeBattleVector center = Vector(ReadBytes(frame, TargetPointAddress, 8));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(center.X - anchor.X, center.Y - anchor.Y, center.Z - anchor.Z);
        byte[] alignment = resources.ReadStatic(AlignmentAddress, 12);
        NativeBattleModuleAxisAngle axis = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(alignment), BinaryPrimitives.ReadInt32LittleEndian(alignment.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(alignment.AsSpan(8)), direction.X, direction.Y, direction.Z);
        var rotation = NativeBattleModuleAlignment.Rotation5714F0(axis.Angle, axis.Axis);
        NativeBattleEffectArena matrix = ReadBytes(frame, MatrixAddress, 32);
        for (int index = 0; index < rotation.Length; index++)
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
            NativeBattleEffectArena parameters = inputs.ReadPhysical(frame, frame.State.ReadDword(ParametersGlobal), 8);
            ushort mask = BinaryPrimitives.ReadUInt16LittleEndian(parameters.Read(2, 2));
            NativeBattleEffect312PointStep target = inputs.TargetCenter5020A0(frame, mask, ReadBytes(frame, TargetPointAddress, 8));
            frame = WriteBytes(target.Frame, TargetPointAddress, target.Output8, 8);
            frame = frame with { State = frame.State.WriteDword(CueGlobal, 0) };
        }
        if (age == 43)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect312CaptureStage59D400(TargetPointAddress) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(312, Read(frame, address), 0x59D400) : null });
        }
        if (age == 1)
        {
            frame = BirthMain(frame, 0x59D680, task => task.WriteWord(0x1C, 0x6800).WriteWord(0x1E, 0x14CC));
        }
        if (age == 8)
        {
            (frame, uint first) = SpawnMain(frame, 0x59D7B0);
            (frame, int angle) = Random(frame, 4096);
            frame = Write(frame, first, Read(frame, first).WriteWord(0x18, unchecked((ushort)angle)));
            (frame, int velocity) = Random(frame, 40);
            frame = Write(frame, first, Read(frame, first).WriteWord(0x1A, unchecked((ushort)(velocity + 20))).WriteWord(0x1C, 0x2000));
            (frame, uint second) = SpawnMain(frame, 0x59D7B0);
            (frame, int secondAngle) = Random(frame, 4096);
            ushort firstAngle = Read(frame, first).ReadWord(0x18);
            frame = Write(frame, second, Read(frame, second).WriteWord(0x18, unchecked((ushort)secondAngle)).WriteWord(0x1A, unchecked((ushort)-unchecked((short)firstAngle))).WriteWord(0x1C, 0x2000));
        }
        if (age >= 6 && age <= 30 && age % 6 == 2)
        {
            frame = BirthMain(frame, 0x59D930, task => task.WriteWord(0x1C, 0x340).WriteWord(0x1E, 0x340));
        }
        if (age == 8)
        {
            for (int index = 0; index < 6; index++)
            {
                int selected = index;
                frame = BirthMain(frame, 0x59DA50, task => task.WriteWord(14, (ushort)selected));
            }
        }
        if (age == 44)
        {
            frame = BirthAtCenterMain(frame, 0x5A0150, task => task.WriteWord(0x1C, 0).WriteWord(0x1E, 512));
            frame = BirthAtCenterMain(frame, 0x5A0030, task => task.WriteWord(0x1C, 0).WriteWord(0x1E, 512));
        }
        if (age == 54)
        {
            frame = BirthAtCenterMain(frame, 0x5A0290, task => task.WriteWord(0x1C, 4096).WriteWord(0x20, 0x555).WriteWord(0x22, 0x555));
            for (int index = 0; index < 16; index++)
            {
                (frame, uint born) = SpawnMain(frame, 0x5A03D0);
                (frame, int delay) = Random(frame, 4);
                NativeBattleModulePoolTask child = Read(frame, born).WriteWord(14, unchecked((ushort)(delay + index)));
                child = CopyTask(child, 0x10, ReadBytes(frame, TargetPointAddress, 8), 8);
                frame = Write(frame, born, child);
                (frame, int x) = Random(frame, 5600);
                frame = Write(frame, born, Read(frame, born).WriteWord(0x10, unchecked((ushort)(Signed(Read(frame, born), 0x10) + x - 2800))));
                (frame, int z) = Random(frame, 2800);
                frame = Write(frame, born, Read(frame, born).WriteWord(0x14, unchecked((ushort)(Signed(Read(frame, born), 0x14) + z - 1400))));
                (frame, int angle) = Random(frame, 4096);
                frame = Write(frame, born, Read(frame, born).WriteWord(0x18, unchecked((ushort)angle)).WriteWord(0x1A, unchecked((ushort)(angle & 1))));
                (frame, int scale) = Random(frame, 6144);
                frame = Write(frame, born, Read(frame, born).WriteWord(0x1C, unchecked((ushort)(scale + 7168))).WriteWord(0x20, 0x4000));
                (frame, int speed) = Random(frame, 10);
                frame = Write(frame, born, Read(frame, born).WriteWord(0x22, unchecked((ushort)(speed + 6))));
            }
        }
        if (age == 55)
        {
            frame = BirthAtCenterMain(frame, 0x5A0570, task => task);
            frame = BirthAtCenterMain(frame, 0x5A0780, task => task);
        }
        if (age == 6 || age == 51)
        {
            frame = inputs.Execute(frame, age == 6 ? new NativeBattleEffect312Flash5712C0(2, 1, 10, 255) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(312, Read(frame, address), 0x5712) : null } : new NativeBattleEffect312Flash5712C0(0, 2, 4, 255) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(312, Read(frame, address), 0x5712) : null });
        }
        if (age == 44 || age == 54)
        {
            for (int actor = 0; actor < 3; actor++)
            {
                NativeBattleEffectArena row = inputs.ReadModelArena9C(frame, 0x1D972C0u + (uint)(actor * 0x9C));
                if ((row.Read(0, 1)[0] & 2) == 0)
                {
                    continue;
                }
                int contact = ContactIndexMain(frame, actor, inputs);
                (frame, uint born) = SpawnMain(frame, age == 44 ? 0x5A0AA0u : 0x5A0BF0u);
                NativeBattleModulePoolTask child = Read(frame, born).WriteWord(14, (ushort)actor);
                if (age == 44)
                {
                    child = child.WriteWord(0x1A, unchecked((ushort)contact)).WriteWord(0x20, (ushort)(contact >= 0 ? 500 : 250));
                    frame = Write(frame, born, child);
                }
                else
                {
                    frame = Write(frame, born, child);
                    (frame, int scale) = Random(frame, 3);
                    frame = Write(frame, born, Read(frame, born).WriteWord(0x18, unchecked((ushort)((scale + 1) << 12))));
                    (frame, int displacement) = Random(frame, 1500);
                    short y = unchecked((short)(-3000 - displacement));
                    if (contact < 0)
                    {
                        y = unchecked((short)(y >> 1));
                    }
                    frame = Write(frame, born, Read(frame, born).WriteWord(0x20, unchecked((ushort)y)).WriteWord(0x1A, unchecked((ushort)contact)));
                }
            }
        }
        if (age < 8)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect312Stage5713E0(age << 8, 0));
        }
        else if (age >= 27 && age < 35)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect312 stage cue requires its actual primitive process.");
            NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect312 stage cue requires current far-color controls.");
            far = far with { FarRed = 48u << 4, FarGreen = 16u << 4, FarBlue = 16u << 4 };
            frame = frame with { State = frame.State.WriteDword(CueGlobal, 0) };
            NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect312 stage cue requires current color registers."), frame.State.ReadDword(CueGlobal), unchecked((uint)(((age - 27) << 12) / 8)), far, inputs.Colors);
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far }, ScriptedColorRegisters = result.Registers }, State = frame.State.WriteDword(CueGlobal, result.PackedRgb) };
            frame = inputs.Execute(frame, new NativeBattleEffect312Stage5713E0((age - 11) << 7, result.PackedRgb));
        }
        else if (age >= 90)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect312Stage5713E0(((98 - age) * 3) << 7, 0x101030));
        }
        if (age == 1 || age == 48)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect312Sound501330(age == 1 ? 0xCDF64Cu : 0xCDF650u, 0, 128));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return new(frame, next > 98 ? 2 : 0);
    }

    private static (NativeBattleEffect312Frame Frame, uint Address) SpawnMain(NativeBattleEffect312Frame frame, uint callback)
    {
        (frame, uint address) = TrySpawn(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect312 requires its selected child allocation.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
        return (frame, address);
    }

    private static NativeBattleEffect312Frame BirthMain(NativeBattleEffect312Frame frame, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        (frame, uint address) = SpawnMain(frame, callback);
        return Write(frame, address, initialize(Read(frame, address)));
    }

    private static NativeBattleEffect312Frame BirthAtCenterMain(NativeBattleEffect312Frame frame, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        return BirthMain(frame, callback, task => initialize(CopyTask(task, 0x10, ReadBytes(frame, TargetPointAddress, 8), 8)));
    }

    private static (NativeBattleEffect312Frame Frame, int Value) Random(NativeBattleEffect312Frame frame, int modulus)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        return (frame with { Random = random }, value % modulus);
    }

    private static int ContactIndexMain(NativeBattleEffect312Frame frame, int actor, NativeBattleEffect312Inputs inputs)
    {
        return NativeBattleEffect312Contacts.Find(frame.State.ReadDword(ParametersGlobal), actor, (address, length) => inputs.ReadPhysical(frame, address, length));
    }
}

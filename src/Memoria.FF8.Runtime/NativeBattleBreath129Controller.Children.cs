using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath129Controller
{
    public static (NativeBattleBreath129Frame Frame, int Result) AdvanceTarget(NativeBattleBreath129Frame frame, uint address, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint actor = ModelAddress(task.ReadByte(15));
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(Signed(task, 22)) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleBreath129ActStep act = inputs.AdvanceAct(frame, address + 24, resources.Act(0x1225468), new(NativeBattlePackedMatrix32.FromMatrix(composed, null), 0, Working(frame) + 0x35F0));
        frame = act.Frame;
        task = Read(frame, address);
        int age = Signed(task, 12);
        if (age <= 8 && (age & 1) == 0)
        {
            (frame, uint childAddress) = Append(frame, 1, 0x6B6130);
            if (childAddress != 0)
            {
                uint row = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, actor));
                NativeBattleBreath129BodyStep body = inputs.ReadBody(frame, row);
                frame = body.Frame;
                NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
                frame = frame with { Random = random };
                int bone = sample * body.Source.BoneMatrices.Length >> 15;
                NativeBattleBreath129PointStep point = inputs.ReadBone502170(frame, actor, bone, 0);
                frame = WriteBytes(point.Frame, childAddress + 16, point.Point8, 8);
                random = frame.Random.Draw(out ushort size);
                frame = frame with { Random = random };
                frame = Write(frame, childAddress, Read(frame, childAddress).WriteWord(12, 0).WriteWord(22, unchecked((ushort)((size & 0x1FF) + 0x400))));
            }
        }
        if (age == 16)
        {
            frame = inputs.Execute(frame, new NativeBattleBreath129ContactIndex(Read(frame, address).ReadByte(14)));
        }
        if (age >= 12 && (age & 3) == 0)
        {
            uint row = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, actor));
            frame = SpawnFibres(frame, row, 0x12292F8u + (uint)(((age - 12) >> 2) * 8), address + 0x128, 12, 5, 0x800);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 24 ? 2 : 0);
    }

    public static (NativeBattleBreath129Frame Frame, int Result) AdvanceSprite(NativeBattleBreath129Frame frame, uint address, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleModulePoolTask task = Read(frame, address);
        short size = Signed(task, 22);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 16), Signed(task, 18), Signed(task, 20)), size, size, unchecked((short)-(size >> 2))) };
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0x12238C0u)).Write(4, BitConverter.GetBytes(task.ReadWord(12))).Write(36, new byte[2]);
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleBreath129DrawSprite180(temporary, 0x12238C0) { PilotIdentity = inputs.CapturePresentation ? NativeBattleBreath129VisualIdentity.Task(frame, address, 0x12238C0) : null });
        frame = Release(frame, 180);
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 9 ? 2 : 0);
    }

    public static NativeBattleBreath129Frame SpawnFibres(NativeBattleBreath129Frame frame, uint bodyRow, uint pairs, uint triangles, byte widthShift, byte fadeShift, short increment)
    {
        (frame, uint address) = Append(frame, 3, 0x6B68C0);
        if (address == 0)
        {
            return frame;
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteWord(16, unchecked((ushort)increment)).WriteByte(18, widthShift).WriteByte(19, fadeShift).WriteDword(20, pairs).WriteDword(24, triangles).WriteDword(28, bodyRow);
        for (int index = 0; index < 16; index++)
        {
            int sine = NativeBattleCameraMath.Sine(index * 2048 / 15);
            task = task.WriteWord(32 + index * 6, unchecked((ushort)(unchecked(sine * 260) >> 12))).WriteWord(34 + index * 6, 128).WriteWord(36 + index * 6, 0);
        }
        return Write(frame, address, task);
    }

    public static (NativeBattleBreath129Frame Frame, int Result) AdvanceFibre(NativeBattleBreath129Frame frame, uint address, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        frame = NativeBattleBreath129Geometry.GenerateFibre6B68C0(frame, address, resources, inputs);
        return AdvanceFibreTail6B6C3F(frame, address);
    }

    public static (NativeBattleBreath129Frame Frame, int Result) AdvanceFibreTail6B6C3F(NativeBattleBreath129Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short progress = unchecked((short)(Signed(task, 14) + Signed(task, 16)));
        task = task.WriteWord(14, unchecked((ushort)progress));
        if (progress <= 4096)
        {
            return (Write(frame, address, task), 0);
        }
        task = task.WriteWord(14, 4096);
        int shift = task.ReadByte(19) & 31;
        for (int index = 0; index < 16; index++)
        {
            int decrement = (0x1400 - ReadSigned(frame, Working(frame) + 0x35C0u + (uint)(index * 2))) >> shift;
            short value = unchecked((short)(Signed(task, 34 + index * 6) - decrement));
            task = task.WriteWord(34 + index * 6, value < 0 ? (ushort)0 : unchecked((ushort)value));
        }
        task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 10 ? 2 : 0);
    }
}

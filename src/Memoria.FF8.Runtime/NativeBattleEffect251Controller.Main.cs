using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect251Controller
{
    public static NativeBattleEffect251Step AdvanceMain5000(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint caster = frame.State.ReadDword(CasterGlobal);
        uint target = frame.State.ReadDword(TargetGlobal);
        uint buffer = frame.State.ReadDword(ArenaGlobal);
        NativeBattleEffect251PointStep queried = inputs.Point502170(frame, caster, 0, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        frame = queried.Frame;
        NativeBattleEffectArena point = queried.Output8;
        if (age < 113)
        {
            frame = WriteBytes(frame, address + 0x48, point, 8);
        }
        else
        {
            point = point.Write(0, BitConverter.GetBytes(Read(frame, address).ReadWord(0x48))).Write(4, BitConverter.GetBytes(Read(frame, address).ReadWord(0x4C)));
        }
        int intensity = age < 12 ? unchecked(age * 2300) / 12 : age >= 208 ? unchecked((220 - age) * 2300) / 12 : 2300;
        frame = frame with { State = frame.State.WriteDword(StageAmountGlobal, unchecked((uint)intensity)) };
        if (age == 55)
        {
            frame = InitializeCommonPool(frame, TrailDescriptor);
            frame = RetireCommonPool(frame, BurstDescriptor);
            frame = InitializeCommonPool(frame, ActDescriptor);
            frame = InitializeCommonPool(frame, SpriteDescriptor);
            if (ReadCurrentPause(frame) == 0)
            {
                frame = SpawnTrail5AF0(frame);
            }
        }
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 56);
        int local = age - 73;
        if ((uint)local < 20)
        {
            if (local == 0)
            {
                frame = BindProgramme(frame, address + 0x50, 0x142FB40, 0x1C8, resources);
            }
            context = MainContext(ref frame, context, Word251(point, 0), 0, Word251(point, 4), inputs).Write(0x20, BitConverter.GetBytes(0x1438778u)).Write(0x24, BitConverter.GetBytes((ushort)0)).Write(0x28, BitConverter.GetBytes(unchecked((ushort)(local > 10 ? (local - 10) * 4096 / 10 : 0)))).Write(0x34, BitConverter.GetBytes(checked(buffer + 0xA880)));
            frame = NativeBattleEffect251Programme.Advance(frame, address + 0x50, 0x142FB40, context, resources, inputs).Frame;
        }
        local = age - 83;
        if ((uint)local < 95)
        {
            if (local == 0)
            {
                frame = BindProgramme(frame, address + 0x218, 0x1434430, 0x24C, resources);
            }
            if (local < 83 && local % 6 == 0 && ReadCurrentPause(frame) == 0)
            {
                (frame, uint child) = TrySpawn(frame, ActDescriptor, 0x6F6530);
                if (child != 0)
                {
                    frame = BindProgramme(frame, child + 0x14, 0x1431F88, 0xC0, resources);
                    NativeBattleModulePoolTask spawned = Read(frame, child).WriteWord(12, unchecked((ushort)Word251(point, 0))).WriteWord(14, unchecked((ushort)(Word251(point, 2) - 256))).WriteWord(16, unchecked((ushort)Word251(point, 4)));
                    frame = Write(frame, child, spawned);
                }
            }
            int delta = age >= 141 ? Math.Min(4096, unchecked((age - 141) << 12) / 27) : 0;
            context = MainContext(ref frame, context, Word251(point, 0), 0, Word251(point, 4), inputs).Write(0x20, BitConverter.GetBytes(0x145D2B4u)).Write(0x24, BitConverter.GetBytes((ushort)0)).Write(0x26, BitConverter.GetBytes(unchecked((ushort)(local * 32)))).Write(0x28, BitConverter.GetBytes((ushort)0)).Write(0x2A, BitConverter.GetBytes(unchecked((ushort)(local * 64)))).Write(0x2C, BitConverter.GetBytes((ushort)256)).Write(0x2E, BitConverter.GetBytes((ushort)4096)).Write(0x30, BitConverter.GetBytes(unchecked((ushort)(4096 + delta)))).Write(0x34, BitConverter.GetBytes(checked(buffer + 0xA880)));
            if (age >= 141 && age < 168)
            {
                int drop = 4096 - NativeBattleCameraMath.Cosine((4096 - NativeBattleCameraMath.Cosine(delta >> 2)) >> 2);
                frame = WriteModelWord(frame, caster, 0x1E, Signed(Read(frame, address), 0x12) - drop, inputs);
                frame = WriteModelWord(frame, target, 0x1E, Signed(Read(frame, address), 0x1A) - drop, inputs);
            }
            frame = NativeBattleEffect251Programme.Advance(frame, address + 0x218, 0x1434430, context, resources, inputs).Frame;
        }
        local = age - 83;
        if ((uint)local < 31)
        {
            int fraction = local * 4096 / 31;
            int weight = NativeBattleCameraMath.Sine((fraction + 66) / 4);
            frame = BlendToModel(frame, address + 0x10, address + 0x30, 4096 - weight, weight, caster, inputs);
        }
        local = age - 114;
        if ((uint)local < 25)
        {
            if (local == 0)
            {
                frame = BindProgramme(frame, address + 0x50, 0x1433318, 0x1A0, resources);
                frame = WriteBytes(frame, address + 0x38, point, 8);
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            context = MainContext(ref frame, context, Signed(task, 0x38), 0, Signed(task, 0x3C), inputs).Write(0x20, BitConverter.GetBytes(0x145D2B0u)).Write(0x24, BitConverter.GetBytes((ushort)0)).Write(0x26, BitConverter.GetBytes(unchecked((ushort)(local * 32)))).Write(0x28, BitConverter.GetBytes((ushort)0)).Write(0x34, BitConverter.GetBytes(checked(buffer + 0xA880)));
            frame = NativeBattleEffect251Programme.Advance(frame, address + 0x50, 0x1433318, context, resources, inputs).Frame;
        }
        local = age - 163;
        if ((uint)local < 14)
        {
            if (local == 0)
            {
                frame = BindProgramme(frame, address + 0x50, 0x1432448, 0x144, resources);
                NativeBattleEffect251PointStep targetPoint = inputs.Point571400(frame, target, TaskView(Read(frame, address), 0x30, 8));
                frame = WriteBytes(targetPoint.Frame, address + 0x30, targetPoint.Output8, 8);
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            context = MainContext(ref frame, context, Signed(task, 0x30), -4096, Signed(task, 0x34), inputs).Write(0x20, BitConverter.GetBytes(0x1438778u)).Write(0x24, BitConverter.GetBytes((ushort)0)).Write(0x28, BitConverter.GetBytes((ushort)0)).Write(0x34, BitConverter.GetBytes(checked(buffer + 0xA880)));
            frame = NativeBattleEffect251Programme.Advance(frame, address + 0x50, 0x1432448, context, resources, inputs).Frame;
            if (ReadCurrentPause(frame) == 0)
            {
                NativeBattleEffectArena actor = inputs.ReadModelArena9C(frame, target);
                frame = WriteModelWord(frame, target, 0x1C, Word251(actor, 0x1C) + Signed(Read(frame, address), 0x40), inputs);
                frame = WriteModelWord(frame, target, 0x20, Word251(actor, 0x20) + Signed(Read(frame, address), 0x44), inputs);
                if (local == 13)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect251ModelFlags(target, 1, 0xFF, 4));
                }
            }
        }
        local = age - 200;
        if ((uint)local < 20)
        {
            if (local == 0)
            {
                frame = BindProgramme(frame, address + 0x218, 0x142D8A4, 0x208, resources);
                if (ReadCurrentPause(frame) == 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect251SetModelAnimation505C00(target, 5));
                }
            }
            NativeBattleModulePoolTask task = Read(frame, address);
            context = MainContext(ref frame, context, Signed(task, 0x18), 0, Signed(task, 0x1C), inputs).Write(0x20, BitConverter.GetBytes(0x1438778u)).Write(0x24, BitConverter.GetBytes((ushort)0)).Write(0x28, BitConverter.GetBytes((ushort)0)).Write(0x34, BitConverter.GetBytes(checked(buffer + 0xA880)));
            frame = NativeBattleEffect251Programme.Advance(frame, address + 0x218, 0x142D8A4, context, resources, inputs).Frame;
        }
        foreach (int start in new[] { 114, 163 })
        {
            if ((uint)(age - start) < 4)
            {
                frame = NativeBattleEffect251Lower.Fade6020(frame, 255, 255, 255, age - start, 0, 1, 3, inputs);
            }
        }
        local = age - 192;
        if ((uint)local < 8)
        {
            if (local == 0)
            {
                frame = WriteModelWord(frame, target, 0x1C, Signed(Read(frame, address), 0x18), inputs);
                frame = WriteModelWord(frame, target, 0x20, Signed(Read(frame, address), 0x1C), inputs);
            }
            if (ReadCurrentPause(frame) == 0)
            {
                int phase = (local * 4096 >> 3) + 256;
                int height = 4096 - NativeBattleCameraMath.Cosine((4096 - NativeBattleCameraMath.Cosine(phase >> 2)) >> 2);
                frame = WriteModelWord(frame, target, 0x1E, height - 4096, inputs);
                if (local == 7)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect251WriteModelBytes(target, 0x1C, TaskView(Read(frame, address), 0x18, 8), 8));
                }
            }
        }
        if ((uint)local < 27)
        {
            NativeBattleEffect251PointStep bounds = inputs.Bounds6690(frame, target);
            frame = bounds.Frame;
            int midpoint = (Word251(bounds.Output8, 0) + Word251(bounds.Output8, 2)) >> 1;
            NativeBattleEffectArena angles = PutPoint(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, new(2048, 0, 0));
            NativeBattleEffectArena pivot = PutPoint(point, 0, new(0, unchecked((short)midpoint), 0));
            frame = inputs.Execute(frame, new NativeBattleEffect251DrawTarget6A2F60(target, angles, pivot));
            frame = inputs.Execute(frame, new NativeBattleEffect251ModelFlags(target, 1, 0xFF, 4));
        }
        else if (local == 27)
        {
            if (ReadCurrentPause(frame) == 0)
            {
                uint parameters = frame.State.ReadDword(ParametersGlobal);
                uint group = Dword251(inputs.ReadPhysical(frame, parameters + 4, 4), 0);
                uint targetArgument = Dword251(inputs.ReadPhysical(frame, group + 8, 4), 0);
                frame = inputs.Execute(frame, new NativeBattleEffect251Contact506690(targetArgument));
            }
            frame = inputs.Execute(frame, new NativeBattleEffect251ModelFlags(target, 2, 0xFFFB, 0));
        }
        if (age == 33)
        {
            frame = WriteBytes(frame, BurstDescriptor + 4, NativeBattleEffectArena.Clear(), 4);
            frame = WriteBytes(frame, BurstDescriptor, NativeBattleEffectArena.Clear(), 4);
        }
        else if (age == 35)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect251QueueFile5341D0(713, checked(buffer + 0xB300), 0));
        }
        else if (age == 54)
        {
            NativeBattleEffect251PollStep poll = inputs.IsIoBusy534270(frame);
            frame = poll.Frame;
            if (poll.Busy)
            {
                return new(frame, 0);
            }
        }
        frame = inputs.Execute(frame, new NativeBattleEffect251AdvanceIo534210());
        uint sound = age == 0 ? 0x142D738u : age == 68 ? 0x142D740u : age == 199 ? 0x142D73Cu : 0;
        if (sound != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect251Sound501330(sound, 0, 128));
        }
        local = age - 114;
        if ((uint)local < 54)
        {
            if (local % 5 == 0)
            {
                (frame, uint child) = TrySpawn(frame, ActDescriptor, 0x6F6530);
                if (child != 0)
                {
                    frame = BindProgramme(frame, child + 0x14, 0x1431418, 0x144, resources);
                    NativeBattleEffect251PointStep childPoint = inputs.Point571400(frame, target, TaskView(Read(frame, child), 12, 8));
                    frame = WriteBytes(childPoint.Frame, child + 12, childPoint.Output8, 8);
                    frame = inputs.Execute(frame, new NativeBattleEffect251SetModelAnimation505C00(target, 5));
                }
            }
            if (age < 141)
            {
                int fraction = (local % 5) * 4096 / 9;
                int weight = NativeBattleCameraMath.Sine(fraction / 2);
                frame = BlendToModel(frame, address + 0x30, address + 0x28, 4096 - weight, weight, caster, inputs);
            }
        }
        local = age - 168;
        if ((uint)local < 14)
        {
            int fraction = (local + 1) * 4096 / 14;
            if (local == 0)
            {
                frame = WriteBytes(frame, address + 0x38, CopyBytes(inputs.ReadModelArena9C(frame, caster), 0x1C, NativeBattleEffectArena.Clear(), 0, 8), 8);
            }
            frame = BlendToModel(frame, address + 0x38, address + 0x10, 4096 - fraction, fraction, caster, inputs);
            if (local == 13)
            {
                frame = SpawnSpriteRing(frame, address);
            }
        }
        if (age >= 220)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect251WriteModelBytes(caster, 0x1C, TaskView(Read(frame, address), 0x10, 8), 8));
            return new(frame, 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleEffectArena MainContext(ref NativeBattleEffect251Frame frame, NativeBattleEffectArena context, int x, int y, int z, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = x, Y = y, Z = z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = frame with { State = frame.State with { CallbackStackWord58 = unchecked((ushort)matrix.Y) } };
        return CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, context, 0, 32);
    }

    private static NativeBattleEffect251Frame BlendToModel(NativeBattleEffect251Frame frame, uint first, uint second, int firstWeight, int secondWeight, uint model, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleWeightedPointsStep blend = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Vector(ReadBytes(frame, first, 6)), Vector(ReadBytes(frame, second, 6)), unchecked((short)firstWeight), unchecked((short)secondWeight), inputs.Colors);
        frame = frame with { Memory = blend.Memory };
        return inputs.Execute(frame, new NativeBattleEffect251WriteModelBytes(model, 0x1C, PointView(blend.Position), 6));
    }

    private static NativeBattleEffect251Frame SpawnSpriteRing(NativeBattleEffect251Frame frame, uint main)
    {
        for (int index = 0; index < 16; index++)
        {
            (frame, uint address) = TrySpawn(frame, SpriteDescriptor, 0x6F65F0);
            if (address == 0)
            {
                break;
            }
            int radius = DrawRandom(ref frame) + 0x4000;
            int x = unchecked(NativeBattleCameraMath.Sine(index * 256) * radius) >> 15;
            int z = unchecked(NativeBattleCameraMath.Cosine(index * 256) * radius) >> 15;
            NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, unchecked((ushort)(Signed(Read(frame, main), 0x10) + x / 8))).WriteWord(14, 0).WriteWord(16, unchecked((ushort)(Signed(Read(frame, main), 0x14) + z / 8))).WriteWord(18, 0).WriteByte(20, unchecked((byte)(x >> 4))).WriteByte(22, unchecked((byte)(z >> 4)));
            task = task.WriteByte(21, unchecked((byte)(248 - (DrawRandom(ref frame) & 7))));
            frame = Write(frame, address, task);
        }
        return frame;
    }
}

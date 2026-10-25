using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect251Controller
{
    public static NativeBattleEffect251Step AdvanceRoot4580(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool first = root.ReadByte(15) != 0;
        frame = Write(frame, address, root.WriteByte(15, first ? (byte)0 : (byte)1));
        uint buffer = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(buffer + (uint)(first ? FirstPacketOffset : SecondPacketOffset))).WriteDword(StageAmountGlobal, 0) };
        root = Read(frame, address);
        if (root.ReadWord(12) == 1 && ReadCurrentPause(frame) == 0 && root.ReadByte(14) == 0)
        {
            frame = Write(frame, address, root.WriteByte(14, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect251InitializeIo534110(checked(buffer + IoOffset)));
            frame = InitializeCommonPool(frame, MainDescriptor);
            frame = InitializeCommonPool(frame, BurstDescriptor);
            (frame, uint main) = TrySpawn(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect251 main allocation is mandatory.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x178);
            uint caster = frame.State.ReadDword(CasterGlobal);
            uint target = frame.State.ReadDword(TargetGlobal);
            frame = SpawnBurst4840(frame, target);
            frame = WriteBytes(frame, main + 0x10, CopyBytes(inputs.ReadModelArena9C(frame, caster), 0x1C, NativeBattleEffectArena.Clear(), 0, 8), 8);
            frame = WriteBytes(frame, main + 0x18, CopyBytes(inputs.ReadModelArena9C(frame, target), 0x1C, NativeBattleEffectArena.Clear(), 0, 8), 8);
            frame = WriteModelWord(frame, target, 0x1E, 0, inputs);
            NativeBattleEffect251PointStep point = inputs.Point571400(frame, target, TaskView(Read(frame, main), 0x20, 8));
            frame = WriteBytes(point.Frame, main + 0x20, point.Output8, 8);
            NativeBattleEffectArena source = inputs.ReadModelArena9C(frame, caster);
            NativeBattleEffectArena second = inputs.ReadModelArena9C(frame, target);
            int dx = Word251(source, 0x1C) - Word251(point.Output8, 0);
            int dz = Word251(source, 0x20) - Word251(point.Output8, 4);
            int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dz * dz));
            int radius = Word251(source, 0x26) + Word251(second, 0x26);
            int outer = radius + radius / 2;
            NativeBattleModulePoolTask task = Read(frame, main);
            task = task.WriteWord(0x28, unchecked((ushort)(Word251(point.Output8, 0) + unchecked(outer * dx) / distance))).WriteWord(0x2A, 0).WriteWord(0x2C, unchecked((ushort)(Word251(point.Output8, 4) + unchecked(outer * dz) / distance)));
            task = task.WriteWord(0x30, unchecked((ushort)(Word251(point.Output8, 0) + unchecked(radius * dx) / distance))).WriteWord(0x32, 0).WriteWord(0x34, unchecked((ushort)(Word251(point.Output8, 4) + unchecked(radius * dz) / distance)));
            task = task.WriteWord(0x40, unchecked((ushort)(unchecked(-dx << 9) / distance))).WriteWord(0x44, unchecked((ushort)(unchecked(-dz << 9) / distance)));
            frame = Write(frame, main, task);
            uint firstObject = checked(0x1434430u + Dword251(ReadBytes(frame, 0x1434438, 4), 0));
            frame = NativeBattleEffect251Lower.Mutate4FA0(frame, firstObject);
            uint secondObject = checked(0x1434430u + Dword251(ReadBytes(frame, 0x143443C, 4), 0));
            frame = NativeBattleEffect251Lower.Mutate4FA0(frame, secondObject);
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(14) != 0)
        {
            frame = frame with { State = frame.State.WriteView(SavedViewAddress, PackedArena(inputs.ReadView(frame)), 32) };
            (frame, survivors) = AdvanceDescriptor(frame, MainDescriptor, resources, inputs);
            foreach (uint descriptor in new uint[] { BurstDescriptor, SpriteDescriptor, TrailDescriptor, ActDescriptor })
            {
                (frame, _) = AdvanceDescriptor(frame, descriptor, resources, inputs);
            }
        }
        frame = inputs.Execute(frame, new NativeBattleEffect251Intensity5713E0(0, unchecked((int)frame.State.ReadDword(StageAmountGlobal))));
        if (ReadCurrentPause(frame) == 0)
        {
            root = Read(frame, address);
            if (root.ReadByte(14) != 0 && survivors == 0)
            {
                return new(inputs.Execute(frame, new NativeBattleEffect251Intensity5713E0(0, 0)), 2);
            }
            frame = Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect251Frame SpawnBurst4840(NativeBattleEffect251Frame frame, uint model)
    {
        (frame, uint address) = TrySpawn(frame, BurstDescriptor, BurstCallback);
        if (address == 0)
        {
            return frame;
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0).WriteWord(14, 0).WriteWord(0x22, 0).WriteWord(0x20, checked((ushort)Actor(model)));
        return Write(frame, address, task);
    }

    public static NativeBattleEffect251Step AdvanceBurst4890(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Inputs inputs)
    {
        if (ReadCurrentPause(frame) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        uint model = ModelAddress(Signed(task, 0x20));
        if (Signed(task, 12) == 1)
        {
            NativeBattleEffectArena actor = inputs.ReadModelArena9C(frame, model);
            int radius = Math.Min(1000, unchecked(2800 * Word251(actor, 0x26)) >> 12);
            NativeBattleEffect251PointStep center = inputs.Point571400(frame, model, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = center.Frame;
            for (int index = 0; index < 4; index++)
            {
                (frame, uint child) = TrySpawn(frame, BurstDescriptor, 0x6F4A30);
                if (child == 0)
                {
                    continue;
                }
                NativeBattleModulePoolTask particle = CopyTask(Read(frame, child), 0x10, center.Output8, 8).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(DrawRandom(ref frame) % 6 + index * 2)));
                particle = particle.WriteWord(0x10, unchecked((ushort)(Signed(particle, 0x10) + DrawRandom(ref frame) % radius - (radius >> 1))));
                particle = particle.WriteWord(0x12, unchecked((ushort)(Signed(particle, 0x12) + DrawRandom(ref frame) % radius - (radius >> 1))));
                particle = particle.WriteWord(0x14, unchecked((ushort)(Signed(particle, 0x14) + DrawRandom(ref frame) % radius - (radius >> 1))));
                particle = particle.WriteWord(0x16, unchecked((ushort)Word251(actor, 0x24)));
                if (Signed(particle, 0x12) > Word251(actor, 0x24) - 550)
                {
                    particle = particle.WriteWord(0x12, unchecked((ushort)(Signed(particle, 0x12) - 500 - DrawRandom(ref frame) % 300)));
                }
                particle = particle.WriteWord(0x1C, unchecked((ushort)(DrawRandom(ref frame) % 1536 + 3328)));
                frame = Write(frame, child, particle);
            }
        }
        if (Signed(task, 12) == 6)
        {
            frame = inputs.Point571400(frame, model, NativeBattleEffectArena.Clear().MarkUnknown(0, 8)).Frame;
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        return new(frame, Signed(task, 12) >= 22 ? 2 : 0);
    }

    public static NativeBattleEffect251Frame SpawnTrail5AF0(NativeBattleEffect251Frame frame)
    {
        (frame, uint address) = TrySpawn(frame, TrailDescriptor, 0x6F5B20);
        if (address == 0)
        {
            throw new InvalidDataException("Effect251 requires its mandatory trail allocation.");
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0);
        for (int index = 0; index < 7; index++)
        {
            task = task.WriteDword(0x10 + index * 20, 0);
        }
        return Write(frame, address, task);
    }

    public static int DrawRandom(ref NativeBattleEffect251Frame frame)
    {
        NativeCrtRandomState next = frame.Random.Draw(out ushort value);
        frame = frame with { Random = next };
        return value;
    }

    public static NativeBattleEffect251Frame WriteModelWord(NativeBattleEffect251Frame frame, uint model, int offset, int value, NativeBattleEffect251Inputs inputs)
    {
        return inputs.Execute(frame, new NativeBattleEffect251WriteModelBytes(model, offset, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((ushort)value))), 2));
    }
}

using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect338Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect338Controller
{
    private static NativeBattleEffect338Frame InitializeParticleBank(NativeBattleEffect338Frame frame)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect338 particle reset requires its actual arena.");
        NativeBattleEffect338ParticleBank bank = new(NativeBattleEffectNodeContextMemory.Copy(arena, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 1536), 0, 1536), null);
        bank = bank.Reset6EC0C0();
        frame = WriteBytes(frame, ArenaAddress, bank.Storage, 1536);
        return Dword(frame, 0x25561D8, ArenaAddress);
    }

    private static NativeBattleEffect338ParticleBank ParticleBank(NativeBattleEffect338Frame frame)
    {
        uint cursor = frame.State.ReadDword(0x25561D8);
        if (cursor < ArenaAddress || cursor - ArenaAddress >= 1536)
        {
            throw new InvalidDataException("Effect338 particle cursor leaves its actual COMMON bank.");
        }
        return new(ReadBytes(frame, ArenaAddress, 1536), checked((int)(cursor - ArenaAddress)));
    }

    private static (NativeBattleEffect338Frame Frame, uint Address) AllocateParticle(NativeBattleEffect338Frame frame)
    {
        var allocated = ParticleBank(frame).Allocate6EC980();
        if (allocated.Offset is not int offset)
        {
            return (frame, 0);
        }
        frame = Dword(frame, 0x25561D8, checked(ArenaAddress + (uint)allocated.Bank.NextOffset!.Value));
        return (frame, checked(ArenaAddress + (uint)offset));
    }

    private static NativeBattleEffect338Frame AdvanceParticleBank(NativeBattleEffect338Frame frame) => WriteBytes(frame, ArenaAddress, ParticleBank(frame).Advance6EC9D0().Storage, 1536);

    private static NativeBattleEffect338Frame SpawnProgramme(NativeBattleEffect338Frame frame, uint pointAddress, uint sourceAddress, short height, NativeBattleEffect338Resources resources)
    {
        (frame, uint child) = Append(frame, Pools[2].Descriptor, 0x6ECC50);
        if (child == 0)
        {
            return frame;
        }
        NativeBattleEffectArena point = PointResource(resources, pointAddress);
        NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, unchecked((ushort)(Word338(point, 0) + Word(frame, 0x2556248)))).WriteWord(14, unchecked((ushort)(Word338(point, 2) + Word(frame, 0x2556242) + height))).WriteWord(16, unchecked((ushort)(Word338(point, 4) + Word(frame, 0x255624C)))).WriteWord(18, 0);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x184].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Programme(sourceAddress), 0x184);
        task = CopyTask(task, 20, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), 0x184);
        return Write(frame, child, task);
    }

    private static NativeBattleEffect338Frame SpawnImpactParticles(NativeBattleEffect338Frame frame, uint main, uint pointAddress, NativeBattleEffect338Resources resources)
    {
        (frame, NativeBattleEffectArena point) = ReadEmbeddedPoint(frame, main, pointAddress, resources);
        (frame, uint first) = AllocateParticle(frame);
        if (first == 0)
        {
            return frame;
        }
        NativeBattleEffectArena hit = ReadBytes(frame, first, 24);
        hit = NativeBattleEffectNodeContextMemory.Copy(point, 0, hit, 4, 4).Write(8, BitConverter.GetBytes(unchecked((short)(Word338(point, 4) - 500)))).Write(10, BitConverter.GetBytes((ushort)7)).Write(12, new byte[4]).Write(16, new byte[2]).Write(20, new byte[4]).Write(18, BitConverter.GetBytes((ushort)3000)).Write(0, BitConverter.GetBytes(0x1371464u));
        frame = WriteBytes(frame, first, hit, 24);
        for (int index = 0; index < 7; index++)
        {
            (frame, uint particle) = AllocateParticle(frame);
            if (particle == 0)
            {
                break;
            }
            NativeBattleEffectArena row = ReadBytes(frame, particle, 24);
            row = NativeBattleEffectNodeContextMemory.Copy(point, 0, row, 4, 4).Write(8, BitConverter.GetBytes(unchecked((short)(Word338(point, 4) - 500)))).Write(10, BitConverter.GetBytes((ushort)10));
            frame = Roll(frame, out ushort xRandom);
            short vx = unchecked((short)((xRandom & 127) + 128));
            frame = Roll(frame, out ushort yRandom);
            short vy = unchecked((short)((yRandom & 127) - 64));
            row = row.Write(14, BitConverter.GetBytes(vy)).Write(6, BitConverter.GetBytes(unchecked((short)(Word338(row, 6) + vy * 2))));
            frame = Roll(frame, out ushort signRandom);
            if ((signRandom & 1) != 0)
            {
                vx = unchecked((short)-vx);
            }
            row = row.Write(12, BitConverter.GetBytes(vx)).Write(16, new byte[2]).Write(22, [0]).Write(20, [Acceleration6ECAF3(vx)]).Write(21, [Acceleration6ECAF3(vy)]).Write(18, BitConverter.GetBytes((ushort)3072)).Write(0, BitConverter.GetBytes(0x13711FCu));
            frame = WriteBytes(frame, particle, row, 24);
        }
        return frame;
    }

    public static byte Acceleration6ECAF3(short velocity)
    {
        int high = unchecked((int)((unchecked((long)(int)0x99999999 * velocity)) >> 32));
        int scaled = high >> 2;
        scaled = unchecked(scaled + (int)((uint)scaled >> 31));
        return unchecked((byte)(scaled >> 2));
    }

    private static NativeBattleEffect338Frame SpawnFinalProgramme(NativeBattleEffect338Frame frame, uint main, uint pointAddress, NativeBattleEffect338Resources resources)
    {
        (frame, uint child) = Append(frame, Pools[2].Descriptor, 0x6ECC50);
        if (child == 0)
        {
            return frame;
        }
        (frame, NativeBattleEffectArena point) = ReadEmbeddedPoint(frame, main, pointAddress, resources);
        NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, unchecked((ushort)Word338(point, 0))).WriteWord(14, 0).WriteWord(16, unchecked((ushort)Word338(point, 4))).WriteWord(18, 0);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0xF8].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Programme(0x13755B8), 0xF8);
        frame = Write(frame, child, CopyTask(task, 20, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), 0xF8));
        for (int index = 0; index < 8; index++)
        {
            (frame, uint fragment) = Append(frame, Pools[3].Descriptor, 0x6ED0C0);
            if (fragment == 0)
            {
                break;
            }
            task = Read(frame, fragment).WriteDword(24, 0).WriteByte(25, 20);
            frame = Roll(frame, out ushort scale);
            task = task.WriteWord(32, unchecked((ushort)((scale & 2047) + 2048)));
            task = CopyTask(task, 12, point, 6);
            frame = Roll(frame, out ushort life);
            task = task.WriteByte(27, (byte)((life & 3) + 7));
            frame = Roll(frame, out ushort vx);
            task = task.WriteWord(18, unchecked((ushort)((vx & 127) - 64)));
            frame = Roll(frame, out ushort vz);
            task = task.WriteWord(22, unchecked((ushort)(-128 - (vz & 127))));
            frame = Roll(frame, out ushort vy);
            task = task.WriteWord(20, unchecked((ushort)(-128 - (vy & 127))));
            for (int field = 28; field < 32; field++)
            {
                frame = Roll(frame, out ushort value);
                task = task.WriteByte(field, unchecked((byte)value));
            }
            frame = Write(frame, fragment, task);
        }
        return frame;
    }

    private static (NativeBattleEffect338Frame Frame, NativeBattleEffectArena Point8) ReadEmbeddedPoint(NativeBattleEffect338Frame frame, uint main, uint sourceAddress, NativeBattleEffect338Resources resources)
    {
        NativeBattleEffectArena sourcePoint = PointResource(resources, sourceAddress);
        NativeBattleEffect338Skeleton skeleton = frame.State.Skeleton ?? throw new NotSupportedException("Effect338 point query requires its current embedded skeleton.");
        int count = skeleton.Storage.Read(0, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(skeleton.Storage, 0, count);
        skeleton = skeleton.Publish(pose);
        ImmutableArray<NativeBattleActionMatrix> bones = Enumerable.Range(0, count).Select(index => NativeBattleArenaPose.ReadMatrix(skeleton.Storage, 32 + index * 48)).ToImmutableArray();
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena prepared = NativeBattleBodyBoneTemporary.Prepare508C90(resources.Model.PoseSource, pose, bones, Context(frame, temporary, 88));
        frame = Release(PublishContext(frame, temporary, prepared, 88), 88);
        frame = frame with { State = frame.State with { Skeleton = skeleton } };
        int bone = Word338(sourcePoint, 6);
        if ((uint)bone >= (uint)count)
        {
            throw new InvalidDataException("Effect338 point resource selects a bone outside its original current model.");
        }
        NativeBattleModulePoolTask task = Read(frame, main);
        NativeBattleActionMatrix world = TaskMatrix(task, 0x64);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(world, bones[bone]), null));
        NativeBattleGeometryRegisterStep registers = Registers338(frame);
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Vector338(sourcePoint), unchecked((short)bone)));
        NativeBattleEffectArena result = StoreIr338(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), registers);
        return (Publish338(frame, registers), result);
    }

    private static NativeBattleEffectArena PointResource(NativeBattleEffect338Resources resources, uint address)
    {
        uint offset = unchecked(address - 0x1376658);
        if (offset >= 64 || offset % 8 != 0)
        {
            throw new InvalidDataException("Effect338 point address leaves its selected original rows.");
        }
        return NativeBattleEffectArena.Clear().Write(0, resources.Points.AsSpan().Slice(checked((int)offset), 8));
    }
}

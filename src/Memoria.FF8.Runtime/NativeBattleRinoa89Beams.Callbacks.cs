using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89BeamServices(NativeBattleActionMatrix View, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, ImmutableArray<byte> Mesh1497140, NativeBattleGfAnimationSource Program14A3EB0, Func<NativeBattleRinoa89Frame, NativeBattleGfGeometryBatch, bool, NativeBattleRinoa89Frame> PublishPackets, Func<NativeBattleRinoa89Frame, NativeBattleRinoa89Frame> ApplyContacts6FC410);

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89TaskStep AdvanceBeam6FD580(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRinoa89BeamServices services)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        RequireBeamCallback(pool, task, 0x6FD580);
        short age = unchecked((short)task.ReadWord(44));
        if (age < 12)
        {
            NativeBattleActionMatrix pose = ReadBeamPose(task);
            frame = BeamDrawMesh(frame, pose, services, true);
            if (age > 3 && age < 8)
            {
                frame = BeamSpawnSparks(frame, pose, 8, true);
            }
            task = task.WriteDword(32, unchecked((uint)(pose.X - pose.Rotation[2]))).WriteDword(36, unchecked((uint)(pose.Y - pose.Rotation[5]))).WriteDword(40, unchecked((uint)(pose.Z - pose.Rotation[8])));
        }
        if (age >= 6)
        {
            NativeBattleActionMatrix pose = ReadBeamPose(task);
            pose = pose with { X = BeamCenterWord(frame, 0) + pose.Rotation[2] / 4, Y = BeamCenterWord(frame, 2) + pose.Rotation[5] / 4, Z = BeamCenterWord(frame, 4) + pose.Rotation[8] / 4 };
            if (age <= 8 && (age & 1) == 0)
            {
                frame = BeamSpawnEmitters(frame, pose, 4, 0, false, false);
                frame = BeamSpawnEmitters(frame, pose, 4, 1, true, false);
            }
            if (age == 6)
            {
                frame = BeamSpawnSparks(frame, pose, 24, false);
                frame = services.ApplyContacts6FC410(frame);
            }
            if (services.Program14A3EB0.Address != 0x14A3EB0)
            {
                throw new InvalidDataException("Native6FD580 requires its actual retained programme.");
            }
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(services.View, pose);
            NativeBattleRinoa89ProgramContext context = new(matrix, ArenaAddress + 100492, 0);
            NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(48, 344).ToImmutableArray());
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(services.Program14A3EB0, 0, (current, node) =>
            {
                NativeBattleRinoa89ProgramDraw drawn = NativeBattleRinoa89ProgramDrawing.Draw6FBB80(frame, services.Program14A3EB0, node, context, services.Projection, services.Colors);
                frame = services.PublishPackets(drawn.Frame, drawn.Packets, true);
                return current;
            });
            task = task.WriteBytes(48, advanced.Workspace.Data.AsSpan());
            if (advanced.Remaining == 0)
            {
                return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 2);
            }
        }
        task = task.WriteWord(44, unchecked((ushort)(age + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 0);
    }

    public static NativeBattleRinoa89TaskStep AdvanceBeam6FDC20(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRinoa89BeamServices services)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        RequireBeamCallback(pool, task, 0x6FDC20);
        short age = unchecked((short)task.ReadWord(44));
        if (age < 12)
        {
            NativeBattleActionMatrix pose = ReadBeamPose(task);
            frame = BeamDrawMesh(frame, pose, services, false);
            task = task.WriteDword(32, unchecked((uint)(pose.X + pose.Rotation[2] / -2))).WriteDword(36, unchecked((uint)(pose.Y + pose.Rotation[5] / -2))).WriteDword(40, unchecked((uint)(pose.Z + pose.Rotation[8] / -2)));
        }
        if (age >= 3)
        {
            NativeBattleActionMatrix pose = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], BeamCenterWord(frame, 0), BeamCenterWord(frame, 2), BeamCenterWord(frame, 4), 4096);
            uint phase = unchecked((uint)(age - 3));
            if (phase <= 9 && (age & 3) == 0)
            {
                byte palette = checked((byte)((phase >> 2) + 2));
                frame = BeamSpawnEmitters(frame, pose, 48, palette, false, true);
            }
            if (age == 3)
            {
                frame = BeamSpawnEmitters(frame, pose, 16, 1, true, false);
                frame = BeamSpawnSparks(frame, pose, 16, false);
                task = WriteBeamRotation(task, BeamRotationX701220(1024)).WriteWord(30, 0);
            }
        }
        task = task.WriteWord(44, unchecked((ushort)(age + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, age < 12 ? 0 : 2);
    }

    private static NativeBattleRinoa89Frame BeamDrawMesh(NativeBattleRinoa89Frame frame, NativeBattleActionMatrix pose, NativeBattleRinoa89BeamServices services, bool common)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 120);
        uint address = allocation.Address;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(services.View, pose);
        NativeBattleFileStaging staging = allocation.Staging;
        for (int lane = 0; lane < 9; lane++)
        {
            staging = staging.Write(address + (uint)lane * 2, BitConverter.GetBytes(matrix.Rotation[lane]));
        }
        staging = staging.MarkUnknown(address + 18, 2).Write(address + 20, BitConverter.GetBytes(matrix.X)).Write(address + 24, BitConverter.GetBytes(matrix.Y)).Write(address + 28, BitConverter.GetBytes(matrix.Z));
        staging = Dword(Dword(staging, address + 60, 48), address + 32, 0x1497140);
        NativeBattleModuleMeshContext context = new(48, null, null, null, null, null, 17, 2);
        NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch572200(staging, address + 32, 0x1497140, services.Mesh1497140, matrix, services.Projection, context, services.Colors);
        frame = services.PublishPackets(frame with { Staging = drawn.Staging }, drawn.Batch, common);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 120) };
    }

    private static void RequireBeamCallback(NativeBattleEffectTaskPool pool, NativeBattleModulePoolTask task, uint callback)
    {
        if (pool.DescriptorAddress != Beams || pool.BankAddress != ArenaAddress || pool.Stride != 392 || pool.Slots.Length != 8 || task.Callback != callback)
        {
            throw new InvalidDataException("Native Wishing Star beam requires its actual registered callback and physical pool.");
        }
    }
}

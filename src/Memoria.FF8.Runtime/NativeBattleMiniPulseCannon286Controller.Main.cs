using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMiniPulseCannon286Controller
{
    private static (NativeBattleMiniPulseCannon286Frame Frame, int Result) AdvanceRoot(NativeBattleMiniPulseCannon286Frame frame, uint address, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(frame.State.ReadDword(ArenaGlobal) + (alternate ? 0xFD4u : 0x10FD4u)));
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, 0x2507CC8, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Mini Pulse Cannon's freshly reset main pool must admit its sole task.");
            }
            frame = ClearDwords701200(frame, main + 12, 434);
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Mini Pulse Cannon requires its first physical contact.");
            uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
            frame = Write(frame, main, Read(frame, main).WriteDword(16, target));
            int[] offsets = [28, 584, 928, 1604];
            int[] lengths = [556, 344, 676, 144];
            NativeBattleGfAnimationSource[] sources = [resources.ProgrammeA, resources.ProgrammeB, resources.ProgrammeC, resources.ProgrammeD];
            for (int index = 0; index < offsets.Length; index++)
            {
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(TaskView(Read(frame, main), offsets[index], lengths[index]), 0, lengths[index]);
                workspace = workspace.Initialize7016B0(sources[index], lengths[index]);
                frame = WriteBytes(frame, main + (uint)offsets[index], workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), lengths[index]);
            }
            NativeBattleMiniPulseCannon286PointStep anchor = inputs.ReadAnchor571400(frame, target);
            frame = WriteBytes(anchor.Frame, main + 20, anchor.Point8, 8);
        }
        if (Read(frame, address).ReadByte(15) != 0)
        {
            uint caster = frame.State.ReadDword(CasterGlobal);
            frame = inputs.Execute(frame, new NativeBattleMiniPulseCannon286ComposeCaster5095B0(caster));
            (frame, int mainSurvivors) = AdvancePool(frame, 2, resources, inputs);
            if (mainSurvivors == 0)
            {
                frame = inputs.Execute(frame, new NativeBattleMiniPulseCannon286ActorFlags(caster, 8, 0));
            }
            frame = inputs.Execute(frame, new NativeBattleMiniPulseCannon286RestoreCaster508C90(caster));
            (frame, int particles) = AdvancePool(frame, 1, resources, inputs);
            if (mainSurvivors == 0 && particles == 0)
            {
                return (frame, 2);
            }
        }
        root = Read(frame, address);
        return (Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleMiniPulseCannon286Frame Frame, int Result) AdvanceMain(NativeBattleMiniPulseCannon286Frame frame, uint address, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 40);
        if ((uint)(age - 25) < 42)
        {
            int time = age - 25;
            NativeBattleMiniPulseCannon286MuzzleStep muzzle = UpdateMuzzle67A770(frame, resources, inputs);
            frame = muzzle.Frame;
            uint caster = frame.State.ReadDword(CasterGlobal);
            short yaw = inputs.ReadModel(frame, caster).Rotation14 ?? throw new NotSupportedException("Mini Pulse Cannon requires its current caster yaw.");
            NativeBattleActionMatrix world = NativeBattlePandemona291Restoration.Rotation701270(yaw) with { X = muzzle.X, Y = muzzle.Y, Z = muzzle.Z };
            if (time < 8)
            {
                frame = SpawnSprites286(frame, time, world, muzzle);
            }
            caller = Caller286(caller, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world));
            caller = caller.Write(32, BitConverter.GetBytes((short)-32)).Write(34, BitConverter.GetBytes((ushort)0)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
            frame = AdvanceProgramme286(frame, address, 28, 556, caller, resources, inputs);
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleModelTransform target = inputs.ReadModel(frame, task.ReadDword(16)).Transform ?? throw new NotSupportedException("Mini Pulse Cannon requires its current recipient fields.");
            int x = unchecked(Signed(task, 20) - muzzle.X);
            int z = unchecked(muzzle.Z - Signed(task, 24));
            int y = time < 14 ? unchecked(muzzle.Y - target.Angle36) : time < 24 ? unchecked(unchecked((muzzle.Y - target.Angle36) * (24 - time)) / 10) : 0;
            var basis = NativeBattleFunguarLaser277.Basis50CBA0(x, y, z);
            world = basis.Matrix with { X = muzzle.X, Y = muzzle.Y, Z = muzzle.Z };
            int scale = time <= 14 ? 4096 : time <= 26 ? basis.Length >> 2 : basis.Length >> 1;
            world = NativeBattlePoseMatrices.Scale56BEF0(world, 4096, 4096, scale);
            caller = Caller286(caller, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world)).Write(34, BitConverter.GetBytes(unchecked((ushort)((time & 3) << 5))));
            frame = AdvanceProgramme286(frame, address, 584, 344, caller, resources, inputs);
        }
        if ((uint)(age - 49) < 20)
        {
            NativeBattleMiniPulseCannon286MuzzleStep muzzle = UpdateMuzzle67A770(frame, resources, inputs);
            frame = muzzle.Frame;
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleActionMatrix world = NativeBattleFunguarLaser277.Basis50CBA0(unchecked(muzzle.X - Signed(task, 20)), 0, unchecked(Signed(task, 24) - muzzle.Z)).Matrix with { X = Signed(task, 20), Y = Signed(task, 22), Z = Signed(task, 24) };
            caller = Caller286(caller, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world));
            frame = AdvanceProgramme286(frame, address, 928, 676, caller, resources, inputs);
            NativeBattleModelTransform target = inputs.ReadModel(frame, task.ReadDword(16)).Transform ?? throw new NotSupportedException("Mini Pulse Cannon requires its current recipient fields.");
            world = world with { Y = target.Angle36 };
            caller = Caller286(caller, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world)).Write(32, BitConverter.GetBytes((short)-128)).Write(34, BitConverter.GetBytes((ushort)0)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
            frame = AdvanceProgramme286(frame, address, 1604, 144, caller, resources, inputs);
        }
        if (age == 24)
        {
            frame = inputs.Execute(frame, new NativeBattleMiniPulseCannon286Sound501330(resources.Sound, 0, 128));
        }
        if (age == 53)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Mini Pulse Cannon requires its current first physical contact.");
            frame = inputs.Execute(frame, new NativeBattleMiniPulseCannon286Contact506690(first));
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        short next = unchecked((short)(current.ReadWord(12) + 1));
        frame = Write(frame, address, current.WriteWord(12, unchecked((ushort)next)));
        return (frame, next >= 69 ? 2 : 0);
    }

    private static NativeBattleMiniPulseCannon286Frame AdvanceProgramme286(NativeBattleMiniPulseCannon286Frame frame, uint task, int offset, int length, NativeBattleEffectArena caller, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(ReadBytes(frame, task + (uint)offset, length), 0, length);
        NativeBattleGfAnimationSource source = resources.ReadProgramme(workspace.ResourceAddress);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, task + (uint)offset, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), length);
            frame = DrawProgrammeNode67A4E0(frame, source, node, caller, resources, inputs);
            return NativeBattleGfAnimationWorkspace.Borrow(ReadBytes(frame, task + (uint)offset, length), 0, length);
        });
        return WriteBytes(frame, task + (uint)offset, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan().Slice(0, length)), length);
    }

    private static NativeBattleEffectArena Caller286(NativeBattleEffectArena caller, NativeBattleActionMatrix matrix) => NativeBattleEffectNodeContextMemory.Copy(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, caller, 0, 32);

    private static NativeBattleMiniPulseCannon286Frame SpawnSprites286(NativeBattleMiniPulseCannon286Frame frame, int time, NativeBattleActionMatrix world, NativeBattleMiniPulseCannon286MuzzleStep muzzle)
    {
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(world, 0), true, false);
        for (int index = 0; index < 2; index++)
        {
            (frame, uint address) = Append(frame, 0x2507CB8, 0x67A7C0);
            if (address == 0)
            {
                break;
            }
            frame = DrawRandom(frame, out ushort angleRandom);
            int angle = unchecked(((time + 16) << 8) - index * 0x800 + (angleRandom & 63) - 32);
            frame = DrawRandom(frame, out ushort radiusRandom);
            int radius = (radiusRandom & 1023) + 512;
            frame = DrawRandom(frame, out ushort zRandom);
            NativeBattleVector vector = new(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)), unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12)), unchecked((short)((zRandom & 255) - 128)));
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleEffectArena retained = TaskView(task, 26, 2);
            short? w = retained.ReadSnapshot(0, 2).Known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(retained.Read(0, 2)) : null;
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Mini Pulse Cannon requires its current arithmetic owner."));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, vector, w), false);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            short[] values = [unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value)];
            int[] origin = [muzzle.X, muzzle.Y, muzzle.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteWord(20 + axis * 2, unchecked((ushort)(values[axis] + origin[axis]))).WriteWord(28 + axis * 2, unchecked((ushort)(-values[axis] / 10)));
            }
            task = task.WriteDword(12, SpriteAddress).WriteDword(16, 1200).WriteWord(26, 0).WriteWord(34, 10);
            frame = Write(frame, address, task);
        }
        return frame;
    }

    public static NativeBattleActionHeaderSlot Header(NativeBattleMiniPulseCannon286Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(ParametersGlobal) != 0x1D99A78)
        {
            throw new NotSupportedException("Mini Pulse Cannon requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Mini Pulse Cannon parameter header leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}

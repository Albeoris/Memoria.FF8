using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShotgun281Controller
{
    private static (NativeBattleShotgun281Frame Frame, int Result) AdvanceRoot(NativeBattleShotgun281Frame frame, uint address, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, 0x2507F8C, unchecked(frame.State.ReadDword(0x2507FD0) + (alternate ? 0x2018u : 0x12018u)));
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, 0x2507F58, 0x67DF80);
            if (main == 0)
            {
                throw new InvalidDataException("Shotgun's freshly reset main pool must admit its sole task.");
            }
            frame = ClearDwords701200(frame, main + 12, 133);
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Shotgun requires its actual first physical contact.");
            uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
            frame = Write(frame, main, Read(frame, main).WriteDword(16, target));
            NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + 20, 524);
            workspace = workspace.Initialize7016B0(resources.ProgrammeA, 524);
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + 20, workspace);
            NativeBattleModelTransform casterPosition = inputs.ReadModel(frame, frame.State.ReadDword(0x2507F30)).Transform ?? throw new NotSupportedException("Shotgun requires its current caster position.");
            NativeBattleModelTransform targetPosition = inputs.ReadModel(frame, target).Transform ?? throw new NotSupportedException("Shotgun requires its current recipient position.");
            int x = targetPosition.Position28.X - casterPosition.Position28.X;
            int z = casterPosition.Position28.Z - targetPosition.Position28.Z;
            frame = WriteBasis281(frame, 0x2507F90, NativeBattleFunguarLaser277.Basis50CBA0(x, 0, z).Matrix);
            frame = WriteBasis281(frame, 0x2507FB0, NativeBattleFunguarLaser277.Basis50CBA0(-x, 0, -z).Matrix);
        }
        if (Read(frame, address).ReadByte(15) != 0)
        {
            uint caster = frame.State.ReadDword(0x2507F30);
            frame = inputs.Execute(frame, new NativeBattleShotgun281ComposeCaster5095B0(caster));
            (frame, int mainSurvivors) = AdvancePool(frame, 3, resources, inputs);
            frame = inputs.Execute(frame, new NativeBattleShotgun281RestoreCaster508C90(caster));
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            (frame, int particleSurvivors) = AdvancePool(frame, 1, resources, inputs);
            if (mainSurvivors == 0 && particleSurvivors == 0)
            {
                return (frame, 2);
            }
        }
        root = Read(frame, address);
        return (Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleShotgun281Frame Frame, int Result) AdvanceMain(NativeBattleShotgun281Frame frame, uint address, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if ((uint)age < 14)
        {
            frame = UpdateMuzzle67E500(frame, resources, inputs);
            NativeBattleActionMatrix caller = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), ReadPacked281(frame, 0x2507F90).ReadMatrix());
            (frame, _) = DrawProgramme67E2D0(frame, address, NativeBattlePackedMatrix32.FromMatrix(caller, null), 524, resources, inputs);
            if (age is 1 or 2)
            {
                frame = SpawnMuzzleParticles281(frame);
            }
            if (age == 1)
            {
                frame = inputs.Execute(frame, new NativeBattleShotgun281Flash5712C0(0, 1, 0, 96));
            }
        }
        if ((uint)(Signed(Read(frame, address), 12) - 4) < 8)
        {
            (frame, uint impact) = Append(frame, 0x2507F48, 0x67E6B0);
            if (impact != 0)
            {
                uint target = Read(frame, address).ReadDword(16);
                NativeBattleEffectArena retained = TaskView(Read(frame, impact), 12, 8);
                while (true)
                {
                    frame = DrawRandom(frame, out ushort random);
                    int count = inputs.ReadBoneCount(frame, target);
                    int selected = unchecked(random * count) >> 15;
                    NativeBattleShotgun281PointStep anchor = ReadAnchor67E550(frame, target, selected, retained, inputs);
                    frame = anchor.Frame;
                    if (anchor.Result == 0)
                    {
                        frame = WriteBytes(frame, impact + 12, anchor.Point8, 6);
                        break;
                    }
                }
                frame = Write(frame, impact, Read(frame, impact).WriteWord(18, 0));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, impact + 20, 312);
                workspace = workspace.Initialize7016B0(resources.ProgrammeB, 312);
                NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, impact + 20, workspace);
                frame = SpawnImpactParticles281(frame, impact);
            }
        }
        if (Read(frame, address).ReadWord(12) == 2)
        {
            frame = inputs.Execute(frame, new NativeBattleShotgun281Sound501330(resources.Sound, 0, 128));
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short next = unchecked((short)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        if (next >= 14)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Shotgun requires its current first physical contact.");
            frame = inputs.Execute(frame, new NativeBattleShotgun281Contact506690(first));
            return (frame, 2);
        }
        return (frame, 0);
    }

    private static NativeBattleShotgun281Frame SpawnMuzzleParticles281(NativeBattleShotgun281Frame frame)
    {
        for (int index = 0; index < 16; index++)
        {
            (frame, uint particle) = Append(frame, 0x2507F38, 0x67E5F0);
            if (particle == 0)
            {
                break;
            }
            NativeBattleModulePoolTask task = Read(frame, particle).WriteWord(16, unchecked((ushort)frame.State.ReadDword(0x2507FA4))).WriteWord(18, unchecked((ushort)frame.State.ReadDword(0x2507FA8))).WriteWord(20, unchecked((ushort)frame.State.ReadDword(0x2507FAC)));
            frame = DrawRandom(frame, out ushort angle);
            frame = DrawRandom(frame, out ushort radius);
            short x = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * (radius & 127)) >> 12));
            short y = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * (radius & 127)) >> 12));
            frame = DrawRandom(frame, out ushort depth);
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(ReadPacked281(frame, 0x2507F90).ReadMatrix(), new(x, y, unchecked((short)(-128 - (depth & 127)))));
            task = task.WriteDword(12, 0x10A3140).WriteWord(22, 0).WriteWord(30, 8);
            task = WriteVelocity281(task, velocity);
            frame = Write(frame, particle, task);
        }
        return frame;
    }

    private static NativeBattleShotgun281Frame SpawnImpactParticles281(NativeBattleShotgun281Frame frame, uint impact)
    {
        for (int index = 0; index < 8; index++)
        {
            (frame, uint particle) = Append(frame, 0x2507F38, 0x67E5F0);
            if (particle == 0)
            {
                break;
            }
            frame = WriteBytes(frame, particle + 16, TaskView(Read(frame, impact), 12, 8), 8);
            frame = DrawRandom(frame, out ushort first);
            frame = DrawRandom(frame, out ushort second);
            NativeBattleVector direction = NativeBattleRinoa89Controller.Direction6A6020(second, first);
            frame = DrawRandom(frame, out ushort radius);
            int scale = (radius & 63) + 32;
            NativeBattleVector velocity = new(unchecked((short)(unchecked(direction.X * scale) >> 11)), unchecked((short)(unchecked(direction.Y * scale) >> 11)), unchecked((short)(unchecked(direction.Z * scale) >> 11)));
            NativeBattleModulePoolTask task = Read(frame, particle).WriteDword(12, 0x10A3030).WriteWord(22, 0).WriteWord(30, 10);
            frame = Write(frame, particle, WriteVelocity281(task, velocity));
        }
        return frame;
    }

    private static NativeBattleModulePoolTask WriteVelocity281(NativeBattleModulePoolTask task, NativeBattleVector velocity)
    {
        short[] values = [velocity.X, velocity.Y, velocity.Z];
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(24 + axis * 2, unchecked((ushort)values[axis])).WriteWord(32 + axis * 2, unchecked((ushort)(-values[axis] >> 3)));
        }
        return task;
    }

    private static NativeBattleShotgun281Frame WriteBasis281(NativeBattleShotgun281Frame frame, uint address, NativeBattleActionMatrix matrix)
    {
        NativeBattleEffectArena view = frame.State.ReadView(address, 32);
        for (int index = 0; index < 9; index++)
        {
            view = view.Write(index * 2, BitConverter.GetBytes(matrix.Rotation[index]));
        }
        return frame with { State = frame.State.WriteView(address, view, 32) };
    }

    public static NativeBattleActionHeaderSlot Header(NativeBattleShotgun281Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(0x2507F2C) != 0x1D99A78)
        {
            throw new NotSupportedException("Shotgun requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Shotgun parameter header leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}

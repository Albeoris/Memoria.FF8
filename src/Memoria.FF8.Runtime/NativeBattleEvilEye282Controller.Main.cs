using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEvilEye282Controller
{
    private static (NativeBattleEvilEye282Frame Frame, int Result) AdvanceRoot(NativeBattleEvilEye282Frame frame, uint address, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, 0x2507EF4, unchecked(frame.State.ReadDword(0x2507F00) + (alternate ? 0x162Cu : 0x1162Cu)));
        frame = Dword(frame, 0x2507EFC, 0);
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, 0x2507EC0, 0x67D680);
            if (main == 0)
            {
                throw new InvalidDataException("Evil-Eye's freshly reset main pool must admit its sole task.");
            }
            frame = ClearDwords701200(frame, main + 12, 342);
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int count = Header(frame, packets).ReadByte(16);
            if (count > 3)
            {
                throw new NotSupportedException("Evil-Eye recipient pointers exceed the three physical rows before its programme workspace.");
            }
            frame = Write(frame, main, Read(frame, main).WriteWord(14, checked((ushort)count)));
            for (int index = 0; index < count; index++)
            {
                packets = inputs.ReadPackets(frame);
                int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Evil-Eye requires its current physical contact pointer.");
                uint model = ModelAddress(packets.ReadContact(checked(first + index)).Primary.ActorIndex);
                frame = Write(frame, main, Read(frame, main).WriteDword(16 + index * 4, model));
            }
            NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + 28, 1352);
            workspace = workspace.Initialize7016B0(resources.ProgrammeA, 1352);
            NativeBattleSelectedProgrammeVisuals.Allocated(frame.Effect.Pools, main + 28);
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + 28, workspace);
        }
        int mainSurvivors = 1;
        int targetSurvivors = 1;
        int particleSurvivors = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(inputs.ReadView(frame).ReadMatrix());
            NativeBattleEffectArena view = frame.State.ReadView(0x2507F08, 32);
            NativeBattleEffectArena source = PackedArena(NativeBattlePackedMatrix32.FromMatrix(normalized, null));
            view = NativeBattleEffectNodeContextMemory.Copy(source, 0, view, 0, 18);
            view = NativeBattleEffectNodeContextMemory.Copy(source, 20, view, 20, 12);
            frame = frame with { State = frame.State.WriteView(0x2507F08, view, 32) };
            (frame, mainSurvivors) = AdvancePool(frame, 3, resources, inputs);
            (frame, targetSurvivors) = AdvancePool(frame, 2, resources, inputs);
            (frame, particleSurvivors) = AdvancePool(frame, 1, resources, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEvilEye282Stage5713E0(unchecked((int)frame.State.ReadDword(0x2507EFC)), 0));
        if (Read(frame, address).ReadByte(15) != 0 && mainSurvivors == 0 && targetSurvivors == 0 && particleSurvivors == 0)
        {
            return (frame, 2);
        }
        root = Read(frame, address);
        return (Write(frame, address, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)))), 0);
    }

    private static (NativeBattleEvilEye282Frame Frame, int Result) AdvanceMain(NativeBattleEvilEye282Frame frame, uint address, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        int cue = age <= 41 ? 2200 : unchecked(unchecked((49 - age) * 275) << 3) >> 3;
        frame = Dword(frame, 0x2507EFC, unchecked((uint)cue));
        int time = age - 2;
        if ((uint)time < 26)
        {
            uint caster = frame.State.ReadDword(0x2507E98);
            short yaw = inputs.ReadModel(frame, caster).Rotation14 ?? throw new NotSupportedException("Evil-Eye requires its current caster yaw.");
            NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(unchecked((short)-yaw));
            NativeBattleEvilEye282MuzzleStep muzzle = UpdateMuzzle67DAC0(frame, resources, inputs);
            frame = muzzle.Frame;
            local = local with { X = muzzle.X, Y = muzzle.Y, Z = muzzle.Z };
            NativeBattleActionMatrix caller = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            (frame, _) = DrawProgramme67D820(frame, address, 28, 1352, NativeBattlePackedMatrix32.FromMatrix(caller, 0), resources, inputs);
        }
        if (time == 3)
        {
            frame = inputs.Execute(frame, new NativeBattleEvilEye282Flash5712C0(0, 1, 0, 96));
        }
        if (Read(frame, address).ReadWord(12) == 2)
        {
            frame = inputs.Execute(frame, new NativeBattleEvilEye282Sound501330(resources.Sound, 0, 128));
        }
        if (Read(frame, address).ReadWord(12) == 10)
        {
            for (int index = 0; index < Signed(Read(frame, address), 14); index++)
            {
                (frame, uint child) = Append(frame, 0x2507EB0, 0x67DB20);
                if (child == 0)
                {
                    continue;
                }
                uint model = Read(frame, address).ReadDword(16 + index * 4);
                frame = Write(frame, child, Read(frame, child).WriteDword(20, model));
                NativeBattleEvilEye282PointStep bone = inputs.ReadBone502170(frame, model, 0xF1, 0);
                frame = bone.Frame;
                frame = WriteBytes(frame, child + 12, bone.Point8, 6);
                frame = Write(frame, child, Read(frame, child).WriteWord(18, 0));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, child + 24, 384);
                workspace = workspace.Initialize7016B0(resources.ProgrammeB, 384);
                NativeBattleSelectedProgrammeVisuals.Allocated(frame.Effect.Pools, child + 24);
                NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, child + 24, workspace);
            }
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short next = unchecked((short)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)next)));
        if (next >= 49)
        {
            frame = inputs.Execute(frame, new NativeBattleEvilEye282ContactsAll506BA0(Parameters(frame).HeaderIndex7C));
            frame = Dword(frame, 0x2507EFC, 0);
            return (frame, 2);
        }
        return (frame, 0);
    }

    public static NativeBattleActionHeaderSlot Header(NativeBattleEvilEye282Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(0x2507E94) != 0x1D99A78)
        {
            throw new NotSupportedException("Evil-Eye requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Evil-Eye parameter header leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}

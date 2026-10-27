using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMegaSpark279Controller
{
    private static (NativeBattleMegaSpark279Frame Frame, int Result) AdvanceRoot(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(frame.State.ReadDword(ArenaGlobal) + (uint)(alternate ? FirstPacketOffset : SecondPacketOffset)));
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, 0x25080D0, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Mega Spark main reset did not provide its mandatory native slot.");
            }
            frame = ClearDwords701200(frame, main + 12, 714);
            for (int index = 0; index < 8; index++)
            {
                short sine = unchecked((short)NativeBattleCameraMath.Sine((index << 11) / 7));
                frame = WriteBytes(frame, ArenaAddress + (uint)(GeneratedSineOffset + index * 2), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(sine)), 2);
            }
            frame = ResetLinkedRows67F310(frame);
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int count = Header(frame, packets).ReadByte(16);
            frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count));
            for (int index = 0; index < count; index++)
            {
                packets = inputs.ReadPackets(frame);
                int first = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Mega Spark requires its actual physical contacts.");
                uint target = ModelAddress(packets.ReadContact(first + index).Primary.ActorIndex);
                NativeBattleModulePoolTask task = Read(frame, main).WriteDword(20 + index * 4, target);
                short z = ModelTransform279(inputs.ReadModel(frame, target)).Position28.Z;
                frame = Write(frame, main, task.WriteWord(16, unchecked((ushort)(Signed(task, 16) + z))));
            }
            NativeBattleModulePoolTask current = Read(frame, main);
            frame = Write(frame, main, current.WriteWord(16, unchecked((ushort)(Signed(current, 16) / count))));
            NativeBattleModelTransform transform = ModelTransform279(inputs.ReadModel(frame, frame.State.ReadDword(CasterGlobal)));
            frame = WriteBytes(frame, main + 32, PointView(transform.Position28, transform.Vertical34), 8);
        }
        int mains = 1;
        int attachments = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, _) = AdvancePool(frame, 4, resources, inputs);
            uint caster = frame.State.ReadDword(CasterGlobal);
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279ComposeCaster5095B0(caster));
            (frame, mains) = AdvancePool(frame, 6, resources, inputs);
            (frame, attachments) = AdvancePool(frame, 5, resources, inputs);
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279RestoreCaster508C90(caster));
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            (frame, _) = AdvancePool(frame, 3, resources, inputs);
            (frame, _) = AdvancePool(frame, 1, resources, inputs);
        }
        if (Read(frame, address).ReadByte(15) != 0 && mains == 0 && attachments == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }

    public static NativeBattleMegaSpark279Frame ResetLinkedRows67F310(NativeBattleMegaSpark279Frame frame)
    {
        for (int index = 0; index < LinkedRowCapacity; index++)
        {
            frame = WriteBytes(frame, ArenaAddress + (uint)(LinkedRowsOffset + index * LinkedRowStride + 6), NativeBattleEffectArena.Clear().Write(0, new byte[] { 255, 255 }), 2);
        }
        return WriteBytes(frame, ArenaAddress + LinkedHintOffset, NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
    }

    public static (NativeBattleMegaSpark279Frame Frame, int Result) AdvanceMain(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        uint caster = frame.State.ReadDword(CasterGlobal);
        frame = inputs.Execute(frame, new NativeBattleMegaSpark279ModelFields(caster, 0, 0x1000, 128));
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        if ((uint)age < 32)
        {
            int delta = unchecked(Signed(task, 36) - Signed(task, 16) + 5000);
            short z = unchecked((short)(Signed(task, 36) - (int)(unchecked((uint)(delta * age)) >> 5)));
            short rotation = unchecked((short)(unchecked((uint)(age << 7)) >> 5));
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279ModelFields(caster, 0, 0, rotation, z));
            if ((uint)age >= 15 && (uint)age < 31)
            {
                if (age == 15)
                {
                    for (int index = 2; index >= 0; index--)
                    {
                        frame = InitializeProgramme279(frame, address + (uint)(0x75C + index * 328), resources.ProgrammeB, ProgrammeBWorkspaceLength);
                    }
                }
                for (int index = 2; index >= 0; index--)
                {
                    (frame, NativeBattleFullMacVertex point) = ReadCasterPoint680C00(frame, SourcePointsAddress + (uint)(index * 8), resources, inputs);
                    short rotationX = inputs.ReadModel(frame, caster).Rotation12 ?? throw new NotSupportedException("Mega Spark requires its actual current caster pitch.");
                    NativeBattleEffectArena caller = ProgrammeCaller279(frame, point, NativeBattleGfMatrices.Euler701310(new(rotationX, 0, 0)), inputs);
                    (frame, _) = DrawProgramme(frame, address, 0x75C + index * 328, ProgrammeBWorkspaceLength, resources.ProgrammeB, caller, resources, inputs);
                }
            }
            if ((age & 3) == 0)
            {
                int row = (age >> 2) & 7;
                uint row100 = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, caster));
                for (int index = 0; index < 3; index++)
                {
                    (frame, _) = BirthAttachment6804B0(frame, row100, AttachmentTablesAddress + (uint)(index * 192 + row * 24), SourcePointsAddress + (uint)(index * 8), 0, 12, 7, resources, inputs);
                }
            }
        }
        int programmePhase = unchecked(age - 20);
        if ((uint)programmePhase < 40)
        {
            if (programmePhase >= 4)
            {
                if (programmePhase == 4)
                {
                    frame = InitializeProgramme279(frame, address + 40, resources.ProgrammeA, ProgrammeAWorkspaceLength);
                }
                (frame, NativeBattleFullMacVertex point) = ReadCasterPoint680C00(frame, ExtraPointAddress, resources, inputs);
                NativeBattleEffectArena caller = ProgrammeCaller279(frame, point, NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), inputs);
                (frame, _) = DrawProgramme(frame, address, 40, ProgrammeAWorkspaceLength, resources.ProgrammeA, caller, resources, inputs);
            }
            if ((programmePhase & 7) == 0)
            {
                uint row100 = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, caster));
                for (int index = 0; index < 3; index++)
                {
                    (frame, _) = BirthAttachment6804B0(frame, row100, SourcePointsAddress + (uint)(index * 8), ExtraPointAddress, 1, 14, 7, resources, inputs);
                }
            }
        }
        int targetPhase = unchecked(age - 40);
        if ((uint)targetPhase < 15)
        {
            int count = Signed(Read(frame, address), 14);
            int ordinal = checked((int)((uint)targetPhase % (uint)count));
            uint target = Read(frame, address).ReadDword(20 + ordinal * 4);
            if (targetPhase == 0)
            {
                frame = inputs.Execute(frame, new NativeBattleMegaSpark279Flash5712C0(0, 1, 2, 128));
            }
            (frame, NativeBattleFullMacVertex start) = ReadCasterPoint680C00(frame, ExtraPointAddress, resources, inputs);
            NativeBattleStackBytes stack = frame.StackBytes ?? throw new NotSupportedException("Mega Spark requires the current composed stack observation for its packed start point.");
            NativeBattleEffectArena startPoint = PointView(new(unchecked((short)start.X), unchecked((short)start.Y), unchecked((short)start.Z)), unchecked((short)stack.ReadUInt16(-0x76)));
            NativeBattleMegaSpark279PointStep endpoint;
            if (targetPhase >= 4 && targetPhase <= count + 4)
            {
                endpoint = inputs.ReadAnchor571400(frame, target);
            }
            else
            {
                frame = DrawRandom(frame, out ushort selected);
                int bone = unchecked(selected * inputs.ReadBoneCount(frame, target)) >> 15;
                endpoint = inputs.ReadBone502170(frame, target, bone, 0);
            }
            frame = endpoint.Frame;
            frame = DrawRandom(frame, out ushort randomX);
            frame = DrawRandom(frame, out ushort randomY);
            frame = DrawRandom(frame, out ushort randomZ);
            NativeBattleEffectArena direction = PointView(new(unchecked((short)((randomX & 255) - 128)), unchecked((short)(-64 - (randomY & 127))), unchecked((short)((randomZ & 63) + 64))));
            (frame, _) = BirthRow67FA80(frame, startPoint, endpoint.Point8, direction, null, 0, 0, 512, 1, 2, resources, inputs);
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279Sound501330(resources.Sound, 0, 128));
        }
        if (age == 48)
        {
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279Flash5712C0(0, 1, 0, 128));
        }
        else if (age == 50)
        {
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279Flash5712C0(0, 1, 0, 64));
        }
        else if (age == 58)
        {
            NativeBattleActionHeaderSlot header = Header(frame, inputs.ReadPackets(frame));
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Mega Spark requires its actual current contact base.");
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279Contact506BA0(first, header.ReadByte(16)));
        }
        int remaining = unchecked(85 - age);
        if ((uint)remaining < 16)
        {
            NativeBattleModulePoolTask current = Read(frame, address);
            int delta = unchecked(Signed(current, 36) - Signed(current, 16) + 5000);
            short z = unchecked((short)(Signed(current, 36) - (int)(unchecked((uint)(delta * remaining)) >> 4)));
            short rotation = unchecked((short)(unchecked((uint)(remaining << 7)) >> 4));
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279ModelFields(caster, 0, 0, rotation, z));
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) >= 85)
        {
            frame = inputs.Execute(frame, new NativeBattleMegaSpark279ModelFields(caster, 0x1000, 0, 0, Signed(task, 36)));
            return (frame, 2);
        }
        return (frame, 0);
    }

    private static NativeBattleMegaSpark279Frame InitializeProgramme279(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleGfAnimationSource source, int length)
    {
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address, length);
        workspace = workspace.Initialize7016B0(source, length);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address, workspace);
        return frame;
    }

    private static NativeBattleEffectArena ProgrammeCaller279(NativeBattleMegaSpark279Frame frame, NativeBattleFullMacVertex point, NativeBattleActionMatrix rotation, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), rotation with { X = point.X, Y = point.Y, Z = point.Z });
        return PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
    }

    public static (NativeBattleMegaSpark279Frame Frame, NativeBattleFullMacVertex Point) ReadCasterPoint680C00(NativeBattleMegaSpark279Frame frame, uint sourceAddress, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        var bytes = resources.ReadStatic(sourceAddress, 8);
        int bone = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(6));
        uint row100 = NativeBattleModelResourceRow.Address100(inputs.ReadModel(frame, frame.State.ReadDword(CasterGlobal)));
        frame = LoadMatrix(frame, inputs.ReadCurrentBoneMatrix(frame, row100, bone));
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(4))), unchecked((short)bone));
        registers = NativeBattleGeometry.Transform(registers);
        NativeBattleFullMacVertex point = new(registers.Colors.Mac[0] ?? throw new NotSupportedException("Mega Spark point lacks its current MAC1."), registers.Colors.Mac[1] ?? throw new NotSupportedException("Mega Spark point lacks its current MAC2."), registers.Colors.Mac[2] ?? throw new NotSupportedException("Mega Spark point lacks its current MAC3."), registers.Colors.Flags ?? throw new NotSupportedException("Mega Spark point lacks its current transform flags."));
        return (frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, point);
    }

    private static NativeBattleModelTransform ModelTransform279(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Mega Spark requires its actual model transform.");

    public static NativeBattleActionHeaderSlot Header(NativeBattleMegaSpark279Frame frame, NativeBattleActionPacketsState packets)
    {
        if (frame.State.ReadDword(ParametersGlobal) != 0x1D99A78)
        {
            throw new NotSupportedException("Mega Spark requires its actual current parameter pointer.");
        }
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Mega Spark parameter header leaves its actual arena.");
        }
        return packets.Headers[index];
    }
}

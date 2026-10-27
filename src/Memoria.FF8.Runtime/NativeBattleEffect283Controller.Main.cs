using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect283Controller
{
    private static (NativeBattleEffect283Frame Frame, int Result) AdvanceRoot(NativeBattleEffect283Frame frame, uint address, NativeBattleEffect283Resources resources, NativeBattleEffect283Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        bool alternate = root.ReadDword(0x10) != 0;
        frame = Dword(frame, CursorGlobal, checked(frame.State.ReadDword(ArenaGlobal) + (uint)(alternate ? FirstPacketOffset : SecondPacketOffset)));
        frame = Write(frame, address, Read(frame, address).WriteDword(0x10, alternate ? 0u : 1u));
        int age = Signed(root, 0xC);
        if (age == 1 && root.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(0xF, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, MainDescriptor, MainCallback);
            frame = ClearDwords701200(frame, main + 0xC, 0x18F);
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int header = Parameters(frame).HeaderIndex7C;
            if ((uint)header >= packets.Headers.Length)
            {
                throw new InvalidDataException("Effect283 requires its actual current action header.");
            }
            int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect283 requires its current first contact pointer.");
            uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
            frame = Write(frame, main, Read(frame, main).WriteDword(0x28, target));
            frame = inputs.Execute(frame, new NativeBattleEffect283ModelFlags(target, 0, 4));
            foreach ((NativeBattleGfAnimationSource source, int offset, int length) in new[] { (resources.MainProgramme, MainProgrammeWorkspaceOffset, MainProgrammeWorkspaceLength), (resources.SecondaryProgramme, SecondaryProgrammeWorkspaceOffset, SecondaryProgrammeWorkspaceLength) })
            {
                NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[length].ToImmutableArray()).Initialize7016B0(source, length);
                frame = WriteBytes(frame, main + (uint)offset, workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), length);
            }
            NativeBattleEffect283PointStep anchor = inputs.ReadAnchor571400(frame, target);
            frame = anchor.Frame;
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect283 requires its current target transform.");
            NativeBattleModulePoolTask task = CopyTask(Read(frame, main), 0x20, anchor.Point8, 8);
            task = task.WriteWord(0x18, BinaryPrimitives.ReadUInt16LittleEndian(anchor.Point8.Read(0, 2))).WriteWord(0x1A, unchecked((ushort)transform.Angle36)).WriteWord(0x1C, BinaryPrimitives.ReadUInt16LittleEndian(anchor.Point8.Read(4, 2))).WriteDword(0x14, model.Color44 ?? throw new NotSupportedException("Effect283 requires its original target Color44.")).WriteDword(0x10, unchecked((uint)(int)transform.Scale38));
            frame = Write(frame, main, task);
        }
        if (Read(frame, address).ReadByte(0xF) != 0)
        {
            (frame, int survivors) = AdvancePool(frame, 1, resources, inputs);
            if (survivors == 0)
            {
                return (inputs.Execute(frame, new NativeBattleEffect283Stage5713E0(0, 0)), 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1)))), 0);
    }

    public static (NativeBattleEffect283Frame Frame, int Result) AdvanceMain(NativeBattleEffect283Frame frame, uint address, NativeBattleEffect283Resources resources, NativeBattleEffect283Inputs inputs)
    {
        int age = Signed(Read(frame, address), 0xC);
        if ((uint)age >= 60)
        {
            throw new NotSupportedException("Effect283 main requires its natural native age domain.");
        }
        int phase = age * 7 * 4096 / 60;
        int index = phase >> 12;
        int fraction = phase & 4095;
        NativeBattleWeightedColorStep color = NativeBattleWeightedColor56CC00.Blend(frame.Memory, resources.Palette8[index], resources.Palette8[index + 1], (short)(4096 - fraction), (short)fraction, address, inputs.Colors);
        frame = frame with { Memory = color.Memory };
        uint flags = age < 8 || age >= 52 ? 0u : color.Color;
        int stage = age < 8 ? 275 * age : age >= 52 ? 275 * (60 - age) : 2200;
        frame = inputs.Execute(frame, new NativeBattleEffect283Stage5713E0(stage, flags));
        frame = DrawProgramme283(frame, address, MainProgrammeWorkspaceOffset, MainProgrammeWorkspaceLength, resources.MainProgramme, ProgrammeCaller283(frame, address, 0x18, (ushort)((age & 15) << 3)), inputs).Frame;
        uint relative = unchecked((uint)(age - 24));
        uint target = Read(frame, address).ReadDword(0x28);
        if (relative < 36)
        {
            if (relative < 20)
            {
                int bank = 20 - (int)relative;
                int sine = NativeBattleCameraMath.Sine(bank * 1024 / 20);
                uint saved = Read(frame, address).ReadDword(0x14);
                NativeBattleGeometryRegisterStep registers = Registers283(frame);
                registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - sine)) } }, (ushort)(byte)saved, (ushort)(byte)(saved >> 8), (ushort)(byte)(saved >> 16));
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
                uint faded = 0x32000000u;
                for (int axis = 0; axis < 3; axis++)
                {
                    faded |= (uint)unchecked((byte)registers.Colors.Ir[axis]!.Value) << (axis * 8);
                }
                frame = inputs.Execute(Publish283(frame, registers), new NativeBattleEffect283ModelColor44(target, faded));
                frame = LoadMatrix(frame, inputs.ReadView(frame));
                NativeBattleEffectArena point = TaskView(Read(frame, address), 0x20, 8);
                registers = NativeBattleGeometry.LoadVector(Registers283(frame), 0, Vector283(point), W283(point));
                var controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect283 requires its current projection controls.");
                registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                frame = Publish283(frame, registers);
                int depth = unchecked((int)registers.Geometry.Sz[3]!.Value) >> 2;
                if (unchecked((ushort)depth) > 0)
                {
                    uint screen = registers.Geometry.Sxy[2]!.Value;
                    NativeBattleEffect283BodyResult body = inputs.DrawBody124(frame, target, unchecked((short)screen), unchecked((short)(screen >> 16)), Signed(Read(frame, address), 0xE), bank, sine);
                    frame = Write(body.Frame, address, Read(body.Frame, address).WriteWord(0xE, unchecked((ushort)body.MaxDeviation)));
                }
            }
            if (relative == 20)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect283ModelFlags(target, 4, 0));
                frame = inputs.Execute(frame, new NativeBattleEffect283ModelColor44(target, Read(frame, address).ReadDword(0x14)));
            }
            frame = DrawProgramme283(frame, address, SecondaryProgrammeWorkspaceOffset, SecondaryProgrammeWorkspaceLength, resources.SecondaryProgramme, ProgrammeCaller283(frame, address, 0x20, 0), inputs).Frame;
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect283Sound501330(resources.Sound, 0, 128));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1))));
        return (frame, age + 1 >= 60 ? 2 : 0);
    }

    private static NativeBattleEffectArena ProgrammeCaller283(NativeBattleEffect283Frame frame, uint task, int pointOffset, ushort window)
    {
        NativeBattleModulePoolTask main = Read(frame, task);
        NativeBattleEffectArena caller = NativeBattleEffectNodeContextMemory.Copy(TaskView(main, pointOffset, 8), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, 8);
        for (int offset = 8; offset <= 16; offset += 4)
        {
            caller = caller.Write(offset, BitConverter.GetBytes(main.ReadDword(0x10)));
        }
        return caller.Write(20, BitConverter.GetBytes(1)).Write(24, BitConverter.GetBytes((short)-512)).Write(26, BitConverter.GetBytes(window)).Write(28, BitConverter.GetBytes(checked(frame.State.ReadDword(ArenaGlobal) + ScratchOffset)));
    }

    public static (NativeBattleEffect283Frame Frame, int Remaining) DrawProgramme283(NativeBattleEffect283Frame frame, uint taskAddress, int workspaceOffset, int workspaceBytes, NativeBattleGfAnimationSource source, NativeBattleEffectArena caller32, NativeBattleEffect283Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask task = Read(frame, taskAddress);
            task.RequireKnownPayload(workspaceOffset, workspaceBytes);
            return NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (workspaceOffset - 12), workspaceBytes);
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, taskAddress + (uint)workspaceOffset, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), workspaceBytes);
            frame = inputs.DrawProgrammeNode(frame, source, node, caller32);
            return Workspace();
        });
        frame = WriteBytes(frame, taskAddress + (uint)workspaceOffset, advanced.Workspace.CopyTo(NativeBattleEffectArena.Clear(), 0), workspaceBytes);
        return (frame, advanced.Remaining);
    }

    public static NativeBattleGeometryRegisterStep Registers283(NativeBattleEffect283Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect283 requires its current geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect283 requires its current arithmetic owner."));
    public static NativeBattleVector Vector283(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
    public static short? W283(NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    public static NativeBattleEffect283Frame Publish283(NativeBattleEffect283Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
}

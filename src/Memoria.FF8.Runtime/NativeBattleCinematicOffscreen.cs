using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicOffscreenStageRequest(int Group, uint ContextAddress, uint OrderingAddress, byte DepthShift, uint OutputAddress, uint MatrixAddress);
public sealed record NativeBattleCinematicOffscreenStageStep(NativeBattleWaterExecutionFrame Frame, uint OutputAddress);
public sealed record NativeBattleCinematicOffscreenServices(
    Func<NativeBattleWaterExecutionFrame, byte> ReadBuffer80,
    Func<NativeBattleWaterExecutionFrame, byte, ImmutableArray<byte>> ReadDrawingEnvironment,
    Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> SetDebug45B580,
    Func<NativeBattleWaterExecutionFrame, ushort, NativeBattleWaterExecutionFrame> SetFocalLength,
    Func<NativeBattleWaterExecutionFrame, ushort, uint> ResolveMatrixB65520,
    Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> ProjectAnchor,
    Func<NativeBattleWaterExecutionFrame, uint, int, NativeBattleWaterExecutionFrame> ClearOrdering45D530,
    Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> InsertPacket45C7A0,
    Func<NativeBattleWaterExecutionFrame, NativeBattleCinematicOffscreenStageRequest, NativeBattleCinematicOffscreenStageStep> DrawStageGroup,
    Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> ExecuteSoftwareOrdering45CEE0,
    Func<NativeBattleWaterExecutionFrame, int> ReadCurrentEffect);

/// <summary>The cinematic private stage-capture descriptors and software ordering-table passes.</summary>
public static class NativeBattleCinematicOffscreen
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicOffscreenServices services)
    {
        int command = frame.ReadTemporaryWord(0x14A) & 0x1FF;
        if (command is not (0x4F or 0x90))
        {
            throw new NotSupportedException("Native offscreen capture requires its selected command.");
        }
        uint advance = command == 0x4F ? 8u : 10u;
        uint control = CoreDword(frame, 0x27973BC);
        if (BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(unchecked(control + 0x30), 2)) != 0)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, advance);
        }
        if (command == 0x4F)
        {
            frame = frame.WriteTemporaryWord(0xD4, Operand(4));
            frame = frame.WriteTemporaryWord(0xD6, Operand(6));
        }
        else
        {
            for (uint offset = 2; offset <= 8; offset += 2)
            {
                frame = frame.WriteTemporaryWord(0xCE + (int)offset, Operand(offset));
            }
        }
        uint descriptor = ManagerWord74(frame);
        frame = ArenaDword(frame, descriptor, 0);
        frame = ArenaDword(frame, unchecked(descriptor + 4), 0);
        uint size;
        if (command == 0x4F)
        {
            ushort flags = Operand(2);
            size = 0xEC;
            if ((flags & 1) != 0)
            {
                frame = ArenaDword(frame, unchecked(descriptor + 4), unchecked(ManagerWord74(frame) + 0xEC));
                size = 0x40EC;
            }
            if ((flags & 2) != 0)
            {
                frame = ArenaDword(frame, descriptor, unchecked(ManagerWord74(frame) + size));
                size = unchecked(size + 0x1000);
            }
            frame = ArenaDword(frame, unchecked(descriptor + 8), unchecked(ManagerWord74(frame) + size));
            size = unchecked(size + 0x5DC0);
        }
        else
        {
            frame = ArenaDword(frame, descriptor, unchecked(ManagerWord74(frame) + 0xEC));
            frame = ArenaDword(frame, unchecked(descriptor + 8), unchecked(ManagerWord74(frame) + 0x10EC));
            size = 0x5BEC;
        }
        frame = ArenaDword(frame, unchecked(descriptor + 0xC), size);
        frame = WriteSlotDword(frame, 0xC4, size);
        frame = Allocate(frame, ReadArenaDword(frame, unchecked(descriptor + 0xC))).Frame;
        frame = ArenaDword(frame, unchecked(descriptor + 0x14), ManagerWord74(frame));
        if (command == 0x4F)
        {
            frame = services.ProjectAnchor(frame);
            frame = frame.WriteTemporaryDword(0xF0, descriptor);
            frame = Render4F(frame, descriptor, services);
        }
        else
        {
            frame = Render90(frame, descriptor, services);
        }
        return NativeBattleScriptedCommandReads.Advance(frame, advance);

        ushort Operand(uint offset) => BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, offset, 2));
    }

    public static NativeBattleWaterExecutionFrame Render4F(NativeBattleWaterExecutionFrame frame, uint descriptor, NativeBattleCinematicOffscreenServices services)
    {
        frame = services.SetDebug45B580(frame);
        frame = frame.WriteTemporaryDword(0xE4, ReadArenaDword(frame, unchecked(descriptor + 8)));
        uint context = unchecked(descriptor + 0x10);
        frame = Context(frame, context, unchecked((short)(frame.ReadTemporaryWord(0xD0) - 0x60)), unchecked((short)(frame.ReadTemporaryWord(0xD2) - 0x80)), 192, 256);
        uint matrix = services.ResolveMatrixB65520(frame, 0);
        frame = frame.WriteTemporaryDword(0xE8, matrix);
        ImmutableArray<byte> source = AlternateEnvironment(frame, services);
        uint capture = unchecked(descriptor + 0x50);
        frame = CopyEnvironment(frame, capture, source);
        frame = ArenaDword(frame, capture, frame.ReadTemporaryDword(0xD4));
        frame = ArenaDword(frame, unchecked(capture + 4), 0x010000C0);
        frame = ArenaWord(frame, unchecked(capture + 8), unchecked((ushort)(frame.ReadTemporaryWord(0xD4) - frame.ReadTemporaryWord(0xD0) + 0x60)));
        frame = ArenaWord(frame, unchecked(capture + 0xA), unchecked((ushort)(frame.ReadTemporaryWord(0xD6) - frame.ReadTemporaryWord(0xD2) + 0x80)));
        frame = ArenaByte(frame, unchecked(capture + 0x18), 1);
        frame = BuildEnvironmentPacket(frame, unchecked(capture + 0x1C), Prefix(frame, capture));
        frame = BuildEnvironmentPacket(frame, unchecked(descriptor + 0xAC), source);
        frame = Pass(frame, ReadArenaDword(frame, unchecked(descriptor + 4)), 4096, [2, 3], 4, 0x3FC0);
        return Pass(frame, ReadArenaDword(frame, descriptor), 1024, [0, 1], 6, 0xFC0);

        NativeBattleWaterExecutionFrame Pass(NativeBattleWaterExecutionFrame current, uint ordering, int entries, int[] groups, byte shift, uint captureOffset)
        {
            current = current.WriteTemporaryDword(0xE0, ordering);
            if (ordering == 0)
            {
                return current;
            }
            current = services.ClearOrdering45D530(current, ordering, entries);
            current = services.InsertPacket45C7A0(current, unchecked(current.ReadTemporaryDword(0xE0) + 0x40), unchecked(descriptor + 0xAC));
            foreach (int group in groups)
            {
                NativeBattleCinematicOffscreenStageStep step = services.DrawStageGroup(current, new(group, context, current.ReadTemporaryDword(0xE0), shift, current.ReadTemporaryDword(0xE4), current.ReadTemporaryDword(0xE8)));
                current = step.Frame.WriteTemporaryDword(0xE4, step.OutputAddress);
            }
            current = services.InsertPacket45C7A0(current, unchecked(current.ReadTemporaryDword(0xE0) + captureOffset), unchecked(descriptor + 0x6C));
            uint storedOrdering = ReadArenaDword(current, unchecked(descriptor + (entries == 4096 ? 4u : 0u)));
            return services.ExecuteSoftwareOrdering45CEE0(current, unchecked(storedOrdering + (uint)((entries - 1) * 4)));
        }
    }

    public static NativeBattleWaterExecutionFrame Render90(NativeBattleWaterExecutionFrame frame, uint descriptor, NativeBattleCinematicOffscreenServices services)
    {
        frame = services.SetDebug45B580(frame);
        frame = services.SetFocalLength(frame, 256);
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CD50(new(unchecked((short)SlotWord(frame, 0x8C)), unchecked((short)SlotWord(frame, 0x8E)), unchecked((short)SlotWord(frame, 0x90))));
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(0xB0 + lane * 2, unchecked((ushort)euler.Rotation[lane]));
        }
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xC2, false).SetItem(0xC3, false) } };
        frame = frame.WriteTemporaryDword(0xC4, unchecked((uint)-(int)(short)SlotWord(frame, 0x94)));
        frame = frame.WriteTemporaryDword(0xC8, unchecked((uint)-(int)(short)SlotWord(frame, 0x98)));
        frame = frame.WriteTemporaryDword(0xCC, unchecked((uint)(int)(short)SlotWord(frame, 0x96)));
        frame = frame.WriteTemporaryDword(0xE4, ReadArenaDword(frame, unchecked(descriptor + 8)));
        uint context = unchecked(descriptor + 0x10);
        frame = Context(frame, context, 0, 0, 320, 256);
        ImmutableArray<byte> source = AlternateEnvironment(frame, services);
        uint capture = unchecked(descriptor + 0x50);
        frame = CopyEnvironment(frame, capture, source);
        frame = ArenaDword(frame, capture, frame.ReadTemporaryDword(0xD0));
        frame = ArenaDword(frame, unchecked(capture + 4), frame.ReadTemporaryDword(0xD4));
        frame = ArenaWord(frame, unchecked(capture + 8), unchecked((ushort)(frame.ReadTemporaryWord(0xD0) - 32)));
        frame = ArenaWord(frame, unchecked(capture + 0xA), frame.ReadTemporaryWord(0xD2));
        frame = ArenaByte(frame, unchecked(capture + 0x18), 1);
        if (services.ReadCurrentEffect(frame) == 204)
        {
            frame = ArenaByte(frame, unchecked(capture + 0x1B), 8);
        }
        frame = BuildEnvironmentPacket(frame, unchecked(capture + 0x1C), Prefix(frame, capture));
        frame = BuildEnvironmentPacket(frame, unchecked(descriptor + 0xAC), source);
        frame = frame.WriteTemporaryDword(0xE0, ReadArenaDword(frame, descriptor));
        frame = services.ClearOrdering45D530(frame, frame.ReadTemporaryDword(0xE0), 1024);
        frame = services.InsertPacket45C7A0(frame, unchecked(frame.ReadTemporaryDword(0xE0) + 0x40), unchecked(descriptor + 0xAC));
        NativeBattleCinematicOffscreenStageStep draw = services.DrawStageGroup(frame, new(1, context, frame.ReadTemporaryDword(0xE0), 6, frame.ReadTemporaryDword(0xE4), unchecked(frame.TemporaryFrame.Address + 0xB0)));
        frame = draw.Frame.WriteTemporaryDword(0xE4, draw.OutputAddress);
        frame = services.InsertPacket45C7A0(frame, unchecked(frame.ReadTemporaryDword(0xE0) + 0xFC0), unchecked(descriptor + 0x6C));
        return services.ExecuteSoftwareOrdering45CEE0(frame, unchecked(ReadArenaDword(frame, descriptor) + 0xFFC));
    }

    private static NativeBattleWaterExecutionFrame Context(NativeBattleWaterExecutionFrame frame, uint context, short x, short y, ushort width, ushort height)
    {
        frame = ArenaWord(frame, unchecked(context + 0x14), unchecked((ushort)x));
        frame = ArenaWord(frame, unchecked(context + 0x16), unchecked((ushort)y));
        frame = ArenaDword(frame, unchecked(context + 0x18), width | ((uint)height << 16));
        frame = ArenaDword(frame, unchecked(context + 0x1C), 0x808080);
        frame = ArenaDword(frame, unchecked(context + 0x28), 0);
        frame = ArenaWord(frame, unchecked(context + 0x24), 0);
        frame = ArenaDword(frame, unchecked(context + 0x20), uint.MaxValue);
        return ArenaDword(frame, unchecked(context + 0x10), uint.MaxValue);
    }

    private static ImmutableArray<byte> AlternateEnvironment(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicOffscreenServices services)
    {
        byte buffer = services.ReadBuffer80(frame);
        ImmutableArray<byte> source = services.ReadDrawingEnvironment(frame, unchecked((byte)((buffer - 1) & 1)));
        if (source.IsDefault || source.Length < 28)
        {
            throw new InvalidDataException("Native offscreen capture requires its alternate environment's produced prefix.");
        }
        return source;
    }

    private static NativeBattleWaterExecutionFrame CopyEnvironment(NativeBattleWaterExecutionFrame frame, uint address, ImmutableArray<byte> source) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 28), source.AsSpan().Slice(0, 28)) };
    private static ImmutableArray<byte> Prefix(NativeBattleWaterExecutionFrame frame, uint address) => frame.Arena.Read(ArenaOffset(address, 28), 28).ToImmutableArray();

    private static NativeBattleWaterExecutionFrame BuildEnvironmentPacket(NativeBattleWaterExecutionFrame frame, uint packetAddress, ImmutableArray<byte> prefix)
    {
        byte[] environment = new byte[92];
        prefix.AsSpan().Slice(0, 28).CopyTo(environment);
        ImmutableArray<byte> packet = NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), environment.ToImmutableArray());
        int length = prefix[0x18] != 0 ? 36 : 24;
        return frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(packetAddress + 3), length - 3), packet.AsSpan().Slice(3, length - 3)) };
    }

    private static (NativeBattleWaterExecutionFrame Frame, uint Address) Allocate(NativeBattleWaterExecutionFrame frame, uint size)
    {
        uint address = ManagerWord74(frame);
        uint aligned = (size & 3) == 0 ? size : unchecked(size + 4) & 0xFFFFFFFC;
        uint manager = CoreDword(frame, 0x27973EC);
        return (WriteCoreDword(frame, unchecked(manager + 0x74), unchecked(address + aligned)), address);
    }

    private static uint ManagerWord74(NativeBattleWaterExecutionFrame frame) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
    private static uint ReadArenaDword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    private static NativeBattleWaterExecutionFrame ArenaByte(NativeBattleWaterExecutionFrame frame, uint address, byte value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), [value]) };
    private static NativeBattleWaterExecutionFrame ArenaWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
    private static NativeBattleWaterExecutionFrame ArenaDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
}

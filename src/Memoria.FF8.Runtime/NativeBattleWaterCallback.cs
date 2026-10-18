using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterCallbackInput(NativeBattleWater222Resources Resources, ushort GraphicsFlags, ImmutableArray<byte> ViewMatrix, uint OrderingTablePointer, uint CommonCursor, short Focal);
public sealed record NativeBattleWaterCallbackServices(Func<NativeBattleWaterExecutionFrame, uint, ushort> ReadInstruction, Func<NativeBattleWaterExecutionFrame, ushort, NativeBattleWaterExecutionFrame> ExecuteInstruction, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> UpdateSlot, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> DrawSlot, Func<NativeBattleWaterExecutionFrame, uint, int, NativeBattleWaterExecutionFrame> ClearOrderingTable, Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> PublishFocal);
public sealed record NativeBattleWaterCallbackResult(NativeBattleWaterExecutionFrame Frame, uint CommonCursor, uint TaskResult);

/// <summary>The callback order around explicit proved command, update, draw and graphics services.</summary>
public static class NativeBattleWaterCallback
{
    public static NativeBattleWaterCallbackResult RunA9B870(NativeBattleWaterExecutionFrame frame, NativeBattleWaterCallbackInput input, NativeBattleWaterCallbackServices services, int instructionBudget = 4096, int traversalBudget = 256)
    {
        input.Resources.Validate();
        if (input.ViewMatrix.IsDefault || input.ViewMatrix.Length != 32 || traversalBudget <= 0)
        {
            throw new InvalidDataException("Native effect callback requires its current view and bounded diagnostic traversal.");
        }
        ArgumentNullException.ThrowIfNull(services.ReadInstruction);
        ArgumentNullException.ThrowIfNull(services.ExecuteInstruction);
        ArgumentNullException.ThrowIfNull(services.UpdateSlot);
        ArgumentNullException.ThrowIfNull(services.DrawSlot);
        ArgumentNullException.ThrowIfNull(services.ClearOrderingTable);
        ArgumentNullException.ThrowIfNull(services.PublishFocal);
        frame = NativeBattleWaterFrameBinding.Bind(frame, input.Resources.Profile);
        uint manager = Manager(frame);
        frame = WriteCoreWord(frame, manager + 0x32, unchecked((ushort)(CoreWord(frame, manager + 0x32) + 1)));
        frame = frame.WriteTemporaryByte(0x141, (byte)(CoreByte(frame, manager + 0x32) & 1));
        frame = frame with { Random = frame.Random.Draw(out _) };
        if ((CoreWord(frame, manager) & 0x0400) != 0)
        {
            uint context = CoreDword(frame, 0x27973BC);
            frame = WriteCoreDword(frame, context + 0x2C, unchecked(input.OrderingTablePointer + 0xC));
            frame = WriteCoreWord(frame, context + 0x24, 0);
        }
        frame = WriteCoreByte(frame, manager + 0x35, (input.GraphicsFlags & 1) != 0 ? (byte)0xFF : (byte)0);
        frame = frame.WriteTemporaryByte(0x140, frame.ReadTemporaryByte(0x141));
        frame = frame.WriteTemporaryDword(0x138, unchecked(input.OrderingTablePointer + 0x44));
        byte[] view = input.ViewMatrix.ToArray();
        view[0x12] = 0;
        view[0x13] = 0;
        frame = frame with { Core = frame.Core.Write(0x27979E8, view), ModuleViewBackup = view.ToImmutableArray() };
        frame = WriteCoreDword(frame, manager + 0x7C, input.CommonCursor);
        frame = WriteCoreDword(frame, manager + 0xD8, CoreDword(frame, manager + 0xD4));
        int parity = unchecked((sbyte)frame.ReadTemporaryByte(0x140));
        uint alternate = unchecked(CoreDword(frame, manager + 0x84) + (uint)(parity * (int)CoreDword(frame, manager + 0x8C)));
        frame = WriteCoreDword(frame, manager + 0x88, alternate);
        if ((CoreWord(frame, Manager(frame)) & 0x8000) != 0)
        {
            frame = services.ClearOrderingTable(frame, frame.ReadTemporaryDword(0x138), 0x1000);
        }
        if ((CoreWord(frame, Manager(frame)) & 0x8000) != 0)
        {
            frame = ApplyDebugA9BA90(frame);
        }
        frame = WriteCoreByte(frame, Manager(frame) + 0xD2, 0);
        if (CoreByte(frame, Manager(frame) + 0x35) == 0)
        {
            frame = NativeBattleWaterScriptScheduler.RunScriptPass(true, frame, services.ReadInstruction, services.ExecuteInstruction, instructionBudget);
            frame = RunPhysicsA9BB90(frame, input.Resources, services.UpdateSlot, traversalBudget);
            frame = NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, services.ReadInstruction, services.ExecuteInstruction, instructionBudget);
        }
        frame = WriteCoreWord(frame, CoreDword(frame, 0x27973BC) + 4, 0);
        frame = frame.WriteTemporaryWord(0x146, 0);
        frame = RunDrawAA0030(frame, services.DrawSlot, traversalBudget);
        frame = services.PublishFocal(frame, input.Focal);
        uint cursor = CoreDword(frame, Manager(frame) + 0x7C);
        uint command = CoreDword(frame, 0x27973C0);
        uint result = (CoreWord(frame, command + 0xA) & 0x8000) == 0 ? 2u : 0u;
        return new(frame, cursor, result);
    }

    public static NativeBattleWaterExecutionFrame RunPhysicsA9BB90(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot, int traversalBudget = 256)
    {
        frame = frame.WriteTemporaryByte(0x144, 0);
        int traversed = 0;
        while (true)
        {
            byte cursor = frame.ReadTemporaryByte(0x144);
            byte entry = OrderByte(frame, 0x2797454, cursor);
            if (entry == 0xFF)
            {
                return frame;
            }
            RequireTraversal(ref traversed, traversalBudget);
            if (frame.ReadTemporaryByte(0x145) == 0 || (entry & 0x80) != 0)
            {
                byte index = (byte)(entry & 0x7F);
                frame = frame.WriteTemporaryByte(0x142, index);
                uint manager = Manager(frame);
                uint slot = unchecked(CoreDword(frame, manager + 0x90) + 0x100u * index);
                frame = WriteCoreDword(frame, 0x27973E8, slot);
                int file = ArenaOffset(CoreDword(frame, 0x2798A6C), resources.Files[1].Data.Length);
                int slotBase = ArenaOffset(CoreDword(frame, manager + 0x90), 0);
                int capacity = CoreWord(frame, CoreDword(frame, 0x279744C) + 0x38);
                NativeBattleWaterSlotBank bank = new(file, resources.Files[1].Data.Length, capacity, slotBase);
                frame = frame with { Arena = NativeBattleWaterSlots.IntegratePhysicsA9BB90(frame.Arena, bank, index) };
                byte type = ReadSlot(frame, 0x18, 1)[0];
                frame = updateSlot(frame, type);
            }
            frame = frame.WriteTemporaryByte(0x144, unchecked((byte)(frame.ReadTemporaryByte(0x144) + 1)));
        }
    }

    public static NativeBattleWaterExecutionFrame RunDrawAA0030(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> drawSlot, int traversalBudget = 256)
    {
        frame = frame.WriteTemporaryDword(0, 0);
        frame = frame.WriteTemporaryDword(4, CoreDword(frame, Manager(frame) + 0x7C));
        int traversed = 0;
        while (true)
        {
            uint cursor = frame.ReadTemporaryDword(0);
            byte entry = OrderByte(frame, 0x2797554, cursor);
            if (entry == 0xFF)
            {
                return frame;
            }
            RequireTraversal(ref traversed, traversalBudget);
            frame = frame.WriteTemporaryByte(0x142, entry);
            uint manager = Manager(frame);
            frame = WriteCoreDword(frame, 0x27973E8, unchecked(CoreDword(frame, manager + 0x90) + 0x100u * entry));
            frame = frame.WriteTemporaryDword(0x14C, unchecked(frame.ReadTemporaryDword(0x138) + 4u * (uint)(SlotWord(frame, 0x48) & 0x0FFF)));
            bool separate = (ReadSlot(frame, 0x4C, 1)[0] & 1) != 0;
            if (separate)
            {
                frame = frame.WriteTemporaryDword(4, CoreDword(frame, manager + 0x7C));
                frame = WriteCoreDword(frame, manager + 0x7C, CoreDword(frame, manager + 0xD8));
            }
            frame = drawSlot(frame, ReadSlot(frame, 0x1C, 1)[0]);
            if (separate)
            {
                frame = WriteCoreDword(frame, Manager(frame) + 0xD8, CoreDword(frame, Manager(frame) + 0x7C));
                frame = WriteCoreDword(frame, Manager(frame) + 0x7C, frame.ReadTemporaryDword(4));
            }
            frame = frame.WriteTemporaryDword(0, unchecked(frame.ReadTemporaryDword(0) + 1));
        }
    }

    public static NativeBattleWaterExecutionFrame ApplyDebugA9BA90(NativeBattleWaterExecutionFrame frame)
    {
        uint manager = Manager(frame);
        byte enabled = CoreByte(frame, manager + 0x4C);
        if ((enabled & 0x10) != 0)
        {
            uint control = CoreDword(frame, manager + 0x40);
            Word(0x8000, 0x2797782, 32);
            Word(0x2000, 0x2797782, -32);
            Word(0x1000, 0x2797780, -32);
            Word(0x4000, 0x2797780, 32);
            Word(4, 0x2797784, -32);
            Word(8, 0x2797784, 32);
            Dword(1, 0x2797778, -32);
            Dword(2, 0x2797778, 32);

            void Word(uint bit, uint address, int delta)
            {
                if ((control & bit) != 0)
                {
                    frame = WriteCoreWord(frame, address, unchecked((ushort)(CoreWord(frame, address) + delta)));
                }
            }

            void Dword(uint bit, uint address, int delta)
            {
                if ((control & bit) != 0)
                {
                    frame = WriteCoreDword(frame, address, unchecked(CoreDword(frame, address) + (uint)delta));
                }
            }
        }
        if ((enabled & 0x40) != 0)
        {
            uint control = CoreDword(frame, manager + 0x40);
            Adjust(0x8000, 0x2797770, -256);
            Adjust(0x2000, 0x2797770, 256);
            Adjust(0x1000, 0x2797774, -256);
            Adjust(0x4000, 0x2797774, 256);
            Adjust(1, 0x2797778, -256);
            Adjust(2, 0x2797778, 256);
            Adjust(4, 0x2797778, -16);
            Adjust(8, 0x2797778, 16);

            void Adjust(uint bit, uint address, int delta)
            {
                if ((control & bit) != 0)
                {
                    frame = WriteCoreDword(frame, address, unchecked(CoreDword(frame, address) + (uint)delta));
                }
            }
        }
        return frame;
    }

    private static uint Manager(NativeBattleWaterExecutionFrame frame) => CoreDword(frame, 0x27973EC);
    private static byte CoreByte(NativeBattleWaterExecutionFrame frame, uint address) => frame.Core.Read(address, 1)[0];
    private static NativeBattleWaterExecutionFrame WriteCoreByte(NativeBattleWaterExecutionFrame frame, uint address, byte value) => frame with { Core = frame.Core.Write(address, new byte[] { value }) };

    private static byte OrderByte(NativeBattleWaterExecutionFrame frame, uint address, uint index)
    {
        if (index >= 128)
        {
            throw new NotSupportedException("Native effect iteration leaves its current physical order owner.");
        }
        return CoreByte(frame, address + index);
    }

    private static void RequireTraversal(ref int count, int budget)
    {
        if (++count > budget)
        {
            throw new NotSupportedException("Native effect iteration exhausted its diagnostic traversal budget.");
        }
    }
}

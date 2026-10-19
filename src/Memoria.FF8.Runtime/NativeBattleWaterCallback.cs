using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterCallbackInput(NativeBattleWater222Resources Resources, ushort GraphicsFlags, ImmutableArray<byte> ViewMatrix, uint OrderingTablePointer, uint CommonCursor, short Focal);
public sealed record NativeBattleWaterCallbackServices(Func<NativeBattleWaterExecutionFrame, uint, ushort> ReadInstruction, Func<NativeBattleWaterExecutionFrame, ushort, NativeBattleWaterExecutionFrame> ExecuteInstruction, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> UpdateSlot, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> DrawSlot, Func<NativeBattleWaterExecutionFrame, uint, int, NativeBattleWaterExecutionFrame> ClearOrderingTable, Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> PublishFocal)
{
    public Func<NativeBattleWaterExecutionFrame, short>? ReadCurrentFocal { get; init; }
}
public sealed record NativeBattleWaterCallbackResult(NativeBattleWaterExecutionFrame Frame, uint CommonCursor, uint TaskResult);

/// <summary>The callback order around explicit proved command, update, draw and graphics services.</summary>
public static class NativeBattleWaterCallback
{
    public static NativeBattleWaterCallbackResult RunA9B870(NativeBattleWaterExecutionFrame frame, NativeBattleWaterCallbackInput input, NativeBattleWaterCallbackServices services, int instructionBudget = 4096, int traversalBudget = 256)
    {
        input.Resources.Validate();
        ArgumentNullException.ThrowIfNull(services.DrawSlot);
        NativeBattleScriptedCallbackInput sharedInput = new(new(input.Resources.Profile.InitializationDirectory, input.Resources.Profile.OwnerDirectory), input.Resources.Files[1].Data.Length, input.GraphicsFlags, input.ViewMatrix, input.OrderingTablePointer, input.CommonCursor, input.Focal);
        NativeBattleScriptedCallbackServices sharedServices = new(services.ReadInstruction, services.ExecuteInstruction, services.UpdateSlot, (current, budget) => RunDrawAA0030(current, services.DrawSlot, budget), services.ClearOrderingTable, services.PublishFocal) { ReadCurrentFocal = services.ReadCurrentFocal };
        return NativeBattleScriptedCallback.Run(frame, sharedInput, sharedServices, instructionBudget, traversalBudget);
    }

    public static NativeBattleWaterExecutionFrame RunPhysicsA9BB90(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot, int traversalBudget = 256)
    {
        return RunPhysicsA9BB90(frame, resources.Files[1].Data.Length, updateSlot, traversalBudget);
    }

    public static NativeBattleWaterExecutionFrame RunPhysicsA9BB90(NativeBattleWaterExecutionFrame frame, int resource1Length, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot, int traversalBudget = 256)
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
                int file = ArenaOffset(CoreDword(frame, 0x2798A6C), resource1Length);
                int slotBase = ArenaOffset(CoreDword(frame, manager + 0x90), 0);
                int capacity = CoreWord(frame, CoreDword(frame, 0x279744C) + 0x38);
                NativeBattleWaterSlotBank bank = new(file, resource1Length, capacity, slotBase);
                frame = frame with { Arena = NativeBattleWaterSlots.IntegratePhysicsA9BB90(frame.Arena, bank, index) };
                byte type = ReadSlot(frame, 0x18, 1)[0];
                frame = updateSlot(frame, type);
            }
            frame = frame.WriteTemporaryByte(0x144, unchecked((byte)(frame.ReadTemporaryByte(0x144) + 1)));
        }
    }

    public static NativeBattleWaterExecutionFrame RunDrawAA0030(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> drawSlot, int traversalBudget = 256)
    {
        return RunDrawSlots(frame, current => current, drawSlot, traversalBudget);
    }

    public static NativeBattleWaterExecutionFrame RunDrawSlots(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> prepareSlot, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> drawSlot, int traversalBudget = 256)
    {
        ArgumentNullException.ThrowIfNull(prepareSlot);
        ArgumentNullException.ThrowIfNull(drawSlot);
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
            frame = prepareSlot(frame);
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

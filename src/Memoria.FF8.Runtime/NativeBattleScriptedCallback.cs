using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedCallbackInput(NativeBattleScriptedInitializationDirectories Directories, int Resource1Length, ushort GraphicsFlags, ImmutableArray<byte> ViewMatrix, uint OrderingTablePointer, uint CommonCursor, short Focal);
public sealed record NativeBattleScriptedCallbackServices(Func<NativeBattleWaterExecutionFrame, uint, ushort> ReadInstruction, Func<NativeBattleWaterExecutionFrame, ushort, NativeBattleWaterExecutionFrame> ExecuteInstruction, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> UpdateSlot, Func<NativeBattleWaterExecutionFrame, int, NativeBattleWaterExecutionFrame> DrawSlots, Func<NativeBattleWaterExecutionFrame, uint, int, NativeBattleWaterExecutionFrame> ClearOrderingTable, Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> PublishFocal)
{
    public Func<NativeBattleWaterExecutionFrame, short>? ReadCurrentFocal { get; init; }
}

/// <summary>Shared scripted cinematic callback ordering, with the selected clone's explicit dispatch and draw services.</summary>
public static class NativeBattleScriptedCallback
{
    public static NativeBattleWaterCallbackResult Run(NativeBattleWaterExecutionFrame frame, NativeBattleScriptedCallbackInput input, NativeBattleScriptedCallbackServices services, int instructionBudget = 4096, int traversalBudget = 256)
    {
        input.Directories.Validate();
        ArgumentNullException.ThrowIfNull(services.DrawSlots);
        if (input.Resource1Length <= 0)
        {
            throw new InvalidDataException("Native effect callback requires its current physics resource extent.");
        }
        if (input.ViewMatrix.IsDefault || input.ViewMatrix.Length != 32 || traversalBudget <= 0)
        {
            throw new InvalidDataException("Native effect callback requires its current view and bounded diagnostic traversal.");
        }
        ArgumentNullException.ThrowIfNull(services.ReadInstruction);
        ArgumentNullException.ThrowIfNull(services.ExecuteInstruction);
        ArgumentNullException.ThrowIfNull(services.UpdateSlot);
        ArgumentNullException.ThrowIfNull(services.ClearOrderingTable);
        ArgumentNullException.ThrowIfNull(services.PublishFocal);
        using NativeBattleWaterTemporaryFrame.PrivateWrite temporary = frame.TemporaryFrame.BeginPrivateWrite();
        frame = frame with { TemporaryFrame = temporary.View };
        NativeBattleWaterFrameBindingResult binding = NativeBattleWaterFrameBinding.Bind(frame.Core, frame.TemporaryFrame, input.Directories);
        frame = frame with { Core = binding.Core, TemporaryFrame = binding.TemporaryFrame };
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
            frame = NativeBattleWaterCallback.ApplyDebugA9BA90(frame);
        }
        frame = WriteCoreByte(frame, Manager(frame) + 0xD2, 0);
        if (CoreByte(frame, Manager(frame) + 0x35) == 0)
        {
            frame = NativeBattleWaterScriptScheduler.RunScriptPass(true, frame, services.ReadInstruction, services.ExecuteInstruction, instructionBudget);
            frame = NativeBattleWaterCallback.RunPhysicsA9BB90(frame, input.Resource1Length, services.UpdateSlot, traversalBudget);
            frame = NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, services.ReadInstruction, services.ExecuteInstruction, instructionBudget);
        }
        frame = WriteCoreWord(frame, CoreDword(frame, 0x27973BC) + 4, 0);
        frame = frame.WriteTemporaryWord(0x146, 0);
        frame = services.DrawSlots(frame, traversalBudget);
        frame = services.PublishFocal(frame, services.ReadCurrentFocal is { } readFocal ? readFocal(frame) : input.Focal);
        uint cursor = CoreDword(frame, Manager(frame) + 0x7C);
        uint command = CoreDword(frame, 0x27973C0);
        uint result = (CoreWord(frame, command + 0xA) & 0x8000) == 0 ? 2u : 0u;
        frame = frame with { TemporaryFrame = temporary.Publish() };
        return new(frame, cursor, result);
    }

    private static uint Manager(NativeBattleWaterExecutionFrame frame) => CoreDword(frame, 0x27973EC);
    private static byte CoreByte(NativeBattleWaterExecutionFrame frame, uint address) => frame.Core.Read(address, 1)[0];
    private static NativeBattleWaterExecutionFrame WriteCoreByte(NativeBattleWaterExecutionFrame frame, uint address, byte value) => frame with { Core = frame.Core.Write(address, new byte[] { value }) };
}

using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterIoCommandTests
{
    private const uint Manager = 0x2797690;
    private const uint Pc = 0xFFFFFFFE;
    private const uint Destination = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x100, 0x158, 1, 0x158);

    [Theory]
    [InlineData(0xC006, 363)]
    [InlineData(0xC206, 364)]
    [InlineData(0xC406, 365)]
    public void SelectedReadsPublishBusyAndCurrentDestinationWithoutExtraOperandReads(ushort instruction, short file)
    {
        NativeBattleEffectArena arena = Program(instruction).MarkUnknown(0x1002, 8).MarkUnknown(0x158, 0x100);
        NativeBattleScriptedEffectMemory core = Core();
        NativeBattleScriptedEffectMemory before = NativeBattleStorageSnapshots.Capture(core);
        NativeBattleWaterIoCommandResult result = Run(core, arena, length: 2);
        Assert.Equal(new NativeBattleWaterFileReadRequest(file, Destination, 0, 0xAA0ED0), result.Request);
        Assert.Equal((byte)0xFF, result.Core.Read(0x2798219, 1)[0]);
        Assert.Equal(Destination, ReadDword(result.Core, Manager + 0xB4));
        Assert.Equal(Destination, ReadDword(result.Core, Manager + 0xB8));
        Assert.Equal(0u, result.NextInstructionPointer);
        Assert.Null(result.YieldWait);
        Assert.Equal(core.Read(0x2798A68, 8), result.Core.Read(0x2798A68, 8));
        Assert.Equal((byte)0, before.Read(0x2798219, 1)[0]);
        Assert.Same(core, result.Core);
    }

    [Theory]
    [InlineData(0xC006)]
    [InlineData(0x2029)]
    [InlineData(0x0008)]
    public void BusyReadsOnlyCurrentWaitAndDoesNotConsumeUnknownOperandsOrPointerOrReady(ushort instruction)
    {
        NativeBattleEffectArena arena = PutWord(Program(instruction), 0x158 + 0xC8, 0xFFFF).MarkUnknown(0x1002, 8);
        NativeBattleScriptedEffectMemory core = Core().Write(0x2798219, new byte[] { 7 }).MarkUnknown(0x2798218, 1).MarkUnknown(Manager + 0xB4, 8);
        NativeBattleWaterIoCommandResult result = Run(core, arena, length: 2);
        Assert.Equal(Pc, result.NextInstructionPointer);
        Assert.Equal((ushort?)0xFFFF, result.YieldWait);
        Assert.Null(result.Request);
        Assert.Equal(NativeBattleTestSnapshots.Json(core), NativeBattleTestSnapshots.Json(result.Core));
        Assert.Null(result.Textures);
    }

    [Fact]
    public void WaitDefersReadyAndWaitReadsAndDistinguishesZeroWaitFromContinue()
    {
        NativeBattleEffectArena arena = Program(8).MarkUnknown(0x158 + 0xC8, 2);
        NativeBattleWaterIoCommandResult ready = Run(Core(), arena, length: 2);
        Assert.Null(ready.YieldWait);
        Assert.Equal(0u, ready.NextInstructionPointer);
        NativeBattleScriptedEffectMemory notReady = Core().Write(0x2798218, new byte[] { 0 });
        Assert.Throws<NotSupportedException>(() => Run(notReady, arena));
        arena = PutWord(arena, 0x158 + 0xC8, 0);
        NativeBattleWaterIoCommandResult waiting = Run(notReady, arena);
        Assert.Equal((ushort?)0, waiting.YieldWait);
        Assert.Equal(Pc, waiting.NextInstructionPointer);
        Assert.Throws<NotSupportedException>(() => Run(Core().MarkUnknown(0x2798218, 1), arena));
    }

    [Fact]
    public void CompletionChangesOnlyBusyIncludingUnknownReadyAndPointers()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.Unknown().Write(0x2798219, new byte[] { 0xFF });
        NativeBattleScriptedEffectMemory result = NativeBattleWaterIoCommands.CompleteAA0ED0(core);
        Assert.Equal((byte)0, result.Read(0x2798219, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Read(0x2798218, 1));
        Assert.Throws<NotSupportedException>(() => result.Read(Manager + 0xB4, 8));
        Assert.Equal(NativeBattleTestSnapshots.Json(core with { Core = core.Core.Write((int)(0x2798219 - NativeBattleScriptedEffectMemory.Address), new byte[] { 0 }) }), NativeBattleTestSnapshots.Json(result));
    }

    [Fact]
    public void LiteralRectangleUsesCurrentPointerDirectQueueAndLatestArenaAfterJson()
    {
        NativeBattleEffectArena arena = Program(0x2029, 704, 256, 2, 2).Write(0x4000, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        NativeBattleScriptedEffectMemory core = Core().MarkUnknown(Manager + 0xD2, 1);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWaterIoCommandResult result = Run(core, arena, textures: textures);
        Assert.Equal(8u, result.NextInstructionPointer);
        Assert.Null(result.Request);
        Assert.Null(result.YieldWait);
        Assert.Equal(1, result.Textures!.Count);
        Assert.Equal(new NativeBattleEffectTextureRectangle(704, 256, 2, 2), result.Textures.Slots[0]!.Rectangle);
        Assert.Equal(0x4000, result.Textures.Slots[0]!.ArenaOffset);
        result = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterIoCommandResult>(NativeBattleTestSnapshots.Json(result))!;
        arena = arena.Write(0x4000, new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 });
        NativeBattleEffectTextureDrain drained = result.Textures!.Drain505D20(arena);
        Assert.Equal(new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 }, drained.Uploads.Single().Bytes);
        Assert.Equal(new byte[] { 9, 10, 11, 12 }, drained.State.ReadVram().AsSpan(2 * (256 * 1024 + 704), 4).ToArray());
        Assert.Equal(NativeBattleTestSnapshots.Json(core), NativeBattleTestSnapshots.Json(result.Core));
    }

    [Fact]
    public void ExternalDeliveryIntoCurrentArenaPreservesGuardsAndIsSeenBySubsequentUpload()
    {
        NativeBattleEffectArena arena = Program(0xC006).Write(0x3FFF, new byte[] { 0xA5 }).Write(0xC000, new byte[] { 0x5A });
        NativeBattleWaterIoCommandResult pending = Run(Core(), arena, length: 2);
        pending = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterIoCommandResult>(NativeBattleTestSnapshots.Json(pending))!;
        Assert.Equal((byte)0xFF, pending.Core.Read(0x2798219, 1)[0]);
        byte[] delivered = Enumerable.Range(0, 0x8000).Select(index => (byte)(index * 17 + 3)).ToArray();
        int offset = checked((int)(pending.Request!.DestinationAddress - NativeBattleEffectNodeMemory.ArenaAddress));
        arena = arena.Write(offset, delivered);
        Assert.Equal((byte)0xA5, arena.Read(0x3FFF, 1)[0]);
        Assert.Equal((byte)0x5A, arena.Read(0xC000, 1)[0]);
        NativeBattleScriptedEffectMemory completed = NativeBattleWaterIoCommands.CompleteAA0ED0(pending.Core);
        arena = PutWord(PutWord(PutWord(PutWord(PutWord(arena, 0x1000, 0x2029), 0x1002, 704), 0x1004, 256), 0x1006, 64), 0x1008, 256);
        NativeBattleWaterIoCommandResult uploaded = Run(completed, arena, textures: NativeBattleEffectTextureState.FromColdProcess());
        Assert.Equal(delivered, uploaded.Textures!.Drain505D20(arena).Uploads.Single().Bytes);
        Assert.Equal(Destination, ReadDword(uploaded.Core, Manager + 0xB4));
    }

    [Fact]
    public void UnsupportedBranchesAndMissingConsumedOwnersFailWithoutDefaults()
    {
        Assert.Throws<NotSupportedException>(() => Run(Core(), Program(6)));
        Assert.Throws<NotSupportedException>(() => Run(Core(), Program(0x2129)));
        Assert.Throws<NotSupportedException>(() => Run(Core().MarkUnknown(Manager + 0xB8, 4), Program(0xC006)));
        Assert.Throws<NotSupportedException>(() => Run(Core(), Program(0x2029, 0, 0, 1, 1)));
        Assert.Throws<InvalidDataException>(() => Run(Core(), Program(0x2029, 0, 0, 1, 1), length: 2));
        NativeBattleScriptedEffectMemory outside = PutDword(Core(), Manager + 0xB4, 0x80300000);
        Assert.Throws<InvalidDataException>(() => Run(outside, Program(0x2029, 0, 0, 1, 1), textures: NativeBattleEffectTextureState.FromColdProcess()));
    }

    private static NativeBattleWaterIoCommandResult Run(NativeBattleScriptedEffectMemory core, NativeBattleEffectArena arena, int length = 10, NativeBattleEffectTextureState? textures = null) => NativeBattleWaterIoCommands.Execute(core, arena, Bank, 0, new(0x1000, length, Pc), Pc, Manager, textures);

    private static NativeBattleScriptedEffectMemory Core()
    {
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(0x2798218, new byte[] { 0xFF });
        core = PutDword(core, Manager + 0xB8, Destination);
        core = PutDword(core, Manager + 0xB4, Destination);
        return PutDword(core, 0x2798A68, 0x12345678);
    }

    private static NativeBattleEffectArena Program(params ushort[] words)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < words.Length; index++)
        {
            arena = PutWord(arena, 0x1000 + 2 * index, words[index]);
        }
        return arena;
    }

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleScriptedEffectMemory PutDword(NativeBattleScriptedEffectMemory core, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return core.Write(address, bytes);
    }

    private static uint ReadDword(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
}

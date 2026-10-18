using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterIoServiceTests
{
    private const uint Manager = 0x2797690;
    private const int DestinationOffset = 0x4000;
    private const uint Destination = NativeBattleEffectNodeMemory.ArenaAddress + DestinationOffset;

    [Theory]
    [InlineData(363)]
    [InlineData(364)]
    [InlineData(365)]
    public void ActualQueueCopiesWholeFileIntoSameArenaThenCompletesOnSchedulerCallback(short fileId)
    {
        var fixture = Fixture(fileId);
        NativeBattleWaterIoQueued queued = NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory, NativeBattleFileIoState.FromColdProcess(), fixture.Request, fixture.Resources);
        Assert.Equal(fixture.Resources.Files[fileId - 363].Bytes, queued.Memory.Arena!.Read(DestinationOffset, 0x8000));
        Assert.Equal((byte)0xA5, queued.Memory.Arena.Read(DestinationOffset - 1, 1)[0]);
        Assert.Equal((byte)0x5A, queued.Memory.Arena.Read(DestinationOffset + 0x8000, 1)[0]);
        Assert.Equal(fixture.Memory.Arena!.Read(0x2000, 0x58), queued.Memory.Arena.Read(0x2000, 0x58));
        Assert.Equal(fixture.Memory.Cursor, queued.Memory.Cursor);
        Assert.Equal(fixture.Memory.AllocationCount, queued.Memory.AllocationCount);
        Assert.Equal((byte)0xFF, queued.Memory.ScriptedEffects!.Read(0x2798219, 1)[0]);
        Assert.Equal((byte)0x71, queued.Memory.ScriptedEffects.Read(0x2798218, 1)[0]);
        Assert.Equal((short?)1, queued.FileIo.ActiveCount);
        Assert.Equal((short?)1, queued.FileIo.Rows[0].Countdown);
        Assert.Equal(0x8000, queued.FileIo.Rows[0].FileResult);
        Assert.Equal((uint?)0xAA0ED0, queued.FileIo.Rows[0].CompletionCallback);
        int callbacks = 0;
        queued = Pump(queued, () => callbacks++);
        Assert.Equal(0, callbacks);
        Assert.Equal((byte)0xFF, queued.Memory.ScriptedEffects!.Read(0x2798219, 1)[0]);
        queued = Pump(queued, () => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.Equal((short?)0, queued.FileIo.ActiveCount);
        Assert.Equal((byte)0, queued.Memory.ScriptedEffects!.Read(0x2798219, 1)[0]);
        Assert.Equal((byte)0x71, queued.Memory.ScriptedEffects.Read(0x2798218, 1)[0]);
        Assert.Equal(Destination, ReadDword(queued.Memory.ScriptedEffects, Manager + 0xB4));
        Assert.Equal(Destination, ReadDword(queued.Memory.ScriptedEffects, Manager + 0xB8));
    }

    [Fact]
    public void PendingJsonResumesSameQueueAndUploadedRectangleReadsDeliveredCurrentBytes()
    {
        var fixture = Fixture(364);
        NativeBattleWaterIoQueued queued = NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory, NativeBattleFileIoState.FromColdProcess(), fixture.Request, fixture.Resources);
        NativeBattleWaterIoQueued restored = JsonSerializer.Deserialize<NativeBattleWaterIoQueued>(JsonSerializer.Serialize(queued))!;
        for (int pass = 0; pass < 2; pass++)
        {
            queued = Pump(queued, () => { });
            restored = Pump(restored, () => { });
            Assert.Equal(JsonSerializer.Serialize(queued), JsonSerializer.Serialize(restored));
        }
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(768, 256, 64, 256), restored.Memory.Arena!, DestinationOffset);
        Assert.Equal(fixture.Resources.Files[1].Bytes, textures.Drain505D20(restored.Memory.Arena).Uploads.Single().Bytes);
    }

    [Fact]
    public void RequestMetadataAndDestinationMustMatchPublishedOwner()
    {
        var fixture = Fixture(363);
        Reject(fixture.Request with { FileId = 362 });
        Reject(fixture.Request with { Argument = 1 });
        Reject(fixture.Request with { CompletionCallback = 0xAA0ED1 });
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory, NativeBattleFileIoState.FromColdProcess(), fixture.Request with { DestinationAddress = Destination + 4 }, fixture.Resources));
        NativeBattleWaterIoResources shortFile = fixture.Resources with { Files = fixture.Resources.Files.SetItem(0, fixture.Resources.Files[0] with { Bytes = [1, 2] }) };
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory, NativeBattleFileIoState.FromColdProcess(), fixture.Request, shortFile));

        void Reject(NativeBattleWaterFileReadRequest request)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory, NativeBattleFileIoState.FromColdProcess(), request, fixture.Resources));
        }
    }

    [Fact]
    public void DestinationRequiresAllocatedPrefixAndActualCoreRatherThanHydration()
    {
        var fixture = Fixture(363);
        NativeBattleFileIoState io = NativeBattleFileIoState.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory with { Cursor = DestinationOffset + 0x8000 - 1 }, io, fixture.Request, fixture.Resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory with { ScriptedEffects = null }, io, fixture.Request, fixture.Resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory with { Arena = null }, io, fixture.Request, fixture.Resources));
        NativeBattleScriptedEffectMemory core = fixture.Memory.ScriptedEffects!;
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory with { ScriptedEffects = core.Write(0x2798219, new byte[] { 0 }) }, io, fixture.Request, fixture.Resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory with { ScriptedEffects = core.MarkUnknown(Manager + 0xB4, 4) }, io, fixture.Request, fixture.Resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterIoService.CopyAndQueueFile(fixture.Memory with { ScriptedEffects = core.MarkUnknown(0x27973EC, 4) }, io, fixture.Request, fixture.Resources));
    }

    private static NativeBattleWaterIoQueued Pump(NativeBattleWaterIoQueued queued, Action completed)
    {
        NativeBattleEffectMemory memory = queued.Memory;
        NativeBattleFileIoState io = queued.FileIo.Advance482590((current, completion) =>
        {
            Assert.Equal(0xAA0ED0u, completion.Callback);
            Assert.Equal(0x8000, completion.Result);
            memory = memory with { ScriptedEffects = NativeBattleWaterIoCommands.CompleteAA0ED0(memory.ScriptedEffects!) };
            completed();
            return current;
        });
        return new(memory, io);
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleWaterFileReadRequest Request, NativeBattleWaterIoResources Resources) Fixture(short fileId)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x2000, Enumerable.Repeat((byte)0xCC, 0x58).ToArray()).Write(DestinationOffset - 1, new byte[] { 0xA5 }).Write(DestinationOffset + 0x8000, new byte[] { 0x5A });
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(0x2798218, new byte[] { 0x71 });
        core = PutDword(core, 0x27973EC, Manager);
        core = PutDword(core, Manager + 0xB8, Destination);
        ushort instruction = (ushort)(0xC006 + ((fileId - 363) << 9));
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, instruction);
        arena = arena.Write(0x1000, bytes);
        NativeBattleWaterIoCommandResult started = NativeBattleWaterIoCommands.Execute(core, arena, new(0x2000, 0x858, 8, 0x2058), 0, new(0x1000, 2, 0x1000), 0x1000, Manager);
        NativeBattleEffectMemory memory = new(arena, 0xD000, 0, new NativeBattleEffectHeapAllocation?[256].ToImmutableArray(), 1, 222, 0, 0) { ScriptedEffects = started.Core };
        NativeBattleWaterIoResources resources = new(Enumerable.Range(0, 3).Select(index => new NativeBattleArchiveFile((ushort)(363 + index), $"ma8def_p.{index}", Enumerable.Range(0, 0x8000).Select(offset => (byte)(offset * 3 + index)).ToImmutableArray())).ToImmutableArray());
        return (memory, started.Request!, resources);
    }

    private static NativeBattleScriptedEffectMemory PutDword(NativeBattleScriptedEffectMemory core, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return core.Write(address, bytes);
    }

    private static uint ReadDword(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
}

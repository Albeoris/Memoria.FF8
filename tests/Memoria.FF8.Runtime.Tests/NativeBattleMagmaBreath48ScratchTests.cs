using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMagmaBreath48ScratchTests
{
    [Theory]
    [InlineData(80)]
    [InlineData(88)]
    [InlineData(180)]
    public void Native_downward_scratch_reservation_retains_canonical_bytes_and_never_changes_5082_allocator(int size)
    {
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 180).Select(index => unchecked((byte)index)).ToArray()).MarkUnknown(179, 1);
        NativeBattleMagmaBreath48Frame frame = NativeBattleMagmaBreath48EngineTests.Frame(0);
        frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, bytes), State = frame.State.WriteDword(0x26495D8, 0x2793E58), Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        var allocation = Allocate(frame, size);
        Assert.Equal(0x2793E58u - (uint)size, allocation.Address);
        Assert.Equal(allocation.Address, allocation.Frame.State.ReadDword(0x26495D8));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(allocation.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleEffectArena context = Context(allocation.Frame, allocation.Address, size);
        Assert.Equal(unchecked((byte)(180 - size)), context.Read(0, 1)[0]);
        Assert.Throws<NotSupportedException>(() => context.Read(size - 1, 1));
        NativeBattleMagmaBreath48Frame written = PublishContext(allocation.Frame, allocation.Address, context.Write(0, new byte[] { 0xEF }).MarkUnknown(1, 1), size);
        NativeBattleMagmaBreath48Frame released = Release(written, size);
        Assert.Equal(0x2793E58u, released.State.ReadDword(0x26495D8));
        Assert.Equal((byte)0xEF, Context(released, allocation.Address, size).Read(0, 1)[0]);
        Assert.Throws<NotSupportedException>(() => Context(released, allocation.Address, size).Read(1, 1));
        NativeBattleEffectArena retained = NativeBattleEffectNodeContextMemory.Capture(released.Memory);
        if (size < 180)
        {
            Assert.Equal(bytes.Read(0, 180 - size), retained.Read(0, 180 - size));
        }
        Assert.Equal(SnapshotJson(frame.Staging), SnapshotJson(released.Staging));
    }

    [Fact]
    public void Sprite_and_mesh_views_alias_the_same_physical_retained_tail()
    {
        NativeBattleMagmaBreath48Frame frame = NativeBattleMagmaBreath48EngineTests.Frame(0);
        frame = frame with { State = frame.State.WriteDword(0x26495D8, 0x2793E58) };
        var sprite = Allocate(frame, 180);
        frame = PublishContext(sprite.Frame, sprite.Address, Context(sprite.Frame, sprite.Address, 180).Write(100, BitConverter.GetBytes(0x12345678u)), 180);
        frame = Release(frame, 180);
        var mesh = Allocate(frame, 88);
        Assert.Equal(sprite.Address + 92, mesh.Address);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(Context(mesh.Frame, mesh.Address, 88).Read(8, 4)));
        frame = PublishContext(mesh.Frame, mesh.Address, Context(mesh.Frame, mesh.Address, 88).Write(0x24, BitConverter.GetBytes(0xABCDEF01u)), 88);
        frame = Release(frame, 88);
        Assert.Equal(0xABCDEF01u, BinaryPrimitives.ReadUInt32LittleEndian(Context(frame, sprite.Address, 180).Read(92 + 0x24, 4)));
        Assert.Equal(0x2793E58u, frame.State.ReadDword(0x26495D8));
    }

    [Fact]
    public void Native_spawn80_records_full_reached_stores_before_later_mesh_and_keeps_padding_and_current_process()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame source = NativeBattleCuragaEmitterTests.Source(fixture.Frame, bytes =>
        {
            bytes[0x34] = 1;
            bytes[0x33] = 1;
            bytes[0x21] = 1;
        });
        source = NativeBattleCuragaSpawnTests.Channel(source, 0xCC, 5);
        source = NativeBattleCuragaSpawnTests.Channel(source, 0xD0, -7);
        source = NativeBattleCuragaSpawnTests.Channel(source, 0xD4, 9);
        NativeBattleEffectArena initial = NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 180).Select(index => unchecked((byte)(index + 19))).ToArray()).MarkUnknown(118, 2).MarkUnknown(138, 2).MarkUnknown(146, 2).MarkUnknown(154, 2).MarkUnknown(168, 4);
        NativeBattleEffectMemory process = NativeBattleEffectNodeContextMemory.Publish(source.Memory with { Arena = source.Memory.Arena!.Write(NativeBattleCuragaSpawnTests.Emitter + 0x60, BitConverter.GetBytes((ushort)7)) }, initial);
        NativeBattleTheEnd85SpawnScratchObserver observer = new(initial);
        int emitter = NativeBattleCuragaSpawnTests.Emitter;
        int geometry = emitter + 20 * 108;
        NativeBattleEffectNodeMemory memory = new(process, source.Effect, fixture.Task, emitter, 20 * 108, geometry, 100 * 672, useSharedScratch: true) { SpawnScratchObserver = observer };
        ushort cursor = 0;
        NativeBattleEffectNodeInputs inputs = new(source.State.CopiedCamera, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (owner, index) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, 100, ref cursor, owner, index));
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, new(NativeBattleCuragaExecutableState.Address, source.Memory.CuragaExecutable!.Data!), inputs);
        NativeCrtRandomState random = new(1);
        _ = lifecycle.Spawn(emitter, 1, lifecycle.ReadSource(0), ref random);
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        NativeBattleEffectMemory current = written.Memory with { ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Ir0 = 0x12345678 } };
        NativeBattleEffectArena physical = NativeBattleEffectNodeContextMemory.Copy(observer.View, 100, NativeBattleEffectNodeContextMemory.Capture(current), 100, 80);
        NativeBattleMagmaBreath48Frame frame = NativeBattleMagmaBreath48EngineTests.Frame(0) with { Memory = NativeBattleEffectNodeContextMemory.Publish(current, physical) };
        Assert.Equal(initial.Read(0, 100), physical.Read(0, 100));
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(physical.Read(178, 2)));
        Assert.Equal(new byte[12], physical.Read(120, 12));
        Assert.Equal(new short[] { 5, -7, 9 }, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt16LittleEndian(physical.Read(172 + index * 2, 2))).ToArray());
        Assert.Equal(0x12345678u, frame.Memory.ScriptedColorRegisters!.Ir0);
        foreach (int padding in new[] { 118, 138, 146, 154, 168 })
        {
            Assert.Throws<NotSupportedException>(() => physical.Read(padding, 1));
        }
        NativeBattleEffectArena laterMesh = Context(frame, NativeBattleEffectNodeContextMemory.Address + 92, 88);
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(laterMesh.Read(86, 2)));
        Assert.Throws<NotSupportedException>(() => laterMesh.Read(26, 2));
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}

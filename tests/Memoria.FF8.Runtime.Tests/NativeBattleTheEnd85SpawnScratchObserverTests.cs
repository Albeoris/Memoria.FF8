using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85SpawnScratchObserverTests
{
    [Fact]
    public void Reached_placement_records_native_frame_angles_radii_displacements_and_identity_translation()
    {
        NativeBattleEffectArena initial = Retained().MarkUnknown(118, 2).MarkUnknown(138, 2).MarkUnknown(146, 2).MarkUnknown(154, 2).MarkUnknown(168, 4);
        NativeBattleEffectArena result = Spawn(initial, source =>
        {
            source[0x34] = 1;
            source[0x33] = 1;
            source[0x21] = 0;
        }, 1, 7);
        Assert.Equal((ushort)7, Word(result, 78));
        Assert.Equal(new short[] { 41, 2083, 2238 }, Vector(result, 40));
        Assert.Equal(new short[] { 5, -7, 9 }, Vector(result, 72));
        Assert.Equal(new short[] { 0, 0, 0 }, Vector(result, 48));
        Assert.Equal(0, Dword(result, 20));
        Assert.Equal(0, Dword(result, 24));
        Assert.Equal(0, Dword(result, 28));
        short[] unit = Vector(result, 32);
        Assert.Equal(unchecked(5 * unit[0] << 4), Dword(result, 56));
        Assert.Equal(unchecked(-7 * unit[1] << 4), Dword(result, 60));
        Assert.Equal(unchecked(9 * unit[2] << 4), Dword(result, 64));
        foreach (int offset in new[] { 18, 38, 46, 54, 68, 69, 70, 71 })
        {
            Assert.False(result.ReadSnapshot(100 + offset, 1).Known[0]);
        }
        Assert.Equal(initial.Read(0, 100), result.Read(0, 100));
        NativeBattleEffectArena independent = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(independent));
    }

    [Fact]
    public void No_placement_preserves_every_prior_byte_except_frame_and_random_Euler_XYZ()
    {
        NativeBattleEffectArena initial = Retained();
        NativeBattleEffectArena result = Spawn(initial, source =>
        {
            source[0x34] = 0;
            source[0x21] = 0;
        }, 1, 9);
        byte[] expected = initial.Read(0, 180);
        expected.AsSpan(148, 6).Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(178), 9);
        Assert.Equal(expected, result.Read(0, 180));
    }

    [Fact]
    public void Orientation_copy_transfers_all_eight_bytes_and_preserves_unknown_padding_knownness()
    {
        NativeBattleEffectArena initial = Retained().MarkUnknown(146, 2);
        NativeBattleEffectArena result = Spawn(initial, source =>
        {
            source[0x34] = 0;
            source[0x21] = 1;
        }, 1, 3);
        var source = initial.ReadSnapshot(140, 8);
        var copied = result.ReadSnapshot(148, 8);
        Assert.Equal(source.Data, copied.Data);
        Assert.Equal(source.Known, copied.Known);
        Assert.Throws<NotSupportedException>(() => result.Read(148, 8));
    }

    [Fact]
    public void Zero_instances_still_stores_frame_and_leaves_entire_temporary_body_untouched()
    {
        NativeBattleEffectArena initial = Retained().MarkUnknown(100, 78);
        NativeBattleEffectArena result = Spawn(initial, source => source[0x34] = 1, 0, 11);
        Assert.Equal((ushort)11, Word(result, 78));
        Assert.Equal(initial.ReadSnapshot(100, 78).Data, result.ReadSnapshot(100, 78).Data);
        Assert.Equal(initial.ReadSnapshot(100, 78).Known, result.ReadSnapshot(100, 78).Known);
    }

    [Fact]
    public void Plane_placement_uses_shifted_radius_Y_and_keeps_selector_one_copy_padding()
    {
        NativeBattleEffectArena result = Spawn(Retained(), source =>
        {
            source[0x34] = 2;
            source[0x33] = 1;
            source[0x21] = 1;
        }, 1, 0);
        Assert.Equal(new short[] { 41, 0, 1024 }, Vector(result, 40));
        Assert.Equal(unchecked((uint)(unchecked((short)Word(result, 74)) << 16)), unchecked((uint)Dword(result, 60)));
        Assert.Equal(result.Read(140, 8), result.Read(148, 8));
    }

    private static NativeBattleEffectArena Spawn(NativeBattleEffectArena initial, Action<byte[]> edit, int instances, ushort frame)
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame current = NativeBattleCuragaEmitterTests.Source(fixture.Frame, edit);
        current = NativeBattleCuragaSpawnTests.Channel(current, 0xCC, 5);
        current = NativeBattleCuragaSpawnTests.Channel(current, 0xD0, -7);
        current = NativeBattleCuragaSpawnTests.Channel(current, 0xD4, 9);
        NativeBattleEffectArena arena = current.Memory.Arena!.Write(NativeBattleCuragaSpawnTests.Emitter + 96, BitConverter.GetBytes(frame));
        NativeBattleEffectMemory memoryOwner = NativeBattleEffectNodeContextMemory.Publish(current.Memory with { Arena = arena }, initial);
        NativeBattleTheEnd85SpawnScratchObserver observer = new(initial);
        NativeBattleEffectNodeMemory memory = new(memoryOwner, current.Effect, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 0x25F8, NativeBattleCuragaSpawnTests.Node, 0x16F80, useSharedScratch: true) { SpawnScratchObserver = observer };
        using NativeBattleEffectNodeBank bank = new(NativeBattleCuragaExecutableState.Address, current.Memory.CuragaExecutable!.Data!);
        ushort cursor = 0;
        NativeBattleEffectNodeInputs inputs = new(current.State.CopiedCamera, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, NativeBattleCuragaSpawnTests.Node, 140, ref cursor, emitter, source));
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
        NativeCrtRandomState random = new(1);
        lifecycle.Spawn(NativeBattleCuragaSpawnTests.Emitter, instances, lifecycle.ReadSource(0), ref random);
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        NativeBattleEffectMemory published = NativeBattleEffectNodeContextMemory.Publish(written.Memory, observer.View);
        return NativeBattleEffectNodeContextMemory.Capture(published);
    }
    private static NativeBattleEffectArena Retained() => NativeBattleEffectArena.Clear().Write(0, Enumerable.Range(0, 180).Select(index => unchecked((byte)(index + 19))).ToArray());
    private static ushort Word(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(view.Read(100 + offset, 2));
    private static int Dword(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt32LittleEndian(view.Read(100 + offset, 4));
    private static short[] Vector(NativeBattleEffectArena view, int offset) => Enumerable.Range(0, 3).Select(axis => unchecked((short)Word(view, offset + axis * 2))).ToArray();
}

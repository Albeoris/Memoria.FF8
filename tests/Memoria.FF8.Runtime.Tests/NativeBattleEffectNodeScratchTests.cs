using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectNodeScratchTests
{
    [Fact]
    public void Partial_writes_preserve_unknown_and_retained_words_through_JSON()
    {
        NativeBattleEffectNodeScratch unknown = NativeBattleEffectNodeScratch.Unknown();
        NativeBattleEffectNodeScratch flags = unknown.WriteFlags(0x80020000);
        Assert.Equal((byte)0x0F, flags.KnownMask);
        Assert.Throws<NotSupportedException>(() => flags.ReadEuler());
        NativeBattleEffectNodeScratch placement = flags.WritePlacement(new(123, -456, 789));
        Assert.Equal((byte)0x3F, placement.KnownMask);
        Assert.Equal(new NativeBattleVector(123, -456, 789), placement.ReadEuler());
        NativeBattleEffectNodeScratch retained = new([1, 2, 3, 4, 5, 6, 0xAB, 0xCD], byte.MaxValue);
        retained = retained.WritePlacement(new(1, 2, 3)).WriteFlags(0x80004000);
        Assert.Equal(new byte[] { 3, 0, 0xAB, 0xCD }, retained.Bytes.Skip(4).ToArray());
        NativeBattleEffectNodeScratch restored = JsonSerializer.Deserialize<NativeBattleEffectNodeScratch>(JsonSerializer.Serialize(retained))!;
        Assert.Equal(JsonSerializer.Serialize(retained.WritePlacement(new(-1, -2, -3))), JsonSerializer.Serialize(restored.WritePlacement(new(-1, -2, -3))));
        Assert.Null((NativeBattleEffectMemory.FromColdProcess() with { NodeScratch2793E30 = null }).NodeScratch2793E30);
        Assert.Equal((byte)255, NativeBattleEffectMemory.FromColdProcess().NodeScratch2793E30!.KnownMask);
    }

    [Fact]
    public void Memory_flush_preserves_partial_unknowns_and_unproved_callers_do_not_publish_this_owner()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { NodeScratch2793E30 = null } };
        NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 0x25F8, NativeBattleCuragaSpawnTests.Node, 0x16F80, useSharedScratch: true);
        memory.WriteProjectionFlags(0x80020000);
        NativeBattleEffectMemory flushed = memory.FlushStorage().Memory;
        Assert.Equal((byte)0x0F, flushed.NodeScratch2793E30!.KnownMask);
        Assert.Throws<NotSupportedException>(() => flushed.NodeScratch2793E30.ReadEuler());
        Assert.Same(flushed.NodeScratch2793E30, flushed.BeginLoad(99).NodeScratch2793E30);
        frame = NativeBattleCuragaEmitterTests.Source(frame, source =>
        {
            source[0x21] = 1;
            source[0x34] = 2;
        });
        NativeBattleCuragaSpawnStep ordinary = NativeBattleCuragaNodeKinematics.Spawn8818A0(frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 1, new(1));
        Assert.Null(ordinary.Frame.Memory.NodeScratch2793E30);
    }

    [Fact]
    public void Zero_quad_sprite_publishes_all_four_post_roll_rotation_words()
    {
        byte[] source = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(12), 20);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 512) with { Z = 4096 };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleEffectNodeScratch scratch = NativeBattleEffectNodeScratch.Unknown();
        NativeBattleActionMatrix? published = null;
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(source.ToImmutableArray(), 0, matrix, projection, 1024, 17, 2, observeRolledMatrix: value =>
        {
            published = value;
            scratch = scratch.WriteSpriteMatrix(value);
        });
        Assert.Empty(batch.Attempts);
        Assert.NotNull(published);
        Assert.Equal((byte)255, scratch.KnownMask);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(published.Rotation[index + 4], BinaryPrimitives.ReadInt16LittleEndian(scratch.Bytes.AsSpan().Slice(index * 2)));
        }
        Assert.NotEqual(matrix.Rotation.ToArray(), published.Rotation.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mesh_flags_publish_first_three_even_on_rejection_and_ignore_fourth_projection(bool rejectFirst)
    {
        ImmutableArray<byte> mesh = Mesh(rejectFirst);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleEffectNodeScratch scratch = new(Enumerable.Repeat((byte)0xA5, 8).ToImmutableArray(), byte.MaxValue);
        int publications = 0;
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch884370(mesh, matrix, projection, new(0x30, null, null, null, null, null, 17, 2), observeFirstThreeFlags: flags =>
        {
            publications++;
            scratch = scratch.WriteFlags(flags);
        });
        Assert.Equal(1, publications);
        uint published = BinaryPrimitives.ReadUInt32LittleEndian(scratch.Bytes.AsSpan());
        Assert.Equal(rejectFirst ? 0x80060000u : 0u, published);
        Assert.All(scratch.Bytes.Skip(4), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(rejectFirst ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, Assert.Single(batch.Attempts).WriteMask[44]);
        Assert.NotEqual(0u, NativeBattleModuleProjection.Project45FE10(matrix, new(100, 100, 0), projection).Flags);
        byte[] empty = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(empty, 8);
        NativeBattleEffectNodeScratch before = scratch;
        NativeBattleRestorationMeshPackets.EmitBatch884370(empty.ToImmutableArray(), matrix, projection, new(0x30, null, null, null, null, null, 17, 2), observeFirstThreeFlags: flags => scratch = scratch.WriteFlags(flags));
        Assert.Same(before, scratch);
    }

    [Fact]
    public void Proved_caller_spawn_uses_retained_Euler_and_flushes_placement_without_resetting_fourth_word()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source => source[0x21] = 1);
        frame = frame with { Memory = frame.Memory with { NodeScratch2793E30 = null } };
        Assert.Throws<NotSupportedException>(() => Spawn(frame));
        NativeBattleEffectNodeScratch seed = new([0, 0, 0, 0, 0, 0, 0xAB, 0xCD], byte.MaxValue);
        frame = frame with { Memory = frame.Memory with { NodeScratch2793E30 = seed.WritePlacement(new(321, -654, 987)) } };
        NativeBattleEffectNodeMemoryWrite retained = Spawn(frame);
        Assert.Equal(frame.Memory.NodeScratch2793E30, retained.Memory.NodeScratch2793E30);
        NativeBattleCuragaFrame restored = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(Spawn(restored)));
        frame = NativeBattleCuragaEmitterTests.Source(frame, source => source[0x34] = 2);
        NativeBattleEffectNodeMemoryWrite placed = Spawn(frame);
        Assert.Equal(new NativeBattleVector(41, 0, 1024), placed.Memory.NodeScratch2793E30!.ReadEuler());
        Assert.Equal(new byte[] { 0xAB, 0xCD }, placed.Memory.NodeScratch2793E30.Bytes.Skip(6).ToArray());

        NativeBattleEffectNodeMemoryWrite Spawn(NativeBattleCuragaFrame current)
        {
            NativeBattleEffectNodeMemory memory = new(current.Memory, current.Effect, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 0x25F8, NativeBattleCuragaSpawnTests.Node, 0x16F80, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(NativeBattleCuragaExecutableState.Address, current.Memory.CuragaExecutable!.Data!.Value);
            NativeBattleEffectNodeInputs inputs = new(current.State.CopiedCamera, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => NativeBattleCuragaSpawnTests.Node);
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
            NativeCrtRandomState random = new(1);
            lifecycle.Spawn(NativeBattleCuragaSpawnTests.Emitter, 1, bank.Read(NativeBattleCuragaExecutableState.Address + 0x104, 0x158).ToArray(), ref random);
            return memory.FlushStorage();
        }
    }

    private static ImmutableArray<byte> Mesh(bool rejectFirst)
    {
        byte[] bytes = new byte[108];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] vertices = [new(-100, -100, rejectFirst ? (short)0 : (short)4096), new(100, -100, 4096), new(-100, 100, 4096), new(100, 100, 0)];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), vertices[index].Z);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x3C808080);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(76 + index * 2), (ushort)(index * 2));
        }
        return bytes.ToImmutableArray();
    }
}

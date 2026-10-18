using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectOwnedModelDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleShadowSource Shadow => new(View with { Z = 0 }, new(4096, 0, 4096), Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 10).ToImmutableArray());

    [Fact]
    public void Body_and_shadow_use_one_private_batch_and_preserve_body_padding_and_scratch_suffix()
    {
        var fixture = Fixture();
        NativeBattleEffectOwnedModelDraw draw = Draw(fixture.Task, fixture.State, fixture.Resource);
        Assert.Equal(272, draw.Packets.AllocationBytes);
        Assert.Equal(24, draw.ProjectedBytesWritten);
        Assert.All(draw.ProjectedScratch.AsSpan().Slice(24).ToArray(), value => Assert.Equal(0xA5, value));
        Assert.Equal(273, draw.Packets.Attempts[0].Primitive!.Bucket);
        Assert.All(draw.Packets.Attempts.Skip(1), attempt => Assert.Equal(4121, attempt.Primitive!.Bucket));
        Assert.Equal(32, draw.Packets.Attempts[1].ByteOffsetRelative);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, draw.Packets.Attempts[0].WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, draw.Packets.Attempts[0].WriteMask[31]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(1024, Enumerable.Repeat((byte)0xA5, 272).ToArray()) };
        string commonBefore = JsonSerializer.Serialize(memory.PrimitiveProcess);
        NativeBattleEffectPacketWrite appended = NativeBattleEffectPacketArena.Append(memory, 1024, NativeBattleLogicalOrdering.Clear(), draw.Packets);
        Assert.Equal(1296, appended.Cursor);
        Assert.Equal(commonBefore, JsonSerializer.Serialize(appended.Memory.PrimitiveProcess));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, appended.Memory.Arena!.Read(1024 + 30, 2));
    }

    [Fact]
    public void World_matrix_bounds_and_shadow_gate_are_task_owned_without_an_actor_binding()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = fixture.Task.WriteWord(0x82, 0xA5A5).WriteWord(0x4C, 100).WriteWord(0x4E, 200).WriteWord(0x50, 300).WriteDword(0x114, 2048).WriteByte(0x42, 0xA5);
        NativeBattleEffectOwnedModelDraw draw = Draw(task, fixture.State, fixture.Resource);
        Assert.Equal((ushort)2048, draw.Task.ReadWord(0x70));
        Assert.Equal((ushort)0, draw.Task.ReadWord(0x82));
        Assert.Equal(100u, draw.Task.ReadDword(0x84));
        Assert.Equal(200u, draw.Task.ReadDword(0x88));
        Assert.Equal(300u, draw.Task.ReadDword(0x8C));
        Assert.Equal((byte)0xA5, draw.Task.ReadByte(0x42));
        Assert.Equal(unchecked((ushort)draw.Bounds.Minimum.X), draw.Task.ReadWord(0x64));
        Assert.Equal(unchecked((ushort)draw.Bounds.Maximum.Z), draw.Task.ReadWord(0x6E));
        NativeBattleEffectOwnedModelDraw hiddenShadow = Draw(fixture.Task.WriteByte(0x30, (byte)(fixture.Task.ReadByte(0x30) | 0x20)), fixture.State, fixture.Resource);
        Assert.Equal(32, hiddenShadow.Packets.AllocationBytes);
        Assert.Single(hiddenShadow.Packets.Attempts);
    }

    [Fact]
    public void Object_mask_skips_projection_and_body_but_keeps_shadow_and_fails_unproved_alpha_or_bounds_scale()
    {
        var fixture = Fixture();
        NativeBattleEffectOwnedModelDraw masked = Draw(fixture.Task.WriteDword(0xAC, 0), fixture.State, fixture.Resource);
        Assert.Equal(240, masked.Packets.AllocationBytes);
        Assert.Equal(0, masked.ProjectedBytesWritten);
        Assert.All(masked.ProjectedScratch, value => Assert.Equal(0xA5, value));
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Task.WriteByte(0x37, 1), fixture.State, fixture.Resource));
        Assert.Throws<NotSupportedException>(() => Draw(fixture.Task.WriteWord(0x13E, 2048), fixture.State, fixture.Resource));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOwnedModelDrawing.Draw7558A0(fixture.Task, fixture.State, fixture.Resource, View, Projection, Shadow, new(true), NativeBattleGteColorInterpolation.CodeConstants45F270, [0]));
    }

    [Fact]
    public void Independent_restore_keeps_exact_current_pose_world_bounds_scratch_and_next_draw_packets()
    {
        var fixture = Fixture();
        NativeBattleEffectOwnedModelStep original = new(fixture.Task, fixture.State, false);
        NativeBattleEffectOwnedModelStep restored = JsonSerializer.Deserialize<NativeBattleEffectOwnedModelStep>(JsonSerializer.Serialize(original))!;
        NativeBattleEffectOwnedModelDraw expected = Draw(original.Task, original.State, fixture.Resource);
        NativeBattleEffectOwnedModelDraw actual = Draw(restored.Task, restored.State, fixture.Resource);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal((ushort)0, original.Task.ReadWord(0x70));
    }

    private static NativeBattleEffectOwnedModelDraw Draw(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource) => NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, state, resource, View, Projection, Shadow, new(true), NativeBattleGteColorInterpolation.CodeConstants45F270, Enumerable.Repeat((byte)0xA5, 32).ToImmutableArray());

    private static (NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState State, NativeBattleEffectOwnedModelResource Resource) Fixture()
    {
        byte[] bytes = new byte[104];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] offsets = [24, 88, 96, 100, 104];
        for (int index = 0; index < offsets.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), offsets[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        NativeBattlePoseSource source = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), [1]);
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-50, -50, 0), 0), new(new(50, -50, 0), 0), new(new(0, 50, 0), 0)], [new(false, face.ToImmutableArray())]);
        NativeBattleEffectOwnedModelResource resource = new(0x1000, bytes.ToImmutableArray(), source, [mesh]);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x7557B0, new byte[0x154 - 12].ToImmutableArray());
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(task, 0x2200000, resource, 3, 0x32808080);
        task = initialized.Task.WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
        NativeBattleDecodedPose pose = new(new(0, 0, 0), [new(new(0, 0, 0), new(4096, 4096, 4096))], false);
        return (task, initialized.State with { DecodedPose = pose }, resource);
    }
}

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
    public void Multiple_objects_share_one_renderer_entry_and_append_body_packets_before_shadow()
    {
        var fixture = Fixture();
        NativeBattleEffectOwnedModelResource resource = fixture.Resource with { Geometry = [fixture.Resource.Geometry[0], fixture.Resource.Geometry[0]] };
        int entries = 0;
        NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(fixture.Task, fixture.State, resource, View, Projection, Shadow, new(true), NativeBattleGteColorInterpolation.CodeConstants45F270, Enumerable.Repeat((byte)0xA5, 32).ToImmutableArray(), () =>
        {
            entries++;
            return true;
        });
        Assert.Equal(1, entries);
        Assert.Equal(64, draw.BodyAllocationBytes);
        Assert.Equal(304, draw.Packets.AllocationBytes);
        Assert.Equal(24, draw.ProjectedBytesWritten);
        Assert.Equal(0, draw.Packets.Attempts[0].ByteOffsetRelative);
        Assert.Equal(32, draw.Packets.Attempts[1].ByteOffsetRelative);
        Assert.Equal(64, draw.Packets.Attempts[2].ByteOffsetRelative);
        Assert.Equal(draw.Packets.Attempts[0].Primitive!.Words, draw.Packets.Attempts[1].Primitive!.Words);
    }

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
    public void Object_mask_skips_projection_and_body_but_keeps_shadow()
    {
        var fixture = Fixture();
        NativeBattleEffectOwnedModelDraw masked = Draw(fixture.Task.WriteDword(0xAC, 0), fixture.State, fixture.Resource);
        Assert.Equal(240, masked.Packets.AllocationBytes);
        Assert.Equal(0, masked.ProjectedBytesWritten);
        Assert.All(masked.ProjectedScratch, value => Assert.Equal(0xA5, value));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOwnedModelDrawing.Draw7558A0(fixture.Task, fixture.State, fixture.Resource, View, Projection, Shadow, new(true), NativeBattleGteColorInterpolation.CodeConstants45F270, [0]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(63)]
    [InlineData(255)]
    public void Native_alpha_context_preserves_final_packet_material_and_task_alpha(byte alpha)
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = fixture.Task.WriteDword(0x58, 0x02808080);
        NativeBattleEffectOwnedModelDraw ordinary = Draw(task, fixture.State, fixture.Resource);
        NativeBattleEffectOwnedModelDraw faded = Draw(task.WriteByte(0x37, alpha), fixture.State, fixture.Resource);
        Assert.Equal(JsonSerializer.Serialize(ordinary.Packets), JsonSerializer.Serialize(faded.Packets));
        Assert.Equal(ordinary.ProjectedScratch, faded.ProjectedScratch);
        Assert.Equal(alpha, faded.Task.ReadByte(0x37));
    }

    [Fact]
    public void SharedRendererEntryCanSkipOnlyBodyWhileKeepingWorldBoundsAndShadow()
    {
        var fixture = Fixture();
        int entries = 0;
        NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(fixture.Task, fixture.State, fixture.Resource, View, Projection, Shadow, new(true), NativeBattleGteColorInterpolation.CodeConstants45F270, Enumerable.Repeat((byte)0xA5, 32).ToImmutableArray(), () =>
        {
            entries++;
            return false;
        });
        Assert.Equal(1, entries);
        Assert.Equal(0, draw.BodyAllocationBytes);
        Assert.Equal(0, draw.ProjectedBytesWritten);
        Assert.Equal(240, draw.Packets.AllocationBytes);
        Assert.All(draw.Packets.Attempts, attempt => Assert.Equal(4121, attempt.Primitive!.Bucket));
        Assert.All(draw.ProjectedScratch, value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((ushort)4096, draw.Task.ReadWord(0x70));
        Assert.DoesNotContain("BodyAllocationBytes", JsonSerializer.Serialize(draw));
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

    [Theory]
    [InlineData(9216, -5, 1, -10, 5)]
    [InlineData(5120, -3, 0, -6, 2)]
    [InlineData(4096, -3, 0, -5, 2)]
    [InlineData(2048, -2, -1, -3, 0)]
    [InlineData(-2048, 0, -1, 1, -2)]
    public void Shadow_XZ_scale_uses_signed_midpoints_and_truncation_toward_zero(int scale, int minX, int maxX, int minZ, int maxZ)
    {
        NativeBattleEffectLocalBounds result = NativeBattleEffectOwnedModelDrawing.ScaleShadowBounds7156EE(new(new(-3, 5, -5), new(0, 7, 2)), (short)scale);
        Assert.Equal(new NativeBattleEffectLocalBounds(new((short)minX, 5, (short)minZ), new((short)maxX, 7, (short)maxZ)), result);
    }

    [Fact]
    public void Shadow_scaled_coordinates_wrap_as_words_while_body_projection_is_unchanged()
    {
        NativeBattleEffectLocalBounds wrapped = NativeBattleEffectOwnedModelDrawing.ScaleShadowBounds7156EE(new(new(-30000, -10, -30000), new(30000, 10, 30000)), 9216);
        Assert.Equal(new NativeBattleEffectLocalBounds(new(-1964, -10, -1964), new(1964, 10, 1964)), wrapped);
        var fixture = Fixture();
        NativeBattleEffectOwnedModelDraw original = Draw(fixture.Task, fixture.State, fixture.Resource);
        NativeBattleEffectOwnedModelDraw scaled = Draw(fixture.Task.WriteWord(0x13E, 9216), fixture.State, fixture.Resource);
        Assert.Equal(NativeBattleEffectOwnedModelDrawing.ScaleShadowBounds7156EE(original.Bounds, 9216), scaled.Bounds);
        Assert.Equal(unchecked((ushort)scaled.Bounds.Minimum.X), scaled.Task.ReadWord(0x64));
        Assert.Equal(unchecked((ushort)scaled.Bounds.Maximum.Z), scaled.Task.ReadWord(0x6E));
        Assert.Equal(original.ProjectedScratch, scaled.ProjectedScratch);
        Assert.Equal(JsonSerializer.Serialize(original.Packets.Attempts[0]), JsonSerializer.Serialize(scaled.Packets.Attempts[0]));
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

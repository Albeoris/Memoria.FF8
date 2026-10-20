using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePain34Tests
{
    [Fact]
    public void Current_queue_stack_has_exact_writes_and_a_fresh_queue_cannot_inherit_a_prior_one()
    {
        NativeBattleStackBytes stack = NativeBattleStackBytes.Unknown("pain-player-queue", -0x94, 4);
        Assert.Throws<NotSupportedException>(() => NativeBattlePain34Players.RequirePhysicalBlock40(stack));
        stack = stack.StoreUInt32(-0x94, 0, 0x865A62, "player-state0-bone-fraction");
        Assert.Equal(0u, NativeBattlePain34Players.RequirePhysicalBlock40(stack));
        stack = stack.StoreUInt32(-0x94, 1, 0x865AA8, "player-state1-block40");
        Assert.Equal(1u, NativeBattlePain34Players.RequirePhysicalBlock40(stack));
        NativeBattleStackBytes restored = JsonSerializer.Deserialize<NativeBattleStackBytes>(JsonSerializer.Serialize(stack))!;
        Assert.Equal(1u, NativeBattlePain34Players.RequirePhysicalBlock40(restored));
        NativeBattleStackBytes next = NativeBattleStackBytes.Unknown("pain-player-queue", -0x94, 4);
        Assert.Throws<NotSupportedException>(() => NativeBattlePain34Players.RequirePhysicalBlock40(next));
    }

    [Theory]
    [InlineData(3, 44, 0x0A000000u)]
    [InlineData(7, 56, 0x0D000000u)]
    public void Glow_embeds_one_draw_mode_word_in_each_actual_packet(int group, int packetLength, uint header)
    {
        (ImmutableArray<byte> mesh, ImmutableArray<byte> vertices) = Mesh(group);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattlePain34Drawing.Emit8DE9D0(mesh, 0, vertices, matrix, projection, 0x2430, 0, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(packetLength, batch.AllocationBytes);
        Assert.Equal(packetLength, attempt.PacketLength);
        Assert.Equal(header, primitive.Words[0]);
        Assert.Equal(0xE1000220u, primitive.Words[^1]);
        Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(packetLength - 4)));
        Assert.All(attempt.WriteMask.Skip(packetLength - 4), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void Billboard_branch_keeps_word_offsets_separate_from_camera_rotation()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x200);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 200);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 300);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024) with { X = 7, Y = 8, Z = 9 };
        NativeBattleActionMatrix actual = NativeBattlePain34Drawing.Compose(node, new(20, 30, 40), camera);
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(camera, new(20, 30, 40));
        Assert.Equal(unchecked(x + 107), actual.X);
        Assert.Equal(unchecked(y + 208), actual.Y);
        Assert.Equal(unchecked(z + 309), actual.Z);
        Assert.Equal(NativeBattleCuragaMatrices.Identity8DD770().Rotation.ToArray(), actual.Rotation.ToArray());
    }

    [Theory]
    [InlineData(0u, 40, false)]
    [InlineData(1u, 44, true)]
    public void Actual_physical_word_selects_normal_or_glow_renderer(uint physical, int allocation, bool publishesMode)
    {
        (ImmutableArray<byte> mesh, ImmutableArray<byte> vertices) = Mesh(3);
        byte[] program = new byte[12 + mesh.Length];
        BinaryPrimitives.WriteInt32LittleEndian(program, 1);
        BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(8), 12);
        mesh.AsSpan().CopyTo(program.AsSpan(12));
        vertices.AsSpan().CopyTo(program.AsSpan(24));
        byte[] record = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(24 + axis * 2), 4096);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Pain34 = NativeBattlePain34State.FromColdProcess() };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, new byte[88]).Write(NativeBattleTemporaryMemory.Cursor, BitConverter.GetBytes(NativeBattleTemporaryMemory.Address)).Write(NativeBattleTemporaryMemory.Address + 40, BitConverter.GetBytes(0xA1B2C3D4u));
        NativeBattleOriginalEffectState effect = new(34, 34, new(34, 0x865600, 0x8655E0), null, null);
        NativeBattlePain34Frame frame = new(memory, effect, staging) { PlayerQueueStack = NativeBattleStackBytes.Unknown("pain-player-queue", -0x94, 4).StoreUInt32(-0x94, physical, physical == 1 ? 0x865AA8u : 0x865E42u, "declared-current-physical-store") };
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattlePain34DrawingStep drawn = NativeBattlePain34Drawing.Draw865AE0(frame, new(0x1000000, program.ToImmutableArray()), new(0, 0, record.ToImmutableArray()), new(0, 0, 0), new(matrix, 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(allocation, drawn.Packets.AllocationBytes);
        Assert.NotNull(Assert.Single(drawn.Packets.Attempts).Primitive);
        Assert.Equal(publishesMode, drawn.DrawMode2795BD0.HasValue);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 24, 4)));
        Assert.Equal(checked(0x1000000u + (uint)program.Length), BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 32, 4)));
        Assert.NotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 36, 4)));
        Assert.Equal(0xA1B2C3D4u, BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 40, 4)));
        Assert.Equal(250u, BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 44, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Staging.Read(NativeBattleTemporaryMemory.Address + 48, 4)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(drawn.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }
    [Fact]
    public void Rejected_glow_attempt_retains_area_depth_and_trailing_mode_bytes()
    {
        (ImmutableArray<byte> mesh, ImmutableArray<byte> original) = Mesh(3);
        byte[] vertices = original.ToArray();
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8 + 4), 0);
        }
        uint observedFlags = 0;
        int observedArea = 12345;
        uint observedDepth = 67890;
        int sourceCursor = -1;
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGfGeometryBatch batch = NativeBattlePain34Drawing.Emit8DE9D0(mesh, 0, vertices.ToImmutableArray(), matrix, new(matrix, 216, 160, 108), 0x2430, 0, 0, NativeBattleGteColorInterpolation.CodeConstants45F270, flags => observedFlags = flags, area => observedArea = area, depth => observedDepth = depth, cursor => sourceCursor = cursor);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.NotEqual(0u, observedFlags & 0x60000);
        Assert.Equal(12345, observedArea);
        Assert.Equal(67890u, observedDepth);
        Assert.Equal(mesh.Length, sourceCursor);
        Assert.All(attempt.WriteMask.Skip(attempt.PacketLength - 4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }
    [Fact]
    public void Controller_persists_emitter_clocks_releases_its_parent_and_completes_the_physical_root()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 45, 0, 34, 1, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 26, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(34, 34, new(34, 0x865600, 0x8655E0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26BA9E0, 0x10000)] };
        NativeBattlePain34Resources resources = new(new(0x15E7040, []), new(0x15EA2E8, []), new("declared-unused-camera", []), 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Pain34 = NativeBattlePain34State.FromColdProcess() };
        NativeBattlePain34Frame frame = NativeBattlePain34Controller.Initialize865600(new(memory, effect, NativeBattleFileStaging.Unknown()), resources, packets);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint rootAddress = NativeBattlePain34Controller.Bank;
        hierarchy = hierarchy.Write(rootAddress, hierarchy.Read(rootAddress).WriteByte(41, 4).WriteByte(99, 1));
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, 0x8658A0, 88, rootAddress, (pointer, header, target) => NativeBattleCureController.SelectActors(effect.Parameters!, packets, pointer, header, target));
        uint emitterAddress = appended.Address;
        hierarchy = appended.Hierarchy;
        hierarchy = hierarchy.Write(emitterAddress, hierarchy.Read(emitterAddress).WriteByte(41, 1).WriteWord(36, 19));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource poseSource = new(skeleton.ToImmutableArray(), [0]);
        NativeBattleDecodedPose pose = new(new(0, 0, 0), [new(new(0, 0, 0), new(4096, 4096, 4096))], false);
        NativeBattlePackedMatrix32 camera = new(new byte[32].ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray());
        int contacts = 0;
        NativeBattlePain34Inputs inputs = new(packets, camera, actor => new(actor, 0) { SequenceState = new() { DecodedPose = pose } }, _ => poseSource, (_, _) => throw new InvalidOperationException("No player is declared in this controller regression."), (current, index) =>
        {
            Assert.Equal(0, index);
            contacts++;
            return current;
        }, _ => throw new InvalidOperationException("An emitter at age19 must not replay its first-frame sound."));
        frame = NativeBattlePain34Controller.Advance865720(frame, resources, inputs);
        hierarchy = new(frame.Effect.Pools);
        Assert.Equal((ushort)20, hierarchy.Read(emitterAddress).ReadWord(36));
        Assert.Equal((ushort)1, hierarchy.Read(rootAddress).ReadWord(36));
        Assert.Equal((byte)1, hierarchy.Read(rootAddress).ReadByte(99));
        frame = NativeBattlePain34Controller.Advance865720(frame, resources, inputs);
        hierarchy = new(frame.Effect.Pools);
        Assert.Equal((ushort)21, hierarchy.Read(emitterAddress).ReadWord(36));
        Assert.Equal((byte)0, hierarchy.Read(rootAddress).ReadByte(99));
        Assert.Equal((byte)2, hierarchy.Read(emitterAddress).ReadByte(41));
        frame = NativeBattlePain34Controller.Advance865720(frame, resources, inputs);
        Assert.Equal(1, contacts);
        Assert.Equal(0, frame.Effect.Pools[1].Slots[0].Flags & 1);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(rootAddress).ReadByte(40));
        for (int callback = 0; callback < 5; callback++)
        {
            frame = NativeBattlePain34Controller.Advance865720(frame, resources, inputs);
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
        Assert.Equal(1, contacts);
    }
    private static (ImmutableArray<byte> Mesh, ImmutableArray<byte> Vertices) Mesh(int selectedGroup)
    {
        int faceLength = selectedGroup == 3 ? 24 : 36;
        byte[] mesh = new byte[44 + 32 + faceLength];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 44);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 4);
        int cursor = 44;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(cursor), group == selectedGroup ? 1 : 0);
            cursor += 4;
            if (group != selectedGroup)
            {
                continue;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor), selectedGroup == 3 ? 0x2C808080u : 0x3C808080u);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(cursor + 4 + corner * 2), checked((ushort)(corner * 2)));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 12), 0x12340201);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 16), 0xABCD0403);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 20), 0x08070605);
            if (selectedGroup == 7)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(cursor + 24 + corner * 4), 0x00808080);
                }
            }
            cursor += faceLength;
        }
        byte[] vertices = new byte[32];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8), (short)((corner & 1) == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8 + 2), (short)(corner < 2 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8 + 4), 1000);
        }
        return (mesh.ToImmutableArray(), vertices.ToImmutableArray());
    }
}

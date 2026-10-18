using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterActorDrawTests
{
    private const uint Manager = 0x2797690;
    private const uint Header = 0x2797068;
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleActionMatrix Camera = new([4096, 0, 0, 0, 0, -4096, 0, 4096, 0], 0, 0, 3000, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(null, 216, 0, 0);

    [Fact]
    public void Accepted_shadow_has_eight_compound_packets_zero_proxy_depths_and_restores_actual_local_pose()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        List<string> calls = [];
        ImmutableArray<NativeBattleActionMatrix> published = [];
        NativeBattleWaterActorPoseServices services = Services(calls, bones => published = bones);
        uint? table = null;
        NativeBattleWaterActorShadowResult result = NativeBattleWaterActorDraw.FloorShadowA9C670(frame, Model(), Identity, new(0, 0, 0), services, Projection, 0, 0, 0x12340000, address =>
        {
            table = address;
            return 4121;
        });
        Assert.Equal(new[] { "local", "world", "local" }, calls);
        NativeBattleActionMatrix actualWorld = Assert.Single(published);
        Assert.Equal(Identity.Rotation.ToArray(), actualWorld.Rotation.ToArray());
        Assert.Equal((Identity.X, Identity.Y, Identity.Z, Identity.Scale), (actualWorld.X, actualWorld.Y, actualWorld.Z, actualWorld.Scale));
        Assert.Equal((ushort)0x23, result.Actor.Flags0);
        Assert.Equal(288, result.Packets.AllocationBytes);
        Assert.Equal(0x3120u, WaterSchedulerFixture.ReadCoreDword(result.Frame, Manager + 0x7C));
        Assert.Equal(0x12344064u, table);
        Assert.Equal(8, result.Packets.Attempts.Length);
        foreach (NativeBattleGfGeometryAttempt attempt in result.Packets.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.Equal(4121, primitive.Bucket);
            Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4);
            Assert.Equal(new uint[] { 0x08000000, 0xE1000040, 0, 0x30112233 }, primitive.Words.Take(4));
            Assert.Equal(0u, primitive.Words[5]);
            Assert.Equal(0u, primitive.Words[7]);
            Assert.Equal(Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 3), attempt.WriteMask.Take(3));
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(221);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0x3000, NativeBattleLogicalOrdering.Clear(), result.Packets);
        Assert.Equal(8, written.Ordering.Packets.Length);
        Assert.Equal(8, written.Ordering.Proxies.Length);
        Assert.Equal((byte)8, written.Memory.Arena!.Read(0x3003, 1)[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    public void Zero_or_negative_winding_culls_only_packets_and_always_restores_local_pose(short cameraYFromZ)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = CameraOwner(frame, Camera with { Rotation = [4096, 0, 0, 0, 0, cameraYFromZ, 0, 4096, 0] });
        frame = frame with { Core = frame.Core.MarkUnknown(Manager + 0x7C, 4) };
        List<string> calls = [];
        NativeBattleWaterActorShadowResult result = NativeBattleWaterActorDraw.FloorShadowA9C670(frame, Model() with { Color44 = null }, Identity, new(0, 0, 0), Services(calls), Projection, short.MaxValue, int.MinValue, 0, UnexpectedTable);
        Assert.Empty(result.Packets.Attempts);
        Assert.Equal(new[] { "local", "world", "local" }, calls);
        Assert.Same(frame.Core, result.Frame.Core);
        Assert.Throws<NotSupportedException>(() => result.Frame.Core.Read(Manager + 0x7C, 4));
        Assert.Equal((ushort)0x23, result.Actor.Flags0);
    }

    [Fact]
    public void Own_bone_length_extends_root_padded_bounds_and_all_nine_padding_words_remain_unknown()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        bool[] known = frame.TemporaryFrame.Known.ToArray();
        for (int index = 0; index < 9; index++)
        {
            known[0xB6 + index * 8] = false;
            known[0xB7 + index * 8] = false;
        }
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = known.ToImmutableArray() } };
        byte[] skeleton = Skeleton(2);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x12), 30000);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x42), 1000);
        ImmutableArray<NativeBattleActionMatrix> bones = [Identity, Identity with { Z = 2000 }];
        NativeBattleWaterActorPoseServices services = new((current, _) => new(current, skeleton.ToImmutableArray(), bones), KeepWorld);
        NativeBattleWaterActorShadowResult result = NativeBattleWaterActorDraw.FloorShadowA9C670(frame, Model(), Identity, new(0, 0, 0), services, Projection, 0, 0, 0, Resolve);
        Assert.Equal((ushort)1750, result.Frame.ReadTemporaryWord(0xB4));
        NativeBattleModuleProjectedVertex rootPaddedX = NativeBattleModuleProjection.Project45FAA0(Camera with { Y = -1250 }, new(500, 0, 0), Projection, 0, 0);
        Assert.Equal(rootPaddedX.Screen, result.Frame.ReadTemporaryDword(0xC0));
        Assert.Equal((ushort)0, result.Frame.ReadTemporaryWord(0xC4));
        for (int index = 0; index < 9; index++)
        {
            Assert.False(result.Frame.TemporaryFrame.Known[0xB6 + index * 8]);
            Assert.False(result.Frame.TemporaryFrame.Known[0xB7 + index * 8]);
        }
    }

    [Fact]
    public void Camera_center_uses_saturated_IR_translation_and_does_not_cull_projection_flags()
    {
        NativeBattleWaterExecutionFrame frame = CameraOwner(Fixture(), Camera with { X = 50000, Z = 50000 });
        NativeBattleWaterActorShadowResult result = NativeBattleWaterActorDraw.FloorShadowA9C670(frame, Model(), Identity, new(0, 0, 0), Services([]), Projection, short.MinValue, int.MinValue, 0, Resolve);
        NativeBattleActionMatrix expected = Camera with { X = short.MaxValue, Z = short.MaxValue };
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FAA0(expected, new(0, 0, 0), Projection, short.MinValue, int.MinValue);
        Assert.Equal(projected.Screen, result.Frame.ReadTemporaryDword(0xF0));
        Assert.Equal(projected.Flags, result.Frame.ReadTemporaryDword(0xFC));
        Assert.NotEqual(0u, projected.Flags);
        Assert.Equal(8, result.Packets.Attempts.Length);
    }

    [Fact]
    public void Wrapper_caches_offset_once_changes_current_actor_only_and_reuses_same_pose_for_two_anchors()
    {
        NativeBattleWaterExecutionFrame frame = WrapperFixture();
        List<string> calls = [];
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        NativeBattleWaterActorDrawResult first = NativeBattleWaterActorDraw.Draw26A9C340(frame, models, Services(calls), Projection, 0, 0, 0, Resolve);
        Assert.Equal(new[] { "local", "local", "world", "local" }, calls);
        NativeBattleGraphicsModel actor = first.Models[0];
        Assert.Equal((ushort)0x1023, actor.Flags0);
        Assert.Equal(new NativeBattleVector(6, 8, 10), actor.Transform!.Position28);
        Assert.Equal(models[0].GeometryWorldMatrix, actor.GeometryWorldMatrix);
        Assert.Equal(models[0].Transform!.Scale148, actor.Transform.Scale148);
        Assert.Equal(models[0].Transform!.Vertical34, actor.Transform.Vertical34);
        Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0 }, first.Frame.Arena.Read(0x20BC, 6));
        Assert.Equal(uint.MaxValue, WaterSchedulerFixture.ReadSlotDword(first.Frame, 0, 0xC4));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(first.Frame.Arena.Read(0x20C2, 2)));
        Assert.False(first.Frame.TemporaryFrame.Known[0x72]);
        Assert.Equal((ushort)0xA5A5, first.Frame.ReadTemporaryWord(0x56));
        for (int index = 1; index < 7; index++)
        {
            Assert.Same(models[index], first.Models[index]);
        }
        NativeBattleWaterExecutionFrame again = WaterSchedulerFixture.SlotWord(first.Frame, 0, 0x8E, 1024);
        ImmutableArray<NativeBattleGraphicsModel> warm = first.Models.SetItem(0, first.Models[0] with { GeometryWorldMatrix = null });
        NativeBattleWaterActorDrawResult next = NativeBattleWaterActorDraw.Draw26A9C340(again, warm, Services([]), Projection, 0, 0, 0, Resolve);
        Assert.Equal(new NativeBattleVector(-14, 8, 50), next.Models[0].Transform!.Position28);
        Assert.Equal((short)1024, next.Models[0].Rotation14);
        Assert.Null(next.Models[0].GeometryWorldMatrix);
    }

    [Fact]
    public void First_cache_saturates_IR_with_actual_retained_backup_translation()
    {
        NativeBattleWaterExecutionFrame frame = WrapperFixture();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27971E4 + 20, 50000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27971E4 + 24, unchecked((uint)-50000));
        NativeBattleWaterActorDrawResult result = NativeBattleWaterActorDraw.Draw26A9C340(frame, Models(), Services([]), Projection, 0, 0, 0, Resolve);
        Assert.Equal((ushort)short.MaxValue, WaterSchedulerFixture.ReadSlotWord(result.Frame, 0, 0xBC));
        Assert.Equal(unchecked((ushort)short.MinValue), WaterSchedulerFixture.ReadSlotWord(result.Frame, 0, 0xBE));
    }

    [Fact]
    public void Independent_JSON_next_preserves_slot_cache_unknown_padding_and_current_actor()
    {
        NativeBattleWaterActorDrawResult first = NativeBattleWaterActorDraw.Draw26A9C340(WrapperFixture(), Models(), Services([]), Projection, 0, 0, 0, Resolve);
        NativeBattleWaterExecutionFrame clone = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(first.Frame))!;
        ImmutableArray<NativeBattleGraphicsModel> actors = JsonSerializer.Deserialize<ImmutableArray<NativeBattleGraphicsModel>>(JsonSerializer.Serialize(first.Models));
        NativeBattleWaterActorDrawResult expected = NativeBattleWaterActorDraw.Draw26A9C340(first.Frame, first.Models, Services([]), Projection, 0, 0, 0, Resolve);
        NativeBattleWaterActorDrawResult actual = NativeBattleWaterActorDraw.Draw26A9C340(clone, actors, Services([]), Projection, 0, 0, 0, Resolve);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void Missing_published_world_or_uncaptured_actor_pointer_is_not_synthesized()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterActorDraw.Draw26A9C340(WrapperFixture(), models.SetItem(0, models[0] with { GeometryWorldMatrix = null }), Services([]), Projection, 0, 0, 0, Resolve));
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WrapperFixture(), Header + 0x60, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterActorDraw.Draw26A9C340(frame, models, Services([]), Projection, 0, 0, 0, Resolve));
    }

    private static NativeBattleWaterActorPoseServices Services(List<string> calls, Action<ImmutableArray<NativeBattleActionMatrix>>? observe = null)
    {
        ImmutableArray<NativeBattleActionMatrix> local = [Identity];
        return new((frame, actor) =>
        {
            calls.Add("local");
            return new(frame, Skeleton(1).ToImmutableArray(), local);
        }, (frame, actor, bones) =>
        {
            calls.Add("world");
            observe?.Invoke(bones);
            return frame;
        });
    }

    private static NativeBattleWaterExecutionFrame KeepWorld(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsModel actor, ImmutableArray<NativeBattleActionMatrix> bones) => frame;
    private static int Resolve(uint address) => 4121;
    private static int UnexpectedTable(uint address) => throw new InvalidOperationException("Culled shadow consumed an ordering-table pointer.");

    private static NativeBattleGraphicsModel Model() => new(0, 3) { Color44 = 0x30112233, Transform = new(new(0, 0, 0), new(100, 200, 300), 17, 19, 4096), GeometryWorldMatrix = Identity with { X = 110, Y = 220, Z = 330 } };
    private static ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(index => Model() with { Index = index }).ToImmutableArray();

    private static byte[] Skeleton(int count)
    {
        byte[] bytes = new byte[0x10 + count * 0x30];
        bytes[0] = checked((byte)count);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x10), -1);
        return bytes;
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, 0x3000);
        return CameraOwner(frame, Camera);
    }

    private static NativeBattleWaterExecutionFrame WrapperFixture()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = WaterSchedulerFixture.CoreDword(frame, Header + 0x60, 0x1D972C0);
        frame = CoreVector(frame, Header + 0x84, new(1, 2, 3));
        frame = CoreVector(frame, Header + 0xD8, new(0, 0, 0));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 5);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 6);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC2, 0xBEEF);
        return frame;
    }

    private static NativeBattleWaterExecutionFrame CoreVector(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleVector value)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), value.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), value.Z);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }

    private static NativeBattleWaterExecutionFrame CameraOwner(NativeBattleWaterExecutionFrame frame, NativeBattleActionMatrix matrix)
    {
        byte[] bytes = new byte[32];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), matrix.Rotation[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
        return frame with { Core = frame.Core.Write(0x27979E8, bytes) };
    }
}

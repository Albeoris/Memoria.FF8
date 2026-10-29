using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup15ItemCaptureTests
{

    [Theory]
    [InlineData((ushort)292)]
    [InlineData((ushort)293)]
    [InlineData((ushort)294)]
    [InlineData((ushort)295)]
    [InlineData((ushort)296)]
    public void First_child_draw_uses_its_live_native_owner_before_pool_publication_and_keeps_native_state(ushort effect)
    {
        ItemFixture native = Fixture(effect);
        ItemFixture captured = Fixture(effect);
        List<NativeBattlePilotVisualIdentity> identities = [];
        NativeBattleStatusCureItemInputs inputs = captured.Inputs with
        {
            DrawWithPilotIdentity = (frame, node, _, identity) =>
            {
                Assert.Single(frame.Effect.Pools);
                NativeBattlePilotVisualIdentity owner = Assert.IsType<NativeBattlePilotVisualIdentity>(identity);
                Assert.Equal(effect, owner.Effect);
                Assert.Equal(node.ResourceOffset, owner.Primitive);
                Assert.Equal(((long)captured.Resources.Layout.Address << 32) | 20u, owner.ResourceGeneration);
                identities.Add(owner);
                return frame;
            }
        };
        NativeBattleStatusCureItemStep nativeFirst = NativeBattleStatusCureItemController.Advance(native.Frame, native.Resources, native.Inputs);
        NativeBattleStatusCureItemStep capturedFirst = NativeBattleStatusCureItemController.Advance(captured.Frame, captured.Resources, inputs);
        Assert.Empty(identities);
        NativeBattleStatusCureItemStep nativeBirth = NativeBattleStatusCureItemController.Advance(nativeFirst.Frame, native.Resources, native.Inputs);
        NativeBattleStatusCureItemStep capturedBirth = NativeBattleStatusCureItemController.Advance(capturedFirst.Frame, captured.Resources, inputs);
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeBirth.Frame), NativeBattleTestSnapshots.Json(capturedBirth.Frame));
        Assert.Equal(nativeBirth.Contacts, capturedBirth.Contacts);
        Assert.Equal(nativeBirth.Sounds, capturedBirth.Sounds);
        if (effect is 292 or 293 or 294 or 295 or 296)
        {
            NativeBattlePilotVisualIdentity identity = Assert.Single(identities);
            NativeBattleModulePoolTask actualChild = capturedBirth.Frame.Effect.Pools[1].Slots[0];
            Assert.Equal(NativeBattlePilotVisualLifetimes.Identity(effect, actualChild, 32) with { ResourceGeneration = identity.ResourceGeneration }, identity);
            ItemFixture other = Fixture(effect);
            NativeBattlePilotVisualIdentity? next = null;
            NativeBattleStatusCureItemInputs otherInputs = other.Inputs with
            {
                DrawWithPilotIdentity = (frame, _, _, owner) =>
                {
                    next = owner;
                    return frame;
                }
            };
            var first = NativeBattleStatusCureItemController.Advance(other.Frame, other.Resources, otherInputs);
            NativeBattleStatusCureItemController.Advance(first.Frame, other.Resources, otherInputs);
            NativeBattlePilotVisualIdentity nextIdentity = Assert.IsType<NativeBattlePilotVisualIdentity>(next);
            Assert.NotEqual(identity.Generation, nextIdentity.Generation);
        }
        else
        {
            Assert.Empty(identities);
        }
    }

    [Theory]
    [InlineData((ushort)292)]
    [InlineData((ushort)293)]
    [InlineData((ushort)294)]
    [InlineData((ushort)295)]
    [InlineData((ushort)296)]
    public void Authored_node_identity_survives_workspace_frame_advance_and_preserves_contacts(ushort effect)
    {
        ItemFixture fixture = Fixture(effect);
        List<NativeBattlePilotVisualIdentity> identities = [];
        NativeBattleStatusCureItemInputs inputs = fixture.Inputs with
        {
            DrawWithPilotIdentity = (frame, _, _, identity) =>
            {
                identities.Add(Assert.IsType<NativeBattlePilotVisualIdentity>(identity));
                return frame;
            }
        };
        NativeBattleStatusCureItemStep step = NativeBattleStatusCureItemController.Advance(fixture.Frame, fixture.Resources, inputs);
        step = NativeBattleStatusCureItemController.Advance(step.Frame, fixture.Resources, inputs);
        Assert.Empty(step.Contacts);
        step = NativeBattleStatusCureItemController.Advance(step.Frame, fixture.Resources, inputs);
        Assert.Empty(step.Contacts);
        step = NativeBattleStatusCureItemController.Advance(step.Frame, fixture.Resources, inputs);
        Assert.Equal(new[] { 0 }, step.Contacts);
        Assert.Equal(0u, step.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(3, identities.Count);
        Assert.All(identities, identity => Assert.Equal(identities[0], identity));
    }

    [Theory]
    [InlineData((ushort)292, false)]
    [InlineData((ushort)292, true)]
    [InlineData((ushort)293, false)]
    [InlineData((ushort)293, true)]
    [InlineData((ushort)294, false)]
    [InlineData((ushort)294, true)]
    [InlineData((ushort)295, false)]
    [InlineData((ushort)295, true)]
    [InlineData((ushort)296, false)]
    [InlineData((ushort)296, true)]
    public void Selected_and_actual_morph_corners_keep_native_packets_arena_fourth_words_and_depth_bias(ushort effect, bool morph)
    {
        ItemFixture native = Fixture(effect);
        ItemFixture captured = Fixture(effect);
        NativeBattlePilotVisualIdentity identity = ChildIdentity(captured);
        NativeBattleGfAnimationNode node = Node(morph);
        NativeBattleStatusCureItemGeometryStep baseline = NativeBattleStatusCureItemGeometry.Emit(native.Frame, native.Resources, node, default, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleStatusCureItemGeometryStep actual = NativeBattleStatusCureItemGeometry.Emit(captured.Frame, captured.Resources, node, default, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(baseline.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(baseline.Batch), NativeBattleTestSnapshots.Json(actual.Batch));
        Assert.Null(Assert.Single(baseline.Batch.Attempts).Primitive!.PilotGeometry);
        NativeBattleGeometryPrimitive primitive = Assert.Single(actual.Batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(identity with { Primitive = unchecked(identity.Primitive * 65537 + 156) }, geometry.Identity);
        Assert.Equal(new Vector3(morph ? -150 : -100, -100, morph ? 1100 : 1000), geometry.A);
        Assert.Equal(new Vector3(morph ? 150 : 100, 100, morph ? 1100 : 1000), geometry.D);
        Assert.Equal((byte)4, geometry.CornerCount);
        Assert.Equal(0, geometry.DepthOffset);
        Assert.Equal(17 + (((morph ? 1100 : 1000) / 4 - 128) >> 2), primitive.Bucket);
        Assert.Equal(Enumerable.Repeat(morph ? 1100 : 1000, 4), primitive.VertexPayload4);
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(effect);
        int scratch = 256 + profile.ChildStride;
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, actual.Frame.Memory.Arena!.Read(scratch + index * 8 + 6, 2));
        }
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 }, actual.Frame.Memory.Arena!.Read(scratch + 32, 8));
    }

    [Theory]
    [InlineData((ushort)292)]
    [InlineData((ushort)293)]
    [InlineData((ushort)294)]
    [InlineData((ushort)295)]
    [InlineData((ushort)296)]
    public void Native_near_rejection_keeps_invalid_fourth_unreached_with_capture(ushort effect)
    {
        ItemFixture native = Fixture(effect, true);
        ItemFixture captured = Fixture(effect, true);
        NativeBattleStatusCureItemGeometryStep baseline = NativeBattleStatusCureItemGeometry.Emit(native.Frame, native.Resources, Node(false), default, Projection);
        NativeBattleStatusCureItemGeometryStep actual = NativeBattleStatusCureItemGeometry.Emit(captured.Frame, captured.Resources, Node(false), default, Projection, pilotIdentity: ChildIdentity(captured));
        Assert.Null(Assert.Single(baseline.Batch.Attempts).Primitive);
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(NativeBattleTestSnapshots.Json(baseline.Batch), NativeBattleTestSnapshots.Json(actual.Batch));
        Assert.Equal(NativeBattleTestSnapshots.Json(baseline.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
    }

    [Fact]
    public void Group16_echo_screen_reached_billboard_header_keeps_actual_source_and_native_state()
    {
        ItemFixture native = Fixture(295);
        ItemFixture captured = Fixture(295);
        NativeBattleActionMatrix billboard = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 0, 0, 0, 4096);
        native = native with { Frame = native.Frame with { Execution = native.Frame.Execution with { Billboard = billboard } } };
        captured = captured with { Frame = captured.Frame with { Execution = captured.Frame.Execution with { Billboard = billboard } } };
        var node = Node(false);
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0x1000);
        node = node with { Data = data.ToImmutableArray() };
        var baseline = NativeBattleStatusCureItemGeometry.Emit(native.Frame, native.Resources, node, default, Projection);
        var actual = NativeBattleStatusCureItemGeometry.Emit(captured.Frame, captured.Resources, node, default, Projection, pilotIdentity: ChildIdentity(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(baseline), NativeBattleTestSnapshots.Json(actual));
        var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(actual.Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Equal((ushort)295, geometry.Identity.Effect);
        Assert.Equal(new Vector3(100, -100, 1000), geometry.A);
        Assert.Equal(new Vector3(-100, 100, 1000), geometry.D);
    }

    private sealed record ItemFixture(NativeBattleStatusCureItemFrame Frame, NativeBattleStatusCureItemResources Resources, NativeBattleStatusCureItemInputs Inputs);
    private static NativeBattleCameraProjection Projection => new(NativeBattleCuragaMatrices.Identity8DD770(), 256, 160, 108);

    private static NativeBattlePilotVisualIdentity ChildIdentity(ItemFixture fixture)
    {
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(fixture.Resources.Effect);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, NativeBattleEffectNodeMemory.ArenaAddress + 256, profile.ChildStride, 1);
        (pool, int slot) = pool.Append(profile.ChildCallback);
        return NativeBattlePilotVisualLifetimes.Identity(profile.Effect, pool.Slots[slot], 32) with { ResourceGeneration = ((long)profile.SourceAddress << 32) | 20u };
    }

    private static ItemFixture Fixture(ushort effect, bool nearBadFourth = false)
    {
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(effect);
        byte[] layout = Layout(nearBadFourth);
        NativeBattleStatusCureItemResources resources = new(effect, new(profile.SourceAddress, layout.ToImmutableArray()), 300000u + effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, (ushort)1, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.TextureGlobal, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(effect - 1);
        byte[] tim = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(20), 12);
        memory = memory.LoadIntoArena(tim);
        memory = memory with { Arena = memory.Arena!.Write(256 + profile.ChildStride, Enumerable.Repeat((byte)0xA5, 40).ToArray()) };
        NativeBattleStatusCureItemFrame frame = NativeBattleStatusCureItemController.Initialize(memory, selected, resources, 256);
        NativeBattleStatusCureItemInputs inputs = new(packets, Projection.View!, _ => default, _ => 0, (current, _, _) => current);
        return new(frame, resources, inputs);
    }

    private static NativeBattleGfAnimationNode Node(bool morph)
    {
        NativeBattleGfAnimationNode node = NativeBattleStatusCureItemTests.Node(0, default);
        byte[] data = node.Data.ToArray();
        if (morph)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        }
        return node with { ResourceOffset = 32, Data = data.ToImmutableArray() };
    }

    private static byte[] Layout(bool nearBadFourth)
    {
        byte[] source = new byte[196];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 64);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(20), 3);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(24), 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(64), 76);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(68), 4);
        NativeBattleVector[] vertices = [new(-100, -100, 1000), new(100, -100, 1000), new(-100, 100, 1000), new(100, 100, 1000)];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int index = 0; index < vertices.Length; index++)
            {
                int at = 76 + shape * 32 + index * 8;
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(at), unchecked((short)(vertices[index].X * (shape + 1))));
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(at + 2), vertices[index].Y);
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(at + 4), nearBadFourth ? (short)0 : unchecked((short)(1000 + shape * 200)));
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(at + 6), 0xBEEF);
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(152), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(156), 0x2C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(160 + corner * 2), nearBadFourth && corner == 3 ? (ushort)198 : (ushort)(corner * 2));
        }
        return source;
    }
}

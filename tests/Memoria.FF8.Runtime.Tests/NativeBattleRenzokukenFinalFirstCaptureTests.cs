using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenFinalFirstCaptureTests
{
    [Theory]
    [InlineData((ushort)334, 0x589710u)]
    [InlineData((ushort)335, 0x5872C0u)]
    [InlineData((ushort)336, 0x5848F0u)]
    [InlineData((ushort)337, 0x582270u)]
    public void Actual_group_meshes_keep_native_scale_fade_depth_packets_and_selected_task_resource(ushort effect, uint callback)
    {
        (string Native, NativeBattleGfGeometryBatch Batch) Draw(bool capture)
        {
            var setup = Setup(effect, callback, 8);
            NativeBattleGfGeometryBatch? batch = null;
            NativeBattleRenzokuken160Inputs inputs = Inputs(capture, (frame, operation) =>
            {
                batch = Assert.IsType<NativeBattleRenzokuken160MeshDraw>(operation).Packets;
                return frame;
            });
            var step = NativeBattleRenzokukenSpecialController.AdvanceChild334337(setup.Frame, setup.Pool, 0, inputs, setup.Resources, Identity, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException());
            return (NativeBattleTestSnapshots.Json(step), batch!);
        }
        var native = Draw(false);
        var captured = Draw(true);
        Assert.Equal(native.Native, captured.Native);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        var primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(effect, geometry.Identity.Effect);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.Equal(callback == 0x589710u ? 0xD00005L : 0xD00002L, geometry.Identity.ResourceGeneration);
        Assert.False(geometry.ScreenSpace);
        Assert.True(geometry.Continuous);
        Assert.Equal(4000, geometry.A.Z);
        Assert.Equal(primitive.VertexPayload4[0], (int)geometry.A.Z);
    }

    [Theory]
    [InlineData((ushort)334, 0x589400u)]
    [InlineData((ushort)335, 0x586F90u)]
    [InlineData((ushort)336, 0x584A30u)]
    [InlineData((ushort)337, 0x5823B0u)]
    public void Actual_target_rows_keep_native_bone_random_padding_packets_and_births_on_pause_and_reuse(ushort effect, uint callback)
    {
        (NativeBattleRenzokuken160ChildStep Step, NativeBattleModulePacketBatch[] Batches) Draw(bool capture)
        {
            var setup = Setup(effect, callback, 0);
            List<NativeBattleModulePacketBatch> batches = [];
            var inputs = Inputs(capture, (frame, operation) =>
            {
                batches.Add(Assert.IsType<NativeBattleRenzokukenSpecialSpriteDraw>(operation).Packets);
                return frame;
            });
            var step = NativeBattleRenzokukenSpecialController.AdvanceChild334337(setup.Frame, setup.Pool, 0, inputs, setup.Resources, Identity, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException());
            return (step, batches.ToArray());
        }
        var native = Draw(false);
        string frozen = NativeBattleTestSnapshots.Json(native.Step);
        string packets = NativeBattleTestSnapshots.Json(native.Batches);
        var captured = Draw(true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured.Step));
        Assert.Equal(packets, NativeBattleTestSnapshots.Json(captured.Batches));
        NativeBattlePilotVisualIdentity[] identities = captured.Batches.Select(batch => Geometry(Assert.Single(batch.Primitives).Primitive, effect).Identity).ToArray();
        Assert.Equal(3, identities.Length);
        Assert.Equal(3, identities.Distinct().Count());
        var row0 = NativeBattleRenzokukenVisualLifetimes.Particle(captured.Step.Frame.Effect, 3, 0, Sprite.Address)!.Value;
        var row1 = NativeBattleRenzokukenVisualLifetimes.Particle(captured.Step.Frame.Effect, 3, 1, Sprite.Address)!.Value;
        var resources = NativeBattleRenzokukenSpecialController.ReadState(captured.Step.Frame).Resources;
        var paused = NativeBattleRenzokukenSpecialController.AdvanceChild334337(captured.Step.Frame, captured.Step.Pool, 0, Inputs(true, (frame, _) => frame) with { GraphicsFlags = 0x201 }, resources, Identity, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException());
        Assert.Equal(NativeBattleTestSnapshots.Json(captured.Step.Frame), NativeBattleTestSnapshots.Json(paused.Frame));
        Assert.Equal(row0, NativeBattleRenzokukenVisualLifetimes.Particle(paused.Frame.Effect, 3, 0, Sprite.Address));
        var state = NativeBattleRenzokukenSpecialController.ReadState(paused.Frame);
        byte[] rows = state.ExtraStorage.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(rows, 0);
        var reusedFrame = NativeBattleRenzokukenSpecialController.Store(paused.Frame, state with { ExtraStorage = rows.ToImmutableArray() });
        var reused = NativeBattleRenzokukenSpecialController.AdvanceChild334337(reusedFrame, paused.Pool, 0, Inputs(true, (frame, _) => frame), resources, Identity, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException());
        var reborn = NativeBattleRenzokukenVisualLifetimes.Particle(reused.Frame.Effect, 3, 0, Sprite.Address)!.Value;
        Assert.Equal(row0.Generation, reborn.Generation);
        Assert.NotEqual(row0.ResourceGeneration, reborn.ResourceGeneration);
        Assert.Equal(row1, NativeBattleRenzokukenVisualLifetimes.Particle(reused.Frame.Effect, 3, 1, Sprite.Address));
    }

    [Theory]
    [InlineData((ushort)336, 0x584F20u)]
    [InlineData((ushort)337, 0x582850u)]
    public void Actual_radial_sprite_admission_keeps_native_five_draw_CRT_velocity_and_accepted_world_rows(ushort effect, uint callback)
    {
        (string Native, NativeBattleModulePacketBatch[] Batches) Draw(bool capture)
        {
            var setup = Setup(effect, callback, 0);
            List<NativeBattleModulePacketBatch> batches = [];
            var inputs = Inputs(capture, (frame, operation) =>
            {
                batches.Add(Assert.IsType<NativeBattleRenzokukenSpecialSpriteDraw>(operation).Packets);
                return frame;
            });
            var step = NativeBattleRenzokukenSpecialController.AdvanceChild334337(setup.Frame, setup.Pool, 0, inputs, setup.Resources, Identity, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException());
            NativeCrtRandomState expected = setup.Frame.Random;
            for (int draw = 0; draw < 14 * 5; draw++)
            {
                expected = expected.Draw(out _);
            }
            Assert.Equal(expected, step.Frame.Random);
            return (NativeBattleTestSnapshots.Json(step), batches.ToArray());
        }
        var native = Draw(false);
        var captured = Draw(true);
        Assert.Equal(native.Native, captured.Native);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batches), NativeBattleTestSnapshots.Json(captured.Batches));
        var geometry = captured.Batches.SelectMany(batch => batch.Primitives).Select(primitive => Geometry(primitive.Primitive, effect)).ToArray();
        Assert.Equal(14, geometry.Length);
        Assert.Equal(14, geometry.Select(item => item.Identity).Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual334_paired_line_rows_keep_native32byte_addresses_padding_fade_and_pause(bool paused)
    {
        (string Native, NativeBattleGfGeometryBatch[] Batches, NativeBattleOriginalEffectState Effect) Draw(bool capture)
        {
            var setup = Setup(334, 0x588FC0, paused ? 9 : 0);
            if (paused)
            {
                var state = NativeBattleRenzokukenSpecialController.ReadState(setup.Frame);
                byte[] rows = state.ExtraStorage.ToArray();
                for (int row = 0; row < 64; row += 32)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(rows.AsSpan(row), 1);
                    Put(rows, row + 4, 5);
                    Put(rows, row + 12, 6000);
                    Put(rows, row + 18, 2048);
                    Put(rows, row + 24, 2896);
                    Put(rows, row + 28, 2896);
                }
                setup = (NativeBattleRenzokukenSpecialController.Store(setup.Frame, state with { ExtraStorage = rows.ToImmutableArray() }), setup.Pool, setup.Resources);
            }
            List<NativeBattleGfGeometryBatch> batches = [];
            var inputs = Inputs(capture, (frame, operation) =>
            {
                batches.Add(Assert.IsType<NativeBattleRenzokuken160MeshDraw>(operation).Packets);
                return frame;
            }) with { GraphicsFlags = paused ? 0x201u : 0u };
            var step = NativeBattleRenzokukenSpecialController.AdvanceChild334337(setup.Frame, setup.Pool, 0, inputs, setup.Resources, Identity, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException());
            return (NativeBattleTestSnapshots.Json(step), batches.ToArray(), step.Frame.Effect);
        }
        var native = Draw(false);
        var captured = Draw(true);
        Assert.Equal(native.Native, captured.Native);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batches), NativeBattleTestSnapshots.Json(captured.Batches));
        Assert.Equal(2, captured.Batches.Length);
        var first = Geometry(Assert.Single(captured.Batches[0].Attempts).Primitive!, 334);
        var second = Geometry(Assert.Single(captured.Batches[1].Attempts).Primitive!, 334);
        Assert.NotEqual(first.Identity, second.Identity);
        var source = NativeBattleRenzokukenVisualLifetimes.Particle(captured.Effect, 3, 0, 0xD00004)!.Value;
        Assert.Equal(source.ResourceGeneration, first.Identity.ResourceGeneration);
    }

    [Fact]
    public void Actual_special_sprite_near_rejection_keeps_native_selection_and_emits_no_metadata()
    {
        var setup = Setup(334, 0x589400, 0);
        NativeBattleRenzokuken160Sprite sprite = new(Sprite, 0, new(0, 0, 100), 4096, null, 0);
        var native = NativeBattleRenzokukenSpecialSpriteDrawing.Draw64DD70(sprite, Identity, Projection, 0);
        string frozen = NativeBattleTestSnapshots.Json(native);
        var identity = NativeBattleRenzokukenVisualLifetimes.Task(setup.Frame.Effect, setup.Pool.Slots[0], Sprite.Address);
        var captured = NativeBattleRenzokukenSpecialSpriteDrawing.Draw64DD70(sprite with { PilotIdentity = identity }, Identity, Projection, 0);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Empty(captured.Packets.Primitives);
        Assert.Equal(0, captured.Packets.AllocationBytes);
    }

    private static NativeBattlePilotVisualGeometry Geometry(NativeBattleGeometryPrimitive primitive, ushort effect)
    {
        var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(effect, geometry.Identity.Effect);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.NotEqual(0, geometry.Identity.ResourceGeneration);
        Assert.False(geometry.ScreenSpace);
        Assert.True(geometry.Continuous);
        Assert.True(float.IsFinite(geometry.A.X) && float.IsFinite(geometry.A.Y) && float.IsFinite(geometry.A.Z));
        Assert.All(primitive.VertexPayload4, depth => Assert.True(depth > 0));
        return geometry;
    }

    private static readonly NativeBattleActionMatrix Identity = NativeBattleCuragaMatrices.Identity8DD770();
    private static readonly NativeBattleCameraProjection Projection = new(Identity, 256, 160, 108);
    private static readonly NativeBattleSearchSpriteResource Sprite = CreateSprite();

    private static NativeBattleRenzokuken160Inputs Inputs(bool capture, Func<NativeBattleRenzokuken160Frame, NativeBattleRenzokuken160Operation, NativeBattleRenzokuken160Frame> execute)
    {
        return new(0, [], _ => new(0, 0) { GeometryWorldMatrix = Identity }, execute)
        {
            CapturePresentation = capture,
            ReadBone = (_, _, fraction) => fraction == 0 ? new(0, 0, 4000) : new(0, -300, 6000)
        };
    }

    private static (NativeBattleRenzokuken160Frame Frame, NativeBattleEffectTaskPool Pool, NativeBattleRenzokuken160Resources Resources) Setup(ushort effect, uint callback, int age)
    {
        NativeBattleRenzokukenSpecialProfile profile = NativeBattleRenzokukenSpecialProfiles.ForEffect(effect);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(profile.MainDescriptor, profile.MainBank, 16, 1);
        (root, _) = root.Append(profile.RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, 100);
        (children, _) = children.Append(callback);
        children = children.WriteSlot(0, children.Slots[0].WriteBytes(12, new byte[24]).WriteWord(12, unchecked((ushort)age)).WriteWord(20, 4000).WriteWord(28, 4096));
        byte[] rows = new byte[3200];
        if (callback == 0x5872C0)
        {
            BinaryPrimitives.WriteInt32LittleEndian(rows, -1);
        }
        Put(rows, 12, 4000);
        Put(rows, 28, 4096);
        var meshes = Enumerable.Range(0, 6).Select(index => Mesh(index)).ToImmutableArray();
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], meshes, 0, 0) { SpecialSprites = [Sprite, Sprite], SpecialAlignmentAxis = [0, 0, 4096] };
        var core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 200, Origin = new(100, -200, 6000), Direction = new(2896, 0, 2896) };
        var state = new NativeBattleRenzokukenSpecialState(core, resources) { ExtraStorage = rows.ToImmutableArray(), PreviousOrigin = core.Origin, PreviousDirection = core.Direction };
        var memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(effect, state) };
        var original = new NativeBattleOriginalEffectState(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), null, null) { Pools = [root, children] };
        return (new(memory, original, NativeCrtRandomState.FromColdProcess()), children, resources);
    }

    private static NativeBattleStaticMeshResource Mesh(int id)
    {
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] coordinates = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0, 100, 100, 0, 0];
        for (int index = 0; index < coordinates.Length; index++)
        {
            Put(bytes, 8 + index * 2, coordinates[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 0x2C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            Put(bytes, 60 + corner * 2, corner * 2);
        }
        return new(checked((byte)id), checked(0xD00000u + (uint)id), bytes.ToImmutableArray());
    }

    private static NativeBattleSearchSpriteResource CreateSprite()
    {
        byte[] bytes = new byte[56];
        Put(bytes, 8, 8);
        for (int phase = 0; phase <= 8; phase++)
        {
            Put(bytes, 10 + phase * 2, 32);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
        bytes[36] = 4;
        bytes[37] = 128;
        bytes[38] = 4;
        bytes[39] = 0x2E;
        bytes[40] = 8;
        bytes[41] = 8;
        return new(0xD70000, bytes.ToImmutableArray());
    }

    private static void Put(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
}

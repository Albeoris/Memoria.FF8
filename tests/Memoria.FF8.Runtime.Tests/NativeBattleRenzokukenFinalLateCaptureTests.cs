using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenFinalLateCaptureTests
{
    [Theory]
    [InlineData((ushort)339, 0x580310u, 0)]
    [InlineData((ushort)339, 0x580310u, 8)]
    [InlineData((ushort)340, 0x57D6B0u, 0)]
    [InlineData((ushort)340, 0x57D6B0u, 8)]
    [InlineData((ushort)341, 0x57B330u, 0)]
    [InlineData((ushort)341, 0x57B330u, 8)]
    [InlineData((ushort)342, 0x579440u, 0)]
    [InlineData((ushort)342, 0x579440u, 8)]
    [InlineData((ushort)343, 0x577240u, 0)]
    [InlineData((ushort)343, 0x577240u, 8)]
    public void Actual_late_mesh_callbacks_preserve_detached_native_packets_state_scale_and_fade(ushort effect, uint callback, int age)
    {
        (string Native, NativeBattleGfGeometryBatch Batch) Draw(bool capture)
        {
            var setup = Setup(effect, callback, age);
            NativeBattleGfGeometryBatch? batch = null;
            var inputs = Inputs(capture, (frame, operation) =>
            {
                batch = Assert.IsType<NativeBattleRenzokuken160MeshDraw>(operation).Packets;
                return frame;
            });
            var step = NativeBattleRenzokukenSpecialController.AdvanceLateSpecialChild(setup.Frame, setup.Pool, 0, inputs, setup.Resources, _ => new(0, 0, 0, 0), Identity, Projection, null, NativeBattleGteColorInterpolation.CodeConstants45F270, 0);
            return (NativeBattleTestSnapshots.Json(step), batch!);
        }
        var native = Draw(false);
        string frozenPackets = NativeBattleTestSnapshots.Json(native.Batch);
        var captured = Draw(true);
        Assert.Equal(native.Native, captured.Native);
        Assert.Equal(frozenPackets, NativeBattleTestSnapshots.Json(captured.Batch));
        var primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        var geometry = Geometry(primitive, effect);
        Assert.Equal(primitive.VertexPayload4[0], (int)geometry.A.Z);
        Assert.NotEqual(0, geometry.Identity.ResourceGeneration);
    }

    [Theory]
    [InlineData((ushort)341)]
    [InlineData((ushort)342)]
    public void Actual_extra_particle_emission_preserves_native_random_draws_and_reuses_only_admitted_row_birth(ushort effect)
    {
        uint callback = effect == 341 ? 0x57B520u : 0x5790D0u;
        (NativeBattleRenzokuken160ChildStep Step, NativeBattleModulePacketBatch[] Batches) Draw(bool capture)
        {
            var setup = Setup(effect, callback, 0);
            var state = NativeBattleRenzokukenSpecialController.ReadState(setup.Frame);
            byte[] rows = new byte[3200];
            var frame = NativeBattleRenzokukenSpecialController.Store(setup.Frame, state with { ExtraStorage = rows.ToImmutableArray() });
            var pool = setup.Pool.WriteSlot(0, setup.Pool.Slots[0].WriteWord(14, effect == 342 ? (ushort)1 : (ushort)0));
            List<NativeBattleModulePacketBatch> batches = [];
            var inputs = Inputs(capture, (current, operation) =>
            {
                batches.Add(Assert.IsType<NativeBattleRenzokukenSpecialSpriteDraw>(operation).Packets);
                return current;
            });
            return (Particles(frame, pool, inputs), batches.ToArray());
        }
        var native = Draw(false);
        string frozen = NativeBattleTestSnapshots.Json(native.Step);
        string packets = NativeBattleTestSnapshots.Json(native.Batches);
        var captured = Draw(true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured.Step));
        Assert.Equal(packets, NativeBattleTestSnapshots.Json(captured.Batches));
        Assert.NotEmpty(captured.Batches.SelectMany(batch => batch.Primitives));
        Assert.All(captured.Batches.SelectMany(batch => batch.Primitives), primitive => Geometry(primitive.Primitive, effect));
        var row0 = NativeBattleRenzokukenVisualLifetimes.Particle(captured.Step.Frame.Effect, 3, 0, Sprite.Address)!.Value;
        var row1 = NativeBattleRenzokukenVisualLifetimes.Particle(captured.Step.Frame.Effect, 3, 1, Sprite.Address)!.Value;
        var paused = Particles(captured.Step.Frame, captured.Step.Pool, Inputs(true, (frame, _) => frame) with { GraphicsFlags = 0x201 });
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(paused));
        Assert.Equal(row0, NativeBattleRenzokukenVisualLifetimes.Particle(paused.Frame.Effect, 3, 0, Sprite.Address));
        var state = NativeBattleRenzokukenSpecialController.ReadState(paused.Frame);
        byte[] rows = state.ExtraStorage.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(rows, 0);
        var reusedFrame = NativeBattleRenzokukenSpecialController.Store(paused.Frame, state with { ExtraStorage = rows.ToImmutableArray() });
        var reused = Particles(reusedFrame, paused.Pool, Inputs(true, (frame, _) => frame));
        Assert.NotEqual(row0.ResourceGeneration, NativeBattleRenzokukenVisualLifetimes.Particle(reused.Frame.Effect, 3, 0, Sprite.Address)!.Value.ResourceGeneration);
        Assert.Equal(row1, NativeBattleRenzokukenVisualLifetimes.Particle(reused.Frame.Effect, 3, 1, Sprite.Address));
        string standalone = NativeBattleTestSnapshots.Json(reused.Frame);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleRenzokuken160Frame>(standalone)!;
        Assert.Equal(standalone, NativeBattleTestSnapshots.Json(restored));
        List<NativeBattleModulePacketBatch> warmPackets = [];
        var warm = Particles(reused.Frame, reused.Pool, Inputs(true, (frame, operation) =>
        {
            warmPackets.Add(Assert.IsType<NativeBattleRenzokukenSpecialSpriteDraw>(operation).Packets);
            return frame;
        }) with { GraphicsFlags = 0x201 });
        string warmNative = NativeBattleTestSnapshots.Json(warm);
        string warmBytes = NativeBattleTestSnapshots.Json(warmPackets);
        List<NativeBattleModulePacketBatch> coldPackets = [];
        var cold = Particles(restored, restored.Effect.Pools[1], Inputs(true, (frame, operation) =>
        {
            coldPackets.Add(Assert.IsType<NativeBattleRenzokukenSpecialSpriteDraw>(operation).Packets);
            return frame;
        }) with { GraphicsFlags = 0x201 });
        Assert.Equal(warmNative, NativeBattleTestSnapshots.Json(cold));
        Assert.Equal(warmBytes, NativeBattleTestSnapshots.Json(coldPackets));
        Assert.All(coldPackets.SelectMany(batch => batch.Primitives), primitive => Geometry(primitive.Primitive, effect));


        NativeBattleRenzokuken160ChildStep Particles(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, NativeBattleRenzokuken160Inputs inputs)
        {
            // The342 contact record provides the actual particle source position and direction.
            if (effect == 342)
            {
                var state = NativeBattleRenzokukenSpecialController.ReadState(frame);
                byte[] impacts = state.Core.ImpactStates.ToArray();
                Put(impacts, 156 + 4, 6000);
                frame = NativeBattleRenzokukenSpecialController.Store(frame, state with { Core = state.Core with { ImpactStates = impacts.ToImmutableArray() } });
                return NativeBattleRenzokukenSpecialController.Advance5790D0(frame, pool, 0, inputs, Sprite, Identity, Projection, 0);
            }
            return NativeBattleRenzokukenSpecialController.Advance57B520(frame, pool, 0, inputs, Sprite, _ => new(0, 0, 6000, 0), Identity, Projection, 0);
        }
    }

    [Theory]
    [InlineData((ushort)339)]
    [InlineData((ushort)340)]
    [InlineData((ushort)341)]
    [InlineData((ushort)342)]
    [InlineData((ushort)343)]
    public void Successful_stored_task_admission_preserves_native_row_and_links_and_changes_resource_on_reuse(ushort effect)
    {
        var plain = Setup(effect, 1, 0);
        var captured = Setup(effect, 1, 0);
        var native = NativeBattleRenzokukenSpecialController.AllocateStoredTask(plain.Frame, plain.Pool, 0x123456);
        string frozen = System.Text.Json.JsonSerializer.Serialize(new { Frame = NativeBattleStorageSnapshots.Capture(native.Frame), Pool = NativeBattleStorageSnapshots.Capture(native.Pool), native.Slot });
        var admitted = NativeBattleRenzokukenSpecialController.AllocateStoredTask(captured.Frame, captured.Pool, 0x123456);
        Assert.Equal(frozen, System.Text.Json.JsonSerializer.Serialize(new { Frame = NativeBattleStorageSnapshots.Capture(admitted.Frame), Pool = NativeBattleStorageSnapshots.Capture(admitted.Pool), admitted.Slot }));
        int row = admitted.Pool.Slots[admitted.Slot].ReadWord(14);
        var birth = NativeBattleRenzokukenVisualLifetimes.Particle(admitted.Frame.Effect, 3, row, Sprite.Address)!.Value;
        var state = NativeBattleRenzokukenSpecialController.ReadState(admitted.Frame);
        byte[] rows = state.ExtraStorage.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(rows.AsSpan(row * 32), 0);
        var frame = NativeBattleRenzokukenSpecialController.Store(admitted.Frame, state with { ExtraStorage = rows.ToImmutableArray() });
        var reused = NativeBattleRenzokukenSpecialController.AllocateStoredTask(frame, admitted.Pool, 0x123456);
        Assert.Equal(row, reused.Pool.Slots[reused.Slot].ReadWord(14));
        var next = NativeBattleRenzokukenVisualLifetimes.Particle(reused.Frame.Effect, 3, row, Sprite.Address)!.Value;
        Assert.Equal(birth.Generation, next.Generation);
        Assert.NotEqual(birth.ResourceGeneration, next.ResourceGeneration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Actual342_contacts_keep_native_CRT_velocity_reversal_and_mark_only_successful_spark_debris_births(int contact)
    {
        (NativeBattleRenzokuken160ChildStep Step, NativeBattleRenzokuken160Frame Initial) Advance(bool capture)
        {
            var setup = Setup(342, 0x578A00, contact);
            var resources = setup.Resources with { ContactLeadFrames = Enumerable.Repeat(1, 10).ToImmutableArray() };
            var inputs = Inputs(capture, (frame, _) => frame) with { AbSegments = [1, 1, 1, 1, 1] };
            return (NativeBattleRenzokukenSpecialController.Advance578A00(setup.Frame, setup.Pool, 0, inputs, resources), setup.Frame);
        }
        var native = Advance(false);
        string frozen = NativeBattleTestSnapshots.Json(native.Step);
        var captured = Advance(true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured.Step));
        Assert.Equal(native.Step.Frame.Random, captured.Step.Frame.Random);
        var state = NativeBattleRenzokukenSpecialController.ReadState(captured.Step.Frame).Core;
        Assert.Equal(3, Enumerable.Range(0, 40).Count(row => BinaryPrimitives.ReadInt16LittleEndian(state.Sparks.AsSpan().Slice(row * 12)) == 0));
        Assert.Equal(12, Enumerable.Range(0, 40).Count(row => BinaryPrimitives.ReadInt16LittleEndian(state.Debris.AsSpan().Slice(row * 20)) == 0));
        for (int bank = 0; bank < 2; bank++)
        {
            int admitted = bank == 0 ? 3 : 12;
            for (int row = 0; row < 40; row++)
            {
                var identity = NativeBattleRenzokukenVisualLifetimes.Particle(captured.Step.Frame.Effect, bank, row, Sprite.Address)!.Value;
                Assert.Equal(unchecked(((long)Sprite.Address << 32) ^ (row < admitted ? 1L : 0L)), identity.ResourceGeneration);
            }
        }
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
        BinaryPrimitives.WriteInt32LittleEndian(rows, -1);
        var meshes = new uint[] { 0xC9FA74, 0xCA1C2C, 0xC99D2C, 0xC98C04, 0xC9B9EC, 0xC9CF24, 0xC91DF4, 0xC8AF84, 0xC852AC }.Select(address => Mesh(0) with { Address = address }).ToImmutableArray();
        NativeBattleRenzokuken160Resources resources = new(new("animation", []), null!, [], [], [], meshes, 0, 0) { SpecialSprites = [Sprite, Sprite], SpecialAlignmentAxis = [0, 0, 4096], ImpactAxis = [0, 0, 4096], ContactOffsets = new NativeBattleVector[10].ToImmutableArray() };
        var core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 200, Origin = new(100, -200, 6000), Direction = new(2896, 0, 2896) };
        var state = new NativeBattleRenzokukenSpecialState(core, resources) { ExtraStorage = rows.ToImmutableArray(), PreviousOrigin = core.Origin, PreviousDirection = core.Direction, CapturedMatrix = Identity, ContactOffsets = new NativeBattleVector[10].ToImmutableArray() };
        var memory = NativeBattleEffectMemory.FromColdProcess() with { RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(effect, state) };
        var original = new NativeBattleOriginalEffectState(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), null, null) { Pools = [root, children] };
        return (new(memory, original, NativeCrtRandomState.FromColdProcess()), children, resources);
    }

    private static NativeBattleStaticMeshResource Mesh(int id)
    {
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] coordinates = [-100, -100, -100, 0, 100, -100, -100, 0, -100, 100, 100, 0, 100, 100, 100, 0];
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
        byte[] bytes = new byte[80];
        Put(bytes, 8, 16);
        for (int phase = 0; phase <= 16; phase++)
        {
            Put(bytes, 10 + phase * 2, 48);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(48), 1);
        bytes[52] = 4;
        bytes[53] = 128;
        bytes[54] = 4;
        bytes[55] = 0x2E;
        bytes[56] = 8;
        bytes[57] = 8;
        return new(0xD70000, bytes.ToImmutableArray());
    }

    private static void Put(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)value));
}

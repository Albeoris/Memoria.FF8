using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRenzokukenCaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Module_particle_ledger_records_only_actual_accepted_offsets_without_changing_native_batch(bool capturedWrites)
    {
        NativeBattleGeometryPrimitive first = new(17, [0x07000000, 0x24808080], [100, 200, 300, 0]);
        NativeBattleGeometryPrimitive second = new(18, [0x07000000, 0x24808080], [400, 500, 600, 0]);
        NativeBattleModulePacketBatch batch = new([new(first, 12, 0), new(second, 52, 0)], 84);
        if (capturedWrites)
        {
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray();
            batch = batch with { CapturedWrites = new([new(0, new byte[8].ToImmutableArray(), mask, null), new(12, new byte[8].ToImmutableArray(), mask, first), new(44, new byte[8].ToImmutableArray(), mask, null), new(52, new byte[8].ToImmutableArray(), mask, second)], 84) };
        }
        string frozen = NativeBattleTestSnapshots.Json(batch);
        ImmutableArray<NativeBattlePrimitiveReference> prior = [new("OTHER", 0, 4)];
        ImmutableArray<NativeBattlePrimitiveReference> recorded = NativeBattleRenzokukenVisualLifetimes.Record(prior, "20DFAB8", 0, 100, batch);
        Assert.Equal(new NativeBattlePrimitiveReference[] { prior[0], new("20DFAB8", 0, 112), new("20DFAB8", 0, 152) }, recorded);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(batch));
        Assert.All(batch.Primitives, item => Assert.Null(item.Primitive.PilotGeometry));
    }

    [Theory]
    [InlineData((ushort)141)]
    [InlineData((ushort)159)]
    [InlineData((ushort)160)]
    [InlineData((ushort)161)]
    public void Actual_mesh_callbacks_preserve_detached_native_steps_and_capture_their_real_task_and_resource(ushort effect)
    {
        (string native, NativeBattleGfGeometryBatch plain) = Mesh(effect, false);
        (string captured, NativeBattleGfGeometryBatch visual) = Mesh(effect, true);
        Assert.Equal(native, captured);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(visual));
        Assert.All(plain.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        NativeBattleGeometryPrimitive primitive = Assert.Single(visual.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(effect, geometry.Identity.Effect);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.Equal(0xDAB970, geometry.Identity.ResourceGeneration);
        Assert.Equal((byte)4, geometry.CornerCount);
        Assert.True(geometry.A.Z > 0);
        Assert.Equal(primitive.VertexPayload4[0], (int)geometry.A.Z);
    }

    [Theory]
    [InlineData((ushort)141)]
    [InlineData((ushort)160)]
    [InlineData((ushort)161)]
    public void Successful_spiral_allocation_changes_only_reused_physical_row_birth_and_preserves_pause_and_CRT(ushort effect)
    {
        NativeBattleRenzokuken160Frame frame = Frame(effect);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => new(0, 0), (current, _) => current)
        {
            ReadBoneCount = _ => 0,
            ReadBone = (_, _, _) => new(100, -200, 4096)
        };
        NativeBattleRenzokuken160ChildStep first = NativeBattleRenzokuken160Controller.Advance5FFFE0(frame, pool, 0, inputs);
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 6; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, first.Frame.Random);
        int bank = NativeBattleRenzokukenVisualLifetimes.Bank(effect, 2);
        NativeBattlePilotVisualIdentity row0 = NativeBattleRenzokukenVisualLifetimes.Particle(first.Frame.Effect, bank, 0, 0xD00000)!.Value;
        Assert.NotEqual(row0, NativeBattleRenzokukenVisualLifetimes.Particle(first.Frame.Effect, bank, 0, 0xD10000)!.Value);
        NativeBattlePilotVisualIdentity row1 = NativeBattleRenzokukenVisualLifetimes.Particle(first.Frame.Effect, bank, 1, 0xD00000)!.Value;
        NativeBattleRenzokuken160ChildStep paused = NativeBattleRenzokuken160Controller.Advance5FFFE0(first.Frame, first.Pool, 0, inputs with { GraphicsFlags = 0x200 });
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Frame), NativeBattleTestSnapshots.Json(paused.Frame));
        Assert.Equal(row0, NativeBattleRenzokukenVisualLifetimes.Particle(paused.Frame.Effect, bank, 0, 0xD00000)!.Value);
        NativeBattleRenzokuken160State state = Core(first.Frame);
        byte[] particles = state.Spirals.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(particles, -1);
        frame = Store(first.Frame, state with { Spirals = particles.ToImmutableArray() });
        NativeBattleRenzokuken160ChildStep next = NativeBattleRenzokuken160Controller.Advance5FFFE0(frame, first.Pool, 0, inputs);
        NativeBattlePilotVisualIdentity reused = NativeBattleRenzokukenVisualLifetimes.Particle(next.Frame.Effect, bank, 0, 0xD00000)!.Value;
        Assert.Equal(row0.Generation, reused.Generation);
        Assert.NotEqual(row0.ResourceGeneration, reused.ResourceGeneration);
        Assert.Equal(row1, NativeBattleRenzokukenVisualLifetimes.Particle(next.Frame.Effect, bank, 1, 0xD00000)!.Value);
        NativeBattlePilotVisualGeometry earlier = new(row0, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, default, 3);
        Assert.Equal(earlier, NativeBattlePilotVisualGeometry.Sample(earlier, earlier with { Identity = reused, A = Vector3.One }, 0.5f));
    }

    [Theory]
    [InlineData((ushort)141)]
    [InlineData((ushort)159)]
    [InlineData((ushort)160)]
    [InlineData((ushort)161)]
    public void Shared_streak_children_keep_distinct_actual_births_without_changing_native_payloads_links_or_CRT(ushort effect)
    {
        uint emitter = effect == 332 ? 0x58BE50u : 0x5FF4E0u;
        uint streak = effect == 332 ? 0x58C0E0u : 0x5FF770u;
        NativeBattleModuleState native = Modules(emitter);
        NativeBattleModuleState visual = Modules(emitter);
        NativeBattleRenzokuken160Frame ownerFrame = Frame(effect);
        NativeBattlePilotVisualIdentity parent = NativeBattleRenzokukenVisualLifetimes.Task(ownerFrame.Effect, ownerFrame.Effect.Pools[0].Slots[0], 0)!.Value;
        visual = NativeBattleRenzokukenVisualLifetimes.Shared(visual, 0, parent);
        NativeBattleModuleInputs inputs = new(0)
        {
            CrtRandom = NativeCrtRandomState.FromColdProcess(),
            ReadModelAnchor = _ => new(0, 0, 0, 0),
            ReadModelBoneCount = _ => 0,
            ReadModelBone = (_, _, _) => new(0, -700, 4096)
        };
        NativeBattleModuleCallback plain = NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(native, 0, inputs, streak);
        string frozen = NativeBattleTestSnapshots.Json(plain);
        NativeBattleModuleCallback captured = NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(visual, 0, inputs, streak);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualIdentity[] children = captured.State.Slots.Skip(1).Take(6).Select(task => task.PilotIdentity!.Value).ToArray();
        Assert.Equal(6, children.Distinct().Count());
        Assert.All(captured.State.Slots.Skip(1).Take(6), child => Assert.Equal(streak, child.Callback));
        Assert.All(children, owner => Assert.Equal(effect, owner.Effect));
        NativeBattleModuleState cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleState>(NativeBattleTestSnapshots.Json(captured.State))!;
        Assert.Null(cold.Slots[1].PilotIdentity);
        string coldNative = NativeBattleTestSnapshots.Json(cold);
        cold = NativeBattleRenzokukenVisualLifetimes.EnsureShared(cold, 1, effect);
        Assert.Equal(coldNative, NativeBattleTestSnapshots.Json(cold));
        Assert.Equal(effect, cold.Slots[1].PilotIdentity!.Value.Effect);
        Assert.NotEqual(children[0].Generation, cold.Slots[1].PilotIdentity!.Value.Generation);
    }

    [Theory]
    [InlineData((ushort)141)]
    [InlineData((ushort)160)]
    [InlineData((ushort)161)]
    public void Live_history_curve_capture_keeps_native_curve_W_CRT_and_authored_sample_pair_identity(ushort effect)
    {
        (string Native, NativeBattleGfGeometryBatch Batch) Run(bool capture)
        {
            NativeBattleRenzokuken160Frame frame = Frame(effect);
            byte[] history = Core(frame).TrailHistory.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(history, 1);
            BinaryPrimitives.WriteInt16LittleEndian(history.AsSpan(8), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(history.AsSpan(12), 100);
            BinaryPrimitives.WriteInt16LittleEndian(history.AsSpan(16), 4096);
            frame = Store(frame, Core(frame) with { TrailHistory = history.ToImmutableArray() });
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, 1));
            NativeBattleGfGeometryBatch? batch = null;
            NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => new(0, 0), (current, operation) =>
            {
                batch = Assert.IsType<NativeBattleRenzokuken160TrailDraw>(operation).Packets;
                return current;
            }) { CapturePresentation = capture, ReadWeaponTip = _ => new(new(0, 100, 4096), 4096, 0, 0, null, null) };
            NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleRenzokuken160ChildStep step = NativeBattleRenzokuken160Controller.Advance5FFAA0(frame, pool, 0, inputs, view, new(view, 256, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), NativeBattleGteColorInterpolation.CodeConstants45F270);
            Assert.Equal(frame.Random, step.Frame.Random);
            return (NativeBattleTestSnapshots.Json(step), batch!);
        }
        var native = Run(false);
        var captured = Run(true);
        Assert.Equal(native.Native, captured.Native);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        NativeBattlePilotVisualGeometry[] samples = captured.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry)).ToArray();
        Assert.Equal(60, samples.Length);
        Assert.Equal(60, samples.Select(sample => sample.Identity.Primitive).Distinct().Count());
        Assert.All(samples, sample =>
        {
            Assert.Equal(effect, sample.Identity.Effect);
            Assert.Equal((byte)4, sample.CornerCount);
            Assert.Equal(4096, sample.A.Z);
        });
    }

    [Fact]
    public void Spider_spiral_emitter_registers_real_row_reuse_without_changing_its_native_CRT_draw_count()
    {
        NativeBattleRenzokuken160Frame owner = Frame(159);
        NativeBattleRenzokuken159Frame frame = new(owner.Memory with { Renzokuken159 = NativeBattleRenzokuken159State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 100 } }, owner.Effect, owner.Random);
        NativeBattleRenzokuken159Inputs inputs = new(0, [], _ => new(0, 0), (current, _) => current) { ReadBoneCount = _ => 0, ReadBone = (_, _, _) => new(100, -200, 4096) };
        NativeBattleRenzokuken159ChildStep first = NativeBattleRenzokuken159Controller.Advance601EC0(frame, frame.Effect.Pools[1], 0, inputs);
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 6; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, first.Frame.Random);
        NativeBattlePilotVisualIdentity birth = NativeBattleRenzokukenVisualLifetimes.Particle(first.Frame.Effect, 2, 0, 0xD00000)!.Value;
        byte[] particles = first.Frame.Memory.Renzokuken159!.Spirals.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(particles, -1);
        frame = first.Frame with { Memory = first.Frame.Memory with { Renzokuken159 = first.Frame.Memory.Renzokuken159 with { Spirals = particles.ToImmutableArray() } } };
        NativeBattleRenzokuken159ChildStep second = NativeBattleRenzokuken159Controller.Advance601EC0(frame, first.Pool, 0, inputs);
        NativeBattlePilotVisualIdentity reused = NativeBattleRenzokukenVisualLifetimes.Particle(second.Frame.Effect, 2, 0, 0xD00000)!.Value;
        Assert.Equal(birth.Generation, reused.Generation);
        Assert.NotEqual(birth.ResourceGeneration, reused.ResourceGeneration);
    }

    [Fact]
    public void Captured259_actual_5A8340_mesh_keeps_native_bytes_registers_and_real_resource_identity()
    {
        var native = Mesh(259, false);
        var captured = Mesh(259, true);
        Assert.Equal(native.Native, captured.Native);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal((ushort)259, visual.Identity.Effect);
        Assert.Equal(0xDAB970L, visual.Identity.ResourceGeneration);
        Assert.True(visual.Identity.Generation > 0);
        Assert.True(visual.Continuous);
        Assert.Equal(primitive.VertexPayload4[0], (int)visual.A.Z);
    }

    [Fact]
    public void Captured259_spiral_birth_reuse_and_pause_keep_original_CRT_and_native_rows()
    {
        Successful_spiral_allocation_changes_only_reused_physical_row_birth_and_preserves_pause_and_CRT(259);
    }

    [Fact]
    public void Captured259_shared_streak_births_and_cold_recapture_keep_original_payload_links_and_CRT()
    {
        Shared_streak_children_keep_distinct_actual_births_without_changing_native_payloads_links_or_CRT(259);
    }

    [Fact]
    public void Captured259_live_history_keeps_original_curve_W_and_actual_sample_pair_capture()
    {
        Live_history_curve_capture_keeps_native_curve_W_CRT_and_authored_sample_pair_identity(259);
    }

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(false, 8, false)]
    [InlineData(false, 8, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 8, false)]
    [InlineData(true, 8, true)]
    public void Captured332_actual_late_meshes_preserve_native_fade_pause_bytes_and_selected_resource(bool second, ushort age, bool paused)
    {
        (string Native, NativeBattleGfGeometryBatch Batch) Draw(bool capture)
        {
            NativeBattleRenzokuken160Frame frame = Frame(332);
            var profile = NativeBattleRenzokukenVariantProfile.Renzokuken332;
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.ChildDescriptor, profile.ChildBank, 36, profile.ChildCapacity);
            (pool, _) = pool.Append(second ? 0x58D2D0u : 0x58D130u);
            pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, age).WriteWord(28, 4096).WriteWord(30, 0).WriteWord(32, second ? (ushort)0 : (ushort)4096).WriteWord(34, 0));
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], pool] } };
            var source = Resources();
            source = source with { Meshes = source.Meshes.Add(source.Meshes[2] with { Id = 3 }).Add(source.Meshes[2] with { Id = 4, Address = 0x1234000 }) };
            NativeBattleGfGeometryBatch? batch = null;
            NativeBattleRenzokuken160Inputs inputs = new(paused ? 0x201u : 0u, [], _ => throw new InvalidOperationException(), (current, operation) =>
            {
                batch = Assert.IsType<NativeBattleRenzokuken160MeshDraw>(operation).Packets;
                return current;
            }) { CapturePresentation = capture };
            NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleCameraProjection projection = new(view, 256, 160, 108);
            NativeBattleRenzokuken160ChildStep step = second ? NativeBattleRenzokukenVariantController.Advance58D2D0(frame, pool, 0, inputs, source, view, projection, NativeBattleGteColorInterpolation.CodeConstants45F270) : NativeBattleRenzokukenVariantController.Advance58D130(frame, pool, 0, inputs, source, view, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
            Assert.Equal(paused ? age : age + 1, step.Pool.Slots[0].ReadWord(12));
            return (NativeBattleTestSnapshots.Json(step), batch!);
        }
        var native = Draw(false);
        var captured = Draw(true);
        Assert.Equal(native.Native, captured.Native);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        var primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal((ushort)332, geometry.Identity.Effect);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.Equal(second ? 0x1234000L : 0xDAB970L, geometry.Identity.ResourceGeneration);
        Assert.False(geometry.ScreenSpace);
        Assert.True(geometry.Continuous);
        Assert.Equal(primitive.VertexPayload4[0], (int)geometry.A.Z);
    }

    [Fact]
    public void Captured332_actual_spiral_reuse_pause_shared_births_and_cold_history_preserve_native_contracts()
    {
        Successful_spiral_allocation_changes_only_reused_physical_row_birth_and_preserves_pause_and_CRT(332);
        Shared_streak_children_keep_distinct_actual_births_without_changing_native_payloads_links_or_CRT(332);
        Live_history_curve_capture_keeps_native_curve_W_CRT_and_authored_sample_pair_identity(332);
    }

    private static (string Native, NativeBattleGfGeometryBatch Batch) Mesh(ushort effect, bool capture)
    {
        NativeBattleRenzokuken160Frame frame = Frame(effect);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleGfGeometryBatch? batch = null;
        NativeBattleRenzokuken160Inputs inputs = new(0, [], _ => new(0, 0) { GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() }, (current, operation) =>
        {
            batch = Assert.IsType<NativeBattleRenzokuken160MeshDraw>(operation).Packets;
            return current;
        }) { CapturePresentation = capture };
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        if (effect == 159)
        {
            NativeBattleRenzokuken159Frame dedicated = new(frame.Memory with { Renzokuken159 = NativeBattleRenzokuken159State.FromColdProcess() }, frame.Effect, frame.Random);
            NativeBattleRenzokuken159Inputs special = new(0, [], _ => new(0, 0), (current, operation) =>
            {
                batch = Assert.IsType<NativeBattleRenzokuken159MeshDraw>(operation).Packets;
                return current;
            }) { CapturePresentation = capture };
            NativeBattleRenzokuken159Resources resources = new(new("animation", []), null!, [], [], [], Resources().Meshes, 0, 0);
            NativeBattleRenzokuken159ChildStep step = NativeBattleRenzokuken159Controller.Advance602670(dedicated, pool, 0, special, resources, view, projection, colors);
            return (NativeBattleTestSnapshots.Json(step), batch!);
        }
        NativeBattleRenzokuken160ChildStep result = effect switch
        {
            259 => NativeBattleRenzokukenVariantController.Advance5A8340(frame, pool, 0, inputs, Resources(), view, projection, colors),
            141 => NativeBattleRenzokukenVariantController.Advance61F590(frame, pool, 0, inputs, Resources(), view, projection, colors),
            160 => NativeBattleRenzokuken160Controller.Advance600780(frame, pool, 0, inputs, Resources(), view, projection, colors),
            161 => NativeBattleRenzokukenSpecialController.Advance5FEA70(frame, pool, 0, inputs, Resources(), view, projection, colors),
            _ => throw new InvalidOperationException()
        };
        return (NativeBattleTestSnapshots.Json(result), batch!);
    }

    private static NativeBattleRenzokuken160Frame Frame(ushort effect)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 16, 1);
        (root, _) = root.Append(1);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x3000, 0x4000, 36, 100);
        (children, _) = children.Append(effect == 259 ? 0x5A8340u : 2u);
        children = children.WriteSlot(0, children.Slots[0].WriteWord(20, 4096).WriteWord(28, 4096).WriteWord(32, 4096));
        NativeBattleRenzokuken160State core = NativeBattleRenzokuken160State.FromColdProcess().Enter(0, 0, 3, false) with { Duration = 100 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Renzokuken160 = core, RenzokukenVariants = ImmutableDictionary<ushort, NativeBattleRenzokukenVariantState>.Empty.Add(141, new(core, new(0, 0, 4096), new(0, 0, 4096), null)), RenzokukenSpecials = ImmutableDictionary<ushort, NativeBattleRenzokukenSpecialState>.Empty.Add(161, new(core, Resources())) };
        if (effect is 259 or 332)
        {
            NativeBattleRenzokukenVariantState selected = new(core, new(0, 0, 4096), new(0, 0, 4096), null);
            if (effect == 332)
            {
                selected = selected with { LateMatrix = NativeBattleCuragaMatrices.Identity8DD770(), ContactPadding = new(0, 0, 0) };
            }
            memory = memory with { RenzokukenVariants = memory.RenzokukenVariants!.Add(effect, selected) };
        }
        return new(memory, new(effect, effect - 1, new(effect, 1, 1), null, null) { Pools = [root, children] }, NativeCrtRandomState.FromColdProcess());
    }

    private static NativeBattleRenzokuken160State Core(NativeBattleRenzokuken160Frame frame) => frame.Effect.Binding.Effect switch
    {
        141 or 259 or 332 => NativeBattleRenzokukenVariantController.ReadState(frame).Core,
        161 => NativeBattleRenzokukenSpecialController.ReadState(frame).Core,
        _ => frame.Memory.Renzokuken160!
    };

    private static NativeBattleRenzokuken160Frame Store(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160State core) => frame.Effect.Binding.Effect switch
    {
        141 or 259 or 332 => NativeBattleRenzokukenVariantController.WriteState(frame, core),
        161 => NativeBattleRenzokukenSpecialController.WriteState(frame, core),
        _ => frame with { Memory = frame.Memory with { Renzokuken160 = core } }
    };

    private static NativeBattleModuleState Modules(uint callback = 0x5FF4E0)
    {
        NativeBattleModuleState modules = NativeBattleModuleState.Clear();
        (modules, _) = modules.AppendCallback(callback);
        byte[] bytes = modules.Slots[0].Payload.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 4);
        return modules with { Slots = modules.Slots.SetItem(0, modules.Slots[0] with { Payload = bytes.ToImmutableArray() }) };
    }

    private static NativeBattleRenzokuken160Resources Resources()
    {
        byte[] mesh = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] coordinates = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0, 100, 100, 0, 0];
        for (int index = 0; index < coordinates.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), coordinates[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(56), 0x2C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(60 + corner * 2), (ushort)(corner * 2));
        }
        NativeBattleStaticMeshResource[] meshes = [new(0, 0x1000, mesh.ToImmutableArray()), new(1, 0x2000, mesh.ToImmutableArray()), new(2, 0xDAB970, mesh.ToImmutableArray())];
        return new(new("animation", []), null!, [], [], [], meshes.ToImmutableArray(), 0, 0) { ImpactAxis = [0, 0, 4096] };
    }
}

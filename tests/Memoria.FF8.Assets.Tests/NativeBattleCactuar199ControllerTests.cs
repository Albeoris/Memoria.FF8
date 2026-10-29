using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCactuar199ControllerTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleActionMatrix View = Identity with { Z = 16384 };
    private static readonly NativeBattleCameraProjection Projection = new(View, 216, 160, 108);

    [Fact]
    public void ReusedPhysicalSuffixKeepsSpecialAppenderWordEWhileGeneralBodyAndParticleAppenderClearIt()
    {
        using GameAssets game = Installed();
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        Fixture fixture = Create(game, resources);
        Run current = Step(fixture.Run, fixture.Packets, resources, colors, 0);
        NativeBattleEffectTaskPool children = current.Frame.Effect.Pools[1];
        Assert.Equal(0x63E9C0u, children.Slots[1].Callback);
        children = children.WriteSlot(0, children.Slots[0].WriteWord(0xC, 10));
        children = children.WriteSlot(2, children.Slots[2].WriteWord(0xE, 0xCAFE));
        current = current with { Frame = current.Frame with { Effect = current.Frame.Effect with { Pools = current.Frame.Effect.Pools.SetItem(1, children) } } };
        current = Step(current, fixture.Packets, resources, colors, 0);
        children = current.Frame.Effect.Pools[1];
        Assert.Equal(0x5A8C20u, children.Slots[2].Callback);
        Assert.Equal((ushort)0, children.Slots[2].ReadWord(0xE));
        children = children.WriteSlot(2, children.Slots[2].WriteWord(0xC, 43));
        children = children.WriteSlot(3, children.Slots[3].WriteWord(0xE, 0xDEAD));
        children = children.WriteSlot(4, children.Slots[4].WriteWord(0xE, 0xBEEF));
        current = current with { Frame = current.Frame with { Effect = current.Frame.Effect with { Pools = current.Frame.Effect.Pools.SetItem(1, children) } } };
        current = Step(current, fixture.Packets, resources, colors, 0);
        children = current.Frame.Effect.Pools[1];
        Assert.Equal(0x5A9140u, children.Slots[3].Callback);
        Assert.Equal((ushort)0xDEAD, children.Slots[3].ReadWord(0xE));
        Assert.Equal(0x5A9290u, children.Slots[4].Callback);
        Assert.Equal((ushort)0, children.Slots[4].ReadWord(0xE));
        Assert.Equal((ushort)1, children.Slots[3].ReadWord(0xC));
        Assert.Equal((ushort)1, children.Slots[4].ReadWord(0xC));
    }

    [Fact]
    public void OriginalLocalSchedulerPreservesTapeBodyNeedlesAndHitC49AcrossIndependentRestoreAndAsyncHold()
    {
        using GameAssets game = Installed();
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        Fixture fixture = Create(game, resources);
        Run current = fixture.Run;
        HashSet<int> restoredAt = [];
        bool observedParticles = false;
        bool observedNeedles = false;
        for (int callback = 0; callback < 400 && current.Frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            if (current.CompletionAddress is { } address && current.CompletionWait == 5)
            {
                current = current with { Frame = current.Frame with { Effect = NativeBattleCactuar199Controller.WriteCompletionByte(current.Frame.Effect, address, 1) } };
            }
            int body = current.Frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x5A8C20).Select(task => (int)task.ReadWord(0xC)).DefaultIfEmpty(-1).First();
            bool compare = body is 0 or 5 or 40 or 43 or 87 or 116 && restoredAt.Add(body);
            string? checkpoint = compare ? NativeBattleSnapshotJson.Serialize(current) : null;
            Run next = Step(current, fixture.Packets, resources, colors, 0);
            if (compare)
            {
                Run restored = NativeBattleSnapshotJson.Deserialize<Run>(checkpoint!)!;
                Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Step(restored, fixture.Packets, resources, colors, 0)));
            }
            observedParticles |= next.ParticleDraws > 0;
            observedNeedles |= next.NeedleDraws > 0;
            current = next;
        }
        Assert.Equal(0u, current.Frame.Effect.ReturnedDescriptor);
        Assert.All(current.Frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(152, current.TimelineCallbacks);
        Assert.Equal(120, current.BodyDraws);
        Assert.Equal(1, current.Contacts);
        Assert.Equal(new[] { 49 }, current.ContactCounters);
        Assert.Equal(16, current.Reactions);
        Assert.Equal(55, current.HitCallbacks);
        Assert.True(current.HitDelayCallbacks is >= 0 and <= 7);
        Assert.Equal(2, current.Sounds);
        Assert.Equal(2, current.StreamStarts);
        Assert.Equal(2, current.StreamPlays);
        Assert.Equal(1, current.FullscreenFlashes);
        Assert.Equal(1, current.VibrationStops);
        Assert.True(current.CompletionWait >= 5);
        Assert.True(current.CommonBytes > 0);
        Assert.True(observedParticles);
        Assert.True(observedNeedles);
        Assert.NotNull(current.Frame.Memory.Cactuar199!.LookupCounter);
        Assert.NotEqual(new NativeCrtRandomState(1), current.Frame.Random);
        Assert.Equal(0x818, current.Frame.Memory.Cactuar199.RawModel.ProjectedScratch!.Value.Length);
        Assert.True(restoredAt.Count >= 5);
        Assert.Contains(current.Frame.Memory.Arena!.Read(NativeBattleCactuar199Controller.TargetCacheOffset, 512 * 0x30), value => value != 0);
        Assert.All(Enumerable.Range(0, resources.Model.PoseSource.BoneCount), index => Assert.Equal(new byte[2], current.Frame.Memory.Arena.Read(0x1C000 + index * 8 + 6, 2)));
    }

    [Fact]
    public void Pause200KeepsPrivateTimelineAndPhysicsButAdvancesCameraWhilePause1KeepsCameraCounter()
    {
        using GameAssets game = Installed();
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        Fixture fixture = Create(game, resources);
        Run initializedCamera = Step(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Run)), fixture.Packets, resources, colors, 0);
        Assert.NotNull(initializedCamera.Frame.Memory.Cactuar199!.CameraTask);
        NativeBattleModulePoolTask timeline = initializedCamera.Frame.Effect.Pools[1].Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == 0x5A8940);
        ushort cameraFrame = initializedCamera.Frame.Memory.Cactuar199.CameraTask!.Frame;
        uint seed = initializedCamera.Frame.Random.Seed;
        Run pause200 = Step(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(initializedCamera)), fixture.Packets, resources, colors, 0x200);
        Assert.Equal(timeline.ReadWord(0xC), pause200.Frame.Effect.Pools[1].Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == 0x5A8940).ReadWord(0xC));
        Assert.Equal(unchecked((ushort)(cameraFrame + 1)), pause200.Frame.Memory.Cactuar199!.CameraTask!.Frame);
        Assert.Equal(seed, pause200.Frame.Random.Seed);
        Run pause1 = Step(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(initializedCamera)), fixture.Packets, resources, colors, 1);
        Assert.Equal(cameraFrame, pause1.Frame.Memory.Cactuar199!.CameraTask!.Frame);
        Assert.Equal(seed, pause1.Frame.Random.Seed);
        Assert.Equal<int>(initializedCamera.Frame.Effect.Pools[0].Slots[0].ReadWord(0xC) + 1, pause1.Frame.Effect.Pools[0].Slots[0].ReadWord(0xC));
        Assert.Equal(initializedCamera.Frame.Memory.Arena!.Read(0x20000, 0x16B80), pause1.Frame.Memory.Arena!.Read(0x20000, 0x16B80));
        Assert.Equal((byte)0, fixture.Run.Frame.Memory.Arena!.Read(0, 1)[0]);
        Assert.Equal(resources.EntryModelCopySeed.Data.AsSpan().Slice(4).ToArray(), fixture.Run.Frame.Memory.Arena.Read(4, 0x21E8));
        Assert.Equal<int?>(3, fixture.Run.Frame.Memory.Cactuar199!.FirstTargetActor);
        Assert.Equal(0, fixture.Run.Frame.Memory.Cursor);
        Assert.Throws<NotSupportedException>(() => NativeBattleCactuar199Controller.Initialize5A8750(fixture.Run.Frame.Memory with { Cactuar199 = null }, fixture.Run.Frame.Effect, Inputs(fixture.Run, fixture.Packets, resources, colors, 0, _ => { }), resources, fixture.Run.Frame.Random));
    }

    private static Run Step(Run current, NativeBattleActionPacketsState packets, NativeBattleCactuar199Resources resources, NativeBattleSummonColors colors, uint flags)
    {
        Run metrics = current;
        NativeBattleCactuar199Frame frame = current.Frame with { Memory = current.Frame.Memory with { PrimitiveProcess = current.Frame.Memory.PrimitiveProcess!.Initialize500400() } };
        NativeBattleModulePoolTask? timeline = frame.Effect.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x5A8940);
        NativeBattleModulePoolTask? hit = frame.Effect.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x5AA2A0);
        if (timeline is not null && (flags & 0x201) == 0)
        {
            metrics = metrics with { TimelineCallbacks = metrics.TimelineCallbacks + 1 };
        }
        if (hit is not null && (flags & 0x201) == 0)
        {
            metrics = hit.ReadWord(0xE) > 0 ? metrics with { HitDelayCallbacks = metrics.HitDelayCallbacks + 1 } : metrics with { HitCallbacks = metrics.HitCallbacks + 1 };
        }
        NativeBattleCactuar199Inputs inputs = Inputs(metrics with { Frame = frame }, packets, resources, colors, flags, next => metrics = next);
        Run updates = metrics;
        inputs = inputs with
        {
            Execute = (context, operation) =>
            {
                switch (operation)
                {
                    case NativeBattleCactuar199CameraPublish camera:
                        updates = updates with { Pose = camera.Step.Pose };
                        return context with { Memory = context.Memory with { GuardianCameraProcess = camera.Step.Task.Process } };
                    case NativeBattleCactuar199Contact:
                        int counter = context.Effect.Pools[1].Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == 0x5AA2A0).ReadWord(0xC);
                        updates = updates with { Contacts = updates.Contacts + 1, ContactCounters = updates.ContactCounters.Add(counter) };
                        break;
                    case NativeBattleCactuar199HitReaction:
                        updates = updates with { Reactions = updates.Reactions + 1 };
                        break;
                    case NativeBattleCactuar199Sound:
                        updates = updates with { Sounds = updates.Sounds + 1 };
                        break;
                    case NativeBattleCactuar199StreamStart:
                        updates = updates with { StreamStarts = updates.StreamStarts + 1 };
                        break;
                    case NativeBattleCactuar199StreamPlay play:
                        Assert.Equal(new NativeBattleCactuar199StreamPlay(128, 0, 96), play);
                        updates = updates with { StreamPlays = updates.StreamPlays + 1 };
                        break;
                    case NativeBattleCactuar199FullscreenFlash:
                        updates = updates with { FullscreenFlashes = updates.FullscreenFlashes + 1 };
                        break;
                    case NativeBattleCactuar199StopVibration:
                        updates = updates with { VibrationStops = updates.VibrationStops + 1 };
                        break;
                    case NativeBattleCactuar199CommonTextureLoad load:
                        Assert.Equal(0x4000, load.DestinationArenaOffset);
                        updates = updates with { CompletionAddress = load.CompletionAddress };
                        break;
                }
                return context;
            },
            ReadCameraPose = () => updates.Pose
        };
        frame = NativeBattleCactuar199Controller.Advance5AA3A0(frame, inputs, resources);
        if (hit is null && (flags & 0x201) == 0)
        {
            NativeBattleModulePoolTask? bornHit = frame.Effect.Pools[1].Slots.FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback == 0x5AA2A0);
            if (bornHit is not null)
            {
                updates = bornHit.ReadWord(0xC) > 0 ? updates with { HitCallbacks = updates.HitCallbacks + 1 } : updates with { HitDelayCallbacks = updates.HitDelayCallbacks + 1 };
            }
        }
        return updates with { Frame = frame, BodyDraws = metrics.BodyDraws, ParticleDraws = metrics.ParticleDraws, NeedleDraws = metrics.NeedleDraws, CommonBytes = metrics.CommonBytes, CompletionWait = updates.CompletionAddress is null ? 0 : updates.CompletionWait + 1 };
    }

    private static NativeBattleCactuar199Inputs Inputs(Run current, NativeBattleActionPacketsState packets, NativeBattleCactuar199Resources resources, NativeBattleSummonColors colors, uint flags, Action<Run> update)
    {
        Run metrics = current;
        NativeBattlePoseSource source = resources.Model.PoseSource;
        NativeBattleDecodedPose actorPose = NativeBattlePoseDecoder.DecodeFirst509440(source.Animations.AsSpan(), source.BoneCount, 0).Pose;
        NativeBattleGraphicsModel Model(int actor) => new(actor, 3) { Rotation14 = actor == 3 ? (short)2048 : (short)0, Transform = new(new(0, 0, 0), new(0, 0, 1000), 0, 0, 4096), GeometryWorldMatrix = Identity, SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = actorPose } };
        NativeBattleCactuar199Frame Append(NativeBattleCactuar199Frame frame, NativeBattleGfGeometryBatch batch)
        {
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(frame.Memory.PrimitiveProcess!, NativeBattleLogicalOrdering.Clear(), batch);
            metrics = metrics with { CommonBytes = metrics.CommonBytes + batch.AllocationBytes };
            update(metrics);
            return frame with { Memory = frame.Memory with { PrimitiveProcess = written.Process } };
        }
        return new(flags, packets, () => View, () => current.Pose, () => current.Frame.Memory.GuardianCameraProcess!, Model, _ => source, (actor, bone, fraction) => Raw(NativeBattlePoseMatrices.ReadEffectOwnedBone(source, actorPose, Identity with { Z = 1000 }, bone, fraction)), _ => Raw(new(0, 0, 1000)), frame =>
        {
            NativeBattleCactuar199State state = frame.Memory.Cactuar199!;
            NativeBattleCactuarRawModelDraw draw = NativeBattleCactuarRawModel.Draw5A9010(state.RawModel, resources.Model, state.BaseView!, Projection, colors);
            metrics = metrics with { BodyDraws = metrics.BodyDraws + 1 };
            update(metrics);
            return Append(frame with { Memory = frame.Memory with { Cactuar199 = state with { RawModel = draw.State } } }, draw.Packets);
        }, (frame, task) => Append(frame, NativeBattleCactuarAuxiliaryDrawing.Mesh(task, resources, unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Cactuar199!.RawModel.Model.AsSpan().Slice(0x58))), frame.Memory.Cactuar199.BaseView!, Projection, colors)), (frame, particle) =>
        {
            NativeBattleCactuarParticleDraw draw = NativeBattleCactuarAuxiliaryDrawing.Particle5A9290(particle, resources.Sprite, frame.Memory.Cactuar199!.BaseView!, Projection)!;
            metrics = metrics with { ParticleDraws = metrics.ParticleDraws + 1 };
            update(metrics);
            return new(frame, draw.FrameResult);
        }, (frame, matrix, position, color) =>
        {
            NativeBattleCactuarNeedleDraw draw = NativeBattleCactuarNeedleDrawing.Emit5A9C10(matrix, position, color, View, Projection);
            metrics = metrics with { NeedleDraws = metrics.NeedleDraws + 1 };
            update(metrics);
            return new(Append(frame, draw.Packets), draw.Packets.AllocationBytes == 80);
        }, frame => new(frame, 7), (frame, _) => frame);
    }

    private static Fixture Create(GameAssets game, NativeBattleCactuar199Resources resources)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 199, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(199, game.ReadNativeEffectCatalogForEffects([199]));
        var loaded = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleCactuar199Frame initial = new(loaded.Memory, loaded.Effect with { Parameters = new(0, 1, 8, 0, 0, 0) }, new(1));
        Run current = new(initial, new(new(0, -100, 8192), new(0, 0, 0), 216, 0), []);
        NativeBattleCactuar199Frame entered = NativeBattleCactuar199Controller.Initialize5A8750(initial.Memory, initial.Effect, Inputs(current, packets, resources, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => { }), resources, initial.Random);
        NativeBattleEffectTextureDrain drain = entered.Memory.Textures!.Drain505D20(entered.Memory.Arena);
        return new(current with { Frame = entered with { Memory = entered.Memory with { Textures = drain.State } } }, packets);
    }
    private static ImmutableArray<byte> Raw(NativeBattleVector vector)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, vector.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), vector.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), vector.Z);
        bytes[6] = 0xA5;
        bytes[7] = 0xB6;
        return bytes.ToImmutableArray();
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Fixture(Run Run, NativeBattleActionPacketsState Packets);
    private sealed record Run(NativeBattleCactuar199Frame Frame, NativeBattleCameraPose Pose, ImmutableArray<int> ContactCounters, uint? CompletionAddress = null, int CompletionWait = 0, int TimelineCallbacks = 0, int BodyDraws = 0, int HitCallbacks = 0, int HitDelayCallbacks = 0, int Contacts = 0, int Reactions = 0, int Sounds = 0, int StreamStarts = 0, int StreamPlays = 0, int FullscreenFlashes = 0, int VibrationStops = 0, int ParticleDraws = 0, int NeedleDraws = 0, int CommonBytes = 0);
}

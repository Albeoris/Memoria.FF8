using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMoogle96ControllerTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleActionMatrix View = Identity with { Z = 16384 };
    private static readonly NativeBattleCameraProjection Projection = new(View, 216, 160, 108);

    [Fact]
    public void OriginalResourcesDrainIndependentModelCameraBothNodeBanksAndRealFileCallbacksBeforeAuxiliaryRelease()
    {
        using GameAssets game = Installed();
        NativeBattleMoogle96Resources resources = game.ReadNativeMoogle96Resources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        Fixture fixture = Create(resources);
        Run current = fixture.Run;
        HashSet<int> modelPhases = [];
        HashSet<int> cameraPhases = [];
        HashSet<string> restoredBoundaries = [];
        for (int callback = 0; callback < 2000 && current.AuxiliaryAddress is null; callback++)
        {
            NativeBattleMoogle96State shared = current.Frame.Memory.Moogle96!;
            NativeBattleModulePoolTask? model = current.Frame.Effect.Pools[7].Head is null ? null : current.Frame.Effect.Pools[7].Slots[0];
            if (model is not null)
            {
                modelPhases.Add(model.ReadByte(0x29));
            }
            foreach (NativeBattleModulePoolTask camera in current.Frame.Effect.Pools[5].Slots.Where(task => task.Callback == 0x732A00))
            {
                cameraPhases.Add(camera.ReadByte(0x29));
            }
            string? boundary = current.FileIo.ActiveCount > 0 ? "file" : model is not null ? $"model{model.ReadByte(0x29)}" : shared.Nodes.Banks[1].InitializationFlag != 0 ? "second-bank" : null;
            Run? restored = boundary is not null && restoredBoundaries.Add(boundary) ? NativeBattleSnapshotJson.Deserialize<Run>(NativeBattleSnapshotJson.Serialize(current)) : null;
            Run next = Step(current, fixture.Packets, resources, rules);
            if (restored is not null)
            {
                Assert.NotSame(current.Frame.Effect.Pools[0], restored.Frame.Effect.Pools[0]);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Step(restored, fixture.Packets, resources, rules)));
            }
            current = next;
        }
        Assert.NotNull(current.AuxiliaryAddress);
        Assert.Equal(NativeBattleMoogle96Controller.MainBank + 0x62, current.AuxiliaryAddress);
        Assert.Equal((byte)8, current.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.All(current.Frame.Effect.Pools.Skip(1), pool => Assert.Null(pool.Head));
        Assert.Equal(new[] { 0, 1 }, current.Contacts);
        Assert.Equal(4, current.FileRequests);
        Assert.Equal(4, current.FileCompletions);
        Assert.Equal((short)0, current.FileIo.ActiveCount);
        Assert.True(current.TextureCopies > 0);
        Assert.True(current.ModelBytes > 0);
        Assert.True(current.ProgramBytes > 0);
        Assert.True(current.DirectorPackets > 0);
        Assert.True(current.CameraPublications > 12);
        Assert.Equal(1, current.VibrationStarts);
        Assert.Equal(1, current.VibrationStops);
        Assert.Equal(2, current.Sounds);
        Assert.All(new[] { 1, 2, 3, 4 }, phase => Assert.Contains(phase, modelPhases));
        Assert.Contains(14, cameraPhases);
        Assert.Contains("file", restoredBoundaries);
        Assert.True(restoredBoundaries.Count >= 4);
        Assert.All(current.Frame.Memory.Moogle96!.Nodes.Banks, bank => Assert.Equal(1u, bank.InitializationFlag));
        Assert.False(NativeBattleStorageSnapshots.Capture(resources.ProgramSource).Data.SequenceEqual(NativeBattleStorageSnapshots.Capture(current.Frame.Memory.Moogle96.ProgramSource!).Data));
        Assert.Equal(0xFD8, current.Frame.Memory.Moogle96.ProjectedScratch!.Value.Length);
        Assert.Contains((short)4096, current.Depths);
        Assert.Equal((short)0, current.Depths[^1]);
        Assert.Equal((short)127, current.Gains[^1]);
        Assert.Equal((ushort)7, current.Models[4].Flags0 & 7);
        Assert.Equal(0xA1445566u, current.Models[4].Color44);
        Run waiting = Step(current, fixture.Packets, resources, rules);
        Assert.NotEqual(0u, waiting.Frame.Effect.ReturnedDescriptor);
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Controller.WriteCompletionByte(current.Frame.Effect, current.AuxiliaryAddress!.Value + 1, 1));
        current = current with { Frame = current.Frame with { Effect = NativeBattleMoogle96Controller.WriteCompletionByte(current.Frame.Effect, current.AuxiliaryAddress!.Value, 1) } };
        for (int callback = 0; callback < 4 && current.Frame.Effect.ReturnedDescriptor != 0; callback++)
        {
            current = Step(current, fixture.Packets, resources, rules);
        }
        Assert.Equal(0u, current.Frame.Effect.ReturnedDescriptor);
        Assert.All(current.Frame.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void EntryRequiresExplicitColdOwnersAndPreservesWarmedSourceAndScriptAcrossIndependentRestore()
    {
        using GameAssets game = Installed();
        NativeBattleMoogle96Resources resources = game.ReadNativeMoogle96Resources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        Fixture fixture = Create(resources);
        Run initialCheckpoint = NativeBattleStorageSnapshots.Capture(fixture.Run);
        NativeBattleMoogle96Frame initial = fixture.Run.Frame;
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Controller.Initialize731DE0(initial.Memory with { Moogle96 = null }, initial.Effect, fixture.Packets, resources, initial.Random));
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96Controller.Initialize731DE0(initial.Memory with { Moogle96 = initial.Memory.Moogle96! with { ProgramSource = null, ProgramSourceKnownCold = false } }, initial.Effect, fixture.Packets, resources, initial.Random));
        Run current = fixture.Run;
        for (int callback = 0; callback < 40; callback++)
        {
            current = Step(current, fixture.Packets, resources, rules);
        }
        Assert.NotNull(current.Frame.Memory.Moogle96!.Io);
        NativeBattleMoogle96State retained = current.Frame.Memory.Moogle96;
        NativeBattleMoogle96Frame restored = NativeBattleSnapshotJson.Deserialize<NativeBattleMoogle96Frame>(NativeBattleSnapshotJson.Serialize(current.Frame))!;
        NativeBattleMoogle96Frame nextCast = NativeBattleMoogle96Controller.Initialize731DE0(restored.Memory, NativeBattleStorageSnapshots.Restore(initialCheckpoint).Frame.Effect, fixture.Packets, resources, restored.Random);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(retained.ProgramSource), NativeBattleSnapshotJson.Serialize(nextCast.Memory.Moogle96!.ProgramSource));
        Assert.Equal(retained.Io!.Script.ToArray(), nextCast.Memory.Moogle96.Io!.Script.ToArray());
        Assert.False(nextCast.Memory.Moogle96.ProgramSourceKnownCold);
        Assert.False(nextCast.Memory.Moogle96.IoScriptKnownCold);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(retained.Nodes), NativeBattleSnapshotJson.Serialize(nextCast.Memory.Moogle96.Nodes));
        Run first = Step(NativeBattleStorageSnapshots.Restore(initialCheckpoint), fixture.Packets, resources, rules);
        Assert.Equal(0x2000, first.Frame.Memory.Moogle96!.PrivateBufferAliasOffset);
        Run second = Step(first, fixture.Packets, resources, rules);
        Assert.Equal(0x1000, second.Frame.Memory.Moogle96!.PrivateBufferAliasOffset);
        NativeBattleGfGeometryBatch director = NativeBattleMoogle96DirectorPackets.Batch7327B0();
        Assert.Equal(12, director.AllocationBytes);
        Assert.Equal(2u, director.Attempts[0].Primitive!.Words[0]);
        Assert.Equal(0u, director.Attempts[0].Primitive!.Words[0] >> 24);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), director);
        Assert.Equal(12, written.Process.ByteCursor);
        Assert.Single(written.Ordering.Proxies);
        Assert.Empty(written.Ordering.Traverse(new byte[256].ToImmutableArray(), (reference, count) => written.Process.ReadCommonPacket(reference.ByteOffset, count)));
    }

    private static Run Step(Run run, NativeBattleActionPacketsState packets, NativeBattleMoogle96Resources resources, NativeBattleGraphicsRules rules)
    {
        NativeBattleMoogle96Frame frame = run.Frame with { Memory = run.Frame.Memory with { PrimitiveProcess = run.Frame.Memory.PrimitiveProcess!.Initialize500400() } };
        NativeBattleGraphicsModel[] models = run.Models.ToArray();
        NativeBattleFileIoState fileIo = run.FileIo;
        Run metrics = run;
        NativeBattleMoogle96Frame AppendCommon(NativeBattleMoogle96Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess!, NativeBattleLogicalOrdering.Clear(), batch);
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
        NativeBattleMoogle96Inputs inputs = new(packets, View, actor => models[actor], _ => resources.Model.PoseSource, 0x8090A0B0, (current, task, model) =>
        {
            NativeBattleMoogle96State shared = current.Memory.Moogle96!;
            NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model, View, Projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, shared.ProjectedScratch ?? new byte[0xFD8].ToImmutableArray());
            metrics = metrics with { ModelBytes = metrics.ModelBytes + draw.Packets.AllocationBytes };
            return new(AppendCommon(current with { Memory = current.Memory with { Moogle96 = shared with { ProjectedScratch = draw.ProjectedScratch } } }, draw.Packets), draw.Task);
        }, (current, task) =>
        {
            NativeBattleMoogle96State shared = current.Memory.Moogle96!;
            NativeBattleMoogle96ProgramDraw draw = NativeBattleMoogle96ProgramDrawing.Advance734450(task, shared.ProgramSource!, View, Projection, rules.SummonColors!, shared.Global257CF24!.Value);
            metrics = metrics with { ProgramBytes = metrics.ProgramBytes + draw.Packets.AllocationBytes };
            return new(AppendCommon(current, draw.Packets), draw.Task, draw.Remaining);
        }, (current, address) =>
        {
            NativeBattleMoogle96State shared = current.Memory.Moogle96!;
            NativeBattleMoogle96NodeState nodes = shared.Nodes with { Work = shared.Nodes.Work with { PrivateDrawCursor = current.Effect.DrawArenaOffset } };
            NativeBattleMoogle96NodeStep advanced = NativeBattleMoogle96Nodes.AdvanceContext(new(current.Memory, current.Effect, nodes, current.Random), address, actor => models[actor], Projection, 0, NativeBattleLogicalOrdering.Clear(), rules.SummonColors!);
            return new(new(advanced.Frame.Memory with { Moogle96 = advanced.Frame.Memory.Moogle96! with { Nodes = advanced.Frame.State } }, advanced.Frame.Effect with { DrawArenaOffset = advanced.Frame.State.Work.PrivateDrawCursor }, advanced.Frame.Random), advanced.Complete);
        }, current =>
        {
            metrics = metrics with { VibrationStarts = metrics.VibrationStarts + 1 };
            return new(current, 7);
        }, (current, operation) =>
        {
            switch (operation)
            {
                case NativeBattleMoogle96FileRequest request:
                    NativeBattleMoogle96IoQueued queued = NativeBattleMoogle96Io.CopyAndQueueFile(current.Memory.Moogle96!.Io!, current.Memory, fileIo, resources.Files.Single(file => file.FileId == request.Request.FileId));
                    fileIo = queued.FileIo;
                    metrics = metrics with { FileRequests = metrics.FileRequests + 1 };
                    return current with { Memory = queued.Memory };
                case NativeBattleMoogle96Contact contact:
                    metrics = metrics with { Contacts = metrics.Contacts.Add(contact.Contact) };
                    break;
                case NativeBattleMoogle96Sound:
                    metrics = metrics with { Sounds = metrics.Sounds + 1 };
                    break;
                case NativeBattleMoogle96StopVibration stop:
                    Assert.Equal((short)7, stop.Handle);
                    metrics = metrics with { VibrationStops = metrics.VibrationStops + 1 };
                    break;
                case NativeBattleMoogle96CameraPublish:
                    metrics = metrics with { CameraPublications = metrics.CameraPublications + 1 };
                    break;
                case NativeBattleMoogle96StageFade fade when fade.Depth is { } depth:
                    metrics = metrics with { Depths = metrics.Depths.Add(depth) };
                    break;
                case NativeBattleMoogle96MusicGain gain:
                    metrics = metrics with { Gains = metrics.Gains.Add(gain.Value) };
                    break;
                case NativeBattleMoogle96EnemyFlags enemy:
                    models[enemy.Actor] = models[enemy.Actor] with { Flags0 = enemy.Flags0 };
                    break;
                case NativeBattleMoogle96EnemyFade enemy:
                    models[enemy.Actor] = models[enemy.Actor] with { Flags0 = enemy.Flags0, Color40 = enemy.Color40, Alpha7 = enemy.Alpha7 };
                    break;
                case NativeBattleMoogle96DirectorModeE1:
                    metrics = metrics with { DirectorPackets = metrics.DirectorPackets + 1 };
                    return AppendCommon(current, NativeBattleMoogle96DirectorPackets.Batch7327B0());
                case NativeBattleMoogle96TextureCopy copy:
                    metrics = metrics with { TextureCopies = metrics.TextureCopies + 1 };
                    return current with { Memory = current.Memory with { Textures = current.Memory.Textures!.EnqueueCopy505EB0(copy.Copy.SourceX, copy.Copy.SourceY, copy.Copy.Width, copy.Copy.Height, copy.Copy.DestinationX, copy.Copy.DestinationY) } };
                case NativeBattleMoogle96CommonTextureLoad load:
                    metrics = metrics with { AuxiliaryAddress = load.CompletionAddress };
                    break;
            }
            return current;
        });
        frame = NativeBattleMoogle96Controller.Advance731F70(frame, inputs, resources);
        fileIo = fileIo.Advance482590((current, completion) =>
        {
            Assert.Equal(0x732590u, completion.Callback);
            NativeBattleMoogle96IoCompletion completed = NativeBattleMoogle96Io.Complete732590(frame.Memory.Moogle96!.Io!, frame.Memory);
            frame = frame with { Memory = completed.Memory with { Moogle96 = completed.Memory.Moogle96! with { Io = completed.State } } };
            metrics = metrics with { FileCompletions = metrics.FileCompletions + 1 };
            return current;
        });
        NativeBattleEffectTextureDrain drain = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena);
        frame = frame with { Memory = frame.Memory with { Textures = drain.State } };
        return metrics with { Frame = frame, Models = models.ToImmutableArray(), FileIo = fileIo };
    }

    private static Fixture Create(NativeBattleMoogle96Resources resources)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 96, null));
        foreach (byte actor in new byte[] { 3, 4 })
        {
            (packets, _) = packets.AppendContact(new(new(actor, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(2);
        NativeBattleDecodedPose pose = NativeBattlePoseDecoder.DecodeFirst509440(resources.Model.PoseSource.Animations.AsSpan(), resources.Model.PoseSource.BoneCount, 0).Pose;
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(actor => NativeBattleBodyBoneMatrices.Publish508C90(new NativeBattleGraphicsModel(actor, actor == 6 ? (ushort)0 : actor == 4 ? (ushort)7 : (ushort)3) { ModelId4 = 0, Flags2 = 0, Color40 = 0x80606060, Color44 = 0xA1445566, Transform = new(new(0, 0, 0), new((short)(actor * 100), 0, 300), 77, 0, 4096), GeometryWorldMatrix = Identity, GeometryBounds52 = new(new(-100, -100, -100), new(100, 100, 100)), SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = pose } }, resources.Model.PoseSource)).ToImmutableArray();
        NativeBattleOriginalEffectState effect = new(96, 95, new(96, 0x731DE0, 0x731DC0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x257B740, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Moogle96 = NativeBattleMoogle96State.FromColdProcess(), Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() };
        NativeBattleMoogle96Frame frame = NativeBattleMoogle96Controller.Initialize731DE0(memory, effect, packets, resources, new(1));
        return new(new(frame, models, NativeBattleFileIoState.FromColdProcess(), [], [], [], null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), packets);
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Fixture(Run Run, NativeBattleActionPacketsState Packets);
    private sealed record Run(NativeBattleMoogle96Frame Frame, ImmutableArray<NativeBattleGraphicsModel> Models, NativeBattleFileIoState FileIo, ImmutableArray<int> Contacts, ImmutableArray<short> Depths, ImmutableArray<short> Gains, uint? AuxiliaryAddress, int FileRequests, int FileCompletions, int TextureCopies, int ModelBytes, int ProgramBytes, int DirectorPackets, int CameraPublications, int VibrationStarts, int VibrationStops, int Sounds);
}

using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMeteor223MotionTests(ITestOutputHelper testOutput)
{
    [Fact]
    public void Original_Meteor_grid_uses_native_pixel_flags_and_continuous_world_motion()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selectedOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR_MOTION_OUTPUT");
        Assert.SkipUnless(directory is not null && selectedOutput is not null, "Select installed resources and a fresh external motion output directory.");
        string output = Path.GetFullPath(selectedOutput!);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 2 && row.MagicId == 223);
        string fixture = Path.Combine(output, "input");
        NativeEffectFixtureRequest request = new("meteor-motion", 223, 2, action.ActionId, 3, 800, 240) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 800 };
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, fixture);
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "ordinary-produced-action104.battle.json"));
        object fixtureFlags = NativeBattleOrdinaryMagicFixtureFlagTests.VerifyBoundaries(fixture, source, assets, game, action);
        File.WriteAllText(Path.Combine(output, "fixture-flags.json"), JsonSerializer.Serialize(fixtureFlags, new JsonSerializerOptions { WriteIndented = true }));
        NativeBattleOrdinaryMagicFixtureFlagTests.DeleteFixtureSnapshots(fixture, exported);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216, new PixelBounds(0, 0, 320, 216)) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        NativeBattlePreparedPresentation? earlier = null;
        Face[] earlierFaces = [];
        List<object> intervals = [];
        List<object> submissions = [];
        HashSet<string> renderedKinds = [];
        bool sawEffectFaces = false;
        bool checkedColdGrid = false;
        int observedIntervals = 0;
        int timelineCompatibleIntervals = 0;
        int eligibleIntervals = 0;
        int maximumEffectFaces = 0;
        Dictionary<string, long> producerFaces = [];
        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        for (int tick = 0; tick < 512; tick++)
        {
            battle.Tick();
            battle.RefreshPreparedPresentation();
            NativeBattlePreparedPresentation? later = battle.PreparedPresentation;
            if (later?.PilotEffect != 223)
            {
                if (sawEffectFaces)
                {
                    break;
                }
                earlier = null;
                earlierFaces = [];
                continue;
            }
            Face[] laterFaces = Faces(later, battle.NativeEffectMemory!.Arena!);
            sawEffectFaces |= laterFaces.Length > 0;
            Assert.All(laterFaces.Where(face => face.Kind == "grid25"), face =>
            {
                Assert.Equal((ushort)1, face.Command.Command.Flags20);
                Assert.Equal(1f, face.Geometry.ScreenScale);
            });
            if (earlier is not null && later.Callback == earlier.Callback + 1)
            {
                bool timelineCompatible = earlier.CanInterpolateMotion && later.CanInterpolateMotion && earlier.PilotGeneration == later.PilotGeneration && earlier.SceneMotion == later.SceneMotion && earlier.PilotProjection is not null && later.PilotProjection is not null && earlier.PilotCamera is not null && later.PilotCamera is not null;
                bool canSample = renderer.CanSampleNativePilotInterval(earlier, later);
                observedIntervals++;
                timelineCompatibleIntervals += timelineCompatible ? 1 : 0;
                eligibleIntervals += canSample ? 1 : 0;
                maximumEffectFaces = Math.Max(maximumEffectFaces, laterFaces.Length);
                foreach (var producer in laterFaces.GroupBy(face => face.Kind))
                {
                    producerFaces[producer.Key] = producerFaces.GetValueOrDefault(producer.Key) + producer.LongCount();
                }
                if (intervals.Count < 2 || !canSample && intervals.Count < 6)
                {
                    intervals.Add(new { earlier = earlier.Callback, later = later.Callback, earlier.PilotGeneration, timelineCompatible, canSample, renderer.LastPilotFallbackCount, renderer.LastPilotFallbackReason, earlierFaces = earlierFaces.Length, laterFaces = laterFaces.Length, producers = DescribeProducers(earlierFaces, laterFaces), missing = Missing(earlier, later) });
                }
                foreach (string kind in new[] { "grid25", "mesh27" })
                {
                    Face? selectedBefore = earlierFaces.FirstOrDefault(before => before.Kind == kind && VisibleNativeFace(before.Command) && laterFaces.Any(next => VisibleNativeFace(next.Command) && before.Geometry.Identity == next.Geometry.Identity && before.Geometry.CornerCount == next.Geometry.CornerCount && (WorldDelta(before, next) > 0.000001f || !Uv(before.Command).SequenceEqual(Uv(next.Command)))));
                    if (!timelineCompatible || selectedBefore is null || renderedKinds.Contains(kind) || submissions.Count >= 2)
                    {
                        continue;
                    }
                    Face selectedAfter = laterFaces.First(next => selectedBefore.Geometry.Identity == next.Geometry.Identity && selectedBefore.Geometry.CornerCount == next.Geometry.CornerCount);
                    NativeBattlePreparedPresentation selectedEarlier = IsolateFace(earlier, selectedBefore.Command);
                    NativeBattlePreparedPresentation selectedLater = IsolateFace(later, selectedAfter.Command);
                    if (new[] { 0.25f, 0.5f, 0.75f }.SelectMany(alpha => ExpectedPositions(selectedEarlier, selectedLater, selectedBefore, selectedAfter, alpha)).Any(point => point.X <= 1 || point.X >= 319 || point.Y <= 1 || point.Y >= 215))
                    {
                        continue;
                    }
                    renderedKinds.Add(kind);
                    string earlierWords = CommandHash(selectedEarlier);
                    string laterWords = CommandHash(selectedLater);
                    NativeBattlePrimitiveCapabilities capabilities = source.Definition.NativeGraphics!.PrimitiveCapabilities!;
                    renderer.RenderNativePrimitiveFrame(selectedEarlier.Primitives, selectedEarlier.Textures, capabilities: capabilities, textureWindowCache: selectedEarlier.TextureWindows, effectTextureSource: assets.EffectTextureSource);
                    string nativeEarlier = capture.Hash();
                    renderer.RenderNativePrimitiveFrame(selectedLater.Primitives, selectedLater.Textures, capabilities: capabilities, textureWindowCache: selectedLater.TextureWindows, effectTextureSource: assets.EffectTextureSource);
                    string nativeLater = capture.Hash();
                    List<object> samples = [];
                    foreach (float alpha in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                    {
                        renderer.RenderNativePilotPrimitiveFrame(selectedEarlier, selectedLater, alpha, capabilities: capabilities, effectTextureSource: assets.EffectTextureSource);
                        string hash = capture.Hash();
                        Vector2[] expected = ExpectedPositions(selectedEarlier, selectedLater, selectedBefore, selectedAfter, alpha);
                        float? error = alpha is > 0 and < 1 && capture.Vertices.Count == expected.Length ? capture.Vertices.Select((vertex, index) => Vector2.Distance(vertex.Position, expected[index])).Max() : null;
                        if (alpha == 0)
                        {
                            Assert.Equal(nativeEarlier, hash);
                        }
                        else if (alpha == 1)
                        {
                            Assert.Equal(nativeLater, hash);
                        }
                        else
                        {
                            Assert.Equal(expected.Length, capture.Vertices.Count);
                            Assert.InRange(error!.Value, 0, 0.002f);
                            Assert.True(renderer.PilotSubmission.CoveredContinuousCorners > 0);
                            Assert.NotEqual(nativeEarlier, hash);
                            Assert.NotEqual(nativeLater, hash);
                        }
                        Assert.Equal(earlierWords, CommandHash(selectedEarlier));
                        Assert.Equal(laterWords, CommandHash(selectedLater));
                        samples.Add(new { alpha, vertices = capture.Vertices.Count, vertexSha256 = hash, equalsNativeEarlier = hash == nativeEarlier, equalsNativeLater = hash == nativeLater, renderer.LastPilotFallbackCount, renderer.LastPilotFallbackReason, renderer.PilotSubmission, positionNorm = capture.Vertices.Sum(vertex => (double)vertex.Position.Length()), maximumExpectedPositionError = error, firstPositions = capture.Vertices.Take(6).Select(vertex => new { vertex.Position.X, vertex.Position.Y }).ToArray() });
                    }
                    if (kind == "grid25" && !checkedColdGrid)
                    {
                        VerifyColdNext(battle.Capture(), game);
                        checkedColdGrid = true;
                    }
                    submissions.Add(new { kind, earlier = earlier.Callback, later = later.Callback, producers = DescribeProducers(earlierFaces, laterFaces), identity = selectedBefore.Geometry.Identity, selectedBefore.Geometry.ScreenScale, beforeWorld = Corners(selectedBefore.Geometry), afterWorld = Corners(selectedAfter.Geometry), beforeUv = Uv(selectedBefore.Command), afterUv = Uv(selectedAfter.Command), earlierWords, laterWords, nativeEarlier, nativeLater, samples });
                }
            }
            if (earlier?.Callback != later.Callback)
            {
                earlier = later;
                earlierFaces = laterFaces;
            }
        }
        File.WriteAllText(Path.Combine(output, "motion.json"), JsonSerializer.Serialize(new { effect = 223, action.Command, action.ActionId, maximumTicks = 512, observedIntervals, timelineCompatibleIntervals, eligibleIntervals, maximumEffectFaces, producerFaces, intervals, submissions, checkedColdGrid, interpretation = "Producer classifications use original slot draw kinds. Whole-pixel grid delivery and world interpolation are checked independently of native discrete UV motion." }, new JsonSerializerOptions { WriteIndented = true }));
        testOutput.WriteLine($"Meteor motion: {observedIntervals} adjacent intervals and {submissions.Count} recorded submissions in {output}.");
        Assert.NotEmpty(intervals);
        Assert.True(sawEffectFaces, "The bounded original-resource continuation must emit effect-owned geometry.");
        Assert.Contains("grid25", renderedKinds);
        Assert.Contains("mesh27", renderedKinds);
        Assert.True(checkedColdGrid);
    }

    private static void VerifyColdNext(BattleSnapshot snapshot, GameAssets game)
    {
        string frozen = JsonSerializer.Serialize(snapshot);
        BattleSession direct = BattleSession.Restore(snapshot);
        BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
        direct.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        cold.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        direct.Tick();
        cold.Tick();
        Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Assert.Equal(frozen, JsonSerializer.Serialize(snapshot));
        direct.RefreshPreparedPresentation();
        cold.RefreshPreparedPresentation();
        Assert.Equal(CommandHash(direct.PreparedPresentation!), CommandHash(cold.PreparedPresentation!));
        Face[] directFaces = Faces(direct.PreparedPresentation!, direct.NativeEffectMemory!.Arena!).Where(face => face.Kind == "grid25").ToArray();
        Face[] coldFaces = Faces(cold.PreparedPresentation!, cold.NativeEffectMemory!.Arena!).Where(face => face.Kind == "grid25").ToArray();
        Assert.NotEmpty(directFaces);
        Assert.Equal(directFaces.Length, coldFaces.Length);
        for (int index = 0; index < directFaces.Length; index++)
        {
            Assert.Equal((ushort)1, coldFaces[index].Command.Command.Flags20);
            Assert.Equal(1f, coldFaces[index].Geometry.ScreenScale);
            Assert.Equal(directFaces[index].Command.Command.Packet, coldFaces[index].Command.Command.Packet);
            Assert.Equal(directFaces[index].Command.Command.WordOffset, coldFaces[index].Command.Command.WordOffset);
            Assert.Equal(directFaces[index].Geometry.A, coldFaces[index].Geometry.A);
            Assert.Equal(directFaces[index].Geometry.B, coldFaces[index].Geometry.B);
            Assert.Equal(directFaces[index].Geometry.C, coldFaces[index].Geometry.C);
            Assert.Equal(directFaces[index].Geometry.D, coldFaces[index].Geometry.D);
            Assert.Equal(Uv(directFaces[index].Command), Uv(coldFaces[index].Command));
        }
    }

    private static string CommandHash(NativeBattlePreparedPresentation frame)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> bytes = stackalloc byte[4];
        foreach (NativeBattleLogicalResolvedCommand command in frame.Primitives.LastCommands)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes, command.Command.Flags20);
            hash.AppendData(bytes);
            foreach (uint word in command.Command.Words)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, word);
                hash.AppendData(bytes);
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static NativeBattlePreparedPresentation IsolateFace(NativeBattlePreparedPresentation presentation, NativeBattleLogicalResolvedCommand command) => presentation with { Primitives = presentation.Primitives with { LastCommands = [command], LastPcRenderTail = null }, UiFrame = null };

    private static bool VisibleNativeFace(NativeBattleLogicalResolvedCommand resolved)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        int stride = 1 + ((command.Opcode & 4) != 0 ? 1 : 0) + ((command.Opcode & 16) != 0 ? 1 : 0);
        float scale = (command.Flags20 & 1) != 0 ? 1 : 0.125f;
        return Enumerable.Range(0, (command.Opcode & 8) != 0 ? 4 : 3).All(corner =>
        {
            uint xy = command.Words[1 + corner * stride];
            float x = unchecked((short)xy) * scale;
            float y = unchecked((short)(xy >> 16)) * scale;
            return x > 20 && x < 300 && y > 20 && y < 196;
        });
    }

    private static Vector2[] ExpectedPositions(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later, Face first, Face next, float alpha)
    {
        NativeBattleCameraProjection projection = NativeBattlePilotVisualGeometry.SampleProjection(earlier.PilotProjection!, later.PilotProjection!, earlier.PilotCamera!, later.PilotCamera!, alpha);
        if (first.Geometry.FocalLengthOverride is ushort focal)
        {
            projection = projection with { FocalLength = focal };
        }
        bool full = (first.Command.Command.Flags20 & 1) != 0;
        Vector2 Point(int corner)
        {
            Vector3 world = NativeBattlePilotVisualGeometry.SampleCorner(first.Geometry, next.Geometry, corner, alpha);
            Vector3 projected = NativeBattlePilotVisualGeometry.Project(world, projection);
            Vector2 coordinate = new(projected.X * (full ? 0.125f : 1), projected.Y * (full ? 0.125f : 1));
            NativeBattlePrimitiveDrawMode mode = first.Command.DrawMode;
            if (mode.NormalViewport is { } viewport && mode.ViewportInputs is { } inputs)
            {
                return new((coordinate.X * (full ? viewport.ScaleX : viewport.LowCoordinateScaleX) + viewport.NormalOffsetX + 0.5f) * 320 / inputs.Width, (coordinate.Y * (full ? viewport.ScaleY : viewport.LowCoordinateScaleY) + viewport.NormalOffsetY + 0.5f) * 216 / inputs.Height);
            }
            return coordinate * (full ? 1 : 0.125f);
        }
        return first.Geometry.CornerCount == 4 ? [Point(0), Point(1), Point(2), Point(1), Point(3), Point(2)] : [Point(0), Point(1), Point(2)];
    }

    private sealed record Face(NativeBattleLogicalResolvedCommand Command, NativeBattlePilotVisualGeometry Geometry, string Kind, int DrawKind, uint SlotAddress, uint? Model, uint? GeometryAddress, short[] PoseWords);

    private static Face[] Faces(NativeBattlePreparedPresentation presentation, NativeBattleEffectArena arena)
    {
        return presentation.Primitives.LastCommands.Where(Active).Where(command => (command.Command.Opcode & 0xE0) == 0x20 && command.Command.PilotGeometry?.Identity.Effect == 223).Select(command =>
        {
            NativeBattlePilotVisualGeometry geometry = command.Command.PilotGeometry!;
            uint slot = checked(geometry.Identity.Bank + (uint)geometry.Identity.Slot * 0x100);
            int offset = checked((int)(slot - NativeBattleEffectNodeMemory.ArenaAddress));
            int drawKind = arena.Read(offset + 0x1C, 1)[0];
            string kind = drawKind == 3 ? "model3" : drawKind == 25 ? "grid25" : drawKind is 1 or 2 or 9 or 27 or 57 ? $"mesh{drawKind}" : $"draw{drawKind}";
            uint? model = null;
            uint? modelGeometry = null;
            if (drawKind == 3)
            {
                model = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset + 0xBC, 4));
                uint table = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked((int)(model.Value + 0x14 - NativeBattleEffectNodeMemory.ArenaAddress)), 4));
                modelGeometry = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(checked((int)(table + 4 - NativeBattleEffectNodeMemory.ArenaAddress)), 4));
            }
            short[] pose = Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 0x8C + index * 2, 2))).ToArray();
            return new Face(command, geometry, kind, drawKind, slot, model, modelGeometry, pose);
        }).ToArray();
    }

    private static object[] DescribeProducers(Face[] before, Face[] after)
    {
        return after.GroupBy(face => (face.Kind, face.SlotAddress)).Select(group =>
        {
            Face[] next = group.ToArray();
            Face[] prior = before.Where(face => face.SlotAddress == group.Key.SlotAddress && face.Kind == group.Key.Kind).ToArray();
            var pairs = prior.Select(first => (First: first, Next: next.FirstOrDefault(second => second.Geometry.Identity == first.Geometry.Identity && second.Geometry.CornerCount == first.Geometry.CornerCount))).Where(pair => pair.Next is not null).ToArray();
            return (object)new
            {
                kind = group.Key.Kind,
                slot = $"0x{group.Key.SlotAddress:X8}",
                drawKind = next[0].DrawKind,
                model = next[0].Model,
                geometry = next[0].GeometryAddress,
                previousPoseWords8C = prior.FirstOrDefault()?.PoseWords,
                nextPoseWords8C = next[0].PoseWords,
                previousFaces = prior.Length,
                nextFaces = next.Length,
                duplicateNextIdentities = next.GroupBy(face => face.Geometry.Identity).Count(identities => identities.Count() > 1),
                previousResourceGenerations = prior.Select(face => face.Geometry.Identity.ResourceGeneration).Distinct().ToArray(),
                nextResourceGenerations = next.Select(face => face.Geometry.Identity.ResourceGeneration).Distinct().ToArray(),
                identityMismatches = prior.Count(first => next.Any(second => first.Geometry.Identity.Bank == second.Geometry.Identity.Bank && first.Geometry.Identity.Slot == second.Geometry.Identity.Slot && first.Geometry.Identity.Primitive == second.Geometry.Identity.Primitive && first.Geometry.Identity != second.Geometry.Identity)),
                matchedFaces = pairs.Length,
                continuousMatchedFaces = pairs.Count(pair => pair.First.Geometry.Continuous && pair.Next!.Geometry.Continuous),
                worldMovingFaces = pairs.Count(pair => WorldDelta(pair.First, pair.Next!) > 0.000001f),
                maximumWorldDeltaSquared = pairs.Select(pair => WorldDelta(pair.First, pair.Next!)).DefaultIfEmpty().Max(),
                uvMovingFaces = pairs.Count(pair => !Uv(pair.First.Command).SequenceEqual(Uv(pair.Next!.Command))),
                examples = pairs.Take(3).Select(pair => new { identity = pair.First.Geometry.Identity, beforeWorld = Corners(pair.First.Geometry), afterWorld = Corners(pair.Next!.Geometry), beforeUv = Uv(pair.First.Command), afterUv = Uv(pair.Next!.Command), beforePage = pair.First.Command.DrawMode.TexturePage, afterPage = pair.Next!.Command.DrawMode.TexturePage }).ToArray()
            };
        }).ToArray();
    }

    private static object[] Missing(params NativeBattlePreparedPresentation[] frames)
    {
        return frames.SelectMany(frame => frame.Primitives.LastCommands).Where(Active).Where(item => item.Command.PilotGeometry is null && item.Command.ScreenCoverage is null && item.Command.Opcode is not (>= 0xE1 and <= 0xE6) and not 0x02 and not 0x80).GroupBy(item => (item.Command.Packet.Bank, item.Command.Opcode)).Select(group => (object)new { group.Key.Bank, opcode = $"0x{group.Key.Opcode:X2}", count = group.Count(), example = group.First().Command.Packet }).ToArray();
    }

    private static bool Active(NativeBattleLogicalResolvedCommand command) => command.Pass == 1 && (command.Command.Opcode & 2) == 0 || command.Pass == 2 && (command.Command.Opcode & 2) != 0;

    private static float WorldDelta(Face before, Face after) => Enumerable.Range(0, Math.Min(before.Geometry.CornerCount, after.Geometry.CornerCount)).Select(corner => Vector3.DistanceSquared(before.Geometry.Corner(corner), after.Geometry.Corner(corner))).DefaultIfEmpty().Max();

    private static float[][] Corners(NativeBattlePilotVisualGeometry geometry) => Enumerable.Range(0, geometry.CornerCount).Select(corner => new[] { geometry.Corner(corner).X, geometry.Corner(corner).Y, geometry.Corner(corner).Z }).ToArray();

    private static ushort[] Uv(NativeBattleLogicalResolvedCommand resolved)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        if ((command.Opcode & 0xE4) != 0x24)
        {
            return [];
        }
        int count = (command.Opcode & 8) != 0 ? 4 : 3;
        int stride = (command.Opcode & 16) != 0 ? 3 : 2;
        return Enumerable.Range(0, count).Select(corner => (ushort)command.Words[2 + corner * stride]).ToArray();
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }

        public string Hash()
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Span<byte> bytes = stackalloc byte[4];
            foreach (TriangleVertex vertex in Vertices)
            {
                foreach (float value in new[] { vertex.Position.X, vertex.Position.Y, vertex.Depth, vertex.ReciprocalW, vertex.Uv.X, vertex.Uv.Y })
                {
                    BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
                    hash.AppendData(bytes);
                }
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }
    }
}

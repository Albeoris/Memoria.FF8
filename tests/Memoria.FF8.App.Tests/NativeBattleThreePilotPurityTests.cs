using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleThreePilotPurityTests
{
    [Theory]
    [InlineData((ushort)2, 1)]
    [InlineData((ushort)3, 7)]
    [InlineData((ushort)144, 4)]
    [InlineData((ushort)142, 2)]
    [InlineData((ushort)143, 3)]
    [InlineData((ushort)103, 5)]
    [InlineData((ushort)104, 6)]
    public void Original_spell_fractional_draws_preserve_full_native_state_and_the_next_callback(ushort effect, int action)
    {
        AssertPurePresentation(effect, action, false);
    }

    [Theory]
    [InlineData((ushort)116, 0)]
    [InlineData((ushort)185, 1)]
    [InlineData((ushort)201, 2)]
    [InlineData((ushort)95, 3)]
    [InlineData((ushort)205, 4)]
    [InlineData((ushort)325, 5)]
    [InlineData((ushort)278, 6)]
    [InlineData((ushort)6, 7)]
    [InlineData((ushort)291, 8)]
    [InlineData((ushort)203, 9)]
    [InlineData((ushort)204, 10)]
    [InlineData((ushort)191, 11)]
    [InlineData((ushort)202, 12)]
    [InlineData((ushort)199, 13)]
    [InlineData((ushort)90, 14)]
    [InlineData((ushort)206, 15)]
    public void Original_guardian_fractional_draws_preserve_full_native_state_and_the_next_callback(ushort effect, int guardian)
    {
        AssertPurePresentation(effect, guardian, true);
    }

    private static void AssertPurePresentation(ushort effect, int action, bool guardian)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle resources are required.");
        using BattleAssets assets = new(directory);
        // Initialize a fresh diagnostic state only; no field script, intro, travel or gameplay replay is executed.
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        DiagnosticBattleDriverRecipe driver = new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: guardian && action is 6 or 9 ? "party-0" : "enemy-6", Ability: guardian ? BattleAbility.GuardianForce : BattleAbility.Magic, ActionId: action), effect)
        {
            ExpectedNativeAction = guardian ? 64 + action : action,
            ExpectedNativeCommand = guardian ? (byte)254 : (byte)2
        };
        DiagnosticCharacterPreparation character = guardian
            ? new(0, 0, [], [action]) { Commands = [new(0, 21)], HitPoints = 486 }
            : new(0, 0, [new(action, 2)], [0]) { MagicCommand = new(0, 20), HitPoints = 486 };
        DiagnosticBattleRequest request = new(982, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(guardian ? action : 0, true)], [character], true),
            Target = new("enemy-6", 142, 126, 6, 3, 195, 1, 355, 76),
            Driver = driver,
            PrepareEntryBeforePresentation = true
        };
        request.Validate();
        BattleSession battle = world.StartDiagnosticBattle(request);
        world.PrepareDiagnosticEntryBeforePresentation(request);
        Assert.True(RecreateGame.DiagnosticCommandReady(battle, driver));
        world.ApplyBattleInput(driver.Input);

        Capture triangles = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = triangles, PilotDiagnosticsEnabled = true };
        (NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later) = FindEndpoints(battle, renderer, effect);
        if (effect == 3)
        {
            Assert.All(EffectGeometry(earlier, effect), geometry => Assert.False(geometry.Continuous));
        }
        else
        {
            Assert.Contains(EffectGeometry(earlier, effect), geometry => geometry.Continuous);
        }
        var livePools = battle.NativeGraphics?.OriginalEffect?.Pools.Select(pool => new
        {
            pool.DescriptorAddress,
            pool.BankAddress,
            pool.Stride,
            pool.RequireOwner().IsArenaBound,
            pool.RequireOwner().StorageOffset,
            IsBoundToEffectArena = battle.NativeEffectMemory?.Arena is { } arena && pool.RequireOwner().IsBoundToStorage(arena, 0x20DFAB8)
        }).ToArray();
        BattleSnapshot frozen = battle.Capture();
        string frozenJson = JsonSerializer.Serialize(frozen);
        BattleSession control = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozenJson)!);
        string restoredJson = JsonSerializer.Serialize(control.Capture());
        if (restoredJson != frozenJson && Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_ARTIFACTS") is string artifacts)
        {
            string directoryName = Path.Combine(artifacts, $"effect-{effect}-restore-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryName);
            File.WriteAllText(Path.Combine(directoryName, "before.battle.json"), frozenJson);
            File.WriteAllText(Path.Combine(directoryName, "restored.battle.json"), restoredJson);
            File.WriteAllText(Path.Combine(directoryName, "manifest.json"), JsonSerializer.Serialize(new { Effect = effect, Action = action, Guardian = guardian, Phase = "restore-roundtrip-before-render", LivePools = livePools, RuntimeSha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(BattleSession).Assembly.Location))) }));
        }
        Assert.Equal(frozenJson, restoredJson);
        object? memoryOwner = battle.NativeEffectMemory;
        object? graphicsOwner = battle.NativeGraphics;
        object? cameraOwner = battle.NativeCamera;
        object? ioOwner = battle.NativeFileIo;
        NativeBattleActionPacketsState packetOwner = battle.NativeActionPackets;
        NativeBattlePreparedPresentation? preparedOwner = battle.PreparedPresentation;
        long? nativeCallbacks = battle.NativeHostCallbacks;
        NativeBattlePilotVisualGeometry[] earlierGeometry = EffectGeometry(earlier, effect);
        NativeBattlePilotVisualGeometry[] laterGeometry = EffectGeometry(later, effect);
        List<BattleEvent> sampledEvents = [];
        List<BattleEvent> controlEvents = [];
        battle.DiagnosticEventPublished += sampledEvents.Add;
        control.DiagnosticEventPublished += controlEvents.Add;
        NativeBattlePrimitiveCapabilities capabilities = frozen.Definition.NativeGraphics!.PrimitiveCapabilities ?? throw new InvalidOperationException("The original fixture must select its native primitive device profile.");
        Func<int, int, bool, Albeoris.Games.FF8.Imaging.PixelImage> icons = assets.ReadNativePcUiIcons().Resolve;
        BattleScenePresentation controlScene = new(assets, frozen.Definition, true);
        controlScene.Render(control, 320, 216, new Capture());
        Assert.Equal(frozenJson, JsonSerializer.Serialize(control.Capture()));

        renderer.RenderNativePrimitiveFrame(earlier.Primitives, earlier.Textures, capabilities: capabilities, textureWindowCache: earlier.TextureWindows, uiFrame: earlier.UiFrame, uiTexture: icons);
        TriangleVertex[] nativeEarlier = triangles.Vertices.ToArray();
        renderer.RenderNativePrimitiveFrame(later.Primitives, later.Textures, capabilities: capabilities, textureWindowCache: later.TextureWindows, uiFrame: later.UiFrame, uiTexture: icons);
        TriangleVertex[] nativeLater = triangles.Vertices.ToArray();
        NativeBattlePilotSubmission quarter = default;
        NativeBattlePilotSubmission threeQuarter = default;
        for (int draw = 0; draw < 120; draw++)
        {
            float alpha = draw % 5 / 4f;
            renderer.RenderNativePilotPrimitiveFrame(earlier, later, alpha, capabilities: capabilities, uiTexture: icons);
            Assert.Equal(0, renderer.LastPilotFallbackCount);
            if (alpha == 0)
            {
                Assert.Equal(nativeEarlier, triangles.Vertices);
            }
            else if (alpha == 1)
            {
                Assert.Equal(nativeLater, triangles.Vertices);
            }
            else if (alpha == 0.25f)
            {
                quarter = renderer.PilotSubmission;
            }
            else if (alpha == 0.75f)
            {
                threeQuarter = renderer.PilotSubmission;
            }
        }
        Assert.True(quarter.Corners > 0);
        Assert.Equal(quarter.MaterialSignature, threeQuarter.MaterialSignature);
        if (effect == 3)
        {
            Assert.Equal(quarter.WorldSignature, threeQuarter.WorldSignature);
            Assert.Equal(0, quarter.CoveredContinuousCorners);
            Assert.True(quarter.DiscreteCorners > 0);
        }
        else
        {
            Assert.True(quarter.CoveredContinuousCorners > 0);
            Assert.NotEqual(quarter.WorldSignature, threeQuarter.WorldSignature);
        }
        Assert.Same(earlier.PilotProjection, NativeBattlePilotVisualGeometry.SampleProjection(earlier.PilotProjection!, later.PilotProjection!, earlier.PilotCamera!, later.PilotCamera!, 0));
        Assert.Same(later.PilotProjection, NativeBattlePilotVisualGeometry.SampleProjection(earlier.PilotProjection!, later.PilotProjection!, earlier.PilotCamera!, later.PilotCamera!, 1));
        NativeBattleCameraProjection quarterCamera = NativeBattlePilotVisualGeometry.SampleProjection(earlier.PilotProjection!, later.PilotProjection!, earlier.PilotCamera!, later.PilotCamera!, 0.25f);
        NativeBattleCameraProjection threeQuarterCamera = NativeBattlePilotVisualGeometry.SampleProjection(earlier.PilotProjection!, later.PilotProjection!, earlier.PilotCamera!, later.PilotCamera!, 0.75f);
        if (effect == 3 && earlierGeometry.Any(geometry => NativeBattlePilotVisualGeometry.Project(geometry.A, quarterCamera) != NativeBattlePilotVisualGeometry.Project(geometry.A, threeQuarterCamera)))
        {
            Assert.NotEqual(quarter.ProjectedSignature, threeQuarter.ProjectedSignature);
        }
        Assert.Equal(earlierGeometry, EffectGeometry(earlier, effect));
        Assert.Equal(laterGeometry, EffectGeometry(later, effect));
        Assert.Same(memoryOwner, battle.NativeEffectMemory);
        Assert.Same(graphicsOwner, battle.NativeGraphics);
        Assert.Same(cameraOwner, battle.NativeCamera);
        Assert.Same(ioOwner, battle.NativeFileIo);
        Assert.Same(packetOwner, battle.NativeActionPackets);
        Assert.Same(preparedOwner, battle.PreparedPresentation);
        Assert.Equal(nativeCallbacks, battle.NativeHostCallbacks);
        Assert.Empty(sampledEvents);
        Assert.Empty(controlEvents);
        BattleSnapshot afterDraws = battle.Capture();
        Assert.Equal(frozen.Random, afterDraws.Random);
        Assert.Equal(frozen.NativeGlobalRandom, afterDraws.NativeGlobalRandom);
        Assert.Equal(frozen.NativeCrtRandom, afterDraws.NativeCrtRandom);
        Assert.Equal(frozenJson, JsonSerializer.Serialize(afterDraws));
        Assert.Equal(frozenJson, JsonSerializer.Serialize(control.Capture()));

        battle.Tick();
        control.Tick();
        Assert.Equal(nativeCallbacks + 1, battle.NativeHostCallbacks);
        Assert.Equal(battle.NativeHostCallbacks, control.NativeHostCallbacks);
        Assert.Equal(JsonSerializer.Serialize(controlEvents), JsonSerializer.Serialize(sampledEvents));
        Assert.Equal(JsonSerializer.Serialize(control.Capture()), JsonSerializer.Serialize(battle.Capture()));
    }

    private static (NativeBattlePreparedPresentation Earlier, NativeBattlePreparedPresentation Later) FindEndpoints(BattleSession battle, BattleRenderer renderer, ushort effect)
    {
        NativeBattlePreparedPresentation? earlier = null;
        int compatibleIntervals = 0;
        int maximumMatches = 0;
        Dictionary<string, int> uncovered = [];
        HashSet<string> capturedUncovered = [];
        for (int callback = 0; callback < 1024; callback++)
        {
            battle.Tick();
            NativeBattlePreparedPresentation? later = battle.PreparedPresentation;
            if (later?.PilotEffect != effect)
            {
                earlier = null;
                continue;
            }
            if (earlier is not null && later.Callback == earlier.Callback + 1 && later.PilotGeneration == earlier.PilotGeneration && earlier.PilotCamera is not null && later.PilotCamera is not null && earlier.PilotProjection is not null && later.PilotProjection is not null && renderer.CanSampleNativePilotInterval(earlier, later))
            {
                NativeBattlePilotVisualGeometry[] before = EffectGeometry(earlier, effect);
                NativeBattlePilotVisualGeometry[] after = EffectGeometry(later, effect);
                compatibleIntervals++;
                NativeBattlePilotVisualGeometry[] matching = before.Where(a => after.Any(b => a.Identity == b.Identity && a.CornerCount == b.CornerCount)).ToArray();
                maximumMatches = Math.Max(maximumMatches, matching.Length);
                bool moving = effect == 3 || matching.Any(a => a.Continuous && after.Any(b => a.Identity == b.Identity && Enumerable.Range(0, a.CornerCount).Any(corner => NativeBattlePilotVisualGeometry.SampleCorner(a, b, corner, 0.25f) != NativeBattlePilotVisualGeometry.SampleCorner(a, b, corner, 0.75f))));
                bool visibleArea = matching.Any(a => Vector3.Cross(a.B - a.A, a.C - a.A).LengthSquared() > 0.0001f || !a.ScreenOffsets.IsDefaultOrEmpty);
                if (matching.Length > 0 && moving && visibleArea)
                {
                    return (earlier, later);
                }
            }
            else if (renderer.LastPilotFallbackCount > 0)
            {
                bool hasEffectGeometry = EffectGeometry(later, effect).Length > 0;
                string phase = hasEffectGeometry ? "geometry" : battle.NativeGraphics?.OriginalEffect?.Pools.Any(pool => pool.Head.HasValue) == true ? "live" : "loading";
                NativeBattleLogicalResolvedCommand[] missingCommands = later.Primitives.LastCommands.Where(value => value.Pass > 0 && value.Command.PilotGeometry is null && (value.Command.Opcode & 0xE0) == 0x20).Take(12).ToArray();
                if (missingCommands.Length > 0 && Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_ARTIFACTS") is string artifacts && capturedUncovered.Add(phase))
                {
                    string directoryName = Path.Combine(artifacts, $"effect-{effect}-uncovered-{later.Callback}-{Guid.NewGuid():N}");
                    Directory.CreateDirectory(directoryName);
                    File.WriteAllText(Path.Combine(directoryName, "source.battle.json"), JsonSerializer.Serialize(battle.Capture()));
                    File.WriteAllText(Path.Combine(directoryName, "commands.json"), JsonSerializer.Serialize(new { Effect = effect, later.Callback, Phase = phase, HasEffectGeometry = hasEffectGeometry, Commands = missingCommands.Select(value => new { value.Pass, value.Command.Packet, value.Command.Opcode, value.Command.OrderingBucket, value.Command.ModuleDraw, value.Command.TextureSourceOwner, value.Command.Words, value.Command.Flags20, value.Command.VertexPayload4 }) }));
                }
                foreach (NativeBattleLogicalResolvedCommand missing in later.Primitives.LastCommands.Where(value => value.Pass > 0 && value.Command.PilotGeometry is null && (value.Command.Opcode & 0xE0) == 0x20).DistinctBy(value => (value.Command.Opcode, value.Command.Packet.Bank)).Take(12))
                {
                    string key = $"{missing.Command.Packet.Bank}/{missing.Command.Opcode:X2}";
                    uncovered[key] = uncovered.GetValueOrDefault(key) + 1;
                }
            }
            if (earlier?.Callback != later.Callback)
            {
                earlier = later;
            }
        }
        throw new InvalidOperationException($"The original effect{effect} did not publish a compatible visible consecutive endpoint pair within its bounded callback budget. Compatible intervals:{compatibleIntervals}; maximum matching primitives:{maximumMatches}; uncovered:{string.Join(",", uncovered.Select(value => $"{value.Key}:{value.Value}"))}.");
    }

    private static NativeBattlePilotVisualGeometry[] EffectGeometry(NativeBattlePreparedPresentation frame, ushort effect) => frame.Primitives.LastCommands.Select(command => command.Command.PilotGeometry).OfType<NativeBattlePilotVisualGeometry>().Where(geometry => geometry.Identity.Effect == effect).ToArray();

    private sealed class Capture : ITriangleRenderer, ILineRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = [];

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

        public void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
        }
    }
}

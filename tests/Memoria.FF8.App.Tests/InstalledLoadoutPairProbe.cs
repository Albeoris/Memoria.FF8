using Vector3 = System.Numerics.Vector3;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

/// <summary>Opt-in bounded rendering of retained adjacent native frames without executing another battle callback.</summary>
internal static class InstalledLoadoutPairProbe
{
    internal static void Capture(BattleAssets assets, BattleSession battle, NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_PAIR_PROBE");
        if (string.IsNullOrWhiteSpace(output) || File.Exists(Path.Combine(output, "pair.json")))
        {
            return;
        }
        Assert.Equal(earlier.Callback + 1, later.Callback);
        Directory.CreateDirectory(output);
        long callback = battle.Presentation!.Graphics.Callback;
        string before = JsonSerializer.Serialize(battle.Capture());
        using PairGame game = new(assets, battle.Capture().Definition, earlier, later, output);
        game.Run();
        Assert.True(game.Completed);
        Assert.Equal(callback, battle.Presentation.Graphics.Callback);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private sealed class PairGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private readonly WindowModeController window;
        private readonly BattleAssets assets;
        private readonly BattleScenePresentation scene;
        private readonly NativeBattlePreparedPresentation earlier;
        private readonly NativeBattlePreparedPresentation later;
        private readonly string output;

        internal PairGame(BattleAssets assets, BattleDefinition definition, NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later, string output)
        {
            this.assets = assets;
            this.earlier = earlier;
            this.later = later;
            this.output = output;
            scene = new(assets, definition);
            graphics = new(this) { PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 720, GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
            window = WindowModeController.PrepareTransferChild(Window, graphics);
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            Window.Title = "Memoria retained native frame pair diagnostic";
        }

        internal bool Completed { get; private set; }

        protected override void Draw(GameTime gameTime)
        {
            if (Completed)
            {
                return;
            }
            using GpuTriangleRenderer gpu = new(GraphicsDevice);
            BattleRenderer renderer = new(960, 720) { TriangleRenderer = gpu, PilotDiagnosticsEnabled = true };
            Assert.True(renderer.CanSampleNativePilotInterval(earlier, later));
            List<object> renders = [];
            Dictionary<string, string> pixels = [];
            Render("01-earlier", earlier, null);
            Render("02-current", later, null);
            Render("03-pair-alpha0", earlier, 0);
            Render("04-pair-alpha05", earlier, 0.5f);
            Render("05-pair-alpha1", later, 1);
            Assert.Equal(pixels["01-earlier"], pixels["03-pair-alpha0"]);
            Assert.Equal(pixels["02-current"], pixels["05-pair-alpha1"]);
            File.WriteAllText(Path.Combine(output, "pair.json"), JsonSerializer.Serialize(new { EarlierCallback = earlier.Callback, CurrentCallback = later.Callback, EarlierFrame = earlier.Frame, CurrentFrame = later.Frame, Earlier = Inspect(earlier), Current = Inspect(later), Matches = Match(earlier, later), Renders = renders }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            Completed = true;
            GC.KeepAlive(window);
            Exit();

            void Render(string name, NativeBattlePreparedPresentation frame, float? alpha)
            {
                IReadOnlyList<NativeBattleTextureReplacement> replacements = (IReadOnlyList<NativeBattleTextureReplacement>)typeof(BattleScenePresentation).GetMethod("NativeModelReplacements", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [alpha is null ? frame : earlier])!;
                renderer.ObservePilotSource(alpha is null ? frame : earlier, alpha is null ? null : later, alpha ?? 0, alpha is not null);
                if (alpha is float fraction)
                {
                    renderer.RenderNativePilotPrimitiveFrame(earlier, later, fraction, capabilities: earlier.PrimitiveCapabilities, replacements: replacements, uiTexture: earlier.UiFrame is null ? null : assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource);
                }
                else
                {
                    renderer.RenderNativePrimitiveFrame(frame.Primitives, frame.Textures, capabilities: frame.PrimitiveCapabilities, textureWindowCache: frame.TextureWindows, replacements: replacements, uiFrame: frame.UiFrame, uiTexture: frame.UiFrame is null ? null : assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource);
                }
                using FileStream png = File.Create(Path.Combine(output, name + ".png"));
                gpu.Target.SaveAsPng(png, 960, 720);
                string hash = Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(GpuTestDevice.Read(gpu.Target).AsSpan())));
                pixels.Add(name, hash);
                renders.Add(new { Name = name, Alpha = alpha, PixelHash = hash, renderer.PilotSubmission, renderer.LastPilotFallbackCount, renderer.LastPilotFallbackReason });
            }
        }
    }

    private static List<NativeBattleLogicalCommandReference> Commands(NativeBattlePreparedPresentation frame)
    {
        List<NativeBattleLogicalCommandReference> commands = [];
        foreach (ref readonly NativeBattleLogicalResolvedCommand resolved in frame.Primitives.LastCommands)
        {
            if (resolved.Pass != 0 && !resolved.Command.Pilot.IsEmpty)
            {
                commands.Add(resolved.Command);
            }
        }
        return commands;
    }

    private static object Inspect(NativeBattlePreparedPresentation frame)
    {
        List<NativeBattleLogicalCommandReference> commands = Commands(frame);
        var groups = commands.GroupBy(command => command.Pilot.Identity).ToArray();
        return new
        {
            frame.SceneMotion,
            frame.PilotEffect,
            frame.PilotGeneration,
            frame.CanInterpolateMotion,
            Commands = commands.Count,
            ModelCommands = commands.Count(command => command.PresentationActorId is not null),
            DuplicateIdentityGroups = groups.Count(group => group.Count() > 1),
            UnequalDuplicateGroups = groups.Count(group => group.Select(GeometrySignature).Distinct().Skip(1).Any()),
            ActorGeometry = commands.GroupBy(command => command.PresentationActorId ?? "effect").Select(group => new { Owner = group.Key, Count = group.Count(), Signature = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("|", group.Select(GeometrySignature))))) }).ToArray()
        };
    }

    private static string GeometrySignature(NativeBattleLogicalCommandReference command)
    {
        NativeBattlePilotGeometryReference geometry = command.Pilot;
        return JsonSerializer.Serialize(new { geometry.CornerCount, Corners = Enumerable.Range(0, geometry.CornerCount).Select(geometry.Corner).ToArray() }, new JsonSerializerOptions { IncludeFields = true });
    }

    private static object Match(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later)
    {
        var first = Commands(earlier);
        var last = Commands(later).GroupBy(command => command.Pilot.Identity).ToDictionary(group => group.Key, group => group.Last());
        int matched = 0;
        int unmatched = 0;
        int cornerMismatch = 0;
        float maximum = 0;
        string? maximumOwner = null;
        NativeBattlePilotVisualIdentity maximumIdentity = default;
        foreach (NativeBattleLogicalCommandReference command in first)
        {
            if (!last.TryGetValue(command.Pilot.Identity, out NativeBattleLogicalCommandReference next))
            {
                unmatched++;
                continue;
            }
            matched++;
            if (command.Pilot.CornerCount != next.Pilot.CornerCount)
            {
                cornerMismatch++;
                continue;
            }
            for (int corner = 0; corner < command.Pilot.CornerCount; corner++)
            {
                float distance = Vector3.Distance(command.Pilot.Corner(corner), next.Pilot.Corner(corner));
                if (distance > maximum)
                {
                    maximum = distance;
                    maximumOwner = command.PresentationActorId;
                    maximumIdentity = command.Pilot.Identity;
                }
            }
        }
        return new { Matched = matched, Unmatched = unmatched, CornerCountMismatches = cornerMismatch, MaximumPairedCornerDisplacement = maximum, MaximumOwner = maximumOwner, MaximumIdentity = maximumIdentity, SharedVertices = SharedVertexSplits(first, last) };
    }

    private sealed class SharedVertexUse
    {
        internal bool Matched;
        internal bool Unmatched;
        internal float MaximumDestinationDisplacement;
    }

    private static object[] SharedVertexSplits(List<NativeBattleLogicalCommandReference> earlier, Dictionary<NativeBattlePilotVisualIdentity, NativeBattleLogicalCommandReference> later)
    {
        Dictionary<(string Owner, Vector3 Position), SharedVertexUse> vertices = [];
        Dictionary<string, (int Matched, int Unmatched)> faces = [];
        foreach (NativeBattleLogicalCommandReference command in earlier)
        {
            if (command.PresentationActorId is not { } owner)
            {
                continue;
            }
            bool matched = later.TryGetValue(command.Pilot.Identity, out NativeBattleLogicalCommandReference next) && command.Pilot.Continuous && next.Pilot.Continuous && command.Pilot.CornerCount == next.Pilot.CornerCount;
            (int Matched, int Unmatched) counts = faces.GetValueOrDefault(owner);
            faces[owner] = matched ? (counts.Matched + 1, counts.Unmatched) : (counts.Matched, counts.Unmatched + 1);
            for (int corner = 0; corner < command.Pilot.CornerCount; corner++)
            {
                Vector3 point = command.Pilot.Corner(corner);
                var key = (owner, point);
                if (!vertices.TryGetValue(key, out SharedVertexUse? use))
                {
                    use = new();
                    vertices.Add(key, use);
                }
                if (matched)
                {
                    use.Matched = true;
                    use.MaximumDestinationDisplacement = Math.Max(use.MaximumDestinationDisplacement, Vector3.Distance(point, next.Pilot.Corner(corner)));
                }
                else
                {
                    use.Unmatched = true;
                }
            }
        }
        return faces.Select(pair =>
        {
            SharedVertexUse[] shared = vertices.Where(vertex => vertex.Key.Owner == pair.Key && vertex.Value.Matched && vertex.Value.Unmatched).Select(vertex => vertex.Value).ToArray();
            float maximum = shared.Length == 0 ? 0 : shared.Max(vertex => vertex.MaximumDestinationDisplacement);
            return (object)new { Owner = pair.Key, MatchedFaces = pair.Value.Matched, UnmatchedFaces = pair.Value.Unmatched, SharedMatchedUnmatchedVertices = shared.Length, MovingSharedVertices = shared.Count(vertex => vertex.MaximumDestinationDisplacement > 0), MaximumDestinationDisplacement = maximum, MaximumHalfIntervalSeparation = maximum * 0.5f };
        }).ToArray();
    }

}

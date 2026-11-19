using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

/// <summary>Opt-in original native-frame material ownership; diagnostic omissions never alter a live battle.</summary>
internal static class InstalledGarmentSurfaceProbe
{
    internal static void CaptureIfRequested(GameWorld world, string actorId, int recipient, int donor)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_PROBE");
        if (string.IsNullOrWhiteSpace(directory) || recipient != 6 || donor is not (7 or 10))
        {
            return;
        }
        BattleSession battle = world.Battle!;
        if (battle.Presentation is not { EntryReady: true } presentation || battle.PreparedPresentation is not { } frame)
        {
            return;
        }
        NativeBattleGraphicsModel model = presentation.Graphics.ActorModels.Get(battle.ReadPresentationModelOwner(actorId));
        NativeBattleSequenceState sequence = model.SequenceState!;
        int pose = donor == 7 ? 0 : 28;
        int first = donor == 7 ? 15 : 6;
        if (sequence.Pose != pose || sequence.PoseFrame114 is not byte selectedFrame || selectedFrame < first || selectedFrame > first + 1)
        {
            return;
        }
        string output = Path.Combine(directory, $"recipient-{recipient:D2}-style-{donor:D2}-selected-{pose:D3}-{selectedFrame:D3}");
        if (File.Exists(Path.Combine(output, "surface.json")))
        {
            return;
        }
        Assert.False(Directory.Exists(output), "Use a distinct surface-probe directory after an interrupted capture.");
        Directory.CreateDirectory(output);
        BattleCombatantDefinition definition = battle.GetCombatantDefinition(actorId);
        Assert.Equal(donor, definition.WeaponProfile!.ResourceOwner);
        Assert.Equal(frame.Callback, presentation.Graphics.Callback);
        BattleModel body = world.BattleResources.LoadPlayer(recipient, definition.Costume);
        var joints = BattleHumanoidRigProfiles.SemanticPairs(recipient, body, recipient, body);
        HashSet<int> lowerBones = joints.Where(joint => joint.Joint is "pelvis" or "hip-a" or "thigh-a" or "shin-a" or "foot-a" or "hip-b" or "thigh-b" or "shin-b" or "foot-b").Select(joint => joint.Recipient).ToHashSet();
        BattleMesh mesh = body.Meshes[0];
        HashSet<int> omitted = Enumerable.Range(0, mesh.Faces.Count).Where(index => mesh.Faces[index].Vertices.All(vertex => lowerBones.Contains(mesh.Vertices[vertex].Bone))).ToHashSet();
        bool Omit(NativeBattleLogicalCommandReference command) => command.PresentationActorId == actorId && !command.Pilot.IsEmpty && command.Pilot.Identity.Bank == 0x502D40 && omitted.Contains(command.Pilot.Identity.Primitive);
        NativeBattleLogicalResolvedCommand[] kept = frame.Primitives.LastCommands.Where(command => !Omit(command.Command)).ToArray();
        Assert.True(kept.Length < frame.Primitives.LastCommands.Count);
        NativeBattlePreparedPresentation diagnostic = frame with { Primitives = frame.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(kept) } };
        string before = JsonSerializer.Serialize(battle.Capture());
        using SurfaceGame game = new(world.BattleResources, battle.Capture().Definition, frame, diagnostic, actorId, mesh, omitted, output, donor, new { SelectedPose = sequence.Pose, SelectedFrame = sequence.PoseFrame114, sequence.PoseLimit115, DecodedPoseHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(sequence.DecodedPose))), frame.Callback, frame.Frame, frame.Camera, model.GeometryWorldMatrix });
        game.Run();
        Assert.True(game.Completed);
        Assert.Equal(frame.Callback, presentation.Graphics.Callback);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private sealed class SurfaceGame : Game
    {
        private const int Width = 3440;
        private const int Height = 1334;
        private readonly GraphicsDeviceManager graphics;
        private readonly WindowModeController window;
        private readonly BattleAssets assets;
        private readonly BattleScenePresentation scene;
        private readonly NativeBattlePreparedPresentation original;
        private readonly NativeBattlePreparedPresentation diagnostic;
        private readonly string actorId;
        private readonly BattleMesh mesh;
        private readonly HashSet<int> omitted;
        private readonly string output;
        private readonly object provenance;
        private readonly PixelBounds patch;

        internal SurfaceGame(BattleAssets assets, BattleDefinition definition, NativeBattlePreparedPresentation original, NativeBattlePreparedPresentation diagnostic, string actorId, BattleMesh mesh, HashSet<int> omitted, string output, int donor, object provenance)
        {
            this.assets = assets;
            this.original = original;
            this.diagnostic = diagnostic;
            this.actorId = actorId;
            this.mesh = mesh;
            this.omitted = omitted;
            this.output = output;
            this.provenance = provenance;
            patch = donor == 7 ? new(1600, 590, 125, 205) : new(1530, 840, 210, 180);
            scene = new(assets, definition);
            graphics = new(this) { PreferredBackBufferWidth = Width, PreferredBackBufferHeight = Height, GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
            window = WindowModeController.PrepareTransferChild(Window, graphics);
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            Window.Title = "Memoria original native surface ownership diagnostic";
        }

        internal bool Completed { get; private set; }

        protected override void Draw(GameTime gameTime)
        {
            if (Completed)
            {
                return;
            }
            using GpuTriangleRenderer gpu = new(GraphicsDevice);
            PixelBounds projection = ViewportLayout.Fit(320, 240, Width, Height);
            BattleRenderer renderer = new(Width, Height, projection) { TriangleRenderer = gpu, PilotDiagnosticsEnabled = true };
            var replacements = (IReadOnlyList<NativeBattleTextureReplacement>)typeof(BattleScenePresentation).GetMethod("NativeModelReplacements", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [original])!;
            Microsoft.Xna.Framework.Color[] first = Render(original, "01-original-native.png");
            object originalSubmission = renderer.PilotSubmission;
            Microsoft.Xna.Framework.Color[] second = Render(diagnostic, "02-DIAGNOSTIC-no-pure-lowerbody.png");
            int changed = 0;
            for (int y = patch.Y; y < patch.Y + patch.Height; y++)
            {
                for (int x = patch.X; x < patch.X + patch.Width; x++)
                {
                    if (first[y * Width + x] != second[y * Width + x])
                    {
                        changed++;
                    }
                }
            }
            File.WriteAllText(Path.Combine(output, "surface.json"), JsonSerializer.Serialize(new { Provenance = provenance, Width, Height, Projection = projection, Patch = patch, ChangedPatchPixels = changed, OriginalPixelHash = Hash(first), DiagnosticPixelHash = Hash(second), OriginalSubmission = originalSubmission, DiagnosticSubmission = renderer.PilotSubmission, original.Primitives.LastDepthState, OriginalCommands = original.Primitives.LastCommands.Count, DiagnosticCommands = diagnostic.Primitives.LastCommands.Count, OmittedAuthoredFaceIds = omitted.Order().ToArray(), PatchCommands = InspectPatch(original, projection), Qualification = "Original retained prepared frame uses actual renderer/material/depth/ordering. Omitted-lowerbody image is a diagnostic comparison only, never actual acceptance. Selected phase may lead decoded presentation by one frame; actual callback and decoded hash are recorded." }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            Completed = true;
            GC.KeepAlive(window);
            Exit();

            Microsoft.Xna.Framework.Color[] Render(NativeBattlePreparedPresentation frame, string name)
            {
                renderer.ObservePilotSource(frame, null, 0, false);
                renderer.RenderNativePrimitiveFrame(frame.Primitives, frame.Textures, capabilities: frame.PrimitiveCapabilities, textureWindowCache: frame.TextureWindows, replacements: replacements, uiFrame: frame.UiFrame, uiTexture: frame.UiFrame is null ? null : assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource);
                using FileStream png = File.Create(Path.Combine(output, name));
                gpu.Target.SaveAsPng(png, Width, Height);
                return GpuTestDevice.Read(gpu.Target);
            }
        }

        private object[] InspectPatch(NativeBattlePreparedPresentation frame, PixelBounds projection)
        {
            Type projectorType = typeof(BattleRenderer).GetNestedType("NativeBattlePrimitiveViewportProjector", BindingFlags.NonPublic)!;
            ConstructorInfo constructor = projectorType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            MethodInfo project = projectorType.GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic)!;
            List<object> result = [];
            foreach (ref readonly NativeBattleLogicalResolvedCommand resolved in frame.Primitives.LastCommands)
            {
                NativeBattleLogicalCommandReference command = resolved.Command;
                if (command.PresentationActorId != actorId || command.Pilot.IsEmpty || command.Pilot.Identity.Bank != 0x502D40 || command.Pilot.Identity.Primitive is < 0 || command.Pilot.Identity.Primitive >= mesh.Faces.Count)
                {
                    continue;
                }
                int count = command.Pilot.CornerCount;
                if ((command.Opcode & 0xF4) != 0x24)
                {
                    continue;
                }
                object projector = constructor.Invoke([projection, (uint)command.Flags20, resolved.DrawMode]);
                System.Numerics.Vector2[] positions = new System.Numerics.Vector2[count];
                for (int corner = 0; corner < count; corner++)
                {
                    int word = 1 + corner * 2;
                    uint packed = command.WordSpan[word];
                    BattleSceneScreenPoint point = command.HasProjectedCoordinates ? command.ProjectedCoordinateSpan[corner] : new(unchecked((short)packed), unchecked((short)(packed >> 16)));
                    positions[corner] = (System.Numerics.Vector2)project.Invoke(projector, [point.X, point.Y])!;
                }
                if (positions.Max(point => point.X) < patch.X || positions.Min(point => point.X) > patch.X + patch.Width || positions.Max(point => point.Y) < patch.Y || positions.Min(point => point.Y) > patch.Y + patch.Height)
                {
                    continue;
                }
                int faceIndex = command.Pilot.Identity.Primitive;
                BattleFace face = mesh.Faces[faceIndex];
                result.Add(new { Face = faceIndex, Bones = face.Vertices.Select(index => mesh.Vertices[index].Bone).ToArray(), face.Texture, face.Uv, face.NativeMaterial, Omitted = omitted.Contains(faceIndex), command.Pilot.Identity, command.Opcode, command.OrderingBucket, Depths = command.VertexPayloadSpan.ToArray(), Screen = positions, command.ModelTextureOwner, resolved.DrawMode.TexturePage });
            }
            return result.ToArray();
        }

        private static string Hash(Microsoft.Xna.Framework.Color[] pixels) => Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())));
    }
}

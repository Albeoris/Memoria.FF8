using System.Reflection;
using System.Collections.Immutable;
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
        bool fitRequested = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_FIT") == "1";
        string? auxiliaryState = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_AUXILIARY_STATE");
        if (battle.Presentation is not { EntryReady: true } presentation || battle.PreparedPresentation is not { } frame)
        {
            return;
        }
        BattlePartyActorModel? owner = presentation.Graphics.ActorModels.Owners.OfType<BattlePartyActorModel>().SingleOrDefault(item => item.Actor.ActorId == actorId);
        if (owner is null || presentation.Graphics.ActorModels.Get(owner).SequenceState is not { } sequence)
        {
            return;
        }
        NativeBattleGraphicsModel model = presentation.Graphics.ActorModels.Get(owner);
        bool returnRequested = donor == 7 && Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_RETURN") == "1";
        Assert.False(returnRequested && fitRequested, "The return ownership diagnostic does not import auxiliary states.");
        var requested = RequestedPhase(donor);
        int pose = requested.Pose;
        int first = requested.Frame - 1;
        if (fitRequested && !string.IsNullOrWhiteSpace(auxiliaryState))
        {
            using JsonDocument input = JsonDocument.Parse(File.ReadAllText(auxiliaryState));
            JsonElement source = input.RootElement.GetProperty("Source");
            Assert.Equal(donor, source.GetProperty("Owner").GetInt32());
            Assert.Equal(pose, source.GetProperty("Animation").GetInt32());
            int target = source.GetProperty("Frame").GetInt32();
            Assert.InRange(target, 1, byte.MaxValue);
            first = target - 1;
        }
        if (sequence.Pose != pose || sequence.PoseFrame114 is not byte selectedFrame || selectedFrame < first || selectedFrame > first + 1)
        {
            return;
        }
        if (fitRequested && selectedFrame != first + 1)
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
        object compiledPoseCorrelation = CompareCompiledPose(world.BattleResources, definition, body, model, sequence, donor, joints.Select(joint => joint.Recipient).ToHashSet());
        HashSet<int> lowerBones = joints.Where(joint => joint.Joint is "pelvis" or "hip-a" or "thigh-a" or "shin-a" or "foot-a" or "hip-b" or "thigh-b" or "shin-b" or "foot-b").Select(joint => joint.Recipient).ToHashSet();
        BattleMesh mesh = body.Meshes[0];
        HashSet<int> omitted = Enumerable.Range(0, mesh.Faces.Count).Where(index => mesh.Faces[index].Vertices.All(vertex => lowerBones.Contains(mesh.Vertices[vertex].Bone))).ToHashSet();
        bool Omit(NativeBattleLogicalCommandReference command) => command.PresentationActorId == actorId && !command.Pilot.IsEmpty && command.Pilot.Identity.Bank == 0x502D40 && omitted.Contains(command.Pilot.Identity.Primitive);
        NativeBattleLogicalResolvedCommand[] kept = frame.Primitives.LastCommands.Where(command => !Omit(command.Command)).ToArray();
        Assert.True(kept.Length < frame.Primitives.LastCommands.Count);
        NativeBattlePreparedPresentation diagnostic = frame with { Primitives = frame.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(kept) } };
        string before = JsonSerializer.Serialize(battle.Capture());
        (NativeBattlePreparedPresentation Baseline, NativeBattlePreparedPresentation Candidate, object Facts)? fit = fitRequested ? InstalledGarmentAttachmentFitProbe.Prepare(world, actorId, body, frame) : null;
        using SurfaceGame game = new(world.BattleResources, battle.Capture().Definition, frame, diagnostic, actorId, mesh, omitted, output, donor, new { SelectedPose = sequence.Pose, SelectedFrame = sequence.PoseFrame114, sequence.PoseLimit115, DecodedPoseHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(sequence.DecodedPose))), CompiledPoseCorrelation = compiledPoseCorrelation, frame.Callback, frame.Frame, frame.Camera, model.GeometryWorldMatrix }, fit);
        game.Run();
        Assert.True(game.Completed);
        Assert.Equal(frame.Callback, presentation.Graphics.Callback);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private static object CompareCompiledPose(BattleAssets assets, BattleCombatantDefinition definition, BattleModel body, NativeBattleGraphicsModel model, NativeBattleSequenceState sequence, int donor, HashSet<int> physical)
    {
        NativeBattleDecodedPose actual = sequence.DecodedPose ?? throw new InvalidDataException("The visible diagnostic actor has no decoded native body pose.");
        NativeBattlePoseSource rig = body.NativePoseSource!;
        BattleModel compiledModel = assets.LoadPlayerForStyle(definition.ModelId, definition.Costume, donor, definition.WeaponId);
        NativeBattlePoseSource compiled = compiledModel.NativePoseSource!;
        Assert.True(rig.Skeleton.AsSpan().SequenceEqual(compiled.Skeleton.AsSpan()));
        byte animation = sequence.Pose ?? throw new InvalidDataException("The diagnostic actor has no selected native animation.");
        int frames = NativeBattlePoseDecoder.ReadFrameCounts(compiled.Animations.AsSpan())[animation];
        ImmutableArray<NativeBattleActionMatrix> actualMatrices = NativeBattlePoseMatrices.StageBoneMatrices(rig, actual);
        List<object> rows = [];
        List<int> physicalMatches = [];
        List<int> completeMatches = [];
        NativeBattleDecodedPose? firstPhysicalSample = null;
        NativeBattleAuxiliaryContactCalibration? calibration = BattleAuxiliaryContactCalibrations.Create(definition.ModelId, body);
        for (int frame = 1; frame <= frames; frame++)
        {
            NativeBattleDecodedPose sample = compiled.Sample(animation, checked((byte)frame), sequence.PoseFlags109);
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(compiled, sample);
            int[] differences = Enumerable.Range(0, matrices.Length).Where(bone => !EqualMatrix(actualMatrices[bone], matrices[bone])).ToArray();
            int[] physicalDifferences = differences.Where(physical.Contains).ToArray();
            int[] auxiliaryDifferences = differences.Where(bone => !physical.Contains(bone)).ToArray();
            if (physicalDifferences.Length == 0)
            {
                physicalMatches.Add(frame);
                firstPhysicalSample ??= sample;
            }
            if (differences.Length == 0)
            {
                completeMatches.Add(frame);
            }
            rows.Add(new { Frame = frame, PhysicalDifferences = physicalDifferences, AuxiliaryDifferences = auxiliaryDifferences, NativeRootEqual = actual.Translation == sample.Translation, actual.HasScale, SampleHasScale = sample.HasScale });
        }
        int[] publicationDifferences = model.CurrentBodyBoneMatrices is { } publication && publication.Matrices.Length == actualMatrices.Length ? Enumerable.Range(0, actualMatrices.Length).Where(bone => !EqualMatrix(actualMatrices[bone], publication.Matrices[bone])).ToArray() : [];
        return new
        {
            definition.ModelId,
            definition.Costume,
            definition.WeaponId,
            SourceOwner = donor,
            CompiledModel = compiledModel.Name,
            CompiledAnimationSha256 = Convert.ToHexString(SHA256.HashData(compiled.Animations.AsSpan())),
            ActualSelectedPose = animation,
            ActualSelectedFrame = sequence.PoseFrame114,
            sequence.PoseFlags109,
            CompiledFrameCount = frames,
            PhysicalMatchingFrames = physicalMatches,
            AllBoneMatchingFrames = completeMatches,
            PublishedMatricesAvailable = model.CurrentBodyBoneMatrices is not null,
            PublishedVsDecodedDifferentBones = publicationDifferences,
            ActualNativePose = actual,
            FirstPhysicallyMatchingCompiledPose = firstPhysicalSample,
            ActualRefinementReport = calibration is null ? null : NativeBattleAuxiliaryContactSolver.Inspect(rig, actual, calibration),
            MatchingCompiledRefinementReport = calibration is null || firstPhysicalSample is null ? null : NativeBattleAuxiliaryContactSolver.Inspect(rig, firstPhysicalSample, calibration),
            Frames = rows,
            Qualification = "Every compiled frame is compared by exact native Stage matrices; selected counter is not assumed to identify the currently decoded/rendered frame. Physical and auxiliary matches are reported separately."
        };
    }

    private static bool EqualMatrix(NativeBattleActionMatrix first, NativeBattleActionMatrix second) => first.X == second.X && first.Y == second.Y && first.Z == second.Z && first.Scale == second.Scale && first.Rotation.AsSpan().SequenceEqual(second.Rotation.AsSpan());

    internal static void AssertRequestedCaptured(int recipient, int donor)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_PROBE");
        string? auxiliaryState = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_AUXILIARY_STATE");
        if (recipient != 6 || donor is not (7 or 10) || string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_FIT") != "1")
        {
            if (donor == 7 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_POSE")))
            {
                var requested = RequestedPhase(donor);
                string nativeOutput = Path.Combine(directory, $"recipient-{recipient:D2}-style-{donor:D2}-selected-{requested.Pose:D3}-{requested.Frame:D3}");
                foreach (string name in new[] { "surface.json", "01-original-native.png", "02-DIAGNOSTIC-no-pure-lowerbody.png" })
                {
                    Assert.True(File.Exists(Path.Combine(nativeOutput, name)), $"Requested compiled-pose binding diagnostic was not captured: {name}.");
                }
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(auxiliaryState))
        {
            return;
        }
        using JsonDocument input = JsonDocument.Parse(File.ReadAllText(auxiliaryState));
        JsonElement source = input.RootElement.GetProperty("Source");
        Assert.Equal(donor, source.GetProperty("Owner").GetInt32());
        string output = Path.Combine(directory, $"recipient-{recipient:D2}-style-{donor:D2}-selected-{source.GetProperty("Animation").GetInt32():D3}-{source.GetProperty("Frame").GetInt32():D3}");
        foreach (string name in new[] { "surface.json", "attachment-fit.json", "01-original-native.png", "02-DIAGNOSTIC-no-pure-lowerbody.png", "03-DIAGNOSTIC-reemitted-decoded-baseline.png", "04-DIAGNOSTIC-warm-auxiliary-state.png" })
        {
            Assert.True(File.Exists(Path.Combine(output, name)), $"Requested native garment diagnostic was not captured: {name}.");
        }
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_CANDIDATE_OMISSION") == "1")
        {
            Assert.True(File.Exists(Path.Combine(output, "05-DIAGNOSTIC-warm-state-no-pure-lowerbody.png")), "Requested candidate ownership comparison was not captured.");
        }
    }

    internal static void WaitForRequestedIdleCapture(GameWorld world, string actorId, int recipient, int donor, Action observe)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_PROBE");
        string? auxiliaryState = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_AUXILIARY_STATE");
        if (recipient != 6 || donor is not (7 or 10) || string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        if (donor == 7 && Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_RETURN") == "1")
        {
            return;
        }
        bool fitRequested = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_FIT") == "1";
        int target;
        string marker;
        if (fitRequested)
        {
            if (string.IsNullOrWhiteSpace(auxiliaryState))
            {
                return;
            }
            using JsonDocument input = JsonDocument.Parse(File.ReadAllText(auxiliaryState));
            JsonElement source = input.RootElement.GetProperty("Source");
            Assert.Equal(donor, source.GetProperty("Owner").GetInt32());
            if (source.GetProperty("Animation").GetInt32() != 0)
            {
                return;
            }
            target = source.GetProperty("Frame").GetInt32();
            marker = "attachment-fit.json";
        }
        else
        {
            var requested = RequestedPhase(donor);
            if (requested.Pose != 0)
            {
                return;
            }
            target = requested.Frame;
            marker = "surface.json";
        }
        string output = Path.Combine(directory, $"recipient-{recipient:D2}-style-{donor:D2}-selected-000-{target:D3}");
        bool AtRequestedPhase()
        {
            BattleSession battle = world.Battle!;
            NativeBattleGraphicsModel model = battle.Presentation!.Graphics.ActorModels.Get(battle.ReadPresentationModelOwner(actorId));
            return model.SequenceState is { Pose: 0 } sequence && sequence.PoseFrame114 == target && File.Exists(Path.Combine(output, marker));
        }
        for (int callback = 0; callback < 70 && !AtRequestedPhase(); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            observe();
        }
        Assert.True(AtRequestedPhase(), "The actual native idle did not reach the requested visible diagnostic phase within70callbacks.");
        AssertRequestedCaptured(recipient, donor);
    }

    private static (int Pose, int Frame) RequestedPhase(int donor)
    {
        string? pose = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_POSE");
        string? frame = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_FRAME");
        if (!string.IsNullOrWhiteSpace(pose) || !string.IsNullOrWhiteSpace(frame))
        {
            Assert.True(int.TryParse(pose, out int poseValue), "The explicit diagnostic animation must be an integer.");
            Assert.True(int.TryParse(frame, out int frameValue), "The explicit diagnostic frame must be an integer.");
            Assert.InRange(poseValue, 0, byte.MaxValue);
            Assert.InRange(frameValue, 1, byte.MaxValue);
            return (poseValue, frameValue);
        }
        if (donor == 7 && Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_RETURN") == "1")
        {
            return (9, 6);
        }
        return donor == 7 ? (0, 16) : (28, 7);
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
        private readonly (NativeBattlePreparedPresentation Baseline, NativeBattlePreparedPresentation Candidate, object Facts)? fit;

        internal SurfaceGame(BattleAssets assets, BattleDefinition definition, NativeBattlePreparedPresentation original, NativeBattlePreparedPresentation diagnostic, string actorId, BattleMesh mesh, HashSet<int> omitted, string output, int donor, object provenance, (NativeBattlePreparedPresentation Baseline, NativeBattlePreparedPresentation Candidate, object Facts)? fit)
        {
            this.assets = assets;
            this.original = original;
            this.diagnostic = diagnostic;
            this.actorId = actorId;
            this.mesh = mesh;
            this.omitted = omitted;
            this.output = output;
            this.provenance = provenance;
            this.fit = fit;
            patch = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_SURFACE_POSE") is not null ? new(0, 0, Width, Height) : donor == 7 ? new(1600, 590, 125, 205) : new(1530, 840, 210, 180);
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
            File.WriteAllText(Path.Combine(output, "surface.json"), JsonSerializer.Serialize(new { Provenance = provenance, Width, Height, Projection = projection, Patch = patch, ChangedPatchPixels = changed, OriginalPixelHash = Hash(first), DiagnosticPixelHash = Hash(second), OriginalSubmission = originalSubmission, DiagnosticSubmission = renderer.PilotSubmission, OriginalDescriptor = original.Primitives.LastDescriptorPerspective, original.Primitives.LastDepthState, OriginalCommands = original.Primitives.LastCommands.Count, DiagnosticCommands = diagnostic.Primitives.LastCommands.Count, OmittedAuthoredFaceIds = omitted.Order().ToArray(), PatchCommands = InspectPatch(original, projection), Qualification = "Original retained prepared frame uses actual renderer/material/depth/ordering. Omitted-lowerbody image is a diagnostic comparison only, never actual acceptance. Selected phase may lead decoded presentation by one frame; actual callback and decoded hash are recorded." }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            if (fit is { } candidateFit)
            {
                var baseline = Render(candidateFit.Baseline, "03-DIAGNOSTIC-reemitted-decoded-baseline.png");
                string candidateName = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_AUXILIARY_STATE")) ? "04-DIAGNOSTIC-parent-relative-rigid-fit.png" : "04-DIAGNOSTIC-warm-auxiliary-state.png";
                var candidate = Render(candidateFit.Candidate, candidateName);
                if (Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_CANDIDATE_OMISSION") == "1")
                {
                    NativeBattleLogicalResolvedCommand[] candidateKept = candidateFit.Candidate.Primitives.LastCommands.Where(item => item.Command.PresentationActorId != actorId || item.Command.Pilot.Identity.Bank != 0x502D40 || !omitted.Contains(item.Command.Pilot.Identity.Primitive)).ToArray();
                    NativeBattlePreparedPresentation candidateOmission = candidateFit.Candidate with { Primitives = candidateFit.Candidate.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(candidateKept) } };
                    Render(candidateOmission, "05-DIAGNOSTIC-warm-state-no-pure-lowerbody.png");
                }
                File.WriteAllText(Path.Combine(output, "attachment-fit.json"), JsonSerializer.Serialize(new { candidateFit.Facts, BaselineHash = Hash(baseline), CandidateHash = Hash(candidate), BaselineCommands = candidateFit.Baseline.Primitives.LastCommands.Count, CandidateCommands = candidateFit.Candidate.Primitives.LastCommands.Count, BaselineDescriptor = candidateFit.Baseline.Primitives.LastDescriptorPerspective, CandidateDescriptor = candidateFit.Candidate.Primitives.LastDescriptorPerspective, BaselineDepth = candidateFit.Baseline.Primitives.LastDepthState, CandidateDepth = candidateFit.Candidate.Primitives.LastDepthState, BaselineCapabilities = candidateFit.Baseline.PrimitiveCapabilities, CandidateCapabilities = candidateFit.Candidate.PrimitiveCapabilities, BaselinePatchCommands = InspectPatch(candidateFit.Baseline, projection), CandidatePatchCommands = InspectPatch(candidateFit.Candidate, projection), Qualification = "Both rebuilt frames use the same actual decoded pose/camera and existing native stage/body packet admission and ordering on detached process forks. Decoded pose may lead the retained original by one callback. No native Tick or gameplay state change. Hypothesis only; no production correction." }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            }
            Completed = true;
            GC.KeepAlive(window);
            Exit();

            Microsoft.Xna.Framework.Color[] Render(NativeBattlePreparedPresentation frame, string name)
            {
                var replacements = (IReadOnlyList<NativeBattleTextureReplacement>)typeof(BattleScenePresentation).GetMethod("NativeModelReplacements", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [frame])!;
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
            int commandIndex = -1;
            foreach (ref readonly NativeBattleLogicalResolvedCommand resolved in frame.Primitives.LastCommands)
            {
                commandIndex++;
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
                result.Add(new { CommandIndex = commandIndex, resolved.Pass, Face = faceIndex, Bones = face.Vertices.Select(index => mesh.Vertices[index].Bone).ToArray(), face.Texture, face.Uv, face.NativeMaterial, face.Color, Omitted = omitted.Contains(faceIndex), command.Pilot.Identity, command.Pilot.ScreenSpace, command.Pilot.UseSourceDepths, command.Pilot.SourceDepths, HasWholeModelPose = command.Pilot.ModelPose is not null, command.Flags20, command.Opcode, command.OrderingBucket, Depths = command.VertexPayloadSpan.ToArray(), Screen = positions, command.ModelTextureOwner, command.TextureSourceOwner, resolved.DrawMode, ActorOpacity = frame.Primitives.ActorOpacity(actorId) });
            }
            return result.ToArray();
        }

        private static string Hash(Microsoft.Xna.Framework.Color[] pixels) => Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())));
    }
}

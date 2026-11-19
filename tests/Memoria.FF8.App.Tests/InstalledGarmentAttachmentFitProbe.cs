using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Private opt-in hypothesis renderer; never publishes a garment correction to a battle.</summary>
internal static class InstalledGarmentAttachmentFitProbe
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    internal static (NativeBattlePreparedPresentation Baseline, NativeBattlePreparedPresentation Candidate, object Facts) Prepare(GameWorld world, string actorId, BattleModel body, NativeBattlePreparedPresentation original)
    {
        BattlePresentationProcess process = world.Battle!.Presentation!;
        BattleModelOwner owner = world.Battle.ReadPresentationModelOwner(actorId);
        NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(owner);
        NativeBattleDecodedPose pose = model.SequenceState!.DecodedPose!;
        NativeBattlePoseSource source = body.NativePoseSource!;
        string? auxiliaryState = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_GARMENT_AUXILIARY_STATE");
        if (string.IsNullOrWhiteSpace(auxiliaryState))
        {
            throw new InvalidDataException("Select an external diagnostic auxiliary state explicitly.");
        }
        NativeBattleDecodedPose candidate = ReadAuxiliaryState(auxiliaryState, world, actorId, body, model.SequenceState, pose, out object facts);
        string before = System.Text.Json.JsonSerializer.Serialize(world.Battle.Capture());
        NativeBattlePreparedPresentation baselineFrame = Reemit(process, owner, source, pose, original);
        NativeBattlePreparedPresentation candidateFrame = Reemit(process, owner, source, candidate, original);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(world.Battle.Capture()));
        var originalFaces = original.Primitives.LastCommands.Where(item => item.Command.PresentationActorId == actorId && item.Command.Pilot.Identity.Bank == 0x502D40).GroupBy(item => item.Command.Pilot.Identity.Primitive).ToDictionary(group => group.Key, group => group.Select(item => item.Command.Pilot).ToArray());
        float maximumCornerDifference = 0;
        int matchedFaces = 0;
        foreach (ref readonly NativeBattleLogicalResolvedCommand item in baselineFrame.Primitives.LastCommands)
        {
            if (item.Command.PresentationActorId != actorId || item.Command.Pilot.Identity.Bank != 0x502D40 || !originalFaces.TryGetValue(item.Command.Pilot.Identity.Primitive, out var prior))
            {
                continue;
            }
            matchedFaces++;
            float closestOccurrence = float.PositiveInfinity;
            foreach (NativeBattlePilotGeometryReference occurrence in prior)
            {
                if (occurrence.CornerCount != item.Command.Pilot.CornerCount)
                {
                    continue;
                }
                float maximum = 0;
                for (int corner = 0; corner < item.Command.Pilot.CornerCount; corner++)
                {
                    maximum = Math.Max(maximum, Vector3.Distance(occurrence.Corner(corner), item.Command.Pilot.Corner(corner)));
                }
                closestOccurrence = Math.Min(closestOccurrence, maximum);
            }
            maximumCornerDifference = Math.Max(maximumCornerDifference, closestOccurrence);
        }
        return (baselineFrame, candidateFrame, new { Fit = facts, OriginalAdmittedBodyFaces = originalFaces.Count, OriginalRepeatedFaceIds = originalFaces.Count(pair => pair.Value.Length > 1), MatchedReemittedBodyFaces = matchedFaces, MaximumRetainedToDecodedWorldCornerDifference = maximumCornerDifference, Qualification = "Original command lists can repeat a face across passes; each re-emitted face is compared to its nearest retained occurrence and multiplicity is reported. Retained model geometry can precede selected/current decoded pose; compare re-emitted baseline and candidate at the identical decoded pose, and verify the baseline reproduces the visible defect before crediting a correction." });
    }

    private static NativeBattlePreparedPresentation Reemit(BattlePresentationProcess original, BattleModelOwner selectedOwner, NativeBattlePoseSource source, NativeBattleDecodedPose pose, NativeBattlePreparedPresentation retained)
    {
        Type type = typeof(BattlePresentationProcess);
        object state = type.GetMethod("ReadNativeState", PrivateInstance)!.Invoke(original, null)!;
        Type snapshots = type.Assembly.GetType("Memoria.FF8.Runtime.NativeBattleStorageSnapshots")!;
        object fork = snapshots.GetMethod("Fork", PrivateStatic)!.MakeGenericMethod(state.GetType()).Invoke(null, [state])!;
        ConstructorInfo constructor = type.GetConstructors(PrivateInstance).Single(item => item.GetParameters() is { Length: 2 } parameters && parameters[0].ParameterType == type);
        BattlePresentationProcess staged = (BattlePresentationProcess)constructor.Invoke([original, fork]);
        FieldInfo hostedPackets = type.GetField("hostedModelPackets", PrivateInstance)!;
        hostedPackets.SetValue(staged, Activator.CreateInstance(hostedPackets.FieldType, true));
        NativeBattleGraphicsState graphics = staged.Graphics;
        NativeBattleGraphicsModel selected = graphics.ActorModels.Get(selectedOwner);
        selected = selected with { SequenceState = selected.SequenceState! with { DecodedPose = pose } };
        selected = NativeBattleBodyBoneMatrices.Publish508C90(selected, source);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(selectedOwner, selected) };
        NativeBattleEffectMemory memory = staged.Memory;
        NativeBattlePrimitiveProcess drawing = memory.PrimitiveProcess!;
        byte buffer = drawing.Buffer80 ?? throw new InvalidDataException("The diagnostic fork requires its selected native drawing table.");
        bool descriptor = retained.Primitives.LastDescriptorPerspective ?? throw new InvalidDataException("The original consumed drawing descriptor is absent.");
        drawing = drawing with { TablePerspective = drawing.TablePerspective.SetItem(buffer, descriptor), Depth = retained.Primitives.LastDepthState, Color = retained.Primitives.LastColorState };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(staged, memory with { PrimitiveProcess = drawing });
        graphics = graphics with { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        graphics = Invoke<NativeBattleGraphicsState>(staged, "SubmitNativeStagePackets", graphics, staged.Stage);
        foreach (BattleModelOwner owner in graphics.ActorModels.Owners)
        {
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(owner);
            if ((actor.Flags0 & 2) == 0)
            {
                continue;
            }
            NativeBattleGraphicsResource resource = Invoke<NativeBattleGraphicsResource>(staged, "NativeGraphicsPoseResource", graphics, owner);
            graphics = Invoke<NativeBattleGraphicsState>(staged, "SubmitNativeModelPackets502D40", graphics, owner, actor, resource);
        }
        graphics = Invoke<NativeBattleGraphicsState>(staged, "FinishNativePrimitiveFrame500900", graphics);
        Assert.Equal(retained.Primitives.LastDescriptorPerspective, graphics.PrimitiveFrame!.LastDescriptorPerspective);
        Assert.Equal(retained.Primitives.LastDepthState, graphics.PrimitiveFrame.LastDepthState);
        Assert.Equal(retained.Primitives.LastColorState, graphics.PrimitiveFrame.LastColorState);
        NativeBattlePreparedPrimitiveFrame primitives = NativeBattlePreparedPrimitiveFrame.From(graphics.PrimitiveFrame!) with { ActorOpacities = retained.Primitives.ActorOpacities };
        return retained with { Primitives = primitives, Textures = staged.Memory.Textures, TextureWindows = staged.Memory.PrimitiveProcess!.TextureWindowCache, UiFrame = staged.Memory.UiImmediateFrame };
    }

    private static T Invoke<T>(BattlePresentationProcess process, string name, params object[] arguments)
    {
        return (T)typeof(BattlePresentationProcess).GetMethod(name, PrivateInstance)!.Invoke(process, arguments)!;
    }

    private sealed record AuxiliarySource(int Owner, int Animation, int Frame);
    private sealed record AuxiliaryRotation(int Bone, short X, short Y, short Z);
    private sealed record AuxiliaryInput(int Recipient, AuxiliarySource Source, AuxiliaryRotation[] AuxiliaryRotations, string? Purpose = null);

    private static NativeBattleDecodedPose ReadAuxiliaryState(string path, GameWorld world, string actorId, BattleModel body, NativeBattleSequenceState sequence, NativeBattleDecodedPose pose, out object facts)
    {
        AuxiliaryInput input = System.Text.Json.JsonSerializer.Deserialize<AuxiliaryInput>(File.ReadAllText(path)) ?? throw new InvalidDataException("The diagnostic auxiliary state is absent.");
        BattleCombatantDefinition actor = world.Battle!.GetCombatantDefinition(actorId);
        Assert.Equal(actor.ModelId, input.Recipient);
        Assert.Equal(actor.WeaponProfile!.ResourceOwner, input.Source.Owner);
        Assert.Equal(sequence.Pose, checked((byte)input.Source.Animation));
        Assert.NotEmpty(input.AuxiliaryRotations);
        Assert.Equal(input.AuxiliaryRotations.Length, input.AuxiliaryRotations.Select(rotation => rotation.Bone).Distinct().Count());
        var semantic = BattleHumanoidRigProfiles.SemanticPairs(input.Recipient, body, input.Recipient, body);
        int[] mapped = semantic.Select(pair => pair.Recipient).ToArray();
        BattlePresentationProcess process = world.Battle.Presentation!;
        BattleModelOwner owner = world.Battle.ReadPresentationModelOwner(actorId);
        NativeBattleGraphicsResource resource = Invoke<NativeBattleGraphicsResource>(process, "NativeGraphicsPoseResource", process.Graphics, owner);
        NativeBattleDecodedPose expected = resource.PoseSource!.Sample(checked((byte)input.Source.Animation), checked((byte)input.Source.Frame), sequence.PoseFlags109);
        foreach (int bone in mapped)
        {
            Assert.Equal(expected.Bones[bone], pose.Bones[bone]);
        }
        Assert.Equal(expected.Translation, pose.Translation);
        var bones = pose.Bones.ToBuilder();
        foreach (AuxiliaryRotation rotation in input.AuxiliaryRotations)
        {
            Assert.InRange(rotation.Bone, 0, body.Bones.Count - 1);
            foreach (int mappedBone in mapped)
            {
                for (int current = mappedBone; current >= 0; current = body.Bones[current].Parent)
                {
                    Assert.NotEqual(current, rotation.Bone);
                }
            }
            bones[rotation.Bone] = bones[rotation.Bone] with { Rotation = new(rotation.X, rotation.Y, rotation.Z) };
        }
        NativeBattleDecodedPose candidate = pose with { Bones = bones.ToImmutable() };
        Matrix4x4[] originalGlobal = Globals(body.NativePoseSource!, pose);
        Matrix4x4[] candidateGlobal = Globals(body.NativePoseSource!, candidate);
        foreach (int bone in mapped)
        {
            Assert.Equal(pose.Bones[bone], candidate.Bones[bone]);
            Assert.Equal(originalGlobal[bone], candidateGlobal[bone]);
        }
        facts = new { Policy = "DIAGNOSTIC ONLY imported auxiliary state, no production parameters", InputPurpose = input.Purpose, input.Source, input.Recipient, SourceFrameMatchesCompiledMappedPose = true, sequence.PoseFrame114, MappedPhysicalJointsUnchanged = true, input.AuxiliaryRotations };
        return candidate;
    }

    private static Matrix4x4[] Globals(NativeBattlePoseSource source, NativeBattleDecodedPose pose)
    {
        return (Matrix4x4[])typeof(NativeBattlePoseRetargeter).GetMethod("GlobalRotations", PrivateStatic)!.Invoke(null, [source, pose])!;
    }

}

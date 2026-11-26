using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private BattleGuardianFormResources BrothersCompanionResource(NativeBattleGuardianFormPresentation form)
    {
        BattleGuardianFormResources source = GuardianFormResources(form.GuardianForceId, form.VariantId);
        BattleGuardianFormActionBinding action = source.InnateBindings.Single(binding => binding is { Command: 8, ResourceActionId: 213, NativeIdentity.EffectId: 258 });
        string variant = action.PresentedModels.Single(model => model != form.VariantId);
        return GuardianFormResources(form.GuardianForceId, variant);
    }

    private NativeBattleGraphicsState StartBrothersCompanion(NativeBattleGraphicsState graphics)
    {
        if (graphics.GuardianFormPresentation is not { CinematicEffect: 205, Phase: 3, Companion: null } form ||
            graphics.OriginalEffect is not { Binding.Effect: 258, Parameters: { } parameters } ||
            nativeActionPackets.ReadHeader(parameters.Header) is not { Source: var source, Command: 8, Action: 213 } || source != form.Actor)
        {
            return graphics;
        }
        string id = $"brothers-companion:{form.InvocationId}";
        BattleForeignEnemyModel companion = new(new(id));
        var resource = BrothersCompanionResource(form).VisualResource;
        var texture = NativeBattleModelTextures.Prepare507400(resource.BodyTexturePack ?? throw new InvalidDataException("Mad Cow Special requires the other brother's texture pack."), HostedEnemyBlockedTextureSlots);
        NativeBattleEffectMemory memory = RequireWaterMemory();
        var textures = memory.Textures ?? throw new InvalidDataException("Mad Cow Special requires the current texture owner.");
        var cache = memory.PrimitiveProcess?.TextureWindowCache ?? throw new InvalidDataException("Mad Cow Special requires the current texture cache.");
        var leaf = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), texture);
        textures = textures.WithPresentationTexture(id, new(leaf, NativeBattlePcTextureWindowCache.Reset4647A0(cache)));
        NativeEffectMemory = memory with { Textures = textures };
        return graphics with { GuardianFormPresentation = form with { Companion = companion, CompanionTexture = texture.Binding, CompanionAge = 0 } };
    }

    private BattleModelOwner? SelectBrothersCompanionOperand(NativeBattleGraphicsState graphics, NativeBattleModelSlot slot)
    {
        if (graphics.GuardianFormPresentation is not { Companion: { } companion } form || slot.Offset is not (3 or 4))
        {
            return null;
        }
        // The original effect addresses Minotaur as 3 and Sacred as 4.
        return slot.Offset == (form.BodyModelId == SacredBody ? 4 : 3) ? form.Actor : companion;
    }

    private NativeBattleGraphicsState WithBrothersCompanionModel(NativeBattleGraphicsState graphics)
    {
        if (graphics.GuardianFormPresentation is not { Companion: { } companion } form ||
            graphics.OriginalEffect is not { Binding.Effect: 258 })
        {
            return graphics;
        }
        BattleGuardianFormResources resource = BrothersCompanionResource(form);
        NativeBattleGraphicsResource body = resource.VisualResource;
        NativeBattlePoseSource pose = body.PoseSource ?? throw new InvalidDataException("Mad Cow Special requires the other brother's pose rig.");
        NativeBattleGraphicsModel caster = graphics.ActorModels.Get(form.Actor);
        NativeBattleModelTransform transform = caster.Transform ?? throw new InvalidDataException("Mad Cow Special requires the performing brother's placement.");
        // Preserve the separation of the two authored positions while facing the allied side.
        int direction = form.BodyModelId == SacredBody ? -1 : 1;
        NativeBattleVector position = transform.Position28;
        position = new(unchecked((short)(position.X + direction * 2300)), position.Y, unchecked((short)(position.Z - direction * 200)));
        byte animation = 0;
        byte limit = body.AnimationFrames[animation];
        byte frame = checked((byte)(form.CompanionAge % limit + 1));
        short scale = checked((short)(BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(2, 2)) * 4));
        NativeBattleGraphicsModel model = new(3)
        {
            ModelId4 = checked((byte)(BattleGuardianFormProfiles.ProfileFor(resource).ModelId + 16)),
            Color40 = caster.Color40,
            Color44 = caster.Color44,
            Flags2 = 0,
            Flags8 = 0,
            Kind5 = 0,
            Rotation12 = 0,
            Rotation14 = 0,
            Rotation16 = 0,
            ScaleVectorAddress48 = 0,
            Transform = new(position, position, 0, transform.Angle36, scale),
            Sequence = 1,
            SequenceState = new() { Pose = animation, PoseFrame114 = frame, PoseLimit115 = limit, DecodedPose = pose.Sample(animation, frame, 0) },
            BodyTextureSlots = form.CompanionTexture
        };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, pose);
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, pose) };
        model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model, pose);
        return graphics with { ActorModels = graphics.ActorModels.Add(companion, model) };
    }

    private static NativeBattleGraphicsState WithoutBrothersCompanionModel(NativeBattleGraphicsState graphics)
    {
        return graphics.GuardianFormPresentation?.Companion is { } companion && graphics.ActorModels.Contains(companion)
            ? graphics with { ActorModels = graphics.ActorModels.Remove(companion) }
            : graphics;
    }

    private NativeBattleGraphicsState DrawBrothersCompanion(NativeBattleGraphicsState graphics)
    {
        if (graphics.GuardianFormPresentation is not { Companion: { } companion } form ||
            graphics.OriginalEffect is not { Binding.Effect: 258, ReturnedDescriptor: > 0 })
        {
            return graphics;
        }
        graphics = WithBrothersCompanionModel(graphics);
        graphics = SubmitNativeModelPackets502D40(graphics, companion, graphics.ActorModels.Get(companion), BrothersCompanionResource(form).VisualResource);
        return WithoutBrothersCompanionModel(graphics) with { GuardianFormPresentation = form with { CompanionAge = form.CompanionAge + 1 } };
    }

    private NativeBattleGraphicsState RetireBrothersCompanion(NativeBattleGraphicsState graphics)
    {
        if (graphics.GuardianFormPresentation is not { Companion: { } companion } form ||
            graphics.OriginalEffect is { Binding.Effect: 258, ReturnedDescriptor: > 0 })
        {
            return graphics;
        }
        ReleaseBrothersCompanionTexture(companion);
        return graphics with { GuardianFormPresentation = form with { Companion = null, CompanionTexture = null, CompanionAge = 0 } };
    }

    private void ReleaseBrothersCompanionTexture(BattleForeignEnemyModel companion)
    {
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleEffectTextureState textures = memory.Textures!;
        NativeEffectMemory = memory with { Textures = textures with { PresentationTextures = textures.PresentationTextures!.Remove(companion.Presentation.Key) } };
    }
}

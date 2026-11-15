using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverPhysicalHostEffectsState(NativeBattleModuleState Modules, NativeCrtRandomState Random, int HostTarget, BattleModelOwner TargetOwner, NativeBattleModelAliases Aliases, NativeBattleVector Target, NativeBattleActionMatrix? WeaponMatrix)
{
    public NativeBattleEffectArena? Temporary { get; init; }
    public NativeBattleEffectArena? Packets { get; init; }
    public NativeBattleLogicalOrdering? Ordering { get; init; }
    public ImmutableArray<NativeBattleModuleDraw> Sprites { get; init; } = [];
    public bool Complete => Modules.Head is null;
}

internal sealed record CrossoverPhysicalHostEffectsStep(CrossoverPhysicalHostEffectsState State, bool Contact, ImmutableArray<NativeBattleModuleSound> Sounds);

internal sealed partial class CrossoverGunbladeHostAttack
{
    private ImmutableArray<NativeBattleModuleResource> physicalModules;
    private ImmutableArray<NativeBattleStaticMeshResource> physicalMeshes;
    private NativeBattleEffectTextureState? physicalTextures;
    private Func<CrossoverGunbladeHostPlacement, int, CrossoverNativeForeignTarget?>? readNativeTargetGeometry;

    public void BindNativeTargetGeometry(Func<CrossoverGunbladeHostPlacement, int, CrossoverNativeForeignTarget?> read)
    {
        readNativeTargetGeometry = read;
    }

    private static CrossoverPhysicalHostEffectsState CapturePhysicalEffects(CrossoverPhysicalHostEffectsState current) => current with
    {
        Temporary = current.Temporary?.Capture(),
        Packets = current.Packets?.Capture(),
        Ordering = current.Ordering is { } ordering ? NativeBattleEffectComponentStorage.Capture(ordering) : null
    };

    public CrossoverNativeEffectVisual? AttackEffectVisual(CrossoverNativeSpriteRenderer compositor)
    {
        if (state is not { Placement: { } placement, PhysicalEffects: { } attack } || attack.Sprites.IsEmpty && attack.Ordering is null)
        {
            return null;
        }
        NativeBattleEffectTextureState textures = ReadPhysicalTextures();
        return new(compositor, attack.Sprites, textures, placement.Matrix, CrossoverGunbladeHostPlacement.OriginToHostScale)
        {
            Ordering = attack.Ordering,
            Memory = attack.Packets is { } packets ? NativeBattleEffectMemory.FromColdProcess() with { Arena = packets, Textures = textures } : null
        };
    }

    private CrossoverPhysicalHostEffectsState QueuePhysicalHostParticle(CrossoverPhysicalHostEffectsState? current, NativeBattleVector target, int hostTargetSlot, NativeBattleSequenceParticle particle, NativeBattleGraphicsModel model, bool contact, NativeCrtRandomState random)
    {
        if (hostTargetSlot is < 0 or 3 or > 9 || state.Invocation?.TargetSlot != hostTargetSlot || !roster.Participants.Any(actor => actor.HostSlot == hostTargetSlot))
        {
            throw new InvalidDataException("Physical attack particles require the active host target occurrence.");
        }
        NativeBattleModuleResource selected = ReadPhysicalResources().Single(item => item.Id == (byte)particle.PackedParameters);
        if (selected.WrapperCallback == 0x56F9C0 && (model.ModelId4 != 4 || particle.Placement != 1 || contact || current is { Complete: false }))
        {
            throw new InvalidDataException("Rinoa's original projectile requires its original B0 model placement and a free shared module owner.");
        }
        CrossoverPhysicalHostEffectsState effects = current ?? BeginPhysicalHostEffects(hostTargetSlot, target, random);
        BattleModelOwner owner = contact ? effects.TargetOwner : sourceOwner;
        NativeBattleModuleState modules = particle.Placement switch
        {
            1 => effects.Modules.AppendModelResource(owner, selected, particle.PackedParameters, effects.Aliases),
            3 => effects.Modules.AppendBoneParticle(owner, particle.Bone, particle.Fraction, selected, particle.PackedParameters, effects.Aliases),
            4 => effects.Modules.AppendPositionResource(owner, particle.Position ?? throw new InvalidDataException("Physical attack placement4 lacks its original position."), selected, particle.PackedParameters, effects.Aliases),
            _ => throw new InvalidDataException("Physical attack particle has an unknown original placement.")
        };
        return effects with { Modules = modules };
    }

    private CrossoverPhysicalHostEffectsState BeginPhysicalHostEffects(int hostTargetSlot, NativeBattleVector target, NativeCrtRandomState random)
    {
        BattleModelOwner targetOwner = roster.ReadModelOwner(hostTargetSlot);
        NativeBattleModelAliases aliases = sourceAliases;
        if (targetOwner != sourceOwner)
        {
            BattleModelRoster models = new([sourceOwner, targetOwner]);
            byte targetOperand = targetOwner.Side == BattleSide.Enemies ? (byte)3 : ActorSlot == 0 ? (byte)1 : (byte)0;
            aliases = new(models, [(NativeBattleModelSlot.Decode(checked((byte)ActorSlot)), sourceOwner), (NativeBattleModelSlot.Decode(targetOperand), targetOwner)]);
        }
        return new(NativeBattleModuleState.Clear(), random, hostTargetSlot, targetOwner, aliases, target, null);
    }

    private CrossoverPhysicalHostEffectsStep AdvancePhysicalHostEffects(CrossoverPhysicalHostEffectsState current, NativeBattleGraphicsModel model, CrossoverGunbladeHostPlacement placement)
    {
        ImmutableArray<NativeBattleModuleResource> resources = ReadPhysicalResources();
        NativeBattleCameraProjection projection = CrossoverNativeSpriteRenderer.Camera(readHostCamera(), placement.Matrix);
        NativeBattleActionMatrix worldBasis = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix? weaponMatrix = current.WeaponMatrix;
        CrossoverNativeForeignTarget? nativeTarget = current.HostTarget == ActorSlot ? null : readNativeTargetGeometry?.Invoke(placement, current.HostTarget);
        NativeBattleGraphicsModel? targetModel = current.HostTarget == ActorSlot ? model : nativeTarget?.NativeModel;
        NativeBattlePoseSource? targetPose = current.HostTarget == ActorSlot ? resource.PoseSource : nativeTarget?.NativeResource?.PoseSource;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.FromColdProcess());
        if (current.Temporary is { } saved)
        {
            staging = NativeBattleTemporaryMemory.PublishView(staging, NativeBattleTemporaryMemory.Address, saved, 248);
        }
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        int packetCursor = 0;
        bool contact = false;
        ImmutableArray<NativeBattleModuleDraw>.Builder sprites = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleModuleInputs inputs = new(0)
        {
            Resources = resources,
            CrtRandom = current.Random,
            ModelAliases = current.Aliases,
            ReadModelAnchor = actor => actor == current.TargetOwner && actor != sourceOwner ? TargetAnchor() : NativeBattlePoseMatrices.ReadAnchor(model, resource.PoseSource!),
            ReadModelBone = (actor, bone, fraction) => actor == current.TargetOwner && actor != sourceOwner ? TargetBone(bone, fraction) : NativeBattlePoseMatrices.ReadBone(model, resource.PoseSource!, bone, fraction),
            ReadModelRotation14 = actor => actor == current.TargetOwner && actor != sourceOwner ? targetModel?.Rotation14 ?? (short)0 : model.Rotation14!.Value,
            TransformModelPosition = (actor, local) => actor == sourceOwner ? NativeBattlePoseMatrices.TransformModulePosition570CC0(model, local) : throw new InvalidDataException("Physical attack placement4 requires the source model matrix.")
        };
        NativeBattleModuleStep step = current.Modules.Advance(inputs, Dispatch);
        foreach (NativeBattleModuleDraw draw in step.Draws)
        {
            if (draw.Kind == NativeBattleModuleDrawKind.Sprite)
            {
                sprites.Add(draw);
            }
            else if (draw.Kind == NativeBattleModuleDrawKind.Impact570860)
            {
                if (physicalMeshes.IsDefault)
                {
                    physicalMeshes = game.ReadNativeEffectMeshes();
                }
                NativeBattleCameraState camera = new(0, [], []) { Projection = projection };
                NativeBattleModuleImpactPacketStep impact = NativeBattleModuleImpactPackets.Draw570860(memory, staging, draw, camera.CapturePackedView1D97778(), projection, graphicsRules.SummonColors!, physicalMeshes.SingleOrDefault(item => item.Id == 5), resources, 0);
                memory = impact.Memory;
                staging = impact.Staging;
                foreach (NativeBattleGfGeometryBatch batch in impact.Batches)
                {
                    Append(batch);
                }
            }
            else
            {
                throw new NotSupportedException($"Physical attack particles require their native {draw.Kind} draw producer.");
            }
        }
        CrossoverPhysicalHostEffectsState next = current with
        {
            Modules = step.State,
            Random = step.CrtRandomAfter ?? current.Random,
            WeaponMatrix = weaponMatrix,
            Temporary = NativeBattleTemporaryMemory.ReadView(staging, NativeBattleTemporaryMemory.Address, 248),
            Packets = packetCursor == 0 ? null : memory.Arena,
            Ordering = packetCursor == 0 ? null : ordering,
            Sprites = sprites.ToImmutable()
        };
        return new(next, contact, sounds.ToImmutable());

        NativeBattleModuleCallback Dispatch(NativeBattleModuleState modules, int slot, NativeBattleModuleInputs services)
        {
            // TODO(TD-025): Forward the host contact flags to the projectile's secondary critical cue.
            NativeBattleRinoaPhysicalHitInputs projectile = new(services, BattleContactTaskStorageContract.OriginalArena, () => (sourceOwner, current.TargetOwner), _ => model.Flags0, PlaySound, () => false, () => contact = true, draw => sprites.Add(draw));
            switch (modules.Slots[slot].Callback)
            {
                case 0x56F9C0:
                    return NativeBattleRinoaPhysicalHit.AdvanceRoot(modules, slot, projectile);
                case 0x56FCD0:
                    return NativeBattleRinoaPhysicalHit.AdvanceSpark(modules, slot, projectile);
                case 0x56FE10:
                    NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(staging, 248);
                    staging = temporary.Staging;
                    NativeBattleModuleCallback result = NativeBattleRinoaPhysicalHit.AdvanceProjectile(modules, slot, projectile, worldBasis, (active, basis, first, next) => DrawProjectile(active, projectile, basis, first, next, temporary.Address));
                    staging = NativeBattleTemporaryMemory.Release5082D0(staging, 248);
                    return result;
                default:
                    throw new NotSupportedException("Rinoa's original projectile has an unrecognized shared module callback.");
            }
        }

        NativeBattleModuleState DrawProjectile(NativeBattleModuleState modules, NativeBattleRinoaPhysicalHitInputs inputs, NativeBattleActionMatrix basis, int first, int next, uint temporary)
        {
            ImmutableArray<NativeBattleVector> curve = NativeBattleRinoaPhysicalHit.ReadCurveForDraw(modules, inputs);
            weaponMatrix = null;
            if (next < curve.Length)
            {
                NativeBattleVector start = curve[first];
                NativeBattleVector end = curve[next];
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(end.X - start.X, 0, end.Z - start.Z);
                NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
                NativeBattleActionMatrix local = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), start.X, 0, start.Z, 4096);
                NativeBattlePoseSource linked = resource.LinkedPoseSource ?? throw new InvalidDataException("Rinoa's original projectile requires its selected linked weapon skeleton.");
                short scale = BinaryPrimitives.ReadInt16LittleEndian(linked.Skeleton.AsSpan().Slice(2));
                weaponMatrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Compose56C2F0(basis, local), scale, scale, scale);
            }
            NativeBattleActionMatrix view = NativeBattleGfMatrices.Compose56C2F0(projection.View!, basis);
            NativeBattleRinoaPhysicalRibbonDraw ribbon = NativeBattleRinoaPhysicalRibbon.Draw(memory, staging, temporary, curve, view, first, next, projection, modules, inputs.StorageContract);
            memory = ribbon.Memory;
            staging = ribbon.Staging;
            Append(ribbon.Batch);
            return modules;
        }

        void Append(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, packetCursor, ordering, batch);
            (memory, packetCursor, ordering) = (written.Memory, written.Cursor, written.Ordering);
        }

        void PlaySound(BattleModelOwner actor, int number, NativeBattleModuleAnchor anchor)
        {
            if (actor != sourceOwner || graphicsRules.ActorSounds1120 is not { } table)
            {
                throw new InvalidDataException("Rinoa's original projectile sound requires the source actor table.");
            }
            Vector3 position = Vector3.Transform(new(anchor.X, anchor.Y, anchor.Z), placement.Matrix);
            sounds.Add(new(table[4 * 7 + number], 0, anchor) { Pan = readHostPan(position) });
        }

        NativeBattleModuleAnchor TargetAnchor()
        {
            if (targetModel is not null && targetPose is not null)
            {
                return NativeBattlePoseMatrices.ReadAnchor(targetModel, targetPose);
            }
            NativeBattleVector target = readHostImpactAnchor is { } read ? placement.ResolvePosition(read(current.HostTarget)) : current.Target;
            return new(target.X, target.Y, target.Z, 0);
        }

        NativeBattleVector TargetBone(byte bone, short fraction)
        {
            if (targetModel is not null && targetPose is not null)
            {
                return NativeBattlePoseMatrices.ReadBone(targetModel, targetPose, bone, fraction);
            }
            Vector3 position = (readHostTargetBone ?? throw new NotSupportedException("The selected host target requires its current joint pose for the native impact."))(current.HostTarget, bone);
            return placement.ResolvePosition(position);
        }
    }

    private ImmutableArray<NativeBattleModuleResource> ReadPhysicalResources()
    {
        if (physicalModules.IsDefault)
        {
            physicalModules = game.ReadNativeBattleModuleResources();
        }
        return physicalModules;
    }

    private NativeBattleEffectTextureState ReadPhysicalTextures()
    {
        if (physicalTextures is null)
        {
            NativeBattleEffectInitializerAsset common = game.ReadNativeCommonEffectTexture();
            (NativeBattleEffectTextureState textures, _) = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(new(common.Name, common.Data));
            physicalTextures = textures.Drain505D20(NativeBattleEffectArena.Clear()).State;
        }
        return physicalTextures;
    }
}

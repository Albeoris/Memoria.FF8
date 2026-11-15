namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsResource NativeGraphicsPoseResource(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        bool sharedMaterial = NativeModelTextureCache?.PresentationActors is null;
        NativeBattleGraphicsResource? presented = ReadStandingPresentationModelResource(graphics, owner);
        NativeBattleForeignActor? foreign = presentationForeignActors?.GetValueOrDefault(owner);
        if (foreign is { NativeResource: null })
        {
            throw new NotSupportedException("The host actor cannot supply the native skeleton, animation or textured body resource required by the selected effect.");
        }
        if (sharedMaterial && (owner == presentationSourceOwner || presentationNativeTargets?.ContainsKey(owner) == true || foreign is not null) &&
            presented is not null && model.ResourceMagicId is null && model.ActiveStagedResourceRow100 is null)
        {
            return presented;
        }
        NativeBattleGraphicsResource originalResource = presented ?? throw new InvalidDataException("Native graphics pose lost its original model resource.");
        if (graphics.ActorModels.Get(owner).ActiveStagedResourceRow100 is not null)
        {
            NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native staged graphics requires its actual shared file bytes.");
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native staged graphics requires its current CPU skeleton/geometry references.");
            System.Collections.Immutable.ImmutableArray<NativeBattleEffectTaskPool> pools = graphics.OriginalEffect?.Pools ?? [];
            return NativeBattleVictoryResources.Resolve(graphics.ActorModels.Get(owner), originalResource, ReadPointer, ReadBytes);

            NativeBattleResourcePointer? ReadPointer(uint address)
            {
                if (NativeBattleEffectTaskMemory.TryReadPointer(pools, address, out NativeBattleResourcePointer? pointer))
                {
                    return pointer;
                }
                if (address >= NativeBattleLaserEye71Profile.SavedBindingAddress && address < NativeBattleLaserEye71Profile.SavedBindingAddress + 32)
                {
                    return (NativeEffectMemory?.LaserEye71 ?? throw new NotSupportedException("Native bound graphics requires its retained binding row.")).ReadResourcePointer(address);
                }
                if (address >= NativeBattleRayBomb307Profile.SavedBindingAddress && address < NativeBattleRayBomb307Profile.SavedBindingAddress + 32)
                {
                    return (NativeEffectMemory?.RayBomb307 ?? throw new NotSupportedException("Native bound graphics requires its retained binding row.")).ReadResourcePointer(address);
                }
                if (address >= NativeBattleUltraWaves184Profile.SavedBindingAddress && address < NativeBattleUltraWaves184Profile.SavedBindingAddress + 32)
                {
                    return (NativeEffectMemory?.UltraWaves184 ?? throw new NotSupportedException("Native bound graphics requires its retained binding row.")).ReadResourcePointer(address);
                }
                if (address >= NativeBattleGatlingGun305Profile.BindingSave && address < NativeBattleGatlingGun305Profile.BindingSave + 32)
                {
                    return (NativeEffectMemory?.GatlingGun305 ?? throw new NotSupportedException("Native bound graphics requires its retained binding row.")).ReadResourcePointer(address);
                }
                if (address >= NativeBattleDegenerator306Profile.BindingSave && address < NativeBattleDegenerator306Profile.BindingSave + 32)
                {
                    return (NativeEffectMemory?.Degenerator306 ?? throw new NotSupportedException("Native bound graphics requires its retained binding row.")).ReadResourcePointer(address);
                }
                return IsNativeEffectArenaAddress(address) ? RequireWaterMemory().Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : staging.ReadResourcePointer(address);
            }

            byte[] ReadBytes(NativeBattleResourcePointer pointer, int length)
            {
                if (pointer.Cpu is not null)
                {
                    return NativeBattleModelResourceView.Open(cpu, staging, pointer, length).Read(0, length);
                }
                uint address = pointer.StagingAddress ?? throw new NotSupportedException("Native bound resource has no actual byte owner.");
                if (NativeBattleEffectTaskMemory.TryRead(pools, address, length, out byte[] bytes))
                {
                    return bytes;
                }
                return IsNativeEffectArenaAddress(address) ? RequireWaterMemory().Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : staging.Read(address, length);
            }
        }
        if (graphics.ActorModels.Get(owner).ResourceRenzokukenTargetBank is byte targetBank)
        {
            ushort effect = graphics.ActorModels.Get(owner).ResourceMagicId ?? throw new InvalidDataException("Native target animation bank lost its Renzokuken resource owner.");
            BattleNativeRenzokukenResource resource = RenzokukenResource(effect);
            if (targetBank >= resource.TargetResources.Length)
            {
                throw new InvalidDataException("Native target animation bank exceeds its Renzokuken resource directory.");
            }
            NativeBattleGraphicsResource original = graphics.ActorModels.Get(owner).ModelId4 >= 16 && (sharedMaterial || OwnsHostedEnemyPoseRow(owner, model))
                ? ResolveNativeEnemyResource(graphics.ActorModels.Get(owner).ActiveResourceRow100 ?? throw new NotSupportedException("Native target animation requires its loaded enemy resource row."), originalResource)
                : originalResource;
            NativeBattleGraphicsResource target = resource.TargetResources[targetBank].BindGraphicsResource(original.PoseSource ?? throw new NotSupportedException("Native target animation requires its original skeleton owner."));
            return (original with { SequenceSection = target.SequenceSection, SequenceSectionAddress = target.SequenceSectionAddress, AnimationFrames = target.AnimationFrames, PoseSource = target.PoseSource }).ApplyAnimationBankOverrides($"renzokuken-{effect}-target-{targetBank}");
        }
        if (graphics.ActorModels.Get(owner).ModelId4 >= 16)
        {
            if (!sharedMaterial && !OwnsHostedEnemyPoseRow(owner, model))
            {
                return originalResource;
            }
            int enemyRow = graphics.ActorModels.Get(owner).ActiveResourceRow100 ?? throw new NotSupportedException("Native enemy resource view requires its actually loaded physical row; migrate the named older snapshot explicitly.");
            return ResolveNativeEnemyResource(enemyRow, originalResource);
        }
        if (graphics.ActorModels.Get(owner).ResourceMagicId is ushort magic)
        {
            NativeBattleGraphicsResource changed = definition.NativeRenzokukenResources.Single(resource => resource.MagicId == magic).BindGraphicsResource(originalResource.PoseSource ?? throw new NotSupportedException("Embedded native pose requires its original skeleton owner."), originalResource.LinkedPoseSource);
            return (changed with { Geometry = originalResource.Geometry, LinkedGeometry = originalResource.LinkedGeometry, LinkedGeometryUsesBodySkeleton = originalResource.LinkedGeometryUsesBodySkeleton, LinkedTexturePack = originalResource.LinkedTexturePack, AnimationBankOverrides = originalResource.AnimationBankOverrides }).ApplyAnimationBankOverrides("renzokuken-" + magic);
        }
        return originalResource;
    }

    private static bool IsNativeEffectArenaAddress(uint address) => address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress < NativeBattleEffectArena.Length;

    internal NativeBattleVector ReadNativeGraphicsBone(BattleModelOwner owner, byte selector, short fraction)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        return ReadNativeGraphicsBone(graphics, owner, selector, fraction);
    }

    private int ReadNativeGraphicsBoneCount(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        if (NativeGraphicsUsesForeignPose(graphics, owner) && presentationForeignActors?.TryGetValue(owner, out NativeBattleForeignActor? foreign) == true)
        {
            return foreign.BoneCount > 0 ? foreign.BoneCount : throw new NotSupportedException("The host actor cannot supply its actual skeleton segment count.");
        }
        return NativeGraphicsPoseResource(graphics, owner).PoseSource?.BoneCount ?? throw new NotSupportedException("The native actor has no selected skeleton.");
    }

    private NativeBattleVector ReadNativeGraphicsBone(NativeBattleGraphicsState graphics, BattleModelOwner owner, byte selector, short fraction)
    {
        if (NativeGraphicsUsesForeignPose(graphics, owner) && presentationForeignActors?.TryGetValue(owner, out NativeBattleForeignActor? foreign) == true)
        {
            return NativeBattleGfMatrices.Transform460840(NativeBattleForeignPlacement.Delta(foreign, graphics.ActorModels.Get(owner)), foreign.ReadBone(selector, fraction));
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        if ((model.Flags0 & 2) == 0)
        {
            return (model.Transform ?? throw new NotSupportedException("Native502170 requires its model position WORDs.")).Position28;
        }
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, owner).PoseSource ?? throw new NotSupportedException("Native bone projection requires its original skeleton and compressed animation source.");
        return NativeBattlePoseMatrices.ReadBone(model, source, selector, fraction);
    }

    internal NativeBattleCameraPoint TransformNativeGraphicsCameraPoint(BattleModelOwner owner, byte lowNibble, NativeBattleCameraPoint local, bool mirror)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        NativeBattlePoseSource? source = lowNibble != 15 && (model.Flags0 & 2) != 0 ? NativeGraphicsPoseResource(graphics, owner).PoseSource ?? throw new NotSupportedException("Native actor-relative camera requires its original skeleton and animation source.") : null;
        NativeBattleVector result = NativeBattlePoseMatrices.TransformCameraPoint(model, source, lowNibble, new(local.X, local.Y, local.Z), mirror);
        return new(result.X, result.Y, result.Z);
    }

    private NativeBattleModuleAnchor ReadNativeModelAnchor(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        if (NativeGraphicsUsesForeignPose(graphics, owner) && presentationForeignActors?.TryGetValue(owner, out NativeBattleForeignActor? foreign) == true)
        {
            NativeBattleModuleAnchor anchor = foreign.Anchor;
            NativeBattleVector point = NativeBattleGfMatrices.Transform460840(NativeBattleForeignPlacement.Delta(foreign, graphics.ActorModels.Get(owner)), new(anchor.X, anchor.Y, anchor.Z));
            return anchor with { X = point.X, Y = point.Y, Z = point.Z };
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        if ((model.Flags0 & 2) == 0)
        {
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native571400 requires its model position and scale WORDs.");
            NativeBattleVector position = transform.Position28;
            return new(position.X, position.Y, position.Z, unchecked((short)(transform.Scale38 * 2000 >> 12)));
        }
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, owner).PoseSource ?? throw new NotSupportedException("Native571400 requires its original skeleton and compressed animation source.");
        return NativeBattlePoseMatrices.ReadAnchor(model, source);
    }
}

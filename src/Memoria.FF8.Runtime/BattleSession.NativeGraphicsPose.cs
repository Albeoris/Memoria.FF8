namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsResource NativeGraphicsPoseResource(NativeBattleGraphicsState graphics, int index)
    {
        NativeBattleGraphicsResource? presentedResource = index == presentationSourceIndex ? presentationSourceResource : null;
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true)
        {
            presentedResource = foreign.NativeResource ?? throw new NotSupportedException($"Host actor slot {index} cannot supply a native FFVIII skeleton, animation bank or textured body resource required by the selected effect.");
        }
        bool hosted = graphics.PresentationRoster is not null && NativeEffectMemory?.Textures?.PresentationTextures is not null;
        if (!hosted && presentedResource is not null && graphics.ActorModels.Get(index).ResourceMagicId is null && graphics.ActorModels.Get(index).ActiveStagedResourceRow100 is null)
        {
            return presentedResource;
        }
        if (!hosted && presentationNativeTargets?.TryGetValue(index, out NativeBattleEffectModelBinding? effectTarget) == true && graphics.ActorModels.Get(index).ResourceMagicId is null && graphics.ActorModels.Get(index).ActiveStagedResourceRow100 is null)
        {
            return effectTarget.Resource;
        }
        int capacity = hosted ? graphics.PresentationRoster!.ActorCount : 7;
        if ((uint)index >= (uint)capacity || graphics.ActorIds[index] is not string actorId)
        {
            throw new InvalidDataException($"Native graphics pose index {index} has no allocated resource owner.");
        }
        if (hosted)
        {
            if (!StringComparer.Ordinal.Equals(graphics.PresentationRoster![index].ActorId, actorId))
            {
                throw new InvalidDataException("Hosted graphics pose requires the selected row's actual actor identity.");
            }
            presentedResource = ReadHostedPresentationModelResource(index, actorId) ?? throw new NotSupportedException("Hosted graphics pose requires the selected actor's native model resource.");
        }
        NativeBattleGraphicsBinding binding = presentedResource is not null
            ? new(actorId, graphics.ActorModels.Get(index).ModelId4 ?? throw new InvalidDataException("Native presentation participant lost its model identity."), presentedResource)
            : FindNativeGraphicsBinding(actorId) ?? throw new InvalidDataException("Native graphics pose lost its original model resource.");
        if (graphics.ActorModels.Get(index).ActiveStagedResourceRow100 is not null)
        {
            NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native staged graphics requires its actual shared file bytes.");
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native staged graphics requires its current CPU skeleton/geometry references.");
            System.Collections.Immutable.ImmutableArray<NativeBattleEffectTaskPool> pools = graphics.OriginalEffect?.Pools ?? [];
            return NativeBattleVictoryResources.Resolve(graphics.ActorModels.Get(index), binding.Resource, ReadPointer, ReadBytes);

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
        if (graphics.ActorModels.Get(index).ResourceRenzokukenTargetBank is byte targetBank)
        {
            ushort effect = graphics.ActorModels.Get(index).ResourceMagicId ?? throw new InvalidDataException("Native target animation bank lost its Renzokuken resource owner.");
            BattleNativeRenzokukenResource resource = RenzokukenResource(effect);
            if (targetBank >= resource.TargetResources.Length)
            {
                throw new InvalidDataException("Native target animation bank exceeds its Renzokuken resource directory.");
            }
            NativeBattleGraphicsResource original = graphics.ActorModels.Get(index).ModelId4 >= 16 && (!hosted || OwnsHostedEnemyPoseRow(graphics.ActorModels.Get(index)))
                ? ResolveNativeEnemyResource(graphics.ActorModels.Get(index).ActiveResourceRow100 ?? throw new NotSupportedException("Native target animation requires its loaded enemy resource row."), binding.Resource)
                : binding.Resource;
            NativeBattleGraphicsResource target = resource.TargetResources[targetBank].BindGraphicsResource(original.PoseSource ?? throw new NotSupportedException("Native target animation requires its original skeleton owner."));
            return (original with { SequenceSection = target.SequenceSection, SequenceSectionAddress = target.SequenceSectionAddress, AnimationFrames = target.AnimationFrames, PoseSource = target.PoseSource }).ApplyAnimationBankOverrides($"renzokuken-{effect}-target-{targetBank}");
        }
        if (graphics.ActorModels.Get(index).ModelId4 >= 16)
        {
            if (hosted && !OwnsHostedEnemyPoseRow(graphics.ActorModels.Get(index)))
            {
                return binding.Resource;
            }
            int enemyRow = graphics.ActorModels.Get(index).ActiveResourceRow100 ?? throw new NotSupportedException("Native enemy resource view requires its actually loaded physical row; migrate the named older snapshot explicitly.");
            return ResolveNativeEnemyResource(enemyRow, binding.Resource);
        }
        if (graphics.ActorModels.Get(index).ResourceMagicId is ushort magic)
        {
            NativeBattleGraphicsResource changed = definition.NativeRenzokukenResources.Single(resource => resource.MagicId == magic).BindGraphicsResource(binding.Resource.PoseSource ?? throw new NotSupportedException("Embedded native pose requires its original skeleton owner."), binding.Resource.LinkedPoseSource);
            return (changed with { Geometry = binding.Resource.Geometry, LinkedGeometry = binding.Resource.LinkedGeometry, LinkedGeometryUsesBodySkeleton = binding.Resource.LinkedGeometryUsesBodySkeleton, LinkedTexturePack = binding.Resource.LinkedTexturePack, AnimationBankOverrides = binding.Resource.AnimationBankOverrides }).ApplyAnimationBankOverrides("renzokuken-" + magic);
        }
        return binding.Resource;
    }

    private static bool IsNativeEffectArenaAddress(uint address) => address >= NativeBattleEffectNodeMemory.ArenaAddress && (long)address - NativeBattleEffectNodeMemory.ArenaAddress < NativeBattleEffectArena.Length;

    internal NativeBattleVector ReadNativeGraphicsBone(int index, byte selector, short fraction)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        return ReadNativeGraphicsBone(graphics, index, selector, fraction);
    }

    private int ReadNativeGraphicsBoneCount(NativeBattleGraphicsState graphics, int index)
    {
        if (NativeGraphicsUsesForeignPose(graphics, index) && presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true)
        {
            return foreign.BoneCount > 0 ? foreign.BoneCount : throw new NotSupportedException("The host actor cannot supply its actual skeleton segment count.");
        }
        return NativeGraphicsPoseResource(graphics, index).PoseSource?.BoneCount ?? throw new NotSupportedException("The native actor has no selected skeleton.");
    }

    private NativeBattleVector ReadNativeGraphicsBone(NativeBattleGraphicsState graphics, int index, byte selector, short fraction)
    {
        if (NativeGraphicsUsesForeignPose(graphics, index) && presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true)
        {
            return NativeBattleGfMatrices.Transform460840(NativeBattleForeignPlacement.Delta(foreign, graphics.ActorModels.Get(index)), foreign.ReadBone(selector, fraction));
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        if ((model.Flags0 & 2) == 0)
        {
            return (model.Transform ?? throw new NotSupportedException("Native502170 requires its model position WORDs.")).Position28;
        }
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native bone projection requires its original skeleton and compressed animation source.");
        return NativeBattlePoseMatrices.ReadBone(model, source, selector, fraction);
    }

    internal NativeBattleCameraPoint TransformNativeGraphicsCameraPoint(int index, byte lowNibble, NativeBattleCameraPoint local, bool mirror)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        NativeBattlePoseSource? source = lowNibble != 15 && (model.Flags0 & 2) != 0 ? NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native actor-relative camera requires its original skeleton and animation source.") : null;
        NativeBattleVector result = NativeBattlePoseMatrices.TransformCameraPoint(model, source, lowNibble, new(local.X, local.Y, local.Z), mirror);
        return new(result.X, result.Y, result.Z);
    }

    private NativeBattleModuleAnchor ReadNativeModelAnchor(NativeBattleGraphicsState graphics, int index)
    {
        if (NativeGraphicsUsesForeignPose(graphics, index) && presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true)
        {
            NativeBattleModuleAnchor anchor = foreign.Anchor;
            NativeBattleVector point = NativeBattleGfMatrices.Transform460840(NativeBattleForeignPlacement.Delta(foreign, graphics.ActorModels.Get(index)), new(anchor.X, anchor.Y, anchor.Z));
            return anchor with { X = point.X, Y = point.Y, Z = point.Z };
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        if ((model.Flags0 & 2) == 0)
        {
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native571400 requires its model position and scale WORDs.");
            NativeBattleVector position = transform.Position28;
            return new(position.X, position.Y, position.Z, unchecked((short)(transform.Scale38 * 2000 >> 12)));
        }
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native571400 requires its original skeleton and compressed animation source.");
        return NativeBattlePoseMatrices.ReadAnchor(model, source);
    }
}

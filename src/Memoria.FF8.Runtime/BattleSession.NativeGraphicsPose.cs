namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsResource NativeGraphicsPoseResource(NativeBattleGraphicsState graphics, int index)
    {
        if ((uint)index >= 7 || graphics.ActorIds[index] is not string actorId)
        {
            throw new InvalidDataException($"Native graphics pose index {index} has no allocated resource owner.");
        }
        NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(actorId) ?? throw new InvalidDataException("Native graphics pose lost its original model resource.");
        if (graphics.Models[index].ActiveStagedResourceRow100 is not null)
        {
            NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native staged graphics requires its actual shared file bytes.");
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native staged graphics requires its current CPU skeleton/geometry references.");
            System.Collections.Immutable.ImmutableArray<NativeBattleEffectTaskPool> pools = graphics.OriginalEffect?.Pools ?? [];
            return NativeBattleVictoryResources.Resolve(graphics.Models[index], binding.Resource, ReadPointer, ReadBytes);

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
                if (pointer.Cpu is { } reference)
                {
                    return NativeBattleModelEnemyHeap.Read(cpu, staging, reference, length);
                }
                uint address = pointer.StagingAddress ?? throw new NotSupportedException("Native bound resource has no actual byte owner.");
                if (NativeBattleEffectTaskMemory.TryRead(pools, address, length, out byte[] bytes))
                {
                    return bytes;
                }
                return IsNativeEffectArenaAddress(address) ? RequireWaterMemory().Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : staging.Read(address, length);
            }
        }
        if (graphics.Models[index].ResourceRenzokukenTargetBank is byte targetBank)
        {
            ushort effect = graphics.Models[index].ResourceMagicId ?? throw new InvalidDataException("Native target animation bank lost its Renzokuken resource owner.");
            BattleNativeRenzokukenResource resource = RenzokukenResource(effect);
            if (targetBank >= resource.TargetResources.Length)
            {
                throw new InvalidDataException("Native target animation bank exceeds its Renzokuken resource directory.");
            }
            NativeBattleGraphicsResource original = graphics.Models[index].ModelId4 >= 16
                ? ResolveNativeEnemyResource(graphics.Models[index].ActiveResourceRow100 ?? throw new NotSupportedException("Native target animation requires its loaded enemy resource row."), binding.Resource)
                : binding.Resource;
            NativeBattleGraphicsResource target = resource.TargetResources[targetBank].BindGraphicsResource(original.PoseSource ?? throw new NotSupportedException("Native target animation requires its original skeleton owner."));
            return (original with { SequenceSection = target.SequenceSection, AnimationFrames = target.AnimationFrames, PoseSource = target.PoseSource }).ApplyAnimationBankOverrides($"renzokuken-{effect}-target-{targetBank}");
        }
        if (graphics.Models[index].ModelId4 >= 16)
        {
            int enemyRow = graphics.Models[index].ActiveResourceRow100 ?? throw new NotSupportedException("Native enemy resource view requires its actually loaded physical row; migrate the named older snapshot explicitly.");
            return ResolveNativeEnemyResource(enemyRow, binding.Resource);
        }
        if (graphics.Models[index].ResourceMagicId is ushort magic)
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

    private NativeBattleVector ReadNativeGraphicsBone(NativeBattleGraphicsState graphics, int index, byte selector, short fraction)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
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
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattlePoseSource? source = lowNibble != 15 && (model.Flags0 & 2) != 0 ? NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native actor-relative camera requires its original skeleton and animation source.") : null;
        NativeBattleVector result = NativeBattlePoseMatrices.TransformCameraPoint(model, source, lowNibble, new(local.X, local.Y, local.Z), mirror);
        return new(result.X, result.Y, result.Z);
    }

    private NativeBattleModuleAnchor ReadNativeModelAnchor(NativeBattleGraphicsState graphics, int index)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
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

using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleGraphicsDefinition ReadNativeGraphicsDefinition(IReadOnlyList<BattleCombatantDefinition> actors, NativeBattleGraphicsRules rules, BattleEncounter? encounter = null)
    {
        rules.Validate();
        BattleCombatantDefinition[] party = actors.Where(actor => actor.IsPlayer).ToArray();
        ImmutableArray<NativeBattleGraphicsBinding> bindings = actors.Select(actor => new NativeBattleGraphicsBinding(actor.Id, checked((byte)(actor.IsPlayer ? actor.ModelId : actor.ModelId + 16)), ReadNativeGraphicsResource(actor)) { InitialPosition = actor.IsPlayer ? NativeBattleFormation.PartyPosition(Array.IndexOf(party, actor), party.Length) : encounter is null ? null : encounter.Slots.Single(slot => slot.Slot == actor.SceneSlot).NativePosition ?? throw new InvalidDataException("Native graphics formation lacks its original scene position WORDs.") }).ToImmutableArray();
        //47DAC0 queues enemy103 completions before47DD30/47DBA0 player102/103 descriptors.
        ImmutableArray<string> order = actors.Where(actor => !actor.IsPlayer && actor.InitialActive).Concat(actors.Where(actor => actor.IsPlayer)).Select(actor => actor.Id).ToImmutableArray();
        return new(rules, bindings, order) { PrimitiveTables = ReadNativePrimitiveTables(), SoftwarePrimitiveTable = ReadNativeSoftwarePrimitiveTable(), PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8(), PcUiIcons = ReadNativePcUiIcons().Binding };
    }

    public NativeBattleGraphicsResource ReadNativeGraphicsResource(BattleCombatantDefinition actor)
    {
        if (actor.IsPlayer)
        {
            BattleModel body = LoadPlayer(actor.ModelId, actor.Costume);
            BattleModel owner = LoadPlayerSequenceOwner(actor.ModelId, actor.WeaponId, body);
            BattleModel? linked = actor.WeaponId is int weapon ? ResolveWeaponModel(actor.ModelId, weapon, body) : null;
            //507BF0/507E20 replace the primary resource's sequence pointer while retaining its body animation pointer.
            NativeBattleGraphicsResource result = (owner.Sequences ?? throw new InvalidDataException("Native player sequence owner lacks its original resource.")).NativeResource with { AnimationFrames = body.Animations.Select(animation => checked((byte)animation.Frames.Count)).ToImmutableArray(), PoseSource = body.NativePoseSource, LinkedAnimationFrames = linked is null ? [] : linked.Animations.Select(animation => checked((byte)animation.Frames.Count)).ToImmutableArray(), LinkedPoseSource = linked?.NativePoseSource, HasActionCamera = body.NativeActionCameraBank is not null, ActionCameraBank = body.NativeActionCameraBank, TextureAnimations = body.NativeTextureAnimations, BodyTexturePack = body.NativeBodyTexturePack, LinkedTexturePack = owner.NativeWeaponTexturePack, LinkedGeometryUsesBodySkeleton = owner.NativeWeaponUsesBodySkeleton, Geometry = body.NativeRenderGeometry ?? NativeBattleGeometryMetadata.FromMeshes(body.NativeControlMeshes ?? body.Meshes), LinkedGeometry = owner.NativeWeaponUsesBodySkeleton == true ? owner.NativeRenderGeometry ?? NativeBattleGeometryMetadata.FromMeshes(owner.NativeControlMeshes ?? owner.Meshes) : linked is null ? [] : linked.NativeRenderGeometry ?? NativeBattleGeometryMetadata.FromMeshes(linked.NativeControlMeshes ?? linked.Meshes) };
            return BindNativeModelLoadSources(BindNativeAnimationBankOverrides(result, body, linked, actor.ModelId), actor);
        }
        return ReadEnemyNativeGraphicsResource(actor.ModelId);
    }

    public NativeBattleGraphicsResource ReadEnemyNativeGraphicsResource(int model)
    {
        byte[] bytes = Read($"c0m{model:000}.dat");
        NativeBattleModelDatSource dat = ReadNativeModelDatSource(checked((byte)(model + 16)), false, 0);
        if (model == 127)
        {
            NativeBattleGraphicsResource shared = ReadEnemyNativeGraphicsResource(126);
            return shared with { Geometry = [], TextureAnimations = [], BodyTexturePack = null, ModelLoadSources = [dat], TextBank = ReadEnemyGraphicsTextBank(bytes) };
        }
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections != 11)
        {
            throw new NotSupportedException("A native graphics model requires its original eleven-section DAT.");
        }
        int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20));
        int end = Enumerable.Range(0, sections).Select(index => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4 + 4 * index))).Where(offset => offset > start).DefaultIfEmpty(bytes.Length).Min();
        int animationStart = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        int animationCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(animationStart));
        if ((uint)animationCount > 256 || animationStart + 4L + animationCount * 4L > bytes.Length)
        {
            throw new InvalidDataException("Original graphics animation directory exceeds its DAT.");
        }
        ImmutableArray<byte>.Builder frames = ImmutableArray.CreateBuilder<byte>(animationCount);
        for (int index = 0; index < animationCount; index++)
        {
            int pointer = checked(animationStart + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(animationStart + 4 + index * 4)));
            if ((uint)pointer >= bytes.Length || bytes[pointer] == 0)
            {
                throw new InvalidDataException("Original graphics animation lacks its frame-count byte.");
            }
            frames.Add(bytes[pointer]);
        }
        int skeletonStart = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
        //507080 dispatches enemy models to507120, whose resource DWORD44 addresses DAT DWORD6.
        int cameraStart = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24));
        int cameraEnd = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28));
        int textureStart = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(44));
        int motionStart = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16));
        NativeBattleGraphicsResource resource = new(bytes.AsSpan(start, end - start).ToArray().ToImmutableArray()) { TextBank = ReadEnemyGraphicsTextBank(bytes), AnimationFrames = frames.MoveToImmutable(), PoseSource = new(bytes.AsSpan(skeletonStart, 16 + bytes[skeletonStart] * 48).ToArray().ToImmutableArray(), bytes.AsSpan(animationStart, motionStart - animationStart).ToArray().ToImmutableArray()), HasActionCamera = cameraStart != cameraEnd, ActionCameraBank = cameraStart == cameraEnd ? null : new(bytes.AsSpan(cameraStart, cameraEnd - cameraStart).ToArray().ToImmutableArray()), BodyTexturePack = new($"c0m{model:000}.dat:body-textures", bytes.AsSpan(textureStart).ToArray().ToImmutableArray()), TextureAnimations = bytes.AsSpan(motionStart, start - motionStart).ToArray().ToImmutableArray(), ModelLoadSources = [dat], Geometry = LoadEnemy(model).NativeRenderGeometry ?? NativeBattleGeometryMetadata.FromMeshes(LoadEnemy(model).NativeControlMeshes ?? LoadEnemy(model).Meshes), LinkedGeometry = [], LinkedGeometryUsesBodySkeleton = false };
        BattleModel decoded = LoadEnemy(model);
        resource = resource with { PoseSource = decoded.NativePoseSource, AnimationFrames = decoded.Animations.Select(animation => checked((byte)animation.Frames.Count)).ToImmutableArray() };
        resource.Validate();
        if (model == 126)
        {
            NativeExecutableMemory memory = new(GameDataResources.ReadNativeExecutable(resources));
            NativeBattlePoseSource source = resource.PoseSource ?? throw new InvalidDataException("Native model142/143 requires its shared skeleton source.");
            resource = resource with { PoseSource = source with { Model8FBoneAliases = memory.Read(0xB8B7DC, 16).ToImmutableArray() }, ModelLoadSources = [dat, ReadNativeModelDatSource(143, false, 0)] };
        }
        return resource;
    }
}

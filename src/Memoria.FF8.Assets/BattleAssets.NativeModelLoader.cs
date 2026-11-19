using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private NativeBattleGraphicsResource BindNativeModelLoadSources(NativeBattleGraphicsResource resource, BattleCombatantDefinition actor)
    {
        byte body = checked((byte)actor.Costume);
        byte? linked = actor.WeaponId is int weapon ? checked((byte)WeaponDefinitions.Single(item => item.Id == weapon).Tier) : null;
        int style = actor.WeaponId is int selectedWeapon ? WeaponOwner(selectedWeapon) : actor.ModelId;
        System.Collections.Immutable.ImmutableArray<NativeBattleModelDatSource>.Builder sources = System.Collections.Immutable.ImmutableArray.CreateBuilder<NativeBattleModelDatSource>();
        NativeBattleModelDatSource bodySource = ReadNativeModelDatSource(checked((byte)actor.ModelId), false, body);
        if (style != actor.ModelId)
        {
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("A composed battle body requires its compiled recipient motion.");
            Dictionary<int, System.Collections.Immutable.ImmutableArray<byte>> replacements = new()
            {
                [0] = pose.Skeleton,
                [2] = pose.Animations
            };
            if (resource.ActionCameraBank is { } camera)
            {
                replacements[4] = camera.Data;
            }
            BattleModel composed = LoadPlayerForWeapon(actor.ModelId, actor.Costume, actor.WeaponId);
            if (composed.NativeGeometryBytes is { } geometry)
            {
                replacements[1] = geometry;
            }
            int maximumVertices = (resource.Geometry ?? []).Concat(resource.LinkedGeometry ?? []).Select(mesh => mesh.Vertices.Length).DefaultIfEmpty(0).Max();
            if (maximumVertices > ushort.MaxValue)
            {
                throw new NotSupportedException("The composed battle geometry exceeds the native projected-vertex scratch contract.");
            }
            byte[] boundedGeometry = replacements.TryGetValue(1, out var selectedGeometry) ? selectedGeometry.ToArray() : bodySource.Bytes.AsSpan().Slice(bodySource.Section(1), bodySource.Section(2) - bodySource.Section(1)).ToArray();
            if (boundedGeometry.Length < 4)
            {
                throw new InvalidDataException("The composed battle geometry lacks its native scratch maximum trailer.");
            }
            BinaryPrimitives.WriteInt32LittleEndian(boundedGeometry.AsSpan()[^4..], maximumVertices);
            replacements[1] = System.Collections.Immutable.ImmutableArray.CreateRange(boundedGeometry);
            int sections = BinaryPrimitives.ReadInt32LittleEndian(bodySource.Bytes.AsSpan());
            if (composed.NativeBodyTexturePack is { } textures)
            {
                replacements[sections is 9 or 10 ? 8 : 5] = textures.Data;
            }
            if (sections is 9 or 10)
            {
                replacements[5] = resource.SequenceSection;
            }
            bodySource = ReplaceNativeDatSections(bodySource, replacements);
        }
        sources.Add(bodySource);
        if (linked is byte selector && style != 7)
        {
            NativeBattleModelDatSource equipment = ReadNativeModelDatSource(checked((byte)style), true, selector);
            if (style != actor.ModelId && actor.WeaponId is int sharedWeapon)
            {
                BattleModel composed = LoadPlayerForWeapon(actor.ModelId, actor.Costume, actor.WeaponId);
                BattleModel model = ResolvePlayerRenderWeapon(actor.ModelId, sharedWeapon, composed) ?? throw new InvalidDataException("A shared weapon has no admitted equipment geometry.");
                bool reduced = model.NativeWeaponUsesBodySkeleton == true;
                Dictionary<int, System.Collections.Immutable.ImmutableArray<byte>> replacements = [];
                if (model.NativeGeometryBytes is { } geometry)
                {
                    replacements[reduced ? 0 : 1] = geometry;
                }
                else if (reduced)
                {
                    throw new InvalidDataException("A shared weapon requires its rewritten native bone groups.");
                }
                if (model.NativeWeaponTexturePack is { } textures)
                {
                    replacements[reduced ? 4 : 6] = textures.Data;
                }
                if (!reduced && model.NativePoseSource is { } pose)
                {
                    replacements[0] = pose.Skeleton;
                    replacements[2] = pose.Animations;
                }
                if (replacements.Count != 0)
                {
                    equipment = ReplaceNativeDatSections(equipment, replacements);
                }
            }
            sources.Add(equipment with { ModelId = checked((byte)actor.ModelId) });
        }
        return resource with { BodyFileSelector129 = body, LinkedFileSelector128 = linked, ModelLoadSources = sources.ToImmutable() };
    }

    /// <summary>5077B0/507BF0's exact file ID selection from observed model BYTE129/128, without kernel-order inference.</summary>
    public NativeBattleModelDatSource ReadNativeModelDatSource(byte modelId, bool linked, byte observedSelector)
    {
        if (modelId >= 16 && !linked)
        {
            NativeBattleArchiveFile enemy = ReadNativeBattleFile(checked((ushort)(modelId + 150)));
            return ApplyNativeModelAnimationOverride(new(modelId, false, 0, checked((short)enemy.FileId), enemy.Name, enemy.Bytes));
        }
        if (modelId >= 11 || linked && modelId == 7)
        {
            throw new NotSupportedException("Native party file-table selection requires its original model0..10 and an existing linked callback.");
        }
        IGameNativeMemorySource memory = GameDataResources.ReadNativeMemory(this.resources);
        uint table = linked ? 0xB8B940u : 0xB8B914u;
        uint selectors = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(memory, table + modelId * 4u, 4).AsSpan());
        if (selectors == 0)
        {
            throw new NotSupportedException("Native party file selector points at an absent original table.");
        }
        short fileId = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleEffectStaticResourcesReader.Read(memory, checked(selectors + observedSelector * 2u), 2).AsSpan());
        if (fileId < 0)
        {
            throw new NotSupportedException("Native party file selector has no nonnegative original resource ID.");
        }
        NativeBattleArchiveFile file = ReadNativeBattleFile(checked((ushort)fileId));
        NativeBattleModelDatSource result = new(modelId, linked, observedSelector, fileId, file.Name, file.Bytes);
        _ = result.Section(0);
        return ApplyNativeModelAnimationOverride(result);
    }
}

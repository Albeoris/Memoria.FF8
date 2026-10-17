using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private NativeBattleGraphicsResource BindNativeModelLoadSources(NativeBattleGraphicsResource resource, BattleCombatantDefinition actor)
    {
        byte body = checked((byte)actor.Costume);
        byte? linked = actor.WeaponId is int weapon ? checked((byte)WeaponDefinitions.Single(item => item.Id == weapon).Tier) : null;
        System.Collections.Immutable.ImmutableArray<NativeBattleModelDatSource>.Builder sources = System.Collections.Immutable.ImmutableArray.CreateBuilder<NativeBattleModelDatSource>();
        sources.Add(ReadNativeModelDatSource(checked((byte)actor.ModelId), false, body));
        if (linked is byte selector && actor.ModelId != 7)
        {
            sources.Add(ReadNativeModelDatSource(checked((byte)actor.ModelId), true, selector));
        }
        return resource with { BodyFileSelector129 = body, LinkedFileSelector128 = linked, ModelLoadSources = sources.ToImmutable() };
    }

    /// <summary>5077B0/507BF0's exact file ID selection from observed model BYTE129/128, without kernel-order inference.</summary>
    public NativeBattleModelDatSource ReadNativeModelDatSource(byte modelId, bool linked, byte observedSelector)
    {
        if (modelId >= 16 && !linked)
        {
            NativeBattleArchiveFile enemy = ReadNativeBattleFile(checked((ushort)(modelId + 150)));
            return new(modelId, false, 0, checked((short)enemy.FileId), enemy.Name, enemy.Bytes);
        }
        if (modelId >= 11 || linked && modelId == 7)
        {
            throw new NotSupportedException("Native party file-table selection requires its original model0..10 and an existing linked callback.");
        }
        byte[] executable = File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        uint table = linked ? 0xB8B940u : 0xB8B914u;
        uint selectors = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, table + modelId * 4u, 4).AsSpan());
        if (selectors == 0)
        {
            throw new NotSupportedException("Native party file selector points at an absent original table.");
        }
        short fileId = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(selectors + observedSelector * 2u), 2).AsSpan());
        if (fileId < 0)
        {
            throw new NotSupportedException("Native party file selector has no nonnegative original resource ID.");
        }
        NativeBattleArchiveFile file = ReadNativeBattleFile(checked((ushort)fileId));
        NativeBattleModelDatSource result = new(modelId, linked, observedSelector, fileId, file.Name, file.Bytes);
        _ = result.Section(0);
        return result;
    }
}

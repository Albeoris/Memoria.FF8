using System.Buffers.Binary;
using System.Text;
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
        uint nameAddress = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, checked(0xB84CCC + (uint)fileId * 4), 4).AsSpan());
        byte[] nameBytes = NativeBattleEffectStaticResourcesReader.Read(executable, nameAddress, 128).ToArray();
        int end = Array.IndexOf(nameBytes, (byte)0);
        if (end <= 0)
        {
            throw new InvalidDataException("Native model file-name table lacks a bounded original name.");
        }
        string name = Encoding.ASCII.GetString(nameBytes, 0, end).ToLowerInvariant();
        NativeBattleModelDatSource result = new(modelId, linked, observedSelector, fileId, name, System.Collections.Immutable.ImmutableArray.CreateRange(Read(name)));
        _ = result.Section(0);
        return result;
    }
}

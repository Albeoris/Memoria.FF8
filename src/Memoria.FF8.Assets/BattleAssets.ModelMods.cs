using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private NativeBattleGraphicsResource BindNativeAnimationBankOverrides(NativeBattleGraphicsResource resource, BattleModel body, BattleModel? weapon, int character)
    {
        if (modelOverrides is null)
        {
            return resource;
        }
        List<NativeBattleAnimationBankOverride> replacements = [];
        Add(body, false);
        if (weapon is not null && weapon.NativeWeaponUsesBodySkeleton != true)
        {
            Add(weapon, true);
        }
        return resource with { AnimationBankOverrides = replacements.ToImmutableArray() };

        void Add(BattleModel owner, bool linked)
        {
            string modelId = "battle/" + owner.Name.ToLowerInvariant();
            foreach (string bank in modelOverrides.BattleBanks(modelId))
            {
                IGameNativeMemorySource memory = GameDataResources.ReadNativeMemory(resources);
                ModelBoundExternalBank? binding = ModelExternalBankCatalog.ForCharacter(character, NativeActions).SingleOrDefault(item => item.Descriptor.Bank == bank);
                byte[] bytes;
                uint? sourceDirectory = null;
                if (binding is not null)
                {
                    if (binding.UnsupportedReason is string unsupported)
                    {
                        throw new NotSupportedException(unsupported);
                    }
                    if (linked && binding.SectionCount != 3)
                    {
                        throw new NotSupportedException("This external bank has no independent linked weapon animation section.");
                    }
                    bytes = ModelAssetDiscoveryBindings.ReadExternalBank(memory, binding.Descriptor.Address, binding.SectionCount);
                    sourceDirectory = binding.Descriptor.Address;
                }
                else if (bank.StartsWith("victory-", StringComparison.Ordinal) && int.TryParse(bank.AsSpan(8), out int section))
                {
                    byte[] mapping = memory.Read(0xB8B7E0, 12);
                    if (!Enumerable.Range(0, 6).Any(index => mapping[index * 2] == character && mapping[index * 2 + 1] == section))
                    {
                        throw new NotSupportedException("The selected victory bank is not bound to this character.");
                    }
                    byte[] file = Read(Path.GetFileName(ModelAssetDiscoveryBindings.ResolveBattleFile(memory, 766)));
                    int count = BinaryPrimitives.ReadInt32LittleEndian(file);
                    if (count is < 1 or > 128 || section < 0 || section >= count || file.Length < 8 + count * 4)
                    {
                        throw new InvalidDataException("Victory section directory is out of bounds.");
                    }
                    int start = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(4 + section * 4));
                    int end = section + 1 < count ? BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(8 + section * 4)) : file.Length;
                    if (start < 8 + count * 4 || end < start || end > file.Length)
                    {
                        throw new InvalidDataException("Victory section extent is out of bounds.");
                    }
                    bytes = file[start..end];
                }
                else
                {
                    throw new NotSupportedException($"The bank '{bank}' has no implemented gameplay binding.");
                }
                BattleModel original = BattleResourceReader.ReadLinkedAnimationBank(owner with { EditableOverride = null }, bytes, linked);
                BattleModel replacement = modelOverrides.ApplyBattleBank(modelId, bank, original);
                NativeBattlePoseSource source = original.NativePoseSource ?? throw new NotSupportedException("An external override requires its actual native skeleton.");
                NativeBattlePoseSource changed = replacement.NativePoseSource ?? throw new NotSupportedException("An external override requires native animation tracks.");
                replacements.Add(new(bank, sourceDirectory, linked, Convert.ToHexString(SHA256.HashData(source.Animations.AsSpan())), changed.Animations));
            }
        }
    }

    private NativeBattleModelDatSource ApplyNativeModelAnimationOverride(NativeBattleModelDatSource source)
    {
        if (modelOverrides is null || !modelOverrides.HasModelOverride("battle/" + source.Name.ToLowerInvariant()))
        {
            return source;
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan());
        if (count is not (7 or 8 or 9 or 10 or 11))
        {
            return source;
        }
        BattleModel model = source.Linked ? modelOverrides.Apply("battle/" + source.Name.ToLowerInvariant(), BattleResourceReader.ReadModel(source.Name, source.Bytes.ToArray(), 6)) : LoadModel(source.Name);
        NativeBattlePoseSource pose = model.NativePoseSource ?? throw new NotSupportedException("A native model override requires its actual skeleton and animation bytes.");
        int start = source.Section(2);
        int end = source.Section(3);
        if (source.Bytes.AsSpan().Slice(start, end - start).SequenceEqual(pose.Animations.AsSpan()))
        {
            return source;
        }
        int change = pose.Animations.Length - (end - start);
        byte[] bytes = new byte[checked(source.Bytes.Length + change)];
        source.Bytes.AsSpan().Slice(0, start).CopyTo(bytes);
        pose.Animations.AsSpan().CopyTo(bytes.AsSpan(start));
        source.Bytes.AsSpan().Slice(end).CopyTo(bytes.AsSpan(start + pose.Animations.Length));
        for (int section = 3; section <= count; section++)
        {
            int offset = 4 + section * 4;
            int value = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(offset));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), checked(value + change));
        }
        return source with { Bytes = bytes.ToImmutableArray() };
    }

}

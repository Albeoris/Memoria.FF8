using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokuken334337ResourcesReader
{
    public static NativeBattleRenzokuken160Resources Read(byte[] executable, ushort effect)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == effect);
        (uint Animation, uint Outer, uint[] Sprites, uint[] Meshes, uint Colors, uint Lead, uint Axis, uint Offsets, uint LateAxis, uint[] ExtraSprites) spec = effect switch
        {
            334 => (0xE70EF4, 0xE71854, [0xCB979C, 0xCB9A6C, 0xCB9948], [0xCB9D24, 0xCBA70C, 0xCBB374, 0xE70D5C, 0xCBD48C, 0xCBE15C], 0xCC0340, 0xCC03B0, 0xCC03A0, 0, 0xCC03E0, [0xCB95F0]),
            335 => (0xE6C628, 0xE6CC38, [0xCB291C, 0xCB2BEC, 0xCB2AC8], [0xCB2EA4, 0xCB388C, 0xCB44F4, 0xE6C490], 0xCB94C0, 0xCB9530, 0xCB9520, 0xCB9560, 0xCB95D0, [0xCB2770]),
            336 => (0xE63CD4, 0xE640EC, [0xCABABC, 0xCABD8C, 0xCABC68], [0xCAC044, 0xCACA2C, 0xCAE7BC, 0xE63B3C, 0xCB047C, 0xCACCAC], 0xCB2660, 0xCB26D0, 0xCB26C0, 0xCB2700, 0, [0xCAB910]),
            337 => (0xE5D9A8, 0xE5DF6C, [0xCA4CAC, 0xCA4F7C, 0xCA4E58], [0xCA5234, 0xCA5C1C, 0xCA79AC, 0xE5D810, 0xCA6884, 0xCA5E9C], 0xCAB850, 0xCAB8C0, 0xCAB8B0, 0, 0xCAB8F0, [0xCA4B00]),
            _ => throw new NotSupportedException($"Native Renzokuken effect {effect} has no original late resource mapping.")
        };
        NativeBattleModuleResource common = NativeBattleModuleResourceReader.Read(executable).Single(resource => resource.Id == 3);
        int contacts = actor.Timeline.InputThresholds.Length;
        return new(new($"FF8_EN:{spec.Animation:X}", NativeBattleEffectStaticResourcesReader.Read(memory, spec.Animation, checked((int)(spec.Outer - spec.Animation)))), actor, spec.Sprites.Select(Sprite).ToImmutableArray(), Enumerable.Range(0, 24).Select(index => U32(spec.Colors + 4u * (uint)index)).ToImmutableArray(), Enumerable.Range(0, contacts).Select(index => unchecked((int)U32(spec.Lead + 4u * (uint)index))).ToImmutableArray(), spec.Meshes.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray(), U32(spec.Animation - 0x19C), U32(0xB8B7D8))
        {
            ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(spec.Axis + 4u * (uint)index))).ToImmutableArray(),
            StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(spec.Animation - 0x10 + 2u * (uint)index)).ToImmutableArray(),
            StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(spec.Animation - 0x20 + 2u * (uint)index)).ToImmutableArray(),
            StreakReferencePoint = Vector(0xB8B7F0),
            ContactOffsets = spec.Offsets == 0 ? null : Enumerable.Range(0, contacts).Select(index => Vector(spec.Offsets + 8u * (uint)index)).ToImmutableArray(),
            SpecialAlignmentAxis = spec.LateAxis == 0 ? [] : Enumerable.Range(0, 3).Select(index => unchecked((int)U32(spec.LateAxis + 4u * (uint)index))).ToImmutableArray(),
            SpecialSprites = spec.ExtraSprites.Select(Sprite).Append(new(common.Address, common.Bytes)).ToImmutableArray()
        };

        NativeBattleSearchSpriteResource Sprite(uint address) => new(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleVector Vector(uint address) => new(unchecked((short)U16(address)), unchecked((short)U16(address + 2)), unchecked((short)U16(address + 4)));
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleRenzokuken160Resources> nativeRenzokuken334337Resources = [];

    public NativeBattleRenzokuken160Resources ReadNativeRenzokuken334337Resources(ushort effect)
    {
        if (!nativeRenzokuken334337Resources.TryGetValue(effect, out NativeBattleRenzokuken160Resources? resource))
        {
            resource = NativeBattleRenzokuken334337ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), effect);
            nativeRenzokuken334337Resources.Add(effect, resource);
        }
        return resource;
    }
}

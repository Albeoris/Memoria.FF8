using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokukenLateSpecialResourcesReader
{
    public static NativeBattleRenzokuken160Resources Read(byte[] executable, ushort effect)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == effect);
        (uint Animation, uint Outer, uint[] Sprites, uint[] Meshes, uint Colors, uint Lead, uint Axis, uint Offsets, uint LateAxis, uint[] ExtraSprites) spec = effect switch
        {
            339 => (0xE55F10, 0xE56798, [0xC9DE9C, 0xC9E16C, 0xC9E048], [0xC9E424, 0xC9EE0C, 0xC9FA74, 0xE55D78, 0xCA1C2C], 0xCA4A40, 0xCA4AB0, 0xCA4AA0, 0, 0xCA4AE0, [0xC9DCF0]),
            340 => (0xE50D14, 0xE510FC, [0xC9702C, 0xC972FC, 0xC971D8], [0xC975B4, 0xC97F9C, 0xC99D2C, 0xE50B7C, 0xC9B9EC, 0xC98C04, 0xC9CF24], 0xC9DBD0, 0xC9DC40, 0xC9DC30, 0xC9DC70, 0xC9DCD0, [0xC96E80]),
            341 => (0xE4EBB4, 0xE4F038, [0xC9021C, 0xC904EC, 0xC903C8], [0xC907A4, 0xC9118C, 0xC91DF4, 0xE4EA1C], 0xC96DC0, 0xC96E30, 0xC96E20, 0, 0xC96E60, []),
            342 => (0xE47144, 0xE476F4, [0xC893AC, 0xC8967C, 0xC89558], [0xC89934, 0xC8A31C, 0xC8AF84, 0xE46FAC], 0xC8FF50, 0xC8FFC0, 0xC8FFB0, 0xC8FFF0, 0, []),
            343 => (0xE422E8, 0xE426C0, [0xC825AC, 0xC8287C, 0xC82758], [0xC82B34, 0xC8351C, 0xC852AC, 0xE42150], 0xC89150, 0xC891C0, 0xC891B0, 0, 0, [0xC82400]),
            _ => throw new NotSupportedException($"Native Renzokuken effect {effect} has no original late resource mapping.")
        };
        int contacts = actor.Timeline.InputThresholds.Length;
        return new(new($"FF8_EN:{spec.Animation:X}", NativeBattleEffectStaticResourcesReader.Read(memory, spec.Animation, checked((int)(spec.Outer - spec.Animation)))), actor, spec.Sprites.Select(Sprite).ToImmutableArray(), Enumerable.Range(0, 24).Select(index => U32(spec.Colors + 4u * (uint)index)).ToImmutableArray(), Enumerable.Range(0, contacts).Select(index => unchecked((int)U32(spec.Lead + 4u * (uint)index))).ToImmutableArray(), spec.Meshes.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray(), U32(spec.Animation - 0x19C), U32(0xB8B7D8))
        {
            ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(spec.Axis + 4u * (uint)index))).ToImmutableArray(),
            StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(spec.Animation - 0x10 + 2u * (uint)index)).ToImmutableArray(),
            StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(spec.Animation - 0x20 + 2u * (uint)index)).ToImmutableArray(),
            StreakReferencePoint = Vector(0xB8B7F0),
            ContactOffsets = spec.Offsets == 0 ? null : Enumerable.Range(0, contacts).Select(index => Vector(spec.Offsets + 8u * (uint)index)).ToImmutableArray(),
            SpecialAlignmentAxis = spec.LateAxis == 0 ? null : new(unchecked((int)U32(spec.LateAxis)), unchecked((int)U32(spec.LateAxis + 4)), unchecked((int)U32(spec.LateAxis + 8))),
            SpecialSprites = spec.ExtraSprites.Select(Sprite).ToImmutableArray()
        };

        NativeBattleSearchSpriteResource Sprite(uint address) => new(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleVector Vector(uint address) => new(unchecked((short)U16(address)), unchecked((short)U16(address + 2)), unchecked((short)U16(address + 4)));
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleRenzokuken160Resources> nativeRenzokukenLateSpecialResources = [];

    public NativeBattleRenzokuken160Resources ReadNativeRenzokukenLateSpecialResources(ushort effect)
    {
        if (!nativeRenzokukenLateSpecialResources.TryGetValue(effect, out NativeBattleRenzokuken160Resources? resource))
        {
            resource = NativeBattleRenzokukenLateSpecialResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), effect);
            nativeRenzokukenLateSpecialResources.Add(effect, resource);
        }
        return resource;
    }
}

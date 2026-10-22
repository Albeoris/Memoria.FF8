using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLevelDeath74Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleEffectOwnedModelResource Model, NativeBattleCuragaExecutableSource AuraNodes, NativeBattleCuragaExecutableSource DarkNodes, NativeBattleEffectHeapAllocation FirstTim, NativeBattleEffectHeapAllocation SecondTim, NativeBattleStaticMeshResource Spark, ImmutableArray<byte> SparkTables, NativeBattleModuleResource CasterResource, uint AppearanceSound, uint StrikeSound)
{
    public NativeBattleDeathResources FamilyResources()
    {
        Validate();
        return new(Animation, Model, AuraNodes.Data, DarkNodes.Data, FirstTim, SecondTim, Spark, SparkTables, AppearanceSound, StrikeSound) { Effect = 74, CasterResource = CasterResource };
    }

    public NativeBattleCuragaExecutableSource Nodes(uint address)
    {
        return address switch
        {
            0x1771F5C => AuraNodes,
            0x1776950 => DarkNodes,
            _ => throw new NotSupportedException("LV?Death requires one of its actual addressed node sources.")
        };
    }

    public void Validate()
    {
        if (Animation is null || Animation.Identity != "FF8_EN:1582184" || Animation.Data.IsDefault || Animation.Data.Length != 440 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != 440 || Model is null || Model.Address != 0x158233C || Model.Bytes.IsDefault || Model.Bytes.Length != 0x3FCC || Model.Geometry.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("LV?Death requires its complete original camera and effect-owned model.");
        }
        Model.PoseSource.Validate();
        ValidateNodes(AuraNodes, 0x1771F5C, 0x49F4);
        ValidateNodes(DarkNodes, 0x1776950, 0x25EC);
        if (FirstTim is null || FirstTim.Identity != "FF8_EN:1778F3C" || FirstTim.Data.IsDefault || FirstTim.Data.Length != 33824 || SecondTim is null || SecondTim.Identity != "FF8_EN:178135C" || SecondTim.Data.IsDefault || SecondTim.Data.Length != 16928 || Spark is null || Spark.Address != 0x1582074 || Spark.Bytes.IsDefaultOrEmpty || SparkTables.IsDefault || SparkTables.Length != 128 || AppearanceSound != 0x557B1 || StrikeSound != 0x5581D)
        {
            throw new InvalidDataException("LV?Death requires both original texture blocks, spark resources and sound values.");
        }
        ValidateTim(FirstTim.Data);
        ValidateTim(SecondTim.Data);
        if (CasterResource is null || CasterResource.Address != 0x15863B0 || CasterResource.Bytes.IsDefault || CasterResource.Bytes.Length != 0x1B74 || BinaryPrimitives.ReadUInt32LittleEndian(CasterResource.Bytes.AsSpan()) != 3 || !Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt32LittleEndian(CasterResource.Bytes.AsSpan().Slice(4 + index * 4))).SequenceEqual(new[] { 0x14, 0x1648, 0x1698, 0x1B74 }))
        {
            throw new InvalidDataException("LV?Death requires its complete original caster animation and sequence directory.");
        }
    }

    private static void ValidateTim(ImmutableArray<byte> bytes)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(4)) != 9)
        {
            throw new InvalidDataException("LV?Death requires its original indexed TIM format.");
        }
        int cursor = 8;
        for (int block = 0; block < 2; block++)
        {
            if (cursor > bytes.Length - 12)
            {
                throw new InvalidDataException("LV?Death TIM header leaves its complete original texture.");
            }
            ReadOnlySpan<byte> header = bytes.AsSpan().Slice(cursor);
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header));
            int x = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4));
            int y = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6));
            int width = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(8));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(10));
            if (width == 0 || height == 0 || x + width > 1024 || y + height > 512 || length != checked(12 + width * height * 2) || cursor > bytes.Length - length)
            {
                throw new InvalidDataException("LV?Death TIM rectangle leaves its original payload or VRAM bounds.");
            }
            cursor = checked(cursor + length);
        }
        if (cursor != bytes.Length)
        {
            throw new InvalidDataException("LV?Death requires both complete TIM blocks without an unowned tail.");
        }
    }

    private static void ValidateNodes(NativeBattleCuragaExecutableSource source, uint address, int length)
    {
        if (source is null || source.ResourceAddress != address || source.Data.IsDefault || source.Data.Length != length || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("LV?Death requires its complete original unrelocated node sources.");
        }
    }
}

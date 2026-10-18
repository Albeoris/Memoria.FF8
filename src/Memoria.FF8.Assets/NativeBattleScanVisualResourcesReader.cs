using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleScanVisualResourcesReader
{
    public static NativeBattleScanVisualResources Read(IGameNativeMemorySource memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        byte[] bytes = memory.Read(0x15D1898, 0x54);
        ImmutableArray<NativeBattleScanReticlePoint> points = Enumerable.Range(0, 21).Select(index => new NativeBattleScanReticlePoint(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4 + 2)))).ToImmutableArray();
        if (points[20] != new NativeBattleScanReticlePoint(short.MaxValue, 0))
        {
            throw new InvalidDataException("Native Scan trajectory lacks its original terminal record.");
        }
        uint[] meshes = [0x15CE93C, 0x15CE9B8, 0x15CDDEC, 0x15CE1D4, 0x15CF37C, 0x15CF5B4, 0x15CF6FC, 0x15CF784, 0x15CF80C, 0x15CF894, 0x15CF91C, 0x15CF9A4, 0x15CFA2C, 0x15CEA34, 0x15CF674, 0x15CF614];
        uint[] vertices = [0x15CFAB4, 0x15CFC3C, 0x15CFDC4, 0x15D02CC];
        (uint Address, int Length)[] tables = [(0x15D15A0, 0x24), (0x15D15C4, 0x24), (0x15D15E8, 0x48), (0x15D1630, 0x48), (0x15D1678, 0x50), (0x15D16C8, 0x50)];
        return new(points)
        {
            Sound15D0B3C = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x15D0B3C, 4)),
            MeshesByAddress = meshes.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)),
            VertexBanksByAddress = vertices.ToImmutableDictionary(address => address, ReadVertexBank),
            TablesByAddress = tables.ToImmutableDictionary(table => table.Address, table => memory.Read(table.Address, table.Length).ToImmutableArray()),
            LineArrays15D1718 = Enumerable.Range(0, 8).Select(index => memory.Read(checked(0x15D1718u + (uint)(index * 0x30)), 0x30).ToImmutableArray()).ToImmutableArray(),
            Sprite15D07D4 = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x15D07D4),
            Sprite15D0AF8 = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0x15D0AF8)
        };

        ImmutableArray<byte> ReadVertexBank(uint address)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(address + 4, 4));
            if (count < 0 || count > 4096)
            {
                throw new InvalidDataException("Native Scan source vertex bank exceeds its bounded original count.");
            }
            return memory.Read(address, checked(8 + count * 8)).ToImmutableArray();
        }
    }
}

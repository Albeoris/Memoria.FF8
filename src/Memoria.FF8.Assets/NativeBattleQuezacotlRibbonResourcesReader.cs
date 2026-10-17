using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleQuezacotlRibbonResourcesReader
{
    public static NativeBattleQuezacotlRibbonResources Read(byte[] executable)
    {
        ImmutableArray<byte> pairs = NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE7F0, 80);
        int maximum = 0;
        for (int index = 0; index < 20; index++)
        {
            int offset = index * 4;
            short start = BinaryPrimitives.ReadInt16LittleEndian(pairs.AsSpan().Slice(offset));
            short count = BinaryPrimitives.ReadInt16LittleEndian(pairs.AsSpan().Slice(offset + 2));
            if (start < 0 || count < 1)
            {
                throw new InvalidDataException("Original Quezacotl ribbon control directory is invalid.");
            }
            maximum = Math.Max(maximum, checked(start + count));
        }
        ImmutableArray<byte> squareRoot = NativeBattleEffectStaticResourcesReader.Read(executable, 0x12B4570, 512);
        ImmutableArray<byte> particleScale = NativeBattleEffectStaticResourcesReader.Read(executable, 0x12B4770, 8192);
        NativeBattleQuezacotlRibbonResources resources = new(pairs, NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AE840, checked(maximum * 24)), Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(squareRoot.AsSpan().Slice(index * 2))).ToImmutableArray(), NativeBattleEffectStaticResourcesReader.Read(executable, 0x12B05F0, 0x4000))
        {
            ParticleScaleWords4096 = Enumerable.Range(0, 4096).Select(index => BinaryPrimitives.ReadInt16LittleEndian(particleScale.AsSpan().Slice(index * 2))).ToImmutableArray(),
            SecondaryBank = new(0x12AF800, 0x12AF828, NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AF800, 40), NativeBattleEffectStaticResourcesReader.Read(executable, 0x12AF828, 65 * 24))
        };
        resources.Validate();
        return resources;
    }
}

using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStageCallbackTests
{
    [Fact]
    public void AllSupportedStageCallbacksMatchTheCompleteOriginalBodyAndExactCallTargets()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        using PEReader reader = new(new MemoryStream(executable));
        byte[] staticTemplate = Bytes(0x51B0A0, 35);
        byte[] textureTemplate = Bytes(0x519540, 45);
        int statics = 0;
        int textures = 0;
        int unsupported = 0;
        for (int stage = 0; stage < 163; stage++)
        {
            NativeBattleStageCallback callback = NativeBattleStageCallbackReader.Read(stage);
            if (callback.UpdateTexture0 is not bool texture)
            {
                unsupported++;
                continue;
            }
            byte[] template = texture ? textureTemplate : staticTemplate;
            byte[] actual = Bytes(callback.Address, template.Length);
            (int Offset, uint Target)[] calls = texture ? [(21, 0x51B0D0), (29, 0x500FD0)] : [(19, 0x500FD0)];
            for (int index = 0; index < template.Length; index++)
            {
                if (!calls.Any(call => index > call.Offset && index < call.Offset + 5))
                {
                    Assert.Equal(template[index], actual[index]);
                }
            }
            foreach ((int offset, uint target) in calls)
            {
                Assert.Equal(target, unchecked(callback.Address + (uint)offset + 5u + (uint)BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(offset + 1))));
            }
            if (texture)
            {
                textures++;
            }
            else
            {
                statics++;
            }
        }
        Assert.Equal(140, statics);
        Assert.Equal(9, textures);
        Assert.Equal(14, unsupported);
        for (int stage = 0; stage < 163; stage++)
        {
            NativeBattleStageColorInitializer initializer = NativeBattleStageColorsReader.Read(stage);
            if (initializer.Colors is not { } colors)
            {
                Assert.True(stage is 37 or 38);
                Assert.True(initializer.Source44 is 0x1D9C108 or 0x1D9C0D4);
                continue;
            }
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(Bytes(initializer.Source40, 4)), colors.Color40);
            Assert.Equal((BinaryPrimitives.ReadUInt32LittleEndian(Bytes(initializer.Source44, 4)) & 0xFFFFFF) | 0x32000000u, colors.Color44);
        }
        Assert.Equal(new NativeBattleStageColors(0x00464B6E, 0x32464646), NativeBattleStageColorsReader.Read(31).Colors);
        Assert.Equal(NativeBattleStageState.FromColdProcess().ModelColors!.Color40, BinaryPrimitives.ReadUInt32LittleEndian(Bytes(0xB8B7D8, 4)));
        Assert.Equal(NativeBattleStageState.FromColdProcess().ModelColors!.Color44, BinaryPrimitives.ReadUInt32LittleEndian(Bytes(0xB8B9A8, 4)));

        byte[] Bytes(uint address, int length)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + length <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), length).ToArray();
        }
    }
}

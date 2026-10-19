using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGuestCpuAddressTests
{
    [Fact]
    public void OriginalPartyBankPointerAndColdResourceTableAuthenticateCopiedDwords()
    {
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe")));
        uint party = BinaryPrimitives.ReadUInt32LittleEndian(original.Read(0xB6D084, 4));
        Assert.Equal(0x196B018u, party);
        for (byte bank = 0; bank < 3; bank++)
        {
            Assert.Equal(party + 0x10000u * bank + 40, new NativeBattleModelCpuReference(bank, 40).PhysicalAddress);
        }
        Assert.Equal(new byte[11 * 52], original.ReadLoader(0x1D99768, 11 * 52));
        Assert.Null(NativeBattleModelCpuResources.FromColdProcess().Rows[2].Fields[44]);
        foreach (uint callback in new uint[] { 0xA8B230, 0x960270, 0x955350, 0x94B470 })
        {
            Assert.Equal(original.Read(0xB2BB40, 8), original.Read(callback, 8));
        }
    }
}

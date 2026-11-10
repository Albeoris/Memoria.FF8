using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7BattleAllTests
{
    [Fact]
    public void OriginalLinkedSlotsGrantAndAccumulateIndependentSpellUses()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original resources for FFVII All materia checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));

        byte[] native = new byte[132];
        native.AsSpan(0x40, 0x40).Fill(byte.MaxValue);
        Ff7CharacterState cloud = new(0, "Cloud", 6, 0, 314, 54, native);
        native[0x1C] = 0;
        native[0x1D] = 1;
        Equip(0x40, 53, 2500);
        Equip(0x44, 23, 0);
        Equip(0x60, 23, 1500);
        Equip(0x64, 53, 0);
        Assert.Equal(new KeyValuePair<byte, int>[] { new(0, 1), new(1, 1) }, Ff7NativeBattleAll.Read(cloud, kernel).OrderBy(pair => pair.Key));

        native[0x1D] = 7;
        Assert.Equal(new KeyValuePair<byte, int>[] { new(0, 3), new(1, 1) }, Ff7NativeBattleAll.Read(cloud, kernel).OrderBy(pair => pair.Key));

        Equip(0x40, 54, 55000);
        Assert.Equal(new KeyValuePair<byte, int>[] { new(0, 2), new(4, 1), new(5, 1) }, Ff7NativeBattleAll.Read(cloud, kernel).OrderBy(pair => pair.Key));

        void Equip(int offset, byte materia, uint ap)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(native.AsSpan(offset), materia | ap << 8);
        }
    }
}

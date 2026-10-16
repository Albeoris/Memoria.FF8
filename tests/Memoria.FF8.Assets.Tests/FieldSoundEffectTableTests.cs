using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldSoundEffectTableTests
{
    [Fact]
    public void Table_retains_unsigned_DWORD_identity_order_and_original_hash()
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 10228);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 10069);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), uint.MaxValue);
        FieldSoundEffectTable table = FieldSoundEffectTable.Read("generic.sfx", bytes);
        Assert.Equal(new uint[] { 10228, 10069, uint.MaxValue }, table.EffectIds);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), table.Sha256);
        bytes[0] = 0;
        Assert.Equal(10228u, table.EffectIds[0]);
        Assert.Throws<InvalidDataException>(() => FieldSoundEffectTable.Read("bad.sfx", [1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => FieldSoundEffectTable.Read("bad.bin", []));
    }

    [Fact]
    public void Actual_chamber_table_selects_original_effect_IDs_without_audio_payload()
    {
        string gameDirectory = Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY") ?? TestResources.GameDirectory;
        using GameAssets assets = new(gameDirectory);
        FieldSoundEffectTable table = assets.LoadFieldSoundEffects(130);
        Assert.Equal("bdifrit1.sfx", table.MemberName);
        Assert.Equal("F20528987E221E8D0B02FF1D83F977FA02E5940260E2BF4519151F118CE8B354", table.Sha256);
        Assert.Equal(new uint[] { 10228, 10069 }, table.EffectIds);
        Assert.Same(table, assets.LoadFieldSoundEffects(130));
    }
}

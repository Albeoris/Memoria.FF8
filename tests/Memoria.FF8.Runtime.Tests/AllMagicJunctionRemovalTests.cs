using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class AllMagicJunctionRemovalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Removal_clears_every_physical_record_and_preserves_everything_except_magic_junctions_and_hp(bool shared, bool infinite)
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        for (int row = 0; row < 8; row++)
        {
            source = MagicSystemFixture.Stock(source, row, row + 1, 100);
        }
        source = MagicSystemFixture.Modes(source, shared, infinite);
        for (int row = 0; row < 8; row++)
        {
            NativeCharacterVital record = source.Records[row];
            source.Records[row] = record with
            {
                HitPoints = row == 0 ? new NativeHpWord(0, 0) : row == 1 ? new NativeHpWord(5, 90) : new NativeHpWord(90, 90),
                MemberFlags = row >= 3 ? (ushort)0 : record.MemberFlags,
                Status = (ushort)(row * 4),
                Junction = record.Junction with { Commands = 0x01020304, Abilities = 0x05060708, MagicJunctions = Enumerable.Repeat((byte)(row + 1), 20).ToArray() }
            };
            Assert.True(source.GetMagicMaximumHitPoints(row) > 90);
        }
        string original = JsonSerializer.Serialize(source);
        NativeCharacterVitals result = source.RemoveAllMagicJunctions();
        NativeCharacterVital[] expected = source.Records.Select((record, row) => record with
        {
            Junction = record.Junction with { MagicJunctions = new byte[20] },
            HitPoints = row == 0 ? new NativeHpWord(0, 0) : row == 1 ? new NativeHpWord(5, 10) : new NativeHpWord(10, 10)
        }).ToArray();

        Assert.Equal(JsonSerializer.Serialize(source with { Records = expected }), JsonSerializer.Serialize(result));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.All(result.Records, record => Assert.All(record.Junction.MagicJunctions, spell => Assert.Equal((byte)0, spell)));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(result.RemoveAllMagicJunctions()));
        result.Records[7].Junction.MagicJunctions[19] = 56;
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }
}

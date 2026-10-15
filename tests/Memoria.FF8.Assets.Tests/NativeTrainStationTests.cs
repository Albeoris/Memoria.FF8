using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTrainStationTests
{
    [Theory]
    [InlineData(0, -28338, -23747, -794, "4E91FFFF3DA3FFFFE6FCE10D")]
    [InlineData(1, -20474, -42712, -555, "06B0FFFF2859FFFFD5FD8204")]
    [InlineData(2, -51438, -12555, -566, "1237FFFFF5CEFFFFCAFD0C0F")]
    public void Original20402dcStationRecordsPreserveRawZSignedHeightAndUnusedTail(int index, int x, int rawZ, short height, string bytes)
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldTrainStation station = assets.ReadTrainStation(index);
        Assert.Same(station, assets.ReadTrainStation(index));
        Assert.Equal(x, station.X);
        Assert.Equal(rawZ, station.RawZ);
        Assert.Equal(height, station.NativeHeight);
        Assert.Equal(bytes, Convert.ToHexString(station.Bytes.AsSpan()));
        NativeWorldTrainStation restored = JsonSerializer.Deserialize<NativeWorldTrainStation>(JsonSerializer.Serialize(station))!;
        restored.Validate();
        Assert.Equal(station.SectionHash, restored.SectionHash);
        Assert.Equal<byte>(station.Bytes, restored.Bytes);
        Assert.Throws<InvalidDataException>(() => (station with { Bytes = ImmutableArray<byte>.Empty }).Validate());
    }

    [Fact]
    public void AbsentStationRecordsRefuseBeforeReadingTheNextSection()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.ReadTrainStation(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.ReadTrainStation(3));
    }
}

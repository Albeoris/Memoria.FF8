using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterMeshColorTests
{
    [Theory]
    [InlineData(127, 1)]
    [InlineData(120, 0)]
    [InlineData(124, 1)]
    [InlineData(116, 0)]
    [InlineData(252, 2)]
    public void RoundEvenPrecedesFourBitShift(int channel, int expected)
    {
        uint material = 0x3C000000u | (uint)channel;
        Assert.Equal(0x3C000000u | (uint)expected, NativeBattleWaterMeshColor.Modulate4601B0(material, 1));
    }

    [Fact]
    public void EveryBoundedChannelPairMatchesIndependentNearestEvenConversion()
    {
        for (uint channel = 0; channel <= 255; channel++)
        {
            for (uint modulation = 0; modulation <= 255; modulation++)
            {
                int expected = Math.Clamp((int)Math.Round(channel * modulation / 8d, MidpointRounding.ToEven) >> 4, 0, 255);
                uint packed = NativeBattleWaterMeshColor.Modulate4601B0(0x2A000000u | channel * 0x010101u, 0xFF000000u | modulation * 0x010101u);
                Assert.Equal(0x2A000000u | (uint)expected * 0x010101u, packed);
            }
        }
        Assert.Equal(0x3CFFFFFFu, NativeBattleWaterMeshColor.Modulate4601B0(0x3CFFFFFF, 0x00FFFFFF));
        Assert.Equal(0xFF000000u, NativeBattleWaterMeshColor.Modulate4601B0(0xFF123456, 0));
        Assert.Equal(0x209E1E01u, NativeBattleWaterMeshColor.Modulate4601B0(0x20A07F7F, 0x007F1F01));
    }

    [Fact]
    public void PreparationWritesOnlyFiveCompleteDwordsAndPreservesOtherUnknownBytes()
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.Unknown(), NativeBattleEffectArena.Clear(), new(0x1D9A000, Enumerable.Repeat((byte)0xA5, 0x180).ToImmutableArray(), new bool[0x180].ToImmutableArray()), new(123));
        frame = frame.WriteTemporaryDword(0x84, 0x02AB3412);
        NativeBattleWaterMeshColorSetup setup = NativeBattleWaterMeshColor.PrepareA9E8A0(frame);
        Assert.Equal(0x02AB3412u, setup.Modulation);
        Assert.Equal(0x10001000u, setup.Frame.ReadTemporaryDword(0xE0));
        Assert.Equal(0x1000u, setup.Frame.ReadTemporaryDword(0xE4));
        Assert.Equal(0u, setup.Frame.ReadTemporaryDword(0xE8));
        Assert.Equal(0x1560u, setup.Frame.ReadTemporaryDword(0xEC));
        Assert.Equal(0u, setup.Frame.ReadTemporaryDword(0xF0));
        for (int index = 0; index < 0x180; index++)
        {
            if (index is >= 0xE0 and < 0xF4)
            {
                Assert.True(setup.Frame.TemporaryFrame.Known[index]);
            }
            else
            {
                Assert.Equal(frame.TemporaryFrame.Known[index], setup.Frame.TemporaryFrame.Known[index]);
                Assert.Equal(frame.TemporaryFrame.Bytes[index], setup.Frame.TemporaryFrame.Bytes[index]);
            }
        }
        Assert.Same(frame.Core, setup.Frame.Core);
        Assert.Same(frame.Arena, setup.Frame.Arena);
        Assert.Equal(frame.Random, setup.Frame.Random);
    }

    [Fact]
    public void JsonPreservesPreparedContextAndMissingModulationIsNotHydrated()
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x1D9A000, new byte[0x180].ToImmutableArray(), new bool[0x180].ToImmutableArray()), new(0));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterMeshColor.PrepareA9E8A0(frame));
        NativeBattleWaterMeshColorSetup setup = NativeBattleWaterMeshColor.PrepareA9E8A0(frame.WriteTemporaryDword(0x84, 0x027F017F));
        NativeBattleWaterMeshColorSetup restored = JsonSerializer.Deserialize<NativeBattleWaterMeshColorSetup>(JsonSerializer.Serialize(setup))!;
        Assert.Equal(JsonSerializer.Serialize(setup), JsonSerializer.Serialize(restored));
        Assert.Equal(NativeBattleWaterMeshColor.Modulate4601B0(0x30ABCDEF, setup.Modulation), NativeBattleWaterMeshColor.Modulate4601B0(0x30ABCDEF, restored.Modulation));
    }
}

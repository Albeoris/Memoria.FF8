using System.Collections.Immutable;
using System.Globalization;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFuelMessageTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(99)]
    [InlineData(255)]
    public void InstalledFuelMessagesBindAndRunTheirExpandedNativeText(int remaining)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        byte count = checked((byte)remaining);
        byte[] bytes = assets.ReadFuelMessageBytes(count);
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)0, bytes[^1]);
        Assert.DoesNotContain((byte)0, bytes[..^1]);
        Assert.DoesNotContain((byte)1, bytes);
        Assert.DoesNotContain((byte)7, bytes);
        Assert.DoesNotContain((byte)13, bytes);
        string decoded = FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetString(bytes.AsSpan(0, bytes.Length - 1));
        Assert.Equal(decoded, assets.ReadFuelMessage(count));
        Assert.False(string.IsNullOrWhiteSpace(decoded));

        NativeWorldMessage binding = assets.ReadNativeFuelMessage(count);
        NativeWorldMessage originalWindow = assets.ReadNativeWorldMessage(1, 43);
        Assert.Equal(originalWindow with { MessageId = count == 0 ? (byte)43 : (byte)254, FuelRemaining = count == 0 ? null : count }, binding);
        binding.Validate();
        if (count == 0)
        {
            byte[] original = assets.ReadWorldMessageBytes(43);
            Assert.Contains((byte)13, original);
            List<byte> expanded = [];
            for (int index = 0; index < original.Length; index++)
            {
                if (original[index] == 13)
                {
                    Assert.Equal((byte)194, original[++index]);
                    expanded.AddRange(FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("Fuel"));
                }
                else
                {
                    expanded.Add(original[index]);
                }
            }
            Assert.Equal(expanded.ToArray(), bytes);
            Assert.Contains("Fuel", decoded);
            Assert.Null(binding.FuelRemaining);
        }
        else
        {
            Assert.Contains("Fuel", decoded);
            Assert.Contains(count.ToString(CultureInfo.InvariantCulture), decoded);
            string template = assets.ReadFuelMessage(1);
            Assert.Contains("1", template);
            Assert.Equal(template.Replace("1", count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), decoded);
            Assert.Equal(count, binding.FuelRemaining);
        }

        NativeWorldTextState text = NativeWorldTextState.Create(ImmutableArray.CreateRange(bytes), 0);
        for (int tick = 0; tick < 1024 && !text.TextComplete; tick++)
        {
            text = text.Advance();
        }
        Assert.True(text.TextComplete);
        Assert.Equal(bytes.Length - 1, text.Position);
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(binding with { Text = NativeWorldTextState.Create(ImmutableArray.CreateRange(bytes), 0), TextComplete = false });
        ui.Validate();
    }
}

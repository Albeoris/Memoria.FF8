using System.Collections.Immutable;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeItemOrderAliasTests
{
    [Fact]
    public void Signed_item_ids_read_current_canonical_shop_and_input_bytes()
    {
        byte[] shops = new byte[400];
        shops[339] = 17;
        shops[399] = 31;
        GameState state = new()
        {
            ShopHistory = new(Hash, shops.ToImmutableArray()),
            InputConfiguration = new(Hash, 0xA5020001, Enumerable.Range(1, 12).Select(value => (byte)value).ToImmutableArray()) { NativeBattleSpeedByte = 33, BattleMessageSpeed = 22, FieldMessageSpeedByte = 11, SoundVolume = 100 }
        };
        NativeItemOrderAliases aliases = NativeItemOrderAliases.From(state);
        Assert.Equal((byte)17, aliases.Read(128));
        Assert.Equal((byte)31, aliases.Read(188));
        Assert.Equal((byte)33, aliases.Read(189));
        Assert.Equal((byte)22, aliases.Read(190));
        Assert.Equal((byte)11, aliases.Read(191));
        Assert.Equal((byte)100, aliases.Read(192));
        Assert.Equal(new byte[] { 1, 0, 2, 0xA5, 1, 2 }, Enumerable.Range(193, 6).Select(aliases.Read));
        Assert.Equal((byte)17, state.ShopHistory.Bytes[339]);
    }

    [Fact]
    public void High_item_mask_uses_x86_low_five_shift_bits_and_does_not_mutate_sources()
    {
        NativeEconomy source = Economy();
        source.Items[0] = new(128, 1);
        byte[] shops = new byte[400];
        shops[339] = 33;
        NativeItemOrderAliases aliases = NativeItemOrderAliases.From(new GameState { ShopHistory = new(Hash, shops.ToImmutableArray()) });
        NativeEconomy result = source.UpdateItemOrder(2, aliases);
        Assert.Equal((byte)0, result.ItemOrder![1]);
        Assert.Equal((byte)1, result.ItemOrder[0]);
        Assert.Equal((byte)1, source.ItemOrder![1]);
        Assert.Equal(source.Items, result.Items);
    }

    [Fact]
    public void Fully_used_native_mask_completes_without_an_order_write()
    {
        NativeEconomy source = Economy();
        for (int index = 0; index < 31; index++)
        {
            source.Items[index] = new((byte)(index + 1), 1);
        }
        source.Items[31] = new(128, 1);
        byte[] shops = new byte[400];
        shops[339] = 31;
        NativeItemOrderAliases aliases = NativeItemOrderAliases.From(new GameState { ShopHistory = new(Hash, shops.ToImmutableArray()) });
        Assert.Same(source, source.UpdateItemOrder(32, aliases));
    }

    [Fact]
    public void Unknown_canonical_bytes_and_invalid_native_item_domain_fail_only_when_reached()
    {
        NativeEconomy source = Economy();
        source.Items[0] = new(128, 1);
        NativeItemOrderAliases missing = NativeItemOrderAliases.From(new GameState());
        Assert.Throws<NotSupportedException>(() => source.UpdateItemOrder(1, missing));
        source.Items[0] = new(199, 1);
        Assert.Throws<NotSupportedException>(() => source.UpdateItemOrder(1, missing));
        source.Items[1] = new(1, 1);
        Assert.Same(source, source.UpdateItemOrder(1, missing));
    }

    private static string Hash => NativeInputConfiguration.OriginalInitHash;

    private static NativeEconomy Economy()
    {
        return new()
        {
            Version = 1,
            Rule = NativeEconomy.InitializationRule,
            InitOutSha256 = Hash,
            Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(),
            ItemOrder = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(),
            Gil = 0,
            PastGil = 0,
            EventBankGil72 = 0,
            EventBankPastGil76 = 0
        };
    }
}

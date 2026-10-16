using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePlayerTextureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InstalledSquallBodyRetainsItsOriginalTextureMotionTableAndRelocatesToThePhysicalPartySlot(int physicalSlot)
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "player", IsPlayer = true, ModelId = 0, WeaponId = 0 });
        Assert.NotNull(resource.TextureAnimations);
        Assert.NotNull(resource.BodyTexturePack);
        ushort mask = (ushort)(3 << (2 * physicalSlot));
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(resource.BodyTexturePack!, unchecked((ushort)~mask));
        Assert.NotEmpty(load.TimOffsets);
        Assert.Equal(mask, load.AllocatedSlots);
        Assert.Equal((byte?)(2 * physicalSlot), load.Binding.PaletteRows[0]);
        NativeBattleEffectTextureState uploaded = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
        Assert.Contains(uploaded.ReadVram(), value => value != 0);
        NativeBattleTextureCopy copy = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(resource.TextureAnimations, mask, 0, 1));
        byte[] before = uploaded.ReadVram();
        NativeBattleEffectTextureState copied = uploaded.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20().State;
        byte[] after = copied.ReadVram();
        for (int row = 0; row < copy.Height; row++)
        {
            Assert.Equal(before.AsSpan(2 * ((copy.SourceY + row) * 1024 + copy.SourceX), copy.Width * 2).ToArray(), after.AsSpan(2 * ((copy.DestinationY + row) * 1024 + copy.DestinationX), copy.Width * 2).ToArray());
        }
        Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(load.Source.Data.AsSpan().Slice(load.TimOffsets[0])));
    }
}

using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRegionalCinematicResourcesTests
{
    [Theory]
    [InlineData(234, 3, 0xA488F0u, 0xA488C0u, 0x27969E8u, 0x2796A00u, 30, 0xA49C20u)]
    [InlineData(235, 6, 0xA3E370u, 0xA3E340u, 0x27969B0u, 0x27969C8u, 32, 0xA3F700u)]
    public void Regional_profiles_read_their_actual_tables_and_keep_their_actor_draw_variant(int effect, int textures, uint entry, uint initializer, uint queue, uint bank, int draw, uint handler)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        Assert.Equal(entry, profile.Entry);
        Assert.Equal(initializer, profile.Initializer);
        Assert.Equal(queue, profile.QueueAddress);
        Assert.Equal(bank, profile.BankAddress);
        Assert.Equal(textures, profile.TextureCount);
        Assert.False(profile.Lit);
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(profile.InitializationAddress));
        NativeBattleCinematicResources resources = assets.ReadNativeCinematicResources(effect);
        resources.Validate();
        Assert.Null(resources.Vibration);
        Assert.Equal(handler, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(profile.DrawTable + (uint)(4 * draw), 4)));
        Assert.Equal(handler, NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", draw, handler));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, "draw_table", draw + 1, handler));
        foreach (int opcode in new[] { 0, 2, 3, 0x30, 0x39, 0x47, 0x4B, 0x81, 0x82, 0x9B, 0xA1, 0xA4, 0xAC, 0xB2, 0xFB })
        {
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(profile.InstructionTable + (uint)(4 * opcode), 4));
            Assert.NotEqual(0u, NativeBattleCinematicDispatch.RequireSlot(effect, "vm_table", opcode, selected));
        }
        Assert.Equal(new[] { 0x16B, 0x16C, 0x16D }, resources.Files.Select(file => (int)file.FileId).Order().ToArray());
    }
}

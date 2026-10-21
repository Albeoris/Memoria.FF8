using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStatusCureItemResourcesTests
{
    [Theory]
    [InlineData((ushort)292, 0x22B0, 300173u, "848F18CF55EDB760DCF4F269CF560430AC8D147DA3771336571C96F51F5DC866")]
    [InlineData((ushort)293, 0x1984, 300179u, "C08CEEAABA5A9289EAAAD1653B651BC8CB216E5AF5600901510D3D78BB3478F6")]
    [InlineData((ushort)294, 0x2B4C, 300167u, "F3601987AE6139332CD2918A6818176AF91644C42F958E0F1B6ACBF2ABCCB62B")]
    [InlineData((ushort)295, 0x3180, 300197u, "3FD94C634CB7417A32884141A56145A72258B4780AA960DCB1787FDFCEF6FC0E")]
    [InlineData((ushort)296, 0x200C, 300164u, "9AC83AB1812E0067ED29669E53BDF30D22C65F0424E5BBA3DE23A9C0828F545F")]
    public void Original_item_ACT_source_initializes_and_advances_its_own_workspace_to_completion(ushort effect, int length, uint sound, string hash)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(effect);
        NativeBattleStatusCureItemResources resources = game.ReadNativeStatusCureItemResources(effect);
        Assert.Same(resources, game.ReadNativeStatusCureItemResources(effect));
        Assert.Equal(profile.SourceAddress, resources.Layout.Address);
        Assert.Equal(length, resources.Layout.Data.Length);
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(resources.Layout.Data.AsSpan())));
        Assert.Equal(sound, resources.Sound);
        byte[] executable = File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe"));
        NativeBattleEffectBinding binding = NativeBattleEffectCatalogReader.Read(executable)[effect - 1];
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource(profile.TextureName, profile.TextureGlobal), binding.InitializerResource);
        int table = BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(4));
        int duration = BinaryPrimitives.ReadInt32LittleEndian(resources.Layout.Data.AsSpan().Slice(table + 4));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[profile.WorkspaceBytes].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Layout, profile.WorkspaceBytes);
        int nodes = 0;
        int active = 0;
        while (true)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.Layout, 0, (current, _) =>
            {
                nodes++;
                return current;
            });
            workspace = step.Workspace;
            active++;
            Assert.True(active <= duration);
            if (step.Remaining == 0)
            {
                break;
            }
        }
        Assert.Equal(duration, active);
        Assert.Equal(duration, workspace.Frame);
        Assert.True(nodes > 0);
    }
}

using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDuel212ResourcesTests
{
    [Fact]
    public void OriginalBurningRaveRetainsItsActorAndAllReachedGeometry()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleDuel212Resources resources = game.ReadNativeDuel212Resources();
        resources.Validate();
        Assert.Equal(0x1350, resources.ReadActorDat().Length);
        Assert.Equal(2u, resources.Dword(0x1181078));
        Assert.Equal(new short[] { 360, 361 }, resources.Files.Select(file => file.FileId));
        Assert.All(resources.Files, file => Assert.False(file.Data.IsDefaultOrEmpty));
        NativeBattleThundaga105Bounds first = NativeBattleDuel212Preparation.Bounds69A700(resources, checked(0x118BA10u + resources.Dword(0x118BA18)));
        NativeBattleThundaga105Bounds second = NativeBattleDuel212Preparation.Bounds69A700(resources, checked(0x118BA10u + resources.Dword(0x118BA1C)));
        Assert.True(first.Maximum.X > first.Minimum.X);
        Assert.True(first.Maximum.Z > first.Minimum.Z);
        Assert.True(second.Maximum.X > first.Minimum.X);
        Assert.Equal(new uint[] { 0x11826E0, 0x1183FA4, 0x1184174, 0x11843DC, 0x1184694 }, resources.Meshes.Select(mesh => mesh.Address));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        (textures, _) = textures.EnqueueTim505E30(resources.InitialTim);
        textures.Validate();
        foreach ((uint address, int size) in new (uint, int)[] { (0x11883AC, 400), (0x1187F2C, 68), (0x1184ACC, 560), (0x118BA10, 1156) })
        {
            NativeBattleGfAnimationSource source = new(address, resources.Read(address, checked((int)(NativeBattleDuel212Resources.EndAddress - address))));
            NativeBattleGfAnimationWorkspace workspace = new(System.Collections.Immutable.ImmutableArray.CreateRange(new byte[size]));
            workspace = workspace.Initialize7016B0(source, size);
            Assert.Equal(address, workspace.ResourceAddress);
        }
    }
}

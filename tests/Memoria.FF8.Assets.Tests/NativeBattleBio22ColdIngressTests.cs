using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBio22ColdIngressTests
{
    [Fact]
    public void Authenticated_cold_PE_ingress_seeds_each_physical_mesh_once_and_rejects_exchanged_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectMemory memory = Assert.IsType<NativeBattleEffectMemory>(game.ReadColdProcessNativeBattleMemory().EffectMemory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        Assert.Equal(resources.MeshBaseline.Bytes.ToArray(), memory.Bio22Geometry!.Bytes.ToArray());
        Assert.Equal(resources.SecondMeshBaseline.Bytes.ToArray(), memory.Bio22SecondaryGeometry!.Bytes.ToArray());
        Assert.Equal(resources.ThirdMeshBaseline.Bytes.ToArray(), memory.Bio22ThirdGeometry!.Bytes.ToArray());
        Assert.Equal(resources.FourthMeshBaseline.Bytes.ToArray(), memory.Bio22FourthGeometry!.Bytes.ToArray());
        memory.Validate();
        Assert.Throws<InvalidDataException>(() => (memory with { Bio22Geometry = memory.Bio22SecondaryGeometry }).Validate());
        Assert.Throws<InvalidDataException>(() => (memory with { Bio22ThirdGeometry = memory.Bio22FourthGeometry }).Validate());
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().Bio22Geometry);
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().Bio22FourthGeometry);
    }
}

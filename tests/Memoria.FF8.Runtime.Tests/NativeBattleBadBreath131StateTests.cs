using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath131StateTests
{
    [Fact]
    public void Cold_globals_are_loader_zero_but_mutable_mesh_seeds_are_not_fabricated()
    {
        NativeBattleBadBreath131State state = NativeBattleBadBreath131State.FromColdProcess();
        state.Validate();
        Assert.True(state.PendingColdSources);
        foreach (uint address in new uint[] { 0x2521010, 0x2521038, 0x252103C, 0x2521040, 0x252105C, 0x2521060 })
        {
            Assert.Equal(0u, state.ReadDword(address));
        }
        Assert.Null(state.MeshA);
        Assert.Null(state.MeshB);
        Assert.Throws<NotSupportedException>(() => state.ReadMesh(NativeBattleBadBreath131Profile.MeshAAddress));
    }

    [Theory]
    [InlineData(0x2521014u)]
    [InlineData(0x2521044u)]
    [InlineData(0x2521048u)]
    [InlineData(0x2521064u)]
    [InlineData(0x2521068u)]
    [InlineData(0x2521098u)]
    public void Scalar_owner_excludes_unused_intervals_root_tasks_and_all_descriptors(uint address)
    {
        NativeBattleBadBreath131State state = NativeBattleBadBreath131State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 123));
    }

    [Fact]
    public void Unknown_import_and_partial_scalar_writes_retain_unknowns_after_JSON()
    {
        NativeBattleBadBreath131State state = NativeBattleBadBreath131State.FromUnknown().WriteWord(0x2521060, 0x1234);
        Assert.Null(state.PendingColdSources);
        Assert.Equal((ushort)0x1234, state.ReadWord(0x2521060));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x2521060));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(0x2521038));
        NativeBattleBadBreath131State restored = JsonSerializer.Deserialize<NativeBattleBadBreath131State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(0x2521060));
    }
}

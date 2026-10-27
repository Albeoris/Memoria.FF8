using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRush91MemoryTests
{
    [Fact]
    public void Cold_static_owners_are_zero_and_warm_bytes_survive_module_loads_and_JSON()
    {
        NativeBattleEffectMemory cold = NativeBattleEffectMemory.FromColdProcess();
        Assert.Equal(0x5A0, cold.Rush91MorphScratch!.Value.Length);
        Assert.All(cold.Rush91MorphScratch.Value, value => Assert.Equal(0, value));
        Assert.All(cold.FullscreenOverlay!.Read(0, 108), value => Assert.Equal(0, value));
        ImmutableArray<byte> morph = Enumerable.Repeat((byte)0xA5, 0x5A0).ToImmutableArray();
        NativeBattleFullscreenOverlayState overlay = new(cold.FullscreenOverlay.Packets.Write(0, new byte[] { 1, 2, 3, 4 }));
        NativeBattleEffectMemory warm = cold with { Rush91MorphScratch = morph, FullscreenOverlay = overlay, FullscreenOverlayPause2795914 = 0xFEDCBA98 };
        foreach (int selectedIndex in new[] { 90, 93, 90 })
        {
            warm = warm.BeginLoad(selectedIndex);
            warm = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(warm))!;
            Assert.Equal(morph, warm.Rush91MorphScratch!.Value);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, warm.FullscreenOverlay!.Read(0, 4));
            Assert.Equal(0xFEDCBA98u, warm.FullscreenOverlayPause2795914);
        }
    }

    [Fact]
    public void Missing_imported_static_owners_are_not_reconstructed_by_restore_or_module_load()
    {
        NativeBattleEffectMemory source = NativeBattleEffectMemory.FromColdProcess() with { Rush91MorphScratch = null, FullscreenOverlay = null, FullscreenOverlayPause2795914 = null };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(source))!;
        restored = restored.BeginLoad(93);
        Assert.Null(restored.Rush91MorphScratch);
        Assert.Null(restored.FullscreenOverlay);
        Assert.Null(restored.FullscreenOverlayPause2795914);
    }
}

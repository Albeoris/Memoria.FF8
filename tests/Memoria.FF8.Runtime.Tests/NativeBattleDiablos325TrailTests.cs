using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325TrailTests
{
    [Fact]
    public void Initialization_writes_only_native_point_sentinels_and_block_heads_and_keeps_the_old_puff_cursor_and_PRNG()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x4C5C, Enumerable.Repeat((byte)0xA5, 0x3C01).ToArray()).MarkUnknown(0x4C5C, 6);
        frame = frame with { Memory = frame.Memory with { Arena = arena, Diablos325 = frame.Memory.Diablos325! with { Mode51F0 = 3, ParticleCursor51F4 = 0x20DFAB8u + 0x564C, Control51E4 = 123 } } };
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        Assert.Null(frame.Memory.Diablos325!.Trail);
        NativeBattleDiablos325Frame initialized = NativeBattleDiablos325Trail.Initialize657E70(frame, 5, 0, unchecked((ushort)-32));
        NativeBattleDiablos325Trail trail = Assert.IsType<NativeBattleDiablos325Trail>(initialized.Memory.Diablos325!.Trail);
        Assert.Equal(new NativeBattleDiablos325Trail(0x20DFAB8u + 0x4C5C, 0x20DFAB8u + 0x7C5C, 10, 16, 0, 0xFFE0), trail);
        trail.Validate();
        Assert.Equal(frame.Memory.Diablos325! with { Mode51F0 = 0, Trail = trail }, initialized.Memory.Diablos325);
        for (int index = 0; index < 512; index++)
        {
            Assert.Equal(new byte[] { 0xFF, 0xFF }, initialized.Memory.Arena!.Read(0x4C5C + index * 24 + 6, 2));
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 16), initialized.Memory.Arena.Read(0x4C5C + index * 24 + 8, 16));
        }
        for (int index = 0; index < 64; index++)
        {
            Assert.Equal(new byte[4], initialized.Memory.Arena!.Read(0x7C5C + index * 48, 4));
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 44), initialized.Memory.Arena.Read(0x7C5C + index * 48 + 4, 44));
        }
        Assert.Throws<NotSupportedException>(() => initialized.Memory.Arena!.Read(0x4C5C, 6));
        Assert.Equal((byte)0xA5, initialized.Memory.Arena!.Read(0x885C, 1)[0]);
        Assert.Equal(frame.Random, initialized.Random);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
        NativeBattleDiablos325State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDiablos325State>(NativeBattleTestSnapshots.Json(initialized.Memory.Diablos325))!;
        Assert.Equal(trail, restored.Trail);
    }

    [Fact]
    public void Absent_warm_trail_owner_stays_unknown_until_native_initialization()
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325State.FromColdProcess();
        string saved = NativeBattleTestSnapshots.Json(state);
        Assert.DoesNotContain("Trail", saved);
        NativeBattleDiablos325State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDiablos325State>(saved)!;
        restored.Validate();
        Assert.Null(restored.Trail);
    }
}

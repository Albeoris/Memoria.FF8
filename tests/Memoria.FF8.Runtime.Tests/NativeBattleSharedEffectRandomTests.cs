using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSharedEffectRandomTests
{
    [Theory]
    [InlineData(0u, 14)]
    [InlineData(1u, 139)]
    [InlineData(32767u, 32657)]
    [InlineData(32768u, 14)]
    [InlineData(0x80000000u, 14)]
    [InlineData(uint.MaxValue, 32657)]
    public void Draw_preserves_native_DWORD_wrap_then_masks_15_bits(uint seed, int expected)
    {
        NativeBattleSharedEffectRandomState before = new(seed);
        NativeBattleSharedEffectRandomState after = before.Draw7059E0(out ushort actual);
        Assert.Equal((ushort)expected, actual);
        Assert.Equal((uint)expected, after.State);
        Assert.Equal(seed, before.State);
    }

    [Fact]
    public void Seeder_writes_all_32_operand_bits_without_reading_unknown_old_state()
    {
        NativeBattleEffectMemory unknown = NativeBattleEffectMemory.FromColdProcess() with { SharedEffectRandom = null };
        NativeBattleEffectMemory seeded = unknown.SeedSharedEffectRandom7059D0(0xDEADBEEF);
        Assert.Equal(0xDEADBEEFu, seeded.SharedEffectRandom!.State);
        Assert.Null(unknown.SharedEffectRandom);
        Assert.Equal(0x80000001u, seeded.SharedEffectRandom.Seed7059D0(0x80000001).State);
        NativeBattleEffectMemory drawn = seeded.DrawSharedEffectRandom7059E0(out ushort actual);
        //The independent wide expression uses only the seed's proved low15 bits; fullDWORD storage is checked above.
        uint expected = ((0xDEADBEEFu & 0x7FFFu) * 125u + 14u) % 32768u;
        Assert.Equal((ushort)expected, actual);
        Assert.Equal(expected, drawn.SharedEffectRandom!.State);
    }

    [Fact]
    public void Unknown_owner_roundtrips_absent_and_fails_only_when_draw_is_reached()
    {
        NativeBattleEffectMemory before = NativeBattleEffectMemory.FromColdProcess() with { SharedEffectRandom = null };
        string json = JsonSerializer.Serialize(before);
        Assert.DoesNotContain("SharedEffectRandom", json);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(json)!;
        Assert.Null(restored.SharedEffectRandom);
        NativeBattleEffectMemory loaded = restored.BeginLoad(115);
        Assert.Null(loaded.SharedEffectRandom);
        Assert.Contains("DWORD2557098", Assert.Throws<NotSupportedException>(() => loaded.DrawSharedEffectRandom7059E0(out _)).Message);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Module_load_and_sound_publication_preserve_the_same_process_seed()
    {
        NativeBattleEffectMemory before = NativeBattleEffectMemory.FromColdProcess().SeedSharedEffectRandom7059D0(0xCAFEBABE);
        NativeBattleEffectMemory after = before.BeginLoad(115).PublishSound().BeginLoad(23);
        Assert.Same(before.SharedEffectRandom, after.SharedEffectRandom);
        Assert.Equal(0xCAFEBABEu, after.SharedEffectRandom!.State);
    }

    [Fact]
    public void Shared_module_private_Quezacotl_UI_and_CRT_random_have_distinct_storage()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleUiRandomState ui = new(1);
        NativeCrtRandomState crt = new(1);
        ushort privateSeed = 1;
        memory = memory.DrawSharedEffectRandom7059E0(out ushort shared);
        Assert.Equal((ushort)14, shared);
        Assert.Equal((ushort)139, NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref privateSeed));
        Assert.Equal(14u, memory.SharedEffectRandom!.State);
        Assert.Equal(1u, ui.State);
        Assert.Equal(new NativeCrtRandomState(1), crt);
        memory = memory.DrawSharedEffectRandom7059E0(out shared);
        Assert.Equal((ushort)1764, shared);
        Assert.Equal((ushort)139, privateSeed);
    }

    [Fact]
    public void Independent_JSON_restore_has_identical_next_draws_without_seed_reconstruction()
    {
        NativeBattleEffectMemory first = NativeBattleEffectMemory.FromColdProcess().SeedSharedEffectRandom7059D0(uint.MaxValue);
        NativeBattleEffectMemory second = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(first))!;
        for (int index = 0; index < 11; index++)
        {
            first = first.DrawSharedEffectRandom7059E0(out ushort actual);
            second = second.DrawSharedEffectRandom7059E0(out ushort restored);
            Assert.Equal(actual, restored);
            Assert.Equal(first.SharedEffectRandom, second.SharedEffectRandom);
        }
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Old_missing_seed_is_not_interpreted_as_known_zero()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleSharedEffectRandomState>("{}"));
        NativeBattleSharedEffectRandomState cold = NativeBattleSharedEffectRandomState.FromColdProcess();
        Assert.Equal(0u, cold.State);
        Assert.Equal(0u, NativeBattleEffectMemory.FromColdProcess().SharedEffectRandom!.State);
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeCharacterStatTests
{
    private static NativeCharacterVitals Read()
    {
        using GameAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        return assets.ReadNewGameCharacterVitals();
    }

    [Fact]
    public void AuthenticInitializationRetainsSavedInputsAndRecomputesGuestIdentityHp()
    {
        NativeCharacterVitals state = Read();
        Assert.Equal("4D0D23473AB54AC078B0C77A1672D970BF51E7824BAC1724027B5766B706C9EA", state.InitOutSha256);
        Assert.Equal(new uint[] { 6500, 7000, 12000, 7500, 10000, 7000, 8000, 25000 }, state.Records.Select(row => row.Experience));
        Assert.Equal(new byte[] { 0, 7, 11, 15, 19, 24, 28, 29 }, state.Records.Select(row => row.Weapon));
        Assert.Equal(new ushort[] { 1, 0, 0, 0, 0, 0, 0, 0 }, state.Records.Select(row => row.MemberFlags));
        Assert.Equal(new byte[] { 30, 31, 32 }, state.DreamWeapons);
        Assert.All(state.Records, row =>
        {
            Assert.Equal((ushort)0, row.HitPointBonus);
            Assert.Equal(6, row.StatBonuses.Length);
            Assert.Equal(32, row.MagicInventory.Length);
            Assert.All(row.MagicInventory, stock => Assert.Equal(new NativeMagicStock(0, 0), stock));
        });
        Assert.Equal(new NativeHpWord(486, 486), state.Records[0].HitPoints);
        Assert.Equal((ushort)447, state.Definitions.CharacterMaximum(state.Records[0] with { CharacterId = 8 }));
        Assert.Equal((ushort)399, state.Definitions.CharacterMaximum(state.Records[0] with { CharacterId = 9 }));
        Assert.Equal((ushort)509, state.Definitions.CharacterMaximum(state.Records[0] with { CharacterId = 10 }));
        Assert.Equal(16, state.GuardianForces.Length);
        NativeCharacterVitals copy = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(state))!;
        copy.Validate();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state), JsonSerializer.SerializeToElement(copy)));
    }

    [Fact]
    public void RebuildClampsDomainsAndEveryAcquiredUnassignedGuardianForce()
    {
        NativeCharacterVitals source = Read();
        NativeCharacterVital[] records = source.Records.ToArray();
        records[0] = records[0] with { HitPoints = new NativeHpWord(100, 9999) };
        NativeCharacterVitals acquired = (source with { Records = records, CombatParty = [0, 255, 255] }).AcquireGuardianForce(15);
        Assert.Equal(source.GuardianForces[15] with { Flags = (byte)(source.GuardianForces[15].Flags | 1) }, acquired.GuardianForces[15]);
        NativeCharacterVitals rebuilt = acquired.RebuildParty();
        Assert.Equal(new NativeHpWord(100, 486), rebuilt.Records[0].HitPoints);
        Assert.Equal(new NativeHpWord(100, 9999), acquired.Records[0].HitPoints);
        ushort maximum = acquired.Definitions.GuardianForceMaximum(15, acquired.GuardianForces[15]);
        Assert.Equal(maximum, rebuilt.GuardianForces[15].MaximumHitPoints);
        Assert.Equal(Math.Min(acquired.GuardianForces[15].HitPoints, maximum), rebuilt.GuardianForces[15].HitPoints);
        Assert.Equal(source.GuardianForces[0], rebuilt.GuardianForces[0]);
        Assert.Equal((ushort)0, rebuilt.Records[0].Junction.GuardianForces);
    }

    [Fact]
    public void RestUsesMemberAndDreamMaskAndRefusesAnUnknownEarlierGuardianForceMaximum()
    {
        NativeCharacterVitals source = Read().AcquireGuardianForce(0);
        NativeCharacterVital[] records = source.Records.ToArray();
        records[0] = records[0] with { HitPoints = new NativeHpWord(0, 0), Status = ushort.MaxValue };
        records[1] = records[1] with { MemberFlags = 1, HitPoints = new NativeHpWord(1, 1), Status = ushort.MaxValue };
        NativeCharacterVitals state = source with { Records = records, CombatParty = [0, 255, 255] };
        NativeCharacterVitals restored = state.Rest();
        Assert.Equal(new NativeHpWord(486, 486), restored.Records[0].HitPoints);
        Assert.Equal(new NativeHpWord(state.Definitions.CharacterMaximum(records[1]), state.Definitions.CharacterMaximum(records[1])), restored.Records[1].HitPoints);
        Assert.Equal((ushort)0x80, restored.Records[0].Status);
        Assert.Equal((ushort)0x80, restored.Records[1].Status);
        Assert.Equal(restored.GuardianForces[0].MaximumHitPoints, restored.GuardianForces[0].HitPoints);
        Assert.Equal(state.CombatParty, restored.CombatParty);
        NativeCharacterVitals dream = (state with { DreamEnabled = true }).Rest();
        Assert.Equal(records[1], dream.Records[1]);
        NativeCharacterVital[] inactive = state.Records.Select(row => row with { MemberFlags = 0 }).ToArray();
        Assert.Throws<NotSupportedException>(() => (state with { Records = inactive }).Rest());
        Assert.Null(state.GuardianForces[0].MaximumHitPoints);
        Assert.Equal(new NativeHpWord(0, 0), state.Records[0].HitPoints);
    }

    [Fact]
    public void MissingStatInputsAreRequiredCurrentShape()
    {
        JsonNode state = JsonSerializer.SerializeToNode(Read())!;
        state["Records"]![0]!.AsObject().Remove("Experience");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeCharacterVitals>(state.ToJsonString()));
    }
}

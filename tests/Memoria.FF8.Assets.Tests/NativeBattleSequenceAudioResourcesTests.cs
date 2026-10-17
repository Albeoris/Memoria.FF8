using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSequenceAudioResourcesTests
{
    [Fact]
    public void Authenticated_graphics_rules_carry_the_same_actor_sound_DWORDs_as_the_original_audio_catalog()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeMusicCatalog catalog = new(game.GameDirectory);
        var table = game.ReadNativeBattleGraphicsRules().ActorSounds1120!.Value;
        Assert.Equal(1120, table.Length);
        foreach (int identity in new[] { 0, 16, 77, 159 })
        {
            for (int slot = 0; slot < 7; slot++)
            {
                Assert.Equal(catalog.BattleActorSound(identity, slot), table[identity * 7 + slot]);
            }
        }
        Assert.Equal(210009u, table[1]);
        NativeBattleModuleSound sound = NativeBattleSequenceAudio.Resolve505AB0(new(1, true, 4, 0), 0, null, _ => 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), table)!;
        Assert.Equal(210009u, sound.Sound);
        Assert.Equal((byte)26, sound.Commit46B2A0(NativeGlobalSoundProcess.FromBufferlessColdProcess()).Sound.ReadCommittedVoice());
    }
}

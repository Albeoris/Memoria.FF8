using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSequenceAudioResourcesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void Installed_Zell_contact_program_uses_actor_sounds_with_distinct_critical_and_miss_branches(byte hitFlags)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(TestResources.GameDirectory);
        var sounds = game.ReadNativeBattleGraphicsRules().ActorSounds1120;
        BattleWeaponDefinition[] weapons = assets.WeaponDefinitions.Where(weapon => weapon.CharacterId == 1).ToArray();
        Assert.Equal(4, weapons.Length);
        foreach (BattleWeaponDefinition weapon in weapons)
        {
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "attacker", IsPlayer = true, ModelId = 1, WeaponId = weapon.Id }) with { PoseSource = null, LinkedPoseSource = null };
            int start = resource.SequenceStart(13);
            int contact = resource.SequenceSection.AsSpan().Slice(start, resource.SequenceEnd(13) - start).IndexOf(new byte[] { 0x97, 0, 0, 0xB4 });
            Assert.True(contact >= 0);
            NativeBattleGraphicsModel model = new(0, 3) { ModelId4 = 1, Flags2 = 4, Flags8 = 0, Sequence = 13, ResourceFlags44 = 1, SequenceState = new() { Offset46 = checked((ushort)contact), Pose = 0, PoseFrame114 = 0, PoseLimit115 = 1 } };
            List<uint> played = [];
            bool completed = false;
            NativeBattleSequenceInputs inputs = new(null, null)
            {
                ReadExternal = variable => variable == 25 ? hitFlags & 2 : throw new InvalidOperationException(),
                SelectModelSequence = (current, opcode, _, selected) =>
                {
                    Assert.Equal((byte)0xBF, opcode);
                    Assert.Equal((byte)4, selected);
                    return current;
                },
                ScheduleContactEffect = (current, program) => (current, NativeBattleSequenceParticles.Read505900(program, 0, suppressed: (hitFlags & 4) != 0).NextOffset),
                ApplyActionContacts = current =>
                {
                    completed = true;
                    return current;
                },
                PlaySound = (current, cue) =>
                {
                    NativeBattleSequenceSound source = new(checked((byte)cue.First), cue.Kind == NativeBattleSequenceCueKind.ActorSound, checked((byte)cue.Second), checked((ushort)cue.Third)) { OverrideOnContact4 = cue.OverrideSoundOnContact4 };
                    NativeBattleModuleSound sound = NativeBattleSequenceAudio.Resolve505AB0(source, current.Index, 3, _ => current.ModelId4!.Value, index =>
                    {
                        Assert.Equal(0, index);
                        return default;
                    }, _ => 128, sounds, hitFlags)!;
                    played.Add(sound.Sound);
                }
            };
            for (int callback = 0; callback < 100 && !completed; callback++)
            {
                // Exercise independently deserialized sequence state at every callback boundary.
                model = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(model))!;
                model = NativeBattleSequenceMachine.Advance(model, resource, inputs).Model;
            }
            Assert.True(completed);
            uint[] expected = hitFlags switch
            {
                2 => [210001, 210001, 210003],
                4 => [31, 31],
                _ => [210001, 210001]
            };
            Assert.Equal(expected, played);
        }
    }

    [Fact]
    public void Authenticated_graphics_rules_carry_the_same_actor_sound_DWORDs_as_the_original_audio_catalog()
    {
        using GameAssets game = new(TestResources.GameDirectory);
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

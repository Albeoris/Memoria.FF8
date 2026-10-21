using System.Text.Json;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCinematicSoundStopOutputTests
{
    [Fact]
    public void Selected_voice_stop_is_emitted_once_without_stopping_other_sounds_or_music()
    {
        BattleSession battle = Battle();
        List<byte> stopped = [];
        BattleAudioPresentation output = new(_ => throw new InvalidOperationException(), stopSounds: () => throw new InvalidOperationException(), playMusicCommand: _ => throw new InvalidOperationException(), stopSoundVoice: stopped.Add);
        output.Update(battle);
        CommittedEvents(battle).Add(new(0, "NativeSoundStopVoice", null, null, 2, "Selected voice") { NativeStopSoundVoice = 2 });
        string before = JsonSerializer.Serialize(battle.Capture());
        output.Update(battle);
        output.Update(battle);
        Assert.Equal(new byte[] { 2 }, stopped);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(before)!);
        output.Update(restored);
        output.Update(restored);
        Assert.Equal(new byte[] { 2 }, stopped);
    }

    [Fact]
    public void Selected_voice_requires_its_transient_output_handler_and_a_physical_voice()
    {
        BattleSession battle = Battle();
        BattleAudioPresentation output = new(_ => { });
        output.Update(battle);
        CommittedEvents(battle).Add(new(0, "NativeSoundStopVoice", null, null, 1, "Selected voice") { NativeStopSoundVoice = 1 });
        Assert.Throws<NotSupportedException>(() => output.Update(battle));
        BattleSession invalid = Battle();
        BattleAudioPresentation checkedOutput = new(_ => { }, stopSoundVoice: _ => throw new InvalidOperationException());
        checkedOutput.Update(invalid);
        CommittedEvents(invalid).Add(new(0, "NativeSoundStopVoice", null, null, 32, "Invalid voice") { NativeStopSoundVoice = 32 });
        Assert.Throws<InvalidDataException>(() => checkedOutput.Update(invalid));
    }

    private static List<BattleEvent> CommittedEvents(BattleSession battle) => (List<BattleEvent>)typeof(BattleSession).GetField("events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;

    private static BattleSession Battle() => new(new BattleDefinition { Combatants = [new() { Id = "party", IsPlayer = true, Name = "Party", NativeDeathCount = 0 }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
}

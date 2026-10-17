using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMusicCommandReceiptTests
{
    [Fact]
    public void Old_event_JSON_has_no_music_receipt_and_does_not_gain_one_from_its_kind_or_message()
    {
        BattleEvent old = new(7, "NativeMusicCommand", null, null, 0, "Native501B60 consumed singleton command3.");
        string json = JsonSerializer.Serialize(old);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(BattleEvent.NativeMusicCommand), out _));
        BattleEvent restored = JsonSerializer.Deserialize<BattleEvent>(json)!;
        Assert.Null(restored.NativeMusicCommand);
        Assert.Equal(old, restored);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)255)]
    public void Committed_receipt_JSON_preserves_actual_output_operands_without_changing_sound_allocation(byte command)
    {
        NativeBattleMusicCommandReceipt receipt = command switch
        {
            2 => new(command, new(0, -1, true, true, false, 63, null), PauseOutput: true),
            3 => new(command, GainChannel: ushort.MaxValue, Gain: 92, InitialGain: 0, EffectiveDuration: 60, ResumeOutput: true),
            4 => new(command, GainChannel: 0, Gain: 0, EffectiveDuration: 60),
            _ => new(command)
        };
        BattleEvent committed = new(7, "NativeMusicCommand", null, null, 0, "Committed actual singleton output.") { NativeMusicCommand = receipt };
        BattleEvent restored = JsonSerializer.Deserialize<BattleEvent>(JsonSerializer.Serialize(committed))!;
        Assert.Equal(receipt, restored.NativeMusicCommand);
        Assert.Null(restored.NativeModuleSound);
        Assert.Null(restored.NativeSound);
        Assert.False(restored.NativeStopAllSounds);
    }

    [Fact]
    public void Whole_session_restore_retains_pending_live_request_instead_of_reexecuting_fresh_entry_reset()
    {
        NativeMusicProcessState buffers = new(1, null, null, null, 0, null);
        NativeBattleMusicProcessState music = new(buffers, 0, null);
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            NativeMusicProcess = music,
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.UnknownBufferless()
        };
        BattleSession component = new(definition, BattleAtbMode.TurnBased);
        Assert.Null(component.NativeMusicProcess!.Commands);
        NativeBattleMusicProcessState live = music.InitializeCommands501C60().QueueGain501B10(60, 92);
        BattleSnapshot source = component.Capture() with { NativeMusicProcess = live };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeMusicProcess!.Commands!.Pending);
        Assert.Equal((byte?)3, restored.NativeMusicProcess.Commands.Command);
        Assert.Null(restored.Capture().Definition.NativeMusicProcess!.Commands);
        Assert.DoesNotContain(restored.Events, entry => entry.NativeMusicCommand is not null);
    }
}

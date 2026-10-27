using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionFieldMemoryTests
{
    private const uint Address = 0x1D999C0;

    [Fact]
    public void Cold_field_activation_owns_once_and_keeps_GameState_detached_during_updates()
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeBattleRetainedMemory historical = session.State.NativeBattleMemory!;
        string original = JsonSerializer.Serialize(historical);
        NativeBattleRetainedMemory working = session.ActivateNativeFieldMemory();
        Assert.Same(working, session.ActivateNativeFieldMemory());
        Assert.False(working.FileStaging!.IsSnapshot);
        Assert.False(((INativeBattleStorageNode)working.EffectMemory!.Textures!).IsSnapshot);
        working.FileStaging.Write(Address, [17, 0, 0, 0]);
        NativeBattleEffectTextureState textures = working.EffectMemory.Textures.Upload45BD30(0, 0, 1, 1, [18, 0]);
        working = working with { EffectMemory = working.EffectMemory with { Textures = textures } };
        session.SetNativeFieldEyeOwners(working, session.State.CrtRandom);
        Assert.Same(working, session.CurrentNativeBattleMemory);
        Assert.Same(historical, session.State.NativeBattleMemory);
        Assert.Equal(original, JsonSerializer.Serialize(historical));
        Assert.Equal((byte)17, session.CurrentNativeBattleMemory!.FileStaging!.ReadByte(Address));
        Assert.Equal((ushort)18, session.CurrentNativeBattleMemory.EffectMemory!.Textures!.ReadVramWord(0, 0));
    }

    [Fact]
    public void Direct_preparation_reuses_one_field_capture_and_does_not_publish_gameplay_state()
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeBattleRetainedMemory working = session.ActivateNativeFieldMemory();
        working.FileStaging!.Write(Address, [23, 0, 0, 0]);
        session.SetNativeFieldEyeOwners(working, session.State.CrtRandom);
        GameState before = session.State;
        NativeBattleRetainedMemory? capturedInBinding = null;
        int preparations = 0;
        using IDisposable binding = session.BindSnapshotPreparation(state =>
        {
            preparations++;
            capturedInBinding = session.CaptureNativeFieldMemory();
            return state;
        });
        GameState saved = session.PrepareSnapshot();
        Assert.Equal(1, preparations);
        Assert.Same(capturedInBinding, saved.NativeBattleMemory);
        Assert.Same(before, session.State);
        Assert.Same(working, session.CurrentNativeBattleMemory);
        Assert.True(saved.NativeBattleMemory!.FileStaging!.IsSnapshot);
        working.FileStaging.Write(Address, [24, 0, 0, 0]);
        Assert.Equal((byte)23, saved.NativeBattleMemory.FileStaging.ReadByte(Address));
        Assert.Equal((byte)24, session.CurrentNativeBattleMemory!.FileStaging!.ReadByte(Address));
        session.CompleteSnapshotPreparation(binding);
        Assert.Equal((byte)24, session.State.NativeBattleMemory!.FileStaging!.ReadByte(Address));
        Assert.True(session.State.NativeBattleMemory.FileStaging.IsSnapshot);
        Assert.NotSame(working, session.ActivateNativeFieldMemory());
    }

    [Fact]
    public void Field_process_JSON_and_battle_entry_handoff_have_independent_working_owners()
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeBattleRetainedMemory working = session.ActivateNativeFieldMemory();
        working.FileStaging!.Write(Address, [31, 0, 0, 0]);
        session.SetNativeFieldEyeOwners(working, session.State.CrtRandom);
        NativeFieldEyeProcess frozen = new NativeFieldEyeProcess(1, working, session.State.CrtRandom).CaptureSnapshot();
        string json = JsonSerializer.Serialize(frozen);
        NativeFieldEyeProcess first = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        NativeFieldEyeProcess second = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        first.Memory.FileStaging!.Write(Address, [32, 0, 0, 0]);
        Assert.Equal((byte)31, second.Memory.FileStaging!.ReadByte(Address));
        Assert.Equal((byte)31, working.FileStaging.ReadByte(Address));
        NativeBattleRetainedMemory handoff = session.CaptureNativeFieldMemory()!;
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = handoff,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        session.SetLiveBattle(battle);
        battle.NativeFileStaging!.Write(Address, [33, 0, 0, 0]);
        Assert.Equal((byte)31, handoff.FileStaging!.ReadByte(Address));
        Assert.Equal((byte)31, working.FileStaging.ReadByte(Address));
        Assert.Throws<InvalidOperationException>(() => session.ActivateNativeFieldMemory());
        Assert.Equal(json, JsonSerializer.Serialize(frozen));
    }
}

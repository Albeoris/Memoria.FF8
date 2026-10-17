using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStatusIconResourcesTests
{
    [Fact]
    public void Authenticated_status_directory_preserves_its_five_distinct_resources_and_original_frame_tables()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleStatusIconResources icons = Assert.IsType<NativeBattleStatusIconResources>(game.ReadNativeBattleGraphicsRules().StatusIcons);
        Assert.Equal(0xFF000000u, icons.DefaultHeader);
        Assert.Equal(new byte[] { 5, 6, 7, 8, 12 }, icons.Entries.Select(entry => entry.StatusBit).ToArray());
        Assert.Equal(new uint[] { 0xC7EA38, 0xC7E8BC, 0xC7EC54, 0xC7E600, 0xC7EC98 }, icons.Entries.Select(entry => entry.Sprite.Address).ToArray());
        Assert.Equal(new[] { 540, 380, 68, 700, 700 }, icons.Entries.Select(entry => entry.Sprite.Bytes.Length).ToArray());
        Assert.Equal(new short[] { 0, 0, 192, 0, -104 }, icons.Entries.Select(entry => entry.CameraX).ToArray());
        Assert.Equal(new short[] { -32, -8, -104, 0, -16 }, icons.Entries.Select(entry => entry.CameraY).ToArray());
        Assert.Equal(new short[] { 3328, 4096, 3328, 4096, 3072 }, icons.Entries.Select(entry => entry.Scale).ToArray());
        int[] counts = [8, 8, 4, 8, 8];
        int[] quads = [3, 2, 1, 4, 4];
        for (int index = 0; index < icons.Entries.Length; index++)
        {
            NativeBattleModuleResource sprite = icons.Entries[index].Sprite;
            Assert.Equal(counts[index], BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)));
            for (int phase = 0; phase < counts[index]; phase++)
            {
                NativeBattleModuleSpriteFrame frame = sprite.SpriteFrame(phase);
                Assert.Equal(quads[index] * 20, frame.Quads.Length);
                Assert.Equal(index == 1, frame.ExtendedPacket);
                Assert.Equal(phase == counts[index] - 1, frame.NextOffset < 0);
            }
        }
        NativeBattleModuleResource repeated = icons.Entries[2].Sprite;
        Assert.Equal(repeated.SpriteFrame(0).Quads.ToArray(), repeated.SpriteFrame(1).Quads.ToArray());
        Assert.Equal(repeated.SpriteFrame(2).Quads.ToArray(), repeated.SpriteFrame(3).Quads.ToArray());
        Assert.Equal(42, game.ReadNativeBattleModuleResources().Length);
        Assert.DoesNotContain(game.ReadNativeBattleModuleResources(), resource => icons.Entries.Any(entry => entry.Sprite.Address == resource.Address));
        NativeBattleGraphicsRules oldMetadata = game.ReadNativeBattleGraphicsRules() with { StatusIcons = null };
        Assert.DoesNotContain("StatusIcons", JsonSerializer.Serialize(oldMetadata));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleGraphicsRules>(JsonSerializer.Serialize(oldMetadata))!.StatusIcons);
    }

    [Fact]
    public void Original_poison_initialization_draws_in_the_same_callback_and_retains_next_frame_across_whole_session_JSON()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, NativeStatus0 = 2, NativeDeathCount = 0, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1 }
        ];
        BattleSession session = new(new()
        {
            EncounterId = 94,
            StageId = encounter.StageId,
            Combatants = actors,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        }, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !session.NativeEntryReady; callback++)
        {
            session.Tick();
        }
        Assert.True(session.NativeEntryReady);
        //DispatchTurnBasedFrame skips idle gauge time until readiness; this callback test explicitly starts with a ready player.
        Assert.True(session.Combatants.Single(actor => actor.Id == "party").IsReady);
        NativeBattleGraphicsState before = session.NativeGraphics!;
        Assert.Equal(0x20u, before.Models[0].Flags8!.Value & 0x11E0);
        Assert.Equal(0u, before.Flags & 0x410);
        Assert.Equal(0, before.Models[0].Flags0 & 4);
        Assert.Equal(0, before.Models[0].Flags2!.Value & 4);
        byte timer = before.Models[0].StatusTimers[5] ?? throw new InvalidDataException("Original poison initialization did not own its selected graphics timer.");
        Assert.Null(before.Models[0].StatusTimers[6]);
        string saved = JsonSerializer.Serialize(session.Capture());
        BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved)!);
        TimeSpan callbackStep = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        session.AdvanceNativeGraphicsPresentation(callbackStep);
        clone.AdvanceNativeGraphicsPresentation(callbackStep);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(clone.Capture()));
        NativeBattleGraphicsState after = session.NativeGraphics!;
        Assert.Equal(before.Callback + 1, after.Callback);
        Assert.Equal((byte?)((timer + 1) % 8), after.Models[0].StatusTimers[5]);
        Assert.Null(after.Models[0].StatusTimers[6]);
        NativeBattleModuleDraw draw = Assert.Single(after.ModuleDraws, item => item.Resource?.Address == 0xC7EA38);
        Assert.Equal((ushort)timer, draw.Phase);
        int drawIndex = after.ModuleDraws.IndexOf(draw);
        Assert.Contains(after.PrimitiveFrame!.LastCommands, command => command.Command.ModuleDraw == drawIndex);
        Assert.Equal(session.Capture().Combatants.Select(actor => actor.CurrentHp), clone.Capture().Combatants.Select(actor => actor.CurrentHp));
    }
}

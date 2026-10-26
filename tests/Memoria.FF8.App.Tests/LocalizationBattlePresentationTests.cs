using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationBattlePresentationTests
{
    [Fact]
    public void ExistingBattleReadsUnicodeGenerationByNumericIdentityAndKeepsPlayerNames()
    {
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        LocalizationBattlePresentation presentation = new(resources);
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Player chosen", IsPlayer = true }, new() { Id = "enemy", Name = "Original enemy", ModelId = 1 }] }, BattleAtbMode.Normal);
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleActionDefinition magic = new(BattleAbility.Magic, 1, "Original spell", BattleActionEffect.Damage, 1, false);
        BattleEvent message = new(0, "NativeText", "enemy", null, 30, "Original message") { NativeTextIdentity = BattleTextIdentity.Enemy(1, 2), NativeMessageMode = 3 };
        Dictionary<string, LocalizationValue> values = new()
        {
            [LocalizationSourceExporter.IndexedKey("en", "c0m001_names", 0)] = new("敵の名前", "test", "test"),
            [LocalizationSourceExporter.IndexedKey("en", "c0m001_dialogue_battle", 2)] = new("Сообщение 😀", "test", "test"),
            [LocalizationSourceExporter.IndexedKey("en", "kernel_magicspells_name", 1)] = new("Магия", "test", "test")
        };
        resources.Prepare(null, localization: new(values)).Commit();
        Assert.Equal("敵の名前", presentation.CombatantName(battle.Combatants[1]));
        Assert.Equal("Player chosen", presentation.CombatantName(battle.Combatants[0]));
        Assert.Equal("Магия", presentation.ActionName(magic));
        Assert.Equal("Сообщение 😀", presentation.Message(message));
        resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal("Original enemy", presentation.CombatantName(battle.Combatants[1]));
        Assert.Equal("Original spell", presentation.ActionName(magic));
        Assert.Equal("Original message", presentation.Message(message));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(message.NativeTextIdentity, JsonSerializer.Deserialize<BattleEvent>(JsonSerializer.Serialize(message))!.NativeTextIdentity);
        Assert.Null(JsonSerializer.Deserialize<BattleEvent>("{\"Frame\":0,\"Kind\":\"NativeText\",\"Amount\":30,\"Message\":\"Old snapshot\"}")!.NativeTextIdentity);
    }

    [Fact]
    public void UnicodeScanProjectionUsesNativeRevealAndScrollWithoutChangingItsByteOwner()
    {
        NativeBattleScanByteOwner row = new(new byte[60].ToImmutableArray(), Enumerable.Repeat(true, 60).ToImmutableArray());
        row = row.WriteByte(0x20, 2).WriteByte(0x22, 0).WriteWord(0x12, 16);
        string before = JsonSerializer.Serialize(row);
        NativeScanWindowText window = new(0, new(0, 0, 100, 100), new(0, 0, 100, 100), 4096, 4096, 0, []) { OriginalRectangle = new(0, 0, 100, 100), BackendFlags38 = 0 };
        NativeScanWindowText prefix = LocalizationBattlePresentation.ScanWindow(window, row, "😀日本語\nПеревод");
        Assert.Equal("😀日", prefix.Document.Text);
        Assert.Equal(-14, prefix.Lines[0].Y);
        Assert.Equal(before, JsonSerializer.Serialize(row));
        NativeScanWindowText complete = LocalizationBattlePresentation.ScanWindow(window, row.WriteByte(0x28, 1), "😀日本語\nПеревод");
        Assert.Equal("😀日本語\nПеревод", complete.Document.Text);
        Assert.Equal(window.Progress, complete.Progress);
    }
}

using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationChocoboPresentationTests
{
    [Fact]
    public void EveryProducerTemplateHasAnAuthoringIdentityWithTheSameFallback()
    {
        Assert.All(ChocoboText.Templates, pair => Assert.Equal(pair.Value, SystemTextCatalog.Defaults[pair.Key]));
        Assert.All(ChocoboRewardCatalog.Entries, reward =>
        {
            Assert.Equal(reward.DisplayName, reward.NameText.Fallback);
            Assert.Equal(reward.Description, reward.DescriptionText.Fallback);
        });
    }

    [Theory]
    [InlineData("broken {")]
    [InlineData("missing argument {99}")]
    public void MalformedTranslationsFallBackWithoutFormattingPlayerNamesAsTemplates(string translation)
    {
        ChocoboPresentationText name = ChocoboText.Create("chocobo.literal", "Player {99} 😀");
        ChocoboPresentationText line = ChocoboText.Create("chocobo.header.name", name, 12, 20, "Attack");
        Assert.Equal(line.Fallback, line.Format((id, fallback) => id == line.SemanticId ? translation : fallback));
        Assert.Contains("Player {99} 😀", line.Fallback);
    }

    [Fact]
    public void NativeReaderKeepsItsExceptionTypeAndProvidesATypedDiagnostic()
    {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => NativeChocoboWorldReader.Read([1, 2, 3]));
        ChocoboPresentationText text = ChocoboPresentationText.FromException(exception);
        Assert.Equal("chocobo.error.container", text.SemanticId);
        Assert.Equal(exception.Message, text.Fallback);
        Assert.Equal("選択した形式を確認してください", text.Format((id, fallback) => id == text.SemanticId ? "選択した形式を確認してください" : fallback));
    }

    [Fact]
    public void ImportPreviewResolvesCurrentGenerationAndItemNamesWithoutChangingNativeOwners()
    {
        GameState state = State();
        string original = JsonSerializer.Serialize(state);
        NativeChocoboWorldImport imported = Import(2);
        NativeChocoboWorldTransferTable table = new(Enumerable.Repeat((byte)33, 512).ToImmutableArray());
        ChocoboImportPreview preview = ChocoboWorldTransfers.Preview(state, imported, table);
        Assert.True(preview.CanImport);
        Assert.Equal("chocobo.import.items-added", preview.MessageText!.SemanticId);
        Assert.Equal("Added 2 item(s). Boko progression and transfer checkpoint updated.", preview.Message);
        Assert.Equal(2, Assert.Single(preview.Candidate!.Economy!.Items, item => item.ItemId == 33).Count);
        Assert.Equal(state.MenuRandom!.Advance(2), preview.Candidate.MenuRandom);
        Assert.Equal(state.PocketStation!.ChocoboName!.Value, preview.Candidate.PocketStation!.ChocoboName!.Value);
        string currentItemName = "Initial item";
        ChocoboImportReview review = ChocoboWorldPresentation.Import("fixture.bin", state, imported, preview, false, null, _ => currentItemName, _ => null);
        Dictionary<string, string> translations = new()
        {
            [SystemTextCatalog.Key("chocobo.header.name")] = "名前 {0} / {1:D4} / {2} / {3}",
            [SystemTextCatalog.Key("chocobo.item.incoming")] = "受領予定",
            [SystemTextCatalog.Key("chocobo.item.capacity")] = "{0} · 付与 {1} · 超過 {2}",
            [SystemTextCatalog.Key("chocobo.import.items-added")] = "追加 {0}"
        };
        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Chocobo World"] = "Native Import";
        MenuController menus = new(() => data);
        menus.ConnectLocalizationText((key, fallback) => translations.GetValueOrDefault(key, fallback));
        menus.ConnectChocoboWorld(() => [], () => null, _ => review, () => preview.Message, () => new([], false), () => "");
        menus.Open("config-chocobo");
        menus.Execute(new("chocobo-import-preview", "fixture.bin"));
        string first = string.Join("\n", menus.CurrentScreen.Details(menus));
        Assert.Contains("名前 KeptName", first);
        Assert.Contains("Initial item", first);
        Assert.Contains("受領予定", first);
        Assert.Contains("付与 2", first);
        translations[SystemTextCatalog.Key("chocobo.header.name")] = "Имя {0} / {1:D4} / {2} / {3}";
        currentItemName = "日本語の品";
        string next = string.Join("\n", menus.CurrentScreen.Details(menus));
        Assert.Contains("Имя KeptName", next);
        Assert.Contains("日本語の品", next);
        Assert.DoesNotContain("Initial item", next);
        Assert.Equal(original, JsonSerializer.Serialize(state));
        Assert.Equal("Initial item", review.Items[0].Name);
    }

    [Fact]
    public void CapacityDiagnosticsKeepClaimAtomicAndLocalizeNestedRewardNames()
    {
        NativeItemStock[] items = Enumerable.Range(1, 198).Where(id => ChocoboRewardCatalog.FindItem(id) is null).Select(id => new NativeItemStock((byte)id, 1)).ToArray();
        Array.Resize(ref items, NativeEconomy.ItemSlotCount);
        for (int index = 194; index < items.Length; index++)
        {
            items[index] = new((byte)(199 + index - 194), 1);
        }
        GameState state = State() with { GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.Memoria }, Economy = State().Economy! with { Items = items } };
        string before = JsonSerializer.Serialize(state);
        ChocoboRewardGrantResult preview = ChocoboRewardTransactions.ClaimRemaining(state);
        Assert.Equal(ChocoboRewardGrantStatus.InventoryFull, preview.Status);
        Assert.Same(state, preview.State);
        Assert.Contains("リボン", preview.MessageText!.Format((id, fallback) => id == "chocobo.reward.ribbon.name" ? "リボン" : fallback));
        ChocoboClaimReview review = ChocoboWorldPresentation.Claim(state, preview, null, _ => null, _ => null, _ => null);
        Assert.Equal("chocobo.claim.capacity-review", review.ReasonText!.SemanticId);
        Assert.Equal(4, review.ReasonText.Arguments[0]);
        Assert.Equal(0, review.ReasonText.Arguments[1]);
        Assert.False(review.CanClaim);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    private static NativeChocoboWorldImport Import(int count)
    {
        byte[] raw = new byte[64];
        raw[0] = 3;
        raw[1] = 0x20;
        raw[2] = 0x20;
        raw[3] = 0x30;
        raw[0x0C] = 0x11;
        raw[0x0D] = 2;
        raw[0x14] = (byte)count;
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x28), 123);
        return new("Fixture", new string('A', 64), 0, 1, 123, 211, 20, 20, 30, 0, 3, ImmutableArray.Create(count, 0, 0, 0), raw.ToImmutableArray(), raw.ToImmutableArray());
    }

    private static GameState State()
    {
        byte[] raw = new byte[64];
        raw[0] = 1;
        raw[1] = 1;
        raw[2] = 6;
        raw[3] = 6;
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x28), 123);
        byte[] name = new byte[12];
        FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("KeptName").CopyTo(name, 0);
        return new()
        {
            GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.NativeImport },
            MenuRandom = NativeMenuRandomState.FromColdProcess(),
            PocketStation = new(NativeInputConfiguration.OriginalInitHash, raw.ToImmutableArray()) { ChocoboName = name.ToImmutableArray() },
            Economy = new()
            {
                Version = 1,
                Rule = NativeEconomy.InitializationRule,
                InitOutSha256 = NativeInputConfiguration.OriginalInitHash,
                Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(),
                Gil = 0,
                PastGil = 0,
                EventBankGil72 = 0,
                EventBankPastGil76 = 0
            }
        };
    }
}

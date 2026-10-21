using System.Collections.Immutable;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuColumnTests
{
    public static IEnumerable<object[]> MagicCases()
    {
        foreach (bool shared in new[] { false, true })
        {
            foreach (bool finite in new[] { false, true })
            {
                foreach (int stock in new[] { 6, 100 })
                {
                    foreach (int uses in new[] { 1, 4 })
                    {
                        foreach (bool bound in new[] { false, true })
                        {
                            yield return [shared, finite, stock, uses, bound];
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(MagicCases))]
    public void FieldMagicRowsKeepNamesOwnersAndOneCountInSeparateColumns(bool shared, bool finite, int stock, int uses, bool bound)
    {
        foreach (string screen in new[] { "magic", "magic-use", "magic-stock", "magic-exchange", "junction-stock" })
        {
            MenuData data = MagicData(shared, finite, stock, uses, bound);
            MenuController menus = new(() => data);
            menus.Open(screen);
            UiView view = menus.BuildView();
            UiList list = Assert.Single(Descendants(view.Root).OfType<UiList>(), element => screen is "magic" ? element.Id == "magic-preview-character-0" : element.Id == "commands");
            int offset = screen == "junction-stock" ? 1 : 0;
            Assert.Equal(new[] { "Fire", "Very long spell name" }, list.Items.Skip(offset));
            string[] counts = finite ? [uses + "/4", (5 - uses) + "/4"] : [stock.ToString(), (106 - stock).ToString()];
            Assert.Equal(counts, list.SecondaryItems.Skip(offset));
            Assert.Empty(list.Images);
            Assert.Equal(shared && bound ? data.Magic[0].OwnerIcon : null, list.TrailingImages[offset]);
            Assert.Null(list.TrailingImages[offset + 1]);
            Assert.True(list.TrailingImagesBeforeSecondary);
            Assert.True(list.AutoSecondaryWidth);
            Assert.Equal(UiAlignment.End, list.SecondaryAlignment);
            if (offset != 0)
            {
                Assert.Equal("Empty", list.Items[0]);
                Assert.Equal("", list.SecondaryItems[0]);
                Assert.Null(list.TrailingImages[0]);
            }
        }
    }

    [Theory]
    [InlineData(960, 672, "100%")]
    [InlineData(1280, 720, "150%")]
    public void FieldMagicGeometryKeepsNameStartsAndCountEdgesAlignedWithMissingOwner(int width, int height, string scale)
    {
        MenuData data = MagicData(true, false, 6, 1, true);
        data.Settings["Text scale"] = scale;
        MenuController menus = new(() => data);
        menus.Open("magic-stock");
        UiView view = menus.BuildView();
        view.Resize(width, height);
        UiList list = Assert.Single(Descendants(view.Root).OfType<UiList>(), element => element.Id == "commands");
        RecordingCanvas canvas = new();
        list.Draw(canvas, new UiTheme());
        AssertAligned(canvas, "Fire", "Very long spell name", "6", "100");
        Assert.Single(canvas.Images);
    }

    [Theory]
    [InlineData("magic-target")]
    [InlineData("magic-exchange-target")]
    [InlineData("item-target")]
    public void TargetNamesAndHpHaveSeparateColumns(string screen)
    {
        MenuData data = MagicData(false, false, 6, 1, false);
        MenuController menus = new(() => data);
        menus.Open(screen);
        UiList list = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiList>(), element => element.Id == "commands");
        Assert.Equal(new[] { "A", "Long recipient name" }, list.Items);
        Assert.Equal(new[] { "HP 7 / 90", "HP 100 / 1000" }, list.SecondaryItems);
        Assert.True(list.AutoSecondaryWidth);
        Assert.Equal(UiAlignment.End, list.SecondaryAlignment);
    }

    [Theory]
    [InlineData(960, 672, 1f)]
    [InlineData(1280, 720, 1.5f)]
    public void BattleMagicNamesAndFiniteStockUseAlignedColumns(int width, int height, float scale)
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 6), new(2, 100)] },
                new() { Id = "target", Name = "Target", MaximumHp = 100, CurrentHp = 100 }
            ],
            ActionDefinitions =
            [
                new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 10, false),
                new(BattleAbility.Magic, 2, "Very long spell name", BattleActionEffect.Damage, 10, false)
            ]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }) { TextScale = scale };
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingAction);
        Assert.Equal(new[] { "Fire", "Very long spell name" }, hud.ChoiceLabels);
        UiView view = hud.BuildView();
        view.Resize(width, height);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        AssertAligned(canvas, "Fire", "Very long spell name", "6", "100");
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void InfiniteBattleRowsShowOnlyRatiosAndSharedJunctionOwner(bool shared, bool bound, bool exhausted)
    {
        NativeCharacterVitals vitals = BattleVitals();
        vitals = vitals.ChangeMagicStock(3, 1, 100, MagicOperationReason.ScriptReward, out _);
        vitals = vitals.ChangeMagicStock(3, 2, 6, MagicOperationReason.ScriptReward, out _);
        if (bound)
        {
            vitals = MagicJunctionPolicy.Assign(vitals, 3, 0, 1);
        }
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = shared, InfiniteMagic = true }).Vitals;
        BattleSession battle = new(new BattleDefinition
        {
            MagicVitals = vitals,
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, ModelId = 3, MagicRecordId = 3, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic] },
                new() { Id = "target", Name = "Target", MaximumHp = 100, CurrentHp = 100 }
            ],
            ActionDefinitions =
            [
                new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 10, false),
                new(BattleAbility.Magic, 2, "Very long spell name", BattleActionEffect.Damage, 10, false)
            ]
        }, BattleAtbMode.TurnBased);
        if (exhausted)
        {
            BattleSnapshot snapshot = battle.Capture();
            NativeCharacterVitals spent = snapshot.MagicVitals!.DeepCopy();
            spent.MagicSystem.SharedBattleMasks[1] = 15;
            spent.MagicSystem.PersonalBattleMasks[3][1] = 15;
            battle = BattleSession.Restore(snapshot with { MagicVitals = spent });
        }
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingAction);
        Assert.Equal(new[] { "Fire", "Very long spell name" }, hud.ChoiceLabels);
        RecordingCanvas canvas = new();
        UiTheme theme = new();
        hud.BuildView().Draw(canvas, theme);
        string ratio = (exhausted ? 0 : bound ? 4 : 1) + "/" + (bound ? 4 : 1);
        TextDraw name = Assert.Single(canvas.Texts, draw => draw.Text == "Fire");
        TextDraw value = Assert.Single(canvas.Texts, draw => draw.Text == ratio && draw.Bounds.Y == name.Bounds.Y);
        Assert.Equal(exhausted ? theme.Muted : theme.Foreground, name.Color);
        Assert.Equal(exhausted ? theme.Muted : theme.Foreground, value.Color);
        Assert.DoesNotContain(canvas.Texts, draw => draw.Text is "100" or "6" || draw.Text.Contains("Battle ", StringComparison.Ordinal));
        Assert.Equal(shared && bound ? 1 : 0, canvas.Images.Count(image => image.Kind == UiImageKind.CharacterPortrait && image.Id == 3));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_MENU_COLUMN_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            using System.Drawing.Bitmap bitmap = new(960, 672);
            using (BattlePreviewCanvas preview = new(bitmap, TestResources.GameDirectory))
            {
                hud.BuildView().Draw(preview, theme);
            }
            bitmap.Save(Path.Combine(output, $"magic-shared-{shared}-bound-{bound}-exhausted-{exhausted}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static NativeCharacterVitals BattleVitals()
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        return new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new string('0', 64),
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(10, 10), 0, new NativeCharacterJunction((ushort)(1 << id), 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [3, 255, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 10, new NativeGuardianForceLearnedBank(0x3FFFF, 0), 10, new byte[21].ToImmutableArray(), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), Enumerable.Repeat((byte)1, 57).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), new byte[116].ToImmutableArray(), new byte[19].ToImmutableArray(), new byte[5].ToImmutableArray()),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
    }

    private static MenuData MagicData(bool shared, bool finite, int stock, int uses, bool bound)
    {
        MenuData data = new() { SharedMagicStock = shared };
        MenuCharacter first = new() { Id = "character-0", NativeId = 0, Name = "A", InParty = true, CurrentHp = 7, MaximumHp = 90, NativeFieldItemTargetAllowed = true };
        first.AvailableJunctionSlots.Add("HP");
        data.Characters.Add(first);
        data.Characters.Add(new MenuCharacter { Id = "character-1", NativeId = 1, Name = "Long recipient name", InParty = true, CurrentHp = 100, MaximumHp = 1000, NativeFieldItemTargetAllowed = true });
        data.Magic.Add(new MenuMagic { Id = "magic-0-1", CharacterId = first.Id, Name = "Fire", Quantity = stock, RemainingUses = finite ? uses : null, Capacity = finite ? 4 : null, JunctionAvailable = !bound, Owner = bound ? "Owner" : "", Availability = bound ? "Bound" : "Available", Caster = "Caster", OwnerIcon = bound ? new UiImageSource(UiImageKind.CharacterPortrait, 0) : null, Icon = new UiImageSource(UiImageKind.Icon, 1) });
        data.Magic.Add(new MenuMagic { Id = "magic-0-2", CharacterId = first.Id, Name = "Very long spell name", Quantity = 106 - stock, RemainingUses = finite ? 5 - uses : null, Capacity = finite ? 4 : null });
        data.SupportedActions.Add("magic-use");
        return data;
    }

    private static void AssertAligned(RecordingCanvas canvas, string firstName, string secondName, string firstCount, string secondCount)
    {
        TextDraw first = Assert.Single(canvas.Texts, draw => draw.Text == firstName);
        TextDraw second = Assert.Single(canvas.Texts, draw => draw.Text == secondName);
        TextDraw firstValue = Assert.Single(canvas.Texts, draw => draw.Text == firstCount);
        TextDraw secondValue = Assert.Single(canvas.Texts, draw => draw.Text == secondCount);
        Assert.Equal(first.Bounds.X, second.Bounds.X, 3);
        Assert.Equal(firstValue.GlyphRight, secondValue.GlyphRight, 3);
        Assert.True(first.Bounds.Right <= firstValue.Bounds.X);
        Assert.True(second.Bounds.Right <= secondValue.Bounds.X);
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record TextDraw(string Text, UiRect Bounds, float GlyphRight, UiColor Color);

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<TextDraw> Texts { get; } = [];
        public List<UiImageSource> Images { get; } = [];
        private readonly Stack<UiRect> clips = new();

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize * 0.4f), fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            UiRect visible = clips.Count > 0 ? bounds.Intersect(clips.Peek()) : bounds;
            Texts.Add(new(text, visible, bounds.X + MeasureText(text, fontSize, bounds.Width).Width, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add(source);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
            clips.Push(clips.Count > 0 ? bounds.Intersect(clips.Peek()) : bounds);
        }

        public void PopClip()
        {
            clips.Pop();
        }
    }
}

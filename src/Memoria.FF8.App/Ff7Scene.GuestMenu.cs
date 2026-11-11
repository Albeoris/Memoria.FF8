using System.Globalization;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private static readonly string[] GuestMenuCommands = ["junction", "magic", "gf", "ability", "switch", "card", "tutorial"];
    private MenuController? guestPlayerMenu;
    private GameAssets? guestMenuAssets;
    private LiveMenuCatalog? guestMenuCatalog;

    private bool HasActiveGuestMenu => Session is { } session && ActiveGuestCharacters(envelope.GetMixedCombatBindings(), session.Progress.Party).Length != 0;

    internal static int[] ActiveGuestCharacters(IReadOnlyList<Ff7MixedCombatBinding> bindings, IReadOnlyList<int> party)
    {
        return party.SelectMany(storyCharacter => bindings.Where(binding => binding.StoryCharacter == storyCharacter).Select(binding => binding.OriginCharacter)).ToArray();
    }

    internal static void RegisterGuestMainMenu(MenuController menu)
    {
        menu.Register(new()
        {
            Id = "main",
            Title = "Final Fantasy VIII",
            Help = "Select a command.",
            Layout = MenuLayout.Main,
            Entries = controller => GuestMenuCommands.Select(id => controller.Definitions.Single(screen => screen.Id == id)).Select(screen => new MenuEntry(screen.Id, screen.Title, screen.Help, screen.Id)).ToArray()
        });
    }

    private MenuController EnsureGuestPlayerMenu()
    {
        if (guestPlayerMenu is not null)
        {
            return guestPlayerMenu;
        }
        guestMenuAssets ??= new(identityFf8Directory ?? throw new InvalidOperationException("The guest menu requires the selected FFVIII installation."));
        guestMenuCatalog ??= LiveMenuCatalog.ReadInstalled(guestMenuAssets);
        guestPlayerMenu = new(ReadGuestMenuData, ApplyGuestMenuAction) { ShowMainScreenTitle = true };
        guestPlayerMenu.ConnectPreferences(() => PresentationPreferences, ApplyGuestMenuAction);
        guestPlayerMenu.ConnectReadOnlyTutorialControls(() => sharedInput.Profile, () => sharedInput.ControllerPresentation, () => sharedInput.LastInputDevice);
        RegisterGuestMainMenu(guestPlayerMenu);
        guestPlayerMenu.Register(new()
        {
            Id = "tutorial-seed",
            Title = "SeeD Test",
            Help = "SeeD tests are available in the FFVIII world.",
            Entries = _ => [new("test", "SeeD Test", "Return to the FFVIII world to take a SeeD test.", Enabled: false)]
        });
        guestPlayerMenu.ScreenOpened += VisitGuestMenuScreen;
        return guestPlayerMenu;
    }

    private MenuData ReadGuestMenuData()
    {
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(envelope);
        GameState state = ReadGuestMenuState(document);
        MenuData data = LiveMenuData.Build(state, document.Progression!.SceneFacts?.Names, guestMenuCatalog, placeId: document.Progression.SceneFacts?.PlaceId);
        NativeRefinementMenuProjection.Augment(data, state, guestMenuAssets!.ReadNativeRefinementDefinitions(), guestMenuCatalog!);
        data.SeeDRank = document.Progression.Clocks.SeeD is { SalaryUiEnabled: true } seed ? seed.RankPoints / 100 : null;
        data.ShowSeeDRank = data.SeeDRank.HasValue;
        return data;
    }

    private GameState ReadGuestMenuState(GameplaySaveComponentDocument document)
    {
        GameAssets assets = guestMenuAssets!;
        GameplaySaveProgression progress = document.Progression ?? throw new InvalidDataException("The suspended FFVIII save has no durable menu owners.");
        NativeWorldControlState? controls = progress.Story.Controls is { } savedControls ? NativeWorldControlState.CreateNewGame() with { EventFlags103 = savedControls.Flags, EventFlags104 = savedControls.EventFlags, Menu209 = (byte)(savedControls.Menu & ~2), MusicKeep207 = savedControls.MusicKeep } : null;
        NativeCharacterVitals vitals = document.Characters.Restore(assets.ReadNewGameCharacterVitals());
        byte[] party = [255, 255, 255];
        int[] active = ActiveGuestCharacters(envelope.GetMixedCombatBindings(), Session!.Progress.Party);
        for (int index = 0; index < active.Length; index++)
        {
            int row = vitals.FindPhysicalRow(checked((byte)active[index]));
            if ((uint)row >= vitals.Records.Length)
            {
                throw new InvalidDataException("The guest menu requires the retained FFVIII record for each active guest.");
            }
            party[index] = checked((byte)row);
        }
        return new()
        {
            Scene = document.Resume.Kind == GameplaySaveResumeKind.Field ? SceneKind.Field : SceneKind.WorldMap,
            FieldId = document.Resume.FieldId,
            GameplaySceneFacts = progress.SceneFacts,
            CharacterVitals = vitals with { CombatParty = party },
            Economy = document.Economy.Restore(assets.ReadNewGameEconomy()),
            LimitProgress = document.Limits.Restore(assets.ReadNewGameLimitProgress()),
            WorldControls = controls,
            CardInventory = progress.Collections.Cards,
            RefinedCardIds = progress.Collections.RefinedCardIds,
            GameplayPreferences = progress.Rules.Preferences,
            ExamTimer = progress.Clocks.ExamTimer,
            Playtime = progress.Clocks.Playtime,
            ShopHistory = progress.Collections.ShopHistory is { } shops ? assets.ReadNewGameShopHistory() with { Bytes = shops } : null,
            InputConfiguration = progress.Rules.Input is { } input ? assets.ReadNewGameInputConfiguration() with { Options = input.Options, ButtonMap = input.ButtonMap, BattleMessageSpeed = input.BattleMessageSpeed, NativeBattleSpeedByte = input.BattleSpeed, FieldMessageSpeedByte = input.FieldMessageSpeed, SoundVolume = input.SoundVolume } : null
        };
    }

    private void ApplyGuestMenuAction(MenuAction action)
    {
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(envelope);
        GameState state = ReadGuestMenuState(document);
        NativeCharacterVitals vitals = state.CharacterVitals!;
        if (NativeJunctionMenuProjection.IsAction(action))
        {
            NativeCharacterVitals next = NativeJunctionMenuInput.Apply(vitals, NativeJunctionMenuProjection.PlayerAction(action), guestMenuAssets!.ReadNativeTutorialCatalog().AutoJunction);
            document = document with { Characters = CaptureGuestMenuCharacters(document, next) };
        }
        else if (NativeMagicMenuProjection.IsAction(action))
        {
            PlayerAction input = NativeMagicMenuProjection.PlayerAction(action);
            var selection = NativeMagicMenuInput.Validate(input);
            LiveMenuCatalog catalog = guestMenuCatalog!;
            BattleNativePlayerStatRules Rules(int row) => catalog.ReadStatRules(vitals, row);
            NativeCharacterVitals next = NativeMagicMenuInput.Apply(vitals, input, action.Kind == "magic-use" ? Rules(selection.Source) : null, state.WorldControls, Rules);
            if (action.Kind == "magic-use")
            {
                NativeCharacterVital before = vitals.Records[selection.Target];
                NativeCharacterVital after = next.Records[selection.Target];
                guestPlayerMenu!.SetActionSound(catalog.FieldMagicUseSound(selection.Spell, before.HitPoints != after.HitPoints || before.Status != after.Status));
            }
            document = document with { Characters = CaptureGuestMenuCharacters(document, next) };
        }
        else if (action.Kind == "attack-mode")
        {
            int row = int.Parse(action.TargetId.AsSpan("character-".Length), CultureInfo.InvariantCulture);
            MagicSystemState magic = vitals.MagicSystem.DeepCopy();
            magic.AttackModes[row] = Enum.Parse<MagicAttackMode>(action.Value);
            document = document with { Characters = CaptureGuestMenuCharacters(document, vitals with { MagicSystem = magic }) };
        }
        else if (action.Kind is "native-refine" or "card-mod")
        {
            document = RefineGuestMenuItem(document, state, action);
        }
        else
        {
            throw new NotSupportedException($"Unsupported FFVIII guest menu action '{action.Kind}'.");
        }
        PublishGuestMenuDocument(document);
    }

    private GameplaySaveComponentDocument RefineGuestMenuItem(GameplaySaveComponentDocument document, GameState state, MenuAction action)
    {
        NativeRefinementDefinitions definitions = guestMenuAssets!.ReadNativeRefinementDefinitions();
        if (action.Kind == "card-mod")
        {
            int card = int.Parse(action.SubjectId.AsSpan("card-".Length), CultureInfo.InvariantCulture);
            int index = definitions.FindRecipeIndex(115, card);
            action = new("native-refine", $"115:{index}", "0", "1");
        }
        PlayerAction input = NativeRefinementMenuProjection.PlayerAction(action);
        (int recipe, int row) = NativeRefinementInput.Validate(input);
        int kind = definitions.Abilities[input.Value - 92].Kind;
        NativeCharacterVitals vitals = state.CharacterVitals!;
        if (kind is 0 or 2 && !vitals.IsMenuCharacterAvailable(row, state.WorldControls))
        {
            throw new InvalidOperationException("Refinement requires an available FFVIII recipient.");
        }
        NativeRefinementResult next = definitions.Refine(vitals, state.Economy!, state.CardInventory, input.Value, recipe, row, input.Quantity!.Value, state.GameplayPreferences.CardModOnce, state.RefinedCardIds, NativeItemOrderAliases.From(state));
        GameplaySaveProgression progress = document.Progression!;
        GameplaySaveCollections collections = progress.Collections with { Cards = next.Cards };
        if (kind == 4)
        {
            int card = definitions.Recipe(input.Value, recipe).Ingredient;
            if (!collections.RefinedCardIds.Contains(card))
            {
                collections = collections with { RefinedCardIds = collections.RefinedCardIds.Add(card) };
            }
        }
        return document with { Characters = CaptureGuestMenuCharacters(document, next.Vitals), Economy = GameplaySaveEconomy.Capture(next.Economy), Progression = progress with { Collections = collections } };
    }

    private static GameplaySaveCharacters CaptureGuestMenuCharacters(GameplaySaveComponentDocument document, NativeCharacterVitals vitals)
    {
        return GameplaySaveCharacters.Capture(vitals with { CombatParty = document.Characters.CombatParty });
    }

    private void VisitGuestMenuScreen(string screen)
    {
        if (screen != "card")
        {
            return;
        }
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(envelope);
        GameplaySaveProgression progress = document.Progression!;
        if (progress.Collections.Cards is { } cards && cards.NativeVisitNormalMenu() is { } updated && cards.CollectionFlags != updated.CollectionFlags)
        {
            PublishGuestMenuDocument(document with { Progression = progress with { Collections = progress.Collections with { Cards = updated } } });
            guestPlayerMenu!.InvalidateLiveData();
        }
    }

    private void PublishGuestMenuDocument(GameplaySaveComponentDocument document)
    {
        using MemoryStream destination = new();
        GameplaySaveCodec.Write(destination, document);
        envelope = envelope with { Ff8Payload = destination.ToArray() };
        MixedCombatSettled?.Invoke(envelope);
    }
}

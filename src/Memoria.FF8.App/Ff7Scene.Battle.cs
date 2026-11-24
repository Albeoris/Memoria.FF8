using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private LgpArchive? battleArchive;
    private LgpArchive? battleMagicArchive;
    private PcTexture? battleCureTexture;
    private PcTexture? battleGrenadeTexture;
    private Ff7BattleRenderer? battleRenderer;
    private CrossoverArenaSurfaceCapture? arenaSurfaceCapture;
    private Ff7BattleSession? presentedBattle;
    private IReadOnlyList<string>? battleSpellNames;
    private Ff7BattleCommandOption? selectedBattleCommand;
    private (Ff7BattleCommand Command, string Name)? battleCommandPage;
    private Ff7BattleCommandOption? unavailableBattleCommand;
    private Ff7BattleGuestAbilityFailure? presentedGuestAbilityFailure;
    private bool battleTargetGroup;
    private int? battleInputOwner;
    private readonly BattleCursorMemory<(int Slot, int Character), (Ff7BattleCommand Command, string Name), BattleCursorChoice> battleCursor = new();
    private sealed record BattleCursorChoice(int Action, int TargetSlot, int TargetIdentity, bool Group);
    private (Ff7BattleCommand Command, string Name)? highlightedBattleCategory;
    private int? highlightedBattleArgument;
    private (int Slot, int Identity)? highlightedBattleTarget;
    private bool battlePointerPressed;
    private readonly List<(UiRect Bounds, int Owner, (Ff7BattleCommand Command, string Name)? Category, int? Argument, (int Slot, int Identity)? Target)> battlePointerRows = [];
    private bool pendingLimitConfirm;
    private BattleSmartAttackPreview? displayedGuestSmartPreview;

    private bool ForceGuestPhysical => (sharedInput.HeldFieldButtons & 3) == 1;

    private BattleSmartAttackPreview? GuestAttackPreview(int owner, Ff7BattleCommandOption command, int target)
    {
        return command.Command == Ff7BattleCommand.Attack ? mixedCombat?.GuestAt(owner)?.PreviewSmartAttack(target, ForceGuestPhysical) : null;
    }

    private void UpdateBattle(GameTime gameTime, Ff7BattleSession battle)
    {
        Ff7Session session = Session!;
        EnsureMixedBattle(battle);
        if (TryAdvanceAutoPlayBattle(gameTime, battle))
        {
            return;
        }
        bool mouseHeld = pointerState.LeftButton == ButtonState.Pressed;
        bool mouseClicked = mouseHeld && !battlePointerPressed;
        battlePointerPressed = mouseHeld;
        if (UpdateEnemyScan(battle, gameTime.ElapsedGameTime.TotalSeconds))
        {
            UpdateAudio();
            return;
        }
        if (battle.Outcome != Ff7BattleOutcome.Fighting)
        {
            if (!battle.ResultReady)
            {
                AdvanceBattleTime(gameTime, battle, escapeHeld: false);
                return;
            }
            UpdateAudio();
            if (battle.Outcome == Ff7BattleOutcome.Victory)
            {
                PrepareBattleResult(session, battle);
                if (semanticInput.MenuPage < 0 || semanticInput.MenuHorizontal < 0)
                {
                    battleResultPager!.MoveBack();
                    return;
                }
                if (semanticInput.MenuPage > 0 || semanticInput.MenuHorizontal > 0)
                {
                    battleResultPager!.MoveNext(battleResultRows.Length);
                    return;
                }
                if (semanticInput.Confirm && battleResultPager!.MoveNext(battleResultRows.Length))
                {
                    return;
                }
            }
            if (semanticInput.Confirm)
            {
                if (!TrySettleMixedBattle(battle))
                {
                    if (battle.Outcome == Ff7BattleOutcome.Victory)
                    {
                        session.ConfirmBattleVictory();
                    }
                    else if (battle.Outcome == Ff7BattleOutcome.Escaped)
                    {
                        session.ConfirmBattleEscape();
                    }
                    else if (battle.ReturnsAfterDefeat)
                    {
                        session.ConfirmBattleDefeat();
                    }
                    else
                    {
                        StopAudio();
                        autoPlay = null;
                        Session = null;
                        session.Dispose();
                        titleSelection = 1;
                    }
                }
                tickRemainder = 0;
                restoredNativeBattle = null;
                ResetBattlePresentation();
                sharedInput.SuppressHeldUiInput();
            }
            return;
        }
        if (ShowGuestAbilityFailure(battle))
        {
            UpdateAudio();
            return;
        }
        if (ReadUnavailableBattleCommand())
        {
            UpdateAudio();
            return;
        }
        if (ReadGuestLimitProcess())
        {
            AdvanceBattleTime(gameTime, battle, escapeHeld: false);
            ShowGuestAbilityFailure(battle);
            return;
        }
        if (CurrentBattleAtbMode == BattleAtbMode.TurnBased && sharedInput.BattleAdvanceTurnRequested)
        {
            battle.AdvanceTurn();
        }
        if (battleInputOwner != battle.ReadyPlayer)
        {
            battleInputOwner = battle.ReadyPlayer;
            highlightedBattleCategory = null;
            if (battleInputOwner is int ready && PresentationPreferences.CursorMemory && battleCursor.TryGetCommand(BattleCursorActor(battle, ready), out var remembered))
            {
                highlightedBattleCategory = remembered;
            }
            highlightedBattleArgument = null;
            highlightedBattleTarget = null;
            battleCommandPage = null;
            selectedBattleCommand = null;
        }
        if (battleInputOwner is int owner)
        {
            ReadBattleCommand(battle, owner, mouseClicked);
        }
        else if (battle.BeatRushSelecting)
        {
            if (semanticInput.Cancel)
            {
                battle.CancelBeatRush();
                pendingLimitConfirm = false;
            }
            else
            {
                pendingLimitConfirm |= semanticInput.Confirm;
            }
        }
        if (unavailableBattleCommand is not null)
        {
            UpdateAudio();
            return;
        }
        bool escapeHeld = IsBattleEscapeHeld(sharedInput.HeldFieldButtons);
        ReadMixedTrigger();
        AdvanceBattleTime(gameTime, battle, escapeHeld);
        ShowGuestAbilityFailure(battle);
    }

    internal static bool IsBattleEscapeHeld(ushort sharedButtons)
    {
        return (sharedButtons & 0x03) == 0x03;
    }

    private bool ReadUnavailableBattleCommand()
    {
        if (unavailableBattleCommand is null)
        {
            return false;
        }
        if (semanticInput.Confirm || semanticInput.Cancel)
        {
            unavailableBattleCommand = null;
        }
        return true;
    }

    private bool ShowGuestAbilityFailure(Ff7BattleSession battle)
    {
        if (battle.GuestAbilityFailure is not { } failure || failure == presentedGuestAbilityFailure)
        {
            return false;
        }
        presentedGuestAbilityFailure = failure;
        Ff7BattleCommandOption? option = battle.Commands(failure.ActorSlot).FirstOrDefault(candidate => candidate.Command == Ff7BattleCommand.Magic && candidate.Argument == failure.Ability);
        string name = option?.DisplayName ?? mixedCombat?.GuestAt(failure.ActorSlot)?.MagicName(failure.Ability) ?? "Ability unavailable";
        unavailableBattleCommand = new(Ff7BattleCommand.Magic, failure.Ability, 0, true, false) { DisplayName = name, UnsupportedReason = failure.Reason };
        selectedBattleCommand = null;
        return true;
    }

    private void AdvanceBattleTime(GameTime gameTime, Ff7BattleSession battle, bool escapeHeld)
    {
        Ff7Session session = Session!;
        session.AdvanceClock(gameTime.ElapsedGameTime);
        tickRemainder += sharedClock.Advance(TimeSpan.FromSeconds(Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25)), SceneKind.Field, battle: true).TotalSeconds;
        bool turnBased = CurrentBattleAtbMode == BattleAtbMode.TurnBased;
        const double tickDuration = 1d / Ff7BattleSession.LogicUpdatesPerSecond;
        while (tickRemainder >= tickDuration && !battle.ResultReady)
        {
            tickRemainder -= tickDuration;
            battle.SetCameraMenuPhase(selectedBattleCommand is null ? (short)-1 : (short)0);
            session.AdvanceTick(advanceClock: false, waitForBattleCommand: selectedBattleCommand is not null || battleCommandPage is not null, confirmBattleLimit: pendingLimitConfirm, escapeBattleHeld: escapeHeld, turnBasedBattle: turnBased);
            pendingLimitConfirm = false;
            if (!ReferenceEquals(session.Battle, battle))
            {
                ResetBattlePresentation();
                if (session.Battle is { } successor)
                {
                    EnsureMixedBattle(successor);
                }
                break;
            }
            if (UpdateEnemyScan(battle, 0) || battle.GuestAbilityFailure is { } failure && failure != presentedGuestAbilityFailure)
            {
                break;
            }
        }
        UpdateAudio();
    }

    private void ReadBattleCommand(Ff7BattleSession battle, int owner, bool mouseClicked)
    {
        var clicked = mouseClicked ? battlePointerRows.FirstOrDefault(row => row.Owner == owner && row.Bounds.Contains(pointerState.X, pointerState.Y)) : default;
        if (selectedBattleCommand is { } selected)
        {
            if (semanticInput.Cancel)
            {
                if (!CancelGuestMagicSelection())
                {
                    selectedBattleCommand = null;
                }
                return;
            }
            Ff7BattleCommandOption? current = battle.Commands(owner).SingleOrDefault(option => option.Command == selected.Command && option.Argument == selected.Argument);
            if (current?.UnsupportedReason is not null)
            {
                selectedBattleCommand = null;
                unavailableBattleCommand = current;
                return;
            }
            if (current is null || (current.CandidateTargets ?? current.ValidTargets) == 0)
            {
                selectedBattleCommand = null;
                return;
            }
            Ff7BattleActorState[] targets = BattleTargets(battle, current);
            if (targets.Length == 0)
            {
                selectedBattleCommand = null;
                return;
            }
            SynchronizeGuestMagicSelection(battle, owner, current);
            if (current.GroupUses > 0 && semanticInput.MenuPage != 0)
            {
                battleTargetGroup = !battleTargetGroup;
            }
            int direction = semanticInput.MenuVertical != 0 ? semanticInput.MenuVertical : semanticInput.MenuHorizontal;
            int previous = Array.FindIndex(targets, target => BattleTargetIdentity(target) == highlightedBattleTarget);
            int pointer = clicked.Target is { } hit && clicked.Argument == current.Argument && clicked.Category == BattleCategory(current)
                ? Array.FindIndex(targets, target => BattleTargetIdentity(target) == hit) : -1;
            bool validConfirm = highlightedBattleTarget is null || previous >= 0 || direction != 0;
            int targetIndex = pointer >= 0 ? pointer : MoveBattleChoice(Math.Max(0, previous), targets.Length, direction);
            Ff7BattleActorState target = targets[targetIndex];
            highlightedBattleTarget = BattleTargetIdentity(target);
            int targetSlot = target.Actor.Slot;
            bool useAll = battleTargetGroup && current.GroupUses > 0;
            ushort mask = (ushort)(1 << targetSlot);
            if (useAll || !current.SelectSingle)
            {
                mask = current.CandidateTargets ?? current.ValidTargets;
                if (!current.AllTargets)
                {
                    mask &= (ushort)(targetSlot < 3 ? 15 : 0x3F0);
                }
            }
            BattleSmartAttackPreview? preview = GuestAttackPreview(owner, current, targetSlot);
            BattleSmartAttackPreview? previousPreview = displayedGuestSmartPreview;
            displayedGuestSmartPreview = preview;
            bool submitted = false;
            if (CanConfirmGuestMagicSelection(current) && (semanticInput.CardChallenge || sharedInput.UiDetailRequested))
            {
                submitted = battle.SubmitMagicSequence(owner, current.Argument, battleMagicTargets, battleMagicTargetGroups);
            }
            else if (pointer >= 0 || validConfirm && semanticInput.Confirm)
            {
                string? reason = targets.Where(target => (mask & 1 << target.Actor.Slot) != 0).Select(target => battle.GuestAbilityTargetUnsupportedReason(owner, current.Command, current.Argument, target.Actor.Slot)).FirstOrDefault(reason => reason is not null);
                if (reason is not null)
                {
                    unavailableBattleCommand = current with { UnsupportedReason = reason };
                    return;
                }
                if (preview is not null)
                {
                    bool displayed = previousPreview is not { } displayedPreview || displayedPreview.ActorId != preview.ActorId || displayedPreview.TargetIndex != targetSlot || displayedPreview.ForcePhysical != preview.ForcePhysical || displayedPreview == preview;
                    submitted = displayed && mixedCombat!.GuestAt(owner)!.SubmitSmartAttack(preview);
                }
                else if (!CollectGuestMagicTarget(battle, owner, current, mask, useAll, out submitted))
                {
                    submitted = battle.Submit(owner, current.Command, current.Argument, mask, useAll);
                }
            }
            if (submitted)
            {
                if (PresentationPreferences.CursorMemory)
                {
                    battleCursor.Remember(BattleCursorActor(battle, owner), BattleCategory(current), new(current.Argument, targetSlot, target.Identity, useAll));
                }
                selectedBattleCommand = null;
                battleCommandPage = null;
            }
            return;
        }
        Ff7BattleCommandOption[] commands = AvailableBattleCommands(battle, owner);
        if (commands.Length == 0)
        {
            return;
        }
        if (battleCommandPage is { } page)
        {
            Ff7BattleCommandOption[] actions = commands.Where(option => BattleCategory(option) == page).ToArray();
            if (semanticInput.Cancel || actions.Length == 0)
            {
                battleCommandPage = null;
                return;
            }
            int previousAction = Array.FindIndex(actions, action => action.Argument == highlightedBattleArgument);
            int pointerAction = clicked.Category == page && clicked.Target is null && clicked.Argument is int argument ? Array.FindIndex(actions, action => action.Argument == argument) : -1;
            bool validConfirm = highlightedBattleArgument is null || previousAction >= 0 || semanticInput.MenuHorizontal != 0 || semanticInput.MenuVertical != 0;
            int actionIndex = Math.Max(0, previousAction);
            if (pointerAction >= 0)
            {
                actionIndex = pointerAction;
            }
            else if (page.Command == Ff7BattleCommand.Magic)
            {
                BattleDrawLayout layout = BattleDrawLayout.Calculate(actions.Length);
                (int column, int row) = layout.GetCell(Math.Min(actionIndex, actions.Length - 1));
                column = MoveBattleChoice(column, layout.Columns, semanticInput.MenuHorizontal);
                row = MoveBattleChoice(row, layout.ColumnRows(column), semanticInput.MenuVertical);
                actionIndex = layout.GetIndex(column, row);
            }
            else
            {
                actionIndex = MoveBattleChoice(actionIndex, actions.Length, semanticInput.MenuVertical);
            }
            highlightedBattleArgument = actions[actionIndex].Argument;
            if (pointerAction >= 0 || validConfirm && semanticInput.Confirm)
            {
                SelectBattleAction(battle, owner, actions[actionIndex]);
            }
            return;
        }
        var categories = commands.Select(BattleCategory).Distinct().ToArray();
        int previousCategory = Array.FindIndex(categories, category => category == highlightedBattleCategory);
        int pointerCategory = clicked.Argument is null && clicked.Category is { } categoryHit ? Array.IndexOf(categories, categoryHit) : -1;
        bool validCategory = highlightedBattleCategory is null || previousCategory >= 0 || semanticInput.MenuVertical != 0;
        int commandIndex = pointerCategory >= 0 ? pointerCategory : MoveBattleChoice(Math.Max(0, previousCategory), categories.Length, semanticInput.MenuVertical);
        highlightedBattleCategory = categories[commandIndex];
        if (pointerCategory >= 0 || validCategory && semanticInput.Confirm)
        {
            var category = categories[commandIndex];
            if (category.Command is Ff7BattleCommand.Magic or Ff7BattleCommand.Item or Ff7BattleCommand.Summon || category.Command == Ff7BattleCommand.Limit && commands.Count(option => BattleCategory(option) == category) > 1)
            {
                battleCommandPage = category;
                highlightedBattleArgument = PresentationPreferences.CursorMemory ? battleCursor.Selection(BattleCursorActor(battle, owner), category)?.Action : null;
                if (highlightedBattleArgument is int rememberedArgument && !commands.Any(option => BattleCategory(option) == category && option.Argument == rememberedArgument && option.UnsupportedReason is null))
                {
                    highlightedBattleArgument = null;
                }
            }
            else
            {
                SelectBattleAction(battle, owner, commands.First(option => BattleCategory(option) == category));
            }
        }
    }

    private static Ff7BattleCommandOption[] AvailableBattleCommands(Ff7BattleSession battle, int owner)
    {
        return battle.Commands(owner).Where(option => (option.CandidateTargets ?? option.ValidTargets) != 0 || option.UnsupportedReason is not null).ToArray();
    }

    private static (Ff7BattleCommand Command, string Name) BattleCategory(Ff7BattleCommandOption option)
    {
        return (option.Command, option.Category ?? option.Command.ToString());
    }

    private static int MoveBattleChoice(int index, int count, int direction)
    {
        return (Math.Min(index, count - 1) + Math.Sign(direction) + count) % count;
    }

    private static (int Slot, int Character) BattleCursorActor(Ff7BattleSession battle, int slot) => (slot, battle.Actors.Single(actor => actor.Actor.Slot == slot).Identity);

    private static (int Slot, int Identity) BattleTargetIdentity(Ff7BattleActorState actor) => (actor.Actor.Slot, actor.Identity);

    private void SelectBattleAction(Ff7BattleSession battle, int owner, Ff7BattleCommandOption option)
    {
        if (option.UnsupportedReason is not null)
        {
            unavailableBattleCommand = option;
            return;
        }
        if (mixedCombat?.GuestAt(owner)?.StartsLimitSelectionWithoutTarget(option.Argument) == true)
        {
            if (battle.Submit(owner, option.Command, option.Argument, checked((ushort)(1 << owner))))
            {
                selectedBattleCommand = null;
                battleCommandPage = null;
            }
            return;
        }
        selectedBattleCommand = option;
        BeginGuestMagicSelection(option);
        displayedGuestSmartPreview = null;
        BattleCursorChoice? memory = PresentationPreferences.CursorMemory ? battleCursor.Selection(BattleCursorActor(battle, owner), BattleCategory(option)) : null;
        highlightedBattleTarget = memory?.Action == option.Argument && BattleTargets(battle, option).Any(target => BattleTargetIdentity(target) == (memory.TargetSlot, memory.TargetIdentity) && battle.GuestAbilityTargetUnsupportedReason(owner, option.Command, option.Argument, target.Actor.Slot) is null)
            ? (memory.TargetSlot, memory.TargetIdentity) : null;
        battleTargetGroup = highlightedBattleTarget is not null && memory!.Group && option.GroupUses > 0;
    }

    private string? ActionTitle(Ff7BattleSession battle)
    {
        return battle.ActionTitle switch
        {
            { Command: 0x20, Argument: var slot } => battle.SceneAttackName(slot),
            { Command: (byte)Ff7BattleCommand.Item, Argument: var item } => BattleItemName(item),
            { Command: (byte)Ff7BattleCommand.Magic or (byte)Ff7BattleCommand.Summon, Argument: var spell } => BattleSpellName(spell),
            { Command: (byte)Ff7BattleCommand.Limit, Argument: var limit } => BattleLimitName(limit),
            _ => null
        };
    }

    private string BattleItemName(int item)
    {
        menuText ??= Ff7KernelTextResources.Load(installation);
        return menuText.ItemNames[item];
    }

    private string BattleSpellName(int spell)
    {
        battleSpellNames ??= KernelTextArchive.ReadPlainStrings(KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path))[9].Span, 128);
        return battleSpellNames[spell];
    }

    private static string BattleLimitName(int limit)
    {
        return limit switch { 0x80 => "Braver", 0x81 => "Cross-slash", 0x87 => "Big Shot", 0x89 => "Mindblow", 0x95 => "Beat Rush", _ => Ff7BattleCommand.Limit.ToString() };
    }

    private string CommandName(Ff7BattleCommandOption option, int owner)
    {
        if (option.DisplayName is not null)
        {
            return option.DisplayName;
        }
        CrossoverGuestHostActions? guest = mixedCombat?.GuestAt(owner);
        if (option.Command == Ff7BattleCommand.Item)
        {
            if (guest is { HasOriginItems: true })
            {
                return guest.ItemName(option.Argument);
            }
            return BattleItemName(option.Argument);
        }
        if (option.Command == Ff7BattleCommand.Summon)
        {
            return BattleSpellName(option.Argument);
        }
        if (option.Command == Ff7BattleCommand.Magic)
        {
            if (guest is not null)
            {
                return guest.MagicName(option.Argument);
            }
            return BattleSpellName(option.Argument);
        }
        if (option.Command == Ff7BattleCommand.Limit)
        {
            return guest is not null && option.Argument == 0
                ? "Renzokuken"
                : BattleLimitName(option.Argument);
        }
        return option.Command.ToString();
    }

    private static Ff7BattleActorState[] BattleTargets(Ff7BattleSession battle, Ff7BattleCommandOption command)
    {
        return battle.Actors.Where(actor => ((command.CandidateTargets ?? command.ValidTargets) & 1 << actor.Actor.Slot) != 0).OrderByDescending(actor => (actor.Actor.Slot >= 4) == command.DefaultsToEnemies).ToArray();
    }

    private Ff7BattleRenderer BattleRenderer(Ff7BattleSession battle)
    {
        if (!ReferenceEquals(presentedBattle, battle))
        {
            battleArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            battleMagicArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
            battleCureTexture ??= Ff7BattleCureResources.Load(installation);
            battleGrenadeTexture ??= Ff7BattleGrenadeResources.Load(installation);
            battleRenderer = new(battleArchive, battle.Formation.LocationId, battleMagicArchive, cureTexture: battleCureTexture, grenadeTexture: battleGrenadeTexture, shadow: Ff7BattleShadowResources.Load(installation, battle.Formation.LocationId));
            arenaSurfaceCapture?.Dispose();
            arenaSurfaceCapture = new(() => new GpuTriangleRenderer(device));
            presentedBattle = battle;
        }
        return battleRenderer!;
    }

    private static Ff7BattleActorVisual BattleActorVisual(Ff7BattleSession battle, Ff7BattleActorState actor)
    {
        int slot = actor.Actor.Slot;
        Ff7BattlePlacement placement = battle.RenderActorPlacement(slot);
        string model = battle.ActorModelPrefix(slot);
        int? weapon = model == "rs" ? null : slot < 3 ? battle.WeaponModel(slot) : actor.Identity == 16 ? 0 : null;
        (int clip, int frame) = battle.RenderActorPose(slot);
        return new(model, placement.Position, placement.Yaw * MathF.Tau / 4096, clip, frame, weapon, battle.RenderActorLimit(slot)) { ModelModifiers = battle.ActorModelOutput(slot) };
    }

    private void RenderBattle(Ff7BattleSession battle, Rectangle bounds)
    {
        EnsureMixedBattle(battle);
        Ff7BattleRenderer renderer = BattleRenderer(battle);
        if (displayedEnemyScan is not null)
        {
            RenderEnemyScanModel();
            return;
        }
        List<Ff7BattleActorVisual> visuals = [];
        foreach (Ff7BattleActorState actor in battle.Actors)
        {
            int slot = actor.Actor.Slot;
            if (!battle.IsActorPresentationReady(slot) || IsMixedBattleSlot(slot) || mixedCombat?.ActiveGuest?.SuppressForeignActor(slot) == true)
            {
                continue;
            }
            if (slot >= 4 && (actor.Actor.HitPoints == 0 || (actor.Actor.Conditions & 8) == 0))
            {
                bool participating = battle.Action is { } current && (current.Command.ActorSlot == slot || (current.Command.TargetMask & 1 << slot) != 0);
                if (!participating && !battle.HasActorDeathPresentation(slot))
                {
                    continue;
                }
            }
            visuals.Add(BattleActorVisual(battle, actor) with { Slot = slot, PresentationTransform = mixedCombat?.ActiveGuest?.ReadForeignPlacement(slot), ShadowSuppressed = mixedCombat?.ActiveGuest?.SuppressForeignShadow(slot) == true, NativeColor = mixedCombat?.ActiveGuest?.ReadForeignColor(slot) });
        }
        CrossoverNativeEffectVisual[] guestEffects = mixedCombat?.Guests.Select(guest => guest.EffectVisual).OfType<CrossoverNativeEffectVisual>().ToArray() ?? [];
        if (mixedCombat?.ActiveGuest is null)
        {
            arenaSurfaceCapture!.Reset();
        }
        arenaSurfaceCapture!.Prepare(guestEffects.SelectMany(effect => effect.Ordering?.Packets.Select(packet => packet.Pilot.ForeignSurface?.Material).OfType<INativeBattleForeignMaterial>() ?? []));
        HashSet<NativeBattleArenaPrimitiveId> replacedArena = mixedCombat?.Guests.SelectMany(guest => guest.ArenaReplacements).SelectMany(replacement => replacement.Geometry.Faces).Select(face => face.Id).ToHashSet() ?? [];
        replacedArena.UnionWith(mixedCombat?.Guests.SelectMany(guest => guest.SuppressedArenaPrimitives) ?? []);
        renderer.Render(triangles, bounds.Width, bounds.Height, new(battle.GuestRenderCameraOutput, visuals, battle.Action?.Fire, battle.Action?.Ice, battle.Action?.Bolt, Cure: battle.Action?.Cure, Braver: battle.Action?.Braver) { CaptureActorBounds = selectedBattleCommand is not null, BeatRush = battle.Action?.BeatRushEffect, BigShot = battle.Action?.BigShot, Hitmarks = battle.Hitmarks, SecondaryEffects = battle.SecondaryEffects, PrimaryEffects = battle.PrimaryEffects, SummonModel = battle.Action?.Summon?.Model, SmokeShot = battle.Action?.SmokeShot, Smog = battle.Action?.Smog, Destroyed = battle.Action?.Destroyed, SearchScope = battle.Action?.SearchScope, Rockets = battle.Action?.Rockets, StageFade = battle.RenderStageFade, Guests = MixedBattleVisuals(battle), GuestEffectLayers = guestEffects, SuppressArenaPrimitives = replacedArena });
    }

    private void ResetBattlePresentation()
    {
        battleRenderer = null;
        displayedEnemyScan = null;
        dismissedEnemyScan = null;
        enemyScanModel = null;
        enemyScanRenderer?.Dispose();
        enemyScanRenderer = null;
        arenaSurfaceCapture?.Dispose();
        arenaSurfaceCapture = null;
        presentedBattle = null;
        battleCursor.Clear();
        battlePointerRows.Clear();
        battlePointerPressed = pointerState.LeftButton == ButtonState.Pressed;
        highlightedBattleCategory = null;
        highlightedBattleArgument = null;
        highlightedBattleTarget = null;
        battleResultPager = null;
        battleResultRows = [];
        battleResultReward = null;
        battleInputOwner = null;
        pendingLimitConfirm = false;
        unavailableBattleCommand = null;
        presentedGuestAbilityFailure = null;
        selectedBattleCommand = null;
        displayedGuestSmartPreview = null;
        battleCommandPage = null;
        battleTargetGroup = false;
    }
}

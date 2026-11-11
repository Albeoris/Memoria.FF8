using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private LgpArchive? battleArchive;
    private LgpArchive? battleMagicArchive;
    private PcTexture? battleCureTexture;
    private PcTexture? battleGrenadeTexture;
    private Ff7BattleRenderer? battleRenderer;
    private Ff7BattleSession? presentedBattle;
    private IReadOnlyList<string>? battleSpellNames;
    private Ff7BattleCommandOption? selectedBattleCommand;
    private Ff7BattleCommand? battleCommandPage;
    private int battleCommandIndex;
    private int battleActionIndex;
    private int battleTargetIndex;
    private bool battleTargetGroup;
    private int? battleInputOwner;
    private bool pendingLimitConfirm;

    private void UpdateBattle(GameTime gameTime, Ff7BattleSession battle)
    {
        Ff7Session session = Session!;
        EnsureMixedBattle(battle);
        if (battle.Outcome != Ff7BattleOutcome.Fighting)
        {
            if (!battle.ResultReady)
            {
                AdvanceBattleTime(gameTime, battle, escapeHeld: false);
                return;
            }
            UpdateAudio();
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
        if (battleInputOwner != battle.ReadyPlayer)
        {
            battleInputOwner = battle.ReadyPlayer;
            battleCommandIndex = 0;
            battleActionIndex = 0;
            battleCommandPage = null;
            selectedBattleCommand = null;
        }
        if (battleInputOwner is int owner)
        {
            ReadBattleCommand(battle, owner);
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
        bool escapeHeld = (Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons) & 0x0C) == 0x0C;
        ReadMixedTrigger();
        AdvanceBattleTime(gameTime, battle, escapeHeld);
    }

    private void AdvanceBattleTime(GameTime gameTime, Ff7BattleSession battle, bool escapeHeld)
    {
        Ff7Session session = Session!;
        session.AdvanceClock(gameTime.ElapsedGameTime);
        tickRemainder += sharedClock.Advance(TimeSpan.FromSeconds(Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25)), SceneKind.Field, battle: true).TotalSeconds;
        const double tickDuration = 1d / Ff7BattleSession.LogicUpdatesPerSecond;
        while (tickRemainder >= tickDuration && !battle.ResultReady)
        {
            tickRemainder -= tickDuration;
            battle.SetCameraMenuPhase(selectedBattleCommand is null ? (short)-1 : (short)0);
            session.AdvanceTick(advanceClock: false, waitForBattleCommand: selectedBattleCommand is not null || battleCommandPage is not null, confirmBattleLimit: pendingLimitConfirm, escapeBattleHeld: escapeHeld);
            pendingLimitConfirm = false;
        }
        UpdateAudio();
    }

    private void ReadBattleCommand(Ff7BattleSession battle, int owner)
    {
        if (selectedBattleCommand is { } selected)
        {
            if (semanticInput.Cancel)
            {
                selectedBattleCommand = null;
                return;
            }
            Ff7BattleCommandOption? current = battle.Commands(owner).SingleOrDefault(option => option.Command == selected.Command && option.Argument == selected.Argument);
            if (current is null || current.ValidTargets == 0)
            {
                selectedBattleCommand = null;
                return;
            }
            Ff7BattleActorState[] targets = BattleTargets(battle, current);
            if (current.GroupUses > 0 && semanticInput.MenuPage != 0)
            {
                battleTargetGroup = !battleTargetGroup;
            }
            int direction = semanticInput.MenuVertical != 0 ? semanticInput.MenuVertical : semanticInput.MenuHorizontal;
            battleTargetIndex = (Math.Min(battleTargetIndex, targets.Length - 1) + Math.Sign(direction) + targets.Length) % targets.Length;
            int targetSlot = targets[battleTargetIndex].Actor.Slot;
            bool useAll = battleTargetGroup && current.GroupUses > 0;
            ushort mask = (ushort)(useAll || !current.SelectSingle ? current.ValidTargets & (targetSlot < 3 ? 15 : 0x3F0) : 1 << targetSlot);
            if (semanticInput.Confirm && battle.Submit(owner, current.Command, current.Argument, mask, useAll))
            {
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
            Ff7BattleCommandOption[] actions = commands.Where(option => option.Command == page).ToArray();
            if (semanticInput.Cancel || actions.Length == 0)
            {
                battleCommandPage = null;
                return;
            }
            if (page == Ff7BattleCommand.Magic)
            {
                BattleDrawLayout layout = BattleDrawLayout.Calculate(actions.Length);
                (int column, int row) = layout.GetCell(Math.Min(battleActionIndex, actions.Length - 1));
                column = MoveBattleChoice(column, layout.Columns, semanticInput.MenuHorizontal);
                row = MoveBattleChoice(row, layout.ColumnRows(column), semanticInput.MenuVertical);
                battleActionIndex = layout.GetIndex(column, row);
            }
            else
            {
                battleActionIndex = MoveBattleChoice(battleActionIndex, actions.Length, semanticInput.MenuVertical);
            }
            if (semanticInput.Confirm)
            {
                SelectBattleAction(actions[battleActionIndex]);
            }
            return;
        }
        Ff7BattleCommand[] categories = commands.Select(option => option.Command).Distinct().ToArray();
        battleCommandIndex = MoveBattleChoice(battleCommandIndex, categories.Length, semanticInput.MenuVertical);
        if (semanticInput.Confirm)
        {
            Ff7BattleCommand category = categories[battleCommandIndex];
            if (category is Ff7BattleCommand.Magic or Ff7BattleCommand.Item || category == Ff7BattleCommand.Limit && commands.Count(option => option.Command == category) > 1)
            {
                battleCommandPage = category;
                battleActionIndex = 0;
            }
            else
            {
                SelectBattleAction(commands.First(option => option.Command == category));
            }
        }
    }

    private static Ff7BattleCommandOption[] AvailableBattleCommands(Ff7BattleSession battle, int owner)
    {
        return battle.Commands(owner).Where(option => option.ValidTargets != 0).ToArray();
    }

    private static int MoveBattleChoice(int index, int count, int direction)
    {
        return (Math.Min(index, count - 1) + Math.Sign(direction) + count) % count;
    }

    private void SelectBattleAction(Ff7BattleCommandOption option)
    {
        selectedBattleCommand = option;
        battleTargetIndex = 0;
        battleTargetGroup = false;
    }

    private string? ActionTitle(Ff7BattleSession battle)
    {
        return battle.ActionTitle switch
        {
            { Command: 0x20, Argument: var slot } => battle.SceneAttackName(slot),
            { Command: (byte)Ff7BattleCommand.Item, Argument: var item } => BattleItemName(item),
            { Command: (byte)Ff7BattleCommand.Magic, Argument: var spell } => BattleSpellName(spell),
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
        return limit switch { 0x80 => "Braver", 0x87 => "Big Shot", 0x95 => "Beat Rush", _ => Ff7BattleCommand.Limit.ToString() };
    }

    private string CommandName(Ff7BattleCommandOption option, int owner)
    {
        CrossoverGuestHostActions? guest = mixedCombat?.GuestAt(owner);
        if (option.Command == Ff7BattleCommand.Item)
        {
            if (guest is { HasOriginItems: true })
            {
                return guest.ItemName(option.Argument);
            }
            return BattleItemName(option.Argument);
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
        return battle.Actors.Where(actor => (command.ValidTargets & 1 << actor.Actor.Slot) != 0).OrderByDescending(actor => (actor.Actor.Slot >= 4) == command.DefaultsToEnemies).ToArray();
    }

    private Ff7BattleRenderer BattleRenderer(Ff7BattleSession battle)
    {
        if (!ReferenceEquals(presentedBattle, battle))
        {
            battleArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            battleMagicArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
            battleCureTexture ??= Ff7BattleCureResources.Load(installation);
            battleGrenadeTexture ??= Ff7BattleGrenadeResources.Load(installation);
            battleRenderer = new(battleArchive, battle.Formation.LocationId, battleMagicArchive, cureTexture: battleCureTexture, grenadeTexture: battleGrenadeTexture);
            presentedBattle = battle;
        }
        return battleRenderer!;
    }

    private static Ff7BattleActorVisual BattleActorVisual(Ff7BattleSession battle, Ff7BattleActorState actor)
    {
        int slot = actor.Actor.Slot;
        Ff7BattlePlacement placement = battle.RenderActorPlacement(slot);
        string model = Ff7BattleAnimationResources.ModelPrefix(actor.Identity, slot >= 4);
        int? weapon = slot < 3 ? battle.WeaponModel(slot) : actor.Identity == 16 ? 0 : null;
        (int clip, int frame) = battle.RenderActorPose(slot);
        return new(model, placement.Position, placement.Yaw * MathF.Tau / 4096, clip, frame, weapon, battle.RenderActorLimit(slot)) { ModelModifiers = battle.ActorModelOutput(slot) };
    }

    private void RenderBattle(Ff7BattleSession battle, Rectangle bounds)
    {
        EnsureMixedBattle(battle);
        Ff7BattleRenderer renderer = BattleRenderer(battle);
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
            visuals.Add(BattleActorVisual(battle, actor));
        }
        renderer.Render(triangles, bounds.Width, bounds.Height, new(battle.GuestRenderCameraOutput, visuals, battle.Action?.Fire, battle.Action?.Ice, battle.Action?.Bolt, Cure: battle.Action?.Cure, Braver: battle.Action?.Braver) { BeatRush = battle.Action?.BeatRushEffect, BigShot = battle.Action?.BigShot, Hitmarks = battle.Hitmarks, SecondaryEffects = battle.SecondaryEffects, PrimaryEffects = battle.PrimaryEffects, SmokeShot = battle.Action?.SmokeShot, Smog = battle.Action?.Smog, Destroyed = battle.Action?.Destroyed, SearchScope = battle.Action?.SearchScope, Rockets = battle.Action?.Rockets, StageFade = battle.RenderStageFade, Guests = MixedBattleVisuals(battle), GuestEffectLayers = mixedCombat?.Guests.Select(guest => guest.EffectVisual).OfType<CrossoverNativeEffectVisual>().ToArray() ?? [] });
    }

    private void ResetBattlePresentation()
    {
        battleRenderer = null;
        presentedBattle = null;
        battleInputOwner = null;
        pendingLimitConfirm = false;
        selectedBattleCommand = null;
        battleCommandIndex = 0;
        battleActionIndex = 0;
        battleCommandPage = null;
        battleTargetIndex = 0;
        battleTargetGroup = false;
    }
}

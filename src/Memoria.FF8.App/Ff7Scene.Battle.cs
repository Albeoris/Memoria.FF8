using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
    private int battleCommandIndex;
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
                ResetBattlePresentation();
                sharedInput.SuppressHeldUiInput();
            }
            return;
        }
        if (battleInputOwner != battle.ReadyPlayer)
        {
            battleInputOwner = battle.ReadyPlayer;
            battleCommandIndex = 0;
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
            session.AdvanceTick(advanceClock: false, waitForBattleCommand: selectedBattleCommand is not null, confirmBattleLimit: pendingLimitConfirm, escapeBattleHeld: escapeHeld);
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
            }
            return;
        }
        Ff7BattleCommandOption[] commands = battle.Commands(owner).Where(option => option.ValidTargets != 0).ToArray();
        if (commands.Length == 0)
        {
            return;
        }
        battleCommandIndex = (Math.Min(battleCommandIndex, commands.Length - 1) + Math.Sign(semanticInput.MenuVertical) + commands.Length) % commands.Length;
        if (semanticInput.Confirm)
        {
            selectedBattleCommand = commands[battleCommandIndex];
            battleTargetIndex = 0;
            battleTargetGroup = false;
        }
    }

    private void DrawBattle(SpriteBatch batch, Rectangle bounds, Ff7BattleSession battle)
    {
        if (battle.ResultReady)
        {
            DrawWindow(batch, new Rectangle(50, 91, 220, 67), bounds);
            string result = battle.Outcome switch { Ff7BattleOutcome.Victory => "Victory!", Ff7BattleOutcome.Escaped => "Escaped", _ => "Game Over" };
            DrawPlain(batch, result, 62, 103, bounds);
            if (battle.Outcome == Ff7BattleOutcome.Victory)
            {
                Ff7BattleReward reward = battle.PreviewVictory();
                DrawPlain(batch, $"{reward.Experience} EXP   {reward.AbilityPoints} AP   {reward.Gil} Gil", 62, 122, bounds);
            }
            DrawPlain(batch, "Confirm", 62, 140, bounds);
            return;
        }
        DrawWindow(batch, new Rectangle(113, 171, 207, 69), bounds);
        DrawPlain(batch, "Party        HP       MP      ATB", 120, 176, bounds);
        foreach (Ff7BattleActorState actor in battle.Actors.Where(actor => actor.Actor.Slot < 3))
        {
            int slot = actor.Actor.Slot;
            int y = 191 + slot * 14;
            DrawPlain(batch, battle.Name(slot), 120, y, bounds, slot == battle.ReadyPlayer ? 6 : 7);
            DrawPlain(batch, $"{actor.Actor.HitPoints}/{actor.Actor.MaxHitPoints}", 190, y, bounds);
            DrawPlain(batch, actor.Actor.MagicPoints.ToString(), 252, y, bounds);
            Rectangle gauge = new(276, y + 5, 36, 5);
            batch.Draw(pixel, Logical(gauge, bounds), Color.DarkSlateGray);
            if (actor.Gauge != 0)
            {
                gauge.Width = Math.Max(1, gauge.Width * actor.Gauge / ushort.MaxValue);
                batch.Draw(pixel, Logical(gauge, bounds), actor.AwaitingTurn ? Color.Yellow : Color.LightBlue);
            }
            if (actor.LimitGauge != 0)
            {
                Rectangle limit = new(276, y + 11, Math.Max(1, 36 * actor.LimitGauge / byte.MaxValue), 2);
                batch.Draw(pixel, Logical(limit, bounds), actor.LimitGauge == byte.MaxValue ? Color.Yellow : Color.LightCoral);
            }
        }
        if (battle.BeatRushSelecting)
        {
            DrawWindow(batch, new Rectangle(0, 173, 114, 67), bounds);
            DrawPlain(batch, "Beat Rush", 8, 179, bounds);
            DrawPlain(batch, battle.BeatRushVisible!.Value.ToString(), 8, 199, bounds, 6);
            DrawPlain(batch, "Confirm to stop", 8, 221, bounds);
        }
        else if (battle.ReadyPlayer is int ready)
        {
            DrawWindow(batch, new Rectangle(0, 157, 114, 83), bounds);
            if (selectedBattleCommand is { } selected)
            {
                Ff7BattleActorState[] targets = BattleTargets(battle, selected);
                int cursor = Math.Min(battleTargetIndex, targets.Length - 1);
                DrawRows(targets.Select(target => battle.Name(target.Actor.Slot)).ToArray(), cursor);
                if (selected.GroupUses > 0)
                {
                    DrawPlain(batch, battleTargetGroup ? "All targets" : "Single target", 8, 143, bounds, 6);
                }
            }
            else
            {
                DrawRows(battle.Commands(ready).Where(command => command.ValidTargets != 0).Select(CommandName).ToArray(), battleCommandIndex);
            }
        }
        string? message = battle.EscapeBlocked ? "Can't escape!" : battle.Message;
        if (message is { Length: > 0 })
        {
            DrawWindow(batch, new Rectangle(10, 9, 300, 23), bounds);
            DrawPlain(batch, message, 16, 15, bounds);
        }
        if (battle.DisplayedHit is { } hit && battle.Tick - hit.Tick < Ff7BattleSession.LogicUpdatesPerSecond)
        {
            string value = hit.Damage.Missed ? "Miss" : Math.Abs(hit.Damage.Damage).ToString();
            DrawPlain(batch, $"{battle.Name(hit.Target)}  {value}", 16, 40, bounds, hit.Damage.Damage < 0 ? 6 : 7);
        }

        void DrawRows(string[] rows, int cursor)
        {
            int start = Math.Max(0, Math.Min(cursor - 2, rows.Length - 5));
            for (int row = start; row < Math.Min(rows.Length, start + 5); row++)
            {
                DrawPlain(batch, rows[row], 8, 162 + (row - start) * 15, bounds, row == cursor ? 6 : 7);
            }
        }
    }

    private string CommandName(Ff7BattleCommandOption option)
    {
        if (option.Command == Ff7BattleCommand.Item)
        {
            menuText ??= Ff7KernelTextResources.Load(installation);
            return menuText.ItemNames[option.Argument] + " " + option.Cost;
        }
        if (option.Command == Ff7BattleCommand.Magic)
        {
            if (mixedCombat is { } mixed && Session?.Battle is { } battle && battle.ReadyPlayer == battle.GuestVitals?.HostSlot)
            {
                return mixed.Actions.MagicName(option.Argument) + " " + option.Cost;
            }
            battleSpellNames ??= KernelTextArchive.ReadPlainStrings(KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path))[9].Span, 128);
            return battleSpellNames[option.Argument] + " " + option.Cost + " MP";
        }
        if (option.Command == Ff7BattleCommand.Limit)
        {
            return option.Argument switch { 0x80 => "Braver", 0x87 => "Big Shot", 0x95 => "Beat Rush", _ => option.Command.ToString() };
        }
        return option.Command.ToString();
    }

    private static Ff7BattleActorState[] BattleTargets(Ff7BattleSession battle, Ff7BattleCommandOption command)
    {
        return battle.Actors.Where(actor => (command.ValidTargets & 1 << actor.Actor.Slot) != 0).OrderByDescending(actor => (actor.Actor.Slot >= 4) == command.DefaultsToEnemies).ToArray();
    }

    private void RenderBattle(Ff7BattleSession battle, Rectangle bounds)
    {
        EnsureMixedBattle(battle);
        if (!ReferenceEquals(presentedBattle, battle))
        {
            battleArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            battleMagicArchive ??= LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
            battleCureTexture ??= Ff7BattleCureResources.Load(installation);
            battleGrenadeTexture ??= Ff7BattleGrenadeResources.Load(installation);
            battleRenderer = new(battleArchive, battle.Formation.LocationId, battleMagicArchive, cureTexture: battleCureTexture, grenadeTexture: battleGrenadeTexture);
            presentedBattle = battle;
        }
        List<Ff7BattleActorVisual> visuals = [];
        foreach (Ff7BattleActorState actor in battle.Actors)
        {
            int slot = actor.Actor.Slot;
            if (!battle.IsActorPresentationReady(slot) || IsMixedBattleSlot(slot))
            {
                continue;
            }
            Ff7BattlePlacement placement = battle.RenderActorPlacement(slot);
            string model;
            int? weapon;
            if (slot < 3)
            {
                model = Ff7BattleAnimationResources.ModelPrefix(actor.Identity, false);
                weapon = battle.WeaponModel(slot);
            }
            else
            {
                bool participating = battle.Action is { } current && (current.Command.ActorSlot == slot || (current.Command.TargetMask & 1 << slot) != 0);
                if ((actor.Actor.HitPoints == 0 || (actor.Actor.Conditions & 8) == 0) && !participating)
                {
                    continue;
                }
                model = Ff7BattleAnimationResources.ModelPrefix(actor.Identity, true);
                weapon = actor.Identity == 16 ? 0 : null;
            }
            (int clip, int frame) = battle.RenderActorPose(slot);
            visuals.Add(new(model, placement.Position, placement.Yaw * MathF.Tau / 4096, clip, frame, weapon, battle.RenderActorLimit(slot)) { ModelModifiers = battle.ActorModelOutput(slot) });
        }
        battleRenderer!.Render(triangles, bounds.Width, bounds.Height, new(battle.GuestRenderCameraOutput, visuals, battle.Action?.Fire, battle.Action?.Ice, battle.Action?.Bolt, Cure: battle.Action?.Cure, Braver: battle.Action?.Braver) { Grenade = battle.Action?.Grenade, BeatRush = battle.Action?.BeatRushEffect, BigShot = battle.Action?.BigShot, Hitmarks = battle.Hitmarks, SecondaryEffects = battle.SecondaryEffects, PrimaryEffects = battle.PrimaryEffects, SmokeShot = battle.Action?.SmokeShot, Smog = battle.Action?.Smog, Destroyed = battle.Action?.Destroyed, SearchScope = battle.Action?.SearchScope, Rockets = battle.Action?.Rockets, StageFade = battle.StageFade, Guest = MixedBattleVisual(battle), GuestEffects = mixedCombat?.Actions.EffectVisual });
    }

    private void ResetBattlePresentation()
    {
        battleRenderer = null;
        presentedBattle = null;
        battleInputOwner = null;
        pendingLimitConfirm = false;
        selectedBattleCommand = null;
        battleCommandIndex = 0;
        battleTargetIndex = 0;
        battleTargetGroup = false;
    }
}

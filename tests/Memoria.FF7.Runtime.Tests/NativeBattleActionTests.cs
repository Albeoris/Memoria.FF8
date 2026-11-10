using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleActionTests
{
    public static void Run()
    {
        StatusClock();
        InstalledActions();
    }

    private static void StatusClock()
    {
        const uint poisonAndSleep = Ff7NativeBattleActionResolver.Poison | Ff7NativeBattleActionResolver.Sleep;
        Ff7BattleStatusTimers timers = Ff7NativeBattleStatusClock.Apply(default, 0, poisonAndSleep);
        Check(timers == new Ff7BattleStatusTimers(0, 100, 10), "Native status admission changed Sleep or Poison duration.");
        Ff7BattleStatusTimers almostDue = new(8000, 1, 1);
        Ff7BattleStatusTimers restored = JsonSerializer.Deserialize<Ff7BattleStatusTimers>(JsonSerializer.Serialize(almostDue));
        Ff7BattleStatusTick due = Ff7NativeBattleStatusClock.Advance(restored, poisonAndSleep, 0);
        Check(due == new Ff7BattleStatusTick(new(354, 0, 10), Ff7NativeBattleActionResolver.Poison, true), "Native status phase changed Sleep expiry or Poison scheduling after restore.");
        Ff7BattleStatusTick slow = Ff7NativeBattleStatusClock.Advance(almostDue, poisonAndSleep | Ff7NativeBattleActionResolver.Slow, 0);
        Check(slow.Timers.Remainder == 81 && slow.PoisonDue, "Slow must halve the original status clock rate.");
        Ff7BattleStatusTick stopped = Ff7NativeBattleStatusClock.Advance(almostDue, poisonAndSleep | 0x400, 0);
        Check(stopped == new Ff7BattleStatusTick(almostDue, poisonAndSleep | 0x400, false), "Stop must freeze the native status clock.");
        Check(Ff7NativeBattleStatusClock.ReadPersistentStatuses(0xFF) == 0x30, "Only Sadness and Fury are copied into the native character status byte.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 23, 302, 0, 140) == 40, "The original Limit gauge truncates the HP ratio before its divisor.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 23, 302, 0x10, 140) == 20, "Sadness must halve original Limit gain.");
        Check(Ff7NativeBattleLimits.GainFromEnemyDamage(0, 23, 302, 0x20, 140) == 80, "Fury must double original Limit gain.");
        byte full = Ff7NativeBattleLimits.GainFromEnemyDamage(240, 23, 302, 0, 140);
        Check(Ff7NativeBattleLimits.IsReady(full) && Ff7NativeBattleLimits.Consume(full) == 0, "Original Limit gauge must saturate and reset on use.");
    }

    private static void InstalledActions()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        InstalledEscape(kernel[2], formations);
        InstalledTargeting(kernel, formations[300]);
        InstalledFieldBattleSetup(installation);
        Ff7BattleDamageActor cloud = new(6, 36, 23, 6, 14, 0, 0, 24, 17);
        Ff7BattleDamageActor enemy = new(5, 21, 18, 52, 4, 0, 0, 48, 36, IsEnemy: true);
        Ff7BattleActionVitals cloudVitals = new(200, 302, 11, 56, 0);
        Ff7BattleActionVitals enemyVitals = new(15, 75, 0, 0, 0);
        Ff7BattleRng NewRandom() => new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle());

        Ff7BattleActionReceipt Item(ushort itemId, Ff7BattleActionVitals vitals)
        {
            Ff7BattleAttack record = Ff7NativeBattleActionResolver.ReadItem(itemId, kernel);
            return Ff7NativeBattleActionResolver.Resolve(cloud, vitals, cloud, vitals, Ff7BattleDamageAction.FromAttack(record), record, NewRandom(), sameActor: true);
        }

        Check(Item(0, cloudVitals).Target.Hp == 300, "Original Potion must restore exactly 100 HP.");
        Check(Item(3, cloudVitals).Target == cloudVitals with { Mp = 56 }, "Original Ether must restore MP and cap it at the target maximum.");
        Ff7BattleActionReceipt revived = Item(7, cloudVitals with { Hp = 0, Statuses = 1 });
        Check(revived.Target.Hp == 75 && revived.RemovedStatuses == 1, "Original Phoenix Down must revive with one-quarter maximum HP.");
        Check(Item(7, cloudVitals).Target.Hp == 275, "A queued Phoenix Down that resolves after revival must still restore one-quarter maximum HP.");
        Check(Item(8, cloudVitals with { Statuses = 8 }).RemovedStatuses == 8, "Original Antidote must remove Poison.");
        Ff7BattleAttack grenade = Ff7NativeBattleActionResolver.ReadItem(20, kernel);
        Ff7BattleRng random = NewRandom();
        Ff7BattleActionReceipt thrown = Ff7NativeBattleActionResolver.Resolve(cloud, cloudVitals, enemy, enemyVitals with { Hp = 200, MaxHp = 200 }, Ff7BattleDamageAction.FromAttack(grenade), grenade, random);
        Check(thrown.Damage.Damage == 136 && thrown.Target.Hp == 64 && random.Capture().WordCounter == 0 && random.Capture().Indices[0] == 42, $"Original Grenade damage changed its fixed-power variance: {thrown}.");

        Ff7InventorySlot[] inventory = [new(0, 1), new(3, 2)];
        Check(Ff7NativeBattleActionResolver.TrySpendItem(inventory, 0, out Ff7InventorySlot[] reduced) && reduced.SequenceEqual(new[] { new Ff7InventorySlot(3, 2) }) && inventory.Length == 2, "Original Item cost must remove the exhausted slot once.");
        Ff7BattleAttack bolt = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(0x21 * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        Check(Ff7NativeBattleActionResolver.TrySpendCastingCost(cloudVitals, bolt, out Ff7BattleActionVitals spent) && spent.Mp == 7, "Original Bolt must spend four MP.");
        Check(!Ff7NativeBattleActionResolver.TrySpendCastingCost(cloudVitals with { Statuses = 0x80 }, bolt, out _), "Silence must reject an MP action.");
        Check(!Ff7NativeBattleActionResolver.TrySpendCastingCost(cloudVitals with { Mp = 3 }, bolt, out _), "Insufficient MP must reject an MP action.");

        Ff7BattleAttack Attack(int formationId, int attackId) => SceneBattleData.Read(formations[formationId]).Attacks.Single(entry => entry.Id == attackId).Data;
        Ff7BattleAttack bubbles = Attack(341, 0x127);
        byte[] chanceTable = kernel[2].Data.ToArray();
        chanceTable[0xE1C + 41] = 199;
        random = new(chanceTable, Ff7BattleRng.BeginBattle());
        Ff7BattleActionReceipt statusHit = Ff7NativeBattleActionResolver.Resolve(enemy, enemyVitals, cloud, cloudVitals, Ff7BattleDamageAction.FromAttack(bubbles), bubbles, random);
        Check(statusHit.AddedStatuses == 4 && statusHit.Target.Hp == 200 && random.Capture().Indices[0] == 42, "Original Hell Bubbles must roll Sleep once before the zero-numeric action.");
        chanceTable[0xE1C + 41] = 203;
        random = new(chanceTable, Ff7BattleRng.BeginBattle());
        Ff7BattleActionReceipt statusMiss = Ff7NativeBattleActionResolver.Resolve(enemy, enemyVitals, cloud, cloudVitals, Ff7BattleDamageAction.FromAttack(bubbles), bubbles, random);
        Check(statusMiss.AddedStatuses == 0 && random.Capture().Indices[0] == 42, "A native status roll equal to its threshold must fail.");

        Ff7BattleAttack smog = Attack(352, 0x129);
        chanceTable[0xE1C + 41] = 0;
        random = new(chanceTable, Ff7BattleRng.BeginBattle());
        Ff7BattleDamageAction smogAction = Ff7BattleDamageAction.FromAttack(smog);
        Ff7BattleActionReceipt poisoned = Ff7NativeBattleActionResolver.Resolve(enemy, enemyVitals, cloud, cloudVitals, smogAction, smog, random);
        Check(poisoned.Damage.Damage == 14 && poisoned.AddedStatuses == 8 && random.Capture() is { Stream: 1, WordCounter: 2 } && random.Capture().Indices.SequenceEqual(new byte[] { 46, 22, 10, 5, 2, 1, 0, 0 }), "Original Smog must roll Poison before physical accuracy, variance and critical chance.");
        random = new(chanceTable, Ff7BattleRng.BeginBattle());
        Ff7BattleActionReceipt immune = Ff7NativeBattleActionResolver.Resolve(enemy, enemyVitals, cloud, cloudVitals with { StatusImmunities = 8 }, smogAction, smog, random);
        Check(immune.Damage == poisoned.Damage && immune.AddedStatuses == 0 && random.Capture().Indices[0] == 46, "Poison immunity must preserve the action's admitted RNG and damage.");

        Ff7BattleAttack drain = Attack(348, 0x12C);
        random = NewRandom();
        Ff7BattleActionReceipt drained = Ff7NativeBattleActionResolver.Resolve(enemy, enemyVitals, cloud, cloudVitals with { Hp = 6 }, Ff7BattleDamageAction.FromAttack(drain), drain, random);
        Check(drained.Damage.Damage == 11 && drained.Target.Hp == 0 && drained.AddedStatuses == 1 && drained.Source.Hp == 21 && random.Capture().Indices[0] == 45, "Original Tentacle Drain must credit only the victim's available HP.");
        Check(Ff7NativeBattleActionResolver.Resolve(cloud, cloudVitals, cloud, cloudVitals, Ff7NativeBattleStatusClock.PoisonAttack, null, NewRandom(), sameActor: true).Target.Hp == 191, "Original Poison tick must deal one thirty-second of maximum HP.");
        InstalledLimits(installation, kernel);
    }

    private static void InstalledTargeting(IReadOnlyList<KernelSection> kernel, SceneFormation opening)
    {
        const ushort party = 3;
        const ushort enemies = 0x30;
        Ff7BattleTargetMasks masks = new(party | enemies, party | enemies, 0, party | enemies, party, 0);
        Ff7BattleRngState original = Ff7BattleRng.BeginBattle();
        Ff7BattleRng NewRandom() => new(kernel[2].Data.Span, JsonSerializer.Deserialize<Ff7BattleRngState>(JsonSerializer.Serialize(original))!);
        byte weapon = kernel[5].Data.Span[0];
        Ff7BattleRng random = NewRandom();
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, 0x10, weapon, 0xFFFF, false, masks, random) == 0x10 && random.Capture().Indices[0] == 42, "A still-valid native Attack consumes its one-target byte.");
        Ff7BattleTargetMasks enemyKo = masks with { Available = 0x23, Targetable = 0x23, CloseRange = 0x23, KnockedOut = 0x10 };
        random = NewRandom();
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, 0x10, weapon, 0xFFFF, false, enemyKo, random) == 0x20 && random.Capture().Indices[0] == 42, "A queued Attack whose target died must switch to the remaining enemy.");

        Ff7BattleAttack cure = Ff7BattleAttack.Read(kernel[1].Data.Span);
        Ff7BattleAttack fire = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(0x1B * Ff7BattleAttack.RecordSize));
        Ff7BattleAttack potion = Ff7NativeBattleActionResolver.ReadItem(0, kernel);
        Ff7BattleAttack phoenix = Ff7NativeBattleActionResolver.ReadItem(7, kernel);
        Ff7BattleTargetMasks allyKo = masks with { Available = 0x31, KnockedOut = 2 };
        random = NewRandom();
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, 2, cure.TargetFlags, cure.SpecialFlags, false, allyKo, random) == 1 && random.Capture().Indices[0] == 42, "A queued Cure must choose a living party fallback when its ally target dies.");
        random = NewRandom();
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, 0x10, potion.TargetFlags, potion.SpecialFlags, false, enemyKo, random) == 1 && random.Capture().Indices[0] == 42, "A queued Potion whose selected enemy dies falls back to the default party side.");
        random = NewRandom();
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, 2, phoenix.TargetFlags, phoenix.SpecialFlags, false, allyKo, random) == 2 && Ff7NativeBattleTargets.ResolvePlayer(0, 2, phoenix.TargetFlags, phoenix.SpecialFlags, false, masks, random) == 2, "Phoenix Down must retain its chosen ally through both KO and revival.");
        random = NewRandom();
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, 0x10, fire.TargetFlags, fire.SpecialFlags, false, masks, random) == 0x10 && random.Capture().Indices[0] == 41, "Ordinary Fire retains the original singleton with no selector draw.");
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, enemies, fire.TargetFlags, fire.SpecialFlags, true, enemyKo, random) == 0x20 && random.Capture().Indices[0] == 41, "Original All targeting keeps surviving selected enemies without a singleton draw.");
        Check(Ff7NativeBattleTargets.ResolvePlayer(0, enemies, fire.TargetFlags, fire.SpecialFlags, false, masks, random) == 0x10 && random.Capture().Indices[0] == 42, "Original All selection with no uses left collapses to a random single target.");

        Ff7BattleAttack machineGun = SceneBattleData.Read(opening).Attacks.Single(attack => attack.Id == 272).Data;
        random = NewRandom();
        Check(Ff7NativeBattleTargets.Resolve(4, 2, machineGun.TargetFlags, machineGun.SpecialFlags, false, allyKo, random) == 1 && random.Capture().Indices[0] == 42, "The common target selector must reassign enemy Machine Gun to the remaining player and consume one byte.");
    }

    private static void InstalledFieldBattleSetup(Ff7Installation installation)
    {
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        PcFieldScripts ReadScripts(string name) => PcFieldScripts.Read(PcFieldFile.ReadCompressed(fields.ReadEntry(fields.Find(name).Single())).GetSection(PcFieldSectionKind.ScriptAndText).Span);
        int BattleTrack(PcFieldScripts field, int scriptOffset)
        {
            ReadOnlySpan<byte> data = field.Data.Span;
            Check(data[scriptOffset] == 0xF6, "Expected the original field battle music opcode.");
            int header = checked((int)field.AkaoOffsets[data[scriptOffset + 1]]);
            Check(data.Slice(header, 4).SequenceEqual("AKAO"u8), "Expected the original field AKAO music descriptor.");
            return data[header + 4] - 1;
        }

        PcFieldScripts platform = ReadScripts("md1stin");
        Check(platform.Data.Span.Slice(0x3BA, 3).SequenceEqual(new byte[] { 0x72, 0x20, 0 }) && platform.Data.Span.Slice(0x4C6, 4).SequenceEqual(new byte[] { 0x70, 0, 0x2C, 1 }), "The original platform script requests formation 300 with fanfare suppression.");
        PcFieldScripts reactor = ReadScripts("nmkin_5");
        Check(BattleTrack(reactor, 0x361) == 7 && BattleTrack(reactor, 0x5FA) == 2, "The original Reactor 1 script switches the encounter track before Guard Scorpion.");
        PcFieldScripts bridge = ReadScripts("md8brdg");
        Check(BattleTrack(bridge, 0x5CE) == 7 && bridge.Data.Span.Slice(0x5D3, 3).SequenceEqual(new byte[] { 0x72, 8, 0 }), "The original bridge script stages BAT and forbids escape.");
        Check(BattleTrack(ReadScripts("tunnel_3"), 0x26A) == 7 && BattleTrack(ReadScripts("southmk2"), 0x2E6) == 10, "The original tunnel and Air Buster scripts stage BAT and CHU respectively.");
    }

    private static void InstalledEscape(KernelSection kernel, IReadOnlyList<SceneFormation> formations)
    {
        Check(!Ff7NativeBattleEscape.IsAllowed(formations[300]) && Ff7NativeBattleEscape.IsAllowed(formations[301]) && !Ff7NativeBattleEscape.IsAllowed(formations[301], requestDisablesEscape: true), "Escape admission must combine the original scene and request flags.");
        Check(Ff7NativeBattleEscape.Start(formations[304], 2).Counter == 2 && Ff7NativeBattleEscape.Start(formations[304], 1).Counter == 1, "Escape counter must use the native scene word or the initiative override.");
        Ff7BattleEscapeState before = new(1, 1, 7920, ElapsedTicks: 3, HeldTicks: 2, CachedRoll: 0x3FFF);
        Ff7BattleEscapeState restored = JsonSerializer.Deserialize<Ff7BattleEscapeState>(JsonSerializer.Serialize(before));
        Ff7BattleEscapeState zero = Ff7NativeBattleEscape.AdvanceClock(restored, bothButtonsHeld: true, 0, 0);
        Check(zero == new Ff7BattleEscapeState(0, 1, 1, CachedRoll: 0x3FFF), "The native escape clock must leave the terminal decrement for presentation to acknowledge.");
        Check(Ff7NativeBattleEscape.AdvanceClock(before with { Remainder = 7919 }, true, 0, 0).Remainder == 8192, "The native escape window requires time strictly above 8192.");
        Check(Ff7NativeBattleEscape.AdvanceClock(before with { HeldTicks = 1 }, true, 0, 0).Counter == 1, "Holding the buttons exactly half a native escape window must fail.");
        Check(Ff7NativeBattleEscape.AdvanceClock(before with { CachedRoll = 0x4000 }, true, 0, 0).Counter == 1 && Ff7NativeBattleEscape.AdvanceClock(before with { CachedRoll = 0x4000 }, true, 0, 1).Counter == 0, "The native escape roll must be strictly below 0x4000 except in the initiative types.");

        Ff7BattleRng random = new(kernel.Data.Span, Ff7BattleRng.BeginBattle());
        Ff7BattleEscapeInput blocked = Ff7NativeBattleEscape.UpdatePresentation(zero, true, false, true, false, false, random);
        Check(blocked.ShowBlockedMessage && !blocked.Escaped && blocked.State.AcknowledgedCounter == 1 && blocked.State.CachedRoll == 5890 && random.Capture().WordCounter == 1 && random.Capture().Indices[0] == 43, "A prohibited attempt must preserve the escape counter handshake and consume the original presentation word.");
        Ff7BattleEscapeInput idle = Ff7NativeBattleEscape.UpdatePresentation(blocked.State, false, true, true, false, false, random);
        Check(!idle.Escaped && !idle.ShowBlockedMessage && idle.State.AcknowledgedCounter == 1 && random.Capture().WordCounter == 2, "An idle native presentation tick still consumes a word.");
        Ff7BattleEscapeInput escaped = Ff7NativeBattleEscape.UpdatePresentation(idle.State, true, true, true, false, false, random);
        Check(escaped.Escaped && !escaped.ShowBlockedMessage && escaped.State.AcknowledgedCounter == 0 && Ff7NativeBattleEscape.FieldResult == 8, "An eligible native presentation tick must acknowledge escape and publish field result bit 8.");
    }

    private static void InstalledLimits(Ff7Installation installation, IReadOnlyList<KernelSection> kernel)
    {
        KernelInitialData initial = KernelInitialData.Read(kernel[3].Data.Span);
        Ff7CharacterState[] party = initial.Characters.Take(3).Select(character => new Ff7CharacterState(character.Id, "", character.Level, character.Experience, character.CurrentHp, character.CurrentMp, character.Raw.ToArray())).ToArray();
        Check(party.Select(character => Ff7NativeBattleLimits.ReadGaugeDivisor(character, kernel)).SequenceEqual(new[] { 140, 129, 120 }), "Original starting Limit divisors changed.");
        Check(party.SelectMany(character => Ff7NativeBattleLimits.ReadLearnedIds(character, kernel)).SequenceEqual(new byte[] { 0x80, 0x87, 0x95 }), "Original opening Limit selections changed.");
        IReadOnlyList<Ff7BattleAttack> attacks = Ff7PcExecutableData.LoadBattleLimits(installation);
        Check(attacks[0] is { DamageCalculation: 0x11, Power: 48 } && attacks[7] is { DamageCalculation: 0x11, Power: 52 } && attacks[0x15].AdditionalEffect == 0x13, "Original executable Braver, Big Shot or Beat Rush driver changed.");
        IReadOnlyList<byte> symbols = Ff7PcExecutableData.LoadBeatRushReel(installation);
        Check(symbols.SequenceEqual(new byte[] { 1, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }), "Original Beat Rush reel symbols changed.");

        Ff7BeatRushReel AdvanceTo(int stopAt, bool inputBlocked = false)
        {
            Ff7BeatRushReel reel = Ff7BeatRushReel.Start();
            for (int tick = 1; tick <= stopAt; tick++)
            {
                reel = reel.Advance(tick == stopAt, inputBlocked);
            }
            return reel;
        }

        Check(!AdvanceTo(9).Stopped && !AdvanceTo(56, inputBlocked: true).Stopped, "Original Beat Rush input lockout and menu gate must reject Confirm.");
        Check(AdvanceTo(10).ReadResult(symbols) == Ff7BeatRushSlot.Hit && AdvanceTo(55).ReadResult(symbols) == Ff7BeatRushSlot.Hit, "Original Beat Rush Confirm timings changed a Hit result.");
        Ff7BeatRushReel beforeYeah = JsonSerializer.Deserialize<Ff7BeatRushReel>(JsonSerializer.Serialize(Ff7BeatRushReel.Start(55) with { InputDelay = 0 }));
        Check(beforeYeah.Advance(true).ReadResult(symbols) == Ff7BeatRushSlot.Yeah, "Original Beat Rush must update position before checking a restored Confirm edge.");
        Ff7BattleAttack hit = Ff7NativeBattleLimits.ReadBeatRush(kernel, Ff7BeatRushSlot.Hit)!;
        Ff7BattleAttack yeah = Ff7NativeBattleLimits.ReadBeatRush(kernel, Ff7BeatRushSlot.Yeah)!;
        Check(hit.DamageCalculation == 0xB1 && (hit.SpecialFlags ^ yeah.SpecialFlags) == 0x2000 && Ff7NativeBattleLimits.ReadBeatRush(kernel, Ff7BeatRushSlot.Miss) is null, "Original Beat Rush result must map to the secondary hit, force-critical hit, or no action.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

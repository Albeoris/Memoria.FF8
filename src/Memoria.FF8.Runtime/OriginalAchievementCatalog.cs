namespace Memoria.FF8.Runtime;

public sealed record OriginalAchievementEntry(int Index, AchievementId Id, string NativeKey, string Name, bool Supported, string? Blocker = null);

/// <summary>The original 45-entry catalogue and its implemented conditions. Edition mappings remain explicit.</summary>
public static class OriginalAchievementCatalog
{
    public static AchievementId Id(string suffix) => new("ff8.original." + suffix);

    public static IReadOnlyList<OriginalAchievementEntry> Entries { get; } = Array.AsReadOnly<OriginalAchievementEntry>([
        new(0, Id("gf.quezacotl"), "UNLOCK_GF_QUEZACOTL", "Quezacotl", true),
        new(1, Id("gf.shiva"), "UNLOCK_GF_SHIVA", "Shiva", true),
        new(2, Id("gf.ifrit"), "UNLOCK_GF_IFRIT", "Ifrit", true),
        new(3, Id("gf.siren"), "UNLOCK_GF_SIREN", "Siren", true),
        new(4, Id("gf.brothers"), "UNLOCK_GF_BROTHERS", "Brothers", true),
        new(5, Id("gf.diablos"), "UNLOCK_GF_DIABLOS", "Diablos", true),
        new(6, Id("gf.carbuncle"), "UNLOCK_GF_CARBUNCLE", "Carbuncle", true),
        new(7, Id("gf.leviathan"), "UNLOCK_GF_LEVIATHAN", "Leviathan", true),
        new(8, Id("gf.pandemona"), "UNLOCK_GF_PANDEMONA", "Pandemona", true),
        new(9, Id("gf.cerberus"), "UNLOCK_GF_CERBERUS", "Cerberus", true),
        new(10, Id("gf.alexander"), "UNLOCK_GF_ALEXANDER", "Alexander", true),
        new(11, Id("gf.doomtrain"), "UNLOCK_GF_DOOMTRAIN", "Doomtrain", true),
        new(12, Id("gf.bahamut"), "UNLOCK_GF_BAHAMUT", "Bahamut", true),
        new(13, Id("gf.cactuar"), "UNLOCK_GF_CACTUAR", "Cactuar", true),
        new(14, Id("gf.tonberry"), "UNLOCK_GF_TONBERRY", "Tonberry", true),
        new(15, Id("gf.eden"), "UNLOCK_GF_EDEN", "Eden", true),
        new(16, Id("draw.stock.100"), "DRAW_100_MAGIC", "Magician", true),
        new(17, Id("seed.rank_a"), "REACH_SEED_RANK_A", "Top Rank", true),
        new(18, Id("cards.play"), "CARDGAME_FIRST_TIME", "Card player", true),
        new(19, Id("cards.all"), "COLLECT_ALL_CARDS", "Collector", true),
        new(20, Id("weapon.upgrade"), "UPGRADE_WEAPON_FIRST_TIME", "Handyman", true),
        new(21, Id("hp.maximum"), "REACH_MAX_HP", "Maximum HP", true),
        new(22, Id("gil.maximum"), "REACH_MAX_GIL", "Maximum Gil", true),
        new(23, Id("level.100"), "REACH_LEVEL_100", "Top level", true),
        new(24, Id("finish"), "FINISH_THE_GAME", "End of Game", true),
        new(25, Id("finish.initial_level"), "FINISH_THE_GAME_INITIAL_LEVEL", "Contrived Finish", true),
        new(26, Id("chocobo.capture"), "CAPTURE_CHOCOBO_FIRST_TIME", "Chocobo", true),
        new(27, Id("ragnarok"), "FOUND_RAGNAROK", "Ragnarok", true),
        new(28, Id("salary.first"), "SEED_FIRST_SALARY", "First salary", true),
        // TODO(TD-018): Publish ChocoLoot and Boko-level facts from an implemented Chocobo World import transaction.
        new(29, Id("chocoworld.item"), "CHOCORPG_FIRST_ITEM", "Choco loot", false, "Chocobo World item import is not implemented."),
        // TODO(TD-018): Add the canonical Omega-completion owner and its accepted battle/script mutation before registering this rule.
        new(30, Id("omega"), "BEAT_OMEGA_WEAPON", "Omega Destroyed", false, "Omega completion flag has no current canonical owner; required battle content is not established."),
        new(31, Id("obel_lake"), "OBEL_LAKE_SECRET", "Obel Lake Secret", true),
        new(32, Id("ufo"), "UFO", "UFO", true),
        new(33, Id("cards.cc"), "CARDS_CLUB_MASTER", "Cards Club Master", true),
        new(34, Id("cards.rare_loss"), "LOSER", "Loser", true),
        new(35, Id("cards.wins.100"), "PROFESSIONAL", "Professional Player", true),
        new(36, Id("timber_maniacs"), "TIMBER_MANIACS", "Timber Maniacs", true),
        new(37, Id("magazines.all"), "MAGAZINES_ADDICT", "Magazine Addict", true),
        new(38, Id("draw.points.100"), "MAGIC_FINDER", "Magic Miner", true),
        // TODO(TD-018): Decode the original Chocobo World level operand in its future import owner before enabling this entry.
        new(39, Id("chocoworld.level.100"), "CHICOBO_TOP_LEVEL", "Top Level Boko", false, "Chocobo World level import and its native encoding are not implemented."),
        new(40, Id("blue_magic.all"), "BLUE_MAGICS", "Blue Magic Master", true),
        new(41, Id("angelo.all"), "DOG_TRICKS", "Dog Trainer", true),
        new(42, AchievementRegistry.EnemiesDefeated100, "TOTAL_KILLS_100", "100 kills", true),
        new(43, Id("kills.1000"), "TOTAL_KILLS_1000", "1000 kills", true),
        new(44, Id("kills.10000"), "TOTAL_KILLS_10000", "10000 kills", true)
    ]);

    public static AchievementRegistry CreateRegistry()
    {
        AchievementRegistry registry = AchievementRegistry.CreateCombatRegistry(remastered: false);
        for (int index = 0; index < 16; index++)
        {
            if (index == 5)
            {
                continue;
            }
            int mask = 1 << index;
            State(index, progress => progress.GuardianForceMask is ushort flags ? (flags & mask) != 0 : null);
        }
        State(17, progress => progress.MaximumSeeDRank);
        State(19, progress => progress.AllCards);
        State(20, progress => progress.WeaponUpgraded);
        State(22, progress => progress.MaximumGil);
        State(23, progress => progress.MaximumCharacterLevel);
        State(26, progress => progress.Chocobo);
        State(31, progress => progress.ObelLake);
        registry.Register<OriginalAchievementObservedEvent>(Definition(35, 100), fact => fact.Progress.CardWins);
        State(36, progress => progress.TimberManiacs);
        State(37, progress => progress.MagazineCollection);
        State(40, progress => progress.AllBlueMagic);
        State(41, progress => progress.AllAngeloTricks);
        Action(16, OriginalAchievementActionKind.StockDraw, 100, fact => fact.StockDraw!.Quantity > 0, AchievementScope.AccountCumulative);
        Action(18, OriginalAchievementActionKind.CardPlayed);
        Action(21, OriginalAchievementActionKind.MaximumHitPoints);
        Action(24, OriginalAchievementActionKind.FinishGame);
        Action(25, OriginalAchievementActionKind.FinishGame, predicate: fact => fact.SquallExperience < 7000);
        Action(27, OriginalAchievementActionKind.Ragnarok);
        Action(28, OriginalAchievementActionKind.FirstSalary);
        Action(33, OriginalAchievementActionKind.CardsClubMaster);
        Action(34, OriginalAchievementActionKind.RareCardLost);
        Action(38, OriginalAchievementActionKind.DrawPoint, 100, scope: AchievementScope.AccountCumulative);
        registry.Register<BattleKillTotalObservedEvent>(Definition(5), fact => fact.RewardPolicy == BattleRewardPolicy.NativeCombat && fact.Outcome == BattleOutcome.Victory ? Boolean(fact.DiablosAcquired) : null);
        registry.Register<BattleKillTotalObservedEvent>(Definition(32), fact => fact.RewardPolicy == BattleRewardPolicy.NativeCombat && fact.Outcome == BattleOutcome.Victory ? Boolean(fact.UfoCompleted) : null);
        registry.Register<BattleKillTotalObservedEvent>(Definition(43, 1000), fact => QualifyingKills(fact));
        registry.Register<BattleKillTotalObservedEvent>(Definition(44, 10000), fact => QualifyingKills(fact));
        return registry;

        void State(int index, Func<OriginalAchievementProgress, bool?> predicate)
        {
            registry.Register<OriginalAchievementObservedEvent>(Definition(index), fact =>
            {
                fact.Validate();
                return Boolean(predicate(fact.Progress));
            });
        }

        void Action(int index, OriginalAchievementActionKind kind, long target = 1, Func<OriginalAchievementActionEvent, bool>? predicate = null, AchievementScope scope = AchievementScope.SaveHighWater)
        {
            registry.Register<OriginalAchievementActionEvent>(Definition(index, target, scope), fact =>
            {
                fact.Validate();
                return fact.Kind == kind && (predicate is null || predicate(fact)) ? 1 : null;
            });
        }
    }

    private static long? Boolean(bool? value) => value is null ? null : value.Value ? 1 : 0;

    private static AchievementDefinition Definition(int index, long target = 1, AchievementScope scope = AchievementScope.SaveHighWater)
    {
        OriginalAchievementEntry entry = Entries[index];
        return new(entry.Id, entry.Name, entry.Name, target, scope);
    }

    private static long? QualifyingKills(BattleKillTotalObservedEvent fact)
    {
        if (!Enum.IsDefined(fact.Outcome) || !Enum.IsDefined(fact.RewardPolicy))
        {
            throw new InvalidDataException("Combat achievement observation has an invalid outcome or reward policy.");
        }
        return fact.RewardPolicy == BattleRewardPolicy.NativeCombat && fact.Outcome != BattleOutcome.Active ? fact.NativeKillTotal : null;
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public enum BattleResultPage
{
    Experience,
    Items,
    AbilityPoints,
    GuardianForce
}

public sealed partial class BattleHudController
{
    private BattleRewardSummary? resultRewards;
    private NativeCharacterVitals? resultVitals;
    private BattleRewardOffer[] resultLoot = [];
    private bool resultInitialized;
    private bool experienceStarted;
    private bool experienceFinished;
    private long experienceSubticks;
    private long experienceTicks;
    private long resultAgeSubticks = 16 * TimeSpan.TicksPerSecond;
    private long itemTransitionSubticks;
    private int resultItem;
    private int resultGuardianForce;
    private bool itemAcknowledged;
    private IReadOnlyList<string?> resultTexts = [];
    private Func<int, string> resultItemDescription = _ => "";
    private Func<int, string> resultAbilityName = id => $"Ability {id}";

    public BattleResultPage ResultPage { get; private set; }
    public bool ExperienceCounting => experienceStarted && !experienceFinished;
    public bool ExperienceFinished => experienceFinished;
    internal BattleRewardSummary? ResultRewards
    {
        get
        {
            InitializeResults();
            return resultRewards;
        }
    }
    internal int ResultItemIndex => resultItem;
    internal IReadOnlyList<BattleRewardOffer> ResultLoot => resultLoot;
    internal bool ItemAcknowledged => itemAcknowledged;
    internal BattleGuardianForceRewardPresentation? ResultGuardianForce => ResultRewards?.GuardianForceGrowth.ElementAtOrDefault(resultGuardianForce);
    internal int ResultAbilityPoints => ResultRewards is { } rewards ? rewards.AbilityPointsNotice ?? rewards.GuardianForceAbilityPoints.Values.Select(value => (int)value).DefaultIfEmpty().Max() : 0;
    internal float ResultOpacity
    {
        get
        {
            InitializeResults();
            return (float)Math.Clamp(resultAgeSubticks / (16.0 * TimeSpan.TicksPerSecond), 0, 1);
        }
    }

    public void ConfigureResults(IReadOnlyList<string?> texts, Func<int, string>? itemDescription = null, Func<int, string>? abilityName = null)
    {
        resultTexts = texts.ToArray();
        resultItemDescription = itemDescription ?? (_ => "");
        resultAbilityName = abilityName ?? (id => $"Ability {id}");
    }

    private void InitializeResults()
    {
        if (resultInitialized || session.Outcome != BattleOutcome.Victory)
        {
            return;
        }
        resultRewards = previewRewards?.Invoke();
        resultVitals = session.MagicVitals;
        resultLoot = resultRewards is null ? [] : resultRewards.LootOffers.Length > 0 ? resultRewards.LootOffers.ToArray() : [.. resultRewards.Items.Select(item => new BattleRewardOffer(false, item.Id, item.Count, true)), .. resultRewards.Cards.Select(id => new BattleRewardOffer(true, id, 1, true))];
        resultLoot = NativeBattleResultPresentation.GroupLootOffers(resultLoot);
        resultInitialized = true;
        resultAgeSubticks = 0;
    }

    private void AdvanceResults(TimeSpan elapsed)
    {
        if (session.Outcome != BattleOutcome.Victory)
        {
            return;
        }
        InitializeResults();
        long subticks = checked(elapsed.Ticks * BattleSession.FramesPerSecond);
        resultAgeSubticks = checked(resultAgeSubticks + subticks);
        if (ResultPage == BattleResultPage.Items && itemAcknowledged)
        {
            itemTransitionSubticks = checked(itemTransitionSubticks + subticks);
            int duration = resultItem + 1 < resultLoot.Length ? 16 : 32;
            if (itemTransitionSubticks >= duration * TimeSpan.TicksPerSecond)
            {
                AdvanceResultItem();
            }
        }
        if (ExperienceCounting)
        {
            experienceSubticks = checked(experienceSubticks + subticks);
            experienceTicks += experienceSubticks / TimeSpan.TicksPerSecond;
            experienceSubticks %= TimeSpan.TicksPerSecond;
            if (Party.All(actor => RemainingExperience(actor) == 0))
            {
                experienceFinished = true;
            }
        }
    }

    internal ushort AwardedExperience(BattleCombatant actor)
    {
        return byte.TryParse(actor.Id.AsSpan(actor.Id.LastIndexOf('-') + 1), out byte row) ? ResultRewards?.CharacterExperience.GetValueOrDefault(row) ?? 0 : (ushort)0;
    }

    internal BattleCharacterRewardPresentation? CharacterResult(BattleCombatant actor)
    {
        return byte.TryParse(actor.Id.AsSpan(actor.Id.LastIndexOf('-') + 1), out byte row) ? ResultRewards?.CharacterGrowth.FirstOrDefault(growth => growth.Row == row) : null;
    }

    internal uint RemainingExperience(BattleCombatant actor)
    {
        uint awarded = AwardedExperience(actor);
        if (experienceFinished || CharacterResult(actor)?.PreviousLevel >= 100)
        {
            return 0;
        }
        NativeCharacterVitals? vitals = resultVitals;
        BattleCharacterRewardPresentation? growth = CharacterResult(actor);
        long step = 1;
        if (vitals is not null && growth is not null)
        {
            NativeStatCurve curve = vitals.Definitions.Characters[vitals.Records[growth.Row].CharacterId];
            int level = growth.PreviousLevel;
            long lower = (level - 1) * (level - 1) * curve.ExperienceFraction / 256 + (level - 1) * 10 * curve.ExperienceStep;
            long upper = level * level * curve.ExperienceFraction / 256 + level * 10 * curve.ExperienceStep;
            step = Math.Max(1, (upper - lower) >> 7);
        }
        long consumed = Math.Min(awarded, experienceTicks * step);
        if (growth?.FinalLevel >= 100 && consumed >= growth.FinalExperience - growth.PreviousExperience)
        {
            return 0;
        }
        return (uint)Math.Max(0, awarded - consumed);
    }

    internal (uint? Total, int Level, uint? Next) DisplayedExperience(BattleCombatant actor)
    {
        BattleCharacterRewardPresentation? growth = CharacterResult(actor);
        if (growth is null)
        {
            return (null, actor.Level, null);
        }
        if (experienceFinished || growth.PreviousLevel >= 100)
        {
            return (growth.FinalExperience, growth.FinalLevel, growth.ExperienceToNextLevel);
        }
        uint total = growth.PreviousExperience + AwardedExperience(actor) - RemainingExperience(actor);
        if (growth.FinalLevel >= 100 && total >= growth.FinalExperience)
        {
            return (growth.FinalExperience, growth.FinalLevel, 0);
        }
        if (resultVitals is not { } vitals)
        {
            return (total, growth.PreviousLevel, growth.PreviousExperienceToNextLevel);
        }
        NativeStatCurve curve = vitals.Definitions.Characters[vitals.Records[growth.Row].CharacterId];
        int level = NativeStatDefinitions.Level(total, curve);
        uint threshold = (uint)(level * level * curve.ExperienceFraction / 256 + level * 10 * curve.ExperienceStep);
        return (total, level, level >= 100 ? 0 : threshold - total);
    }

    internal string ResultText(int id, string fallback, int number = 0, string? subject = null)
    {
        string encoded = id < resultTexts.Count && resultTexts[id] is { } text ? text : resultTexts.Count == 0 ? fallback : throw new NotSupportedException($"Battle results require native message {id}.");
        encoded = Regex.Replace(encoded, @"\{(?:Var|Dialog)\s*(35|37|38)\}", _ => subject ?? "");
        encoded = encoded.Replace("{Dialog CardLevel}", number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal).Replace("{Dialog 32}", number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        NativeMessageVariables variables = NativeMessageVariables.FromColdProcess().WithWrite(0, number);
        return DialogueTextFormatter.Format(encoded, battleNames, variables, NativeButtonBindingHint);
    }

    internal string ResultItemName(int index)
    {
        BattleRewardSummary? rewards = ResultRewards;
        if (rewards is null)
        {
            return "";
        }
        BattleRewardOffer offer = resultLoot[index];
        return !offer.IsCard ? itemName(offer.Id) : ResultText(110, "{Var 35} Card", subject: cardName(offer.Id));
    }

    internal string ResultDescription(int index) => index < resultLoot.Length && !resultLoot[index].IsCard ? resultItemDescription(resultLoot[index].Id) : "";
    internal string ResultLearnedAbility(int id) => resultAbilityName(id);

    public void ConfirmResult()
    {
        if (session.Outcome != BattleOutcome.Victory || completionRequested)
        {
            return;
        }
        InitializeResults();
        if (resultAgeSubticks < 16 * TimeSpan.TicksPerSecond)
        {
            return;
        }
        if (ResultPage == BattleResultPage.Experience)
        {
            if (!experienceStarted)
            {
                experienceStarted = true;
                experienceSubticks = 0;
                experienceFinished = Party.All(actor => AwardedExperience(actor) == 0);
            }
            else if (!experienceFinished)
            {
                experienceFinished = true;
            }
            else
            {
                ResultPage = BattleResultPage.Items;
                resultAgeSubticks = 0;
            }
        }
        else if (ResultPage == BattleResultPage.Items)
        {
            int count = resultLoot.Length;
            if (count > 0 && !itemAcknowledged)
            {
                itemAcknowledged = true;
                itemTransitionSubticks = 0;
            }
            else if (count == 0 || resultItem + 1 < count)
            {
                AdvanceResultItem();
            }
            else
            {
                return;
            }
        }
        else if (ResultPage == BattleResultPage.AbilityPoints)
        {
            BeginGuardianForceResults();
        }
        else if (++resultGuardianForce >= (resultRewards?.GuardianForceGrowth.Length ?? 0))
        {
            Complete();
        }
        else
        {
            resultAgeSubticks = 0;
        }
        if (!completionRequested)
        {
            SoundRequested?.Invoke(MenuSound.Confirm);
        }
    }

    private void AdvanceResultItem()
    {
        if (++resultItem < resultLoot.Length)
        {
            itemAcknowledged = false;
        }
        else if (ResultAbilityPoints > 0)
        {
            ResultPage = BattleResultPage.AbilityPoints;
            resultAgeSubticks = 0;
        }
        else
        {
            BeginGuardianForceResults();
        }
    }

    private void BeginGuardianForceResults()
    {
        if (resultRewards?.GuardianForceGrowth.Length > 0)
        {
            ResultPage = BattleResultPage.GuardianForce;
            resultAgeSubticks = 0;
        }
        else
        {
            Complete();
        }
    }
}

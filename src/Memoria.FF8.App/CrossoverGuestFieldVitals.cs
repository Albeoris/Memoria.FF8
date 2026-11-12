using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class CrossoverGuestFieldVitals : IFf7GuestFieldVitals, IDisposable
{
    private readonly GameAssets assets;
    private readonly Func<CombinedSessionSave> read;
    private readonly Action<CombinedSessionSave> publish;

    public CrossoverGuestFieldVitals(string directory, Func<CombinedSessionSave> read, Action<CombinedSessionSave> publish)
    {
        assets = new(directory);
        this.read = read;
        this.publish = publish;
    }

    public IReadOnlyList<int> StoryCharacters => read().GetMixedCombatBindings().Select(binding => binding.StoryCharacter).ToArray();

    public Ff7GuestFieldVitalsView Read() => ReadAll().Single();

    public IReadOnlyList<Ff7GuestFieldVitalsView> ReadAll()
    {
        CombinedSessionSave source = read();
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(assets.ReadNewGameCharacterVitals());
        return source.GetMixedCombatBindings().Select(binding => ReadGuest(document, vitals, binding)).ToArray();
    }

    public void Recover(bool clearStatuses) => RecoverCharacters(StoryCharacters, clearStatuses);
    public void RecoverCharacters(IReadOnlyCollection<int> storyCharacters, bool clearStatuses) => Update(storyCharacters, null, clearStatuses);
    public void AddHitPoints(short amount) => AddHitPoints(StoryCharacters.Single(), amount);
    public void AddHitPoints(int storyCharacter, short amount) => Update([storyCharacter], amount, false);

    private void Update(IReadOnlyCollection<int> storyCharacters, short? amount, bool clearStatuses)
    {
        CombinedSessionSave source = read();
        Ff7MixedCombatBinding[] affected = source.GetMixedCombatBindings().Where(binding => storyCharacters.Contains(binding.StoryCharacter)).ToArray();
        if (affected.Length == 0)
        {
            return;
        }
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(assets.ReadNewGameCharacterVitals());
        NativeCharacterVital[] rows = (NativeCharacterVital[])document.Characters.Records.Clone();
        foreach (Ff7MixedCombatBinding binding in affected)
        {
            int row = ReadGuestRow(vitals, binding.OriginCharacter);
            NativeCharacterVital saved = vitals.Records[row];
            ushort maximum = CrossoverGuestRecord.MaximumHitPoints(vitals, row, binding.OriginCharacter);
            ushort hp = amount is short delta ? checked((ushort)Math.Clamp((int)saved.HitPoints.Minimum + delta, 0, maximum)) : maximum;
            ushort statuses = (ushort)(saved.Status & ~(clearStatuses ? Ff7GuestBattleStatuses.SupportedOrigin : 1));
            if (hp == 0)
            {
                statuses |= 1;
            }
            rows[row] = saved with { HitPoints = new(hp, hp), Status = statuses };
        }
        using MemoryStream destination = new();
        GameplaySaveCodec.Write(destination, document with { Characters = document.Characters with { Records = rows } });
        publish(source with { Ff8Payload = destination.ToArray() });
    }

    private static Ff7GuestFieldVitalsView ReadGuest(GameplaySaveComponentDocument document, NativeCharacterVitals vitals, Ff7MixedCombatBinding binding)
    {
        int row = ReadGuestRow(vitals, binding.OriginCharacter);
        NativeCharacterVital saved = vitals.Records[row];
        return new(binding.StoryCharacter, ReadGuestName(document, binding.OriginCharacter), NativeStatDefinitions.Level(saved.Experience, vitals.Definitions.Characters[binding.OriginCharacter]), saved.HitPoints.Minimum, CrossoverGuestRecord.MaximumHitPoints(vitals, row, binding.OriginCharacter));
    }

    internal static string ReadGuestName(GameplaySaveComponentDocument document, int character)
    {
        int subject = GameNames.CharacterSubject(character);
        return document.Progression?.SceneFacts?.Names.GetValueOrDefault(subject) ?? document.Progression?.OriginalResidue?.Names.GetValueOrDefault(subject) ?? GameNames.DefaultName(subject);
    }

    private static int ReadGuestRow(NativeCharacterVitals vitals, int character)
    {
        int row = CrossoverGuestRecord.FindOriginalRow(vitals, character);
        if (row < 0)
        {
            throw new NotSupportedException($"The suspended FFVIII save has no {CrossoverCharacterCatalog.AppearanceNames[character]} record.");
        }
        return row;
    }

    public void Dispose() => assets.Dispose();
}

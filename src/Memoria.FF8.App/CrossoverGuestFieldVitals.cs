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

    public Ff7GuestFieldVitalsView Read()
    {
        CombinedSessionSave source = read();
        Ff7MixedCombatBinding binding = source.MixedCombat ?? throw new InvalidOperationException("Field vitals require a mapped FFVIII character.");
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(assets.ReadNewGameCharacterVitals());
        int row = ReadGuestRow(vitals, binding.OriginCharacter);
        NativeCharacterVital saved = vitals.Records[row];
        return new(binding.StoryCharacter, ReadGuestName(document, binding.OriginCharacter), NativeStatDefinitions.Level(saved.Experience, vitals.Definitions.Characters[binding.OriginCharacter]), saved.HitPoints.Minimum, vitals.GetMagicMaximumHitPoints(row));
    }

    public void Recover(bool clearStatuses) => Update(null, clearStatuses);
    public void AddHitPoints(short amount) => Update(amount, false);

    private void Update(short? amount, bool clearStatuses)
    {
        CombinedSessionSave source = read();
        Ff7MixedCombatBinding binding = source.MixedCombat ?? throw new InvalidOperationException("Field vitals require a mapped FFVIII character.");
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(assets.ReadNewGameCharacterVitals());
        int row = ReadGuestRow(vitals, binding.OriginCharacter);
        NativeCharacterVital saved = vitals.Records[row];
        ushort maximum = vitals.GetMagicMaximumHitPoints(row);
        ushort hp = amount is short delta ? checked((ushort)Math.Clamp((int)saved.HitPoints.Minimum + delta, 0, maximum)) : maximum;
        NativeHpWord exact = vitals.WithSavedHitPoints(row, hp).Records[row].HitPoints;
        ushort statuses = (ushort)(saved.Status & ~(clearStatuses ? Ff7GuestBattleStatuses.SupportedOrigin : 1));
        if (hp == 0)
        {
            statuses |= 1;
        }
        NativeCharacterVital[] rows = (NativeCharacterVital[])document.Characters.Records.Clone();
        rows[row] = saved with { HitPoints = exact, Status = statuses };
        using MemoryStream destination = new();
        GameplaySaveCodec.Write(destination, document with { Characters = document.Characters with { Records = rows } });
        publish(source with { Ff8Payload = destination.ToArray() });
    }

    internal static string ReadGuestName(GameplaySaveComponentDocument document, int character)
        => document.Progression?.SceneFacts?.Names.GetValueOrDefault(character) ?? CrossoverCharacterCatalog.AppearanceNames[character];

    private static int ReadGuestRow(NativeCharacterVitals vitals, int character)
    {
        int row = vitals.FindPhysicalRow(checked((byte)character));
        if ((uint)row >= vitals.Records.Length)
        {
            throw new NotSupportedException($"The suspended FFVIII save has no {CrossoverCharacterCatalog.AppearanceNames[character]} record.");
        }
        return row;
    }

    public void Dispose() => assets.Dispose();
}

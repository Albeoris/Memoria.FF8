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
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(assets.ReadNewGameCharacterVitals());
        int row = vitals.FindPhysicalRow(0);
        NativeCharacterVital saved = vitals.Records[row];
        return new(source.MixedCombat!.StoryCharacter, document.Progression?.SceneFacts?.Names.GetValueOrDefault(0) ?? "Squall", NativeStatDefinitions.Level(saved.Experience, vitals.Definitions.Characters[0]), saved.HitPoints.Minimum, vitals.GetMagicMaximumHitPoints(row));
    }

    public void Recover(bool clearStatuses) => Update(null, clearStatuses);
    public void AddHitPoints(short amount) => Update(amount, false);

    private void Update(short? amount, bool clearStatuses)
    {
        CombinedSessionSave source = read();
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(assets.ReadNewGameCharacterVitals());
        int row = vitals.FindPhysicalRow(0);
        NativeCharacterVital saved = vitals.Records[row];
        ushort maximum = vitals.GetMagicMaximumHitPoints(row);
        ushort hp = amount is short delta ? checked((ushort)Math.Clamp((int)saved.HitPoints.Minimum + delta, 0, maximum)) : maximum;
        NativeHpWord exact = vitals.WithSavedHitPoints(row, hp).Records[row].HitPoints;
        ushort statuses = clearStatuses ? (ushort)0 : (ushort)(saved.Status & ~1);
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

    public void Dispose() => assets.Dispose();
}

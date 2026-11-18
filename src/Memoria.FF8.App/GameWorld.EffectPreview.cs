using System.Numerics;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record EffectPreviewRequest([property: JsonRequired] int EnemyId, [property: JsonRequired] byte AllyId, ushort? EffectId = null)
{
    public int? AbilityId { get; init; }
    public byte? CommandId { get; init; }
    internal byte AbilityCommand => CommandId ?? 3;
    public int StageId { get; init; }
    public byte Seed { get; init; }
    public double DurationSeconds { get; init; } = 300;
    public DiagnosticPresentationDeclaration? Presentation { get; init; }

    internal void Validate()
    {
        if (EnemyId is < 0 or > 238 || AllyId > 10 || EffectId is < 1 or > 400 || StageId is < 0 or > 255 || !double.IsFinite(DurationSeconds) || DurationSeconds <= 0 || DurationSeconds > 3600 || EffectId.HasValue == AbilityId.HasValue || AbilityId is < 0 or > ushort.MaxValue || CommandId is not null && AbilityId is null)
        {
            throw new InvalidDataException("Effect preview requires valid native enemy, ally, effect and stage identities and a bounded duration.");
        }
        if (AbilityId is not null && AbilityCommand is not (2 or 3))
        {
            throw new NotSupportedException("Whole ability preview currently supports native Magic command2 and GF command3.");
        }
        Presentation?.Validate();
    }

    internal BattleEncounter CreateArena()
    {
        BattleEnemyPlacement enemy = new(0, EnemyId, new Vector3(0, 0, -2100), 1, false, false) { NativePosition = new(0, 0, -2100) };
        return new(0, StageId, 0, 0, 0, [enemy]);
    }
}

internal sealed partial class GameWorld
{
    private EffectPreviewRequest? effectPreviewRequest;

    internal BattleSession StartEffectPreview(EffectPreviewRequest request, EffectComponentPreviewSource? source = null)
    {
        request.Validate();
        if (source is not null)
        {
            return StartEffectComponentPreview(request, source);
        }
        effectPreviewRequest = request;
        NativeCharacterVitals vitals = session.State.CharacterVitals ?? throw new NotSupportedException("Graphical preview requires its initialized saved character owner.");
        int[] rows = vitals.Records.Select((record, row) => (record, row)).Where(candidate => candidate.record.CharacterId == request.AllyId).Select(candidate => candidate.row).ToArray();
        if (rows.Length != 1)
        {
            throw new InvalidDataException("Graphical preview requires one saved character row for its selected identity.");
        }
        int row = rows[0];
        DiagnosticBattlePreparation fixture = new([], [new(row, request.AllyId, [], [])], true) { CombatParty = [checked((byte)row), 255, 255] };
        BattleSession active = StartDiagnosticBattle(new(0, request.Seed, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, Fixture = fixture, AtbMode = BattleAtbMode.Normal });
        active.ConfigureDiagnosticEffectPreview();
        for (int frame = 0; frame < 3600 && (!active.NativeEntryReady || active.ObserveDiagnosticDrainBlockers().Count != 0); frame++)
        {
            active.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
        }
        if (!active.NativeEntryReady || active.ObserveDiagnosticDrainBlockers().Count != 0)
        {
            throw new NotSupportedException("The graphical preview arena did not finish its native model/resource initialization.");
        }
        SynchronizeNativeBattleHostCallbacks(active);
        active.FillDiagnosticEffectPreviewAllyAtb();
        return active;
    }
}

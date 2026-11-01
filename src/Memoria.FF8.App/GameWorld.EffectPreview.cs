using System.Numerics;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record EffectPreviewRequest([property: JsonRequired] int EnemyId, [property: JsonRequired] byte AllyId, [property: JsonRequired] ushort EffectId)
{
    public int StageId { get; init; }
    public byte Seed { get; init; }
    public double DurationSeconds { get; init; } = 300;
    public DiagnosticPresentationDeclaration? Presentation { get; init; }

    internal void Validate()
    {
        if (EnemyId is < 0 or > 238 || AllyId > 10 || EffectId is < 1 or > 400 || StageId is < 0 or > 255 || !double.IsFinite(DurationSeconds) || DurationSeconds <= 0 || DurationSeconds > 3600)
        {
            throw new InvalidDataException("Effect preview requires valid native enemy, ally, effect and stage identities and a bounded duration.");
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

    internal BattleSession StartEffectPreview(EffectPreviewRequest request)
    {
        request.Validate();
        effectPreviewRequest = request;
        DiagnosticBattlePreparation fixture = new([], [new(0, request.AllyId, [], [])], true) { CombatParty = [0, 255, 255] };
        BattleSession active = StartDiagnosticBattle(new(0, request.Seed, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, Fixture = fixture, AtbMode = BattleAtbMode.Normal });
        active.ConfigureDiagnosticEffectPreview();
        for (int callback = 0; callback < 1800 && (!active.NativeEntryReady || active.ObserveDiagnosticDrainBlockers().Count != 0); callback++)
        {
            active.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1d / 30));
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

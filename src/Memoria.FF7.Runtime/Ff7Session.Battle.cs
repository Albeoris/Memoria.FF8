using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private IReadOnlyList<SceneFormation>? battleFormations;
    private IReadOnlyList<KernelSection>? battleKernel;

    private int CompleteDebugBattle(FieldHostRequest request)
    {
        FieldBattleCommand battle = request.Command as FieldBattleCommand ?? throw new InvalidDataException("FFVII victory requires an owned battle request.");
        battleFormations ??= SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        battleKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = battleFormations.SingleOrDefault(formation => formation.FormationId == battle.Formation) ?? throw new InvalidDataException("FFVII battle formation is absent from the installed scene archive.");
        Progress = Ff7DebugVictory.Apply(Progress, formation, battleKernel, FieldGeneration, request.Id);
        return 0;
    }
}

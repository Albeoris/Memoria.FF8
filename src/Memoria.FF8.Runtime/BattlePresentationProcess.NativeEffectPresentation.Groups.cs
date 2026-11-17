using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private ImmutableArray<BattleModelOwner> ReadPresentationSideActors(bool party)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        return party ? ReadRequiredPresentationPartyActors() : [.. graphics.ActorModels.Enemies.Entries.Select(entry => entry.Owner)];
    }
}

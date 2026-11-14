using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableArray<int> ReadPresentationSideActors(bool party)
    {
        if (presentationForeignActors is null)
        {
            return Enumerable.Range(party ? 0 : 3, party ? 3 : 4).ToImmutableArray();
        }
        return party ? ReadRequiredPresentationPartyActors() : presentationForeignActors.Values.Where(actor => !actor.IsParty).Select(actor => actor.Index).Order().ToImmutableArray();
    }
}

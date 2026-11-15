using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private bool TryAdvanceAutoPlayPartyMenu()
    {
        if (autoPlay is not { Running: true, Paused: false } || Session is not { } session || session.Host.Process.Menu is not { Mode: 7, Completed: false } menu)
        {
            return false;
        }
        if (menu.Parameter != 0)
        {
            throw new NotSupportedException($"Automatic play cannot complete native party selector parameter {menu.Parameter}.");
        }
        int[] members = ChooseAutoPlayStoryParty(session.Progress, session.GuestFieldVitals?.ReadAll());
        if (!session.CompleteStoryPartyMenu(menu.RequestId, members))
        {
            throw new InvalidOperationException("The native party selector rejected the automatic party or its menu ownership.");
        }
        storyPartyMenuRequest = null;
        storyPartyMembers = members;
        ClosePlayerMenu();
        autoPlay.ResetIdleDeadline();
        return true;
    }

    internal static int[] ChooseAutoPlayStoryParty(Ff7Progress progress, IReadOnlyList<Ff7GuestFieldVitalsView>? guests = null)
    {
        if (progress.Party.Length != 3)
        {
            throw new InvalidDataException("The native party selector requires three party slots.");
        }
        Dictionary<int, int> hitPoints = progress.Characters.ToDictionary(character => character.Id, character => character.HitPoints);
        foreach (Ff7GuestFieldVitalsView guest in guests ?? [])
        {
            hitPoints[guest.StoryCharacter] = guest.Hp;
        }
        int[] members = [255, 255, 255];
        HashSet<int> selected = [];
        for (int slot = 0; slot < members.Length; slot++)
        {
            int id = progress.Party[slot];
            if (id is < 0 or > 8)
            {
                continue;
            }
            bool available = (progress.AvailableCharacters & 1 << id) != 0;
            bool locked = (progress.LockedCharacters & 1 << id) != 0;
            if (locked && !available)
            {
                throw new NotSupportedException("The native party selector has an unavailable locked party member.");
            }
            if (available && (locked || hitPoints.GetValueOrDefault(id) > 0))
            {
                if (!selected.Add(id))
                {
                    throw new InvalidDataException("The native party selector contains duplicate current party members.");
                }
                members[slot] = id;
            }
        }
        for (int slot = 0; slot < members.Length; slot++)
        {
            if (members[slot] != 255)
            {
                continue;
            }
            int replacement = Enumerable.Range(0, 9).FirstOrDefault(id => (progress.AvailableCharacters & 1 << id) != 0 && (progress.LockedCharacters & 1 << id) == 0 && hitPoints.GetValueOrDefault(id) > 0 && !selected.Contains(id), -1);
            if (replacement < 0)
            {
                throw new NotSupportedException("Automatic play cannot form a native party of three from the available living members and locked slots.");
            }
            members[slot] = replacement;
            selected.Add(replacement);
        }
        return members;
    }
}

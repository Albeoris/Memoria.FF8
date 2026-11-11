using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private string? identityFf8Directory;
    private Ff7CharacterPresentationIdentity characterIdentity = new([]);

    public void ConfigureCharacterIdentity(string ff8Directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ff8Directory);
        identityFf8Directory = ff8Directory;
        RefreshCharacterIdentity();
    }

    private void RefreshCharacterIdentity()
    {
        characterIdentity = Ff7CharacterPresentationIdentity.FromSave(envelope);
        DisposeNamingPresentation();
        commonDialoguePages.Clear();
        if (Session is not null)
        {
            AttachCharacterIdentity(Session);
        }
    }

    private void AttachCharacterIdentity(Ff7Session session)
    {
        characterIdentity = Ff7CharacterPresentationIdentity.FromSave(envelope);
        session.PresentationNameResolver = (character, nativeName) => characterIdentity.Resolve(character, nativeName).Name;
        ApplyMappedCombat(session);
    }
}

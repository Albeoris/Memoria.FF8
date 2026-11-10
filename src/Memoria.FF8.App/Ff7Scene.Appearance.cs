using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    public void SetAppearance(CharacterAppearanceMapping[] mappings, IFf7ActorAppearance? selected)
    {
        CombinedSessionSave prepared = Capture() with { Mappings = mappings };
        prepared.Validate();
        envelope = prepared;
        RefreshCharacterIdentity();
        appearance = selected;
        renderer = null;
        renderedGeneration = -1;
        ResetWorldPresentation();
    }
}

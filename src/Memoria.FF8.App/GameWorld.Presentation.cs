using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    //A field/world transition exits the native callback before the skin/draw consumers.
    //Its departing VM deliberately retains the previous draw sample. Present the last
    //complete framebuffer until the destination is allocated; do not relabel that sample
    //with the new callback frame or ask the dormant field to produce another image.
    internal bool CanPresentCurrentField => Field is not null && session.State.Scene is SceneKind.Field or SceneKind.Movie && Field.Id == session.State.FieldId && Events.FieldId == Field.Id && Events.NextTrigger.Kind is not (PlayerTriggerKind.FieldTransition or PlayerTriggerKind.WorldMap);
}

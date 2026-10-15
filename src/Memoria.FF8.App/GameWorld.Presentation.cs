using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    //A field/world transition exits the native callback before the skin/draw consumers.
    //Its departing VM deliberately retains the previous draw sample. Present the last
    //complete framebuffer until the destination is allocated; do not relabel that sample
    //with the new callback frame or ask the dormant field to produce another image.
    internal bool CanPresentCurrentField => Field is not null && session.State.Scene is SceneKind.Field or SceneKind.Movie && Field.Id == session.State.FieldId && Events.FieldId == Field.Id && Events.NextTrigger.Kind is not (PlayerTriggerKind.FieldTransition or PlayerTriggerKind.WorldMap) && !HasPendingInitialModelCallback;

    //52C650/531980 initialize and register models without running472B30/530810.
    //A visible model can first be presented after the ordinary callback executes
    //its skin sample. Rendering must neither invent that sample nor run its clock.
    private bool HasPendingInitialModelCallback => Events.Scene.NativeFieldInitialization?.Stage == NativeFieldInitializationStage.Completed && Events.Scene.FieldCallbackClock is { InitializationTransactions: 1, OrdinaryCallbacks: 0, MovieCallbacks: 0 } && Events.Scene.ModelParts is { } parts && parts.Instances.Any(instance => Events.Actors[instance.ObjectId] is { Active: true, Visible: true });
}

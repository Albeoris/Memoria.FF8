using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private enum FieldMutationBoundary
    {
        None,
        Transition,
        Party,
        Tutorial
    }

    private FieldMutationBoundary ActiveFieldMutationBoundary
    {
        get
        {
            // World scenes retain the departed field checkpoint as history, including its transition receipt.
            if (State.Scene != SceneKind.Field || State.EventState is not { ValueKind: JsonValueKind.Object } checkpoint)
            {
                return FieldMutationBoundary.None;
            }
            static bool Present(JsonElement checkpoint, string name) => checkpoint.TryGetProperty(name, out JsonElement value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            if (checkpoint.TryGetProperty("TransitionRequested", out JsonElement transition) && transition.ValueKind == JsonValueKind.True)
            {
                return FieldMutationBoundary.Transition;
            }
            if (Present(checkpoint, "PartyJoin") || Present(checkpoint, "PartySplit"))
            {
                return FieldMutationBoundary.Party;
            }
            if (checkpoint.TryGetProperty("Scene", out JsonElement scene) && scene.ValueKind == JsonValueKind.Object && (Present(scene, "NativeTutorial") || Present(scene, "NativeMenuTutorial")))
            {
                return FieldMutationBoundary.Tutorial;
            }
            return FieldMutationBoundary.None;
        }
    }
}

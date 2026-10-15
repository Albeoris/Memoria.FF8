using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeFieldButtonInput? FieldButtonInput => State.FieldButtonInput;
    public bool CanStoreFieldButtonInput => true;

    public void SetFieldButtonInput(NativeFieldButtonInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        input.Validate();
        State = State with { FieldButtonInput = input };
    }

    public NativeInputConfiguration? InputConfiguration => State.InputConfiguration;
    public bool CanStoreInputConfiguration => true;

    public void InitializeNewGameInputConfiguration(NativeInputConfiguration configuration)
    {
        if (State.InputConfiguration is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Input initialization requires the untouched authentic New Game template load.");
        }
        configuration.Validate();
        State = State with { InputConfiguration = configuration };
    }

    public void SetInputConfiguration(NativeInputConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        State = State with { InputConfiguration = configuration };
    }

    private static void ValidateInputConfigurationCheckpoint(GameState state, JsonElement? vm)
    {
        state.InputConfiguration?.Validate();
        if (vm is not JsonElement snapshot)
        {
            return;
        }
        if (!snapshot.TryGetProperty("InputConfiguration", out JsonElement saved))
        {
            throw new InvalidDataException("Saved VM is missing its canonical native input configuration; migrate this save explicitly.");
        }
        if (!snapshot.TryGetProperty("FieldButtonInput", out JsonElement savedInput) || !JsonElement.DeepEquals(savedInput, JsonSerializer.SerializeToElement(state.FieldButtonInput, JsonOptions)))
        {
            throw new InvalidDataException("Root and VM native input sampler processes disagree.");
        }
        JsonElement expected = JsonSerializer.SerializeToElement(state.InputConfiguration, JsonOptions);
        if (!JsonElement.DeepEquals(saved, expected))
        {
            throw new InvalidDataException("Root and VM native input configurations disagree.");
        }
    }
}

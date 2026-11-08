namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleComponentTerminalTextureTests
{
    [Theory]
    [InlineData(3, 2)]
    [InlineData(3, 6)]
    [InlineData(7, 6)]
    public void Resident_FF_branch_preserves_payload_program_and_texture_callbacks_across_cold_next(ushort modelFlags, ushort resourceFlags)
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(0, 14, new([255], 0));
        NativeBattleGraphicsModel model = new(0, modelFlags) { ResourceFlags44 = resourceFlags, Sequence = 14 };
        string frozen = NativeBattleTestSnapshots.Json(state);
        NativeBattleAuxiliaryState cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleAuxiliaryState>(frozen)!;
        NativeBattleAuxiliaryInputs inputs = new()
        {
            ReadModel = _ => model,
            ApplyTexture = (_, _, _) => throw new InvalidOperationException("A terminal FF deadline must not copy texture bytes."),
            RestoreTextureBlink = _ => throw new InvalidOperationException("A stationary resident task must not release blink ownership.")
        };
        for (int callback = 0; callback < 3; callback++)
        {
            state = state.Advance(inputs).State;
            cold = cold.Advance(inputs).State;
            Assert.Equal(frozen, NativeBattleTestSnapshots.Json(state));
            Assert.Equal(frozen, NativeBattleTestSnapshots.Json(cold));
        }
        Assert.NotNull(state.Head);
        Assert.Equal((byte)255, state.Slots[state.Head!.Value].Program!.Source[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Resident_FF_task_still_restores_and_releases_when_native_model_predicates_change(int change)
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(0, 14, new([255], 0));
        NativeBattleGraphicsModel model = new(0, change == 0 ? (ushort)1 : (ushort)3) { ResourceFlags44 = change == 1 ? (ushort)0x42 : (ushort)2, Sequence = change == 2 ? (byte)15 : (byte)14 };
        int copies = 0;
        int restores = 0;
        NativeBattleAuxiliaryState next = state.Advance(new()
        {
            ReadModel = _ => model,
            ApplyTexture = (_, selector, variant) =>
            {
                Assert.Equal(0, selector);
                Assert.Equal(0, variant);
                copies++;
            },
            RestoreTextureBlink = _ => restores++
        }).State;
        Assert.Equal(1, copies);
        Assert.Equal(1, restores);
        Assert.Null(next.Head);
    }

    [Fact]
    public void Nonterminal_deadline_still_advances_elapsed_and_cannot_be_treated_as_stationary()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(0, 14, new([254, 255], 0));
        NativeBattleGraphicsModel model = new(0, 3) { ResourceFlags44 = 2, Sequence = 14 };
        NativeBattleAuxiliaryState next = state.Advance(new() { ReadModel = _ => model }).State;
        Assert.NotEqual(state.Slots[0].Payload.ToArray(), next.Slots[0].Payload.ToArray());
        Assert.Equal(0, next.Slots[0].Program!.Offset);
    }
}

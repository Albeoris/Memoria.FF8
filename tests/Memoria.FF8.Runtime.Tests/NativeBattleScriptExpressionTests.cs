using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptExpressionTests
{
    [Theory]
    [InlineData(0xC6, 13)]
    [InlineData(0xCA, 7)]
    [InlineData(0xCE, 30)]
    [InlineData(0xD2, 3)]
    [InlineData(0xD6, 2)]
    [InlineData(0xDA, 11)]
    [InlineData(0xDE, 9)]
    [InlineData(0xE2, 1)]
    public void ExpressionsUseTheOriginalDwordArithmetic(byte opcode, int expected)
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = NativeBattleScriptValue.Scalar(10) };
        int cursor = 1;
        byte ReadByte()
        {
            cursor++;
            return 3;
        }
        NativeBattleScriptExpressions.Execute(opcode, 0, ref cursor, state, ReadByte, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(expected, state.Accumulator!.Value.RequireScalar());
        Assert.Equal(2, cursor);
    }

    [Theory]
    [InlineData(0xC4, 9, 3)]
    [InlineData(0xC5, 9, 2)]
    [InlineData(0xC6, 265, 2)]
    public void OperandWidthsDistinguishSignedWordSignedByteAndUnsignedByte(byte opcode, int expected, int next)
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = NativeBattleScriptValue.Scalar(10) };
        int cursor = 1;
        byte ReadByte()
        {
            cursor++;
            return 255;
        }
        NativeBattleScriptExpressions.Execute(opcode, 0, ref cursor, state, ReadByte, _ => NativeBattleScriptValue.Scalar(0), (_, _) => throw new InvalidOperationException());
        Assert.Equal(expected, state.Accumulator!.Value.RequireScalar());
        Assert.Equal(next, cursor);
    }

    [Theory]
    [InlineData(0xE6, true)]
    [InlineData(0xE7, false)]
    [InlineData(0xE8, true)]
    [InlineData(0xE9, true)]
    [InlineData(0xEA, false)]
    [InlineData(0xEB, true)]
    [InlineData(0xEC, false)]
    [InlineData(0xED, true)]
    [InlineData(0xEE, false)]
    [InlineData(0xEF, true)]
    [InlineData(0xF0, true)]
    [InlineData(0xF1, false)]
    [InlineData(0xF2, true)]
    [InlineData(0xF3, false)]
    public void BranchesAreRelativeToTheirOpcodeAndPreserveTheirSignedWidth(byte opcode, bool taken)
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = NativeBattleScriptValue.Scalar(0) };
        int cursor = 101;
        int operand = 0;
        byte ReadByte()
        {
            cursor++;
            return operand++ == 0 ? (byte)0xFE : (byte)0xFF;
        }
        NativeBattleScriptExpressions.Execute(opcode, 100, ref cursor, state, ReadByte, _ => NativeBattleScriptValue.Scalar(0), (_, _) => throw new InvalidOperationException());
        Assert.Equal(taken ? 98 : opcode < 0xED ? 102 : 103, cursor);
    }

    [Fact]
    public void TemporaryDwordsAreLocalToOneCallbackAndVariablesAreOwnedByTheCaller()
    {
        NativeBattleScriptExpressionState state = new() { Accumulator = NativeBattleScriptValue.Scalar(int.MinValue) };
        int cursor = 1;
        byte NextTemporary()
        {
            cursor++;
            return 128;
        }
        NativeBattleScriptExpressions.Execute(0xE5, 0, ref cursor, state, NextTemporary, _ => NativeBattleScriptValue.Scalar(0), (_, _) => throw new InvalidOperationException());
        state.Accumulator = NativeBattleScriptValue.Scalar(0);
        NativeBattleScriptExpressions.Execute(0xC3, 0, ref cursor, state, NextTemporary, _ => NativeBattleScriptValue.Scalar(0), (_, _) => throw new InvalidOperationException());
        Assert.Equal(int.MinValue, state.Accumulator!.Value.RequireScalar());
        Assert.Throws<InvalidDataException>(() => NativeBattleScriptExpressions.Execute(0xC3, 0, ref cursor, new(), NextTemporary, _ => NativeBattleScriptValue.Scalar(0), (_, _) => throw new InvalidOperationException()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Camera_side_comparison_preserves_an_unaliased_actor_in_scratch_across_yield(bool enemy)
    {
        var boundary = new NativeBattleModelBoundary(Enumerable.Repeat(new NativeBattleGraphicsModel(0), 7));
        BattleModelOwner actor = enemy ? new BattleEnemyActorModel(new("added-enemy")) : new BattlePartyActorModel(new("added-party"));
        var models = new BattleActorModels([new BattleModelEntry<BattleModelOwner>(actor, new NativeBattleGraphicsModel(0))]);
        NativeBattleScriptValue operand = NativeBattleScriptValue.CaptureActor(actor, models, boundary);
        Assert.Throws<NotSupportedException>(() => operand.RequireScalar());
        NativeBattleCameraBank bank = new([]);
        ImmutableArray<byte> program = [0xC3, 25, 0xE5, 127, 1, 0xC2, 3, 0xCB, 127, 0xEB, 4, 7, 2, 6, 2];
        NativeBattleCameraState initial = NativeBattleCameraState.Initialize() with { ControlCursor = 0 };
        NativeBattleCameraState first = initial.AdvanceControl(bank, program, new() { ReadExternal = _ => operand });
        Assert.Equal(5, first.ControlCursor);
        Assert.Null(first.Scratch[0]);
        Assert.Empty(initial.ControlValues);

        NativeBattleCameraState branched = first.AdvanceControl(bank, program, new());
        Assert.Equal(enemy, branched.ForceCamera);
        Assert.Null(branched.ControlCursor);

        NativeBattleCameraState overwritten = (first with { ControlCursor = 0 }).AdvanceControl(bank, [0xC2, 42, 0xE5, 127, 2], new());
        Assert.Equal(42, overwritten.ReadControlWord(127).RequireScalar());
        Assert.Throws<NotSupportedException>(() => first.ReadControlWord(127).RequireScalar());
    }

    [Fact]
    public void Original_operand_keeps_its_read_time_alias_but_retained_camera_binding_resolves_current_owner()
    {
        var boundary = new NativeBattleModelBoundary(Enumerable.Repeat(new NativeBattleGraphicsModel(0), 7));
        BattleActorModels models = new([]);
        var first = new BattleEnemyActorModel(new("authored-allocation"));
        var next = new BattleEnemyActorModel(new("replacement"));
        var extra = new BattleEnemyActorModel(new("extra"));
        models = models.Add(extra, new NativeBattleGraphicsModel(0));
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(3);
        (boundary, models) = boundary.BindOriginalSlot(slot, first, models);
        NativeBattleScriptValue operand = NativeBattleScriptValue.CaptureActor(first, models, boundary);
        NativeBattleModelAliases aliases = boundary.CaptureAliases(models);
        BattleRetainedModelReference retained = BattleRetainedModelReference.Capture(first, aliases);
        BattleRetainedModelReference canonical = BattleRetainedModelReference.Capture(extra, aliases);
        var camera = NativeBattleCameraAliases.Unknown().Observe50D300(first, BattleModelTargets.From([extra]), models, aliases);

        (boundary, models) = boundary.BindOriginalSlot(slot, next, models);
        Assert.Equal(3, operand.RequireScalar());
        Assert.True(NativeBattleScriptValue.Subtract(NativeBattleScriptValue.Scalar(3), operand).TakeBranch(3));
        Assert.Equal(next, retained.Resolve(models, boundary));
        Assert.Equal(next, camera.Resolve50D790(0x90, models, boundary));
        Assert.Equal(extra, canonical.Resolve(models, boundary));
        Assert.Equal(extra, camera.Resolve50D790(0xA0, models, boundary));
        BattleActorModels retired = models.Remove(extra);
        Assert.Throws<InvalidDataException>(() => canonical.Resolve(retired, boundary));
        Assert.Throws<InvalidDataException>(() => camera.Resolve50D790(0xA0, retired, boundary));
    }
}

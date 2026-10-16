using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldAllocatedContactPredicateTests
{
    [Theory]
    [InlineData(512, true)]
    [InlineData(511, false)]
    [InlineData(1537, true)]
    [InlineData(3072, false)]
    public void BroadContactUsesTheOriginalSignedFacingComparison(int bodyYaw, bool expected)
    {
        Assert.Equal(expected, Evaluate(0xFF1D, 88, bodyYaw, -1, 0, [new(88, new(0, 0, -100))]));
    }

    [Fact]
    public void PrimaryAndBroadIndicesSelectDistinctActualObjects()
    {
        (byte Shape, NativeWorldPathPosition Position)[] objects = [new(88, new(100, 0, 0)), new(89, new(100, 0, 0))];
        Assert.True(Evaluate(0xFF1B, 88, 0, 0, 1, objects));
        Assert.False(Evaluate(0xFF1D, 88, 0, 0, 1, objects));
        Assert.True(Evaluate(0xFF1D, 89, 0, 0, 1, objects));
    }

    [Theory]
    [InlineData(0xFF1A, true)]
    [InlineData(0xFF1B, false)]
    [InlineData(0xFF1C, true)]
    [InlineData(0xFF1D, false)]
    public void OnlyTheNativeNonVehicleVariantsExcludeTheSevenIdentitySlots(ushort opcode, bool expected)
    {
        Assert.Equal(expected, Evaluate(opcode, 88, 0, 0, 0, [new(88, new(100, 0, 0))], [0, -1, -1, -1, -1, -1, -1]));
    }

    [Fact]
    public void MissingContactsAndSignedShapeBytesDoNotBecomeProximityMatches()
    {
        (byte Shape, NativeWorldPathPosition Position)[] objects = [new(255, new(100, 0, 0))];
        Assert.Null(Evaluate(0xFF1D, 65535, 0, -1, null, objects));
        Assert.False(Evaluate(0xFF1D, 65535, 0, -1, -1, objects));
        Assert.False(Evaluate(0xFF1D, 65535, 0, -1, 0, objects));
        Assert.Throws<InvalidDataException>(() => Evaluate(0xFF1D, 88, 0, -1, 1, objects));
    }

    [Fact]
    public void FacingUsesNativeGraphicsZAndNormalizesTheYawSeam()
    {
        WorldFieldSelectionInput input = new(-18900, 47000, 1350, 48);
        NativeWorldPathPosition ship = new(-18900, 0, -46850);
        Assert.True(WorldAllocatedContactPredicate.Evaluate(input, 0xFF1D, 88, 3072, -1, 0, 1, _ => 88, _ => ship, []));
        Assert.True(Evaluate(0xFF1D, 88, 4095, -1, 0, [new(88, new(100, 0, 0))]));
    }

    [Fact]
    public void OriginalWhiteSeeDClauseSelectsItsPacketOnlyFromTheRetainedBroadContact()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldFieldSelectionInput input = new(-18900, 47000, 1350, 48);
        NativeWorldPathPosition ship = new(-18900, 0, -46850);
        bool? Predicate(ushort opcode, ushort operand, int broad)
        {
            if (opcode == 0xFF25)
            {
                return operand == 0;
            }
            if (opcode == 0xFF20)
            {
                return new WorldFieldInputContext(0, 0, false, -1, -1, -1, -1).Matches(operand);
            }
            Assert.Equal((ushort)0xFF1D, opcode);
            Assert.Equal((ushort)88, operand);
            return WorldAllocatedContactPredicate.Evaluate(input, opcode, operand, 3072, -1, broad, 1, _ => 88, _ => ship, []);
        }
        NativeWorldEventFlags flags = new(0, ulong.MaxValue);
        WorldFieldSelection? selected = assets.WorldEvents.SelectWorldEvent(input, _ => false, flags, (opcode, operand) => Predicate(opcode, operand, 0));
        Assert.NotNull(selected);
        Assert.Equal(17, selected.Entry.Index);
        Assert.Equal(853, selected.Entry.FieldId);
        Assert.Contains(selected.Predicates, predicate => predicate.Opcode == 0xFF1D && predicate.Operand == 88 && predicate.Passed == true);
    }

    private static bool? Evaluate(ushort opcode, ushort shape, int bodyYaw, int? primary, int? broad, IReadOnlyList<(byte Shape, NativeWorldPathPosition Position)> objects, int[]? excluded = null)
    {
        return WorldAllocatedContactPredicate.Evaluate(new(0, 0, 1350, 48), opcode, shape, bodyYaw, primary, broad, objects.Count, index => objects[index].Shape, index => objects[index].Position, excluded ?? []);
    }
}

using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Theory]
    [InlineData(5, 6553, -64, 22)]
    [InlineData(-2, -16384, 160, -55)]
    [InlineData(-1, -32768, 321, -111)]
    public void GuestDecelerationUsesSignedWordDivisionAndOnlyItsThreeAccelerationWords(short divisor, short x, short y, short z)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFF16, unchecked((ushort)divisor));
        frame = PutWord(PutWord(PutWord(frame, 0x22, 0x8000), 0x24, 321), 0x26, unchecked((ushort)-111));
        frame = PutWord(PutWord(frame, 0x28, 0xABCD), 0x30, 0x1234);
        List<string> order = [];
        NativeBattleWaterParticleServices services = Services() with
        {
            RequireHandler = (table, index) =>
            {
                Assert.Equal("pop_table", table);
                Assert.Equal(22, index);
                order.Add("require");
            },
            ExecuteSelected = (current, program) =>
            {
                order.Add("execute");
                return NativeBattleGuestParticle22.Execute(current, program);
            }
        };
        NativeBattleWaterExecutionFrame result = One(frame, services);
        Assert.Equal(new[] { "require", "execute" }, order);
        Assert.Equal(unchecked((ushort)x), Word(result, 0x2A));
        Assert.Equal(unchecked((ushort)y), Word(result, 0x2C));
        Assert.Equal(unchecked((ushort)z), Word(result, 0x2E));
        Assert.Equal((ushort)0xABCD, Word(result, 0x28));
        Assert.Equal((ushort)0x1234, Word(result, 0x30));
        Assert.Equal((ushort)0x8000, Word(result, 0x22));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void GuestDecelerationDoesNotInventAValueForZeroDivisionOrAMissingOperand()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(22, 0);
        NativeBattleWaterScriptProgram program = new(0x1000, 4, 0x1000);
        Assert.Throws<DivideByZeroException>(() => NativeBattleGuestParticle22.Execute(frame, program));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleGuestParticle22.Execute(frame, program));
        Assert.Throws<NotSupportedException>(() => One(Fixture(22, 4)));
    }
}

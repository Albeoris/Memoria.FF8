using System.Buffers.Binary;
using System.Text;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAngeloTrainingDefinitionTests
{
    [Fact]
    public void ReadsWordTextOffsetsSeparateDescriptionsAndByteTrainingTotals()
    {
        (byte[] records, byte[] messages) = Fixture();
        records[4] = 0;
        records[5] = 255;
        records[7 * 8 + 4] = 255;

        var result = NativeAngeloTrainingDefinitionReader.Read(records, messages, Encoding.ASCII);

        Assert.Equal(8, result.Length);
        Assert.All(result, move =>
        {
            Assert.Equal("Training name", move.Name);
            Assert.Equal("Training description", move.Description);
        });
        Assert.Equal((byte)0, result[0].TrainingTotal);
        Assert.Equal((byte)255, result[7].TrainingTotal);
    }

    [Fact]
    public void AdditionalAngelWingRowIsNotPartOfTheEightTrainingMoves()
    {
        (byte[] records, byte[] messages) = Fixture();
        byte[] withWing = new byte[9 * 8];
        records.CopyTo(withWing, 0);
        Array.Fill(withWing, (byte)255, 8 * 8, 8);

        var result = NativeAngeloTrainingDefinitionReader.Read(withWing, messages, Encoding.ASCII);

        Assert.Equal(8, result.Length);
        Assert.Equal(NativeAngeloTrainingDefinitionReader.Read(records, messages, Encoding.ASCII).ToArray(), result.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(65)]
    public void RejectsMissingOrPartialTrainingRecords(int length)
    {
        Assert.Throws<InvalidDataException>(() => NativeAngeloTrainingDefinitionReader.Read(new byte[length], [0], Encoding.ASCII));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RejectsNameOrDescriptionOffsetsAtTheMessageBoundary(int fieldOffset)
    {
        (byte[] records, byte[] messages) = Fixture();
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(fieldOffset), checked((ushort)messages.Length));

        Assert.Throws<InvalidDataException>(() => NativeAngeloTrainingDefinitionReader.Read(records, messages, Encoding.ASCII));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RejectsUnterminatedNameOrDescriptionText(int fieldOffset)
    {
        (byte[] records, byte[] messages) = Fixture();
        messages[^1] = (byte)'X';
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(fieldOffset), checked((ushort)(messages.Length - 1)));

        Assert.Throws<InvalidDataException>(() => NativeAngeloTrainingDefinitionReader.Read(records, messages, Encoding.ASCII));
    }

    private static (byte[] Records, byte[] Messages) Fixture()
    {
        byte[] records = new byte[8 * 8];
        byte[] messages = new byte[320];
        Encoding.ASCII.GetBytes("Training name").CopyTo(messages, 260);
        Encoding.ASCII.GetBytes("Training description").CopyTo(messages, 280);
        for (int index = 0; index < 8; index++)
        {
            Span<byte> row = records.AsSpan(index * 8, 8);
            BinaryPrimitives.WriteUInt16LittleEndian(row, 260);
            BinaryPrimitives.WriteUInt16LittleEndian(row[2..], 280);
            row[4] = (byte)(index + 1);
        }
        return (records, messages);
    }
}

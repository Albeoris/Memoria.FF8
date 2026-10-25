using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace Memoria.FF8.Assets;

public sealed record NativeAngeloTrainingDefinition(string Name, string Description, byte TrainingTotal);

public static class NativeAngeloTrainingDefinitionReader
{
    public static ImmutableArray<NativeAngeloTrainingDefinition> Read(byte[] records, byte[] messages, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(encoding);
        if (records.Length < 8 * 8 || records.Length % 8 != 0)
        {
            throw new InvalidDataException("Angelo training requires eight complete menu records.");
        }
        string Text(int offset)
        {
            if ((uint)offset >= messages.Length)
            {
                throw new InvalidDataException("Angelo menu text offset is outside its message resource.");
            }
            int end = Array.IndexOf(messages, (byte)0, offset);
            if (end < 0)
            {
                throw new InvalidDataException("Angelo menu text requires a terminating zero.");
            }
            return encoding.GetString(messages, offset, end - offset);
        }
        ImmutableArray<NativeAngeloTrainingDefinition>.Builder result = ImmutableArray.CreateBuilder<NativeAngeloTrainingDefinition>(8);
        for (int index = 0; index < 8; index++)
        {
            ReadOnlySpan<byte> row = records.AsSpan(index * 8, 8);
            result.Add(new NativeAngeloTrainingDefinition(Text(BinaryPrimitives.ReadUInt16LittleEndian(row)), Text(BinaryPrimitives.ReadUInt16LittleEndian(row[2..])), row[4]));
        }
        return result.MoveToImmutable();
    }
}

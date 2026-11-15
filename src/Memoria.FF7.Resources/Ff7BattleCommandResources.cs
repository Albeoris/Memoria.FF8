using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed class Ff7BattleCommandResources
{
    private readonly IReadOnlyList<string> names;

    private Ff7BattleCommandResources(IReadOnlyList<string> names)
    {
        this.names = names;
    }

    public static Ff7BattleCommandResources Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        if (!StringComparer.OrdinalIgnoreCase.Equals(installation.Language, "en"))
        {
            throw new NotSupportedException("The battle command text profile requires the original English string tables.");
        }
        IReadOnlyList<ReadOnlyMemory<byte>> sections = KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path));
        return Read(sections[8].Span);
    }

    public static Ff7BattleCommandResources Read(ReadOnlySpan<byte> commandNameSection)
    {
        if (commandNameSection.Length < 64 || BinaryPrimitives.ReadUInt16LittleEndian(commandNameSection) != 64)
        {
            throw new InvalidDataException("The command name category requires a directory of 32 entries.");
        }
        return new(KernelTextArchive.ReadPlainStrings(commandNameSection, 32));
    }

    public string Name(byte command)
    {
        if (command >= names.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }
        return names[command];
    }
}

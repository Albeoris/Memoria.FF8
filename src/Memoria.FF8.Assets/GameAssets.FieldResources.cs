using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record FieldLogicResources(int Id, string Name, byte[] Program, IReadOnlyList<string> Messages, FieldInformation Information)
{
    public bool HasColorCorrections { get; init; } = true;
}

public sealed partial class GameAssets
{
    /// <summary>Reads a coherent resource snapshot without initializing actors or rendering the field.</summary>
    public FieldLogicResources ReadFieldLogicResources(string fieldName)
    {
        int id = Array.FindIndex(fieldNames, name => name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (id < 0 || string.IsNullOrWhiteSpace(fieldName))
        {
            throw new InvalidDataException($"The installed field map does not contain {fieldName}.");
        }
        string name = fieldNames[id];
        Dictionary<string, byte[]> members = new(StringComparer.OrdinalIgnoreCase);
        byte[] ReadMember(string member)
        {
            if (!members.TryGetValue(member, out byte[]? bytes))
            {
                bytes = resources.Read(new ResourceKey($"field/{name}/{member}"));
                members.Add(member, bytes);
            }
            return bytes;
        }
        byte[] program = ReadMember(name + ".jsm");
        byte[] informationBytes = ReadMember(name + ".inf");
        FieldInformation information = FieldInformation.Read(informationBytes);
        string[] messages = MsdFile.Read(ReadMember(name + ".msd"), FF8Encoding.CreateEuropean()).Texts.ToArray();
        NativeEncounterTables.ReadField(ReadMember(name + ".rat"), ReadMember(name + ".mrt"));
        byte[]? colors = ReadOptionalFieldColors(() => ReadMember(name + ".pcb"));
        if (NativeRuntimeProfile is { IsVerified: true })
        {
            int allocationPrefix = ReadNativeFieldAllocationPrefix(name, ReadMember);
            ReadNativeProgramAllocationEnd(program, allocationPrefix);
            NativeFieldRegistrationLoaderInputs loader = ReadNativeFieldRegistrationInputs(id, informationBytes, program, colors, allocationPrefix);
            loader.Validate(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(program)));
        }
        return new(id, name, program, messages, information) { HasColorCorrections = colors is not null };
    }

    private static byte[]? ReadOptionalFieldColors(Func<byte[]> read)
    {
        try
        {
            return read();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}

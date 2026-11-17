using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// Inspects historical fixture data without restoring its presentation process.
internal static class NativeBattleFixtureInspection
{
    internal static Dictionary<string, string> CollectBinaryHashes()
    {
        Assembly[] assemblies = [typeof(BattleScenePresentation).Assembly, typeof(BattleSession).Assembly, typeof(GameAssets).Assembly, typeof(Memoria.FF8.Rendering.ITriangleRenderer).Assembly, typeof(NativeBattleFixtureInspection).Assembly];
        Dictionary<string, string> hashes = [];
        foreach (Assembly assembly in assemblies)
        {
            string name = assembly.GetName().Name ?? throw new InvalidDataException("An executing binary has no assembly identity.");
            hashes.Add(name, Hash(assembly.Location));
        }
        return hashes;
    }

    internal static void ValidateActionIdentity(NativeBattleDescriptorPacket descriptor, NativeBattleActionPacketsState? packets, NativeBattleActionGraphicsState? action, ushort effect)
    {
        Require(packets is not null, "The standalone action packet owner is absent.");
        uint offset = unchecked(descriptor.Argument - 0x1D280C4u);
        Require(offset % 20 == 0 && offset / 20 < NativeBattleRetainedActionPackets.HeaderCapacity, "The action104 descriptor does not address a physical20-byte action header.");
        int index = (int)(offset / 20);
        Require(packets!.ReadHeader(new BattleActionHeaderReference(index)).Effect == effect, "The queued action104 header selects a different effect from the expected fixture.");
        Require(action is null || action.Header.Ordinal == index, "The started graphics action does not belong to the queued action104 header.");
    }

    internal static bool IsInitialized(NativeBattleOriginalEffectState? state, ushort effect, uint entry)
    {
        return state is { ReturnedDescriptor: > 0 } && state.Binding.Effect == effect && state.Binding.Entry == entry;
    }

    internal static JsonElement ReadPointer(JsonElement root, string pointer)
    {
        if (!pointer.StartsWith('/'))
        {
            throw new InvalidDataException("Snapshot observables require an absolute JSON pointer.");
        }
        JsonElement value = root;
        foreach (string encoded in pointer[1..].Split('/'))
        {
            string segment = encoded.Replace("~1", "/").Replace("~0", "~");
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(segment, out JsonElement property))
            {
                value = property;
            }
            else if (value.ValueKind == JsonValueKind.Array && int.TryParse(segment, out int index) && index >= 0 && index < value.GetArrayLength())
            {
                value = value[index];
            }
            else
            {
                throw new InvalidDataException($"Required snapshot observable '{pointer}' is absent; owners cannot be reconstructed.");
            }
        }
        return value;
    }

    internal static BattleSnapshot ReadLogicalSnapshot(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement source = document.RootElement;
        if (source.ValueKind == JsonValueKind.Object &&
            (source.TryGetProperty("NativeGraphics", out _) || source.TryGetProperty("NativeCommandBanks", out _)))
        {
            throw new NotSupportedException("This historical battle fixture requires case-specific migration before execution. Use current battle admission and private in-process continuation.");
        }
        return source.Deserialize<BattleSnapshot>() ?? throw new InvalidDataException("The standalone snapshot is null.");
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}

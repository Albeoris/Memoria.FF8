using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCheckpointByteEncodingContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(689);
        byte[] content = SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 3), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        string hash = Convert.ToHexString(SHA256.HashData(content));
        NativeFieldRegistrationResources resources = field.NativeRegistration! with { ProgramHash = hash, Loader = field.NativeRegistration!.Loader! with { Program = new("protocol/jsm", hash, content.ToImmutableArray()) } };
        NativeFieldEyeProcess coldProcess = new NativeFieldEyeProcess(1, assets.ReadColdProcessNativeBattleMemory(), null).CaptureSnapshot();
        (GameSession Session, EventEngine Engine) Create()
        {
            GameSession session = GameSession.CreateColdProcess(coldProcess.RestoreOwned().Memory);
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.RequestField(field.Id, 0);
            EventEngine engine = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                ReadNativeEyes = model => field.Models[model].NativeEyes,
                ReadNativeFieldRegistrationResources = () => resources
            };
            engine.LoadField(field.Id, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            return (session, engine);
        }

        var host = Create();
        NativeBattleFileStaging working = host.Session.CurrentNativeBattleMemory!.FileStaging!;
        uint address = checked((uint)working.KnownExtentEnd);
        byte[] original = [0, 255, 17, 42];
        working.Write(address, original);
        JsonElement compact = host.Engine.Capture();
        JsonElement encodedMemory = compact.GetProperty("Scene").GetProperty("NativeEyeProcess").GetProperty("Memory");
        Require(encodedMemory.GetProperty("FileStaging").GetProperty("KnownRanges").EnumerateArray().All(range => range.GetProperty("Data").ValueKind == JsonValueKind.String), "Native field checkpoint staging bytes were not encoded as base64.");
        NativeFieldEyeProcess frozen = compact.GetProperty("Scene").GetProperty("NativeEyeProcess").Deserialize<NativeFieldEyeProcess>()!;
        Require(frozen.Memory.FileStaging!.Read(address, original.Length).AsSpan().SequenceEqual(original), "Compact field checkpoint changed physical staging bytes.");

        JsonNode legacyNode = JsonNode.Parse(compact.GetRawText())!;
        ConvertRegionsToLegacyArrays(legacyNode);
        JsonElement legacy = JsonSerializer.SerializeToElement(legacyNode);
        Require(legacy.GetProperty("Scene").GetProperty("NativeEyeProcess").GetProperty("Memory").GetProperty("FileStaging").GetProperty("KnownRanges").EnumerateArray().All(range => range.GetProperty("Data").ValueKind == JsonValueKind.Array), "Legacy checkpoint fixture did not retain numeric byte arrays.");
        var compactHost = Create();
        var legacyHost = Create();
        compactHost.Engine.Restore(compact);
        legacyHost.Engine.Restore(legacy);
        Require(JsonElement.DeepEquals(compactHost.Engine.Capture(), legacyHost.Engine.Capture()), "Legacy numeric and base64 checkpoints restored different canonical field state.");
        compactHost.Engine.Step();
        legacyHost.Engine.Step();
        Require(JsonElement.DeepEquals(compactHost.Engine.Capture(), legacyHost.Engine.Capture()), "Legacy and base64 restored native field callbacks diverged.");

        working.Write(address, [23, 24, 25, 26]);
        compactHost.Session.CurrentNativeBattleMemory!.FileStaging!.Write(address, [31, 32, 33, 34]);
        Require(frozen.Memory.FileStaging.Read(address, original.Length).AsSpan().SequenceEqual(original), "A live write mutated the detached checkpoint's physical bytes.");
        Require(legacyHost.Session.CurrentNativeBattleMemory!.FileStaging!.Read(address, original.Length).AsSpan().SequenceEqual(original), "Independent restored checkpoints shared a mutable staging authority.");
        Require(working.Read(address, original.Length).AsSpan().SequenceEqual(new byte[] { 23, 24, 25, 26 }), "Writing a restored checkpoint mutated its source native authority.");
        Console.WriteLine("Native field checkpoint byte encoding: compact base64, historical numeric arrays, identical restored callback and independent native staging writes.");
    }

    private static void ConvertRegionsToLegacyArrays(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (obj.Count == 2 && obj.ContainsKey("Offset") && obj["Data"] is JsonValue data && data.TryGetValue<string>(out string? encoded))
            {
                obj["Data"] = JsonSerializer.SerializeToNode(Convert.FromBase64String(encoded).Select(value => (int)value).ToArray());
            }
            foreach (var property in obj)
            {
                ConvertRegionsToLegacyArrays(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                ConvertRegionsToLegacyArrays(item);
            }
        }
    }
}

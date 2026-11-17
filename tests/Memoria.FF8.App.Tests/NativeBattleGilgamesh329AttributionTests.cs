using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGilgamesh329AttributionTests
{
    [Fact]
    public void External_saved_Excaliber_pair_has_exact_model_prefix_and_isolated_effect_pixels()
    {
        string? priorPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH329_PRIOR");
        string? currentPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH329_CURRENT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH329_ATTRIBUTION_OUTPUT");
        Assert.SkipUnless(priorPath is not null && currentPath is not null && output is not null, "Select an external adjacent Excaliber snapshot pair and a distinct attribution directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        var (prior, priorCallback, _) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(priorPath!, 329);
        var (current, currentCallback, frame) = NativeBattleFixtureInspection.ReadArchivedEffectFrame(currentPath!, 329);
        Assert.Equal(priorCallback + 1, currentCallback);
        NativeBattleFixtureState before = ReadInputs(prior);
        NativeBattleFixtureState after = ReadInputs(current);
        NativeBattleGraphicsDefinition definition = Assert.IsType<NativeBattleGraphicsDefinition>(NativeBattleFixtureState.ReadArchived<NativeBattleGraphicsDefinition>(current.GetProperty("Definition"), "NativeGraphics"));
        List<NativeBattleModelPrefixDiagnostic> matches = [];
        ImmutableHashSet<NativeBattlePrimitiveReference> owned = NativeBattleAquaPresentationTests.AquaPacketReferences(before, after, definition, matches.Add);
        File.WriteAllText(Path.Combine(output!, "prefix-diagnostics.json"), JsonSerializer.Serialize(new
        {
            PriorSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(priorPath!))),
            CurrentSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(currentPath!))),
            PriorFrame = prior.GetProperty("Frame").GetInt64(),
            PriorCallback = priorCallback,
            CurrentFrame = current.GetProperty("Frame").GetInt64(),
            CurrentCallback = currentCallback,
            Matches = matches,
            Packets = owned.OrderBy(packet => packet.ByteOffset).ToArray()
        }));
        Assert.Contains(matches, match => match.Start >= 0 && match.BodyPrimitives > 0 && match.MismatchOffset is null);
        Assert.NotEmpty(owned);
        BattleRenderer renderer = new(320, 224, ViewportLayout.Fit(320, 224, 320, 224));
        NativeBattleEffectMemory memory = Assert.IsType<NativeBattleEffectMemory>(NativeBattleFixtureState.ReadArchived<NativeBattleEffectMemory>(current, "NativeEffectMemory"));
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, memory.Textures, capabilities: definition.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess!.TextureWindowCache, effectTextureSource: assets.EffectTextureSource);
        NativeBattlePrimitiveFrame without = frame with { LastCommands = NativeBattleResolvedCommandList.From(frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet))) };
        byte[] plain = renderer.RenderNativePrimitiveFrame(without, memory.Textures, capabilities: definition.PrimitiveCapabilities, textureWindowCache: memory.PrimitiveProcess.TextureWindowCache, effectTextureSource: assets.EffectTextureSource);
        int changed = 0;
        Assert.Equal(pixels.Length, plain.Length);
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            if (!pixels.AsSpan(offset, 3).SequenceEqual(plain.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        File.WriteAllBytes(Path.Combine(output!, "visible.rgba"), pixels);
        File.WriteAllBytes(Path.Combine(output!, "without-effect.rgba"), plain);
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = changed > 0 ? "isolated" : "zero-rgb", Effect = 329, Scope = "Exact saved pair model-prefix attribution; no native callback execution", Frame = current.GetProperty("Frame").GetInt64(), NativeCallback = currentCallback, OwnedPackets = owned.Count, ChangedRgb = changed }));
        Assert.True(changed > 0, "The exactly attributed original effect prefix must contribute visible RGB pixels.");
    }
    private static NativeBattleFixtureState ReadInputs(JsonElement source)
    {
        return new()
        {
            NativeGraphics = NativeBattleFixtureState.ReadArchived<NativeBattleGraphicsState>(source, "NativeGraphics"),
            NativeCamera = NativeBattleFixtureState.ReadArchived<NativeBattleCameraState>(source, "NativeCamera"),
            NativeFileStaging = NativeBattleFixtureState.ReadArchived<NativeBattleFileStaging>(source, "NativeFileStaging"),
            NativeModelCpuResources = NativeBattleFixtureState.ReadArchived<NativeBattleModelCpuResources>(source, "NativeModelCpuResources")
        };
    }
}

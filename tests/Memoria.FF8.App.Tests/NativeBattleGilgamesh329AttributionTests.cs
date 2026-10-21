using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

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
        BattleSnapshot prior = NativeBattleEffectFamilyRunner.LoadSnapshot(priorPath!);
        BattleSnapshot current = NativeBattleEffectFamilyRunner.LoadSnapshot(currentPath!);
        Assert.Equal((ushort)329, prior.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal((ushort)329, current.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(prior.NativeGraphics.Callback + 1, current.NativeGraphics.Callback);
        List<NativeBattleModelPrefixDiagnostic> matches = [];
        ImmutableHashSet<NativeBattlePrimitiveReference> owned = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current, matches.Add);
        File.WriteAllText(Path.Combine(output!, "prefix-diagnostics.json"), JsonSerializer.Serialize(new
        {
            PriorSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(priorPath!))),
            CurrentSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(currentPath!))),
            prior.Frame,
            PriorCallback = prior.NativeGraphics.Callback,
            CurrentFrame = current.Frame,
            CurrentCallback = current.NativeGraphics.Callback,
            Matches = matches,
            Packets = owned.OrderBy(packet => packet.ByteOffset).ToArray()
        }));
        Assert.Contains(matches, match => match.Start >= 0 && match.BodyPrimitives > 0 && match.MismatchOffset is null);
        Assert.NotEmpty(owned);
        BattleSession selected = BattleSession.Restore(current);
        byte[] pixels = new BattleScenePresentation(assets, current.Definition, true).Render(selected, 320, 224).ToArray();
        NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
        BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
        byte[] plain = new BattleScenePresentation(assets, current.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
        int changed = 0;
        Assert.Equal(pixels.Length, plain.Length);
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            if (!pixels.AsSpan(offset, 3).SequenceEqual(plain.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        NativeBattleRestorationPresentationTests.Publish(output!, "visible", current, pixels);
        NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", without, plain);
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = changed > 0 ? "isolated" : "zero-rgb", Effect = 329, Scope = "Exact saved pair model-prefix attribution; no native callback execution", current.Frame, NativeCallback = current.NativeGraphics.Callback, OwnedPackets = owned.Count, ChangedRgb = changed }));
        Assert.True(changed > 0, "The exactly attributed original effect prefix must contribute visible RGB pixels.");
    }
}

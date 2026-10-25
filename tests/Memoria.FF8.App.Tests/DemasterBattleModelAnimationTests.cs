using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class DemasterBattleModelAnimationTests
{
    [Fact]
    public void OriginalEyeCopyPlansSelectOnlyTheChangedHdPatchAndRestoreTheNextCopy()
    {
        string? statePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_MODEL_STATE");
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_MODEL_TEXTURE_PACKAGES");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(statePath) && File.Exists(statePath) && !string.IsNullOrWhiteSpace(input), "Configure optional MEMORIA_FF8_BATTLE_MODEL_STATE and MEMORIA_FF8_BATTLE_MODEL_TEXTURE_PACKAGES inputs.");
        bool catalogFile = File.Exists(input);
        string[] directories = JsonSerializer.Deserialize<string[]>(catalogFile ? File.ReadAllText(input!) : input!) ?? throw new InvalidDataException("Expected a JSON package directory array.");
        Assert.NotEmpty(directories);
        string relativeBase = catalogFile ? Path.GetDirectoryName(Path.GetFullPath(input!))! : Environment.CurrentDirectory;
        directories = directories.Select(path => Path.GetFullPath(path, relativeBase)).ToArray();
        DirectoryInfo demaster = Directory.GetParent(directories[0]) ?? throw new InvalidDataException("Missing Demaster package parent.");
        Assert.Equal("Demaster", demaster.Name, ignoreCase: true);
        Assert.All(directories, path => Assert.Equal(demaster.FullName, Directory.GetParent(path)!.FullName, ignoreCase: true));
        string? configuredGame = Environment.GetEnvironmentVariable("FF8_GAME_DIR");
        string game = !string.IsNullOrWhiteSpace(configuredGame) && Directory.Exists(configuredGame) ? configuredGame : TestResources.GameDirectory;
        using GameResourceHost host = new(game, watch: false, modsDirectory: demaster.Parent!.FullName);
        Assert.All(directories, directory => Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(directory, StringComparison.OrdinalIgnoreCase)).Status));
        BattleSnapshot snapshot = GameSession.Load(statePath!).Battle ?? throw new InvalidDataException("The standalone fixture must contain an active battle.");
        BattleSession battle = BattleSession.Restore(snapshot);
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(battle.NativeGraphics);
        using BattleAssets assets = new(game, host.Resources);
        int verified = 0;
        List<object> report = [];
        for (int index = 0; index < graphics.ActorIds.Length; index++)
        {
            if (graphics.ActorIds[index] is not string actorId || graphics.Models[index].BodyTextureSlots is not { } binding)
            {
                continue;
            }
            BattleCombatantDefinition actor = battle.GetCombatantDefinition(actorId);
            if (!actor.IsPlayer)
            {
                continue;
            }
            BattleModel model = assets.LoadPlayer(actor.ModelId, actor.Costume);
            ResourceTextureList textures = Assert.IsType<ResourceTextureList>(model.Textures);
            if (!Enumerable.Range(0, textures.Count).Any(texture => textures.GetTexture(texture).Variants?.ContainsKey("eyes-closed") == true))
            {
                continue;
            }
            NativeBattleTextureCopy open = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, graphics.Models[index].TextureAllocationMask, 0, 0));
            NativeBattleTextureCopy closed = Assert.IsType<NativeBattleTextureCopy>(NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, graphics.Models[index].TextureAllocationMask, 0, 1));
            Assert.NotEqual(open, closed);
            NativeBattleEffectTextureState initial = Assert.IsType<NativeBattleEffectTextureState>(battle.NativeEffectMemory?.Textures);
            NativeBattleEffectTextureState opened = Copy(initial, open);
            NativeBattleEffectTextureState shut = Copy(opened, closed);
            IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, binding);
            bool observed = false;
            foreach (NativeBattleTextureReplacement material in replacements)
            {
                PixelImage openImage = Sample(opened, material);
                PixelImage closedImage = Sample(shut, material);
                if (openImage.Width != closedImage.Width || openImage.Height != closedImage.Height || openImage.Pixels.AsSpan().SequenceEqual(closedImage.Pixels))
                {
                    continue;
                }
                Assert.True(openImage.Width > material.Width && closedImage.Width > material.Width, "Both original eye states must retain HD sampling density.");
                int changed = 0;
                int pixelsPerWord = 4 >> ((material.Page >> 7) & 3);
                for (int y = 0; y < openImage.Height; y++)
                {
                    for (int x = 0; x < openImage.Width; x++)
                    {
                        if (openImage[x, y].Equals(closedImage[x, y]))
                        {
                            continue;
                        }
                        changed++;
                        int nativeX = (material.Page & 15) * 64 + (material.U + x * material.Width / openImage.Width) / pixelsPerWord;
                        int nativeY = (material.Page & 16) * 16 + material.V + y * material.Height / openImage.Height;
                        Assert.InRange(nativeX, (int)closed.DestinationX, closed.DestinationX + closed.Width - 1);
                        Assert.InRange(nativeY, (int)closed.DestinationY, closed.DestinationY + closed.Height - 1);
                    }
                }
                Assert.True(changed > 0);
                NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(shut))!;
                Assert.Equal(closedImage.Pixels, Sample(restored, material).Pixels);
                Assert.Equal(openImage.Pixels, Sample(Copy(restored, open), material).Pixels);
                Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(graphics.Models[index].BodyTextureSlots));
                report.Add(new { model = model.Name, open, closed, material.Page, material.Clut, material.U, material.V, changedPixels = changed, openWidth = openImage.Width, openHeight = openImage.Height });
                observed = true;
                break;
            }
            Assert.True(observed, $"Original eye copies for {model.Name} did not select a changed HD patch.");
            verified++;

            PixelImage Sample(NativeBattleEffectTextureState state, NativeBattleTextureReplacement material)
            {
                PixelImage original = NativeBattleEffectTextureReader.DecodeRegion(state, material.Page, material.Clut, material.U, material.V, material.Width, material.Height);
                return NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, material.Page, material.Clut, material.U, material.V, null, replacements);
            }
        }
        Assert.True(verified > 0, "The configured packages and standalone battle must supply a participant with HD eye planes.");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "battle-model-eye-copies.json"), JsonSerializer.Serialize(new { verified, producer = "Original resource selector 0 variants through 50C780/505EB0/505D20", targets = report }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static NativeBattleEffectTextureState Copy(NativeBattleEffectTextureState state, NativeBattleTextureCopy copy)
    {
        NativeBattleEffectTextureDrain drain = state.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20();
        Assert.Contains(drain.Uploads, upload => upload.Copied && upload.X == copy.DestinationX && upload.Y == copy.DestinationY && upload.Width == copy.Width && upload.Height == copy.Height);
        return drain.State;
    }
}


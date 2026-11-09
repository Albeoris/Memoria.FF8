using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class InnerFieldTests
{
    [Fact]
    public void OptionalInstalledOpeningFieldDecodesInnerSectionsAndInitialKernel()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for read-only field resource checks.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        Assert.NotEmpty(field.Scripts.Entities);
        Assert.NotEmpty(field.Scripts.Texts);
        Assert.NotEmpty(field.Models.Models);
        Assert.Single(field.Cameras);
        Assert.NotEmpty(field.Walkmesh);
        Assert.Equal(12, field.Triggers.Gateways.Count);
        Assert.NotEmpty(field.Palettes.Palettes);
        Assert.Equal(4, field.Background.Layers.Count);
        Assert.NotEmpty(field.Background.Textures);
        Console.WriteLine($"md1stin entities={field.Scripts.Entities.Count}, texts={field.Scripts.Texts.Count}, models={field.Models.Models.Count}, triangles={field.Walkmesh.Count}, palettes={field.Palettes.Palettes.Count}, layers={string.Join(',', field.Background.Layers.Select(layer => layer.Count))}, textures={field.Background.Textures.Count}");
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Assert.True(kernel.Count >= 4);
        Assert.True(kernel[3].Data.Length > 0xB7C - 0x54);
        Console.WriteLine($"Kernel sections={kernel.Count}, initial payload={kernel[3].Data.Length}");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        PcFieldModelDescriptor cloud = Assert.Single(field.Models.Models, model => model.Hrc.StartsWith("AAAA", StringComparison.OrdinalIgnoreCase));
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, cloud);
        Assert.Equal(21, model.Hierarchy.Bones.Count);
        Assert.NotEmpty(model.Geometry);
        Assert.Equal(4, model.Animations.Count);
        Console.WriteLine($"Cloud geometry={model.Geometry.Count}, vertices={model.Geometry.Sum(piece => piece.Polygon.Vertices.Count)}, triangles={model.Geometry.Sum(piece => piece.Polygon.Polygons.Count)}, textures={model.Geometry.Sum(piece => piece.Textures.Count)}");
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Assert.NotEmpty(formations);
        SceneFormation opening = Assert.Single(formations, formation => formation.FormationId == 300);
        Console.WriteLine($"Formations={formations.Count}; formation300 enemies={string.Join(',', opening.Enemies.Where(enemy => enemy.EnemyId != -1).Select(enemy => enemy.EnemyId))}; rewards={string.Join(';', opening.EnemyRewards.Select(reward => $"{reward.EnemyId}:{reward.Experience}/{reward.Gil}/{reward.AbilityPoints}"))}");
        using LgpArchive menu = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MenuArchive).Path);
        Assert.Equal(50, menu.Entries.Count);
        using LgpArchive moviecam = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MovieCameraArchive).Path);
        Console.WriteLine($"Opening movie camera names: {string.Join(',', moviecam.Entries.Where(entry => entry.Name.Contains("open", StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Name))}");
        IReadOnlyList<PcMovieCamera> openingCameras = PcMovieCameras.Read(moviecam.ReadEntry(Assert.Single(moviecam.Find("opening.cam"))));
        Console.WriteLine($"Opening movie cameras={openingCameras.Count}");
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        IReadOnlyList<string> maps = PcFieldMapList.Read(fields.ReadEntry(Assert.Single(fields.Find("maplist"))));
        Assert.Equal("md1stin", maps[116]);
        Console.WriteLine($"Map list={maps.Count};116={maps[116]}");
        Ff7MenuResources menuResources = Ff7MenuResources.Load(installation);
        Assert.True(menuResources.GetPortrait(0).Width > 0);
        Assert.NotNull(menuResources.GetRequiredTexture("usfont_a_h.tex"));
        Console.WriteLine($"Cloud portrait={menuResources.GetPortrait(0).Width}x{menuResources.GetPortrait(0).Height};font={menuResources.GetRequiredTexture("usfont_a_h.tex").Width}x{menuResources.GetRequiredTexture("usfont_a_h.tex").Height}");
        PcFieldFile variantField = PcFieldFile.ReadCompressed(fields.ReadEntry(Assert.Single(fields.Find("ancnt3"))));
        PcFieldModelDescriptor variant = PcFieldModels.Read(variantField.GetSection(PcFieldSectionKind.Models).Span).Models[3];
        byte[] variantBytes = characters.ReadEntry(Assert.Single(characters.Find(Path.GetFileNameWithoutExtension(variant.Hrc) + ".hrc")));
        HrcHierarchy variantHierarchy = HrcHierarchy.Read(variantBytes);
        Assert.Equal(0, variantHierarchy.DeclaredBoneCount);
        Assert.True(variantHierarchy.IsRootOnly);
        Assert.Single(variantHierarchy.Bones);
        foreach (PcFieldModelDescriptor descriptor in field.Models.Models)
        {
            Ff7FieldModelResources decodedModel = Ff7FieldModelResources.Load(characters, descriptor);
            Assert.NotEmpty(decodedModel.Geometry);
        }
        KernelInitialData initial = Ff7InitialGameData.Load(installation);
        Assert.Equal(9, initial.Characters.Count);
        Assert.Equal(320, initial.Inventory.Count);
        Assert.Equal(200, initial.Materia.Count);
        Console.WriteLine($"Initial party={string.Join(',', initial.Party)}, gil={initial.Gil}, Cloud hp={initial.Characters[0].CurrentHp}/{initial.Characters[0].RecordedMaximumHp}, slopes={field.Walkmesh.Count(triangle => triangle.A.Z != triangle.B.Z || triangle.A.Z != triangle.C.Z)}");
        foreach (ReadOnlyMemory<byte> text in field.Scripts.Texts)
        {
            Assert.NotNull(FieldText.Read(text.Span));
        }
        IReadOnlyList<KernelSection> window = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Window).Path));
        Ff7FontResources font = Ff7FontResources.Load(installation);
        Assert.Equal(9, font.GetGlyph(0x21).Advance);
        Assert.Equal(12, font.GetGlyph(0x21).Width);
        Assert.Equal(13, font.GetGlyph(0x21, fixedSpacing: true).Advance);
        Console.WriteLine($"Window types/lengths={string.Join(';', window.Select(section => $"{section.Type}/{section.Data.Length}"))};font={font.Atlas.Width}x{font.Atlas.Height}, palettes={font.Atlas.Palettes.Count}, glyphAadvance={font.GetGlyph(0x21).Advance}");
        Ff7PcExecutableData executableData = Ff7PcExecutableData.Load(installation);
        Assert.Equal(512, executableData.NativeSinCos.Count);
        Assert.Equal(129, executableData.NativeArcAngles.Count);
        Assert.Equal(116, executableData.InitialField);
    }
}

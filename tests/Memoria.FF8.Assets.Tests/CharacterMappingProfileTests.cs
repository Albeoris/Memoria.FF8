using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
namespace Memoria.FF8.Assets.Tests;
public sealed class CharacterMappingProfileTests
{
    [Fact]
    public void InstalledCatalogEnumeratesEveryCandidateAndMissingNativeAction()
    {
        string? reportPath = Environment.GetEnvironmentVariable("MEMORIA_MAPPING_COVERAGE");
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(reportPath) || string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Optional full mapping coverage output was not selected.");
        using GameAssets assets = new(ff8!);
        CrossoverCharacterCatalog catalog = new(Ff7Installation.Discover(ff7!), assets);
        List<object> reports = [];
        int? selectedStory = int.TryParse(Environment.GetEnvironmentVariable("MEMORIA_MAPPING_COVERAGE_STORY"), out int requestedStory) ? requestedStory : null;
        for (int story = selectedStory ?? 0; story < (selectedStory is null ? 9 : selectedStory.Value + 1); story++)
        {
            int? selectedAppearance = int.TryParse(Environment.GetEnvironmentVariable("MEMORIA_MAPPING_COVERAGE_APPEARANCE"), out int requestedAppearance) ? requestedAppearance : null;
            for (int appearance = selectedAppearance ?? 0; appearance < (selectedAppearance is null ? 11 : selectedAppearance.Value + 1); appearance++)
            {
                CharacterCompatibility report = catalog.GetCompatibility(story, appearance);
                Assert.Equal(report.TotalClips - report.SupportedClips, report.TotalClips == 0 ? 0 : report.Missing.Count);
                reports.Add(new { Story = story, Appearance = appearance, Report = report, report.Percentage });
                File.WriteAllText(reportPath!, System.Text.Json.JsonSerializer.Serialize(reports, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void InstalledNativeTargetRetainsAnatomyAndFiniteOpeningAction(int target)
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Selected installed resources are required.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        var descriptor = Ff7FieldResources.Load(installation, "md1stin").Models.Models[0];
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        var source = Ff7FieldModelResources.Load(archive, descriptor);
        Ff7ModelAppearance profile = Ff7ModelAppearance.CreateFieldCharacter(assets, target, descriptor, source);
        Ff8FieldAnatomy anatomy = Ff8FieldAnatomy.Profiles.Single(profile => profile.Character == target);
        string[] address = anatomy.Source.Split('/');
        var entry = assets.GetFieldModelSource(address[1], int.Parse(address[2], System.Globalization.CultureInfo.InvariantCulture));
        var targetModel = assets.LoadFieldModelSource(entry);
        byte[] scriptBytes = assets.ReadResource(new Memoria.FF8.Resources.ResourceKey($"field/{address[1]}/{address[1]}.jsm"));
        var inventory = Memoria.FF8.Assets.Localization.FieldCompatibilityAudit.Inspect(new(address[1], "en", address[1], scriptBytes, []));
        int nativeFieldIdentity = target switch { 7 => 10, 8 => 7, 9 => 8, 10 => 9, _ => target };
        Assert.Contains(inventory.Instructions.GroupBy(operation => (operation.Position.ObjectIndex, operation.Position.ScriptId)), script =>
        {
            var operations = script.ToArray();
            bool model = operations.Any(operation => operation.Opcode == "SETMODEL" && operation.Parameter == entry.ModelIndex);
            return model && operations.Select((operation, index) => (operation, index)).Any(item => item.index > 0 && item.operation.Opcode == "SETPC" && operations[item.index - 1].Opcode == "PSHN_L" && operations[item.index - 1].Parameter == nativeFieldIdentity);
        });
        var calibration = Ff7ModelAppearance.CalibrateNeutralGeometry(targetModel, descriptor, source, [anatomy.LeftLeg[^1], anatomy.RightLeg[^1]], anatomy.LeftArm[^1], anatomy.RightArm[^1]);
        int leftHand = calibration.FirstHandIsLeft ? anatomy.LeftArm[^1] : anatomy.RightArm[^1];
        int rightHand = calibration.FirstHandIsLeft ? anatomy.RightArm[^1] : anatomy.LeftArm[^1];
        var neutral = profile.PoseNativeClip(source, source.Animations["acfe.a"], 0);
        var moved = profile.PoseNativeClip(source, Ff7ModelAppearanceTests.ControlledAnimation(source, "l_uparm"), 0);
        Assert.Contains(targetModel.Vertices.Select((vertex, index) => (vertex, index)), item => item.vertex.Bone == leftHand && System.Numerics.Vector3.Distance(neutral[item.index], moved[item.index]) > 1);
        Assert.All(targetModel.Vertices.Select((vertex, index) => (vertex, index)).Where(item => item.vertex.Bone == rightHand), item => Assert.True(System.Numerics.Vector3.Distance(neutral[item.index], moved[item.index]) < 0.01f));
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 3, 0) { PartyCharacter = 0 };
        Assert.True(profile.SupportsPartVisibility);
        Assert.True(profile.TryCreate(actor with { ModelEffects = new(_ => true, null, false) }, descriptor, source, out var allVisible));
        Assert.NotEmpty(allVisible);
        Assert.True(profile.TryCreate(actor with { ModelEffects = new(_ => false, null, false) }, descriptor, source, out var allHidden));
        Assert.Empty(allHidden);
        Assert.Throws<NotSupportedException>(() => profile.TryCreate(actor with { ModelEffects = new(_ => true, (_, _, color) => color, false) }, descriptor, source, out _));
        int headBone = source.Hierarchy.Bones.Single(bone => bone.Name == "head").Index;
        Ff7ActorVisual headMask = actor with { ModelEffects = new(part => source.Geometry[part].BoneIndex != headBone, null, false) };
        if (target is 0 or 2 or 3 or 4 or 5 or 9)
        {
            Assert.True(profile.TryCreate(headMask, descriptor, source, out var hiddenHead));
            Assert.InRange(hiddenHead.Count, 1, allVisible.Count - 1);
        }
        else
        {
            NotSupportedException unsupported = Assert.Throws<NotSupportedException>(() => profile.TryCreate(headMask, descriptor, source, out _));
            Assert.Contains("part visibility", unsupported.Message);
        }
        foreach (int frame in new[] { 0, 20, 40, 79 })
        {
            Assert.True(profile.TryCreate(actor with { AnimationFrame16 = frame * 16 }, descriptor, source, out var triangles));
            Assert.NotEmpty(triangles);
            Assert.All(triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), vertex => Assert.True(float.IsFinite(vertex.Position.X) && float.IsFinite(vertex.Position.Y) && float.IsFinite(vertex.Position.Z)));
        }
    }

    [Theory]
    [InlineData(100, "Cyan")]
    [InlineData(99.9, "Green")]
    [InlineData(75.1, "Green")]
    [InlineData(75, "Yellow")]
    [InlineData(50, "Yellow")]
    [InlineData(49.9, "Orange")]
    [InlineData(25, "Orange")]
    [InlineData(24.9, "Red")]
    [InlineData(0, "Red")]
    public void CoverageColorUsesExactSharedBoundaries(double percentage, string color)
    {
        Assert.Equal(color, CharacterCompatibility.ColorName(percentage));
    }
}

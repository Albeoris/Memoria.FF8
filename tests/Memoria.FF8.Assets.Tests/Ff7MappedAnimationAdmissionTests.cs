using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using FieldCharacterCatalog = Memoria.FF8.Assets.FieldCharacterCatalog;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7MappedAnimationAdmissionTests
{
    [Fact]
    public void MissingSemanticJointsAreDistinctFromInvalidNativeResources()
    {
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON authored\n:BONES 1\nhip\nroot\n1\n0\n"));
        PcFieldAnimation clip = PcFieldAnimation.Read(AnimationBytes());
        Ff7FieldModelResources original = new("authored.hrc", hierarchy, [], new Dictionary<string, PcFieldAnimation> { ["authored.a"] = clip });
        Assert.Single(Ff7FieldPoseSampler.Sample(original, clip, new(0)).Bones);
        Assert.Throws<Ff7AppearanceMappingException>(() => Ff7FieldAnatomy.Resolve(hierarchy));
        Assert.Throws<InvalidDataException>(() => HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON broken\n:BONES 1\nhip\nmissing-parent\n1\n0\n")));
        byte[] unsupportedOrder = AnimationBytes();
        BinaryPrimitives.WriteUInt32LittleEndian(unsupportedOrder.AsSpan(12), 0x010002);
        Assert.Throws<NotSupportedException>(() => Ff7FieldPoseSampler.Sample(original, PcFieldAnimation.Read(unsupportedOrder), new(0)));
        byte[] corrupt = AnimationBytes();
        BinaryPrimitives.WriteSingleLittleEndian(corrupt.AsSpan(36), float.NaN);
        Assert.Throws<InvalidDataException>(() => PcFieldAnimation.Read(corrupt));
    }

    [Fact]
    public void SelectedDonorHoldsOnlyAnUnmappedMotionAndKeepsItsActorAndWorldOwnersSeparate()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Selected original installations are required.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        PcFieldAnimation clip = original.Animations["aaff.a"];
        PcFieldModelDescriptor descriptor = field.Models.Models[0] with { Animations = ["mapped.a", "unmapped.a", "bad.a"] };
        byte[] wrongOrder = archive.ReadEntry(Assert.Single(archive.Find("aaff.a")));
        BinaryPrimitives.WriteUInt32LittleEndian(wrongOrder.AsSpan(12), 0x010002);
        Ff7FieldModelResources resources = original with { Animations = new Dictionary<string, PcFieldAnimation> { ["mapped.a"] = clip, ["unmapped.a"] = clip, ["bad.a"] = PcFieldAnimation.Read(wrongOrder) } };
        ModelCatalogEntry entry = assets.GetFieldModelSource("bghall_1", 0);
        FieldModel target = assets.LoadFieldModelSource(entry);
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create([entry], _ => target, TestContext.Current.CancellationToken);
        Dictionary<string, string> bindings = new() { ["mapped.a"] = catalog.GetAnimation(entry.Id, 1).Id };
        Ff7ModelAppearance appearance = new(assets, catalog, entry.Id, Path.GetFileName(descriptor.Hrc), Matrix4x4.Identity, bindings, holdUnmappedAnimations: true) { ExpectedSourceRig = Ff7SemanticRetargeter.HierarchyIdentity(resources.Hierarchy) };
        Ff7ActorVisual actor = new(7, 0, 0, 0, 0, 0, 0, Math.Min(7, clip.Frames.Count - 1) * 16) { PartyCharacter = 0 };
        Assert.True(appearance.TrySampleVertices(actor, descriptor, resources, out var admitted));
        Assert.NotEmpty(admitted);
        Ff7ActorVisual unsupported = actor with { AnimationIndex = 1, AnimationFrame16 = Math.Min(15, clip.Frames.Count - 1) * 16 };
        Assert.True(appearance.TrySampleVertices(unsupported, descriptor, resources, out var held));
        Assert.Equal(admitted, held);
        Assert.True(appearance.TryCreate(unsupported, descriptor, resources, out var donor));
        Assert.NotEmpty(donor);
        Assert.True(appearance.TrySampleVertices(unsupported with { X4096 = 4096 }, descriptor, resources, out var moved));
        Assert.All(Enumerable.Range(0, moved.Count), index => Assert.Equal(held[index] + Vector3.UnitX, moved[index]));
        Assert.True(appearance.TrySampleVertices(unsupported with { EntityId = 8 }, descriptor, resources, out var otherActor));
        Assert.Equal(target.Pose(0, 0), otherActor);
        Assert.True(appearance.TrySampleVertices(unsupported with { IsWorldModel = true }, descriptor, resources, out var world));
        Assert.Equal(target.Pose(0, 0), world);
        Assert.True(appearance.TrySampleVertices(unsupported, descriptor, resources with { HrcName = resources.HrcName }, out var nextAllocation));
        Assert.Equal(target.Pose(0, 0), nextAllocation);
        Ff7FieldModelResources otherSource = resources with { Hierarchy = AnonymousHierarchy(original.Hierarchy) };
        Assert.Throws<InvalidDataException>(() => appearance.TrySampleVertices(unsupported, descriptor, otherSource, out _));
        Assert.Throws<NotSupportedException>(() => appearance.TrySampleVertices(unsupported with { AnimationIndex = 2 }, descriptor, resources, out _));
        Assert.Throws<KeyNotFoundException>(() => appearance.TrySampleVertices(unsupported, descriptor, resources with { Animations = new Dictionary<string, PcFieldAnimation>() }, out _));
        Assert.Throws<InvalidDataException>(() => appearance.TrySampleVertices(unsupported, descriptor, resources with { HrcName = "unrelated.hrc" }, out _));
        Assert.Throws<InvalidDataException>(() => appearance.TrySampleVertices(unsupported with { AnimationFrame16 = clip.Frames.Count * 16 }, descriptor, resources, out _));
        Assert.Throws<NotSupportedException>(() => appearance.TryCreate(unsupported with { ModelEffects = new(_ => true, (_, _, color) => color, false) }, descriptor, resources, out _));
        Ff7ModelAppearance strict = new(assets, catalog, entry.Id, Path.GetFileName(descriptor.Hrc), Matrix4x4.Identity, bindings);
        Assert.Throws<NotSupportedException>(() => strict.TrySampleVertices(unsupported, descriptor, resources, out _));
        Assert.Throws<NotSupportedException>(() => Ff7FieldPoseSampler.Sample(resources, resources.Animations["bad.a"], unsupported.PosePhase));
    }

    [Fact]
    public void SharedFactoryReportsAnatomicalGapAndKeepsSelectedDonorInNeutralPose()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Selected original installations are required.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        PcFieldModelDescriptor descriptor = field.Models.Models[0];
        Ff7ModelAppearance compatible = Ff7ModelAppearance.CreateFieldCharacter(assets, 0, descriptor, original);
        Assert.Null(compatible.MappingGap);
        Ff7ActorVisual actor = new(2, 0, 0, 0, 0, 0, 1, 0) { PartyCharacter = 0 };
        Assert.True(compatible.TrySampleVertices(actor, descriptor, original, out var supported));
        Assert.NotEmpty(supported);
        Ff7FieldModelResources unbindable = original with { Hierarchy = AnonymousHierarchy(original.Hierarchy) };
        FieldModel donorModel = assets.LoadFieldModelSource(assets.GetFieldModelSource("bghall_1", 0));
        ModelBone[] corruptDonorBones = donorModel.Bones.ToArray();
        corruptDonorBones[1] = corruptDonorBones[1] with { Parent = -1 };
        Assert.Throws<NotSupportedException>(() => Ff8FieldAnatomy.Profiles[0].Bindings(donorModel with { Bones = corruptDonorBones }, unbindable.Hierarchy));
        Ff7ModelAppearance held = Ff7ModelAppearance.CreateFieldCharacter(assets, 0, descriptor, unbindable);
        Assert.NotNull(held.MappingGap);
        Assert.Throws<Ff7AppearanceMappingException>(() => held.ValidateNativeClip(unbindable, unbindable.Animations["aaff.a"], "aaff.a"));
        Assert.True(held.TrySampleVertices(actor, descriptor, unbindable, out var neutral));
        Assert.True(held.TrySampleVertices(actor with { AnimationFrame16 = 16 }, descriptor, unbindable, out var next));
        Assert.Equal(neutral, next);
        Assert.True(held.TryCreate(actor, descriptor, unbindable, out var donor));
        Assert.NotEmpty(donor);
        Assert.Throws<InvalidDataException>(() => held.TrySampleVertices(actor, descriptor, original, out _));
    }

    private static HrcHierarchy AnonymousHierarchy(HrcHierarchy original)
    {
        Dictionary<string, string> names = original.Bones.ToDictionary(bone => bone.Name, bone => "source_" + bone.Index, StringComparer.OrdinalIgnoreCase);
        StringBuilder text = new($":HEADER_BLOCK 2\n:SKELETON authored\n:BONES {original.DeclaredBoneCount}\n");
        foreach (HrcBone bone in original.Bones)
        {
            text.AppendLine(names[bone.Name]);
            text.AppendLine(bone.ParentName.Equals("root", StringComparison.OrdinalIgnoreCase) ? "root" : names[bone.ParentName]);
            text.AppendLine(bone.Length.ToString("R", CultureInfo.InvariantCulture));
            text.AppendLine(bone.ResourceDescriptors.Count + " " + string.Join(' ', bone.ResourceDescriptors));
        }
        return HrcHierarchy.Read(Encoding.ASCII.GetBytes(text.ToString()));
    }

    private static byte[] AnimationBytes()
    {
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x020001);
        return bytes;
    }
}

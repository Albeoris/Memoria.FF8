using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using FieldCharacterCatalog = Memoria.FF8.Assets.FieldCharacterCatalog;

namespace Memoria.FF8;

internal sealed partial class Ff7ModelAppearance
{
    internal const string GeneralFieldProfileVersion = "ff7-native-humanoid-to-ff8-field-v1";

    internal System.Numerics.Vector3[] PoseNativeClip(Ff7FieldModelResources original, PcFieldAnimation clip, int frame16) => (retargeter ?? throw new NotSupportedException("No semantic retarget profile is bound.")).Pose(original, clip, frame16);

    internal void ValidateNativeClip(Ff7FieldModelResources original, PcFieldAnimation clip, string? nativeName = null)
    {
        if (retargeter is null)
        {
            throw new NotSupportedException("Native clip coverage requires an explicit semantic retarget profile.");
        }
        retargeter.ValidateSource(original);
        if (nativeName is not null && clips.TryGetValue(nativeName, out string? targetIdentity))
        {
            ModelAnimation animation = assets.LoadFieldCharacterAnimation(catalog, sourceId, targetIdentity);
            FieldModel nativeTarget = geometry with { Animations = [animation] };
            bool loop = loopingClips.Contains(nativeName);
            for (int frame = 0; frame < clip.Frames.Count; frame++)
            {
                float phase = MapAnimationPhase(frame, clip.Frames.Count, animation.Frames.Count, loop);
                _ = nativeTarget.PoseInterpolated(0, phase, phase, 0, loop: loop);
            }
            return;
        }
        for (int frame = 0; frame < clip.Frames.Count; frame++)
        {
            _ = retargeter.Pose(original, clip, frame * 16);
        }
    }

    internal static Ff7ModelAppearance CreateFieldCharacter(GameAssets assets, int character, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, bool worldModel = false)
    {
        Ff8FieldAnatomy anatomy = Ff8FieldAnatomy.Profiles.Single(profile => profile.Character == character);
        string[] address = anatomy.Source.Split('/');
        ModelCatalogEntry entry = assets.GetFieldModelSource(address[1], int.Parse(address[2], System.Globalization.CultureInfo.InvariantCulture));
        if (entry.ModelLibraryResource != $"field/main_chr/{anatomy.Library}.mch")
        {
            throw new NotSupportedException("Selected target source does not contain its named native character geometry.");
        }
        FieldModel target = assets.LoadFieldModelSource(entry);
        Ff7FieldAnatomy sourceAnatomy = Ff7FieldAnatomy.Resolve(original.Hierarchy);
        IReadOnlyList<Ff7SemanticJointBinding> bindings = anatomy.Bindings(target, original.Hierarchy);
        bool hasFootMeshes = sourceAnatomy.Feet.All(name => original.Hierarchy.Bones.Where(bone => bone.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Any(bone => original.Geometry.Any(part => part.BoneIndex == bone.Index && part.Polygon.Vertices.Count > 0)));
        Ff7AppearanceCalibration calibration = CalibrateNeutralGeometry(target, descriptor, original, [anatomy.LeftLeg[^1], anatomy.RightLeg[^1]], anatomy.LeftArm[^1], anatomy.RightArm[^1], sourceAnatomy.Feet, sourceAnatomy.Hands, alignHandSides: !hasFootMeshes);
        if (!calibration.FirstHandIsLeft)
        {
            anatomy = anatomy with { LeftArm = anatomy.RightArm, RightArm = anatomy.LeftArm, LeftLeg = anatomy.RightLeg, RightLeg = anatomy.LeftLeg };
            bindings = anatomy.Bindings(target, original.Hierarchy);
        }
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create([entry], _ => target);
        Ff7FootContactBinding feet = new(sourceAnatomy.FootGeometryOwners(original), [anatomy.LeftLeg[^1], anatomy.RightLeg[^1]]);
        Ff7SemanticRetargeter retarget = new(target, descriptor, original, calibration.Transform, bindings, feet);
        Dictionary<string, string> native = new(StringComparer.OrdinalIgnoreCase);
        List<string> loops = ["aaff.a", "aaga.a"];
        if (character == 0 && target.Animations.Count >= 3)
        {
            native["acfe.a"] = catalog.GetAnimation(entry.Id, 0).Id;
            native["aaff.a"] = catalog.GetAnimation(entry.Id, 1).Id;
            native["aaga.a"] = catalog.GetAnimation(entry.Id, 2).Id;
            if (worldModel)
            {
                native[Path.GetFileNameWithoutExtension(descriptor.Animations[0]) + ".a"] = catalog.GetAnimation(entry.Id, 0).Id;
                string walk = Path.GetFileNameWithoutExtension(descriptor.Animations[1]) + ".a";
                native[walk] = catalog.GetAnimation(entry.Id, 1).Id;
                loops.Add(walk);
            }
        }
        return new(assets, catalog, entry.Id, Path.GetFileName(descriptor.Hrc), calibration.Transform, native, retarget, loops)
        {
            calibrationDescriptorScale = float.Parse(descriptor.Scale, System.Globalization.CultureInfo.InvariantCulture),
            ProfileId = GeneralFieldProfileVersion + "/" + character + "/" + catalog.GetSource(entry.Id).RigId
        };
    }
}

using System.Security.Cryptography;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record FieldCharacterClipBinding(string SourceId, int NativeSlot, string DescriptorName, string ArchiveMember, int ArchiveIndex, string AnimationId, int NativeFrameCount, uint RotationOrder);
public sealed record FieldCharacterSource(string Id, string FieldMember, int FieldArchiveIndex, int ModelIndex, PcFieldModelDescriptor Descriptor, string HrcMember, string ModelId, string GeometryId, string MaterialId, string RigId, IReadOnlyList<FieldCharacterClipBinding> Clips);
public sealed record FieldCharacterModel(string Id, string GeometryId, string MaterialId, string RigId, IReadOnlyList<string> Sources);
public sealed record FieldCharacterAnimation(string Id, string RigId, int NativeFrameCount, uint RotationOrder, IReadOnlyList<FieldCharacterClipBinding> Bindings);
public sealed record FieldCharacterAnimationBank(string TargetSourceId, string RigId, IReadOnlyList<FieldCharacterAnimation> Animations, IReadOnlyList<FieldCharacterClipBinding> NativeBindings)
{
    public int ResolveSlot(string sourceId, int nativeSlot)
    {
        string identity = NativeBindings.Single(binding => binding.SourceId == sourceId && binding.NativeSlot == nativeSlot).AnimationId;
        return Animations.Select((animation, index) => (animation.Id, Index: index)).Single(item => item.Id == identity).Index;
    }
}

public sealed record FieldCharacterCatalogDiagnostic(string SourceId, string Message);

/// <summary>Original field descriptor bindings, exact resource-content groups and conservative direct-reuse compatibility.</summary>
public sealed class FieldCharacterCatalog
{
    private readonly IReadOnlyDictionary<string, FieldCharacterSource> bySource;
    private readonly IReadOnlyDictionary<string, FieldCharacterAnimation> byAnimation;

    private FieldCharacterCatalog(FieldCharacterSource[] sources, FieldCharacterCatalogDiagnostic[] diagnostics)
    {
        Sources = Array.AsReadOnly(sources);
        Diagnostics = Array.AsReadOnly(diagnostics);
        Models = Array.AsReadOnly(sources.GroupBy(source => source.ModelId).Select(group => new FieldCharacterModel(group.Key, group.First().GeometryId, group.First().MaterialId, group.First().RigId, Array.AsReadOnly(group.Select(source => source.Id).ToArray()))).ToArray());
        Animations = Array.AsReadOnly(sources.SelectMany(source => source.Clips.Select(clip => (source.RigId, Clip: clip))).GroupBy(item => item.Clip.AnimationId).Select(group => new FieldCharacterAnimation(group.Key, group.First().RigId, group.First().Clip.NativeFrameCount, group.First().Clip.RotationOrder, Array.AsReadOnly(group.Select(item => item.Clip).ToArray()))).ToArray());
        bySource = sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
        byAnimation = Animations.ToDictionary(animation => animation.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<FieldCharacterSource> Sources { get; }
    public IReadOnlyList<FieldCharacterModel> Models { get; }
    public IReadOnlyList<FieldCharacterAnimation> Animations { get; }
    public IReadOnlyList<FieldCharacterCatalogDiagnostic> Diagnostics { get; }
    public FieldCharacterSource GetSource(string sourceId) => bySource[sourceId];
    public FieldCharacterAnimation GetAnimation(string sourceId, int nativeSlot) => byAnimation[GetSource(sourceId).Clips.Single(clip => clip.NativeSlot == nativeSlot).AnimationId];

    public IReadOnlyList<FieldCharacterAnimation> CompatibleAnimations(string sourceId)
    {
        FieldCharacterSource source = GetSource(sourceId);
        return Animations.Where(animation => animation.RigId == source.RigId && animation.Bindings.Any(binding => GetSource(binding.SourceId).GeometryId == source.GeometryId)).ToArray();
    }

    /// <summary>Groups exact compatible clips while retaining each original field/model/slot address for mod bindings.</summary>
    public FieldCharacterAnimationBank CreateAnimationBank(string sourceId)
    {
        FieldCharacterSource source = GetSource(sourceId);
        IReadOnlyList<FieldCharacterAnimation> compatible = CompatibleAnimations(sourceId);
        string[] preferred = source.Clips.Select(clip => clip.AnimationId).Distinct().ToArray();
        FieldCharacterAnimation[] ordered = preferred.Select(id => byAnimation[id]).Concat(compatible.Where(animation => !preferred.Contains(animation.Id)).OrderBy(animation => animation.Id, StringComparer.Ordinal)).ToArray();
        FieldCharacterClipBinding[] bindings = compatible.SelectMany(animation => animation.Bindings).Where(binding => GetSource(binding.SourceId).GeometryId == source.GeometryId).ToArray();
        return new(sourceId, source.RigId, Array.AsReadOnly(ordered), Array.AsReadOnly(bindings));
    }

    public static FieldCharacterCatalog Discover(Ff7Installation installation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        return Create(fields, characters, cancellationToken);
    }

    /// <summary>Archive instances remain caller-owned. The result retains metadata, never original geometry, textures or poses.</summary>
    public static FieldCharacterCatalog Create(LgpArchive fields, LgpArchive characters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(characters);
        List<FieldCharacterSource> sources = [];
        List<FieldCharacterCatalogDiagnostic> diagnostics = [];
        Dictionary<int, (string Rig, string Geometry, string Materials, string Model, int Bones)> models = [];
        Dictionary<int, (string Hash, int Frames, int Bones, uint Order)> animations = [];
        LgpEntry Resolve(string reference, string extension)
        {
            string path = reference.Replace('\\', '/');
            int dot = path.LastIndexOf('.');
            if (dot > path.LastIndexOf('/'))
            {
                path = path[..dot];
            }
            path += extension;
            IReadOnlyList<LgpEntry> matches = characters.Find(path);
            return matches.Count == 1 ? matches[0] : throw new InvalidDataException($"Character resource '{path}' has {matches.Count} matches; an exact unambiguous binding is required.");
        }
        foreach (LgpEntry fieldEntry in fields.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PcFieldModels descriptorSection;
            try
            {
                PcFieldFile field = PcFieldFile.ReadCompressed(fields.ReadEntry(fieldEntry));
                descriptorSection = PcFieldModels.Read(field.GetSection(PcFieldSectionKind.Models).Span);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostics.Add(new("field/" + fieldEntry.Index, fieldEntry.FullPath + ": " + exception.Message));
                continue;
            }
            for (int modelIndex = 0; modelIndex < descriptorSection.Models.Count; modelIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PcFieldModelDescriptor descriptor = descriptorSection.Models[modelIndex];
                string sourceId = $"ff7/field/{fieldEntry.Index}/{fieldEntry.FullPath}/models/{modelIndex}";
                try
                {
                    LgpEntry hrcEntry = Resolve(descriptor.Hrc, ".hrc");
                    if (!models.TryGetValue(hrcEntry.Index, out var model))
                    {
                        HrcHierarchy hrc = HrcHierarchy.Read(characters.ReadEntry(hrcEntry));
                        string rig = Fingerprint("rig", writer =>
                        {
                            writer.Write(hrc.DeclaredBoneCount);
                            writer.Write(hrc.Bones.Count);
                            foreach (HrcBone bone in hrc.Bones)
                            {
                                writer.Write(bone.Index);
                                writer.Write(bone.Name.ToLowerInvariant());
                                writer.Write(bone.ParentName.ToLowerInvariant());
                                writer.Write(bone.Length);
                            }
                        });
                        List<(int Bone, string Polygon, string Hash)> polygons = [];
                        List<(int Bone, string Descriptor, string Hash, string[] TextureHashes)> materials = [];
                        foreach (HrcBone bone in hrc.Bones)
                        {
                            foreach (string rsdReference in bone.ResourceDescriptors)
                            {
                                LgpEntry rsdEntry = Resolve(rsdReference, ".rsd");
                                byte[] rsdBytes = characters.ReadEntry(rsdEntry);
                                RsdDescriptor rsd = RsdDescriptor.Read(rsdBytes);
                                LgpEntry polygon = Resolve(rsd.Polygon, ".p");
                                polygons.Add((bone.Index, polygon.FullPath.ToLowerInvariant(), Hash(characters.ReadEntry(polygon))));
                                string[] textures = rsd.Textures.Select(texture =>
                                {
                                    LgpEntry member = Resolve(texture, ".tex");
                                    return member.FullPath.ToLowerInvariant() + ":" + Hash(characters.ReadEntry(member));
                                }).ToArray();
                                materials.Add((bone.Index, rsdEntry.FullPath.ToLowerInvariant(), Hash(rsdBytes), textures));
                            }
                        }
                        string geometry = Fingerprint("geometry", writer =>
                        {
                            writer.Write(rig);
                            writer.Write(polygons.Count);
                            foreach (var polygon in polygons)
                            {
                                writer.Write(polygon.Bone);
                                writer.Write(polygon.Hash);
                            }
                        });
                        string material = Fingerprint("materials", writer =>
                        {
                            writer.Write(materials.Count);
                            foreach (var item in materials)
                            {
                                writer.Write(item.Bone);
                                writer.Write(item.Hash);
                                writer.Write(item.TextureHashes.Length);
                                foreach (string texture in item.TextureHashes)
                                {
                                    writer.Write(texture);
                                }
                            }
                        });
                        string modelId = Fingerprint("model", writer =>
                        {
                            writer.Write(geometry);
                            writer.Write(material);
                        });
                        model = (rig, geometry, material, modelId, hrc.DeclaredBoneCount);
                        models.Add(hrcEntry.Index, model);
                    }
                    List<FieldCharacterClipBinding> clips = [];
                    for (int slot = 0; slot < descriptor.Animations.Count; slot++)
                    {
                        string original = descriptor.Animations[slot];
                        LgpEntry member = Resolve(original, ".a");
                        if (!animations.TryGetValue(member.Index, out var animation))
                        {
                            byte[] bytes = characters.ReadEntry(member);
                            PcFieldAnimation parsed = PcFieldAnimation.Read(bytes);
                            animation = (Hash(bytes), parsed.Frames.Count, parsed.BoneCount, parsed.RotationOrder);
                            animations.Add(member.Index, animation);
                        }
                        if (animation.Bones != model.Bones)
                        {
                            throw new InvalidDataException($"Descriptor clip {slot} has {animation.Bones} bones for its bound {model.Bones}-bone HRC.");
                        }
                        string clipId = Fingerprint("animation", writer =>
                        {
                            writer.Write(model.Rig);
                            writer.Write(animation.Hash);
                        });
                        clips.Add(new(sourceId, slot, original, member.FullPath, member.Index, clipId, animation.Frames, animation.Order));
                    }
                    sources.Add(new(sourceId, fieldEntry.FullPath, fieldEntry.Index, modelIndex, descriptor, hrcEntry.FullPath, model.Model, model.Geometry, model.Materials, model.Rig, clips.AsReadOnly()));
                }
                catch (Exception exception) when (CatalogError(exception))
                {
                    diagnostics.Add(new(sourceId, exception.Message));
                }
            }
        }
        return new(sources.ToArray(), diagnostics.ToArray());
    }

    private static bool CatalogError(Exception exception) => exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or IndexOutOfRangeException or UnauthorizedAccessException;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string Fingerprint(string kind, Action<BinaryWriter> write)
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes, Encoding.UTF8, leaveOpen: true);
        writer.Write("ff7-field-catalog-v1/" + kind);
        write(writer);
        writer.Flush();
        return "ff7/field/" + kind + "/" + Hash(bytes.ToArray());
    }
}

using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.Assets;

public sealed record FieldCharacterClipBinding(string SourceId, int NativeSlot, string AnimationId, int NativeFrameCount, float SecondsPerFrame);
public sealed record FieldCharacterSource(ModelCatalogEntry Entry, string ModelId, string GeometryId, string MaterialId, string RigId, IReadOnlyList<FieldCharacterClipBinding> Clips);
public sealed record FieldCharacterModel(string Id, string GeometryId, string MaterialId, string RigId, IReadOnlyList<string> Sources);
public sealed record FieldCharacterAnimation(string Id, string RigId, int NativeFrameCount, float SecondsPerFrame, IReadOnlyList<FieldCharacterClipBinding> Bindings);
public sealed record FieldCharacterCatalogDiagnostic(string SourceId, string Message);
public sealed record FieldCharacterAnimationBank(string TargetSourceId, string RigId, IReadOnlyList<FieldCharacterAnimation> Animations, IReadOnlyList<FieldCharacterClipBinding> NativeBindings)
{
    public int ResolveSlot(string sourceId, int nativeSlot)
    {
        string identity = NativeBindings.Single(binding => binding.SourceId == sourceId && binding.NativeSlot == nativeSlot).AnimationId;
        return Animations.Select((animation, index) => (animation.Id, Index: index)).Single(item => item.Id == identity).Index;
    }
}

/// <summary>Field-only content identities and original source bindings. No decoded models or poses are retained.</summary>
public sealed class FieldCharacterCatalog
{
    private readonly IReadOnlyDictionary<string, FieldCharacterSource> bySource;
    private readonly IReadOnlyDictionary<string, FieldCharacterAnimation> byAnimation;

    private FieldCharacterCatalog(FieldCharacterSource[] sources, FieldCharacterCatalogDiagnostic[] diagnostics)
    {
        Sources = Array.AsReadOnly(sources);
        Diagnostics = Array.AsReadOnly(diagnostics);
        Models = Array.AsReadOnly(sources.GroupBy(source => source.ModelId).Select(group => new FieldCharacterModel(group.Key, group.First().GeometryId, group.First().MaterialId, group.First().RigId, Array.AsReadOnly(group.Select(source => source.Entry.Id).ToArray()))).ToArray());
        Animations = Array.AsReadOnly(sources.SelectMany(source => source.Clips.Select(clip => (source.RigId, Clip: clip))).GroupBy(item => item.Clip.AnimationId).Select(group => new FieldCharacterAnimation(group.Key, group.First().RigId, group.First().Clip.NativeFrameCount, group.First().Clip.SecondsPerFrame, Array.AsReadOnly(group.Select(item => item.Clip).ToArray()))).ToArray());
        bySource = sources.ToDictionary(source => source.Entry.Id, StringComparer.Ordinal);
        byAnimation = Animations.ToDictionary(animation => animation.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<FieldCharacterSource> Sources { get; }
    public IReadOnlyList<FieldCharacterModel> Models { get; }
    public IReadOnlyList<FieldCharacterAnimation> Animations { get; }
    public IReadOnlyList<FieldCharacterCatalogDiagnostic> Diagnostics { get; }
    public IReadOnlyList<FieldCharacterSource> FindLibrarySources(string resourcePath) => Sources.Where(source => StringComparer.OrdinalIgnoreCase.Equals(source.Entry.ModelLibraryResource, resourcePath)).ToArray();
    public FieldCharacterSource GetSource(string sourceId) => bySource[sourceId];
    public FieldCharacterAnimation GetAnimation(string sourceId, int nativeSlot) => byAnimation[GetSource(sourceId).Clips.Single(clip => clip.NativeSlot == nativeSlot).AnimationId];
    /// <summary>Direct reuse requires a witnessed binding to the exact geometry and ordered rig; other variants require an explicit retarget profile.</summary>
    public IReadOnlyList<FieldCharacterAnimation> CompatibleAnimations(string sourceId)
    {
        FieldCharacterSource source = GetSource(sourceId);
        return Animations.Where(animation => animation.RigId == source.RigId && animation.Bindings.Any(binding => GetSource(binding.SourceId).GeometryId == source.GeometryId)).ToArray();
    }

    /// <summary>A stable deduplicated bank plan. Native field/slot bindings remain separate from bank ordering and script waits.</summary>
    public FieldCharacterAnimationBank CreateAnimationBank(string sourceId)
    {
        FieldCharacterSource source = GetSource(sourceId);
        IReadOnlyList<FieldCharacterAnimation> compatible = CompatibleAnimations(sourceId);
        string[] preferred = source.Clips.Select(clip => clip.AnimationId).Distinct().ToArray();
        FieldCharacterAnimation[] ordered = preferred.Select(id => byAnimation[id]).Concat(compatible.Where(animation => !preferred.Contains(animation.Id)).OrderBy(animation => animation.Id, StringComparer.Ordinal)).ToArray();
        FieldCharacterClipBinding[] bindings = compatible.SelectMany(animation => animation.Bindings).Where(binding => GetSource(binding.SourceId).GeometryId == source.GeometryId).ToArray();
        return new(sourceId, source.RigId, Array.AsReadOnly(ordered), Array.AsReadOnly(bindings));
    }

    /// <summary>Consumes one decoded original source at a time; equality includes the complete ordered rig and control-bone contract.</summary>
    public static FieldCharacterCatalog Create(IEnumerable<ModelCatalogEntry> entries, Func<ModelCatalogEntry, FieldModel> load, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(load);
        List<FieldCharacterSource> sources = [];
        List<FieldCharacterCatalogDiagnostic> diagnostics = [];
        foreach (ModelCatalogEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Category != "Field")
            {
                throw new ArgumentException("The field catalog accepts field sources only.", nameof(entries));
            }
            try
            {
                if (entry.LoadError is not null)
                {
                    throw new InvalidDataException(entry.LoadError);
                }
                FieldModel model = load(entry);
                string rig = Fingerprint("rig", writer =>
                {
                    writer.Write(model.ControlBones.RawHeader);
                    writer.Write(model.ControlBones.First);
                    writer.Write(model.ControlBones.Second);
                    writer.Write(model.ControlBones.Additional);
                    writer.Write(model.Bones.Count);
                    foreach (ModelBone bone in model.Bones)
                    {
                        writer.Write(bone.Parent);
                        writer.Write(bone.Length);
                    }
                });
                string geometry = Fingerprint("geometry", writer =>
                {
                    writer.Write(rig);
                    writer.Write(model.Vertices.Count);
                    foreach (ModelVertex vertex in model.Vertices)
                    {
                        Write(writer, vertex.Position);
                        writer.Write(vertex.Bone);
                        writer.Write(vertex.SkinBinding);
                    }
                    writer.Write(model.Faces.Count);
                    foreach (ModelFace face in model.Faces)
                    {
                        writer.Write(face.Vertices.Length);
                        foreach (int vertex in face.Vertices)
                        {
                            writer.Write(vertex);
                        }
                        writer.Write(face.TextureCoordinates.Length);
                        foreach (Vector2 uv in face.TextureCoordinates)
                        {
                            writer.Write(uv.X);
                            writer.Write(uv.Y);
                        }
                        writer.Write(face.Texture);
                        writer.Write(face.Quad);
                        writer.Write(face.SemiTransparent);
                        writer.Write(face.VertexColors?.Length ?? -1);
                        foreach (Vector4 color in face.VertexColors ?? [])
                        {
                            writer.Write(color.X);
                            writer.Write(color.Y);
                            writer.Write(color.Z);
                            writer.Write(color.W);
                        }
                    }
                    writer.Write(model.NativeParts?.Parts.Count ?? -1);
                    foreach (var part in model.NativeParts?.Parts ?? [])
                    {
                        foreach (short word in part.Words)
                        {
                            writer.Write(word);
                        }
                    }
                    writer.Write(model.NativeParts?.SkinBindings.Count ?? -1);
                    foreach (var skin in model.NativeParts?.SkinBindings ?? [])
                    {
                        writer.Write(skin.FirstVertex);
                        writer.Write(skin.VertexCount);
                        writer.Write(skin.Bone);
                        writer.Write(skin.OriginalFlags);
                    }
                });
                string materials = Fingerprint("materials", writer =>
                {
                    writer.Write(model.Textures.Count);
                    foreach (var image in model.Textures)
                    {
                        writer.Write(image.Width);
                        writer.Write(image.Height);
                        foreach (var pixel in image.Pixels)
                        {
                            writer.Write(pixel.R);
                            writer.Write(pixel.G);
                            writer.Write(pixel.B);
                            writer.Write(pixel.A);
                        }
                    }
                });
                List<FieldCharacterClipBinding> clips = [];
                for (int slot = 0; slot < model.Animations.Count; slot++)
                {
                    ModelAnimation animation = model.Animations[slot];
                    if (animation.Frames.Count == 0 || animation.Frames.Any(frame => frame.Rotations.Count != model.Bones.Count))
                    {
                        throw new InvalidDataException("A field clip does not match its decoded owner rig.");
                    }
                    string identity = Fingerprint("animation", writer =>
                    {
                        writer.Write(rig);
                        writer.Write(ModelExchange.FramesPerSecond);
                        writer.Write(animation.Frames.Count);
                        foreach (AnimationFrame frame in animation.Frames)
                        {
                            Write(writer, frame.Translation);
                            foreach (Vector3 rotation in frame.Rotations)
                            {
                                Write(writer, rotation);
                            }
                        }
                    });
                    clips.Add(new(entry.Id, slot, identity, animation.Frames.Count, 1f / ModelExchange.FramesPerSecond));
                }
                string modelId = Fingerprint("model", writer =>
                {
                    writer.Write(geometry);
                    writer.Write(materials);
                });
                sources.Add(new(entry, modelId, geometry, materials, rig, clips.AsReadOnly()));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or IndexOutOfRangeException or UnauthorizedAccessException)
            {
                diagnostics.Add(new(entry.Id, exception.Message));
            }
        }
        return new(sources.ToArray(), diagnostics.ToArray());
    }

    private static string Fingerprint(string kind, Action<BinaryWriter> write)
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes, Encoding.UTF8, leaveOpen: true);
        writer.Write("ff8-field-catalog-v1/" + kind);
        write(writer);
        writer.Flush();
        return "ff8/field/" + kind + "/" + Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
    }

    private static void Write(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }
}

public sealed partial class GameAssets
{
    /// <summary>Groups the proved ONE/MCH source catalog without loading field scenes or changing gameplay.</summary>
    public FieldCharacterCatalog DiscoverFieldCharacters(Func<ModelCatalogEntry, bool>? include = null, CancellationToken cancellationToken = default)
    {
        return FieldCharacterCatalog.Create(EnumerateFieldModelSources().Where(entry => include?.Invoke(entry) ?? true), ReadFieldModelCatalogSource, cancellationToken);
    }

    /// <summary>Loads a witnessed native clip for an exact target geometry/rig, without registering presentation textures or changing animation clocks.</summary>
    public ModelAnimation LoadFieldCharacterAnimation(FieldCharacterCatalog catalog, string targetSourceId, string animationId)
    {
        FieldCharacterSource target = catalog.GetSource(targetSourceId);
        FieldCharacterAnimation animation = catalog.CompatibleAnimations(targetSourceId).Single(item => item.Id == animationId);
        FieldCharacterClipBinding binding = animation.Bindings.First(item => catalog.GetSource(item.SourceId).GeometryId == target.GeometryId);
        FieldModel source = ReadFieldModelCatalogSource(catalog.GetSource(binding.SourceId).Entry);
        FieldCharacterCatalog current = FieldCharacterCatalog.Create([catalog.GetSource(binding.SourceId).Entry], _ => source);
        if (current.GetSource(binding.SourceId).GeometryId != target.GeometryId || current.GetAnimation(binding.SourceId, binding.NativeSlot).Id != animationId)
        {
            throw new InvalidDataException("The selected field source changed after catalog discovery. Rebuild the catalog before loading its animation.");
        }
        return source.Animations[binding.NativeSlot];
    }
}

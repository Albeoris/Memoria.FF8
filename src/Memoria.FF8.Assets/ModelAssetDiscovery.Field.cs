using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private IEnumerable<ModelAssetDiscoveryItem> DiscoverFieldReferences(ModelCatalogEntry selected, CancellationToken cancellationToken)
    {
        string? reference = null;
        string? error = null;
        try
        {
            byte[] bytes = resources.Read(new ResourceKey(selected.ResourcePath));
            IReadOnlyList<NativeFieldOneModelEntry> directory = FieldModelReader.ReadDirectory(bytes);
            NativeFieldOneModelEntry source = directory[selected.ModelIndex];
            if (source.Flag >> 28 == 0xD)
            {
                if (source.Name.Length < 4)
                {
                    throw new InvalidDataException("Main-character reference requires four identifier bytes.");
                }
                reference = source.Name[..4];
            }
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            error = exception.Message;
        }
        if (error is not null)
        {
            yield return DiscoveryDiagnostic(selected, "field-reference", error);
            yield break;
        }
        if (reference is null)
        {
            yield break;
        }
        for (int field = 0; field < fieldNames.Length; field++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = fieldNames[field];
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            string path = $"field/{name}/chara.one";
            yield return new(selected.Id + "/scan/" + field, "Discovering field references: " + name, ModelAssetDiscoveryKind.Progress, selected.Id, path, "", -1, "parsed main_chr reverse references");
            IReadOnlyList<NativeFieldOneModelEntry>? directory = null;
            error = null;
            try
            {
                ResourcePrefix prefix = resources.ReadPrefix(new ResourceKey(path), 65536);
                directory = FieldModelReader.ReadDirectory(prefix.Bytes, prefix.Length ?? prefix.Bytes.Length);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                error = exception.Message;
            }
            if (directory is null)
            {
                yield return DiscoveryDiagnostic(selected, "field-" + field, $"{name}: {error}");
                continue;
            }
            for (int index = 0; index < directory.Count; index++)
            {
                NativeFieldOneModelEntry candidate = directory[index];
                if (candidate.Flag >> 28 != 0xD || candidate.Name.Length < 4 || !candidate.Name.AsSpan(0, 4).Equals(reference.AsSpan(), StringComparison.OrdinalIgnoreCase) || path.Equals(selected.ResourcePath, StringComparison.OrdinalIgnoreCase) && index == selected.ModelIndex)
                {
                    continue;
                }
                ModelCatalogEntry occurrence = new($"field/{name}/{index}", candidate.Name + " · " + name + "/" + index, "Field", path, name, field, index);
                int[]? frames = null;
                error = null;
                try
                {
                    frames = ReadFieldClipFrameCounts(resources.Read(new ResourceKey(path)), candidate.Offset, candidate.Size);
                }
                catch (Exception exception) when (CatalogError(exception))
                {
                    error = exception.Message;
                }
                if (frames is null)
                {
                    yield return DiscoveryDiagnostic(occurrence, "clips", error ?? "The referring entry cannot be decoded.");
                    continue;
                }
                Lazy<EditableModel> model = new(() => LoadCatalogModel(occurrence));
                for (int clip = 0; clip < frames.Length; clip++)
                {
                    string target = ModelAssetDiscoveryBindings.ClipTarget(occurrence.Id, "embedded", clip);
                    yield return new(target, $"{name}/{index} / Clip {clip:000}", ModelAssetDiscoveryKind.Clip, selected.Id, path, "embedded", clip, "field main_chr reference " + reference + "; source entry " + index, () => model.Value)
                    {
                        GroupId = target,
                        RigOwnerId = selected.Id,
                        NativeFrameCount = frames[clip]
                    };
                }
            }
        }
    }
    internal static int[] ReadFieldClipFrameCounts(byte[] content, int offset, int length)
    {
        if (offset < 0 || length < 2 || offset > content.Length - length)
        {
            throw new InvalidDataException("Field animation extent lies outside its entry.");
        }
        using BinaryReader reader = new(new MemoryStream(content, offset, length, false));
        int count = reader.ReadUInt16();
        if (count > 1000)
        {
            throw new InvalidDataException("Field animation directory exceeds its supported bounds.");
        }
        int[] frames = new int[count];
        for (int clip = 0; clip < count; clip++)
        {
            frames[clip] = reader.ReadUInt16();
            int bones = reader.ReadUInt16();
            if (frames[clip] is 0 or > 10000 || bones is 0 or > 100)
            {
                throw new InvalidDataException("Field animation frame header exceeds its supported bounds.");
            }
            long bytes = checked((long)frames[clip] * (6 + bones * 4));
            if (bytes > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new InvalidDataException("Field animation samples exceed their entry.");
            }
            reader.BaseStream.Seek(bytes, SeekOrigin.Current);
        }
        return frames;
    }
}

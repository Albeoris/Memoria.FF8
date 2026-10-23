using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    /// <summary>Streams metadata without loading scenes, gameplay state, or model geometry.</summary>
    public IEnumerable<ModelCatalogEntry> EnumerateModelCatalog()
    {
        IFlArchive? battleArchive = null;
        string? battleError = null;
        try
        {
            battleArchive = OpenArchive("battle");
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            battleError = exception.Message;
        }
        if (battleArchive is null)
        {
            yield return new("battle/unavailable", "Battle models", "Battle model", "battle", "battle models") { LoadError = battleError };
        }
        using (IFlArchive? battle = battleArchive)
        {
            if (battle is not null)
            {
                foreach (IFlArchiveEntry entry in battle.Entries.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(entry.RelativePath.Replace('\\', '/')).ToLowerInvariant();
                    if (entry.Size <= 0 || !name.EndsWith(".dat") && !name.EndsWith(".x"))
                    {
                        continue;
                    }
                    string category = name.StartsWith("c0m") ? "Battle enemy" : name.StartsWith("a0stg") ? "Battle stage" : name.Length > 2 && name[0] == 'd' && name[2] == 'w' ? "Battle weapon" : "Battle model";
                    string path = "battle/" + name;
                    string label = name;
                    try
                    {
                        if (category == "Battle enemy")
                        {
                            label = ReadCatalogEnemyName(path);
                        }
                        else if (name.Length > 2 && name[0] == 'd' && name[2] is 'c' or 'w' && int.TryParse(name.AsSpan(1, 1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int character) && character <= 10)
                        {
                            label = GameNames.DefaultName(GameNames.CharacterSubject(character)) + " " + name;
                        }
                    }
                    catch (Exception exception) when (CatalogError(exception))
                    {
                    }
                    yield return new(path, label, category, path, $"{category} {label} {name} {path}");
                }
            }
        }
        byte[]? world = null;
        string? worldError = null;
        try
        {
            world = resources.Read(new ResourceKey("world/chara.one"));
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            worldError = exception.Message;
        }
        if (world is { Length: >= 12 })
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(world.AsSpan(world.Length - 4));
            if (count is > 0 and <= 64)
            {
                for (int shape = 0; shape < count; shape++)
                {
                    string name = $"World character shape {shape}";
                    yield return new($"world/{shape}", name, "World character", "world/chara.one", $"{name} world/chara.one {shape}", ModelIndex: shape);
                }
            }
            else
            {
                worldError = "The world character footer has an invalid model count.";
            }
        }
        else if (world is not null)
        {
            worldError = "The world character container is truncated.";
        }
        if (worldError is not null)
        {
            yield return new("world/chara", "World characters", "World character", "world/chara.one", "world character chara.one") { LoadError = worldError };
        }
        byte[]? wmset = null;
        string? staticError = null;
        try
        {
            wmset = resources.Read(new ResourceKey("world/wmsetus.obj"));
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            staticError = exception.Message;
        }
        if (wmset is not null)
        {
            int count = 0;
            try
            {
                int[] sections = WorldAssetReader.ReadSections(wmset);
                count = Math.Min(WorldAssetReader.InnerPointers(wmset, sections[15]).Length, 192);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                staticError = exception.Message;
            }
            for (int index = 0; index < count; index++)
            {
                int shape = index + 64;
                string name = $"World static shape {shape}";
                yield return new($"world/static/{shape}", name, "World static", "world/wmsetus.obj", $"{name} world/wmsetus.obj {index}", ModelIndex: shape);
            }
        }
        if (staticError is not null)
        {
            yield return new("world/static/unavailable", "World static models", "World static", "world/wmsetus.obj", "world static models wmsetus.obj") { LoadError = staticError };
        }
        for (int fieldId = 0; fieldId < fieldNames.Length; fieldId++)
        {
            string fieldName = fieldNames[fieldId];
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                continue;
            }
            string path = $"field/{fieldName}/chara.one";
            IReadOnlyList<Memoria.FF8.Runtime.NativeFieldOneModelEntry>? directory = null;
            string? error = null;
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
                yield return new($"field/{fieldName}/unavailable", fieldName, "Field", path, $"field {fieldId} {fieldName} {path}", fieldId) { LoadError = error };
                continue;
            }
            for (int index = 0; index < directory.Count; index++)
            {
                string name = directory[index].Name;
                yield return new($"field/{fieldName}/{index}", $"{name} · {fieldName}/{index}", "Field", path, $"field {fieldId} {fieldName} {index} {name} {path}", fieldId, index);
            }
        }
    }

    /// <summary>Loads one original model independently of field and battle initialization.</summary>
    public EditableModel LoadCatalogModel(ModelCatalogEntry entry)
    {
        if (entry.LoadError is not null)
        {
            throw new NotSupportedException(entry.LoadError);
        }
        byte[] bytes = resources.Read(new ResourceKey(entry.ResourcePath));
        if (entry.Category == "Field")
        {
            ResourceKey owner = new(entry.ResourcePath);
            FieldModel model = resources.GetSource<IFieldModelSource>() is { } source
                ? source.ReadModel(owner, bytes, ReadMainCharacter, entry.ModelIndex)
                : FieldModelReader.ReadSingle(bytes, ReadMainCharacter, entry.ModelIndex);
            return ModelExchange.FromField(model);
        }
        if (entry.Category == "World character")
        {
            return ModelExchange.FromField(WorldAssetReader.ReadWorldCharacter(bytes, entry.ModelIndex), ModelExchange.WorldFramesPerSecond);
        }
        if (entry.Category == "World static")
        {
            int[] sections = WorldAssetReader.ReadSections(bytes);
            int index = entry.ModelIndex - 64;
            int start = sections[15] + WorldAssetReader.InnerPointers(bytes, sections[15])[index];
            int textureStart = sections[41] + WorldAssetReader.InnerPointers(bytes, sections[41])[index];
            var texture = WorldAssetReader.ReadTim(bytes, textureStart);
            return ModelExchange.FromWorld(WorldStaticModelReader.Read(checked((byte)entry.ModelIndex), bytes.AsSpan(start, sections[16] - start), texture.Image));
        }
        string filename = Path.GetFileName(entry.ResourcePath);
        if (entry.Category == "Battle stage")
        {
            int id = int.Parse(filename.AsSpan(5, 3), System.Globalization.CultureInfo.InvariantCulture);
            return ModelExchange.FromStage(BattleResourceReader.ReadStage(id, bytes));
        }
        if (entry.Category == "Battle weapon")
        {
            string bodyName;
            using (IFlArchive battle = OpenArchive("battle"))
            {
                bodyName = battle.Entries.Select(item => Path.GetFileName(item.RelativePath.Replace('\\', '/'))).Where(name => name.StartsWith(filename[..2] + "c", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? throw new NotSupportedException("This weapon has no matching character skeleton.");
            }
            byte[] bodyBytes = resources.Read(new ResourceKey("battle/" + bodyName));
            BattleModel body = resources.GetSource<IBattleModelSource>()?.ReadModel(new ResourceKey("battle/" + bodyName), bodyBytes) ?? BattleResourceReader.ReadModel(bodyName, bodyBytes);
            BattleModel weapon = resources.GetSource<IBattleModelSource>()?.ReadWeapon(new ResourceKey(entry.ResourcePath), bytes, body) ?? BattleResourceReader.ReadWeapon(filename, bytes, body);
            return ModelExchange.FromBattle(weapon);
        }
        return ModelExchange.FromBattle(resources.GetSource<IBattleModelSource>()?.ReadModel(new ResourceKey(entry.ResourcePath), bytes) ?? BattleResourceReader.ReadModel(filename, bytes));
    }

    private string ReadCatalogEnemyName(string path)
    {
        using Stream stream = resources.Get(new ResourceKey(path)).OpenRead();
        byte[] header = new byte[32];
        stream.ReadExactly(header);
        int sections = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Enemy information requires the original monster directory.");
        }
        int offset = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(sections == 11 ? 28 : 4));
        if (offset < 0 || offset > 32 * 1024 * 1024)
        {
            throw new InvalidDataException("The enemy information address is invalid.");
        }
        if (stream.CanSeek)
        {
            stream.Seek(offset, SeekOrigin.Begin);
        }
        else
        {
            if (offset < header.Length)
            {
                throw new NotSupportedException("This enemy name requires a seekable resource.");
            }
            byte[] skip = new byte[4096];
            int remaining = offset - header.Length;
            while (remaining != 0)
            {
                int count = Math.Min(remaining, skip.Length);
                stream.ReadExactly(skip.AsSpan(0, count));
                remaining -= count;
            }
        }
        byte[] name = new byte[24];
        stream.ReadExactly(name);
        int end = Array.IndexOf(name, (byte)0);
        if (end < 0)
        {
            throw new InvalidDataException("The enemy name has no terminator.");
        }
        return FF8Encoding.CreateEuropean().GetString(name.AsSpan(0, end));
    }

    private static bool CatalogError(Exception exception) => exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or IndexOutOfRangeException or UnauthorizedAccessException;
}

using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Text;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    private Dictionary<int, string>? battleImageNames;

    public BattleModel ReadWeapon(ResourceKey owner, byte[] content, BattleModel character)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string name = Path.GetFileNameWithoutExtension(owner.Path);
        BattleModel model = BattleResourceReader.ReadWeapon(Path.GetFileName(owner.Path), content, character);
        ClearBattleImageBindings(owner);
        string preferred = $"textures/battle.fs/hd_new/{name}_0.png";
        string fallback = $"textures/battle.fs/hd/{name}_0.png";
        string? path = Contains(preferred) ? preferred : Contains(fallback) ? fallback : null;
        if (path is not null)
        {
            if (model.Textures.Count != 1 || model.TextureRegions.Count != 1 || model.TextureRegions[0].CanvasWidth != 128 || model.TextureRegions[0].CanvasHeight != 32)
            {
                throw new NotSupportedException("The selected battle weapon image has no supported native 128x32 material.");
            }
            imageBindings[new TextureKey(owner, "texture/0")] = new ImageBinding(path, false);
        }
        return model;
    }

    private void BindBattleMonsterImages(ResourceKey owner, string name, BattleModel model)
    {
        if (name.Length != 6 || !name.StartsWith("c0m", StringComparison.OrdinalIgnoreCase) || !int.TryParse(name.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
        {
            return;
        }
        battleImageNames ??= ReadBattleImageNames();
        if (!battleImageNames.TryGetValue(1000 + index, out string? stem))
        {
            return;
        }
        for (int texture = 0; texture < model.Textures.Count; texture++)
        {
            if (texture >= model.TextureRegions.Count || model.TextureRegions[texture] is not { CanvasWidth: 128, CanvasHeight: 128 } region || region.X is not (640 or 704 or 768) || region.Y is not (0 or 128))
            {
                throw new NotSupportedException("The selected battle monster image has no supported native 128x128 material.");
            }
            int material = (region.X - 640) / 64 * 2 + region.Y / 128;
            string suffix = stem == "c0m034" && language == "jp" ? "_jp" : "";
            string path = $"textures/battle.fs/hd_new/{stem}_{material}{suffix}.png";
            if (!Contains(path))
            {
                continue;
            }
            imageBindings[new TextureKey(owner, $"texture/{texture}")] = new ImageBinding(path, false);
        }
    }

    private void ClearBattleImageBindings(ResourceKey owner)
    {
        foreach (TextureKey key in imageBindings.Keys.Where(key => key.Owner == owner).ToArray())
        {
            imageBindings.Remove(key);
        }
    }

    private Dictionary<int, string> ReadBattleImageNames()
    {
        using FileStream file = OpenAuthenticatedRuntimeCode();
        using PEReader image = new(file);
        const uint address = 0x117733D8;
        const int recordCount = 122;
        byte[] records = image.GetSectionData(checked((int)(address - image.PEHeaders.PEHeader!.ImageBase))).GetContent(0, recordCount * 32).ToArray();
        Dictionary<int, string> result = [];
        for (int index = 0; index < recordCount; index++)
        {
            ReadOnlySpan<byte> record = records.AsSpan(index * 32, 32);
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(record);
            if (pointer == 0)
            {
                if (index != recordCount - 1)
                {
                    throw new InvalidDataException("Native battle image table terminates unexpectedly.");
                }
                return result;
            }
            int identity = BinaryPrimitives.ReadInt32LittleEndian(record[8..]);
            int part = BinaryPrimitives.ReadInt32LittleEndian(record[12..]);
            if (identity < 1000 || part != -1)
            {
                continue;
            }
            byte[] encoded = image.GetSectionData(checked((int)(pointer - image.PEHeaders.PEHeader.ImageBase))).GetContent(0, 32).ToArray();
            int end = Array.IndexOf(encoded, (byte)0);
            if (end <= 0 || encoded.AsSpan(0, end).ContainsAnyExceptInRange((byte)'0', (byte)'z'))
            {
                throw new InvalidDataException("Native battle image table has an invalid resource name.");
            }
            if (!result.TryAdd(identity, Encoding.ASCII.GetString(encoded, 0, end)))
            {
                throw new InvalidDataException("Native battle image table has a repeated model identity.");
            }
        }
        throw new InvalidDataException("Native battle image table has no terminating record.");
    }
}

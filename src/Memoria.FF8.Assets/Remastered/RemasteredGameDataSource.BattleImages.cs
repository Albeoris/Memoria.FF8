using System.Globalization;
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
        Dictionary<int, string> result = [];
        foreach (RemasteredBattleImageRecord record in ReadBattleImageTable())
        {
            if (record.Identity < 1000 || record.Member != -1)
            {
                continue;
            }
            if (!result.TryAdd(record.Identity, record.Name))
            {
                throw new InvalidDataException("Native battle image table has a repeated model identity.");
            }
        }
        return result;
    }
}

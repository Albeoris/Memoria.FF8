using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleSummonModelDraw(bool FatChocobo, int Clip, int Frame, Ff7BattleSummonPosition Position, int Yaw);
public readonly record struct Ff7BattleSummonPosition(int X, int Y, int Z)
{
    public System.Numerics.Vector3 ToVector() => new(X, Y, Z);
}

public sealed record Ff7BattleSummonModel(BattleModelData Model, IReadOnlyList<PcPolygonModel?> Parts, IReadOnlyList<PcTexture> Textures)
{
    private readonly Dictionary<int, IReadOnlyList<BattleClipFrame>> frames = [];

    public BattleClipFrame Frame(int clip, int frame)
    {
        if (!frames.TryGetValue(clip, out IReadOnlyList<BattleClipFrame>? animation))
        {
            animation = BattleClipDecoder.Decode(Model.BodyClips[clip]);
            frames.Add(clip, animation);
        }
        return animation[frame % animation.Count];
    }

    public static Ff7BattleSummonModel Load(LgpArchive archive, bool fatChocobo)
    {
        ArgumentNullException.ThrowIfNull(archive);
        string prefix = fatChocobo ? "debcyoda" : "mogurida";
        byte[] skeleton = Read(".d");
        BattleModelData model = BattleModelData.Read(skeleton, Read(".a00"));
        PcPolygonModel?[] parts = new PcPolygonModel?[model.Bones.Count];
        for (int index = 0; index < parts.Length; index++)
        {
            if (model.Bones[index].HasModel)
            {
                parts[index] = PcPolygonModel.Read(Read($".p{index:D2}"));
            }
        }
        PcTexture[] textures = new PcTexture[fatChocobo ? 0 : skeleton[32]];
        for (int index = 0; index < textures.Length; index++)
        {
            textures[index] = PcTexture.Read(Read($".t{index:D2}"));
        }
        return new(model, Array.AsReadOnly(parts), Array.AsReadOnly(textures));

        byte[] Read(string suffix)
        {
            IReadOnlyList<LgpEntry> entries = archive.Find(prefix + suffix);
            if (entries.Count != 1)
            {
                throw new InvalidDataException($"FFVII summon model member {prefix + suffix} is absent or ambiguous.");
            }
            return archive.ReadEntry(entries[0]);
        }
    }
}

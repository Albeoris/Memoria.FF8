using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Immutable MCH records copied by531310;5318D0 clears only their runtime flags.</summary>
public sealed record NativeModelPartsDefinition(string SourceHash, int SourceOffset, IReadOnlyList<NativeModelPartDefinition> Parts, IReadOnlyList<NativeModelSkinBinding> SkinBindings)
{
    public void Validate()
    {
        if (SourceHash is null || SourceHash.Length != 64 || !SourceHash.All(Uri.IsHexDigit) || SourceOffset < 0 || Parts is null || Parts.Count > 1000 || SkinBindings is null || SkinBindings.Count > 1000)
        {
            throw new InvalidDataException("Native model parts require bounded authentic MCH tables.");
        }
        foreach (NativeModelPartDefinition part in Parts)
        {
            if (part is null || part.Words is null || part.Words.Count != 16 || part.FirstSkinBinding < 0 || part.SkinBindingCount < 0 || part.FirstSkinBinding + part.SkinBindingCount > SkinBindings.Count)
            {
                throw new InvalidDataException("Native model part has invalid original WORDs or skin range.");
            }
        }
        if (SkinBindings.Any(skin => skin is null || skin.FirstVertex < 0 || skin.VertexCount < 0 || skin.Bone < 0))
        {
            throw new InvalidDataException("Native model skin binding is malformed.");
        }
    }

    public void RequireDisjointTranslationGeometry()
    {
        Validate();
        HashSet<int> selected = [];
        foreach (NativeModelPartDefinition part in Parts)
        {
            for (int skin = part.FirstSkinBinding; skin < part.FirstSkinBinding + part.SkinBindingCount; skin++)
            {
                if (!selected.Add(skin))
                {
                    throw new InvalidDataException("Native translated parts sharing a skin require separate immediate part draws; a flat pose cannot merge them.");
                }
            }
        }
        int end = 0;
        foreach (NativeModelSkinBinding skin in selected.Select(index => SkinBindings[index]).Where(skin => skin.VertexCount > 0).OrderBy(skin => skin.FirstVertex))
        {
            if (skin.FirstVertex < end)
            {
                throw new InvalidDataException("Native translated skin vertex ranges overlap; a flat pose cannot replace original per-part scratch geometry.");
            }
            end = skin.FirstVertex + skin.VertexCount;
        }
    }
}

public sealed record NativeModelPartDefinition(IReadOnlyList<short> Words)
{
    [JsonIgnore] public short FirstSkinBinding => Words[0];
    [JsonIgnore] public short SkinBindingCount => Words[1];
    [JsonIgnore] public short Counter => Words[2];
    [JsonIgnore] public short StartX => Words[4];
    [JsonIgnore] public short StartY => Words[5];
    [JsonIgnore] public short StartZ => Words[6];
    [JsonIgnore] public ushort OriginalFlags => unchecked((ushort)Words[7]);
    [JsonIgnore] public short TargetX => Words[12];
    [JsonIgnore] public short TargetY => Words[13];
    [JsonIgnore] public short TargetZ => Words[14];
    [JsonIgnore] public short Duration => Words[15];
}

public sealed record NativeModelSkinBinding(short FirstVertex, short VertexCount, short Bone, ushort OriginalFlags);

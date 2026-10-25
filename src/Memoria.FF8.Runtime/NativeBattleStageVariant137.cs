using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageVariant137State([property: JsonRequired] int? Index, [property: JsonRequired] int? Counter, [property: JsonRequired] ImmutableArray<short?> RotationWords)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? GeometryAddress { get; init; }

    public static NativeBattleStageVariant137State FromColdProcess() => new(0, 0, [0, 0, 0, 0]) { GeometryAddress = 0 };

    public static NativeBattleStageVariant137State Unknown() => new(null, null, [null, null, null, null]);

    public void Validate()
    {
        if (RotationWords.IsDefault || RotationWords.Length != 4)
        {
            throw new InvalidDataException("Native stage variant rotation storage requires its four retained WORDs.");
        }
    }

    public int ReadIndex()
    {
        return Index ?? throw new NotSupportedException("Native stage variant selection requires its retained current index.");
    }

    public short ReadRotationWord(uint address)
    {
        Validate();
        if (address < 0x1D99BE8 || address > 0x1D99BEE || (address & 1) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
        return RotationWords[checked((int)(address - 0x1D99BE8) / 2)] ?? throw new NotSupportedException("Native stage variant rotation requires its actual retained WORD.");
    }

    public int SelectCamera50E4E0()
    {
        int index = ReadIndex();
        return unchecked(index / 8 * 16 + index % 8);
    }
}

public sealed partial record NativeBattleStageState
{
    public NativeBattleStageState Rebuild50E3C0(NativeBattleStageSource source, NativeBattleStageColors colors, short rotationSpeed, Func<uint> random, Func<int> globalRandom, long? textureAnimationStartCallback = null)
    {
        Validate();
        if (source.Groups.IsDefault || source.Groups.Length != 4 || source.Groups.Any(group => group is null) || source.Geometry is not { IsDefault: false } geometry || source.TextureAnimations.IsDefault)
        {
            throw new InvalidDataException("Native stage variant rebuild requires all four current groups and decoded geometry.");
        }
        NativeBattleStageExtendedState extended = Extended ?? throw new NotSupportedException("Native stage variant rebuild requires its retained stage process.");
        ImmutableArray<NativeBattleStageGroup>.Builder groups = Groups.ToBuilder();
        for (int group = 0; group < 4; group++)
        {
            groups[group] = Select509440(groups[group] with { Source = source.Groups[group] });
        }
        (NativeBattleStage137State state, short speed) = NativeBattleStage137.CompleteVariant50E3C0(extended.Stage137, rotationSpeed, random, globalRandom);
        NativeBattleStageState result = this with { ResourceStageId = source.ResourceStageId, TextureAnimationStartCallback = textureAnimationStartCallback, Groups = groups.ToImmutable(), MutableGeometry = geometry, TextureAnimations = source.TextureAnimations.IsEmpty ? null : source.TextureAnimations, ModelColors = colors with { Color44 = (colors.Color44 & 0xFFFFFF) | 0x32000000 }, RotationSpeed = speed, Extended = extended with { Stage137 = state } };
        result.Validate();
        return result;
    }
}

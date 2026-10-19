using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325Resources(NativeBattleGfAnimationSource OpeningProgramme, NativeVibrationBank Vibration)
{
    public ImmutableArray<NativeBattleEffectFileAsset> Files { get; init; } = [];
    public ImmutableArray<uint> Sounds { get; init; } = [];
    public ImmutableArray<byte> WellGeometry { get; init; } = [];
    public ImmutableArray<byte> PuffSprite { get; init; } = [];
    public ImmutableArray<byte> PrimitiveKinds { get; init; } = [];
    public ImmutableArray<byte> TextureKinds { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? ParticlePrimitiveKinds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? ParticleTextureKinds { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? RingPuffSprite0 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? RingPuffSprite1 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDiablos325LateResources? LateResources { get; init; }

    public ImmutableArray<byte> ReadPuffSprite(uint address)
    {
        ImmutableArray<byte> data = address switch
        {
            0xFD67A0 => PuffSprite,
            0xFD6814 => RingPuffSprite0 ?? throw new NotSupportedException("Native Diablos requires its first original ring sprite."),
            0xFD69C0 => RingPuffSprite1 ?? throw new NotSupportedException("Native Diablos requires its second original ring sprite."),
            _ => throw new NotSupportedException("Native Diablos puff sprite pointer has no retained original resource owner.")
        };
        if (data.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Diablos requires its selected original puff sprite bytes.");
        }
        return data;
    }

    public void Validate()
    {
        if (OpeningProgramme is null || OpeningProgramme.Address != 0xFD8584 || OpeningProgramme.Data.IsDefaultOrEmpty || OpeningProgramme.Data.Length != 0x3890 || Vibration is null)
        {
            throw new InvalidDataException("Native Diablos requires its original opening programme and vibration bank.");
        }
        LateResources?.Validate();
        Vibration.Validate();
        _ = new NativeBattleGfAnimationWorkspace(ImmutableArray.CreateRange(new byte[0xE0])).Initialize7016B0(OpeningProgramme, 0xE0);
    }
}

using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325Resources(NativeBattleGfAnimationSource OpeningProgramme, NativeVibrationBank Vibration)
{
    public ImmutableArray<NativeBattleEffectFileAsset> Files { get; init; } = [];
    public ImmutableArray<uint> Sounds { get; init; } = [];
    public ImmutableArray<byte> PuffSprite { get; init; } = [];
    public ImmutableArray<byte> PrimitiveKinds { get; init; } = [];
    public ImmutableArray<byte> TextureKinds { get; init; } = [];

    public void Validate()
    {
        if (OpeningProgramme is null || OpeningProgramme.Address != 0xFD8584 || OpeningProgramme.Data.IsDefaultOrEmpty || OpeningProgramme.Data.Length != 0x3890 || Vibration is null)
        {
            throw new InvalidDataException("Native Diablos requires its original opening programme and vibration bank.");
        }
        Vibration.Validate();
        _ = new NativeBattleGfAnimationWorkspace(ImmutableArray.CreateRange(new byte[0xE0])).Initialize7016B0(OpeningProgramme, 0xE0);
    }
}

using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShockwavePulsar260Sound(uint Address, uint Value);
public sealed record NativeBattleShockwavePulsar260Resources(NativeBattleEffectHeapAllocation CasterDat, ImmutableArray<NativeBattleGfAnimationSource> Acts, ImmutableArray<NativeBattleModuleResource> PuffSprites, NativeBattleStaticMeshResource ParticleMesh, ImmutableArray<byte> DefaultDispatch, ImmutableArray<byte> MainDispatch, ImmutableArray<NativeBattleShockwavePulsar260Sound> Sounds, ImmutableArray<NativeBattleEffectFileAsset> Files)
{
    public void Validate()
    {
        uint[] addresses = [0x111D424, 0x111DEBC, 0x111F09C, 0x112015C, 0x112A974];
        int[] lengths = [2712, 4576, 4288, 43032, 10268];
        if (CasterDat.Identity != "FF8_EN:111B3CC" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Acts.IsDefault || Acts.Length != addresses.Length || PuffSprites.IsDefault || PuffSprites.Length != 2 || ParticleMesh.Address != 0x111D30C || ParticleMesh.Bytes.IsDefault || ParticleMesh.Bytes.Length != 280 || DefaultDispatch.IsDefault || DefaultDispatch.Length != 16 || MainDispatch.IsDefault || MainDispatch.Length != 15 || Sounds.IsDefault || Sounds.Length != 3 || Files.IsDefault || Files.Length != 2)
        {
            throw new InvalidDataException("Shockwave Pulsar requires its complete addressed resources.");
        }
        for (int index = 0; index < addresses.Length; index++)
        {
            if (Acts[index].Address != addresses[index] || Acts[index].Data.IsDefault || Acts[index].Data.Length != lengths[index])
            {
                throw new InvalidDataException("Shockwave Pulsar ACT source leaves its actual immutable extent.");
            }
        }
        for (int index = 0; index < 2; index++)
        {
            uint address = index == 0 ? 0x111CF4Cu : 0x111D0F8u;
            int length = index == 0 ? 428 : 532;
            if (PuffSprites[index].Address != address || PuffSprites[index].Bytes.IsDefault || PuffSprites[index].Bytes.Length != length || Files[index].FileId != 360 + index || !string.Equals(Files[index].Name, $"ma8def_{index}.tim", StringComparison.OrdinalIgnoreCase) || Files[index].Data.IsDefault || Files[index].Data.Length != 33824)
            {
                throw new InvalidDataException("Shockwave Pulsar requires its original puff and streamed texture directory.");
            }
        }
        for (int index = 0; index < DefaultDispatch.Length; index++)
        {
            if (DefaultDispatch[index] != 0x30)
            {
                throw new InvalidDataException("Shockwave Pulsar default geometry dispatch is incomplete.");
            }
        }
        for (int index = 0; index < MainDispatch.Length; index++)
        {
            if (MainDispatch[index] is not (0x30 or 0x31))
            {
                throw new InvalidDataException("Shockwave Pulsar geometry dispatch selects an unsupported original writer.");
            }
        }
        for (int index = 0; index < Sounds.Length; index++)
        {
            if (Sounds[index].Address != 0x111CF40u + (uint)index * 4)
            {
                throw new InvalidDataException("Shockwave Pulsar sound descriptor leaves its original table.");
            }
        }
    }
    public NativeBattleGfAnimationSource Act(uint address) => Acts.SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Shockwave Pulsar selected an unsupported ACT source.");
    public NativeBattleModuleResource Puff(uint address) => PuffSprites.SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Shockwave Pulsar selected an unsupported puff source.");
}

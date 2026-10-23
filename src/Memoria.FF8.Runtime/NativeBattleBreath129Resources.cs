using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreath129Resources(NativeBattleEffectHeapAllocation SourceSeeds, NativeBattleGfAnimationSource FirstAct, NativeBattleGfAnimationSource SecondAct, NativeBattleGfAnimationSource ThirdAct, NativeBattleGfAnimationSource FourthAct, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, ImmutableArray<byte> CapturedPairs, ImmutableArray<byte> ControlPairs, ImmutableArray<byte> ControlTriangles, ImmutableArray<byte> Streams, ImmutableArray<byte> DistanceTable, ImmutableArray<ushort> SqrtWords256, ImmutableArray<short> TrigonometricWords8192, uint Sound)
{
    public void Validate()
    {
        if (SourceSeeds.Identity != "FF8_EN:12272DC" || SourceSeeds.Data.IsDefault || SourceSeeds.Data.Length != MutableSourcesLength || Int(0) != 8 + VertexCount * 8 || Int(4) != VertexCount || Int(MeshLength) != IndexedVerticesLength || Int(MeshLength + 4) != 88 || FirstAct.Address != FirstActAddress || FirstAct.Data.IsDefault || FirstAct.Data.Length != 0x1AB0 || SecondAct.Address != SecondActAddress || SecondAct.Data.IsDefault || SecondAct.Data.Length != 0xC30 || ThirdAct.Address != ThirdActAddress || ThirdAct.Data.IsDefault || ThirdAct.Data.Length != 0x7F4 || FourthAct.Address != FourthActAddress || FourthAct.Data.IsDefault || FourthAct.Data.Length != 0xA50 || Sprite.Address != SpriteAddress || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 0xF8 || Sprite.Kind != 0 || Camera.Identity != "FF8_EN:12291CC" || Camera.Data.IsDefault || Camera.Data.Length != 0x128 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x128 || CapturedPairs.IsDefault || CapturedPairs.Length != 32 || ControlPairs.IsDefault || ControlPairs.Length != 32 || ControlTriangles.IsDefault || ControlTriangles.Length != 0x6F0 || Streams.IsDefault || Streams.Length != 0x500 || DistanceTable.IsDefault || DistanceTable.Length != 0x4000 || SqrtWords256.IsDefault || SqrtWords256.Length != 256 || TrigonometricWords8192.IsDefault || TrigonometricWords8192.Length != 8192 || Sound != 0x641DA)
        {
            throw new InvalidDataException("Breath requires its complete original addressed source, ACT, sprite, camera, control, stream and table fronts.");
        }
        int directory = Int(0);
        for (int group = 0; group < 7; group++)
        {
            if (Int(directory + group * 4) != 0)
            {
                throw new InvalidDataException("Breath requires its original GT4-only mutable source.");
            }
        }
        if (Int(directory + 28) != 160 || directory + 32 + 160 * 36 != MeshLength)
        {
            throw new InvalidDataException("Breath source polygon directory leaves its actual mesh extent.");
        }
        for (int face = 0; face < 160; face++)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                ushort index = BinaryPrimitives.ReadUInt16LittleEndian(SourceSeeds.Data.AsSpan().Slice(directory + 32 + face * 36 + 4 + corner * 2));
                if ((index & 1) != 0 || index / 2 >= VertexCount)
                {
                    throw new InvalidDataException("Breath source face leaves its selected vertex bank.");
                }
            }
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 9)
        {
            throw new InvalidDataException("Breath requires all original projected sprite frames.");
        }
        for (int frame = 0; frame < 9; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
        Pairs(CapturedPairs, 8);
        Pairs(ControlPairs, 74);
        if (BinaryPrimitives.ReadInt16LittleEndian(Streams.AsSpan()) != 16)
        {
            throw new InvalidDataException("Breath requires its original sixteen-entry linked stream directory.");
        }
        for (int stream = 0; stream < 16; stream++)
        {
            int offset = BinaryPrimitives.ReadInt16LittleEndian(Streams.AsSpan().Slice(2 + stream * 2)) * 2;
            if (offset < 34 || offset > Streams.Length - 2)
            {
                throw new InvalidDataException("Breath linked stream root leaves its bounded original directory owner.");
            }
        }
        for (int index = 0; index < 64; index++)
        {
            if (DistanceTable[0x3F80 + index * 2] != (byte)SqrtWords256[index] || DistanceTable[0x3F81 + index * 2] != (byte)(SqrtWords256[index] >> 8))
            {
                throw new InvalidDataException("Breath overlapping distance and sqrt views disagree on their actual shared bytes.");
            }
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(SourceSeeds.Data.AsSpan().Slice(offset, 4));
        static void Pairs(ImmutableArray<byte> bytes, int triangles)
        {
            for (int pair = 0; pair < bytes.Length; pair += 4)
            {
                int first = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(pair));
                int count = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(pair + 2));
                if (first < 0 || count <= 0 || first > triangles - count)
                {
                    throw new InvalidDataException("Breath fibre pair selection leaves its actual triangle owner.");
                }
            }
        }
    }

    public NativeBattleGfAnimationSource Act(uint address) => address switch
    {
        FirstActAddress => FirstAct,
        SecondActAddress => SecondAct,
        ThirdActAddress => ThirdAct,
        FourthActAddress => FourthAct,
        _ => throw new NotSupportedException("Breath selected an unsupported addressed ACT source.")
    };

    public ImmutableArray<byte> Read(uint address, int length)
    {
        if (length < 0 || address < CameraAddress && (ulong)address + (uint)length > MeshAddress)
        {
            throw new NotSupportedException("Breath mutable source bytes require their retained physical state owner.");
        }
        foreach ((uint origin, ImmutableArray<byte> bytes) in new (uint, ImmutableArray<byte>)[] { (FirstActAddress, FirstAct.Data), (SecondActAddress, SecondAct.Data), (ThirdActAddress, ThirdAct.Data), (FourthActAddress, FourthAct.Data), (SpriteAddress, Sprite.Bytes), (CameraAddress, Camera.Data), (CapturedPairsAddress, CapturedPairs), (ControlPairsAddress, ControlPairs), (ControlTrianglesAddress, ControlTriangles), (StreamDirectoryAddress, Streams), (DistanceTableAddress, DistanceTable) })
        {
            if (address >= origin && (ulong)address + (uint)length <= (ulong)origin + (uint)bytes.Length)
            {
                return bytes.Slice(checked((int)(address - origin)), length);
            }
        }
        if (address >= SqrtTableAddress && (ulong)address + (uint)length <= SqrtTableAddress + 512)
        {
            return SqrtWords256.SelectMany(BitConverter.GetBytes).ToImmutableArray().Slice(checked((int)(address - SqrtTableAddress)), length);
        }
        if (address >= TrigonometricTableAddress && (ulong)address + (uint)length <= TrigonometricTableAddress + 0x4000)
        {
            return TrigonometricWords8192.SelectMany(BitConverter.GetBytes).ToImmutableArray().Slice(checked((int)(address - TrigonometricTableAddress)), length);
        }
        throw new NotSupportedException("Breath selected bytes outside its bounded immutable original fronts.");
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, Read(address, length).AsSpan());
}

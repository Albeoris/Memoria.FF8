using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect83NodeResourceBank(ImmutableArray<byte> Data)
{
    public void Validate()
    {
        if (Data.IsDefault || Data.Length != NodeSourceLength || Dword(0) != 0)
        {
            throw new InvalidDataException("Effect83 requires its complete original cold relative node bank.");
        }
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = Dword(4 + table * 64 + index * 4);
                if (pointer != 0 && (pointer >= Data.Length || table == 0 && pointer > Data.Length - 344))
                {
                    throw new InvalidDataException("Effect83 node directory leaves its original resource.");
                }
                if (table != 0 || pointer == 0)
                {
                    continue;
                }
                for (int field = 0xC8; field < 0x148; field += 4)
                {
                    if (Dword(checked((int)pointer + field)) >= Data.Length)
                    {
                        throw new InvalidDataException("Effect83 node channel relocation leaves its exact current source owner.");
                    }
                }
            }
        }
        if (Enumerable.Range(0, 16).Count(index => Dword(4 + index * 4) != 0) != 13 || Dword(0x84 + 6 * 4) != 0)
        {
            throw new InvalidDataException("Effect83 requires its thirteen node records and intentional null mesh slot6.");
        }
    }

    public uint Directory(int table, int index)
    {
        if ((uint)table >= 4 || (uint)index >= 16)
        {
            throw new InvalidDataException("Effect83 node directory selection leaves its four physical tables.");
        }
        return Dword(4 + table * 64 + index * 4);
    }

    private uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(offset, 4));
}

public sealed record NativeBattleEffect83Resources(NativeBattleEffectInitializerAsset MainTim, NativeBattleEffectHeapAllocation EmbeddedTim, NativeBattleEffectHeapAllocation Camera, uint Sound, uint TargetRecordAddress, NativeBattleEffectOwnedModelResource Model, NativeBattleEffect83NodeResourceBank Nodes)
{
    public void Validate()
    {
        if (MainTim is null || MainTim.Name != "mag082.tim" || MainTim.Data.IsDefault || MainTim.Data.Length != TextureLength || EmbeddedTim is null || EmbeddedTim.Data.IsDefault || EmbeddedTim.Data.Length != EmbeddedTimLength || Camera is null || Camera.Identity != "FF8_EN:1578444" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || Sound != 0x66901 || TargetRecordAddress != TargetAddress || Model is null || Model.Address != ModelAddress || Model.Bytes.IsDefault || Model.Bytes.Length != ModelLength || Nodes is null)
        {
            throw new InvalidDataException("Effect83 resources differ from their original literal source identities or extents.");
        }
        ValidateTim(MainTim.Data, [320, 240, 256, 2, 384, 256, 320, 256]);
        ValidateTim(EmbeddedTim.Data, [320, 224, 256, 2, 832, 256, 64, 256]);
        if (!Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(index * 2, 2))).SequenceEqual(new ushort[] { 2, 8, 52, 656 }))
        {
            throw new InvalidDataException("Effect83 requires its original two-track camera header.");
        }
        int[] sections = [24, 1336, 6152, 10252, 10252];
        if (!Enumerable.Range(0, sections.Length).Select(Model.Section).SequenceEqual(sections))
        {
            throw new InvalidDataException("Effect83 requires its exact four-section owned model directory.");
        }
        Model.PoseSource.Validate();
        if (Model.PoseSource.BoneCount != 27 || Model.Geometry.IsDefault || Model.Geometry.Length != 1 || Model.Geometry[0].Vertices.Length != 141 || Model.Geometry[0].Polygons.Length != 239 || !NativeBattlePoseDecoder.ReadFrameCounts(Model.PoseSource.Animations.AsSpan()).SequenceEqual(new byte[] { 20, 35, 21 }))
        {
            throw new InvalidDataException("Effect83 owned model requires its original skeleton, body and animation programmes.");
        }
        Model.Geometry[0].Validate();
        Nodes.Validate();
    }

    public ImmutableArray<byte> ReadStatic(uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Effect83 immutable read has a negative extent.");
        }
        foreach ((uint start, ImmutableArray<byte> bytes) in new (uint, ImmutableArray<byte>)[] { (CameraAddress, Camera.Data), (ModelAddress, Model.Bytes), (EmbeddedTimAddress, EmbeddedTim.Data), (SoundAddress, BitConverter.GetBytes(Sound).ToImmutableArray()), (TargetPointerAddress, BitConverter.GetBytes(TargetRecordAddress).ToImmutableArray()) })
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)bytes.Length)
            {
                return bytes.Slice(checked((int)(address - start)), length);
            }
        }
        throw new NotSupportedException("Effect83 immutable read leaves its selected static fronts; node bytes require their current mutable owner.");
    }

    private static void ValidateTim(ImmutableArray<byte> data, ushort[] rectangles)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan()) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(4)) != 9)
        {
            throw new InvalidDataException("Effect83 texture requires its original indexed TIM header.");
        }
        int offset = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset, 4));
            for (int field = 0; field < 4; field++)
            {
                if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(offset + 4 + field * 2, 2)) != rectangles[block * 4 + field])
                {
                    throw new InvalidDataException("Effect83 texture rectangle differs from its original VRAM placement.");
                }
            }
            if (length != 12 + 2 * rectangles[block * 4 + 2] * rectangles[block * 4 + 3] || length > data.Length - offset)
            {
                throw new InvalidDataException("Effect83 TIM block leaves its exact original texture owner.");
            }
            offset += length;
        }
        if (offset != data.Length)
        {
            throw new InvalidDataException("Effect83 TIM has bytes outside its two original blocks.");
        }
    }
}

namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    private static uint RegisterResource5318D0(NativeFieldRegistrationMemory memory, SelectedModel item, uint address)
    {
        uint geometry = item.Geometry;
        memory.Write32(0x1DC3CDC, geometry);
        uint scale = item.Scale == 0 ? 16u : item.Scale;
        uint bones = memory.Read32(geometry);
        uint vertices = memory.Read32(geometry + 4);
        uint eyes = memory.Read32(geometry + 8);
        uint faces = memory.Read32(geometry + 12);
        memory.Write32(0x1DCD6DC, vertices);
        memory.Write32(0x1DCD6E0, eyes);
        uint parts = memory.Read32(geometry + 16);
        uint skins = memory.Read32(geometry + 20);
        uint untextured = memory.Read32(geometry + 24);
        uint textured = memory.Read32(geometry + 28);
        uint[] counts = [(ushort)untextured, untextured >> 16, (ushort)textured, textured >> 16];
        if (faces != counts.Sum(count => (long)count) || item.Textures.Count > 6)
        {
            throw new InvalidDataException("Native field geometry requires matching primitive counts and six bounded texture slots.");
        }
        memory.Write32(ResourcePointers + (uint)item.Index * 4, address);
        memory.Write8(address + 95, eyes == 0 ? (byte)0 : (byte)1);
        memory.Write32(address + 76, 0x12345678);
        memory.Copy(UvOffsets, address + 80, 2);
        memory.Write32(address + 36, parts);
        memory.Write32(address + 40, skins);
        memory.Write32(address + 44, bones);
        for (int type = 0; type < 4; type++)
        {
            memory.Write32(address + 48 + (uint)type * 4, counts[type]);
        }
        uint cursor = checked(address + 96);
        memory.Write32(address, cursor);
        cursor = checked(cursor + (bones + 1) * 64);
        int[] strides = [24, 32, 24, 28];
        for (int type = 0; type < 4; type++)
        {
            memory.Write32(address + 4 + (uint)type * 4, cursor);
            cursor = checked(cursor + (uint)strides[type] * counts[type]);
        }
        memory.Write32(address + 20, cursor);
        cursor = checked(cursor + 8 * eyes);
        memory.Write32(address + 24, cursor);
        cursor = checked(cursor + 8 * vertices);
        memory.Write32(address + 28, cursor);
        cursor = checked(cursor + 32 * parts);
        memory.Write32(address + 32, cursor);
        cursor = checked(cursor + 8 * skins);
        uint sourceBones = checked(geometry + memory.Read32(geometry + 32));
        memory.Write32(0x1DC3CD8, sourceBones);
        memory.Copy(sourceBones, memory.Read32(address) + 64, checked((int)bones * 64));
        for (uint bone = 0; bone < bones; bone++)
        {
            uint target = checked(memory.Read32(address) + (bone + 1) * 64);
            int length = checked((int)scale) * memory.ReadSigned16(sourceBones + bone * 64 + 8);
            memory.Write16(target + 8, unchecked((ushort)(length >> 4)));
            memory.Write32(target + 52, 0);
        }
        uint sourceVertices = checked(geometry + memory.Read32(geometry + 36));
        memory.Write32(0x1DC3CE8, sourceVertices);
        for (uint vertex = 0; vertex < vertices; vertex++)
        {
            uint target = checked(memory.Read32(address + 24) + vertex * 8);
            for (uint axis = 0; axis < 3; axis++)
            {
                int value = checked((int)scale) * memory.ReadSigned16(sourceVertices + vertex * 8 + axis * 2);
                memory.Write16(target + axis * 2, unchecked((ushort)(value >> 4)));
            }
        }
        memory.Copy(checked(geometry + memory.Read32(geometry + 40)), memory.Read32(address + 20), checked((int)eyes));
        memory.Copy(checked(geometry + memory.Read32(geometry + 48)), memory.Read32(address + 28), checked((int)parts * 32));
        memory.Copy(checked(geometry + memory.Read32(geometry + 52)), memory.Read32(address + 32), checked((int)skins * 8));
        uint animation = memory.Read32(geometry + 56);
        ushort clips = memory.Read16(animation);
        memory.Write16(address + 82, clips);
        memory.Write32(address + 84, cursor);
        uint stream = checked(cursor + (uint)clips * 4);
        memory.Write32(address + 88, stream);
        memory.Write32(address + 64, stream);
        uint input = animation + 2;
        for (uint clip = 0; clip < clips; clip++)
        {
            ushort frames = memory.Read16(input);
            ushort animationBones = memory.Read16(input + 2);
            memory.Write16(cursor + clip * 4, frames);
            memory.Write16(cursor + clip * 4 + 2, unchecked((ushort)(animationBones + 1)));
            input += 4;
            for (int frame = 0; frame < frames; frame++)
            {
                memory.Copy(input, stream, 6);
                input += 6;
                stream += 8;
                memory.Copy(input, stream, checked(animationBones * 4));
                input += (uint)animationBones * 4;
                stream += (uint)animationBones * 4;
            }
        }
        uint control = memory.Read32(geometry + 60);
        memory.Write16(address + 92, (ushort)control);
        memory.Write8(address + 94, (byte)(control >> 16));
        uint sourceFaces = checked(geometry + memory.Read32(geometry + 44));
        uint triangle = 0;
        uint quad = 0;
        for (uint face = 0; face < faces; face++)
        {
            uint original = checked(sourceFaces + face * 64);
            uint opcode = memory.Read32(original);
            bool isQuad = opcode == 0x2D010709;
            if (!isQuad && opcode != 0x25010607)
            {
                throw new NotSupportedException($"Native531310 reached unsupported original face header0x{opcode:X8}.");
            }
            uint type = isQuad ? 3u : 2u;
            uint index = isQuad ? quad++ : triangle++;
            if (index >= counts[type])
            {
                throw new InvalidDataException("Native face header exceeds its original primitive category count.");
            }
            uint target = checked(memory.Read32(address + 4 + type * 4) + index * (uint)strides[type]);
            memory.Copy(original + 12, target, isQuad ? 8 : 6);
            int texture = memory.ReadSigned16(original + 54);
            if (texture is < 0 or >= 6)
            {
                throw new InvalidDataException("Native face references an absent texture ordinal.");
            }
            ushort uv = memory.Read16(UvOffsets + (uint)texture * 2);
            uint uvStart = isQuad ? 8u : 6u;
            for (uint point = 0; point < (isQuad ? 4 : 3); point++)
            {
                memory.Write16(target + uvStart + point * 2, unchecked((ushort)(uv + memory.Read16(original + 44 + point * 2))));
            }
            uint colorStart = isQuad ? 20u : 16u;
            memory.Copy(Cluts + (uint)(texture + 6 * item.Index) * 2, target + colorStart - 4, 2);
            memory.Copy(TexturePages + (uint)(texture + 6 * item.Index) * 2, target + colorStart - 2, 2);
            memory.Fill(target + colorStart, 3, 128);
            memory.Copy(original + 29, target + colorStart + 3, 1);
            memory.Write32(target + colorStart + 4, unchecked((uint)memory.ReadSigned16(original + 8)));
        }
        if (triangle != counts[2] || quad != counts[3] || counts[0] != 0 || counts[1] != 0)
        {
            throw new NotSupportedException("Native field resource requires its reached textured primitive layout; unused untextured source tables remain unimplemented.");
        }
        uint partTable = memory.Read32(address + 28);
        uint skinTable = memory.Read32(address + 32);
        for (uint part = 0; part < parts; part++)
        {
            uint row = checked(partTable + part * 32);
            memory.Write16(row + 14, 0);
            int first = memory.ReadSigned16(row);
            int count = memory.ReadSigned16(row + 2);
            if (first < 0 || count < 0 || (ulong)(uint)first + (uint)count > skins)
            {
                throw new InvalidDataException("Native model part selects an invalid skin range.");
            }
            for (int skin = first; skin < first + count; skin++)
            {
                memory.Write16(checked(skinTable + (uint)skin * 8 + 6), 0);
            }
        }
        return stream;
    }
}

namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    private static uint RegisterEmpty531D40(NativeFieldRegistrationMemory memory, int index, int model, uint address)
    {
        memory.Write32(InstancePointers + (uint)index * 4, address);
        memory.Write32(address, unchecked((uint)model));
        InitializeInstanceTransform(memory, address);
        memory.Write8(address + 96, 0);
        return checked(address + 152);
    }

    private static void InitializeInstanceTransform(NativeFieldRegistrationMemory memory, uint address)
    {
        memory.Write16(address + 16, 0);
        memory.Write16(address + 18, 400);
        memory.Write16(address + 20, 0);
        memory.Write16(address + 24, 0);
        memory.Write16(address + 26, 0);
        memory.Write16(address + 28, 0);
        memory.Write32(address + 32, 4096);
        memory.Write32(address + 36, 4096);
        memory.Write32(address + 40, 4096);
        memory.Write16(address + 80, 0);
        memory.Write16(address + 82, 0);
    }

    private static uint RegisterInstance531980(NativeFieldRegistrationMemory memory, int index, int resourceIndex, uint address)
    {
        uint resource = memory.Read32(ResourcePointers + (uint)resourceIndex * 4);
        memory.Write32(InstancePointers + (uint)index * 4, address);
        memory.Write32(address, resource);
        memory.Write8(address + 96, 0);
        memory.Write8(address + 114, 0);
        memory.Write8(address + 97, 17);
        memory.Write16(address + 98, 0);
        memory.Copy(resource + 92, address + 112, 2);
        memory.Copy(resource + 94, address + 115, 1);
        memory.Write32(address + 120, uint.MaxValue);
        memory.Write16(address + 124, ushort.MaxValue);
        memory.Write16(address + 126, 0);
        uint bones = memory.Read32(resource + 44);
        uint transformed = checked(address + 152);
        memory.Write32(address + 4, transformed);
        memory.Copy(memory.Read32(resource), transformed, checked((int)(bones + 1) * 64));
        memory.Write16(address + 100, memory.Read16(resource + 82));
        memory.Copy(resource + 84, address + 104, 4);
        memory.Copy(resource + 88, address + 8, 4);
        memory.Write16(transformed + 4, 0);
        memory.Write16(transformed + 6, 0);
        memory.Write16(transformed + 8, 0);
        memory.Write16(transformed + 12, 0);
        memory.Write16(transformed + 14, 1024);
        memory.Write16(transformed + 16, 0);
        uint faces = 0;
        for (uint type = 0; type < 4; type++)
        {
            faces = checked(faces + memory.Read32(resource + 48 + type * 4));
        }
        uint masks = checked(transformed + (bones + 2) * 64);
        memory.Write32(address + 128, masks);
        memory.Fill(masks, checked((int)faces * 2), 0);
        uint cursor = checked((masks + faces * 2 + 3) & ~3u);
        uint clips = memory.Read32(address + 104);
        memory.Write16(address + 12, unchecked((ushort)(16 * memory.Read16(clips))));
        memory.Write16(address + 14, memory.Read16(clips + 2));
        memory.Copy(address + 8, address + 108, 4);
        memory.Copy(address + 8, address + 148, 4);
        memory.Write32(address + 84, cursor);
        for (uint buffer = 0; buffer < 2; buffer++)
        {
            memory.Write32(address + 88 + buffer * 4, cursor);
            memory.Write32(0x1DCB4C0 + buffer * 4, cursor);
            memory.Write32(0x1DC4004 + buffer * 4, cursor);
            memory.Write32(0x1DCA320 + buffer * 4, cursor);
            uint triangles = memory.Read32(resource + 56);
            uint triangleSource = memory.Read32(resource + 12);
            for (uint triangle = 0; triangle < triangles; triangle++)
            {
                uint source = checked(triangleSource + triangle * 24);
                memory.Write32(cursor, 0x07000000);
                memory.Copy(source + 16, cursor + 4, 3);
                memory.Write8(cursor + 7, (memory.Read8(source + 20) & 4) != 0 ? (byte)38 : (byte)36);
                memory.Copy(source + 6, cursor + 12, 2);
                memory.Copy(source + 12, cursor + 14, 2);
                memory.Copy(source + 8, cursor + 20, 2);
                memory.Copy(source + 14, cursor + 22, 2);
                memory.Copy(source + 10, cursor + 28, 2);
                memory.Write16(cursor + 30, memory.Read8(source + 19));
                cursor += 32;
            }
            memory.Write32(0x1DC3FEC + buffer * 4, cursor);
            uint quads = memory.Read32(resource + 60);
            uint quadSource = memory.Read32(resource + 16);
            for (uint quad = 0; quad < quads; quad++)
            {
                uint source = checked(quadSource + quad * 28);
                memory.Write32(cursor, 0x09000000);
                memory.Copy(source + 20, cursor + 4, 3);
                memory.Write8(cursor + 7, (memory.Read8(source + 24) & 4) != 0 ? (byte)46 : (byte)44);
                memory.Copy(source + 8, cursor + 12, 2);
                memory.Copy(source + 16, cursor + 14, 2);
                memory.Copy(source + 10, cursor + 20, 2);
                memory.Copy(source + 18, cursor + 22, 2);
                memory.Copy(source + 12, cursor + 28, 2);
                memory.Write16(cursor + 30, memory.Read8(source + 23));
                memory.Copy(source + 14, cursor + 36, 2);
                cursor += 40;
            }
        }
        InitializeInstanceTransform(memory, address);
        return cursor;
    }
}

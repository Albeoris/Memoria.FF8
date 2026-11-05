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

    private static uint RegisterInstance531980(NativeFieldRegistrationMemory memory, int index, NativeFieldRegisteredResource registered, NativeFieldResourceOwner bindings, uint address)
    {
        NativeFieldResourceCursor resource = ResolveResource(memory, registered);
        memory.Write32(InstancePointers + (uint)index * 4, address);
        memory.Write32(address, registered.Address);
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
        memory.Copy(memory.ReadPointer(resource), transformed, checked((int)(bones + 1) * 64));
        memory.Write16(address + 100, memory.Read16(resource + 82));
        WriteInstanceResourcePointer(memory, resource, bindings, address, 104, memory.ReadPointer(resource + 84));
        WriteInstanceResourcePointer(memory, resource, bindings, address, 8, memory.ReadPointer(resource + 88));
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
        NativeFieldResourceCursor clips = ReadInstanceResourcePointer(memory, resource, bindings, address, 104);
        memory.Write16(address + 12, unchecked((ushort)(16 * memory.Read16(clips))));
        memory.Write16(address + 14, memory.Read16(clips + 2));
        WriteInstanceResourcePointer(memory, resource, bindings, address, 108, ReadInstanceResourcePointer(memory, resource, bindings, address, 8));
        WriteInstanceResourcePointer(memory, resource, bindings, address, 148, ReadInstanceResourcePointer(memory, resource, bindings, address, 8));
        memory.Write32(address + 84, cursor);
        for (uint buffer = 0; buffer < 2; buffer++)
        {
            memory.Write32(address + 88 + buffer * 4, cursor);
            memory.Write32(0x1DCB4C0 + buffer * 4, cursor);
            memory.Write32(0x1DC4004 + buffer * 4, cursor);
            memory.Write32(0x1DCA320 + buffer * 4, cursor);
            uint triangles = memory.Read32(resource + 56);
            NativeFieldResourceCursor triangleSource = memory.ReadPointer(resource + 12);
            for (uint triangle = 0; triangle < triangles; triangle++)
            {
                NativeFieldResourceCursor source = triangleSource + triangle * 24;
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
            NativeFieldResourceCursor quadSource = memory.ReadPointer(resource + 16);
            for (uint quad = 0; quad < quads; quad++)
            {
                NativeFieldResourceCursor source = quadSource + quad * 28;
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

    private static NativeFieldResourceCursor ReadInstanceResourcePointer(NativeFieldRegistrationMemory memory, NativeFieldResourceCursor resource, NativeFieldResourceOwner? bindings, uint instance, uint field)
    {
        if (bindings is null)
        {
            if (resource.Owner is not null)
            {
                throw new InvalidDataException("Logical field resources require explicit instance cursor bindings.");
            }
            return NativeFieldResourceCursor.Guest(memory.Read32(instance + field));
        }
        if (resource.Owner is null)
        {
            throw new InvalidDataException("Instance cursor binding belongs to another field resource.");
        }
        return new(resource.Owner, memory.Read32(new NativeFieldResourceCursor(bindings, field)));
    }

    private static void WriteInstanceResourcePointer(NativeFieldRegistrationMemory memory, NativeFieldResourceCursor resource, NativeFieldResourceOwner? bindings, uint instance, uint field, NativeFieldResourceCursor target)
    {
        if (bindings is null)
        {
            if (resource.Owner is not null || target.Owner is not null)
            {
                throw new InvalidDataException("Guest field cursor storage cannot encode a resource-local offset.");
            }
            memory.Write32(instance + field, target.Offset);
            return;
        }
        if (resource.Owner is null || !ReferenceEquals(resource.Owner, target.Owner) || target.Offset > resource.Owner.Length)
        {
            throw new InvalidDataException("Instance cursor exceeds its selected shared field resource.");
        }
        memory.Write32(new NativeFieldResourceCursor(bindings, field), target.Offset);
        memory.Forget(instance + field, 4);
    }

    private static NativeFieldResourceCursor ResolveResource(NativeFieldRegistrationMemory memory, NativeFieldRegisteredResource resource) => resource.Resolve(memory.ReadResourcePointer(checked(ResourcePointers + (uint)resource.Index * 4)));

    private static NativeFieldResourceOwner? ResolveInstanceBindings(NativeFieldRegistrationMemory memory, NativeFieldRegisteredInstance instance, NativeFieldRegisteredResource resource)
    {
        if (instance.ResourceReference is not { } reference)
        {
            return null;
        }
        NativeBattleResourcePointer? pointer = memory.ReadResourcePointer(checked(InstancePointers + (uint)reference.InstanceIndex * 4));
        if (reference.ResourceIndex != resource.Index || pointer?.StagingAddress != instance.Address || pointer.FieldStorage is not { InstanceBindings: { } bindings } field || field.ResourceIndex != resource.Index || !pointer.HasValidOwner())
        {
            throw new InvalidDataException("Current instance resource cursors disagree with their retained table receipt.");
        }
        return bindings;
    }
}

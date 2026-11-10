using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Memoria.FF7.Resources;

public readonly record struct Ff7NativeModelPresentation(int ExtraY, int ExtraZ, int Mask);
public sealed record Ff7NativeEyeProfile(bool Enabled, string? ClosedLeft, string? ClosedRight);

public sealed class Ff7PcFieldPresentationData(Ff7NativeModelPresentation[][] fields, IReadOnlyDictionary<string, int> roles, IReadOnlyList<Ff7NativeEyeProfile> eyes, IReadOnlyList<float> nearPlanes)
{
    public Ff7NativeModelPresentation Model(int field, int slot)
    {
        if ((uint)field >= fields.Length || (uint)slot >= fields[field].Length)
        {
            throw new NotSupportedException("Native field presentation requires an original configured field/model slot.");
        }
        return fields[field][slot];
    }

    public float NearPlane(int field)
    {
        if ((uint)field >= fields.Length || nearPlanes.Count != fields.Length)
        {
            throw new NotSupportedException("Native field projection requires its original configured field identity.");
        }
        float near = nearPlanes[field];
        if (!float.IsFinite(near) || near <= 0)
        {
            throw new InvalidDataException("Native field projection has an invalid configured near plane.");
        }
        return near;
    }

    public int Role(string hrc) => roles.TryGetValue(Path.GetFileNameWithoutExtension(hrc), out int role) ? role : 9;
    public Ff7NativeEyeProfile Eyes(string hrc)
    {
        int role = Role(hrc);
        return eyes[role == 9 ? 0 : role];
    }
}

public sealed partial class Ff7PcExecutableData
{
    private static Ff7PcFieldPresentationData ReadFieldPresentationData(Stream stream, PEReader pe)
    {
        int I32(uint address) => BinaryPrimitives.ReadInt32LittleEndian(ReadVirtual(stream, pe, address, 4));
        byte U8(uint address) => ReadVirtual(stream, pe, address, 1)[0];
        if (I32(0x60EED7) != 1200 || I32(0x60EEE6) != 208 || U8(0x60F018) != 18)
        {
            throw new InvalidDataException("The native field presentation initializer dimensions are unsupported.");
        }
        int count = I32(0x60EED7);
        int slots = U8(0x60F018);
        float NearLiteral(uint instruction)
        {
            byte[] code = ReadVirtual(stream, pe, instruction, 10);
            if (code[0] != 0xC7 || ((code[1] & 0xF8) != 0x80 || (code[1] & 7) > 2) || BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(2)) != 0xCC2284)
            {
                throw new InvalidDataException("The native field near-plane initializer store is unsupported.");
            }
            float value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(6)));
            if (!float.IsFinite(value) || value <= 0)
            {
                throw new InvalidDataException("The native field near-plane initializer is not finite and positive.");
            }
            return value;
        }
        float[] nearPlanes = Enumerable.Repeat(NearLiteral(0x60EEEA), count).ToArray();
        void NearOverride(uint fieldOperand, uint instruction)
        {
            int field = I32(fieldOperand);
            if ((uint)field >= count)
            {
                throw new InvalidDataException("The native near-plane override leaves its original field table.");
            }
            nearPlanes[field] = NearLiteral(instruction);
        }
        NearOverride(0x60F062, 0x60F071);
        NearOverride(0x60F469, 0x60F478);
        NearOverride(0x60F485, 0x60F494);
        NearOverride(0x60F4A1, 0x60F4C3);
        int configuredNearCount = U8(0x60F51F);
        uint configuredNearTable = unchecked((uint)I32(0x60F528));
        if (configuredNearCount != 55 || configuredNearTable != 0x905B80)
        {
            throw new InvalidDataException("The native near-plane field-list contract is unsupported.");
        }
        float configuredNear = NearLiteral(0x60F532);
        HashSet<int> configuredFields = [];
        for (int index = 0; index < configuredNearCount; index++)
        {
            int field = I32(configuredNearTable + checked((uint)index * 4));
            if ((uint)field >= count || !configuredFields.Add(field))
            {
                throw new InvalidDataException("The native near-plane field list contains an invalid or duplicate identity.");
            }
            nearPlanes[field] = configuredNear;
        }
        Ff7NativeModelPresentation[][] fields = Enumerable.Range(0, count).Select(_ => Enumerable.Repeat(new Ff7NativeModelPresentation(I32(0x60F02E), I32(0x60F045), unchecked((sbyte)U8(0x60F05C))), slots).ToArray()).ToArray();
        void Store(uint fieldOperand, uint instruction, int axis, bool all = false)
        {
            int field = I32(fieldOperand);
            byte[] code = ReadVirtual(stream, pe, instruction, 11);
            bool indexed = code[0] == 0xC7 && code[1] == 0x84;
            bool mask = code[0] == 0xC6;
            if ((uint)field >= count || code[0] is not (0xC7 or 0xC6) || indexed != all)
            {
                throw new InvalidDataException("The native field presentation literal store is unsupported.");
            }
            uint destination = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(indexed ? 3 : 2));
            uint origin = axis == 0 ? 0xCC2288u : axis == 1 ? 0xCC22D0u : 0xCC2328u;
            int slot = checked((int)(destination - origin)) / (mask ? 1 : 4);
            int value = mask ? unchecked((sbyte)code[6]) : BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(indexed ? 7 : 6));
            if ((uint)slot >= slots || axis == 2 != mask)
            {
                throw new InvalidDataException("A native field presentation store leaves its original model array.");
            }
            foreach (int selected in all ? Enumerable.Range(0, slots) : [slot])
            {
                Ff7NativeModelPresentation previous = fields[field][selected];
                fields[field][selected] = axis switch
                {
                    0 => previous with { ExtraY = value },
                    1 => previous with { ExtraZ = value },
                    _ => previous with { Mask = value }
                };
            }
        }
        Store(0x60F062, 0x60F09F, 0, true);
        Store(0x60F062, 0x60F0B6, 1, true);
        Store(0x60F0C6, 0x60F0D5, 1);
        Store(0x60F0C6, 0x60F0E8, 1);
        Store(0x60F0F5, 0x60F104, 1);
        Store(0x60F111, 0x60F120, 0);
        Store(0x60F111, 0x60F133, 0);
        Store(0x60F111, 0x60F146, 1);
        Store(0x60F111, 0x60F159, 1);
        foreach (uint instruction in new uint[] { 0x60F179, 0x60F19F, 0x60F1C5, 0x60F1EB })
        {
            Store(0x60F166, instruction, 1);
            Store(0x60F166, instruction + 0x13, 0);
        }
        foreach (uint instruction in new uint[] { 0x60F21E, 0x60F244, 0x60F26A, 0x60F290 })
        {
            Store(0x60F20B, instruction, 1);
            Store(0x60F20B, instruction + 0x13, 0);
        }
        Store(0x60F2B0, 0x60F2BF, 1);
        Store(0x60F2B0, 0x60F2D2, 1);
        Store(0x60F2DF, 0x60F328, 0, true);
        Store(0x60F2DF, 0x60F33F, 1, true);
        int specialField = I32(0x60F2DF);
        int specialSlot = U8(0x60F300);
        fields[specialField][specialSlot] = fields[specialField][specialSlot] with { ExtraY = I32(0x60F316) };
        Store(0x60F34F, 0x60F379, 1, true);
        Store(0x60F389, 0x60F398, 2);
        Store(0x60F389, 0x60F3A8, 2);
        Store(0x60F3B2, 0x60F3DC, 1, true);
        Store(0x60F3EC, 0x60F41F, 1, true);
        Store(0x60F3F5, 0x60F41F, 1, true);
        Store(0x60F42F, 0x60F459, 1, true);
        string Text(uint address)
        {
            byte[] bytes = ReadVirtual(stream, pe, address, 64);
            int end = Array.IndexOf(bytes, (byte)0);
            if (end <= 0 || bytes.AsSpan(0, end).ContainsAnyInRange((byte)128, byte.MaxValue))
            {
                throw new InvalidDataException("A native model/eye resource identity is not bounded ASCII.");
            }
            return Encoding.ASCII.GetString(bytes, 0, end);
        }
        int[] roleIndices = [0, 3, 1, 4, 5, 7, 8, 6, 2, 2];
        Dictionary<string, int> roles = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < roleIndices.Length; index++)
        {
            roles.Add(Text(0x907B34u + (uint)index * 8), roleIndices[index]);
        }
        Ff7NativeEyeProfile[] eyes = new Ff7NativeEyeProfile[9];
        for (int role = 0; role < eyes.Length; role++)
        {
            uint address = 0x9078B8u + (uint)role * 20;
            uint left = unchecked((uint)I32(address + 8));
            uint right = unchecked((uint)I32(address + 16));
            eyes[role] = new(I32(address) != 0, left == 0 ? null : Text(left), right == 0 ? null : Text(right));
        }
        return new(fields, new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(roles), Array.AsReadOnly(eyes), Array.AsReadOnly(nearPlanes));
    }
}

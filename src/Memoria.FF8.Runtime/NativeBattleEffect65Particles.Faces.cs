using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Particles
{
    public static NativeBattleEffect65Frame DrawFaces(NativeBattleEffect65Frame frame, uint temporaryAddress, int temporaryLength, int contextOffset, int vertexCount, bool quad, uint cursorGlobal, byte depthShift, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleEffectArena temporary = Context(frame, temporaryAddress, temporaryLength);
        uint vertices = Dword(temporary, contextOffset);
        uint source = Dword(temporary, contextOffset + 4);
        Span<byte> faceStorage = stackalloc byte[36];
        Span<bool> faceKnown = stackalloc bool[36];
        Span<byte> vertexBytes = stackalloc byte[8];
        Span<bool> vertexKnown = stackalloc bool[8];
        ReadFaceBytes(frame, source, faceStorage[..4], faceKnown[..4]);
        int count = BinaryPrimitives.ReadInt32LittleEndian(faceStorage);
        source += 4;
        temporary = temporary.Write(contextOffset + 4, BitConverter.GetBytes(source));
        frame = PublishContext(frame, temporaryAddress, temporary, temporaryLength);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect65 faces require current projection controls.");
        for (int index = 0; index < count; index++)
        {
            Span<byte> face = faceStorage[..(quad ? 36 : 28)];
            ReadFaceBytes(frame, source, face, faceKnown[..face.Length]);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int corner = 0; corner < 3; corner++)
            {
                (NativeBattleVector point, short? w) = ReadFaceVertex(frame, face, corner, vertices, vertexCount, vertexBytes, vertexKnown);
                registers = NativeBattleGeometry.LoadVector(registers, corner, point, w);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            NativeBattleEffect65PacketWrites packet = new(quad ? 52 : 40);
            packet.Dword(0, quad ? 0x0C000000u : 0x09000000u);
            packet.Dword(4, FaceDword(face, 0));
            packet.Dword(12, FaceDword(face, 12));
            packet.Dword(24, FaceDword(face, 16));
            if (quad)
            {
                packet.Dword(36, FaceDword(face, 20));
                packet.Dword(48, FaceDword(face, 20) >> 16);
            }
            else
            {
                packet.Dword(36, FaceDword(face, 8) >> 16);
            }
            uint flags = registers.Colors.Flags!.Value;
            temporary = temporary.Write(contextOffset + 0x14, BitConverter.GetBytes(flags));
            int? bucket = null;
            bool accepted = false;
            if ((flags & 0x60000) == 0)
            {
                bool facing = true;
                if (!quad)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    temporary = temporary.Write(contextOffset + 8, BitConverter.GetBytes(area));
                    facing = area >= 0;
                }
                if (facing)
                {
                    uint[] screens = new uint[quad ? 4 : 3];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        screens[corner] = registers.Geometry.Sxy[corner]!.Value;
                        packet.Dword(8 + corner * 12, screens[corner]);
                    }
                    if (quad)
                    {
                        (NativeBattleVector point, short? w) = ReadFaceVertex(frame, face, 3, vertices, vertexCount, vertexBytes, vertexKnown);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
                        registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                        screens[3] = registers.Geometry.Sxy[2]!.Value;
                        packet.Dword(44, screens[3]);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect65 quad requires current ZSF4."));
                    }
                    else
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect65 triangle requires current ZSF3."));
                    }
                    bool outsideX = screens.All(screen => unchecked((short)screen) < 0 || unchecked((short)screen) > 2560);
                    bool outsideY = screens.All(screen => unchecked((short)(screen >> 16)) < 0 || unchecked((short)(screen >> 16)) > 1728);
                    if (!outsideX && !outsideY)
                    {
                        uint depth = registers.Geometry.Otz!.Value;
                        temporary = temporary.Write(contextOffset + 0x10, BitConverter.GetBytes(depth));
                        for (int corner = 1; corner < screens.Length; corner++)
                        {
                            packet.Dword(4 + corner * 12, FaceDword(face, (quad ? 24 : 20) + (corner - 1) * 4));
                        }
                        bucket = unchecked(17 + (unchecked((int)depth) >> depthShift));
                        accepted = true;
                    }
                }
            }
            uint opcode = face[3] & 0xFCu;
            ImmutableArray<int> payload = !accepted ? [] : opcode is >= 0x20 and <= 0x3C ? quad ? registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray() : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0] : [0, 0, 0, 0];
            NativeBattleGfGeometryAttempt attempt = packet.Attempt(0, bucket, accepted ? payload : []);
            frame = PublishContext(Publish(frame, registers), temporaryAddress, temporary, temporaryLength);
            frame = inputs.Execute(frame, new NativeBattleEffect65DeliverBatch(new([attempt], accepted ? quad ? 52 : 40 : 0), cursorGlobal));
            temporary = Context(frame, temporaryAddress, temporaryLength);
            source = checked(source + (uint)(quad ? 36 : 28));
        }
        temporary = temporary.Write(contextOffset + 4, BitConverter.GetBytes(source));
        return PublishContext(frame, temporaryAddress, temporary, temporaryLength);
    }

    private static uint FaceDword(ReadOnlySpan<byte> face, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(face[offset..]);

    private static void ReadFaceBytes(NativeBattleEffect65Frame frame, uint address, Span<byte> bytes, Span<bool> known)
    {
        ReadWithKnownness(frame, address, bytes, known);
        if (known.Contains(false))
        {
            throw new NotSupportedException("Native effect arena read intersects unresolved physical bytes.");
        }
    }

    private static (NativeBattleVector Point, short? W) ReadFaceVertex(NativeBattleEffect65Frame frame, ReadOnlySpan<byte> face, int corner, uint vertices, int vertexCount, Span<byte> bytes, Span<bool> known)
    {
        int physical = BinaryPrimitives.ReadUInt16LittleEndian(face[(4 + corner * 2)..]) * 4;
        if ((physical & 7) != 0 || physical > vertexCount * 8 - 8)
        {
            throw new InvalidDataException("Effect65 face leaves its current packed vertices.");
        }
        ReadWithKnownness(frame, checked(vertices + (uint)physical), bytes, known);
        if (known[..6].Contains(false))
        {
            throw new NotSupportedException("Effect65 projected face requires its reached current XYZ.");
        }
        return (new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]) : null);
    }
}

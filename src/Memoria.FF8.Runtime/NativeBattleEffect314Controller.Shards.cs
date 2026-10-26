using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Frame EmitShards664420(NativeBattleEffect314Frame frame, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs, uint model, uint offsetAddress, uint rotationBank, int threshold, short velocityMask, ushort texturePageAddition, ushort clutAddition)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect314Prepare662C00(model, offsetAddress));
        NativeBattleEffect298BodySource source = inputs.ReadBody(frame, model);
        NativeBattleGraphicsModel owner = inputs.ReadModel(frame, model);
        NativeBattleActionMatrix world = owner.GeometryWorldMatrix ?? throw new NotSupportedException("Effect314 shards require their actual model world matrix.");
        NativeBattleEffectArena offsets = inputs.ReadArena(frame, offsetAddress, checked(source.CurrentBones.Length * 16));
        ImmutableArray<NativeBattlePackedMatrix32>.Builder bones = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(source.CurrentBones.Length);
        for (int bone = 0; bone < source.CurrentBones.Length; bone++)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, source.CurrentBones[bone].ReadMatrix());
            matrix = matrix with { X = unchecked(matrix.X + I16(offsets, bone * 16)), Y = unchecked(matrix.Y + I16(offsets, bone * 16 + 2)), Z = unchecked(matrix.Z + I16(offsets, bone * 16 + 4)) };
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
            bones.Add(packed);
            frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(inputs.ResolveBoneRecord(frame, model, bone), PackedArena(packed), 32));
        }
        NativeBattleEffect314AddressStep allocation = inputs.AllocateTemporary(frame, 88);
        frame = allocation.Frame;
        uint temporaryAddress = allocation.Address;
        uint main = frame.State.ReadDword(WorkGlobal) + MainOffset;
        uint scratchAddress = source.VertexScratchAddress ?? throw new NotSupportedException("Effect314 shards require their physical vertex workspace.");
        int scratchLength = source.VertexScratchLength ?? throw new NotSupportedException("Effect314 shards require their loaded vertex capacity.");
        NativeBattleEffectArena context = inputs.ReadArena(frame, temporaryAddress, 88);
        context = context.Write(0x14, BitConverter.GetBytes(unchecked((short)threshold))).Write(0x16, BitConverter.GetBytes(velocityMask)).Write(0x18, BitConverter.GetBytes(texturePageAddition)).Write(0x1A, BitConverter.GetBytes(clutAddition));
        context = context.Write(0x1C, BitConverter.GetBytes(main)).Write(0x20, BitConverter.GetBytes(offsetAddress)).Write(0x54, BitConverter.GetBytes(rotationBank)).Write(4, BitConverter.GetBytes(scratchAddress));
        context = context.Write(0xC, BitConverter.GetBytes(owner.Color40 ?? throw new NotSupportedException("Effect314 shards require their current model color."))).Write(0x10, BitConverter.GetBytes((owner.SequenceState ?? throw new NotSupportedException("Effect314 shards require their current mesh mask.")).MeshMask124));
        frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporaryAddress, context, 88));
        uint geometry = source.GeometryAddress;
        int objects = unchecked((int)Dword(geometry));
        for (int objectIndex = 0; objectIndex < objects; objectIndex++)
        {
            if ((unchecked((uint)I32(context, 0x10)) & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            uint cursor = unchecked(geometry + Dword(geometry + 4 + (uint)(objectIndex * 4)));
            int groups = Word(cursor);
            cursor += 2;
            int vertexCount = 0;
            NativeBattleEffectArena scratch = inputs.ReadArena(frame, scratchAddress, scratchLength);
            for (int group = 0; group < groups; group++)
            {
                int bone = Word(cursor);
                int count = Word(cursor + 2);
                cursor += 4;
                if ((uint)bone >= (uint)bones.Count || count < 0)
                {
                    throw new InvalidDataException("Effect314 shard vertices leave their actual bone or vertex bank.");
                }
                short state = I16(offsets, bone * 16 + 6);
                if (state < 2)
                {
                    frame = LoadMatrix(frame, bones[bone]);
                }
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int destination = checked(vertexCount * 8);
                    if (destination > scratchLength - 8)
                    {
                        throw new InvalidDataException("Effect314 shards leave their loaded physical vertex capacity.");
                    }
                    ushort flags = 0;
                    if (state < 2)
                    {
                        NativeBattleEffectArena input = source.ReadGeometry(cursor, 6);
                        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(I16(input, 0), I16(input, 2), I16(input, 4)), 0));
                        scratch = StoreIr(scratch, destination, registers);
                        frame = Publish(frame, registers);
                        flags = state == 1 ? unchecked((ushort)(bone << 8)) : (ushort)0;
                        if (I16(scratch, destination + 2) <= unchecked((short)threshold))
                        {
                            flags |= 1;
                        }
                    }
                    scratch = scratch.Write(destination + 6, BitConverter.GetBytes(flags));
                    vertexCount++;
                    cursor += 6;
                }
            }
            frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(scratchAddress, scratch, scratchLength));
            cursor = (cursor + 3) & ~3u;
            int triangles = unchecked((ushort)Word(cursor));
            int quads = unchecked((ushort)Word(cursor + 2));
            cursor += 12;
            context = context.Write(8, BitConverter.GetBytes((ushort)triangles)).Write(10, BitConverter.GetBytes((ushort)quads)).Write(0, BitConverter.GetBytes(cursor));
            frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporaryAddress, context, 88));
            bool capacity = true;
            for (int kind = 0; kind < 2; kind++)
            {
                int faces = kind == 0 ? triangles : quads;
                int corners = kind == 0 ? 3 : 4;
                context = context.Write(kind == 0 ? 8 : 10, BitConverter.GetBytes(unchecked((ushort)(faces - 1))));
                frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporaryAddress, context, 88));
                for (int faceIndex = 0; faceIndex < faces; faceIndex++)
                {
                    NativeBattleEffectArena face = source.ReadGeometry(cursor, kind == 0 ? 16 : 20);
                    var vertices = ImmutableArray.CreateBuilder<NativeBattleEffectArena>(corners);
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int vertex = U16(face, corner * 2) & 0xFFF;
                        if (vertex >= vertexCount)
                        {
                            throw new InvalidDataException("Effect314 shard face leaves its actual current vertex bank.");
                        }
                        NativeBattleEffectArena point = NativeBattleEffectNodeContextMemory.Copy(scratch, vertex * 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                        vertices.Add(point);
                        context = NativeBattleEffectNodeContextMemory.Copy(point, 0, context, 0x24 + corner * 8, 8);
                    }
                    if (capacity)
                    {
                        (frame, capacity) = CreateFragment6646A0(frame, inputs, main, cursor, face, vertices.MoveToImmutable(), rotationBank, velocityMask, texturePageAddition, clutAddition);
                    }
                    cursor += kind == 0 ? 16u : 20u;
                    context = context.Write(kind == 0 ? 8 : 10, BitConverter.GetBytes(unchecked((ushort)(faces - faceIndex - 2))));
                    frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporaryAddress, context, 88));
                    if (!capacity)
                    {
                        break;
                    }
                }
            }
        }
        return inputs.ReleaseTemporary(frame, 88);

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(source.ReadGeometry(address, 4).Read(0, 4));
        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(source.ReadGeometry(address, 2).Read(0, 2));
    }
}

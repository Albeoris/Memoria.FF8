using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleZantetsukenRendering
{
    public static NativeBattleEffectArena Intersect(NativeBattleEffectArena temporary, int startOffset, int endOffset, int destinationOffset)
    {
        int[] start = [Short(temporary, startOffset), Short(temporary, startOffset + 2), Short(temporary, startOffset + 4)];
        int[] delta = [Short(temporary, endOffset) - start[0], Short(temporary, endOffset + 2) - start[1], Short(temporary, endOffset + 4) - start[2]];
        int[] normal = [Short(temporary, 0xA8), Short(temporary, 0xAA), Short(temporary, 0xAC)];
        int denominator = unchecked(normal[0] * delta[0] + normal[1] * delta[1] + normal[2] * delta[2]) >> 12;
        if (denominator == 0)
        {
            denominator = 1;
        }
        int numerator = unchecked((Short(temporary, 0xAE) << 12) + normal[2] * start[2] + normal[1] * start[1] + normal[0] * start[0]);
        int factor = unchecked(-(numerator / denominator));
        for (int lane = 0; lane < 3; lane++)
        {
            temporary = Word(temporary, destinationOffset + lane * 2, unchecked((short)(start[lane] + (unchecked(factor * delta[lane]) >> 12))));
        }
        int first = unchecked((ushort)Short(temporary, startOffset + 6));
        int second = unchecked((ushort)Short(temporary, endOffset + 6));
        int u = unchecked((first & 255) + (unchecked(((second & 255) - (first & 255)) * factor) >> 12));
        int v = unchecked((first & 0xFF00) + (unchecked(((second & 0xFF00) - (first & 0xFF00)) * factor) >> 12));
        return Word(temporary, destinationOffset + 6, unchecked((short)(u | (v & ~255))));
    }

    private sealed partial class Drawing
    {
        private void Clip(int mask)
        {
            for (int index = 0; index < 3; index++)
            {
                if ((mask & (1 << index)) != 0)
                {
                    temporary = Intersect(temporary, 0x48 + index * 16, 0x48 + (index + 1) % 3 * 16, 0x78 + index * 16);
                }
            }
            if (mask is < 1 or > 6)
            {
                LoadMatrix(inputs.Main);
                return;
            }
            int[] row = inputs.ClipIndices.AsSpan().Slice(mask * 8, 8).ToArray().Select(value => checked(0x48 + (int)value * 16)).ToArray();
            bool positive = unchecked((int)Read(temporary, row[3] + 12)) > 0;
            bool secondQuad = mask is 3 or 5 or 6;
            int[] first = row.Take(3).ToArray();
            int[] second = row.Skip(4).Take(secondQuad ? 4 : 3).ToArray();
            Emit(positive ? second : first, false, true);
            LoadMatrix(inputs.Alternate, 1);
            Emit(positive ? first : second, unchecked((int)Read(temporary, 0x38)) <= 0, true);
            LoadMatrix(inputs.Main);
        }

        private NativeBattleGeometryRegisterStep LoadVertex(NativeBattleGeometryRegisterStep step, int slot, int offset)
        {
            (byte[] bytes, bool[] known) = temporary.ReadSnapshot(offset, 8);
            if (known.Take(6).Any(value => !value))
            {
                throw new NotSupportedException("Zantetsuken clipping requires its actually transformed vertex coordinates.");
            }
            NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
            short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
            if (capturedPoints is not null)
            {
                capturedPoints[offset] = vector;
            }
            return NativeBattleGeometry.LoadVector(step, slot, vector, w);
        }

        private void Emit(int[] offsets, bool dynamicMask, bool project)
        {
            bool quad = offsets.Length == 4;
            NativeBattleGeometryRegisterStep step = Registers;
            NativeBattleActionMatrix? capturedMatrix = inputs.PilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(step.Geometry) : null;
            if (project)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    step = LoadVertex(step, corner, offsets[corner]);
                }
                step = NativeBattleGeometry.ProjectTriple(step, inputs.Projection, Controls.Dqa, Controls.Dqb);
            }
            int size = quad ? 40 : 32;
            byte[] packet = new byte[size];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[size];
            Dword(0, quad ? 0x09000000u : 0x07000000u);
            uint material = Read(temporary, 0x20);
            uint second = Read(temporary, 0x28);
            uint materialMask = dynamicMask ? unchecked(0x019FFFFFu + ((material & 0x02000000) != 0 ? 0x00600000u : 0)) : 0x01FFFFFF;
            if (dynamicMask)
            {
                temporary = Put(temporary, 0x3C, materialMask);
            }
            Dword(4, (second & 0x02000000) | material | (quad ? 0x2C000000u : 0x24000000u));
            Dword(12, Read(temporary, 0x24));
            Dword(20, second & materialMask);
            for (int corner = 0; corner < 3; corner++)
            {
                Dword(8 + corner * 8, step.Geometry.Sxy[corner] ?? throw new NotSupportedException("Zantetsuken packet emission requires its current screen FIFO."));
            }
            if (quad)
            {
                step = LoadVertex(step, 0, offsets[3]);
                step = NativeBattleGeometry.ProjectSingle(step, inputs.Projection, Controls.Dqa, Controls.Dqb);
            }
            for (int corner = 0; corner < offsets.Length; corner++)
            {
                PacketWord(12 + corner * 8, unchecked((ushort)Short(temporary, offsets[corner] + 6)));
            }
            if (quad)
            {
                Dword(32, step.Geometry.Sxy[2]!.Value);
                step = NativeBattleGeometry.Average45E610(step, Controls.Zsf4 ?? throw new NotSupportedException("Zantetsuken quad requires its actual four-depth scale."));
            }
            else
            {
                step = NativeBattleGeometry.Average45E5C0(step, Controls.Zsf3 ?? throw new NotSupportedException("Zantetsuken triangle requires its actual three-depth scale."));
            }
            uint depth = step.Geometry.Otz!.Value;
            temporary = Put(temporary, 0x40, depth);
            ImmutableArray<int> payload = quad
                ? step.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Zantetsuken quad proxy requires its current depth FIFO.")))).ToImmutableArray()
                : [unchecked((int)step.Geometry.Sz[1]!.Value), unchecked((int)step.Geometry.Sz[2]!.Value), unchecked((int)step.Geometry.Sz[3]!.Value), 0];
            int bucket = checked(inputs.TableBaseBucket + (unchecked((int)depth) >> inputs.DepthShift));
            ImmutableArray<uint> words = Enumerable.Range(0, size / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4, 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(bucket, words, payload);
            if (inputs.PilotIdentity is { } identity && capturedPoints is not null)
            {
                int role = unchecked(((((identity.Primitive * 397 + capturedObject) * 397 + capturedFace) * 397 + capturedTriangle) * 397 + capturedMatrixRole) * 397 + capturedPart++);
                System.Numerics.Vector3[] worlds = offsets.Select(offset => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(capturedMatrix!, capturedPoints[offset]), inputs.Projection)).ToArray();
                primitive = NativeBattleGilgameshVisuals.Capture(primitive, identity with { Primitive = role }, worlds);
            }
            attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            allocated = checked(allocated + size);
            temporary = Pointer(temporary, 0x34, inputs.PacketAddress is uint address ? unchecked(address + (uint)allocated) : null);
            Publish(step);

            void Dword(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset, 4), value);
                mask.AsSpan(offset, 4).Fill(NativeBattleGfPacketByteState.Known);
            }
            void PacketWord(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(offset, 2), value);
                mask.AsSpan(offset, 2).Fill(NativeBattleGfPacketByteState.Known);
            }
        }
    }
}

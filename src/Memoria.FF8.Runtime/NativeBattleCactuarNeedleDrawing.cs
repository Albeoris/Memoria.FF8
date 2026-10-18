using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCactuarNeedleDraw(NativeBattleActionMatrix CameraMatrix, NativeBattleVector PreparedPosition, NativeBattleGfGeometryBatch Packets);

/// <summary>5A9C10's saturated camera preparation and two ordered crossed FT4 attempts.</summary>
public static class NativeBattleCactuarNeedleDrawing
{
    public static NativeBattleCactuarNeedleDraw Emit5A9C10(NativeBattleActionMatrix nodeMatrix, NativeBattleVector position, uint color, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        nodeMatrix.Validate();
        view.Validate();
        if ((color >> 24) != 0x2E)
        {
            throw new NotSupportedException("Native Cactuar needle requires its actual FT4 color command.");
        }
        NativeBattleActionMatrix rotationView = view with { X = 0, Y = 0, Z = 0 };
        short[] rotation = new short[9];
        for (int column = 0; column < 3; column++)
        {
            NativeBattleVector current = NativeBattleGfMatrices.Transform460840(rotationView, new(nodeMatrix.Rotation[column], nodeMatrix.Rotation[column + 3], nodeMatrix.Rotation[column + 6]));
            rotation[column] = current.X;
            rotation[column + 3] = current.Y;
            rotation[column + 6] = current.Z;
        }
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, position);
        NativeBattleActionMatrix camera = new(rotation.ToImmutableArray(), unchecked(x + view.X), unchecked(y + view.Y), unchecked(z + view.Z), 4096);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int bucket = 0;
        if (Quad([new(-64, 0, 512), new(64, 0, 512), new(-64, 0, 0), new(64, 0, 0)], true))
        {
            Quad([new(0, -64, 512), new(0, 64, 512), new(0, -64, 0), new(0, 64, 0)], false);
        }
        return new(camera, position, new(attempts.ToImmutable(), allocated));

        bool Quad(NativeBattleVector[] corners, bool first)
        {
            uint[] words = [0x09000000, color, 0, 0x3F540000, 0, 0x00B9000F, 0, 0x00008000, 0, 0x0000800F];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            foreach (int word in new[] { 0, 1, 3, 5, 7, 9 })
            {
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, word * 4, 4);
            }
            Array.Fill(mask, NativeBattleGfPacketByteState.Unknown, 30, 2);
            Array.Fill(mask, NativeBattleGfPacketByteState.Unknown, 38, 2);
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
            uint flags = 0;
            for (int index = 0; index < 3; index++)
            {
                projected[index] = NativeBattleModuleProjection.Project45FE10(camera, corners[index], projection);
                flags |= projected[index].Flags;
            }
            if ((flags & 0x60000) != 0)
            {
                Finish(null);
                return false;
            }
            projected[3] = NativeBattleModuleProjection.Project45FE10(camera, corners[3], projection);
            for (int index = 0; index < 4; index++)
            {
                words[2 + index * 2] = projected[index].Screen;
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, (2 + index * 2) * 4, 4);
            }
            if (first)
            {
                uint otz = unchecked((uint)projected.Sum(vertex => (int)vertex.Depth) * 256u) >> 12;
                bucket = 17 + (int)(otz >> 14);
            }
            ImmutableArray<NativeBattleGfPacketByteState> immutableMask = mask.ToImmutableArray();
            Finish(new(bucket, words.ToImmutableArray(), projected.Select(vertex => (int)vertex.Depth).ToImmutableArray()) { PhysicalWriteMask = immutableMask });
            allocated += 40;
            return true;

            void Finish(NativeBattleGeometryPrimitive? primitive)
            {
                byte[] data = new byte[40];
                for (int index = 0; index < words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), words[index]);
                }
                attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            }
        }
    }
}

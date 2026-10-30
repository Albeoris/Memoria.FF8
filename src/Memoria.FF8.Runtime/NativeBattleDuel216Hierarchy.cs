using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216Program(NativeBattleActionMatrix Basis, NativeBattleActionMatrix? Billboard, uint WindowsAddress, short Depth, short VerticalOffset, short Fade, uint MorphAddress);
public sealed record NativeBattleDuel216HierarchyStep(NativeBattleDuel216Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDuel216Hierarchy
{
    public static NativeBattleDuel216HierarchyStep Draw6978F0(NativeBattleDuel216Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleDuel216Program context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0));
        }
        NativeBattleDuel216Resources resources = frame.Memory.Duel216?.Resources ?? throw new NotSupportedException("Native Duel216 hierarchy requires its actual retained resource owner.");
        int geometryIndex = unchecked((short)node.GeometryIndex);
        uint meshAddress = checked(source.Address + resources.Dword(checked(source.Address + 8u + (uint)(geometryIndex * 4))));
        pilotIdentity = pilotIdentity is { } owner ? owner with { ResourceGeneration = unchecked(owner.ResourceGeneration * 397 ^ meshAddress) } : null;
        int count = checked((int)resources.Dword(meshAddress + 4));
        bool morphed = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint selected;
        if (morphed)
        {
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(resources.StaticData, checked((int)(meshAddress - NativeBattleDuel216Resources.Address)), node.Word40, node.Word42, node.Scalar38);
            selected = context.MorphAddress;
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel216 interpolation requires its actual writable vertex owner.");
            for (int index = 0; index < vertices.Length; index++)
            {
                int destination = NativeBattleWaterExecutionFields.ArenaOffset(checked(selected + (uint)(index * 8)), 6);
                arena = arena.Write(destination, BitConverter.GetBytes(vertices[index].X)).Write(destination + 2, BitConverter.GetBytes(vertices[index].Y)).Write(destination + 4, BitConverter.GetBytes(vertices[index].Z));
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(meshAddress + 12u + (uint)(shape * count * 8));
        }
        uint windowAddress = checked(context.WindowsAddress + (uint)geometryIndex);
        byte window = context.WindowsAddress == 0xFE0FC8 ? resources.SingleWindow[geometryIndex] : resources.Read(windowAddress, 1)[0];
        ushort vertical = window is (byte)'1' or (byte)'3' ? (ushort)(unchecked((byte)context.VerticalOffset) & 0x7F) : window == (byte)'2' ? (ushort)(-unchecked((byte)context.VerticalOffset) & 0x7F) : (ushort)0;
        NativeBattleTextureWindow after = window is (byte)'1' or (byte)'2' ? new(0, 0, 256, 128) : window == (byte)'3' ? new(0, 128, 256, 128) : new(0, 0, 256, 256);
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        flags |= (node.Header & 0x2000) != 0 ? 0xCu : 0;
        bool fade = node.Scalar36 != 0;
        if (fade)
        {
            flags |= 0xC0;
        }
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 108);
        uint address = temporary.Address;
        NativeBattleFileStaging staging = temporary.Staging.Write(address, BitConverter.GetBytes(meshAddress)).Write(address + 4, BitConverter.GetBytes(selected)).Write(address + 12, BitConverter.GetBytes((int)node.Scalar36)).Write(address + 28, BitConverter.GetBytes(flags));
        if (fade)
        {
            staging = staging.Write(address + 8, BitConverter.GetBytes(node.Color32));
        }
        staging = staging.Write(address + 24, BitConverter.GetBytes((int)context.Depth)).Write(address + 32, BitConverter.GetBytes((uint)vertical << 16)).Write(address + 36, new byte[4]).Write(address + 40, BitConverter.GetBytes((ushort)256)).Write(address + 42, BitConverter.GetBytes((ushort)256));
        if (window is (byte)'1' or (byte)'2' or (byte)'3')
        {
            staging = staging.Write(address + 44, BitConverter.GetBytes(after.X)).Write(address + 46, BitConverter.GetBytes(after.Y)).Write(address + 48, BitConverter.GetBytes(after.Width)).Write(address + 50, BitConverter.GetBytes(after.Height));
        }
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose6C53A0(node, context.Basis, context.Billboard);
        ImmutableArray<byte> mesh = resources.Read(meshAddress, checked((int)(NativeBattleDuel216Resources.EndAddress - meshAddress)));
        NativeBattleGfGeometryBatch batch;
        if (window == (byte)'9')
        {
            if (fade)
            {
                flags &= ~0xCu;
                staging = staging.Write(address + 28, BitConverter.GetBytes(flags));
            }
            int depth = unchecked(context.Depth + 256);
            staging = staging.Write(address + 24, BitConverter.GetBytes(depth));
            batch = NativeBattleDuel216MeshPackets.EmitBatch697D10(meshAddress, mesh, matrix, projection, flags, fade ? node.Color32 : null, fade ? node.Scalar36 : null, depth, colors, ReadSelected, (offset, value) => staging = staging.Write(checked(address + (uint)offset), BitConverter.GetBytes(value)), pilotIdentity);
        }
        else
        {
            uint far = node.Color32;
            int amount = node.Scalar36;
            if (context.Fade != 0)
            {
                far = 0;
                amount = context.Fade;
                flags |= 0xC0;
                staging = staging.Write(address + 8, new byte[4]).Write(address + 12, BitConverter.GetBytes(amount)).Write(address + 28, BitConverter.GetBytes(flags));
            }
            NativeBattleEsunaMeshContext operands = new(flags, (flags & 0xC0) != 0 ? far : null, (flags & 0xC0) != 0 ? amount : null, context.Depth, 0, vertical, new(0, 0, 256, 256), after, 17, 2);
            NativeBattleWindowedMesh701DD0Result rendered = NativeBattleWindowedMesh701DD0.Emit(staging, address, meshAddress, mesh, matrix, projection, operands, colors, ReadSelected, pilotIdentity: pilotIdentity);
            staging = rendered.Staging;
            batch = rendered.Batch;
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 108);
        return new(frame with { Staging = staging }, batch);

        NativeBattleVector ReadSelected(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset >= count * 8)
            {
                throw new InvalidDataException("Native Duel216 selected vertex leaves its actual shape.");
            }
            byte[] bytes = morphed ? frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(checked(selected + (uint)offset), 6), 6) : resources.Read(checked(selected + (uint)offset), 6).ToArray();
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
    }
}

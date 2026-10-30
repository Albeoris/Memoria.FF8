using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215Program(NativeBattleActionMatrix Basis, NativeBattleVector Anchor, string Windows, short Depth, short VerticalOffset, short Fade, uint MorphAddress);
public sealed record NativeBattleDuel215HierarchyStep(NativeBattleDuel215Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDuel215Hierarchy
{
    public static NativeBattleDuel215HierarchyStep Draw69C220(NativeBattleDuel215Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleDuel215Program context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null) => Draw(frame, source, node, context, projection, colors, false, pilotIdentity);

    public static NativeBattleDuel215HierarchyStep Draw69C6B0(NativeBattleDuel215Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleDuel215Program context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null) => Draw(frame, source, node, context, projection, colors, true, pilotIdentity);

    public static NativeBattleActionMatrix Matrix(NativeBattleGfAnimationNode node, NativeBattleDuel215Program context, bool anchored)
    {
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        bool direct = (node.Header & 0x200) != 0;
        (int x, int y, int z) = direct ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : NativeBattleGfMatrices.Rotate56C3D0(context.Basis, node.Vector8);
        if (!anchored && !direct)
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(context.Basis, rotation);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096));
            }
            else if (anchored)
            {
                short[] scaled = rotation.Rotation.ToArray();
                for (int index = 0; index < scaled.Length; index++)
                {
                    int factor = index < 3 ? scale.X : index < 6 ? scale.Y : scale.Z;
                    scaled[index] = unchecked((short)(unchecked(factor * scaled[index]) >> 12));
                }
                rotation = rotation with { Rotation = scaled.ToImmutableArray() };
            }
            else
            {
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
            }
        }
        if (anchored)
        {
            if (!direct)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(context.Basis, rotation);
            }
            (int anchorX, int anchorY, int anchorZ) = NativeBattleGfMatrices.Rotate56C3D0(context.Basis, context.Anchor);
            x = unchecked(x + anchorX);
            y = unchecked(y + anchorY);
            z = unchecked(z + anchorZ);
        }
        return rotation with { X = unchecked(x + context.Basis.X), Y = unchecked(y + context.Basis.Y), Z = unchecked(z + context.Basis.Z) };
    }

    private static NativeBattleDuel215HierarchyStep Draw(NativeBattleDuel215Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleDuel215Program context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool anchored, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0));
        }
        NativeBattleDuel215Resources resources = frame.Memory.Duel215?.Resources ?? throw new NotSupportedException("Native Duel215 hierarchy requires its entered static resources.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 108);
        frame = frame with { Staging = allocation.Staging };
        int geometryIndex = unchecked((short)node.GeometryIndex);
        uint meshAddress = checked(source.Address + resources.Dword(checked(source.Address + 8u + (uint)(geometryIndex * 4))));
        pilotIdentity = pilotIdentity is { } owner ? owner with { ResourceGeneration = unchecked(owner.ResourceGeneration * 397 ^ meshAddress) } : null;
        ImmutableArray<byte> mesh = resources.Read(meshAddress, checked((int)(NativeBattleDuel215Resources.EndAddress - meshAddress)));
        int count = checked((int)resources.Dword(meshAddress + 4));
        bool arenaSelected = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint selected;
        if (arenaSelected)
        {
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(resources.StaticData, checked((int)(meshAddress - NativeBattleDuel215Resources.Address)), node.Word40, node.Word42, node.Scalar38);
            selected = context.MorphAddress;
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel215 morph requires its actual writable vertex owner.");
            int destination = NativeBattleWaterExecutionFields.ArenaOffset(selected, checked(count * 8));
            for (int index = 0; index < vertices.Length; index++)
            {
                NativeBattleVector vertex = vertices[index];
                arena = arena.Write(destination + index * 8, BitConverter.GetBytes(vertex.X)).Write(destination + index * 8 + 2, BitConverter.GetBytes(vertex.Y)).Write(destination + index * 8 + 4, BitConverter.GetBytes(vertex.Z));
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(meshAddress + 12u + (uint)(shape * count * 8));
        }
        char window = context.Windows[geometryIndex];
        if (window == '9')
        {
            selected = context.MorphAddress;
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel215 collapse requires its writable vertex owner.");
            arena = NativeBattleDuel215Controller.CollapseVertices69C5A0(arena, NativeBattleWaterExecutionFields.ArenaOffset(selected, checked(count * 8)), mesh.AsSpan(), context.VerticalOffset);
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            arenaSelected = true;
        }
        ushort vertical = window is '1' or '3' ? (ushort)(unchecked((byte)context.VerticalOffset) & 0x7F) : window == '2' ? (ushort)(-unchecked((byte)context.VerticalOffset) & 0x7F) : (ushort)0;
        NativeBattleTextureWindow after = window is '1' or '2' ? new(0, 0, 256, 128) : window == '3' ? new(0, 128, 256, 128) : new(0, 0, 256, 256);
        uint flags = node.Scalar36 != 0 ? 0x20F0u : 0x2030u;
        uint farColor = node.Color32;
        int fade = node.Scalar36;
        uint address = allocation.Address;
        NativeBattleFileStaging staging = frame.Staging;
        staging = Dword(Dword(Dword(Dword(staging, address, meshAddress), address + 4, selected), address + 28, flags), address + 12, unchecked((uint)fade));
        if (fade != 0)
        {
            staging = Dword(staging, address + 8, farColor);
        }
        staging = Dword(Dword(Dword(staging, address + 24, unchecked((uint)context.Depth)), address + 32, (uint)vertical << 16), address + 36, 0);
        staging = Word(Word(staging, address + 40, 256), address + 42, 256);
        if (window is '1' or '2' or '3')
        {
            staging = Word(Word(Word(Word(staging, address + 44, after.X), address + 46, after.Y), address + 48, after.Width), address + 50, after.Height);
        }
        if (context.Fade != 0)
        {
            farColor = 0;
            fade = context.Fade;
            flags |= 0xC0;
            staging = Dword(Dword(Dword(staging, address + 8, 0), address + 12, unchecked((uint)fade)), address + 28, flags);
        }
        NativeBattleEsunaMeshContext operands = new(flags, fade != 0 ? farColor : null, fade != 0 ? fade : null, context.Depth, 0, vertical, new(0, 0, 256, 256), after, 17, 2);
        NativeBattleWindowedMesh701DD0Result drawn = NativeBattleWindowedMesh701DD0.Emit(staging, address, meshAddress, mesh, Matrix(node, context, anchored), projection, operands, colors, ReadSelected, pilotIdentity: pilotIdentity);
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 108) }, drawn.Batch);

        NativeBattleVector ReadSelected(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset >= count * 8)
            {
                throw new InvalidDataException("Native Duel215 selected vertex leaves its actual shape.");
            }
            byte[] bytes = arenaSelected ? frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(checked(selected + (uint)offset), 6), 6) : resources.Read(checked(selected + (uint)offset), 6).ToArray();
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
    private static NativeBattleFileStaging Word(NativeBattleFileStaging staging, uint address, int value) => staging.Write(address, BitConverter.GetBytes(unchecked((ushort)value)));
}

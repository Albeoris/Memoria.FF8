using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One callback's byte-faithful view of the two original cleared auxiliary ranges; unknown bytes stay unknown until a native store.</summary>
internal sealed record NativeBattleEffectNodeMemoryWrite(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

internal partial class NativeBattleEffectNodeMemory
{
    internal const uint ArenaAddress = 0x20DFAB8;
    internal const int ContextOffset = 0x34;
    private NativeBattleEffectMemory memory;
    private readonly NativeBattleOriginalEffectState effect;
    private readonly uint taskAddress;
    private readonly NativeBattleModulePoolTask originalTask;
    private readonly int first;
    private readonly byte[] bytes;
    private readonly bool[] unknown;
    private readonly byte[] taskBytes;
    private bool dirty;
    private NativeBattleEffectNodeScratch? scratch;

    internal NativeBattleEffectNodeMemory(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, uint taskAddress, int first, int emitterBytes, int second, int geometryBytes, bool useSharedScratch = false)
    {
        this.memory = memory;
        scratch = memory.NodeScratch2793E30;
        UseSharedScratch = useSharedScratch;
        this.effect = effect;
        this.taskAddress = taskAddress;
        this.first = first;
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        originalTask = hierarchy.Read(taskAddress);
        if (originalTask.PayloadLength != 676 - 12)
        {
            throw new InvalidDataException("Native effect node context requires its original676-byte task.");
        }
        taskBytes = originalTask.ReadRawPayloadSpan().ToArray();
        if (emitterBytes <= 0 || geometryBytes <= 0 || second != first + emitterBytes || first < 0 || second > NativeBattleEffectArena.Length - geometryBytes)
        {
            throw new InvalidDataException("Native effect node auxiliary ranges contradict their original contiguous allocations.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native effect nodes require the571870 arena.");
        arena.Validate();
        bytes = new byte[checked(emitterBytes + geometryBytes)];
        unknown = new bool[bytes.Length];
        arena.ReadWithKnownness(first, bytes, unknown);
        for (int index = 0; index < unknown.Length; index++)
        {
            unknown[index] = !unknown[index];
        }
    }

    internal byte Byte(int offset) => Read(offset, 1)[0];
    internal NativeBattleEffectMemory CaptureProcessMemory() => memory with { NodeScratch2793E30 = scratch };

    internal void PublishProcessMemory(NativeBattleEffectMemory updated)
    {
        memory = updated;
        scratch = updated.NodeScratch2793E30;
    }

    internal ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Read(offset, 2));
    internal uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Read(offset, 4));
    internal ushort ContextWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12));
    internal uint ContextDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12));
    internal void ContextWord(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12), value);
    internal void ContextDword(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(taskBytes.AsSpan(ContextOffset + offset - 12), value);

    internal void Byte(int offset, byte value) => Write(offset, 1)[0] = value;
    internal void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Write(offset, 2), value);
    internal void Dword(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(Write(offset, 4), value);
    internal void Clear(int offset, int count) => Write(offset, count).Clear();
    internal bool UseSharedScratch { get; }
    internal NativeBattleTheEnd85SpawnScratchObserver? SpawnScratchObserver { get; init; }
    internal NativeBattleVector ReadPlacementEuler() => (scratch ?? NativeBattleEffectNodeScratch.Unknown()).ReadEuler();
    internal void WritePlacementEuler(NativeBattleVector angles)
    {
        if (UseSharedScratch)
        {
            scratch = (scratch ?? NativeBattleEffectNodeScratch.Unknown()).WritePlacement(angles);
        }
    }
    internal void WriteProjectionFlags(uint flags)
    {
        if (UseSharedScratch)
        {
            scratch = (scratch ?? NativeBattleEffectNodeScratch.Unknown()).WriteFlags(flags);
        }
    }
    internal void WriteQuadMatrix(NativeBattleActionMatrix matrix)
    {
        if (UseSharedScratch)
        {
            scratch = (scratch ?? NativeBattleEffectNodeScratch.Unknown()).WriteQuadMatrix(matrix);
        }
    }

    internal void WriteScratchPrefix(int offset, ReadOnlySpan<byte> bytes)
    {
        if (!UseSharedScratch)
        {
            throw new InvalidOperationException("Native scratch publication requires its shared physical owner.");
        }
        scratch = (scratch ?? NativeBattleEffectNodeScratch.Unknown()).WritePrefix(offset, bytes);
    }

    internal byte[] ReadScratchPrefix(int offset, int count) => (scratch ?? NativeBattleEffectNodeScratch.Unknown()).ReadPrefix(offset, count);

    internal void WriteSpriteMatrix(NativeBattleActionMatrix matrix)
    {
        if (UseSharedScratch)
        {
            scratch = (scratch ?? NativeBattleEffectNodeScratch.Unknown()).WriteSpriteMatrix(matrix);
            byte[] rotation = new byte[8];
            for (int index = 0; index < 4; index++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), matrix.Rotation[index]);
            }
            scratch = scratch.WritePrefix(32, rotation);
        }
    }

    internal INativeBattleCuragaSpriteGeometry ProjectedSpriteGeometry(int node, uint resourceAddress, ImmutableArray<byte> resource, ushort phase, NativeBattleCameraProjection projection)
    {
        int offset = node + 0xAC + 18 - first;
        short? padding = unknown.AsSpan(offset, 2).Contains(true) ? null : BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
        return new ProjectedSpriteRegisters(this, resourceAddress, resource, phase, projection, padding);
    }

    private sealed class ProjectedSpriteRegisters(NativeBattleEffectNodeMemory owner, uint resourceAddress, ImmutableArray<byte> resource, ushort phase, NativeBattleCameraProjection projection, short? sourcePadding) : INativeBattleCuragaSpriteGeometry
    {
        private NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 180);
        private NativeBattleGeometryRegisterStep registers = null!;
        private NativeBattleScriptedGraphicsMemory controls = null!;
        private NativeBattleActionMatrix main = null!;

        public void Begin(NativeBattleActionMatrix source, NativeBattleActionMatrix rolled)
        {
            NativeBattleEffectMemory memory = owner.CaptureProcessMemory();
            controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native projected sprites require their actual projection controls.");
            registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native projected sprites require their canonical geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native projected sprites require their canonical data registers."));
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, source, true, true, sourcePadding) };
            context = NativeBattleEffectNodeContextMemory.Capture(memory);
            context = Dword(context, 0, resourceAddress);
            context = Word(context, 4, phase);
            context = Word(context, 0x24, 0);
            context = Word(context, 0x26, BinaryPrimitives.ReadUInt16LittleEndian(resource.AsSpan().Slice(8)));
            int frame = BinaryPrimitives.ReadUInt16LittleEndian(resource.AsSpan().Slice(10 + phase * 2));
            context = Word(context, 0x28, BinaryPrimitives.ReadUInt16LittleEndian(resource.AsSpan().Slice(12 + phase * 2)));
            uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(resource.AsSpan().Slice(frame));
            context = Dword(context, 0x2C, checked(resourceAddress + (uint)frame + 4));
            context = Dword(context, 0x30, descriptor & 0x7FFFFFFF);
            context = Word(context, 0x24, (descriptor & 0x80000000) == 0 ? (ushort)0 : (ushort)0x100);
            for (int index = 0; index < 4; index++)
            {
                context = Word(context, 0x48 + index * 8, 0);
            }
            main = rolled;
            context = Matrix(context, 0x84, rolled).MarkUnknown(0x96, 2);
            context = Dword(context, 0x1C, 0x808080);
            context = context.Write(0x20, resource.AsSpan().Slice(4, 4));
            context = Dword(Dword(Dword(Dword(Dword(context, 0xB0, 0), 0xAC, 0), 0xA4, 4096), 0xA8, 0), 0x3C, uint.MaxValue);
            Publish();
        }

        public NativeBattleCuragaSpriteTriple ProjectQuad(ReadOnlySpan<byte> quad)
        {
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(NativeBattleCuragaMatrices.Identity8DD770(), quad);
            for (int index = 0; index < 9; index++)
            {
                context = Word(context, 0x64 + index * 2, unchecked((ushort)local.Rotation[index]));
            }
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(14));
            if ((flags & 0xC00) != 0)
            {
                short angle = BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(12));
                if (angle == 0 || unchecked((int)ReadDword(0xAC)) != angle)
                {
                    context = Dword(Dword(Dword(context, 0xA4, unchecked((uint)NativeBattleCameraMath.Cosine(angle))), 0xA8, unchecked((uint)NativeBattleCameraMath.Sine(angle))), 0xAC, unchecked((uint)(int)angle));
                }
            }
            short width = (short)(8 * quad[0]);
            short height = (short)(8 * quad[2]);
            NativeBattleVector[] corners = [new(unchecked((short)-width), unchecked((short)-height), 0), new(width, unchecked((short)-height), 0), new(unchecked((short)-width), height, 0), new(width, height, 0)];
            for (int index = 0; index < 4; index++)
            {
                context = Word(Word(context, 0x44 + index * 8, unchecked((ushort)corners[index].X)), 0x46 + index * 8, unchecked((ushort)corners[index].Y));
            }
            int dx = unchecked(8 * quad[0] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(8)));
            int dy = unchecked(8 * quad[2] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(10)));
            context = Dword(Dword(Dword(context, 0x78, unchecked((uint)dx)), 0x7C, unchecked((uint)dy)), 0x80, 0);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, main, true, false, null) };
            for (int column = 0; column < 3; column++)
            {
                registers = NativeBattleGeometry.LoadIrColumn(registers, ReadWord(0x64 + column * 2), ReadWord(0x6A + column * 2), ReadWord(0x70 + column * 2));
                registers = NativeBattleGeometry.Transform(registers, false, true);
                for (int row = 0; row < 3; row++)
                {
                    context = Word(context, 0x64 + row * 6 + column * 2, unchecked((ushort)registers.Colors.Ir[row]!.Value));
                }
            }
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, main, false, true, null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)dx), unchecked((short)dy), 0), 0);
            registers = NativeBattleGeometry.Transform(registers);
            for (int axis = 0; axis < 3; axis++)
            {
                context = Dword(context, 0x78 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            }
            NativeBattleActionMatrix current = new(Enumerable.Range(0, 9).Select(index => unchecked((short)ReadWord(0x64 + index * 2))).ToImmutableArray(), unchecked((int)ReadDword(0x78)), unchecked((int)ReadDword(0x7C)), unchecked((int)ReadDword(0x80)), 4096);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, current, true, true, Padding(0x76)) };
            for (int index = 0; index < 3; index++)
            {
                registers = NativeBattleGeometry.LoadVector(registers, index, corners[index], Padding(0x4A + index * 8));
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint projectedFlags = registers.Colors.Flags!.Value;
            context = Dword(context, 0x40, projectedFlags);
            ImmutableArray<NativeBattleModuleProjectedVertex> projected = Enumerable.Range(0, 3).Select(index => new NativeBattleModuleProjectedVertex(registers.Geometry.Sxy[index]!.Value, unchecked((ushort)registers.Geometry.Sz[index + 1]!.Value), projectedFlags)).ToImmutableArray();
            Publish();
            return new(projected, projectedFlags);
        }

        public NativeBattleModuleProjectedVertex ProjectFourth()
        {
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)ReadWord(0x5C)), unchecked((short)ReadWord(0x5E)), unchecked((short)ReadWord(0x60))), Padding(0x62));
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            NativeBattleModuleProjectedVertex projected = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
            Publish();
            return projected;
        }

        public int AverageDepth()
        {
            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Native projected sprites require their actual four-depth scale."));
            int depth = unchecked((int)registers.Geometry.Otz!.Value);
            context = Dword(context, 0x3C, unchecked((uint)depth));
            Publish();
            return depth;
        }

        private void Publish()
        {
            NativeBattleEffectMemory memory = owner.CaptureProcessMemory() with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
            owner.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(memory, context));
        }

        private ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.Read(offset, 2));
        private uint ReadDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
        private short? Padding(int offset)
        {
            (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 2);
            return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        }
        private static NativeBattleEffectArena Matrix(NativeBattleEffectArena view, int offset, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                view = Word(view, offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
            return Dword(Dword(Dword(view, offset + 20, unchecked((uint)matrix.X)), offset + 24, unchecked((uint)matrix.Y)), offset + 28, unchecked((uint)matrix.Z));
        }
        private static NativeBattleEffectArena Word(NativeBattleEffectArena view, int offset, ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            return view.Write(offset, bytes);
        }
        private static NativeBattleEffectArena Dword(NativeBattleEffectArena view, int offset, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            return view.Write(offset, bytes);
        }
    }

    internal void MarkUnknown(int offset, int count)
    {
        Require(offset, count);
        unknown.AsSpan(offset - first, count).Fill(true);
        dirty |= count != 0;
    }

    internal void Copy(int source, int destination, int count)
    {
        Require(source, count);
        Require(destination, count);
        if ((count & 3) != 0)
        {
            throw new InvalidDataException("Native effect forward copy requires its original DWORD count.");
        }
        Span<byte> word = stackalloc byte[4];
        Span<bool> mask = stackalloc bool[4];
        for (int offset = 0; offset < count; offset += 4)
        {
            //A native memcpy transfers unknown bytes too; their values are required only if a later operation actually reads them.
            bytes.AsSpan(source - first + offset, 4).CopyTo(word);
            unknown.AsSpan(source - first + offset, 4).CopyTo(mask);
            word.CopyTo(bytes.AsSpan(destination - first + offset, 4));
            mask.CopyTo(unknown.AsSpan(destination - first + offset, 4));
        }
        dirty |= count != 0;
    }

    internal uint Pointer(int offset)
    {
        Require(offset, 1);
        return checked(ArenaAddress + (uint)offset);
    }

    internal int Offset(uint pointer)
    {
        if (pointer < ArenaAddress)
        {
            throw new InvalidDataException("Native effect node link is not an effect-arena pointer.");
        }
        int offset = checked((int)(pointer - ArenaAddress));
        Require(offset, 1);
        return offset;
    }

    internal void Link882930(int node, ushort kind)
    {
        Word(node + 8, kind);
        uint pointer = Pointer(node);
        if (ContextDword(0x2C) == 0)
        {
            ContextDword(0x2C, pointer);
            ContextDword(0x30, pointer);
        }
        else
        {
            uint previous = ContextDword(0x30);
            Dword(node, previous);
            Dword(Offset(previous) + 4, pointer);
            ContextDword(0x30, pointer);
        }
    }

    internal void Unlink882960(int node)
    {
        uint previous = Dword(node);
        uint next = Dword(node + 4);
        if (previous == 0)
        {
            ContextDword(0x2C, next);
        }
        else
        {
            Dword(Offset(previous) + 4, next);
        }
        if (next == 0)
        {
            ContextDword(0x30, previous);
        }
        else
        {
            Dword(Offset(next), previous);
        }
    }

    internal NativeBattleEffectNodeMemoryWrite FlushStorage()
    {
        NativeBattleEffectArena arena = memory.Arena!;
        if (dirty)
        {
            //One committed range preserves untouched values and their original unknown masks, avoiding a full sparse-array clone per DWORD store.
            arena = arena.Write(first, bytes);
            for (int index = 0; index < unknown.Length;)
            {
                if (!unknown[index])
                {
                    index++;
                    continue;
                }
                int end = index + 1;
                while (end < unknown.Length && unknown[end])
                {
                    end++;
                }
                arena = arena.MarkUnknown(first + index, end - index);
                index = end;
            }
        }
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        hierarchy = hierarchy.Write(taskAddress, originalTask with { Payload = taskBytes.ToImmutableArray() });
        return new(memory with { Arena = arena, NodeScratch2793E30 = scratch }, effect with { Pools = hierarchy.Pools });
    }

    private ReadOnlySpan<byte> Read(int offset, int count)
    {
        Require(offset, count);
        int index = offset - first;
        if (unknown.AsSpan(index, count).Contains(true))
        {
            throw new NotSupportedException("Native effect node read requires its actual prior bytes at the selected pointer.");
        }
        return bytes.AsSpan(index, count);
    }

    private Span<byte> Write(int offset, int count)
    {
        Require(offset, count);
        int index = offset - first;
        unknown.AsSpan(index, count).Clear();
        dirty = true;
        return bytes.AsSpan(index, count);
    }

    private void Require(int offset, int count)
    {
        if (count < 0 || offset < first || offset - first > bytes.Length - count)
        {
            throw new InvalidDataException("Native effect node access leaves its two original auxiliary ranges.");
        }
    }
}

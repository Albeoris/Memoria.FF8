using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal int Spawn(int emitter, int instanceCount, byte[] source, ref NativeCrtRandomState random)
    {
        sbyte sourceIndex = unchecked((sbyte)memory.Byte(emitter + 0x6A));
        int node = inputs.AllocateGeometry(emitter, sourceIndex) ?? throw new InvalidDataException("Native8818A0 dereferences the exhausted882080 geometry allocation.");
        memory.Byte(node + 0x1D8, unchecked((byte)instanceCount));
        int frame = unchecked((short)memory.Word(emitter + 0x60));
        memory.SpawnScratchObserver?.Begin(frame);
        if (source[0x16] is 0 or 1 or 2)
        {
            uint resource = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(unchecked(memory.ContextDword(0x228) + (uint)(sourceIndex * 4)), 4));
            memory.Dword(node + 0x170, resource);
            memory.Byte(node + 0x1D1, unchecked((byte)(BinaryPrimitives.ReadUInt16LittleEndian(bank.Read(unchecked(resource + 8), 2)) - 1)));
            if (source[0x16] == 2)
            {
                memory.Byte(node + 0x1D3, source[0x17]);
                memory.Byte(node + 0x1D4, source[0x18]);
                NativeBattleEffectNodeRandomValue draw = NativeBattleEffectNodeRandom.Range881F20(random, unchecked((sbyte)source[0x19]), unchecked((sbyte)source[0x1A]));
                random = draw.Random;
                memory.Byte(node + 0x1D5, unchecked((byte)draw.Value));
            }
        }
        memory.Word(node + 0x1DA, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(0x64)));
        for (int instance = 0; instance < instanceCount; instance++)
        {
            int local = node + 0x12C + instance * 16;
            if (inputs.SpawnOrigin is not null)
            {
                NativeBattleVector origin = inputs.SpawnOrigin(ref random);
                memory.Dword(emitter + 0x4C, unchecked((uint)(origin.X << 16)));
                memory.Dword(emitter + 0x50, unchecked((uint)(origin.Y << 16)));
                memory.Dword(emitter + 0x54, unchecked((uint)(origin.Z << 16)));
            }
            memory.Copy(emitter + 0x4C, node + 0xDC + instance * 16, 16);
            if (source[0x22] == 0)
            {
                memory.Copy(emitter + 0x4C, local, 16);
            }
            NativeBattleVector? placementEuler = null;
            if (source[0x34] is 1 or 2)
            {
                short rx = DrawAngle(ref random);
                short ry = source[0x34] == 1 ? DrawAngle(ref random) : (short)0;
                short rz = source[0x34] == 1 ? DrawAngle(ref random) : (short)1024;
                placementEuler = new(rx, ry, rz);
                memory.WritePlacementEuler(placementEuler.Value);
                short[] radius = new short[3];
                for (int axis = 0; axis < 3; axis++)
                {
                    uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0xCC + axis * 4));
                    radius[axis] = BinaryPrimitives.ReadInt16LittleEndian(bank.Read(unchecked(channel + (uint)(frame * 2)), 2));
                    bool sample = source[0x34] == 1 ? source[0x33] == 0 : source[0x33] != 1 || axis == 1;
                    if (sample && radius[axis] != 0)
                    {
                        random = random.Draw(out ushort value);
                        radius[axis] = unchecked((short)(value % radius[axis]));
                    }
                }
                if (source[0x34] == 2)
                {
                    random = random.Draw(out ushort sign);
                    if ((sign & 1) != 0)
                    {
                        radius[1] = unchecked((short)-radius[1]);
                    }
                }
                NativeBattleActionMatrix spread = NativeBattleCuragaMatrices.Euler8818A0(placementEuler.Value);
                if (inputs.ActorScale is { } actorScale)
                {
                    spread = NativeBattlePoseMatrices.Scale56BEF0(spread, actorScale.X, actorScale.Y, actorScale.Z);
                }
                NativeBattleVector unit = NativeBattleCuragaMatrices.RotateVector45DF80(spread, new(0, -4096, 0));
                memory.SpawnScratchObserver?.Placement(placementEuler.Value, spread, radius, unit, source[0x34]);
                Add(local, unchecked(radius[0] * unit.X << 4));
                Add(local + 4, source[0x34] == 1 ? unchecked(radius[1] * unit.Y << 4) : radius[1] << 16);
                Add(local + 8, unchecked(radius[2] * unit.Z << 4));
            }
            NativeBattleVector euler;
            switch (unchecked((sbyte)source[0x21]))
            {
                case 0:
                    NativeBattleEffectNodeRandomVector draw = NativeBattleEffectNodeRandom.Euler881FE0(random, Vector(0x3C), Vector(0x44));
                    random = draw.Random;
                    euler = draw.Value;
                    memory.SpawnScratchObserver?.RandomEuler(euler);
                    break;
                case 1:
                    euler = memory.UseSharedScratch ? memory.ReadPlacementEuler() : placementEuler ?? throw new NotSupportedException("Native node placement selector1 requires its caller's retained Euler scratch owner.");
                    memory.SpawnScratchObserver?.CopyPlacementEuler();
                    break;
                default:
                    throw new NotSupportedException("Native8818A0 requires the actual unwritten Euler scratch for its out-of-range selector.");
            }
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.MultiplyGteRotation45E470(ReadMatrix(emitter + 0x2C), NativeBattleCuragaMatrices.Euler8818A0(euler));
            WriteMatrix(node + 0xC + instance * 32, matrix with { X = 0, Y = 0, Z = 0 });
            if (source[0x27] == 0)
            {
                memory.Dword(node + 0x174 + instance * 4, unchecked((uint)Product(0x4C, ref random)));
                memory.Dword(node + 0x184 + instance * 4, unchecked((uint)Product(0x54, ref random)));
            }
        }
        NativeBattleEffectNodeRandomVector rotation = NativeBattleEffectNodeRandom.Euler881FE0(random, Vector(0x148), Vector(0x150));
        random = rotation.Random;
        memory.Word(node + 0x194, unchecked((ushort)rotation.Value.X));
        memory.Word(node + 0x196, unchecked((ushort)rotation.Value.Y));
        memory.Word(node + 0x198, unchecked((ushort)rotation.Value.Z));
        if (source[0x1E] is 1 or 2)
        {
            NativeBattleEffectNodeRandomValue life = NativeBattleEffectNodeRandom.Range881F20(random, unchecked((sbyte)source[0x1F]), unchecked((sbyte)source[0x20]));
            random = life.Random;
            memory.Word(node + 0x1C8, unchecked((ushort)life.Value));
        }
        if (source[0x27] == 0)
        {
            memory.Dword(node + 0x1DC, unchecked((uint)Product(0x5C, ref random)));
            for (int instance = 0; instance < instanceCount; instance++)
            {
                for (int vector = 0; vector < 3; vector++)
                {
                    int sourceOffset = 0x68 + vector * 32;
                    int output = node + 0x1E0 + vector * 64 + instance * 16;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        NativeBattleEffectNodeRandomValue draw = NativeBattleEffectNodeRandom.ProductRange881F50(random, BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(sourceOffset + axis * 4)), BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(sourceOffset + 16 + axis * 4)));
                        random = draw.Random;
                        memory.Dword(output + axis * 4, unchecked((uint)draw.Value));
                    }
                }
            }
        }
        AppendChildren(node, source);
        return node;

        void Add(int offset, int value) => memory.Dword(offset, unchecked(memory.Dword(offset) + (uint)value));
        NativeBattleVector Vector(int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset + 4)));
        int Product(int offset, ref NativeCrtRandomState state)
        {
            NativeBattleEffectNodeRandomValue draw = NativeBattleEffectNodeRandom.ProductRange881F50(state, BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset)), BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset + 4)));
            state = draw.Random;
            return draw.Value;
        }
    }

    private short DrawAngle(ref NativeCrtRandomState random)
    {
        random = random.Draw(out ushort value);
        return (short)(value & 0xFFF);
    }
}

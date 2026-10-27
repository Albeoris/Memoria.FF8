using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectNodeLifecycle
{
    internal NativeBattleEffectNodeBank AdvanceDrawable(int node, byte[] source)
    {
        int counter = unchecked((short)memory.Word(node + 0x1CA));
        int kind = unchecked((sbyte)source[0x16]);
        if (kind is 4 or 5)
        {
            byte model = ByteCurve(0x12C);
            uint resource = Pointer(memory.ContextDword(0x22C), model);
            memory.Dword(node + 0x170, resource);
            if (kind == 5)
            {
                uint firstShape = Pointer(memory.ContextDword(0x230), ByteCurve(0x130));
                uint secondShape = Pointer(memory.ContextDword(0x230), ByteCurve(0x134));
                if (inputs.SelectedMorph is { } selectedMorph)
                {
                    selectedMorph(resource, firstShape, secondShape, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0x138)));
                }
                else
                {
                    short ratio = WordCurve(0x138);
                    if (inputs.MorphResource is { } morph)
                    {
                        morph(resource, firstShape, secondShape, ratio);
                    }
                    else
                    {
                        Morph881540(resource, firstShape, secondShape, ratio);
                    }
                }
            }
            if (inputs.MutateResource is { } mutate)
            {
                mutate(bank, resource, kind, memory.Byte(node + 0x1D6));
            }
            if (inputs.ResourceUpdate is { } update)
            {
                bank.ReplaceData(update(bank, resource, kind, memory.Byte(node + 0x1D6)));
            }
        }
        short ax = unchecked((short)memory.Word(node + 0xCC));
        short ay = unchecked((short)memory.Word(node + 0xCE));
        short az = unchecked((short)memory.Word(node + 0xD0));
        int basis = node + 0x8C;
        switch (unchecked((sbyte)source[0x1B]))
        {
            case 0:
                WriteRotation(basis, TransposeCamera());
                if (az != 0)
                {
                    WriteRotation(basis, NativeBattleCuragaMatrices.RotateZ8DD960(RotationAt(basis), az));
                }
                break;
            case 1:
                WriteMatrix(basis, NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.Identity8DD770(), 1024));
                break;
            case 2:
                NativeBattleActionMatrix camera = Camera();
                short yaw = unchecked((short)((NativeWorldScriptedPathMath.NativeAngle(camera.Rotation[8], camera.Rotation[2]) + 1024) & 0xFFF));
                WriteMatrix(basis, NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw));
                break;
            case 3:
            case 4:
                int order = unchecked((sbyte)source[0x1D]);
                if (order is >= 0 and <= 3)
                {
                    if (order < 2)
                    {
                        uint parent = memory.Dword(node + 0x1BC);
                        if (parent == 0)
                        {
                            WriteMatrix(basis, NativeBattleCuragaMatrices.Identity8DD770());
                        }
                        else
                        {
                            memory.Copy(memory.Offset(parent) + 0x2C, basis, 32);
                        }
                    }
                    else
                    {
                        WriteRotation(basis, TransposeCamera());
                    }
                    NativeBattleActionMatrix rotation = (order & 1) == 0 ? Rotate(RotationAt(basis), az, ax, ay, false) : Rotate(RotationAt(basis), ay, ax, az, true);
                    WriteRotation(basis, rotation);
                }
                break;
        }
        if (kind is 4 or 5)
        {
            for (int component = 0; component < 3; component++)
            {
                memory.Byte(node + 0x16C + component, ByteCurve(0xF0 + component * 4));
            }
            memory.Word(node + 0x1CE, unchecked((ushort)WordCurve(0xFC)));
            for (int component = 0; component < 3; component++)
            {
                memory.Word(node + 0xD4 + component * 2, unchecked((ushort)WordCurve(0xE4 + component * 4)));
            }
            for (int index = 0; index < 9; index++)
            {
                short value = unchecked((short)memory.Word(basis + index * 2));
                short scale = unchecked((short)memory.Word(node + 0xD4 + index % 3 * 2));
                memory.Word(basis + index * 2, unchecked((ushort)(value * scale >> 12)));
            }
        }
        int count = unchecked((sbyte)memory.Byte(node + 0x1D8));
        if (count == 1)
        {
            Compose(0);
            memory.Dword(node + 0xC8, unchecked(memory.Dword(node + 0xC8) + (uint)(int)BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x38))));
        }
        else
        {
            for (int instance = 0; instance < count; instance++)
            {
                Compose(instance);
                memory.Word(node + 0x19C + instance * 8, unchecked((ushort)memory.Dword(node + 0xC0)));
                memory.Word(node + 0x19E + instance * 8, unchecked((ushort)memory.Dword(node + 0xC4)));
                memory.Word(node + 0x1A0 + instance * 8, unchecked((ushort)(memory.Dword(node + 0xC8) + (uint)(int)BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x38)))));
            }
        }
        if (inputs.ActorScale is { } actorScale)
        {
            WriteRotation(node + 0xAC, NativeBattlePoseMatrices.Scale56BEF0(RotationAt(node + 0xAC), actorScale.X, actorScale.Y, actorScale.Z));
        }
        return bank;

        byte ByteCurve(int field)
        {
            uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(field));
            return bank.Read(unchecked(channel + (uint)counter), 1)[0];
        }
        short WordCurve(int field)
        {
            uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(field));
            return BinaryPrimitives.ReadInt16LittleEndian(bank.Read(unchecked(channel + (uint)(counter * 2)), 2));
        }
        uint Pointer(uint directory, byte index) => BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(unchecked(directory + (uint)index * 4), 4));
        NativeBattleActionMatrix Camera() => inputs.CopiedCamera ?? throw new NotSupportedException("Native880DB0 requires87DC50's actual copied camera matrix.");
        NativeBattleActionMatrix TransposeCamera()
        {
            NativeBattleActionMatrix camera = Camera();
            camera.Validate();
            return camera with { Rotation = Enumerable.Range(0, 9).Select(index => camera.Rotation[index % 3 * 3 + index / 3]).ToImmutableArray() };
        }
        NativeBattleActionMatrix RotationAt(int offset)
        {
            ImmutableArray<short>.Builder words = ImmutableArray.CreateBuilder<short>(9);
            for (int index = 0; index < 9; index++)
            {
                words.Add(unchecked((short)memory.Word(offset + index * 2)));
            }
            return new(words.MoveToImmutable(), 0, 0, 0, 4096); //Only the three rotation rows are consumed by these GTE operations.
        }
        void WriteRotation(int offset, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                memory.Word(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
        }
        void Compose(int instance)
        {
            int[] point = new int[3];
            for (int component = 0; component < 3; component++)
            {
                point[component] = unchecked((int)memory.Dword(node + 0xDC + instance * 16 + component * 4)) / 65536;
                memory.Dword(node + 0xA0 + component * 4, unchecked((uint)point[component]));
            }
            NativeBattleActionMatrix camera = Camera();
            WriteRotation(node + 0xAC, NativeBattlePoseMatrices.MultiplyGteRotation45E470(camera, RotationAt(basis)));
            //460860 rounds the rotation dot product before adding DWORD translation;45E450 stores MAC, not saturated IR.
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(camera, unchecked((short)point[0]), unchecked((short)point[1]), unchecked((short)point[2]));
            memory.Dword(node + 0xC0, unchecked((uint)(x + camera.X)));
            memory.Dword(node + 0xC4, unchecked((uint)(y + camera.Y)));
            memory.Dword(node + 0xC8, unchecked((uint)(z + camera.Z)));
        }
    }

    private void Morph881540(uint mesh, uint firstShape, uint secondShape, short ratio)
    {
        if (firstShape == 0 || secondShape == 0)
        {
            return;
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(bank.Read(unchecked(mesh + 4), 4));
        if (count <= 0)
        {
            return;
        }
        int target = Offset(mesh);
        int first = Offset(firstShape);
        int second = Offset(secondShape);
        if (Overlaps(first) || Overlaps(second))
        {
            throw new NotSupportedException("Native881540 shifted overlapping shape/destination aliases require their original forward read/write owner.");
        }
        if (bank.IsWorking)
        {
            using NativeBattleGfSourceReader source = bank.OpenWholeReader();
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices45E9D0(source, first, source, second, count, ratio);
            foreach (NativeBattleVector vertex in vertices)
            {
                bank.WriteUInt16(target, unchecked((ushort)vertex.X));
                bank.WriteUInt16(target + 2, unchecked((ushort)vertex.Y));
                bank.WriteUInt16(target + 4, unchecked((ushort)vertex.Z));
                target = unchecked(target + 8);
            }
        }
        else
        {
            ImmutableArray<byte> data = bank.Data;
            bank.ReplaceData(NativeBattleGfMorphGeometry.Morph45E9D0(data, first, data, second, count, ratio, data, target));
        }

        int Offset(uint pointer)
        {
            bank.Read(pointer, 8);
            return checked((int)(pointer - bank.Address + 8));
        }
        bool Overlaps(int shape) => shape != target && shape < target + (long)count * 8 && target < shape + (long)count * 8;
    }
}

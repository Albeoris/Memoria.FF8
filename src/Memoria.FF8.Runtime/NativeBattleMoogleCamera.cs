using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMoogleCameraSource(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleMoogleCameraResources(ImmutableArray<NativeBattleMoogleCameraSource> Sources);
public sealed record NativeBattleMoogleCameraCurve(uint Address, ImmutableArray<short> Values);
public sealed record NativeBattleMoogleCameraState([property: JsonRequired] ImmutableArray<byte> Data, [property: JsonRequired] bool EyePaddingKnown)
{
    public void Validate()
    {
        if (Data.IsDefault || Data.Length != 0xEC)
        {
            throw new InvalidDataException("Native Moogle camera requires its complete retained EC-byte owner.");
        }
    }
}
public sealed record NativeBattleMoogleCameraStep(NativeBattleMoogleCameraState State, bool Complete, NativeBattleCameraPose Pose);

/// <summary>732B10/732E00's bounded orbit source and retained scalar interpolation over the existing camera globals.</summary>
public static class NativeBattleMoogleCamera
{
    public static NativeBattleMoogleCameraState Reset732AF0() => new(new byte[0xEC].ToImmutableArray(), true);

    public static NativeBattleMoogleCameraStep Initialize732B10(NativeBattleMoogleCameraState state, NativeBattleMoogleCameraSource source, ImmutableArray<byte> anchor, int continuation, NativeBattleMoogleCameraCurve? curve = null)
    {
        state.Validate();
        if (source.Address == 0 || source.Data.IsDefault || source.Data.Length != 0x48 || anchor.IsDefault || anchor.Length != 8)
        {
            throw new InvalidDataException("Native Moogle camera requires its actual72-byte source and raw8-byte anchor.");
        }
        if (continuation is not (0 or 1) || Word(source.Data, 0x24) is not (0 or 1))
        {
            throw new NotSupportedException("Native Moogle camera supports only the reached anchor modes0/1 and continuation0/1.");
        }
        byte[] data = state.Data.ToArray();
        NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Word(data, 0xE8));
        Dword(data, 0xAC, source.Address);
        Set(data, 0xE2, 0);
        Set(data, 0xD0, 0);
        Dword(data, 0x50, U32(source.Data, 0));
        anchor.AsSpan().CopyTo(data.AsSpan(0xA4));
        int[] starts = [0xC2, 0xC6, 0xCA, 0xBA, 0xBE];
        int[] sourceStarts = [0x2A, 0x2E, 0x32, 0x36, 0x26];
        int[] previous = [0xD8, 0xDA, 0xDC, 0xE0, 0xDE];
        int[] ends = [0xC4, 0xC8, 0xCC, 0xBC, 0xC0];
        for (int index = 0; index < starts.Length; index++)
        {
            Set(data, starts[index], continuation == 0 ? Word(source.Data, sourceStarts[index]) : Word(data, previous[index]));
            Set(data, ends[index], Word(source.Data, sourceStarts[index] + 2));
        }
        foreach ((int destination, int origin) in new (int, int)[] { (0xCE, 0x24), (0xD2, 0x3A), (0xD4, 0x3C), (0xD6, 0x3E), (0xB4, 0x40), (0xB6, 0x42), (0xB8, 0x44) })
        {
            Set(data, destination, Word(source.Data, origin));
        }
        Set(data, 0xC6, unchecked(Word(data, 0xC6) + Word(data, 0xE8)) & 4095);
        Set(data, 0xC8, unchecked(Word(data, 0xC8) + Word(data, 0xE8)) & 4095);
        for (int end = 0; end < 2; end++)
        {
            bool mode1 = Word(source.Data, 0x24) == 1;
            int destination = (mode1 ? 0x74 : 0x64) + end * 8;
            anchor.AsSpan().CopyTo(data.AsSpan(destination));
            int origin = (mode1 ? 0x14 : 4) + end * 8;
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(Word(source.Data, origin), Word(source.Data, origin + 2), Word(source.Data, origin + 4)));
            Set(data, destination, Word(data, destination) + rotated.X);
            Set(data, destination + 2, Word(data, destination + 2) + rotated.Y);
            Set(data, destination + 4, Word(data, destination + 4) + rotated.Z);
        }
        return Advance732E00(state with { Data = data.ToImmutableArray() }, curve);
    }

    public static NativeBattleMoogleCameraStep Advance732E00(NativeBattleMoogleCameraState state, NativeBattleMoogleCameraCurve? curve = null)
    {
        state.Validate();
        byte[] data = state.Data.ToArray();
        if (Word(data, 0xCE) is not (0 or 1))
        {
            throw new NotSupportedException("Native Moogle camera anchor modes beyond0/1 require their separate raw owner contract.");
        }
        Set(data, 0xE2, Word(data, 0xE2) + 1);
        uint pointer = U32(data, 0x50);
        if (pointer == 0)
        {
            Set(data, 0xD2, Word(data, 0xD2) + Word(data, 0xD4));
            short acceleration = Word(data, 0xD4);
            short velocity = Word(data, 0xD2);
            short clamp = Word(data, 0xD6);
            if (acceleration > 0 && velocity > clamp || acceleration < 0 && velocity < clamp)
            {
                Set(data, 0xD2, clamp);
            }
            Set(data, 0xD0, Word(data, 0xD0) + Word(data, 0xD2));
        }
        else
        {
            if (curve is null)
            {
                throw new NotSupportedException("Native Moogle camera curve requires its explicitly supplied bounded WORD owner.");
            }
            long offset = (long)pointer - curve.Address;
            if (curve.Address == 0 || curve.Values.IsDefaultOrEmpty || offset < 0 || (offset & 1) != 0 || offset / 2 >= curve.Values.Length)
            {
                throw new InvalidDataException("Native Moogle camera curve pointer leaves its supplied owner.");
            }
            short value = curve.Values[(int)(offset / 2)];
            Set(data, 0xD0, value);
            if (value != 4096)
            {
                Dword(data, 0x50, unchecked(pointer + 2));
            }
        }
        bool complete = Word(data, 0xD0) >= 4096;
        if (complete)
        {
            Set(data, 0xD0, 4096);
        }
        Set(data, 0xD8, Angle(Word(data, 0xC2), Word(data, 0xC4), Word(data, 0xB4) != 0));
        short yawSelector = Word(data, 0xB6);
        if (yawSelector is 0 or 1)
        {
            Set(data, 0xDA, Angle(Word(data, 0xC6), Word(data, 0xC8), yawSelector == 1));
        }
        Set(data, 0xDC, Angle(Word(data, 0xCA), Word(data, 0xCC), Word(data, 0xB8) != 0));
        Set(data, 0xE0, Lerp(Word(data, 0xBA), Word(data, 0xBC)));
        Set(data, 0xDE, Lerp(Word(data, 0xBE), Word(data, 0xC0)));
        for (int axis = 0; axis < 3; axis++)
        {
            bool mode1 = Word(data, 0xCE) == 1;
            Set(data, (mode1 ? 0x54 : 0x5C) + axis * 2, Lerp(Word(data, (mode1 ? 0x74 : 0x64) + axis * 2), Word(data, (mode1 ? 0x7C : 0x6C) + axis * 2)));
        }
        return RecomputePublication(state with { Data = data.ToImmutableArray() }) with { Complete = complete };

        short Lerp(short start, short end) => unchecked((short)(start + unchecked((end - start) * Word(data, 0xD0)) / 4096));
        int Angle(short start, short end, bool decreasing)
        {
            if (!decreasing && start > end)
            {
                end = unchecked((short)(end + 4096));
            }
            else if (decreasing && start < end)
            {
                start = unchecked((short)(start + 4096));
            }
            return (start + unchecked((end - start) * Word(data, 0xD0)) / 4096) & 4095;
        }
    }

    public static NativeBattleMoogleCameraStep RecomputePublication(NativeBattleMoogleCameraState state)
    {
        state.Validate();
        byte[] data = state.Data.ToArray();
        short mode = Word(data, 0xCE);
        if (mode is not (0 or 1))
        {
            throw new NotSupportedException("Native effect camera publication requires its reached mode0/1.");
        }
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Word(data, 0xDA));
        rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(rotation, Word(data, 0xD8));
        NativeBattleVector offset = NativeBattleGfMatrices.Transform460840(rotation, new(0, 0, Word(data, 0xDE)));
        short[] eye = new short[3];
        int[] offsets = [offset.X, offset.Y, offset.Z];
        for (int axis = 0; axis < 3; axis++)
        {
            eye[axis] = unchecked((short)(Word(data, (mode == 1 ? 0x54 : 0x5C) + axis * 2) + offsets[axis]));
            Set(data, (mode == 1 ? 0x5C : 0x54) + axis * 2, mode == 1 ? eye[axis] + offsets[axis] : eye[axis]);
        }
        if (mode == 0)
        {
            // This fourth WORD is unconsumed native stack padding; zero is canonical, never a native equality claim.
            Set(data, 0x5A, 0);
        }
        NativeBattleCameraPose pose = new(new(eye[0], eye[1], eye[2]), new(Word(data, 0x5C), Word(data, 0x5E), Word(data, 0x60)), Word(data, 0xE0), Word(data, 0xDC));
        return new(new(data.ToImmutableArray(), mode == 1 && state.EyePaddingKnown), false, pose);
    }

    public static NativeBattleCameraState Publish733150(NativeBattleMoogleCameraStep step, NativeBattleCameraState camera)
    {
        step.State.Validate();
        return camera.WriteEffectGlobals(step.Pose) with { EyePaddingF6 = null, TargetPaddingFE = Word(step.State.Data, 0x62) };
    }
    private static short Word(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset));
    private static short Word(byte[] data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));
    private static uint U32(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));
    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
    private static void Set(byte[] data, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), unchecked((ushort)value));
    private static void Dword(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}

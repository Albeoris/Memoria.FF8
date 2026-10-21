using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStage137State
{
    [System.Text.Json.Serialization.JsonRequired]
    public bool Deforming { get; init; } = true;
    [System.Text.Json.Serialization.JsonRequired]
    public int DeformationTick { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int ShakePeriod { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int ShakeAmplitude { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int CallbackCounter { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int WaveDelay { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int WaveCounter { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int WaveRise { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int WaveAmplitude { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public int CameraCounter { get; init; }
}

public sealed record NativeBattleStage137Step(NativeBattleStage137State State, short Phase, short RotationSpeed, short CameraShake, NativeBattleVector? CameraEye, int? DeformTick);

public static class NativeBattleStage137
{
    public const uint CallbackAddress = 0x50DFF0;

    public static NativeBattleStage137State Initialize50DF10(NativeBattleStage137State state) => state with { Deforming = true, ShakeAmplitude = 0 };

    public static NativeBattleStage137Step Advance50DFF0(NativeBattleStage137State state, short phase, uint flags, short rotationSpeed, short cameraShake, ushort graphicsFlags, ushort cameraMask, NativeBattleVector capturedEye, Func<uint> random, Func<int> globalRandom)
    {
        if (phase == 0)
        {
            return new(state with { ShakePeriod = (int)(random() % 30 + 30) }, 1, rotationSpeed, cameraShake, null, null);
        }
        if (phase != 1 || (flags & 8) != 0)
        {
            return new(state, phase, rotationSpeed, cameraShake, null, null);
        }
        int? deform = null;
        NativeBattleVector? position = null;
        if (state.Deforming)
        {
            deform = state.DeformationTick;
            state = state with { DeformationTick = unchecked(state.DeformationTick + 1) };
        }
        else
        {
            rotationSpeed = 0;
            if (state.WaveCounter == 0)
            {
                state = StartWave(state, random, globalRandom);
            }
            else if (state.WaveDelay != 0)
            {
                state = state with { WaveDelay = state.WaveDelay - 1 };
            }
            else
            {
                int counter = state.WaveCounter;
                int fraction = counter > 60 ? 4096 - NativeBattleCameraMath.Sine(NativeBattleCameraMath.Sine(((counter - 60) << 12) / state.WaveRise / 4) / 4) : counter > 30 ? 4096 : 4096 - NativeBattleCameraMath.Sine((counter << 12) / 30 / 4 + 1024);
                rotationSpeed = unchecked((short)((fraction * state.WaveAmplitude) >> 12));
                state = state with { WaveCounter = counter - 1 };
            }
            if (cameraMask != 0 || (graphicsFlags & 0x8000) != 0)
            {
                state = state with { CameraCounter = 0 };
            }
            else
            {
                int sine = NativeBattleCameraMath.Sine(unchecked(8 * state.CameraCounter));
                position = new(unchecked((short)(capturedEye.X + ((2000 * sine) >> 12))), unchecked((short)(capturedEye.Y + ((-500 * sine) >> 12))), capturedEye.Z);
                state = state with { CameraCounter = unchecked(state.CameraCounter + 1) };
            }
        }
        state = state with { CallbackCounter = unchecked(state.CallbackCounter + 1) };
        if ((state.CallbackCounter & 1) != 0 && state.ShakeAmplitude != 0 && cameraShake == 0)
        {
            int period = unchecked(state.ShakePeriod - 1);
            cameraShake = unchecked((short)(period > 0 ? state.ShakeAmplitude : 2 * state.ShakeAmplitude));
            state = state with { ShakePeriod = period > 0 ? period : (int)(random() % 30 + 30) };
        }
        return new(state, phase, rotationSpeed, cameraShake, position, deform);
    }

    private static NativeBattleStage137State StartWave(NativeBattleStage137State state, Func<uint> random, Func<int> globalRandom)
    {
        int delay = (int)(random() % 30 + 30);
        uint sample = random();
        int amplitude = (int)(random() % 768 + 256);
        if (globalRandom() < 0x4000)
        {
            amplitude = -amplitude;
        }
        return state with { WaveDelay = delay, WaveCounter = (int)(sample % 30 + 90), WaveRise = (int)(sample % 30 + 30), WaveAmplitude = amplitude };
    }

    public static NativeBattleRawMesh Deform50E120(NativeBattleRawMesh mesh, int tick)
    {
        NativeBattleVector Deform(NativeBattleVector vertex)
        {
            if (vertex.Y <= 0)
            {
                return vertex;
            }
            double value = Math.Atan2(vertex.X, vertex.Z) * (double)651.89862060546875f + 4503601774854144d;
            int angle = unchecked((int)((uint)BitConverter.DoubleToInt64Bits(value) + 0x80000000u));
            int delta = unchecked(4 * NativeBattleCameraMath.Sine(unchecked(tick + (angle << 10))) + 0x4000) >> 12;
            return vertex with { Y = unchecked((short)(vertex.Y - delta)) };
        }
        ImmutableArray<NativeBattleRawVertexGroup>? packed = mesh.PackedVertexGroups;
        if (packed is { } groups)
        {
            packed = groups.Select(group =>
            {
                byte[] bytes = group.Bytes.ToArray();
                for (int index = 0; index < group.Count; index++)
                {
                    int offset = index * 6;
                    NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 4)));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), Deform(vertex).Y);
                }
                return group with { Bytes = bytes.ToImmutableArray() };
            }).ToImmutableArray();
        }
        return mesh with { Vertices = mesh.Vertices.Select(vertex => vertex with { Position = Deform(vertex.Position) }).ToImmutableArray(), PackedVertexGroups = packed };
    }
}

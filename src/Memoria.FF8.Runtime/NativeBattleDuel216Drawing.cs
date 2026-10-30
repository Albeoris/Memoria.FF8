using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216DrawingStep(NativeBattleDuel216Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDuel216Drawing
{
    public static NativeBattleGfGeometryBatch Flash699EE0(int phase, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((uint)phase > 1)
        {
            throw new InvalidDataException("Native Different Beat flash requires its actual two reached passes.");
        }
        uint color = phase == 0 ? 0x2A808080u : 0x2A000000u;
        ImmutableArray<uint> words = [0x05000000, color, 0, 320, 216u << 16, (216u << 16) | 320];
        byte[] bytes = new byte[24];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(8, words, [0, 0, 0, 0]);
        if (pilotIdentity is { } identity)
        {
            primitive = primitive with { PilotGeometry = new(identity, new(0, 0, 0), new(320, 0, 0), new(0, 216, 0), new(320, 216, 0), 4, false) { ScreenSpace = true, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
        }
        return new([new(0, bytes.ToImmutableArray(), mask, primitive)], 24);
    }

    public static NativeBattleDuel216Frame Morph69A670(NativeBattleDuel216Frame frame, uint sourceAddress, int scale, int angle)
    {
        NativeBattleDuel216Resources resources = frame.Memory.Duel216?.Resources ?? throw new NotSupportedException("Native Different Beat deformation requires its original resource.");
        ImmutableArray<byte> source = resources.Read(sourceAddress, checked((int)(NativeBattleDuel216Resources.EndAddress - sourceAddress)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(4));
        if (count <= 0 || 8L + count * 8L > source.Length || count * 8L > 1216)
        {
            throw new InvalidDataException("Native Different Beat deformation leaves its original selected vertex owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Different Beat deformation requires its physical arena.");
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> vertex = source.AsSpan().Slice(8 + index * 8, 8);
            short x = BinaryPrimitives.ReadInt16LittleEndian(vertex);
            short y = BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(2));
            short z = BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(4));
            int adjusted = unchecked(angle + (y >> 2));
            int sine = NativeBattleCameraMath.Sine(adjusted);
            int cosine = NativeBattleCameraMath.Cosine(adjusted);
            int scaledY = unchecked(scale * y) >> 12;
            short finalX = unchecked((short)(unchecked(cosine * x - sine * scaledY) >> 12));
            short finalY = unchecked((short)(unchecked(cosine * scaledY + sine * x) >> 12));
            arena = arena.Write(11480 + index * 8, BitConverter.GetBytes(finalX)).Write(11482 + index * 8, BitConverter.GetBytes(finalY)).Write(11484 + index * 8, BitConverter.GetBytes(z));
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    public static NativeBattleDuel216DrawingStep DrawMorph6960E0(NativeBattleDuel216Frame frame, int phase, int divisor, bool fade, NativeBattleVector bone20, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int finalPhase = divisor == 3 ? 3 : 4;
        if (divisor is not (3 or 5) || phase < 0 || phase > finalPhase || fade != (phase == finalPhase))
        {
            throw new InvalidDataException("Native Different Beat deformation requires its actual phase and final fade pass.");
        }
        NativeBattleDuel216Resources resources = frame.Memory.Duel216?.Resources ?? throw new NotSupportedException("Native Different Beat deformation requires its original mesh.");
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 148);
        uint address = temporary.Address;
        NativeBattleActionMatrix local = NativeBattleDuel215Controller.ParticleBasis6ED1E0(1024, 2048) with { X = bone20.X, Y = bone20.Y, Z = bone20.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
        NativeBattleFileStaging staging = Matrix(temporary.Staging, address + 108, matrix).Write(address + 140, BitConverter.GetBytes(bone20.X)).Write(address + 142, BitConverter.GetBytes(bone20.Y)).Write(address + 144, BitConverter.GetBytes(bone20.Z));
        frame = Morph69A670(frame with { Staging = staging }, 0x113C3CC, (((phase << 12) / divisor) >> 1) + 2048, 1024 - (phase << 10));
        uint flags = fade ? 8432u : 8240u;
        staging = frame.Staging.Write(address, BitConverter.GetBytes(0x113C3CCu)).Write(address + 4, BitConverter.GetBytes(NativeBattleDuel216Controller.Arena + 11480)).Write(address + 28, BitConverter.GetBytes(flags)).Write(address + 24, BitConverter.GetBytes(-128)).Write(address + 32, new byte[4]);
        if (fade)
        {
            staging = staging.Write(address + 8, new byte[4]).Write(address + 12, BitConverter.GetBytes(2048));
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(flags, fade ? 0u : null, fade ? 2048 : null, -128, 0, 0, full, full, 17, 2);
        ImmutableArray<byte> mesh = resources.Read(0x113C3CC, checked((int)(NativeBattleDuel216Resources.EndAddress - 0x113C3CC)));
        NativeBattleWindowedMesh701DD0Result drawn = NativeBattleWindowedMesh701DD0.Emit(staging, address, 0x113C3CC, mesh, matrix, projection, context, colors, index =>
        {
            byte[] bytes = frame.Memory.Arena!.Read(checked(11480 + index * 8), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }, pilotIdentity);
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 148) }, drawn.Batch);
    }

    public static NativeBattleDuel216DrawingStep Draw69A410(NativeBattleDuel216Frame frame, NativeBattleDuel216MeshParticle particle, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleDuel216Resources resources = frame.Memory.Duel216?.Resources ?? throw new NotSupportedException("Native Different Beat debris requires its original mesh.");
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 152);
        uint address = temporary.Address;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, particle.Local88);
        NativeBattleFileStaging staging = Rotation(temporary.Staging, address + 120, particle.Basis120).Write(address + 138, new byte[2]);
        staging = Matrix(staging, address + 88, matrix).Write(address, BitConverter.GetBytes(particle.Source)).Write(address + 28, new byte[4]);
        ImmutableArray<byte> mesh = resources.Read(particle.Source, checked((int)(NativeBattleDuel216Resources.EndAddress - particle.Source)));
        NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch572200(staging, address, particle.Source, mesh, matrix, projection, new(0, null, null, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity ?? particle.PilotIdentity);
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 152) }, drawn.Batch);
    }

    private static NativeBattleFileStaging Matrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix) => Rotation(staging, address, matrix).MarkUnknown(address + 18, 2).Write(address + 20, BitConverter.GetBytes(matrix.X)).Write(address + 24, BitConverter.GetBytes(matrix.Y)).Write(address + 28, BitConverter.GetBytes(matrix.Z));

    private static NativeBattleFileStaging Rotation(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            staging = staging.Write(checked(address + (uint)(index * 2)), BitConverter.GetBytes(matrix.Rotation[index]));
        }
        return staging;
    }
}

using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89MeshDrawing
{
    public static NativeBattleRinoa89ParticleDraw DrawSpriteMesh6FD3C0(NativeBattleRinoa89Frame frame, NativeBattleActionMatrix localPose, NativeBattleVector angles, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleRinoa89Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 128);
        NativeBattleFileStaging staging = allocation.Staging;
        uint temporary = allocation.Address;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, localPose);
        staging = Word(Word(Word(staging, temporary, unchecked((ushort)angles.X)), temporary + 2, unchecked((ushort)angles.Y)), temporary + 4, unchecked((ushort)angles.Z));
        staging = Matrix(staging, temporary + 8, matrix);
        staging = Dword(Dword(staging, temporary + 40, 0x14A04D8), temporary + 68, 0);
        NativeBattleModuleMeshTemporaryResult draw = NativeBattleModulePackets.EmitRetainedBatch572200(staging, temporary + 40, 0x14A04D8, Resource(resources, 0x14A04D8), matrix, projection, new(0, null, null, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity);
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(draw.Staging, 128) }, draw.Batch);
    }

    public static NativeBattleRinoa89ParticleDraw DrawApproach6FA780(NativeBattleRinoa89Frame frame, int anchorX, int anchorY, int anchorZ, int delta, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleRinoa89Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((uint)delta >= 15)
        {
            throw new InvalidDataException("Native Wishing Star approach requires its actual fifteen-frame phase.");
        }
        int ratio = (delta << 12) / 15;
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 156);
        NativeBattleFileStaging staging = allocation.Staging;
        uint temporary = allocation.Address;
        staging = Dword(Dword(Dword(staging, temporary, checked((uint)ratio)), temporary + 4, checked((uint)ratio)), temporary + 8, checked((uint)ratio));
        NativeBattleActionMatrix local = new([checked((short)ratio), 0, 0, 0, checked((short)ratio), 0, 0, 0, checked((short)ratio)], anchorX, anchorY, unchecked(anchorZ - 13500 * ratio / 4096 + 15000), 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
        staging = Matrix(staging, temporary + 16, matrix);
        staging = Dword(Dword(Dword(Dword(staging, temporary + 48, 0x1497140), temporary + 72, 0), temporary + 76, 0), temporary + 80, 0);
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleWindowedMesh701DD0Result draw = Window(staging, temporary + 48, 0x1497140, matrix, projection, new(0, null, null, 0, 0, 0, full, full, 17, 2), colors, resources, pilotIdentity);
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(draw.Staging, 156) }, draw.Batch);
    }

    public static NativeBattleRinoa89ParticleDraw DrawReturnBurst6FA780(NativeBattleRinoa89Frame frame, int anchorX, int anchorY, int anchorZ, int delta, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleRinoa89Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((uint)delta >= 15)
        {
            throw new InvalidDataException("Native Wishing Star burst requires its actual fifteen-frame phase.");
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 140);
        NativeBattleFileStaging staging = allocation.Staging;
        uint temporary = allocation.Address;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], anchorX, anchorY, anchorZ, 4096));
        staging = Matrix(staging, temporary, matrix);
        staging = Dword(Dword(Dword(staging, temporary + 32, 0x149D0F8), temporary + 40, 0), temporary + 56, unchecked((uint)-64));
        staging = staging.Write(temporary + 64, new byte[20]);
        staging = Word(Word(Word(Word(Word(Word(staging, temporary + 66, (ushort)(32 * (delta & 3))), temporary + 72, 256), temporary + 74, 256), temporary + 78, 128), temporary + 80, 256), temporary + 82, 128);
        uint flags = delta >= 11 ? 240u : 48u;
        int fade = (delta - 10) << 10;
        staging = Dword(staging, temporary + 60, flags);
        if (delta >= 11)
        {
            staging = Dword(staging, temporary + 44, checked((uint)fade));
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleTextureWindow lower = new(0, 128, 256, 128);
        NativeBattleEsunaMeshContext context = new(flags, delta >= 11 ? 0u : null, delta >= 11 ? fade : null, -64, 0, (ushort)(32 * (delta & 3)), full, lower, 17, 2);
        NativeBattleWindowedMesh701DD0Result first = Window(staging, temporary + 32, 0x149D0F8, matrix, projection, context, colors, resources, pilotIdentity);
        short angle = unchecked((short)(delta << 8));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        NativeBattleActionMatrix rotated = NativeBattleGfMatrices.Compose56C2F0(view, new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], anchorX, anchorY, anchorZ, 4096));
        staging = Matrix(first.Staging, temporary, rotated);
        staging = Dword(Dword(Dword(Dword(staging, temporary + 32, 0x1499FA8), temporary + 56, unchecked((uint)-64)), temporary + 60, 48), temporary + 64, 0);
        NativeBattleWindowedMesh701DD0Result second = Window(staging, temporary + 32, 0x1499FA8, rotated, projection, new(48, null, null, -64, 0, 0, full, lower, 17, 2), colors, resources, pilotIdentity);
        int ratio = (delta << 12) / 15;
        staging = Dword(Dword(Dword(Dword(second.Staging, temporary + 32, 0x149C730), temporary + 56, unchecked((uint)-64)), temporary + 60, 240), temporary + 44, checked((uint)ratio));
        NativeBattleWindowedMesh701DD0Result third = Window(staging, temporary + 32, 0x149C730, rotated, projection, new(240, 0, ratio, -64, 0, 0, full, lower, 17, 2), colors, resources, pilotIdentity);
        ImmutableArray<NativeBattleGfGeometryAttempt> attempts = first.Batch.Attempts.AddRange(second.Batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(first.Batch.AllocationBytes + attempt.ByteOffsetRelative) })).AddRange(third.Batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(first.Batch.AllocationBytes + second.Batch.AllocationBytes + attempt.ByteOffsetRelative) }));
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(third.Staging, 140) }, new(attempts, checked(first.Batch.AllocationBytes + second.Batch.AllocationBytes + third.Batch.AllocationBytes)));
    }

    private static NativeBattleWindowedMesh701DD0Result Window(NativeBattleFileStaging staging, uint temporary, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEsunaMeshContext context, NativeBattleSummonColors colors, NativeBattleRinoa89Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null) => NativeBattleWindowedMesh701DD0.Emit(staging, temporary, meshAddress, Resource(resources, meshAddress), matrix, projection, context, colors, _ => throw new InvalidDataException("Native Wishing Star inline mesh unexpectedly selects external vertices."), pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + (int)meshAddress) } : null);
    private static ImmutableArray<byte> Resource(NativeBattleRinoa89Resources resources, uint address)
    {
        int offset = checked((int)(address - NativeBattleRinoa89Resources.StaticAddress));
        return resources.StaticImage.Data.Slice(offset, resources.StaticImage.Data.Length - offset);
    }
    private static NativeBattleFileStaging Matrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            staging = Word(staging, address + (uint)index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return Dword(Dword(Dword(staging.MarkUnknown(address + 18, 2), address + 20, unchecked((uint)matrix.X)), address + 24, unchecked((uint)matrix.Y)), address + 28, unchecked((uint)matrix.Z));
    }
    private static NativeBattleFileStaging Word(NativeBattleFileStaging staging, uint address, ushort value) => staging.Write(address, BitConverter.GetBytes(value));
    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
}



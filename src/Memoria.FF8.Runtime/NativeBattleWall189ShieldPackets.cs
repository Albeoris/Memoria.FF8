using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWall189ShieldPackets
{
    public static NativeBattleModuleMeshTemporaryResult Emit64F6D0(NativeBattleFileStaging staging, uint temporary, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short age, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = age >= 48 ? 243u : 0u;
        int fade = age >= 48 ? 341 * (age - 48) : 0;
        staging = Dword(0, mesh.Address);
        staging = Dword(4, checked(mesh.Address + 8));
        staging = Dword(8, 0);
        staging = Dword(28, flags);
        staging = Dword(24, 0);
        staging = Dword(88, 0xE1000020);
        staging = Dword(92, 0xE1000220);
        if (age >= 48)
        {
            staging = Dword(12, unchecked((uint)fade));
        }
        NativeBattleMeshPacketVariant observations = new(null, null, false, true)
        {
            ObserveFlags = value => staging = Dword(48, value),
            ObserveArea = value => staging = Dword(36, unchecked((uint)value)),
            ObserveAverageDepth = value => staging = Dword(44, value)
        };
        NativeBattleGfGeometryBatch plain = NativeBattleGfGeometryPackets.EmitBatch(mesh.Bytes, 0, default, matrix, projection, new(flags, 0, fade, null, null, 0, 0, 17, 2), colors, observeSourceCursor: cursor => staging = Dword(32, checked(mesh.Address + (uint)cursor)), variant: observations, pilotIdentity: pilotIdentity);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach (NativeBattleGfGeometryAttempt attempt in plain.Attempts)
        {
            if (attempt.Data.Length != 32)
            {
                throw new NotSupportedException("Native Wall captured mesh requires its original FT3-only group.");
            }
            byte[] bytes = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x09000000);
            attempt.WriteMask.AsSpan().Slice(0, 4).CopyTo(mask);
            attempt.Data.AsSpan().Slice(4).CopyTo(bytes.AsSpan(8));
            attempt.WriteMask.AsSpan().Slice(4).CopyTo(mask.AsSpan(8));
            NativeBattleGeometryPrimitive? primitive = null;
            if (attempt.Primitive is { } accepted)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xE1000020);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 0xE1000220);
                mask.AsSpan(4, 4).Fill(NativeBattleGfPacketByteState.Known);
                mask.AsSpan(36, 4).Fill(NativeBattleGfPacketByteState.Known);
                ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
                primitive = accepted with { Words = words };
            }
            attempts.Add(new(allocated, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated += 40;
            }
        }
        return new(staging, new(attempts.ToImmutable(), allocated));

        NativeBattleFileStaging Dword(int offset, uint value) => staging.Write(checked(temporary + (uint)offset), BitConverter.GetBytes(value));
    }
}

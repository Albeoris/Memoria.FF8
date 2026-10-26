using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusCountdownProjectionResult(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, uint Screen, ushort QuarterDepth, uint Flags);

/// <summary>50A410's retained temporary and56C880 projection, using the prior5034E0 camera.</summary>
public static class NativeBattleStatusCountdownProjection
{
    public static NativeBattleStatusCountdownProjectionResult Project50A410(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleCameraState camera, NativeBattleVector anchor, uint statuses)
    {
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native56C880 requires its prior published camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native5034E0 requires its actual1D97778 camera matrix.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 24);
        staging = allocation.Staging;
        NativeBattleEffectArena scratch = NativeBattleTemporaryMemory.ReadView(staging, allocation.Address, 24);
        byte[] xyz = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(xyz, anchor.X);
        BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), anchor.Y);
        BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), anchor.Z);
        scratch = scratch.Write(0, xyz);
        (byte[] vector, bool[] known) = scratch.ReadSnapshot(0, 8);
        short? padding = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(6)) : null;
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native56C880 requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native56C880 requires its current arithmetic registers."));
        short? matrixPadding = camera.CanonicalViewPadding is ushort retainedPadding ? unchecked((short)retainedPadding) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, true, true, matrixPadding) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, anchor, padding);
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Native45FAA0 requires its actual depth cue controls.");
        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
        uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Native45E260 requires its projected screen DWORD.");
        uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Native45E200 requires its resulting projection flags.");
        uint cue = registers.Colors.Ir0 ?? throw new NotSupportedException("Native45E210 requires its resulting depth cue.");
        ushort depth = unchecked((ushort)((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Native45E250 requires its resulting depth.")) >> 2));
        scratch = scratch.Write(8, BitConverter.GetBytes(screen)).Write(12, BitConverter.GetBytes(depth)).Write(16, BitConverter.GetBytes(flags)).Write(20, BitConverter.GetBytes(cue));
        short y = unchecked((short)((short)(screen >> 16) - 8));
        if ((flags & 0x60000) == 0 && (statuses & 0x2000) != 0)
        {
            y = unchecked((short)(y - 8));
        }
        scratch = scratch.Write(10, BitConverter.GetBytes(y));
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, scratch, 24);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 24);
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, staging, screen, depth, flags);
    }
}

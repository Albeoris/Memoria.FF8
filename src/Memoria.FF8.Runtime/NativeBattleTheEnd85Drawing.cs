using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85DrawServices(Func<uint, int, byte[]> Read, Action<uint, ImmutableArray<byte>> Write);
public sealed record NativeBattleTheEnd85DrawingStep(NativeBattleModulePoolTask Task, int Remaining, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Packets, bool PublishedFarColor, uint? FarRed, uint? FarGreen, uint? FarBlue)
{
    public ushort LinkFlags { get; init; }
}

/// <summary>Current model animation, selected morph XYZ and the separately retained104-byte native drawing contexts.</summary>
public static class NativeBattleTheEnd85Drawing
{
    public static NativeBattleTheEnd85DrawingStep Advance785660(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleFileStaging staging, uint frozen, NativeBattleTheEnd85DrawServices services)
    {
        if (task.PayloadLength != 732 - 12 || task.ReadDword(116) != source.Address)
        {
            throw new InvalidDataException("Native The End model requires its complete task, retained animation workspace and current source pointer.");
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native The End model requires its current camera matrix.");
        NativeBattleActionMatrix context = NativeBattleSearchProgramDrawing.ComposeContext758D70(task, view);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        NativeBattleTheEnd85DrawingStep result = Empty(task, staging);
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadRawPayloadSpan().Slice(148 - 12).ToArray().ToImmutableArray());
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, unchecked((int)frozen), (current, node) =>
        {
            NativeBattleTheEnd85DrawingStep drawn = DrawNode785790(task, source, node, context, projection, colors, result.Staging, services);
            attempts.AddRange(drawn.Packets.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocation) }));
            allocation = checked(allocation + drawn.Packets.AllocationBytes);
            result = drawn.PublishedFarColor ? drawn : result with { Staging = drawn.Staging };
            return current;
        });
        return result with { Task = task.WriteRetainedBytes(12 + (148 - 12), advanced.Workspace.Data.AsSpan()), Remaining = advanced.Remaining, Packets = new(attempts.ToImmutable(), allocation) };
    }

    public static NativeBattleTheEnd85DrawingStep DrawNode785790(NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleFileStaging staging, NativeBattleTheEnd85DrawServices services)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return Empty(task, staging);
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 104);
        uint temporary = allocation.Address;
        staging = allocation.Staging;
        int index = unchecked((short)node.GeometryIndex);
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(8 + index * 4), 4));
        uint mesh = checked(source.Address + (uint)geometry);
        Store(0, mesh);
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(geometry + 4), 4));
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint selected;
        if (morph)
        {
            selected = 0x25C1D28;
            uint destination = selected;
            foreach (NativeBattleVector point in NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38))
            {
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, point.X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), point.Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), point.Z);
                services.Write(destination, xyz.ToImmutableArray());
                destination = checked(destination + 8);
            }
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(mesh + 12 + (uint)checked(shape * count * 8));
        }
        Store(4, selected);
        NativeBattleActionMatrix matrix = ComposeNode785790(node, context);
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if ((node.Header & 0x2000) != 0)
        {
            flags |= 12;
        }
        Store(12, unchecked((uint)(int)node.Scalar36));
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
            Store(8, node.Color32);
        }
        Store(20, flags);
        int bias = unchecked((short)task.ReadWord(136));
        if (task.ReadWord(128) == 0 && node.GeometryIndex == 2)
        {
            bias -= 256;
        }
        Store(16, unchecked((uint)bias));
        SetWindows(ref staging, temporary);
        NativeBattleTheEnd85MeshStep emitted = NativeBattleTheEnd85GeometryPackets.Emit785A90(staging, temporary, matrix, projection, colors, services.Read);
        return Completed(task, emitted);

        void Store(int offset, uint value) => staging = staging.Write(temporary + (uint)offset, BitConverter.GetBytes(value));
    }

    public static NativeBattleActionMatrix ComposeNode785790(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
    {
        NativeBattleActionMatrix matrix = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x200) == 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
            matrix = NativeBattleGfMatrices.Multiply56C090(context, matrix);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }

    public static NativeBattleTheEnd85DrawingStep Draw788700(NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleFileStaging staging, NativeBattleTheEnd85DrawServices services)
    {
        if ((task.ReadByte(38) & 4) != 0)
        {
            return Empty(task, staging);
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native The End mesh requires its current camera matrix.");
        NativeBattleActionMatrix local = view with { X = SignedWord(task, 28), Y = SignedWord(task, 30), Z = SignedWord(task, 32) };
        local = NativeBattleCuragaMatrices.RotateZ8DD960(local, SignedWord(task, 72));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(48)), unchecked((int)task.ReadDword(52)), unchecked((int)task.ReadDword(56)));
        return DrawDirect(task, NativeBattlePoseMatrices.Compose56C2F0(view, local), projection, colors, staging, services, null);
    }

    public static NativeBattleTheEnd85DrawingStep Draw78C530(NativeBattleModulePoolTask task, short threshold, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleFileStaging staging, NativeBattleTheEnd85DrawServices services)
    {
        if ((task.ReadByte(38) & 4) != 0)
        {
            return Empty(task, staging);
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native The End title requires its current camera matrix.");
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770();
        local = NativeBattleCuragaMatrices.RotateY8DD8A0(local, SignedWord(task, 70));
        local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, SignedWord(task, 68));
        local = NativeBattleCuragaMatrices.RotateZ8DD960(local, SignedWord(task, 72));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(48)), unchecked((int)task.ReadDword(52)), unchecked((int)task.ReadDword(56))) with { X = SignedWord(task, 28), Y = SignedWord(task, 30), Z = SignedWord(task, 32) };
        return DrawDirect(task, NativeBattlePoseMatrices.Compose56C2F0(view, local), projection, colors, staging, services, threshold);
    }

    public static NativeBattleTheEnd85DrawingStep Draw78D2E0(NativeBattleModulePoolTask task, NativeBattleFileStaging staging)
    {
        SkinWrites packet = new(52, 0);
        packet.Word(0, 0x0C000000);
        packet.Byte(7, 0x3E);
        byte u = task.ReadByte(88);
        packet.Byte(12, 0);
        packet.Byte(24, u);
        packet.Byte(36, 0);
        packet.Byte(48, u);
        packet.Byte(13, 224);
        packet.Byte(25, 224);
        packet.Byte(37, 255);
        packet.Byte(49, 255);
        packet.Word16(14, 15700);
        packet.Word16(26, 182);
        ushort right = unchecked((ushort)(186 + task.ReadWord(88)));
        packet.Word16(8, 186);
        packet.Word16(20, right);
        packet.Word16(32, 186);
        packet.Word16(44, right);
        packet.Word16(10, 160);
        packet.Word16(22, 160);
        packet.Word16(34, 192);
        packet.Word16(46, 192);
        for (int channel = 0; channel < 3; channel++)
        {
            packet.Byte(4 + channel, 128);
            packet.Byte(28 + channel, 128);
            packet.Byte(16 + channel, task.ReadByte(90));
            packet.Byte(40 + channel, task.ReadByte(90));
        }
        NativeBattleGfGeometryBatch batch = new([packet.Attempt(new(5, packet.Words(), [0, 0, 0, 0]))], 52);
        return Empty(task, staging) with { Packets = batch, LinkFlags = 1 };
    }

    public static NativeBattleTheEnd85DrawingStep Draw78D4D0(NativeBattleModulePoolTask task, short height, NativeBattleFileStaging staging)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int half = 0; half < 2; half++)
        {
            SkinWrites packet = new(24, half * 24);
            packet.Word(0, 0x05000000);
            packet.Word(4, 0x29000000);
            short top = half == 0 ? (short)0 : unchecked((short)(216 - height));
            short bottom = half == 0 ? height : (short)216;
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Word16(8 + corner * 4, (ushort)((corner & 1) == 0 ? 0 : 320));
                packet.Word16(10 + corner * 4, unchecked((ushort)(corner < 2 ? top : bottom)));
            }
            packets.Add(packet.Attempt(new(5, packet.Words(), [0, 0, 0, 0])));
        }
        return Empty(task, staging) with { Packets = new(packets.ToImmutable(), 48), LinkFlags = 1 };
    }

    private static NativeBattleTheEnd85DrawingStep DrawDirect(NativeBattleModulePoolTask task, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleFileStaging staging, NativeBattleTheEnd85DrawServices services, short? threshold)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 104);
        uint context = allocation.Address;
        staging = allocation.Staging.Write(context, BitConverter.GetBytes(task.ReadDword(76))).Write(context + 8, BitConverter.GetBytes(task.ReadDword(64))).Write(context + 12, BitConverter.GetBytes((int)SignedWord(task, 80))).Write(context + 16, BitConverter.GetBytes((int)SignedWord(task, 82))).Write(context + 20, BitConverter.GetBytes(0xF0u));
        SetWindows(ref staging, context);
        NativeBattleTheEnd85MeshStep emitted;
        if (threshold is short value)
        {
            staging = staging.Write(context + 100, BitConverter.GetBytes(value));
            emitted = NativeBattleTheEnd85GeometryPackets.Emit78C660(staging, context, matrix, projection, colors, services.Read);
        }
        else
        {
            emitted = NativeBattleTheEnd85GeometryPackets.Emit785A90(staging, context, matrix, projection, colors, services.Read);
        }
        return Completed(task, emitted);
    }

    private static void SetWindows(ref NativeBattleFileStaging staging, uint context)
    {
        foreach (int offset in new[] { 24, 26, 28, 30, 36, 38 })
        {
            staging = staging.Write(context + (uint)offset, BitConverter.GetBytes((ushort)0));
        }
        foreach (int offset in new[] { 32, 34, 40, 42 })
        {
            staging = staging.Write(context + (uint)offset, BitConverter.GetBytes((ushort)256));
        }
    }

    private static NativeBattleTheEnd85DrawingStep Completed(NativeBattleModulePoolTask task, NativeBattleTheEnd85MeshStep emitted) => new(task, 0, NativeBattleTemporaryMemory.Release5082D0(emitted.Staging, 104), emitted.Packets, true, emitted.FarRed, emitted.FarGreen, emitted.FarBlue);
    private static NativeBattleTheEnd85DrawingStep Empty(NativeBattleModulePoolTask task, NativeBattleFileStaging staging) => new(task, 0, staging, new([], 0), false, null, null, null);
    private static short SignedWord(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}

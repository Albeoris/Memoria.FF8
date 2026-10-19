namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeDuel215Body69D100(NativeBattleGraphicsState graphics, int index, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native Duel215 body requires its physical temporary owner.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 76);
        staging = allocation.Staging;
        uint address = allocation.Address;
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel215 body requires its actual world matrix.");
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, world);
        for (int lane = 0; lane < 9; lane++)
        {
            staging = staging.Write(address + (uint)lane * 2, BitConverter.GetBytes(matrix.Rotation[lane]));
        }
        staging = staging.Write(address + 20, BitConverter.GetBytes(matrix.X)).Write(address + 24, BitConverter.GetBytes(matrix.Y)).Write(address + 28, BitConverter.GetBytes(matrix.Z));
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Duel215 body requires its actual projected CPU workspace maximum.");
        staging = staging.Write(address + 36, BitConverter.GetBytes(checked(0x196B018u - (uint)maximum * 8)));
        staging = staging.Write(address + 52, BitConverter.GetBytes((short)0)).Write(address + 54, BitConverter.GetBytes((short)0)).Write(address + 56, BitConverter.GetBytes((short)320)).Write(address + 58, BitConverter.GetBytes((short)216));
        staging = staging.Write(address + 60, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Native Duel215 body requires its actual model color40."))).Write(address + 64, BitConverter.GetBytes(model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Duel215 body requires its actual object mask124."))).Write(address + 72, new byte[4]);
        (byte[] bytes, bool[] known) = staging.ReadSnapshot(0x1D969A8, 4);
        for (int offset = 0; offset < 4; offset++)
        {
            staging = known[offset] ? staging.Write(address + 48 + (uint)offset, bytes.AsSpan(offset, 1)) : staging.MarkUnknown(address + 48 + (uint)offset, 1);
        }
        NativeFileStaging = staging;
        graphics = DrawNativeEffectModel5099D0(graphics, index, matrix, projection, 33, 6, 6);
        staging = NativeFileStaging ?? throw new InvalidDataException("Native Duel215 body lost its temporary owner during drawing.");
        model = graphics.Models[index];
        if (model.LinkedStagedResourceRow is not null || model.LinkedTextureResourceIndex is not null)
        {
            staging = staging.Write(address + 64, BitConverter.GetBytes(uint.MaxValue));
        }
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 76);
        return graphics;
    }

    private NativeBattleGraphicsState DrawNativeDuel215Body69D200(NativeBattleGraphicsState graphics, int index, NativeBattleActionMatrix view, NativeBattleVector angles, NativeBattleVector pivot, NativeBattleCameraProjection projection)
    {
        NativeBattleActionMatrix world = graphics.Models[index].GeometryWorldMatrix ?? throw new NotSupportedException("Native Duel215 transformed body requires its actual model world matrix.");
        NativeBattleActionMatrix matrix = NativeBattleDuel212Body.Matrix6A2F60(world, view, angles, pivot);
        return DrawNativeEffectModelContext92(graphics, index, matrix, pivot, projection, 33, 4, 4);
    }
}

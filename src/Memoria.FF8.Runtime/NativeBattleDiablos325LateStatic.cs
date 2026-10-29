using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325LateStatic
{
    public static NativeBattleDiablos325BodyDraw Draw654DDC(NativeBattleDiablos325Frame frame, ushort counter, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (counter is < 360 or > 367)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos late model requires its streamed geometry.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos late model requires its current camera basis.");
        int age = counter - 360;
        short sine = unchecked((short)NativeBattleCameraMath.Sine(512));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(512));
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, cosine, sine, 0, unchecked((short)-sine), cosine], 0, (age - 14) << 9, unchecked(state.FarZ51DC - ((age + 4) << 9)), 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
        ImmutableArray<byte> geometry = NativeBattleDiablos325Wings.ReadGeometry(arena, checked(state.ModelBuffer5208 + 0x35F7C));
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(geometry, 0, default, matrix, projection, new(0x30, null, null, null, null, null, 17, 2), colors, pilotIdentity: NativeBattleDiablos325VisualGeometry.Identity(frame, 0x654DDC));
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos late model requires its temporary memory owner.");
        process = process.PublishTemporary5082B0Matrix(matrix);
        return new(frame.Memory with { PrimitiveProcess = process }, batch);
    }
}

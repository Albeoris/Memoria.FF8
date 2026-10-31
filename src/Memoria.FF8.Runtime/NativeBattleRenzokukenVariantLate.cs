namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenVariantController
{
    private static (NativeBattleRenzokuken160Frame Frame, NativeBattleEffectTaskPool Pool) PrepareLate(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, NativeBattleRenzokukenVariantProfile profile, NativeBattleRenzokuken160Inputs inputs)
    {
        NativeBattleRenzokukenVariantState state = ReadState(frame);
        switch (profile.Effect)
        {
            case 141:
                int first141 = Append(0x61F590);
                int scale141 = (Rand() % 2560 + 7168) / 3;
                Set(first141, 28, scale141);
                Set(first141, 30, scale141);
                break;
            case 259:
                int first259 = Append(0x5A8340);
                int scale259 = (Rand() % 3072 + 7424) / 3;
                Set(first259, 28, scale259);
                Set(first259, 30, scale259);
                break;
            case 332:
                int first332 = Append(0x58D130);
                Set(first332, 24, Rand() % 4096);
                Set(first332, 28, 8192);
                int scale332 = (Rand() % 1536 + 4608) / 3;
                Set(first332, 32, scale332);
                Set(first332, 34, scale332);
                int second332 = Append(0x58D2D0);
                Set(second332, 24, Rand() % 4096);
                int secondScale332 = (Rand() % 1536 + 4096) / 3;
                Set(second332, 28, secondScale332);
                Set(second332, 30, secondScale332);
                break;
            case 333:
                int first333 = Append(0x58B3A0);
                int scale333 = (Rand() % 3072 + 9216) / 3;
                Set(first333, 28, scale333);
                Set(first333, 30, scale333);
                int second333 = Append(0x58B520);
                int secondScale333 = (Rand() % 1536 + 8192) / 3;
                Set(second333, 28, secondScale333);
                Set(second333, 30, secondScale333);
                break;
            default:
                throw new NotSupportedException("Native Renzokuken late controller has no original implementation.");
        }
        state = state with { LateOrigin = state.Core.Origin, LateDirection = state.Core.Direction };
        if (profile.HasContactOffsets)
        {
            state = state with { LateMatrix = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native Renzokuken late controller requires its actual captured actor matrix64.") };
        }
        return (Store(frame, state), pool);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        int Append(uint callback)
        {
            (pool, int slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, 0));
            return slot;
        }
        void Set(int slot, int offset, int value)
        {
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(offset, unchecked((ushort)value)));
        }
    }

    public static NativeBattleRenzokuken160ChildStep Advance5A8340(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleRenzokukenVariantState state = ReadState(frame);
        NativeBattleActionMatrix body = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native5A8340 requires the current actor matrix64.");
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(body, new(0, -800, 0));
        NativeBattleActionMatrix world = body with { X = offset.X + state.LateOrigin.X, Y = offset.Y + state.LateOrigin.Y, Z = offset.Z + state.LateOrigin.Z };
        return DrawLate(frame, pool, slot, inputs, resources.Meshes[2], world, view, projection, colors, 28, 30, 4, 341, 16);
    }

    public static NativeBattleRenzokuken160ChildStep Advance58D130(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        return Draw332(frame, pool, slot, inputs, resources, view, projection, colors, false);
    }

    public static NativeBattleRenzokuken160ChildStep Advance58D2D0(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        return Draw332(frame, pool, slot, inputs, resources, view, projection, colors, true);
    }

    private static NativeBattleRenzokuken160ChildStep Draw332(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool second)
    {
        NativeBattleRenzokukenVariantState state = ReadState(frame);
        NativeBattleActionMatrix body = state.LateMatrix ?? throw new NotSupportedException("Native58D130/58D2D0 requires the matrix captured by its actual controller.");
        _ = NativeBattleGfMatrices.RotateShort56C4F0(body, new(0, 0, -600));
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short angle = unchecked((short)task.ReadWord(24));
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Multiply56C090(body, NativeBattleGfMatrices.Euler56CE30(new(0, angle, 0))) with { X = state.LateOrigin.X, Y = 0, Z = state.LateOrigin.Z };
        if (!second)
        {
            int scale = unchecked((short)task.ReadWord(28));
            world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, unchecked((short)task.ReadWord(32)), scale);
        }
        return DrawLate(frame, pool, slot, inputs, resources.Meshes[second ? 4 : 2], world, view, projection, colors, second ? 28 : 32, second ? 30 : 34, 4, 409, 14, second);
    }

    public static NativeBattleRenzokuken160ChildStep Advance58B3A0(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        return Draw333(frame, pool, slot, inputs, resources, view, projection, colors, false);
    }

    public static NativeBattleRenzokuken160ChildStep Advance58B520(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        return Draw333(frame, pool, slot, inputs, resources, view, projection, colors, true);
    }

    private static NativeBattleRenzokuken160ChildStep Draw333(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool second)
    {
        NativeBattleRenzokukenVariantState state = ReadState(frame);
        NativeBattleActionMatrix body = state.LateMatrix ?? throw new NotSupportedException("Native58B3A0/58B520 requires the matrix captured by its actual controller.");
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(body, new(0, 400, -900));
        NativeBattleActionMatrix world = body with { X = offset.X + state.LateOrigin.X, Y = offset.Y + state.LateOrigin.Y, Z = offset.Z + state.LateOrigin.Z };
        return DrawLate(frame, pool, slot, inputs, resources.Meshes[second ? 4 : 2], world, view, projection, colors, 28, 30, second ? 6 : 4, 341, second ? 18 : 16);
    }

    private static NativeBattleRenzokuken160ChildStep DrawLate(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix world, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int scaleOffset, int speedOffset, int fadeStart, int fadeStep, int lifetime, bool applyUniformScale = true)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (applyUniformScale)
        {
            int scale = unchecked((short)task.ReadWord(scaleOffset));
            world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale);
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleModuleMeshContext context = new(counter >= fadeStart ? 0xF3u : 0x33u, 0, counter >= fadeStart ? fadeStep * (counter - fadeStart) : null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, projection, context, colors, pilotIdentity: inputs.CapturePresentation && frame.Effect.Binding.Effect is 259 or 332 or 333 ? NativeBattleRenzokukenVisualLifetimes.Task(frame.Effect, task, mesh.Address) : null);
        frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(packets));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = unchecked((short)task.ReadWord(speedOffset));
        task = task.WriteWord(scaleOffset, unchecked((ushort)(task.ReadWord(scaleOffset) + speed))).WriteWord(12, unchecked((ushort)(counter + 1))).WriteWord(speedOffset, unchecked((ushort)(speed - speed / 4)));
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= lifetime ? 2 : 0);
    }
}

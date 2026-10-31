using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep AdvanceLateSpecialChild(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, Func<byte, NativeBattleModuleAnchor> readAnchor, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattleSummonColors colors, short cameraRoll, NativeBattleSearchSpriteResource? commonSprite3 = null)
    {
        uint callback = pool.Slots[slot].Callback;
        ushort effect = frame.Effect.Binding.Effect;
        NativeBattleRenzokukenSpecialProfile profile = NativeBattleRenzokukenSpecialProfiles.ForEffect(effect);
        if (callback == profile.ControllerCallback)
        {
            return AdvanceLateController(frame, pool, slot, inputs, resources);
        }
        (uint trail, uint spiral, uint contact) = LateCallbacks(effect);
        if (callback == trail)
        {
            return AdvanceSpecialTrail(frame, pool, slot, inputs, view, projection, registers, colors);
        }
        if (callback == spiral)
        {
            return NativeBattleRenzokuken160Controller.Advance5FFFE0(frame, pool, slot, inputs);
        }
        if (callback == contact)
        {
            if (effect == 342)
            {
                return Advance578A00(frame, pool, slot, inputs, resources);
            }
            uint impact = effect switch
            {
                339 => 0x57FCB0,
                340 => 0x57D3D0,
                341 => 0x57B050,
                343 => 0x576BE0,
                _ => throw new InvalidDataException()
            };
            return NativeBattleRenzokuken160Controller.Advance600110(frame, pool, slot, inputs, resources, impact, index => ReadLateContactOrigin(frame, inputs, resources, index));
        }
        if (callback is 0x57FCB0 or 0x57D3D0 or 0x57B050 or 0x578DF0 or 0x576BE0)
        {
            return NativeBattleRenzokuken160Controller.Advance6004A0(frame, pool, slot, inputs, resources, view, projection, colors);
        }
        return callback switch
        {
            0x57FF90 => AdvanceStoredBurst(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll, 16),
            0x576EC0 => AdvanceStoredBurst(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll, 14),
            0x580310 => DrawStoredAligned(frame, pool, slot, inputs, resources, view, projection, colors, 0xC9FA74, resources.SpecialAlignmentAxis, 6, 409, 16, 28, 3),
            0x5804A0 => DrawStoredAligned(frame, pool, slot, inputs, resources, view, projection, colors, 0xCA1C2C, resources.SpecialAlignmentAxis, 6, 409, 16, 32, 2),
            0x57D6B0 => DrawStoredAligned(frame, pool, slot, inputs, resources, view, projection, colors, 0xC99D2C, resources.ImpactAxis, 6, 409, 16, 28, 3),
            0x57D980 => DrawStoredAligned(frame, pool, slot, inputs, resources, view, projection, colors, 0xC98C04, resources.SpecialAlignmentAxis, 6, 409, 16, 28, 3),
            0x57B330 => DrawStoredAligned(frame, pool, slot, inputs, resources, view, projection, colors, 0xC91DF4, resources.SpecialAlignmentAxis, 8, 409, 18, 28, 3),
            0x579440 => DrawStoredEuler(frame, pool, slot, inputs, resources, view, projection, colors, false, readAnchor),
            0x5795C0 => DrawStoredEuler(frame, pool, slot, inputs, resources, view, projection, colors, true, readAnchor),
            0x57D840 or 0x57DB10 or 0x577240 => DrawLateDirectMesh(frame, pool, slot, inputs, resources, view, projection, colors),
            0x57DC60 => AdvanceTargetSprites(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll, 29, 14, 3, 100, 25, scaleRange: 2176, scaleBase: 1792),
            0x57DF60 => Advance340Burst(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll),
            0x57B520 => Advance57B520(frame, pool, slot, inputs, commonSprite3 ?? throw new NotSupportedException("Native Renzokuken341 requires its shared sprite3."), readAnchor, view, projection, cameraRoll),
            0x57B880 => Advance57B880(frame, pool, slot, inputs),
            0x5790D0 => Advance5790D0(frame, pool, slot, inputs, resources.SpecialSprites[0], view, projection, cameraRoll),
            _ => throw new NotSupportedException($"Native special Renzokuken callback{callback:X} requires its original contract.")
        };
    }

    private static NativeBattleRenzokukenContactOrigin ReadLateContactOrigin(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, int index)
    {
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        ImmutableArray<NativeBattleVector> offsets = resources.ContactOffsets ?? state.ContactOffsets;
        NativeBattleVector value = offsets[index];
        NativeBattleActionMatrix matrix = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native special Renzokuken contact requires its current actor matrix64.");
        return new(AddLate(state.Core.Origin, NativeBattleGfMatrices.RotateShort56C4F0(matrix, value)), null);
    }

    private static NativeBattleRenzokuken160ChildStep DrawStoredAligned(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint mesh, ImmutableArray<int> axis, int fadeStart, int fadeStep, int lifetime, int scaleOffset, int mode)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        byte[] bank = ReadState(frame).ExtraStorage.ToArray();
        int offset = StoredOffset(task);
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(axis[0], axis[1], axis[2], StoredWord(bank, offset + 24), StoredWord(bank, offset + 26), StoredWord(bank, offset + 28));
        NativeBattleActionMatrix matrix = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), StoredWord(bank, offset + 8), StoredWord(bank, offset + 10), StoredWord(bank, offset + 12), 4096);
        int xzScale = scaleOffset == 32 ? unchecked((short)task.ReadWord(28)) : unchecked((short)task.ReadWord(scaleOffset));
        int yScale = unchecked((short)task.ReadWord(scaleOffset));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, xzScale, yScale, xzScale);
        return FinishLateMesh(frame, pool, slot, inputs, resources, view, projection, colors, matrix, mesh, fadeStart, fadeStep, lifetime, scaleOffset, mode, offset);
    }

    private static NativeBattleRenzokuken160ChildStep DrawStoredEuler(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool downward, Func<byte, NativeBattleModuleAnchor> readAnchor)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        byte[] bank = state.ExtraStorage.ToArray();
        int offset = StoredOffset(task);
        short angle = readAnchor(state.Core.Actor).W;
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(downward ? (short)1024 : (short)-1024, angle, 0));
        rotation = rotation with { X = StoredWord(bank, offset + 8), Y = StoredWord(bank, offset + 10), Z = StoredWord(bank, offset + 12) };
        int scale = unchecked((short)task.ReadWord(28));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale);
        int lifetime = downward ? unchecked((short)task.ReadWord(26)) : 16;
        return FinishLateMesh(frame, pool, slot, inputs, resources, view, projection, colors, matrix, 0xC8AF84, downward ? lifetime - 12 : 6, downward ? 341 : 409, lifetime, 28, downward ? 2 : 3, offset);
    }

    private static NativeBattleRenzokuken160ChildStep DrawLateDirectMesh(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleRenzokukenSpecialState state = ReadState(frame);
        uint callback = task.Callback;
        NativeBattleActionMatrix matrix;
        uint mesh;
        int scaleOffset;
        int fadeStart;
        int lifetime;
        if (callback == 0x57D840)
        {
            matrix = inputs.ReadModel(state.Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native special Renzokuken mesh requires its current actor matrix64.");
            mesh = 0xC9B9EC;
            scaleOffset = 28;
            fadeStart = 6;
            lifetime = 16;
        }
        else if (callback == 0x57DB10)
        {
            matrix = NativeBattleGfMatrices.Euler56CE30(new(0, unchecked((short)task.ReadWord(24)), 0));
            mesh = 0xC9CF24;
            scaleOffset = 32;
            fadeStart = 8;
            lifetime = 18;
        }
        else
        {
            NativeBattleActionMatrix captured = state.CapturedMatrix ?? throw new NotSupportedException("Native Renzokuken343 requires its controller-captured actor matrix64.");
            matrix = NativeBattleGfMatrices.Multiply56C090(captured, NativeBattleGfMatrices.Euler56CE30(new(0, 0, 1024)));
            mesh = 0xC852AC;
            scaleOffset = 28;
            fadeStart = 6;
            lifetime = 18;
        }
        matrix = matrix with { X = unchecked((short)task.ReadWord(16)), Y = unchecked((short)task.ReadWord(18)), Z = unchecked((short)task.ReadWord(20)) };
        int xzScale = scaleOffset == 32 ? unchecked((short)task.ReadWord(28)) : unchecked((short)task.ReadWord(scaleOffset));
        int yScale = unchecked((short)task.ReadWord(scaleOffset));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, xzScale, yScale, xzScale);
        return FinishLateMesh(frame, pool, slot, inputs, resources, view, projection, colors, matrix, mesh, fadeStart, callback == 0x577240 ? 341 : 409, lifetime, scaleOffset, 2, null);
    }

    private static NativeBattleRenzokuken160ChildStep FinishLateMesh(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleActionMatrix world, uint mesh, int fadeStart, int fadeStep, int lifetime, int scaleOffset, int mode, int? storedRow)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleModuleMeshContext context = new(counter >= fadeStart ? 0xF3u : 0x33u, 0, counter >= fadeStart ? fadeStep * (counter - fadeStart) : null, null, null, null, 17, checked((byte)mode));
        NativeBattleStaticMeshResource resource = resources.Meshes.Single(value => value.Address == mesh);
        NativeBattlePilotVisualIdentity? identity = inputs.CapturePresentation ? NativeBattleRenzokukenVisualLifetimes.Task(frame.Effect, task, resource.Address) : null;
        if (identity is { } owner && storedRow is int stored)
        {
            NativeBattlePilotVisualIdentity rowIdentity = NativeBattleRenzokukenVisualLifetimes.Particle(frame.Effect, 3, stored / 32, resource.Address) ?? throw new InvalidDataException("Stored mesh presentation requires its admitted physical row.");
            identity = owner with { ResourceGeneration = rowIdentity.ResourceGeneration };
        }
        NativeBattleGfGeometryBatch drawn = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, context, colors, pilotIdentity: identity);
        frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(drawn));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = unchecked((short)task.ReadWord(scaleOffset + 2));
        task = task.WriteWord(scaleOffset, unchecked((ushort)(task.ReadWord(scaleOffset) + speed))).WriteWord(scaleOffset + 2, unchecked((ushort)(speed - speed / 4))).WriteWord(12, unchecked((ushort)(counter + 1)));
        int result = unchecked((short)task.ReadWord(12)) < lifetime ? 0 : 2;
        if (result == 2 && storedRow is int row)
        {
            byte[] storage = ReadState(frame).ExtraStorage.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(storage.AsSpan(row), 0);
            frame = Store(frame, ReadState(frame) with { ExtraStorage = storage.ToImmutableArray() });
        }
        return new(frame, pool.WriteSlot(slot, task), result);
    }
}

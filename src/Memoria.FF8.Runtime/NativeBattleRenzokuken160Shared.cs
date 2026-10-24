using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken160Controller
{
    public static NativeBattleModuleCallback AdvanceShared5FF4E0(NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs, uint streakCallback = 0x5FF770, bool skipUnavailable = false)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(modules, false, []);
        }
        NativeBattleModuleTask task = modules.Slots[slot];
        byte[] payload = task.Payload.ToArray();
        short counter = Read(payload, 0);
        short actor = Read(payload, 14);
        NativeCrtRandomState? random = inputs.CrtRandom;
        if (counter is >= 4 and <= 8)
        {
            NativeBattleModuleAnchor original = (inputs.ReadModelAnchor ?? throw new NotSupportedException("Native5FF4E0 requires actual571400 anchor coordinates."))(actor);
            NativeBattleVector anchor = new(original.X, original.W, original.Z);
            for (int emitted = 0; emitted < 6; emitted++)
            {
                if (skipUnavailable && modules.Slots.All(candidate => (candidate.Flags & 1) != 0))
                {
                    continue;
                }
                (modules, int child) = modules.AppendCallback(streakCallback);
                byte[] bytes = modules.Slots[child].Payload.ToArray();
                Write(bytes, 0, 0);
                Write(bytes, 2, emitted + Rand() % 6);
                byte bones = (inputs.ReadModelBoneCount ?? throw new NotSupportedException("Native5FF4E0 requires actual body bone-count BYTE."))(actor);
                int bone = bones == 0 ? 0 : Rand() % bones;
                NativeBattleVector point = (inputs.ReadModelBone ?? throw new NotSupportedException("Native5FF4E0 requires actual502170 body-bone positions."))(actor, (byte)bone, 0);
                short x = unchecked((short)(point.X + Rand() % 175 - 87));
                short y = unchecked((short)(point.Y + Rand() % 175 - 87));
                short z = unchecked((short)(point.Z + Rand() % 175 - 87));
                if (y > -600)
                {
                    y = unchecked((short)(y - 600));
                }
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x - anchor.X, y - anchor.Y, z - anchor.Z);
                direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked(direction.X * 4), direction.Y, unchecked(direction.Z * 4));
                int distance = Rand() % 600 + 350;
                Write(bytes, 4, x + (unchecked(direction.X * distance) >> 12));
                Write(bytes, 6, y + (unchecked(direction.Y * distance) >> 12));
                Write(bytes, 8, z + (unchecked(direction.Z * distance) >> 12));
                Write(bytes, 12, bone);
                Write(bytes, 14, actor);
                Write(bytes, 16, Rand() % 8);
                Write(bytes, 20, Rand() % 1664 + 128);
                modules = modules with { Slots = modules.Slots.SetItem(child, modules.Slots[child] with { Payload = bytes.ToImmutableArray() }) };
            }
        }
        if (counter == 1)
        {
            modules = modules.AppendModelEffect(actor, 3);
        }
        ushort next = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(payload) + 1));
        Write(payload, 0, next);
        modules = modules with { Slots = modules.Slots.SetItem(slot, modules.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
        return new(modules, next >= 30, []) { CrtRandomAfter = random };

        int Rand()
        {
            random = (random ?? throw new NotSupportedException("Native5FF4E0 requires the retained CRT random owner.")).Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleModuleCallback AdvanceShared5FF770(NativeBattleModuleState modules, int slot, NativeBattleModuleInputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleVector referencePoint, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Action<NativeBattleGfGeometryBatch> draw)
    {
        NativeBattleModuleTask task = modules.Slots[slot];
        byte[] payload = task.Payload.ToArray();
        short delay = Read(payload, 2);
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        if (delay > 0)
        {
            if (advance)
            {
                Write(payload, 2, delay - 1);
                modules = modules with { Slots = modules.Slots.SetItem(slot, task with { Payload = payload.ToImmutableArray() }) };
            }
            return new(modules, false, []);
        }
        short counter = Read(payload, 0);
        byte bone = checked((byte)Read(payload, 12));
        int actor = Read(payload, 14);
        NativeBattleVector point = (inputs.ReadModelBone ?? throw new NotSupportedException("Native5FF770 requires actual502170 body-bone positions."))(actor, bone, unchecked((short)(counter << 9)));
        var horizontal = NativeBattleActionGeometryRules.NormalizeVector56BC50(referencePoint.X - point.X, 0, referencePoint.Z - point.Z);
        NativeBattleModuleAxisAngle cameraAxis = NativeBattleModuleAlignment.Between571480(0, 0, -4096, horizontal.X, horizontal.Y, horizontal.Z);
        NativeBattleActionMatrix oriented = new(NativeBattleModuleAlignment.Rotation5714F0(cameraAxis.Angle, cameraAxis.Axis), 0, 0, 0, 4096);
        NativeBattleActionMatrix side = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)) with { X = 240 };
        side = side with { Rotation = NativeBattleGfMatrices.Multiply56C090(oriented, side).Rotation };
        int sideAngle = cameraAxis.Axis.Y < 0 ? counter << 6 : -64 * counter;
        NativeBattleActionMatrix turn = new(NativeBattleModuleAlignment.Rotation5714F0(sideAngle, new(unchecked((short)horizontal.X), unchecked((short)horizontal.Y), unchecked((short)horizontal.Z))), 0, 0, 0, 4096);
        side = side with { Rotation = NativeBattleGfMatrices.Multiply56C090(turn, side).Rotation };
        int dx = Read(payload, 4) - point.X;
        int dy = Read(payload, 6) - point.Y;
        int dz = Read(payload, 8) - point.Z;
        int squared = NativeBattleActionGeometryRules.SquaredMagnitude56BC50(dx, dy, dz);
        if (squared < 0)
        {
            throw new NotSupportedException("Native5FF770 squared distance reaches an unproved negative56BEC0 conversion.");
        }
        int distance = unchecked((int)(long)Math.Sqrt(squared));
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix world = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), point.X, point.Y, point.Z, 4096);
        world = NativeBattlePoseMatrices.Scale56BEF0(world, Read(payload, 20), unchecked(distance << 12) / 2000, Read(payload, 20));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        matrix = NativeBattlePoseMatrices.Compose56C2F0(matrix, side);
        int palette = Read(payload, 16);
        if ((uint)counter >= 8 || (uint)palette >= 8 || resources.StreakAgeOffsets.Length != 8 || resources.StreakPaletteOffsets.Length != 8)
        {
            throw new InvalidDataException("Native5FF770 requires its actual eight age and eight palette offsets.");
        }
        NativeBattleStaticMeshResource resource = resources.Meshes[3];
        NativeBattleModuleMeshContext context = new(4339, 0, resources.StreakAgeOffsets[counter], null, null, resources.StreakPaletteOffsets[palette], 17, 2);
        draw(NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, matrix, projection, context, colors));
        if (!advance)
        {
            return new(modules, false, []);
        }
        Write(payload, 16, palette + 1 >= 8 ? 0 : palette + 1);
        ushort nextCounter = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(payload) + 1));
        Write(payload, 0, nextCounter);
        modules = modules with { Slots = modules.Slots.SetItem(slot, task with { Payload = payload.ToImmutableArray() }) };
        return new(modules, nextCounter >= 8, []);
    }
}

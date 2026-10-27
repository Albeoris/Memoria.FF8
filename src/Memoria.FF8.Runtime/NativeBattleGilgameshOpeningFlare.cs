using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshOpeningFlareState([property: JsonRequired] ImmutableArray<byte> ScreenMatrix)
{
    public static NativeBattleGilgameshOpeningFlareState FromColdProcess() => new(new byte[32].ToImmutableArray());

    public void Validate()
    {
        if (ScreenMatrix.IsDefault || ScreenMatrix.Length != 32)
        {
            throw new InvalidDataException("Native593490 requires its current retained32-byte screen matrix.");
        }
    }
}

public sealed record NativeBattleGilgameshOpeningFlareProjection(NativeBattleGilgameshOpeningFlareState State, NativeBattleActionMatrix Matrix);
public sealed record NativeBattleGilgameshOpeningFlareStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleGilgameshOpeningFlareState State, ImmutableArray<NativeBattleModulePacketBatch> Packets, int Result);

/// <summary>5931E0/593250's opening flare and593490's partially written direct screen matrix.</summary>
public static class NativeBattleGilgameshOpeningFlare
{
    public static NativeBattleEffectTaskPool Initialize5931E0(NativeBattleEffectTaskPool pool, NativeBattleActionMatrix world)
    {
        if (pool.DescriptorAddress != 0x21FFD58 || pool.BankAddress != 0x21FFD68 || pool.Stride != 36 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native5931E0 requires its actual raw child task bank.");
        }
        pool.Validate();
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(world, new(0, -14000, 0));
        int slot;
        (pool, slot) = pool.Append(0x593250);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 0).WriteWord(16, unchecked((ushort)(rotated.X + (short)world.X))).WriteWord(18, unchecked((ushort)(rotated.Y + (short)world.Y))).WriteWord(20, unchecked((ushort)(rotated.Z + (short)world.Z))).WriteWord(32, 800).WriteWord(34, 800);
        return pool.WriteSlot(slot, task);
    }

    public static NativeBattleGilgameshOpeningFlareStep Step593250(NativeBattleModulePoolTask task, NativeBattleGilgameshOpeningFlareState state, NativeBattleGilgameshOpeningFlareResources resources, NativeBattleVector cameraEye, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short dqa, int dqb, uint graphicsFlags)
    {
        if (task is null || task.Callback != 0x593250 || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Native593250 requires its actual raw0x24-byte flare task.");
        }
        state.Validate();
        resources.Validate();
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(cameraEye.X - Signed(task, 16), cameraEye.Y - Signed(task, 18), cameraEye.Z - Signed(task, 20));
        NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix alignment = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096);
        int counter = Signed(task, 12);
        int scale = Signed(task, 32);
        int brightness = counter < 2 ? counter << 6 : counter < 10 ? 128 : 228 - 10 * counter;
        if ((counter & 1) != 0)
        {
            brightness >>= 1;
        }
        uint color = unchecked((uint)(((brightness << 8) | brightness) << 8));
        ImmutableArray<NativeBattleModulePacketBatch>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleModulePacketBatch>(8);
        foreach (NativeBattleGilgameshOpeningFlareRow row in resources.Rows)
        {
            int weight = unchecked(row.Weight * 2200) >> 12;
            NativeBattleVector local = new(unchecked((short)(unchecked(weight * (scale + 512)) >> 12)), unchecked((short)(unchecked(weight * (scale / 2 - 256)) >> 12)), unchecked((short)(unchecked(weight * (45 * counter - 3584)) >> 12)));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(alignment, local);
            NativeBattleVector point = new(unchecked((short)(rotated.X + Signed(task, 16))), unchecked((short)(rotated.Y + Signed(task, 18))), unchecked((short)(rotated.Z + Signed(task, 20))));
            NativeBattleGilgameshOpeningFlareProjection projected = Project593490(state, point, row.Size, 0, view, projection, dqa, dqb);
            state = projected.State;
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                Resource = resources.Sprite,
                SpriteFrame = resources.Sprite.ProjectedFrame571C80(row.Frame),
                Phase = unchecked((ushort)row.Frame),
                Anchor = new(0, 0, 0, 0),
                DepthOffset = 0,
                SpriteFlags = 4,
                SpriteColor = color,
                SpriteWorldRotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096]
            };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = projected.Matrix }, 0, 17, 2);
            packets.Add(batch);
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, state, packets.MoveToImmutable(), 0);
        }
        int velocity = Signed(task, 34);
        task = task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + velocity))).WriteWord(34, unchecked((ushort)(velocity - (velocity >> 4)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(task, state, packets.MoveToImmutable(), Signed(task, 12) >= 22 ? 2 : 0);
    }

    public static NativeBattleGilgameshOpeningFlareProjection Project593490(NativeBattleGilgameshOpeningFlareState state, NativeBattleVector point, short size, int depthOffset, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short dqa, int dqb)
    {
        state.Validate();
        ushort h = projection.FocalLength ?? throw new NotSupportedException("Native593490 requires its actual current projection H WORD.");
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FAA0(view, point, projection, dqa, dqb);
        int x = unchecked((short)projected.Screen) / 8 - 160;
        int y = unchecked((short)(projected.Screen >> 16)) / 8 - 108;
        byte[] bytes = state.ScreenMatrix.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes, size);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), size);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), size);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), x);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), unchecked((short)h + depthOffset));
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray();
        return new(new(bytes.ToImmutableArray()), new(rotation, x, y, unchecked((short)h + depthOffset), 4096));
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}

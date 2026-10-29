using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Lower
{
    public static NativeBattleEffect210Step AdvanceRibbon(NativeBattleEffect210Frame frame, uint address, NativeBattleEffect210Resources resources, NativeBattleEffect210Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        (frame, uint temporary) = Allocate(frame, 64);
        NativeBattleEffectArena context = Context(frame, temporary, 64);
        ImmutableArray<NativeBattlePackedMatrix32> bones = inputs.ReadCurrentBoneMatrices32(frame, 4);
        if (bones.Length <= 1)
        {
            throw new NotSupportedException("Effect210 ribbon requires its current companion bone1.");
        }
        NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, 4);
        (byte[] worldData, bool[] worldKnown) = model.ReadSnapshot(0x40, 32);
        NativeBattleActionMatrix world = new NativeBattlePackedMatrix32(worldData.ToImmutableArray(), worldKnown.ToImmutableArray()).ReadMatrix();
        NativeBattleActionMatrix bone = bones[1].ReadMatrix();
        var rotated = NativeBattleGfMatrices.RotateLong56C600(bone, 0, 0, -4096);
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(rotated.X, rotated.Y, rotated.Z);
        rotated = NativeBattleGfMatrices.RotateLong56C600(world, normalized.X, normalized.Y, normalized.Z);
        normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(rotated.X, rotated.Y, rotated.Z);
        context = Put(Put(Put(context, 0x30, unchecked((uint)normalized.X)), 0x34, unchecked((uint)normalized.Y)), 0x38, unchecked((uint)normalized.Z));
        NativeBattleVector local = new(unchecked((short)bone.X), unchecked((short)bone.Y), unchecked((short)bone.Z));
        NativeBattleVector transformed = NativeBattleGfMatrices.RotateShort56C4F0(world, local);
        NativeBattleVector first = new(unchecked((short)(transformed.X + world.X)), unchecked((short)(transformed.Y + world.Y)), unchecked((short)(transformed.Z + world.Z)));
        context = PutPoint(context, 0x20, first);
        NativeBattleVector second = new(unchecked((short)(first.X + ((normalized.X * 777) >> 12))), unchecked((short)(first.Y + ((normalized.Y * 777) >> 12))), unchecked((short)(first.Z + ((normalized.Z * 777) >> 12))));
        context = PutPoint(context, 0x28, second);
        NativeBattleEffectArena data = ReadBytes(frame, address, 0x34C);
        int ring = age % 7;
        data = Put(data, 0x10 + ring * 20, 1);
        data = CopyBytes(context, 0x20, data, 0x14 + ring * 20, 8);
        data = CopyBytes(context, 0x28, data, 0x1C + ring * 20, 8);
        NativeBattleEffect210RibbonBirths.Observe(frame.State.Storage, address, task, ring, age);
        int maximum = age >= 53 ? 60 - age : 7;
        int count = 0;
        for (; count < maximum; count++)
        {
            if (Dword(data, 0x10 + ring * 20) == 0)
            {
                break;
            }
            data = CopyBytes(data, 0x14 + ring * 20, data, 0x9C + count * 8, 8);
            data = CopyBytes(data, 0x1C + ring * 20, data, 0xDC + count * 8, 8);
            ring = ring == 0 ? 6 : ring - 1;
        }
        frame = WriteBytes(frame, address, data, 0x34C);
        if (count > 1)
        {
            (frame, uint workAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = Context(frame, workAddress, 400);
            for (int side = 0; side < 2; side++)
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, data, side == 0 ? 0x9C : 0xDC, work);
                int destination = side == 0 ? 0x11C : 0x21C;
                for (int sample = 0; sample < 32; sample++)
                {
                    data = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 31, data, destination + sample * 8);
                }
                frame = PublishContext(frame, workAddress, work, 400);
                frame = WriteBytes(frame, address, data, 0x34C);
            }
            frame = Release(frame, 400);
            data = CopyBytes(data, 0x21C, data, 0x31C, 8);
            data = CopyBytes(data, 0x11C, data, 0x324, 8);
            data = Direction(data, 0x21C, 0x224, 0x32C);
            data = Direction(data, 0x31C, 0x324, 0x33C);
            frame = WriteBytes(frame, address, data, 0x34C);
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleRibbonPair72Step result = NativeBattleRibbonPair72.DrawLoaded(frame.Memory, CopyBytes(data, 0x11C, NativeBattleEffectArena.Clear().MarkUnknown(0, 256), 0, 256), CopyBytes(data, 0x21C, NativeBattleEffectArena.Clear().MarkUnknown(0, 256), 0, 256), context, inputs.ReadProjection(frame), inputs.Colors, 0x3A185820, pilotIdentity: NativeBattleEffect210RibbonBirths.Identity(frame.State.Storage, address, task, age % 7, (age - count + 1) % 7));
            frame = frame with { Memory = result.Memory };
            context = result.Temporary64;
            frame = PublishContext(frame, temporary, context, 64);
            frame = inputs.Execute(frame, new NativeBattleEffect210DrawBatch(result.Batch, CursorGlobal));
        }
        else
        {
            frame = PublishContext(frame, temporary, context, 64);
        }
        frame = Release(frame, 64);
        if (Paused(frame))
        {
            return new(frame, 0);
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next))), next >= 59 ? 2 : 0);
    }

    private static NativeBattleEffectArena Direction(NativeBattleEffectArena data, int from, int to, int destination)
    {
        NativeBattleVector first = Vector(data, from);
        NativeBattleVector second = Vector(data, to);
        var result = NativeBattleActionGeometryRules.NormalizeVector56BC50(first.X - second.X, first.Y - second.Y, first.Z - second.Z);
        return Put(Put(Put(data, destination, unchecked((uint)result.X)), destination + 4, unchecked((uint)result.Y)), destination + 8, unchecked((uint)result.Z));
    }
}

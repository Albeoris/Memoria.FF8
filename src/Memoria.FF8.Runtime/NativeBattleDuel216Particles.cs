using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216ParticleBirth(NativeBattleDuel216Frame Frame, NativeBattleEffectTaskPool Pool);
public sealed record NativeBattleDuel216BillboardParticle(uint Source, NativeBattleVector Point, ushort Scale, short DepthDisplacement, short Phase);
public sealed record NativeBattleDuel216MeshParticle(uint Source, NativeBattleActionMatrix Basis120, NativeBattleActionMatrix Local88, short AngleX, short AngleY, short Scale);
public sealed record NativeBattleDuel216ParticleServices(Func<NativeBattleDuel216Frame, NativeBattleDuel216BillboardParticle, NativeBattleDuel216Frame> DrawBillboard571C80, Func<NativeBattleDuel216Frame, NativeBattleDuel216MeshParticle, NativeBattleDuel216Frame> DrawMesh69A410);

public static class NativeBattleDuel216Particles
{
    public static NativeBattleDuel216ParticleBirth Spawn699F80(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int count, NativeBattleVector point, int radialRange, int radialBase, int scaleRange, short scaleBase)
    {
        RequirePool(pool, false);
        RequireCount(count);
        for (int index = 0; index <= count; index++)
        {
            if (Full(pool))
            {
                break;
            }
            (pool, int slot) = pool.Append(0x69A060);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            short scale = unchecked((short)(scaleBase + (unchecked(scaleRange * Random(ref frame)) >> 15)));
            int radius = unchecked(radialBase + (unchecked(radialRange * Random(ref frame)) >> 15));
            ushort angle = Random(ref frame);
            int dx = unchecked(radius * NativeBattleCameraMath.Sine(angle)) >> 12;
            int dz = unchecked(radius * NativeBattleCameraMath.Cosine(angle)) >> 12;
            task = Position(task, new(unchecked((short)(point.X + dx)), point.Y, unchecked((short)(point.Z + dz)))).WriteWord(18, unchecked((ushort)scale));
            task = task.WriteByte(23, 0).WriteByte(22, unchecked((byte)(unchecked((uint)(radialBase * dz)) >> 16))).WriteWord(20, unchecked((byte)(unchecked((uint)(radialBase * dx)) >> 16)));
            pool = Store(pool, slot, task);
        }
        return new(frame, pool);
    }

    public static NativeBattleDuel216ParticleBirth Spawn69A110(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int count, NativeBattleVector displacement, Func<NativeBattleDuel216Frame, NativeBattleVector> readActorPosition)
    {
        RequirePool(pool, false);
        RequireCount(count);
        for (int index = 0; index <= count; index++)
        {
            if (Full(pool))
            {
                break;
            }
            (pool, int slot) = pool.Append(0x69A200);
            NativeBattleVector actor = readActorPosition(frame);
            NativeBattleModulePoolTask task = Position(pool.Slots[slot], new(unchecked((short)(displacement.X + actor.X)), unchecked((short)(displacement.Y + actor.Y)), unchecked((short)(displacement.Z + actor.Z)))).WriteWord(18, 2048).WriteByte(23, 0);
            sbyte vx = unchecked((sbyte)(Random(ref frame) + 128));
            sbyte vy = unchecked((sbyte)(128 - Random(ref frame)));
            sbyte vz = unchecked((sbyte)(Random(ref frame) + 128));
            task = task.WriteByte(20, unchecked((byte)vx)).WriteByte(21, unchecked((byte)vy)).WriteByte(22, unchecked((byte)vz));
            task = task.WriteByte(24, unchecked((byte)(vx / -8))).WriteByte(25, unchecked((byte)(vy / -8))).WriteByte(26, unchecked((byte)(vz / -8)));
            pool = Store(pool, slot, task);
        }
        return new(frame, pool);
    }

    public static NativeBattleDuel216ParticleBirth Spawn69A2D0(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int count, NativeBattleVector point, int lifeRange, sbyte lifeBase, int radialRange, int radialBase, int scaleRange, short scaleBase)
    {
        RequirePool(pool, true);
        RequireCount(count);
        for (int index = 0; index <= count; index++)
        {
            uint callback = (Random(ref frame) & 1) == 0 ? 0x69A530u : 0x69A3F0u;
            if (Full(pool))
            {
                break;
            }
            (pool, int slot) = pool.Append(callback);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteDword(24, 0).WriteByte(25, 20);
            short scale = unchecked((short)(scaleBase + (unchecked(scaleRange * Random(ref frame)) >> 15)));
            int radius = unchecked(radialBase + (unchecked(radialRange * Random(ref frame)) >> 15));
            ushort angle = Random(ref frame);
            int dx = unchecked(radius * NativeBattleCameraMath.Sine(angle)) >> 12;
            int dz = unchecked(radius * NativeBattleCameraMath.Cosine(angle)) >> 12;
            task = Position(task, new(unchecked((short)(point.X + dx)), point.Y, unchecked((short)(point.Z + dz))));
            task = task.WriteWord(32, unchecked((ushort)scale)).WriteByte(27, unchecked((byte)(lifeBase + (unchecked(lifeRange * Random(ref frame)) >> 15))));
            task = task.WriteWord(18, unchecked((ushort)(unchecked(radialBase * dx) >> 14))).WriteWord(22, unchecked((ushort)(unchecked(radialBase * dz) >> 14))).WriteWord(20, unchecked((ushort)(unchecked(-radius) >> 2)));
            for (int component = 0; component < 4; component++)
            {
                task = task.WriteByte(28 + component, unchecked((byte)Random(ref frame)));
            }
            pool = Store(pool, slot, task);
        }
        return new(frame, pool);
    }

    public static NativeBattleDuel216Step Advance69A060(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel216ParticleServices services) => AdvanceBillboard(frame, pool, slot, services, false);
    public static NativeBattleDuel216Step Advance69A200(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel216ParticleServices services) => AdvanceBillboard(frame, pool, slot, services, true);

    public static NativeBattleDuel216Step Advance69A410(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel216ParticleServices services)
    {
        RequirePool(pool, true);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback is not (0x69A3F0 or 0x69A530))
        {
            throw new InvalidDataException("Native69A410 requires one of its original wrapper callbacks.");
        }
        short angleX = unchecked((short)(unchecked((sbyte)task.ReadByte(28)) * 16));
        short angleY = unchecked((short)(unchecked((sbyte)task.ReadByte(29)) * 16));
        short scale = unchecked((short)task.ReadWord(32));
        NativeBattleActionMatrix basis = NativeBattleDuel215Controller.ParticleBasis6ED1E0(angleY, scale);
        int sine = NativeBattleCameraMath.Sine(angleX);
        int cosine = NativeBattleCameraMath.Cosine(angleX);
        NativeBattleActionMatrix pitch = new([4096, 0, 0, 0, unchecked((short)cosine), unchecked((short)sine), 0, unchecked((short)-sine), unchecked((short)cosine)], 0, 0, 0, 4096);
        NativeBattleVector point = Point(task);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(pitch, basis) with { X = point.X, Y = point.Y, Z = point.Z };
        frame = services.DrawMesh69A410(frame, new(0x1140AA4, basis, local, angleX, angleY, scale));
        byte life = unchecked((byte)(task.ReadByte(27) - 1));
        task = task.WriteByte(27, life);
        if (life == 0)
        {
            return new(frame, Store(pool, slot, task), 2);
        }
        for (int component = 0; component < 2; component++)
        {
            task = task.WriteByte(28 + component, unchecked((byte)(task.ReadByte(28 + component) + task.ReadByte(30 + component))));
        }
        for (int component = 0; component < 3; component++)
        {
            ushort velocity = task.ReadWord(18 + component * 2);
            task = task.WriteWord(12 + component * 2, unchecked((ushort)(task.ReadWord(12 + component * 2) + velocity)));
            task = task.WriteWord(18 + component * 2, unchecked((ushort)(velocity + unchecked((sbyte)task.ReadByte(24 + component)))));
        }
        return new(frame, Store(pool, slot, task), 0);
    }

    private static NativeBattleDuel216Step AdvanceBillboard(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleDuel216ParticleServices services, bool accelerating)
    {
        RequirePool(pool, false);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != (accelerating ? 0x69A200u : 0x69A060u))
        {
            throw new InvalidDataException("Native Duel216 billboard requires its actual callback.");
        }
        ushort scale = task.ReadWord(18);
        short phase = unchecked((sbyte)task.ReadByte(23));
        frame = services.DrawBillboard571C80(frame, new(accelerating ? 0x113C2F0u : 0x113C1ACu, Point(task), scale, unchecked((short)-(unchecked((short)scale) >> 2)), phase));
        for (int component = 0; component < 3; component++)
        {
            sbyte velocity = unchecked((sbyte)task.ReadByte(20 + component));
            task = task.WriteWord(12 + component * 2, unchecked((ushort)(task.ReadWord(12 + component * 2) + 4 * velocity)));
            if (accelerating)
            {
                task = task.WriteByte(20 + component, unchecked((byte)(velocity + task.ReadByte(24 + component))));
            }
        }
        sbyte next = unchecked((sbyte)(phase + 1));
        task = task.WriteByte(23, unchecked((byte)next));
        return new(frame, Store(pool, slot, task), next < (accelerating ? 8 : 12) ? 0 : 2);
    }

    private static NativeBattleEffectTaskPool Store(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool with { Slots = pool.Slots.SetItem(slot, task) };
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleVector point) => task.WriteWord(12, unchecked((ushort)point.X)).WriteWord(14, unchecked((ushort)point.Y)).WriteWord(16, unchecked((ushort)point.Z));
    private static NativeBattleVector Point(NativeBattleModulePoolTask task) => new(unchecked((short)task.ReadWord(12)), unchecked((short)task.ReadWord(14)), unchecked((short)task.ReadWord(16)));
    private static bool Full(NativeBattleEffectTaskPool pool) => pool.Slots.All(task => (task.Flags & 1) != 0);
    private static ushort Random(ref NativeBattleDuel216Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }
    private static void RequireCount(int count)
    {
        if (count < 0)
        {
            throw new InvalidDataException("Native Duel216 particle birth requires its actual nonnegative count.");
        }
    }
    private static void RequirePool(NativeBattleEffectTaskPool pool, bool mesh)
    {
        if (pool.DescriptorAddress != (mesh ? NativeBattleDuel216Controller.Particles : NativeBattleDuel216Controller.Small) || pool.BankAddress != NativeBattleDuel216Controller.Arena + (mesh ? 8208u : 10512u) || pool.Stride != (mesh ? 36 : 28) || pool.Slots.Length != (mesh ? 64 : 32))
        {
            throw new InvalidDataException("Native Duel216 particles require their actual descriptor and dimensions.");
        }
    }
}

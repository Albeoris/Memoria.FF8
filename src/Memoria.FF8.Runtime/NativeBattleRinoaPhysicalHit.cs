using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoaPhysicalHitState(byte Actor, byte Target, NativeBattleModuleAnchor TargetAnchor, NativeBattleVector Origin)
{
    public ImmutableArray<NativeBattleVector> Controls { get; init; } = [];
    public ImmutableArray<NativeBattleVector> Curve { get; init; } = [];
}

public sealed record NativeBattleRinoaPhysicalHitInputs(NativeBattleModuleInputs Modules, Func<(byte Actor, byte Target)> ReadAliases, Func<byte, ushort> ReadActorFlags, Action<byte, int, NativeBattleModuleAnchor> PlaySound, Func<bool> ReadSecondaryContact, Action ConsumeContact, Action<NativeBattleModuleDraw> DrawSprite);

/// <summary>56F9C0's shared physical-hit owner and56FCD0's eight sparks.</summary>
public static partial class NativeBattleRinoaPhysicalHit
{
    public static NativeBattleModuleCallback AdvanceRoot(NativeBattleModuleState state, int slot, NativeBattleRinoaPhysicalHitInputs inputs)
    {
        if ((inputs.Modules.GraphicsFlags & 1) != 0)
        {
            return new(state, false, []);
        }
        short age = Word(state.Slots[slot], 12);
        NativeCrtRandomState? random = inputs.Modules.CrtRandom;
        if (age == 0)
        {
            (byte actor, byte target) = inputs.ReadAliases();
            NativeBattleModuleAnchor anchor = (inputs.Modules.ReadModelAnchor ?? throw new NotSupportedException("Native56F9C0 requires its target571400 anchor."))(target);
            NativeBattleVector origin = (inputs.Modules.ReadModelBone ?? throw new NotSupportedException("Native56F9C0 requires its actor502170 bone21."))(actor, 21, 0);
            origin = origin with { Y = unchecked((short)(origin.Y - 50)) };
            state = state with { RinoaPhysicalHit = new(actor, target, anchor, origin) };
            (state, int projectile) = state.AppendCallback(0x56FE10);
            state = Store(state, projectile, task => task.WriteWord(12, 0).WriteWord(14, (ushort)(inputs.ReadActorFlags(actor) & 0x200)).WriteWord(24, 0).WriteWord(26, 3));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(origin.X - anchor.X, origin.Y - anchor.Y, origin.Z - anchor.Z);
            NativeCrtRandomState crt = random ?? throw new NotSupportedException("Native56F9C0 requires its retained CRT random stream.");
            for (int index = 0; index < 8; index++)
            {
                (state, int spark) = state.AppendCallback(0x56FCD0);
                crt = crt.Draw(out ushort speedRoll);
                int speed = speedRoll % 40 + 35;
                crt = crt.Draw(out ushort xRoll);
                crt = crt.Draw(out ushort yRoll);
                crt = crt.Draw(out ushort zRoll);
                crt = crt.Draw(out ushort scaleRoll);
                state = Store(state, spark, task => task.WriteWord(12, 0).WriteDword(16, Pack(origin.X, origin.Y)).WriteWord(20, unchecked((ushort)origin.Z)).WriteWord(22, BinaryPrimitives.ReadUInt16LittleEndian(state.ReadPhysicalModuleBytes(0x209D796, 2))).WriteWord(24, unchecked((ushort)(((direction.X * speed) >> 12) + xRoll % 40))).WriteWord(26, unchecked((ushort)(((direction.Y * speed) >> 12) + yRoll % 40))).WriteWord(28, unchecked((ushort)(((direction.Z * speed) >> 12) + zRoll % 40))).WriteWord(32, (ushort)(scaleRoll % 1024 + 2048)));
            }
            random = crt;
        }
        if (age == 11)
        {
            NativeBattleRinoaPhysicalHitState retained = RequireState(state);
            state = state.AppendModelResource(retained.Target, Resource(inputs.Modules, 20), 0x100014);
            inputs.PlaySound(retained.Actor, 1, retained.TargetAnchor);
            if (inputs.ReadSecondaryContact())
            {
                state = state.AppendModelResource(retained.Target, Resource(inputs.Modules, 21), 0x100015);
                inputs.PlaySound(retained.Actor, 2, retained.TargetAnchor);
            }
            inputs.ConsumeContact();
        }
        short next = unchecked((short)(age + 1));
        state = Store(state, slot, task => task.WriteWord(12, unchecked((ushort)next)));
        return new(state, next >= 35, []) { CrtRandomAfter = random };
    }

    public static NativeBattleModuleCallback AdvanceSpark(NativeBattleModuleState state, int slot, NativeBattleRinoaPhysicalHitInputs inputs)
    {
        HitTask task = Read(state, slot);
        bool paused = (inputs.Modules.GraphicsFlags & 1) != 0;
        short delay = Signed(task, 14);
        if (delay > 0)
        {
            if (!paused)
            {
                state = Store(state, slot, current => current.WriteWord(14, unchecked((ushort)(delay - 1))));
            }
            return new(state, false, []);
        }
        short age = Signed(task, 12);
        short scale = Signed(task, 32);
        NativeBattleModuleResource resource = Resource(inputs.Modules, 6);
        if (resource.Kind != 0)
        {
            resource = Resource(inputs.Modules, 0);
        }
        byte color = unchecked((byte)(-16 * age));
        ushort phase = unchecked((ushort)(age / 5 + 7));
        inputs.DrawSprite(new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(phase),
            Phase = phase,
            Anchor = new(Signed(task, 16), Signed(task, 18), Signed(task, 20), Signed(task, 22)),
            Scale = scale,
            DepthOffset = -(scale >> 4),
            SpriteFlags = age >= 8 ? (ushort)20 : (ushort)16,
            SpritePaletteOffsets = [4, 0, 0, 0],
            SpriteColor = age >= 8 ? (uint)(color | color << 8 | color << 16) : 0x808080
        });
        if (paused)
        {
            return new(state, false, []);
        }
        task = task.WriteWord(32, unchecked((ushort)(scale - 64)));
        for (int lane = 0; lane < 3; lane++)
        {
            short velocity = Signed(task, 24 + lane * 2);
            task = task.WriteWord(16 + lane * 2, unchecked((ushort)(Signed(task, 16 + lane * 2) + velocity))).WriteWord(24 + lane * 2, unchecked((ushort)(velocity - (velocity >> 3))));
        }
        short next = unchecked((short)(age + 1));
        task = task.WriteWord(12, unchecked((ushort)next));
        return new(Store(state, slot, _ => task), next >= 15, []);
    }

    private static NativeBattleRinoaPhysicalHitState RequireState(NativeBattleModuleState state) => state.RinoaPhysicalHit ?? throw new NotSupportedException("Native56FE10 requires its initialized56F9C0 process globals.");
    private static NativeBattleModuleResource Resource(NativeBattleModuleInputs inputs, byte id) => inputs.Resources.SingleOrDefault(resource => resource.Id == id) ?? throw new NotSupportedException($"Native Rinoa physical hit requires its original resource{id}.");
    private static uint Pack(short first, short second) => (uint)(unchecked((ushort)first) | unchecked((ushort)second) << 16);
    private static short Word(NativeBattleModuleTask task, int offset) => BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(offset - 12));
    private static short Signed(HitTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static HitTask Read(NativeBattleModuleState state, int slot) => new(state.Slots[slot].Payload);
    private sealed class HitTask(ImmutableArray<byte> payload)
    {
        private readonly byte[] bytes = payload.ToArray();
        public ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset - 12));
        public HitTask WriteWord(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset - 12), value);
            return this;
        }
        public HitTask WriteDword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset - 12), value);
            return this;
        }
        public ReadOnlySpan<byte> Read(int offset, int length) => bytes.AsSpan(offset - 12, length);
    }

    private static NativeBattleModuleState Store(NativeBattleModuleState state, int slot, Func<HitTask, HitTask> write)
    {
        HitTask task = write(Read(state, slot));
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = task.Read(12, 24).ToImmutableArray() }) };
    }
}

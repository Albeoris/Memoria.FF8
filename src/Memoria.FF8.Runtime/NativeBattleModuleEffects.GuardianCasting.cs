using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleState
{
    private NativeBattleModuleCallback AdvanceGuardianCastingSeed(int slot, NativeBattleModuleInputs inputs)
    {
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, []);
        }
        NativeBattleModuleState state = this;
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = NativeBattleTelemetry.Materialize(1072, task.Payload.AsSpan());
        short phase = S16(task.Payload, 0);
        int model = ModelIndex(U32(task.Payload, 20));
        NativeBattleModelTransform transform = (inputs.ReadModelTransform ?? throw new NotSupportedException("Native5742A0 requires the original model WORD36/38."))(model);
        NativeCrtRandomState crt = inputs.CrtRandom ?? throw new NotSupportedException("Native5742A0 requires the existing CRT random owner.");
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        if (phase == 0)
        {
            NativeBattleModuleAnchor anchor = (inputs.ReadModelAnchor ?? throw new NotSupportedException("Native5742A0 requires the actual571400 anchor."))(model);
            WriteAnchor(payload.AsSpan(4), anchor);
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 7 });
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 8 });
        }
        if (phase is >= 1 and <= 20 && (phase & 3) == 1)
        {
            int scale = Math.Min(32767, unchecked(transform.Scale38 * 7572) >> 12);
            (state, int ring) = state.Append(NativeBattleModuleKind.GuardianCastingRing574AE0);
            byte[] bytes = NativeBattleTelemetry.Materialize(1073, state.Slots[ring].Payload.AsSpan());
            W(bytes, 0, 0);
            NativeBattleTelemetry.Copy(1080, payload.AsSpan(4, 8), bytes.AsSpan(4));
            W(bytes, 6, transform.Angle36);
            W(bytes, 12, Draw(ref crt) % 2048);
            int speed = Draw(ref crt) % 10 + 80;
            W(bytes, 14, (S16(task.Payload, 2) & 1) != 0 ? -speed : speed);
            W(bytes, 16, scale);
            W(bytes, 18, scale / 5);
            W(bytes, 20, 0);
            W(bytes, 22, scale / 2);
            state = state.StoreCastingPayload(ring, bytes);
            W(payload, 2, S16(task.Payload, 2) + 1);
        }
        if (phase == 1)
        {
            int scale = Math.Min(32767, unchecked(transform.Scale38 * 9011) >> 12);
            (state, int ground) = state.Append(NativeBattleModuleKind.GuardianCastingGround574990);
            byte[] bytes = NativeBattleTelemetry.Materialize(1074, state.Slots[ground].Payload.AsSpan());
            W(bytes, 0, 0);
            NativeBattleTelemetry.Copy(1081, payload.AsSpan(4, 8), bytes.AsSpan(4));
            W(bytes, 6, transform.Angle36);
            W(bytes, 16, scale * 2);
            W(bytes, 18, unchecked((short)(scale * 2)) / 4);
            state = state.StoreCastingPayload(ground, bytes);
            int radius = unchecked(transform.Scale38 * 2650) >> 12;
            int angle = Draw(ref crt) % 2048;
            for (int index = 0; index < 5; index++)
            {
                (state, int particle) = state.Append(NativeBattleModuleKind.GuardianCastingParticle574670);
                bytes = NativeBattleTelemetry.Materialize(1075, state.Slots[particle].Payload.AsSpan());
                W(bytes, 0, 0);
                W(bytes, 2, Draw(ref crt) % 6);
                NativeBattleTelemetry.Copy(1082, payload.AsSpan(4, 8), bytes.AsSpan(4));
                W(bytes, 6, transform.Angle36 - 500);
                W(bytes, 10, -38);
                W(bytes, 20, angle + index * 819);
                W(bytes, 22, radius);
                state = state.StoreCastingPayload(particle, bytes);
                for (int trailIndex = 0; trailIndex < 3; trailIndex++)
                {
                    (state, int trail) = state.Append(NativeBattleModuleKind.GuardianCastingTrail574580);
                    bytes = NativeBattleTelemetry.Materialize(1076, state.Slots[trail].Payload.AsSpan());
                    W(bytes, 0, 0);
                    W(bytes, 2, trailIndex);
                    W(bytes, 10, -1);
                    W(bytes, 18, -1);
                    W(bytes, 20, particle);
                    state = state.StoreCastingPayload(trail, bytes);
                }
            }
        }
        W(payload, 0, phase + 1);
        return new(state.StoreCastingPayload(slot, payload), unchecked((short)(phase + 1)) > 30, draws.ToImmutable()) { CrtRandomAfter = crt };
    }

    private NativeBattleModuleCallback AdvanceGuardianCastingRing(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        short phase = S16(task.Payload, 0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = ReadAnchor(task.Payload, 4), MeshId = 4, MeshRotation = new(0, S16(task.Payload, 12), 0), MeshScale = new(S16(task.Payload, 16), S16(task.Payload, 20), S16(task.Payload, 16)), MeshClut = 0x3867, MeshFlags = phase >= 8 ? 0x2F0u : 0x230u, MeshFade = phase >= 8 ? (phase - 8) * 341 : 0 };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = NativeBattleTelemetry.Materialize(1077, task.Payload.AsSpan());
        short speed = S16(task.Payload, 14);
        short heightSpeed = S16(task.Payload, 22);
        W(payload, 12, S16(task.Payload, 12) + speed);
        W(payload, 16, S16(task.Payload, 16) + S16(task.Payload, 18));
        W(payload, 14, speed - (speed >> 4));
        W(payload, 20, S16(task.Payload, 20) + (phase < 5 ? heightSpeed : -(heightSpeed / 2)));
        W(payload, 22, heightSpeed - (heightSpeed >> 3));
        W(payload, 0, phase + 1);
        return new(StoreCastingPayload(slot, payload), unchecked((short)(phase + 1)) > 20, [draw]);
    }

    private NativeBattleModuleCallback AdvanceGuardianCastingParticle(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = NativeBattleTelemetry.Materialize(1078, task.Payload.AsSpan());
        short phase = S16(task.Payload, 0);
        bool paused = (inputs.GraphicsFlags & 1) != 0;
        NativeCrtRandomState crt = inputs.CrtRandom ?? throw new NotSupportedException("Native574670 requires the existing CRT random owner.");
        NativeBattleModuleAnchor anchor;
        if (phase < 22)
        {
            NativeBattleVector point = NativeBattlePoseMatrices.RotateVector(new(0, S16(task.Payload, 20), 0), new(0, 0, unchecked((short)-S16(task.Payload, 22))));
            W(payload, 12, point.X + S16(task.Payload, 4));
            W(payload, 14, point.Y + S16(task.Payload, 6));
            W(payload, 16, point.Z + S16(task.Payload, 8));
            NativeBattleVector predicted = NativeBattlePoseMatrices.RotateVector(new(0, unchecked((short)(S16(task.Payload, 20) + 120)), 0), new(0, 0, unchecked((short)-S16(task.Payload, 22))));
            anchor = new(unchecked((short)(predicted.X + S16(task.Payload, 4))), unchecked((short)(predicted.Y + S16(task.Payload, 6) + S16(task.Payload, 10))), unchecked((short)(predicted.Z + S16(task.Payload, 8))), 0);
            if (!paused)
            {
                short speed = S16(task.Payload, 10);
                W(payload, 6, S16(task.Payload, 6) + speed);
                if (speed < 0)
                {
                    W(payload, 10, speed + 1);
                }
                W(payload, 20, S16(task.Payload, 20) + 120);
            }
        }
        else
        {
            if (!paused)
            {
                if (phase == 22)
                {
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(S16(task.Payload, 12) - S16(task.Payload, 4), -100 - Draw(ref crt) % 300, S16(task.Payload, 16) - S16(task.Payload, 8));
                    int speed = Draw(ref crt) % 350 + 350;
                    W(payload, 4, unchecked(speed * direction.X) >> 12);
                    W(payload, 6, unchecked(speed * direction.Y) >> 12);
                    W(payload, 8, unchecked(speed * direction.Z) >> 12);
                }
                W(payload, 12, S16(task.Payload, 12) + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(4)));
                W(payload, 14, S16(task.Payload, 14) + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(6)));
                W(payload, 16, S16(task.Payload, 16) + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(8)));
            }
            anchor = new(unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(12)) + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(4)))), unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(14)) + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(6)))), unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(16)) + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(8)))), 0);
        }
        ushort sprite = unchecked((ushort)(7 - S16(task.Payload, 2) % 7));
        int color = phase < 8 ? unchecked((byte)(phase << 4)) : phase >= 22 ? unchecked((byte)(224 - (phase << 4))) : 128;
        NativeBattleModuleDraw draw = GuardianCastingSprite(inputs, anchor, sprite, phase < 8 || phase >= 22 ? (uint)(color * 0x10101) : 0x808080u, phase < 8 || phase >= 22 ? (ushort)4 : (ushort)0);
        //574670's unobserved stack padding is not a position coordinate;WORD1E is overwritten by this frame cache before any child reads it.
        W(payload, 18, sprite - 1 < 0 ? 7 : sprite - 1);
        if (!paused)
        {
            if (phase >= 22)
            {
                for (int offset = 4; offset <= 8; offset += 2)
                {
                    short speed = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset));
                    W(payload, offset, speed + (speed >> 3));
                }
            }
            W(payload, 2, S16(task.Payload, 2) + 1);
            W(payload, 0, phase + 1);
        }
        return new(StoreCastingPayload(slot, payload), !paused && unchecked((short)(phase + 1)) > 30, [draw]) { CrtRandomAfter = crt };
    }

    private NativeBattleModuleCallback AdvanceGuardianCastingTrail(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        int parentIndex = S16(task.Payload, 20);
        if ((uint)parentIndex >= 300)
        {
            throw new InvalidDataException("Native574580 parent pointer leaves its physical300-slot bank.");
        }
        NativeBattleModuleTask parent = Slots[parentIndex];
        if (S16(parent.Payload, 18) < 0)
        {
            return new(this, false, []);
        }
        byte[] payload = NativeBattleTelemetry.Materialize(1079, task.Payload.AsSpan());
        NativeBattleTelemetry.Copy(1083, task.Payload.AsSpan(4, 8), payload.AsSpan(12));
        NativeBattleTelemetry.Copy(1084, parent.Payload.AsSpan(12, 8), payload.AsSpan(4));
        NativeBattleModuleAnchor anchor = new(S16(parent.Payload, 12), S16(parent.Payload, 14), S16(parent.Payload, 16), 0);
        byte color = unchecked((byte)(72 - unchecked((sbyte)task.Payload[2]) * 24));
        NativeBattleModuleDraw draw = GuardianCastingSprite(inputs, anchor, unchecked((ushort)BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(10))), (uint)(color * 0x10101), 4);
        if ((inputs.GraphicsFlags & 1) == 0)
        {
            W(payload, 0, S16(task.Payload, 0) + 1);
        }
        short next = BinaryPrimitives.ReadInt16LittleEndian(payload);
        return new(StoreCastingPayload(slot, payload), (inputs.GraphicsFlags & 1) == 0 && next > 30 - S16(task.Payload, 2), [draw]);
    }

    private static NativeBattleModuleDraw GuardianCastingSprite(NativeBattleModuleInputs inputs, NativeBattleModuleAnchor anchor, ushort phase, uint color, ushort flags)
    {
        NativeBattleModuleResource resource = inputs.Resources.SingleOrDefault(resource => resource.Id == 4) ?? throw new NotSupportedException("Native guardian casting requires its original5710B0 resource4.");
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = anchor, Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Phase = phase, Scale = 1376, DepthOffset = -172, SpriteFlags = flags, SpriteColor = color };
    }
}

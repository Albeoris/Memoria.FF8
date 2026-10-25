using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleInputs
{
    public Func<int, NativeBattleModelTransform>? ReadModelTransform { get; init; }
    public Func<int, byte>? ReadModelBoneCount { get; init; }
}

public sealed partial record NativeBattleModuleDraw
{
    public byte MeshId { get; init; }
    public NativeBattleVector? MeshRotation { get; init; }
    public NativeBattleVector? MeshScale { get; init; }
    public uint MeshFlags { get; init; }
    public ushort MeshClut { get; init; }
    public int MeshFade { get; init; }
}

public sealed partial record NativeBattleModuleState
{
    public static (byte Index, uint GraphicsFlags) SelectModelEffect(byte operand, uint graphicsFlags) => (graphicsFlags & 0x40000000) != 0 ? ((byte)1, graphicsFlags & ~0x40000000u) : (operand, graphicsFlags);

    public NativeBattleModuleState AppendModelEffect(int model, byte effect)
    {
        if ((uint)model >= 7)
        {
            throw new InvalidDataException("Native571840 requires its physical graphics model pointer.");
        }
        if (effect is not (0 or 1 or 2 or 3 or 4 or 5))
        {
            throw new NotSupportedException($"Native571840 callback selector{effect} requires its original C816A4 implementation.");
        }
        NativeBattleModuleKind kind = effect switch { 0 => NativeBattleModuleKind.CastingSeed574C80, 1 => NativeBattleModuleKind.GuardianCastingSeed5742A0, 2 => NativeBattleModuleKind.SpectralCastingSeed5737C0, 3 => NativeBattleModuleKind.SpectralCastingSeed574260, 4 => NativeBattleModuleKind.BoneSpriteSeed573560, 5 => NativeBattleModuleKind.BoneSpriteLargeSeed5736E0, _ => throw new InvalidOperationException() };
        (NativeBattleModuleState state, int slot) = Append(kind);
        byte[] payload = NativeBattleTelemetry.Materialize(1057, state.Slots[slot].Payload.AsSpan());
        W(payload, 0, 0);
        W(payload, 2, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), ModelAddress(model));
        return state.StoreCastingPayload(slot, payload);
    }

    private NativeBattleModuleCallback AdvanceCastingSeed(int slot, NativeBattleModuleInputs inputs)
    {
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, []);
        }
        NativeBattleModuleState state = this;
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = NativeBattleTelemetry.Materialize(1058, task.Payload.AsSpan());
        short phase = S16(task.Payload, 0);
        NativeCrtRandomState crt = inputs.CrtRandom ?? throw new NotSupportedException("Native574C80 requires the existing CRT random owner.");
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        if (phase == 0)
        {
            int model = ModelIndex(U32(task.Payload, 20));
            NativeBattleModelTransform transform = (inputs.ReadModelTransform ?? throw new NotSupportedException("Native574C80 requires the original model WORD36/38."))(model);
            NativeBattleModuleAnchor anchor = (inputs.ReadModelAnchor ?? throw new NotSupportedException("Native574C80 requires its actual571400 anchor."))(model);
            WriteAnchor(payload.AsSpan(4), anchor);
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 5 });
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 6 });
            int scale = Math.Min(32767, unchecked(transform.Scale38 * 9011) >> 12);
            for (int index = 0; index < 5; index++)
            {
                (state, int child) = state.Append(NativeBattleModuleKind.CastingRing5753A0);
                byte[] bytes = NativeBattleTelemetry.Materialize(1059, state.Slots[child].Payload.AsSpan());
                W(bytes, 0, 0);
                W(bytes, 2, index * 3);
                WriteAnchor(bytes.AsSpan(4), anchor);
                W(bytes, 6, transform.Angle36);
                W(bytes, 12, Draw(ref crt) % 2048);
                W(bytes, 16, scale + index * (scale / 5));
                W(bytes, 20, 0);
                W(bytes, 22, scale / (8 + index * 2) - index * 8);
                state = state.StoreCastingPayload(child, bytes);
            }
            (state, int ground) = state.Append(NativeBattleModuleKind.CastingGround575250);
            byte[] groundBytes = NativeBattleTelemetry.Materialize(1060, state.Slots[ground].Payload.AsSpan());
            W(groundBytes, 0, 0);
            WriteAnchor(groundBytes.AsSpan(4), anchor);
            W(groundBytes, 6, transform.Angle36);
            W(groundBytes, 16, scale * 2);
            W(groundBytes, 18, unchecked((short)(scale * 2)) / 4);
            state = state.StoreCastingPayload(ground, groundBytes);
            int radius = Math.Min(32767, unchecked(transform.Scale38 * 10012) >> 12);
            radius = unchecked(radius * 900) >> 12;
            for (int index = 0; index < 6; index++)
            {
                (state, int child) = state.Append(NativeBattleModuleKind.CastingParticle574F50);
                byte[] bytes = NativeBattleTelemetry.Materialize(1061, state.Slots[child].Payload.AsSpan());
                W(bytes, 0, 0);
                W(bytes, 2, Draw(ref crt) % 6 + index);
                WriteAnchor(bytes.AsSpan(4), anchor);
                W(bytes, 6, transform.Angle36 - 400 - Draw(ref crt) % 150);
                W(bytes, 10, -(Draw(ref crt) % 40) - (40 + index * 4));
                W(bytes, 12, (Draw(ref crt) % 400 + 400) * index);
                W(bytes, 14, Draw(ref crt) % 200 + 80);
                W(bytes, 16, 4096);
                W(bytes, 20, radius);
                W(bytes, 22, 0);
                state = state.StoreCastingPayload(child, bytes);
            }
        }
        W(payload, 0, phase + 1);
        return new(state.StoreCastingPayload(slot, payload), unchecked((short)(phase + 1)) > 30, draws.ToImmutable()) { CrtRandomAfter = crt };
    }

    private NativeBattleModuleCallback AdvanceCastingRing(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = NativeBattleTelemetry.Materialize(1062, task.Payload.AsSpan());
        short delay = S16(task.Payload, 2);
        if (delay > 0)
        {
            if ((inputs.GraphicsFlags & 1) == 0)
            {
                W(payload, 2, delay - 1);
            }
            return new(StoreCastingPayload(slot, payload), false, []);
        }
        short phase = S16(task.Payload, 0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = ReadAnchor(task.Payload, 4), MeshId = 1, MeshRotation = new(0, S16(task.Payload, 12), 0), MeshScale = new(S16(task.Payload, 16), S16(task.Payload, 20), S16(task.Payload, 16)), MeshFlags = phase >= 5 ? 0x2F0u : 0x230u, MeshClut = 0x3867, MeshFade = phase >= 5 ? (phase - 5) * 409 : 0 };
        if ((inputs.GraphicsFlags & 1) == 0)
        {
            short speed = S16(task.Payload, 22);
            W(payload, 20, S16(task.Payload, 20) + speed);
            W(payload, 22, speed + (speed >> 5));
            W(payload, 0, phase + 1);
        }
        return new(StoreCastingPayload(slot, payload), (inputs.GraphicsFlags & 1) == 0 && unchecked((short)(phase + 1)) > 15, [draw]);
    }

    private NativeBattleModuleCallback AdvanceCastingGround(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = NativeBattleTelemetry.Materialize(1063, task.Payload.AsSpan());
        short phase = S16(task.Payload, 0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = ReadAnchor(task.Payload, 4), MeshId = 7, MeshRotation = new(0, 0, 0), MeshScale = new(S16(task.Payload, 16), 4096, S16(task.Payload, 16)), MeshFlags = phase >= 0 ? 0xF3u : 0x33u, MeshFade = phase >= 0 ? phase * 292 : 0 };
        if ((inputs.GraphicsFlags & 1) == 0)
        {
            short delta = S16(task.Payload, 18);
            W(payload, 16, S16(task.Payload, 16) + delta);
            W(payload, 18, delta - delta / 8);
            W(payload, 0, phase + 1);
        }
        return new(StoreCastingPayload(slot, payload), (inputs.GraphicsFlags & 1) == 0 && unchecked((short)(phase + 1)) > 14, [draw]);
    }

    private NativeBattleModuleCallback AdvanceCastingParticle(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = NativeBattleTelemetry.Materialize(1064, task.Payload.AsSpan());
        short delay = S16(task.Payload, 2);
        if (delay > 0)
        {
            if ((inputs.GraphicsFlags & 1) == 0)
            {
                W(payload, 2, delay - 1);
            }
            return new(StoreCastingPayload(slot, payload), false, []);
        }
        NativeBattleVector offset = NativeBattlePoseMatrices.RotateVector(new(0, S16(task.Payload, 12), 0), new(0, 0, unchecked((short)-S16(task.Payload, 20))));
        NativeBattleModuleAnchor anchor = ReadAnchor(task.Payload, 4);
        anchor = anchor with { X = unchecked((short)(anchor.X + offset.X)), Y = unchecked((short)(anchor.Y + offset.Y)), Z = unchecked((short)(anchor.Z + offset.Z)) };
        NativeBattleModuleDraw draw = CastingSprite(inputs, anchor, S16(task.Payload, 16), unchecked((ushort)S16(task.Payload, 22)));
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        NativeBattleModuleState state = this;
        NativeCrtRandomState crt = inputs.CrtRandom ?? throw new NotSupportedException("Native574F50 requires the existing CRT random owner.");
        short phase = S16(task.Payload, 0);
        if (phase is >= 1 and <= 12)
        {
            for (int index = 0; index < 3; index++)
            {
                (state, int child) = state.Append(NativeBattleModuleKind.CastingTrail5751A0);
                byte[] bytes = NativeBattleTelemetry.Materialize(1065, state.Slots[child].Payload.AsSpan());
                W(bytes, 0, Draw(ref crt) % 3 + 4);
                WriteAnchor(bytes.AsSpan(4), anchor);
                W(bytes, 4, anchor.X + 30 - Draw(ref crt) % 60);
                W(bytes, 6, anchor.Y + 30 - Draw(ref crt) % 60);
                W(bytes, 8, anchor.Z + 30 - Draw(ref crt) % 60);
                W(bytes, 10, S16(task.Payload, 10) >> 2);
                W(bytes, 16, Draw(ref crt) % 800 + 2112);
                state = state.StoreCastingPayload(child, bytes);
            }
        }
        short velocity = S16(task.Payload, 10);
        short radius = S16(task.Payload, 20);
        W(payload, 6, S16(task.Payload, 6) + velocity);
        W(payload, 10, velocity + (velocity >> 5));
        W(payload, 12, S16(task.Payload, 12) + S16(task.Payload, 14));
        W(payload, 20, radius + (radius >> 6));
        short sprite = S16(task.Payload, 22);
        if (phase >= 12)
        {
            sprite++;
        }
        W(payload, 22, sprite > 9 ? 0 : sprite);
        W(payload, 0, phase + 1);
        return new(state.StoreCastingPayload(slot, payload), unchecked((short)(phase + 1)) > 18, [draw]) { CrtRandomAfter = crt };
    }

    private NativeBattleModuleCallback AdvanceCastingTrail(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        NativeBattleModuleDraw draw = CastingSprite(inputs, ReadAnchor(task.Payload, 4), S16(task.Payload, 16), unchecked((ushort)(9 - S16(task.Payload, 0))));
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = NativeBattleTelemetry.Materialize(1066, task.Payload.AsSpan());
        short velocity = S16(task.Payload, 10);
        short next = unchecked((short)(S16(task.Payload, 0) - 1));
        W(payload, 16, S16(task.Payload, 16) - 64);
        W(payload, 6, S16(task.Payload, 6) + velocity);
        W(payload, 10, velocity + (velocity >> 4));
        W(payload, 0, next);
        return new(StoreCastingPayload(slot, payload), next < 0, [draw]);
    }

    private static NativeBattleModuleDraw CastingSprite(NativeBattleModuleInputs inputs, NativeBattleModuleAnchor anchor, short scale, ushort phase)
    {
        NativeBattleModuleResource resource = inputs.Resources.SingleOrDefault(resource => resource.Id == 5) ?? throw new NotSupportedException("Native casting requires its original5710B0 resource5.");
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = anchor, Scale = scale, DepthOffset = -(scale >> 3), Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Phase = phase, SpriteFlags = 0 };
    }

    private NativeBattleModuleState StoreCastingPayload(int slot, byte[] payload) => this with { Slots = Slots.SetItem(slot, Slots[slot] with { Payload = NativeBattleTelemetry.Immutable(1067, payload) }) };
    private static void W(byte[] payload, int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset), unchecked((short)value));
    private static ushort Draw(ref NativeCrtRandomState crt)
    {
        crt = crt.Draw(out ushort value);
        return value;
    }
}

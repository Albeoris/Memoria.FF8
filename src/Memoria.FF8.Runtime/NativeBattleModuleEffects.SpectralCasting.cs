using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleState
{
    private NativeBattleModuleCallback AdvanceSpectralCastingSeed(int slot, ushort material)
    {
        (NativeBattleModuleState state, int child) = Append(NativeBattleModuleKind.SpectralCasting573800);
        byte[] payload = state.Slots[child].Payload.ToArray();
        W(payload, 0, 0);
        W(payload, 2, 0);
        W(payload, 16, material);
        Slots[slot].Payload.AsSpan().Slice(20, 4).CopyTo(payload.AsSpan(20));
        return new(state.StoreCastingPayload(child, payload), true, []);
    }

    private NativeBattleModuleCallback AdvanceSpectralCasting(int slot, NativeBattleModuleInputs inputs)
    {
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, []);
        }
        NativeBattleModuleTask task = Slots[slot];
        byte[] payload = task.Payload.ToArray();
        short phase = S16(task.Payload, 0);
        int model = ModelIndex(U32(task.Payload, 20));
        NativeBattleModuleState state = this;
        NativeCrtRandomState? random = inputs.CrtRandom;
        ImmutableArray<NativeBattleModuleDraw>.Builder draws = ImmutableArray.CreateBuilder<NativeBattleModuleDraw>();
        if (phase == 0)
        {
            NativeBattleModuleAnchor anchor = (inputs.ReadModelAnchor ?? throw new NotSupportedException("Native573800 requires its current571400 model anchor."))(model);
            WriteAnchor(payload.AsSpan(4), anchor);
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 22 });
            draws.Add(new(NativeBattleModuleDrawKind.Sound, 0) { Anchor = anchor, Sound = 23 });
        }
        NativeBattleModelTransform? transform = null;
        if (phase is >= 0 and <= 2)
        {
            transform = (inputs.ReadModelTransform ?? throw new NotSupportedException("Native573800 requires its actual modelWORD24/26."))(model);
        }
        if (phase == 1)
        {
            NativeBattleModelTransform currentTransform = transform ?? throw new InvalidDataException("Native573800 phase1 lacks its just-read model transform.");
            int scale = Math.Min(32767, unchecked(currentTransform.Scale38 * 9011) >> 12);
            (state, int column) = state.Append(NativeBattleModuleKind.SpectralCastingColumn573F40);
            byte[] bytes = state.Slots[column].Payload.ToArray();
            W(bytes, 0, 0);
            W(bytes, 2, 0);
            payload.AsSpan(4, 8).CopyTo(bytes.AsSpan(4));
            W(bytes, 6, currentTransform.Angle36);
            W(bytes, 12, Rand() % 2048);
            W(bytes, 14, Rand() % 20 + 120);
            W(bytes, 16, 0);
            W(bytes, 18, scale);
            W(bytes, 20, 0);
            W(bytes, 22, scale / 9);
            state = state.StoreCastingPayload(column, bytes);

            int radialGrowth = scale / 6;
            short half = unchecked((short)(scale / 2));
            for (int ring = 0; ring < 4; ring++)
            {
                (state, int child) = state.Append(NativeBattleModuleKind.SpectralCastingRing5740D0);
                bytes = state.Slots[child].Payload.ToArray();
                W(bytes, 2, ring * 4);
                payload.AsSpan(4, 8).CopyTo(bytes.AsSpan(4));
                W(bytes, 0, 0);
                W(bytes, 6, currentTransform.Angle36);
                W(bytes, 10, S16(task.Payload, 16));
                W(bytes, 12, Rand() % 300 + ring * 300);
                W(bytes, 16, 0);
                W(bytes, 18, radialGrowth);
                W(bytes, 20, 0);
                W(bytes, 22, half);
                int value = Rand();
                int divisor = half / 3;
                if (divisor == 0)
                {
                    throw new InvalidDataException("Native573800 reaches its actual zero signed ring-scale divisor.");
                }
                W(bytes, 22, unchecked(value % divisor + half));
                state = state.StoreCastingPayload(child, bytes);
            }

            (state, int ground) = state.Append(NativeBattleModuleKind.SpectralCastingGround573DF0);
            bytes = state.Slots[ground].Payload.ToArray();
            payload.AsSpan(4, 8).CopyTo(bytes.AsSpan(4));
            W(bytes, 6, currentTransform.Angle36);
            W(bytes, 0, 0);
            short doubled = unchecked((short)(scale * 2));
            W(bytes, 16, doubled);
            W(bytes, 18, doubled / 4);
            state = state.StoreCastingPayload(ground, bytes);
        }
        if (phase is >= 0 and <= 2)
        {
            for (int index = 0; index < 20; index++)
            {
                (state, int particle) = state.Append(NativeBattleModuleKind.SpectralCastingParticle573C40);
                byte[] bytes = state.Slots[particle].Payload.ToArray();
                W(bytes, 2, index);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Rand() % 4096 - 2048, Rand() % 4096 - 2048, Rand() % 4096 - 2048);
                int radius = Rand() % 1000 + 1500;
                short x = unchecked((short)(radius * direction.X >> 12));
                short y = unchecked((short)(radius * direction.Y >> 12));
                short z = unchecked((short)(radius * direction.Z >> 12));
                int life = Rand() % 5 + 6;
                W(bytes, 10, 5);
                W(bytes, 0, life);
                W(bytes, 18, life);
                W(bytes, 12, -(x / life / 2));
                W(bytes, 14, -(y / life / 2));
                W(bytes, 16, -(z / life / 2));
                W(bytes, 4, x + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(4)));
                W(bytes, 6, y + transform!.Angle36 - 800);
                W(bytes, 8, z + BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(8)));
                short scale = unchecked((short)(Rand() % 4096 + 6400));
                W(bytes, 20, scale);
                W(bytes, 22, scale / 10);
                state = state.StoreCastingPayload(particle, bytes);
            }
        }
        short next = unchecked((short)(phase + 1));
        W(payload, 0, next);
        return new(state.StoreCastingPayload(slot, payload), next > 30, draws.ToImmutable()) { CrtRandomAfter = random };

        int Rand()
        {
            NativeCrtRandomState current = random ?? throw new NotSupportedException("Native573800 requires its existing55CBD2 CRT owner.");
            int value = Draw(ref current);
            random = current;
            return value;
        }
    }

    private NativeBattleModuleCallback AdvanceSpectralCastingParticle(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        if (S16(task.Payload, 2) > 0)
        {
            if ((inputs.GraphicsFlags & 1) != 0)
            {
                return new(this, false, []);
            }
            byte[] waiting = task.Payload.ToArray();
            W(waiting, 2, S16(task.Payload, 2) - 1);
            return new(StoreCastingPayload(slot, waiting), false, []);
        }
        NativeBattleModuleResource resource = inputs.Resources.SingleOrDefault(resource => resource.Id == 6) ?? throw new NotSupportedException("Native573C40 requires original5710B0 resource6.");
        ImmutableArray<short> aligned = NativeBattleModuleAlignment.DownToVelocity573C40(S16(task.Payload, 12), S16(task.Payload, 14), S16(task.Payload, 16));
        ImmutableArray<short> rotation = NativeBattlePoseMatrices.ScaleRotation56BEF0(aligned, new(S16(task.Payload, 22), S16(task.Payload, 20), 4096));
        ushort phase = unchecked((ushort)S16(task.Payload, 10));
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = ReadAnchor(task.Payload, 4), Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Phase = phase, Scale = 4096, DepthOffset = 0, SpriteWorldRotation = rotation, SpriteFlags = 0x210, SpritePaletteOffsets = [4, 0, 0, 0] };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        W(payload, 4, S16(task.Payload, 4) + S16(task.Payload, 12));
        W(payload, 6, S16(task.Payload, 6) + S16(task.Payload, 14));
        W(payload, 8, S16(task.Payload, 8) + S16(task.Payload, 16));
        short nextFrame = unchecked((short)(S16(task.Payload, 10) + 1));
        W(payload, 10, nextFrame > S16(task.Payload, 18) ? 0 : nextFrame);
        short life = unchecked((short)(S16(task.Payload, 0) - 1));
        W(payload, 0, life);
        return new(StoreCastingPayload(slot, payload), life <= 0, [draw]);
    }

    private NativeBattleModuleCallback AdvanceSpectralCastingGround(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        short phase = S16(task.Payload, 0);
        short scale = S16(task.Payload, 16);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = ReadAnchor(task.Payload, 4), MeshId = 7, MeshRotation = new(0, 0, 0), MeshScale = new(scale, 4096, scale), MeshFlags = phase < 0 ? 0x33u : 0xF3u, MeshFade = phase < 0 ? 0 : phase * 292 };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        short growth = S16(task.Payload, 18);
        W(payload, 16, scale + growth);
        W(payload, 18, growth - growth / 8);
        short next = unchecked((short)(phase + 1));
        W(payload, 0, next);
        return new(StoreCastingPayload(slot, payload), next > 14, [draw]);
    }

    private NativeBattleModuleCallback AdvanceSpectralCastingColumn(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        short phase = S16(task.Payload, 0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = ReadAnchor(task.Payload, 4), MeshId = 2, MeshRotation = new(0, S16(task.Payload, 12), 0), MeshScale = new(S16(task.Payload, 16), S16(task.Payload, 20), S16(task.Payload, 16)), MeshClut = 0x3AE7, MeshFlags = phase < 10 ? 0x230u : 0x2F0u, MeshFade = phase < 10 ? 0 : (phase - 10) << 8 };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        short speed = S16(task.Payload, 14);
        short growth = S16(task.Payload, 18);
        short vertical = S16(task.Payload, 22);
        W(payload, 12, S16(task.Payload, 12) + speed);
        W(payload, 14, Math.Max(1, (int)unchecked((short)(speed - 5))));
        W(payload, 16, S16(task.Payload, 16) + growth);
        W(payload, 18, growth - growth / 5);
        W(payload, 20, S16(task.Payload, 20) + vertical);
        W(payload, 22, vertical - (vertical >> 6));
        short next = unchecked((short)(phase + 1));
        W(payload, 0, next);
        return new(StoreCastingPayload(slot, payload), next > 26, [draw]);
    }

    private NativeBattleModuleCallback AdvanceSpectralCastingRing(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        if (S16(task.Payload, 2) > 0)
        {
            if ((inputs.GraphicsFlags & 1) != 0)
            {
                return new(this, false, []);
            }
            byte[] waiting = task.Payload.ToArray();
            W(waiting, 2, S16(task.Payload, 2) - 1);
            return new(StoreCastingPayload(slot, waiting), false, []);
        }
        short phase = S16(task.Payload, 0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Mesh, 0) { Anchor = ReadAnchor(task.Payload, 4), MeshId = 3, MeshRotation = new(0, S16(task.Payload, 12), 0), MeshScale = new(S16(task.Payload, 16), S16(task.Payload, 20), S16(task.Payload, 16)), MeshClut = unchecked((ushort)S16(task.Payload, 10)), MeshFlags = phase < 4 ? 0x233u : 0x2F3u, MeshFade = phase < 4 ? 0 : (phase - 4) * 409 };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        short vertical = S16(task.Payload, 22);
        W(payload, 16, S16(task.Payload, 16) + S16(task.Payload, 18));
        W(payload, 20, S16(task.Payload, 20) + vertical);
        W(payload, 22, vertical - vertical / 3);
        short next = unchecked((short)(phase + 1));
        W(payload, 0, next);
        return new(StoreCastingPayload(slot, payload), next > 14, [draw]);
    }
}

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38ActorBounceStep(NativeBattleGraphicsModel Model, NativeBattleQuake38SavedActor Saved, NativeCrtRandomState Random, int? ModelSequence);

public static class NativeBattleQuake38ActorBounce
{
    public static NativeBattleQuake38ActorBounceStep Advance8DBCA0(NativeBattleGraphicsModel model, NativeBattleQuake38SavedActor saved, NativeBattleQuake38State state, NativeBattleQuake38Resources resources, NativeCrtRandomState random, bool actualActionTarget)
    {
        NativeBattleVector original = saved.Position28 ?? throw new NotSupportedException("Native Quake target bounce requires its saved initialized position bytes.");
        byte zone = NativeBattleQuake38Zones.Read8D9A80(resources.ZoneMap, original.X, original.Z, state.Word(0x278C8E8), state.Word(0x278C8E6), state.Word(0x2792D6E));
        if (zone >= 10)
        {
            throw new InvalidDataException("Native Quake target bounce leaves its ten actual wave rows.");
        }
        short height = state.Word(checked(0x278C912u + (uint)zone * 16));
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Quake target bounce requires its current initialized actor transform.");
        NativeBattleVector current = transform.Position28;
        if (saved.BouncePhase == 0)
        {
            if (current.Y >= height)
            {
                random = random.Draw(out ushort rx);
                random = random.Draw(out ushort ry);
                random = random.Draw(out ushort rz);
                random = random.Draw(out ushort x);
                random = random.Draw(out ushort z);
                model = model with
                {
                    Rotation12 = Jitter(saved.Rotation12 ?? throw new NotSupportedException("Native Quake jitter requires saved rotation X."), rx),
                    Rotation14 = Jitter(saved.Rotation14 ?? throw new NotSupportedException("Native Quake jitter requires saved rotation Y."), ry),
                    Rotation16 = Jitter(saved.Rotation16 ?? throw new NotSupportedException("Native Quake jitter requires saved rotation Z."), rz),
                    Transform = transform with { Position28 = new(Jitter(original.X, x), height, Jitter(original.Z, z)) }
                };
            }
            else
            {
                saved = saved with { Velocity = -48, BouncePhase = unchecked((short)(saved.BouncePhase + 1)) };
                model = model with { Rotation12 = saved.Rotation12, Rotation14 = saved.Rotation14, Rotation16 = saved.Rotation16, RotationPadding18 = saved.RotationPadding18, Transform = transform with { Position28 = new(original.X, current.Y, original.Z) } };
            }
            return new(model, saved, random, null);
        }
        if (saved.BouncePhase != 1)
        {
            return new(model, saved, random, null);
        }
        short velocity = unchecked((short)(saved.Velocity + 48));
        short y = unchecked((short)(current.Y + velocity));
        saved = saved with { Velocity = velocity };
        int? sequence = null;
        if (y >= height)
        {
            y = height;
            saved = saved with { Velocity = 0, BouncePhase = unchecked((short)(saved.BouncePhase - 1)) };
            if ((model.Flags0 & 2) != 0 && actualActionTarget)
            {
                random = random.Draw(out ushort kind);
                uint flags8 = model.Flags8 ?? throw new NotSupportedException("Native Quake touchdown requires its current actor graphics status flags.");
                if ((flags8 & 2) == 0)
                {
                    sequence = (kind & 1) != 0 ? 4 : 5;
                }
            }
        }
        model = model with { Transform = transform with { Position28 = current with { Y = y } } };
        return new(model, saved, random, sequence);
    }

    private static short Jitter(short value, ushort random) => unchecked((short)(value + (random & 0x7F) - 64));
}

using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private FieldHostReply ModelEffect(int entity, FieldModelEffectCommand command)
    {
        Ff7Actor actor = GetActor(entity);
        if (command.Wait)
        {
            bool completed = actor.ModelEffect?.Completed ?? true;
            if (completed)
            {
                SetActor(entity, current => current with { ModelEffect = null });
            }
            return new(completed);
        }
        if (command.Operation != 13)
        {
            throw new NotSupportedException($"FFVII KAWAI selector {command.Operation} is not supported.");
        }
        IReadOnlyList<int> parts = ModelPartVertexCounts?.Invoke(actor.Model) ?? throw new NotSupportedException("FFVII KAWAI13 requires native model topology.");
        if (!short.TryParse(field.Models.Models[actor.Model].Scale, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out short nativeScale) || nativeScale > 4096)
        {
            throw new NotSupportedException("FFVII KAWAI13 model scale requires its native render-record owner.");
        }
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Request(actor.ModelEffect, command.Mode, parts, nativeScale, ModelPartPolygonCounts?.Invoke(actor.Model));
        SetActor(entity, current => current with { ModelEffect = state });
        return new(true);
    }

    private void AdvanceModelEffects()
    {
        if (!Process.Actors.Any(actor => actor.ModelEffect is not null))
        {
            return;
        }
        if (Process.Movie is { Completed: false })
        {
            throw new NotSupportedException("FFVII model effects during a movie require the current native movie camera.");
        }
        PcFieldCamera camera = field.Cameras[0];
        Process = Process with
        {
            Actors = Process.Actors.Select(actor => actor.ModelEffect is null ? actor : actor with { ModelEffect = Ff7NativeModelEffect.Advance(actor.ModelEffect, ModelEffectDepth(actor, camera) < 3840) }).ToArray()
        };
    }

    internal static int ModelEffectDepth(Ff7Actor actor, PcFieldCamera camera)
    {
        short x = unchecked((short)(actor.X4096 >> 12));
        short y = unchecked((short)(actor.Y4096 >> 12));
        short z = unchecked((short)((actor.Z4096 >> 12) - 10));
        float rotated = (float)((double)(camera.AxisZ.X / 4096f) * x + (double)(camera.AxisZ.Y / 4096f) * y + (double)(camera.AxisZ.Z / 4096f) * z);
        float depth = (float)((double)rotated + camera.Translation.Z);
        return unchecked((int)((double)depth * 0.25));
    }
}

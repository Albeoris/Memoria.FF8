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
            bool completed = (actor.ModelEffect?.Completed ?? true) && (actor.PartVisibility?.Completed ?? true) && !actor.NativeEyeControlPending && actor.NativeColorOffset?.Pending != true;
            if (completed)
            {
                SetActor(entity, current => current with { ModelEffect = null });
            }
            return new(completed);
        }
        if (command.Operation == 0)
        {
            byte[] payload = command.Payload ?? throw new InvalidDataException("FFVII eye control requires the complete original payload.");
            if (payload.Length != 4 || command.Mode != payload[0])
            {
                throw new InvalidDataException("FFVII eye control requires its four native payload bytes.");
            }
            if (actor.ModelEffect is not null)
            {
                throw new NotSupportedException("FFVII eye selection, automatic blink activation or concurrent procedural colors require their native presentation owners.");
            }
            if (actor.PartVisibility is { Completed: false })
            {
                throw new NotSupportedException("Eye control replacing pending part visibility requires its native single-effect-owner cancellation contract.");
            }
            if (payload[0] == 1 && payload[1] == 1 && payload[2] == 0)
            {
                SetActor(entity, current => current with { AutomaticBlinkDisabled = false, BlinkCountdown = 0, NativeEyeControlPending = true });
            }
            else if (payload[0] is 1 or 2 && payload[1] is 1 or 2)
            {
                if (payload[0] != payload[1])
                {
                    throw new NotSupportedException("Native asymmetric eye selection requires the selected appearance's independent left/right material owners.");
                }
                SetActor(entity, current => current with { AutomaticBlinkDisabled = true, NativeEyeLeft = payload[0], NativeEyeRight = payload[1], NativeEyeControlPending = true });
            }
            else
            {
                SetActor(entity, current => current with { AutomaticBlinkDisabled = true, NativeEyeControlPending = true });
            }
            SetActor(entity, current => current with { NativeColorOffset = current.NativeColorOffset is null ? null : current.NativeColorOffset with { Pending = false } });
            return new(true);
        }
        if (command.Operation == 2)
        {
            if (actor.ModelEffect is { Restored: false } || actor.PartVisibility is { Completed: false })
            {
                throw new NotSupportedException("Native color offsets replacing a pending topology effect require its material restoration contract.");
            }
            byte[] payload = command.Payload ?? throw new InvalidDataException("Native model color offsets require their original payload.");
            if (payload.Length == 0 || command.Mode != payload[0])
            {
                throw new InvalidDataException("The native model color payload differs from its decoded mode.");
            }
            Ff7NativeColorOffsetState offset = Ff7NativeColorOffset.Request(actor.NativeColorOffset, payload);
            SetActor(entity, current => current with { NativeColorOffset = offset, NativeEyeControlPending = false, ModelEffect = null });
            return new(true);
        }
        if (command.Operation == 10)
        {
            if (actor.ModelEffect is { Restored: false })
            {
                throw new NotSupportedException("Part visibility concurrent with native procedural colors requires its shared material-owner contract.");
            }
            byte[] payload = command.Payload ?? throw new InvalidDataException("Native part visibility requires the complete original payload.");
            if (payload.Length == 0 || command.Mode != payload[0])
            {
                throw new InvalidDataException("The native part-visibility count differs from its original payload.");
            }
            int count = ModelPartVertexCounts?.Invoke(actor.Model).Count ?? throw new NotSupportedException("Native part visibility requires the original model topology.");
            _ = fieldId ?? throw new NotSupportedException("Native part visibility requires the selected original field identity.");
            Ff7NativePartVisibilityState visibility = Ff7NativePartVisibility.Request(actor.PartVisibility, payload, count);
            SetActor(entity, current => current with { PartVisibility = visibility, NativeEyeControlPending = false, NativeColorOffset = current.NativeColorOffset is null ? null : current.NativeColorOffset with { Pending = false } });
            return new(true);
        }
        if (command.Operation != 13)
        {
            throw new NotSupportedException($"FFVII KAWAI selector {command.Operation} is not supported.");
        }
        if (actor.PartVisibility is not null)
        {
            throw new NotSupportedException("Procedural colors following explicit native part visibility require their shared material-owner contract.");
        }
        if (actor.NativeColorOffset is { Red: not 0 } or { Green: not 0 } or { Blue: not 0 } or { Pending: true })
        {
            throw new NotSupportedException("Procedural colors replacing native color offsets require their shared original-color cache contract.");
        }
        IReadOnlyList<int> parts = ModelPartVertexCounts?.Invoke(actor.Model) ?? throw new NotSupportedException("FFVII KAWAI13 requires native model topology.");
        if (!short.TryParse(field.Models.Models[actor.Model].Scale, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out short nativeScale) || nativeScale > 4096)
        {
            throw new NotSupportedException("FFVII KAWAI13 model scale requires its native render-record owner.");
        }
        Ff7NativeModelEffectState state = Ff7NativeModelEffect.Request(actor.ModelEffect, command.Mode, parts, nativeScale, ModelPartPolygonCounts?.Invoke(actor.Model));
        SetActor(entity, current => current with { ModelEffect = state, NativeEyeControlPending = false });
        return new(true);
    }

    private void AdvanceModelEffects()
    {
        if (!Process.Actors.Any(actor => actor.ModelEffect is not null || actor.PartVisibility is { Completed: false } || actor.NativeEyeControlPending || actor.NativeColorOffset is { Pending: true }))
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
            Actors = Process.Actors.Select(actor => actor with
            {
                ModelEffect = actor.ModelEffect is null ? null : Ff7NativeModelEffect.Advance(actor.ModelEffect, ModelEffectDepth(actor, camera) < 3840),
                PartVisibility = actor.PartVisibility is null ? null : Ff7NativePartVisibility.Advance(actor.PartVisibility, ModelEffectDepth(actor, camera) < 3840, fieldId ?? throw new NotSupportedException("Native part visibility requires its original field identity.")),
                NativeColorOffset = actor.NativeColorOffset is null ? null : Ff7NativeColorOffset.Advance(actor.NativeColorOffset, ModelEffectDepth(actor, camera) < 3840),
                NativeEyeControlPending = actor.NativeEyeControlPending && ModelEffectDepth(actor, camera) >= 3840
            }).ToArray()
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

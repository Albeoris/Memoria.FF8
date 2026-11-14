namespace Memoria.FF7.Formats;

public sealed partial record Ff7BattlePrimaryElectromagController(int Source, ushort Targets) : Ff7BattlePrimaryCallback;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleElectromag(Ff7BattlePrimaryState state, int source, ushort targets, Ff7BattlePrimaryContext context, int owner = 0)
    {
        ValidateTargets(targets);
        if (source is < 0 or 3 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }
        PrimaryPass pass = new(state, context);
        Ff7BattlePrimaryElectromagController controller = new(source, targets) { Owner = owner };
        int slot = pass.Allocate(controller);
        pass.Slots[slot] = InitializeElectromagGraphics(controller, pass);
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteElectromag(Ff7BattlePrimaryElectromagController callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryElectromagController next = ExecuteElectromagVisual(callback, pass);
        if (callback.Age == 35)
        {
            for (int slot = 0; slot < 10; slot++)
            {
                if ((callback.Targets & 1 << slot) != 0)
                {
                    pass.Contact(slot);
                }
            }
            pass.FadeTo(0, 16);
        }
        int age = callback.Age + 1;
        return age == 55 ? null : next with { Age = age };
    }
}

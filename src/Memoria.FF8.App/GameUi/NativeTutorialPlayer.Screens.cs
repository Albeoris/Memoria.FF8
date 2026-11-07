namespace Memoria.FF8.GameUi;

internal sealed partial class NativeTutorialPlayer
{
    private string? GuardianForceLessonMessage() => textId switch
    {
        11 => Menu.SystemText("tutorial.gf.learning-screen", "Selecting a GF opens its Learn screen."),
        12 => Menu.SystemText("tutorial.gf.summary", "The summary shows its level, HP, experience and current learning target."),
        13 => Menu.SystemText("tutorial.gf.ability-list", "Browse the ability list beside the summary to choose what the GF learns next."),
        15 => Menu.SystemText("tutorial.gf.learned", "Str-J is already learned. Completed abilities show a completion or passive status."),
        16 => Menu.SystemText("tutorial.gf.unlearned", "Abilities you can still learn show an AP total beside their name."),
        17 => Menu.SystemText("tutorial.gf.first-ap-number", "The first AP number"),
        24 => Menu.SystemText("tutorial.gf.learning-summary", "The selected ability now appears under Learning."),
        _ => null
    };
}

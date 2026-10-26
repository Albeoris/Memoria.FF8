using System.Globalization;
using System.Text.RegularExpressions;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class GuardianForceAbilityTextTests
{
    [Fact]
    public void EveryProducerTemplateHasAStableExportedIdentityAndFormatsAllOperands()
    {
        foreach ((string id, string template) in GuardianForceAbilityText.Templates)
        {
            Assert.Equal(template, SystemTextCatalog.Defaults[id]);
            int count = Regex.Matches(template, @"\{(\d+)(?:[^}]*)\}").Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) + 1).DefaultIfEmpty().Max();
            object?[] arguments = Enumerable.Range(0, count).Select(index => (object?)("operand" + index)).ToArray();
            GuardianForceAbilityText text = GuardianForceAbilityText.Create(id, arguments);
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, template, arguments), text.Format());
            Assert.StartsWith("第一 ", text.Format((_, fallback) => "第一 " + fallback));
            Assert.StartsWith("第二 ", text.Format((_, fallback) => "第二 " + fallback));
            Assert.Equal(text.Format(), text.Format((_, _) => "{999}"));
        }
    }

    [Fact]
    public void NestedReviewMessagesAndDeferredNamesReadTheCurrentGeneration()
    {
        string generation = "第一";
        string name = "Name one";
        GuardianForceAbilityText text = GuardianForceAbilityText.Create("guardian.editor.review.skipped", GuardianForceAbilityText.Create("guardian.editor.undo.swap", (Func<string>)(() => name)));
        string Localize(string id, string fallback) => id.EndsWith("skipped", StringComparison.Ordinal) ? generation + "省略 {0}" : generation + "交換 {0}";
        Assert.Equal("第一省略 第一交換 Name one", text.Format(Localize));
        generation = "第二";
        name = "Name two";
        Assert.Equal("第二省略 第二交換 Name two", text.Format(Localize));
        Assert.Equal("Skipped: Undo swap with Name two", text.Format());
    }
}

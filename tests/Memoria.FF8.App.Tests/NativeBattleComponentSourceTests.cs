using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentSourceTests
{
    [Fact]
    public void Prepare_one_selected_existing_component_source()
    {
        string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_EFFECT_ID");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_SOURCE_OUTPUT");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(selected is not null && output is not null && game is not null, "Select installed resources, one existing component selector and an explicit prepared-source output.");
        ushort effect = ushort.Parse(selected!, System.Globalization.CultureInfo.InvariantCulture);
        NativeBattleComponentSelectorEntry entry = NativeBattleComponentSelectorCatalog.Require(effect);
        string evidence = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output!))!, "selector-evidence");
        Dictionary<string, string?> saved = [];
        try
        {
            Set("MEMORIA_FF8_COMPONENT_SOURCE_ONLY", "1");
            NativeBattleComponentSourceAvailabilityEntry availability = NativeBattleComponentSourceAvailability.Get(effect);
            NativeBattleComponentSelector? selector = entry.ExistingSelectors.FirstOrDefault(item => item.SupportsQueuedSourceHandoff && item.TestClass == availability.Provider && item.TestMethod == availability.Method);
            if (selector is not null)
            {
                Set(selector.EnvironmentPrefix + "_EFFECT", effect.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Set(selector.EnvironmentPrefix + "_EVIDENCE", evidence);
                Type type = typeof(NativeBattleComponentSourceTests).Assembly.GetType("Memoria.FF8.App.Tests." + selector.TestClass, true)!;
                MethodInfo method = type.GetMethod(selector.TestMethod, BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidDataException("The indexed component selector is unavailable.");
                Invoke(method, Activator.CreateInstance(type), []);
            }
            else
            {
                BattleSnapshot? source = null;
                foreach (string name in NativeBattleComponentSourceAvailability.FactoryTypes)
                {
                    Type? type = typeof(NativeBattleComponentSourceTests).Assembly.GetType("Memoria.FF8.App.Tests." + name);
                    MethodInfo? method = type?.GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, [typeof(ushort), typeof(string), typeof(string)]);
                    MethodInfo? supports = type?.GetMethod("Supports", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, [typeof(ushort)]);
                    if (method is not null && supports is not null && (bool)Invoke(supports, null, [effect])!)
                    {
                        source = (BattleSnapshot?)Invoke(method, null, [effect, game!, evidence]);
                        if (source is not null)
                        {
                            break;
                        }
                    }
                }
                if (source is null)
                {
                    throw new InvalidDataException($"Effect {effect} has no registered fresh component source factory. Its existing prepared-source executor does not authorize constructing another header or carrier.");
                }
                NativeBattleComponentSelectorCatalog.PublishPreparedSource(effect, source);
            }
            Assert.True(File.Exists(output), "The existing selector did not publish its actual prepared component source.");
            BattleSnapshot published = NativeBattleEffectFamilyRunner.LoadSnapshot(output!);
            BattleSession restored = BattleSession.Restore(published);
            _ = GameWorld.ValidateEffectComponentSource(published, restored, effect);
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(output + ".preparation.json"));
            Assert.Equal("prepared", receipt.RootElement.GetProperty("Phase").GetString());
            Assert.Equal(effect, receipt.RootElement.GetProperty("Effect").GetUInt16());
            Assert.False(receipt.RootElement.GetProperty("OrdinaryAdmissionCertified").GetBoolean());
            Assert.False(receipt.RootElement.GetProperty("FunctionalCompletionCertified").GetBoolean());
        }
        finally
        {
            foreach ((string name, string? value) in saved)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        void Set(string name, string value)
        {
            saved.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static object? Invoke(MethodInfo method, object? instance, object?[] arguments)
    {
        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}

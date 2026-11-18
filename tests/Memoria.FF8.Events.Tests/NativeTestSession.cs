using System.Reflection;
using System.Runtime.ExceptionServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static GameSession CreateInitializedSession()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        return CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
    }

    private static void WaitForNativeWindowReady(EventEngine engine, int channel, Action step)
    {
        bool Ready() => engine.Dialogues.SingleOrDefault(dialogue => dialogue.Channel == channel)?.NativeWindow is { Progress: 4096 } window && window.Text is not { Ready: false };
        for (int callback = 0; callback < 64 && !Ready(); callback++)
        {
            step();
        }
        Require(Ready(), "The native message window did not finish opening and revealing its text before input.");
    }

    private static void CloseNativeWindow(EventEngine engine, int channel, Action step)
    {
        for (int callback = 0; callback < 16; callback++)
        {
            step();
            if (!engine.Dialogues.Any(dialogue => dialogue.Channel == channel))
            {
                return;
            }
        }
        throw new InvalidOperationException("The acknowledged native message window did not complete its closing callbacks.");
    }

    private class NativeTestServices : DispatchProxy
    {
        public GameSession Session { get; set; } = null!;
        public Dictionary<string, Func<object?[]?, object?>> Overrides { get; } = new();

        public static IEventServices Create(GameSession session, out NativeTestServices control)
        {
            IEventServices services = Create<IEventServices, NativeTestServices>();
            control = (NativeTestServices)services;
            control.Session = session;
            return services;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (Overrides.TryGetValue(targetMethod.Name, out Func<object?[]?, object?>? replacement))
            {
                return replacement(args);
            }
            try
            {
                return targetMethod.Invoke(Session, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}

using System.Diagnostics.CodeAnalysis;
using Albeoris.Games.FF8.Jsm.Format;
using Albeoris.Games.FF8.Jsm.Instructions;

namespace Albeoris.Games.FF8.Jsm.Messages;

/// <summary>Extracts, merges, and applies field message presentation parameters.</summary>
public sealed class FieldMessageContextResolver
{
    /// <summary>Extracts statically identifiable message contexts from a JSM document.</summary>
    public FieldMessageContextAnalysis Extract(JsmDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<FieldMessageContext> contexts = [];
        foreach (IMessageInstruction instruction in document.Instructions.OfType<IMessageInstruction>())
        {
            if (TryCreateContext(instruction, out FieldMessageContext? context))
            {
                contexts.Add(context);
            }
        }
        foreach (WindowMessageAssociation association in EnumerateWindowMessageAssociations(document))
        {
            if (TryCreateWindowContext(association, out FieldMessageContext? context))
            {
                contexts.Add(context);
            }
        }

        return Merge(contexts);
    }

    /// <summary>
    /// Merges contexts by message identifier. Missing components do not conflict with known values; incompatible known values are omitted and reported.
    /// </summary>
    public FieldMessageContextAnalysis Merge(IEnumerable<FieldMessageContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        Dictionary<Int32, ContextAccumulator> byMessageId = [];
        foreach (FieldMessageContext context in contexts)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (!byMessageId.TryGetValue(context.MessageId, out ContextAccumulator? accumulator))
            {
                accumulator = new ContextAccumulator(context.MessageId);
                byMessageId.Add(context.MessageId, accumulator);
            }

            accumulator.Add(context);
        }

        FieldMessageContext[] merged = byMessageId.Values
            .OrderBy(accumulator => accumulator.MessageId)
            .Select(accumulator => accumulator.CreateContext())
            .ToArray();
        FieldMessageContextConflict[] conflicts = byMessageId.Values
            .OrderBy(accumulator => accumulator.MessageId)
            .SelectMany(accumulator => accumulator.CreateConflicts())
            .ToArray();
        return new FieldMessageContextAnalysis(merged, conflicts);
    }

    /// <summary>
    /// Applies the supplied context to every statically bound use of its message identifier in <paramref name="document"/>.
    /// The operation is validated before any source operations are changed.
    /// </summary>
    public FieldMessageContextApplyResult Apply(JsmDocument document, FieldMessageContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        List<OperandEdit> edits = [];
        HashSet<IJsmInstruction> affectedInstructions = [];
        Boolean hasPositionTarget = false;
        Boolean hasChoiceTarget = false;
        Boolean hasSizeTarget = false;
        foreach (IMessageInstruction instruction in document.Instructions.OfType<IMessageInstruction>())
        {
            if (!TryEvaluate(instruction.MessageIdExpression, out Int32 messageId) || messageId != context.MessageId)
            {
                continue;
            }

            Int32 previousEditCount = edits.Count;
            if (context.Position is not null && instruction is IPositionedMessageInstruction positioned)
            {
                hasPositionTarget = true;
                AddEdit(edits, positioned.PosXExpression, context.Position.X);
                AddEdit(edits, positioned.PosYExpression, context.Position.Y);
            }

            if (context.Choices is not null && instruction is IChoiceMessageInstruction choice)
            {
                hasChoiceTarget = true;
                AddEdit(edits, choice.FirstLineExpression, context.Choices.FirstLine);
                AddEdit(edits, choice.LastLineExpression, context.Choices.LastLine);
                AddEdit(edits, choice.BeginLineExpression, context.Choices.BeginLine);
                AddEdit(edits, choice.CancelLineExpression, context.Choices.CancelLine);
            }

            if (edits.Count != previousEditCount)
            {
                affectedInstructions.Add(instruction);
            }
        }
        foreach (WindowMessageAssociation association in EnumerateWindowMessageAssociations(document))
        {
            if (!TryEvaluate(association.Message.MessageIdExpression, out Int32 messageId) || messageId != context.MessageId)
            {
                continue;
            }

            Int32 previousEditCount = edits.Count;
            if (context.Position is not null)
            {
                hasPositionTarget = true;
                AddEdit(edits, association.Window.XExpression, context.Position.X);
                AddEdit(edits, association.Window.YExpression, context.Position.Y);
            }
            if (context.Size is not null)
            {
                hasSizeTarget = true;
                AddEdit(edits, association.Window.WidthExpression, context.Size.Width);
                AddEdit(edits, association.Window.HeightExpression, context.Size.Height);
            }
            if (edits.Count != previousEditCount)
            {
                affectedInstructions.Add(association.Window);
            }
        }

        EnsureTargetExists(context.Position, hasPositionTarget, context.MessageId, "position");
        EnsureTargetExists(context.Choices, hasChoiceTarget, context.MessageId, "choices");
        EnsureTargetExists(context.Size, hasSizeTarget, context.MessageId, "window size");

        foreach (OperandEdit edit in edits)
        {
            edit.Expression.Value = edit.Value;
        }

        return new FieldMessageContextApplyResult(affectedInstructions.Count, edits.Count);
    }

    private static void EnsureTargetExists<T>(T? component, Boolean hasTarget, Int32 messageId, String componentName) where T : class
    {
        if (component is not null && !hasTarget)
        {
            throw new InvalidOperationException($"Message {messageId} has no statically writable {componentName} instruction.");
        }
    }

    private static IEnumerable<WindowMessageAssociation> EnumerateWindowMessageAssociations(JsmDocument document)
    {
        foreach (Jsm.GameObject gameObject in document.Objects)
        {
            foreach (Jsm.GameScript script in gameObject.Scripts)
            {
                Dictionary<Int32, IWindowSizeInstruction> pendingWindows = [];
                foreach (IJsmInstruction instruction in script.Instructions)
                {
                    if (instruction is IWindowSizeInstruction window && TryEvaluate(window.ChannelExpression, out Int32 windowChannel))
                    {
                        pendingWindows[windowChannel] = window;
                        continue;
                    }

                    if (instruction is IChannelMessageInstruction message
                        && TryEvaluate(message.ChannelExpression, out Int32 messageChannel)
                        && pendingWindows.Remove(messageChannel, out IWindowSizeInstruction? pendingWindow))
                    {
                        yield return new WindowMessageAssociation(pendingWindow, message);
                    }
                }
            }
        }
    }

    private static Boolean TryCreateWindowContext(WindowMessageAssociation association, [NotNullWhen(true)] out FieldMessageContext? context)
    {
        context = null;
        if (!TryEvaluate(association.Message.MessageIdExpression, out Int32 messageId)
            || !TryEvaluate(association.Window.XExpression, out Int32 x)
            || !TryEvaluate(association.Window.YExpression, out Int32 y)
            || !TryEvaluate(association.Window.WidthExpression, out Int32 width)
            || !TryEvaluate(association.Window.HeightExpression, out Int32 height))
        {
            return false;
        }

        context = new FieldMessageContext(messageId, new FieldMessagePosition(x, y), size: new FieldMessageSize(width, height));
        return true;
    }

    private static Boolean TryCreateContext(IMessageInstruction instruction, [NotNullWhen(true)] out FieldMessageContext? context)
    {
        context = null;
        if (!TryEvaluate(instruction.MessageIdExpression, out Int32 messageId))
        {
            return false;
        }

        FieldMessagePosition? position = null;
        if (instruction is IPositionedMessageInstruction positioned
            && TryEvaluate(positioned.PosXExpression, out Int32 x)
            && TryEvaluate(positioned.PosYExpression, out Int32 y))
        {
            position = new FieldMessagePosition(x, y);
        }

        FieldMessageChoices? choices = null;
        if (instruction is IChoiceMessageInstruction choice
            && TryEvaluate(choice.FirstLineExpression, out Int32 firstLine)
            && TryEvaluate(choice.LastLineExpression, out Int32 lastLine)
            && TryEvaluate(choice.BeginLineExpression, out Int32 beginLine)
            && TryEvaluate(choice.CancelLineExpression, out Int32 cancelLine))
        {
            choices = new FieldMessageChoices(firstLine, lastLine, beginLine, cancelLine);
        }

        context = new FieldMessageContext(messageId, position, choices);
        return true;
    }

    private static Boolean TryEvaluate(IJsmExpression expression, out Int32 value)
    {
        IJsmExpression evaluated = expression.Evaluate(StatelessServices.Instance);
        if (evaluated is IConstExpression constant && constant.Value is >= Int32.MinValue and <= Int32.MaxValue)
        {
            value = (Int32)constant.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static void AddEdit(List<OperandEdit> edits, IJsmExpression expression, Int32 value)
    {
        if (TryEvaluate(expression, out Int32 currentValue) && currentValue == value)
        {
            return;
        }

        if (expression is not Jsm.Expression.PSHN_L constant || constant.SourceOperation is null)
        {
            throw new InvalidOperationException("A field message context can only update a source-backed PSHN_L operand.");
        }

        JsmOperation.ValidateParameter(value);
        edits.Add(new OperandEdit(constant, value));
    }

    private sealed record OperandEdit(Jsm.Expression.PSHN_L Expression, Int32 Value);

    private sealed record WindowMessageAssociation(IWindowSizeInstruction Window, IChannelMessageInstruction Message);

    private sealed class ContextAccumulator(Int32 messageId)
    {
        private FieldMessagePosition? position;
        private FieldMessageChoices? choices;
        private FieldMessageSize? size;
        private Boolean positionConflict;
        private Boolean choicesConflict;
        private Boolean sizeConflict;

        public Int32 MessageId { get; } = messageId;

        public void Add(FieldMessageContext context)
        {
            MergeComponent(context.Position, ref position, ref positionConflict);
            MergeComponent(context.Choices, ref choices, ref choicesConflict);
            MergeComponent(context.Size, ref size, ref sizeConflict);
        }

        public FieldMessageContext CreateContext()
        {
            return new FieldMessageContext(
                MessageId,
                positionConflict ? null : position,
                choicesConflict ? null : choices,
                sizeConflict ? null : size);
        }

        public IEnumerable<FieldMessageContextConflict> CreateConflicts()
        {
            if (positionConflict)
            {
                yield return new FieldMessageContextConflict(MessageId, FieldMessageContextComponent.Position);
            }
            if (choicesConflict)
            {
                yield return new FieldMessageContextConflict(MessageId, FieldMessageContextComponent.Choices);
            }
            if (sizeConflict)
            {
                yield return new FieldMessageContextConflict(MessageId, FieldMessageContextComponent.Size);
            }
        }

        private static void MergeComponent<T>(T? incoming, ref T? current, ref Boolean conflict) where T : class
        {
            if (incoming is null || conflict)
            {
                return;
            }
            if (current is null)
            {
                current = incoming;
                return;
            }

            if (!current.Equals(incoming))
            {
                current = null;
                conflict = true;
            }
        }
    }
}

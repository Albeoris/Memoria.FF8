using System;
using System.Collections.Generic;
using System.Linq;
using Albeoris.Games.FF8.Jsm.Instructions;

namespace Albeoris.Games.FF8.Jsm
{
    public static partial class Jsm
    {
        public static partial class File
        {
            public static List<GameObject> Read(Byte[] data)
            {
                ArgumentNullException.ThrowIfNull(data);
                return ReadCore(data, operations: null, allInstructions: null);
            }

            public static JsmDocument ReadDocument(Byte[] data)
            {
                ArgumentNullException.ThrowIfNull(data);

                Byte[] source = (Byte[])data.Clone();
                IReadOnlyList<JsmOperation> operations = JsmDocument.ReadOperations(source);
                List<IJsmInstruction> instructions = new List<IJsmInstruction>();
                List<GameObject> objects = ReadCore(source, operations, instructions);

                return new JsmDocument(source, objects, instructions.AsReadOnly(), operations);
            }

            private static List<GameObject> ReadCore(
                Byte[] data,
                IReadOnlyList<JsmOperation>? operations,
                List<IJsmInstruction>? allInstructions)
            {
                unsafe
                {
                    fixed (Byte* ptr = data)
                    {
                        Header* header = (Header*)ptr;
                        Group* areas = (Group*)(ptr + sizeof(Header));
                        Group* doors = areas + header->CountAreas;
                        Group* modules = doors + header->CountDoors;
                        Group* objects = modules + header->CountModules;
                        Group* end = objects + header->CountObjects;
                        Script* scripts = (Script*)(ptr + header->ScriptsOffset);
                        Operation* operation = (Operation*)(ptr + header->OperationsOffset);

                        Int64 groupNumber = end - areas;
                        Group[] groups = new Group[groupNumber];
                        for (Group* group = areas; group < end; group++)
                            groups[--groupNumber] = *group;

                        List<GameObject> gameObjects = new List<GameObject>(groups.Length);

                        foreach (Group group in groups.OrderBy(g => g.Label))
                        {
                            List<GameScript> objectScripts = new List<GameScript>(group.ScriptsCount + 1);

                            for (Int32 s = 0; s <= group.ScriptsCount; s++)
                            {
                                Int32 scriptLabel = group.Label + s;

                                UInt16 position = scripts->Position;
                                scripts++;

                                UInt16 count = (UInt16)(scripts->Position - position);
                                Jsm.ExecutableSegment scriptSegment = MakeScript(
                                    operation + position,
                                    count,
                                    position,
                                    operations,
                                    out List<JsmInstruction> scriptInstructions);

                                allInstructions?.AddRange(scriptInstructions);

                                objectScripts.Add(new GameScript(
                                    scriptLabel,
                                    scriptSegment,
                                    scriptInstructions.AsReadOnly()));
                            }

                            gameObjects.Add(new GameObject(group.Label, objectScripts));
                        }

                        return gameObjects;
                    }
                }
            }

            public static IReadOnlyList<IJsmInstruction> GetInstructions(Byte[] data)
            {
                ArgumentNullException.ThrowIfNull(data);

                List<IJsmInstruction> result = new List<IJsmInstruction>();
                _ = ReadCore(data, operations: null, result);
                return result.AsReadOnly();
            }

            private static unsafe Jsm.ExecutableSegment MakeScript(
                Operation* operation,
                UInt16 count,
                Int32 firstOperationIndex,
                IReadOnlyList<JsmOperation>? operations,
                out List<JsmInstruction> instructions)
            {
                instructions = MakeInstructions(operation, count, firstOperationIndex, operations);

                // Combine instructions to logical blocks
                IReadOnlyList<Jsm.IJsmControl> controls = Jsm.Control.Builder.Build(instructions);

                // Arrange instructions by segments and return root
                return Jsm.Segment.Builder.Build(instructions, controls);
            }

            private static unsafe List<JsmInstruction> MakeInstructions(
                Operation* operation,
                UInt16 count,
                Int32 firstOperationIndex,
                IReadOnlyList<JsmOperation>? operations)
            {
                List<JsmInstruction> instructions = new List<JsmInstruction>(count / 2);
                LabeledStack stack = new LabeledStack();
                LabelBuilder labelBuilder = new LabelBuilder(count);

                for (Int32 i = 0; i < count; i++)
                {
                    Jsm.Opcode opcode = operation->Opcode;
                    Int32 parameter = operation->Parameter;
                    operation++;

                    stack.CurrentLabel = i;
                    IJsmExpression expression = Jsm.Expression.TryMake(opcode, parameter, stack);
                    if (expression != null)
                    {
                        if (expression is Jsm.Expression.PSHN_L constant && operations is not null)
                            constant.Bind(operations[firstOperationIndex + i]);

                        stack.Push(expression);
                        continue;
                    }

                    JsmInstruction instruction = JsmInstruction.TryMake(opcode, parameter, stack);
                    if (instruction != null)
                    {
                        labelBuilder.TraceInstruction(i, stack.CurrentLabel, new IndexedInstruction(instructions.Count, instruction));
                        instructions.Add(instruction);
                        continue;
                    }

                    throw new NotSupportedException(opcode.ToString());
                }

                if (stack.Count != 0)
                {
                    if (stack.Count == 1 && stack.Peek() is Expression.PSHN_L && instructions.Last() is IRET)
                    {
                        // Strange behavior in JP version
                        throw new InvalidProgramException("Stack unbalanced.");
                    }
                    else
                    {
                        throw new InvalidProgramException("Stack unbalanced.");
                    }
                }

                if (!(instructions.First() is LBL))
                    throw new InvalidProgramException("Script must start with a label.");

                if (!(instructions.Last() is IRET))
                    throw new InvalidProgramException("Script must end with a return.");

                // Switch from opcodes to instructions
                HashSet<Int32> labelIndices = labelBuilder.Commit();

                // Merge similar instructions
                instructions = InstructionMerger.Merge(instructions, labelIndices);
                return instructions;
            }
        }
    }
}

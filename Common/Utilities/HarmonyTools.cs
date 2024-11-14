using HarmonyLib;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Common.Utilities;

public static class HarmonyTools
{
    /// <summary>
    /// Removes the instruction at the given position along with any instructions upon which it depends to load data.
    /// </summary>
    /// <param name="instructions"> The ilcode list. </param>
    /// <param name="position"> The position of the instruction to remove. </param>
    /// <param name="willReplaceMethod"> Whether or not you will replace the instruction with something that adds the same type to the stack. </param>
    /// <param name="relocateLabels"> If true, will place any labels on the removed instructions on the first instruction after the removed section.</param>
    /// <returns> The first index after the removed section. </returns>
    /// <exception cref="ArgumentException"> If given an invalid program or this would make an invalid program. </exception>
    public static int RemoveFunctionCall(List<CodeInstruction> instructions, int position, bool willReplaceMethod = false, bool relocateLabels = true)
    {
        if (!willReplaceMethod && GetReturnCount(instructions[position].opcode, instructions[position].operand) > 0)
        {
            throw new ArgumentException($"Do not try to remove a method that pushes to the stack.");
        }

        (int, int) range = GetDependencies(instructions, position);

        for(int i = range.Item2 - 1; i > range.Item1 - 1; i--)
        {
            if (relocateLabels && instructions[i].labels.Any())
            {
                instructions[i + 1].labels.AddRange(instructions[i].labels);
            }

            instructions.RemoveAt(i);
        }

        return range.Item1;
    }

    /// <summary>
    /// Adds a call to the given function to the list of instructions at the given position.
    /// 
    /// If the given function has parameters, it will get those from the arguments or local variables of the function being modified.
    /// It will choose the first argument/variable that matches the type of the parameters of the function being added, or is a subclass of the parameter's type.
    /// It will look at the argumets or local variables based on <paramref name="parameterLocations"/>. If the first item in <paramref name="parameterLocations"/> is
    /// <see cref="ParameterLocation.FromLocal"/>, it would look for the type of the added method's first parameter in the local variables.
    /// </summary>
    /// <remarks>
    /// The goal of this method it to make transpilers resistant to changes in local variable or parameter order. You should only use this to add functions that are defined by you so you can be sure that
    /// <paramref name="parameterLocations"/> is accurate and nothing unexpected happens.
    /// </remarks>
    /// <param name="baseMethod"> The method being added to.</param>
    /// <param name="instructions"> The instructions of the method beign modified. </param>
    /// <param name="position"> The position at which to add the method.</param>
    /// <param name="method"> The function to add. </param>
    /// <param name="parameterLocations"> Where to get each parameter of the function being added. Length must be equal to the number of parameters in the added function. </param>
    /// <returns> The first index after the removed section. </returns>
    public static int AddFunctionCall(MethodBase baseMethod, List<CodeInstruction> instructions, int position, MethodInfo method, ParameterLocation[] parameterLocations)
    {
        List<CodeInstruction> callInstructions = MakeFunctionCallCodes(baseMethod, method, parameterLocations);

        for(int i = callInstructions.Count - 1; i >= 0; i--)
        {
            instructions.Insert(position, callInstructions[i]);
        }

        return position + callInstructions.Count;
    }

    /// <summary>
    /// Represents whether to get a parameter from a method's local variables or arguments.
    /// </summary>
    public enum ParameterLocation
    {
        FromLocal = 0,
        FromArgument = 1
    }

    /// <summary>
    /// Generates the instructions necessary to call the given function and load its parameters.
    /// </summary>
    /// <param name="baseMethod"> The method the function would be called inside of. </param>
    /// <param name="method"> The method to be called. </param>
    /// <param name="parameterLocations"> Where to get each parameter of the function being added. Length must be equal to the number of parameters in the added function. </param>
    /// <returns> The instructions necessary to make the call. </returns>
    /// <exception cref="ArgumentException"></exception>
    public static List<CodeInstruction> MakeFunctionCallCodes(MethodBase baseMethod, MethodInfo method, ParameterLocation[] parameterLocations)
    {
        if(parameterLocations.Length != GetParameterCount(method))
        {
            throw new ArgumentException("The method's parameter count must match the length of parameterLocations!");
        }

        List<CodeInstruction> output = new();
        int offset = 0;

        if (!method.IsStatic)
        {
            offset = 1;

            output.Add(Load(baseMethod, method.DeclaringType, parameterLocations[0]));
        }

        for(int i = 0; i < method.GetParameters().Length; i++)
        {
            output.Add(Load(baseMethod, method.GetParameters()[i].ParameterType, parameterLocations[i + offset]));
        }

        output.Add(new CodeInstruction(OpCodes.Call, method));

        return output;
    }

    /// <summary>
    /// Gets the instuction required to load the given type from the given location within the given function.
    /// 
    /// If there are multiple local variables/arguments of the same type, will choose the first one.
    /// </summary>
    /// <param name="method"> The method to load the data from. </param>
    /// <param name="type"> The type to be loaded. </param>
    /// <param name="location"> Where to load the data from. </param>
    /// <returns> A <see cref="CodeInstruction"/> that will load a value of the given type from the given place in the given method. </returns>
    private static CodeInstruction Load(MethodBase method, Type type, ParameterLocation location)
    {
        if (location is ParameterLocation.FromArgument)
        {
            int parameterNumber = GetParameterNumber(method, type);

            return LoadArg(parameterNumber, false);
        }
        else
        {
            int localNumber = GetFirstLocalWithType(method, type);

            return LoadLocal(localNumber, false);
        }
    }

    /// <summary>
    /// Gets the index of the first parameter of the given type in the given method.
    /// </summary>
    /// <param name="method"> The method. </param>
    /// <param name="parameterType"> The type of the parameter to get. </param>
    /// <returns> The index of the first parameter of the given type in the given method. </returns>
    /// <exception cref="ArgumentException"></exception>
    private static int GetParameterNumber(MethodBase method, Type parameterType)
    {
        // If the method is not static, the calling instance will be the first parameter, but it will not be included in method.GetParameters(). The offset accounts for this.
        int offset = 0;

        if (!method.IsStatic)
        {
            if(method.DeclaringType.IsAssignableTo(parameterType))
            {
                return 0;
            }

            offset++;
        }

        for(int i = 0; i < method.GetParameters().Length; i++)
        {
            if (method.GetParameters()[i].ParameterType.IsAssignableTo(parameterType))
            {
                return i + offset;
            }
        }

        throw new ArgumentException($"There is no parameter of type {parameterType}!");
    }

    /// <summary>
    /// Gets the first local variable with the given type in the given function.
    /// </summary>
    /// <param name="method"> The function to get the local variable from. </param>
    /// <param name="type"> The type of the local variable. </param>
    /// <returns> The index of the first local variable with the given type in the given function.</returns>
    /// <exception cref="ArgumentException"></exception>
    private static int GetFirstLocalWithType(MethodBase method, Type type)
    {
        if(!method.HasMethodBody())
        {
            throw new ArgumentException("The given method has no body!");
        }

        foreach(LocalVariableInfo local in method.GetMethodBody().LocalVariables)
        {
            if(local.LocalType.IsAssignableTo(type))
            {
                return local.LocalIndex;
            }
        }

        throw new ArgumentException($"There is no local variable of type {type}!");
    }

    /// <summary>
    /// Gets an instruction that will load the argument with the given number.
    /// </summary>
    /// <param name="number"> The number of the argument to load. </param>
    /// <param name="isAddress"> Whether to load the address of the argument (almost always false).</param>
    /// <returns> An instruction that will load the argument with the given number. </returns>
    private static CodeInstruction LoadArg(int number, bool isAddress)
    {
        if(isAddress)
        {
            return number switch
            {
                < byte.MaxValue => new CodeInstruction(OpCodes.Ldarga_S, (byte)number),
                _ => new CodeInstruction(OpCodes.Ldarga, (ushort)number)
            };
        }

        return number switch
        {
            0 => new CodeInstruction(OpCodes.Ldarg_0),
            1 => new CodeInstruction(OpCodes.Ldarg_1),
            2 => new CodeInstruction(OpCodes.Ldarg_2),
            3 => new CodeInstruction(OpCodes.Ldarg_3),
            < byte.MaxValue => new CodeInstruction(OpCodes.Ldarg_S, (byte)number),
            _ => new CodeInstruction(OpCodes.Ldarg, (ushort)number)
        };
    }

    /// <summary>
    /// Gets an instruction that will load the local variable with the given number.
    /// </summary>
    /// <param name="number"> The number of the local variable to load. </param>
    /// <param name="isAddress"> Whether to load the address of the argument (almost always false).</param>
    /// <returns> An instruction that will load the local variable with the given number. </returns>
    private static CodeInstruction LoadLocal(int number, bool isAddress)
    {
        if (isAddress)
        {
            return number switch
            {
                < byte.MaxValue => new CodeInstruction(OpCodes.Ldloca_S, (byte)number),
                _ => new CodeInstruction(OpCodes.Ldloca, (ushort)number)
            };
        }

        return number switch
        {
            0 => new CodeInstruction(OpCodes.Ldloc_0),
            1 => new CodeInstruction(OpCodes.Ldloc_1),
            2 => new CodeInstruction(OpCodes.Ldloc_2),
            3 => new CodeInstruction(OpCodes.Ldloc_3),
            < byte.MaxValue => new CodeInstruction(OpCodes.Ldloc_S, (byte)number),
            _ => new CodeInstruction(OpCodes.Ldloc, (ushort)number)
        };
    }

    /// <summary>
    /// Gets the range of the instructions that load parameters for the given instruction, inclusive of the instruction.
    /// </summary>
    /// <param name="instructions"> The program to analyze. </param>
    /// <param name="position"> The position of the instruction in the program. </param>
    /// <returns> A tuple representing the range of the dependencies (inclusive, exclusive). </returns>
    public static (int, int) GetDependencies(List<CodeInstruction> instructions, int position)
    {
        int parameterCount = GetParameterCount(instructions[position].opcode, instructions[position].operand);

        if (parameterCount == 0)
        {
            return (position, position + 1);
        }

        int index = position - 1;
        while(true)
        {
            CodeInstruction instruction = instructions[index];

            parameterCount -= GetReturnCount(instruction.opcode, instruction.operand);
            parameterCount += GetParameterCount(instruction.opcode, instruction.operand);

            if(parameterCount < 0)
            {
                throw new ArgumentException("Invalid program!");
            }

            if (parameterCount == 0)
            {
                break;
            }

            if(--index < 0)
            {
                throw new ArgumentException("Invalid program!");
            }
        }

        return (index, position + 1);
    }

    /// <summary>
    /// Gets the number of things the given instruction can be expected to pop off the stack.
    /// 
    /// Data comes from: https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.stackbehaviour?view=net-6.0
    /// </summary>
    /// <param name="opCode"> The opcode of the instruction. </param>
    /// <param name="operand"> The operand. Only used if it's a method because those have variable parameter counts.</param>
    /// <returns> The number of items the instruction pops off the stack, or -1 if it is an unexpected case. </returns>
    public static int GetParameterCount(OpCode opCode, object operand)
    {
        return opCode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 => 1,
            StackBehaviour.Pop1_pop1 => 2,
            StackBehaviour.Popi => 1,
            StackBehaviour.Popi_pop1 => 2,
            StackBehaviour.Popi_popi => 2,
            StackBehaviour.Popi_popi8 => 2,
            StackBehaviour.Popi_popi_popi => 3,
            StackBehaviour.Popi_popr4 => 2,
            StackBehaviour.Popi_popr8 => 2,
            StackBehaviour.Popref => 1,
            StackBehaviour.Popref_pop1 => 2,
            StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popref_popi_popi => 3,
            StackBehaviour.Popref_popi_popi8 => 3,
            StackBehaviour.Popref_popi_popr4 => 3,
            StackBehaviour.Popref_popi_popr8 => 3,
            StackBehaviour.Popref_popi_popref => 3,
            StackBehaviour.Popref_popi_pop1 => 3,
            StackBehaviour.Varpop => operand is MethodInfo info ? GetParameterCount(info) : -1,
            _ => -1
        };
    }

    public static int GetParameterCount(MethodInfo method)
    {
        return method.GetParameters().Length + (method.IsStatic ? 0 : 1);
    }

    /// <summary>
    /// Gets the number of things the given instruction can be expected to push onto the stack.
    /// 
    /// Data comes from: https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.stackbehaviour?view=net-6.0
    /// </summary>
    /// <param name="opCode"> The opcode of the instruction. </param>
    /// <returns> The number of items the instruction pushes onto the stack, or -1 if it is an unexpected case. </returns>
    public static int GetReturnCount(OpCode opCode, object operand)
    {
        return opCode.StackBehaviourPush switch
        { 
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 => 1,
            StackBehaviour.Push1_push1 => 1,
            StackBehaviour.Pushi => 1,
            StackBehaviour.Pushi8 => 1,
            StackBehaviour.Pushr4 => 1,
            StackBehaviour.Pushr8 => 1,
            StackBehaviour.Pushref => 1,
            StackBehaviour.Varpush => operand is MethodInfo info ? (info.ReturnType == typeof(void) ? 0 : 1) : 1,
            _ => -1
        };
    }

    public static void Print(IMonitor monitor, IEnumerable<CodeInstruction> codes)
    {
        foreach (var code in codes)
        {
            monitor.Log($"{code.opcode} | {code.operand}");
        }
    }
}
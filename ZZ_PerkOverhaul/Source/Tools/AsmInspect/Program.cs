using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: AsmInspect <path-to-Assembly-CSharp.dll> [keyword1 keyword2 ...]");
            Console.Error.WriteLine("Example: AsmInspect Assembly-CSharp.dll Cost SkillPoint Perk Progression");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Special modes:");
            Console.Error.WriteLine("  type:<substring>                     Dumps all fields/methods for matching types");
            Console.Error.WriteLine("  il:<type-substring>::<method-name>   Dumps IL for the matching method(s)");
            return 2;
        }

        var asmPath = args[0];
        var asm = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters { ReadSymbols = false });

        var rawFilters = args.Skip(1)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .ToArray();

        var ilRequests = rawFilters
            .Where(a => a.StartsWith("il:", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Substring("il:".Length))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        var typeFilters = rawFilters
            .Where(a => a.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Substring("type:".Length))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        var keywords = rawFilters
            .Where(a => !a.StartsWith("type:", StringComparison.OrdinalIgnoreCase) &&
                        !a.StartsWith("il:", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (keywords.Length == 0)
        {
            keywords = new[]
            {
                "Cost",
                "SkillPoint",
                "Skill_Point",
                "Perk",
                "Progression",
                "XUiC_Skill",
                "Binding",
                "PassiveEffect",
            };
        }

        static bool ContainsAny(string? haystack, IReadOnlyList<string> needles)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            foreach (var n in needles)
            {
                if (haystack.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        var allTypes = asm.MainModule.Types.SelectMany(Flatten).ToArray();

        if (ilRequests.Length > 0)
        {
            Console.WriteLine($"Assembly: {asmPath}");
            Console.WriteLine("IL requests: " + string.Join(", ", ilRequests));
            Console.WriteLine();

            foreach (var req in ilRequests)
            {
                var parts = req.Split(new[] { "::" }, StringSplitOptions.None);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                {
                    Console.WriteLine($"!! Invalid il request '{req}'. Expected format: il:<type-substring>::<method-name>");
                    Console.WriteLine();
                    continue;
                }

                var typeNeedle = parts[0].Trim();
                var methodName = parts[1].Trim();

                var matchedTypes = allTypes
                    .Where(t => t.FullName.IndexOf(typeNeedle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                t.Name.IndexOf(typeNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(t => t.FullName)
                    .ToArray();

                if (matchedTypes.Length == 0)
                {
                    Console.WriteLine($"== {req} ==");
                    Console.WriteLine($"  No types matched '{typeNeedle}'.");
                    Console.WriteLine();
                    continue;
                }

                var matchedMethods = new List<MethodDefinition>();
                foreach (var t in matchedTypes)
                {
                    matchedMethods.AddRange(t.Methods.Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase)));
                }

                if (matchedMethods.Count == 0)
                {
                    Console.WriteLine($"== {req} ==");
                    Console.WriteLine($"  Types matched '{typeNeedle}': {matchedTypes.Length}, but no method named '{methodName}' found.");
                    Console.WriteLine();
                    continue;
                }

                foreach (var m in matchedMethods.OrderBy(m => m.DeclaringType.FullName).ThenBy(m => m.FullName))
                {
                    Console.WriteLine($"== {m.DeclaringType.FullName}::{m.Name} ==");

                    if (!m.HasBody || m.Body?.Instructions == null)
                    {
                        Console.WriteLine("  <no body>");
                        Console.WriteLine();
                        continue;
                    }

                    var strings = new List<string>();
                    foreach (var instr in m.Body.Instructions)
                    {
                        if (instr.OpCode == OpCodes.Ldstr && instr.Operand is string s)
                            strings.Add(s);
                    }

                    if (strings.Count > 0)
                    {
                        Console.WriteLine("  -- string literals --");
                        foreach (var s in strings.Distinct())
                            Console.WriteLine($"  \"{s}\"");
                        Console.WriteLine();
                    }

                    Console.WriteLine("  -- IL --");
                    foreach (var instr in m.Body.Instructions)
                    {
                        var operandText = FormatOperand(instr.Operand);
                        if (!string.IsNullOrEmpty(operandText))
                            Console.WriteLine($"  IL_{instr.Offset:X4}: {instr.OpCode.Name} {operandText}");
                        else
                            Console.WriteLine($"  IL_{instr.Offset:X4}: {instr.OpCode.Name}");
                    }

                    Console.WriteLine();
                }
            }

            return 0;
        }

        if (typeFilters.Length > 0)
        {
            bool MatchesTypeFilter(TypeDefinition t) => typeFilters.Any(tf =>
                t.FullName.IndexOf(tf, StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.Name.IndexOf(tf, StringComparison.OrdinalIgnoreCase) >= 0);

            var matched = allTypes
                .Where(MatchesTypeFilter)
                .OrderBy(t => t.FullName)
                .ToArray();

            Console.WriteLine($"Assembly: {asmPath}");
            Console.WriteLine("Type filters: " + string.Join(", ", typeFilters));
            Console.WriteLine($"Total types: {allTypes.Length}");
            Console.WriteLine($"Matched types: {matched.Length}");
            Console.WriteLine();

            foreach (var t in matched)
            {
                Console.WriteLine($"== {t.FullName} ==");

                foreach (var f in t.Fields.OrderBy(f => f.Name))
                    Console.WriteLine($"  F {f.FieldType.FullName} {f.Name}");

                foreach (var m in t.Methods.OrderBy(m => m.Name))
                {
                    var pars = string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name));
                    Console.WriteLine($"  M {(m.IsStatic ? "static " : "")} {m.ReturnType.FullName} {m.Name}({pars})");
                }

                Console.WriteLine();
            }

            return 0;
        }

        var types = allTypes
            .Where(t => ContainsAny(t.Name, keywords) || ContainsAny(t.FullName, keywords) ||
                        t.Fields.Any(f => ContainsAny(f.Name, keywords) || ContainsAny(f.FieldType.FullName, keywords)) ||
                        t.Methods.Any(m => ContainsAny(m.Name, keywords) || ContainsAny(m.ReturnType.FullName, keywords) ||
                                           m.Parameters.Any(p => ContainsAny(p.Name, keywords) || ContainsAny(p.ParameterType.FullName, keywords))))
            .OrderBy(t => t.FullName)
            .ToArray();

        Console.WriteLine($"Assembly: {asmPath}");
        Console.WriteLine("Keywords: " + string.Join(", ", keywords));
        Console.WriteLine($"Total types: {allTypes.Length}");
        Console.WriteLine($"Matched types: {types.Length}");
        Console.WriteLine();

        foreach (var t in types)
        {
            Console.WriteLine($"== {t.FullName} ==");

            foreach (var f in t.Fields.OrderBy(f => f.Name))
            {
                if (ContainsAny(f.Name, keywords) || ContainsAny(f.FieldType.FullName, keywords))
                    Console.WriteLine($"  F {f.FieldType.FullName} {f.Name}");
            }

            foreach (var m in t.Methods.OrderBy(m => m.Name))
            {
                if (!ContainsAny(m.Name, keywords) &&
                    !ContainsAny(m.ReturnType.FullName, keywords) &&
                    !m.Parameters.Any(p => ContainsAny(p.Name, keywords) || ContainsAny(p.ParameterType.FullName, keywords)))
                    continue;

                var pars = string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name));
                Console.WriteLine($"  M {(m.IsStatic ? "static " : "")} {m.ReturnType.FullName} {m.Name}({pars})");
            }

            Console.WriteLine();
        }

        return 0;
    }

    static string FormatOperand(object? operand)
    {
        if (operand == null) return string.Empty;

        return operand switch
        {
            string s => "\"" + s + "\"",
            MethodReference mr => mr.DeclaringType.FullName + "::" + mr.Name + "(" + string.Join(", ", mr.Parameters.Select(p => p.ParameterType.FullName)) + ")",
            FieldReference fr => fr.DeclaringType.FullName + "::" + fr.Name,
            TypeReference tr => tr.FullName,
            ParameterDefinition pd => pd.Name,
            VariableDefinition vd => "V_" + vd.Index + ":" + vd.VariableType.FullName,
            Instruction i => "IL_" + i.Offset.ToString("X4"),
            Instruction[] arr => "[" + string.Join(", ", arr.Select(i => "IL_" + i.Offset.ToString("X4"))) + "]",
            _ => operand.ToString() ?? string.Empty,
        };
    }

    static System.Collections.Generic.IEnumerable<TypeDefinition> Flatten(TypeDefinition t)
    {
        yield return t;
        foreach (var n in t.NestedTypes.SelectMany(Flatten))
            yield return n;
    }
}

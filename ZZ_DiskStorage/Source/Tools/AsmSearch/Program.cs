using System;
using System.Linq;
using Mono.Cecil;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: AsmSearch <path-to-assembly.dll> <substring1> [substring2] ...");
            Console.Error.WriteLine("       AsmSearch <path-to-assembly.dll> --members <substring1> [substring2] ...");
            return 2;
        }

        var asmPath = args[0];

        var dumpMembers = false;
        var argOffset = 1;
        if (args.Length >= 3 && string.Equals(args[1], "--members", StringComparison.OrdinalIgnoreCase))
        {
            dumpMembers = true;
            argOffset = 2;
        }

        var needles = args.Skip(argOffset).ToArray();
        if (needles.Length == 0)
        {
            Console.Error.WriteLine("At least one substring is required.");
            return 2;
        }

        var asm = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters { ReadSymbols = false });
        var allTypes = asm.MainModule.Types.SelectMany(Flatten).ToArray();

        var matches = allTypes
            .Where(t => needles.All(n => t.FullName.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.FullName)
            .ToArray();

        Console.WriteLine($"Assembly: {asmPath}");
        Console.WriteLine($"Needles: {string.Join(", ", needles)}");
        Console.WriteLine($"Matched types: {matches.Length}");

        foreach (var t in matches)
        {
            Console.WriteLine(t.FullName);

            if (!dumpMembers)
                continue;

            Console.WriteLine($"  Base: {t.BaseType?.FullName}");

            if (t.Interfaces.Count > 0)
                Console.WriteLine($"  Interfaces: {string.Join(", ", t.Interfaces.Select(i => i.InterfaceType.FullName))}");

            foreach (var f in t.Fields.OrderBy(f => f.Name))
                Console.WriteLine($"  F {f.FieldType.FullName} {f.Name}");

            foreach (var m in t.Methods.OrderBy(m => m.Name))
            {
                var pars = string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name));
                Console.WriteLine($"  M {(m.IsStatic ? "static " : "")} {m.ReturnType.FullName} {m.Name}({pars})");
            }
        }

        return 0;
    }

    static System.Collections.Generic.IEnumerable<TypeDefinition> Flatten(TypeDefinition t)
    {
        yield return t;
        foreach (var n in t.NestedTypes.SelectMany(Flatten))
            yield return n;
    }
}

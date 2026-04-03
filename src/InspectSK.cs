using System;
using System.Linq;
using System.Reflection;
using Microsoft.SemanticKernel;

public class Program
{
    public static void Main()
    {
        var type = typeof(Microsoft.SemanticKernel.StreamingFunctionCallUpdateContent);
        Console.WriteLine($"Constructors for {type.FullName}:");
        foreach (var ctor in type.GetConstructors())
        {
            var pars = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"  ({pars})");
        }
    }
}

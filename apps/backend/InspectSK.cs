// using System;
// using System.Linq;
// using System.Reflection;
// using Microsoft.SemanticKernel;

// // Utility script for local diagnostics while investigating Semantic Kernel API shape.
// // Purpose: print constructors of StreamingFunctionCallUpdateContent to verify
// // version-specific signatures during integration debugging.
// // Notes:
// // - Not used by the API runtime.
// // - Safe to keep for troubleshooting; can be removed if no longer needed.

// public class Program
// {
//     public static void Main()
//     {
//         var type = typeof(Microsoft.SemanticKernel.StreamingFunctionCallUpdateContent);
//         Console.WriteLine($"Constructors for {type.FullName}:");
//         foreach (var ctor in type.GetConstructors())
//         {
//             var pars = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
//             Console.WriteLine($"  ({pars})");
//         }
//     }
// }

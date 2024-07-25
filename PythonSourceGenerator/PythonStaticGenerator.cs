﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PythonSourceGenerator.Parser;
using PythonSourceGenerator.Parser.Types;
using PythonSourceGenerator.Reflection;

namespace PythonSourceGenerator;

[Generator(LanguageNames.CSharp)]
public class PythonStaticGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //System.Diagnostics.Debugger.Launch();
        var pythonFilesPipeline = context.AdditionalTextsProvider
            .Where(static text => Path.GetExtension(text.Path) == ".py")
            .Collect();

        context.RegisterSourceOutput(pythonFilesPipeline, static (sourceContext, inputFiles) =>
        {
            foreach (var file in inputFiles)
            {
                // Add environment path
                var @namespace = "Python.Generated"; // TODO: (track) Infer namespace from project

                var fileName = Path.GetFileNameWithoutExtension(file.Path);

                // Convert snakecase to pascal case
                var pascalFileName = string.Join("", fileName.Split('_').Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));

                IEnumerable<MethodDefinition> methods;
                // Read the file
                var code = file.GetText(sourceContext.CancellationToken);

                if (code == null) continue;

                // Parse the Python file
                var result = PythonParser.TryParseFunctionDefinitions(code, out PythonFunctionDefinition[] functions, out GeneratorError[]? errors);

                foreach (var error in errors)
                {
                    // Update text span
                    Location errorLocation = Location.Create(file.Path, TextSpan.FromBounds(0, 1), new LinePositionSpan(new LinePosition(error.StartLine, error.StartColumn), new LinePosition(error.EndLine, error.EndColumn)));
                    sourceContext.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("PSG004", "PythonStaticGenerator", error.Message, "PythonStaticGenerator", DiagnosticSeverity.Error, true), errorLocation));
                }

                if (result) { 
                    methods = ModuleReflection.MethodsFromFunctionDefinitions(functions, fileName);
                    string source = FormatClassFromMethods(@namespace, pascalFileName, methods);
                    sourceContext.AddSource($"{pascalFileName}.py.cs", source);
                    sourceContext.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("PSG002", "PythonStaticGenerator", $"Generated {pascalFileName}.py.cs", "PythonStaticGenerator", DiagnosticSeverity.Info, true), Location.None));
                }
            }
        });
    }

    public static string FormatClassFromMethods(string @namespace, string pascalFileName, IEnumerable<MethodDefinition> methods)
    {
        var paramGenericArgs = methods
            .Select(m => m.ParameterGenericArgs)
            .Where(l => l is not null && l.Any());

        return $$"""
            // <auto-generated/>
            using Python.Runtime;
            using PythonEnvironments;
            using PythonEnvironments.CustomConverters;

            using System;
            using System.Collections.Generic;

            namespace {{@namespace}}
            {
                public static class {{pascalFileName}}Extensions
                {
                    private static readonly I{{pascalFileName}} instance = new {{pascalFileName}}Internal();

                    public static I{{pascalFileName}} {{pascalFileName}}(this IPythonEnvironment env)
                    {
                        return instance;
                    }

                    private class {{pascalFileName}}Internal : I{{pascalFileName}}
                    {
                        {{methods.Select(m => m.Syntax).Compile()}}

                        internal {{pascalFileName}}Internal()
                        {
                            {{InjectConverters(paramGenericArgs)}}
                        }
                    }
                }
                public interface I{{pascalFileName}}
                {
                    {{string.Join(Environment.NewLine, methods.Select(m => m.Syntax).Select(m => $"{m.ReturnType.NormalizeWhitespace()} {m.Identifier.Text}{m.ParameterList.NormalizeWhitespace()};"))}}
                }
            }
            """;
    }

    private static string InjectConverters(IEnumerable<IEnumerable<GenericNameSyntax>> paramGenericArgs)
    {
        List<string> encoders = [];
        List<string> decoders = [];

        foreach (var param in paramGenericArgs)
        {
            Process(encoders, decoders, param);
        }

        return string.Join(Environment.NewLine, encoders.Concat(decoders));

        static void Process(List<string> encoders, List<string> decoders, IEnumerable<GenericNameSyntax> param)
        {
            foreach (var genericArg in param)
            {
                var identifier = genericArg.Identifier.Text;
                var converterType = identifier switch
                {
                    "IEnumerable" => $"ListConverter{genericArg.TypeArgumentList}",
                    "IReadOnlyDictionary" => $"DictionaryConverter{genericArg.TypeArgumentList}",
                    "ValueTuple" => "TupleConverter",
                    _ => throw new NotImplementedException($"No converter for {identifier}")
                };

                var encoder = $"PyObjectConversions.RegisterEncoder(new {converterType}());";
                var decoder = $"PyObjectConversions.RegisterDecoder(new {converterType}());";

                if (!encoders.Contains(encoder))
                {
                    encoders.Add(encoder);
                }

                if (!decoders.Contains(decoder))
                {
                    decoders.Add(decoder);
                }

                // Internally, the DictionaryConverter converts items to a Tuple, so we need the
                // TupleConverter to be registered as well.
                if (identifier == "IReadOnlyDictionary")
                {
                    encoder = $"PyObjectConversions.RegisterEncoder(new TupleConverter());";
                    decoder = $"PyObjectConversions.RegisterDecoder(new TupleConverter());";

                    if (!encoders.Contains(encoder))
                    {
                        encoders.Add(encoder);
                    }

                    if (!decoders.Contains(decoder))
                    {
                        decoders.Add(decoder);
                    }
                }

                var nestedGenerics = genericArg.TypeArgumentList.Arguments.Where(genericArg => genericArg is GenericNameSyntax).Cast<GenericNameSyntax>();
                if (nestedGenerics.Any())
                {
                    Process(encoders, decoders, nestedGenerics);
                }
            }
        }
    }
}
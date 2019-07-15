using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Mappy.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MappyAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MappyAnalyzer";
        public const string Category = "Analysis";
        public const string MessageFormat = "missing assigned properties: {0}";
        public const string Description = "Invalid Mapping";

        /// <summary>
        /// We cant use nameof/typeof of a referenced library becuase then we would need to do voodoo magic with nuget. So as it stands we use hardcoded strings and hope for the best
        /// </summary>
        private static string _mappingAttributeFullName = "Mappy.Core.WarnIfNotMappedCompletelyAttribute";
        //private static string _mappingAttributeFullName = typeof(WarnIfNotMappedCompletelyAttribute).FullName;

        private static string _mappingAttributeName = "WarnIfNotMappedCompletelyAttribute";
        //private static string _mappingAttributeName = nameof(WarnIfNotMappedCompletelyAttribute);

        private static string _mappingAttributeWithoutAttributeSuffix= "WarnIfNotMappedCompletely";
        //private static string _mappingAttributeWithoutAttributeSuffix= nameof(WarnIfNotMappedCompletelyAttribute).Replace("Attribute","");

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSemanticModelAction(AnalyzeSemantic);
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            var compilation = context.Compilation;
            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var result = AnalyzeForMappings(semanticModel, compilation);
                foreach (var item in result)
                {
                    context.ReportDiagnostic(CreateDiagnostic(item));
                };
            }
        }

        private static void AnalyzeSemantic(SemanticModelAnalysisContext context)
        {
            SemanticModel model = context.SemanticModel;
            Compilation compilation = model.Compilation;
            var result = AnalyzeForMappings(model, compilation);
            foreach (var item in result)
            {
                context.ReportDiagnostic(CreateDiagnostic(item));
            };
        }
        private static Diagnostic CreateDiagnostic(KeyValuePair<ISymbol, HashSet<string>> item)
        {
            return Diagnostic.Create(Rule, item.Key.Locations[0], string.Join(", ", item.Value));
        }

        private static Dictionary<ISymbol, HashSet<string>> AnalyzeForMappings(SemanticModel model, Compilation compilation)
        {
            var result = new Dictionary<ISymbol, HashSet<string>>();
            var attribute = compilation.GetTypeByMetadataName(_mappingAttributeFullName);

            var methodDeclarations = model.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
            var methods = methodDeclarations
                .Where(md => md.AttributeLists
                    .Any(al => al.Attributes
                        .Any(a => (a.Name as IdentifierNameSyntax).Identifier.Text == _mappingAttributeWithoutAttributeSuffix)
                        )
                    );

            foreach (var methodSyntax in methods)
            {
                var method = model.GetDeclaredSymbol(methods.First());
                var mappingAttribute = method.GetAttributes().FirstOrDefault(a => a.AttributeClass.MetadataName == _mappingAttributeName);
                List<string> ignoredMembers = ExtractIgnoredMembers(mappingAttribute);

                var targetTypeArguments = method.Parameters.Where(p => p.Type == method.ReturnType);
                if (targetTypeArguments.Any())
                {
                    var targetTypeArgument = targetTypeArguments.Last();
                    var targetPropertyName = targetTypeArgument.Name.ToString();
                    var notMappedMembers = InitializeNotMappedMembers(ignoredMembers, targetTypeArgument);

                    foreach (var statement in methodSyntax.Body.Statements.OfType<ExpressionStatementSyntax>())
                    {
                        var expr = statement.Expression;
                        if (expr is AssignmentExpressionSyntax assignment)
                        {
                            if (assignment.Left is MemberAccessExpressionSyntax ma)
                            {

                                var sourceName = (ma.Expression as IdentifierNameSyntax)?.Identifier.Text;
                                var propertyName = ma?.Name?.Identifier.Text;
                                if (string.Equals(sourceName, targetPropertyName))
                                {
                                    notMappedMembers = notMappedMembers.Where(m => m != propertyName);
                                }
                            }
                        }
                    }
                    if (notMappedMembers.Any())
                    {
                        if (!result.ContainsKey(method))
                        {
                            result.Add(method, new HashSet<string>());
                        }

                        foreach (var item in notMappedMembers)
                        {
                            result[method].Add(item);
                        }
                    }
                }
            }

            return result;

        }

        private static IEnumerable<string> InitializeNotMappedMembers(List<string> ignoredMembers, IParameterSymbol targetTypeArgument)
        {

            var members = GetAllMembers(targetTypeArgument.Type);
            var properties = members.Where(m => !m.IsImplicitlyDeclared && m.Kind == SymbolKind.Property);

            return properties.Where(p => !ignoredMembers.Contains(p.Name)).Select(m => m.Name);
        }

        private static IEnumerable<ISymbol> GetAllMembers(ITypeSymbol type)
        {
            if (type.BaseType != null)
            {
                foreach (var item in GetAllMembers(type.BaseType))
                {
                    yield return item;
                }
            }
            foreach (var item in type.GetMembers())
            {
                yield return item;
            }
        }

        private static List<string> ExtractIgnoredMembers(AttributeData mappingAttribute)
        {
            if (mappingAttribute?.ConstructorArguments.Any() != true)
            {
                return new List<string>();
            }

            var arg = mappingAttribute.ConstructorArguments.First();
            if (arg.Kind == TypedConstantKind.Array && arg.Values != null)
            {
                return arg.Values.Select(v => v.Value as string).ToList();
            }
            if (arg.Kind == TypedConstantKind.Primitive && arg.Value != null)
            {
                return new List<string> { arg.Value as string };
            }
            throw new NotSupportedException("Attributes with these constructor arguments are not supported");
        }
    }
}

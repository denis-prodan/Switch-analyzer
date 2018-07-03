using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SwitchAnalyzer
{
    class InterfaceAnalyzer
    {
        private const string Category = "Correctness";
        public const string InterfaceDiagnosticId = "SA002";
        private const string InterfaceTitle = "Non exhaustive patterns in switch block";
        private const string InterfaceMessageFormat = "Switch case should check interface implementation of type(s): {0}";
        private const string InterfaceDescription = "All interface implementations in pattern matching switch statement should be checked.";
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: InterfaceDiagnosticId,
            title: InterfaceTitle,
            messageFormat: InterfaceMessageFormat, 
            category: Category, 
            defaultSeverity: DiagnosticSeverity.Warning, 
            isEnabledByDefault: true, 
            description: InterfaceDescription);


        public static bool ShouldProceedWithChecks(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            if (PatternMatchingHelper.HasVarDeclaration(caseSyntaxes))
            {
                return false;
            }

            return DefaultCaseCheck.ShouldProceedWithDefault(caseSyntaxes);
        }

        public static IEnumerable<string> GetAllImplementationNames(
            SwitchStatementSyntax switchStatement,
            ITypeSymbol interfaceType,
            SemanticModel semanticModel)
        {
            var allSymbols = semanticModel.LookupSymbols(switchStatement.GetLocation().SourceSpan.Start);
            var namedTypeSymbols = allSymbols.Where(x => x.Kind == SymbolKind.NamedType).OfType<INamedTypeSymbol>();
            var implementations = namedTypeSymbols.Where(namedType => namedType.AllInterfaces.Any(x => x.Name == interfaceType.Name));
            return implementations.Select(x => x.Name);
        }
    }
}

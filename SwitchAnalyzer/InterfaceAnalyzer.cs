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
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(InterfaceDiagnosticId, InterfaceTitle, InterfaceMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: InterfaceDescription);


        public static bool ShouldProceedWithChecks(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            if (HasVarDeclaration(caseSyntaxes))
            {
                return false;
            }

            return DefaultCaseCheck.ShouldProceedWithDefault(caseSyntaxes);
        }

        private static bool HasVarDeclaration(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseExpressions = GetCaseDeclarationPatternSyntaxes(caseSyntaxes);
            var varDeclaration = caseExpressions.Select(x => x.Type)
                .OfType<IdentifierNameSyntax>()
                .Select(x => x.Identifier.Text)
                .FirstOrDefault(x => x == "var");

            return varDeclaration != null;
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

        public static IEnumerable<string> GetCaseValues(IEnumerable<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseExpressions = GetCaseDeclarationPatternSyntaxes(caseSyntaxes);
            var caseValues = caseExpressions.Select(x => x.Type)
                .OfType<IdentifierNameSyntax>()
                .Select(x => x.Identifier.Text);
            return caseValues;
        }

        private static IEnumerable<DeclarationPatternSyntax> GetCaseDeclarationPatternSyntaxes(IEnumerable<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseSwitchSyntaxes = caseSyntaxes.Where(x => x.Labels.FirstOrDefault() is CasePatternSwitchLabelSyntax);
            var caseLabels = caseSwitchSyntaxes.Select(x => x.Labels.FirstOrDefault()).OfType<CasePatternSwitchLabelSyntax>();
            var caseExpressions = caseLabels.Select(x => x.Pattern).OfType<DeclarationPatternSyntax>();
            return caseExpressions;
        }
    }
}

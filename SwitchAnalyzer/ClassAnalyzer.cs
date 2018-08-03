using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace SwitchAnalyzer
{
    class ClassAnalyzer
    {
        private const string Category = "Correctness";
        public const string InterfaceDiagnosticId = "SA003";
        private const string InterfaceTitle = "Non exhaustive patterns in switch block";
        private const string InterfaceMessageFormat = "Switch case should check implementation of type(s): {0}";
        private const string InterfaceDescription = "All class implementations in pattern matching switch statement should be checked.";
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: InterfaceDiagnosticId, 
            title: InterfaceTitle, 
            messageFormat: InterfaceMessageFormat, 
            category: Category, 
            defaultSeverity: DiagnosticSeverity.Warning, 
            isEnabledByDefault: true, 
            description: InterfaceDescription);

        public static bool ShouldProceedWithChecks(SyntaxList<SwitchSectionSyntax> caseSyntaxes, string expressionTypeName)
        {
            if (PatternMatchingHelper.HasVarDeclaration(caseSyntaxes))
            {
                return false;
            }

            if (expressionTypeName == "Object")
            {
                // todo: maybe show warning or info message in this case?
                return false;
            }

            if (HasSameClassDeclaration(caseSyntaxes, expressionTypeName))
            {
                return false;
            }

            return DefaultCaseCheck.ShouldProceedWithDefault(caseSyntaxes);
        }

        private static bool HasSameClassDeclaration(SyntaxList<SwitchSectionSyntax> caseSyntaxes, string className)
        {
            return PatternMatchingHelper.GetCaseValues(caseSyntaxes).Any(x => x == className); 
        }

        public static IEnumerable<SwitchArgumentTypeItem<string>> GetAllImplementationNames(
            SwitchStatementSyntax switchStatement,
            ITypeSymbol className,
            SemanticModel semanticModel)
        {
            var allSymbols = semanticModel.LookupSymbols(switchStatement.GetLocation().SourceSpan.Start);
            var namedTypeSymbols = allSymbols.Where(x => x.Kind == SymbolKind.NamedType).OfType<INamedTypeSymbol>();
            var implementations = namedTypeSymbols
                .Where(namedType => namedType.BaseType?.Name == className.Name 
                                    && !namedType.IsAbstract);
            // todo: Decide what to do with inheritors of abstract class that is inheritor of base class.
            return implementations.Select(x => new SwitchArgumentTypeItem<string>(x.Name, x.Name));
        }
    }
}

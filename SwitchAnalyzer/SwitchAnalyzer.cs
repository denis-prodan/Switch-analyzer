using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SwitchAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SwitchAnalyzer : DiagnosticAnalyzer
    {

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EnumAnalyzer.Rule, InterfaceAnalyzer.Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockAction(Action);
        }

        private static void Action(CodeBlockAnalysisContext context)
        {
            var blockSyntaxes = context.CodeBlock.ChildNodes().OfType<BlockSyntax>(); ;

            var switchStatements = blockSyntaxes.SelectMany(x => x.Statements.OfType<SwitchStatementSyntax>());

            foreach (var switchStatement in switchStatements)
            {
                CheckSwitch(switchStatement, context);
            }
        }

        private static void CheckSwitch(SwitchStatementSyntax switchStatement, CodeBlockAnalysisContext context)
        {
            var expression = switchStatement.Expression;
            var typeInfo = context.SemanticModel.GetTypeInfo(expression);
            var expressionType = typeInfo.ConvertedType;
            if (expressionType.TypeKind == TypeKind.Enum)
            {
                var switchCases = switchStatement.Sections;;
                var shouldProcessWithDefault = EnumAnalyzer.ShouldProceedWithChecks(switchCases);
                if (shouldProcessWithDefault)
                {
                    var allImplementations = EnumAnalyzer.AllEnumValues(expressionType);
                    var caseImplementations = EnumAnalyzer.CaseIdentifiers(switchCases);

                    var notCheckedValues = allImplementations
                        .Where(enumValue => caseImplementations
                            .All(enumInCase => enumInCase != enumValue))
                            .OrderBy(x => x)
                            .ToList();
                    if (notCheckedValues.Any())
                    {
                        var notCoveredEnumTexts = notCheckedValues.Select(caseName => $"{caseName}");
                        var diagnostic = Diagnostic.Create(EnumAnalyzer.Rule, switchStatement.GetLocation(), string.Join(", ", notCoveredEnumTexts));
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }

            if (expressionType.TypeKind == TypeKind.Interface)
            {
                var switchCases = switchStatement.Sections;
                var shouldProcessWithDefault = InterfaceAnalyzer.ShouldProceedWithChecks(switchCases);
                if (shouldProcessWithDefault)
                {
                    var allImplementations = InterfaceAnalyzer.GetAllImplementationNames(switchStatement, expressionType, context.SemanticModel);
                    var caseImplementations = InterfaceAnalyzer.GetCaseValues(switchCases);

                    var notCheckedValues = allImplementations
                        .Where(enumValue => caseImplementations
                            .All(enumInCase => enumInCase != enumValue))
                        .OrderBy(x => x)
                        .ToList();
                    if (notCheckedValues.Any())
                    {
                        var notCoveredEnumTexts = notCheckedValues.Select(caseName => $"{caseName}");
                        var diagnostic = Diagnostic.Create(InterfaceAnalyzer.Rule, switchStatement.GetLocation(), string.Join(", ", notCoveredEnumTexts));
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
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
            var switchCases = switchStatement.Sections;

            if (expressionType.TypeKind == TypeKind.Enum)
            {
                bool ShouldProceed() => EnumAnalyzer.ShouldProceedWithChecks(switchCases);
                IEnumerable<string> AllImplementations() => EnumAnalyzer.AllEnumValues(expressionType);
                IEnumerable<string> CaseImplementations() => EnumAnalyzer.CaseIdentifiers(switchCases);

                ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
            }

            if (expressionType.TypeKind == TypeKind.Interface)
            {
                bool ShouldProceed() => InterfaceAnalyzer.ShouldProceedWithChecks(switchCases);
                IEnumerable<string> AllImplementations() => InterfaceAnalyzer.GetAllImplementationNames(switchStatement, expressionType, context.SemanticModel);
                IEnumerable<string> CaseImplementations() => InterfaceAnalyzer.GetCaseValues(switchCases);

                ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, InterfaceAnalyzer.Rule);
            }

            void ProcessSwitch(Func<bool> shouldProceedFunc,
                Func<IEnumerable<string>> allImplementationsFunc,
                Func<IEnumerable<string>> caseImplementationFunc,
                DiagnosticDescriptor rule) => ProcessSwitchCases(
                shouldProceedFunc: shouldProceedFunc,
                allImplementationsFunc: allImplementationsFunc, 
                caseImplementationFunc: caseImplementationFunc,
                rule: rule, 
                location: switchStatement.GetLocation(),
                context: context);
        }

        private static void ProcessSwitchCases(
            Func<bool> shouldProceedFunc, 
            Func<IEnumerable<string>> allImplementationsFunc,
            Func<IEnumerable<string>> caseImplementationFunc,
            DiagnosticDescriptor rule,
            Location location,
            CodeBlockAnalysisContext context)
        {
            if (shouldProceedFunc == null
                || allImplementationsFunc == null
                || caseImplementationFunc == null
                || rule == null)
                return;

            if (!shouldProceedFunc.Invoke())
                return;

            var allImplementations = allImplementationsFunc.Invoke();
            var caseImplementations = caseImplementationFunc.Invoke();

            var notCheckedValues = allImplementations
                .Where(expectedValue => caseImplementations
                    .All(enumInCase => enumInCase != expectedValue))
                .OrderBy(x => x)
                .ToList();
            if (notCheckedValues.Any())
            {
                var notCoveredEnumTexts = notCheckedValues.Select(caseName => $"{caseName}");
                var diagnostic = Diagnostic.Create(rule, location, string.Join(", ", notCoveredEnumTexts));
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

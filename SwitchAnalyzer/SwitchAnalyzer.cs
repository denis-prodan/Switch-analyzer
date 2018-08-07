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

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EnumAnalyzer.Rule, InterfaceAnalyzer.Rule, ClassAnalyzer.Rule);

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
                IEnumerable<SwitchArgumentTypeItem<int>> AllImplementations() => EnumAnalyzer.AllEnumValues(expressionType);
                IEnumerable<string> CaseImplementations() => EnumAnalyzer.CaseIdentifiers(switchCases);

                ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
            }

            if (expressionType.TypeKind == TypeKind.Interface)
            {
                bool ShouldProceed() => InterfaceAnalyzer.ShouldProceedWithChecks(switchCases);
                IEnumerable<SwitchArgumentTypeItem<string>> AllImplementations() => InterfaceAnalyzer.GetAllImplementationNames(switchStatement, expressionType, context.SemanticModel);
                IEnumerable<string> CaseImplementations() => PatternMatchingHelper.GetCaseValues(switchCases);

                ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, InterfaceAnalyzer.Rule);
            }

            if (expressionType.TypeKind == TypeKind.Class)
            {
                bool ShouldProceed() => ClassAnalyzer.ShouldProceedWithChecks(switchCases, expressionType.Name);
                IEnumerable<SwitchArgumentTypeItem<string>> AllImplementations() => ClassAnalyzer.GetAllImplementationNames(switchStatement, expressionType, context.SemanticModel);
                IEnumerable<string> CaseImplementations() => PatternMatchingHelper.GetCaseValues(switchCases);

                ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, ClassAnalyzer.Rule);
            }

            void ProcessSwitch<T>(Func<bool> shouldProceedFunc,
                Func<IEnumerable<SwitchArgumentTypeItem<T>>> allImplementationsFunc,
                Func<IEnumerable<string>> caseImplementationFunc,
                DiagnosticDescriptor rule) where T: IComparable => ProcessSwitchCases(
                shouldProceedFunc: shouldProceedFunc,
                allImplementationsFunc: allImplementationsFunc,
                caseImplementationFunc: caseImplementationFunc,
                rule: rule,
                location: switchStatement.GetLocation(),
                context: context);
        }

        private static void ProcessSwitchCases<T>(
            Func<bool> shouldProceedFunc, 
            Func<IEnumerable<SwitchArgumentTypeItem<T>>> allImplementationsFunc,
            Func<IEnumerable<string>> caseImplementationFunc,
            DiagnosticDescriptor rule,
            Location location,
            CodeBlockAnalysisContext context) where T: IComparable
        {
            if (shouldProceedFunc == null
                || allImplementationsFunc == null
                || caseImplementationFunc == null
                || rule == null)
                return;

            if (!shouldProceedFunc())
                return;

            var allImplementations = allImplementationsFunc().ToList();

            var obj = new object();
            var caseImplementations = caseImplementationFunc().ToDictionary(x => x, _ => obj);

            var checkedValues = allImplementations
                .Where(expectedValue => caseImplementations.ContainsKey(expectedValue.Name))
                .ToDictionary(x => x.Value, x => obj);

            var notCheckedValues = allImplementations.Where(x =>
                !checkedValues.ContainsKey(x.Value))
                .OrderBy(x => x.Name)
                .ToList();

            if (notCheckedValues.Any())
            {
                var notCoveredValues = notCheckedValues.Select(caseName => caseName.Name);
                var diagnostic = Diagnostic.Create(rule, location, string.Join(", ", notCoveredValues));
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

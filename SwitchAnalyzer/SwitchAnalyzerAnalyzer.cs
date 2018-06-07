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
    public class SwitchAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SwitchAnalyzer";
        private const string Title = "Non exhaustive patterns in switch block";
        private const string MessageFormat = "Switch case should check enum value(s): {0}";
        private const string Description = "All enum cases in switch statement should be checked.";
        private const string Category = "Correctness";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockAction(Action);
        }

        private static void Action(CodeBlockAnalysisContext context)
        {
            var blockSyntaxes = GetBlockSyntaxes(context.CodeBlock);

            var switchStatements = blockSyntaxes.SelectMany(GetSwitchesFromBlock);
            var s = switchStatements.FirstOrDefault();

            foreach (var switchStatement in switchStatements)
            {
                CheckSwitch(switchStatement, context);
            }
        }

        private static void CheckSwitch(SwitchStatementSyntax switchStatement, CodeBlockAnalysisContext context)
        {
            var expression = switchStatement.Expression;
            var s = context.SemanticModel.GetTypeInfo(expression);
            var expressionType = s.ConvertedType;
            if (expressionType.TypeKind == TypeKind.Enum)
            {
                var switchCases = switchStatement.Sections;
                var defaultMember = GetDefaultExpression(switchCases);
                var shouldProcessWithDefault = ShouldProcessWithDefault(defaultMember, context);
                if (shouldProcessWithDefault)
                {
                    var enumSymbols = expressionType.GetMembers().Where(x => x.Kind == SymbolKind.Field);
                    var enumNames = enumSymbols.Select(GetEnumValueName).ToList();
                   
                    var caseExpressions = GetCaseExpressions(switchCases);
                    var allCaseMembers = GetMemeberAccessExpressionSyntaxes(caseExpressions).ToList();
                    var identifiersWithNames = allCaseMembers
                        .Select(GetIdentifierAndName)
                        .Where(x => x.identifier != null && x.name != null);

                    var expressionTypeEnumName = expressionType.Name;
                    var notCheckedValues = enumNames
                        .Where(enumValue => identifiersWithNames
                            .All(enumInCase => enumInCase.name != enumValue 
                                               || enumInCase.identifier != expressionTypeEnumName))
                            .ToList();
                    if (notCheckedValues.Any())
                    {
                        var notCoveredEnumTexts = notCheckedValues.Select(caseName => $"{expressionTypeEnumName}.{caseName}");
                        var diagnostic = Diagnostic.Create(Rule, switchStatement.GetLocation(), string.Join(", ", notCoveredEnumTexts));
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }


        private static (string identifier, string name) GetIdentifierAndName(MemberAccessExpressionSyntax syntax)
        {
            var simpleAccess = syntax.ChildNodes();

            var enumValue = simpleAccess.LastOrDefault() as IdentifierNameSyntax;
            if (simpleAccess.FirstOrDefault() is IdentifierNameSyntax enumType && enumValue != null)
            {
                return (enumType.Identifier.Value.ToString(), enumValue.Identifier.Value.ToString());
            }
            return (null, null);
        }

        private static bool ShouldProcessWithDefault(SwitchSectionSyntax defaultSection, CodeBlockAnalysisContext context)
        {
            if (defaultSection == null)
                return true;

            var statements = defaultSection.Statements;

            return statements.Any(x => IsStatementThrowsNotImplementedExceptionOrInheritor(x, context));
        }

        private static bool IsStatementThrowsNotImplementedExceptionOrInheritor(StatementSyntax statementSyntax, CodeBlockAnalysisContext context)
        {
            if (!(statementSyntax is ThrowStatementSyntax throwStatement))
                return false;

            var typeInfo = context.SemanticModel.GetTypeInfo(throwStatement.Expression);
            return IsNotImplementedException(typeInfo.ConvertedType);
        }

        private static bool IsNotImplementedException(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return false;
            if (typeSymbol.Name == "NotImplementedException")
                return true;
            return IsNotImplementedException(typeSymbol.BaseType);
        }

        private static SwitchSectionSyntax GetDefaultExpression(SyntaxList<SwitchSectionSyntax> caseSyntaxes)
        {
            return caseSyntaxes.FirstOrDefault(x => x.Labels.FirstOrDefault() is DefaultSwitchLabelSyntax);
        }

        private static IEnumerable<MemberAccessExpressionSyntax> GetMemeberAccessExpressionSyntaxes(IEnumerable<ExpressionSyntax> expressions)
        {
            return expressions.SelectMany(GetExpressions);

            IEnumerable<MemberAccessExpressionSyntax> GetExpressions(ExpressionSyntax expression)
            {
                if (expression is MemberAccessExpressionSyntax member)
                {
                    return new[] { member };
                }
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                {
                    return GetExpressions(parenthesized.Expression);
                }
                if (expression is BinaryExpressionSyntax binaryExpression)
                {
                    if (binaryExpression.Kind() == SyntaxKind.BitwiseAndExpression)
                    {
                        var left = GetExpressions(binaryExpression.Left).ToList();
                        var right = GetExpressions(binaryExpression.Right).ToList();
                        if (AreExpressionListsEqual(left, right))
                        {
                            return left;
                        }
                        else
                        {
                            return new MemberAccessExpressionSyntax[0];
                        }
                    }
                    if (binaryExpression.Kind() == SyntaxKind.BitwiseOrExpression)
                    {
                        var left = GetExpressions(binaryExpression.Left);
                        var right = GetExpressions(binaryExpression.Right);
                        return left.Union(right);
                    }
                }
                return new MemberAccessExpressionSyntax[0];
            } 
        }

        private static bool AreExpressionListsEqual(IList<MemberAccessExpressionSyntax> left, IList<MemberAccessExpressionSyntax> right)
        {
            var leftHasRightElements = left.All(l => right.Any(r => AreMemberAccessExpressionsEqual(l, r)));
            var rightHasLeftElements = right.All(r => left.Any(l => AreMemberAccessExpressionsEqual(l, r)));

            return leftHasRightElements && rightHasLeftElements;
        }

        private static bool AreMemberAccessExpressionsEqual(MemberAccessExpressionSyntax left, MemberAccessExpressionSyntax right)
        {
            var leftIdentifier = GetIdentifierAndName(left);
            var rightIdentifier = GetIdentifierAndName(right);
            return $"{leftIdentifier.identifier}.{leftIdentifier.name}" == $"{rightIdentifier.identifier}.{rightIdentifier.name}";
        }
        
        private static IEnumerable<ExpressionSyntax> GetCaseExpressions(IEnumerable<SwitchSectionSyntax> caseSyntaxes)
        {
            var caseSwitchSyntaxes = caseSyntaxes.Where(x => x.Labels.FirstOrDefault() is CaseSwitchLabelSyntax);
            var caseLabels = caseSwitchSyntaxes.Select(x => x.Labels.FirstOrDefault()).OfType<CaseSwitchLabelSyntax>();
            var caseExpressions = caseLabels.Select(x => x.Value);
            return caseExpressions;
        }

        private static string GetEnumValueName(ISymbol symbol) => symbol.Name;

        private static IEnumerable<SwitchStatementSyntax> GetSwitchesFromBlock(BlockSyntax block)
        {
            return block.Statements.OfType<SwitchStatementSyntax>();
        }

        private static IEnumerable<BlockSyntax> GetBlockSyntaxes(SyntaxNode node)
        {
            return node.ChildNodes().OfType<BlockSyntax>();
        }
    }
}

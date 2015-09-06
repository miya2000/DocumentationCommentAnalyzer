using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DocumentationCommentAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DocumentationCommentAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = nameof(DocumentationCommentAnalyzer);
        private const string Category = "Comment";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Resources.AnalyzerTitle, Resources.AnalyzerMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Resources.AnalyzerDescription);
        private static DiagnosticDescriptor RuleBadXml = new DiagnosticDescriptor(DiagnosticId, Resources.AnalyzerTitle, Resources.AnalyzerMessageFormatBadXml, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Resources.AnalyzerDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule, RuleBadXml); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodSyntax = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax);

            var xml = methodSymbol.GetDocumentationCommentXml();
            if (string.IsNullOrEmpty(xml)) return;

            //<!-- Badly formed XML comment ignored for member "M:XX(YY)" -->
            if (xml.StartsWith("<!--"))
            {
                var documentCommentTrivia = methodSyntax.GetLeadingTrivia().Where(n => n.Kind() == SyntaxKind.SingleLineDocumentationCommentTrivia).Last();

                var startLine = context.SemanticModel.SyntaxTree.GetText().Lines.GetLineFromPosition(documentCommentTrivia.Span.Start);
                var diagnosticSpan = TextSpan.FromBounds(startLine.Span.Start, documentCommentTrivia.Span.End);

                var diagnostic = Diagnostic.Create(RuleBadXml, Location.Create(context.SemanticModel.SyntaxTree, diagnosticSpan));
                context.ReportDiagnostic(diagnostic);

                return;
            }

            var commentInfo = ParseDocumentationCommentXml(xml);

            var declarationInfo = CreateDocumentSummary(methodSymbol);

            // allow the omission except public method's <summary>.

            if ((!commentInfo.HasSummary && methodSymbol.DeclaredAccessibility == Accessibility.Public) ||
                (commentInfo.TypeParameterNames.Count > 0 && !commentInfo.TypeParameterNames.SequenceEqual(declarationInfo.TypeParameterNames)) ||
                (commentInfo.ParameterNames.Count > 0 && !commentInfo.ParameterNames.SequenceEqual(declarationInfo.ParameterNames)) ||
                (commentInfo.HasReturns && !declarationInfo.HasReturns))
            {

                var messages = new List<string>();

                if (!commentInfo.HasSummary && methodSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    messages.Add("<summary> requires for public method.");
                }
                if (commentInfo.TypeParameterNames.Count > 0 && !commentInfo.TypeParameterNames.SequenceEqual(declarationInfo.TypeParameterNames))
                {
                    messages.Add("<typeparam> unmatched.");
                }
                if (commentInfo.ParameterNames.Count > 0 && !commentInfo.ParameterNames.SequenceEqual(declarationInfo.ParameterNames))
                {
                    messages.Add("<param> unmatched.");
                }
                if (commentInfo.HasReturns && !declarationInfo.HasReturns)
                {
                    messages.Add("<returns> do not need.");
                }

                var documentCommentTrivia = methodSyntax.GetLeadingTrivia().Where(n => n.Kind() == SyntaxKind.SingleLineDocumentationCommentTrivia).Last();

                var startLine = context.SemanticModel.SyntaxTree.GetText().Lines.GetLineFromPosition(documentCommentTrivia.Span.Start);
                var diagnosticSpan = TextSpan.FromBounds(startLine.Span.Start, methodSyntax.ParameterList.Span.End);

                var diagnostic = Diagnostic.Create(Rule, Location.Create(context.SemanticModel.SyntaxTree, diagnosticSpan), string.Join(" ", messages));

                context.ReportDiagnostic(diagnostic);
            }
        }

        internal class DocumentSummary
        {
            public bool HasSummary { get; set; }
            public bool HasReturns { get; set; }
            public List<string> ParameterNames { get; set; } = new List<string>();
            public List<string> TypeParameterNames { get; set; } = new List<string>();
        }

        internal static DocumentSummary ParseDocumentationCommentXml(string xml)
        {
            var result = new DocumentSummary();

            // Currently, XmlReader is the fastest way to parse XML in C#.
            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        var elementName = reader.Name;
                        if (elementName == "summary")
                        {
                            result.HasSummary = true;
                        }
                        else if (elementName == "typeparam")
                        {
                            result.TypeParameterNames.Add(reader.GetAttribute("name"));
                        }
                        else if (elementName == "param")
                        {
                            result.ParameterNames.Add(reader.GetAttribute("name"));
                        }
                        else if (elementName == "returns")
                        {
                            result.HasReturns = true;
                        }
                    }
                }
            }

            return result;
        }

        internal static DocumentSummary CreateDocumentSummary(IMethodSymbol method)
        {
            var result = new DocumentSummary();

            foreach (var item in method.TypeParameters)
            {
                result.TypeParameterNames.Add(item.Name);
            }
            foreach (var item in method.Parameters)
            {
                //skip in editing.
                if (!string.IsNullOrEmpty(item.Name))
                {
                    result.ParameterNames.Add(item.Name);
                }
            }

            result.HasReturns = !method.ReturnsVoid;

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace DocumentationCommentAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationCommentAnalyzerCodeFixProvider)), Shared]
    public class DocumentationCommentAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DocumentationCommentAnalyzer.DiagnosticId);
        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var methodSyntax = root.FindNode(diagnosticSpan) as MethodDeclarationSyntax;
            var methodSymbol = model.GetDeclaredSymbol(methodSyntax);

            var xml = methodSymbol.GetDocumentationCommentXml();

            //<!-- Badly formed XML comment ignored for member "M:XX(YY)" -->
            if (xml.StartsWith("<!--"))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(Resources.RepairXmlCommentTitle, c => RepairXmlCommentAsync(context.Document, methodSyntax, c), equivalenceKey: Resources.RepairXmlCommentTitle),
                    diagnostic);
            }
            else
            {
                context.RegisterCodeFix(
                    CodeAction.Create(Resources.CodeFixTitle, c => UpdateDocumentCommentAsync(context.Document, methodSyntax, c), equivalenceKey: Resources.CodeFixTitle),
                    diagnostic);
            }
        }

        private async Task<Document> UpdateDocumentCommentAsync(Document document, MethodDeclarationSyntax methodSyntax, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var methodSymbol = model.GetDeclaredSymbol(methodSyntax);

            var xml = methodSymbol.GetDocumentationCommentXml();

            //TODO Implement.

            return document;
        }

        private async Task<Document> RepairXmlCommentAsync(Document document, MethodDeclarationSyntax methodSyntax, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var methodSymbol = model.GetDeclaredSymbol(methodSyntax);

            var xml = methodSymbol.GetDocumentationCommentXml();

            var documentTrivia = methodSyntax.GetLeadingTrivia().Where(n => n.Kind() == SyntaxKind.SingleLineDocumentationCommentTrivia).Last();

            var documentText = documentTrivia.ToFullString().TrimEnd();
            var invalidXml = Regex.Replace(documentText, @"^\s*///", "", RegexOptions.Multiline);

            var newXml = RpairXml(invalidXml);

            var newDocumentCommentText = Regex.Replace(newXml, @"^", "///", RegexOptions.Multiline) + "\r\n";

            var newDocumentTrivia = SyntaxFactory.ParseLeadingTrivia(newDocumentCommentText)[0];

            var newRoot = root.ReplaceTrivia(documentTrivia, newDocumentTrivia.WithAdditionalAnnotations(Formatter.Annotation));

            var newDocument = document.WithSyntaxRoot(newRoot);

            return newDocument;
        }

        public static string RpairXml(string xml)
        {
            // fix exception message language.
            var currentUICulture = CultureInfo.DefaultThreadCurrentUICulture;
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            try
            {
                var retryCount = 20;

                RETRY:

                var orgXml = xml;

                // set ConformanceLevel.Fragment to suppress multiple root elements error. 
                using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Fragment }))
                {
                    var elementStack = new Stack<string>();

                    try
                    {
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                if (!reader.IsEmptyElement)
                                {
                                    elementStack.Push(reader.Name);
                                }
                            }
                            else if (reader.NodeType == XmlNodeType.EndElement)
                            {
                                elementStack.Pop();
                            }
                        }
                    }
                    catch (XmlException ex)
                    {
                        if (--retryCount >= 0)
                        {
                            var message = ex.Message;
                            var position = DocumentationCommentAnalyzerCodeFixProvider.GetPosition(xml, ex.LineNumber, ex.LinePosition);

                            if (message.StartsWith("Name cannot begin with"))
                            {
                                var lessThanIndex = xml.LastIndexOf('<', position - 1);

                                if (lessThanIndex >= 0)
                                {
                                    xml = xml.Substring(0, lessThanIndex) + "&lt;" + xml.Substring(lessThanIndex + 1);
                                }
                            }
                            else if (message.StartsWith("An error occurred while parsing EntityName"))
                            {
                                var lessThanIndex = xml.LastIndexOf('&', position - 1);

                                if (lessThanIndex >= 0)
                                {
                                    xml = xml.Substring(0, lessThanIndex) + "&amp;" + xml.Substring(lessThanIndex + 1);
                                }
                            }
                            else if (message.Contains("does not match the end tag of"))
                            {
                                var rightStr = xml.Substring(position);
                                var endTag = Regex.Match(rightStr, "^\\w*").Value;

                                if (elementStack.Contains(endTag))
                                {
                                    var closingTags = new Stack<string>();
                                    while (true)
                                    {
                                        var tag = elementStack.Pop();
                                        if (tag == endTag) break;
                                        closingTags.Push($"</{tag}>");
                                    }
                                    var insertPos = xml.LastIndexOf('<', position - 1);
                                    xml = xml.Substring(0, insertPos) + string.Join("", closingTags) + xml.Substring(insertPos);
                                }
                                else
                                {
                                    var insertPos = xml.LastIndexOf('<', position - 1);
                                    xml = xml.Substring(0, insertPos) + $"<{endTag}>" + xml.Substring(insertPos);
                                }
                            }
                            else if (message.StartsWith("Unexpected end of file has occurred."))
                            {
                                xml += string.Join("", elementStack.Select(tag => $"</{tag}>"));
                            }
                            else if (message.StartsWith("Unexpected end tag."))
                            {
                                var rightStr = xml.Substring(position);
                                var endTag = Regex.Match(rightStr, "^\\w*").Value;

                                var insertPos = xml.LastIndexOf('<', position - 1);
                                xml = xml.Substring(0, insertPos) + $"<{endTag}>" + xml.Substring(insertPos);
                            }
                            else
                            {
                                // for debug block.
                            }

                            if (xml != orgXml)
                            {
                                goto RETRY;
                            }
                        }
                    }
                }

                return xml;
            }
            finally
            {
                CultureInfo.DefaultThreadCurrentUICulture = currentUICulture;
            }
        }

        public static int GetPosition(string str, int line, int character)
        {
            if (line == 1)
            {
                return character - 1;
            }

            var matches = Regex.Matches(str, @"\r?\n");

            var currentLine = 1;
            foreach (var m in matches.Cast<Match>())
            {
                currentLine++;
                if (currentLine == line)
                {
                    return m.Index + m.Value.Length + character - 1;
                }
            }

            throw new ArgumentException($"str has {matches.Count + 1} lines but specified " + line, "line");
        }
    }
}

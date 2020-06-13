using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace since
{
    class SourceFileWalker : Microsoft.CodeAnalysis.CSharp.CSharpSyntaxWalker
    {
        public static List<ParsedMember> ParseSource(string code)
        {
            var options = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions().WithPreprocessorSymbols("RHINO_SDK").WithDocumentationMode(Microsoft.CodeAnalysis.DocumentationMode.Parse);
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code, options);
            SourceFileWalker sfw = new SourceFileWalker();
            sfw.Visit(tree.GetRoot());
            return sfw._parsedMembers;
        }

        List<ParsedMember> _parsedMembers = new List<ParsedMember>();

        private SourceFileWalker() : base(Microsoft.CodeAnalysis.SyntaxWalkerDepth.StructuredTrivia)
        {
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            _parsedMembers.Add(new ParsedMember(node));
            base.VisitConstructorDeclaration(node);
        }
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            _parsedMembers.Add(new ParsedMember(node));
            base.VisitMethodDeclaration(node);
        }
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            _parsedMembers.Add(new ParsedMember(node));
            base.VisitPropertyDeclaration(node);
        }
        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            _parsedMembers.Add(new ParsedMember(node));
            base.VisitEventFieldDeclaration(node);
        }
        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            //      _parsedMembers.Add(new ParsedMember(node));
            base.VisitEventDeclaration(node);
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            _parsedMembers.Add(new ParsedMember(node));
            base.VisitOperatorDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            _parsedMembers.Add(new ParsedMember(node));
            base.VisitEnumDeclaration(node);
        }
    }
}

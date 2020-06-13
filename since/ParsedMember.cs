using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace since
{
    class ParsedMember
    {
        public ParsedMember(MemberDeclarationSyntax member)
        {
            Member = member;
            Documentation = member.GetLeadingTrivia().Select(i => i.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();
        }
        public DocumentationCommentTriviaSyntax Documentation { get; }
        private MemberDeclarationSyntax Member { get; }

        public bool HasSinceTag()
        {
            bool exists = (Documentation != null && Documentation.Content.ToFullString().Contains("<since>"));
            return exists;
        }

        public int SinceInsertIndex()
        {
            int rc = Member.SpanStart;
            return rc;
        }

        public string Signature
        {
            get
            {
                string className = "";
                string ns = "";
                var parent = Member.Parent.Parent;
                if (Member is EnumDeclarationSyntax)
                    parent = Member.Parent;

                while (parent != null)
                {
                    var parentClassDeclaration = parent as ClassDeclarationSyntax;
                    if (parentClassDeclaration != null)
                    {
                        ns = $"{parentClassDeclaration.Identifier}.{ns}";
                    }
                    var namespaceDeclaration = parent as NamespaceDeclarationSyntax;
                    if (namespaceDeclaration != null)
                    {
                        ns = $"{namespaceDeclaration.Name}.{ns}";
                    }
                    parent = parent.Parent;
                }
                if (ns.Length > 0)
                    className = ns;
                var classDeclaration = Member.Parent as ClassDeclarationSyntax;
                if (classDeclaration != null)
                {
                    className = $"{className}{classDeclaration.Identifier}";
                }
                var interfaceDeclaration = Member.Parent as InterfaceDeclarationSyntax;
                if (interfaceDeclaration != null)
                {
                    className = $"{className}{interfaceDeclaration.Identifier}";
                }
                var structDeclaration = Member.Parent as StructDeclarationSyntax;
                if (structDeclaration != null)
                {
                    className = $"{className}{structDeclaration.Identifier}";
                }
                var enumDeclaration = Member as EnumDeclarationSyntax;
                if (enumDeclaration != null)
                {
                    className = $"{ns}{enumDeclaration.Identifier}";
                }
                

                if (classDeclaration == null && interfaceDeclaration == null && structDeclaration == null && enumDeclaration == null)
                    throw new System.NotImplementedException();

                {
                    MethodDeclarationSyntax method = Member as MethodDeclarationSyntax;
                    if (method != null)
                    {
                        var signature = new System.Text.StringBuilder();
                        signature.Append($"{className}.{method.Identifier}(");
                        int parameterCount = method.ParameterList.Parameters.Count;
                        for (int i = 0; i < parameterCount; i++)
                        {
                            if (i > 0)
                                signature.Append(",");
                            var parameter = method.ParameterList.Parameters[i];
                            string paramType = parameter.Type.ToString();
                            int angleIndex = paramType.IndexOf('<');
                            string prefixType = "";
                            if (angleIndex > 0)
                            {
                                prefixType = paramType.Substring(0, angleIndex);
                                int prefixIndex = prefixType.LastIndexOf('.');
                                if (prefixIndex > 0)
                                    prefixType = prefixType.Substring(prefixIndex + 1);
                                string genericType = paramType.Substring(angleIndex + 1);
                                int genericIndex = genericType.LastIndexOf('.');
                                if (genericIndex > 0)
                                    genericType = genericType.Substring(genericIndex + 1);
                                paramType = prefixType + "<" + genericType;
                            }
                            else
                            {
                                int index = paramType.LastIndexOf('.');
                                if (index > 0)
                                    paramType = paramType.Substring(index + 1);
                            }
                            signature.Append(paramType);
                        }
                        signature.Append(")");
                        return signature.ToString();
                    }
                }
                {
                    PropertyDeclarationSyntax property = Member as PropertyDeclarationSyntax;
                    if (property != null)
                    {
                        var signature = new System.Text.StringBuilder();
                        signature.Append($"{className}.{property.Identifier}");
                        return signature.ToString();
                    }
                }
                {
                    EventDeclarationSyntax evt = Member as EventDeclarationSyntax;
                    if (evt != null)
                    {
                        var signature = new System.Text.StringBuilder();
                        signature.Append($"{className}.{evt.Identifier}");
                        return signature.ToString();
                    }
                }
                {
                    OperatorDeclarationSyntax op = Member as OperatorDeclarationSyntax;
                    if (op != null)
                    {
                        var signature = new System.Text.StringBuilder();
                        signature.Append($"{className}.{op.OperatorToken}");
                        return signature.ToString();
                    }
                }
                {
                    EventFieldDeclarationSyntax eventField = Member as EventFieldDeclarationSyntax;
                    if (eventField != null)
                    {
                        var signature = new System.Text.StringBuilder();
                        string declaration = eventField.ToString();
                        int index = declaration.LastIndexOf(' ');
                        declaration = declaration.Substring(index + 1, declaration.Length - 1 - (index + 1));
                        signature.Append($"{className}.{declaration}");
                        return signature.ToString();
                    }
                }
                {
                    ConstructorDeclarationSyntax constructor = Member as ConstructorDeclarationSyntax;
                    if (constructor != null)
                    {
                        var signature = new System.Text.StringBuilder();
                        signature.Append($"{className}(");
                        int parameterCount = constructor.ParameterList.Parameters.Count;
                        for (int i = 0; i < parameterCount; i++)
                        {
                            if (i > 0)
                                signature.Append(",");
                            var parameter = constructor.ParameterList.Parameters[i];
                            string paramType = parameter.Type.ToString();
                            int angleIndex = paramType.IndexOf('<');
                            string prefixType = "";
                            if (angleIndex > 0)
                            {
                                prefixType = paramType.Substring(0, angleIndex);
                                int prefixIndex = prefixType.LastIndexOf('.');
                                if (prefixIndex > 0)
                                    prefixType = prefixType.Substring(prefixIndex + 1);
                                string genericType = paramType.Substring(angleIndex + 1);
                                int genericIndex = genericType.LastIndexOf('.');
                                if (genericIndex > 0)
                                    genericType = genericType.Substring(genericIndex + 1);
                                paramType = prefixType + "<" + genericType;
                            }
                            else
                            {
                                int index = paramType.LastIndexOf('.');
                                if (index > 0)
                                    paramType = paramType.Substring(index + 1);
                            }
                            signature.Append(paramType);
                        }
                        signature.Append(")");
                        return signature.ToString();
                    }
                }
                {
                    EnumDeclarationSyntax enumDecl = Member as EnumDeclarationSyntax;
                    if( enumDecl != null)
                    {
                        return className;
                    }
                }
                throw new System.NotImplementedException();
            }
        }
    }
}

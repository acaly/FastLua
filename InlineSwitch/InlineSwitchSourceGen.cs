using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InlineSwitch
{
    [Generator]
    public sealed class InlineSwitchSourceGen : ISourceGenerator
    {
#pragma warning disable RS2008 // Enable analyzer release tracking
        public static readonly DiagnosticDescriptor TestDiagnostic = new DiagnosticDescriptor("ILS1001",
            "Debug diagnostic", "Message: {0}",
            "InlineSwitch", DiagnosticSeverity.Warning, true);
        public static readonly DiagnosticDescriptor MultipleSwitch = new DiagnosticDescriptor("ILS2001",
            "Multiple switch statements", "There can only be one switch statement in the transformed method.",
            "InlineSwitch", DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor MultipleDefaultImpl = new DiagnosticDescriptor("ILS2002",
            "Multiple default implementations", "There can only be one implementation method marked with [InlineSwitchCase] without value.",
            "InlineSwitch", DiagnosticSeverity.Error, true);
#pragma warning restore RS2008 // Enable analyzer release tracking

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                var model = context.Compilation.GetSemanticModel(tree);
                Scan(context, model, tree.GetRoot());
            }
        }

        private void Scan(GeneratorExecutionContext context, SemanticModel model, SyntaxNode root)
        {
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (!method.Identifier.ValueText.EndsWith("_Template")) continue;
                foreach (var attr in method.AttributeLists.SelectMany(l => l.Attributes))
                {
                    var attrType = model.GetTypeInfo(attr).Type;
                    if (attrType.GetFullName() == "FastLua.VM.InlineSwitchAttribute")
                    {
                        Generate(context, model, method, root);
                    }
                }
            }
        }

        private static void Generate(GeneratorExecutionContext context, SemanticModel model,
            MethodDeclarationSyntax method, SyntaxNode root)
        {
            var builder = new StringBuilder();
            var methodSymbol = model.GetDeclaredSymbol(method);
            var declType = methodSymbol.ContainingType;

            var impl = methodSymbol.GetAttributes()
                .First(attr => attr.AttributeClass.GetFullName() == "FastLua.VM.InlineSwitchAttribute")
                .ConstructorArguments[0].Value as ITypeSymbol;

            string accessModifier;
            switch(methodSymbol.DeclaredAccessibility)
            {
            case Accessibility.Public: accessModifier = "public "; break;
            case Accessibility.Private: accessModifier = "private "; break;
            case Accessibility.Protected: accessModifier = "protected "; break;
            default: accessModifier = ""; break;
            };

            string staticModifier = methodSymbol.IsStatic ? "static " : "";

            var returnType = methodSymbol.ReturnType.ToDisplayString();
            string MakeRef(RefKind k) => k == RefKind.Ref ? "ref " : k == RefKind.Out ? "out " :
                k == RefKind.RefReadOnly ? "ref readonly " : "";
            var parameters = methodSymbol.Parameters.Select(p => $"{MakeRef(p.RefKind)}{p.Type.ToDisplayString()} {p.Name}");
            var transformedName = methodSymbol.Name.Substring(0, methodSymbol.Name.Length - 9);

            //Copy using directives.
            foreach (var u in root.ChildNodes().OfType<UsingDirectiveSyntax>())
            {
                builder.AppendLine(u.ToFullString());
            }

            var findSwitches = method.Body.DescendantNodes().OfType<SwitchStatementSyntax>();
            var theSwitch = findSwitches.FirstOrDefault();
            if (theSwitch is null || findSwitches.Skip(1).Any())
            {
                context.ReportDiagnostic(Diagnostic.Create(MultipleSwitch, method.GetLocation()));
                return;
            }
            var replaced = method.Body.ReplaceNode(theSwitch, MakeNewSwitch(context, theSwitch, impl));

            //Generate code.
            builder.AppendLine($"namespace {declType.GetFullNamespaceName()}");
            builder.AppendLine(@"{");
            builder.AppendLine($"    partial class {declType.Name}");
            builder.AppendLine(@"    {");
            builder.AppendLine($"        {accessModifier}{staticModifier}partial {returnType} {transformedName}({string.Join(", ", parameters)})");
            builder.AppendLine(@"        {");

            builder.AppendLine(replaced.ToFullString());

            builder.AppendLine(@"        }");
            builder.AppendLine(@"    }");
            builder.AppendLine(@"}");

            context.AddSource(method.Identifier.ValueText + "_InlineSwitch.cs", builder.ToString());
        }

        private static SyntaxNode MakeNewSwitch(GeneratorExecutionContext context, SwitchStatementSyntax old, ITypeSymbol impl)
        {
            try
            {
                return SyntaxFactory.SwitchStatement(old.Expression).AddSections(MakeSections(context, impl).ToArray());
            }
            catch (Exception e)
            {
                context.ReportDiagnostic(Diagnostic.Create(TestDiagnostic, null, "Err " + e.ToString()));
                return old;
            }
        }

        private static IEnumerable<SwitchSectionSyntax> MakeSections(GeneratorExecutionContext context, ITypeSymbol impl)
        {
            foreach (var method in impl.GetMembers().OfType<IMethodSymbol>())
            {
                var attr = method.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass.GetFullName() == "FastLua.VM.InlineSwitchCaseAttribute");
                if (attr is null) continue;

                var body = (MethodDeclarationSyntax)method.DeclaringSyntaxReferences.Single().GetSyntax();
                var replacedSwitch = body.DescendantNodes().OfType<SwitchStatementSyntax>().OrderBy(s => s.Span.Start).First();
                foreach (var section in replacedSwitch.Sections)
                {
                    yield return section;
                }
            }
        }
    }
}

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace InlineSwitch
{
    internal static class RoslynHelpers
    {
        public static string GetFullName(this ITypeSymbol symbol, string typeConcat = ".")
        {
            try
            {
                var ret = symbol.Name;
                while (symbol.ContainingType != null)
                {
                    symbol = symbol.ContainingType;
                    ret = symbol.Name + typeConcat + ret;
                }
                var ns = symbol.ContainingNamespace;
                while (ns != null)
                {
                    ret = ns.Name + "." + ret;
                    ns = ns.ContainingNamespace;
                }
                return ret.Substring(1);
            }
            catch
            {
                return null;
            }
        }

        public static string GetFullNamespaceName(this ITypeSymbol symbol)
        {
            try
            {
                var ret = string.Empty;
                var ns = symbol.ContainingNamespace;
                while (ns != null)
                {
                    ret = ns.Name + "." + ret;
                    ns = ns.ContainingNamespace;
                }
                return ret.Substring(1, ret.Length - 2);
            }
            catch
            {
                return null;
            }
        }

    }
}

using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FastLua.Parser.SyntaxTreeBuilder;
using Token = FastLua.Parser.GenericToken<FastLua.Parser.LuaTokenType>;

namespace FastLua.Parser
{
    public class LuaParser
    {
        private LuaTokenizer _input;
        private SyntaxTreeBuilder _output;
        private readonly List<string> _paramNameList = new();

        public void Reset(LuaTokenizer input, SyntaxTreeBuilder output)
        {
            _input = input;
            _output = output;
        }

        //Entry
        public void Parse()
        {
            var t = _input.Current;
            StatList(ref t);
        }

        private void Next(ref Token t)
        {
            _input.EnsureMoveNext();
            t = _input.Current;
        }

        private static void Check(ref Token t, LuaTokenType type)
        {
            if (t.Type != type)
            {
                throw new LuaCompilerException("Expecting " + type.ToReadableString());
            }
        }

        private void CheckAndNext(ref Token t, LuaTokenType type)
        {
            Check(ref t, type);
            Next(ref t);
        }

        private bool TestAndNext(ref Token t, LuaTokenType type)
        {
            if (t.Type == type)
            {
                Next(ref t);
                return true;
            }
            return false;
        }

        #region stat

        private static bool BlockFollow(Token t, bool withUntil)
        {
            return t.Type switch
            {
                LuaTokenType.EOS => true,
                LuaTokenType.Else => true,
                LuaTokenType.Elseif => true,
                LuaTokenType.End => true,
                LuaTokenType.Eos => true,
                LuaTokenType.Until => withUntil,
                _ => false,
            };
        }

        private void StatList(ref Token t)
        {
            while (!BlockFollow(t, withUntil: true))
            {
                if (t.Type == LuaTokenType.Return)
                {
                    RetStat(ref t);
                    return;
                }
                Statement(ref t);
            }
        }

        public void Statement(ref Token t)
        {
            switch (t.Type)
            {
            case (LuaTokenType)';':
                Next(ref t);
                break;
            case LuaTokenType.If:
                IfStat(ref t);
                break;
            case LuaTokenType.While:
                WhileStat(ref t);
                break;
            case LuaTokenType.Do:
                SimpleBlock(ref t);
                break;
            case LuaTokenType.For:
                ForStat(ref t);
                break;
            case LuaTokenType.Repeat:
                RepeatStat(ref t);
                break;
            case LuaTokenType.Function:
                FuncStat(ref t);
                break;
            case LuaTokenType.Local:
                if (_input.TryPeek(1, out var nextT) && nextT.Type == LuaTokenType.Function)
                {
                    LocalFuncStat(ref t);
                }
                else
                {
                    LocalStat(ref t);
                }
                break;
            case LuaTokenType.Dbcolon:
                LabelStat(ref t);
                break;
            case LuaTokenType.Return:
                RetStat(ref t);
                break;
            case LuaTokenType.Break:
            case LuaTokenType.Goto:
                GotoBreakStatement(ref t);
                break;
            default:
                ExprStat(ref t);
                break;
            }
        }

        private void SimpleBlock(ref Token t)
        {
            Next(ref t);
            var block = _output.CurrentBlock.OpenSimpleBlock();
            StatList(ref t);
            CheckAndNext(ref t, LuaTokenType.End);
            _output.CurrentBlock.Close(block);
        }

        private void ExprStat(ref Token t)
        {
            var e = SuffixExpr(ref t);
            if (t.Type == (LuaTokenType)'=' ||
                t.Type == (LuaTokenType)',')
            {
                if (e is VariableSyntaxNode v)
                {
                    Assignment(ref t, v);
                }
                else
                {
                    throw new LuaCompilerException("Invalid assignment statement.");
                }
            }
            else
            {
                if (e is InvocationExpressionSyntaxNode i)
                {
                    _output.CurrentBlock.Add(new InvocationStatementSyntaxNode() { Invocation = i });
                }
                else
                {
                    throw new LuaCompilerException("Expression statement must be assignment or function call.");
                }
            }
        }

        private void Assignment(ref Token t, VariableSyntaxNode v)
        {
            var assignment = new AssignmentStatementSyntaxNode()
            {
                Variables = { v },
            };
            while (TestAndNext(ref t, (LuaTokenType)','))
            {
                var enext = SuffixExpr(ref t);
                if (enext is VariableSyntaxNode vnext)
                {
                    assignment.Variables.Add(vnext);
                }
                else
                {
                    throw new LuaCompilerException("Invalid assignment statement");
                }
            }
            CheckAndNext(ref t, (LuaTokenType)'=');
            assignment.Values = ExprList(ref t);
            _output.CurrentBlock.Add(assignment);
        }

        private void GotoBreakStatement(ref Token t)
        {
            if (TestAndNext(ref t, LuaTokenType.Goto))
            {
                Check(ref t, LuaTokenType.Name);
                _output.CurrentBlock.GotoNamedLabel(t.Content);
                Next(ref t);
            }
            else
            {
                _output.CurrentBlock.GotoBreakLabel();
            }
        }

        private void RetStat(ref Token t)
        {
            Next(ref t);
            if (BlockFollow(t, withUntil: true) || t.Type == (LuaTokenType)';')
            {
                //Empty.
                _output.CurrentBlock.Add(new ReturnStatementSyntaxNode()
                {
                    Values = new ExpressionListSyntaxNode(),
                });
            }
            else
            {
                _output.CurrentBlock.Add(new ReturnStatementSyntaxNode()
                {
                    Values = ExprList(ref t),
                });
            }
            TestAndNext(ref t, (LuaTokenType)';');
        }

        private void LabelStat(ref Token t)
        {
            Next(ref t);
            Check(ref t, LuaTokenType.Name);
            _output.CurrentBlock.MarkLabel(t.Content);
            Next(ref t);
            CheckAndNext(ref t, LuaTokenType.Dbcolon);
        }

        private void LocalStat(ref Token t)
        {
            Next(ref t);
            var assignment = new LocalStatementSyntaxNode();
            do
            {
                Check(ref t, LuaTokenType.Name);
                var v = _output.CurrentBlock.NewVariable(t.Content, assignment);
                assignment.Variables.Add(v);
                Next(ref t);
            } while (TestAndNext(ref t, (LuaTokenType)','));
            if (TestAndNext(ref t, (LuaTokenType)'='))
            {
                assignment.ExpressionList = ExprList(ref t);
            }
            _output.CurrentBlock.Add(assignment);
        }

        private void LocalFuncStat(ref Token t)
        {
            Next(ref t); //local
            Next(ref t); //function
            Check(ref t, LuaTokenType.Name);
            var assignment = new LocalStatementSyntaxNode();
            var v = _output.CurrentBlock.NewVariable(t.Content, assignment);
            Next(ref t);
            assignment.Variables.Add(v);
            assignment.ExpressionList = new ExpressionListSyntaxNode()
            {
                Expressions = { Body(ref t, hasSelf: false) },
            };
            _output.CurrentBlock.Add(assignment);
        }

        private void FuncStat(ref Token t)
        {
            Next(ref t);
            var v = FuncName(ref t, out var hasSelf);
            var body = Body(ref t, hasSelf);
            var assignment = new AssignmentStatementSyntaxNode()
            {
                Variables = { v },
                Values = new ExpressionListSyntaxNode()
                {
                    Expressions = { body },
                },
            };
            _output.CurrentBlock.Add(assignment);
        }

        private VariableSyntaxNode FuncName(ref Token t, out bool hasSelf)
        {
            hasSelf = false;
            Check(ref t, LuaTokenType.Name);
            var n = _output.CurrentBlock.GetVariable(t.Content);
            Next(ref t);
            while (TestAndNext(ref t, (LuaTokenType)'.'))
            {
                Check(ref t, LuaTokenType.Name);
                n = new IndexVariableSyntaxNode()
                {
                    Table = n,
                    Key = new LiteralExpressionSyntaxNode()
                    {
                        StringValue = t.Content.ToString(),
                    },
                };
                Next(ref t);
            }
            if (TestAndNext(ref t, (LuaTokenType)':'))
            {
                hasSelf = true;
                Check(ref t, LuaTokenType.Name);
                n = new IndexVariableSyntaxNode()
                {
                    Table = n,
                    Key = new LiteralExpressionSyntaxNode()
                    {
                        StringValue = t.Content.ToString(),
                    },
                };
                Next(ref t);
            }
            return n;
        }

        private void RepeatStat(ref Token t)
        {
            Next(ref t);
            var block = _output.CurrentBlock.OpenRepeatBlock();
            StatList(ref t);
            CheckAndNext(ref t, LuaTokenType.Until);
            block.StopCondition = Expr(ref t); //Include the expr inside the block.
            _output.CurrentBlock.Close(block);
        }

        private void ForStat(ref Token t)
        {
            Next(ref t); //for
            //var auxBlock = _output.CurrentBlock.OpenAuxBlock();
            Check(ref t, LuaTokenType.Name);
            if (_input.TryPeek(1, out var nextToken) && nextToken.Type == (LuaTokenType)'=')
            {
                ForNum(ref t);
            }
            else
            {
                ForList(ref t);
            }
        }

        private void ForList(ref Token t)
        {
            var forBlock = _output.CurrentBlock.OpenGenericForBlock();

            forBlock.HiddenVariableF = _output.CurrentBlock.NewVariable(default, forBlock);
            forBlock.HiddenVariableS = _output.CurrentBlock.NewVariable(default, forBlock);
            forBlock.HiddenVariableV = _output.CurrentBlock.NewVariable(default, forBlock);

            do
            {
                Check(ref t, LuaTokenType.Name);
                var v = _output.CurrentBlock.NewVariable(t.Content, forBlock);
                forBlock.LoopVariables.Add(v);
                Next(ref t);
            }
            while (TestAndNext(ref t, (LuaTokenType)','));
            CheckAndNext(ref t, LuaTokenType.In);
            forBlock.ExpressionList = ExprList(ref t);
            var auxBlock = _output.CurrentBlock.OpenAuxBlock();
            CheckAndNext(ref t, LuaTokenType.Do);
            StatList(ref t);
            CheckAndNext(ref t, LuaTokenType.End);
            _output.CurrentBlock.Close(auxBlock);
            _output.CurrentBlock.Close(forBlock);
        }

        private void ForNum(ref Token t)
        {
            var forBlock = _output.CurrentBlock.OpenNumericForBlock();
            var v = _output.CurrentBlock.NewVariable(t.Content, forBlock); //This is a name, confirmed by ForStat.
            forBlock.Variable = v;
            Next(ref t);
            Next(ref t); //Skip the '=', confirmed by ForStat.
            forBlock.From = Expr(ref t);
            CheckAndNext(ref t, (LuaTokenType)',');
            forBlock.To = Expr(ref t);
            if (TestAndNext(ref t, (LuaTokenType)','))
            {
                forBlock.Step = Expr(ref t);
            }
            CheckAndNext(ref t, LuaTokenType.Do);
            var auxBlock = _output.CurrentBlock.OpenAuxBlock();
            StatList(ref t);
            CheckAndNext(ref t, LuaTokenType.End);
            _output.CurrentBlock.Close(auxBlock);
            _output.CurrentBlock.Close(forBlock);
        }

        private void WhileStat(ref Token t)
        {
            Next(ref t);
            var block = _output.CurrentBlock.OpenWhileBlock(Expr(ref t));
            CheckAndNext(ref t, LuaTokenType.Do);
            StatList(ref t);
            _output.CurrentBlock.Close(block);
            CheckAndNext(ref t, LuaTokenType.End);
        }

        private void IfStat(ref Token t)
        {
            var ret = _output.CurrentBlock.AddIfStatement();
            do
            {
                Next(ref t); //Skip if or elseif.
                var block = _output.CurrentBlock.OpenThenElseBlock(Expr(ref t));
                ret.Clauses.Add(block);
                CheckAndNext(ref t, LuaTokenType.Then);
                StatList(ref t);
                _output.CurrentBlock.Close(block);
            } while (t.Type == LuaTokenType.Elseif);
            if (TestAndNext(ref t, LuaTokenType.Else))
            {
                var block = _output.CurrentBlock.OpenThenElseBlock(null);
                ret.Clauses.Add(block);
                StatList(ref t);
                _output.CurrentBlock.Close(block);
            }
            CheckAndNext(ref t, LuaTokenType.End);
        }

        #endregion

        #region expr

        private FunctionExpressionSyntaxNode Body(ref Token t, bool hasSelf)
        {
            bool hasVararg = false;
            var p = _paramNameList;
            p.Clear();
            CheckAndNext(ref t, (LuaTokenType)'(');
            if (!TestAndNext(ref t, (LuaTokenType)')'))
            {
                do
                {
                    if (TestAndNext(ref t, LuaTokenType.Dots))
                    {
                        hasVararg = true;
                        break;
                    }
                    Check(ref t, LuaTokenType.Name);
                    p.Add(t.Content.ToString());
                    Next(ref t);
                } while (TestAndNext(ref t, (LuaTokenType)','));

                CheckAndNext(ref t, (LuaTokenType)')');
            }
            var func = _output.OpenFunction(p, hasSelf, hasVararg);
            StatList(ref t);
            CheckAndNext(ref t, LuaTokenType.End);
            return _output.CloseFunction(func);
        }

        private static UnaryOperator ConvUnOpr(LuaTokenType t)
        {
            return t switch
            {
                LuaTokenType.Not => UnaryOperator.Not,
                (LuaTokenType)'-' => UnaryOperator.Neg,
                (LuaTokenType)'#' => UnaryOperator.Num,
                _ => UnaryOperator.Unknown,
            };
        }

        private static BinaryOperator ConvBinOpr(LuaTokenType t)
        {
            return t switch
            {
                (LuaTokenType)'+' => BinaryOperator.Add,
                (LuaTokenType)'-' => BinaryOperator.Min,
                (LuaTokenType)'*' => BinaryOperator.Mul,
                (LuaTokenType)'/' => BinaryOperator.Div,
                (LuaTokenType)'%' => BinaryOperator.Mod,
                (LuaTokenType)'^' => BinaryOperator.Pow,
                LuaTokenType.Concat => BinaryOperator.Conc,
                LuaTokenType.Ne => BinaryOperator.NE,
                LuaTokenType.Eq => BinaryOperator.E,
                (LuaTokenType)'<' => BinaryOperator.L,
                LuaTokenType.Le => BinaryOperator.LE,
                (LuaTokenType)'>' => BinaryOperator.G,
                LuaTokenType.Ge => BinaryOperator.GE,
                LuaTokenType.And => BinaryOperator.And,
                LuaTokenType.Or => BinaryOperator.Or,
                _ => BinaryOperator.Unknown,
            };
        }

        private ExpressionListSyntaxNode ExprList(ref Token t)
        {
            var ret = new ExpressionListSyntaxNode();
            do
            {
                ret.Expressions.Add(Expr(ref t));
            } while (TestAndNext(ref t, (LuaTokenType)','));
            if (ret.Expressions.Count > 0)
            {
                var lastExpr = ret.Expressions[^1];
                if (lastExpr is InvocationExpressionSyntaxNode || lastExpr is VarargExpressionSyntaxNode)
                {
                    lastExpr.ReceiverMultiRetState = ExpressionReceiverMultiRetState.Variable;
                }
            }
            return ret;
        }

        private ExpressionSyntaxNode Expr(ref Token t)
        {
            return SubExpr(ref t, limit: 0);
        }

        private ExpressionSyntaxNode SubExpr(ref Token t, int limit)
        {
            ExpressionSyntaxNode e;
            var un = ConvUnOpr(t.Type);
            if (un != UnaryOperator.Unknown)
            {
                Next(ref t);
                e = SubExpr(ref t, UnaryExpressionSyntaxNode.Priority);
                e = new UnaryExpressionSyntaxNode()
                {
                    Operand = e,
                    Operator = un,
                };
            }
            else
            {
                e = SimpleExpr(ref t);
            }
            var bn = ConvBinOpr(t.Type);
            while (bn.V != BinaryOperator.Raw.Unknown && bn.PL > limit)
            {
                Next(ref t);
                var rhs = SubExpr(ref t, bn.PR);
                e = new BinaryExpressionSyntaxNode()
                {
                    Left = e,
                    Right = rhs,
                    Operator = bn,
                };
                bn = ConvBinOpr(t.Type);
            }
            return e;
        }

        private ExpressionSyntaxNode SimpleExpr(ref Token t)
        {
            ExpressionSyntaxNode ret;
            switch (t.Type)
            {
            case LuaTokenType.Number:
            {
                var i = LuaParserHelper.ParseInteger(t.Content);
                var d = LuaParserHelper.ParseNumber(t.Content);
                ret = new LiteralExpressionSyntaxNode()
                {
                    Int32Value = i ?? 0, //Set int first so double's type will overwrite.
                    DoubleValue = d.Value,
                };
                Next(ref t);
                return ret;
            }
            case LuaTokenType.String:
                ret = new LiteralExpressionSyntaxNode()
                {
                    StringValue = t.Content.ToString(),
                };
                Next(ref t);
                return ret;
            case LuaTokenType.Nil:
                ret = new LiteralExpressionSyntaxNode()
                {
                    SpecializationType = new() { LuaType = SpecializationLuaType.Nil },
                };
                Next(ref t);
                return ret;
            case LuaTokenType.True:
                ret = new LiteralExpressionSyntaxNode()
                {
                    BoolValue = true,
                };
                Next(ref t);
                return ret;
            case LuaTokenType.False:
                ret = new LiteralExpressionSyntaxNode()
                {
                    BoolValue = false,
                };
                Next(ref t);
                return ret;
            case LuaTokenType.Dots:
                ret = new VarargExpressionSyntaxNode();
                Next(ref t);
                return ret;
            case (LuaTokenType)'{':
                return Constructor(ref t);
            case LuaTokenType.Function:
            {
                Next(ref t);
                return Body(ref t, false);
            }
            default:
                return SuffixExpr(ref t);
            }
        }

        private ExpressionSyntaxNode SuffixExpr(ref Token t)
        {
            var e = PrimaryExpr(ref t);
            while (true)
            {
                switch (t.Type)
                {
                case (LuaTokenType)'.':
                {
                    Next(ref t);
                    Check(ref t, LuaTokenType.Name);
                    e = new IndexVariableSyntaxNode()
                    {
                        Table = e,
                        Key = new LiteralExpressionSyntaxNode()
                        {
                            StringValue = t.Content.ToString(),
                        },
                    };
                    Next(ref t);
                    break;
                }
                case (LuaTokenType)'[':
                {
                    Next(ref t);
                    e = new IndexVariableSyntaxNode()
                    {
                        Table = e,
                        Key = Expr(ref t),
                    };
                    CheckAndNext(ref t, (LuaTokenType)']');
                    break;
                }
                case (LuaTokenType)':':
                {
                    Next(ref t);
                    Check(ref t, LuaTokenType.Name);
                    e = new IndexVariableSyntaxNode()
                    {
                        Table = e,
                        Key = new LiteralExpressionSyntaxNode()
                        {
                            StringValue = t.Content.ToString(),
                        },
                    };
                    Next(ref t);
                    e = FunctionArgs(ref t, e, hasSelf: true);
                    break;
                }
                case (LuaTokenType)'(':
                case (LuaTokenType)'{':
                case LuaTokenType.String:
                    e = FunctionArgs(ref t, e, hasSelf: false);
                    break;
                default:
                    return e;
                }
            }
        }

        private ExpressionSyntaxNode PrimaryExpr(ref Token t)
        {
            ExpressionSyntaxNode ret;
            switch (t.Type)
            {
            case LuaTokenType.Name:
                ret = _output.CurrentBlock.GetVariable(t.Content);
                Next(ref t);
                return ret;
            case (LuaTokenType)'(':
                Next(ref t);
                ret = Expr(ref t);
                CheckAndNext(ref t, (LuaTokenType)')');
                return ret;
            default:
                throw new LuaCompilerException("Expecting expression");
            }
        }

        private ExpressionSyntaxNode FunctionArgs(ref Token t, ExpressionSyntaxNode func, bool hasSelf)
        {
            var ret = new InvocationExpressionSyntaxNode()
            {
                Function = func,
                HasSelf = hasSelf,
                MultiRetState = ExpressionMultiRetState.MayBe,
            };
            switch (t.Type)
            {
            case (LuaTokenType)'(':
                Next(ref t);
                if (!TestAndNext(ref t, (LuaTokenType)')'))
                {
                    ret.Args = ExprList(ref t);
                    CheckAndNext(ref t, (LuaTokenType)')');
                }
                else
                {
                    ret.Args = new ExpressionListSyntaxNode();
                }
                break;
            case (LuaTokenType)'{':
                ret.Args = new ExpressionListSyntaxNode()
                {
                    Expressions =
                    {
                        Constructor(ref t),
                    },
                };
                break;
            case LuaTokenType.String:
                ret.Args = new ExpressionListSyntaxNode()
                {
                    Expressions =
                    {
                        new LiteralExpressionSyntaxNode()
                        {
                            StringValue = t.Content.ToString(),
                        },
                    },
                };
                Next(ref t);
                break;
            default:
                throw new LuaCompilerException("Expecting function arguments.");
            }
            return ret;
        }

        private ExpressionSyntaxNode Constructor(ref Token t)
        {
            var ret = new TableExpressionSyntaxNode();
            Next(ref t); //Skip '{'.
            while (!TestAndNext(ref t, (LuaTokenType)'}'))
            {
                ret.Fields.Add(Field(ref t));
                if (t.Type == (LuaTokenType)',' ||
                    t.Type == (LuaTokenType)';')
                {
                    Next(ref t);
                }
                else
                {
                    CheckAndNext(ref t, (LuaTokenType)'}');
                    break;
                }
            }
            if (ret.Fields.Count > 0)
            {
                ret.Fields[^1].IsLastField = true;
            }
            //'}' has been consumed already.
            return ret;
        }

        private TableConstructorFieldSyntaxNode Field(ref Token t)
        {
            switch (t.Type)
            {
            case LuaTokenType.Name:
            {
                if (_input.TryPeek(1, out var next) && next.Type == (LuaTokenType)'=')
                {
                    var ret = new TableConstructorFieldSyntaxNode()
                    {
                        Key = new LiteralExpressionSyntaxNode()
                        {
                            StringValue = t.Content.ToString(),
                        },
                    };
                    Next(ref t); //name
                    Next(ref t); //'='
                    ret.Value = Expr(ref t);
                    return ret;
                }
                else
                {
                    goto default;
                }
            }
            case (LuaTokenType)'[':
            {
                Next(ref t);
                var k = Expr(ref t);
                CheckAndNext(ref t, (LuaTokenType)']');
                CheckAndNext(ref t, (LuaTokenType)'=');
                var v = Expr(ref t);
                return new TableConstructorFieldSyntaxNode()
                {
                    Key = k,
                    Value = v,
                };
            }
            default:
                return new TableConstructorFieldSyntaxNode()
                {
                    Key = null,
                    Value = Expr(ref t),
                };
            }
        }

        #endregion
    }
}

using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public class SyntaxTreeBuilder
    {
        internal readonly struct LocalVariableUseInfo
        {
            public FunctionDefinitionSyntaxNode Node { get; init; }
            public LocalVariableDefinitionSyntaxNode Definition { get; init; }
            public int IndexInParent { get; init; }
            public int Level { get; init; }
            public FunctionDefinitionSyntaxNode Parent { get; init; }
            public FunctionExpressionSyntaxNode ParentExpr { get; init; }
            public LocalVariableDefinitionSyntaxNode ParentDefinition { get; init; }
        }

        internal readonly struct LocalVariableInfo
        {
            public readonly LocalVariableDefinitionSyntaxNode Node;
            public readonly List<LocalVariableUseInfo> UsedBy;

            public LocalVariableInfo(LocalVariableDefinitionSyntaxNode node)
            {
                Node = node;
                UsedBy = new();
            }

            public LocalVariableInfo(LocalVariableDefinitionSyntaxNode node, LocalVariableInfo importedFrom)
            {
                Node = node;
                UsedBy = importedFrom.UsedBy; //Share the same list (this informs the declaring block).
            }
        }

        internal class FunctionBuilder
        {
            public readonly SyntaxTreeBuilder Owner;
            public readonly int Level;
            public readonly FunctionBuilder ParentFunction;
            public readonly int IndexInParentList; //-1 means main function.
            public readonly FunctionDefinitionSyntaxNode Node;
            public readonly FunctionExpressionSyntaxNode Closure;
            public readonly List<BlockBuilder> BlockStack = new();

            public readonly Dictionary<string, LocalVariableInfo> ImportedUpvals = new();

            public FunctionBuilder(SyntaxTreeBuilder owner)
            {
                Owner = owner;
                Level = 0;
                ParentFunction = null;
                IndexInParentList = -1;
                Node = new FunctionDefinitionSyntaxNode()
                {
                    GlobalId = FunctionDefinitionSyntaxNode.CreateGlobalId(),
                    HasVararg = true, //Main function should be compiled as vararg.
                    VarargType = new SpecializationType
                    {
                        LuaType = SpecializationLuaType.Unspecified,
                    },
                    ParentExternalFunction = null,
                    ReturnNumber = FunctionReturnNumber.MultiRet,
                };
                Closure = null;
                BlockStack.Add(new BlockBuilder(this));
            }

            public FunctionBuilder(FunctionBuilder parent, List<string> paramNames, bool hasSelf, bool hasVararg)
            {
                Owner = parent.Owner;
                Level = parent.Level + 1;
                Debug.Assert(Level == Owner._functionStack.Count);
                ParentFunction = parent;
                IndexInParentList = parent.Node.ChildrenFunctions.Count;
                Node = new FunctionDefinitionSyntaxNode()
                {
                    GlobalId = FunctionDefinitionSyntaxNode.CreateGlobalId(),
                    HasVararg = hasVararg,
                    VarargType = !hasVararg ? default : new SpecializationType
                    {
                        LuaType = SpecializationLuaType.Unspecified,
                    },
                    ParentExternalFunction = new ExternalFunctionReferenceSyntaxNode()
                    {
                        GlobalFunctionId = parent.Node.GlobalId,
                        Index = -1, //-1: reference to parent.
                    },
                    ReturnNumber = FunctionReturnNumber.MultiRet,
                };
                parent.Node.ChildrenFunctions.Add(new ExternalFunctionReferenceSyntaxNode()
                {
                    GlobalFunctionId = Node.GlobalId,
                    Index = IndexInParentList,
                });
                Closure = new FunctionExpressionSyntaxNode()
                {
                    Prototype = new(parent.GetChildReference(this)),
                    UpValueLists = { }, //Will be set up later.
                };
                BlockStack.Add(new BlockBuilder(this));
                if (hasSelf)
                {
                    AddParameter("self");
                }
                foreach (var p in paramNames)
                {
                    AddParameter(p);
                }
            }

            private void AddParameter(string name)
            {
                var localDef = new LocalVariableDefinitionSyntaxNode()
                {
                    Kind = LocalVariableKind.Parameter,
                    Declaration = null,
                };
                Node.Parameters.Add(localDef);
                BlockStack[0].AddArgument(name, localDef);
            }

            public void Close()
            {
                Debug.Assert(BlockStack.Count == 1);
                BlockStack[0].Close(Node); //This will set CurrentBlock to null.
                if (IndexInParentList != -1)
                {
                    Owner.CurrentBlock = Owner._functionStack[^1].BlockStack[^1];
                }
            }

            public ExternalFunctionReferenceSyntaxNode GetChildReference(FunctionBuilder f)
            {
                return Node.ChildrenFunctions[f.IndexInParentList];
            }

            public ExternalFunctionReferenceSyntaxNode GetCurrentChildReference()
            {
                return Node.ChildrenFunctions[^1];
            }

            public LocalVariableInfo GetVariable(string name, FunctionBuilder requestingFunction)
            {
                if (ImportedUpvals.TryGetValue(name, out var info))
                {
                    if (requestingFunction != this)
                    {
                        if (name == "_ENV")
                        {
                            //Update ReferenceBy list.
                            //First create one if not existing (_ENV not exported yet).
                            if (info.Node.ExportUpValueList is null)
                            {
                                Debug.Assert(Node.ImportedUpValueLists.Count == 1);
                                var exportList = new UpValueListSyntaxNode()
                                {
                                    Variables = { new(info.Node) },
                                    ReferencedBy = { }, //Added below.
                                };
                                Node.ImportedUpValueLists[0].UpValueList = exportList;
                                info.Node.ExportUpValueList = new(exportList);
                            }
                            info.Node.ExportUpValueList.Target.ReferencedBy.Add(new(GetCurrentChildReference()));
                        }
                        //Otherwise caller should update the UsedBy list in the returned LocalVariableInfo,
                        //so that the declaring block when closing will properly set ReferenceBy list.
                    }
                    return info;
                }
                if (name == "_ENV")
                {
                    //Special treatment for _ENV (a variable that never closes).

                    //Copy definition to be referenced locally.
                    var definition = new LocalVariableDefinitionSyntaxNode()
                    {
                        Kind = LocalVariableKind.UpValue,
                    };

                    //Export?
                    UpValueListSyntaxNode exportedList = null;
                    if (requestingFunction != this)
                    {
                        exportedList = new UpValueListSyntaxNode()
                        {
                            ParentBlock = null,
                            Variables = { new(definition) },
                            ReferencedBy = { new(GetCurrentChildReference()) },
                        };
                        definition.ExportUpValueList = new(exportedList);
                    }

                    //Import list. (For root function, this is enough. Loader will automatically bind a _ENV table.)
                    Debug.Assert(Node.ImportedUpValueLists.Count == 0); //_ENV is always the first list.
                    var list = new ImportedUpValueListSyntaxNode()
                    {
                        ImportId = 0,
                        UpValueList = exportedList,
                        Variables = { definition },
                    };
                    Node.ImportedUpValueLists.Add(list);
                    definition.ImportUpValueList = new(list);

                    //Only for child functions: update the Closure expression (from parent function).
                    if (Closure is not null)
                    {
                        var parentList = ParentFunction.GetVariable(name, requestingFunction);

                        Debug.Assert(parentList.Node?.ExportUpValueList?.Target is not null);
                        Debug.Assert(Closure.UpValueLists.Count == 0);
                        Closure.UpValueLists.Add(new(parentList.Node.ExportUpValueList.Target));
                    }

                    return new LocalVariableInfo(definition);
                }
                if (ParentFunction is not null)
                {
                    var parentInfo = ParentFunction.BlockStack[^1].GetVariableInternal(name, requestingFunction);
                    if (parentInfo.Node is not null)
                    {
                        var thisDefinition = new LocalVariableDefinitionSyntaxNode()
                        {
                            Kind = LocalVariableKind.UpValue,
                        };
                        //Add to the (shared) list to allow the declaring block to close this later.
                        parentInfo.UsedBy.Add(new()
                        {
                            Node = Node,
                            Level = Level,
                            IndexInParent = IndexInParentList,
                            Definition = thisDefinition,
                            Parent = ParentFunction.Node,
                            ParentExpr = Closure,
                            ParentDefinition = parentInfo.Node,
                        });
                        //Replace with a new definition (as upval).
                        var thisInfo = new LocalVariableInfo(thisDefinition, parentInfo);
                        ImportedUpvals.Add(name, thisInfo);
                        return thisInfo;
                    }
                }
                //Root function, or not found from parent.
                return default;
            }
        }

        public class BlockBuilder
        {
            private BlockBuilder _parent;
            private BlockSyntaxNode _block;
            private FunctionBuilder _parentFunction;
            private bool _isMainBlock;
            private int _parentLocalNumber;
            private LabelStatementSyntaxNode _breakLabel;

            private readonly List<(string name, GotoStatementSyntaxNode node, int localPos)> _unresolvedLabels = new();
            private readonly Dictionary<string, LabelStatementSyntaxNode> _definedLabels = new();

            //TODO currently the LocalVariableInfo (including a list) is created for all locals
            //should avoid creating unless the local is used as upval
            private readonly Dictionary<string, LocalVariableInfo> _declaredLocals = new();

            internal BlockBuilder(FunctionBuilder function)
            {
                _parent = null;
                _block = function.Node;
                _parentFunction = function;
                _isMainBlock = true;
                _breakLabel = null;
                _parentLocalNumber = 0;
            }

            internal BlockBuilder(BlockBuilder parent, BlockSyntaxNode block)
            {
                _parent = parent;
                _block = block;
                _parentFunction = parent._parentFunction;
                _isMainBlock = false;
                Debug.Assert(_isMainBlock == (block is FunctionDefinitionSyntaxNode));
                if (block is LoopBlockSyntaxNode loop)
                {
                    _breakLabel = new();
                    loop.BreakLabel = new(_breakLabel);
                }
                else
                {
                    _breakLabel = parent?._breakLabel;
                }
                _parentLocalNumber = parent._declaredLocals.Count;
            }

            public void Add(StatementSyntaxNode s)
            {
                s.ParentBlock ??= new(_block);
                _block.Statements.Add(s);
            }

            private void CheckClosing()
            {
                if (_block is LoopBlockSyntaxNode)
                {
                    _parent.Add(_breakLabel);
                }
                CheckGotos();
                CheckLocals();
            }

            private void CheckGotos()
            {
                if (_parent is null)
                {
                    foreach (var (_, g, _) in _unresolvedLabels)
                    {
                        if (g.Target is null)
                        {
                            throw new LuaCompilerException("Undefined label.");
                        }
                    }
                }
                else
                {
                    //Move to parent.
                    foreach (var (n, g, _) in _unresolvedLabels)
                    {
                        if (g.Target is not null) continue;
                        _parent._unresolvedLabels.Add((n, g, _parentLocalNumber));
                    }
                }
            }

            private void CheckLocals()
            {
                //TODO currently using a simple implementation that each upval has its own list
                //this is same as the original implementation of Lua, but we should be able to merge
                //some of those which are referenced by same child functions into bigger lists.
                foreach (var local in _declaredLocals.Values)
                {
                    if (local.UsedBy.Count == 0) continue;
                    local.UsedBy.Sort((r1, r2) => r1.Level - r2.Level);

                    var blockExportList = new UpValueListSyntaxNode()
                    {
                        ParentBlock = new(_block),
                        Variables = { new(local.Node) },
                    };
                    local.Node.ExportUpValueList = new(blockExportList);
                    _block.UpValueLists.Add(blockExportList);

                    foreach (var useInfo in local.UsedBy)
                    {
                        //Create import list on node.
                        var importList = new ImportedUpValueListSyntaxNode()
                        {
                            ImportId = useInfo.Node.ImportedUpValueLists.Count,
                            UpValueList = null, //Not exported yet (set up below).
                            Variables = { useInfo.Definition },
                        };
                        useInfo.Node.ImportedUpValueLists.Add(importList);
                        useInfo.Definition.ImportUpValueList = new(importList);

                        UpValueListSyntaxNode parentExportList;

                        //Update parent's ReferenceBy list.
                        var referenceByInfo = useInfo.Parent.ChildrenFunctions[useInfo.IndexInParent];
                        if (useInfo.Parent != _parentFunction.Node)
                        {
                            //Node's parent is not this function. It's import list needs to be exported.
                            //Note that we have sorted uses by depth, so this parent function's import list
                            //is ensured to exist now.

                            var parentDefinition = useInfo.ParentDefinition;
                            var parentImportList = parentDefinition.ImportUpValueList.Target;
                            if (parentImportList.UpValueList is null)
                            {
                                //Not exported yet.
                                parentImportList.UpValueList = new UpValueListSyntaxNode()
                                {
                                    Variables = { new(useInfo.ParentDefinition) },
                                    ReferencedBy = { }, //Added below.
                                };
                                useInfo.ParentDefinition.ExportUpValueList = new(parentImportList.UpValueList);
                            }
                            parentExportList = parentImportList.UpValueList;
                        }
                        else
                        {
                            parentExportList = blockExportList;
                        }
                        parentExportList.ReferencedBy.Add(new(referenceByInfo));

                        //Add closure parameter info.
                        useInfo.ParentExpr.UpValueLists.Add(new(parentExportList));
                        Debug.Assert(useInfo.ParentExpr.UpValueLists.Count == useInfo.Node.ImportedUpValueLists.Count);
                    }
                }
            }

            public void Close(BlockSyntaxNode closing)
            {
                Debug.Assert(closing == _block);
                Debug.Assert(_parentFunction.BlockStack[^1] == this);
                CheckClosing();
                _parentFunction.BlockStack.RemoveAt(_parentFunction.BlockStack.Count - 1);
                _parentFunction.Owner.CurrentBlock = _parent;
            }

            public void GotoBreakLabel()
            {
                if (_breakLabel is null)
                {
                    throw new LuaCompilerException("Cannot break");
                }
                Add(new GotoStatementSyntaxNode()
                {
                    Target = new(_breakLabel),
                });
            }

            public void MarkLabel(ReadOnlySpan<char> name)
            {
                var label = new LabelStatementSyntaxNode();
                Add(label);
                var labelName = name.ToString();
                if (!_definedLabels.TryAdd(labelName, label))
                {
                    throw new LuaCompilerException("Label redefined.");
                }
                var labelPos = _declaredLocals.Count;
                foreach (var (unresolvedName, node, pos) in _unresolvedLabels)
                {
                    if (unresolvedName == labelName)
                    {
                        Debug.Assert(node.Target is null);
                        if (pos != labelPos)
                        {
                            throw new LuaCompilerException("Jump to label cannot skip local variable declaration.");
                        }
                        node.Target = new(label);
                    }
                    //We don't remove resolved labels from the list, as it requires using another
                    //temporary list. Instead, we ignores resolved gotos when closing a block.
                }
            }

            public void GotoNamedLabel(ReadOnlySpan<char> name)
            {
                var labelName = name.ToString();

                //Search defined labels in each open blocks.
                for (var block = this; block is not null; block = block._parent)
                {
                    if (block._definedLabels.TryGetValue(labelName, out var target))
                    {
                        Add(new GotoStatementSyntaxNode()
                        {
                            Target = new(target),
                        });
                        return;
                    }
                }

                //Not found. Add as unresolved goto (without Target).
                var s = new GotoStatementSyntaxNode();
                _unresolvedLabels.Add((labelName, s, _declaredLocals.Count));
                Add(s);
            }

            public LocalVariableDefinitionSyntaxNode NewVariable(ReadOnlySpan<char> name, StatementSyntaxNode decl)
            {
                var def = new LocalVariableDefinitionSyntaxNode()
                {
                    Kind = LocalVariableKind.Local,
                    Declaration = new(decl),
                };
                _declaredLocals.Add(name.ToString(), new(def));
                return def;
            }

            internal void AddArgument(string name, LocalVariableDefinitionSyntaxNode definition)
            {
                Debug.Assert(_isMainBlock);
                _declaredLocals.Add(name.ToString(), new(definition));
            }

            internal LocalVariableInfo GetVariableInternal(string name, FunctionBuilder requestingFunction)
            {
                if (_declaredLocals.TryGetValue(name, out var ret))
                {
                    return ret;
                }
                if (_parent is not null)
                {
                    return _parent.GetVariableInternal(name, requestingFunction);
                }
                //Main block: ask function (to import).
                return _parentFunction.GetVariable(name, requestingFunction);
            }

            public VariableSyntaxNode GetVariable(ReadOnlySpan<char> name)
            {
                var str = name.ToString();
                var ret = GetVariableInternal(str, _parentFunction);
                if (ret.Node is not null)
                {
                    return new NamedVariableSyntaxNode()
                    {
                        Variable = new(ret.Node),
                    };
                }
                ret = GetVariableInternal("_ENV", _parentFunction);
                Debug.Assert(ret.Node is not null);
                return new IndexVariableSyntaxNode()
                {
                    Table = new NamedVariableSyntaxNode()
                    {
                        Variable = new(ret.Node),
                    },
                    Key = new LiteralExpressionSyntaxNode()
                    {
                        StringValue = str,
                    },
                };
            }

            private T OpenBlockInternal<T>(T block) where T : BlockSyntaxNode
            {
                Add(block);
                var builder = new BlockBuilder(this, block);
                _parentFunction.BlockStack.Add(builder);
                _parentFunction.Owner.CurrentBlock = builder;
                return block;
            }

            public IfSyntaxNode AddIfStatement()
            {
                var ret = new IfSyntaxNode();
                Add(ret);
                return ret;
            }

            public SimpleBlockSyntaxNode OpenSimpleBlock()
            {
                return OpenBlockInternal(new SimpleBlockSyntaxNode());
            }

            public ThenElseBlockSyntaxNode OpenThenElseBlock(ExpressionSyntaxNode condition)
            {
                return OpenBlockInternal(new ThenElseBlockSyntaxNode()
                {
                    Condition = condition,
                });
            }

            public WhileBlockSyntaxNode OpenWhileBlock(ExpressionSyntaxNode condition)
            {
                return OpenBlockInternal(new WhileBlockSyntaxNode()
                {
                    Condition = condition,
                });
            }

            public RepeatBlockSyntaxNode OpenRepeatBlock()
            {
                return OpenBlockInternal(new RepeatBlockSyntaxNode());
            }

            public AuxiliaryBlockSyntaxNode OpenAuxBlock()
            {
                return OpenBlockInternal(new AuxiliaryBlockSyntaxNode());
            }

            public NumericForBlockSyntaxNode OpenNumericForBlock()
            {
                return OpenBlockInternal(new NumericForBlockSyntaxNode());
            }

            public GenericForBlockSyntaxNode OpenGenericForBlock()
            {
                return OpenBlockInternal(new GenericForBlockSyntaxNode());
            }
        }

        private readonly List<FunctionBuilder> _functionStack = new();
        private readonly List<FunctionDefinitionSyntaxNode> _closedFunctions = new();
        public BlockBuilder CurrentBlock { get; private set; }

        public void Start()
        {
            _functionStack.Clear();
            var mainFunc = new FunctionBuilder(this);
            _functionStack.Add(mainFunc);
            CurrentBlock = mainFunc.BlockStack[0];
        }

        public SyntaxRoot Finish()
        {
            if (_functionStack.Count != 1)
            {
                throw new InvalidOperationException();
            }
            var mainFunc = _functionStack[0];
            mainFunc.Close();
            var ret = new SyntaxRoot()
            {
                RootFunction = mainFunc.Node,
            };
            ret.Functions.AddRange(_closedFunctions);
            return ret;
        }

        public FunctionDefinitionSyntaxNode OpenFunction(List<string> parameterNames, bool hasSelf, bool hasVararg)
        {
            var f = new FunctionBuilder(_functionStack[^1], parameterNames, hasSelf, hasVararg);
            _functionStack.Add(f);
            CurrentBlock = _functionStack[^1].BlockStack[0];
            return f.Node;
        }

        public FunctionExpressionSyntaxNode CloseFunction(FunctionDefinitionSyntaxNode func)
        {
            Debug.Assert(_functionStack.Count > 1);
            var f = _functionStack[^1];
            _functionStack.RemoveAt(_functionStack.Count - 1);
            Debug.Assert(f.Node == func);
            f.Close();
            _closedFunctions.Add(func);
            return f.Closure;
        }
    }
}

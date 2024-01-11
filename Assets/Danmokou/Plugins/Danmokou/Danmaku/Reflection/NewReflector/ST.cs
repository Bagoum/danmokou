using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.DMath.Functions;
using Danmokou.Reflection;
using MathNet.Numerics;
using Mizuhashi;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection2 {

public interface IDebugPrint {
    /// <summary>
    /// Print a readable description of the entire AST.
    /// </summary>
    public IEnumerable<PrintToken> DebugPrint();
    string DebugPrintStringify() => new ExpressionPrinter().Stringify(DebugPrint().ToArray());
    
    public static IEnumerable<PrintToken> PrintArgs(IReadOnlyList<IDebugPrint> args, string sep = ",") {
        if (args.Count > 1) {
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            for (int ii = 0; ii < args.Count; ++ii) {
                foreach (var x in args[ii].DebugPrint())
                    yield return x;
                if (ii < args.Count - 1) {
                    yield return sep;
                    yield return PrintToken.newline;
                }
            }
            yield return PrintToken.dedent;
            //yield return PrintToken.newline;
        } else if (args.Count == 1) {
            foreach (var x in args[0].DebugPrint())
                yield return x;
        }
    }
}

/// <summary>
/// A syntax tree formed by parsing.
/// <br/>The syntax tree has no knowledge of bindings or types that are not explicitly declared,
/// but it can be transformed into an <see cref="IAST"/> that does.
/// </summary>
public abstract record ST(PositionRange Position) : IDebugPrint {
    public ReflectDiagnostic[] Diagnostics { get; init; } = System.Array.Empty<ReflectDiagnostic>();
    /// <summary>
    /// Annotate this syntax tree with types and bindings.
    /// The resulting AST may contain <see cref="AST.Failure"/>.
    /// </summary>
    protected abstract IAST _AnnotateInner(LexicalScope scope);

    public IAST Annotate(LexicalScope scope) {
        var ast = _AnnotateInner(scope);
        ast.SetDiagnostics(Diagnostics);
        return ast;
    }
    
    /// <summary>
    /// Print a readable description of the entire syntax tree.
    /// </summary>
    public abstract IEnumerable<PrintToken> DebugPrint();

    private static Reflector.InvokedMethod[]? GetOverloads(ST func, LexicalScope scope) => func switch {
        FnIdent fn => fn.Func,
        Ident id => scope.FindStaticMethodDeclaration(id.Name.ToLower()) is { } decls ?
            decls.Select(d => d.Call(id.Name)).ToArray() :
            null,
        _ => null
    };
    
    /// <summary>
    /// An identifier that may be for a function or variable or type, etc.
    /// </summary>
    /// <param name="Position">Position of the identifier in the source.</param>
    /// <param name="Name">Name of the identifier.</param>
    /// <param name="Generic">Whether this identifier is generic, ie. contains &lt;&gt; or array markings []. A generic identifier cannot be a variable.</param>
    public record Ident(PositionRange Position, string Name, bool Generic) : ST(Position) {
        public Ident(Lexer.Token token) : this(token.Position, token.Content, token.Type == Lexer.TokenType.TypeIdentifier) { }
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (scope.FindDeclaration(Name) is { } decl)
                return new AST.Reference(Position, scope, Name, decl);
            if (scope.FindStaticMethodDeclaration(Name) is { } meths)
                return new AST.MethodCall(Position, Position, scope, meths.Select(m => m.Call(Name)).ToArray());
            return new AST.Reference(Position, scope, Name, scope.RequestDeclaration(Position, Name));
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "&" + Name;
        }
    }

    /// <summary>
    /// An identifier that is known to be for a static method. This is generally only constructed by operators.
    /// </summary>
    public record FnIdent(PositionRange Position, params Reflector.InvokedMethod[] Func) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            throw new NotImplementedException();
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Func[0].Name;
        }
    }

    /// <summary>
    /// A joint declaration-assignment, such as `var x = 5`.
    /// </summary>
    /// <param name="Declaration">The variable declaration.</param>
    /// <param name="AssignValue">The value to which the variable is assigned, eg. `5`.</param>
    public record VarDeclAssign(VarDecl Declaration, ST AssignValue) : ST(Declaration.Position.Merge(AssignValue.Position)) {
        private readonly FunctionCall Assignment =
            new(Declaration.Position.Merge(AssignValue.Position),
                new FnIdent(new(Declaration.Position.End, AssignValue.Position.Start),
                    Parser.Lift(typeof(ExMAssign), nameof(ExMAssign.Is)).Call(null)),
                new Ident(Declaration.Position, Declaration.Name, false),
                AssignValue);
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (scope.DeclareVariable(Declaration) is { IsRight:true} r)
                return new AST.Failure(new(Position, 
                    $"The declaration ({Declaration.KnownType?.RName() ?? "(type undetermined)"} {Declaration.Name}) " +
                    $"conflicts with the declaration ({r.Right.KnownType?.RName() ?? "(type undetermined)"} " +
                    $"{r.Right.Name}) at {r.Right.Position}"), scope);
            return Assignment.Annotate(scope);
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Declaration.DebugPrint())
                yield return w;
            yield return " = ";
            foreach (var w in AssignValue.DebugPrint())
                yield return w;
        }
    }

    /// <summary>
    /// A member access `x.y`.
    /// </summary>
    public record MemberAccess(ST Object, Ident Member) : ST(Object.Position.Merge(Member.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            throw new NotImplementedException();
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return ".";
            foreach (var w in Member.DebugPrint())
                yield return w;
        }
    }

    /// <summary>
    /// A C#-style function call with parentheses and commas, such as `f(x, y)`,
    ///  or an operator-based function call such as `x + y`.
    /// <br/>Note that there must be no spaces before the parentheses.
    /// </summary>
    public record FunctionCall(PositionRange Position, ST Fn, params ST[] Args) : ST(Position) {
        public bool OverloadsInterchangeable { get; init; } = false;
        protected override IAST _AnnotateInner(LexicalScope scope) {
            //If we're directly calling a *static method*, then we already know the signatures
            if (Fn is FnIdent fn) {
                return new AST.MethodCall(Position, fn.Position, scope, fn.Func, 
                    Args.Select(a => a.Annotate(scope)).ToArray()) 
                    { OverloadsAreInterchangeable = OverloadsInterchangeable};
            } else if (Fn is Ident id) {
                if (scope.FindStaticMethodDeclaration(id.Name.ToLower()) is { } decls) {
                    var argFilter = decls.Where(d => d.Params.Length == Args.Length).ToList();
                    if (argFilter.Count > 0)
                        return new AST.MethodCall(Position, id.Position, scope,
                            decls.Select(d => d.Call(id.Name)).ToArray(),
                            Args.Select(a => a.Annotate(scope)).ToArray())
                            { OverloadsAreInterchangeable = OverloadsInterchangeable};
                    else
                        return new AST.Failure(new(Position, id.Position,
                            $"There is no method by name `{id.Name}` that takes {Args.Length} arguments." +
                            $"\nThe signatures of the methods named `{id.Name}` are as follows:" +
                            $"\n\t{string.Join("\n\t", decls.Select(o => o.AsSignature))}"), scope);
                } else
                    return new AST.Failure(new(Position, $"Couldn't find any method by the name `{id.Name}`."), scope);
            }
            //instance function calls, lambda calls, etc
            throw new NotImplementedException();
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Fn.DebugPrint())
                yield return w;
            yield return "(";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return ")";
        }
    }

    /// <summary>
    /// A Haskell-style partial function call, such as `f x`.
    /// <br/>In cases where multiple arguments are applied, this is constructed left-associatively as
    ///  `f x y z` = P(P(P(f, x), y), z).
    /// </summary>
    public record PartialFunctionCall(ST Fn, ST Arg) : ST(Fn.Position.Merge(Arg.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            //Get the leftmost function and a list of args
            var argsl = new List<ST>() { Arg };
            var leftmost = Fn;
            while (leftmost is PartialFunctionCall pfc) {
                argsl.Add(pfc.Arg);
                leftmost = pfc.Fn;
            }
            argsl.Reverse();
            var args = argsl.ToArray();

            (string name, IEnumerable<(int reqArgs, IEnumerable<Reflector.InvokedMethod> methods)>)? ArgCounts(ST func) {
                if (func switch {
                        FnIdent fn => fn.Func,
                        Ident id => scope.FindStaticMethodDeclaration(id.Name.ToLower()) is { } decls ?
                            decls.Select(d => d.Call(id.Name)).ToArray() :
                            null,
                        _ => null
                    } is not { } overloads) {
                    return null;
                }
                return (overloads[0].CalledAs ?? overloads[0].Name,
                    overloads.GroupBy(o => o.Params.Length).Select(g => (g.Key, g as IEnumerable<Reflector.InvokedMethod>)));
            }
            
            IEnumerable<(ImmutableList<(int index, int argCt)> pArgs, int endsAt)>
                PossibleArgCounts(int index, ImmutableList<(int index, int argCt)> preceding) {
                if (index >= args.Length)
                    return System.Array.Empty<(ImmutableList<(int, int)>, int)>();
                if (ArgCounts(index >= 0 ? args[index] : leftmost) is not { } counts)
                    //This is not a function, so we consume zero args, but increment the index
                    // since this object itself takes up a space
                    return new[]{ (preceding.Add((index, 0)), index + 1)};
                else 
                    //Note that we may want to prepend `preceding.Add((index, 0)), index + 1)` to this,
                    // which is the case of using a function name as a lambda
                    return counts.Item2
                        .Where(consumed => index + 1 + consumed.reqArgs <= args.Length)
                        .SelectMany(consumed => {
                            IEnumerable<(ImmutableList<(int, int)>, int)> cac = new[]
                                //This function eventually consumes `consumed` args, but the index we start at
                                // is just index+1, since only the function at `index` has been consumed so far
                                { (preceding.Add((index, consumed.reqArgs)), index + 1) };
                            for (int ii = 0; ii < consumed.reqArgs; ++ii) {
                                cac = cac.SelectMany(ca => PossibleArgCounts(ca.Item2, ca.Item1));
                            }
                            return cac;
                        });
            }
            string? ArgCountErrForIndex(int ii) {
                if (ArgCounts(args[ii]) is not { } cts) {
                    return null;
                    //return $"Argument #{ii + 1}: Not a static function, " +
                    //       $"cannot be partially applied (at {args[ii].Position})";
                } else
                    return $"Argument #{ii + 1}: Function `{cts.name}` with possible parameter counts: " +
                           $"{string.Join(", ", cts.Item2.Select(i => i.reqArgs))} (at {args[ii].Position})";
            }
            if (GetOverloads(leftmost, scope) is not { } overloads) {
                return new AST.Failure(new(leftmost.Position,
                    "The function in a partial function application must be a " +
                    "named static function. Please replace this with a parenthesized " +
                    "function call."), scope);
            }
            //Do a quick check to see if the function grouping makes sense in terms of types
            // This is a weak check, so it may return True if the grouping is actually unsound
            bool GroupingIsTypeFeasible(ImmutableList<(int index, int argCt)> grouping) {
                (TypeTree.ITree tree, int nextIndex) MakeForIndex(int index) {
                    //Don't bother type-enforcement on non-function idents
                    if (ArgCounts(index >= 0 ? args[index] : leftmost) is not { } cts)
                        return (new TypeTree.AtomicWithType(new TypeDesignation.Variable()), index + 1);
                    var ct = grouping[index + 1].argCt;
                    var overloads = cts.Item2
                        .First(x => x.reqArgs == ct).methods
                        .Select(x => x.Method);
                    var nextIndex = index + 1;
                    var prms = new TypeTree.ITree[ct];
                    for (int ii = 0; ii < ct; ++ii) {
                        (prms[ii], nextIndex) = MakeForIndex(nextIndex);
                    }
                    return (new TypeTree.Method(overloads.ToArray(), prms), nextIndex);
                }
                var (tree, last) = MakeForIndex(-1);
                if (last != args.Length)
                    return false;
                return tree.PossibleUnifiers(scope.Root.Resolver, Unifier.Empty).IsLeft;
            }
            //If there are overloads that match the numerical requirements, use them
            var sameArgsReq = overloads.Where(f => f.Params.Length == args.Length).ToArray();
            if (sameArgsReq.Length > 0)
                return new AST.MethodCall(Position, leftmost.Position, scope,
                    sameArgsReq, args.ToArray().Select(a => a.Annotate(scope)).ToArray());
            //If there are overloads that require fewer args than provided, use them and do smart grouping
            var lessArgsReq = overloads.Where(f => f.Params.Length < args.Length).ToArray();
            if (lessArgsReq.Length > 0) {
                var possibleArgGroupings = PossibleArgCounts(-1, ImmutableList<(int, int)>.Empty)
                    .Where(cac => cac.endsAt == args.Length)
                    .ToList();
                bool typeFiltered = false;
                if (possibleArgGroupings.Count > 1) {
                    //Only filter by type if we are required to
                    possibleArgGroupings = possibleArgGroupings
                        .Where(p => GroupingIsTypeFeasible(p.pArgs)).ToList();
                    typeFiltered = true;
                }
                if (possibleArgGroupings.Count != 1) {
                    var plural = possibleArgGroupings.Count == 0 ? "no combination" : "multiple combinations";
                    var argsErr = string.Join("\n\t", args.Length.Range().SelectNotNull(ArgCountErrForIndex));
                    if (typeFiltered && possibleArgGroupings.Count == 0) {
                        return new AST.Failure(new(leftmost.Position,
                            $"When resolving the partial method invocation for `{overloads[0].CalledAs ?? overloads[0].Name}`," +
                            $" {argsl.Count} parameters were provided, but overloads were only found with " +
                            $"{string.Join(", ", overloads.Select(o => o.Params.Length).Distinct().OrderBy(x => x))} parameters." +
                            $"\nAttempted to automatically group functions, but multiple combinations of the following functions" +
                            $"combined to {argsl.Count} parameters, and none of them appear to pass type-checking." +
                            $" Please try using parentheses.\n\t{argsErr}"), scope);
                    }
                    return new AST.Failure(new(leftmost.Position,
                        $"When resolving the partial method invocation for `{overloads[0].CalledAs ?? overloads[0].Name}`," +
                        $" {argsl.Count} parameters were provided, but overloads were only found with " +
                        $"{string.Join(", ", overloads.Select(o => o.Params.Length).Distinct().OrderBy(x => x))} parameters." +
                        $"\nAttempted to automatically group functions, but {plural} of the following functions " +
                        $"combined to {argsl.Count} parameters. Please try using parentheses.\n\t{argsErr}"), scope);
                }
                var argsCts = possibleArgGroupings[0].pArgs.ToDictionary(kv => kv.index, kv => kv.argCt);
                var argStack = new Stack<IAST>();
                var mArgsTmp = new List<IAST>();
                for (int ii = args.Length - 1; ii >= -1; --ii) {
                    var st = ii >= 0 ? args[ii] : leftmost;
                    if (!argsCts.TryGetValue(ii, out var mArgCt) || mArgCt == 0)
                        argStack.Push(st.Annotate(scope));
                    else {
                        for (int ima = 0; ima < mArgCt; ++ima)
                            mArgsTmp.Add(argStack.Pop());
                        var mOverloads = (GetOverloads(st, scope) ??
                                          throw new StaticException("Incorrect partial application parameter grouping"))
                            .Where(o => o.Params.Length == mArgCt)
                            .ToArray();
                        if (mOverloads.Length == 0)
                            throw new StaticException("Incorrect partial application overload lookup");
                        argStack.Push(new AST.MethodCall(st.Position.Merge(mArgsTmp[^1].Position), 
                            st.Position, scope, mOverloads, mArgsTmp.ToArray()));
                        mArgsTmp.Clear();
                    }
                }
                return argStack.Pop();
            } else
                throw new NotImplementedException();
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Fn.DebugPrint())
                yield return w;
            yield return "(|";
            foreach (var w in Arg.DebugPrint())
                yield return w;
            yield return "|)";
        }
    }

    /// <summary>
    /// An increment or decrement operation (++/--) on a variable.
    /// </summary>
    /// <param name="Position">Position of AST.</param>
    /// <param name="Num">Variable that is being modified.</param>
    /// <param name="isPost">Whether this is a postfix or prefix increment/decrement.</param>
    /// <param name="isAdd">Whether this is an increment or a decrement.</param>
    /*public record BoundIncrement(PositionRange Position, ST Num, bool isPost, bool isAdd) : ST(Position) {
        public override IEnumerable<PrintToken> DebugPrint() {
            if (!isPost)
                yield return isAdd ? "++" : "--";
            foreach (var w in Num.DebugPrint())
                yield return w;
            if (isPost)
                yield return isAdd ? "++" : "--";
        }
    }*/

    /// <summary>
    /// A block of statements.
    /// </summary>
    public record Block(PositionRange Position, ST[] Args) : ST(Position) {
        public Block(IReadOnlyList<ST> args) : this(args[0].Position.Merge(args[^1].Position), args.ToArray()) { }
        
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var localScope = LexicalScope.Derive(scope);
            return new AST.Block(Position, scope, localScope, Args.Select(a => a.Annotate(localScope)).ToArray());
        }

        public AST.Block AnnotateTopLevel(LexicalScope gs, IDelegateArg[] topLevelArgs) {
            if (gs.Parent is not DMKScope)
                throw new Exception("Top-level block scope parent should be DMKScope");
            //top-level args are declared as variables and copied into the top-level envframe
            var decls = new (VarDecl, ImplicitArgDecl)[topLevelArgs.Length];
            for (int ii = 0; ii < topLevelArgs.Length; ++ii) {
                var arg = topLevelArgs[ii];
                gs.DeclareVariable((decls[ii] = 
                    (new VarDecl(default, arg.Type, arg.Name), arg.MakeImplicitArgDecl())).Item1);
            }
            return new AST.Block(Position, gs.Parent, gs, Args.Select(a => a.Annotate(gs)).ToArray())
                .WithTopLevelArgs(decls);
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"block({{";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return "})";
        }
    }

    /// <summary>
    /// An array. Uniform typing is not guaranteed.
    /// </summary>
    public record Array(PositionRange Position, ST[] Args) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            return new AST.Array(Position, scope, Args.Select(a => a.Annotate(scope)).ToArray());
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"[{Args.Length}] {{";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return "}";
        }
    }
    

    /// <summary>
    /// A number such as `5.0`. This is not included in TypedValue since numbers can have their type
    ///  auto-determined (eg. float vs int) based on usage.
    /// </summary>
    public record Number(PositionRange Position, float Value) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) => new AST.Number(Position, scope, Value);

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Value.ToString();
        }
    }

    /// <summary>
    /// A fixed value of a type not subject to type auto-determination, such as strings, but not numbers.
    /// </summary>
    public record TypedValue<T>(PositionRange Position, T Value) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) => new AST.TypedValue<T>(Position, scope, Value);
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Value?.ToString() ?? "<null>";
        }
    }

}
}
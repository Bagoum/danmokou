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
using Danmokou.SM;
using LanguageServer.VsCode.Contracts;
using MathNet.Numerics;
using Mizuhashi;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using Position = Mizuhashi.Position;
using SemanticTokenTypes = Danmokou.Reflection.SemanticTokenTypes;

namespace Danmokou.Reflection2 {

public record STAnnotater(LexicalScope Scope, LexerMetadata LexerMetadata);

/// <summary>
/// A syntax tree formed by parsing.
/// <br/>The syntax tree has no knowledge of bindings or types that are not explicitly declared,
/// but it can be transformed into an <see cref="IAST"/> that does.
/// </summary>
public abstract record ST(PositionRange Position) : IDebugPrint {
    public ReflectDiagnostic[] Diagnostics { get; init; } = System.Array.Empty<ReflectDiagnostic>();

    private static SemanticToken? Token(PositionRange? pos, string type) => 
        pos.Try(out var p) ? new(p, type) : null;

    private static SemanticToken? Keyword(PositionRange? pos) => Token(pos, SemanticTokenTypes.Keyword);
    private static SemanticToken? Op(PositionRange? pos) => Token(pos, SemanticTokenTypes.Operator);
    private static SemanticToken? Type(PositionRange? pos) => Token(pos, SemanticTokenTypes.Type);
    
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
        public Lexer.Token? KnownType { get; }
        public Ident(Lexer.Token token, Lexer.Token? type = null) : this(token.Position, token.Content,
            token.Type == Lexer.TokenType.TypeIdentifier) {
            this.KnownType = type;
        }

        /// <summary>
        /// Try to reference the variable with name `id`, defined in the scope of `inImport` (if provided)
        ///  else in `scope`.
        /// <br/>If there is no such script function, return null.
        /// </summary>
        public static AST.Reference? TryFindVariable(PositionRange pos, Ident id, ScriptImport? inImport, LexicalScope scope) {
            if ((inImport?.Ef.Scope ?? scope).FindVariable(id.Name) is { } decl)
                return new AST.Reference(pos, scope, inImport, id.Name, decl) { NameOnlyPosition = id.Position };
            return null;
        }

        protected override IAST _AnnotateInner(LexicalScope scope) {
            var kt = Parser.TypeFromToken(KnownType, scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            if (Name.StartsWith("&")) {
                //For scriptFunction we can scan upwards for the single declaration; for dynamic reference, we can't,
                // since there are possibly multiple declarations in different places.
                //That said, we make an exception for const vars for convenience.
                var key = Name.Substring(1);
                if (typ is null && scope.FindVariable(key, DeclarationLookup.ConstOnly) is { } decl) {
                    return new AST.Reference(Position, scope, null, key, decl);
                }
                if (scope.IsConstScope)
                    return new AST.Failure(new(Position, "Cannot use dynamic lookup within a constant scope."), scope);
                var ast = new AST.WeakReference(Position, scope, key, typ);
                ast.AddTokens(new[]{Type(KnownType?.Position)});
                return ast;
            }
            if (typ != null)
                return new AST.Failure(new(Position,
                    "Variable type annotation is only allowed when declaring a variable " +
                    "(eg. `var x::float = 5`) or using a dynamically-scoped variable " +
                    "(eg. `&x::float + 1`)."), scope);
            if (TryFindVariable(Position, this, null, scope) is { } tree)
                return tree;
            if (scope.FindStaticMethodDeclaration(Name)?.Where(m => m.Params.Length == 0).ToArray() is { Length: >0 } meths)
                return AST.MethodCall.Make(Position, Position, scope, meths.Select(m => m.Call(Name)).ToArray(), System.Array.Empty<ST>());
            if (Reflector.bdsl2EnumResolvers.TryGetValue(Name, out var vals))
                return new AST.Reference(Position, scope, null, Name, vals);

            if (scope.ImportDecls.TryGetValue(Name, out var imp)) {
                return new AST.Failure(new(Position, $"`{Name}` is an imported file. You must reference a variable or function defined within the file; eg. `{Name}.myFloat` or `{Name}.myFunction(1, 2)`."),
                    scope) {
                    ImportedScript = imp
                };
            }
            
            var unreachable = scope.ScriptRoot.AllVarsInDescendantScopes
                .Concat(scope.GlobalRoot.AllVisibleVars)
                .Where(x => x.Name == Name).ToList();
            var err = $"Could not determine what \"{Name}\" refers to.";
            if (unreachable.Count > 0) {
                err +=
                    $"\nThere are some declarations for \"{Name}\", but they are not visible from this lexical scope:" +
                    $"\n\t{string.Join("\n\t", unreachable.Select(x => $"{x.Name} at {x.Position}"))}";
                if (scope.IsConstScope && unreachable.Any(u => !u.Constant)) {
                    err += "\nThis is a constant scope. You can only reference const variables.";
                } else {
                    err += $"\nMaybe you need to hoist the declaration for \"{Name}\" (use `hvar` instead of `var`).";
                    if (scope is DynamicLexicalScope)
                        err +=
                            $"\nAlso, this is a dynamic scope. You may need to use dynamic scoping on the variable (ie. `&{Name}` instead of `{Name}`).";
                }
            }
            return new AST.Failure(new(Position, err), scope);
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

    public record Import(PositionRange KwPos, Lexer.Token File, (PositionRange At, Lexer.Token Filename)? Location, (PositionRange As, Lexer.Token Desc)? Name) :
        ST(KwPos.Merge(Name?.Desc.Position ?? File.Position)) {
        //Overriden by language server
        public static Func<Import, Either<EnvFrame, ReflectionException>> Importer { get; set; } =
            imp => StateMachineManager.LoadImport(imp.File.Content);
        
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var ef = Importer(this);
            if (ef.IsRight)
                return new AST.Failure(ef.Right, scope);
            var decl = new ScriptImport(Position, ef.Left, File.Content, Location?.Filename.Content,
                Name?.Desc.Content);
            AST ast;
            if (scope.Declare(decl) is {IsRight:true} r)
                ast = new AST.Failure(new(Position, $"The variable `{decl.Name}` has already been declared at {r.Right.Position}."), scope);
            else {
                ast = new AST.DefaultValue(Position, scope, typeof(void)) {
                    TokenType = null,
                    Description = $"Import {File.Content}" + (Name is { } n ? $" as {n.Desc.Content}" : null)
                };
            }
            ast.AddTokens(new[] { Keyword(KwPos), Keyword(Location?.At), 
                Token(Location?.Filename.Position, SemanticTokenTypes.String), Keyword(Name?.As) });
            return (IAST)ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"import {File.Content}" + (Name is {} n ? $" as {n.Desc.Content}" : null);
        }
    }

    /// <summary>
    /// A return statement.
    /// </summary>
    public record Return(PositionRange KwPos, ST? Value) : ST(Value == null ? KwPos : KwPos.Merge(Value.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (scope.NearestReturn is null)
                return new AST.Failure(
                    new(Position, "This return statement is not contained within a function definition."), scope);
            var ast = new AST.Return(Position, scope, Value?.Annotate(scope));
            ast.AddTokens(new[] { Keyword(KwPos) });
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "return";
            if (Value != null)
                foreach (var w in Value.DebugPrint())
                    yield return w;
        }
    }
    
    /// <summary>
    /// A continue statement.
    /// </summary>
    public record Continue(PositionRange Position) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (scope.NearestContinueBreak is null)
                return new AST.Failure(
                    new(Position, "This continue statement is not contained within a loop."), scope);
            return new AST.Continue(Position, scope);
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "continue";
        }
    }
    /// <summary>
    /// A break statement.
    /// </summary>
    public record Break(PositionRange Position) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (scope.NearestContinueBreak is null)
                return new AST.Failure(
                    new(Position, "This break statement is not contained within a loop."), scope);
            return new AST.Break(Position, scope);
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "continue";
        }
    }

    /// <summary>
    /// A joint declaration-assignment, such as `var x = 5`.
    /// </summary>
    public record VarDeclAssign(Lexer.Token VarKw, Lexer.Token Id, Lexer.Token? Typ, PositionRange EqPos, ST AssignValue) : 
        ST(VarKw.Position.Merge(AssignValue.Position)) {
        public PositionRange? ConstKwPos { get; set; }
        public static readonly MethodSignature VarInitialize =
            Parser.Meth(typeof(ExMAssign), nameof(ExMAssign.VariableInitialize));
        
        /*private readonly FunctionCall Assignment =
            new(Declaration.Position.Merge(AssignValue.Position),
                new FnIdent(EqPos, VarInitialize.Call(null)),
                new Ident(Declaration.Position, Declaration.Name, false),
                AssignValue);*/
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var kt = Parser.TypeFromToken(Typ, scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            var decl = new VarDecl(Id.Position, VarKw.Content == "hvar", typ, Id.Content);
            if (ConstKwPos != null)
                decl.Constant = true;
            
            if (scope.Declare(decl) is { IsRight:true} r)
                return new AST.Failure(new(Position, 
                    $"The variable {decl.Name} has already been declared at {r.Right.Position}."), scope);
            var assignScope = scope;
            if (decl.Constant) {
                assignScope = LexicalScope.Derive(assignScope);
                assignScope.IsConstScope = true;
                assignScope.Type = LexicalScopeType.ExpressionBlock;
            }
            var ret = new AST.MethodCall(decl.Position.Merge(AssignValue.Position), EqPos, scope, new[] { VarInitialize.Call(null) }, new[] {
                new Ident(decl.Position, decl.Name, false).Annotate(scope),
                AssignValue.Annotate(assignScope)
            });
            ret.AddTokens(new[]{ Keyword(ConstKwPos), Keyword(VarKw.Position), Type(Typ?.Position)});
            return ret;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"{VarKw.Content} {Id.Content} = ";
            foreach (var w in AssignValue.DebugPrint())
                yield return w;
        }
    }

    public ReflectionException emptyMemberErr => new(Position, "An identifier is required after this period.");

    /// <summary>
    /// A member access `x.y`.
    /// `x` may be an import name, in which case this is an imported script member,
    /// or `x` may be a type name, in which case this is a static member,
    /// or `x` may be a variable, in which case this is an instance member.
    /// </summary>
    public record MemberAccess(ST Object, Ident Member) : ST(Object.Position.Merge(Member.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (Object is Ident id) {
                if (scope.ImportDecls.TryGetValue(id.Name, out var imp)) {
                    if (Ident.TryFindVariable(Position, Member, imp, scope) is { } tree) {
                        tree.AddTokens(new[] { Keyword(Object.Position) });
                        return tree;
                    } else {
                        return new AST.Failure(Member.Name.Length > 0 ?
                                new(Position,
                                    $"There is no reference with name `{Member.Name}` in import `{imp.Name}`.") :
                                new(Member.Position,
                                    $"An identifier is required after this period, eg. `{imp.Name}.myVariable`.")
                            , imp.Ef.Scope) { ImportedScript = imp, IsImportedScriptMember = true };
                    }
                } else if (Parser.TypeFromString(id.Name).TryL(out var typ)) {
                    var methods = typ.GetMember(Member.Name)
                        .SelectNotNull(TypeMember.MaybeMake)
                        .Where(m => m.Static && m.Params.Length == 0)
                        .ToList();
                    if (methods.Count == 0)
                        return new AST.Failure(Member.Name.Length == 0 ? emptyMemberErr : new(Position, 
                                $"No member {Member.Name} was found on type {typ.RName()}"), scope) 
                            { Completions = (typ, Member.Name) };
                    var ast = AST.MethodCall.Make(Position, Member.Position, scope,
                        methods.Select(m => MethodSignature.Get(m).Call(Member.Name)).ToArray(), System.Array.Empty<ST>());
                    ast.AddTokens(new[] { Type(id.Position) });
                    return ast;
                }
            }
            var inst = Object.Annotate(scope);
            if (Member.Name.Length == 0)
                return new AST.InstanceFailure(emptyMemberErr, scope, inst);
            return new AST.InstanceMethodCall(Position, Member.Position, scope, Member.Name, inst);
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
    /// A member function call `x.f(y)`.
    /// `x` may be an import name, in which case this is an imported script function,
    /// or `x` may be a type name, in which case this is a static function,
    /// or `x` may be a variable, in which case this is an instance function.
    /// </summary>
    public record MemberFunction(PositionRange Position, ST Object, Ident Member, List<ST> Args) : ST(Position) {
        private ST[] AllArgs { get; } = Args.Prepend(Object).ToArray();

        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (Object is Ident id) {
                if (scope.ImportDecls.TryGetValue(id.Name, out var imp)) {
                    if (FunctionCall.LoadScriptFnDecl(Position, Member, imp, scope, Args) is { } sfn) {
                        (sfn as AST)?.AddTokens(new[] { Keyword(Object.Position) });
                        return sfn;
                    } else {
                        return new AST.Failure(Member.Name.Length > 0 ?
                                new(Position,
                                    $"There is no script function with name `{Member.Name}` in import `{imp.Name}`.") :
                                new(Member.Position,
                                    $"An identifier is required after this period, eg. `{imp.Name}.myFunction`."),
                            imp.Ef.Scope) { ImportedScript = imp, IsImportedScriptMember = true };
                    }
                } else if (Parser.TypeFromString(id.Name).TryL(out var typ)) {
                    var methods = typ.GetMember(Member.Name)
                        .SelectNotNull(TypeMember.MaybeMake)
                        .Where(m => m.Static && m.Params.Length == Args.Count)
                        .ToList();
                    if (methods.Count == 0)
                        return new AST.Failure(Member.Name.Length == 0 ? emptyMemberErr : new(Position, 
                                $"No method {Member.Name} with {Args.Count} arguments was found on type {typ.RName()}"), scope) 
                            { Completions = (typ, Member.Name) };
                    var ast = AST.MethodCall.Make(Position, Member.Position, scope,
                        methods.Select(m => MethodSignature.Get(m).Call(Member.Name)).ToArray(), Args);
                    ast.AddTokens(new[] { Type(id.Position) });
                    return ast;
                }
            }
            var inst = Object.Annotate(scope);
            if (Member.Name.Length == 0)
                return new AST.InstanceFailure(emptyMemberErr, scope, inst);
            return new AST.InstanceMethodCall(Position, Member.Position, scope, Member.Name,
                Args.Select(a => a.Annotate(scope)).Prepend(inst).ToArray());
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return ".";
            foreach (var w in Member.DebugPrint())
                yield return w;
        }
    }

    public record Indexer(ST Object, PositionRange OpenBrace, ST Index, PositionRange CloseBrace)
        : ST(Object.Position.Merge(CloseBrace)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var ast = new AST.Indexer(Position, scope, Object.Annotate(scope), Index.Annotate(scope));
            ast.AddTokens(new[] { Op(OpenBrace), Op(CloseBrace) });
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return "[";
            foreach (var w in Index.DebugPrint())
                yield return w;
            yield return "]";
        }
    }

    /// <summary>
    /// A C#-style function call with parentheses and commas, such as `f(x, y)`,
    ///  or an operator-based function call such as `x + y`.
    /// <br/>Note that there must be no spaces before the parentheses.
    /// </summary>
    public record FunctionCall(PositionRange Position, ST Fn, params ST[] Args) : ST(Position) {
        public bool OverloadsInterchangeable { get; init; } = false;

        /// <summary>
        /// Try to invoke the script function with name `id`, defined in the scope of `inImport` (if provided)
        ///  else in `scope`.
        /// <br/>If there is no such script function, return null.
        /// </summary>
        public static IAST? LoadScriptFnDecl(PositionRange Position, Ident id, ScriptImport? inImport, LexicalScope scope, IReadOnlyList<ST> Args) {
            var fnScope = inImport?.Ef.Scope ?? scope;
            var (key, isDynamic) = (id.Name[0] == '&' ? (id.Name.Substring(1), inImport == null) : (id.Name, false));
            var sfn = fnScope.FindScopedFunction(key, isDynamic ? DeclarationLookup.Dynamic : DeclarationLookup.Standard);
            if (sfn != null) {
                if (sfn.CallType.Arguments.Length - 1 != Args.Count)
                    return new AST.Failure(new(Position, id.Position,
                        $"The script function `{id.Name}` takes {sfn.CallType.Arguments.Length - 1} arguments," +
                        $" but {Args.Count} were provided."), fnScope);
                return new AST.ScriptFunctionCall(Position, id.Position, scope, inImport, sfn, isDynamic,
                    Args.Select(a => a.Annotate(scope)).ToArray());
            } else return null;
        }

        public static string NoMethodFoundErr(string name, LexicalScope scope) {
            var err = $"Couldn't find any method by the name `{name}`.";
            var unreachable = scope.ScriptRoot.AllFnsInDescendantScopes.Where(x => x.Name == name).ToList();
            if (unreachable.Count > 0) {
                err += $"\nThere are some declarations for \"{name}\", but they are not visible from this lexical scope:" + 
                       $"\n\t{string.Join("\n\t", unreachable.Select(x => $"{x.Name} at {x.Position}"))}";
                if (scope.IsConstScope && unreachable.Any(u => !u.IsConstant)) 
                    err += "\nThis is a constant scope. You can only reference const functions.";
                else {
                        err += $"\nMaybe you need to hoist the declaration for \"{name}\" (use `hfunction` instead of `function`).";
                    if (scope is DynamicLexicalScope)
                        err += $"\nThis is a dynamic scope. You may need to use dynamic scoping on the function " +
                           $"(ie. `&{name}(args)` instead of `{name}(args)`).";
                }
            }
            return err;
        }
        
        protected override IAST _AnnotateInner(LexicalScope scope) {
            //If we're directly calling a *static method*, then we already know the signatures
            if (Fn is FnIdent fn) {
                return AST.MethodCall.Make(Position, fn.Position, scope, fn.Func, Args, OverloadsInterchangeable);
            } else if (Fn is Ident id) { 
                if (LoadScriptFnDecl(Position, id, null, scope, Args) is {} sfn) {
                    return sfn;
                } else if (scope.FindStaticMethodDeclaration(id.Name.ToLower()) is { } decls) {
                    var argFilter = decls.Where(d => d.Params.Length == Args.Length).ToList();
                    if (argFilter.Count > 0)
                        return AST.MethodCall.Make(Position, id.Position, scope,
                            argFilter.Select(d => d.Call(id.Name)).ToArray(), Args, OverloadsInterchangeable);
                    else {
                        var prms = Args.Select(a => a.Annotate(scope)).ToArray();
                        if (prms.Length == 0) {
                            var nextPos = new PositionRange(id.Position.End, Position.End);
                            prms = new IAST[] {
                                new AST.Failure(new(nextPos, "At least one argument is required here."), scope) {
                                    PossibleTypes = decls.Select(x => x.SharedType.Arguments[0]).Distinct().ToArray()
                                }
                            };
                        }
                        return new AST.Failure(new(Position, id.Position,
                            $"There is no method by name `{id.Name}` that takes {Args.Length} arguments." +
                            $"\nThe signatures of the methods named `{id.Name}` are as follows:" +
                            $"\n\t{string.Join("\n\t", decls.Select(o => o.AsSignature))}"), scope, prms) {
                            Completions = decls
                        };
                    }
                } else if (scope.FindVariable(id.Name) is { } varn) {
                    //Note that we must prioritize static methods over lambdas in the basic case due to 
                    // "times" being a GCX property method and an autovar!
                    return new AST.LambdaCall(Position, scope, Args.Select(a => a.Annotate(scope)).Prepend(
                        new AST.Reference(id.Position, scope, null, id.Name, varn)).ToArray());
                } else {
                    return new AST.Failure(new(Position, NoMethodFoundErr(id.Name, scope)), scope);
                }
            } else if (Fn is not DefaultValue)
                return new AST.LambdaCall(Position, scope, Args.Prepend(Fn).Select(a => a.Annotate(scope)).ToArray());
            else
                return new AST.Failure(new(Position, "Cannot call `null`."), scope);
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

    public record Constructor(PositionRange Position, PositionRange NewKw, Lexer.Token Typ, params ST[] Args) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var kt = Parser.TypeFromToken(Typ, scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            var decls = typ!.GetConstructors().SelectNotNull(MethodSignature.MaybeGet).ToList();
            var argFilter = decls.Where(d => d.Params.Length == Args.Length).ToList();
            if (argFilter.Count > 0) {
                var ast = AST.MethodCall.Make(Position, Typ.Position, scope,
                    argFilter.Select(d => d.Call(null)).ToArray(), Args);
                ast.AddMethodSemanticToken = false;
                ast.AddTokens(new[] { Keyword(NewKw), Type(Typ.Position) });
                return ast;
            } else {
                var prms = Args.Select(a => a.Annotate(scope)).ToArray();
                return new AST.Failure(new(Position, Typ.Position,
                    $"There is no constructor for type {typ.RName()} that takes {Args.Length} arguments." +
                    $"\nThe valid constructors are as follows:" +
                    $"\n\t{string.Join("\n\t", decls.Select(o => o.AsSignature))}"), scope, prms) {
                    Completions = decls
                };
            }
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"new {Typ.Content}(";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return ")";
            
        }
    }
    
    /// <summary>
    /// A function call that explicitly declares possibly fewer arguments than required by the function.
    /// <br/>In the form $(f, a1, a2, a3); where in this case function f requires 3 or more arguments.
    /// </summary>
    public record PartialFunctionCall(PositionRange Position, ST Func, params ST[] Args) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (Func is Ident Fn) {
                if (scope.FindStaticMethodDeclaration(Fn.Name.ToLower()) is { } decls) {
                    var argFilter = decls.Where(d => d.Params.Length >= Args.Length).ToList();
                    if (argFilter.Count > 0)
                        return new AST.PartialMethodCall(Position, Fn.Position, scope,
                            argFilter.Select(a => a.Call(Fn.Name)).ToArray(),
                            Args.Select(a => a.Annotate(scope)).ToArray());
                    else
                        return new AST.Failure(new(Position, Fn.Position,
                            $"There is no method by name `{Fn.Name}` that takes at least {Args.Length} arguments." +
                            $"\nThe signatures of the methods named `{Fn.Name}` are as follows:" +
                            $"\n\t{string.Join("\n\t", decls.Select(o => o.AsSignature))}"), scope) {
                            Completions = decls
                        };
                } else if (scope.FindVariable(Fn.Name) is { } varn) {
                    return new AST.PartialLambdaCall(Position, scope, Args.Select(a => a.Annotate(scope)).Prepend(
                        new AST.Reference(Fn.Position, scope, null, Fn.Name, varn)).ToArray());
                } else if (scope.FindScopedFunction(Fn.Name, DeclarationLookup.Standard) is {} sfn)
                    return new AST.PartialScriptFunctionCall(Position, Fn.Position, scope, null, sfn, 
                        Args.Select(a => a.Annotate(scope)).ToArray());
                else
                    return new AST.Failure(new(Position, FunctionCall.NoMethodFoundErr(Fn.Name, scope)), scope)
                        {Completions = null as List<MethodSignature> };
            } else
                return new AST.PartialLambdaCall(Position, scope, 
                    Args.Prepend(Func).Select(a => a.Annotate(scope)).ToArray());
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Func.DebugPrint())
                yield return w;
            yield return "(";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return ")";
        }
    }

    
    /// <summary>
    /// A Haskell-style curried function call, such as `f x`.
    /// <br/>In cases where multiple arguments are applied, this is constructed left-associatively as
    ///  `f x y z` = P(P(P(f, x), y), z).
    /// </summary>
    public record CurriedFunctionCall(ST Fn, ST Arg) : ST(Fn.Position.Merge(Arg.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            //Get the leftmost function and a list of args
            var argsl = new List<ST>() { Arg };
            var leftmost = Fn;
            while (leftmost is CurriedFunctionCall pfc) {
                argsl.Add(pfc.Arg);
                leftmost = pfc.Fn;
            }
            argsl.Reverse();
            var args = argsl.ToArray();

            (string name, IEnumerable<(int reqArgs, IEnumerable<Reflector.InvokedMethod> methods)>)? ArgCounts(ST func) {
                if (func switch {
                        FnIdent fn => fn.Func,
                        Ident id => 
                            Ident.TryFindVariable(id.Position, id, null, scope) is null &&
                                scope.FindStaticMethodDeclaration(id.Name.ToLower()) is { } decls ?
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
                var item = index >= 0 ? args[index] : leftmost;
                if (ArgCounts(item) is not { } counts)
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
                    "The parser thinks this is a method call, but no static method with this name was found. " +
                    "If this is an instance method, script function, or lambda, please parenthesize the arguments. " +
                    "If this is not a method call, then there is something wrong with your syntax."), scope);
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
                return tree.PossibleUnifiers(scope.GlobalRoot.Resolver, Unifier.Empty).IsLeft;
            }
            //If there are overloads that match the numerical requirements, use them
            var sameArgsReq = overloads.Where(f => f.Params.Length == args.Length).ToArray();
            if (sameArgsReq.Length > 0)
                return AST.MethodCall.Make(Position, leftmost.Position, scope, sameArgsReq, args);
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
                            $"\nAttempted to automatically group functions, but multiple combinations of the following functions " +
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
                var argStack = new Stack<ST>();
                var mArgsTmp = new List<ST>();
                for (int ii = args.Length - 1; ii >= -1; --ii) {
                    var st = ii >= 0 ? args[ii] : leftmost;
                    if (!argsCts.TryGetValue(ii, out var mArgCt) || mArgCt == 0)
                        argStack.Push(st);
                    else {
                        for (int ima = 0; ima < mArgCt; ++ima)
                            mArgsTmp.Add(argStack.Pop());
                        var mOverloads = (GetOverloads(st, scope) ??
                                          throw new StaticException("Incorrect partial application parameter grouping"))
                            .Where(o => o.Params.Length == mArgCt)
                            .ToArray();
                        if (mOverloads.Length == 0)
                            throw new StaticException("Incorrect partial application overload lookup");
                        argStack.Push(
                            new FunctionCall(st.Position.Merge(mArgsTmp[^1].Position), st, mArgsTmp.ToArray()));
                        mArgsTmp.Clear();
                    }
                }
                return argStack.Pop().Annotate(scope);
            } else
                return new AST.Failure(new(leftmost.Position,
                    $"When resolving the partial method invocation for `{overloads[0].CalledAs ?? overloads[0].Name}`," +
                    $" {argsl.Count} parameters were provided, but overloads were only found with " +
                    $"{string.Join(", ", overloads.Select(o => o.Params.Length).Distinct().OrderBy(x => x))} parameters."),
                    scope
                );
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

    public record TypeAs(ST Object, PositionRange AsKw, ST TypeDef) : ST(Object.Position.Merge(TypeDef.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            if (TypeDef is not Ident tid)
                return new AST.Failure(new(TypeDef.Position, "This must be a type name."), scope);
            var ptyp = Parser.TypeFromString(tid.Name);
            if (!ptyp.TryL(out var typ))
                return new AST.Failure(new(TypeDef.Position, ptyp.Right), scope) { IsTypeCompletion = true };
            var ast = new AST.TypeAs(Position, scope, typ, Object.Annotate(scope));
            ast.AddTokens(new[] { Keyword(AsKw), Type(TypeDef.Position) });
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            foreach (var w in Object.DebugPrint())
                yield return w;
            yield return " as ";
            foreach (var w in TypeDef.DebugPrint())
                yield return w;
        }
    }
    
    public record FunctionDef(Lexer.Token Kw, Lexer.Token Name, List<(Lexer.Token name, Lexer.Token? typ)> Args,
        Lexer.Token? ReturnType, Block Body) : ST(Kw.Position.Merge(Body.Position)) {
        public PositionRange? ConstKwPos { get; set; }
        
        protected override IAST _AnnotateInner(LexicalScope scope) {
            bool hoist = Kw.Content == "hfunction";
            var localScope = LexicalScope.Derive(hoist ? scope.HoistedScope : scope);
            localScope.Type = LexicalScopeType.ExpressionEF;
            localScope.IsConstScope = ConstKwPos != null;
            var krt = Parser.TypeFromToken(ReturnType, scope, allowVoid:true);
            if (!krt.TryL(out var retTyp))
                return krt.Right;
            var rt = localScope.Return = new ReturnStatementConfig(localScope, retTyp);
            var args = new ImplicitArgDecl[Args.Count];
            for (int ii = 0; ii < Args.Count; ++ii) {
                var (name, mtypStr) = Args[ii];
                var kat = Parser.TypeFromToken(mtypStr, scope);
                if (!kat.TryL(out var argTyp))
                    return kat.Right;
                args[ii] = new ImplicitArgDecl(name.Position, argTyp, name.Content);
            }
            var fnCallType =
                TypeDesignation.Dummy.Method(rt.Type, args.Select(a => a.TypeDesignation).ToArray());
            var decl = new ScriptFnDecl(null!, hoist, Name.Content, args, fnCallType) {
                IsConstant = localScope.IsConstScope
            };
            rt.Function = decl;
            if (scope.Declare(decl).TryR(out var prev))
                return new AST.Failure(new(Position, 
                    $"The function {decl.Name} has already been declared at {prev.Position}."), scope);
            //due to return statements, the block content itself may have any type
            var _block = Body.AnnotateWithParameters(localScope, null, args);
            if (!_block.TryL(out var block))
                return _block.Right;
            var ast = new AST.ScriptFunctionDef(Position, Name.Content, scope, localScope, decl, block);
            decl.Tree = ast;
            ast.AddTokens(Args.Select(a => Type(a.Item2?.Position)).Concat(new[] {
                    Keyword(ConstKwPos),
                    Keyword(Kw.Position),
                    new SemanticToken(Name.Position, SemanticTokenTypes.Function).WithConst(decl.IsConstant),
                    Type(ReturnType?.Position)
                }));
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "function";
            yield return Name.Content;
            yield return "(";
            foreach (var (a, _) in Args)
                yield return a.Content;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in Body.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
        }
    }

    /// <summary>
    /// A block of statements.
    /// </summary>
    public record Block(PositionRange Position, IReadOnlyList<ST> Args) : ST(Position) {
        public Block(IReadOnlyList<ST> args) : this(args.Count > 0 ? 
            args[0].Position.Merge(args[^1].Position) : 
            new Position(0, 1, 0).CreateEmptyRange(), args) { }

        private IAST[] AnnotateStmts(LexicalScope scope) {
            var args = Args.Select(a => a.Annotate(scope)).ToArray();
            if (args.Length == 0)
                args = new IAST[] { new AST.DefaultValue(Position, scope, typeof(void)) };
            return args;
        }
        
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var localScope = LexicalScope.Derive(scope);
            return new AST.Block(Position, scope, localScope, null, AnnotateStmts(localScope));
        }

        //note: these functions requires localScope to be passed in instead of enclosingScope
        public Either<AST.Block, AST.Failure> AnnotateWithParameters(LexicalScope localScope, IDelegateArg[] arguments) {
            var decls = new ImplicitArgDecl[arguments.Length];
            for (int ii = 0; ii < arguments.Length; ++ii) {
                decls[ii] = arguments[ii].MakeImplicitArgDecl();
            }
            return AnnotateWithParameters(localScope, null, decls);
        }
        
        public Either<AST.Block, AST.Failure> AnnotateWithParameters(LexicalScope localScope, TypeDesignation? retType, ImplicitArgDecl[] arguments) {
            var decls = new (VarDecl, ImplicitArgDecl)[arguments.Length];
            for (int ii = 0; ii < arguments.Length; ++ii) {
                var imp = arguments[ii];
                decls[ii] = (new VarDecl(imp.Position, false, imp.KnownType, imp.Name, imp), imp);
                if (localScope.Declare(decls[ii].Item1) is {IsRight: true} r) {
                    return new AST.Failure(new(Position, 
                        $"The variable {arguments[ii].Name} has already been declared at {r.Right.Position}."), localScope);
                }
            }
            return new AST.Block(Position, localScope.Parent!, localScope, retType, AnnotateStmts(localScope))
                .WithFunctionParams(decls);
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"block({{";
            foreach (var w in IDebugPrint.PrintArgs(Args))
                yield return w;
            yield return "})";
        }
    }
    
    
    
    /// <summary>
    /// An if expression (x ? y : z) that returns one of two values.
    /// </summary>
    public record IfExpression(ST Condition, ST TrueBody, ST FalseBody)
        : ST(Condition.Position.Merge(FalseBody.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var ast = new AST.Conditional(Position, scope, true, Condition.Annotate(scope), TrueBody.Annotate(scope),
                FalseBody.Annotate(scope));
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "(";
            foreach (var w in Condition.DebugPrint())
                yield return w;
            yield return ") ?";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in TrueBody.DebugPrint())
                yield return w;
            yield return ":";
            yield return PrintToken.newline;
            foreach (var w in TrueBody.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
        }
    }

    
    /// <summary>
    /// An if statement (if (x) { y; } else { z; }) with an optional else.
    /// </summary>
    public record IfStatement(PositionRange ifKw, PositionRange? elseKw, ST Condition, Block TrueBody, ST? FalseBody)
        : ST(ifKw.Merge(FalseBody?.Position ?? TrueBody.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var ast = new AST.Conditional(Position, scope, false, Condition.Annotate(scope), TrueBody.Annotate(scope),
                FalseBody?.Annotate(scope));
            ast.AddTokens(new[] { Keyword(ifKw), elseKw is { } ekp ? Keyword(ekp) : null });
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "if (";
            foreach (var w in Condition.DebugPrint())
                yield return w;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in TrueBody.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
            if (FalseBody != null) {
                yield return " else {";
                yield return PrintToken.indent;
                yield return PrintToken.newline;
                foreach (var w in TrueBody.DebugPrint())
                    yield return w;
                yield return PrintToken.dedent;
                yield return PrintToken.newline;
                yield return "}";
            }
        }
    }

    
    
    /// <summary>
    /// An for or while loop.
    /// </summary>
    public record Loop(PositionRange KwPos, ST? Initializer, ST? Condition, ST? Finalizer, Block Body)
        : ST(KwPos.Merge(Body.Position)) {
        protected override IAST _AnnotateInner(LexicalScope scope) {
            var localScope = LexicalScope.Derive(scope);
            localScope.ContinueBreak = (Ex.Label(), Ex.Label(), localScope);
            //order is important for declarations to be read properly
            var init = Initializer?.Annotate(localScope);
            var cond = Condition?.Annotate(localScope);
            var body = (AST.Block)Body.Annotate(localScope);
            body.EndWithLabel = localScope.ContinueBreak.Value.c;
            var ast = new AST.Loop(Position, scope, localScope, init, cond, Finalizer?.Annotate(localScope), body);
            ast.AddTokens(new[] { Keyword(KwPos) });
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"for (";
            if (Initializer != null)
                foreach (var w in Initializer.DebugPrint())
                    yield return w;
            yield return "; ";
            if (Condition != null)
                foreach (var w in Condition.DebugPrint())
                    yield return w;
            yield return "; ";
            if (Finalizer != null)
                foreach (var w in Finalizer.DebugPrint())
                    yield return w;
            yield return ") {";
            yield return PrintToken.indent;
            yield return PrintToken.newline;
            foreach (var w in Body.DebugPrint())
                yield return w;
            yield return PrintToken.dedent;
            yield return PrintToken.newline;
            yield return "}";
        }
    }
    
    /// <summary>
    /// An array of items with the same type.
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
    public record Number(PositionRange Position, string Value) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) => new AST.Number(Position, scope, Value);

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Value;
        }
    }

    public record DefaultValue(PositionRange Position, Lexer.Token? Typ) : ST(Position) {

        protected override IAST _AnnotateInner(LexicalScope scope) {
            var kt = Parser.TypeFromToken(Typ, scope);
            if (!kt.TryL(out var typ))
                return kt.Right;
            var ast = new AST.DefaultValue(Position, scope, typ);
            ast.AddTokens(new[] { Type(Typ?.Position) });
            return ast;
        }

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "null";
        }
    }
    
    /// <summary>
    /// A fixed value of a type not subject to type auto-determination, such as strings, but not numbers.
    /// </summary>
    public record TypedValue<T>(PositionRange Position, T Value) : ST(Position) {
        public SymbolKind Kind { get; init; } = SymbolKind.Constant;
        protected override IAST _AnnotateInner(LexicalScope scope) => 
            new AST.TypedValue<T>(Position, scope, Value, Kind);
        public override IEnumerable<PrintToken> DebugPrint() {
            yield return Value?.ToString() ?? "<null>";
        }
    }

    public record Tuple(PositionRange Position, List<ST> Args) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) =>
            new AST.Tuple(Position, scope, Args.Select(a => a.Annotate(scope)).ToArray());

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return "(";
            for (int ii = 0; ii < Args.Count; ++ii) {
                if (ii > 0) 
                    yield return ", ";
                foreach (var x in Args[ii].DebugPrint())
                    yield return x;
            }
            yield return ")";
        }
    }

    public record Failure(PositionRange Position, string Error) : ST(Position) {
        protected override IAST _AnnotateInner(LexicalScope scope) =>
            new AST.Failure(new ReflectionException(Position, Error), scope);

        public override IEnumerable<PrintToken> DebugPrint() {
            yield return $"throw new Exception(\"{Error}\")";
        }
    }

}
}
using System.Collections.Generic;
using BagoumLib.Expressions;
using Danmokou.Reflection2;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;

namespace Danmokou.Reflection {
public interface IDebugAST : IDebugPrint {
    /// <summary>
    /// Position of the code that will generate this object.
    /// <br/>This is used for debugging/logging/error messaging.
    /// </summary>
    PositionRange Position { get; }
    
    IEnumerable<IDebugAST> Children { get; }
    
    /// <summary>
    /// Return the furthest-down AST in the tree that encloses the given position,
    /// then its ancestors up to the root.
    /// <br/>Each element is paired with the index of the child that preceded it,
    ///  or null if it is the lowermost AST returned.
    /// <br/>Returns null if the AST does not enclose the given position.
    /// </summary>
    IEnumerable<(IDebugAST tree, int? childIndex)>? NarrowestASTForPosition(PositionRange p);
    
    /// <summary>
    /// Print out a readable, preferably one-line description of the AST (not including its children). Consumed by language server.
    /// </summary>
    [PublicAPI]
    string Explain();
    
    /// <summary>
    /// Return a parse tree for the AST. Consumed by language server.
    /// </summary>
    [PublicAPI]
    DocumentSymbol ToSymbolTree(string? descr = null);

    /// <summary>
    /// Describe the semantics of all the parsed tokens in the source code.
    /// Consumed by language server.
    /// </summary>
    [PublicAPI]
    IEnumerable<SemanticToken> ToSemanticTokens();
}
}
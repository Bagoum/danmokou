#if INTERACTIVE
#I "C:\Users\Bagoum\.nuget\packages\\fparsec\1.1.1\lib\\net45"
#r "FParsecCS.dll"
#r "FParsec.dll"
#else
module FParser.ParserCommon
#endif
open FParsec
open System

let OPEN_ARG = '('
let CLOSE_ARG = ')'
let ARG_SEP = ','

let betweenChars c1 c2 p = pchar c1 >>. p .>> pchar c2
let betweenStr s1 s2 p = pstring s1 >>. p .>> pstring s2
let bounded c = pchar c >>. manySatisfy (fun x -> x <> c) .>> pchar c
let whiteInline c =
    c <> '\n' && Char.IsWhiteSpace c
let _paren p = betweenChars OPEN_ARG CLOSE_ARG (spaces >>. sepBy p (spaces .>> pchar ARG_SEP .>> spaces))
let _paren1 p = betweenChars OPEN_ARG CLOSE_ARG p
let ilspaces<'t> :Parser<unit, 't> = skipManySatisfy whiteInline
let ilspaces1<'t> :Parser<unit, 't> = skipMany1Satisfy whiteInline
let printPosition (p:Position) =
    sprintf "Line %d:" p.Line
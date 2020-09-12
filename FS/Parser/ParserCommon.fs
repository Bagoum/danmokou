module FParser.ParserCommon
open FParsec
open System
open Common.Functions

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
let explicitSepBy a sep = pipe2 a (many (sep .>>. a)) (fun hd tl -> hd::(detuple tl)) <|>% []
let ilspaces<'t> :Parser<unit, 't> = skipManySatisfy whiteInline
let ilspaces1<'t> :Parser<unit, 't> = skipMany1Satisfy whiteInline
let printPosition (p:Position) =
    sprintf "Line %d:" p.Line
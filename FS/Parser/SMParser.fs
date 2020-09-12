module FParser.SMParser
open Common.Functions
open Common.Types
open Common.Extensions
open FParsec
open System
open FParser.ParserCommon


let COMMENT = '#'
let PROP_MARKER = "<!>"
let PROP2_MARKER = "<#>"
let PROP_KW = "!$_PROPERTY_$!"
let PROP2_KW = "!$_PARSER_PROPERTY_$!"
let PAREN_OPEN_KW = "!$_PAREN_OPEN_$!"
let PAREN_CLOSE_KW = "!$_PAREN_CLOSE_$!"
let ARGSEP_KW = "!$_ARG_SEPARATOR_$!"
let OPEN_PF = '['
let CLOSE_PF = ']'
let QUOTE = '`'
let MACRO_INVOKE = '$'
let MACRO_VAR = '%'
let MACRO_OL_OPEN = "!!{"
let MACRO_OPEN = "!{"
let MACRO_CLOSE = "!}"
let LAMBDA_MACRO_PRM = "!$"
let MACRO_REINVOKE = "$%"
let private isLetter c =
    c <> COMMENT && c <> MACRO_INVOKE && c <> MACRO_VAR && c <> '!'
    && c <> OPEN_ARG && c <> CLOSE_ARG && c <> ARG_SEP
    && c <> OPEN_PF && c <> CLOSE_PF
    && c <> QUOTE && not <| Char.IsWhiteSpace c
    
let failErrorable = function
    | Failed errs -> fail <| String.concat "\n" errs
    | OK vl -> preturn vl

type internal ParseUnit =
    | Atom of string
    //Quotes may be empty, whereas empty atoms are removed.
    //Atoms also have special handling for eg. {{
    | Quote of string 
    | MacroVar of string // %var
    | LambdaMacroParam // //!$
    | PartialMacroInvoke of (Macro * ParseUnit list) //$add(!$; 5)
    | MacroReinvocation of (string * ParseUnit list) //$%func(6)
    | Words of ParseUnit list
    | NoSpaceWords of ParseUnit list
    | Postfix of ParseUnit list
    | MacroDef of string
    | Newline
    | End
with
    static member Nest(ls: ParseUnit list) =
        if List.length ls = 1
        then List.head ls
        else Words ls
    member this.Reduce() =
        let rec reduce (ls: ParseUnit list) recons =
            if List.length ls = 1
            then ls.[0].Reduce()
            else recons ls
        match this with
        | Words x -> reduce x Words
        | NoSpaceWords x -> reduce x NoSpaceWords
        | Postfix x -> reduce x Postfix
        | x -> x
    member this.CutTrailingNewline() =
        let cut ls =
            let lm1 = List.length ls - 1
            match ls.[lm1] with
            | Newline -> List.take lm1 ls
            | _ -> ls
        match this with
        | Words x -> Words <| cut x
        | NoSpaceWords x -> NoSpaceWords <| cut x
        | Postfix x -> Postfix <| cut x
        | x -> x
and internal MacroArg =
    | Req of string
    | Default of (string * ParseUnit)
with
    static member AsString ma =
        match ma with
        | Req s -> s
        | Default(s,d) -> s
    static member AsDefault ma =
        match ma with
        | Req s -> None
        | Default(s,d) -> Some d
and internal Macro = {
    name: string
    nprms: int
    prmIndMap: Map<string, int>
    prmDefaults : ParseUnit option list
    unformatted: ParseUnit
} with
    static member private ResolveUnit argResolve macroReinvResolve x =
        let resolveAcc = List.map (Macro.ResolveUnit argResolve macroReinvResolve) >> Errorable<_>.Acc
        match x with
        | MacroVar s -> argResolve s
        | Words ats -> (resolveAcc ats).fmap Words
        | NoSpaceWords ats -> (resolveAcc ats).fmap NoSpaceWords
        | Postfix ats -> (resolveAcc ats).fmap Postfix
        | MacroReinvocation (s, args) -> macroReinvResolve(s, args)
        | x -> OK x
    member private this.RealizeOverUnit (args: ParseUnit list) unformatted =
        unformatted |> Macro.ResolveUnit (fun s ->
            match this.prmIndMap.TryFind(s) with
            | None -> Failed [sprintf "Macro body has nonexistent variable \"%s%s\"" "%" s]
            | Some i -> args.[i] |> OK) (fun (s, rargs) ->
            match this.prmIndMap.TryFind(s) with
            | None -> Failed [sprintf "Macro body has nonexistent reinvocation  \"%s%s\"" "$%" s]
            | Some i -> match args.[i].Reduce() with
                        | PartialMacroInvoke (m, pargs) ->
                            let isLambda = (function | LambdaMacroParam -> true | _ -> false)
                            match replaceEntries true pargs rargs isLambda with
                            | Enough x -> (Errorable<_>.Acc (List.map (this.RealizeOverUnit args) x)).bind(m.Invoke)
                            | _ -> Failed [ sprintf "Macro \"%s\" provides too many arguments to partial macro \"%s\". (%d provided, %d required)"
                                                this.name m.name (List.length rargs) (countFilter isLambda pargs) ]
                        | other -> Failed [sprintf "Macro argument \"%s.%s%s\" (arg #%d) must be a partial macro invocation.
                                           This may occur if you already provided all necessary arguments. %O" this.name "%" s i other])
    member internal this.Realize (args: ParseUnit list) =
        if this.nprms = 0 then OK this.unformatted
        else this.RealizeOverUnit args this.unformatted
    member internal this.Invoke (args: ParseUnit list) =
        
        if (args.Length <> this.nprms)
            then
                let defaults = this.prmDefaults |> List.SoftSkip(args.Length) |> List.FilterNone
                if (args.Length + defaults.Length <> this.nprms)
                    then Failed [sprintf "Macro \"%s\" requires %d arguments (%d provided)" this.name this.nprms args.Length]
                    else this.Realize (args@defaults)
            else if args |> List.exists (function | LambdaMacroParam -> true | _ -> false)
                 then (this, args) |> PartialMacroInvoke |> OK
                 else this.Realize args
    static member internal PrmChar = letter <|> digit <|> pchar '_'
    static member internal Prm = Macro.PrmChar |> many1Chars
    static member internal Create name prms (unformatted: ParseUnit) = {
        name = name
        nprms = List.length prms
        prmIndMap = List.mapi (fun i x -> (MacroArg.AsString x, i)) prms |> Map.ofList
        prmDefaults = List.map (MacroArg.AsDefault) prms 
        unformatted = unformatted.CutTrailingNewline()
    }
and internal State = {
   macros: Map<string, Macro>
}
        
type internal LocatedParseUnit = (ParseUnit * Position)

type internal LPUF = 
    static member Map f ((x,y): LocatedParseUnit) = (f x, y)
    static member IMap f ((x,_): LocatedParseUnit) = f x
    static member Remap f ls = List.map (LPUF.Map f) ls
    static member RemapErr f ls = List.map (fun (x,y) -> match f x with
                                                         | OK v -> OK (v, y)
                                                         | Failed errs -> Failed errs) ls
    

let private locate pu =
    getPosition |>> (fun x -> (pu, x))



let private _explicitParen p = pchar OPEN_ARG >>% Atom PAREN_OPEN_KW
                           .>>. (spaces >>. explicitSepBy p (spaces >>. (pchar ARG_SEP >>% Atom ARGSEP_KW) .>> spaces))
                           .>>. (pchar CLOSE_ARG >>% Atom PAREN_CLOSE_KW) |>>
                                (fun ((o, a), c) -> List.append (o::a) [c])


let private simpleString take = isLetter |> take
let private simpleString1 = simpleString many1Satisfy
let private simpleString0 = simpleString manySatisfy
let private sepByAll2 p sep = (p .>>. sep .>>. p .>>. many (sep .>>. p)) |>>
                                (fun (((h0, h1), h2), hl) ->
                                h0::h1::h2::List.foldBack (fun x acc -> (fst x)::(snd x)::acc) hl []          
                            )

let private compileMacro = List.mapi (fun i x -> if i % 2 = 0 then Atom x else MacroVar x) >> NoSpaceWords
let private invokeMacroByName (n, (args: ParseUnit list)) =
    getUserState >>= (fun state ->
        match state.macros.TryFind(n) with
        | None -> fail <| sprintf "No macro exists with name %s." n
        | Some m -> m.Invoke(args) |> failErrorable)
                        
let private cnewln = pchar COMMENT .>> skipManySatisfy (fun x -> x <> '\n') |> optional >>. newline
let rec private word allowNewline x =
    if allowNewline
    then mainParserNL x
    else mainParser x
and private lword allowNewline x =
    if allowNewline
    then lmainParserNL x
    else lmainParser x
and private wordsTopLevel = many1 (pipe2 (word true) ilspaces <| fun x _ -> x)
and private lwordsTopLevel = many1 (pipe2 (lword true) ilspaces <| fun x _ -> x)
and private wordsInBlock = many1 (pipe2 (word true) ilspaces <| fun x _ -> x)
and private wordsInline = many1 (pipe2 (word false) ilspaces <| fun x _ -> x)
and private parenArgs = _paren (wordsInBlock |>> ParseUnit.Nest)
and private explicitParenArgs = _explicitParen (wordsInBlock |>> ParseUnit.Nest)
and private macroPrmDecl = choice [
        attempt <| (Macro.Prm .>> spaces1 .>>. (wordsInBlock |>> ParseUnit.Nest) |>> MacroArg.Default)
        Macro.Prm |>> MacroArg.Req
    ]
and private mainParser = choice [
        pstring "///" .>> skipMany1 anyChar >>% End
        pstring LAMBDA_MACRO_PRM >>% LambdaMacroParam
        pstring MACRO_OL_OPEN >>. ilspaces >>. simpleString1 .>> ilspaces .>>. wordsInline .>> cnewln >>= (fun (n, words) ->
            let m = Macro.Create n [] <| Words words
            updateUserState (fun (state: State) -> { state with macros = state.macros.Add(n, m) }) >>% MacroDef n
        )
        betweenStr MACRO_OPEN MACRO_CLOSE (spaces >>. simpleString1 .>>. (_paren macroPrmDecl) .>> spaces
                                         .>>. wordsTopLevel) .>> cnewln >>= (fun ((n, prms), words) ->
            let m = Macro.Create n prms <| Words words
            updateUserState (fun state -> { state with macros = state.macros.Add(n, m) }) >>% MacroDef n)
        explicitParenArgs |>> Words
        //pchar REF >>. simpleString0 |>> fun x -> Words [ Atom REF_STR; Atom x ]
        //Property syntax: <!> value value value
        pstring PROP_MARKER >>. ilspaces >>. wordsInline |>> (fun words -> (Atom PROP_KW::words) |> Words)
        pstring PROP2_MARKER >>. ilspaces >>. wordsInline |>> (fun words -> (Atom PROP2_KW::words) |> Words)
        //Note: this requires priority so A%B%C gets parsed as one NoSpace block.
        //Because it has priority, it requires attempt so it doesn't break on a normal string like ABC.
        attempt <| sepByAll2 simpleString0 (betweenChars MACRO_VAR MACRO_VAR Macro.Prm) |>> compileMacro
        //Sequence of simple macro arg with no close: %A... <%A,%B:%C>. Note <%A%B> is caught above as '<' '%A' 'B>'.
        attempt <| sepByAll2 simpleString0 (pchar MACRO_VAR >>. Macro.Prm) |>> compileMacro
        simpleString1 |>> Atom
        betweenChars OPEN_PF CLOSE_PF wordsInBlock |>> Postfix 
        pstring MACRO_REINVOKE >>. simpleString1 .>>. parenArgs |>> MacroReinvocation
        pchar MACRO_INVOKE >>. simpleString1 .>>. (parenArgs <|>% []) >>= invokeMacroByName
        bounded QUOTE |>> Quote
    ]
and private mainParserNL = mainParser <|> (cnewln >>% Newline)
and private lmainParser = mainParser >>= locate
and private lmainParserNL = mainParserNL >>= locate

let private swapGeneric sorter matcher remap ls =
                List.foldBack (fun x acc ->
                    let sx = sorter x
                    if matcher sx
                    then (List.head acc)::(remap sx)::(List.tail acc)
                    else sx::acc) ls []
    
let rec private swapPostfix ls = swapGeneric sort
                                     (function  | Postfix _ -> true
                                                | _ -> false)
                                     (function  | Postfix ls -> Words ls
                                                | x -> x) ls
and private sort atom =
    match atom with
    | Words ls -> swapPostfix ls |> Words
    | Postfix ls -> swapPostfix ls |> Postfix
    | NoSpaceWords ls -> swapPostfix ls |> NoSpaceWords
    | _ -> atom
let private lsort (latoms:LocatedParseUnit list) =
    swapGeneric (LPUF.Map sort)
                (LPUF.IMap (function
                            | Postfix _ -> true
                            | _ -> false))
                (LPUF.Map (function
                            | Postfix ls -> Words ls
                            | x -> x)) latoms
    
let replacements = [ ("{{", [ "{"; "{" ]); ("}}", [ "}"; "}" ]) ] |> Map.ofList

let private cutoff = (function | End -> false | _ -> true)

let rec private flatten atom =
    match atom with
    | Atom s -> if String.length s > 0
                then match replacements.TryFind s with
                     | None -> OK [s]
                     | Some arr -> OK arr
                else OK []
    | Quote s -> OK [s]
    | Words ls -> ls |> List.takeWhile cutoff |> List.map flatten |> Errorable<_>.AccConcat
    | Postfix ls -> List.map flatten ls |> Errorable<_>.AccConcat
    | NoSpaceWords ls -> let srcList = (List.map flatten ls |> Errorable<_>.AccFmap (List.filter (fun x -> List.length x > 0)))
                         srcList.fmap(fun x ->
                            List.foldBack (fun x acc ->
                                let lm1 = List.length x - 1
                                let lst = x.[lm1] + (List.head acc)
                                List.append (List.take lm1 x) (lst::List.tail acc))
                                x [ "" ])   
    | Newline -> OK ["\n"]
    | MacroDef _ -> OK []
    | MacroVar s -> Failed [ sprintf "Found a macro variable \"%s%s\" in the output." "%" s ]
    | LambdaMacroParam -> Failed [ "Found an unbound macro argument (!$) in the output." ]
    | PartialMacroInvoke (m, args) -> Failed [ sprintf "The macro \"%s\" was invoked with %d arguments (%d required)"
                                                m.name (countFilter (function | LambdaMacroParam -> false | _ -> true)
                                                           args)  m.nprms
                                                ]
    | _ -> Failed [ "Illegal unit in output" ]

let internal _SMParser s =
                 match (runParserOnString (wordsTopLevel .>> eof |>> (Words))
                            { macros = Map.empty } "StateMachineLexer" s) with
                 | Success (result, state, pos) -> OK result
                 | Failure (errStr, err, state) -> Failed [errStr]
let internal _lSMParser s =
                 match (runParserOnString (lwordsTopLevel .>> eof)
                            { macros = Map.empty } "StateMachineLexer" s) with
                 | Success (result, state, pos) -> OK result
                 | Failure (errStr, err, state) -> Failed [errStr]
        
 
//Avoids a nesting error in il2cpp
let typed_partialZip (ls: (string list * Position) list) =
    let ls = List.map (fun (i, j) -> (i, firstSome j (List.length i))) ls
    ls |> List.map fst |> List.concat, ls |> List.map snd |> List.concat

          
let SMParser s = (_SMParser s).bind(sort >> flatten).fmap(Array.ofList)
let lSMParser s =( _lSMParser s).bind(List.takeWhile (LPUF.IMap cutoff) >> lsort >>
                           LPUF.Remap flatten >> List.map (Errorable<_>.DeTuple printPosition)
                           >> Errorable<_>.Acc).fmap(typed_partialZip)
let SMParser2 s = (_SMParser s).bind(sort >> flatten).fmap(String.concat " ")
let lSMParser2 = lSMParser >> (function
                                | OK (ss, pos) ->
                                    if List.length ss = List.length pos
                                    then String.concat " " ss |> OK
                                    else Failed ["FATAL: The lexer found a position/string mismatch. Please report this!"]
                                | Failed errs -> Failed errs
    )

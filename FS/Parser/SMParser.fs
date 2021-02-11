module FParser.SMParser
open FCommon.Functions
open FCommon.Types
open FCommon.Extensions
open FParsec
open System
open FParser.ParserCommon

//This function is copied from FParser internals. By removing the optimized-closure check,
//this avoids an error with il2cpp type depth.
let (>>==) (p: Parser<'a,'u>) (f: 'a -> Parser<'b,'u>) =
    fun stream ->
        let reply1 = p stream
        if reply1.Status = Ok then
            let p2 = f reply1.Result
            if isNull reply1.Error then
                p2 stream
            else
                let stateTag1 = stream.StateTag
                let mutable reply2 = p2 stream
                if stateTag1 = stream.StateTag then
                    reply2.Error <- mergeErrors reply2.Error reply1.Error
                reply2
        else
            Reply(reply1.Status, reply1.Error)


let COMMENT = '#'
let PROP_MARKER = "<!>"
let PROP2_MARKER = "<#>"
let PROP_KW = "!$_PROPERTY_$!"
let PROP2_KW = "!$_PARSER_PROPERTY_$!"
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
    | PartialMacroInvoke of (Macro * LParseUnit list) //$add(!$; 5)
    | MacroReinvocation of (string * LParseUnit list) //$%func(6)
    | Paren of LParseUnit list
    | Words of LParseUnit list
    | NoSpaceWords of LParseUnit list
    | Postfix of LParseUnit list
    | MacroDef of string
    | Newline
    | End
with
    static member Nest(ls: LParseUnit list) =
        if List.length ls = 1
        then List.head ls |> fst
        else Words ls
    member this.Reduce() =
        let rec reduce (ls: LParseUnit list) recons =
            if List.length ls = 1
            then (fst ls.[0]).Reduce()
            else recons ls
        match this with
        | Paren _ -> this
        | Words x -> reduce x Words
        | NoSpaceWords x -> reduce x NoSpaceWords
        | Postfix x -> reduce x Postfix
        | x -> x
    member this.CutTrailingNewline() =
        let cut ls =
            let lm1 = List.length ls - 1
            match fst ls.[lm1] with
            | Newline -> List.take lm1 ls
            | _ -> ls
        match this with
        | Paren x -> Paren <| cut x
        | Words x -> Words <| cut x
        | NoSpaceWords x -> NoSpaceWords <| cut x
        | Postfix x -> Postfix <| cut x
        | x -> x
and internal LParseUnit = (ParseUnit * Position)
and internal MacroArg =
    | Req of string
    | Default of (string * LParseUnit)
with
    static member AsString ma =
        match ma with
        | Req s -> s
        | Default(s,_) -> s
    static member AsDefault ma =
        match ma with
        | Req _ -> None
        | Default(_,d) -> Some d
and internal Macro = {
    name: string
    nprms: int
    prmIndMap: Map<string, int>
    prmDefaults : LParseUnit option list
    unformatted: LParseUnit
} with
    static member private ResolveUnit argResolve macroReinvResolve (x: LParseUnit) =
        let resolveAcc = List.map (Macro.ResolveUnit argResolve macroReinvResolve) >> Errorable<_>.Acc
        let reloc = function | OK y -> OK (y, snd x) | Failed err -> Failed err
        match fst x with
        | MacroVar s -> argResolve s
        | Paren ats -> (resolveAcc ats).fmap Paren |> reloc
        | Words ats -> (resolveAcc ats).fmap Words |> reloc
        | NoSpaceWords ats -> (resolveAcc ats).fmap NoSpaceWords |> reloc
        | Postfix ats -> (resolveAcc ats).fmap Postfix |> reloc
        | MacroReinvocation (s, args) -> macroReinvResolve(s, args)
        | _ -> OK x
    member private this.RealizeOverUnit (args: LParseUnit list) unformatted =
        unformatted |> Macro.ResolveUnit (fun s ->
            match this.prmIndMap.TryFind(s) with
            | None -> Failed [sprintf "Macro body has nonexistent variable \"%s%s\"" "%" s]
            | Some i -> args.[i] |> OK) (fun (s, rargs) ->
            match this.prmIndMap.TryFind(s) with
            | None -> Failed [sprintf "Macro body has nonexistent reinvocation  \"%s%s\"" "$%" s]
            | Some i -> match (fst args.[i]).Reduce() with
                        | PartialMacroInvoke (m, pargs) ->
                            let isLambda = fst >> (function | LambdaMacroParam -> true | _ -> false)
                            match replaceEntries true pargs rargs isLambda with
                            | Enough x -> (Errorable<_>.Acc (List.map (this.RealizeOverUnit args) x)).bind(m.Invoke)
                            | _ -> Failed [ sprintf "Macro \"%s\" provides too many arguments to partial macro \"%s\". (%d provided, %d required)"
                                                this.name m.name (List.length rargs) (countFilter isLambda pargs) ]
                        | other -> Failed [sprintf "Macro argument \"%s.%s%s\" (arg #%d) must be a partial macro invocation.
                                           This may occur if you already provided all necessary arguments. %O" this.name "%" s i other])
    member internal this.Realize (args: LParseUnit list) =
        if this.nprms = 0 then OK this.unformatted
        else this.RealizeOverUnit args this.unformatted
    member internal this.Invoke (args: LParseUnit list) =
        if (args.Length <> this.nprms)
            then
                let defaults = this.prmDefaults |> List.SoftSkip(args.Length) |> List.FilterNone
                if (args.Length + defaults.Length <> this.nprms)
                    then Failed [sprintf "Macro \"%s\" requires %d arguments (%d provided)" this.name this.nprms args.Length]
                    else this.Realize (args@defaults)
            else if args |> List.exists (fst >> function | LambdaMacroParam -> true | _ -> false)
                 then ((this, args) |> PartialMacroInvoke, snd args.[0]) |> OK
                 else this.Realize args
    static member internal PrmChar = letter <|> digit <|> pchar '_'
    static member internal Prm = Macro.PrmChar |> many1Chars
    static member internal Create name prms (unformatted: LParseUnit) = {
        name = name
        nprms = List.length prms
        prmIndMap = List.mapi (fun i x -> (MacroArg.AsString x, i)) prms |> Map.ofList
        prmDefaults = List.map (MacroArg.AsDefault) prms 
        unformatted = ((fst unformatted).CutTrailingNewline(), snd unformatted)
    }
and internal State = {
   macros: Map<string, Macro>
}

let private locate pu =
    getPosition |>> (fun x -> (pu, x))


let private simpleString take = isLetter |> take
let private simpleString1 = simpleString many1Satisfy
let private simpleString0 = simpleString manySatisfy
let private sepByAll2 p sep = (p .>>. sep .>>. p .>>. many (sep .>>. p)) |>>
                                (fun (((h0, h1), h2), hl) ->
                                h0::h1::h2::List.foldBack (fun x acc -> (fst x)::(snd x)::acc) hl []          
                            )

let private compileMacro(ls, loc) = let l = ls
                                           |> List.mapi (fun i x ->
                                               if String.length x = 0 then None
                                               else if i % 2 = 0
                                               then (Atom x, loc) |> Some
                                               else (MacroVar x, loc) |> Some)
                                           |> List.FilterNone
                                    if List.length l = 1 then l.[0]
                                    else (NoSpaceWords l, loc)
                                
let private invokeMacroByName (n, (args: LParseUnit list)) =
    getUserState >>= (fun state ->
        match state.macros.TryFind(n) with
        | None -> fail <| sprintf "No macro exists with name %s." n
        | Some m -> m.Invoke(args) |> failErrorable)
                        
let private cnewln = pchar COMMENT .>> skipManySatisfy (fun x -> x <> '\n') |> optional >>. newline
let rec private word allowNewline x =
    if allowNewline
    then mainParserNL x
    else mainParser x
and private wordsTopLevel = many1 (pipe2 (word true) ilspaces <| fun x _ -> x)
and private wordsInBlock = many1 (pipe2 (word true) ilspaces <| fun x _ -> x)
and private wordsInline = many1 (pipe2 (word false) ilspaces <| fun x _ -> x)
and private parenArgs = _paren (wordsInBlock |>> ParseUnit.Nest >>= locate)
and private macroPrmDecl = choice [
        attempt <| (Macro.Prm .>> spaces1 .>>. (wordsInBlock |>> ParseUnit.Nest) >>= locate
                    |>> (fun ((title, words), loc) -> MacroArg.Default (title, (words, loc))))
        Macro.Prm |>> MacroArg.Req
    ]
and private mainParser = choice [
        pstring "///" .>> skipMany1 anyChar >>% End >>= locate
        pstring LAMBDA_MACRO_PRM >>% LambdaMacroParam >>= locate
        pstring MACRO_OL_OPEN >>. ilspaces >>. simpleString1 .>> ilspaces .>>. wordsInline .>> cnewln >>== locate >>= (fun ((n, words), loc) ->
            let m = Macro.Create n [] <| (Words words, loc)
            updateUserState (fun (state: State) -> { state with macros = state.macros.Add(n, m) }) >>% (MacroDef n, loc)
        )
        betweenStr MACRO_OPEN MACRO_CLOSE (spaces >>. simpleString1 .>>. (_paren macroPrmDecl) .>> spaces
                                         .>>. wordsTopLevel) .>> cnewln >>== locate >>= (fun (((n, prms), words), loc) ->
            let m = Macro.Create n prms <| (Words words, loc)
            updateUserState (fun state -> { state with macros = state.macros.Add(n, m) }) >>% (MacroDef n, loc))
        parenArgs |>> Paren >>= locate
        //pchar REF >>. simpleString0 |>> fun x -> Words [ Atom REF_STR; Atom x ]
        //Property syntax: <!> value value value
        pstring PROP_MARKER >>. ilspaces >>. wordsInline >>= locate |>> (fun (words, loc) -> ((Atom PROP_KW, loc)::words) |> Words, loc)
        pstring PROP2_MARKER >>. ilspaces >>. wordsInline >>= locate |>> (fun (words, loc) -> ((Atom PROP2_KW, loc)::words) |> Words, loc)
        //Note: this requires priority so A%B%C gets parsed as one NoSpace block.
        //Because it has priority, it requires attempt so it doesn't break on a normal string like ABC.
        attempt <| sepByAll2 simpleString0 (betweenChars MACRO_VAR MACRO_VAR Macro.Prm) >>= locate |>> compileMacro
        //Sequence of simple macro arg with no close: %A... <%A,%B:%C>. Note <%A%B> is caught above as '<' '%A' 'B>'.
        attempt <| sepByAll2 simpleString0 (pchar MACRO_VAR >>. Macro.Prm) >>= locate |>> compileMacro
        simpleString1 |>> Atom >>= locate
        betweenChars OPEN_PF CLOSE_PF wordsInBlock |>> Postfix >>= locate
        pstring MACRO_REINVOKE >>. simpleString1 .>>. parenArgs |>> MacroReinvocation >>= locate
        pchar MACRO_INVOKE >>. simpleString1 .>>. (parenArgs <|>% []) >>= invokeMacroByName
        bounded QUOTE |>> Quote >>= locate
    ]
and private mainParserNL = mainParser <|> (cnewln >>% Newline >>= locate)

let private swapGeneric sorter matcher remap ls =
                List.foldBack (fun x acc ->
                    let sx = sorter x
                    if matcher sx
                    then (List.head acc)::(remap sx)::(List.tail acc)
                    else sx::acc) ls []
    
let rec private swapPostfix ls = swapGeneric sort
                                     (fst >> (function  | Postfix _ -> true
                                                        | _ -> false))
                                     (MapFst (function  | Postfix ls -> Words ls
                                                        | x -> x)) ls
and private sort atom :LParseUnit =
    atom |>
    MapFst (function
    | Paren ls -> swapPostfix ls |> Paren
    | Words ls -> swapPostfix ls |> Words
    | Postfix ls -> swapPostfix ls |> Postfix
    | NoSpaceWords ls -> swapPostfix ls |> NoSpaceWords
    | x -> x)
    
let replacements = [ ("{{", [ "{"; "{" ]); ("}}", [ "}"; "}" ]) ] |> Map.ofList

let private cutoff = (function | End -> false | _ -> true)

let rec private flatten atom =
    match fst atom with
    | Atom s -> if String.length s > 0
                then match replacements.TryFind s with
                     | None -> OK [s]
                     | Some arr -> OK arr
                else OK []
    | Quote s -> OK [s]
    | Paren ls -> ls |> List.map flatten |> Errorable<_>.AccFmap (fun x -> List.concat [
        ["("]
        separateBy [","] x |> List.concat
        [")"]
    ])
    | Words ls -> ls |> List.takeWhile (fst >> cutoff) |> List.map flatten |> Errorable<_>.AccConcat
    | Postfix ls -> List.map flatten ls |> Errorable<_>.AccConcat
    | NoSpaceWords ls -> let srcList = (List.map flatten ls |> Errorable<_>.AccFmap (List.filter (fun x -> List.length x > 0)))
                         srcList.fmap(fun x ->
                            List.foldBack (fun x acc ->
                                let lm1 = List.length x - 1
                                //[a, b, c] [d, e, f]
                                // -> [a, b, cd, e, f]
                                let lst = x.[lm1] + (List.head acc)
                                List.append (List.take lm1 x) (lst::List.tail acc))
                                x [ "" ])   
    | Newline -> OK ["\n"]
    | MacroDef _ -> OK []
    | MacroVar s -> Failed [ sprintf "Found a macro variable \"%s%s\" in the output." "%" s ]
    | LambdaMacroParam -> Failed [ "Found an unbound macro argument (!$) in the output." ]
    | PartialMacroInvoke (m, args) -> Failed [ sprintf "The macro \"%s\" was invoked with %d arguments (%d required)"
                                                m.name (countFilter (fst >> function | LambdaMacroParam -> false | _ -> true)
                                                           args)  m.nprms
                                                ]
    | _ -> Failed [ "Illegal unit in output" ]

let internal _SMParser s =
                 match (runParserOnString (wordsTopLevel .>> eof |>> Words >>= locate)
                            { macros = Map.empty } "StateMachineLexer" s) with
                 | Success (result, _, _) -> OK result
                 | Failure (errStr, _, _) -> Failed [errStr]
          
let SMParser s = (_SMParser s).bind(sort >> flatten).fmap(Array.ofList)
let remakeSMParser s = SMParser s |> Errorable<_>.Fmap (String.concat " ")

type ParsedUnit =
    | S of string
    | P of struct(ParsedUnit * Position)[][]

let rec private flatten2 (atom, loc) : Errorable<struct(ParsedUnit * Position) list> =
    match atom with
    | Atom s -> if String.length s > 0
                then match replacements.TryFind s with
                     | None -> OK [(S s, loc)]
                     | Some arr -> List.map (fun x -> struct(S x, loc)) arr |> OK 
                else OK []
    | Quote s -> OK [(S s, loc)]
    | Paren ls -> ls
                  |> List.map (flatten2 >> Errorable<_>.Fmap Array.ofList)
                  |> Errorable<_>.Acc
                  |> Errorable<_>.Fmap (fun x -> [struct(Array.ofList x |> P, loc)])
    | Words ls -> ls |> List.takeWhile (fst >> cutoff) |> List.map flatten2 |> Errorable<_>.AccConcat
    | Postfix ls -> List.map flatten2 ls |> Errorable<_>.AccConcat
    | NoSpaceWords ls -> List.map flatten ls |> Errorable<_>.AccFmap (fun x ->
                                                      [(x |> List.concat
                                                          |> (String.concat "")
                                                          |> S, loc)])
    | Newline -> OK [(S "\n", loc)]
    | MacroDef _ -> OK []
    | MacroVar s -> Failed [ sprintf "Found a macro variable \"%s%s\" in the output." "%" s ]
    | LambdaMacroParam -> Failed [ "Found an unbound macro argument (!$) in the output." ]
    | PartialMacroInvoke (m, args) -> Failed [ sprintf "The macro \"%s\" was invoked with %d arguments (%d required)"
                                                m.name (countFilter (fst >> function | LambdaMacroParam -> false | _ -> true)
                                                           args)  m.nprms
                                                ]
    | _ -> Failed [ "Illegal unit in output" ]
    
let SMParser2 s = (_SMParser s).bind(sort >> flatten2)

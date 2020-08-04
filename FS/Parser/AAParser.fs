#if INTERACTIVE
#I "C:\Users\Bagoum\.nuget\packages\\fparsec\1.1.1\lib\\net45"
#r "FParsecCS.dll"
#r "FParsec.dll"
#else
module FParser.AAParser
open Common.Types
open Common.Functions
open Common.Extensions
open FParser.ParserCommon
#endif
open FParsec
open System

[<Measure>] type ops
[<Measure>] type s
[<Measure>] type f = ops/s

//Certain punctuation types are marked with Punct units
//IN ADDITION TO the normal atom that contains their value.
//These units are markers for functions.
type Punct =
    | PSpace
    | PComma
    | PPeriod
    | PSemicolon
    | PEllipsis
    with
    member this.Resolve(space, comma, period, semicolon, ellipsis) =
        match this with
        | PSpace -> space
        | PComma -> comma
        | PPeriod -> period
        | PSemicolon -> semicolon
        | PEllipsis -> ellipsis

type FancyText = | Text of string

type WikiEntry = {
    name: string
    color: string
    tooltip: FancyText
} with static member Default = {
        name = "!DEFAULT!"
        color = "!DEFAULT!"
        tooltip = Text "!DEFAULT!"
    }
    
type ArgRef<'t,'u,'v> =
    | Ref1 of 't
    | Ref2 of 'u
    | Ref3 of 'v
    with
    member this.Resolve(f1:Action<'t>, f2:Action<'u>, f3:Action<'v>) =
        match this with
        | Ref1 x -> f1.Invoke(x)
        | Ref2 y -> f2.Invoke(y)
        | Ref3 z -> f3.Invoke(z)
type TextUnit<'t,'u,'v> =
    | Tag of string
    | Atom of string
       //Spaces can be reduced in UI display;
       //multiple spaces are considered as one logical unit here.
    | Newline
    | Punct of (string * Punct)
    | Ref of ArgRef<'t,'u,'v>

type TextCommand<'t,'u,'v> =
    | TTextWait of (string * float<s>)
    | TSpace of string
    | TNewline
    | OpenTag of string
    | CloseTag
    | Wait of float<s>
    | TRef of ArgRef<'t,'u,'v>
    with
    member this.Resolve(ftext:Action<string, float<s>>, fspace:Action<string>, fnl:Action,
                        fopen:Action<string>, fclose:Action, fwait:Action<float<s>>,
                        fr1: Action<'t>, fr2: Action<'u>, fr3: Action<'v>) =
        match this with
        | TTextWait (s,f) -> ftext.Invoke(s, f)
        | TSpace x -> fspace.Invoke(x)
        | TNewline -> fnl.Invoke()
        | OpenTag x -> fopen.Invoke(x)
        | CloseTag -> fclose.Invoke()
        | Wait x -> fwait.Invoke(x)
        | TRef rf -> rf.Resolve(fr1, fr2, fr3)

type private ExportAcc<'t,'u,'v> = {
    //hangChars: int
    cmds: TextCommand<'t,'u,'v> list list
} with static member Default : ExportAcc<'t,'u,'v> = {
        //hangChars = 0
        cmds = []
    }
type Config<'t,'u,'v> = {
    speed: float<f>
    charsPerBlock: int
    blockOps: float<ops>
    //Punct units are mapped to longer pauses as well as events (such as SFX).
    punctOps: Punct -> float<ops>
    blockEvent: ArgRef<'t,'u,'v> option
    punctEvent: Punct -> ArgRef<'t,'u,'v> option
} with
    member this.BlockWait: float<s> = this.blockOps / this.speed
    static member Default: Config<'t,'u,'v> = {
        speed = 10.0<f>
        charsPerBlock = 3
        blockOps = 1.0<ops>
        punctOps = function
                    | PSpace -> 2.0<ops>
                    | PComma -> 4.0<ops>
                    | PPeriod -> 5.0<ops>
                    | PSemicolon -> 6.0<ops>
                    | PEllipsis -> 8.0<ops>
        blockEvent = None
        punctEvent = fun _ -> None
    }
    
let FANCY_TAG = "$/"
let WIKI_INVOKE = "$$"
let ARGWIKI_INVOKE = '$'
let TAG_OPEN = '<'
let TAG_CLOSE = '>'
let TAG_OPEN_S = "<"
let TAG_CLOSE_S =  ">"
let private PUNCTUATION = [
    (',', PComma)
    ('.', PPeriod)
    ('!', PPeriod)
    ('?', PPeriod)
    (';', PSemicolon)
    (':', PSemicolon)
]
let private PUNCT_MAP = PUNCTUATION |> Map.ofList
let private TryResolvePunctuation dflt p = match PUNCT_MAP.TryFind p with
                                            | Some x -> x
                                            | None -> dflt
let private PUNCTUATION_CHARS = List.map fst PUNCTUATION
let private punctuation c = List.contains c PUNCTUATION_CHARS
    
let private inTag c =
    c <> TAG_OPEN
    && c <> TAG_CLOSE
//TODO escaping with \
let private normalLetter c =
    c <> ARGWIKI_INVOKE
    && not <| punctuation c
    && not <| Char.IsWhiteSpace c
    && inTag c

let private normalWord = many1Satisfy normalLetter
let private invokeWord =
    let invokePunctuation c =
        c = '.' || c = '_'
    choice [ letter;
              digit;
              satisfy invokePunctuation
    ] |> many1Chars
    
let private tryFindReference wikiName wikiData wikiKey onSuccess =
    match wikiData wikiKey with
    | Some v -> onSuccess v |> preturn
    | None -> sprintf "No reference was found for %s.%s." wikiName wikiKey |> fail
    
let CreateParser3<'t,'u,'v>
        argn1 (argwiki1:string ->'t option)
        argn2 (argwiki2:string ->'u option)
        argn3 (argwiki3:string ->'v option)
        (wiki:Map<string, string -> WikiEntry option>) =
    let validRefs = [ argn1; argn2; argn3 ] |> List.filter (fun x -> x <> "$") |> String.concat ", "
    let validWikis = wiki |> Map.toList |> List.map fst |> String.concat ", "
    let parser = many1 <| choice [
        pchar TAG_OPEN >>. manySatisfy inTag .>> pchar TAG_CLOSE |>> fun s ->
            String.concat "" [TAG_OPEN_S; s; string TAG_CLOSE_S] |> Tag |> List.singleton
        normalWord |>> (Atom >> List.singleton)
        pstring WIKI_INVOKE >>. invokeWord .>>. _paren1 invokeWord >>=
            (fun (key,key2) -> match Map.tryFind key wiki with
                                | None -> sprintf "Wiki type %s does not exist (%s)." key validWikis |> fail
                                | Some iwiki -> tryFindReference key iwiki key2 (fun x -> [ Atom x.name ])
                )
        pchar ARGWIKI_INVOKE >>. invokeWord .>>. _paren1 invokeWord
            >>= function
                | x,y when x = argn1 -> tryFindReference x argwiki1 y (Ref1 >> Ref >> List.singleton)
                | x,y when x = argn2 -> tryFindReference x argwiki2 y (Ref2 >> Ref >> List.singleton)
                | x,y when x = argn3 -> tryFindReference x argwiki3 y (Ref3 >> Ref >> List.singleton)
                | x,_ -> fail <| sprintf "%s is not a valid reference-getter (%s)." x validRefs
        many1Satisfy whiteInline |>> (fun x -> [ Punct (x, PSpace) ])
        newline >>% [ Newline ] //No comments within text.
        satisfy punctuation |> many1 |>> (fun ps ->
            if maxConsecutive '.' ps > 1 then List.map (fun x -> Punct (string x, PEllipsis)) ps
            else ps |> List.map (fun x -> Punct (string x, TryResolvePunctuation PComma x)))
    ]
    runParserOnString (parser .>> eof) () "AAParser" >>
        function
        | Success (result, _, _) -> result |> List.concat |> OK
        | Failure (errStr, _, _) -> Failed [errStr]

let CreateParser2<'t,'u> = CreateParser3<unit,'t,'u> "$" (fun _ -> None)
let CreateParser1<'t> = CreateParser2<unit,'t> "$" (fun _ -> None)

let private ExportToCommands<'t,'u,'v> (cfg:Config<'t,'u,'v>) (tus:TextUnit<'t,'u,'v> list) =
    let rec exportToCommand acc tu :ExportAcc<'t,'u,'v> =
        match tu with
        //All non-space text is counted for blockOps. Note that punctuation text is
        //reduced through the Punct interface and is not counted here.
        | Tag st -> { acc with cmds = [ TTextWait (st, 0.0<s>) ]::acc.cmds }
        | Atom st ->
                    let (cstr, strs, hchars) =
                        List.fold (fun (c,s,h) x ->
                            let h = h + 1
                            let c = x::c
                            if h = cfg.charsPerBlock
                            then ([], [TTextWait (revToString c, cfg.BlockWait)]::s, 0)
                            else match (cfg.blockEvent, h) with
                                    | Some be, 1 -> (c, [TRef be]::s, h)
                                    | _, _ -> (c, s, h)
                        ) ([], [], 0) (Seq.toList st)
                    { acc with cmds = ((if List.length cstr > 0 then
                                         [TTextWait (revToString cstr, (float)hchars / (float)cfg.charsPerBlock * cfg.BlockWait) ]
                                         else [])::strs, acc.cmds
                                      ) ||> List.append }
        | _ ->  let nextCmds =
                    match tu with
                    | Tag st -> [ TTextWait (st, 0.0<s>) ]
                    | Atom st -> [ TTextWait (st, 0.0<s>) ]
                   //Spaces can be reduced in UI display;
                   //multiple spaces are considered as one logical unit here.
                    | Newline -> [ TNewline ]
                    //Punct objects contain a punctuation text and a marker for insertion of events.
                    | Punct (txt, p)  -> 
                        let ops = cfg.punctOps p
                        let pwait = if (ops > 0.0<ops>) then [Wait (ops / cfg.speed)] else []
                        let text = if (String.IsNullOrWhiteSpace txt) then TSpace txt else TTextWait (txt, 0.0<s>)
                        text::(List.append (cfg.punctEvent p |> asList |> List.map TRef) pwait)
                    | Ref x -> [ TRef x ]
                { acc with cmds = nextCmds::acc.cmds }
    (List.fold exportToCommand ExportAcc.Default tus).cmds |> List.rev |> List.concat
   
let ParseAndExport cfg parser = parser >> Errorable<_>.Fmap (ExportToCommands <| cfg)

let toMatch kvs =
    let kvm = kvs |> Map.ofList
    kvm.TryFind
let parseme = CreateParser1<int> "sfx" ([("abc", 5)] |> toMatch) ([ ("npc", [("blue", WikiEntry.Default)] |> toMatch) ] |> Map.ofList)
parseme "h $$npc(blu2e) h"
let exportme = parseme |> ParseAndExport (Config.Default)


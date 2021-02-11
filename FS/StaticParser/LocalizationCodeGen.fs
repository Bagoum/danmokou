module StaticParser.LocalizationCodeGen
open System
open System.IO
open FCommon.Types
open FCommon.Functions
open FSharp.Data
open StaticParser.LocalizationParser

type LRenderFragment =
    | Newline
    | Indent //Place at the end of the previous line, before the newline
    | Dedent
    | Word of string

let render fragments indent =
    let rec inner fragments indent =
        match fragments with
        | [] -> []
        | f::fs ->
            match f with
            | Newline -> "\n"::(List.replicate indent "\t" |> String.concat "")::(inner fs indent)
            | Indent -> inner fs (indent + 1)
            | Dedent -> inner fs (indent - 1)
            | Word s -> s::(inner fs indent)
    (inner fragments indent)
    |> String.concat ""
    

type Localizable = CsvProvider<"C://Workspace/unity/Danmokou/FS/StaticParser/CSV/StructureGameStrings.csv">

type Row = {
    key: string
    en: string
    jp: string
} with
    static member FromCSV (row: Localizable.Row) =
        {
            key = row.Key
            en = row.EN
            jp = row.JP
        }
let toLocales (row: Row) =
    [ row.en; row.jp ]
    
let argString i = sprintf "arg%d" i

type LGenCtx = {
    localeSwitch: string
    objectType: string
    locales: string list //locale keys for enum
    lslocales: string list //locale keys for LocalizedString creation
    lsclass: string option //if present, zero-arg functions will be saved as LocalizedString instead (preferred)
    methodToLsSuffix : string option //if this and lsclass are present, multi-arg functions will have a second form
                                    // which returns a LocalizedString. This requires resolving all languages,
                                    // but is useful for UI cases where the language can change on the screen.
    errors: string list
    renderFunc: string
    funcStandardizer: string -> string
    topFileName: string
    className: string
    nestedClassName: string
    namespace_: string
    outputHeader: string
    lsGenerated: (string * string) list
} with
    member this.AddError str = { this with errors = str::this.errors }
    member this.AddErrors strs = { this with errors = List.append this.errors strs }
    member this.CSharpify pu =
        match pu with 
        | String s -> sprintf "\"%s\"" s
        | StandardFormat s -> sprintf "\"{%s}\"" s
        | Argument i -> argString i
        | ConjFormat c ->
            c.args
            |> List.map this.CSharpify
            |> String.concat ", "
            |> sprintf "%s(%s)" (this.funcStandardizer c.func)
    
    member this.CSharpify pul =
        pul
        |> List.map this.CSharpify
        |> String.concat ""

let generateArgs nargs =
    [0..(nargs - 1)]
    |> List.map argString
    |> String.concat ", "
let generateParams (ctx:LGenCtx) nargs =
    [0..(nargs - 1)]
    |> List.map (fun i -> sprintf "%s %s" ctx.objectType (argString i))
    |> String.concat ", "


let generateCaseBody locale cset nargs (ctx: LGenCtx) (localized: (ParseSequence * State)) =
    let seq, _ = localized
    if seq.Length = 1 && (nargs <= 0 || match seq.[0] with | String _ -> true | _ -> false)
    then
        let word = ctx.CSharpify seq.[0]
        updateCharset cset word, ctx, [
             Word word
        ]
    else
        let cset, pieces = List.foldBack (fun (p: ParseUnit) (cset, acc) ->
                        let word = ctx.CSharpify p
                        updateCharset cset word, [
                            Newline
                            Word word
                            Word ","
                        ]::acc) seq (cset, List.empty)
        cset, ctx, List.concat [
            [
                sprintf "%s(%s, new[] {" ctx.renderFunc locale |> Word
                Indent
            ]
            List.concat pieces
            [
                Dedent
                Newline
                if nargs > 0 then Word "}, " else Word "}"
                generateArgs nargs |> Word
                Word(")")
            ]
        ]

let generateSwitchCaseOrDefault locale cset nargs (ctx: LGenCtx) caseString localized =
    let cset, ctx, body = generateCaseBody locale cset nargs ctx localized
    cset, ctx, List.concat [
        [
            Newline
            Word caseString
            Word " => "
        ]
        body
        [
            Word ","
        ]
    ]
    
    
    
let generateSwitchCase locale cset nargs (ctx: LGenCtx) lang localized =
    generateSwitchCaseOrDefault locale cset nargs ctx lang localized
    
let generateSwitchDefault locale cset nargs (ctx: LGenCtx) localized =
    generateSwitchCaseOrDefault locale cset nargs ctx "_" localized
   
let generateSwitch nargs ctx (localizeds: ((ParseSequence * State) * string * char Set) list) =
    let default_localized::localizeds = localizeds
    let (dflt_parse, _, dflt_cset) = default_localized
    let dflt_cset, ctx, default_case = generateSwitchDefault ctx.localeSwitch dflt_cset nargs ctx dflt_parse
    let csets, ctx, cases =
        localizeds
        |> List.fold (fun (csets, ctx, acc) (localized, lang, cset) ->
            let cset, ctx, case = generateSwitchCase ctx.localeSwitch cset nargs ctx lang localized
            cset::csets, ctx, case::acc
            ) ([], ctx, [])
    dflt_cset::csets, ctx, List.concat [
        [
            sprintf "%s switch {" ctx.localeSwitch |> Word
            Indent
        ]
        (default_case::cases) |> List.rev |> List.concat
        [
            Dedent
            Newline
            Word "}"
        ]
    ]

let generateLS (cls:string) nargs ctx (localizeds: ((ParseSequence * State) * (string * string) * char Set) list) =
    let default_localized::localizeds = localizeds
    let (dflt_parse, (dflt_locale, dflt_lslocale), dflt_cset) = default_localized
    let dflt_cset, ctx, default_case = generateCaseBody dflt_locale dflt_cset nargs ctx dflt_parse
    let csets, ctx, cases =
        localizeds
        |> List.rev
        |> List.fold (fun (csets, ctx, acc) (localized, (locale, lslocale), cset) ->
            let cset, ctx, case = generateCaseBody locale cset nargs ctx localized
            cset::csets, ctx, (lslocale, case)::acc
            ) ([], ctx, [])
    dflt_cset::csets, ctx, List.concat [
        [
            Word $"new {cls}("
        ]
        default_case
        [
            Word ") {"
            Indent
        ]
        cases
        |> List.collect (fun (loc, strs) -> List.concat [
                [
                    Newline
                    Word loc
                    Word " = "
                ]
                strs
                [
                    Word ","
                ]
            ]);
       [
           Dedent
           Newline
           Word "}"
       ]
    ]
    
        

let generateRow (csets: char Set list) (ctx: LGenCtx) (row: Row) =
    let err_localizeds =
        row
        |> toLocales
        |> List.map stringParser
    if List.length err_localizeds <> List.length ctx.locales
        then failwith "Incorrect number of locales provided"
    let remix_csets, ctx, localizeds =
        List.foldBack(fun x (rcsets, ctx: LGenCtx, localizeds) ->
                    match x with
                    //If the parse is empty, then skip the language for this row
                    | OK parsed, lang, cset -> if List.length (fst parsed) = 0
                                                then (Some cset::rcsets, ctx, localizeds)
                                                else (None::rcsets, ctx, (parsed, lang, cset)::localizeds)
                    | Failed msg, _, cset -> (Some cset::rcsets, ctx.AddErrors msg, localizeds)
        ) (List.zip3 err_localizeds (List.zip ctx.locales ctx.lslocales) csets) ([], ctx, [])
    //No strings, don't generate an entry
    if List.length localizeds = 0 then csets, ctx, []
    else
    let nargs =
        (localizeds
        |> Seq.map (fun ((_, state: State), _, _) -> state.highestArg)
        |> Seq.max) + 1
    if nargs > 0 && List.length localizeds <> List.length ctx.locales
    then Console.WriteLine $"Row {row.key} uses function strings, but does not provide translations for one or more languages."
    let objName = row.key.Replace('.', '_')
    match nargs, ctx.lsclass with
    //Zero-arg: generate a LocalizedString on the backend, with no suffix (?).
    | 0, Some cls ->
        let csets, ctx, ls =
            localizeds
            |> generateLS cls nargs ctx
        mixBack remix_csets csets, { ctx with lsGenerated = (row.key, objName)::ctx.lsGenerated }, List.concat [
            [
                Newline
                Word $"public static readonly {cls} {objName} = "
            ]
            ls
            [
                Word ";"
                Newline
            ]
        ]
    | _, _ ->
        let csets, ctx, switch =
            localizeds
            |> List.map (fun (ps, (l, _), cset) -> (ps, l, cset))
            |> generateSwitch nargs ctx
        if nargs = 0 then
            //Zero-arg, no method-to-LS: generate a string on the backend.
            mixBack remix_csets csets, ctx, List.concat [
                [
                    Newline
                    Word $"public static string {objName} => "
                ]
                switch
                [
                    Word ";"
                    Newline
                ]
            ]
        else
            //Multi-arg: generate a string function, and if method-to-LS is present, a LocalizedString function with suffix.
            let prms = generateParams ctx nargs
            let ls_copy = //cset/ctx output is not important
                match ctx.lsclass, ctx.methodToLsSuffix with
                | Some cls, Some suffix -> localizeds
                                           |> generateLS cls nargs ctx 
                                           |> (fun (csets, ctx, ls) ->
                                               List.concat [
                                                   [
                                                       Newline
                                                       Word $"public static {cls} {objName}{suffix}({prms}) => "
                                                   ]
                                                   ls
                                                   [
                                                       Word ";"
                                                       Newline
                                                   ]
                                               ])
                | _, _ -> []
            mixBack remix_csets csets, ctx, List.concat [
                [
                    Newline
                    Word $"public static string {objName}({prms}) => "
                ]
                switch
                [
                    Word ";"
                    Newline
                ]
                ls_copy
            ]
 
let generateRows csets (ctx: LGenCtx) rows =
    let csets, ctx, genRows =
        rows
        |> Seq.filter (fun (row: Row) -> System.String.IsNullOrWhiteSpace(row.key) |> not)
        |> Seq.fold (fun (csets, ctx, acc) row ->
                let csets, ctx, genRow = generateRow csets ctx row
                (csets, ctx, genRow::acc)
        ) (csets, ctx, [])
    csets, ctx, genRows |> List.rev |> List.concat
    
let generateClass ctx inner =
    List.concat [
        [
            Word ctx.outputHeader
            Newline
            Newline
            sprintf "namespace %s {" ctx.namespace_ |> Word
            Newline
            sprintf "public static partial class %s {" ctx.className |> Word
            Indent
            Newline
        ]
        inner
        [
            Dedent
            Newline
            Word "}"
            Newline
            Word "}"
            Newline
        ]
    ]

let generateNestedClass ctx inner =
    generateClass ctx (List.concat [
        [
            sprintf "public static partial class %s {" ctx.nestedClassName |> Word
            Indent
            Newline
        ]
        inner
        [
            Dedent
            Newline
            Word "}"
        ]
        
    ])

let generateCSV csets ctx (path: string) =
    let csets, ctx, genRows =
        (Localizable.Load path).Rows
        |> Seq.map Row.FromCSV
        |> generateRows csets ctx
    csets, ctx, generateNestedClass ctx genRows

let exportCSV csets ctx path out =
    let csets, ctx, gen = generateCSV csets ctx path
    File.WriteAllText(out, (render gen 0))
    csets, ctx
   
let exportFile csets ctx (path: string) outdir =
    let parts = Path.GetFileName(path).Split(".")
    parts
    |> Array.take (parts.Length - 1)
    |> String.concat "."
    |> sprintf "%s.cs"
    |> fun x -> Path.Join(outdir, x)
    |> exportCSV csets ctx path
  

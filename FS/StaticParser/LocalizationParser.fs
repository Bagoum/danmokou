module StaticParser.LocalizationParser
open FCommon.Types
open FParser.ParserCommon
open FParsec
open System
#nowarn "40"

type State = {
    highestArg: int
}
type ParseUnit =
    | String of string //Normal/escaped
    | StandardFormat of string //eg. `0:F1`. The brackets are stripped.
    | Argument of int //eg. the 1 in `PLURAL(1, ...)`. Becomes `arg1` in the output.
    | ConjFormat of InvokeUnit //eg. `PLURAL(1, coin, coins)`. The brackets are stripped.
                               //The arguments are parsed each as ParseSequence.

    
and ParseSequence = ParseUnit list
and InvokeUnit = {
    func: string
    args: ParseSequence list
}
let QUOTE = '\"'
let ESCAPER = '\\'
let FMT_OPEN = '{'
let FMT_CLOSE = '}'
let INVOKE_START = '$'
let escapeChars = "\"\\nt{}"
let private trivialString =
    let trivialPunctuation c =
        c = '.' || c = '_'
    choice [ letter;
              digit;
              satisfy trivialPunctuation
    ] |> many1Chars

let internal escape = (anyOf escapeChars |>> function
    | 'n' -> "\\n"
    | 't' -> "\\t"
    | '{' -> "{{" //This is how brackets are escaped in C# string formatting
    | '}' -> "}}"
    | '"' -> "\\\""
    | c -> string c)
    
let internal escapedFragment = pchar ESCAPER >>. escape |>> ParseUnit.String
let internal normalFragment allowUnescapedQuote =
    many1Satisfy (fun c ->
        c <> ESCAPER && (c <> QUOTE || allowUnescapedQuote)
        && c <> FMT_OPEN && c <> FMT_CLOSE) |>>
            // quotes alone need to be escaped
            (fun s -> s.Replace("\"", "\\\"") |> ParseUnit.String)
        

let private updateStateArg myArg state =
    {state with highestArg = max state.highestArg myArg}
let rec internal formatFragment =
    (betweenChars FMT_OPEN FMT_CLOSE <| choice [
        pchar INVOKE_START >>. trivialString .>>. _paren invokeArg |>> (fun (func, args) -> ParseUnit.ConjFormat {
            func = func
            args = args
        })
        many1Satisfy (fun c -> c <> FMT_CLOSE) >>= (fun str ->
            let int_str = if (str.Contains(':')) then str.Split(':').[0] else str
            match Int32.TryParse int_str with
                | true, int -> updateStateArg int |> updateUserState >>% ParseUnit.StandardFormat str
                | _ -> fail <| sprintf "%s is not a valid format string." str)
    ])
and internal invokeArg =
    (choice [
        betweenChars QUOTE QUOTE (fullString false)
        trivialString >>= fun s ->
            match Int32.TryParse s with
            | true, int -> updateStateArg int |> updateUserState >>% [ Argument int ]
            | _ -> preturn [String s]
    ])
and internal fullString allowUnescapedQuote s =
    (many <| choice [
        escapedFragment
        normalFragment allowUnescapedQuote
        formatFragment
    ]) s

let stringParser s =
     let state = { highestArg = -1 }
     if String.IsNullOrWhiteSpace s
     then OK ([], state)
     else
        match (runParserOnString (fullString true .>> eof)
                state "LocalizationParser" s) with
        | Success (result, state, _) -> OK (result, state)
        | Failure (errStr, _, _) -> Failed [errStr]






module FTests.SMParser
open FParser.SMParser
open FTests.Utils
open FTests.Extensions
open ParserCS

open FTests

open FParsec
open NUnit.Framework

let USE_CS = true;

[<SetUp>]
let Setup () =
    ()

type HPU =
    | S of string
    | P of HPU[][]


let rec toHPU struct(pu, _) =
    match pu with
    | ParsedUnit.S s -> S s
    | ParsedUnit.P arrs -> Array.map (Array.map toHPU) arrs |> P

let rec toHPU_CS struct(pu: SMParser.ParsedUnit, _) =
    match pu with
    | :? SMParser.ParsedUnit.S as s -> S s.Item
    | :? SMParser.ParsedUnit.P as p -> Array.map (Array.map toHPU_CS) p.Item |> P


let objs str =
    if USE_CS
    then (SMParser.SMParser2Exec(str)).GetOrThrow
         |> List.ofArray
         |> List.map toHPU_CS
    else
        (SMParser2 str).Try |> List.map toHPU
    
let Eq(str, units) = Assert.ListEq(objs str, units)
    
[<Test>]
let TextParen() =
    Eq("(a + b, c) w", [P [| [| S "a"; S "+"; S "b"|] ; [| S "c" |] |]; S "w"])
    Eq("(a + s(b, c), d)", [P [|
        [| S "a"; S "+"; S "s"; P [|[| S "b" |]; [| S "c" |]|] |]
        [| S "d" |]
    |]])
    Eq("""!{ hello(world)
(hello, %world)
!}
$hello((1, 2))""", [P [|
        [|S "hello"|]
        [|P [|
             [| S "1" |]
             [| S "2" |]
         |]|]
    |]])
module FTests.SMParser
open System
open FParser.SMParser
open Common.Types
open FTests.Utils
open FTests.Extensions

open FTests

open FParsec
open NUnit.Framework

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

let objs str =
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
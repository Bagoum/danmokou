module FTests.AAParser
open FParser.AAParser
open FCommon.Types
open FTests.Utils

open FParsec
open NUnit.Framework

[<SetUp>]
let Setup () =
    ()

let ps = Punct (" ", PSpace);
let p = Atom ".";
let pp = Punct (".", PPeriod);
let pexc = Punct ("!", PPeriod);
let pe = Punct (".", PEllipsis);

let Eq<'t>((a:Errorable<'t>), (b:Errorable<'t>)) = Assert.AreEqual(a, b)

let toMatch kvs =
    let kvm = kvs |> Map.ofList
    kvm.TryFind
let wiki = ([ ("npc", [("blue", WikiEntry.Default)] |> toMatch) ] |> Map.ofList)
let map1 = [ ( "a", 1); ("j", 5) ] |> toMatch

let cfg: Config<unit,unit,int> = {
    Config.Default with
        speed = 1.0<f>
        charsPerBlock = 3
        blockOps = 0.0<ops>
        blockEvent = Ref3 1 |> Some
        punctOps = function | PSpace -> 1.0<ops> | _ -> 10.0<ops>
}

[<Test>]
let TestRef() =
    let parser = CreateParser1<int> "sfx" map1 wiki
    Eq(parser "hello $sfx(a) bob", OK [ Atom "hello"; ps; Ref3 1 |> Ref; ps; Atom "bob" ])
    Assert.IsFailed(parser "hello $sfx(b) bob")
    Eq(parser "he.llo! me", OK [ Atom "he"; pp; Atom "llo"; pexc; ps; Atom "me" ])
    Eq(parser "he.llo!\nme", OK [ Atom "he"; pp; Atom "llo"; pexc; Newline; Atom "me" ])
    Eq(parser "hello... me", OK [ Atom "hello";  pe;pe; pe; ps; Atom "me" ])
    Eq(parser "$$npc(blue)", OK [ Atom "!DEFAULT!" ])
    Assert.IsFailed(parser "$$npc(blue2)")
    Assert.IsFailed(parser "$$npc2(blue)")
    
let tw0 s = TTextWait (s, 0.0<s>)
let tw s t = TTextWait (s, t)
[<Test>]
let TextExport() =
    let tr3 = Ref3 1 |> TRef
    let parser = CreateParser1<int> "sfx" map1 wiki
    let exporter = ParseAndExport cfg parser
    Eq(exporter "longword", OK [tr3; tw0 "lon"; tr3; tw0 "gwo"; tr3; tw0 "rd" ])
    Eq(exporter "long word", OK [tr3; tw0 "lon"; tr3; tw0 "g"; TSpace " "; Wait 1.0<s>; tr3;  tw0 "wor"; tr3; tw0 "d" ])
    let cfg2 = { cfg with blockOps = 2.0<ops> }
    let exporter = ParseAndExport cfg2 parser
    Eq(exporter "long2 word", OK [tr3; tw "lon" 2.0<s>; tr3; tw "g2" (4.0<s>/3.0); TSpace " "; Wait 1.0<s>; tr3
                                  tw "wor" 2.0<s>; tr3; tw "d" (2.0<s>/3.0) ])
    Eq(exporter "lo<color=\"red\">ng2 word", OK [tr3; tw "lo" (4.0<s>/3.0); tw0 "<color=\"red\">"; tr3
                                                 tw "ng2" 2.0<s>; TSpace " "; Wait 1.0<s>; tr3; tw "wor" 2.0<s>; tr3
                                                 tw "d" (2.0<s>/3.0) ])

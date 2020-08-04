module FTests.Common

open Common
open System
open Common.Types
open Common.Functions
open Common.Extensions
open NUnit.Framework
open Utils
        
[<SetUp>]
let Setup () =
    ()

[<Test>]
let PartialZip() =
    Assert.AreEqual(partialZip [ ([1;2;3], 10); ([4;5;6], 40) ],
                    ([1; 2; 3; 4; 5; 6], [Some 10; None; None; Some 40; None; None]))
    Assert.AreEqual(partialZip [ ([1], 10); ([4;5], 40) ],
                    ([1; 4; 5], [Some 10; Some 40; None]))

[<Test>]
let MaxConsecutive() =
    Assert.AreEqual(maxConsecutive 5 [ ], 0)
    Assert.AreEqual(maxConsecutive 5 [ 1; 4 ], 0)
    Assert.AreEqual(maxConsecutive 5 [ 1; 5; 5; 4 ], 2)
    Assert.AreEqual(maxConsecutive 5 [ 1; 5; 5; 4; 5; 5; 5 ], 3)
    Assert.AreEqual(maxConsecutive 5 [ 1; 5; 5; 4; 5; 5; 5; 7 ], 3)
    Assert.AreEqual(maxConsecutive 5 [ 1; 5; 5; 4; 1; 1; 1; 7 ], 2)

[<Test>]
let Intertwice() =
    Assert.AreEqual(intertwine [1;2;3] [10;20;30], [1;10;2;20;3;30])
    Assert.AreEqual(intertwine [] [], [])
    Assert.Raises<ArgumentException>((fun () -> intertwine [1;2;3] [10;20] |> ignore))
    Assert.AreEqual(intertwine1 [1;2;3] 8, [1;8;2;8;3;8])
    Assert.AreEqual(intertwine1 [] 8, [])

[<Test>]
let ReplaceEntries() =
    let lt0 = fun x -> x < 0
    Assert.AreEqual(replaceEntries true [1;2;3] [] lt0, Enough [1;2;3])
    Assert.AreEqual(replaceEntries false [1;2;3] [] lt0, Enough [1;2;3])
    Assert.IsTooMany(replaceEntries true [] [1] lt0)
    Assert.AreEqual(replaceEntries true [] [] lt0, UOErrorable<int list>.Enough [])
    Assert.IsTooMany(replaceEntries true [1;2;3] [1] lt0)
    Assert.AreEqual(replaceEntries true [1;-2;3] [72] lt0, Enough [1;72;3])
    Assert.IsTooFew(replaceEntries false [1;2;-3;-4] [1] lt0)
    Assert.AreEqual(replaceEntries true [1;-2;3;-4] [72] lt0, Enough [1;72;3;-4])
    Assert.AreEqual(replaceEntries true [1;-2;3;-4;-5;6] [72;92;84] lt0, Enough [1;72;3;92;84;6])
    Assert.AreEqual(replaceEntries false [1;-2;3;-4;-5;6] [72;92;84] lt0, Enough [1;72;3;92;84;6])
    
[<Test>]
let CountFilter() =
    let lt0 = fun x -> x < 0
    Assert.AreEqual(countFilter lt0 [1;2;3;-4], 1)
    Assert.AreEqual(countFilter lt0 [-2;-4;-3;-4], 4)
    Assert.AreEqual(countFilter lt0 [1;2;3;45], 0)
    Assert.AreEqual(countFilter lt0 [], 0)
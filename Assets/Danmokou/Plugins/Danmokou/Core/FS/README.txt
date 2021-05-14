2021/02/14
Due to some exceedingly strange circumstances, I have been forced to port the F# parsers to C# and remove all compiled F# code from this project. As such, this folder no longer contains Common.dll, Parser.dll, or FSharp*.dll. Ask me about it on Discord if you really want to know.

If you get problems with packages saying that they have the wrong version of F#Core, you can disable "Validate References" on it. 
I have done this for FParsec/FParsecCS.
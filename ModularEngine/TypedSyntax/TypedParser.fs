﻿namespace Microsoft.Research.Dkal.Ast.TypedSyntax

open System.IO
open Microsoft.FSharp.Text.Lexing

open Microsoft.Research.Dkal.Interfaces
open Microsoft.Research.Dkal.Ast

/// The TypedParser parses the typed concrete syntax, which carries type 
/// annotations in every function application and every variable
type TypedParser() = 

  let lexbuff s = LexBuffer<char>.FromString(s)

  interface IAstParser with
    member sp.ParseType s =
      Parser.Type Lexer.tokenize (lexbuff s) :> IType
    
    member sp.ParseTerm s =
      Parser.ITerm Lexer.tokenize (lexbuff s)

    member sp.ParseInfon s = 
      let t = Parser.ITerm Lexer.tokenize (lexbuff s)
      if t.Type <> (Infon :> IType) then
        failwith <| "Expecting infon and found " + t.Type.ToString()
      else
        t

    member sp.ParseRule s = 
      let t = Parser.ITerm Lexer.tokenize (lexbuff s)
      if t.Type <> (Infon :> IType) then
        failwith <| "Expecting rule and found " + t.Type.ToString()
      else
        t
    
    member sp.ParsePolicy s = 
      Parser.Policy Lexer.tokenize (lexbuff s)

    member sp.ParseSignature s =
      { Substrates = []; Tables = []; Relations = [] }

    member sp.ParseAssembly s =
      let sp = (sp :> IAstParser)
      { Policy = sp.ParsePolicy s; Signature = sp.ParseSignature s }

﻿// *********************************************************
//
//    Copyright (c) Microsoft. All rights reserved.
//    This code is licensed under the Apache License, Version 2.0.
//    THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
//    ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
//    IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
//    PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
// *********************************************************

namespace Microsoft.Research.Dkal.Ast.Infon.Syntax.Typed

open System.IO

open Microsoft.Research.Dkal.Interfaces
open Microsoft.Research.Dkal.Ast.Infon
open Microsoft.Research.Dkal.Ast
open Microsoft.Research.Dkal.Ast.Syntax.Parsing

/// The TypedParser parses the typed concrete syntax, which carries type 
/// annotations in every function application and every variable
type TypedParser() = 

  interface IInfonParser with

    member sp.SetParsingContext (context: IParsingContext) = 
      ()

    member sp.ParseType s =
      GeneralParser.TryParse (Parser.Type Lexer.tokenize) s
    
    member sp.ParseTerm s =
      GeneralParser.TryParse (Parser.ITerm Lexer.tokenize) s

    member sp.ParseInfon s = 
      let t = GeneralParser.TryParse (Parser.ITerm Lexer.tokenize) s
      if not (t.Type.IsSubtypeOf Type.AbstractInfon) then
        failwith <| "Expecting infon and found " + t.Type.ToString()
      else
        t

    member sp.ParseRule s = 
      let t = GeneralParser.TryParse (Parser.ITerm Lexer.tokenize) s
      if t.Type <> Type.Infon then
        failwith <| "Expecting rule and found " + t.Type.ToString()
      else
        t
    
    member sp.ParsePolicy s = 
      GeneralParser.TryParse (Parser.Policy Lexer.tokenize) s

    member sp.ParseSignature s =
      { Substrates = []; Relations = []; DatasourceRelations= []; }

    member sp.ParseAssembly s =
      let sp = (sp :> IInfonParser)
      { Policy = sp.ParsePolicy s; Signature = sp.ParseSignature s }

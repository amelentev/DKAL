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

namespace Microsoft.Research.Dkal.Ast.Substrate.Xml.Syntax.Simple

open Microsoft.Research.Dkal.Interfaces
open Microsoft.Research.Dkal.Utils.PrettyPrinting
open Microsoft.Research.Dkal.Substrate
open Microsoft.Research.Dkal.Substrate.Xml

open System.Collections.Generic

/// The TypedXmlPrettyPrinter prints substrate elements into the typed concrete syntax
type TypedXmlPrettyPrinter() =
    
  interface ISubstratePrettyPrinter with
    member spp.PrintTerm t =
      match t with
      | :? XmlSubstrateQueryTerm as t ->
        PrettyPrinter.PrettyPrint <| spp.TokenizeTerm t
      | _ -> failwith "Expecting DummySubstrateTerm when printing SimpleSqlSyntax"

  member private spp.PrintType (t: IType) = t.FullName

  member private spp.PrintVar (v: IVar) = v.Name + ":" + spp.PrintType v.Type

  member private spp.PrintVarOrConst (t: ITerm) = 
    match t with
    | :? IVar as v -> spp.PrintVar v
    | :? IConst as c -> c.ToString()
    | _ -> failwithf "Expecting variable or constant when printing XML term %O" t

  member private spp.TokenizeTerm (t: XmlSubstrateQueryTerm) =
    let outputVars = String.concat "," 
                      <| Seq.map (fun (kv: KeyValuePair<string,ITerm>) -> 
                                    match kv.Key with
                                    | "" -> spp.PrintVarOrConst kv.Value
                                    | att -> spp.PrintVarOrConst kv.Value + "<->\"" + att + "\"") t.Output
    let vars = String.concat "," 
                <| List.map (fun (v: IVar) -> spp.PrintVar v) t.Vars
    [ TextToken <| "\"" + t.XPath + "\"|" + outputVars + "|" + vars ]

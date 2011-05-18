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

namespace Microsoft.Research.Dkal.Substrate.Factories

open Microsoft.Research.Dkal.Interfaces
open Microsoft.Research.Dkal.Substrate.Basic
open Microsoft.Research.Dkal.Substrate.Sql
open Microsoft.Research.Dkal.Substrate.Xml
open Microsoft.Research.Dkal.Substrate.Crypto

open System.Xml.Linq
open System.IO
open NLog

/// The SubstrateFactory provides a factory to construct different substrates.
/// A substrate kind, arguments and namespaces must be provided
type SubstrateFactory() =
  
  static let log = LogManager.GetLogger("Substrate.Factories")

  static member Substrate (kind: string) (args: string list) (namespaces: string list) = 
    match kind, args with
    | "basic", [] -> 
      if not namespaces.IsEmpty then
        log.Warn("Attempt to construct basic substrate with associated namespaces {0}. Namespaces will be ignored.", namespaces)
      new BasicSubstrate() :> ISubstrate
    | "sql", [cs; schema] -> new SqlSubstrate(cs, schema, namespaces) :> ISubstrate
    | "xml", [xml] -> 
      if xml.Contains "<" then
        new XmlSubstrate(XDocument.Parse(xml), namespaces) :> ISubstrate
      else
        new XmlSubstrate(xml, namespaces) :> ISubstrate
    | "crypto", [] -> CryptoSubstrate() :> ISubstrate
    | _ -> failwith <| "Unknown substrate kind/params for " + kind

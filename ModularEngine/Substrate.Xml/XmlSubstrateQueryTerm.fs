﻿namespace Microsoft.Research.Dkal.Substrate.Xml

open System.Collections.Generic
open Microsoft.Research.Dkal.Interfaces
open Microsoft.Research.Dkal.Ast
open System.Text

/// xpath - xpath expression with (optional) input variables encoded as "$VARNAME"
/// vars - input variables
/// outputVars - map from result nodes attribute names (or node name in case of "") to output variable
type XmlSubstrateQueryTerm(ns: string, xpath: string, vars: IVar list, outputVars: IDictionary<string, IVar>) =

  let mutable subst = Substitution.Id

  member x.XPath = xpath
  member x.Vars = vars
  member x.OutputVars = outputVars

  override x.ToString() =
    let sout = outputVars.Keys |> Seq.map (function
        | "" -> outputVars.[""].Name
        | attr -> outputVars.[attr].Name + "<->\"" + attr+"\"") |> String.concat ", "
    "{| \""+ns+"\" | " + xpath + " | " + sout + " |}"

  interface ISubstrateQueryTerm with
    
    member x.Type = Type.SubstrateQuery
    member x.Vars = vars
    member x.Apply subst' =
      subst <- subst.ComposeWith subst'
      x :> ITerm
    member x.Normalize() = x :> ITerm
    member x.UnifyFrom _ _ = None
    member x.Unify _ = None

    member x.Namespace = ns
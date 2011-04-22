﻿[<AutoOpen>]
module Microsoft.Research.Dkal.Ast.ActivePatterns

  open Microsoft.Research.Dkal.Ast

  // Rule patterns
  let (|Rule|_|) mt = match mt with
                      | App({Name="rule"}, [cs; cw; a]) -> Some (cs, cw, a)
                      | _ -> None

  // Action patterns
  let (|Seq|_|) mt =  match mt with
                      | App({Name="seq"}, [a1; a2]) -> Some (a1, a2)
                      | _ -> None
  let (|Send|_|) mt = match mt with
                      | App({Name="send"}, [ppal; i]) -> Some (ppal, i)
                      | _ -> None
  let (|Learn|_|) mt =  match mt with
                        | App({Name="learn"}, [i]) -> Some i
                        | _ -> None
  let (|Forget|_|) mt = match mt with
                        | App({Name="forget"}, [i]) -> Some i
                        | _ -> None
  let (|Install|_|) mt =  match mt with
                          | App({Name="install"}, [r]) -> Some r
                          | _ -> None
  let (|Uninstall|_|) mt =  match mt with
                            | App({Name="uninstall"}, [r]) -> Some r
                            | _ -> None

  // Substrate patterns
  let (|Sql|_|) mt =  match mt with 
                      | App({Name="sql"}, [mt']) -> Some mt'
                      | _ -> None
  let (|Xml|_|) mt =  match mt with 
                      | App({Name="xml"}, [mt']) -> Some mt'
                      | _ -> None

  // Infon patterns
  let (|EmptyInfon|_|) mt = match mt with 
                            | App({Name="emptyInfon"}, []) -> Some ()
                            | _ -> None
  let (|AsInfon|_|) mt =  match mt with 
                          | App({Name="asInfon"}, [exp; substrate]) -> Some (exp, substrate)
                          | _ -> None
  let (|AndInfon|_|) mt = match mt with
                          | App({Name="and"; RetTyp=Infon}, mts) -> Some mts
                          | _ -> None
  let (|ImpliesInfon|_|) mt = match mt with
                              | App({Name="implies"; RetTyp=Infon}, [mt1; mt2]) -> Some (mt1, mt2)
                              | _ -> None
  let (|SaidInfon|_|) mt = match mt with
                            | App({Name="said"}, [ppal; mt']) -> Some (ppal, mt')
                            | _ -> None

  // Bool patterns
  let (|AndBool|_|) mt =  match mt with
                          | App({Name="and"; RetTyp=Bool}, mts) -> Some mts
                          | _ -> None
  let (|OrBool|_|) mt = match mt with
                        | App({Name="or"; RetTyp=Bool}, mts) -> Some mts
                        | _ -> None

  // Literal patterns
  let (|Principal|_|) mt =  match mt with
                            | Const(PrincipalConstant(p)) -> Some p
                            | _ -> None
  let (|True|_|) mt = match mt with
                      | Const(BoolConstant(true)) -> Some ()
                      | _ -> None
  let (|False|_|) mt =  match mt with
                        | Const(BoolConstant(false)) -> Some ()
                        | _ -> None

﻿namespace Microsoft.Research.Dkal.SimpleSyntax

  open Microsoft.Research.Dkal.Interfaces
  open Microsoft.Research.Dkal.Ast
  open Microsoft.Research.Dkal.Utils.PrettyPrinting

  type SimplePrettyPrinter() =
    interface IPrettyPrinter with
      member spp.PrintType t =
        match t with
        | Substrate(t) when t = typeof<int> -> "int"
        | Substrate(t) when t = typeof<float> -> "float"
        | Substrate(t) when t = typeof<string> -> "string"
        | t -> t.ToString().ToLower()

      member spp.PrintTerm mt =
        PrettyPrinter.PrettyPrint <| spp.TokenizeMetaTerm mt

      member spp.PrintInfon mt =
        PrettyPrinter.PrettyPrint <| spp.TokenizeMetaTerm mt

      member spp.PrintAssertion a =
        PrettyPrinter.PrettyPrint <| spp.TokenizeAssertion a
        
      member spp.PrintPolicy p =
        PrettyPrinter.PrettyPrint <| spp.TokenizePolicy p

      member spp.PrintSignature s =
        PrettyPrinter.PrettyPrint <| spp.TokenizeSignature s

      member spp.PrintAssembly a =
        PrettyPrinter.PrettyPrint <| spp.TokenizeAssembly a

    member private spp.PrintType t = (spp :> IPrettyPrinter).PrintType t
    member private spp.PrintMetaTerm mt = (spp :> IPrettyPrinter).PrintInfon mt

    static member FindFunctionSymbol f = 
      match f with
      | "infonAnd" 
      | "boolAnd" -> "&&", true
      | "infonImplies" -> "->", true
      | "infonSaid" -> "said", true
      | "boolOr" -> "||", true
      | "boolNot" -> "!", false
      | "eqBool" | "eqInt32" | "eqDouble" | "eqString" | "eqPrincipal" -> "==", true
      | "neqBool" | "neqInt32" | "neqDouble" | "neqString" | "neqPrincipal" -> "!=", true
      | "ltInt32" | "ltDouble" -> "<", true
      | "lteInt32" | "lteDouble" -> "<=", true
      | "gltInt32" | "gtDouble" -> ">", true
      | "gteInt32" | "gteDouble" -> ">=", true
      | "plusInt32" | "plusDouble" | "plusString" -> "+", true
      | "minusInt32" | "minusDouble" -> "-", true
      | "timesInt32" | "timesDouble" -> "*", true
      | "divInt32" | "divDouble" -> "/", true
      | "uminusInt32" | "uminusDouble" -> "-", false
      | f -> f, false

    member private spp.TokenizeMetaTerm mt =
      match mt with
      | App(f, mts) -> 
        let fSymbol, infix = SimplePrettyPrinter.FindFunctionSymbol f.Name
        let args = List.map spp.PrintMetaTerm mts
        if infix then
          [TextToken <| "(" + (String.concat (" " + fSymbol + " ") args) + ")"]
        elif fSymbol.Contains "." then
          [TextToken <| fSymbol ]
        else
          [TextToken <| fSymbol + "(" + (String.concat ", " args) + ")"]
      | Var(v) -> [TextToken v.Name]
      | Const(c) -> 
        match c with
        | BoolConstant(b) -> [TextToken(b.ToString())]
        | PrincipalConstant(p) -> [TextToken(p.ToString())]
        | SubstrateConstant(o) when o.GetType() = typeof<string> -> [TextToken("\"" + o.ToString() + "\"")]
        | SubstrateConstant(o) -> [TextToken(o.ToString())]
   
    member private spp.TokenizeAssertion (a: Assertion) =
      let vars = a.Vars |> Seq.toList
      let varsDecl = List.map (fun (v: Variable) -> v.Name + ": " + spp.PrintType v.Typ) vars
      let beginVars, endVars =  if varsDecl.Length > 0 then
                                  [ TextToken <| "with " + (String.concat ", " varsDecl);
                                    TabToken;
                                    NewLineToken ], [UntabToken]
                                else
                                  [], []
      match a with
      | Know(k) -> beginVars @ [ TextToken <| "me knows " + spp.PrintMetaTerm k.Fact ] @ endVars
      | CommRule(c) -> 
        beginVars @ [ TextToken "if"; TabToken; NewLineToken; 
                      TextToken <| spp.PrintMetaTerm c.Trigger; UntabToken; NewLineToken;
                      TextToken <| "send to " + spp.PrintMetaTerm c.Target; TabToken; NewLineToken;
                      TextToken <| spp.PrintMetaTerm c.Content; UntabToken] @ endVars

    member private spp.TokenizePolicy (p: Policy) =
      List.collect (fun a -> spp.TokenizeAssertion a @ [ NewLineToken; NewLineToken ]) p.Assertions

    member private spp.TokenizeSignature (s: Signature) =
      List.collect (fun td -> spp.TokenizeTableDeclaration td @ [ NewLineToken; NewLineToken ]) s.Tables
        @ List.collect (fun rd -> spp.TokenizeRelationDeclaration rd @ [ NewLineToken; NewLineToken ]) s.Relations

    member private spp.TokenizeTableDeclaration (td: TableDeclaration) =
      [TextToken <| "table " + td.Name + "(";
        TextToken <| String.concat ", " (List.map (fun (v: Variable) -> v.Name + ": " + spp.PrintType v.Typ) td.Cols);
        TextToken ")" ]

    member private spp.TokenizeRelationDeclaration (rd: RelationDeclaration) =
      [TextToken <| "relation " + rd.Name + "(";
        TextToken <| String.concat ", " (List.map (fun (v: Variable) -> v.Name + ": " + spp.PrintType v.Typ) rd.Args);
        TextToken ")" ]

    member private spp.TokenizeAssembly (a: Assembly) =
      spp.TokenizeSignature a.Signature
        @ spp.TokenizePolicy a.Policy
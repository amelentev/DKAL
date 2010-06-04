﻿namespace Microsoft.Research.DkalEngine

open System
open System.IO
open System.Text
open Microsoft.FSharp.Text
open Microsoft.FSharp.Collections

open Microsoft.Research.DkalEngine.Util
open Microsoft.Research.DkalEngine.Ast

type GQueue<'a> = Collections.Generic.Queue<'a>

type Pref =
  | Said of PrincipalTerm
  | Implied of PrincipalTerm

type SubstSet =
  { substs : list<Subst> }
  
  static member Empty = { substs = [] }
  static member Single s = { substs = [s] }
  member this.All = this.substs
  member this.Add (ss:SubstSet) = { substs = this.substs @ ss.substs }

type Binding =
  {
    formal : Var
    actual : Term
  }

type IViewHooks =
  abstract Recieved : Message -> unit
  abstract Send : Message -> unit
  abstract Knows : Knows -> unit
  abstract QueryResults : Infon * seq<seq<Binding>> -> unit
  abstract SyntaxError : Pos * string -> unit
  abstract Loaded : string -> unit
  abstract Warning : string -> unit
  
type Action = delegate of unit -> unit
  
type Engine =
  {
    mutable sql : option<SqlConnector>
    mutable comm : option<SqlCommunicator>
    mutable me : option<Principal>
    ctx : PreAst.Context
    sentItems : Dict<Message, bool>
    hooks : IViewHooks
    pending : GQueue<Action>
    mutable trace : int
    mutable infonstrate : list<Knows>
    mutable filters : list<Filter>
    mutable communications : list<Communication>
    mutable nextId : int   
    mutable die : bool
    mutable stepwise : bool
  }
  
  static member Make (trace, hooks) =
    let ctx = PreAst.Context.Make()
    ctx.trace <- trace
    { me = None; ctx = ctx; hooks = hooks;
      infonstrate = []; filters = []; communications = []; nextId = 0;
      sentItems = dict()
      trace = trace
      sql = None
      comm = None
      pending = new GQueue<_>()
      die = false
      stepwise = true
      }

  member this.Load filename stream =
    try
      let ctx = this.ctx
      let prelude = Tokenizer.fromString Prelude.text
      let toks = prelude @ Tokenizer.fromFile filename stream
      Parser.addStandardRules ctx
      let toks = Parser.addRules ctx toks
      let toks = Parser.applyRules ctx toks
      //System.Console.WriteLine (Tok.Block (fakePos, toks))
      Resolver.resolveFunctions ctx
      let assertions = List.collect (Resolver.resolve ctx) toks
      let me = ctx.principals.[ctx.options.["me"]]
      
      this.me <- Some me

      let priv_sql_name = "private_sql" + ctx.options.["me"]
      let priv_sql =
        if ctx.options.ContainsKey priv_sql_name then
          ctx.options.[priv_sql_name]
        else
          ctx.options.["private_sql"]          
      this.sql <- Some (SqlConnector priv_sql)

      this.comm <- Some (SqlCommunicator(ctx, me))
            
      this.AddDefaultFilter()       
      this.Populate assertions 
      
      match ctx.options.TryGetValue "stepwise" with
        | true, "0" -> this.stepwise <- false
        | _ -> ()

      this.hooks.Loaded filename
            
    with SyntaxError (pos, s) ->
      this.hooks.SyntaxError (pos, s)
  
  member this.SetTrace v =
    this.ctx.trace <- v
    this.trace <- v

  member this.Populate assertions =
    let myId = this.me.Value.internal_id
    let addAssertion = function
      | Knows k when k.ai.principal.internal_id = myId ->
        this.infonstrate <- k :: this.infonstrate
      | SendTo c when c.ai.principal.internal_id = myId ->
        this.communications <- c :: this.communications
      | ReceiveFrom f when f.ai.principal.internal_id = myId ->
        this.filters <- f :: this.filters
      | _ -> ()
    List.iter addAssertion (List.map this.HandleCertifications assertions)
    
  member private this.NextId () =
    this.nextId <- this.nextId - 1
    this.nextId
  
  member private this.FreshVar tp =
    let id = this.NextId()
    ({ typ = tp; id = id; name = tp.name + "#" + id.ToString() } : Var)

  member private this.Freshen (infon:Infon) =
    let subst = ref Map.empty
    let repl (v:Var) =
      if not ((!subst).ContainsKey v.id) then
        let id = this.NextId()
        subst := (!subst).Add (v.id, { v with id = id; name = v.name + "#" + id.ToString() })      
      (!subst).[v.id]
    infon.Map (function Term.Var v -> Some (Term.Var (repl v)) | _ -> None)
    
  member private this.FreshenList infons =
    match this.Freshen (Term.App (Function.Empty, infons)) with
      | Term.App (_, l) -> l
      | _ -> failwith "cannot happen"
      
  member private this.FakeAI () =
    { origin = fakePos; principal = this.me.Value }
    
  member this.AddDefaultFilter () =
    let src = this.FreshVar Type.Principal
    let msg = this.FreshVar Type.Infon
    let proviso = this.FreshVar Type.Infon
    let filter =
      {
        ai = this.FakeAI()
        source = Term.Var src
        message = Infon.Var msg
        proviso = Infon.Var proviso
        trigger = Infon.Empty
      }
    this.filters <- filter :: this.filters
  
  member private this.InfonsWithPrefix subst pref template =
    let res = ref []
    let rec stripPrefix subst prefixUnif preconds suff = 
      let simpl s (t:Term) = t.Apply s
      
      let immediate = function
        | ([], i) ->
          let rec unifyAndSimpl s = function
            | [] -> s
            | (a, b) :: xs ->
              match s with
                | None -> None
                | Some s -> unifyAndSimpl (unifyTerms s (simpl s a, simpl s b)) xs
        
          if this.trace >= 2 then
            System.Console.Write ("immediate result " + String.concat ", " (prefixUnif |> List.map (fun (a, b) -> String.Format ("{0} =?= {1}", a, b)) ))
          match unifyAndSimpl (Some subst) ((template, i) :: prefixUnif) with
            | Some subst ->
              if this.trace >= 2 then
                System.Console.WriteLine (" YES")
              res := (subst, preconds) :: !res
            | None ->
              if this.trace >= 2 then
                System.Console.WriteLine (" NO")
        | _ -> ()
             
      function
      | (Pref.Implied t1 :: pref, InfonSaid (t2, i))
      | (Pref.Implied t1 :: pref, InfonImplied (t2, i)) ->
        match unifyTerms subst (simpl subst t1, simpl subst t2) with
          | Some subst ->
            stripPrefix subst prefixUnif preconds (fun i -> suff (Infon.Implied (t2, i))) (pref, i)
          | None -> 
            stripPrefix subst ((t1, t2) :: prefixUnif) preconds (fun i -> suff (Infon.Implied (t2, i))) (pref, i)
      | (Pref.Said t1 :: pref, InfonSaid (t2, i)) ->
        match unifyTerms subst (simpl subst t1, simpl subst t2) with
          | Some subst -> 
            stripPrefix subst prefixUnif preconds (fun i -> suff (Infon.Said (t2, i))) (pref, i)
          | None ->
            stripPrefix subst ((t1, t2) :: prefixUnif) preconds (fun i -> suff (Infon.Said (t2, i))) (pref, i)
      | (pref, InfonAnd (a, b)) ->
        stripPrefix subst prefixUnif preconds suff (pref, a)
        stripPrefix subst prefixUnif preconds suff (pref, b)
      | (pref, InfonFollows (a, b)) as t ->
        immediate t
        stripPrefix subst prefixUnif (suff a :: preconds) suff (pref, b)
      | (pref, Infon.Var v) when subst.ContainsKey v.id ->
        stripPrefix subst prefixUnif preconds suff (pref, subst.[v.id])
      | t -> immediate t
    
    List.iter (fun k -> stripPrefix subst [] [] (fun x -> x) (pref, this.Freshen k.infon)) this.infonstrate
    !res
      
  
  member this.DoDerive pref (subst:AugmentedSubst) infon =
    if this.trace > 0 then
      System.Console.WriteLine ("derive: {0} under {1}", infon, substToString subst.subst)
    
    let sum f lst =
      List.fold (fun acc e -> f e @ acc) [] lst
      
    match infon with
      | InfonAnd (i1, i2) ->
        (this.DoDerive pref subst i1) |> sum (fun s1 -> this.DoDerive pref s1 i2)
      | InfonEmpty _ -> [subst]
      | InfonSaid (p, i) ->
        this.DoDerive (Pref.Said p :: pref) subst i
      | InfonImplied (p, i) ->
        this.DoDerive (Pref.Implied p :: pref) subst i
      | Infon.Var v when subst.subst.ContainsKey v.id ->
        this.DoDerive pref subst subst.subst.[v.id]
      | AsInfon e ->
        if pref <> [] then
          failwith "asInfon(...) under said/implied prefix"
        [{ subst with assumptions = e :: subst.assumptions }]
      | templ ->
        let rec checkOne = function
          | (subst, precond :: rest) ->
            this.DoDerive [] subst precond |> sum (fun s -> checkOne (s, rest))
          | (subst, []) -> [subst]
        this.InfonsWithPrefix subst.subst pref templ |> List.map (fun (s, p) -> ({subst with subst = s }, p)) |> sum checkOne

  member private this.Send msg =
    if this.sentItems.ContainsKey msg then
      ()
    else
      this.sentItems.Add (msg, true)
      this.hooks.Send msg
      this.comm.Value.SendMessage msg
      
  member this.Derive s (infon:Term) =
    let vars = (infon.Apply s).Vars()
    let checkAssumptions (augS:AugmentedSubst) =
      let apply (t:Term) = t.Apply augS.subst
      let sqlExpr = SqlCompiler.compile this.ctx this.NextId (augS.assumptions |> List.map apply)
      SqlCompiler.execQuery (this.sql.Value, this.comm.Value, sqlExpr, augS.subst, vars) |> Seq.toList      
    { substs = this.DoDerive [] (AugmentedSubst.NoAssumptions s) infon |> List.collect checkAssumptions }
  
  member this.Listen (msg:Message) =  
    this.hooks.Recieved msg
    let src = Term.Const (Const.Principal msg.source)
    let msg =
      match this.FreshenList [msg.message; msg.proviso] with
        | [m;p] -> { msg with message = m ; proviso = p }
        | _ -> failwith "impossible"
    let matches (filter:Filter) =
      let subst =
        unifyList unifyTerms (Some Map.empty) [(src, filter.source); 
                                               (msg.message, filter.message);
                                               (msg.proviso, filter.proviso)]
      let apply s =
        let t = msg.message.Apply s
        let infons =
          match t with
            | InfonCert (_, e) ->
              match msg.proviso.Apply s with
                | InfonEmpty ->
                  let acc = vec()
                  match this.EvidenceCheck acc e with
                    | Some _ ->
                      Seq.toList acc
                    | _ ->
                      this.hooks.Warning ("fake certified infon")
                      []
                | _ ->
                  this.hooks.Warning ("certified infon with proviso")
                  []
            | _ ->
              match msg.proviso.Apply s with
                | InfonEmpty ->
                  [Infon.Said (src, t)]
                | proviso ->
                  [Infon.Follows (proviso, Infon.Implied (src, t))]
        List.map (fun infon -> { ai = this.FakeAI(); infon = infon }) infons
          
      match subst with
        | Some s ->
          (this.Derive s filter.trigger).All |> List.collect apply
        | None -> []
    let newInfons = this.filters |> List.collect matches
    List.iter this.hooks.Knows newInfons
    this.infonstrate <- newInfons @ this.infonstrate
    
  member this.Listen () =
    match this.comm.Value.CheckForMessage() with
      | Some m -> this.Listen m
      | None -> ()
    
  member this.Me = this.me
  
  member this.Talk () =
    let runCommFor (comm:Communication) (subst:Subst) =
      match comm.target.Apply subst with
        | Term.Const (Const.Principal p) ->
          let msg = ({ source = this.me.Value
                       target = p
                       message = comm.message.Apply subst
                       proviso = comm.proviso.Apply subst
                     } : Message).Canonical()
          this.Send msg
        | t ->
          System.Console.WriteLine ("attempting broadcast message: " + t.ToString())
          
    this.communications |> List.iter (fun comm -> (this.Derive Map.empty comm.trigger).All |> List.iter (runCommFor comm))
  
  member this.ParseInfon s = 
    try
      let toks = Tokenizer.fromString s
      let toks = Parser.applyRules this.ctx toks
      match toks with
        | [t]
        | [t; PreAst.Tok.NewLine _] -> Some (Resolver.resolveInfon this.ctx t)
        | [PreAst.Tok.NewLine _]
        | [] -> raise (SyntaxError (fakePos, "infon expected"))
        | _ -> raise (SyntaxError (fakePos, "only one infon expected"))
    with 
      | SyntaxError (pos, s) ->
        this.hooks.SyntaxError (pos, s)
        None
              
  member this.Ask (i:string) =
    match this.ParseInfon i with
      | Some i -> 
        let bind (s:Subst) =
          i.Vars() |> Seq.map (fun v -> { formal = v; actual = s.[v.id].Apply s })
        this.hooks.QueryResults (i, (this.Derive Map.empty i).All |> Seq.map bind)
      | None -> ()
    
  member this.Add i =
    match this.ParseInfon i with
      | Some i -> this.infonstrate <- { ai = this.FakeAI(); infon = i } :: this.infonstrate
      | None -> ()
  
  member this.Invoke a = 
    let wrapped () =
      try a()
      with e ->
        this.hooks.SyntaxError (fakePos, "exception: " + e.Message)
    lock this.pending (fun () -> this.pending.Enqueue (fun () -> wrapped ()))
  
  member this.Step () =
    if this.sql.IsSome then
      let cnt = this.sentItems.Count
      if not this.stepwise then
        this.Talk()
      if cnt = this.sentItems.Count then
        match this.comm.Value.CheckForMessage() with
          | Some msg -> this.Listen msg; true
          | None -> false
      else false
    else false
  
  member this.AsyncDie () = this.Invoke (fun () -> this.die <- true)
  member this.AsyncAsk i = this.Invoke (fun () -> this.Ask i)
  member this.AsyncAdd i = this.Invoke (fun () -> this.Add i)
  member this.AsyncLoad n = this.Invoke (fun () -> this.Load n (File.OpenText n))
  member this.AsyncLoadStream n = this.Invoke (fun () -> this.Load "stdin.dkal" n)
  member this.AsyncStep () = this.Invoke (fun () -> this.Talk ())
  
  member this.Close () =
    this.sql |> Option.iter (fun s -> s.Close())
    this.comm |> Option.iter (fun s -> s.Close())
      
  member this.EventLoop () =
    let doYield () = System.Threading.Thread.Sleep 1000
    let rec loop () =
      let act =
          lock this.pending (fun () -> if this.pending.Count > 0 then Some (this.pending.Dequeue()) else None)
      match act with
          | Some a -> a.Invoke()
          | None ->
            if this.Step () then ()
            else doYield()
      if not this.die then loop ()
    loop ()
    this.Close()


  //
  // Evidential things
  // 
  
  // return true iff signature is the signature of principal under infon
  member private this.SignatureCheck principal infon signature =
    // TODO
    true
  
  member private this.MakeSignature (infon:Infon) =
    // TODO
    Term.Const (Const.Int 42)
    
  member private this.FinalOutcome = function
    | InfonFollows (_, i) -> this.FinalOutcome i
    | i -> i
  
  member private this.IsMe = function
    | Term.Const (Const.Principal p) -> p = this.me.Value
    | _ -> false
  
  member private this.CanSign principal infon =
    match this.FinalOutcome infon with
      | InfonSaid (p, _)
      | InfonImplied (p, _) -> principal = p
      | _ -> false
  
  member private this.EvidenceCheck (acc:Vec<_>) (ev:Term) =
    let ret inf =
      acc.Add inf
      acc.Add (Infon.Cert (inf, ev))
      Some inf
      
    match ev with
      | App (f, [p; inf; sign]) when f === Function.EvSignature ->
        if this.SignatureCheck p inf sign && this.CanSign p inf then
          ret inf
        else
          this.hooks.Warning ("spoofed signature")
          None
      | App (f, [a; b]) when f === Function.EvMp ->
        match this.EvidenceCheck acc a, this.EvidenceCheck acc b with
          | Some i1, Some (InfonFollows (i1', i2)) when i1 = i1' ->
            ret i2
          | _ ->
            this.hooks.Warning ("malformed mp")
            None
      | _ ->
        this.hooks.Warning ("unhandled evidence constructor")
        None                        

  member private this.HandleCertifications = function
    | Assertion.SendTo ({ certified = true } as comm) ->
      let comm =
        match comm.proviso with
          | InfonEmpty ->
            let msg = comm.message
            match this.FinalOutcome comm.message with
               | InfonSaid (p, _)
               | InfonImplied (p, _) when this.IsMe p ->
                 { comm with message = Infon.Cert (msg, App (Function.EvSignature, [p; msg; this.MakeSignature msg])) }
               | _ ->
                 let v = this.FreshVar Type.Evidence
                 let msg = Infon.Cert (msg, Term.Var v)
                 { comm 
                   with message = msg
                        trigger = Infon.And (msg, comm.trigger) }
          | _ ->
            this.hooks.Warning ("certified provisional communication not supported at this time")
            comm
      Assertion.SendTo { comm with certified = false }
    | t -> t
        
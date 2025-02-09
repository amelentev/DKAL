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

namespace Microsoft.Research.Dkal.Executor.Simple

open System.Collections.Generic
open System.Threading
open NLog

open Microsoft.Research.Dkal.Ast
open Microsoft.Research.Dkal.Ast.Infon
open Microsoft.Research.Dkal.Interfaces
open Microsoft.Research.Dkal.Substrate

/// The SimpleExecutor runs an endless loop. On each iteration, every rule is 
/// processed to see if it has to be applied. All necessary changes are saved
/// until the end of the iteration. If the set of changes is consistent, they
/// all get applied and a new iteration starts.
type SimpleExecutor(router: IRouter, 
                    logicEngine: ILogicEngine, 
                    signatureProvider: ISignatureProvider, 
                    infostrate: IInfostrate,
                    mailbox: IMailBox) as se = 
  let log = LogManager.GetLogger("Executor.Simple")
  
  /// The inbox holds the messages that arrive from the router but still
  /// haven't been moved to the mailbox
  let inbox = new Queue<ITerm * ITerm>()

  /// The rules set contains all the current rules that came from installed
  /// policies or were added as a consequence of install actions in other rules
  let rules = new HashSet<ITerm>()
  
  /// Keeps the messages that were sent until a fixed-point is reached, no 
  /// message is sent twice in the same epoch (from fixed-point a to fixed-point
  /// b)
  let sentMessages = new HashSet<ITerm * ITerm>()

  /// Used by the worker thread to wait for messages when there is no processing
  /// to be performed.
  let notEmpty = new AutoResetEvent(false)

  /// The worker thread executes an endless loop of rounds. See se.ExecuteRound
  let mutable worker: option<Thread> = None

  /// Flag that's set to true when the executor needs to stop so that the worker
  /// thread will read it and terminate
  let mutable finish: bool = false

  /// To generate pseudo-unique integer identifiers with 'fresh' keyword
  let random = new System.Random(se.GetHashCode())

  // ---- Callbacks ----

  /// Callback to invoke whenever we reach a fixed-point
  let mutable fixedPointCb: unit -> unit = fun _ -> ()

  /// Callback to invoke whenever we wake up after a fixed-point
  let mutable wakeUpCb: unit -> unit = fun _ -> ()

  /// Callback to invoke whenever an action is about to be executed
  let mutable actionCb: ITerm -> unit = fun _ -> ()

  /// Callback to invoke whenever a message is received
  let mutable receiveCb: ITerm -> ITerm -> unit = fun _ _ -> ()

  /// Callback to invoke whenever an execution round is about to start
  let mutable roundStartCb: unit -> unit = fun _ -> ()

  do
    logicEngine.Infostrate <- infostrate
    logicEngine.SignatureProvider <- signatureProvider
    router.Receive(fun msg from -> 
                      lock inbox (fun () ->
                      if inbox.Count = 0 then 
                        match from with
                        | PrincipalConstant(p) when p <> router.Me -> 
                          wakeUpCb()
                        | _ -> ()
                      inbox.Enqueue((msg, from))
                      receiveCb msg from
                      notEmpty.Set() |> ignore))

  interface IExecutor with
    
    member se.InstallRule (r: ITerm) =
      match r with
      | Forall(_, r) -> (se :> IExecutor).InstallRule r
      | SeqRule(rs) -> List.fold (fun change rule -> (se :> IExecutor).InstallRule rule || change) false rs
      | _ -> rules.Add(r)

    member se.UninstallRule (r: ITerm) =
      match r with
      | Forall(_, r) -> (se :> IExecutor).UninstallRule r
      | SeqRule(rs) -> List.fold (fun change rule -> (se :> IExecutor).UninstallRule rule || change) false rs
      | _ -> rules.Remove(r)

    member se.FixedPointCallback (cb: unit -> unit) = 
      fixedPointCb <- cb
        
    member se.WakeUpCallback (cb: unit -> unit) = 
      wakeUpCb <- cb

    member se.ActionCallback (cb: ITerm -> unit) =
      actionCb <- cb

    member se.ReceiveCallback (cb: ITerm -> ITerm -> unit) = 
      receiveCb <- cb

    member se.RoundStartCallback (cb: unit -> unit) =
      roundStartCb <- cb

    member se.Start () = 
      // Start communications
      router.Start()

      // Start logic engine
      logicEngine.Start()

      // Start worker thread
      finish <- false
      let t = new Thread(se.Work)
      worker <- Some t
      t.Start()

    member se.Stop () =
      // Stop worker thread
      finish <- true
      match worker with 
      | Some worker -> 
        lock inbox (fun () -> notEmpty.Set() |> ignore)
        worker.Join()
      | None -> ()
      
      // Stop engine
      logicEngine.Stop()

      // Stop Communications
      router.Stop()

  member private se.Work() =
    try
      while not finish do
        // Execute a round
        let changing = se.ExecuteRound()

        // If there were no changes clear the sent message set
        // and wait for at least one new message to arrive
        if not(changing) then
          sentMessages.Clear()
          fixedPointCb()
          notEmpty.WaitOne() |> ignore

        // Move messages (if any) to mailbox
        lock inbox (fun () -> 
                    while inbox.Count > 0 do 
                      // TODO instead of moving all messages, add a parameter and 
                      // move only so many at a given round: 
                      // 0 -> no messages are ever used
                      // 1 -> one message at a time
                      // 2 -> up to two messages at a time
                      // ...
                      // all -> all pending messages considered together (current implementation)
                      let msg, from = inbox.Dequeue()
                      log.Debug("{0}: ---- GOT FROM {1}: {2} ---", router.Me, from, msg)
                      mailbox.Add msg from)
    with e -> 
      log.ErrorException("Error at principal " + router.Me, e)
      fixedPointCb()

  member private se.ExecuteRound() =
    log.Debug("{0}: ------------- round start -------------------", router.Me)
    roundStartCb()
    
    // Collects the actions that each rule mandates
    let rec traverse rule = 
      match rule with
      | Rule(condition, action, elseRule) ->
        let actions = Seq.map action.Apply (se.SolveCondition condition [Substitution.Id]) |> Seq.toList
        if Seq.isEmpty actions then 
          traverse elseRule
        else
          actions
      | EmptyRule -> []
      | SeqRule (rules) ->
        Seq.collect traverse rules |> Seq.toList
      | _ -> failwithf "Expecting rule when executing round, found %O" rule

    // Traverse rules
    let actions = Seq.toList <| Seq.collect traverse rules

    // Check consistency and apply changes
    let changed = if ConsistencyChecker.AreConsistentActions actions then
                    se.ApplyActions actions
                  else
                    failwith <| "Found inconsistent set of actions"

    // Prune messages from mail box
    mailbox.Prune()

    // Return true if some change was computed
    log.Debug("{0}: ------------- round END ------------------- {1} changed", router.Me, if changed then "" else "NOT ")
    changed
    
  /// Given a set list of action terms, each of the actions is applied.
  /// Returns true iff at least one of the actions produced a change.
  member private se.ApplyActions (actions: ITerm list) = 
    let substrateUpdates = new HashSet<ISubstrateUpdateTerm>()
    let messagesToSend = new HashSet<ITerm * ITerm>()
    let mutable changed = false
    
    let rec allActions a = 
      match a with
      | SeqAction(actions) -> List.collect allActions actions
      | _ -> [a]

//    let actions = List.toArray <| List.collect allActions actions

    let rec instantiateFresh a =
      match a with
      | SeqAction actions -> 
        let actions = List.toArray actions
        for i in [0..actions.Length-1] do
          match actions.[i] with
          | Fresh(v) ->
            match v with
            | Var(v) -> 
              let s = Substitution.Id.Extend(v, Const(Constant(random.Next())))
              for j in [i+1..actions.Length-1] do
                actions.[j] <- actions.[j].Apply(s)
            | _ -> failwithf "Expecting variable in fresh expression, found %O" v
          | _ -> ()
        SeqAction <| Seq.toList actions
      | _ -> a

    let applyOne a =
      match a with
      | Learn(infon) -> 
                // check for consistency in engines that support negation
                match logicEngine with
                | :? INegLogicEngine as negLogicEngine ->
                    if infostrate.Forget(NotInfon(infon)) ||
                       not (Seq.isEmpty(logicEngine.Derive (NotInfon(infon)) [Substitution.Id])) then
                        failwithf "ERROR when learning %O: engine already knew (or can derive) its negation." infon
                | _ -> ()
                infostrate.Learn infon
      | Relearn(infon) -> match logicEngine with
                          | :? INegLogicEngine as negLogicEngine ->
                                   infostrate.Forget(NotInfon(infon)) |> ignore
                                   if not (Seq.isEmpty(logicEngine.Derive (NotInfon(infon)) [Substitution.Id])) then
                                       failwithf "Cannot relearn %O! I've forgotten its negation, but it is still derivable!" infon
                                   infostrate.Learn infon
                          | _ -> failwithf "Relearning infons not allowed on %s logic engine" (logicEngine.GetType().ToString())
      | Forget(infon) -> infostrate.Forget infon // forgetting may not mean it is not derivable anyway
      | Send(ppal, infon) -> 
        let infon = match infon with 
                    | JustifiedInfon(infon, ev) -> JustifiedInfon(ForallMany(infon.Vars, infon), ev)
                    | infon -> ForallMany(infon.Vars, infon)
        messagesToSend.Add (infon, ppal) |> ignore; false
      | JustifiedSend(ppal, infon) ->
        let infon = ForallMany(infon.Vars, infon)
        let evidence = SignatureEvidence(PrincipalConstant(router.Me), infon, Constant(signatureProvider.ConstructSignature infon router.Me))
        messagesToSend.Add (JustifiedInfon(infon, evidence), ppal) |> ignore; false
      | JustifiedSay(ppal, infon) ->
        let infon = ForallMany(infon.Vars, infon)
        let said = SaidInfon(PrincipalConstant(router.Me), infon)
        let evidence = SignatureEvidence(PrincipalConstant(router.Me), said, Constant(signatureProvider.ConstructSignature said router.Me))
        messagesToSend.Add (JustifiedInfon(said, evidence), ppal) |> ignore; false
      | Install(rule) -> (se :> IExecutor).InstallRule rule
      | Uninstall(rule) -> (se :> IExecutor).UninstallRule rule
      | Apply(su) -> 
        substrateUpdates.Add su |> ignore; false
      | Fresh(v) -> false // it is already instantiated, so no action needed
      | Freeze(rel) -> match logicEngine with
                       | :? INegLogicEngine as negLogicEngine -> negLogicEngine.Freeze rel
                       | _ -> failwithf "Logic engine type %s does not support freezing" (logicEngine.GetType().ToString())
      | _ -> failwithf "Unrecognized action %O" a

    for a in actions do
      let a = instantiateFresh a
      for a in allActions a do
        actionCb(a)
        let changes = applyOne a
        changed <- changed || changes        

    let changedFromSubstrateUpdates = SubstrateDispatcher.Update substrateUpdates
    let changedFromMessages = se.Send messagesToSend
    changed || changedFromSubstrateUpdates || changedFromMessages

  /// Given a condition term and a sequence of substitutions, it returns a subset of
  /// substitutions that satisfy the condition, possibly specialized.
  member private se.SolveCondition (condition: ITerm) (substs: ISubstitution seq) =
    match condition with
    | EmptyCondition -> substs
    | WireCondition(i, p) -> 
      mailbox.Matches i p substs
    | KnownCondition(i) ->
      logicEngine.Derive i substs
    | SeqCondition(conds) ->
      List.fold (fun substs cond -> se.SolveCondition cond substs) substs conds
    | _ -> failwithf "Unrecognized condition %O" condition

  /// Sends the given messages by invoking the IRouter implementation, unless the 
  /// messages have already been sent in this epoch. Messages to the same destination
  /// are grouped into one big message
  member private se.Send (messages: HashSet<ITerm * ITerm>) =
    let needSending = Seq.filter (fun m -> not <| sentMessages.Contains m) messages
    // let groupedByDestination = Seq.groupBy (fun (infon, ppal) -> ppal) needSending
    for (msgs, ppal) in needSending do
      router.Send msgs ppal
    sentMessages.UnionWith needSending
    (needSending |> Seq.toList).IsEmpty |> not

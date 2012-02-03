module Interp
open Types
open Util
open State
open Subst
open Authenticate 
open Crypto 
open Net

type communication = i:infon{(Net.Received i)}
type communications = list communication

type condition =
  | If   : polyterm -> condition
  | Upon : polyterm -> condition
type conditions = list condition
type WFCond :: vars => condition => P =
  | WFCond_If: xs:vars -> i:polyterm 
            -> Typing.polytyping xs i 
            -> WFCond xs (If i)
  | WFCond_Upon: xs:vars -> i:polyterm
            -> Typing.polytyping xs i 
            -> WFCond xs (Upon i)
logic function Vars : polyterm -> vars 
assume forall (xs:vars) (i:term). (Vars (ForallT xs i))=(FreeVars i)
assume forall (xs:vars) (i:term). (Vars (MonoTerm i))=(FreeVars i)
logic function VarsCond : condition -> vars
assume forall (i:polyterm). (VarsCond (If i)) = (Vars i)
assume forall (i:polyterm). (VarsCond (Upon i)) = (Vars i)
logic function VarsConds : conditions -> vars
assume Vars_nil: (VarsConds [])=[]
assume Vars_cons: forall (c:condition) (cs:conditions). (VarsConds (c::cs))=(Append (VarsCond c) (VarsConds cs))
type Binds :: conditions => vars => E
assume (forall (cs:conditions) (xs:vars). Includes (VarsConds cs) xs => Binds cs xs)

type action =
  | Learn : polyterm -> action (* infon *)
  | Drop  : polyterm -> action (* infon *)
  | Add   : polyterm -> action
  | WithFresh : var -> list action -> action
  | Fwd   : term -> polyterm -> action (* prin, infon *)
  | Send  : term -> polyterm -> action (* prin, infon *)
type actions = list action
type WFAct :: vars => action => P =
  | WFAct_Learn: xs:vars -> i:polyterm
              -> Typing.polytyping xs i 
              -> WFAct xs (Learn i)
  | WFAct_Drop : xs:vars -> i:polyterm
              -> Typing.polytyping xs i 
              -> WFAct xs (Drop i)
  | WFAct_Fwd  : xs:vars -> p:term -> i:polyterm
              -> Typing.typing xs p Principal
              -> Typing.polytyping xs i 
              -> WFAct xs (Fwd p i)
  | WFAct_Send : xs:vars -> p:term -> i:polyterm
              -> Typing.typing xs p Principal
              -> Typing.polytyping xs i 
              -> WFAct xs (Send p i)


(* -------------------------------------------------------------------------------- *)
(* Spec: Validity of condition(s) *)
(* -------------------------------------------------------------------------------- *)
logic function CondSubst : condition -> substitution -> condition
assume forall (i:polyterm) (s:substitution). (CondSubst (If i) s) = (If (PolySubst i s))
assume forall (i:polyterm) (s:substitution). (CondSubst (Upon i) s) = (Upon (PolySubst i s))

logic function ActionSubst : action -> substitution -> action
logic function ActionsSubst : actions -> substitution -> actions
assume forall (i:polyterm) (s:substitution). (ActionSubst (Learn i) s) = (Learn (PolySubst i s))
assume forall (i:polyterm) (s:substitution). (ActionSubst (Drop i) s) = (Drop (PolySubst i s))
assume forall (i:polyterm) (s:substitution). (ActionSubst (Add i) s) = (Add (PolySubst i s))
assume forall (x:var) (al:actions) (s:substitution). (ActionSubst (WithFresh x al) s) = (WithFresh x (ActionsSubst al s))
assume forall (p:term) (i:polyterm) (s:substitution). (ActionSubst (Fwd p i) s) = (Fwd (Subst p s) (PolySubst i s))
assume forall (p:term) (i:polyterm) (s:substitution). (ActionSubst (Send p i) s) = (Send (Subst p s) (PolySubst i s))
assume forall (s:substitution). (ActionsSubst [] s) = []
assume forall (a:action) (al:actions) (s:substitution). (ActionsSubst (a::al) s) = ((ActionSubst a s)::(ActionsSubst al s))

type HoldsOne :: substitution => vars => condition => substitution => E
assume Holds_if: forall (xs:vars) (i:polyterm) (substin:substitution) (subst:substitution) 
                         (s:substrate) (k:infostrate). 
         ((Includes xs (Domain subst)) && 
          (HilbertianQIL.polyentails s k [] (PolySubst (PolySubst i substin) subst)))
      => HoldsOne substin xs (If i) subst

assume Holds_upon: forall (xs:vars) (pat:polyterm) (substin:substitution) (subst:substitution) (c:communication). 
         ((Includes xs (Domain subst)) &&
          (exists (c':polyterm). HilbertianQIL.alphaEquiv c c' && ((PolySubst (PolySubst pat substin) subst)=c')))
      => HoldsOne substin xs (Upon pat) subst

type HoldsMany :: vars => list condition => substitution => E
assume forall (xs:vars). HoldsMany xs [] []
assume forall (xs:vars) (c:condition) (cs:conditions) 
               (s0:substitution) (s1:substitution).
          (HoldsMany xs cs s0) && (HoldsOne s0 xs c s1)
         => HoldsMany xs (c::cs) s1
type Holds :: _ = fun (xs:vars) (cs:list condition) (s:substitution) => 
    ((Includes xs (Domain s)) && (Includes (Domain s) xs) && (HoldsMany xs cs s))

type substholdsone :: _ = (fun (xs:vars) (c:condition) (s0:substitution) => (s:substitution{HoldsOne s0 xs c s}))
type substholdsmany :: _ = (fun (xs:vars) (cs:conditions) => (s:substitution{HoldsMany xs cs s}))

(* -------------------------------------------------------------------------------- *)
(* Spec: Enabled actions *)
(* -------------------------------------------------------------------------------- *)
type Enabled :: action => E
assume forall (i:polyterm). ((Enabled (Learn i)) && CheckedInfon i) => Knows i
assume forall (me:principal) (p:principal) (i:polyterm).
          (Enabled (Send (Const (PrincipalConstant p)) i) && IsMe me) 
       => Says me i
assume forall (i:polyterm). (Enabled (Add i)) => Net.Received i

type ruleAction :: _ = (fun (xs:vars) (cs:conditions) => 
                        (a:action{forall (subst:substitution). Holds xs cs subst => (Enabled (ActionSubst a subst))}))
type ruleActions :: _ = (fun (xs:vars) (cs:conditions) => (list (ruleAction xs cs)))

type rule =
  | Rule : xs:vars 
        -> cs:conditions 
        -> list (ruleAction xs cs)
        -> rule
type WFR :: rule => P = 
  | WFR_Rule : xs:vars -> cs:conditions{Binds cs xs} -> acts:ruleActions xs cs
            -> ForallL condition cs  (WFCond xs) 
            -> ForallL (ruleAction xs cs) acts (WFAct xs)
            -> WFR (Rule xs cs acts)
type wfrule = r:rule{WFR r}

(* ================= Pattern matching for polyterms ===================== *)
val match_pattern: tm:polyterm
            -> s1:substitution
            -> upat:vars 
            -> pattern:polyterm
            -> option (s2:substitution{Includes upat (Domain s2) && 
                                       (exists (tm':polyterm). HilbertianQIL.alphaEquiv tm tm' && 
                                         (tm' = (PolySubst (PolySubst pattern s1) s2)))})
let rec match_pattern tm s1 upat pat = 
  let s1pat = polysubst pat s1 in 
    match tm, s1pat with 
      | MonoTerm tm', MonoTerm s1pat' -> 
          (match Unify.doMatch tm' s1 upat s1pat' with 
             | Some s2 -> 
                 let _ = HilbertianQIL.AEQ_Mono tm' in 
                   Some s2
             | _ -> None)
            
      | ForallT _ _, ForallT ys s1pat' -> 
          match InfonLogic.alphaConvertWith tm ys with 
            | Some(((ForallT ys' tm'), aeq)) when ys'=ys -> 
                (match Unify.doMatch tm' s1 upat s1pat' with 
                   | Some s2 when check_disjoint (freeVarsSubst s2) ys' -> 
                       (* assert (InfonLogic.alphaEquiv tm (ForallT ys' tm')); *)
                       (* assert (tm' = (Subst s1pat' s2)); *)
                       (* assert ((PolySubst (PolySubst pat s1) s2) = (ForallT ys' tm')); *)
                       Some s2
                   | _ -> None)
            | _ -> None

(* ================= InfonLogic entailment ===================== *)
val derive: u:vars
          -> goal:polyterm
          -> subst0:substitution
          -> list (substholdsone u (If goal) subst0)
let derive u goal subst0 = 
  let s = State.getSubstrate () in
  let k = State.getInfostrate () in 
  let goal' = polysubst goal subst0 in 
    if (includes u (domain subst0)) (* TODO: push into pre-condition *)
    then match InfonLogic.deriveQuant u s k subst0 goal with
      | None -> []
      | Some ((subst, pf)) -> [subst]
    else []
(* ================= Process state and messaging ===================== *)
val comms : Ref (list communication)
let comms = newref []

val get_communications: unit -> unit
let rec get_communications _unit = 
  let message = Net.receive () in
    match Net.bytes2infon message with 
      | Some infon -> (comms := infon::(!comms)); ()
      | _ -> get_communications ()
          
val dropCommunications: i:polyterm{Enabled (Drop i)} -> bool
let dropCommunications i = 
  let changed, comms' = 
    fold_right (fun (j:communication) (changed, out) -> 
                  if (i:polyterm)=j then (true, out) 
                  else (changed, j::out)) (!comms) (false, []) in 
    (comms := comms');
    changed

val addCommunication: communication -> unit
let addCommunication i = (comms := (i::!comms)); ()

let outbuffer = newref []
let clear_out_buffer () = (outbuffer := []); ()

let dispatch p m = 
  if List_exists (fun (m':msg) -> m'=m) (!outbuffer)
  then false
  else
    ((outbuffer := m::(!outbuffer));
     (Net.send p (Net.msg2bytes m));
     true)

val fwd: p:principal -> i:polyterm{Enabled (Fwd (Const (PrincipalConstant p)) i)} -> bool 
let fwd p i = 
  let me = lookup_me () in 
  let m = Forwarded (me, p, i) in 
    dispatch p m 
  
val send : p:principal -> i:polyterm{Enabled (Send (Const (PrincipalConstant p)) i)} -> bool 
let send p i = 
  let z = lookup_me() in 
  let msg : jmessage = ( (z, p, (i: (i:polyterm{Says z i}))) : (z:principal * p:principal * i:polyterm{Says z i})) in 
  let m = Justified msg in 
    dispatch p m 
                               
(* ========================== Rule engine ==================== *)
val matchComm : comm:communication 
             -> xs:vars -> pat:polyterm 
             -> s0:substitution 
             -> list (substholdsone xs (Upon pat) s0)
let matchComm (comm:communication) xs pat s0 = 
  match match_pattern comm s0 xs pat with
    | None -> []
    | Some s1 -> [s1]

let matchComms xs pat s = collect (fun comm -> matchComm comm xs pat s) (!comms)
  
val evalCond: xs:vars -> s0:substitution -> c:condition -> list (substholdsone xs c s0)
let evalCond xs s0 c = match c with 
  | If pat -> derive xs pat s0
  | Upon pat -> matchComms xs pat s0

val evalConds: xs:vars -> cs:conditions -> list (substholdsmany xs cs)
let rec evalConds xs cs =
  match cs with
    | [] -> ([(emptySubst())]) 
    | c::cstl ->
        let sl' = evalConds xs cstl in
          collect (fun (s0:substitution{HoldsMany xs cstl s0}) ->  (* abbrevs don't work here ... fix *)
                     map (fun (s1:substitution{HoldsOne s0 xs c s1}) -> (s1:substholdsmany xs (c::cstl)))
                       (evalCond xs s0 c))
            sl' 

val holds: xs:vars -> cs:conditions -> list (s:substitution{Holds xs cs s})
let holds xs cs = 
  mapSome (fun (s:substholdsmany xs cs) -> 
             let d = domain s in
               if (includes d xs) && (includes xs d) then Some (s:(s:substitution{Holds xs cs s}))
               else None)          
    (evalConds xs cs)
    
val substAction: s:substitution
              -> a:action 
              -> act:action{act=(ActionSubst a s)}
val substActions: s:substitution
              -> al:actions 
              -> al':actions{al'=(ActionsSubst al s)}
let rec substAction s a =
  match a with
    | Learn i -> Learn (Subst.polysubst i s)
    | Drop i -> Drop (Subst.polysubst i s)
    | Fwd p i -> Fwd (Subst.subst p s) (Subst.polysubst i s)
    | Send p i -> Send (Subst.subst p s) (Subst.polysubst i s)
    | Add i -> Add (Subst.polysubst i s)
    | WithFresh x al -> WithFresh x (substActions s al)
and substActions s = function 
  | [] -> []
  | a::al -> 
      let a' = (substAction s a) in 
      let al' = (substActions s al) in 
        (a':action)::al'

let crev x f = collect f x
let cmap x f = map f x 

val enabledActions : wfrule -> list (a:action{Enabled a})
let enabledActions r = match r with 
  | Rule xs cs acts -> 
      crev acts (fun (a:action{forall (subst:substitution). Holds xs cs subst => (Enabled (ActionSubst a subst))}) -> 
                   cmap (holds xs cs)
                     (fun (s:substitution{Holds xs cs s}) -> 
                         let a':(a':action{Enabled a'}) = substAction s a in  a'))
                           (* assert (Holds xs cs s); *)
                           (* assert (a'=(ActionSubst a s)); *)
                           (* assert (Enabled a'); *)
                           (* a')) *)
        
let allEnabledActions rs = collect enabledActions rs

val asPrincipal: p:term -> q:principal{p=(Const (PrincipalConstant q))}
let asPrincipal = function 
  | Const (PrincipalConstant p) -> p
  | _ -> raise "Unexpected type"

let freshint = 
  let ctr = newref 0 in 
  fun () -> 
    let x = !ctr in 
      (ctr := x + 1);
      x

val applyAction: a:action{Enabled a} -> unit
let rec applyAction a = 
  match a with 
    | Learn i -> 
        if checkInfon i (* should be able to drop this check for well-typed rules. *)
        then State.addToInfostrate (i:infon)
        else ()
    | Drop i -> dropCommunications i; ()
    | Add i -> 
        if checkInfon i 
        then addCommunication i
        else ()
    | Fwd p i -> fwd (asPrincipal p) i; ()
    | Send p i -> send (asPrincipal p) i; ()
    | WithFresh x al' ->
        let c = freshint () in 
        let al' = substActions (mkSubst [x] [(Const (Int c))]) al' in 
          iterate (fun (a:action) -> assume (Enabled a); applyAction a) al'
  
val go: list wfrule -> unit
let rec go rs = 
  let _ = clear_out_buffer () in 
  let _ = get_communications () in (* blocks until new comms arrive *)
  let actions = allEnabledActions rs in 
  let _ = iterate applyAction actions in 
  let _ = print_string (strcat ("Message store: ") (string_of_any (!comms))) in
    go rs

let varsOne c : v:vars{v=(VarsCond c)} = match c with 
  | If (MonoTerm i) -> freeVars i
  | If (ForallT xs i) -> freeVars i
  | Upon (MonoTerm i) -> freeVars i
  | Upon (ForallT xs i) -> freeVars i 

val vars: c:conditions -> v:vars{v=(VarsConds c)}
let rec vars = function 
  | [] -> []
  | c::rest ->  append (varsOne c) (vars rest)
        
val checkWFR: r:rule -> b:bool{b=true => WFR r}
let checkWFR = function 
  | Rule xs cs acts -> 
      let wfCond ys c : option (WFCond ys c) = match c with 
        | If i -> (match Typing.doPolyTyping ys i with 
                     | MkPartial pf -> Some (WFCond_If ys i pf))
        | Upon i -> (match Typing.doPolyTyping ys i with 
                       | MkPartial pf -> Some (WFCond_Upon ys i pf)) in
      let wfAct ys a : option (WFAct ys a) = match a with 
        | Learn i -> (match Typing.doPolyTyping ys i with 
                        | MkPartial pf -> Some (WFAct_Learn ys i pf))
        | Drop i -> (match Typing.doPolyTyping ys i with 
                       | MkPartial pf -> Some (WFAct_Drop ys i pf))
        | Fwd p i -> 
            let (tp, pfp) = Typing.doTyping ys p in 
              if tp=Principal
              then match Typing.doPolyTyping ys i with
                | MkPartial pf -> Some (WFAct_Fwd ys p i pfp pf)
              else None            
        | Send p i -> 
            let (tp, pfp) = Typing.doTyping ys p in 
              if tp=Principal
              then match Typing.doPolyTyping ys i with
                | MkPartial pf -> Some (WFAct_Send ys p i pfp pf)
              else None in
      let wfconds = mapL_p<condition, (WFCond xs)> (wfCond xs) cs in 
      let wfacts = mapL_p<(ruleAction xs cs), (WFAct xs)> (wfAct xs) acts in 
        match wfconds, wfacts with 
          | Some pf1, Some pf2 when includes (vars cs) xs -> 
              let pf = WFR_Rule xs cs acts pf1 pf2 in true
          | _ -> false

val tcrule: rule -> wfrule
let tcrule r = assume (WFR r); r
  (* if checkWFR r  *)
  (* then r  *)
  (* else (assume (WFR r); *)
  (*       r) (\* raise "Ill-typed rule" *\) *)

val run : list rule -> unit
let run rules = go (map tcrule rules)


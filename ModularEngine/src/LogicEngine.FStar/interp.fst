module Interp
open Types
open Util
open Marshall
open State
open Subst

let me = Crypto.lookup_my_credentials () 

type infon = polyterm
type communication = i:infon{Net.Received i}
type communications = list communication

type condition =
  | If   : polyterm -> condition
  | Upon : polyterm -> condition
type conditions = list condition
type WFCond :: vars => condition => P =
  | WFCond_If: xs:vars -> i:infon 
            -> Typing.polytyping xs i 
            -> WFCond xs (If i)
  | WFCond_Upon: xs:vars -> i:infon 
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
  | Fwd   : term -> polyterm -> action (* prin, infon *)
  | Send  : term -> polyterm -> action (* prin, infon *)
type actions = list action
type WFAct :: vars => action => P =
  | WFAct_Learn: xs:vars -> i:infon 
              -> Typing.polytyping xs i 
              -> WFAct xs (Learn i)
  | WFAct_Drop : xs:vars -> i:infon 
              -> Typing.polytyping xs i 
              -> WFAct xs (Drop i)
  | WFAct_Fwd  : xs:vars -> p:term -> i:infon 
              -> Typing.typing xs p Principal
              -> Typing.polytyping xs i 
              -> WFAct xs (Fwd p i)
  | WFAct_Send : xs:vars -> p:term -> i:infon 
              -> Typing.typing xs p Principal
              -> Typing.polytyping xs i 
              -> WFAct xs (Send p i)


(* -------------------------------------------------------------------------------- *)
(* Spec: Validity of condition(s) *)
(* -------------------------------------------------------------------------------- *)
logic function CondSubst : condition -> substitution -> condition
assume forall (i:infon) (s:substitution). (CondSubst (If i) s) = (If (PolySubst i s))
assume forall (i:infon) (s:substitution). (CondSubst (Upon i) s) = (Upon (PolySubst i s))

logic function ActionSubst : action -> substitution -> action
assume forall (i:infon) (s:substitution). (ActionSubst (Learn i) s) = (Learn (PolySubst i s))
assume forall (i:infon) (s:substitution). (ActionSubst (Drop i) s) = (Drop (PolySubst i s))
assume forall (p:term) (i:infon) (s:substitution). (ActionSubst (Fwd p i) s) = (Fwd (Subst p s) (PolySubst i s))
assume forall (p:term) (i:infon) (s:substitution). (ActionSubst (Send p i) s) = (Send (Subst p s) (PolySubst i s))

type HoldsOne :: substitution => vars => condition => substitution => E
assume Holds_if: forall (xs:vars) (i:infon) (substin:substitution) (subst:substitution) 
                         (s:substrate) (k:infostrate). 
         ((Includes xs (Domain subst)) && 
          (InfonLogic.polyentails s k [] (PolySubst (PolySubst i substin) subst)))
      => HoldsOne substin xs (If i) subst

assume Holds_upon: forall (xs:vars) (pat:infon) (substin:substitution) (subst:substitution) (c:communication). 
         ((Includes xs (Domain subst)) &&
          ((PolySubst (PolySubst pat substin) subst)=c))
      => HoldsOne substin xs (Upon pat) subst

type HoldsMany :: vars => list condition => substitution => E
assume forall (xs:vars). HoldsMany xs [] []
assume forall (xs:vars) (c:condition) (cs:conditions) 
               (s0:substitution) (s1:substitution).
          (HoldsMany xs cs s0) && (HoldsOne s0 xs c s1)
         => HoldsMany xs (c::cs) s1
type Holds :: _ = fun (xs:vars) (cs:list condition) (s:substitution) => 
    ((Includes xs (Domain s)) && (Includes (Domain s) xs) && (HoldsMany xs cs s))
(* -------------------------------------------------------------------------------- *)
(* Spec: Enabled actions *)
(* -------------------------------------------------------------------------------- *)
type Enabled :: action => E
assume forall (i:infon). (Enabled (Learn i)) => Knows i
assume forall (me:principal) (p:principal) (i:infon).
          (Enabled (Send (Const (PrincipalConstant p)) i) && Crypto.IsMe me) 
       => SaysTo me p i

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

(* ================= Wrapping external functions ===================== *)
type substholdsone :: _ = (fun (xs:vars) (c:condition) (s0:substitution) => (s:substitution{HoldsOne s0 xs c s}))
type substholdsmany :: _ = (fun (xs:vars) (cs:conditions) => (s:substitution{HoldsMany xs cs s}))
(* type substholdsex  :: _ = (fun (xs:vars) (c:condition) (sl:substitutions) => (s:substitution{exists (s0:substitution). In s0 sl && HoldsOne s0 xs c s})) *)

val derive: u:vars
          -> goal:infon
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
    match Marshall.parseInfon (b2s message) with 
      | Some infon -> comms := infon::(!comms)
      | _ -> get_communications ()
          
val dropCommunications: i:infon{Enabled (Drop i)} -> bool
let dropCommunications i = 
  let changed, comms' = 
    fold_right (fun (j:communication) (changed, out) -> 
                  if (i:infon)=j then (true, out) 
                  else (changed, j::out)) (!comms) (false, []) in 
    (comms := comms');
    changed

let outbuffer = newref []
let clear_out_buffer () = outbuffer := []

let dispatch p m = 
  if List_exists (fun (m':msg) -> m'=m) (!outbuffer)
  then false
  else
    ((outbuffer := m::(!outbuffer));
     (Net.send p (Marshall.msg2bytes m));
     true)

val fwd: p:principal -> i:infon{Enabled (Fwd (Const (PrincipalConstant p)) i)} -> bool 
let fwd p i = 
  let m = Forwarded (me, p, i) in 
    dispatch p m 
  
val send : p:principal -> i:infon{Enabled (Send (Const (PrincipalConstant p)) i)} -> bool 
let send p i = 
  let z : principal = me in 
  let msg : message SaysTo = ( (z, p, (i: (i:infon{SaysTo z p i}))) : (z:principal * p:principal * i:infon{SaysTo z p i})) in 
  let m = Justified msg in 
    dispatch p m 
                               
(* ========================== Rule engine ==================== *)
val matchComm : comm:communication 
             -> xs:vars -> pat:infon 
             -> s0:substitution 
             -> list (substholdsone xs (Upon pat) s0)
let matchComm (comm:communication) xs pat s0 = 
  match Unify.match_pattern comm s0 xs pat with
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
let substAction s a =
  match a with
    | Learn i -> Learn (Subst.polysubst i s)
    | Drop i -> Drop (Subst.polysubst i s)
    | Fwd p i -> Fwd (Subst.subst p s) (Subst.polysubst i s)
    | Send p i -> Send (Subst.subst p s) (Subst.polysubst i s)

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

val applyAction: b:bool -> a:action{Enabled a} -> bool 
let applyAction b a = 
  match a with 
    | Learn i -> State.addToInfostrate i; true
    | Drop i -> dropCommunications i 
    | Fwd p i -> fwd (asPrincipal p) i
    | Send p i -> send (asPrincipal p) i

val until_fix: list wfrule -> unit
let rec until_fix rs = 
  let actions = allEnabledActions rs in
    if fold_left applyAction false actions
    then until_fix rs 
    else ()
  
val go: list wfrule -> unit
let rec go rs = 
  let _ = clear_out_buffer () in 
  let _ = get_communications () in (* blocks until new comms arrive *)
  let _ = until_fix rs in
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
let tcrule r = if checkWFR r then r else raise "Ill-typed rule"

val run : list rule -> unit
let run rules = go (map tcrule rules)


module Test

exception Error of string

let sendMsg (p:string) (msg:string) =
  let bytes = System.Text.Encoding.Unicode.GetBytes msg in
  let _ = Net.send p bytes in
  System.Console.WriteLine("sent " + msg)

let getMsg() =
  let bytes = Net.receive() in
  let msg = System.Text.Encoding.Unicode.GetString(bytes) in
  System.Console.WriteLine("got " + msg)

let _ = 
  let args = System.Environment.GetCommandLineArgs() |> Seq.toList in
  match args with
    | [_;configFile] ->
      if not (System.IO.File.Exists (configFile)) then
        (System.Console.WriteLine("File not found: {0}", configFile); exit(1))
      else Authenticate.readConfig configFile
    | _ -> (System.Console.WriteLine("usage: test config-file-name"); exit(1)) in
  let p = Authenticate.lookup_me() in
  if (p = "org1") then sendMsg "site1" "msg from org1 to site1"
  else if (p = "site1") then
    (getMsg(); sendMsg "phys1" "msg from site1 to phys1")
  else if (p = "phys1") then
    (getMsg(); sendMsg "keyMgr" "msg from phys1 to keyMgr"; getMsg(); Prims.stopAllServers true; ())
  else if (p = "keyMgr") then
    (getMsg(); sendMsg "phys1" "msg keys from keyMgr to phys1")
    

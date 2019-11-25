#r "paket:
nuget Fake.Core.Target
nuget Fake.IO.FileSystem //"
// include Fake modules, see Fake modules section

open System

open Fake.Core
open Fake.IO.FileSystemOperators // </> operator for Path.Combine

let blogRoot = __SOURCE_DIRECTORY__ </> "blog"
let blogContent = blogRoot </> "_public"

let psi exe arg dir (x: ProcStartInfo) : ProcStartInfo =
  { x with
      FileName = exe
      Arguments = arg
      WorkingDirectory = dir }

let run exe dir =
  ShellCommand exe
  |> CreateProcess.fromCommand
  |> CreateProcess.withWorkingDirectory dir
  |> CreateProcess.withTimeout TimeSpan.MaxValue
  |> CreateProcess.ensureExitCode
  |> Proc.run
  |> ignore

type FornaxCmd =
| Clean
| Build
with override this.ToString() =
      match this with
      | Clean -> "fornax clean"
      | Build -> "fornax build"

let fornax (cmd: FornaxCmd) =
  run (cmd.ToString()) blogRoot

// *** Define Targets ***
Target.create "Clean" (fun _ ->
  fornax Clean
)

Target.create "Build" (fun _ ->
  fornax Build
)

Target.create "Deploy" (fun _ ->
  ignore()
  // cd blog/_public # should be its own git repo
  // git add -u
  // git commit -m "update blog content"
  // git push # have to enter ssh passphrase
)

open Fake.Core.TargetOperators

// *** Define Dependencies ***
"Clean"
  ==> "Build"
  ==> "Deploy"

// *** Start Build ***
Target.runOrDefault "Deploy"
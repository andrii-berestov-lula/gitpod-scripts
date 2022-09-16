open System
open System.Diagnostics
open System.IO
open System.Text
open System.Text.RegularExpressions

module Runner =
    let runUntilEnd (cmd: string) (args: string) =
        use proc =
            new Process(StartInfo = ProcessStartInfo(cmd, args, RedirectStandardOutput = true, UseShellExecute = false))

        proc.Start() |> ignore
        let sb = StringBuilder()

        while proc.HasExited |> not do
            sb.Append(proc.StandardOutput.ReadToEnd())
            |> ignore

        sb |> string

    let runInBackground cmd =
        let proc = Process.Start(cmd, "")
        proc.Id


module Git =
    let private git = Runner.runUntilEnd "git"
    let getCurrentBranch () = (git "branch --show-current").Trim()
    let getRemoteName () = (git "remote").Trim()

    let getRemoteOriginUrl () =
        (git $"config --get remote.{getRemoteName ()}.url")
            .Trim()
            .Replace(".git", "")


let openGitPod origin branch =
    let url = $"https://gitpod.io#/{origin}/tree/{branch}"

    use proc =
        new Process(StartInfo = ProcessStartInfo(UseShellExecute = true, FileName = url))

    proc.Start()

let killBackgroundProcessOnCancel (pids: int []) =
    Console.CancelKeyPress.AddHandler (fun _ ea ->
        printfn $"Exiting script because %O{ea.SpecialKey} has been pressed"

        for pid in pids do
            try
                let proc = Process.GetProcessById pid
                printfn $"Killing process with PID: %d{proc.Id}"
                proc.Kill()
            with
            | :? ArgumentException ->
                printfn "Invalid PID provided"
                ())

let runCompanion (toolsDir: string) =
    let printSeparator () =
        printfn $"""%s{String.replicate 78 "#"}"""

    let toolsDir =
        if (toolsDir.EndsWith '/' || toolsDir.EndsWith '\\')
           |> not then
            toolsDir + Path.DirectorySeparatorChar.ToString()
        else
            toolsDir

    let remoteUrl = Git.getRemoteOriginUrl ()
    printfn $"Your remote url is: %s{remoteUrl}"

    let branchName = Git.getCurrentBranch ()
    printfn $"Your current branch name is: %s{branchName}"

    printSeparator ()

    let companionId = Runner.runInBackground $"%s{toolsDir}companion"

    killBackgroundProcessOnCancel [| companionId |]

    openGitPod (Git.getRemoteOriginUrl ()) (Git.getCurrentBranch ())
    |> ignore

    while true do
        ()


let main argv =
    match List.ofArray argv with
    | [] -> runCompanion "./bin/"
    | [ x ] -> runCompanion x
    | x :: xs -> runCompanion x


main (fsi.CommandLineArgs |> Array.tail)

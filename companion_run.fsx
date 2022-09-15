open System
open System.Diagnostics
open System.Text
open System.Text.RegularExpressions

let runCommand (cmd: string) (args:string) =
    use proc = new Process(
        StartInfo = ProcessStartInfo(cmd,args,
            RedirectStandardOutput = true,
            UseShellExecute = false)
        )
    proc.Start() |> ignore
    let sb = StringBuilder()
    while proc.HasExited |> not do
        sb.Append(proc.StandardOutput.ReadToEnd()) |> ignore
    sb |> string
 
let runSimpleCommand cmd =
    let proc = Process.Start(cmd, "")
    proc.Id
    

module Git =
    
    let private git  = runCommand "git"
    let getCurrentBranch () = (git "branch --show-current").Trim()
    let getRemoteName () = (git "remote").Trim()
    let getRemoteOriginUrl () = (git $"config --get remote.{getRemoteName ()}.url").Trim().Replace(".git","")
    
        
let openGitPod origin branch =
    let url = $"https://gitpod.io#/{origin}/tree/{branch}"
    use proc = new Process(
        StartInfo = ProcessStartInfo(
            UseShellExecute = true,
            FileName = url
            ))
    proc.Start()
    
let main argv =    
    let printSeparator () = printfn $"""%s{String.replicate 78 "#"}"""
    let remoteUrl = Git.getRemoteOriginUrl()
    printfn $"Your remote url is: %s{remoteUrl}"
    let branchName = Git.getCurrentBranch()
    printfn $"Your current branch name is: %s{branchName}"
    printSeparator ()
    
    // Run companion 
    let companionId = runSimpleCommand "./bin/companion"
    
    Console.CancelKeyPress.Add(fun arg ->
        printSeparator ()
        printfn "Exiting script"
        let prc = Process.GetProcessById companionId
        printfn $"Retrieved process: {prc}"
        prc.Kill()
        printfn $"Killed companion app with PID: %d{prc.Id}"
        printSeparator()
        arg.Cancel <- true
        )
    printfn $"Companion PID: %d{companionId}"
    openGitPod (Git.getRemoteOriginUrl()) (Git.getCurrentBranch()) |> ignore
    

main();;
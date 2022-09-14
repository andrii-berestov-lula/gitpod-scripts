open System
open System.IO
open System.Runtime.InteropServices

let client = new System.Net.Http.HttpClient()

open type OperatingSystem
open System.Threading.Tasks

type OperatingSystem =
    | Mac
    | Linux
    | Windows
    | Unsupported

type CpuArchitecture =
    | X64
    | X86
    | Arm64
    | Unsupported

type OsType =
    { Name: OperatingSystem
      Architecture: CpuArchitecture }

// TODO: Arm64 mac shows as X86 for some reason
let osInfo: OsType =
    { Name =
        if IsMacOS() then Mac
        elif IsWindows() then Windows
        elif IsLinux() then Linux
        else OperatingSystem.Unsupported
      Architecture =
        match Architecture() with
        | Architecture.Arm64 -> Arm64
        | Architecture.X86 -> X86
        | Architecture.X64 -> X64
        | _ -> Unsupported }

let companionDownloadLink =
    "https://gitpod.io/static/bin/gitpod-local-companion"
    + match osInfo with
      | { Name = Mac; Architecture = Arm64 } -> "-darwin-arm64"
      | { Name = Mac; Architecture = _ } -> "-darwin"
      | { Name = Windows; Architecture = X64 } -> "-windows.exe"
      | { Name = Windows; Architecture = X86 } -> "-windows-386.exe"
      | { Name = Linux; Architecture = Arm64 } -> "-linux-arm64"
      | { Name = Linux; Architecture = _ } -> "-linux"
      | { Name = _; Architecture = _ } -> failwith "companion app is not supported for this platform"

let oathkeeperDownloadLink =
    "https://github.com/ory/oathkeeper/releases/download/v0.39.4/"
    + match osInfo with
      | { Name = Mac; Architecture = Arm64 } -> "oathkeeper_0.39.4-macOS_arm64.tar.gz"
      | { Name = Mac; Architecture = _ } -> "oathkeeper_0.39.4-macOS_64bit.tar.gz"
      | { Name = Windows; Architecture = X64 } -> "oathkeeper_0.39.4-windows_64bit.zip"
      | { Name = Windows; Architecture = X86 } -> "oathkeeper_0.39.4-windows_32bit.zip"
      | { Name = Linux
          Architecture = X64 | X86 } -> "oathkeeper_0.39.4-linux_64bit.tar.gz"
      | { Name = Linux; Architecture = Arm64 } -> "oathkeeper_0.39.4-linux_arm64.tar.gz"
      | { Name = Linux; Architecture = _ } -> "oathkeeper_0.39.4-linux_32bit.tar.gz"
      | { Name = _; Architecture = _ } -> failwith "oathkeeper app is not supported for this platform"

let rec downloadToolTo (fullPath: string) =
    let downloadFileFrom (url: string) =
        task {
            use file = File.OpenWrite(fullPath)
            let! res = client.GetStreamAsync(url)
            do! res.CopyToAsync(file)
        }

    match fullPath with
    | s when s.EndsWith "companion" -> downloadFileFrom companionDownloadLink
    | s when s.EndsWith "oathkeeper" -> downloadFileFrom oathkeeperDownloadLink
    | _ -> failwith "No such tool"

let makeExecutable filepath =
    match osInfo.Name with
    | Windows -> ()
    | _ ->
        let cmd = $"chmod +x {filepath}"

        use proc = System.Diagnostics.Process.Start("/bin/bash", $"-c \"{cmd}\"")

        proc.WaitForExit()
        ()

let extractInto toolsDir (filepath: string) =
    let runCommand (cmd: string) (args: string) =
        use proc = System.Diagnostics.Process.Start(cmd, args)
        proc.WaitForExit()
        ()

    match osInfo.Name with
    | Windows -> runCommand "pwsh.exe" $"--command \"Expand-Archive -Force {filepath} {toolsDir}\""
    | Linux
    | Mac ->
        File.Move(filepath, $"{filepath}.tar")
        let fileName = Path.GetFileName(filepath)
        runCommand "/bin/bash" $"-c \"tar -xvf {filepath}.tar -C {toolsDir} {fileName}\""
        File.Delete($"{filepath}.tar")
    | _ -> failwith "You're in a trouble"

let cleanupIfRequired dir =
    let notExecutable (name:string) = name.EndsWith(".exe") |> not
    match osInfo.Name with
    | Windows -> 
        let a: Collections.Generic.IEnumerable<string> = Directory.EnumerateFiles dir 
        a |> Seq.filter notExecutable |> Seq.iter File.Delete 
    | _ -> printfn "Cleanup not required"

// HOW IT SHOULD LOOK LIKE IN THE END
let installTools toolsDir tools = task {
    printf $"Your OS info: %A{osInfo}\n"

    if not (Directory.Exists toolsDir) then
        Directory.CreateDirectory toolsDir |> ignore

    let fullPathTools = tools |> Array.map ((+) toolsDir)

    let! _ =
        fullPathTools
        |> Array.filter (not << File.Exists)
        |> Array.map downloadToolTo
        |> Task.WhenAll

    fullPathTools
    |> Array.iter (fun path ->
        if   path.Contains("oathkeeper") then 
            extractInto toolsDir path
            cleanupIfRequired toolsDir
        elif path.Contains("companion")  then makeExecutable path)
}

(installTools "./bin/" [| "oathkeeper"; "companion" |]).Wait()

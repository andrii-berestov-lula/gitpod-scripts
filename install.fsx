open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open type OperatingSystem

type System.String with
    member this.EndsWith (chars: char array) =
        chars |> Seq.map (this.EndsWith) |> Seq.forall ((=) true)

// Links for companion app https://www.gitpod.io/blog/local-app
let companionUrlPrefix = "https://gitpod.io/static/bin/gitpod-local-companion"

type DependencyInfo = { Name: string ; Present: bool }
type OperatingSystem = Mac | Linux | Windows | Unsupported
type CpuArchitecture = X64 | X86 | Arm64 | Unsupported
type OsType = { Name: OperatingSystem; Architecture: CpuArchitecture }

let getOSName () =
    if IsMacOS() then Mac
    elif IsWindows() then Windows
    elif IsLinux() then Linux
    else OperatingSystem.Unsupported

// TODO: Arm64 mac shows as X86 for some reason
let getArch (): CpuArchitecture =
    match Architecture() with
    | Architecture.Arm64 -> Arm64
    | Architecture.X86 -> X86
    | Architecture.X64 -> X64
    | _ -> Unsupported

let getOsType () = {
    Name = getOSName ()
    Architecture = getArch ()
}
let private fileExists' name = File.Exists(name)
let toolExists name = {Name=name; Present=fileExists' name}

let prependDir (path:string) filename =  if path.EndsWith([|'/';'\\'|]) then $"{path}{filename}" else $"{path}/{filename}"
let getCompanionDownloadLinkFor osType =
    companionUrlPrefix +
    match osType with
    | {Name = Mac; Architecture = Arm64} -> "-darwin-arm64"
    | {Name= Mac; Architecture=X86|X64}  -> "-darwin"
    | {Name=Windows; Architecture=X64} -> "-windows.exe"
    | {Name=Windows;Architecture=X86} -> "-windows-386.exe"
    | {Name=Linux;Architecture=X64} -> "-linux-arm64"
    | {Name=Linux; Architecture=X86} -> "-linux"
    | {Name=_; Architecture=_} -> failwith "companion app is not supported for this platform"
    |> Uri

let downloadFileTo path name (url: Uri) =
    task {
        use file = File.OpenWrite(path + name )
        use client = new HttpClient()
        let! res = client.GetStreamAsync(url)
        do! res.CopyToAsync(file)
        return path + name
    } |> Async.AwaitTask |> Async.RunSynchronously

let dirPresent dir = Directory.Exists(dir)
let createDir dir = Directory.CreateDirectory(dir) |> ignore

let downloadTool dir osType (tool: DependencyInfo) =
    match tool.Name with
    | "companion" as n -> getCompanionDownloadLinkFor osType |> downloadFileTo dir n
    | "oathkeeper" -> failwith "implement me"
    | _ -> failwith "flow not supported"
    
let isToolMissing tool = tool.Present = true
let makeExecutable (osType:OsType) filepath =
    match osType.Name with
    | Windows -> true
    | _ -> 
        let cmd = $"chmod +x {filepath}"
        use proc = System.Diagnostics.Process.Start("/bin/bash", $"-c \"{cmd}\"")
        proc.WaitForExit()
        proc.ExitCode = 0
 
// HOW IT SHOULD LOOK LIKE IN THE END
let installTools toolsDir tools =
    if not (dirPresent <| toolsDir) then createDir <| toolsDir
    tools
    |> Seq.map (prependDir toolsDir)
    |> Seq.map toolExists
    |> Seq.filter isToolMissing
    |> Seq.map (downloadTool toolsDir <| getOsType ())
    |> Seq.map (makeExecutable <| getOsType())
    |> ignore
    // addDirToPath toolsDir
//     
installTools "./bin/" [|"oathkeeper";"companion"|]
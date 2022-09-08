open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open type OperatingSystem


// Links for companion app https://www.gitpod.io/blog/local-app
// TODO: check if directory exists. If no, create it
let toolsDir = "./bin/"
let requiredTools = [|"oathkeeper";"companion"|]
let companionUrlPrefix = "https://gitpod.io/static/bin/gitpod-local-companion"

type DependencyInfo = { Name: string ; Present: bool }
type OperatingSystem = Mac | Linux | Windows
type CpuArchitecture = X64 | X86 | Arm64 | Unsupported
type OsType = { Name: OperatingSystem; Architecture: CpuArchitecture }

let getOSName () =
    if IsMacOS() then Mac
    elif IsWindows() then Windows
    else Linux

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
let fileExists name = {Name=name; Present=fileExists' name}

let prependDir (path:string) filename = path + filename
let prependToolsDir = prependDir toolsDir

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

let downloadCompanionApp = downloadFileTo toolsDir "companion" <| (getCompanionDownloadLinkFor <| getOsType ())

let makeFileExecutable filePath =
    let cmd = $"chmod +x {filePath}"
    use proc = System.Diagnostics.Process.Start("/bin/bash", $"-c \"{cmd}\"")
    proc.WaitForExit()
    proc.ExitCode = 0
 
// HOW IT SHOULD LOOK LIKE IN THE END
// let installTools toolsDir tools =
//     if not  dirPresent toolsDir then createDir toolsDir
//     tools
//     |> Seq.map (prependDir toolsDir)
//     |> Seq.map isToolPresent
//     |> Seq.filter isToolMissing
//     |> Seq.map downloadTool osType
//     |> Seq.map makeExecutable osType
//     |> ignore
//     addDirToPath toolsDir
//     
// installTools "./bin/" [|"oathkeeper";"companion"|]
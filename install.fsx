#r "nuget: SharpCompress"

open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open type OperatingSystem

open SharpCompress

type OperatingSystem = Mac | Linux | Windows | Unsupported
type CpuArchitecture = X64 | X86 | Arm64 | Unsupported
type OsType = { Name: OperatingSystem; Architecture: CpuArchitecture }

// TODO: Arm64 mac shows as X86 for some reason
let osInfo: OsType  =
    {
        Name = if IsMacOS() then Mac elif IsWindows() then Windows elif IsLinux() then Linux else OperatingSystem.Unsupported
        Architecture =
            match Architecture() with
            | Architecture.Arm64 -> Arm64
            | Architecture.X86 -> X86
            | Architecture.X64 -> X64
            | _ -> Unsupported
    }

let companionDownloadLink =
    "https://gitpod.io/static/bin/gitpod-local-companion" +
    match osInfo with
    | {Name = Mac; Architecture = Arm64} -> "-darwin-arm64"
    | {Name= Mac; Architecture=X86|X64}  -> "-darwin"
    | {Name=Windows; Architecture=X64} -> "-windows.exe"
    | {Name=Windows;Architecture=X86} -> "-windows-386.exe"
    | {Name=Linux;Architecture=X64} -> "-linux-arm64"
    | {Name=Linux; Architecture=X86} -> "-linux"
    | {Name=_; Architecture=_} -> failwith "companion app is not supported for this platform"

let oathkeeperDownloadLink = 
    "https://github.com/ory/oathkeeper/releases/download/v0.39.4/" + 
    match osInfo with
    | {Name = Mac; Architecture = Arm64} -> "oathkeeper_0.39.4-macOS_arm64.tar.gz"
    | {Name = Mac; Architecture = X64 | X86} -> "oathkeeper_0.39.4-macOS_64bit.tar.gz"
    | {Name = Windows; Architecture = X64} -> "oathkeeper_0.39.4-windows_64bit.zip"
    | {Name = Windows; Architecture = X86} -> "oathkeeper_0.39.4-windows_32bit.zip"
    | {Name = Linux; Architecture = X64} -> "oathkeeper_0.39.4-linux_64bit.tar.gz"
    | {Name = Linux; Architecture = X86} -> "oathkeeper_0.39.4-linux_32bit.tar.gz"
    | {Name = _; Architecture = _} -> failwith "oathkeeper app is not supported for this platform"

let rec downloadTool (fullPath:string) =
    let downloadFileTo (url: string) =
        task {
            use file = File.OpenWrite(fullPath)
            use client = new HttpClient()
            let! res = client.GetStreamAsync(url)
            do! res.CopyToAsync(file)
            return fullPath
        }
    
    match fullPath with
    | s when s.EndsWith "companion" -> downloadFileTo companionDownloadLink
    | s when s.EndsWith "oathkeeper" -> downloadFileTo companionDownloadLink
    | _ -> failwith "No such tool"
    
let makeExecutable filepath =
    match osInfo.Name with
    | Windows -> true
    | _ -> 
        let cmd = $"chmod +x {filepath}"
        use proc = System.Diagnostics.Process.Start("/bin/bash", $"-c \"{cmd}\"")
        proc.WaitForExit()
        proc.ExitCode = 0

(*let extract filepath =
    GZipArchive
    SharpCompress.Readers.Tar.TarReader.Open*)

let printPass msg x =
    printf $"%s{msg}: %A{x}\n\n"
    x
 
// HOW IT SHOULD LOOK LIKE IN THE END
let installTools toolsDir tools =
    printf $"Your OS info: %A{osInfo}\n"
    if not (Directory.Exists toolsDir) then Directory.CreateDirectory toolsDir |> ignore
    let fullPathTools = tools |> Seq.map ((+) toolsDir)
        
    fullPathTools
        |> Seq.filter (File.Exists >> not) |> printPass "filtered"
        |> Seq.map (fun f -> downloadTool f |> Async.AwaitTask |> Async.RunSynchronously) |> printPass "downloaded"
        |> ignore
    
    fullPathTools
        |> Seq.map makeExecutable |> printPass "made executable"
        |> ignore
        

installTools "./bin/" [|"oathkeeper"; "companion"|] |> ignore
    
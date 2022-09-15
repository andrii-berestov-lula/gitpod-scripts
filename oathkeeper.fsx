(*
    module UserConfig = 
        let read
        let save
        
    module Ory = 
        let register
        let login
    
    If we call script w/o any argv -> 
        check if .user_config file is present in the current dir
            | Yes -> login
            | No -> 
                    ask for login or register
                    | register -> register
                    | _ -> login
*)

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json

type UserConfig = {
    Email: string
    Password: string
}
module UserConfig =
    let private secureAsk msg =
        printf msg
        let sb = StringBuilder()
        let mutable key = ConsoleKeyInfo()
        while key.Key <> ConsoleKey.Enter do
            key <- Console.ReadKey true
            if key.Key <> ConsoleKey.Enter then  sb.Append key.KeyChar |> ignore
        sb |> string
    let read path =
        File.ReadAllText path |> JsonSerializer.Deserialize<UserConfig>
    let ask () =
        printf "Please provide your username: "
        let username = Console.ReadLine()
        let password =  secureAsk "Please provide your password: "
        { Email = username ; Password = password }
        
    let save path config =
        let ctx = JsonSerializer.Serialize(config)
        File.WriteAllText(path, ctx)

module Ory =
    let private client = new HttpClient()
    let register () = ()
    let login user = ()
let oathkeeperSetup config = () 
let main argv =
    let defaultConfigPath = "./.user_config"
    match List.ofArray argv with
    | [] -> 
            if File.Exists defaultConfigPath
            then oathkeeperSetup (UserConfig.read defaultConfigPath)
            else
                printfn $"Please call `dotnet fsi {fsi.CommandLineArgs.[0]} register`"
                Environment.Exit(1)
    | ["register"] ->
                    if File.Exists defaultConfigPath
                    then File.Delete defaultConfigPath
                    UserConfig.ask () |> UserConfig.save defaultConfigPath
    | _ -> failwith "Flow not supported"
                        
                        
                   
    
 
main (fsi.CommandLineArgs |> Array.tail )
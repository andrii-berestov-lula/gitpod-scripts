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
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json

type UserConfig =
    { Email: string
      Password: string
      KratosUrl: string }

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

module UserConfig =
    let private secureAsk msg =
        printf msg
        let sb = StringBuilder()
        let mutable key = ConsoleKeyInfo()

        while key.Key <> ConsoleKey.Enter do
            key <- Console.ReadKey true

            if key.Key <> ConsoleKey.Enter then
                sb.Append key.KeyChar |> ignore

        printfn ""
        sb |> string

    let read path =
        File.ReadAllText path
        |> JsonSerializer.Deserialize<UserConfig>

    let ask () =
        printf "Please provide your username: "
        let username = Console.ReadLine()
        let password = secureAsk "Please provide your password: "
        printf "Please provide Kratos URL [http://localhost:4433]: "
        let kratosUrl = Console.ReadLine()

        { Email = username
          Password = password
          KratosUrl =
            if kratosUrl.Length > 0 then
                kratosUrl
            else
                "http://localhost:4433" }

    let save path config =
        let ctx = JsonSerializer.Serialize(config)
        File.WriteAllText(path, ctx)
        config

module Ory =
    type SelfServiceResponse = { Id: string }
    let private client = new HttpClient()

    let private getToken (url: string) =
        task {
            let! res = client.GetAsync(url)
            let! resStream = res.Content.ReadAsStreamAsync()

            let! ssr =
                JsonSerializer.DeserializeAsync<SelfServiceResponse>(
                    resStream,
                    JsonSerializerOptions(PropertyNameCaseInsensitive = true)
                )

            return
                match ssr.Id with
                | null -> None
                | x -> Some x
        }

    let private getRegisterToken user =
        getToken $"{user.KratosUrl}/self-service/registration/api"

    let private getLoginToken user =
        getToken $"{user.KratosUrl}/self-service/login/api"

    let private registerUser id user =
        task {
            let url = $"{user.KratosUrl}/self-service/registration?flow={id}"

            let body =
                {| csrf_token = ""
                   method = "password"
                   password = user.Password
                   traits = {| email = user.Email |} |}

            use stream = new MemoryStream()
            do! JsonSerializer.SerializeAsync(stream, body)
            stream.Position <- 0
            use sr = new StreamReader(stream)
            let! body = sr.ReadToEndAsync()
            let body = new StringContent(body)
            body.Headers.ContentType <- MediaTypeHeaderValue("application/json")
            let! res = client.PostAsync(url, body)
            let! resText = res.Content.ReadAsStringAsync()

            return
                if res.StatusCode = HttpStatusCode.OK then
                    Some res
                else
                    printfn $"Registration error response:\n%O{resText}"
                    None
        }

    let private loginUser id user =
        task {
            let url = $"{user.KratosUrl}/self-service/login?flow={id}"

            let body =
                {| csrf_token = ""
                   method = "password"
                   password = user.Password
                   password_identifier = user.Email |}

            use stream = new MemoryStream()
            do! JsonSerializer.SerializeAsync(stream, body)
            stream.Position <- 0
            use sr = new StreamReader(stream)
            let! body = sr.ReadToEndAsync()
            let body = new StringContent(body)
            body.Headers.ContentType <- MediaTypeHeaderValue("application/json")
            let! res = client.PostAsync(url, body)
            let! resText = res.Content.ReadAsStringAsync()

            return
                if res.StatusCode = HttpStatusCode.OK then
                    Some res
                else
                    printfn $"Login error response:\n%O{resText}"
                    None
        }

    let register user =
        try
            match getRegisterToken user
                  |> Async.AwaitTask
                  |> Async.RunSynchronously
                with
            | None ->
                printfn "Could not retrieve flowId"
                Environment.Exit(1)
            | Some x ->
                match registerUser x user
                      |> Async.AwaitTask
                      |> Async.RunSynchronously
                    with
                | None ->
                    printfn $"Could not register user: %s{user.Email}"
                    Environment.Exit(2)
                | Some _ ->
                    printfn $"Successfully registered user: %s{user.Email}"
                    Environment.Exit(0)
        with
        | :? AggregateException ->
            printfn "Gitpod should have gone timeout. Please start it once again in Browser."
            Environment.Exit(3)

    let login user =
        try
            match getLoginToken user
                  |> Async.AwaitTask
                  |> Async.RunSynchronously
                with
            | None ->
                printfn "Could not retrieve flowId"
                Environment.Exit(1)
            | Some x ->
                match loginUser x user
                      |> Async.AwaitTask
                      |> Async.RunSynchronously
                    with
                | None ->
                    printfn $"Could not log in user: %s{user.Email}"
                    Environment.Exit(2)
                | Some _ ->
                    printfn $"Successfully logged in user: %s{user.Email}"
                    Environment.Exit(0)
        with
        | :? AggregateException ->
            printfn "Gitpod should have gone timeout. Please start it once again in Browser."
            Environment.Exit(3)


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

let runOathkeeper (toolsDir: string) (configPath: string) =
    let toolsDir =
        if (toolsDir.EndsWith '/' || toolsDir.EndsWith '\\')
           |> not then
            toolsDir + Path.DirectorySeparatorChar.ToString()
        else
            toolsDir

    let oathkeeperId = Runner.runInBackground $"%s{toolsDir}oathkeeper serve -c %s{configPath}"
    killBackgroundProcessOnCancel [| oathkeeperId |]
    while true do
        ()


let main argv =
    let defaultConfigPath = "./.user_config"

    match List.ofArray argv with
    | [ "register" ] ->
        if File.Exists defaultConfigPath then
            File.Delete defaultConfigPath

        UserConfig.ask ()
        |> UserConfig.save defaultConfigPath
        |> Ory.register
    | [] ->
        if File.Exists defaultConfigPath then
            Ory.login <| UserConfig.read defaultConfigPath
            runOathkeeper "./bin/" "./ory/config/oathkeeper/oathkeeper.yml"
            
        else
            printfn $"Please call `dotnet fsi {fsi.CommandLineArgs.[0]} register`"
            Environment.Exit(1)
    | [ x ] ->
        if File.Exists defaultConfigPath then
            Ory.login <| UserConfig.read defaultConfigPath
            runOathkeeper x "./ory/config/oathkeeper/oathkeeper.yml"
            
        else
            printfn $"Please call `dotnet fsi {fsi.CommandLineArgs.[0]} register`"
            Environment.Exit(1)
    | [x;c] ->
        if File.Exists defaultConfigPath then
            Ory.login <| UserConfig.read defaultConfigPath
            runOathkeeper x c
        else
            printfn $"Please call `dotnet fsi {fsi.CommandLineArgs.[0]} register`"
            Environment.Exit(1)
    | _ -> failwith "Flow not supported"

main (fsi.CommandLineArgs |> Array.tail)

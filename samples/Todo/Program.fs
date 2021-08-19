module Todo.Program

open Falco
open Falco.HostBuilder
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

// ------------
// Register services
// ------------
let configureServices (services : IServiceCollection) =
    services.AddAntiforgery()
            .AddFalco() |> ignore

// ------------
// Activate middleware
// ------------
let configureApp (endpoints : HttpEndpoint list) (app : IApplicationBuilder) =    
    app.UseStaticFiles()       
       .UseFalco(endpoints) |> ignore

// ------------
// Web host
// ------------
let configureWebhost (endpoints : HttpEndpoint list) (webhost : IWebHostBuilder) =
    webhost.ConfigureServices(configureServices)
           .Configure(configureApp endpoints)

[<EntryPoint>]
let main args =        
    try
        webHost args {
            configure configureWebhost

            endpoints [            
                all "/todo/create" 
                    [
                        GET, Todo.Controller.create
                        POST, Todo.Controller.createSubmit
                    ]
                get "/todo/change-status"
                    Todo.Controller.changeStatusSubmit
                get "/" 
                    Todo.Controller.index
            ]
        }           
        0
    with 
    | ex -> 
        printfn "%s\n\n%s" ex.Message ex.StackTrace
        -1
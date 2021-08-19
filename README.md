# Falco

[![NuGet Version](https://img.shields.io/nuget/v/Falco.svg)](https://www.nuget.org/packages/Falco)
[![Build Status](https://travis-ci.org/pimbrouwers/Falco.svg?branch=master)](https://travis-ci.org/pimbrouwers/Falco)

```fsharp
open Falco
open Falco.Routing
open Falco.HostBuilder

webHost [||] {
    endpoints [                    
        get "/" (Response.ofPlainText "Hello World")
    ]
}
```

[Falco](https://github.com/pimbrouwers/Falco) is a toolkit for building fast, functional-first and fault-tolerant web applications using F#.

- Built upon the high-performance primitives of ASP.NET Core.
- Optimized for building HTTP applications quickly.
- Seamlessly integrates with existing .NET Core middleware and frameworks.

## Key Features

- Asynchronous [request handling](#request-handling).
- Simple and powerful [routing](#routing) API.
- Fast, secure and configurable [web server](#host-builder).
- Native F# [view engine](#markup).
- Uniform API for [model binding](#model-binding).
- [Authentication](#authentication) and [security](#security) utilities.
- Built-in support for [large uploads](#handling-large-uploads).

## Design Goals

- Aim to be very small and easily learnable.
- Should be extensible.
- Should provide a toolset to build a working end-to-end web application.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Sample Applications](#sample-applications)
3. [Request Handling](#request-handling)
4. [Routing](#routing)
5. [Model Binding](#model-binding)
6. [JSON](#json)
7. [Markup](#markup)
8. [Host Builder](#host-builder)
9. [Authentication](#authentication)
10. [Security](#security)
11. [Why "Falco"?](#why-falco)
12. [Find a bug?](#find-a-bug)
13. [License](#license)

## Getting Started

### Using `dotnet new`

The easiest way to get started with Falco is by installing the `Falco.Template` package, which adds a new template to your `dotnet new` command line tool:

```cmd
dotnet new -i "Falco.Template::*"
```

Afterwards you can create a new Falco application by running:

```cmd
dotnet new falco -o HelloWorldApp
```

### Manually installing

Create a new F# web project:

```cmd
dotnet new web -lang F# -o HelloWorldApp
```

Install the nuget package:

```cmd
dotnet add package Falco
```

Remove the `Startup.fs` file and save the following in `Program.fs` (if following the manual install path):

```fsharp
module HelloWorld.Program

open Falco
open Falco.Routing
open Falco.HostBuilder

[<EntryPoint>]
let main args =
    webHost args {
        endpoints [ 
            get "/" (Response.ofPlainText "Hello World")
        ]
    }
    0
```

Run the application:

```cmd
dotnet run
```

There you have it, an industrial-strength [Hello World][7] web app, achieved using only base ASP.NET Core libraries. Pretty sweet!

## Sample Applications

Code is always worth a thousand words, so for the most up-to-date usage, the [/samples][6] directory contains a few sample applications.

| Sample | Description |
| ------ | ----------- |
| [Hello World][7] | A basic hello world app |
| [Configure Host][21] | Demonstrating how to configure the `IHost` instance using the `webHost` computation expression |
| [Blog][17] | A basic markdown (with YAML frontmatter) blog |
| [Third-part View Engine][22] | Demonstrating how to render with an external view engine, specifically [Scriban][23] |
| [Falco Journal][20] | A bullet journal built using Falco |

## Request Handling

The `HttpHandler` type is used to represent the processing of a request. It can be thought of as the eventual (i.e. asynchronous) completion and processing of an HTTP request, defined in F# as: `HttpContext -> Task`. Handlers will typically involve some combination of: route inspection, form/query binding, business logic and finally response writing.  With access to the `HttpContext` you are able to inspect all components of the request, and manipulate the response in any way you choose.

Basic request/response handling is divided between the aptly named [`Request`][18] and [`Response`][16] modules, which offer a suite of continuation-passing style (CPS) `HttpHandler` functions for common scenarios.

### Plain Text responses

```fsharp
let textHandler : HttpHandler =
    Response.ofPlainText "hello world"
```

### HTML responses

```fsharp
let htmlHandler : HttpHandler =
    let doc =
        Elem.html [ Attr.lang "en" ] [
            Elem.head [] []
            Elem.body [] [
                Elem.h1 [] [ Text.raw "Sample App" ]                
            ]
        ]

    doc
    |> Response.ofHtml
```

Alternatively, if you're using an external view engine and want to return an HTML response from a string literal, then you can use `Response.ofHtmlString`.

```fsharp
let htmlHandler : HttpHandler = 
    let html = "<html>...</html>"

    html
    |> Response.ofHtmlString
```

### JSON responses

> IMPORTANT: This handler uses the default `System.Text.Json.JsonSerializer`. See [JSON](#json) section below for further information.

```fsharp
type Person =
    { First : string
      Last  : string }

let jsonHandler : HttpHandler =
    { First = "John"; Last = "Doe" }
    |> Response.ofJson
```

### Redirect (301/302) Response

```fsharp
let oldUrlHandler : HttpHandler =
    Response.redirect "/new-url" true
```

> Note: The trailing `bool` value is used to indicate permanency (i.e., true = 301 / false = 302)

## Accessing Request Data

Falco exposes a [uniform API](#model-binding) to obtain typed values from the various sources of request data. Note, the similarity in the various binders below. 

### Route Collection

```fsharp
let helloHandler : HttpHandler =
    let routeBinder (route : RouteCollectionReader) =
        let name = route.GetString "name" "World" 
        sprintf "Hello %s" name
        
    Request.mapRoute routeBinder Response.ofPlainText
```

### Query Parameters

```fsharp
let helloHandler : HttpHandler =
    let queryBinder (query : QueryCollectionReader) =
        let name = query.GetString "name" "World" 
        sprintf "Hello %s" name
        
    Request.mapQuery queryBinder Response.ofPlainText
```

### Form Data

```fsharp
let helloHandler : HttpHandler =
    let formBinder (query : FormCollectionReader) =
        let name = query.GetString "name" "World" 
        sprintf "Hello %s" name
        
    Request.mapForm formBinder Response.ofPlainText
```

To prevent XSS attacks it is often advisable to use a [CSRF token](#security) during form submissions. In these situations, you'll want to validate the token before processing the form input using the `Request.mapFormSecure` (or `Request.bindFormSecure`). These functions will automatically validate the token for you before consuming input.

```fsharp
let secureHelloHandler : HttpHandler =
    let formBinder (query : FormCollectionReader) =
        let name = query.GetString "name" "World" 
        sprintf "Hello %s" name

    let invalidTokenHandler : HttpHandler =
        Response.withStatusCode 403
        >> Resposne.ofEmpty
        
    Request.mapFormSecure formBinder Response.ofPlainText invalidTokenHandler
```

## Response Modifiers

Response modifiers can be thought of as the in-and-out modification of the `HttpResponse`. A preamble to writing and returning. Since these functions receive the `Httpcontext` as input and return it as the only output, they can take advantage of [function compoistion](22).

### Set the status code of the response

```fsharp
let notFoundHandler : HttpHandler =
    Response.withStatusCode 404
    >> Response.ofPlainText "Not found"
```

### Add a header to the response

```fsharp
let handlerWithHeader : HttpHandler =
    Response.withHeader "Content-Language" "en-us"
    >> Response.ofPlainText "Hello world"
```


### Add a cookie to the response 

```fsharp
let handlerWithHeader : HttpHandler =
    Response.withCookie "greeted" "1"
    >> Response.ofPlainText "Hello world"
```

> IMPORTANT: *Do not* use this for authentication. Instead use the `Auth.signIn` and `Auth.signOut` functions found in the [Authentication](#authentication) module.

## Routing

The breakdown of [Endpoint Routing][3] is simple. Associate a specific [route pattern][5] (and optionally an HTTP verb) to an `HttpHandler` which represents the ongoing processing (and eventual return) of a request.

Bearing this in mind, routing can practically be represented by a list of these "mappings" known in Falco as an `HttpEndpoint` which bind together: a route, verb and handler.

```fsharp
let helloHandler : HttpHandler =
    let getMessage (route : RouteCollectionReader) =
        route.GetString "name" "World" 
        |> sprintf "Hello %s"
        
    Request.mapRoute getMessage Response.ofPlainText

let loginHandler : HttpHandler = // ...

let loginSubmitHandler : HttpHandler = // ...  

let endpoints : HttpEndpoint list =
  [
    // a basic GET handler
    get "/hello/{name:alpha}" helloHandler

    // multi-method endpoint
    all "/login"
        [
            POST, loginSubmitHandler
            GET,  loginHandler
        ]
  ]
```

## Model Binding

Reflection-based approaches to binding at IO boundaries work well for simple use cases. But as the complexity of the input rises it becomes error-prone and often involves tedious workarounds. This is especially true for an expressive, algebraic type system like F#. As such, it is often advisable to take back control of this process from the runtime. An added bonus of doing this is that it all but eliminates the need for `[<CLIMutable>]` attributes.

We can make this simpler by creating a succinct API to obtain typed values from `IFormCollection`, `IQueryCollection`, `RouteValueDictionary` and `IHeaderCollection`. _Readers_ for all four exist as derivatives of `StringCollectionReader` which is an abstraction intended to make it easier to work with the string-based key/value collections.

The built-in model binding handlers come in two flavors, both of which are continuation-style handlers:

1. `Request.mapXXX` 
    - Signature: `(map: XXXCollectionReader -> 'a) (next : 'a -> HttpHandler) -> HttpHandler`
    - The "map" family of handlers are more commonly used and assume that binding will always succeed in one manner or another, either via default values or `Option<T>`.

2. `Request.bindXXX`
    - Signature: `(bind: XXXCollectionReader -> Result<'a, 'b>) (handleOk : 'a -> HttpHandler) -> (handleError : 'b -> HttpHandler) -> HttpHandler`
    - The "bind" family of handlers are useful when you explicitly want to indicate errors during binding and return a different response when they occur.

### Route Binding

```fsharp
let mapRouteHandler : HttpHandler =
    let routeMap (r : RouteCollectionReader) = 
        r.GetString "Name" "John Doe"
    
    Request.mapRoute routeMap Response.ofJson

let bindRouteHandler : HttpHandler = 
    let routeBind (r : RouteCollectionReader) =
        match r.TryGetString "Name" with
        | Some name -> Ok name
        | _         -> Error {| Message = "Invalid route" |}
    
    let handleOk = Response.ofJson
    let handleError = Response.ofJson

    Request.bindRoute routeBind handleOk handleError

let manualRouteHandler : HttpHandler =
    fun ctx ->
        let r : RouteCollectionReader = Request.getRoute ctx
        let name = r.GetString "Name" "John Doe"  

        Response.ofJson name ctx
```

### Query Binding

```fsharp
type Person = { FirstName : string; LastName : string }

let mapQueryHandler : HttpHandler =    
    let queryMap (q : QueryCollectionReader) =
        let first = q.GetString "FirstName" "John" // Get value or return default value
        let last = q.GetString "LastName" "Doe"
        { FirstName = first; LastName = last }

    Request.mapQuery queryMap Response.ofJson 

let bindQueryHandler : HttpHandler = 
    let queryBind (q : QueryCollectionReader) =
        match q.TryGetString "FirstName", q.TryGetString "LastName" with
        | Some f, Some l -> Ok { FirstName = f; LastName = l }
        | _  -> Error {| Message = "Invalid query string" |}

    let handleOk = Response.ofJson
    let handleError = Response.ofJson

    Request.bindQuery queryBind handleOk handleError 

let manualQueryHandler : HttpHandler =
    fun ctx ->
        let q : QueryCollectionReader = Request.getQuery ctx
        
        let person = 
            { FirstName = q.GetString "FirstName" "John" // Get value or return default value
              LastName  = q.GetString "LastName" "Doe" }

        Response.ofJson person ctx
```

### Form Binding

The `FormCollectionReader` has full access to the `IFormFilesCollection` via the `_.Files` member.

> Note the addition of `Request.mapFormSecure` and `Request.bindFormSecure` which will automatically validate CSRF tokens for you.

```fsharp
type Person = { FirstName : string; LastName : string }

let mapFormHandler : HttpHandler =   
    let formMap (f : FormCollectionReader) =
        let first = f.GetString "FirstName" "John" // Get value or return default value
        let last = f.GetString "LastName" "Doe"        
        { FirstName = first; LastName = last }

    Request.mapForm formMap Response.ofJson 

let mapFormSecureHandler : HttpHandler =    
    let formMap (f : FormCollectionReader) =
        let first = f.GetString "FirstName" "John" // Get value or return default value
        let last = f.GetString "LastName" "Doe"        
        { FirstName = first; LastName = last }

    let handleInvalidCsrf : HttpHandler = 
        Response.withStatusCode 400 >> Response.ofEmpty

    Request.mapFormSecure formMap Response.ofJson handleInvalidCsrf

let bindFormHandler : HttpHandler = 
    let formBind (f : FormCollectionReader) =
        match f.TryGetString "FirstName", f.TryGetString "LastName" with
        | Some f, Some l -> Ok { FirstName = f; LastName = l }
        | _  -> Error {| Message = "Invalid form data" |}

    let handleOk = Response.ofJson
    let handleError = Response.ofJson

    Request.bindForm formBind handleOk handleError 

let bindFormSecureHandler : HttpHandler = 
    let formBind (f : FormCollectionReader) =
        match f.TryGetString "FirstName", f.TryGetString "LastName" with
        | Some f, Some l -> Ok { FirstName = f; LastName = l }
        | _  -> Error {| Message = "Invalid form data" |}

    let handleOk = Response.ofJson
    let handleError = Response.ofJson
    let handleInvalidCsrf : HttpHandler = 
        Response.withStatusCode 400 >> Response.ofEmpty

    Request.bindFormSecure formBind handleOk handleError handleInvalidCsrf

let manualFormHandler : HttpHandler =
    fun ctx -> task {
        let! f : FormCollectionReader = Request.getForm ctx
        
        let person = 
            { FirstName = f.GetString "FirstName" "John" // Get value or return default value
              LastName = f.GetString "LastName" "Doe" }

        return! Response.ofJson person ctx
    }        
```

#### `multipart/form-data` Binding 

Microsoft defines [large uploads][15] as anything **> 64KB**, which well... is most uploads. Anything beyond this size and they recommend streaming the multipart data to avoid excess memory consumption.

To make this process **a lot** easier Falco provides a set of four `HttpHandler`'s analogous to the form handlers above, which utilize an `HttpContext` extension method called `TryStreamFormAsync()` that will attempt to stream multipart form data, or return an error message indicating the likely problem.

Below is an example demonstrating the insecure map variant:

```fsharp
let imageUploadHandler : HttpHandler =
    let formBinder (f : FormCollectionReader) : IFormFile option =
        f.TryGetFormFile "profile_image"
    
    let uploadImage (profileImage : IFormFile option) : HttpHandler = 
        // Process the uploaded file ...

    // Safely buffer the multipart form submission
    Request.mapFormStream formBinder uploadImage
```

## JSON

Included in Falco are basic JSON in/out handlers, `Request.bindJson` and `Response.ofJson` respectively. Both rely on `System.Text.Json` and thus have minimal support for F#'s algebraic types.

```fsharp
type Person = { FirstName : string; LastName : string }

let jsonHandler : HttpHandler =
    { FirstName = "John"; LastName = "Doe" }
    |> Response.ofJson

let jsonBindHandler : HttpHandler =    
    let handleOk person : HttpHandler = 
        let message = sprintf "hello %s %s" person.First person.Last
        Response.ofPlainText message

    let handleError error : HttpHandler = 
        let message = sprintf "Invalid JSON: %s" error
        Response.withStatusCode 400 >> Response.ofPlainText message

    Request.bindJson handleOk handleError
```

## Markup

A core feature of Falco is the XML markup module. It can be used to produce any form of angle-bracket markup (i.e. HTML, SVG, XML etc.).

For example, the module is easily extended since creating new tags is simple. An example to render `<svg>`'s:

```fsharp
let svg (width : float) (height : float) =
    Elem.tag "svg" [
        Attr.create "version" "1.0"
        Attr.create "xmlns" "http://www.w3.org/2000/svg"
        Attr.create "viewBox" (sprintf "0 0 %f %f" width height)
    ]

let path d = Elem.tag "path" [ Attr.create "d" d ] []

let bars =
    svg 384.0 384.0 [
        path "M368 154.668H16c-8.832 0-16-7.168-16-16s7.168-16 16-16h352c8.832 0 16 7.168 16 16s-7.168 16-16 16zm0 0M368 32H16C7.168 32 0 24.832 0 16S7.168 0 16 0h352c8.832 0 16 7.168 16 16s-7.168 16-16 16zm0 0M368 277.332H16c-8.832 0-16-7.168-16-16s7.168-16 16-16h352c8.832 0 16 7.168 16 16s-7.168 16-16 16zm0 0"
    ]
```

### HTML View Engine

Most of the standard HTML tags & attributes have been built into the markup module and produce objects to represent the HTML node. Nodes are either:

- `Text` which represents `string` values. (Ex: `Text.raw "hello"`, `Text.rawf "hello %s" "world"`)
- `SelfClosingNode` which represent self-closing tags (Ex: `<br />`).
- `ParentNode` which represent typical tags with, optionally, other tags within it (Ex: `<div>...</div>`).

The benefits of using the Falco markup module as an HTML engine include:

- Writing your views in plain F#, directly in your assembly.
- Markup is compiled alongside the rest of your code, leading to improved performance and ultimately simpler deployments.

```fsharp
// Create an HTML5 document using built-in template
let doc = 
    Templates.html5 "en"
        [ Elem.title [] [ Text.raw "Sample App" ] ] // <head></head>
        [ Elem.h1 [] [ Text.raw "Sample App" ] ]    // <body></body>
```

Since views are plain F# they can easily be made strongly-typed:
```fsharp
type Person = { FirstName : string; LastName : string }

let doc (person : Person) = 
    Elem.html [ Attr.lang "en" ] [
        Elem.head [] [                    
            Elem.title [] [ Text.raw "Sample App" ]                                                            
        ]
        Elem.body [] [                     
            Elem.main [] [
                Elem.h1 [] [ Text.raw "Sample App" ]
                Elem.p  [] [ Text.rawf "%s %s" person.First person.Last ]
            ]
        ]
    ]
```

Views can also be combined to create more complex views and share output:

```fsharp
let master (title : string) (content : XmlNode list) =
    Elem.html [ Attr.lang "en" ] [
        Elem.head [] [                    
            Elem.title [] [ Text.raw "Sample App" ]                                                            
        ]
        Elem.body [] content
    ]

let divider = 
    Elem.hr [ Attr.class' "divider" ]

let homeView =
    [
        Elem.h1 [] [ Text.raw "Homepage" ]
        divider
        Elem.p  [] [ Text.raw "Lorem ipsum dolor sit amet, consectetur adipiscing."]
    ]
    |> master "Homepage" 

let aboutView =
    [
        Elem.h1 [] [ Text.raw "About" ]
        divider
        Elem.p  [] [ Text.raw "Lorem ipsum dolor sit amet, consectetur adipiscing."]
    ]
    |> master "About Us"
```

## Host Builder

[Kestrel][1] is the web server at the heart of ASP.NET. It's performant, secure, and maintained by incredibly smart people. Getting it up and running is usually done using `Host.CreateDefaultBuilder(args)`, but it can grow verbose quickly. 

To make things more expressive, Falco exposes an optional computation expression. Below is an example using the builder taken from the [Configure Host][21] sample.

```fsharp
[<EntryPoint>]
let main args = 
    webHost args {
        use_ifnot FalcoExtensions.IsDevelopment HstsBuilderExtensions.UseHsts
        use_https
        use_compression
        use_static_files

        use_if    FalcoExtensions.IsDevelopment DeveloperExceptionPageExtensions.UseDeveloperExceptionPage
        use_ifnot FalcoExtensions.IsDevelopment (FalcoExtensions.UseFalcoExceptionHandler exceptionHandler)

        endpoints [            
            get "/greet/{name:alpha}" 
                handleGreeting

            get "/json" 
                handleJson

            get "/html" 
                handleHtml
                
            get "/" 
                handlePlainText
        ]
    }
    0
```

### Fully Customizing the Host

To assume full control over configuring your `IHost` use the `configure` custom operation. It expects a function with the signature of `HttpEndpoint list -> IWebHostBuilder -> IWebHostBuilder` and assumes you will register and activate Falco (i.e., `AddFalco()` and `UseFalco(endpoints)`).

```fsharp
[<EntryPoint>]
let main args = 
    let configureServices : IServiceCollection -> unit = 
      fun services -> services.AddFalco() |> ignore
    
    let configureApp : HttpEndpoint list -> IApplicationBuilder -> unit =
       fun endpoints app -> app.UseFalco(endpoints) |> ignore

    let configureWebHost : HttpEndpoint list -> IWebHostBuilder =
      fun endpoints webHost ->
          webHost.ConfigureLogging(configureLogging)
                 .ConfigureServices(configureServices)
                 .Configure(configureApp endpoints)

    webHost args {
      configure configureWebHost
      endpoints []
    }
```

## Authentication

ASP.NET Core has amazing built-in support for authentication. Review the [docs][13] for specific implementation details. Falco includes some authentication utilities.

> To use the authentication helpers, ensure the service has been registered (`AddAuthentication()`) with the `IServiceCollection` and activated (`UseAuthentication()`) using the `IApplicationBuilder`.

Prevent user from accessing secure endpoint:

```fsharp
open Falco.Security

let secureResourceHandler : HttpHandler =
    let handleAuth : HttpHandler = 
        "hello authenticated user"
        |> Response.ofPlainText 

    let handleInvalid : HttpHandler =
        Response.withStatusCode 403 
        >> Response.ofPlainText "Forbidden"

    Request.ifAuthenticated handleAuth handleInvalid
```

Prevent authenticated user from accessing anonymous-only end-point:

```fsharp
let anonResourceOnlyHandler : HttpHandler =
    let handleAnon : HttpHandler = 
        Response.ofPlainText "hello anonymous"

    let handleInvalid : HttpHandler = 
        Response.withStatusCode 403 
        >> Response.ofPlainText "Forbidden"

    Request.ifNotAuthenticated handleAnon handleInvalid
```

Allow only user's from a certain group to access endpoint"

```fsharp
let secureResourceHandler : HttpHandler =
    let handleAuthInRole : HttpHandler = 
        Response.ofPlainText "hello admin"

    let handleInvalid : HttpHandler = 
        Response.withStatusCode 403 
        >> Response.ofPlainText "Forbidden"

    // Option 1
    let rolesAllowed = ["Admin"; "SuperUser"]
    Request.ifAuthenticatedInRole rolesAllowed handleAuthInRole handleInvalid

    // Option 2
    let rolesAllowed = Admin | SuperUser
    Request.ifAuthenticatedInRole rolesAllowed handleAuthInRole handleInvalid

     // Option 2b (reduced overhead)
    [<Struct>]
    type rolesAllowed = Admin | SuperUser
        with override this.ToString() =
                match this with
                | Admin     -> "Admin"
                | SuperUser -> "SuperUser"
        
    Request.ifAuthenticatedInRole rolesAllowed handleAuthInRole handleInvalid
```

Allow only user's with a certain scope to access endpoint"

```fsharp
let secureResourceHandler : HttpHandler =
    let handleAuthHasScope : HttpHandler = 
        Response.ofPlainText "user1, user2, user3"

    let handleInvalid : HttpHandler = 
        Response.withStatusCode 403 
        >> Response.ofPlainText "Forbidden"

    let issuer = "https://oauth2issuer.com"
    let scope = "read:users"

    Request.ifAuthenticatedWithScope issuer scope handleAuthHasScope handleInvalid
```

End user session (sign out):

```fsharp
let logOut : HttpHandler =         
    let authScheme = "..."
    let redirectTo = "/login"

    Response.signOutAndRedirect authScheme redirectTo
```

## Security

Cross-site scripting attacks are extremely common since they are quite simple to carry out. Fortunately, protecting against them is as easy as performing them.

The [Microsoft.AspNetCore.Antiforgery][14] package provides the required utilities to easily protect yourself against such attacks.

Falco provides a few handlers via `Falco.Security.Xss`:

> To use the Xss helpers, ensure the service has been registered (`AddAntiforgery()`) with the `IServiceCollection` and activated (`UseAntiforgery()`) using the `IApplicationBuilder`.

```fsharp
open Falco.Markup
open Falco.Security 

let formView token =     
    Elem.html [] [
        Elem.body [] [
            Elem.form [ Attr.method "post" ] [
                Elem.input [ Attr.name "first_name" ]

                Elem.input [ Attr.name "last_name" ]

                // using the CSRF HTML helper
                Xss.antiforgeryInput token

                Elem.input [ Attr.type' "submit"; Attr.value "Submit" ]
            ]                                
        ]
    ]
    
// A handler that demonstrates obtaining a
// CSRF token and applying it to a view
let csrfViewHandler : HttpHandler = 
    formView
    |> Response.ofHtmlCsrf
    
// A handler that demonstrates validating
// the request's CSRF token
let mapFormSecureHandler : HttpHandler =    
    let mapPerson (form : FormCollectionReader) =
        { FirstName = form.GetString "first_name" "John" // Get value or return default value
          LastName = form.GetString "first_name" "Doe" }

    let handleInvalid : HttpHandler = 
        Response.withStatusCode 400 
        >> Response.ofEmpty

    Request.mapFormSecure mapPerson Response.ofJson handleInvalid
```

### Crytography

Many sites have the requirement of a secure log in and sign up (i.e. registering and maintaining a user's database). Thus, generating strong hashes and random salts is important.

Falco helpers are accessed by importing `Falco.Auth.Crypto`.

```fsharp
open Falco.Security

// Generating salt,
// using System.Security.Cryptography.RandomNumberGenerator,
// create a random 16 byte salt and base 64 encode
let salt = Crypto.createSalt 16 

// Generate random int for iterations
let iterations = Crypto.randomInt 10000 50000

// Pbkdf2 Key derivation using HMAC algorithm with SHA256 hashing function
let password = "5upe45ecure"
let hashedPassword = password |> Crypto.sha256 iterations 32 salt
```

## Why "Falco"?

[Kestrel][1] has been a game changer for the .NET web stack. In the animal kingdom, "Kestrel" is a name given to several members of the falcon genus. Also known as "Falco".

## Find a bug?

There's an [issue](https://github.com/pimbrouwers/Falco/issues) for that.

## License

Built with ♥ by [Pim Brouwers](https://github.com/pimbrouwers) in Toronto, ON. Licensed under [Apache License 2.0](https://github.com/pimbrouwers/Falco/blob/master/LICENSE).

[1]: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-3.1 "Kestrel web server implementation in ASP.NET Core"
[2]: https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/ "System.IO.Pipelines: High performance IO in .NET"
[3]: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-3.1#configuring-endpoint-metadata "EndpointRouting in ASP.NET Core"
[4]: https://github.com/giraffe-fsharp/Giraffe "A native functional ASP.NET Core web framework for F# developers."
[5]: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-3.1#route-template-reference
[6]: https://github.com/pimbrouwers/Falco/tree/master/samples
[7]: https://github.com/pimbrouwers/Falco/tree/master/samples/HelloWorld
[8]: https://github.com/pimbrouwers/Falco/tree/master/samples/Blog
[9]: https://en.wikipedia.org/wiki/Function_composition "Function composition"
[10]: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-3.1 "ASP.NET Core Middlware"
[11]: https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/options "F# Options"
[12]: https://wiki.haskell.org/Combinator "Combinator"
[13]: https://docs.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-3.1 "Overview of ASP.NET Core authentication"
[14]: https://docs.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-3.1 "Prevent Cross-Site Request Forgery (XSRF/CSRF) attacks in ASP.NET Core"
[15]: https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-3.1#upload-large-files-with-streaming "Large file uploads"
[16]: https://github.com/pimbrouwers/Falco/tree/master/src/Response.fs
[17]: https://github.com/pimbrouwers/Falco/tree/master/samples/Blog
[18]: https://github.com/pimbrouwers/Falco/tree/master/src/Request.fs
[19]: https://github.com/pimbrouwers/Jay
[20]: https://github.com/pimbrouwers/FalcoJournal
[21]: https://github.com/pimbrouwers/Falco/tree/master/samples/ConfigureHost
[22]: https://github.com/pimbrouwers/Falco/tree/master/samples/ScribanExample
[23]: https://github.com/scriban/scriban
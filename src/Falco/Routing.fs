﻿namespace Falco

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.FileProviders
open Falco.StringUtils

/// Specifies an association of a route pattern to a collection of
/// HttpEndpointHandler.
type HttpEndpoint =
    { Pattern  : string
      Handlers : (HttpVerb * HttpHandler) seq
      Configure : EndpointBuilder -> EndpointBuilder }

module Routing =
    /// Constructor for multi-method HttpEndpoint.
    let all
        (pattern : string)
        (handlers : (HttpVerb * HttpHandler) seq) : HttpEndpoint =
        { Pattern  = pattern
          Handlers = handlers
          Configure = id }

    /// Constructor for a singular HttpEndpoint.
    let route verb pattern handler =
        all pattern [ verb, handler ]

    /// HttpEndpoint constructor that matches any HttpVerb.
    let any pattern handler =
        route ANY pattern handler

    /// GET HttpEndpoint constructor.
    let get pattern handler =
        route GET pattern handler

    /// HEAD HttpEndpoint constructor.
    let head pattern handler =
        route HEAD pattern handler

    /// POST HttpEndpoint constructor.
    let post pattern handler =
        route POST pattern handler

    /// PUT HttpEndpoint constructor.
    let put pattern handler =
        route PUT pattern handler

    /// PATCH HttpEndpoint constructor.
    let patch pattern handler =
        route PATCH pattern handler

    /// DELETE HttpEndpoint constructor.
    let delete pattern handler =
        route DELETE pattern handler

    /// OPTIONS HttpEndpoint constructor.
    let options pattern handler =
        route OPTIONS pattern handler

    /// TRACE HttpEndpoint construct.
    let trace pattern handler =
        route TRACE pattern handler

    /// HttpEndpoint constructor that matches any HttpVerb which maps the route
    /// using the provided `map` function.
    let mapAny pattern map handler =
        any pattern (Request.mapRoute map handler)

    /// GET HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    let mapGet pattern map handler =
        get pattern (Request.mapRoute map handler)

    /// HEAD HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    let mapHead pattern map handler =
        head pattern (Request.mapRoute map handler)

    /// POST HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    let mapPost pattern map handler =
        post pattern (Request.mapRoute map handler)

    /// PUT HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    let mapPut pattern map handler =
        put pattern (Request.mapRoute map handler)

    /// PATCH HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    let mapPatch pattern map handler =
        patch pattern (Request.mapRoute map handler)

    /// DELETE HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    let mapDelete pattern map handler =
        delete pattern (Request.mapRoute map handler)

    /// OPTIONS HttpEndpoint constructor which maps the route using the provided
    /// `map` function.
    let mapOptions pattern map handler =
        options pattern (Request.mapRoute map handler)

    /// TRACE HttpEndpoint construct which maps the route using the provided
    /// `map` function.
    let mapTrace pattern map handler =
        trace pattern (Request.mapRoute map handler)

    /// Configure the display name attribute of the endpoint.
    let setDisplayName (displayName : string) (endpoint : HttpEndpoint) =
        let configure (builder : EndpointBuilder) =
            (builder :?> RouteEndpointBuilder).DisplayName <- displayName
            builder

        { endpoint with Configure = endpoint.Configure >> configure }

    /// Set an explicit order for the endpoint.
    let setOrder (n : int) (endpoint : HttpEndpoint) =
        let configure (builder : EndpointBuilder) =
            (builder :?> RouteEndpointBuilder).Order <- n
            builder

        { endpoint with Configure = endpoint.Configure >> configure }

[<Sealed>]
type FalcoEndpointDataSource(httpEndpoints : HttpEndpoint seq) =
    inherit EndpointDataSource()

    let conventions = List<Action<EndpointBuilder>>()

    new() = FalcoEndpointDataSource([])

    member val FalcoEndpoints = List<HttpEndpoint>()

    override x.Endpoints with get() = x.BuildEndpoints()

    override _.GetChangeToken() = NullChangeToken.Singleton

    member private this.BuildEndpoints () =
        let endpoints = List<Endpoint>()

        for endpoint in Seq.concat [ httpEndpoints; this.FalcoEndpoints ] do
            let routePattern = Patterns.RoutePatternFactory.Parse endpoint.Pattern

            for (verb, handler) in endpoint.Handlers do
                let verbStr = verb.ToString()

                let displayName =
                    if strEmpty verbStr then endpoint.Pattern
                    else strConcat [|verbStr; " "; endpoint.Pattern|]

                let endpointBuilder = RouteEndpointBuilder(
                    requestDelegate = HttpHandler.toRequestDelegate handler,
                    routePattern = routePattern,
                    order = 0,
                    DisplayName = displayName)

                endpointBuilder.DisplayName <- displayName
                endpoint.Configure endpointBuilder |> ignore

                for convention in conventions do
                    convention.Invoke(endpointBuilder)

                let routeNameMetadata = RouteNameMetadata(endpoint.Pattern)
                endpointBuilder.Metadata.Add(routeNameMetadata)

                let httpMethodMetadata =
                    match verb with
                    | ANY -> HttpMethodMetadata [||]
                    | _   -> HttpMethodMetadata [|verbStr|]

                endpointBuilder.Metadata.Add(httpMethodMetadata)

                endpoints.Add(endpointBuilder.Build())

        endpoints

    interface IEndpointConventionBuilder with
        member _.Add(convention: Action<EndpointBuilder>) : unit =
            conventions.Add(convention)

        member _.Finally (_: Action<EndpointBuilder>): unit =
            ()

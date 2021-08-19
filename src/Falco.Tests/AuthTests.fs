module Falco.Tests.Auth

open System.Security.Claims
open Falco
open Falco.Security
open FSharp.Control.Tasks.V2.ContextInsensitive
open FsUnit.Xunit
open Microsoft.AspNetCore.Authentication
open NSubstitute
open Xunit

[<Theory>] 
[<InlineData(false)>]
[<InlineData(true)>]
let ``Auth.isAuthenticated return based on principal authentication`` (isAuthenticated : bool) = 
    let ctx = getHttpContextWriteable isAuthenticated

    Auth.isAuthenticated ctx
    |> should equal isAuthenticated


type myRoles = Admin | Other

[<Struct>]
type fastRoles = SuperUser | RegularUser
    with override this.ToString() =
            match this with
            | SuperUser   -> "SuperUser"
            | RegularUser -> "RegularUser"

type RolesData() as this =
    inherit TheoryData<obj, bool>()
    do  this.Add("Admin", true)
        this.Add("Admin", false)
        this.Add(Admin, false)
        this.Add(Admin, true)
        this.Add(Other, false)
        this.Add(Other, true)
        this.Add(SuperUser, false)
        this.Add(RegularUser, true)

[<Theory>] 
[<ClassData(typeof<RolesData>)>]
let ``Auth.isInRole returns if principal has role`` (role) (inRole : bool) = 
    let ctx = getHttpContextWriteable false
    ctx.User.IsInRole(role.ToString()).Returns(inRole) |> ignore
    Auth.isInRole [role] ctx
    |> should equal inRole

[<Fact>]
let ``Auth.tryFindClaim returns claim if exists`` () =
    let ctx = getHttpContextWriteable true
    let claimType = "email"
    let claimValue = "test@test.com"
    ctx.User.Claims.Returns([Claim(claimType, claimValue)]) |> ignore
    
    let claim = Auth.tryFindClaim (fun c -> c.Type = claimType && c.Value = claimValue) ctx
    claim.IsSome |> should equal true
    
    claim.Value
    |> fun c -> c.Value
    |> should equal claimValue

[<Fact>]
let ``Auth.tryFindClaim returns none claim if does not exist`` () =
    let ctx = getHttpContextWriteable true    
    ctx.User.Claims.Returns([]) |> ignore
    
    let claim = Auth.tryFindClaim (fun c -> c.Value = "falco") ctx
    claim.IsNone |> should equal true

[<Fact>]
let ``Auth.getClaim returns claim if exists`` () =
    let ctx = getHttpContextWriteable true
    let claimValue = "falco"
    ctx.User.Claims.Returns([Claim(ClaimTypes.Name, claimValue)]) |> ignore
    
    let claim = Auth.getClaim ClaimTypes.Name ctx
    claim.IsSome |> should equal true
    
    claim.Value
    |> fun c -> c.Value
    |> should equal claimValue

[<Fact>]
let ``Auth.getClaim returns none claim if does not exist`` () =
    let ctx = getHttpContextWriteable true    
    ctx.User.Claims.Returns([]) |> ignore
    
    let claim = Auth.getClaim ClaimTypes.Name ctx
    claim.IsNone |> should equal true

[<Fact>]
let ``Auth.getClaimValue returns claim value if exists`` () =
    let ctx = getHttpContextWriteable true
    let expected = "falco"
    ctx.User.Claims.Returns([Claim(ClaimTypes.Name, expected)]) |> ignore
    
    let claimValue = Auth.getClaimValue ClaimTypes.Name ctx
    claimValue.IsSome |> should equal true
    
    claimValue.Value   
    |> should equal expected

[<Fact>]
let ``Auth.getClaimValue returns none claim if does not exist`` () =
    let ctx = getHttpContextWriteable true    
    ctx.User.Claims.Returns([]) |> ignore
    
    let claim = Auth.getClaimValue ClaimTypes.Name ctx
    claim.IsNone |> should equal true

[<Fact>]
let ``Auth.hasSCope should return true if scope claim from issuer is found and has specific value`` () =
    let ctx = getHttpContextWriteable true
    let claims = [
        Claim("sub", "123", "str", "issuer1");
        Claim("scope", "read create update delete", "str", "issuer2")
    ]
    ctx.User.Claims.Returns(claims) |> ignore

    Auth.hasScope "issuer2" "update" ctx
    |> should equal true

[<Fact>]
let ``Auth.hasSCope should return false if no claim from issuer is found`` () =
    let ctx = getHttpContextWriteable true
    let claims = [
        Claim("sub", "123", "str", "issuer1");
        Claim("scope", "read create update delete", "str", "issuer2")
    ]
    ctx.User.Claims.Returns(claims) |> ignore

    Auth.hasScope "issuer3" "update" ctx
    |> should equal false

[<Fact>]
let ``Auth.hasSCope should return false if scope claim from issuer is not found`` () =
    let ctx = getHttpContextWriteable true
    let claims = [
        Claim("sub", "123", "str", "issuer1");
        Claim("scope", "read create update delete", "str", "issuer2")
    ]
    ctx.User.Claims.Returns(claims) |> ignore

    Auth.hasScope "issuer1" "update" ctx
    |> should equal false

[<Fact>]
let ``Auth.hasSCope should return false if scope claim from issuer has not specific value`` () =
    let ctx = getHttpContextWriteable true
    let claims = [
        Claim("sub", "123", "str", "issuer1");
        Claim("scope", "read create update delete", "str", "issuer2")
    ]
    ctx.User.Claims.Returns(claims) |> ignore

    Auth.hasScope "issuer2" "manage" ctx
    |> should equal false
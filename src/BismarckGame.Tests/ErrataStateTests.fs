module BismarckGame.Tests.ErrataStateTests

open Xunit
open BismarckGame.Core.Scenarios.BismarckBasicGame
open BismarckGame.Core.Common
open BismarckGame.Tests.TestHelpers

[<Fact>]
let ``17.3 Dorsetshire reinforcement is scheduled at Z10`` () =
    let (_, _, destination) = scenario.PendingReinforcements |> List.find (fun (_, id, _) -> id = ShipId "GBR-CA-Dorsetshire")
    Assert.Equal(coord 'Z' 10, destination)

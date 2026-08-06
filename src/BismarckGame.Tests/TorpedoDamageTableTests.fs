module BismarckGame.Tests.TorpedoDamageTableTests

open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.Tables.TorpedoDamageTable

let private get = function Ok value -> value | Error message -> failwith message

[<Fact>]
let ``Torpedo Damage Table resolves cruiser rows`` () =
    Assert.Equal(Midships(1, Some 5), resolve "Test Cruiser" BismarckGame.Core.Common.HeavyCruiser 1 |> get)
    Assert.Equal(Midships(2, Some 20), resolve "Test Cruiser" BismarckGame.Core.Common.HeavyCruiser 4 |> get)
    Assert.Equal(Sunk, resolve "Test Cruiser" BismarckGame.Core.Common.HeavyCruiser 5 |> get)

[<Fact>]
let ``Torpedo Damage Table resolves battleship and aircraft rows`` () =
    Assert.Equal(Midships(2, Some 8), resolve "Test BB" BismarckGame.Core.Common.Battleship 5 |> get)
    Assert.Equal(Midships(2, Some 12), resolve "Test CV" BismarckGame.Core.Common.AircraftCarrier 4 |> get)
    Assert.Equal(Sunk, resolve "Test CV" BismarckGame.Core.Common.AircraftCarrier 6 |> get)

[<Fact>]
let ``starred targets take one midships hit and half evasion reduction`` () =
    Assert.Equal(Midships(1, Some 3), resolve "King George V" BismarckGame.Core.Common.Battleship 3 |> get)

[<Fact>]
let ``Bismarck and Tirpitz treat torpedo damage as miss`` () =
    Assert.Equal(Miss, resolve "Bismarck" BismarckGame.Core.Common.Battleship 6 |> get)
    Assert.Equal(Miss, resolve "Tirpitz" BismarckGame.Core.Common.Battleship 6 |> get)

[<Fact>]
let ``23.53 British torpedo hit table uses two dice`` () =
    Assert.True(torpedoHit 20 6 |> get)
    Assert.False(torpedoHit 20 7 |> get)

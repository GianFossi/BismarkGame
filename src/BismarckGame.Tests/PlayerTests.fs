module BismarckGame.Tests.PlayerTests

open Xunit
open ROP
open BismarckGame.Core.Common
open BismarckGame.Core.GameState
open BismarckGame.Core.Markers
open BismarckGame.Core.Players
open BismarckGame.Tests.TestHelpers

[<Fact>]
let ``standard roster creates British and German player seats`` () =
    let roster = createRoster ()

    Assert.Equal(British, roster.British.Side)
    Assert.Equal(German, roster.German.Side)

[<Fact>]
let ``player dashboard only includes own units and discovered enemy contacts`` () =
    let roster = createRoster ()
    let state =
        { testState () with
            LocationMarkers =
                [ { Zone = coord 'A' 1
                    RevealedShipClass = Some Battleship
                    Owner = German } ] }

    let british = dashboard state roster.British

    Assert.Single(british.View.OwnShips) |> ignore
    Assert.Equal(British, british.View.OwnShips.Head.Nationality)
    Assert.Single(british.View.RevealedEnemyContacts) |> ignore
    Assert.Equal(Some Battleship, british.View.RevealedEnemyContacts.Head.ShipClass)
    Assert.DoesNotContain(british.View.OwnShips, fun ship -> ship.Nationality = German)

[<Fact>]
let ``player cannot submit commands for opponent ships`` () =
    let roster = createRoster ()
    let state = testState ()

    let result =
        submitCommand unusedTables (constantRoll 3) roster.British (MoveShip(ShipId "GER-1", coord 'A' 2)) state

    match result with
    | Success _ -> failwith "British player should not be allowed to move a German ship"
    | Failure messages -> Assert.Contains(messages, fun message -> message.Contains("cannot issue a German command"))

[<Fact>]
let ``player can submit commands for own ships`` () =
    let roster = createRoster ()
    let state = testState ()

    match submitCommand unusedTables (constantRoll 3) roster.British (MoveShip(ShipId "GBR-1", coord 'C' 2)) state with
    | Failure messages -> failwith (System.String.Join("; ", messages))
    | Success(updated, warnings) ->
        Assert.Empty(warnings)
        Assert.Equal(Some(coord 'C' 2), updated.Players.[British].Ships.[ShipId "GBR-1"].CurrentZone)

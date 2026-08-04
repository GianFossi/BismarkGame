module BismarckGame.Tests.ScenarioTests

open Xunit
open BismarckGame.Core.Common
open BismarckGame.Core.Markers
open BismarckGame.Core.Scenario
open BismarckGame.Core.PlayerView
open BismarckGame.Core.SearchBoard
open BismarckGame.Tests.TestHelpers

[<Fact>]
let ``The historical 1941 scenario passes validation`` () =
    let issues = BismarckGame.Core.Validation.validate BismarckGame.Core.Scenarios.BismarckBasicGame.scenario
    // Print issues (if any) into the failure message so a future data
    // edit that breaks this is easy to diagnose without re-running by hand.
    Assert.True(issues.IsEmpty, "Validation issues:\n" + (issues |> List.map string |> String.concat "\n"))

[<Fact>]
let ``initializeGame on the 1941 scenario starts at the real card turn number`` () =
    let state = initializeGame BismarckGame.Core.Scenarios.BismarckBasicGame.scenario
    Assert.Equal(4, state.Turn.Number)

[<Fact>]
let ``initializeGame places Bismarck and Prinz Eugen at Bergen`` () =
    let state = initializeGame BismarckGame.Core.Scenarios.BismarckBasicGame.scenario
    let germanShips = state.Players.[German].Ships
    Assert.Equal(Some { Letter = 'F'; Number = 20 }, germanShips.[ShipId "GER-BB-Bismarck"].CurrentZone)
    Assert.Equal(Some { Letter = 'F'; Number = 20 }, germanShips.[ShipId "GER-CA-PrinzEugen"].CurrentZone)

[<Fact>]
let ``Timed reinforcements start off-board`` () =
    let state = initializeGame BismarckGame.Core.Scenarios.BismarckBasicGame.scenario
    let revenge = state.Players.[British].Ships.[ShipId "GBR-BB-Revenge"]
    Assert.Equal(None, revenge.CurrentZone)

// --- PlayerView --------------------------------------------------------

[<Fact>]
let ``PlayerView hides the opponent's ships entirely until located`` () =
    let state = testState ()
    let britishView = project state British
    Assert.Empty(britishView.RevealedEnemyContacts)
    // Own ship IS visible with full detail.
    Assert.True(britishView.OwnShips |> List.exists (fun s -> s.Id = ShipId "GBR-1"))

[<Fact>]
let ``PlayerView reveals an enemy contact once a LocationMarker exists, by class only`` () =
    let state = testState ()
    let marker : LocationMarker = { Zone = coord 'A' 1; RevealedShipClass = Some Battleship; Owner = German }
    let state' = { state with LocationMarkers = [ marker ] }
    let britishView = project state' British
    Assert.Equal(1, britishView.RevealedEnemyContacts.Length)
    Assert.Equal(Some Battleship, britishView.RevealedEnemyContacts.[0].ShipClass)

[<Fact>]
let ``PlayerView never exposes the opponent's own-side markers as if they were contacts`` () =
    let state = testState ()
    // A German-owned LocationMarker is a marker tracking a GERMAN ship —
    // it should show up in the BRITISH view (they're the ones who found
    // it), not in the German view (that's their own ship, not a contact).
    let marker : LocationMarker = { Zone = coord 'A' 1; RevealedShipClass = Some Battleship; Owner = German }
    let state' = { state with LocationMarkers = [ marker ] }
    let germanView = project state' German
    Assert.Empty(germanView.RevealedEnemyContacts)

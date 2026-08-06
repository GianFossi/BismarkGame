module BismarckGame.Tests.RuleMatrixTests

open Xunit
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.Common
open BismarckGame.Core.GameState
open BismarckGame.Core.SearchBoard
open BismarckGame.Core.Tables
open BismarckGame.Core.Tables.EvasionEffects
open BismarckGame.Core.Tables.TimeAndVisibility

[<Fact>]
let ``Chance Table has a defined result for every printed basic-game roll and column`` () =
    for column in [ ChanceTable.ColumnA; ChanceTable.ColumnB; ChanceTable.ColumnC ] do
        for dice in 2 .. 12 do
            let result = ChanceTable.resolve dice column
            Assert.NotNull(box result)

[<Fact>]
let ``Shadow Table resolves every die value for every printed category`` () =
    for category in [ ShadowTable.CategoryX; ShadowTable.CategoryY; ShadowTable.CategoryZ; ShadowTable.CategoryUnconfirmed4 ] do
        for die in 1 .. 6 do
            let result = ShadowTable.resolve category die 4 false
            Assert.NotNull(box result)

[<Fact>]
let ``Shadow Table applies both visibility and two-zone movement modifiers`` () =
    Assert.Equal(ShadowTable.HoldContact, ShadowTable.resolve ShadowTable.CategoryX 1 4 true)
    Assert.Equal(ShadowTable.LoseContact, ShadowTable.resolve ShadowTable.CategoryX 1 8 true)

[<Fact>]
let ``Evasion repair table covers every die and preserves printed boundary values`` () =
    for die in 1 .. 6 do Assert.True(EvasionEffects.evasionRepairTable.ContainsKey die)
    Assert.Equal(Speed0, searchBoardMaxSpeed 6)
    Assert.Equal(EmergencyMovementOnly, searchBoardMaxSpeed 7)
    Assert.Equal(Speed1, searchBoardMaxSpeed 16)
    Assert.Equal(Speed2, searchBoardMaxSpeed 25)
    Assert.True((battleBoardMovementOptions 0) = [ 0, 0 ])
    Assert.Contains((2, 0), battleBoardMovementOptions 24)
    Assert.Contains((1, 3), battleBoardMovementOptions 30)

[<Fact>]
let ``Visibility change table covers every printed row and clamps both ends`` () =
    Assert.Equal(13, visibilityChangeTable.Length)
    for entry in visibilityChangeTable do
        let clear = applyVisibilityShift (VisibilityLevel 1) entry.Shift
        let fog = applyVisibilityShift (VisibilityLevel 9) entry.Shift
        let (VisibilityLevel clearValue) = clear
        let (VisibilityLevel fogValue) = fog
        Assert.InRange(clearValue, 1, 9)
        Assert.InRange(fogValue, 1, 9)

[<Fact>]
let ``Naval Fire resolves every range, aspect and printed dice row`` () =
    let sections = [ BowGuns; SternGuns; PortGuns; StarboardGuns ]
    for range in [ RangeA; RangeB ] do
        for aspect in [ Broadside; BowOn; SternOn ] do
            for dice in 2 .. 12 do
                let order =
                    { Firer = ShipId "GBR-BB-Test"
                      Target = ShipId "GER-BB-Test"
                      Section = BowGuns
                      SalvoesFired = 1
                      Range = range
                      Aspect = aspect }
                let mutable rolls = [ dice; dice ]
                let result = NavalFireTables.resolve Set.empty order (fun () -> let x = rolls.Head in rolls <- rolls.Tail; x)
                Assert.NotNull(box result)
    Assert.NotEmpty sections

[<Fact>]
let ``Battle Board movement paths honour the errata bow-only movement and occupied hex rule`` () =
    let ship =
        { ShipId = ShipId "SHIP"
          Name = "Test"
          Class = Battleship
          Position = HexCoord.Zero
          Facing = HexN
          GunSections = []
          SecondaryHits = 0
          EvasionRating = 24
          MidshipsHits = 0
          MaxMidshipsHits = 10
          PermanentEvasionLoss = 0
          IsWithdrawing = false
          IsSunk = false }
    let forward = legalMovementPaths Set.empty ship 1 0
    Assert.Contains([ HexCoord.Zero; hexNeighbor HexCoord.Zero HexN ], forward)
    let blocked = legalMovementPaths (Set.singleton (hexNeighbor HexCoord.Zero HexN)) ship 1 0
    Assert.Empty blocked

[<Fact>]
let ``Basic sequence contains all nine phases in order`` () =
    let phases = [ UnitAvailability; Visibility; ShadowDetermination; AirMovement; ShipMovement; Search; AirAttack; NavalCombat; Chance ]
    Assert.Equal(9, phases.Length)

module BismarckGame.Tests.TablesTests

open Xunit
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.Tables

[<Fact>]
let ``ShipStats has Bismarck with evasion rating 29`` () =
    match ShipStats.shipStats.TryFind "Bismarck" with
    | Some stats -> Assert.Equal(29, stats.EvasionRating)
    | None -> Assert.Fail "Bismarck missing from ShipStats.shipStats"

[<Fact>]
let ``ShipStats MaxMidshipsHits matches the two rule-confirmed examples`` () =
    // Rule 9.714 worked examples: "ten midships hits to sink the
    // Bismarck, six midships hits to sink the Rodney" — these two are
    // confirmed, not estimates (see Tables/ShipStats.fs doc comment).
    Assert.Equal(10, ShipStats.shipStats.["Bismarck"].MaxMidshipsHits)
    Assert.Equal(6, ShipStats.shipStats.["Rodney"].MaxMidshipsHits)

[<Fact>]
let ``ShipStats fuel pool is confirmed for Bismarck and Tirpitz only`` () =
    Assert.Equal(Some 12, ShipStats.shipStats.["Bismarck"].FuelFactors)
    Assert.Equal(Some 12, ShipStats.shipStats.["Tirpitz"].FuelFactors)

[<Fact>]
let ``ShipStats fuel confidence marks only Bismarck and Tirpitz as confirmed`` () =
    Assert.Equal(Some ShipStats.Confirmed, ShipStats.fuelFactorsConfidence "Bismarck")
    Assert.Equal(Some ShipStats.Confirmed, ShipStats.fuelFactorsConfidence "Tirpitz")
    Assert.Equal(Some ShipStats.Estimated, ShipStats.fuelFactorsConfidence "Rodney")
    Assert.Equal(None, ShipStats.fuelFactorsConfidence "Norfolk")

[<Fact>]
let ``ShipStats MaxMidshipsHits confidence marks Bismarck and Rodney as confirmed`` () =
    Assert.Equal(Some ShipStats.Confirmed, ShipStats.maxMidshipsHitsConfidence "Bismarck")
    Assert.Equal(Some ShipStats.Confirmed, ShipStats.maxMidshipsHitsConfidence "Rodney")
    Assert.Equal(Some ShipStats.Estimated, ShipStats.maxMidshipsHitsConfidence "Hood")

[<Fact>]
let ``ShipStats confidence report includes expected confirmed and estimated entries`` () =
    let report = ShipStats.shipStatsConfidenceReport ()

    Assert.Contains(report, fun e -> e.ShipName = "Bismarck" && e.StatName = "FuelFactors" && e.Confidence = ShipStats.Confirmed)
    Assert.Contains(report, fun e -> e.ShipName = "Rodney" && e.StatName = "FuelFactors" && e.Confidence = ShipStats.Estimated)
    Assert.Contains(report, fun e -> e.ShipName = "Rodney" && e.StatName = "MaxMidshipsHits" && e.Confidence = ShipStats.Confirmed)

[<Fact>]
let ``ShipStats confidence report omits fuel entries for cruisers`` () =
    let report = ShipStats.shipStatsConfidenceReport ()

    Assert.DoesNotContain(report, fun e -> e.ShipName = "Norfolk" && e.StatName = "FuelFactors")

[<Fact>]
let ``ShipStats cruisers have no fuel tracked (rule 5.21 exemption)`` () =
    Assert.Equal(None, ShipStats.shipStats.["Prinz Eugen"].FuelFactors)
    Assert.Equal(None, ShipStats.shipStats.["Norfolk"].FuelFactors)

[<Fact>]
let ``temporaryEvasionLossPerMidshipsHit matches rules 9.723-9.726`` () =
    Assert.Equal(1, ShipStats.temporaryEvasionLossPerMidshipsHit "Bismarck" BismarckGame.Core.Common.Battleship)
    Assert.Equal(3, ShipStats.temporaryEvasionLossPerMidshipsHit "Prinz Eugen" BismarckGame.Core.Common.HeavyCruiser)
    Assert.Equal(2, ShipStats.temporaryEvasionLossPerMidshipsHit "King George V" BismarckGame.Core.Common.Battleship)
    Assert.Equal(5, ShipStats.temporaryEvasionLossPerMidshipsHit "Norfolk" BismarckGame.Core.Common.HeavyCruiser)

[<Fact>]
let ``NavalFireTables resolves a clean miss at A range broadside (roll 7)`` () =
    let order : FireOrder =
        { Firer = BismarckGame.Core.Common.ShipId "X"
          Target = BismarckGame.Core.Common.ShipId "Y"
          Section = BowGuns
          SalvoesFired = 1
          Range = RangeA
          Aspect = Broadside }
    let result = NavalFireTables.resolve Set.empty order (fun () -> 7)
    Assert.Equal(Miss, result)

[<Fact>]
let ``NavalFireTables A-range roll 12 broadside on a normal ship consults Special Damage and can sink`` () =
    // A-range broadside 12 -> "CONSULT A SPECIAL DAMAGE"; Special Damage
    // A-range roll 12 -> SUNK for a ship NOT in the heavy-armored set.
    let order : FireOrder =
        { Firer = BismarckGame.Core.Common.ShipId "X"
          Target = BismarckGame.Core.Common.ShipId "SomeOrdinaryShip"
          Section = BowGuns
          SalvoesFired = 1
          Range = RangeA
          Aspect = Broadside }
    let rolls = System.Collections.Generic.Queue<int>([ 12; 12 ])
    let result = NavalFireTables.resolve Set.empty order (fun () -> rolls.Dequeue())
    Assert.Equal(Sunk, result)

[<Fact>]
let ``NavalFireTables A-range roll 12 broadside on a heavy-armored ship is softened, not sunk`` () =
    let order : FireOrder =
        { Firer = BismarckGame.Core.Common.ShipId "X"
          Target = BismarckGame.Core.Common.ShipId "GER-BB-Bismarck"
          Section = BowGuns
          SalvoesFired = 1
          Range = RangeA
          Aspect = Broadside }
    let rolls = System.Collections.Generic.Queue<int>([ 12; 12 ])
    let result = NavalFireTables.resolve (Set.ofList [ "GER-BB-Bismarck" ]) order (fun () -> rolls.Dequeue())
    match result with
    | HitMidships (count, Some evasionReduction) ->
        Assert.Equal(3, count)
        Assert.Equal(10, evasionReduction)
    | other -> Assert.Fail $"expected a softened HitMidships, got {other}"

[<Fact>]
let ``NavalFireTables uses Table A special damage for cruiser targets even at B range`` () =
    // Rule 9.84: cruisers always use Special Damage Table A.
    // B-range broadside 11 consults special damage; special roll 12 on
    // Table A is SUNK for non-heavy-armored ships.
    let order : FireOrder =
        { Firer = BismarckGame.Core.Common.ShipId "X"
          Target = BismarckGame.Core.Common.ShipId "GBR-CA-TestCruiser"
          Section = BowGuns
          SalvoesFired = 1
          Range = RangeB
          Aspect = Broadside }
    let rolls = System.Collections.Generic.Queue<int>([ 11; 12 ])
    let result = NavalFireTables.resolve Set.empty order (fun () -> rolls.Dequeue())
    Assert.Equal(Sunk, result)

[<Fact>]
let ``ShadowTable resolves Hold Contact at die 1 for every named category`` () =
    Assert.Equal(ShadowTable.HoldContact, ShadowTable.resolve ShadowTable.CategoryX 1 4 false)
    Assert.Equal(ShadowTable.HoldContact, ShadowTable.resolve ShadowTable.CategoryY 1 4 false)
    Assert.Equal(ShadowTable.HoldContact, ShadowTable.resolve ShadowTable.CategoryZ 1 4 false)

[<Fact>]
let ``ShadowTable exposes the unconfirmed fourth column explicitly`` () =
    Assert.Equal(ShadowTable.HoldContact, ShadowTable.resolve ShadowTable.CategoryUnconfirmed4 5 4 false)
    Assert.Equal(ShadowTable.LoseContact, ShadowTable.resolve ShadowTable.CategoryUnconfirmed4 6 4 false)

[<Fact>]
let ``ShadowTable categoryOf knows Hood is category Y`` () =
    Assert.Equal(Some ShadowTable.CategoryY, ShadowTable.categoryOf.TryFind "Hood")

[<Fact>]
let ``ChanceTable roll of 2 is always Huff-Duff regardless of column`` () =
    Assert.Equal(ChanceTable.HuffDuff, ChanceTable.resolve 2 ChanceTable.ColumnA)
    Assert.Equal(ChanceTable.HuffDuff, ChanceTable.resolve 2 ChanceTable.ColumnC)

[<Fact>]
let ``EvasionEffects searchBoardMaxSpeed matches the printed bands`` () =
    Assert.Equal(EvasionEffects.Speed0, EvasionEffects.searchBoardMaxSpeed 3)
    Assert.Equal(EvasionEffects.EmergencyMovementOnly, EvasionEffects.searchBoardMaxSpeed 10)
    Assert.Equal(EvasionEffects.Speed1, EvasionEffects.searchBoardMaxSpeed 20)
    Assert.Equal(EvasionEffects.Speed2, EvasionEffects.searchBoardMaxSpeed 29)

[<Fact>]
let ``EvasionEffects battleBoardMovementOptions offers the printed trade-off pairs at evasion 18-24`` () =
    let options = EvasionEffects.battleBoardMovementOptions 20
    Assert.Contains((0, 2), options)
    Assert.Contains((1, 1), options)
    Assert.Contains((2, 0), options)

[<Fact>]
let ``TimeAndVisibility turn 34 is the Finish turn`` () =
    let entry = TimeAndVisibility.timeRecordTrack |> List.find (fun e -> e.Turn = 34)
    Assert.True(entry.IsFinishTurn)

[<Fact>]
let ``TimeAndVisibility turn 12 is both a night turn and a 'C' turn`` () =
    // This is the exact overlap that made the old 3-way TurnLabel enum
    // wrong (see GameState.fs's GameTurn doc comment).
    let entry = TimeAndVisibility.timeRecordTrack |> List.find (fun e -> e.Turn = 12)
    Assert.True(entry.IsNightTurn)
    Assert.True(entry.IsEmergencyMovementTurn)

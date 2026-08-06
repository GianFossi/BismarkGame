module BismarckGame.Tests.ErrataRulesTests

open Xunit
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.Common
open BismarckGame.Core.ErrataRules

[<Fact>]
let ``9.64 assigns before halving`` () =
    Assert.Equal(3, assignAndHalveSalvoes 7 4)
    Assert.Equal(7, assignAndHalveSalvoes 7 3)

[<Fact>]
let ``9.716 direct means the adjacent bow or stern hex only`` () =
    let bow = hexNeighbor HexCoord.Zero HexN
    let stern = hexNeighbor HexCoord.Zero HexS
    let sector = hexNeighbor HexCoord.Zero HexNE
    Assert.Equal((true, false), isDirectBowOrStern HexCoord.Zero bow HexN)
    Assert.Equal((false, true), isDirectBowOrStern HexCoord.Zero stern HexN)
    Assert.Equal((false, false), isDirectBowOrStern HexCoord.Zero sector HexN)

[<Fact>]
let ``9.717 fills the selected secondary side before midships`` () =
    Assert.Equal((1, 0, false), resolveSecondaryHit 0 0 1 1 PortGuns)
    Assert.Equal((0, 1, false), resolveSecondaryHit 0 0 1 1 StarboardGuns)
    Assert.Equal((1, 0, true), resolveSecondaryHit 1 0 1 1 PortGuns)

[<Fact>]
let ``27.52 combines only carrier air units in one task force`` () =
    Assert.True(mayCombineTaskForceAirAttack true true)
    Assert.False(mayCombineTaskForceAirAttack false true)
    Assert.False(mayCombineTaskForceAirAttack true false)

[<Fact>]
let ``9.222 task force speed ignores carriers`` () =
    Assert.Equal(Some 32, taskForceEvasion [ AircraftCarrier, 40; Battleship, 32; LightCruiser, 28 ])
    Assert.Equal(None, taskForceEvasion [ AircraftCarrier, 40 ])

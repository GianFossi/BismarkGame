module BismarckGame.Tests.SubmarineAttackTableTests

open Xunit
open BismarckGame.Core.Tables.SubmarineAttackTable

let private value (result: Result<AttackResult, string>) : AttackResult = match result with | Ok x -> x | Error e -> failwith e

[<Fact>]
let ``submarine table reproduces edge rows and stars`` () =
    Assert.Equal({ Salvoes = 1; EliminationStars = 0 }, resolve 0 0 0 |> value)
    Assert.Equal({ Salvoes = 6; EliminationStars = 2 }, resolve 6 0 0 |> value)
    Assert.Equal({ Salvoes = 1; EliminationStars = 3 }, resolve 6 17 0 |> value)

[<Fact>]
let ``prior submarine losses reduce the die row`` () =
    Assert.Equal(resolve 3 0 0, resolve 4 0 1)

[<Fact>]
let ``defensive fire follows day and night bands and star die`` () =
    Assert.Equal(Ok (LoseOneDestroyer false), resolveDefensiveFire 2)
    Assert.Equal(Ok (LoseOneDestroyer false), resolveDefensiveFire 4)
    Assert.Equal(Ok (LoseOneDestroyer false), resolveDefensiveFire 5)
    Assert.Equal(Ok Miss, resolveDefensiveFire 12)
    Assert.Equal(Ok (LoseOneDestroyer true), resolveDefensiveFireAt true 3 1)
    Assert.Equal(Ok Miss, resolveDefensiveFireAt true 4 1)

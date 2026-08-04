/// <summary>
/// RulesTablesImpl.fs
/// Concrete IRulesTables implementation. Both ResolveNavalFire and
/// ResolveShadow are wired to transcribed data.
/// </summary>
module BismarckGame.Core.Tables.RulesTablesImpl

open BismarckGame.Core.Tables
open BismarckGame.Core.BattleBoard
open BismarckGame.Core.Update

let basicGame : IRulesTables =
    { new IRulesTables with
        member _.ResolveNavalFire(order: FireOrder, rollTwoDice: unit -> int) : FireResult =
            NavalFireTables.resolve NavalFireTables.heavyArmoredShipNames order rollTwoDice

        member _.ResolveShadow(shadowerName: string, visibilityLevel: int, targetMoving2Zones: bool, roll: int) : bool =
            match ShadowTable.categoryOf.TryFind shadowerName with
            | None ->
                // Update.fs's DeclareShadow already checks this before
                // calling here, so reaching this branch means the caller
                // and this table have drifted out of sync — fail closed
                // (no shadow) rather than guessing.
                false
            | Some category ->
                match ShadowTable.resolve category roll visibilityLevel targetMoving2Zones with
                | ShadowTable.HoldContact -> true
                | ShadowTable.LoseContact -> false }

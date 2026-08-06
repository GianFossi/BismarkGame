/// <summary>
/// NavalFireTables.fs
/// Concrete Naval Fire and Special Damage tables transcribed from the
/// physical Bismarck Battleboard (Avalon Hill, 1978/79). The British and
/// German printed tables are numerically identical (they're the same
/// chart printed twice, once per side of the board) — this is the one
/// implementation used by both sides. Source: Battleboard "FIRING AT
/// BROADSIDE / BOW / STERN" tables for A and B range, and the "SPECIAL
/// DAMAGE" sub-table.
/// </summary>
module BismarckGame.Core.Tables.NavalFireTables

open BismarckGame.Core.Common
open BismarckGame.Core.BattleBoard

/// <summary>
/// Raw entry in the main Naval Fire table, before the "consult special
/// damage" indirection is resolved.
/// </summary>
type private MainEntry =
    | MMiss
    | MSecondary
    | MSection of GunSectionType
    | MMidships of int
    | MConsultSpecial

/// <summary>
/// Raw entry in the Special Damage sub-table. Rows 10-12 at A range give
/// King George V / Prince of Wales / Bismarck a lighter outcome than
/// other ships (their heavier armor/compartmentalization) — printed on
/// the board as "3 MIDSHIPS*" / "SUNK**" with a footnote naming those
/// three ships.
/// </summary>
type private SpecialOutcome =
    | SpecialMidships of count: int * evasionReduction: int option
    | SpecialSunk

type private SpecialEntry =
    { Normal: SpecialOutcome
      HeavyArmoredOverride: SpecialOutcome option }

let private rangeBTable : Map<TargetAspect * int, MainEntry> =
    [ Broadside, 2, MMidships 1;       BowOn, 2, MSecondary;        SternOn, 2, MSecondary
      Broadside, 3, MSecondary;        BowOn, 3, MMiss;             SternOn, 3, MMiss
      Broadside, 4, MMiss;             BowOn, 4, MMiss;             SternOn, 4, MMiss
      Broadside, 5, MMiss;             BowOn, 5, MMiss;             SternOn, 5, MMiss
      Broadside, 6, MSection BowGuns;  BowOn, 6, MSection BowGuns;  SternOn, 6, MSection SternGuns
      Broadside, 7, MMiss;             BowOn, 7, MMiss;             SternOn, 7, MMiss
      Broadside, 8, MSection SternGuns; BowOn, 8, MMiss;            SternOn, 8, MMiss
      Broadside, 9, MMiss;             BowOn, 9, MMiss;             SternOn, 9, MMiss
      Broadside, 10, MMiss;            BowOn, 10, MMiss;            SternOn, 10, MMiss
      Broadside, 11, MConsultSpecial;  BowOn, 11, MMidships 1;      SternOn, 11, MMidships 1
      Broadside, 12, MMidships 1;      BowOn, 12, MConsultSpecial;  SternOn, 12, MConsultSpecial ]
    |> List.map (fun (a, d, e) -> (a, d), e)
    |> Map.ofList

let private rangeATable : Map<TargetAspect * int, MainEntry> =
    [ Broadside, 2, MSecondary;        BowOn, 2, MMiss;             SternOn, 2, MMiss
      Broadside, 3, MMidships 2;       BowOn, 3, MSecondary;        SternOn, 3, MSecondary
      Broadside, 4, MSecondary;        BowOn, 4, MSection BowGuns;  SternOn, 4, MSection SternGuns
      Broadside, 5, MSection SternGuns; BowOn, 5, MSection BowGuns; SternOn, 5, MSection SternGuns
      Broadside, 6, MSection BowGuns;  BowOn, 6, MSection BowGuns;  SternOn, 6, MSection SternGuns
      Broadside, 7, MMiss;             BowOn, 7, MMiss;             SternOn, 7, MMiss
      Broadside, 8, MSection BowGuns;  BowOn, 8, MMiss;             SternOn, 8, MMiss
      Broadside, 9, MSection SternGuns; BowOn, 9, MMiss;            SternOn, 9, MMiss
      Broadside, 10, MMidships 1;      BowOn, 10, MMidships 1;      SternOn, 10, MMidships 1
      Broadside, 11, MConsultSpecial;  BowOn, 11, MConsultSpecial;  SternOn, 11, MConsultSpecial
      Broadside, 12, MConsultSpecial;  BowOn, 12, MMidships 2;      SternOn, 12, MMidships 2 ]
    |> List.map (fun (a, d, e) -> (a, d), e)
    |> Map.ofList

let private specialDamageTable : Map<FireRange * int, SpecialEntry> =
    let plain n = { Normal = SpecialMidships(n, None); HeavyArmoredOverride = None }
    [ (RangeB, 2), plain 1
      (RangeB, 3), plain 1
      (RangeB, 4), plain 1
      (RangeB, 5), plain 1
      (RangeB, 6), plain 1
      (RangeB, 7), plain 1
      (RangeB, 8), plain 1
      (RangeB, 9), { Normal = SpecialMidships(2, Some 1); HeavyArmoredOverride = None }
      (RangeB, 10), { Normal = SpecialMidships(2, Some 3); HeavyArmoredOverride = None }
      (RangeB, 11), { Normal = SpecialMidships(2, Some 5); HeavyArmoredOverride = None }
      (RangeB, 12), { Normal = SpecialMidships(2, Some 7); HeavyArmoredOverride = None }
      (RangeA, 2), plain 1
      (RangeA, 3), plain 1
      (RangeA, 4), plain 1
      (RangeA, 5), plain 1
      (RangeA, 6), plain 1
      (RangeA, 7), { Normal = SpecialMidships(1, Some 1); HeavyArmoredOverride = None }
      (RangeA, 8), { Normal = SpecialMidships(2, Some 3); HeavyArmoredOverride = None }
      (RangeA, 9), { Normal = SpecialMidships(2, Some 5); HeavyArmoredOverride = None }
      (RangeA, 10), { Normal = SpecialMidships(3, Some 7); HeavyArmoredOverride = Some(SpecialMidships(2, Some 7)) }
      (RangeA, 11), { Normal = SpecialMidships(3, Some 10); HeavyArmoredOverride = Some(SpecialMidships(2, Some 10)) }
      (RangeA, 12), { Normal = SpecialSunk; HeavyArmoredOverride = Some(SpecialMidships(3, Some 10)) } ]
    |> Map.ofList

/// <summary>
/// King George V, Prince of Wales, and Bismarck get the softened Special
/// Damage outcome (footnote on the printed table). Pass ship IDs, not
/// names, since that's what FireOrder carries.
/// </summary>
let heavyArmoredShipNames = set [ "GER-BB-Bismarck"; "GBR-BB-KingGeorgeV"; "GBR-BB-PrinceOfWales" ]

let private isCruiserId (targetId: string) =
    targetId.Contains("-CA-") || targetId.Contains("-CL-")

/// <summary>
/// Resolves one FireOrder against the printed tables. `rollTwoDice` should
/// return the sum of two six-sided dice (2-12); it may be called a second
/// time internally if the main table result is "consult special damage".
/// </summary>
let resolve (heavyArmored: Set<string>) (order: FireOrder) (rollTwoDice: unit -> int) : FireResult =
    let mainTable = match order.Range with
                    | RangeB -> rangeBTable
                    | RangeA -> rangeATable
                    | OutOfRange -> Map.empty

    match mainTable.TryFind(order.Aspect, rollTwoDice ()) with
    | None -> Miss   // out of range, or (defensively) an unmapped roll
    | Some MMiss -> Miss
    | Some MSecondary -> HitSecondary
    | Some(MSection s) -> HitSection s
    | Some(MMidships n) -> HitMidships(n, None)
    | Some MConsultSpecial ->
        let (ShipId targetId) = order.Target
        let specialDamageRange = if isCruiserId targetId then RangeA else order.Range
        match specialDamageTable.TryFind(specialDamageRange, rollTwoDice ()) with
        | None -> HitMidships(1, None)   // defensive default; should not happen for A/B ranges
        | Some entry ->
            let outcome =
                if heavyArmored.Contains targetId then
                    entry.HeavyArmoredOverride |> Option.defaultValue entry.Normal
                else
                    entry.Normal
            match outcome with
            | SpecialMidships(n, red) -> HitMidships(n, red)
            | SpecialSunk -> Sunk

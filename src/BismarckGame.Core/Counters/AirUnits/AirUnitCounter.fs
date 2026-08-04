namespace BismarckGame.Core.Counters.AirUnits

open BismarckGame.Core.Common
open BismarckGame.Core.Counters

/// Common behavior for every air unit counter (rule 2.43).
[<AbstractClass>]
type AirUnitCounter
    (
        id,
        name,
        nationality,
        enduranceRating: int,
        searchStrengthDay: int,
        searchStrengthNight: int
    ) =
    inherit Counter(id, name, nationality)

    member _.EnduranceRating = enduranceRating
    member _.SearchStrengthDay = searchStrengthDay
    member _.SearchStrengthNight = searchStrengthNight

    /// <summary>
    /// LR recon units "can never launch an air attack" (rule 2.434).
    /// True by default for the bomber types; overridden false below.
    /// </summary>
    abstract member CanAttack: bool
    default _.CanAttack = true

    abstract member ClassCode: string

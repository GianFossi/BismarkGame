namespace BismarckGame.Core.Counters.Ships

open BismarckGame.Core.Common
open BismarckGame.Core.Counters

/// Common behavior for every ship counter (rule 2.42). Evasion rating and
/// search strength stay per-instance constructor parameters — they vary
/// ship-by-ship even within one class (compare Hood's evasion 29 against
/// Ramillies' 19, both Battleship-family; see Tables/ShipStats.fs for the
/// real printed values). What each concrete class below fixes is the
/// behavior that follows purely from being that CLASS of ship, regardless
/// of which individual ship it is.
[<AbstractClass>]
type ShipCounter
    (
        id,
        name,
        nationality,
        evasionRating: int,
        searchStrengthDay: int,
        searchStrengthNight: int
    ) =
    inherit Counter(id, name, nationality)

    member _.EvasionRating = evasionRating
    member _.SearchStrengthDay = searchStrengthDay
    member _.SearchStrengthNight = searchStrengthNight

    /// <summary>
    /// Aircraft carriers are the one ship type with no patrol face on
    /// their counter (rule 2.423: "Aircraft carriers do not have a
    /// patrol mode. They cannot patrol."). True for every other class.
    /// </summary>
    abstract member CanPatrol: bool
    default _.CanPatrol = true

    /// <summary>
    /// "Aircraft Carriers cannot shadow" (Shadow Table card, Basic Game
    /// Tables Card). True for every other class by default.
    /// </summary>
    abstract member CanShadow: bool
    default _.CanShadow = true

    /// <summary>
    /// Two-letter/letter+number recognition code as used in the manual's
    /// class list (rule 2.422) — e.g. "BB", "CA". Concrete classes must
    /// supply this; it has no sensible default.
    /// </summary>
    abstract member ClassCode: string

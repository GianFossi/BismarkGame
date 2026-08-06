/// <summary>
/// Configuration.fs
/// Strongly typed runtime options and storage configuration records.
/// These are pure data shapes; persistence is implemented outside the
/// core engine so Update.fs remains rule-focused and side-effect free.
/// </summary>
module BismarckGame.Core.Configuration

/// <summary>
/// Runtime options that influence how a host app runs the engine.
/// </summary>
[<CLIMutable>]
type GameOptions = { ScenarioId: string; UseFixedSeed: bool; RandomSeed: int; AutoSimulateTurns: int; AutoSaveEnabled: bool; AutoSaveEveryTurns: int; EnableEventLogging: bool }

/// <summary>
/// XML serializer behavior options for persistence helpers.
/// </summary>
[<CLIMutable>]
type XmlPersistenceOptions =
    { IndentOutput: bool
      OmitXmlDeclaration: bool
      EncodingName: string }

/// <summary>
/// Host-app paths and filenames used by persistence services.
/// </summary>
[<CLIMutable>]
type StoragePaths = { RootPath: string; SaveDirectory: string; LogsDirectory: string; DatabasePath: string; ImagesDirectory: string; XmlDirectory: string; OptionsFileName: string; ConfigurationFileName: string; GameStatusFileName: string; EventLogFileName: string }

/// <summary>
/// Top-level app configuration that groups storage paths and XML options.
/// </summary>
[<CLIMutable>]
type AppConfiguration =
    { Paths: StoragePaths
      Xml: XmlPersistenceOptions
      LastScenarioId: string
      LastStatusFilePath: string }

/// <summary>
/// Baseline game options used when no options file exists yet.
/// </summary>
let defaultGameOptions : GameOptions =
    { ScenarioId = "bismarck-1941-basic"
      UseFixedSeed = true
      RandomSeed = 42
      AutoSimulateTurns = 0
      AutoSaveEnabled = false
      AutoSaveEveryTurns = 1
      EnableEventLogging = false }

/// <summary>
/// Baseline XML persistence options used when no config file exists yet.
/// </summary>
let defaultXmlPersistenceOptions : XmlPersistenceOptions =
    { IndentOutput = true
      OmitXmlDeclaration = false
      EncodingName = "utf-8" }

/// <summary>
/// Baseline storage paths and file names used by host applications.
/// </summary>
let defaultStoragePaths : StoragePaths =
  { RootPath = ".";
    SaveDirectory = "saves";
    LogsDirectory = "logs";
    DatabasePath = "data\\bismarck.db";
    ImagesDirectory = "docs\\Parts\\counter-images";
    XmlDirectory = "config";
    OptionsFileName = "game-options.xml";
    ConfigurationFileName = "app-configuration.xml";
    GameStatusFileName = "game-status.xml";
    EventLogFileName = "game-events.xml" }

/// <summary>
/// Baseline full app configuration used when no config file exists yet.
/// </summary>
let defaultAppConfiguration : AppConfiguration =
    { Paths = defaultStoragePaths
      Xml = defaultXmlPersistenceOptions
      LastScenarioId = defaultGameOptions.ScenarioId
      LastStatusFilePath = "" }

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using GTA;
using GTA.Chrono;
using GTA.Math;
using GTA.Native;

[assembly: AssemblyTitle("Diretor de Cenas GTA")]
[assembly: AssemblyVersion("0.9.211.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8")]

// Gravação por tomada, preparação da série e fila com reinício nas marcas iniciais.
// O sufixo .3.cs seleciona explicitamente a API v3 do ScriptHookVDotNet.
public sealed class DiretorGTA : Script
{
    private readonly string logPath;
    private readonly bool showMessages;
    private readonly string projectRoot;
    private readonly string dataRoot;
    private readonly string scenesDirectory;
    private readonly string initialScene;
    private readonly string queueFile;
    private CenaJsonDiretor.Fila activeQueue;
    private string selectedSceneFile;
    private int queueIndex;
    private readonly Keys mountKey;
    private readonly Keys clearKey;
    private readonly Keys actionKey;
    private readonly Keys presetKey;
    private readonly Keys targetKey;
    private readonly Keys freeCameraKey;
    private readonly Keys markCameraKey;
    private readonly Keys markLocalKey;
    private readonly bool controllerMarkEnabled;
    private bool controllerMarkReleased, controllerMarkHolding, controllerMarkConsuming, controllerLocationMode;
    private uint controllerMarkStart;
    private const uint ControllerMarkHoldMs = 1000;
    private readonly Keys nextSceneKey;
    private readonly Keys previousSceneKey;
    private readonly Keys restartKey;
    private readonly Keys recordKey;
    private readonly Keys retryCaptureKey;
    private readonly Keys nextLocationKey;
    private readonly Keys previousLocationKey;
    private readonly Keys nextTestKey;
    private readonly Keys nextEpisodeKey;
    private readonly Keys nextSubtakeKey;
    private int selectedEpisode, productionSceneIndex;
    private int secretGrandmaSubtake, clubEntrySubtake, flightBoardingSubtake;
    private string mountedFlightSourceHash;
    private int dismissalSubtake = 1;
    private string mountedDismissalSourceHash;
    private string mountedPurchaseSourceHash;
    private TemporadaDiretor season;
    private readonly bool captureEnabled;
    private readonly string finalCleanupCaptureRoot;
    private readonly bool automaticRecording;
    private bool automaticRecordingDisabled, automaticShortcutActive;
    private bool automaticCapturePending;
    private CapturaDiretor directorCapture;
    private string appearancePythonPath, appearanceChoiceHash;
    private Process appearanceChoiceWorker;
    private readonly Dictionary<int, string> richPreviewSavedPaths = new Dictionary<int, string>();
    private Action afterOwnedSeatRelease;
    private Stopwatch ownedSeatReleaseTimer;
    private bool finalClearRequestedForOwnedSeats;
    private int productionLocationIndex = -1;
    private readonly string[] productionLocations = { "ROCA_GRAPESEED", "CASA_VO", "HOSPITAL_SANDY", "CASA_DINJER", "ESTRADA_SENORA",
        "IGREJA_CHAPARRAL", "MANSAO_CHEFE", "CASSINO_FORA", "CANAL_LS", "GALPAO_CYPRESS", "CENTRO_LEGION", "BANCO_MAZE",
        "RUA_STRAWBERRY", "VIADUTO_LAPUERTA", "AEROPORTO_TERMINAL", "AEROPORTO_HANGAR", "AEROPORTO_AR", "PRAIA_DELPERRO",
        "BOATE_FORA", "BOATE_DENTRO", "MANSAO_DJANIRA",
        "CANAL_LS_RAMPA_ENTRADA", "CANAL_LS_RAMPA_TOPO", "CANAL_LS_FUGA", "EXTRA_BAR_IRACI", "CASA_VO_INTERIOR" };
    private readonly string localMarkId;
    private readonly float cleanupRadius;
    private readonly int locationTimeout;
    private LocaisDiretor directorLocations;
    private OperacaoLocalDiretor directorLocalOperation;
    private C4OwnShellModelsProbeDiretor directorShellModelsTrial;
    private OperacaoProducaoDiretor.Command shellModelsCommand;
    private bool shellModelsReplyPending;
    private C4VinewoodShellTrialDiretor directorShellTrial;
    private OperacaoProducaoDiretor.Command shellTrialCommand;
    private bool shellTrialReplyPending, shellTrialRecovery;
    private OperacaoProducaoDiretor directorProductionOperation;
    private MesaColisaoTrialDiretor directorMesaTrial;
    private OperacaoProducaoDiretor.Command mesaTrialCommand;
    private AmbienteDiretor directorEnvironment;
    private bool awaitingCollision;
    private Stopwatch areaPreparationTimer;
    private readonly bool subtitlesEnabled;
    private readonly bool narrationSubtitles;
    private readonly float subtitleScale;
    private readonly string cameraMarkName;
    private CameraDiretor directorCamera;
    private bool c4ViewportSaved, c4ViewportChanged;
    private int c4SavedViewportInterior, c4SavedViewportRoom, c4OwnViewportInterior, c4OwnViewportRoom;
    private int c4BeforePoseInterior, c4BeforePoseRoom, c4RoomActorHandle;
    private uint c4NextRoomLog;
    private bool c4ViewportPending, c4ViewportForceReturned;
    private uint c4ViewportRequestedFrame, c4ViewportRequestedTime;
    private int c4RoomCameraHandle;
    private readonly string[] cameraPresets = { "DOIS", "MEDIA", "PERTO", "BAIXA", "ALTA", "ATRAS", "OLHOS", "LONGE" };
    private int cameraPresetIndex;
    private int cameraTargetIndex;
    private uint mountedAt;
    private bool countingDown;
    private bool awaitingCaptureStart;
    private Stopwatch captureStartTimer;
    private long captureStartNextPoll;
    private string preparedCaptureSrt;
    private bool awaitingCaptureEnd;
    private Stopwatch captureEndTimer;
    private const int CaptureFinalHoldMs = 2000;
    private bool takeFinished;
    private uint countdownStart;
    private const int CountdownMs = 3000;
    private string subtitleText = "";
    private string statusText = "";
    private uint statusUntil;
    private bool interfaceErrorReported;
    private bool ownHelpText;
    private int nextTerminalCameraTime;
    private readonly List<GTA.UI.FeedPost> ownNotifications = new List<GTA.UI.FeedPost>();
    private Ped hiddenPlayer;
    private bool playerStateSaved;
    private bool savedPlayerVisible;
    private bool savedPlayerFrozen;
    private bool savedPlayerCollision;
    private Vector3 savedPlayerPosition;
    private float savedPlayerHeading;
    private GTA.UI.TextElement subtitleElement;
    private GTA.UI.TextElement statusElement;
    private GTA.UI.TextElement countdownElement;
    private readonly int modelTimeout;
    private readonly int animationTimeout;
    private readonly int maxActors;
    private bool announced;
    private readonly List<Ped> createdPeds = new List<Ped>();
    private readonly object logSync = new object();
    private readonly Dictionary<string, Model> pendingModels = new Dictionary<string, Model>();
    private readonly Dictionary<string, Ped> actorsById = new Dictionary<string, Ped>();
    private readonly HashSet<string> requestedAnimations = new HashSet<string>();
    private readonly Dictionary<string, GestureData> gestures = new Dictionary<string, GestureData>();
    private readonly Dictionary<string, Vector3> initialPositions = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, float> initialHeadings = new Dictionary<string, float>();
    private readonly Dictionary<int, Vector3> actionDestinations = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, float> turnStartHeadings = new Dictionary<int, float>();
    private readonly Dictionary<int, float> turnTargetHeadings = new Dictionary<int, float>();
    private readonly HashSet<int> startedActions = new HashSet<int>();
    private readonly HashSet<int> finishedActions = new HashSet<int>();
    private int cutIndex = -1;
    private bool playing;
    private uint actionStartTime;
    private int segmentIndex = -1;
    private int nextMouthTime;
    private int mouthInterval = 600;
    private const string MouthDictionary = "mp_facial";
    private const string MouthClip = "mic_chatter";
    private const string MaleNeutralDictionary = "facials@gen_male@variations@normal";
    private const string FemaleNeutralDictionary = "facials@gen_female@variations@normal";
    private SceneData activeScene;
    private Stopwatch loadingTimer;
    private bool awaitingVisual;
    private PreparacaoElencoDiretor directorCast;
    private readonly bool automaticCast;
    private bool mountAfterCast;
    private PilotoPedidoDiretor directorPilot;
    private bool awaitingPilot;
    private bool pilotSelected;
    private int testSelection;
    private int castPreviewIndex;
    private readonly string[] castPreviewFiles = { "teste_fila_94.json", "teste_fila_95.json", "teste_fila_96.json",
        "teste_fila_91.json", "teste_fila_92.json", "teste_fila_93.json" };
    private EnsaioAcaoDiretor directorRehearsal;
    private EscritaCenario85Diretor directorWriting85;
    private EscritaFullTracks85Diretor directorWritingC6;
    private bool awaitingRehearsal;
    private EpisodioUmDiretor directorEpisodeOne;
    private CenaQuedaDiretor.QuedaSpec episodeOneFallSpec;
    private EpisodiosTresSeteDiretor directorThreeSeven;
    private CenaVooTemporadaDiretor directorSeasonFlight;
    private CenaEmpurraoAviaoDiretor directorPlanePush;
    private bool awaitingPlanePush;
    private ArmarioSerieDiretor directorWardrobe;
    private bool awaitingWardrobe;
    private LuzesBoateDiretor directorBoateLights;
    private bool awaitingBoateLights;
    private VeiculosSerieDiretor directorVehicles;
    private PortasSerieDiretor directorDoors;
    private FigurinoTransitorioDiretor directorTransientOutfit;
    private bool awaitingThreeSeven, awaitingSeasonFlight, awaitingVehicles, awaitingDoors;
    private CenaTemporadaDiretor directorSeasonWater;
    private bool awaitingSeasonWater;

    private bool HasOwnEpisodeCamera
    { get { return (!Object.ReferenceEquals(directorEpisodeOne, null) && directorEpisodeOne.UsesOwnCamera) ||
        (!Object.ReferenceEquals(directorSeasonFlight, null) && directorSeasonFlight.UsesOwnCamera) ||
        (!Object.ReferenceEquals(directorPlanePush, null) && directorPlanePush.UsesOwnCamera) ||
        !Object.ReferenceEquals(directorFoldPaper86, null) || !Object.ReferenceEquals(directorAuthoredOpening99, null); } }
    private bool awaitingEpisodeOne, awaitingEpisodeReset, episodeResetNeedsRelease;
    private bool episodeResetRestoreCamera;
    private Stopwatch episodeResetTimer;
    private Vector3 technicalWaterMark;
    private int nextWaterCameraTime;
    private bool awaitingStaging;
    private AcoesSerieDiretor directorStaging;
    private InterpretacaoDiretor directorInterpretation;
    private bool awaitingInterpretation;
    private ObjetosSerieDiretor directorProps;
    private PapelDobradoPoly86Diretor directorFoldPaper86;
    private PapelDobradoAutoralC5Diretor directorAuthoredPaperC5;
    private PapelBolso98Diretor directorPocket98;
    private ObservadorAbertura99 directorOpening99;
    private PapelAberturaAutoral99Diretor directorAuthoredOpening99;
    private MapaEntrada99Diretor directorMapOpening99;
    private const string PaperPoly86SourceHash = "51D463E7009A60CB66C4C8934FE6A55D8A7F82A4DF4D4DFB556013AD0B6A892F";
    private bool awaitingProps;
    private PosesSerieDiretor directorPoses;
    private EntradaSentadaAutorDiretor directorEntry;
    private bool awaitingEntry;
    private CartaFisicaDiretor directorCartaTrial;
    private CartaFisicaDiretor.EnsaioSpec cartaTrialSpec;
    private bool awaitingCartaTrial;
    private GorjetaFisicaTrialDiretor directorGorjetaTrial;
    private GorjetaFisicaTrialDiretor.EnsaioSpec gorjetaTrialSpec;
    private bool awaitingGorjetaTrial;
    private LimpezaMesaDiretor directorTableWipe;
    private bool awaitingTableWipe;
    private bool awaitingPoses;
    private bool awaitingPoseReset;
    private bool actionAfterPoseReset;
    private bool cameraAfterPoseReset;
    private readonly Dictionary<string, int> stagingTaskRevisions = new Dictionary<string, int>(StringComparer.Ordinal);
    private Vector3 anchor;
    private Vector3 right;
    private Vector3 forward;
    private bool sceneReady;
    private bool environmentSaved;
    private GameClockDateTime savedClock;
    private bool savedClockPaused;
    private uint savedWeatherFrom, savedWeatherTo;
    private float savedWeatherBlend;
    private bool savedPoliceIgnore;
    private bool savedDispatchCops;
    private int savedWantedLevel;
    private bool restorePolice;

    public DiretorGTA()
    {
        string bootstrapPath = Path.ChangeExtension(Filename, ".ini");
        if (!File.Exists(bootstrapPath))
            throw new InvalidOperationException("Diretor: configuração de instalação ausente: " + bootstrapPath);

        ScriptSettings bootstrap = ScriptSettings.Load(bootstrapPath);
        projectRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(
            bootstrap.GetValue<string>("Projeto", "Pasta", @"C:\GTA_Diretor")));
        if (!Directory.Exists(projectRoot))
            throw new InvalidOperationException("Diretor: pasta do projeto não encontrada: " + projectRoot);

        dataRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(
            bootstrap.GetValue<string>("Projeto", "Dados", projectRoot)));
        string logDir = Path.Combine(dataRoot, "logs");
        Directory.CreateDirectory(logDir);
        logPath = Path.Combine(logDir, "DiretorGTA.log");

        try
        {
            string configPath = Path.Combine(projectRoot, "configuracoes", "DiretorGTA.ini");
            if (!File.Exists(configPath))
                throw new InvalidOperationException("Configuração não encontrada: " + configPath);

            ScriptSettings config = ScriptSettings.Load(configPath);
            showMessages = config.GetValue<bool>("Interface", "Mensagens", true);
            mountKey = ReadKey(config, "Montar", "F6");
            clearKey = ReadKey(config, "Limpar", "F10");
            actionKey = ReadKey(config, "Acao", "F7");
            presetKey = ReadKey(config, "ProximoPlano", "F5");
            targetKey = ReadKey(config, "TrocarAtorCamera", "F9");
            freeCameraKey = ReadKey(config, "CameraLivre", "F11");
            markCameraKey = ReadKey(config, "MarcarCamera", "Insert");
            markLocalKey = ReadKey(config, "MarcarLocal", "F3");
            controllerMarkEnabled = config.GetValue<bool>("Controle", "MarcarLocal", true);
            nextSceneKey = ReadKey(config, "ProximaCena", "PageDown");
            previousSceneKey = ReadKey(config, "CenaAnterior", "PageUp");
            restartKey = ReadKey(config, "ReiniciarTomada", "Delete");
            recordKey = ReadKey(config, "PrepararGravacao", "Home");
            retryCaptureKey = ReadKey(config, "OrganizarNovamente", "End");
            nextLocationKey = ReadKey(config, "ProximoLocal", "L");
            previousLocationKey = ReadKey(config, "LocalAnterior", "K");
            nextTestKey = ReadKey(config, "ProximoTeste", "N");
            nextEpisodeKey = ReadKey(config, "ProximoEpisodio", "O");
            nextSubtakeKey = ReadKey(config, "ProximaSubtomada", "T");
            automaticRecording = config.GetValue<bool>("Gravacao", "AutomatizarAposPrimeira", true);
            HashSet<Keys> uniqueKeys = new HashSet<Keys>();
            foreach (Keys key in new Keys[] { mountKey, clearKey, actionKey, presetKey, targetKey, freeCameraKey, markCameraKey, markLocalKey,
                nextSceneKey, previousSceneKey, restartKey, recordKey, retryCaptureKey, nextLocationKey, previousLocationKey, nextTestKey, nextEpisodeKey, nextSubtakeKey })
                if (!uniqueKeys.Add(key))
                    throw new InvalidOperationException("Cada função do Diretor deve usar uma tecla diferente.");
            subtitlesEnabled = config.GetValue<bool>("Legendas", "Exibir", true);
            narrationSubtitles = config.GetValue<bool>("Legendas", "MostrarNarracao", true);
            subtitleScale = ReadSubtitleScale(config);
            WriteLog("CONFIG_LEGENDAS", "EscalaFonte=" + subtitleScale.ToString(CultureInfo.InvariantCulture) +
                "; cultura=" + CultureInfo.CurrentCulture.Name + "; Exibir=" + subtitlesEnabled + "; Narracao=" + narrationSubtitles);
            cameraMarkName = config.GetValue<string>("Camera", "NomeMarca", "meu_plano");
            if (String.IsNullOrWhiteSpace(cameraMarkName) || cameraMarkName.Length > 48)
                throw new InvalidOperationException("Camera/NomeMarca: use um nome com 1 a 48 caracteres.");
            scenesDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(
                config.GetValue<string>("Pastas", "Cenas", Path.Combine(dataRoot, "cenas"))));
            initialScene = config.GetValue<string>("Cena", "Inicial", "teste_json.json");
            if (initialScene != Path.GetFileName(initialScene) ||
                (!initialScene.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !initialScene.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Cena Inicial deve ser somente o nome de um arquivo .json ou .xml.");
            pilotSelected = config.GetValue<bool>("Cena", "PilotoAoIniciar", false);
            if (pilotSelected) testSelection = 1;
            queueFile = config.GetValue<string>("Fila", "Arquivo", "").Trim();
            if (queueFile.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(queueFile, @"\Afila_(teste|elenco|piloto|EP(0[1-9]|1[0-5]))\.json\z"))
                throw new InvalidOperationException("Fila/Arquivo: use fila_teste.json, fila_elenco.json, fila_piloto.json, fila_EPnn.json ou deixe vazio.");
            modelTimeout = config.GetValue<int>("Limites", "TimeoutModeloMs", 5000);
            animationTimeout = config.GetValue<int>("Limites", "TimeoutAnimacaoMs", 5000);
            maxActors = config.GetValue<int>("Limites", "MaxPersonagens", 5);
            locationTimeout = config.GetValue<int>("Ambiente", "TimeoutLocalMs", 10000);
            string radiusText = config.GetValue<string>("Ambiente", "RaioLimpeza", "40").Trim();
            float radius;
            if (!Single.TryParse(radiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out radius) ||
                Single.IsNaN(radius) || Single.IsInfinity(radius) || radius < 5f || radius > 100f)
                throw new InvalidOperationException("Ambiente/RaioLimpeza: use de 5 a 100 metros, com ponto decimal.");
            cleanupRadius = radius;
            if (locationTimeout < 1000 || locationTimeout > 20000)
                throw new InvalidOperationException("Ambiente/TimeoutLocalMs: use de 1000 a 20000 ms.");
            localMarkId = config.GetValue<string>("Local", "NomeMarca", "TESTE_AMBIENTE");
            if (!IsLocalId(localMarkId))
                throw new InvalidOperationException("Local/NomeMarca: use de 1 a 48 letras maiúsculas, números ou _, começando com letra.");
            if (modelTimeout < 1000 || modelTimeout > 15000 || animationTimeout < 1000 || animationTimeout > 15000 || maxActors < 1 || maxActors > 5)
                throw new InvalidOperationException("Limites inválidos: timeout entre 1000 e 15000 ms; máximo entre 1 e 5 personagens.");

            // Os multiplicadores de densidade valem só no frame atual.
            Interval = 0;
            directorCamera = new CameraDiretor(WriteLog);
            directorLocations = new LocaisDiretor(WriteLog);
            directorLocalOperation = new OperacaoLocalDiretor(dataRoot, WriteLog, Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "menyooStuff", "PropList.txt"));
            directorProductionOperation = new OperacaoProducaoDiretor(dataRoot, WriteLog);
            directorEnvironment = new AmbienteDiretor(WriteLog);
            automaticCast = config.GetValue<bool>("Elenco", "GerarAoMontar", true);
            if (automaticCast)
                directorCast = new PreparacaoElencoDiretor(dataRoot, WriteLog, config.GetValue<int>("Elenco", "Semente", 20601003));
            captureEnabled = config.GetValue<bool>("Gravacao", "Habilitada", false);
            finalCleanupCaptureRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.GetValue<string>("Gravacao", "PastaCapturas", @"%USERPROFILE%\Videos\Captures")));
            appearancePythonPath = Environment.ExpandEnvironmentVariables(config.GetValue<string>("Gravacao", "Python",
                @"%USERPROFILE%\AppData\Local\Programs\Python\Python312\python.exe"));
            if (captureEnabled)
            {
                if (config.GetValue<string>("Gravacao", "Modo", "manual").Trim() != "manual")
                    throw new InvalidOperationException("Gravacao/Modo: use manual. AutomatizarAposPrimeira habilita o atalho somente depois de uma captura real conferida nesta sessão.");
                string captures = finalCleanupCaptureRoot;
                directorCapture = new CapturaDiretor(dataRoot, captures, appearancePythonPath, Path.Combine(projectRoot, "ferramentas", "organizar_capturas.py"), WriteLog);
            }
            Tick += OnTick;
            KeyUp += OnKeyUp;
            Aborted += OnAborted;
            WriteLog("SCRIPT_CARREGADO", "Versão 0.9.211; organização de tomadas; montar=" + mountKey + "; ação=" + actionKey + "; limpar=" + clearKey +
                "; próxima=" + KeyLabel(nextSceneKey) + "; anterior=" + KeyLabel(previousSceneKey) + "; reiniciar=" + KeyLabel(restartKey) +
                "; fila=" + queueFile + "; cenas=" + scenesDirectory + "; dados=" + dataRoot + "; controle_marcar=" + controllerMarkEnabled);
        }
        catch (Exception ex)
        {
            WriteLog("ERRO_INICIALIZACAO", ex.ToString());
            throw;
        }
    }

    private static float ReadSubtitleScale(ScriptSettings config)
    {
        // GetValue<float> da API interpreta conforme o idioma do Windows.
        // Ler o texto evita tratar o ponto de 0.45 como separador de milhares em pt-BR.
        string value = config.GetValue<string>("Legendas", "EscalaFonte", "0.45").Trim();
        float scale;
        if (!Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) ||
            Single.IsNaN(scale) || Single.IsInfinity(scale) || scale < 0.3f || scale > 0.7f)
            throw new InvalidOperationException("Legendas/EscalaFonte: use um número entre 0.3 e 0.7, com ponto decimal. Valor recebido: " + value);
        return scale;
    }

    private static Keys ReadKey(ScriptSettings config, string name, string fallback)
    {
        string value = config.GetValue<string>("Teclas", name, fallback);
        Keys key;
        if (!Enum.TryParse<Keys>(value, true, out key) || !Enum.IsDefined(typeof(Keys), key) || IsReserved(key))
            throw new InvalidOperationException("Tecla inválida ou reservada em " + name + ": " + value);
        return key;
    }

    private static bool IsReserved(Keys key)
    {
        return (int)key < 1 || (int)key > 255 || key == Keys.F1 || key == Keys.F2 || key == Keys.F4 ||
            key == Keys.F8 || key == Keys.F12 || key == Keys.Tab || key == Keys.LWin ||
            key == Keys.RWin || key == Keys.Menu || key == Keys.ControlKey || key == Keys.ShiftKey ||
            key == Keys.LShiftKey || key == Keys.RShiftKey || key == Keys.LControlKey ||
            key == Keys.RControlKey || key == Keys.LMenu || key == Keys.RMenu;
    }

    private static string KeyLabel(Keys key)
    {
        if (key == Keys.PageDown) return "PageDown";
        if (key == Keys.PageUp) return "PageUp";
        return key.ToString();
    }

    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            if (!Object.ReferenceEquals(directorProductionOperation, null))
                directorProductionOperation.Update(!playing && !countingDown && loadingTimer == null &&
                    !awaitingCaptureStart && !awaitingCaptureEnd && !awaitingEpisodeReset && !awaitingPoseReset &&
                    Object.ReferenceEquals(afterOwnedSeatRelease, null) && Object.ReferenceEquals(appearanceChoiceWorker, null) &&
                    !Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) && Game.Player.CanControlCharacter &&
                    AtalhoGravacaoDiretor.IsGameForeground &&
                    (Object.ReferenceEquals(directorLocalOperation, null) || !directorLocalOperation.IsBusy) &&
                    (Object.ReferenceEquals(directorCast, null) || !directorCast.IsBusy) &&
                    (Object.ReferenceEquals(directorCapture, null) || (!directorCapture.IsArmed && !directorCapture.IsPending)), ExecuteProductionOperation,
                    !Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) && Game.Player.CanControlCharacter &&
                    AtalhoGravacaoDiretor.IsGameForeground);
            if (!Object.ReferenceEquals(directorShellModelsTrial, null))
            {
                PollShellModelsTrial();
                if (!Object.ReferenceEquals(directorShellModelsTrial, null)) return; // Query only: no local mark/scene/camera/ARM/capture.
            }
            if (!Object.ReferenceEquals(directorShellTrial, null))
            {
                PollShellTrial();
                if (!Object.ReferenceEquals(directorShellTrial, null))
                {
                    if (!Object.ReferenceEquals(directorLocalOperation, null)) directorLocalOperation.Update(directorShellTrial.IsReady &&
                        !Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) && !Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) &&
                        AtalhoGravacaoDiretor.IsGameForeground && Game.Player.CanControlCharacter);
                    return; // Separate Native inspection only; no scene/camera/countdown/ARM.
                }
            }
            if (!Object.ReferenceEquals(directorMesaTrial, null))
            {
                PollMesaTrial();
                return; // O ensaio nao entra no fluxo de montagem/contagem/captura.
            }
            if (!Object.ReferenceEquals(directorLocalOperation, null))
                directorLocalOperation.Update(!sceneReady && loadingTimer == null && !playerStateSaved && !playing &&
                    !countingDown && !awaitingCaptureStart && !awaitingCaptureEnd && !awaitingEpisodeReset && !awaitingPoseReset &&
                    Object.ReferenceEquals(afterOwnedSeatRelease, null) && Object.ReferenceEquals(appearanceChoiceWorker, null) &&
                    !Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) && Game.Player.CanControlCharacter &&
                    AtalhoGravacaoDiretor.IsGameForeground &&
                    GTA.Input.Controls.IsControlEnabled(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.MoveUpDown) &&
                    (Object.ReferenceEquals(directorCast, null) || !directorCast.IsBusy) &&
                    (Object.ReferenceEquals(directorCapture, null) || (!directorCapture.IsArmed && !directorCapture.IsPending)));
            PollControllerLocation();
            PollAppearanceChoice();
            if (!Object.ReferenceEquals(afterOwnedSeatRelease, null))
            {
                MaintainPlayerAndHud();
                if (environmentSaved && !Object.ReferenceEquals(activeScene, null)) ApplyDensity();
                AdvanceOwnedSeatRelease();
                return;
            }
            if (!announced && Game.Player.CanControlCharacter)
            {
                WriteLog("PRIMEIRO_TICK", "GTA=" + Game.FileVersion + "; tempo_jogo_ms=" + Game.GameTime);
                ShowMessage("Diretor 0.9.211: " + mountKey + " monta; " + actionKey + " ação; " + KeyLabel(recordKey) + " prepara gravação; " + clearKey + " limpa. L/K escolhem local; " + markLocalKey + (controllerMarkEnabled ? " ou R1 + direcional baixo (1s) marca." : " marca."));
                announced = true;
            }
            if (!Object.ReferenceEquals(directorCapture, null))
            {
                string captureResult = directorCapture.Poll();
                if (!String.IsNullOrEmpty(captureResult))
                {
                    if (automaticCapturePending && !captureResult.StartsWith("Salvo: ", StringComparison.Ordinal))
                        automaticRecordingDisabled = true;
                    automaticCapturePending = false;
                    takeFinished = false;
                    ShowMessage(captureResult);
                }
            }
            if (!Object.ReferenceEquals(directorCast, null) && directorCast.IsBusy)
            {
                string castStatus = directorCast.Update();
                if (!String.IsNullOrEmpty(castStatus)) ShowMessage(castStatus);
                if (directorCast.IsReady && mountAfterCast)
                {
                    mountAfterCast = false;
                    MountSelection(0);
                }
                return;
            }
            if (loadingTimer != null)
            {
                MaintainPlayerAndHud();
                AdvanceLoading();
            }
            if (environmentSaved && activeScene != null && (loadingTimer != null || sceneReady))
                ApplyDensity();
            if (!Object.ReferenceEquals(directorEnvironment, null) && directorEnvironment.IsActive)
                directorEnvironment.Update(Game.Player.Character, createdPeds);
            if (!Object.ReferenceEquals(directorBoateLights, null) && directorBoateLights.IsReady && directorBoateLights.IsActive) directorBoateLights.RunFrame();
            if (sceneReady)
            {
                if (!directorCamera.IsInitialized)
                {
                    MaintainPlayerAndHud();
                    if (directorEnvironment.IsActive && !directorEnvironment.IsReady)
                    {
                        if (!Object.ReferenceEquals(areaPreparationTimer, null) && areaPreparationTimer.ElapsedMilliseconds >= locationTimeout)
                            throw new InvalidOperationException("A limpeza da área não terminou a tempo. Escolha uma área menor ou reduza o raio no INI.");
                    }
                    else if (unchecked((uint)Game.GameTime - mountedAt) >= 500)
                        StartSceneCamera();
                }
                if (directorCamera.IsInitialized)
                {
                    MaintainPlayerAndHud();
                    if (!HasOwnEpisodeCamera) directorCamera.Update(System.Math.Min(Game.LastFrameTime, 0.05f));
                    MaintainC4Viewport();
                    if (activeScene.Id == "EP10_C06" && !Object.ReferenceEquals(directorWritingC6, null))
                    {
                        RequireWritingC6Contract(activeScene, selectedSceneFile);
                        if (!directorCamera.PollWritingC6Close(directorWritingC6, actorsById["djanira_pobre"])) return;
                        if (!Object.ReferenceEquals(areaPreparationTimer, null))
                        {
                            areaPreparationTimer = null; ShowMessage("Diretor: close de escrita preparado. " + ReadyControls());
                            WriteLog("CAMERA_PRONTA", "Scene=EP10_C06; OwnWritingClose=True; Full14s=True; ProductionVisualApproval=False");
                        }
                        if (!playing && !takeFinished && !directorWritingC6.IsFinished && !Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE))
                            directorWritingC6.RequirePreparedWeedPose();
                    }
                    if (!Object.ReferenceEquals(directorAuthoredOpening99, null))
                    {
                        RequireAuthoredOpening99Contract(activeScene, selectedSceneFile);
                        if (playing || countingDown || takeFinished)
                            throw new InvalidOperationException("Observacao99 exclusiva: nenhum fluxo narrativo/count/SAVE permitido.");
                        if (!directorAuthoredOpening99.IsObservationPrepared)
                        {
                            if (!directorAuthoredOpening99.PollPreparation()) return;
                            areaPreparationTimer = null;
                            ShowMessage("Diretor: observacao99 preparada em fase0. F7 observa corpo; F10 limpa. Sem ARM/SAVE.");
                            WriteLog("PAPEL_ABRE99_OBSERVACAO_PREPARADA", "Scene=teste_fila_99; Phase0Held=True; ObservationOnly=True; NarrativeReady=False; ARM=False; Capture=False; OpeningProved=False");
                        }
                        if (!Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE))
                        {
                            if (directorAuthoredOpening99.IsObserving) directorAuthoredOpening99.PollObservation();
                            else if (!directorAuthoredOpening99.IsFinished) directorAuthoredOpening99.RequireObservationPrepared();
                            else directorAuthoredOpening99.RequireObservationFinished();
                        }
                        directorAuthoredOpening99.DrawFrame();
                        return; // Technical measurement only: no generic action/cuts/SRT/ACAO_CONCLUIDA.
                    }
                    if (!Object.ReferenceEquals(directorFoldPaper86, null))
                    {
                        RequirePaperPoly86Contract(activeScene, selectedSceneFile);
                        if (!directorFoldPaper86.IsReady)
                        {
                            if (!directorFoldPaper86.PollPreparation()) return;
                            areaPreparationTimer = null;
                            ShowMessage("Diretor: previa visual propria pronta. F10 limpa; F7/ARM bloqueados.");
                            WriteLog("CAMERA_PRONTA", "Scene=teste_fila_86; Mode=DRAW_POLY_AUTHORED_STATIC_FOLDED; ActualOwnRenderedCameraConfirmed=True; ActionAllowed=False; ARM=False");
                        }
                        directorFoldPaper86.DrawFrame();
                        return; // F6 visual only: no countdown/action/capture/scene interface.
                    }
                    if (!Object.ReferenceEquals(directorAuthoredPaperC5, null) && !directorAuthoredPaperC5.IsReady)
                    {
                        if (!directorAuthoredPaperC5.PollCamera(directorProps.GetAuthoredPaperC5Owner())) return;
                        areaPreparationTimer = null;
                        ShowMessage("Diretor: papel autoral e camera proprios preparados. " + ReadyControls());
                        WriteLog("CAMERA_PRONTA", "Scene=EP08_C05; SourceSHA256=" + PapelDobradoAutoralC5Diretor.ExactSourceHash +
                            "; OwnRendererReady=True; OriginalHandoffGuards=True; FoldedColliderProved=False; VisualApproval=False");
                    }
                    if (!Object.ReferenceEquals(directorPocket98, null) && !directorPocket98.IsReady)
                    {
                        RequirePocket98Contract(activeScene, selectedSceneFile);
                        if (!directorPocket98.PollCamera()) return;
                        areaPreparationTimer = null;
                        WriteLog("CAMERA_PRONTA", "Scene=teste_fila_98; SourceSHA256=" + PapelBolso98Diretor.ExactSourceHash + "; TechnicalOnly=True; PocketAcquisitionPending=True; ARM=False; Capture=False");
                    }
                    if (activeScene.Id == "teste_fila_98" && actionAfterPoseReset && !awaitingPoseReset && !awaitingEpisodeReset && directorPocket98.IsReady)
                    { actionAfterPoseReset = false; BeginPreparedAction(); }
                    if (activeScene.Id == "teste_fila_85" && !Object.ReferenceEquals(directorWriting85, null) &&
                        directorWriting85.IsWeedFullTrack && !playing && !takeFinished && !Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE))
                    { RequireWeed85TechnicalState(); directorWriting85.RequirePreparedWeedPose(); }
                    if (activeScene.Id == "teste_fila_84" && !Game.IsPaused && !Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE))
                    {
                        RequireOfficeCell84Contract(activeScene, selectedSceneFile);
                        if (playing || countingDown || !Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending))
                            throw new InvalidOperationException("Sono84 exige apenas previa F6 sem acao/writer/ARM.");
                        if (!Object.ReferenceEquals(directorPoses, null) && directorPoses.IsReady && !awaitingPoseReset) directorPoses.PollOfficeCellBodyPreview84();
                    }
                    if (IsRoomC4Scene && c4ViewportPending) return;
                    if (activeScene.Id == "EP07_C02" && directorCamera.IsGalpaoWitnessPosePending) return;
                    if (awaitingEpisodeReset) AdvanceEpisodeReset();
                    if (awaitingPoseReset)
                    {
                        directorPoses.PollLoading();
                        if (directorPoses.IsReady)
                        {
                            awaitingPoseReset = false;
                            if (cameraAfterPoseReset) SetSceneShot(activeScene.CameraPreset, activeScene.CameraActor, false);
                            directorCamera.FixCurrentPose();
                            cameraAfterPoseReset = false;
                            if (!Object.ReferenceEquals(directorInterpretation, null)) directorInterpretation.Reset();
                            if (actionAfterPoseReset && !awaitingEpisodeReset) { actionAfterPoseReset = false; BeginPreparedAction(); }
                            else ShowMessage("Diretor: tomada reiniciada. " + ReadyControls());
                        }
                    }
                    if (IsAuthoredPaperC5Source(activeScene) && actionAfterPoseReset && !awaitingPoseReset && !awaitingEpisodeReset && directorAuthoredPaperC5.IsReady)
                    { actionAfterPoseReset = false; BeginPreparedAction(); }
                    AdvanceCaptureStart();
                    AdvanceCountdown();
                    AdvanceAction();
                    AdvanceCaptureEnd();
                    if (!Object.ReferenceEquals(directorAuthoredPaperC5, null)) directorAuthoredPaperC5.DrawFrame(directorProps.GetAuthoredPaperC5Owner());
                    if (!Object.ReferenceEquals(directorPocket98, null)) directorPocket98.DrawFrame();
                    if (!Object.ReferenceEquals(directorAuthoredOpening99, null) && directorAuthoredOpening99.IsReady) directorAuthoredOpening99.DrawFrame();
                    DrawSceneInterface();
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("ERRO_EXECUCAO", ex.ToString());
            Cleanup();
            ShowMessage(ex is InvalidOperationException ? "Diretor: " + ex.Message : "Diretor: a cena foi interrompida. Consulte o log.");
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!announced || e.Alt || e.Control || e.Shift ||
            Game.IsKeyPressed(Keys.LWin) || Game.IsKeyPressed(Keys.RWin) ||
            (e.KeyCode != mountKey && e.KeyCode != clearKey && e.KeyCode != actionKey && e.KeyCode != presetKey &&
             e.KeyCode != targetKey && e.KeyCode != freeCameraKey && e.KeyCode != markCameraKey && e.KeyCode != markLocalKey &&
             e.KeyCode != nextSceneKey && e.KeyCode != previousSceneKey && e.KeyCode != restartKey &&
             e.KeyCode != recordKey && e.KeyCode != retryCaptureKey && e.KeyCode != nextLocationKey && e.KeyCode != previousLocationKey && e.KeyCode != nextTestKey && e.KeyCode != nextEpisodeKey && e.KeyCode != nextSubtakeKey &&
             !(RichChoiceNumber(e.KeyCode) > 0 && sceneReady && Array.IndexOf(castPreviewFiles, activeScene.Id + ".json") >= 0)))
            return;
        try
        {
            if (!Object.ReferenceEquals(directorFoldPaper86, null) && e.KeyCode != clearKey)
            { WriteLog("PAPER_POLY86_TECLA_BLOQUEADA", "F6 visual somente; nenhuma acao/ARM/SAVE; F10 restaura proxy/camera proprios."); return; }
            if (!Object.ReferenceEquals(activeScene, null) && activeScene.Id == "EP07_C02" &&
                directorCamera.IsGalpaoWitnessPosePending && e.KeyCode != clearKey)
            { WriteLog("CAMERA_C2_COMANDO_PENDENTE", "Aguardando getter pos-frame; ARM/acao/ajustes bloqueados; limpar permitido."); return; }
            if (!Object.ReferenceEquals(directorShellModelsTrial, null) && e.KeyCode != clearKey)
            { WriteLog("C4_MODELS_TECLA_BLOQUEADA", "Consulta de3modelos; marca/montagem/camera/ARM/F7 bloqueados; F10 libera somente requests proprios."); return; }
            if (!Object.ReferenceEquals(directorShellTrial, null) && e.KeyCode != clearKey)
            { WriteLog("C4_SHELL_TECLA_BLOQUEADA", "Prova tecnica; ARM/acao/montagem bloqueados; F10 restaura."); return; }
            if (!Object.ReferenceEquals(directorMesaTrial, null) && e.KeyCode != clearKey)
            { WriteLog("MESA_TRIAL_TECLA_BLOQUEADA", "Ensaio diagnostico pendente; limpar continua permitido."); return; }
            // Conservar o ultimo quadro sem avisos; limpar e reiniciar continuam imediatos.
            if (awaitingCaptureEnd && e.KeyCode != clearKey && e.KeyCode != restartKey) return;
            if (!Object.ReferenceEquals(directorLocalOperation, null) && directorLocalOperation.IsBusy && e.KeyCode != clearKey)
            { ShowMessage("Diretor: aguardando o deslocamento físico de locação terminar."); return; }
            if (!Object.ReferenceEquals(appearanceChoiceWorker, null))
            { ShowMessage("Registrando a opção escolhida e os hashes; espere terminar."); return; }
            if (!Object.ReferenceEquals(afterOwnedSeatRelease, null) && e.KeyCode != clearKey)
            { ShowMessage("Aguardando saída dos assentos próprios; a próxima cena abrirá sozinha."); return; }
            WriteLog("TECLA_RECEBIDA", "Tecla=" + e.KeyCode + "; tempo_jogo_ms=" + Game.GameTime);
            if (awaitingCaptureStart && e.KeyCode != clearKey && e.KeyCode != restartKey)
            { ShowMessage("Diretor: aguardando a captura começar; a contagem abrirá automaticamente."); return; }
            if (awaitingPoseReset && e.KeyCode != clearKey)
            {
                ShowMessage("Diretor: restaurando as poses antes da tomada. " + clearKey + " cancela.");
                return;
            }
            if (!Object.ReferenceEquals(directorCast, null) && directorCast.IsBusy && e.KeyCode != clearKey && e.KeyCode != mountKey)
            {
                ShowMessage("Preparando o elenco: " + directorCast.CompletedCount + "/" + directorCast.TotalCount + ". " + clearKey + " cancela.");
                return;
            }
            if (RichChoiceNumber(e.KeyCode) > 0)
                ChooseRichAppearance(RichChoiceNumber(e.KeyCode));
            else if (e.KeyCode == clearKey)
                ClearAfterOwnedSeatRelease();
            else if (e.KeyCode == markLocalKey)
                MarkLocation();
            else if (e.KeyCode == actionKey)
                BeginAction();
            else if (e.KeyCode == restartKey)
                RestartTake();
            else if (e.KeyCode == recordKey || e.KeyCode == retryCaptureKey)
                CaptureCommand(e.KeyCode);
            else if (e.KeyCode == nextLocationKey || e.KeyCode == previousLocationKey)
                SelectProductionLocation(e.KeyCode == nextLocationKey ? 1 : -1);
            else if (e.KeyCode == nextTestKey)
                SelectTest();
            else if (e.KeyCode == nextEpisodeKey)
                SelectEpisode();
            else if (e.KeyCode == nextSubtakeKey)
                SelectProductionSubtake();
            else if (e.KeyCode == nextSceneKey || e.KeyCode == previousSceneKey)
                MountSelection(e.KeyCode == nextSceneKey ? 1 : -1);
            else if (e.KeyCode == presetKey || e.KeyCode == targetKey || e.KeyCode == freeCameraKey || e.KeyCode == markCameraKey)
                CameraCommand(e.KeyCode);
            else
                MountSelection(0);
        }
        catch (Exception ex)
        {
            WriteLog("ERRO_COMANDO", ex.ToString());
            if (!sceneReady || e.KeyCode == restartKey)
                Cleanup();
            ShowMessage("Diretor: " + ex.Message);
        }
    }

    private bool OwnedActorsReleased()
    {
        bool ready = true;
        if (!Object.ReferenceEquals(directorEpisodeOne, null) && directorEpisodeOne.IsReady) ready = directorEpisodeOne.ReleaseActorsForReset() && ready;
        if (!Object.ReferenceEquals(directorPilot, null) && directorPilot.IsReady) ready = directorPilot.ReleaseActorsForReset() && ready;
        if (!Object.ReferenceEquals(directorRehearsal, null) && directorRehearsal.IsReady) ready = directorRehearsal.ReleaseActorsForReset() && ready;
        if (!Object.ReferenceEquals(directorThreeSeven, null) && directorThreeSeven.IsReady) ready = directorThreeSeven.ReleaseActorsForReset() && ready;
        if (!Object.ReferenceEquals(directorSeasonFlight, null) && (directorSeasonFlight.IsReady || finalClearRequestedForOwnedSeats))
        {
            if (finalClearRequestedForOwnedSeats) directorSeasonFlight.StopOwnFlightForFinalCleanup();
            ready = directorSeasonFlight.ReleaseActorsForReset() && ready;
        }
        // Release é seguro também durante carga/erro do inserto; aguardar antes de Dispose fora da captura.
        if (!Object.ReferenceEquals(directorPlanePush, null)) ready = directorPlanePush.ReleaseActorsForReset() && ready;
        if (!Object.ReferenceEquals(directorWardrobe, null)) ready = directorWardrobe.ReleaseActorsForReset() && ready;
        if (!Object.ReferenceEquals(directorVehicles, null) && directorVehicles.IsReady) ready = directorVehicles.ReleaseActorsForReset() && ready;
        return ready;
    }

    private bool DeferOwnedSeatRelease(Action completed)
    {
        if (OwnedActorsReleased()) return false;
        afterOwnedSeatRelease = completed;
        if (Object.ReferenceEquals(ownedSeatReleaseTimer, null)) ownedSeatReleaseTimer = Stopwatch.StartNew();
        if (directorCamera.IsInitialized) directorCamera.FixCurrentPose();
        WriteLog("LIMPEZA_ASSENTOS_PENDENTE", "Saída física fora da gravação; preservar tarefas e entidades até o próximo frame.");
        ShowMessage("Diretor: aguardando saída dos assentos próprios; espere. F10 cancela a próxima montagem.");
        return true;
    }

    private void ClearAfterOwnedSeatRelease()
    {
        finalClearRequestedForOwnedSeats = true;
        StopAutomaticRecording();
        if (!Object.ReferenceEquals(directorCapture, null)) directorCapture.Cancel("limpeza solicitada");
        playing = false; countingDown = false; subtitleText = "";
        Action clear = delegate { ShowMessage(Cleanup() ? "Diretor: cena limpa; ambiente restaurado." : "Diretor: limpeza incompleta. Consulte o log."); };
        if (!Object.ReferenceEquals(afterOwnedSeatRelease, null))
        { afterOwnedSeatRelease = clear; ShowMessage("Próxima montagem cancelada; concluindo a limpeza dos assentos."); return; }
        try { if (!DeferOwnedSeatRelease(clear)) clear(); }
        catch (Exception ex)
        {
            // Um ator removido/morto não pode bloquear a restauração do jogador.
            // Cleanup conserva qualquer recurso cuja propriedade não seja segura.
            WriteLog("LIBERACAO_ASSENTOS_INTERROMPIDA", ex.ToString());
            afterOwnedSeatRelease = null; ownedSeatReleaseTimer = null; finalClearRequestedForOwnedSeats = false;
            clear();
        }
    }

    private void AdvanceOwnedSeatRelease()
    {
        try
        {
            if (Object.ReferenceEquals(ownedSeatReleaseTimer, null))
                throw new InvalidOperationException("A saida dos assentos nao possui prazo registrado; entidades preservadas.");
            if (ownedSeatReleaseTimer.ElapsedMilliseconds > 5000)
            {
                if (!finalClearRequestedForOwnedSeats || playing || countingDown || awaitingCaptureStart ||
                    awaitingCaptureEnd || automaticShortcutActive || automaticCapturePending ||
                    (Object.ReferenceEquals(directorCapture, null) && captureEnabled) || Object.ReferenceEquals(directorSeasonFlight, null) ||
                    activeScene.Id != "EP07_C03" ||
                    directorSeasonFlight.Part != CenaVooTemporadaDiretor.BoardingPart.EmbarqueDjaniraDecolagem)
                    throw new InvalidOperationException("A saída dos assentos próprios não terminou em cinco segundos; entidades preservadas.");
                if (!Object.ReferenceEquals(directorCapture, null)) directorCapture.RequireNoWriterForFinalCleanup();
                else CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
                directorSeasonFlight.ReleaseStuckRearForFinalCleanup();
            }
            if (!OwnedActorsReleased()) return;
            Action completed = afterOwnedSeatRelease;
            afterOwnedSeatRelease = null; ownedSeatReleaseTimer = null; finalClearRequestedForOwnedSeats = false;
            WriteLog("LIMPEZA_ASSENTOS_CONCLUIDA", "Todos os ocupantes próprios fora dos veículos; continuar limpeza/montagem.");
            completed();
        }
        catch (Exception ex)
        {
            afterOwnedSeatRelease = null; ownedSeatReleaseTimer = null; finalClearRequestedForOwnedSeats = false;
            WriteLog("ERRO_LIMPEZA_ASSENTOS", ex.ToString());
            ShowMessage("Diretor: " + ex.Message);
        }
    }

    private static int RichChoiceNumber(Keys key)
    {
        if (key >= Keys.D1 && key <= Keys.D3) return (int)key - (int)Keys.D1 + 1;
        if (key >= Keys.NumPad1 && key <= Keys.NumPad3) return (int)key - (int)Keys.NumPad1 + 1;
        return 0;
    }

    private void ChooseRichAppearance(int option)
    {
        if (!sceneReady || !directorCamera.IsInitialized || playing || countingDown || loadingTimer != null ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)))
            throw new InvalidOperationException("Espere a prévia pronta e escolha fora da gravação.");
        string savedPath;
        if (!richPreviewSavedPaths.TryGetValue(option, out savedPath))
        { ShowMessage("Abra a opção " + option + " com PageUp/PageDown e espere a conferência das peças antes de escolhê-la."); return; }
        RequirePlainPath(savedPath);
        VisualDiretor.Visual chosen = VisualDiretor.Load(savedPath, "mp_f_freemode_01");
        XmlDocument saved = new XmlDocument(); saved.XmlResolver = null; saved.Load(savedPath);
        XmlNode source = saved.SelectSingleNode("/OutfitPedData/DiretorOpcao/SHA256CanonicoOrigem");
        if (Object.ReferenceEquals(source, null) || !System.Text.RegularExpressions.Regex.IsMatch(source.InnerText, "\\A[0-9A-F]{64}\\z"))
            throw new InvalidOperationException("A opção não tem a referência do figurino original.");
        appearanceChoiceHash = chosen.Sha256;
        string request = Path.Combine(dataRoot, "logs", "escolha_djanira_" + Guid.NewGuid().ToString("N") + ".json");
        string workerPath = Path.Combine(projectRoot, "ferramentas", "escolher_djanira.py");
        RequirePlainPath(request); RequirePlainPath(workerPath); RequirePlainPath(appearancePythonPath);
        if (!File.Exists(appearancePythonPath) || !File.Exists(workerPath))
            throw new InvalidOperationException("A ferramenta de escolha não foi encontrada.");
        string json = "{\"versao\":1,\"opcao\":" + option.ToString(CultureInfo.InvariantCulture) +
            ",\"visualSHA256\":\"" + appearanceChoiceHash + "\",\"origemSHA256\":\"" + source.InnerText + "\"}\n";
        using (FileStream stream = new FileStream(request, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { byte[] bytes = new UTF8Encoding(false).GetBytes(json); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        ProcessStartInfo start = new ProcessStartInfo(appearancePythonPath);
        start.Arguments = "-X utf8 \"" + workerPath + "\" --pedido \"" + request + "\"";
        start.WorkingDirectory = projectRoot; start.UseShellExecute = false; start.CreateNoWindow = true;
        start.WindowStyle = ProcessWindowStyle.Hidden; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        appearanceChoiceWorker = Process.Start(start);
        if (Object.ReferenceEquals(appearanceChoiceWorker, null)) throw new InvalidOperationException("A ferramenta de escolha não iniciou.");
        WriteLog("DJANIRA_ESCOLHA_SOLICITADA", "Opção=" + option + "; pedido=" + request + "; SHA256=" + appearanceChoiceHash);
        ShowMessage("Opção " + option + " escolhida. Registrando o figurino e a continuidade...");
    }

    private void PollAppearanceChoice()
    {
        if (Object.ReferenceEquals(appearanceChoiceWorker, null) || !appearanceChoiceWorker.HasExited) return;
        int code = appearanceChoiceWorker.ExitCode;
        string output = appearanceChoiceWorker.StandardOutput.ReadToEnd() + appearanceChoiceWorker.StandardError.ReadToEnd();
        appearanceChoiceWorker.Dispose(); appearanceChoiceWorker = null;
        if (code != 0)
        { WriteLog("ERRO_ESCOLHA_DJANIRA", output); ShowMessage("A escolha não terminou. Confira o log e o backup antes de escolher novamente."); return; }
        VisualDiretor.Visual canonical = VisualDiretor.Load(Path.Combine(dataRoot, "catalogos", "visuais", "DJANIRA_RICA.xml"), "mp_f_freemode_01");
        if (canonical.Sha256 != appearanceChoiceHash) throw new InvalidOperationException("O hash da escolha publicada divergiu.");
        ReadScene(Path.Combine(scenesDirectory, selectedSceneFile));
        WriteLog("DJANIRA_ESCOLHA_REGISTRADA", "SHA256=" + canonical.Sha256 + "; " + output);
        ShowMessage("Djanira rica escolhida e registrada para a próxima temporada. PageDown continua a prévia do elenco.");
    }

    private void ShowMessage(string text)
    {
        if (!showMessages)
            return;
        if (sceneReady && !Object.ReferenceEquals(directorCamera, null) && directorCamera.IsInitialized)
        {
            statusText = text;
            statusUntil = unchecked((uint)Game.GameTime + 6000);
            return;
        }

        // Uma falha de aviso não deve interromper a montagem nem a limpeza.
        bool helpSent = false;
        bool notificationSent = false;
        try
        {
            GTA.UI.Screen.ShowHelpText(text, 20000, false, false);
            ownHelpText = true;
            helpSent = true;
        }
        catch (Exception ex)
        {
            WriteLog("ERRO_AVISO_AJUDA", ex.ToString());
        }
        try
        {
            var notification = GTA.UI.Notification.PostTickerForced(text, false, false);
            // O operador != de FeedPost pode desreferenciar null; comparar as referências.
            notificationSent = !Object.ReferenceEquals(notification, null);
            if (notificationSent)
            {
                ownNotifications.Add(notification);
                if (ownNotifications.Count > 64)
                {
                    GTA.UI.FeedPost oldest = ownNotifications[0];
                    ownNotifications.RemoveAt(0);
                    oldest.Delete();
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("ERRO_AVISO_NOTIFICACAO", ex.ToString());
        }
        WriteLog("AVISO_ENVIADO", "Ajuda=" + helpSent + "; notificacao=" + notificationSent);
        if (!helpSent && !notificationSent)
        {
            try { GTA.UI.Screen.ShowSubtitle(text, 12000); }
            catch (Exception ex) { WriteLog("ERRO_AVISO_LEGENDA", ex.ToString()); }
        }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        Cleanup();
        if (!Object.ReferenceEquals(directorLocalOperation, null)) directorLocalOperation.Dispose();
        WriteLog("SCRIPT_ENCERRADO", "O script foi encerrado ou recarregado.");
    }

    private static bool IsLocalId(string id)
    {
        return !String.IsNullOrEmpty(id) && System.Text.RegularExpressions.Regex.IsMatch(id, @"\A[A-Z][A-Z0-9_]{0,47}\z");
    }

    private void MarkLocation()
    {
        if (!Object.ReferenceEquals(directorLocalOperation, null) && directorLocalOperation.IsBusy)
            throw new InvalidOperationException("Aguarde o deslocamento físico terminar antes de marcar.");
        if (sceneReady || loadingTimer != null || playerStateSaved)
            throw new InvalidOperationException("Limpe a cena com " + clearKey + " antes de marcar o local.");
        string id = productionLocationIndex < 0 ? localMarkId : productionLocations[productionLocationIndex];
        if (id == "AEROPORTO_AR")
            throw new InvalidOperationException("AEROPORTO_AR depende do teste próprio de avião. Uma marca no chão não representa o ponto aéreo.");
        directorLocations.Save(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), id, Game.Player.Character);
        controllerLocationMode = false;
        selectedEpisode = 0;
        if (id == "ROCA_GRAPESEED")
        {
            pilotSelected = true;
            testSelection = 1;
            selectedSceneFile = "";
            ShowMessage("ROCA_GRAPESEED marcada. " + mountKey + " monta O PEDIDO: moto, conversa e celular. Escolha um espaço plano e aberto.");
            return;
        }
        pilotSelected = false;
        testSelection = 0;
        ShowMessage("Diretor: local " + id + " marcado." + (productionLocationIndex < 0 ? " " + mountKey + " monta aqui." : " " + KeyLabel(nextLocationKey) + " seleciona o próximo local."));
    }

    private void SelectProductionLocation(int direction)
    {
        if (sceneReady || loadingTimer != null || playerStateSaved)
            throw new InvalidOperationException("Limpe com " + clearKey + " antes de selecionar o local.");
        if (productionLocationIndex < 0) productionLocationIndex = direction > 0 ? 0 : productionLocations.Length - 1;
        else productionLocationIndex = (productionLocationIndex + direction + productionLocations.Length) % productionLocations.Length;
        string id = productionLocations[productionLocationIndex];
        controllerLocationMode = true;
        controllerMarkReleased = false;
        controllerMarkHolding = false;
        ShowMessage("Marcar " + (productionLocationIndex + 1) + "/" + productionLocations.Length + ": " + id +
            (id == "CANAL_LS" ? ". Entre na água e aperte " : id == "CANAL_LS_RAMPA_ENTRADA" ?
                ". Na água junto ao pé da rampa, aperte " : ". Vá ao ponto, fique a pé e aperte ") +
            markLocalKey + (controllerMarkEnabled ? " ou segure R1 + direcional baixo por 1s" : "") + ". L/K escolhem local ou subponto real da rampa.");
        WriteLog("LOCAL_SELECIONADO", "Id=" + id + "; índice=" + (productionLocationIndex + 1));
    }

    private void PollControllerLocation()
    {
        try { PollControllerLocationCore(); }
        catch (Exception ex)
        {
            controllerMarkReleased = false;
            controllerMarkHolding = false;
            WriteLog("ERRO_COMANDO_CONTROLE", ex.ToString());
            ShowMessage(ex is InvalidOperationException ? "Diretor: " + ex.Message : "Diretor: não foi possível marcar pelo controle. Consulte o log.");
        }
    }

    private void PollControllerLocationCore()
    {
        // As ações do GTA recebem o controle já reconhecido pelo jogo, sem drivers extras.
        // Exigir ambos soltos evita salvar uma marca ao fechar menu, cancelar ou recarregar.
        if (!controllerMarkEnabled || !announced || Game.IsPaused ||
            Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) ||
            !Game.Player.CanControlCharacter || sceneReady || loadingTimer != null || playerStateSaved ||
            playing || countingDown || !Object.ReferenceEquals(afterOwnedSeatRelease, null) ||
            !Object.ReferenceEquals(appearanceChoiceWorker, null) ||
            (!Object.ReferenceEquals(directorCast, null) && directorCast.IsBusy) ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)) ||
            !GTA.Input.Controls.IsControlEnabled(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.MoveUpDown) ||
            !AtalhoGravacaoDiretor.IsGameForeground)
        {
            controllerMarkReleased = false;
            controllerMarkHolding = false;
            controllerMarkConsuming = false;
            return;
        }
        bool shoulder = GTA.Input.Controls.IsDisabledControlPressed(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.Cover);
        bool down = GTA.Input.Controls.IsDisabledControlPressed(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.CharacterWheel);
        if (!shoulder && !down)
        {
            controllerMarkReleased = true;
            controllerMarkHolding = false;
            controllerMarkConsuming = false;
            return;
        }
        if (Game.LastInputMethod != GTA.InputMethod.GamePad)
        {
            controllerMarkReleased = false;
            controllerMarkHolding = false;
            controllerMarkConsuming = false;
            return;
        }
        if (shoulder && down) controllerMarkConsuming = true;
        if (shoulder || controllerMarkConsuming)
        {
            GTA.Input.Controls.DisableControlActionThisFrame(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.CharacterWheel, true);
            GTA.Input.Controls.DisableControlActionThisFrame(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.MultiplayerInfo, true);
            // L/K arma a marcação; R1 sozinho conserva cobertura fora desse modo.
            if (controllerLocationMode || down || controllerMarkConsuming)
                GTA.Input.Controls.DisableControlActionThisFrame(GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.Cover, true);
        }
        if (!shoulder || !down)
        {
            controllerMarkHolding = false;
            return;
        }
        if (!controllerMarkReleased) return;
        uint now = unchecked((uint)Game.GameTime);
        if (!controllerMarkHolding)
        {
            controllerMarkHolding = true;
            controllerMarkStart = now;
        }
        if (unchecked(now - controllerMarkStart) < ControllerMarkHoldMs) return;
        controllerMarkReleased = false;
        controllerMarkHolding = false;
        Ped player = Game.Player.Character;
        if (Object.ReferenceEquals(player, null) || !player.Exists() || player.IsDead || player.IsInVehicle() ||
            player.IsJumping || player.IsFalling || player.IsRagdoll || player.IsInCover || player.IsGoingIntoCover || player.Speed > 0.5f)
        {
            ShowMessage("Para marcar pelo controle, fique a pé, parado e fora da cobertura. Solte os botões e tente outra vez.");
            return;
        }
        WriteLog("CONTROLE_RECEBIDO", "Comando=MarcarLocal; combinação=R1+DirecionalBaixo; sustentado_ms=" + unchecked(now - controllerMarkStart));
        MarkLocation();
    }

    private void CaptureCommand(Keys key)
    {
        if (!captureEnabled || Object.ReferenceEquals(directorCapture, null))
            throw new InvalidOperationException("Organização de gravação desabilitada no INI.");
        if (key == retryCaptureKey)
        {
            if (playing || countingDown || directorCapture.IsArmed)
                throw new InvalidOperationException("Termine a ação antes de tentar organizar novamente.");
            directorCapture.Retry();
            ShowMessage("Gravação: procurando o vídeo finalizado da tomada anterior.");
            return;
        }
        if (!sceneReady || !directorCamera.IsInitialized || playing || countingDown || awaitingEpisodeReset || awaitingPoseReset)
            throw new InvalidOperationException("Monte a cena e espere a câmera pronta antes de preparar a gravação.");
        if (directorCapture.IsArmed)
        {
            directorCapture.Cancel("preparação cancelada pelo usuário");
            takeFinished = false;
            ShowMessage("Gravação cancelada no Diretor. Se a Barra de Jogos ainda grava, pare com Win+Alt+R.");
            return;
        }
        ArmCurrentCapture();
        takeFinished = false;
        ShowMessage("Gravação preparada: Win+Alt+R começa; " + actionKey + " ação; ao terminar, Win+Alt+R para. O Diretor organiza sozinho.");
    }

    private void SelectTest()
    {
        if (playing || countingDown || loadingTimer != null ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsPending || directorCapture.IsArmed)))
            throw new InvalidOperationException("Termine ou limpe a tomada antes de escolher outro teste.");
        if (DeferOwnedSeatRelease(SelectTest)) return;
        if (!Cleanup()) throw new InvalidOperationException("A limpeza ainda não terminou.");
        testSelection = (testSelection + 1) % 5;
        selectedEpisode = 0;
        pilotSelected = testSelection == 1;
        selectedSceneFile = "";
        if (testSelection == 0) castPreviewIndex = 0;
        string label = testSelection == 0 ? "Três opções de macacão, depois os13 visuais; PageDown navega" : testSelection == 1 ? "O PEDIDO, em ROCA_GRAPESEED" : testSelection == 2 ? "Ensaio de coreografia, na marca de teste existente" : testSelection == 3 ? "Vinheta da série, na marca PRAIA_DELPERRO" : "Ensaio de saída do avião sobre água; exige CANAL_LS marcado nadando";
        ShowMessage(label + ". " + mountKey + " monta; " + KeyLabel(nextTestKey) + " escolhe o próximo teste.");
    }

    private void SelectEpisode()
    {
        if (playing || countingDown || loadingTimer != null ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsPending || directorCapture.IsArmed)))
            throw new InvalidOperationException("Termine ou limpe a tomada antes de escolher o episódio.");
        TemporadaDiretor loaded = TemporadaDiretor.Load(projectRoot);
        if (DeferOwnedSeatRelease(SelectEpisode)) return;
        if (!Cleanup()) throw new InvalidOperationException("A limpeza ainda não terminou.");
        season = loaded;
        selectedEpisode = selectedEpisode % 15 + 1;
        productionSceneIndex = 0;
        selectedSceneFile = "";
        pilotSelected = false;
        testSelection = 0;
        ShowMessage("EP" + selectedEpisode.ToString("D2", CultureInfo.InvariantCulture) + " selecionado, " + season.GetEpisode(selectedEpisode).Count +
            " cenas. " + mountKey + " monta; PageDown/PageUp escolhem cenas. N volta aos testes.");
    }

    // Status e somente leitura; pronta descreve TODAS as barreiras, inclusive reset apos CAMERA_PRONTA.
    private bool AreProductionSceneBarriersReady
    {
        get
        {
            return Object.ReferenceEquals(directorMesaTrial, null) && sceneReady && !Object.ReferenceEquals(directorCamera, null) && directorCamera.IsInitialized &&
                loadingTimer == null && !playing && !countingDown &&
                !awaitingCollision && !awaitingVisual && !awaitingPilot && !awaitingRehearsal &&
                !awaitingStaging && !awaitingInterpretation && !awaitingProps && !awaitingPoses &&
                !awaitingEpisodeOne && !awaitingThreeSeven && !awaitingSeasonFlight && !awaitingPlanePush &&
                !awaitingVehicles && !awaitingDoors && !awaitingSeasonWater && !awaitingWardrobe &&
                !awaitingBoateLights && !awaitingEntry && !awaitingCartaTrial && !awaitingGorjetaTrial && !awaitingTableWipe && !awaitingEpisodeReset && !awaitingPoseReset &&
                !awaitingCaptureStart && !awaitingCaptureEnd &&
                Object.ReferenceEquals(afterOwnedSeatRelease, null) && Object.ReferenceEquals(appearanceChoiceWorker, null) &&
                (Object.ReferenceEquals(directorEntry, null) || directorEntry.IsReady) &&
                (Object.ReferenceEquals(directorCartaTrial, null) || directorCartaTrial.IsReady) &&
                (Object.ReferenceEquals(directorGorjetaTrial, null) || directorGorjetaTrial.IsReady) &&
                (Object.ReferenceEquals(directorTableWipe, null) || directorTableWipe.IsReady) &&
                (Object.ReferenceEquals(directorFoldPaper86, null) || directorFoldPaper86.IsReady) &&
                (!IsAuthoredPaperC5Source(activeScene) || (!Object.ReferenceEquals(directorAuthoredPaperC5, null) && directorAuthoredPaperC5.IsReady)) &&
                (Object.ReferenceEquals(directorCast, null) || (directorCast.IsReady && !directorCast.IsBusy));
        }
    }

    // Mantem prontaFalse enquantoARM. Este predicate compartilha TODASbarreiras comstatusmanual.
    private bool IsProductionSceneReady
    {
        get { return AreProductionSceneBarriersReady &&
            (Object.ReferenceEquals(directorCapture, null) || (!directorCapture.IsArmed && !directorCapture.IsPending)); }
    }
    private string PreparedManualCountdownSession
    {
        get
        {
            if (!AreProductionSceneBarriersReady || !captureEnabled || Object.ReferenceEquals(activeScene, null) ||
                activeScene.Episode < 1 || activeScene.Episode > 15 || activeScene.DurationMs <= 0 ||
                Object.ReferenceEquals(directorCapture, null) || takeFinished || automaticCapturePending || automaticShortcutActive ||
                CanRecordAutomatically()) return "";
            return directorCapture.GetManualCountdownPreparedSession(activeScene.Id);
        }
    }

    private string ExecuteProductionOperation(OperacaoProducaoDiretor.Command command)
    {
        if (command.Action == "status")
        {
            Ped actual = Game.Player.Character;
            string manualSession = PreparedManualCountdownSession;
            return "cena=" + (Object.ReferenceEquals(activeScene, null) ? "" : activeScene.Id) +
                "; pronta=" + IsProductionSceneReady + "; contagemManualPronta=" + (manualSession.Length > 0) +
                "; contagemManualSessao=" + manualSession + "; elenco=" + (Object.ReferenceEquals(directorCast, null) || directorCast.IsReady) +
                "; jogador=" + actual.Position + "; direcao=" + actual.Heading.ToString("R", CultureInfo.InvariantCulture) +
                "; noAr=" + actual.IsInAir + "; alturaSolo=" + Function.Call<float>(Hash.GET_ENTITY_HEIGHT_ABOVE_GROUND, actual.Handle) +
                FlightSourceStatus();
        }
        if (command.Action == "limpar")
        {
            if (!Cleanup()) throw new InvalidOperationException("A limpeza ainda aguarda atores nos assentos.");
            return "Cena limpa; estados anteriores restaurados.";
        }
        if (!Object.ReferenceEquals(directorShellModelsTrial, null))
            throw new InvalidOperationException("C4 model query active; only status/limpar allowed; no mark or production.");
        if (!Object.ReferenceEquals(directorShellTrial, null))
        {
            if (command.Action == "marcar" &&
                ((command.Local == "MANSAO_DJANIRA" && (directorShellTrial.IsMadrazo || directorShellTrial.IsModern1)) ||
                 (command.Local == "CASA_VO_INTERIOR" && directorShellTrial.IsFarmOriginal)) &&
                directorShellTrial.IsReady && !directorShellTrial.IsRestoring)
            {
                RequireShellTrialSafe();
                Ped actual = Game.Player.Character;
                if (actual.IsInAir || actual.IsJumping || actual.IsFalling || actual.IsRagdoll || actual.Speed > 0.5f ||
                    Function.Call<float>(Hash.GET_ENTITY_HEIGHT_ABOVE_GROUND, actual.Handle) > 1.8f ||
                    !Function.Call<bool>(Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, actual.Handle) ||
                    Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actual.Handle) != directorShellTrial.MeasuredInterior ||
                    actual.CurrentInteriorRoomKey == 0 || actual.CurrentInteriorRoomKey == -1)
                    throw new InvalidOperationException("Protected shell mark requires the actual settled player in the measured interior; no author coordinates copied.");
                directorLocations.Save(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), command.Local, actual);
                productionLocationIndex = Array.IndexOf(productionLocations, command.Local);
                WriteLog(directorShellTrial.IsFarmOriginal ? "FARM_MARCA_INTERIOR_EXPLORATORIA" : directorShellTrial.IsModern1 ? "C4_MODERN1_MARCA_EXPLORATORIA" : "C4_MADRAZO_MARCA_EXPLORATORIA", "Id=" + command.Id + "; Local=" + command.Local + "; PlayerGetter=" + actual.Position +
                    "; Interior=" + directorShellTrial.MeasuredInterior + "; Room=" + actual.CurrentInteriorRoomKey + "; IPLSnapshotStillActive=True; RootVisualRequired=True; EmptyRoomApproved=False; ProductionReady=False");
                return "Marca real exploratoria capturada; IPL/snapshot ainda ativos; F10 restaura. Referencia IPL privada precisa de CAS Root antes de qualquer PREP futuro; nenhuma aprovacao de sala vazia/producao.";
            }
            throw new InvalidOperationException("Shell trial active; status/limpar or explicit NativeReady Madrazo mark only; local sondar remains available.");
        }
        if (command.Action == "ensaioshell")
        {
            if (command.Scene == "c4_room_models" && command.Local == "MANSAO_DJANIRA" && command.Reference == "stream_only")
            {
                if (!Cleanup()) throw new InvalidOperationException("Cleanup pending; no model query requested.");
                RequireShellTrialSafe();
                var captured = directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), "MANSAO_DJANIRA");
                directorShellModelsTrial = new C4OwnShellModelsProbeDiretor(captured, WriteLog,
                    delegate { CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog); });
                shellModelsCommand = command;
                WriteLog("C4_OWN_MODELS_COMANDO", "Id=" + command.Id + "; ExactScope=c4_room_models/MANSAO_DJANIRA/stream_only; CapturedAnchor=" + captured.Position +
                    "; PlayerMoved=False; MarkWritten=False; ActorCreated=False; CameraCreated=False; OwnSpawn=False; ProductionReady=False");
                directorShellModelsTrial.Begin(); // Retain the field before any streaming request; F10/Aborted can release partial requests.
                shellModelsReplyPending = true;
                return null; // Only after bounded query, never after a room/floor claim.
            }
            bool farmOriginalVisit = command.Scene == "oneil_farm" && command.Local == "CASA_VO" && command.Reference == "farm_original";
            bool modern1Visit = command.Scene == "modern1_room" && command.Local == "MANSAO_DJANIRA" && command.Reference == "apartment_modern1";
            if (!farmOriginalVisit && !modern1Visit && (command.Scene != "vinewood_none" || command.Local != "MANSAO_DJANIRA" ||
                (!String.IsNullOrEmpty(command.Reference) && command.Reference != "restaurar" && command.Reference != "cali_sem_mobiliario" && command.Reference != "madrazo")))
                throw new InvalidOperationException("Shell trial requires the existing mansion contract or exact oneil_farm/CASA_VO/farm_original; no mark/capture.");
            if (!Cleanup()) throw new InvalidOperationException("Cleanup pending; no shell requested.");
            RequireShellTrialSafe();
            directorShellTrial = new C4VinewoodShellTrialDiretor(dataRoot, command.Id, WriteLog, command.Reference == "restaurar", command.Reference == "cali_sem_mobiliario", command.Reference == "madrazo", farmOriginalVisit, modern1Visit);
            shellTrialCommand = command; shellTrialRecovery = command.Reference == "restaurar";
            directorShellTrial.Begin();
            shellTrialReplyPending = true;
            return null; // XML result only after bounded Native coherence or measured restoration.
        }
        if (!Object.ReferenceEquals(activeScene, null) && activeScene.Id == "EP07_C02" &&
            directorCamera.IsGalpaoWitnessPosePending)
            throw new InvalidOperationException("Camera C2: operacao aguardando getter pos-frame; status/limpar permitidos.");
        if (command.Action == "ensaiomesa")
        {
            if ((command.Scene != "prop_table_01" && command.Scene != "prop_table_03") ||
                command.Local != "BANCO_MAZE" || !String.IsNullOrEmpty(command.Reference))
                throw new InvalidOperationException("Ensaio mesa exige modelo 01/03 e BANCO_MAZE marcado.");
            if (!Cleanup()) throw new InvalidOperationException("Limpeza pendente; ensaio mesa nao iniciado.");
            Ped actual = Game.Player.Character;
            if (Object.ReferenceEquals(actual, null) || !actual.Exists() || actual.IsDead ||
                actual.IsInVehicle() || actual.IsPositionFrozen || actual.IsInAir)
                throw new InvalidOperationException("Ensaio mesa exige jogador vivo/a pe em piso real.");
            var mark = directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), "BANCO_MAZE");
            if ((actual.Position - mark.Position).Length() > 5f)
                throw new InvalidOperationException("Visite a marca real BANCO_MAZE antes do ensaio mesa.");
            double radians = mark.Heading * Math.PI / 180.0;
            Vector3 basis = new Vector3(-(float)Math.Sin(radians), (float)Math.Cos(radians), 0f);
            Vector3 measured = mark.Position + basis * 1.5f;
            float floor;
            bool found = World.GetGroundHeight(new Vector3(measured.X, measured.Y, measured.Z + 0.25f), out floor);
            bool nativeFound = found;
            float nativeFloor = floor;
            if (!found || Single.IsNaN(floor) || Single.IsInfinity(floor) || Math.Abs(measured.Z - floor) > 3f)
                found = TryClosestSceneMapFloor(measured, out floor);
            if (!found || Single.IsNaN(floor) || Single.IsInfinity(floor) || Math.Abs(measured.Z - floor) > 3f)
                throw new InvalidOperationException("Ensaio mesa nao encontrou piso nativo proximo da marca real.");
            measured.Z = floor;
            WriteLog("MESA_TRIAL_PISO", "Local=BANCO_MAZE; Mark=" + mark.Position + "; Heading=" + mark.Heading +
                "; Forward=" + basis + "; Front=1.5; NativeFound=" + nativeFound + "; NativeFloor=" + nativeFloor +
                "; MeasuredFloorPoint=" + measured + "; Actors=0; ProductionReady=False");
            MesaColisaoTrialDiretor trial = new MesaColisaoTrialDiretor(WriteLog);
            try { trial.PrepareOnMeasuredFloor(command.Scene, measured, mark.Heading); }
            catch { trial.Dispose(); throw; }
            directorMesaTrial = trial;
            mesaTrialCommand = command;
            return null; // Resposta XML somente quando o diagnostico terminar.
        }
        if (command.Action == "montar")
        {
            if (automaticCast && !directorCast.IsReady) throw new InvalidOperationException("Prepare os visuais registrados antes de montar.");
            string source;
            bool extra = command.Scene == "EP01_EXTRA_IRACI_BAR" || command.Scene == "EP01_EXTRA_DINJER_DEDO";
            if (extra)
                source = Path.Combine(projectRoot, "producao", "extras_serie", "EP01", command.Scene + ".json");
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(command.Scene, @"\AEP(0[1-9]|1[0-5])_C(0[1-9]|[1-9][0-9])\z");
                if (!match.Success) throw new InvalidOperationException("ID de producao desconhecido.");
                int episode = Int32.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int number = Int32.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                TemporadaDiretor loaded = TemporadaDiretor.Load(projectRoot);
                var entries = loaded.GetEpisode(episode);
                if (number > entries.Count || entries[number - 1].Id != command.Scene) throw new InvalidOperationException("Cena ausente do roteiro.");
                source = TemporadaDiretor.DraftPath(projectRoot, entries[number - 1]);
                season = loaded; selectedEpisode = episode; productionSceneIndex = number - 1;
            }
            if (command.Scene == "EP13_C02")
            {
                dismissalSubtake = 1; MountProduction(0);
                return "Montagem iniciada; aguarde CAMERA_PRONTA, ID=EP13_C02; Parte=A_PRIMEIRA; Integral=False";
            }
            string target = Path.Combine(scenesDirectory, command.Scene + ".json");
            PrepareProductionFile(source, target, command.Scene);
            if (extra) { selectedEpisode = 0; pilotSelected = false; testSelection = 0; }
            if (command.Scene == "EP07_C03") flightBoardingSubtake = 0;
            BeginMount(command.Scene + ".json", null, 0);
            return "Montagem iniciada; aguarde CAMERA_PRONTA, ID=" + command.Scene;
        }
        Ped player = Game.Player.Character;
        if (sceneReady || loadingTimer != null || playerStateSaved || Object.ReferenceEquals(player, null) ||
            !player.Exists() || player.IsDead || player.IsInVehicle() || player.IsPositionFrozen)
            throw new InvalidOperationException("Visitar/marcar requer cena limpa e jogador a pe/descongelado.");
        int localIndex = Array.IndexOf(productionLocations, command.Local);
        if (localIndex < 0 || command.Local == "AEROPORTO_AR") throw new InvalidOperationException("Local terrestre desconhecido.");
        if (command.Action == "marcar")
        {
            if (player.IsInAir || Function.Call<float>(Hash.GET_ENTITY_HEIGHT_ABOVE_GROUND, player.Handle) > 1.8f)
                throw new InvalidOperationException("Espere o jogador assentar fisicamente no piso antes de marcar.");
            directorLocations.Save(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), command.Local, player);
            productionLocationIndex = localIndex;
            return "Marca capturada pelos getters do jogador no jogo; local=" + command.Local;
        }
        if (command.Action == "visitar")
        {
            Vector3 position; float heading;
            if (String.IsNullOrEmpty(command.Reference))
            {
                var mark = directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), command.Local);
                position = mark.Position; heading = mark.Heading;
            }
            else if (command.Reference == "viaProxima")
            {
                Vector3 origin = player.Position;
                if (!OperacaoProducaoDiretor.ResolveClosestRoad(origin, out position, out heading))
                    throw new InvalidOperationException("O getter de via proxima nao retornou uma posicao/direcao real valida.");
                WriteLog("VISITA_VIA_PROXIMA_NATIVE", "Local=" + command.Local + "; origemGetter=" + origin + "; destinoGetter=" + position + "; direcaoGetter=" + heading.ToString("R", CultureInfo.InvariantCulture) + "; SomentePreparo=true; CapturaDeMarca=false");
            }
            else
            {
                if (command.Reference != "autor") throw new InvalidOperationException("Referencia desconhecida.");
                string path = Path.Combine(projectRoot, "producao", "locacoes_referencia", command.Local + ".xml");
                RequirePlainPath(path);
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4096 };
                var doc = new XmlDocument(); doc.XmlResolver = null;
                using (var reader = XmlReader.Create(path, settings)) doc.Load(reader);
                var element = doc.DocumentElement;
                if (Object.ReferenceEquals(element, null) || element.Name != "Referencia" || element.GetAttribute("local") != command.Local ||
                    !element.GetAttribute("origem").StartsWith("https://", StringComparison.Ordinal))
                    throw new InvalidOperationException("Referencia exige origem primaria e local correspondente.");
                float x, y, z;
                if (!Single.TryParse(element.GetAttribute("x"), NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                    !Single.TryParse(element.GetAttribute("y"), NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                    !Single.TryParse(element.GetAttribute("z"), NumberStyles.Float, CultureInfo.InvariantCulture, out z) ||
                    !Single.TryParse(element.GetAttribute("direcao"), NumberStyles.Float, CultureInfo.InvariantCulture, out heading) ||
                    Single.IsNaN(x) || Single.IsInfinity(x) || Single.IsNaN(y) || Single.IsInfinity(y) || Single.IsNaN(z) || Single.IsInfinity(z) ||
                    Single.IsNaN(heading) || Single.IsInfinity(heading) || Math.Abs(x) > 15000 || Math.Abs(y) > 15000 || Math.Abs(z) > 3000 || heading < 0 || heading >= 360)
                    throw new InvalidOperationException("Coordenadas da referencia invalidas.");
                position = new Vector3(x, y, z);
                foreach (XmlNode node in element.ChildNodes)
                {
                    var ipl = node as XmlElement;
                    if (Object.ReferenceEquals(ipl, null) || ipl.Name != "Ipl" ||
                        !System.Text.RegularExpressions.Regex.IsMatch(ipl.GetAttribute("nome"), @"\A[a-z0-9_]{1,96}\z"))
                        throw new InvalidOperationException("Referencia contem IPL invalido.");
                    Function.Call(Hash.REQUEST_IPL, ipl.GetAttribute("nome"));
                }
            }
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, position.X, position.Y, position.Z);
            player.PositionNoOffset = position; player.Heading = heading;
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, position.X, position.Y, position.Z);
            WriteLog("VISITA_REFERENCIA_PRODUCAO", "Local=" + command.Local + "; destino=" + position + "; ainda nao e marca capturada.");
            return "Visita solicitada; aguarde colisao/piso e confira imagem antes de marcar " + command.Local;
        }
        throw new InvalidOperationException("Operacao desconhecida.");
    }


    private void RequireShellTrialSafe()
    {
        Ped player = Game.Player.Character;
        if (sceneReady || loadingTimer != null || playerStateSaved || environmentSaved || playing || countingDown ||
            !Object.ReferenceEquals(activeScene, null) || createdPeds.Count != 0 ||
            Game.IsPaused || Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) || Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) ||
            !AtalhoGravacaoDiretor.IsGameForeground || !Game.Player.CanControlCharacter ||
            Object.ReferenceEquals(player, null) || !player.Exists() || player.IsDead || player.IsInVehicle() || player.IsPositionFrozen ||
            (!Object.ReferenceEquals(directorLocalOperation, null) && directorLocalOperation.IsBusy) ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)))
            throw new InvalidOperationException("Shell trial requires clean Story, no actor/scene/capture/local operation.");
        CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
    }
    private void PollShellModelsTrial()
    {
        if (Game.IsPaused || Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) || Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) ||
            !AtalhoGravacaoDiretor.IsGameForeground || !Game.Player.CanControlCharacter ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)))
        { RestoreShellModelsTrial("Guarda Story/foco/captura mudou"); return; }
        try
        {
            RequireShellTrialSafe();
            if (directorShellModelsTrial.Poll() && shellModelsReplyPending)
            {
                directorProductionOperation.CompleteShellTrial(shellModelsCommand,
                    "QueryFinished=True; ThreeLiteralModelsOnly=True; StreamAndRestBoundsAreDiagnosticOnly=True; " +
                    "OwnSpawn=False; MarkWritten=False; FloorProven=False; EmptyRoomApproved=False; Ready=False; ProductionReady=False; F10ReleasesOwnRequests=True", false);
                shellModelsReplyPending = false;
            }
        }
        catch (Exception ex) { WriteLog("C4_OWN_MODELS_ERRO", ex.ToString()); RestoreShellModelsTrial(ex.Message); }
    }
    private bool RestoreShellModelsTrial(string reason)
    {
        if (Object.ReferenceEquals(directorShellModelsTrial, null)) return true;
        try
        {
            CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
            directorShellModelsTrial.ReleaseOwnRequests();
            if (shellModelsReplyPending)
            {
                directorProductionOperation.CompleteShellTrial(shellModelsCommand,
                    reason + "; OwnRequestsReleased=True; NoEntityCreated=True; MarkWritten=False; ProductionReady=False", true);
                shellModelsReplyPending = false;
            }
            directorShellModelsTrial = null; shellModelsCommand = null;
            return true;
        }
        catch (Exception ex) { WriteLog("ERRO_RESTAURO_C4_OWN_MODELS", ex.ToString()); return false; }
    }
    private void PollShellTrial()
    {
        if (directorShellTrial.IsRestoring) { if (RestoreShellTrial("Continuacao de limpeza")) Cleanup(); return; }
        if (Game.IsPaused || Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) || Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) ||
            !AtalhoGravacaoDiretor.IsGameForeground || !Game.Player.CanControlCharacter ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)))
        { RestoreShellTrial("Guarda Story/foco/captura mudou"); return; }
        try
        {
            directorShellTrial.Poll();
            if (directorShellTrial.IsReady && shellTrialReplyPending)
            {
                directorProductionOperation.CompleteShellTrial(shellTrialCommand,
                    "NativeCoherent=True; Interior=" + directorShellTrial.MeasuredInterior +
                    "; Profile=" + directorShellTrial.Profile + "; AuthorInteriorReference=" + directorShellTrial.InteriorReferenceInfo + "; FloorAndEmptyRoomRequireRootImage=True; ProductionReady=False; F10RestoresOriginal=True", false);
                shellTrialReplyPending = false;
            }
        }
        catch (Exception ex) { WriteLog("C4_SHELL_TRIAL_ERRO", ex.ToString()); RestoreShellTrial(ex.Message); }
    }
    private bool RestoreShellTrial(string reason)
    {
        if (Object.ReferenceEquals(directorShellTrial, null)) return true;
        try
        {
            CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
            if (!directorShellTrial.Restore()) { WriteLog("C4_SHELL_RESTAURO_PENDENTE", reason + "; snapshot duravel; nenhum ARM"); return false; }
            if (shellTrialReplyPending)
                directorProductionOperation.CompleteShellTrial(shellTrialCommand, reason + "; RestoredOriginal=True; ProductionReady=False", !shellTrialRecovery);
            shellTrialReplyPending = shellTrialRecovery = false; shellTrialCommand = null; directorShellTrial = null;
            return true;
        }
        catch (Exception ex) { WriteLog("ERRO_RESTAURO_C4_SHELL", ex.ToString()); return false; }
    }

    private void PollMesaTrial()
    {
        if (Game.IsPaused || Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE) ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) || !AtalhoGravacaoDiretor.IsGameForeground ||
            !Game.Player.CanControlCharacter)
        { CancelMesaTrial("Ensaio mesa interrompido: sessao Story ativa indisponivel."); return; }
        directorMesaTrial.Poll();
        if (!directorMesaTrial.IsFinished) return;
        string conclusion = directorMesaTrial.Conclusion;
        directorMesaTrial.Dispose();
        directorMesaTrial = null;
        directorProductionOperation.CompleteMesaTrial(mesaTrialCommand,
            "Diagnostico=" + conclusion + "; ProductionReady=False; GuardsProducaoIntactas=True", conclusion == "erro_diagnostico");
        mesaTrialCommand = null;
    }

    private void CancelMesaTrial(string reason)
    {
        if (!Object.ReferenceEquals(directorMesaTrial, null))
        { directorMesaTrial.Dispose(); directorMesaTrial = null; }
        if (!Object.ReferenceEquals(mesaTrialCommand, null))
        {
            directorProductionOperation.CompleteMesaTrial(mesaTrialCommand, reason, true);
            mesaTrialCommand = null;
        }
    }

    private void MountProduction(int direction)
    {
        season = TemporadaDiretor.Load(projectRoot);
        List<TemporadaDiretor.Cena> scenes = season.GetEpisode(selectedEpisode);
        int next = productionSceneIndex + direction;
        if (next < 0 || next >= scenes.Count) { ShowMessage("Esta é a " + (next < 0 ? "primeira" : "última") + " cena do episódio."); return; }
        TemporadaDiretor.Cena entry = scenes[next];
        if (direction != 0 && !sceneReady)
        {
            productionSceneIndex = next;
            WriteLog("CENA_PRODUCAO_SELECIONADA", "Id=" + entry.Id + "; episódio=" + selectedEpisode +
                "; índice=" + (next + 1) + "; total=" + scenes.Count + "; montagem=False");
            ShowMessage("EP" + selectedEpisode.ToString("D2", CultureInfo.InvariantCulture) + " — Cena " + (next + 1) +
                " de " + scenes.Count + " — " + entry.Id + " — " + entry.Nome + ". " + mountKey + " monta.");
            return;
        }
        if (entry.Id == "EP01_C05" || (entry.Id == "EP02_C01" && clubEntrySubtake == 1) ||
            (entry.Id == "EP07_C03" && flightBoardingSubtake > 0) || entry.Id == "EP13_C02")
        {
            bool grandma = entry.Id == "EP01_C05";
            bool flight = entry.Id == "EP07_C03";
            bool dismissal = entry.Id == "EP13_C02";
            string part = dismissal ? DismissalPartName(dismissalSubtake) : flight ? (flightBoardingSubtake == 1 ? "embarque_dinjer_chefe" : "embarque_djanira_decolagem") :
                grandma ? (secretGrandmaSubtake == 0 ? "hospital" : "cassino") : "fora";
            string expectedLocal = dismissal ? "BOATE_DENTRO" : flight ? "AEROPORTO_HANGAR" :
                grandma ? (part == "hospital" ? "HOSPITAL_SANDY" : "CASSINO_FORA") : "BOATE_FORA";
            string relative = Path.Combine("subtomadas", entry.Id, part, entry.Id + ".json");
            string sourcePart = Path.Combine(projectRoot, "producao", relative);
            string targetPart = Path.Combine(scenesDirectory, relative);
            RequirePlainPath(targetPart);
            // O destino só é criado após validar modelo, atuação e a marca real.
            SceneData draft = ReadScene(File.Exists(targetPart) ? targetPart : sourcePart);
            if (draft.Id != entry.Id || draft.Encenacao != "serie_v1" || draft.LocalId != expectedLocal)
                throw new InvalidOperationException("Subtomada difere do local/ID esperado; arquivo preservado.");
            directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), draft.LocalId);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPart));
            PrepareProductionFile(sourcePart, targetPart, entry.Id);
            BeginMount(relative, null, 0);
            productionSceneIndex = next;
            return;
        }
        // Uma cena ainda em desenvolvimento permanece no índice, sem fingir que foi encenada.
        string source = TemporadaDiretor.DraftPath(projectRoot, entry);
        string target = Path.Combine(scenesDirectory, entry.Id + ".json");
        PrepareProductionFile(source, target, entry.Id);
        BeginMount(entry.Id + ".json", null, 0);
        productionSceneIndex = next;
    }

    private void SelectProductionSubtake()
    {
        bool grandma = selectedEpisode == 1 && productionSceneIndex == 4;
        bool club = selectedEpisode == 2 && productionSceneIndex == 0;
        bool flight = selectedEpisode == 7 && productionSceneIndex == 2;
        bool dismissal = selectedEpisode == 13 && productionSceneIndex == 1;
        if (!grandma && !club && !flight && !dismissal)
        { ShowMessage("T alterna EP01_C05, EP02_C01, partes do embarque EP07_C03 e entregas EP13_C02."); return; }
        if (playing || countingDown || loadingTimer != null || awaitingPoseReset || awaitingEpisodeReset ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsPending || directorCapture.IsArmed)))
        { ShowMessage("Termine a tomada e a organização antes de trocar a subtomada."); return; }
        int previous = dismissal ? dismissalSubtake : flight ? flightBoardingSubtake : grandma ? secretGrandmaSubtake : clubEntrySubtake;
        if (dismissal) dismissalSubtake = previous % 3 + 1;
        else if (flight) flightBoardingSubtake = (previous + 1) % 3;
        else if (grandma) secretGrandmaSubtake = 1 - previous;
        else clubEntrySubtake = 1 - previous;
        try { MountProduction(0); }
        catch
        {
            if (dismissal) dismissalSubtake = previous;
            else if (flight) flightBoardingSubtake = previous;
            else if (grandma) secretGrandmaSubtake = previous;
            else clubEntrySubtake = previous;
            throw;
        }
    }

    private void PrepareProductionFile(string source, string target, string id)
    {
        RequirePlainPath(source); RequirePlainPath(target);
        SceneData scene = ReadScene(File.Exists(target) ? target : source);
        if (scene.Id != id) throw new InvalidOperationException("Identidade do arquivo de produção difere da seleção.");
        if (scene.MarkedLocation) directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), scene.LocalId);
        if (File.Exists(target)) return; // Preservar edições feitas pelo usuário.
        byte[] bytes = File.ReadAllBytes(source);
        string snapshot = Path.Combine(dataRoot, "logs", "cena_preparada_" + Guid.NewGuid().ToString("N") + ".json");
        RequirePlainPath(snapshot);
        using (FileStream copy = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { copy.Write(bytes, 0, bytes.Length); copy.Flush(true); }
        if (!BytesEqual(bytes, File.ReadAllBytes(source)) || !BytesEqual(bytes, File.ReadAllBytes(snapshot)))
            throw new InvalidOperationException("A cena mudou durante a preparação; runtime preservado.");
        using (FileStream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
        if (!BytesEqual(bytes, File.ReadAllBytes(target))) throw new InvalidOperationException("A publicação da cena não confere.");
        ReadScene(target);
        WriteLog("CENA_PRODUCAO_PUBLICADA", "Id=" + id + "; snapshot=" + snapshot + "; teste visual pendente");
    }

    private void MountSelection(int direction)
    {
        if (automaticCast && !directorCast.IsReady)
        {
            if (direction != 0) { ShowMessage("Aperte " + mountKey + " primeiro para preparar o elenco."); return; }
            if (directorCast.IsBusy) { ShowMessage("Criando e registrando o elenco; " + clearKey + " cancela."); return; }
            Ped player = Game.Player.Character;
            if (!Game.Player.CanControlCharacter || Object.ReferenceEquals(player, null) || !player.Exists() || player.IsInVehicle())
                throw new InvalidOperationException("Fique a pé para preparar o elenco.");
            if (!Cleanup()) throw new InvalidOperationException("Limpe a cena anterior antes de preparar o elenco.");
            directorCast.Start(player.Position);
            mountAfterCast = true;
            ShowMessage("Diretor: criando e registrando os 13 visuais da série. Espere; depois a câmera abrirá a prévia.");
            return;
        }
        if (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsPending || directorCapture.IsArmed))
        {
            ShowMessage("Gravação em preparação ou organização. Finalize a tomada ou cancele com " + clearKey + ".");
            return;
        }
        if (loadingTimer != null || (sceneReady && !directorCamera.IsInitialized))
        {
            ShowMessage("Diretor: preparando a cena. " + clearKey + " cancela.");
            return;
        }
        if (playing || countingDown || awaitingEpisodeReset || awaitingPoseReset)
        {
            ShowMessage("Diretor: espere a tomada terminar. " + KeyLabel(restartKey) + " reinicia; " + clearKey + " limpa.");
            return;
        }
        if (selectedEpisode > 0) { MountProduction(direction); return; }
        if (testSelection == 4 && direction == 0)
        {
            PrepareProductionFile(Path.Combine(projectRoot, "cenas", "teste_fila_90.json"), Path.Combine(scenesDirectory, "teste_fila_90.json"), "teste_fila_90");
            BeginMount("teste_fila_90.json", null, 0);
            return;
        }
        if (testSelection == 3 && direction == 0)
        {
            season = TemporadaDiretor.Load(projectRoot);
            TemporadaDiretor.Cena entry = season.GetVinheta();
            string source = TemporadaDiretor.DraftPath(projectRoot, entry);
            PrepareProductionFile(source, Path.Combine(scenesDirectory, entry.Id + ".json"), entry.Id);
            BeginMount(entry.Id + ".json", null, 0);
            return;
        }
        string effectiveQueue = queueFile;
        if (testSelection == 2 && direction == 0)
        {
            PrepareTechnicalFile("teste_coreografia");
            BeginMount("teste_coreografia.json", null, 0);
            return;
        }
        if (pilotSelected && direction == 0)
        {
            PreparePilotFile();
            BeginMount("EP01_C08.json", null, 0);
            return;
        }
        if ((pilotSelected || testSelection != 0) && direction != 0)
        {
            pilotSelected = false;
            testSelection = 0;
            selectedSceneFile = "";
            direction = 0;
        }
        if (automaticCast && directorCast.IsReady && testSelection == 0 &&
            (queueFile == "fila_teste.json" || queueFile == "fila_elenco.json"))
        {
            int firstPreview = 0;
            string canonicalPath = Path.Combine(dataRoot, "catalogos", "visuais", "DJANIRA_RICA.xml");
            VisualDiretor.Load(canonicalPath, "mp_f_freemode_01");
            XmlDocument canonical = new XmlDocument(); canonical.XmlResolver = null; canonical.Load(canonicalPath);
            if (!Object.ReferenceEquals(canonical.SelectSingleNode("/OutfitPedData/DiretorOpcao"), null)) firstPreview = 3;
            int nextPreview = castPreviewIndex + direction;
            // A escolha registrada é reutilizada nas próximas sessões/temporadas.
            // As opções temporárias não são geradas outra vez após a escolha.
            if (direction >= 0 && nextPreview < firstPreview) nextPreview = firstPreview;
            if (nextPreview < firstPreview || nextPreview >= castPreviewFiles.Length)
            { ShowMessage("Esta é a " + (nextPreview < firstPreview ? "primeira" : "última") + " prévia do elenco."); return; }
            string preview = castPreviewFiles[nextPreview];
            PrepareProductionFile(Path.Combine(projectRoot, "cenas", preview), Path.Combine(scenesDirectory, preview), Path.GetFileNameWithoutExtension(preview));
            BeginMount(preview, null, 0);
            castPreviewIndex = nextPreview;
            return;
        }
        if (effectiveQueue.Length == 0)
        {
            if (direction != 0)
                throw new InvalidOperationException("Nenhuma fila configurada. " + mountKey + " monta a cena individual.");
            BeginMount(initialScene, null, 0);
            return;
        }

        // Reler e validar a fila inteira antes de alterar a seleção ou limpar entidades.
        CenaJsonDiretor.Fila queue = CenaJsonDiretor.LoadFila(Path.Combine(scenesDirectory, effectiveQueue),
            Path.Combine(dataRoot, "catalogos"), cleanupRadius);
        int current = String.IsNullOrEmpty(selectedSceneFile) ? 0 : queue.Arquivos.IndexOf(selectedSceneFile);
        if (current < 0)
        {
            if (direction != 0)
                throw new InvalidOperationException("A cena selecionada saiu da fila. Use " + mountKey + " para carregar a primeira.");
            current = 0;
        }
        int next = current + direction;
        if (next < 0 || next >= queue.Arquivos.Count)
        {
            ShowMessage(next < 0 ? "Diretor: esta é a primeira cena da fila." : "Diretor: esta é a última cena da fila.");
            return;
        }
        BeginMount(queue.Arquivos[next], queue, next);
    }

    // EP02_C01/C02 only. Source: Bob74 037dd6c71f6894be771e9343008223d7a3305d79,
    // dlc_afterhours/nightclubs.lua 47..65,639..660; lib/common.lua 89..124.
    private static readonly string[] Ep02NightclubStyleSets = { "Int01_ba_Style01", "Int01_ba_Style02", "Int01_ba_Style03" };
    private readonly bool[] ep02NightclubStyleOriginal = new bool[3];
    private int ep02NightclubStyleInterior, ep02NightclubStyleRequestFrame, ep02NightclubStyleVerifiedFrame = -1, ep02NightclubStyleStableFrames;
    private bool ep02NightclubStyleSaved, ep02NightclubStyleChanged, ep02NightclubStyleReady;
    private string ep02NightclubStyleScene, ep02NightclubStyleJournal;
    private Stopwatch ep02NightclubStyleTimer;
    private long ep02NightclubStyleLastLogMs;

    private bool PollEp02NightclubStyleBeforeFloor(int actualInterior)
    {
        if (Object.ReferenceEquals(activeScene, null) || (activeScene.Id != "EP02_C01" && activeScene.Id != "EP02_C02") ||
            activeScene.LocalId != "BOATE_DENTRO" || !activeScene.MarkedLocation || !awaitingCollision ||
            Object.ReferenceEquals(hiddenPlayer, null) || !hiddenPlayer.Exists() || hiddenPlayer.IsDead ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) || Function.Call<bool>(Hash.NETWORK_IS_IN_SESSION) ||
            actualInterior <= 0 || !Function.Call<bool>(Hash.IS_VALID_INTERIOR, actualInterior) ||
            !Function.Call<bool>(Hash.IS_INTERIOR_READY, actualInterior))
            throw new InvalidOperationException("EP02 estilo: exige interior real pronto, marca real e jogador vivo no modo historia antes dos pisos.");
        if (!ep02NightclubStyleSaved)
        {
            ep02NightclubStyleInterior = actualInterior; ep02NightclubStyleScene = activeScene.Id;
            bool any = false;
            for (int i = 0; i < 3; i++)
            {
                ep02NightclubStyleOriginal[i] = Function.Call<bool>(Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, actualInterior, Ep02NightclubStyleSets[i]);
                any |= ep02NightclubStyleOriginal[i];
            }
            string folder = Path.Combine(projectRoot, "backups", "ep02-boate-estilo");
            RequirePlainPath(folder); Directory.CreateDirectory(folder);
            ep02NightclubStyleJournal = Path.Combine(folder, DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
            ep02NightclubStyleRequestFrame = Game.FrameCount;
            ep02NightclubStyleSaved = true; ep02NightclubStyleChanged = false; ep02NightclubStyleReady = false;
            ep02NightclubStyleVerifiedFrame = -1; ep02NightclubStyleStableFrames = 0;
            // A complete durable snapshot precedes the first entity-set mutation.
            WriteEp02NightclubStyleJournal("before", "snapshot_before_any_setter");
            WriteLog("EP02_BOATE_ESTILO_SNAPSHOT", "Cena=" + ep02NightclubStyleScene + "; InteriorReal=" + actualInterior +
                "; " + Ep02NightclubStyleOriginalText() + "; Journal=" + ep02NightclubStyleJournal + ".before.xml; AntesDosPisos=True; NenhumSetterAinda=True;");
            ep02NightclubStyleTimer = Stopwatch.StartNew(); ep02NightclubStyleLastLogMs = 0;
            if (!any)
            {
                ep02NightclubStyleChanged = true; // Retain restoration duty even if the native throws.
                Function.Call(Hash.ACTIVATE_INTERIOR_ENTITY_SET, actualInterior, Ep02NightclubStyleSets[1]);
                Function.Call(Hash.REFRESH_INTERIOR, actualInterior);
                WriteLog("EP02_BOATE_ESTILO_SOLICITADO", "Cena=" + ep02NightclubStyleScene + "; InteriorReal=" + actualInterior +
                    "; Ativado=Int01_ba_Style02; TresOriginais=False; OutrosEntitySetsAlterados=False; TintAlterado=False; PinAlterado=False; ProximosDoisTicksObrigatorios=True; ChaoAprovado=False;");
            }
            else
                WriteLog("EP02_BOATE_ESTILO_PREEXISTENTE", "Cena=" + ep02NightclubStyleScene + "; InteriorReal=" + actualInterior +
                    "; " + Ep02NightclubStyleOriginalText() + "; TodosEntitySetsPreservados=True; SetterExecutado=False; ProximosDoisTicksObrigatorios=True;");
            return false;
        }
        if (actualInterior != ep02NightclubStyleInterior || activeScene.Id != ep02NightclubStyleScene)
            throw new InvalidOperationException("EP02 estilo: identidade do interior/cena mudou; snapshot preservado sem alterar outro interior.");
        bool expected = true; bool[] observed = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            observed[i] = Function.Call<bool>(Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, actualInterior, Ep02NightclubStyleSets[i]);
            if (observed[i] != (ep02NightclubStyleChanged ? i == 1 : ep02NightclubStyleOriginal[i])) expected = false;
        }
        if (!expected)
        {
            ep02NightclubStyleStableFrames = 0;
            if (!Object.ReferenceEquals(ep02NightclubStyleTimer, null) && ep02NightclubStyleTimer.ElapsedMilliseconds >= 5000)
                throw new InvalidOperationException("EP02 estilo: tres getters nao confirmaram em5s; snapshot/restauro conservados, sem flexibilizar o piso.");
            if (!Object.ReferenceEquals(ep02NightclubStyleTimer, null) && ep02NightclubStyleTimer.ElapsedMilliseconds - ep02NightclubStyleLastLogMs >= 500)
            {
                ep02NightclubStyleLastLogMs = ep02NightclubStyleTimer.ElapsedMilliseconds;
                WriteLog("EP02_BOATE_ESTILO_AGUARDANDO", "Cena=" + ep02NightclubStyleScene + "; InteriorReal=" + actualInterior +
                    "; Style01=" + observed[0] + "; Style02=" + observed[1] + "; Style03=" + observed[2] + "; ElapsedMs=" + ep02NightclubStyleLastLogMs + "; SnapshotPreservado=True; ChaoAprovado=False;");
            }
            return false;
        }
        if (ep02NightclubStyleReady) return true;
        if (Game.FrameCount <= ep02NightclubStyleRequestFrame || Game.FrameCount == ep02NightclubStyleVerifiedFrame) return false;
        ep02NightclubStyleVerifiedFrame = Game.FrameCount; ep02NightclubStyleStableFrames++;
        if (ep02NightclubStyleStableFrames < 2) return false;
        WriteEp02NightclubStyleJournal("applied", ep02NightclubStyleChanged ? "style02_three_getters_two_later_ticks" : "existing_style_preserved_three_getters_two_later_ticks");
        ep02NightclubStyleReady = true;
        WriteLog("EP02_BOATE_ESTILO_CONFIRMADO", "Cena=" + ep02NightclubStyleScene + "; InteriorReal=" + actualInterior +
            "; Style01=" + observed[0] + "; Style02=" + observed[1] + "; Style03=" + observed[2] + "; DoisFramesPosteriores=True; EstiloAlterado=" + ep02NightclubStyleChanged +
            "; OutrosEntitySetsAlterados=False; PisoAindaExigeMedicao=True; AprovacaoVisual=False;");
        return true;
    }

    private void RestoreEp02NightclubStyle()
    {
        if (!ep02NightclubStyleSaved) return;
        if (Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) || Function.Call<bool>(Hash.NETWORK_IS_IN_SESSION) ||
            ep02NightclubStyleInterior <= 0 || !Function.Call<bool>(Hash.IS_VALID_INTERIOR, ep02NightclubStyleInterior) ||
            !Function.Call<bool>(Hash.IS_IPL_ACTIVE, "ba_int_placement_ba_interior_0_dlc_int_01_ba_milo_"))
            throw new InvalidOperationException("EP02 estilo: interior original/IPL nao estao validos no modo historia; snapshot/journal conservados antes de removerIPL/ON_ENTER_SP.");
        if (ep02NightclubStyleChanged)
        {
            WriteLog("EP02_BOATE_ESTILO_RESTAURAR", "Cena=" + ep02NightclubStyleScene + "; InteriorReal=" + ep02NightclubStyleInterior +
                "; " + Ep02NightclubStyleOriginalText() + "; AntesRemoverIPL=True; AntesON_ENTER_SP=True;");
            for (int i = 0; i < 3; i++)
            {
                bool observed = Function.Call<bool>(Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, ep02NightclubStyleInterior, Ep02NightclubStyleSets[i]);
                if (observed == ep02NightclubStyleOriginal[i]) continue;
                Function.Call(ep02NightclubStyleOriginal[i] ? Hash.ACTIVATE_INTERIOR_ENTITY_SET : Hash.DEACTIVATE_INTERIOR_ENTITY_SET,
                    ep02NightclubStyleInterior, Ep02NightclubStyleSets[i]);
            }
            Function.Call(Hash.REFRESH_INTERIOR, ep02NightclubStyleInterior);
            for (int i = 0; i < 3; i++)
                if (Function.Call<bool>(Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, ep02NightclubStyleInterior, Ep02NightclubStyleSets[i]) != ep02NightclubStyleOriginal[i])
                    throw new InvalidOperationException("EP02 estilo: getter de restauro divergiu; snapshot/journal conservados e IPL/ON_ENTER_SP bloqueados.");
        }
        WriteEp02NightclubStyleJournal("restored", ep02NightclubStyleChanged ? "original_three_getters_after_refresh_verified_before_ipl_release" : "no_style_mutation_original_preserved");
        WriteLog("EP02_BOATE_ESTILO_RESTAURADO", "Cena=" + ep02NightclubStyleScene + "; InteriorReal=" + ep02NightclubStyleInterior +
            "; " + Ep02NightclubStyleOriginalText() + "; SetterRestauroExecutado=" + ep02NightclubStyleChanged + "; GettersRestauro=" + ep02NightclubStyleChanged +
            "; EstiloPreexistentePreservado=" + !ep02NightclubStyleChanged + "; AntesRemoverIPL=True; AntesON_ENTER_SP=True; Journal=" + ep02NightclubStyleJournal + ".restored.xml;");
        ep02NightclubStyleSaved = ep02NightclubStyleChanged = ep02NightclubStyleReady = false;
        ep02NightclubStyleInterior = ep02NightclubStyleStableFrames = 0; ep02NightclubStyleTimer = null;
        ep02NightclubStyleScene = ep02NightclubStyleJournal = null;
    }

    private string Ep02NightclubStyleOriginalText()
    { return "Style01Original=" + ep02NightclubStyleOriginal[0] + "; Style02Original=" + ep02NightclubStyleOriginal[1] + "; Style03Original=" + ep02NightclubStyleOriginal[2]; }
    private void WriteEp02NightclubStyleJournal(string suffix, string state)
    {
        string path = ep02NightclubStyleJournal + "." + suffix + ".xml"; RequirePlainPath(path);
        string text = "<Ep02NightclubStyle versao=\"1\" source=\"Bob74-037dd6c71f6894be771e9343008223d7a3305d79\" estado=\"" + state +
            "\" cena=\"" + ep02NightclubStyleScene + "\" interiorReal=\"" + ep02NightclubStyleInterior.ToString(CultureInfo.InvariantCulture) +
            "\" originalStyle01=\"" + ep02NightclubStyleOriginal[0].ToString().ToLowerInvariant() + "\" originalStyle02=\"" + ep02NightclubStyleOriginal[1].ToString().ToLowerInvariant() +
            "\" originalStyle03=\"" + ep02NightclubStyleOriginal[2].ToString().ToLowerInvariant() + "\" requestFrame=\"" + ep02NightclubStyleRequestFrame.ToString(CultureInfo.InvariantCulture) +
            "\" originalSnapshotBeforeSetter=\"true\" otherEntitySetsChanged=\"false\" />";
        if (File.Exists(path))
        {
            if (File.ReadAllText(path, Encoding.UTF8) != text) throw new InvalidOperationException("EP02 estilo: journal existente divergiu; preservar ambos estados sem sobrescrever.");
            return;
        }
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
    }

    private readonly List<string> sceneOwnedIpls = new List<string>();
    private int dismissalInteriorBeforeFloor;
    private bool dismissalInteriorRefreshedBeforeFloor, dismissalInteriorBeforeFloorLogged;
    private long dismissalInteriorBeforeFloorLastLogMs;
    // EP02 refilmagem: bootstrap temporario de recursos DLC no modo historia.
    // Sem getter global de GROUP_MAP: nunca afirmar snapshot/restauro exato desse grupo.
    private bool ep02OwnDlcMapBootstrap;
    private string ep02DlcMapBootstrapScene;
    private void PrepareEp02DlcMapResources()
    {
        if (Object.ReferenceEquals(activeScene, null) ||
            (activeScene.Id != "EP02_C01" && activeScene.Id != "EP02_C02") ||
            activeScene.LocalId != "BOATE_DENTRO" || !activeScene.MarkedLocation ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) ||
            Function.Call<bool>(Hash.NETWORK_IS_IN_SESSION) ||
            Object.ReferenceEquals(hiddenPlayer, null) || !hiddenPlayer.Exists() || hiddenPlayer.IsDead)
            throw new InvalidOperationException("EP02 DLC: bootstrap exige boate marcada e jogador vivo no modo historia.");
        if (ep02OwnDlcMapBootstrap) return;
        ep02OwnDlcMapBootstrap = true; // Conservar para Cleanup mesmo se a chamada for interrompida.
        ep02DlcMapBootstrapScene = activeScene.Id;
        WriteLog("EP02_DLC_MAPAS_PREPARO", "Cena=" + ep02DlcMapBootstrapScene +
            "; IPLAtivoAntes=False; ModoHistoria=True; EstadoGlobalAnteriorMedido=False; RestauroGlobalExatoAlegado=False; EntradaSessaoOnline=False;");
        Function.Call(Hash.SET_INSTANCE_PRIORITY_MODE, true);
        try
        {
            // API191 documenta ON_ENTER_MP como exemplo de CallLongBlockingFunc.
            // Carrega recursos GROUP_MAP; nao chama qualquer operacao de entrar em rede.
            Function.CallLongBlockingFunc(Hash.ON_ENTER_MP);
        }
        finally { Function.Call(Hash.SET_INSTANCE_PRIORITY_MODE, false); }
        WriteLog("EP02_DLC_MAPAS_SOLICITADOS", "Cena=" + ep02DlcMapBootstrapScene +
            "; ON_ENTER_MP=True; PrioridadeInstancia=False; EstadoGlobalAnteriorMedido=False; RestauroGlobalExatoAlegado=False; InteriorEPisoAindaObrigatorios=True;");
    }
    private void RestoreEp02DlcMapResources()
    {
        if (!ep02OwnDlcMapBootstrap) return;
        if (createdPeds.Count != 0 || sceneOwnedIpls.Count != 0 || playerStateSaved || ep02NightclubStyleSaved ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) ||
            Function.Call<bool>(Hash.NETWORK_IS_IN_SESSION))
            throw new InvalidOperationException("EP02 DLC: retornar recursos SP somente apos jogador/atores/IPLs proprios limpos no modo historia.");
        WriteLog("EP02_DLC_MAPAS_RETORNO_SP", "Cena=" + ep02DlcMapBootstrapScene +
            "; AtoresProprios=0; IPLsProprios=0; JogadorRestaurado=True; EstadoGlobalAnteriorMedido=False; RestauroGlobalExatoAlegado=False;");
        Function.Call(Hash.SET_INSTANCE_PRIORITY_MODE, true);
        try { Function.CallLongBlockingFunc(Hash.ON_ENTER_SP); }
        finally { Function.Call(Hash.SET_INSTANCE_PRIORITY_MODE, false); }
        ep02OwnDlcMapBootstrap = false;
        WriteLog("EP02_DLC_MAPAS_RECURSOS_SP", "Cena=" + ep02DlcMapBootstrapScene +
            "; ON_ENTER_SP=True; PrioridadeInstancia=False; EstadoGlobalAnteriorMedido=False; RestauroGlobalExatoAlegado=False; EntradaSessaoOnline=False;");
        ep02DlcMapBootstrapScene = null;
    }

    private void RequestMarkedSceneIpls(string local, Vector3 markedPosition)
    {
        string path = Path.Combine(projectRoot, "producao", "locacoes_referencia", local + ".xml");
        RequirePlainPath(path);
        if (!File.Exists(path)) return;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4096 };
        var doc = new XmlDocument(); doc.XmlResolver = null;
        using (var reader = XmlReader.Create(path, settings)) doc.Load(reader);
        var root = doc.DocumentElement;
        if (Object.ReferenceEquals(root, null) || root.Name != "Referencia" || root.GetAttribute("local") != local ||
            !root.GetAttribute("origem").StartsWith("https://", StringComparison.Ordinal))
            throw new InvalidOperationException("Referencia de IPL exige origem primaria e local correspondente.");
        var names = new List<string>();
        foreach (XmlNode node in root.ChildNodes)
        {
            var item = node as XmlElement;
            if (Object.ReferenceEquals(item, null) || item.Name != "Ipl" ||
                !System.Text.RegularExpressions.Regex.IsMatch(item.GetAttribute("nome"), @"\A[a-z0-9_]{1,96}\z"))
                throw new InvalidOperationException("Referencia de IPL invalida; marca real preservada.");
            names.Add(item.GetAttribute("nome"));
        }
        if (names.Count == 0) return;
        float referenceX, referenceY, referenceZ;
        if (!Single.TryParse(root.GetAttribute("x"), NumberStyles.Float, CultureInfo.InvariantCulture, out referenceX) ||
            !Single.TryParse(root.GetAttribute("y"), NumberStyles.Float, CultureInfo.InvariantCulture, out referenceY) ||
            !Single.TryParse(root.GetAttribute("z"), NumberStyles.Float, CultureInfo.InvariantCulture, out referenceZ) ||
            Single.IsNaN(referenceX) || Single.IsInfinity(referenceX) ||
            Single.IsNaN(referenceY) || Single.IsInfinity(referenceY) ||
            Single.IsNaN(referenceZ) || Single.IsInfinity(referenceZ) ||
            Single.IsNaN(markedPosition.X) || Single.IsInfinity(markedPosition.X) ||
            Single.IsNaN(markedPosition.Y) || Single.IsInfinity(markedPosition.Y) ||
            Single.IsNaN(markedPosition.Z) || Single.IsInfinity(markedPosition.Z) ||
            Math.Abs(referenceX) > 15000f || Math.Abs(referenceY) > 15000f || Math.Abs(referenceZ) > 3000f)
            throw new InvalidOperationException("Referencia de IPL exige XYZ finito; marca real preservada.");
        // O codigo narrativo nao prova que a marca ainda esteja no mesmo edificio da referencia.
        // Esta janela e apenas confinamento do pedido de IPL; nao aprova chao/interior/captura.
        double deltaX = (double)markedPosition.X - referenceX;
        double deltaY = (double)markedPosition.Y - referenceY;
        double deltaZ = (double)markedPosition.Z - referenceZ;
        double distanceSquaredXY = deltaX * deltaX + deltaY * deltaY;
        if (distanceSquaredXY > 625.0 || Math.Abs(deltaZ) > 10.0)
        {
            WriteLog("LOCAL_IPL_REFERENCIA_DISTANTE", "Local=" + local + "; Marca=" + markedPosition +
                "; Referencia=" + new Vector3(referenceX, referenceY, referenceZ) +
                "; DistanciaXY=" + Math.Sqrt(distanceSquaredXY).ToString("R", CultureInfo.InvariantCulture) +
                "; DeltaZ=" + deltaZ.ToString("R", CultureInfo.InvariantCulture) +
                "; LimiteXY=25; LimiteZ=10; PedidoIPL=false; XYZMarcaPreservado=true; ChaoAprovado=false; Origem=" + path);
            return;
        }
        foreach (string name in names)
        {
            bool active = Function.Call<bool>(Hash.IS_IPL_ACTIVE, name);
            if (!active)
            {
                if (!sceneOwnedIpls.Contains(name)) sceneOwnedIpls.Add(name);
                if (local == "BOATE_DENTRO" && name == "ba_int_placement_ba_interior_0_dlc_int_01_ba_milo_" &&
                    !Object.ReferenceEquals(activeScene, null) &&
                    (activeScene.Id == "EP02_C01" || activeScene.Id == "EP02_C02"))
                    PrepareEp02DlcMapResources();
                Function.Call(Hash.REQUEST_IPL, name);
            }
            WriteLog("LOCAL_IPL_PREPARO", "Local=" + local + "; IPL=" + name + "; AtivoAntes=" + active + "; Origem=" + path + "; XYZMarcaPreservado=true");
        }
    }
    private void RestoreMarkedSceneIpls()
    {
        RestoreEp02NightclubStyle(); // Verified before releasing any owned IPL or GROUP_MAP resource.
        for (int i = sceneOwnedIpls.Count - 1; i >= 0; i--)
        {
            string name = sceneOwnedIpls[i];
            if (Function.Call<bool>(Hash.IS_IPL_ACTIVE, name)) Function.Call(Hash.REMOVE_IPL, name);
            if (Function.Call<bool>(Hash.IS_IPL_ACTIVE, name)) throw new InvalidOperationException("IPL proprio ainda ativo: " + name);
            sceneOwnedIpls.RemoveAt(i);
            WriteLog("LOCAL_IPL_RESTAURADO", "IPL=" + name + "; EstadoOriginal=false; SomenteRecursoPedidoPeloDiretor=true");
        }
        RestoreEp02DlcMapResources();
    }

    // EP13_C02 e refilmagens EP02_C01/C02: IPL frio nao comprova instancia pronta para medir os pisos.
    // Getter do interior vem do jogador real ja movido na preparacao; nenhum id/XYZ de autor vira marca.
    private bool PollDismissalInteriorBeforeFloor()
    {
        if (Object.ReferenceEquals(activeScene, null) ||
            (activeScene.Id != "EP13_C02" && activeScene.Id != "EP02_C01" && activeScene.Id != "EP02_C02")) return true;
        string interiorLogPrefix = activeScene.Id.Substring(0, 4);
        string interiorLabel = activeScene.Id == "EP13_C02" ? "C13" : activeScene.Id;
        if (activeScene.LocalId != "BOATE_DENTRO" || !activeScene.MarkedLocation || !awaitingCollision ||
            Object.ReferenceEquals(hiddenPlayer, null) || !hiddenPlayer.Exists() || hiddenPlayer.IsDead ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) || Object.ReferenceEquals(loadingTimer, null))
            throw new InvalidOperationException(interiorLabel + " interior: preparo exige boate marcada/jogador proprio/modo historia, antes dos pisos.");
        const string ipl = "ba_int_placement_ba_interior_0_dlc_int_01_ba_milo_";
        Vector3 position = hiddenPlayer.Position;
        bool active = Function.Call<bool>(Hash.IS_IPL_ACTIVE, ipl);
        int fromEntity = Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, hiddenPlayer.Handle);
        int atPosition = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, position.X, position.Y, position.Z);
        if (fromEntity > 0 && atPosition > 0 && fromEntity != atPosition)
            throw new InvalidOperationException(interiorLabel + " interior: getters do jogador e posicao discordam; nenhum id fixo sera usado.");
        int actual = fromEntity > 0 ? fromEntity : atPosition;
        bool valid = actual > 0 && Function.Call<bool>(Hash.IS_VALID_INTERIOR, actual);
        bool ready = valid && Function.Call<bool>(Hash.IS_INTERIOR_READY, actual);
        bool disabled = valid && Function.Call<bool>(Hash.IS_INTERIOR_DISABLED, actual);
        bool capped = valid && Function.Call<bool>(Hash.IS_INTERIOR_CAPPED, actual);
        long elapsed = loadingTimer.ElapsedMilliseconds;
        if (!dismissalInteriorBeforeFloorLogged || elapsed - dismissalInteriorBeforeFloorLastLogMs >= 500 ||
            (active && valid && ready && !dismissalInteriorRefreshedBeforeFloor))
        {
            bool style1 = valid && Function.Call<bool>(Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, actual, "Int01_ba_Style01");
            bool style2 = valid && Function.Call<bool>(Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, actual, "Int01_ba_Style02");
            bool style3 = valid && Function.Call<bool>(Hash.IS_INTERIOR_ENTITY_SET_ACTIVE, actual, "Int01_ba_Style03");
            WriteLog(interiorLogPrefix + "_INTERIOR_ANTES_PISO", "Cena=" + activeScene.Id + "; IPLAtivo=" + active + "; Jogador=" + hiddenPlayer.Handle +
                "; PosicaoAtual=" + position + "; FromEntity=" + fromEntity + "; AtPosition=" + atPosition + "; InteriorReal=" + actual +
                "; Valido=" + valid + "; Pronto=" + ready + "; Disabled=" + disabled + "; Capped=" + capped +
                "; Style01=" + style1 + "; Style02=" + style2 + "; Style03=" + style3 + "; ElapsedMs=" + elapsed +
                "; RefreshIssued=" + dismissalInteriorRefreshedBeforeFloor + "; EntitySetsAlterados=False; PinAlterado=False; AtoresCriados=" + createdPeds.Count + "; ChaoAprovado=False");
            dismissalInteriorBeforeFloorLogged = true; dismissalInteriorBeforeFloorLastLogMs = elapsed;
        }
        if (!active || !valid || !ready)
        {
            if (elapsed >= locationTimeout)
                throw new InvalidOperationException(interiorLabel + " interior: IPL/instancia real nao ficou pronta no limite existente; nao medir piso inexistente.");
            return false;
        }
        if (disabled || capped)
            throw new InvalidOperationException(interiorLabel + " interior: instancia pronta mas disabled/capped; diagnostico preservado, nenhum setter de estado original.");
        if (dismissalInteriorBeforeFloor != 0 && dismissalInteriorBeforeFloor != actual)
            throw new InvalidOperationException(interiorLabel + " interior: identidade do interior mudou durante preparo.");
        dismissalInteriorBeforeFloor = actual;
        if (!dismissalInteriorRefreshedBeforeFloor)
        {
            // Refresh so da instancia REAL ja carregada, sem ativar/desativar qualquer entity set.
            Function.Call(Hash.REFRESH_INTERIOR, actual);
            dismissalInteriorRefreshedBeforeFloor = true;
            WriteLog(interiorLogPrefix + "_INTERIOR_REFRESH_ANTES_PISO", "Cena=" + activeScene.Id + "; InteriorReal=" + actual + "; ProntoAntes=True; EntitySetsAlterados=False; PinAlterado=False; ChaoAprovado=False; ProximoTickObrigatorio=True");
            return false;
        }
        if ((activeScene.Id == "EP02_C01" || activeScene.Id == "EP02_C02") && !PollEp02NightclubStyleBeforeFloor(actual)) return false;
        return true; // Ainda exige todas as medicoes de piso/caminhada originais, sem tolerancia nova.
    }

    private void BeginMount(string sceneFile, CenaJsonDiretor.Fila queue, int index)
    {
        Ped player = Game.Player.Character;
        if (!Game.Player.CanControlCharacter || Object.ReferenceEquals(player, null) || !player.Exists() || player.IsInVehicle())
            throw new InvalidOperationException("Fique a pé, em uma área plana com espaço à frente, para montar.");

        // Validar primeiro: um arquivo inválido não apaga uma tomada já montada.
        string nextFlightHash = null;
        string mountingSource = Path.Combine(scenesDirectory, sceneFile);
        if (Path.GetFileName(sceneFile) == "EP07_C03.json")
        {
            CenaVooTemporadaDiretor.PartFromSource("EP07_C03", sceneFile);
            nextFlightHash = FlightSourceHash(mountingSource);
        }
        string nextDismissalHash = Path.GetFileName(sceneFile) == "EP13_C02.json" ? FlightSourceHash(mountingSource) : null;
        string nextPurchaseHash = Path.GetFileName(sceneFile) == "EP13_C03.json" ? FlightSourceHash(mountingSource) : null;
        SceneData scene = ReadScene(mountingSource);
        if (scene.Id == "teste_fila_98") RequirePocket98Contract(scene, sceneFile);
        if (scene.Id == "teste_fila_99") RequireOpening99Contract(scene, sceneFile);
        if (IsAuthoredPaperC5Source(scene))
        {
            RequireAuthoredPaperC5Contract(scene, sceneFile);
            CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
        }
        if (IsPaperPoly86Source(scene, sceneFile))
        {
            RequirePaperPoly86Contract(scene, sceneFile);
            CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
        }
        if (scene.Id == "teste_fila_84") RequireOfficeCell84Contract(scene, sceneFile);
        if (scene.Id == "teste_fila_85") RequireWriting85Contract(scene, sceneFile);
        if (scene.Id == "EP10_C06") RequireWritingC6Contract(scene, sceneFile);
        if (scene.Id == "EP13_C02") RequireDismissalSceneContract(scene, sceneFile);
        if (scene.Id == "EP13_C03") RequirePurchaseSceneContract(scene, sceneFile, nextPurchaseHash);
        if (nextPurchaseHash != null && (scene.Id != "EP13_C03" || nextPurchaseHash != FlightSourceHash(mountingSource)))
            throw new InvalidOperationException("Fonte EP13_C03 mudou durante leitura; montagem cancelada antes do ambiente.");
        if (nextDismissalHash != null && (scene.Id != "EP13_C02" || nextDismissalHash != FlightSourceHash(mountingSource)))
            throw new InvalidOperationException("Fonte EP13_C02 mudou durante leitura; remonte a parte correta.");
        if (nextFlightHash != null && (scene.Id != "EP07_C03" || nextFlightHash != FlightSourceHash(mountingSource)))
            throw new InvalidOperationException("Fonte C3 mudou durante leitura/validacao; montagem cancelada.");
        LocaisDiretor.MarcaLocal location = scene.MarkedLocation ?
            directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), scene.LocalId) : null;
        CenaQuedaDiretor.QuedaSpec nextFall = scene.Id == "EP01_C01" || scene.Id == "EP07_C04" || scene.Id == "EP07_C05" || scene.SaidaAgua ?
            EpisodioUmDiretor.LoadQuedaSpec(directorLocations, Path.Combine(dataRoot, "catalogos", "locais_marcados.xml")) : null;
        if (!Object.ReferenceEquals(nextFall, null)) nextFall.InverterEixo = scene.InvertAxis;
        if (scene.Id == "EP01_C01" && scene.LocalId != "CANAL_LS")
            throw new InvalidOperationException("A queda completa exige CANAL_LS real e os três subpontos reais da rampa/fuga.");
        if (scene.Id == "EP01_C01" && (scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" ||
            scene.Actions.Count != 0 || scene.Direcao.Count != 0 || scene.Poses.Count != 0 ||
            scene.Objetos.Count != 0 || scene.Interpretacao.Count != 0))
            throw new InvalidOperationException("A queda usa Djanira sozinha e atuação física própria; não admite ações, poses ou objetos conflitantes.");
        if (scene.Id == "teste_fila_90" && (scene.LocalId != "CANAL_LS" || scene.Encenacao != "serie_v1" ||
            scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" || scene.Actions.Count != 0 || scene.Direcao.Count != 0))
            throw new InvalidOperationException("O ensaio de queda exige Djanira sozinha, CANAL_LS real e direção técnica do helper.");
        if (scene.Actors.Count > maxActors)
            throw new InvalidOperationException("A cena ultrapassa o limite de " + maxActors + " personagens.");
        if (EpisodioUmDiretor.Supports(scene.Id) && scene.Encenacao != "serie_v1")
            throw new InvalidOperationException("A atuação completa dessa cena exige encenacao=serie_v1; o arquivo editado foi preservado.");
        if (EpisodioUmDiretor.Supports(scene.Id) && scene.DurationMs != EpisodioUmDiretor.ExpectedDurationMs(scene.Id))
            throw new InvalidOperationException("A duração desta atuação deve ser " + EpisodioUmDiretor.ExpectedDurationMs(scene.Id) + " ms; o arquivo editado foi preservado.");
        if (scene.EmpurraoAviao && (scene.Id != "EP07_C05" || scene.Encenacao != "serie_v1" || scene.LocalId != "CANAL_LS" || scene.DurationMs != 5000 ||
            scene.Actors.Count != 3 || !scene.Actors.Exists(delegate(ActorData item) { return item.Id == "djanira_pobre"; }) ||
            !scene.Actors.Exists(delegate(ActorData item) { return item.Id == "o_chefe"; }) || !scene.Actors.Exists(delegate(ActorData item) { return item.Id == "dinjer"; }) ||
            scene.Actions.Count != 0 || scene.Direcao.Count != 0 || scene.Interpretacao.Count != 0 || scene.Objetos.Count != 0 || scene.Poses.Count != 0 ||
            scene.TransicoesPose.Count != 0 || scene.Veiculos.Count != 0 || scene.Portas.Count != 0 || scene.SaidaAgua))
            throw new InvalidOperationException("O inserto do empurrão exige EP07_C05 silenciosa5s, três atores originais e somente controlador físico próprio.");
        if (DeferOwnedSeatRelease(delegate { BeginMount(sceneFile, queue, index); })) return;
        if (!Cleanup())
            throw new InvalidOperationException("Limpeza anterior incompleta. Tente Limpar novamente.");

        activeQueue = queue;
        queueIndex = index;
        selectedSceneFile = sceneFile;
        activeScene = scene;
        SaveC4Viewport();
        mountedFlightSourceHash = nextFlightHash;
        mountedDismissalSourceHash = nextDismissalHash;
        mountedPurchaseSourceHash = nextPurchaseHash;
        if (scene.Id == "EP13_C02") dismissalSubtake = DismissalPartIndex(sceneFile);
        if (scene.Id == "EP07_C03")
            flightBoardingSubtake = (int)CenaVooTemporadaDiretor.PartFromSource(scene.Id, sceneFile);
        episodeOneFallSpec = nextFall;
        if (scene.Id == "teste_fila_90" || scene.Id == "EP01_C01" || scene.Id == "EP07_C04" || scene.Id == "EP07_C05" || scene.SaidaAgua) technicalWaterMark = location.Position;
        WriteLog("FILA_SELECAO", "Arquivo=" + sceneFile + "; fila=" + queueFile + "; índice=" + (index + 1) +
            "; total=" + (Object.ReferenceEquals(queue, null) ? 1 : queue.Arquivos.Count));
        if (scene.Episode > 0)
            Directory.CreateDirectory(Path.Combine(dataRoot, "Serie", "A_CALOTEIRA_1_TEMP", "EP" + scene.Episode.ToString("D2", CultureInfo.InvariantCulture)));
        SaveAndApplyEnvironment();
        SaveAndHidePlayer();
        if (scene.MarkedLocation) RequestMarkedSceneIpls(scene.LocalId, location.Position);
        awaitingCollision = !Object.ReferenceEquals(location, null);
        if (awaitingCollision)
        {
            hiddenPlayer.PositionNoOffset = location.Position;
            hiddenPlayer.Heading = location.Heading;
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, location.Position.X, location.Position.Y, location.Position.Z);
            WriteLog("TELEPORTE_LOCAL", "Id=" + scene.LocalId + "; origem=" + savedPlayerPosition + "; destino=" + location.Position);
        }
        player = hiddenPlayer;
        if (scene.Segments.Count > 0 || scene.Actions.Count > 0)
        {
            LoadGestures();
            if (scene.Segments.Count > 0)
            {
                RequestAnimation(MouthDictionary);
                RequestAnimation(MaleNeutralDictionary);
                RequestAnimation(FemaleNeutralDictionary);
            }
            foreach (SegmentData segment in scene.Segments)
            {
                if (segment.Narration)
                    continue;
                GestureData gesture;
                if (!gestures.TryGetValue(segment.Intention, out gesture))
                    throw new InvalidOperationException("Gesto não encontrado no catálogo: " + segment.Intention);
                RequestAnimation(gesture.MaleDictionary);
                RequestAnimation(gesture.FemaleDictionary);
            }
            foreach (CenaJsonDiretor.Acao action in scene.Actions)
            {
                if (action.Tipo != "animacao")
                    continue;
                GestureData gesture = gestures[action.Animacao];
                RequestAnimation(gesture.MaleDictionary);
                RequestAnimation(gesture.FemaleDictionary);
            }
        }
        Vector3 playerPosition = player.Position;
        double headingRadians = player.Heading * Math.PI / 180.0;
        right = new Vector3((float)Math.Cos(headingRadians), (float)Math.Sin(headingRadians), 0f);
        forward = new Vector3(-right.Y, right.X, 0f);
        WriteLog("EIXOS_HORIZONTAIS", "Heading=" + player.Heading + "; Right=" + right + "; Forward=" + forward);
        anchor = playerPosition + forward * scene.Distance;
        if (scene.ClearArea)
            directorEnvironment.Start(anchor, scene.CleanupRadius, player);
        foreach (ActorData actor in scene.Actors)
        {
            if (pendingModels.ContainsKey(actor.ModelName))
                continue;
            Model model = new Model(actor.ModelName);
            if (!model.IsInCdImage || !model.IsValid || !model.IsPed)
                throw new InvalidOperationException("Modelo de personagem não encontrado no jogo: " + actor.ModelName);
            pendingModels.Add(actor.ModelName, model);
            model.Request();
            WriteLog("MODELO_SOLICITADO", actor.ModelName);
        }
        loadingTimer = Stopwatch.StartNew();
        WriteLog("MONTAGEM_INICIADA", "Cena=" + scene.Id + "; origem=" + playerPosition + "; âncora=" + anchor);
        if (scene.Id == "EP13_C03") LogPurchaseSource("MONTAGEM");
        if (scene.Id == "EP07_C03")
            WriteLog("EP07_SUBTOMADA_MONTADA", "Cena=EP07_C03; Parte=" +
                CenaVooTemporadaDiretor.PartName(CenaVooTemporadaDiretor.PartFromSource(scene.Id, sceneFile)) +
                "; Arquivo=" + sceneFile.Replace('\\', '/') + "; SHA256=" + mountedFlightSourceHash +
                "; Versao=" + typeof(DiretorGTA).Assembly.GetName().Version + "; DuracaoMs=45000; Integral=" + (flightBoardingSubtake == 0));
        if (scene.Id == "EP13_C02")
            WriteLog("EP13_SUBTOMADA_MONTADA", "Cena=EP13_C02; Parte=" + DismissalPartName(dismissalSubtake) +
                "; Arquivo=" + sceneFile.Replace('\\', '/') + "; SHA256=" + mountedDismissalSourceHash +
                "; Versao=" + typeof(DiretorGTA).Assembly.GetName().Version + "; DuracaoMs=14000; Integral=False; PartesNecessarias=3");
        ShowMessage("Diretor: preparando " + scene.Actors.Count + " personagens...");
    }

    // Fallback limitado a colisao Map|Objects real da camada da ancora; sem consultar modelo da entidade.
    // World.Raycast usa o probe sincronizado. Resultado incompleto continua sem prova de chao.
    private bool TryClosestSceneMapFloor(Vector3 point, out float ground)
    {
        ground = float.NaN;
        Vector3 source = new Vector3(point.X, point.Y, anchor.Z + 0.25f);
        Vector3 target = new Vector3(point.X, point.Y, anchor.Z - 3f);
        // Options4 conserva IgnoreNoCollision, mas inclui colisores Glass/SeeThrough.
        ShapeTestHandle probe = ShapeTest.StartExpensiveSyncTestLOSProbe(source, target,
            IntersectFlags.Map | IntersectFlags.Objects, null, (ShapeTestOptions)4);
        ShapeTestResult ray;
        ShapeTestStatus rayStatus = probe.GetResult(out ray);
        bool complete = rayStatus == ShapeTestStatus.Ready;
        bool hit = complete && ray.DidHit;
        Vector3 contact = hit ? ray.HitPosition : Vector3.Zero;
        Vector3 normal = hit ? ray.SurfaceNormal : Vector3.Zero;
        bool finite = !Single.IsNaN(contact.X) && !Single.IsInfinity(contact.X) &&
            !Single.IsNaN(contact.Y) && !Single.IsInfinity(contact.Y) &&
            !Single.IsNaN(contact.Z) && !Single.IsInfinity(contact.Z) &&
            !Single.IsNaN(normal.X) && !Single.IsInfinity(normal.X) &&
            !Single.IsNaN(normal.Y) && !Single.IsInfinity(normal.Y) &&
            !Single.IsNaN(normal.Z) && !Single.IsInfinity(normal.Z);
        bool valid = hit && finite && System.Math.Abs(contact.X - point.X) <= 0.01f &&
            System.Math.Abs(contact.Y - point.Y) <= 0.01f && contact.Z >= target.Z && contact.Z <= source.Z &&
            System.Math.Abs(contact.Z - anchor.Z) <= 3f && System.Math.Abs(normal.X) <= 1.01f &&
            System.Math.Abs(normal.Y) <= 1.01f && normal.Z >= 0.7f && normal.Z <= 1.01f;
        if (valid) ground = contact.Z;
        WriteLog("CHAO_MAIN_MAP_FALLBACK", "Cena=" + activeScene.Id + "; Source=" + source + "; Target=" + target +
            "; Result=" + (int)rayStatus + "; Hit=" + hit + "; Contact=" + contact + "; Normal=" + normal +
            "; Valido=" + valid + "; Flags=Map|Objects; Options=4; Metodo=ShapeTest_sincrono; SemModeloEntidade=true; MargemAncora=3");
        return valid;
    }

    private void PreparePilotFile()
    {
        string target = Path.Combine(scenesDirectory, "EP01_C08.json");
        RequirePlainPath(target);
        // A marca é capturada no jogo; nenhum número XYZ vem do rascunho.
        directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), "ROCA_GRAPESEED");
        if (File.Exists(target))
        {
            SceneData existing = ReadScene(target);
            if (existing.Encenacao != "pedido_v1")
                throw new InvalidOperationException("EP01_C08 existente tem outra encenação. O arquivo foi preservado; revise antes de montar.");
            return;
        }
        string source = Path.Combine(projectRoot, "producao", "piloto", "EP01_C08.json");
        RequirePlainPath(source);
        byte[] bytes = File.ReadAllBytes(source);
        SceneData draft = ReadScene(source);
        if (draft.Encenacao != "pedido_v1") throw new InvalidOperationException("Rascunho de EP01_C08 não contém a encenação completa do pedido.");
        string snapshot = Path.Combine(dataRoot, "logs", "piloto_preparado_" + Guid.NewGuid().ToString("N") + ".json");
        RequirePlainPath(snapshot);
        using (FileStream backup = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { backup.Write(bytes, 0, bytes.Length); backup.Flush(true); }
        if (!BytesEqual(bytes, File.ReadAllBytes(snapshot))) throw new InvalidOperationException("A cópia do piloto não confere; runtime preservado.");
        if (!BytesEqual(bytes, File.ReadAllBytes(source))) throw new InvalidOperationException("O rascunho mudou durante a preparação; runtime preservado.");
        RequirePlainPath(target);
        using (FileStream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
        if (!BytesEqual(bytes, File.ReadAllBytes(target))) throw new InvalidOperationException("A publicação do piloto não confere.");
        ReadScene(target);
        WriteLog("PILOTO_PUBLICADO", "Arquivo=" + target + "; snapshot=" + snapshot + "; recursos reais e marca conferidos; teste visual pendente");
    }

    private void PrepareTechnicalFile(string id)
    {
        if (id != "teste_coreografia") throw new InvalidOperationException("Teste técnico desconhecido.");
        string target = Path.Combine(scenesDirectory, id + ".json");
        RequirePlainPath(target);
        if (File.Exists(target)) { ReadScene(target); return; }
        string source = Path.Combine(projectRoot, "producao", "testes", id + ".json");
        RequirePlainPath(source);
        byte[] bytes = File.ReadAllBytes(source);
        SceneData template = ReadScene(source);
        directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), template.LocalId);
        using (FileStream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
        if (!BytesEqual(bytes, File.ReadAllBytes(target))) throw new InvalidOperationException("A publicação do teste não confere.");
        WriteLog("ENSAIO_PUBLICADO", "Arquivo=" + target + "; elenco/marca reais conferidos; execução e visual pendentes");
    }

    private static bool BytesEqual(byte[] first, byte[] second)
    {
        if (first.Length != second.Length) return false;
        for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
        return true;
    }

    private static void RequirePlainPath(string path)
    {
        string current = Path.GetFullPath(path);
        while (!String.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Vínculo/junction não permitido: " + current);
            string parent = Path.GetDirectoryName(current);
            if (String.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
    }

    private void RequireGorjetaTrialScene()
    {
        if (activeScene.Id != GorjetaFisicaTrialDiretor.SceneId || activeScene.Episode != 0 || activeScene.Number != 0 ||
            Object.ReferenceEquals(gorjetaTrialSpec, null) || activeScene.DurationMs != gorjetaTrialSpec.JanelaMs ||
            activeScene.Encenacao != "serie_v1" || activeScene.Actors.Count != 1 || activeScene.Actors[0].Id != "dinjer" ||
            activeScene.Segments.Count != 1 || !activeScene.Segments[0].Pause ||
            activeScene.Actions.Count != 0 || activeScene.Direcao.Count != 0 || activeScene.Interpretacao.Count != 0 ||
            activeScene.Objetos.Count != (gorjetaTrialSpec.Corpo == "cfm_feminino_upper" ? 2 : 1) || activeScene.Poses.Count != 0 || activeScene.TransicoesPose.Count != 0 ||
            activeScene.Veiculos.Count != 0 || activeScene.Portas.Count != 0 ||
            activeScene.SaidaAgua || activeScene.EmpurraoAviao || activeScene.ArmarioModular)
            throw new InvalidOperationException("Trial gorjeta exige teste_fila_87, DINJER propria, pausa tecnica integral e mesa unica, sem canais concorrentes.");
        ObjetosSerieDiretor.PropSpec prop = activeScene.Objetos[0];
        if (prop.Codigo != GorjetaFisicaTrialDiretor.TableCode || prop.Recurso != "mesa" || prop.Modelo != "prop_table_01" ||
            !prop.AssentarNoChao || !prop.VisivelInicial || !String.IsNullOrEmpty(prop.AtorInicial) ||
            !String.IsNullOrEmpty(prop.ApoioInicial) || prop.Eventos.Count != 0)
            throw new InvalidOperationException("Trial gorjeta exige mesa01 propria assentada/visivel, sem attachment ou eventos.");
        if (gorjetaTrialSpec.Corpo == "cfm_feminino_upper")
        {
            ObjetosSerieDiretor.PropSpec chair = activeScene.Objetos[1];
            if (chair.Codigo != GorjetaFisicaTrialDiretor.ChairCode || chair.Recurso != "cadeira_simples" || chair.Modelo != "prop_chair_01a" ||
                !chair.AssentarNoChao || !chair.VisivelInicial || !String.IsNullOrEmpty(chair.AtorInicial) ||
                !String.IsNullOrEmpty(chair.ApoioInicial) || chair.Eventos.Count != 0)
                throw new InvalidOperationException("Trial upper exige somente cadeira01a propria assentada/visivel declarada, sem attachment/eventos.");
        }
    }

    private void ReadGorjetaTrialConfig()
    {
        if (!Object.ReferenceEquals(directorGorjetaTrial, null) || !Object.ReferenceEquals(gorjetaTrialSpec, null))
            throw new InvalidOperationException("Instancia/config anterior de gorjeta ainda presente.");
        string path = Path.Combine(dataRoot, "catalogos", "trial-gorjeta87.json");
        RequirePlainPath(path);
        long length = new FileInfo(path).Length;
        if (length < 1 || length > 512) throw new InvalidOperationException("Config gorjeta deve ter1..512bytes.");
        byte[] source = File.ReadAllBytes(path);
        gorjetaTrialSpec = GorjetaFisicaTrialDiretor.LoadSpec(path);
        if (!BytesEqual(source, File.ReadAllBytes(path))) throw new InvalidOperationException("Config gorjeta mudou durante leitura.");
        RequireGorjetaTrialScene();
        string folder = Path.Combine(dataRoot, "logs", "trial-gorjeta87");
        RequirePlainPath(folder); Directory.CreateDirectory(folder);
        string snapshot = Path.Combine(folder, "config_" + Guid.NewGuid().ToString("N") + ".json");
        RequirePlainPath(snapshot);
        using (FileStream stream = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { stream.Write(source, 0, source.Length); stream.Flush(true); }
        if (!BytesEqual(source, File.ReadAllBytes(snapshot))) throw new InvalidOperationException("Snapshot gorjeta nao confere.");
        WriteLog("GORJETA_TRIAL_CONFIG", "Source=" + path + "; Snapshot=" + snapshot + "; Type=" + gorjetaTrialSpec.Tipo +
            "; Segment=" + gorjetaTrialSpec.Segmento + "; Body=" + gorjetaTrialSpec.Corpo + "; WindowMs=" + gorjetaTrialSpec.JanelaMs +
            "; OnlyTechnicalScene=true; OpeningVisualConfirmed=false; CountingVisualConfirmed=false; ProductionApproval=false");
        awaitingGorjetaTrial = true;
    }

    private void PrepareGorjetaTrial()
    {
        RequireGorjetaTrialScene();
        if (!Object.ReferenceEquals(directorGorjetaTrial, null) || Object.ReferenceEquals(directorProps, null) || !directorProps.IsReady)
            throw new InvalidOperationException("Gorjeta exige nova instancia depois da mesa propria pronta.");
        directorGorjetaTrial = new GorjetaFisicaTrialDiretor(WriteLog,
            Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "menyooStuff"));
        directorGorjetaTrial.Prepare(activeScene.Id, actorsById["dinjer"], delegate(Ped candidate) {
            Ped current;
            return actorsById.TryGetValue("dinjer", out current) && Object.ReferenceEquals(candidate, current) &&
                createdPeds.Exists(delegate(Ped own) { return Object.ReferenceEquals(own, candidate); });
        }, directorProps, gorjetaTrialSpec);
        awaitingGorjetaTrial = true;
    }

    private bool ReleaseGorjetaTrialForReset()
    {
        if (Object.ReferenceEquals(directorGorjetaTrial, null)) return true;
        if (!directorGorjetaTrial.Cleanup()) return false;
        directorGorjetaTrial.Dispose(); directorGorjetaTrial = null; awaitingGorjetaTrial = false;
        return true;
    }

    private bool ResetGorjetaTrial()
    {
        RequireGorjetaTrialScene();
        if (Object.ReferenceEquals(directorProps, null)) throw new InvalidOperationException("Mesa de gorjeta ausente no reset.");
        if (!directorProps.IsReady) { directorProps.PollLoading(); awaitingGorjetaTrial = true; return false; }
        if (Object.ReferenceEquals(directorGorjetaTrial, null)) PrepareGorjetaTrial();
        if (!directorGorjetaTrial.IsReady) directorGorjetaTrial.PollLoading();
        awaitingGorjetaTrial = !directorGorjetaTrial.IsReady;
        return directorGorjetaTrial.IsReady;
    }

    private void RequireCartaTrialScene()
    {
        if (activeScene.Id != CartaFisicaDiretor.SceneId || activeScene.Episode != 0 || activeScene.Number != 0 ||
            activeScene.DurationMs != 9000 || activeScene.Encenacao != "serie_v1" ||
            activeScene.Actors.Count != 1 || activeScene.Actors[0].Id != "djanira_pobre" ||
            activeScene.Segments.Count != 1 || !activeScene.Segments[0].Narration || activeScene.Segments[0].Pause ||
            activeScene.Actions.Count != 0 || activeScene.Direcao.Count != 0 || activeScene.Interpretacao.Count != 0 ||
            activeScene.Objetos.Count != 0 || activeScene.Poses.Count != 0 || activeScene.TransicoesPose.Count != 0 ||
            activeScene.Veiculos.Count != 0 || activeScene.Portas.Count != 0 ||
            activeScene.SaidaAgua || activeScene.EmpurraoAviao || activeScene.ArmarioModular)
            throw new InvalidOperationException("Trial carta exige exclusivamente teste_fila_89, atriz propria unica e9s narracao sem canais concorrentes.");
    }

    private void PrepareCartaTrial()
    {
        RequireCartaTrialScene();
        if (!Object.ReferenceEquals(directorCartaTrial, null) || Object.ReferenceEquals(cartaTrialSpec, null))
            throw new InvalidOperationException("Instancia/config trial invalida.");
        directorCartaTrial = new CartaFisicaDiretor(WriteLog,
            Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "menyooStuff"));
        directorCartaTrial.Prepare(activeScene.Id, actorsById["djanira_pobre"], delegate(Ped candidate) {
            return !Object.ReferenceEquals(candidate, null) &&
                Object.ReferenceEquals(candidate, actorsById["djanira_pobre"]) &&
                createdPeds.Exists(delegate(Ped own) { return Object.ReferenceEquals(own, candidate); });
        }, cartaTrialSpec);
        awaitingCartaTrial = true;
    }

    private bool ReleaseCartaTrialForReset()
    {
        if (Object.ReferenceEquals(directorCartaTrial, null)) return true;
        if (!directorCartaTrial.Cleanup()) return false;
        directorCartaTrial.Dispose(); directorCartaTrial = null; awaitingCartaTrial = false;
        return true;
    }

    private bool ResetCartaTrial()
    {
        if (Object.ReferenceEquals(directorCartaTrial, null)) PrepareCartaTrial();
        if (!directorCartaTrial.IsReady) directorCartaTrial.PollLoading();
        awaitingCartaTrial = !directorCartaTrial.IsReady;
        return directorCartaTrial.IsReady;
    }

    private void AdvanceLoading()
    {
        if (activeScene.Id == "EP10_C06" && !Object.ReferenceEquals(directorWritingC6, null) && !directorWritingC6.IsReady)
        { directorWritingC6.PollLoading(); if (directorWritingC6.IsReady) FinishMount(); return; }
        // Only technical85 fulltracks owns this resource wait. Keep production loading paths unchanged.
        if (activeScene.Id == "teste_fila_85" && !Object.ReferenceEquals(directorWriting85, null) && !directorWriting85.IsReady)
        {
            directorWriting85.PollLoading();
            if (directorWriting85.IsReady) FinishMount();
            return;
        }
        if (awaitingStaging || awaitingInterpretation || awaitingRehearsal || awaitingPilot || awaitingProps || awaitingPoses || awaitingEpisodeOne || awaitingThreeSeven || awaitingSeasonFlight || awaitingPlanePush || awaitingVehicles || awaitingDoors || awaitingSeasonWater || awaitingWardrobe || awaitingBoateLights || awaitingEntry || awaitingCartaTrial || awaitingGorjetaTrial || awaitingTableWipe)
        {
            if (awaitingBoateLights) { directorBoateLights.PollLoading(); awaitingBoateLights = !directorBoateLights.IsReady; }
            if (awaitingProps) { directorProps.PollLoading(); awaitingProps = !directorProps.IsReady; }
            if (awaitingStaging) { directorStaging.PollLoading(); awaitingStaging = !directorStaging.IsReady; }
            if (awaitingInterpretation) { directorInterpretation.PollLoading(); awaitingInterpretation = !directorInterpretation.IsReady; }
            if (awaitingRehearsal) { directorRehearsal.PollLoading(); awaitingRehearsal = !directorRehearsal.IsReady; }
            if (awaitingPilot) { directorPilot.PollLoading(); awaitingPilot = !directorPilot.IsReady; }
            if (awaitingEpisodeOne) { directorEpisodeOne.PollLoading(); awaitingEpisodeOne = !directorEpisodeOne.IsReady; }
            if (awaitingDoors) { directorDoors.PollLoading(); awaitingDoors = !directorDoors.IsReady; }
            if (awaitingVehicles) { directorVehicles.PollLoading(); awaitingVehicles = !directorVehicles.IsReady; }
            if (awaitingThreeSeven) { directorThreeSeven.PollLoading(); awaitingThreeSeven = !directorThreeSeven.IsReady; }
            if (awaitingSeasonWater) { directorSeasonWater.PollLoading(); awaitingSeasonWater = !directorSeasonWater.IsReady; }
            if (awaitingSeasonFlight && Object.ReferenceEquals(directorSeasonFlight, null) && !awaitingProps && !awaitingStaging && !awaitingInterpretation)
            {
                directorSeasonFlight = new CenaVooTemporadaDiretor(WriteLog);
                if (activeScene.Id == "EP07_C03") directorSeasonFlight.ConfigureAppearance(Path.Combine(dataRoot, "catalogos", "velum-aparencia.xml"));
                directorSeasonFlight.Prepare(activeScene.Id, new Dictionary<string, Ped>(actorsById, StringComparer.Ordinal), anchor, right, forward, activeScene.LocalId, episodeOneFallSpec,
                    CenaVooTemporadaDiretor.PartFromSource(activeScene.Id, selectedSceneFile));
                if (directorSeasonFlight.HandlesObjectEvents) directorSeasonFlight.BindObjects(directorProps);
            }
            if (awaitingSeasonFlight && !Object.ReferenceEquals(directorSeasonFlight, null)) { directorSeasonFlight.PollLoading(); awaitingSeasonFlight = !directorSeasonFlight.IsReady; }
            if (awaitingPlanePush) { directorPlanePush.PollLoading(); awaitingPlanePush = !directorPlanePush.IsReady; }
            if (awaitingWardrobe && Object.ReferenceEquals(directorWardrobe, null) && !awaitingProps && !awaitingInterpretation)
            {
                directorWardrobe = new ArmarioSerieDiretor(WriteLog, Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "menyooStuff"));
                directorWardrobe.Prepare(new Dictionary<string, Ped>(actorsById, StringComparer.OrdinalIgnoreCase));
                directorWardrobe.BindOwnDoor(directorProps); // Vincular uma unica vez, depois do streaming dos objetos.
            }
            if (awaitingWardrobe && !Object.ReferenceEquals(directorWardrobe, null))
            {
                directorWardrobe.PollLoading(); awaitingWardrobe = !directorWardrobe.IsReady;
                if (!awaitingWardrobe) RecordWardrobeEntities();
            }
            if (awaitingEntry && Object.ReferenceEquals(directorEntry, null) && !awaitingProps && !awaitingStaging && !awaitingInterpretation)
            {
                directorEntry = new EntradaSentadaAutorDiretor(WriteLog,
                    Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "menyooStuff"));
                directorEntry.Prepare(actorsById["djanira_pobre"], delegate(Ped candidate) {
                    Ped current;
                    return actorsById.TryGetValue("djanira_pobre", out current) && Object.ReferenceEquals(candidate, current) &&
                        createdPeds.Exists(delegate(Ped own) { return Object.ReferenceEquals(own, candidate); });
                }, directorProps, "CADEIRA_DJANIRA", 0, activeScene.Segments[1].EndMs, activeScene.Segments[1].EndMs);
            }
            if (awaitingEntry && !Object.ReferenceEquals(directorEntry, null))
            { directorEntry.PollLoading(); awaitingEntry = !directorEntry.IsReady; }
            if (awaitingCartaTrial) { directorCartaTrial.PollLoading(); awaitingCartaTrial = !directorCartaTrial.IsReady; }
            if (awaitingGorjetaTrial && Object.ReferenceEquals(directorGorjetaTrial, null) && !awaitingProps && !awaitingStaging && !awaitingInterpretation)
                PrepareGorjetaTrial();
            if (awaitingGorjetaTrial && !Object.ReferenceEquals(directorGorjetaTrial, null))
            { directorGorjetaTrial.PollLoading(); awaitingGorjetaTrial = !directorGorjetaTrial.IsReady; }
            if (awaitingTableWipe && Object.ReferenceEquals(directorTableWipe, null) && !awaitingProps && !awaitingStaging && !awaitingInterpretation)
            {
                directorTableWipe = new LimpezaMesaDiretor(WriteLog);
                directorTableWipe.Prepare(activeScene.Id, actorsById["dinjer"], delegate(Ped candidate) {
                    Ped current;
                    return actorsById.TryGetValue("dinjer", out current) && Object.ReferenceEquals(candidate, current) &&
                        createdPeds.Exists(delegate(Ped own) { return Object.ReferenceEquals(own, candidate); });
                }, directorProps, "MESA_LIMPEZA", 0, 4000);
            }
            if (awaitingTableWipe && !Object.ReferenceEquals(directorTableWipe, null))
            { directorTableWipe.PollLoading(); awaitingTableWipe = !directorTableWipe.IsReady; }
            if (!awaitingProps && !awaitingStaging && !awaitingInterpretation && !awaitingPilot && !awaitingRehearsal && !awaitingEpisodeOne &&
                !awaitingThreeSeven && !awaitingSeasonFlight && !awaitingPlanePush && !awaitingVehicles && !awaitingDoors && !awaitingSeasonWater && !awaitingWardrobe && !awaitingBoateLights && !awaitingEntry && !awaitingCartaTrial && !awaitingGorjetaTrial && !awaitingTableWipe &&
                activeScene.Poses.Count > 0 && Object.ReferenceEquals(directorPoses, null))
            {
                if (!Object.ReferenceEquals(directorStaging, null) && !awaitingStaging) directorStaging.Reset();
                ObserveC4Room("ANTES_POSE", true);
                directorPoses = new PosesSerieDiretor(WriteLog);
                directorPoses.Prepare(new Dictionary<string, Ped>(actorsById, StringComparer.Ordinal), activeScene.Poses, directorProps, false, activeScene.Id == "EP03_C03" ? activeScene.Id : null);
                directorPoses.PrepareTransitions(activeScene.TransicoesPose);
                if (!Object.ReferenceEquals(directorStaging, null)) directorStaging.BindPoses(directorPoses);
                awaitingPoses = true;
            }
            if (awaitingPoses && !Object.ReferenceEquals(directorPoses, null)) { directorPoses.PollLoading(); awaitingPoses = !directorPoses.IsReady; }
            if (awaitingStaging || awaitingInterpretation || awaitingRehearsal || awaitingPilot || awaitingProps || awaitingPoses || awaitingEpisodeOne || awaitingThreeSeven || awaitingSeasonFlight || awaitingPlanePush || awaitingVehicles || awaitingDoors || awaitingSeasonWater || awaitingWardrobe || awaitingBoateLights || awaitingEntry || awaitingCartaTrial || awaitingGorjetaTrial || awaitingTableWipe) return;
            if (!Object.ReferenceEquals(directorStaging, null) && !Object.ReferenceEquals(directorVehicles, null)) directorStaging.BindVehicles(directorVehicles);
            else if (!Object.ReferenceEquals(directorStaging, null) && !Object.ReferenceEquals(directorThreeSeven, null)) directorStaging.BindVehicles(directorThreeSeven.OwnVehicles);
            if (!Object.ReferenceEquals(directorProps, null) && !Object.ReferenceEquals(directorVehicles, null)) directorProps.BindVehicles(directorVehicles);
            else if (!Object.ReferenceEquals(directorProps, null) && !Object.ReferenceEquals(directorThreeSeven, null)) directorProps.BindVehicles(directorThreeSeven.OwnVehicles);
            FinishMount();
            return;
        }
        if (awaitingVisual)
        {
            bool visualsReady = true;
            foreach (ActorData actor in activeScene.Actors)
                if (!Object.ReferenceEquals(actor.Visual, null) &&
                    !(actor.RichOption > 0 ? VariantesDjaniraDiretor.IsReady(actorsById[actor.Id]) :
                        VisualDiretor.IsReady(actorsById[actor.Id]))) visualsReady = false;
            if (!visualsReady) return;
            awaitingVisual = false;
            CompleteLoading();
            return;
        }
        if (!PollDismissalInteriorBeforeFloor()) return;
        if (awaitingCollision)
        {
            if (Object.ReferenceEquals(hiddenPlayer, null) || !hiddenPlayer.Exists())
                throw new InvalidOperationException("O jogador foi removido durante o teleporte.");
            Vector3 point = hiddenPlayer.Position;
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
            if (!Function.Call<bool>(Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, hiddenPlayer.Handle))
            {
                if (loadingTimer.ElapsedMilliseconds >= locationTimeout)
                    throw new InvalidOperationException("O chão do local marcado não carregou a tempo. Marque uma área plana ao ar livre.");
                return;
            }
        }
        bool loaded = true;
        foreach (KeyValuePair<string, Model> entry in pendingModels)
        {
            if (!entry.Value.IsLoaded)
            {
                loaded = false;
                if (loadingTimer.ElapsedMilliseconds >= modelTimeout)
                    throw new InvalidOperationException("Tempo limite ao carregar o modelo " + entry.Key + ".");
            }
        }
        foreach (string dictionary in requestedAnimations)
        {
            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dictionary))
            {
                loaded = false;
                if (loadingTimer.ElapsedMilliseconds >= animationTimeout)
                    throw new InvalidOperationException("Tempo limite ao carregar a animação " + dictionary + ".");
            }
        }
        if (!loaded)
            return;
        // O dicionário existir não comprova que o clip indicado existe nele.
        foreach (GestureData gesture in gestures.Values)
        {
            if (requestedAnimations.Contains(gesture.MaleDictionary))
                ValidateBodyClip(gesture.MaleDictionary, gesture.MaleClip);
            if (requestedAnimations.Contains(gesture.FemaleDictionary))
                ValidateBodyClip(gesture.FemaleDictionary, gesture.FemaleClip);
        }

        // Na boate, iniciar sob o teto conserva o piso da marca real.
        float groundProbeRise = (activeScene.LocalId == "BOATE_DENTRO" || activeScene.LocalId == "CASA_VO_INTERIOR") ? 0.5f : 2f;
        // Conferir TODOS os pontos antes de criar o primeiro personagem.
        List<Vector3> positions = new List<Vector3>();
        foreach (ActorData actor in activeScene.Actors)
        {
            if (activeScene.Id == "teste_fila_90" || activeScene.Id == "EP01_C01" || activeScene.Id == "EP07_C04" || activeScene.Id == "EP07_C05" || activeScene.SaidaAgua)
            {
                // XY/Z capturados no jogo, na água. O ensaio mede a superfície e
                // posiciona o avião somente durante o preparo, fora da captura.
                positions.Add(technicalWaterMark);
                continue;
            }
            Vector3 point = anchor + right * actor.Right + forward * actor.Forward;
            float ground;
            bool groundFound = World.GetGroundHeight(new Vector3(point.X, point.Y, anchor.Z + (activeScene.MarkedLocation ? 0.25f : groundProbeRise)), out ground, GetGroundHeightMode.Normal);
            if (!groundFound || System.Math.Abs(ground - anchor.Z) > 3f)
            {
                float mappedGround;
                if (TryClosestSceneMapFloor(point, out mappedGround)) { ground = mappedGround; groundFound = true; }
            }
            if (!groundFound)
            {
                Function.Call(Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, anchor.Z);
                if (awaitingCollision && loadingTimer.ElapsedMilliseconds < locationTimeout)
                    return;
                throw new InvalidOperationException("O chão não carregou para " + actor.Name + ". Marque uma área plana ao ar livre.");
            }
            if (System.Math.Abs(ground - anchor.Z) > 3f)
                throw new InvalidOperationException("Não encontrei um chão próximo para " + actor.Name + ". Escolha uma área plana ao ar livre.");
            // Deixar a física assentar o ped; StandStill mantém a pose sem congelá-lo no ar.
            positions.Add(new Vector3(point.X, point.Y, ground + 1f + actor.Up));
        }

        for (int i = 0; i < activeScene.Actions.Count; i++)
        {
            CenaJsonDiretor.Acao action = activeScene.Actions[i];
            if (action.Tipo != "andarAte")
                continue;
            Vector3 point = anchor + right * action.Direita + forward * action.Frente;
            float ground;
            bool groundFound = World.GetGroundHeight(new Vector3(point.X, point.Y, anchor.Z + (activeScene.MarkedLocation ? 0.25f : groundProbeRise)), out ground, GetGroundHeightMode.Normal);
            if (!groundFound || System.Math.Abs(ground - anchor.Z) > 3f)
            {
                float mappedGround;
                if (TryClosestSceneMapFloor(point, out mappedGround)) { ground = mappedGround; groundFound = true; }
            }
            if (!groundFound)
            {
                Function.Call(Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, anchor.Z);
                if (loadingTimer.ElapsedMilliseconds < locationTimeout)
                    return;
                throw new InvalidOperationException("O chão do destino de andarAte não carregou. Escolha uma área plana.");
            }
            if (System.Math.Abs(ground - anchor.Z) > 3f)
                throw new InvalidOperationException("andarAte: destino distante do chão da cena.");
            actionDestinations[i] = new Vector3(point.X, point.Y, ground + action.Altura);
        }

        for (int i = 0; i < activeScene.Actors.Count; i++)
        {
            ActorData actor = activeScene.Actors[i];
            Ped ped = Ped.Create(pendingModels[actor.ModelName], positions[i], 0f);
            if (Object.ReferenceEquals(ped, null) || !ped.Exists())
                throw new InvalidOperationException("O jogo não conseguiu criar " + actor.Name + ".");
            createdPeds.Add(ped);
            ped.IsPersistent = true;
            ped.IsInvincible = true;
            if (activeScene.Id == "EP01_C01" || activeScene.Id == "teste_fila_90" || activeScene.Id == "EP07_C04" || activeScene.Id == "EP07_C05" || activeScene.SaidaAgua)
            {
                ped.SetConfigFlag(PedConfigFlagToggles.DrownsInWater, false);
                ped.SetConfigFlag(PedConfigFlagToggles.DiesInstantlyWhenSwimming, false);
            }
            ped.BlockPermanentEvents = true;
            ped.CanRagdoll = false;
            ped.Task.ClearAllImmediately();
            actorsById.Add(actor.Id, ped);
            if (actor.RichOption > 0)
            {
                VisualDiretor.Visual poor = VisualDiretor.Load(Path.Combine(dataRoot, "catalogos", "visuais", "DJANIRA_POBRE.xml"), "mp_f_freemode_01");
                VariantesDjaniraDiretor.Begin(ped, poor, actor.Visual, actor.RichOption, dataRoot, WriteLog);
            }
            else if (!Object.ReferenceEquals(actor.Visual, null)) VisualDiretor.Apply(ped, actor.Visual);
            WriteLog("PERSONAGEM_CRIADO", "Id=" + actor.Id + "; modelo=" + actor.ModelName + "; handle=" + ped.Handle + "; posição=" + positions[i]);
        }
        awaitingVisual = activeScene.Actors.Exists(delegate(ActorData actor) { return !Object.ReferenceEquals(actor.Visual, null); });
        if (awaitingVisual) return;
        CompleteLoading();
    }

    private void CompleteLoading()
    {
        if (activeScene.LocalId == "BOATE_DENTRO")
        {
            directorBoateLights = new LuzesBoateDiretor(WriteLog);
            directorBoateLights.SoftFillEnabled = activeScene.Id == "EP02_C01" || activeScene.Id == "EP02_C02";
            directorBoateLights.Prepare(activeScene.LocalId, actorsById[activeScene.Actors[0].Id]);
            awaitingBoateLights = true;
        }
        foreach (ActorData actor in activeScene.Actors)
        {
            Ped ped = actorsById[actor.Id];
            if (actor.RichOption > 0)
            {
                actor.Visual = VariantesDjaniraDiretor.Current(ped);
                richPreviewSavedPaths[actor.RichOption] = VariantesDjaniraDiretor.SavedPath(ped);
            }
            if (!Object.ReferenceEquals(actor.Visual, null)) RecordAppearanceCheck(actor, ped);
            if (!String.IsNullOrEmpty(actor.LookAt))
            {
                Ped target = actorsById[actor.LookAt];
                Vector3 delta = target.Position - ped.Position;
                ped.Heading = Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
            }
            else if (Array.IndexOf(castPreviewFiles, activeScene.Id + ".json") >= 0)
                ped.Heading = Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, -forward.X, -forward.Y);
            else ped.Heading = Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, forward.X, forward.Y);
            if (actor.Rotation.HasValue)
                ped.Heading = (Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, forward.X, forward.Y) + actor.Rotation.Value + 360f) % 360f;
            ped.Task.StandStill(-1);
            if (activeScene.Segments.Count > 0)
                ResetMouth(ped);
        }
        ReleaseModels();
        if (activeScene.Id == GorjetaFisicaTrialDiretor.SceneId) ReadGorjetaTrialConfig();
        if (activeScene.Id == CartaFisicaDiretor.SceneId)
        {
            RequireCartaTrialScene();
            string path = Path.Combine(dataRoot, "catalogos", "trial-carta89.json");
            RequirePlainPath(path);
            long configBytes = new FileInfo(path).Length;
            if (configBytes < 1 || configBytes > 512) throw new InvalidOperationException("Config trial deve ter1..512bytes.");
            byte[] source = File.ReadAllBytes(path);
            cartaTrialSpec = CartaFisicaDiretor.LoadSpec(path);
            if (!BytesEqual(source, File.ReadAllBytes(path))) throw new InvalidOperationException("Config trial mudou durante leitura.");
            string logFolder = Path.Combine(dataRoot, "logs", "trial-carta89");
            RequirePlainPath(logFolder); Directory.CreateDirectory(logFolder);
            string snapshot = Path.Combine(logFolder, "config_" + Guid.NewGuid().ToString("N") + ".json");
            RequirePlainPath(snapshot);
            using (FileStream stream = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(source, 0, source.Length); stream.Flush(true); }
            if (!BytesEqual(source, File.ReadAllBytes(snapshot))) throw new InvalidOperationException("Snapshot trial nao confere.");
            WriteLog("CARTA_TRIAL_CONFIG", "Source=" + path + "; Snapshot=" + snapshot +
                "; Segment=" + cartaTrialSpec.Segmento + "; Objects=" + cartaTrialSpec.Objetos +
                "; OnlyTechnicalScene=true; ProductionApproval=false");
            PrepareCartaTrial();
            awaitingCollision = false;
            return;
        }
        if (FigurinoTransitorioDiretor.Supports(activeScene.Id))
        {
            directorTransientOutfit = new FigurinoTransitorioDiretor(WriteLog);
            directorTransientOutfit.ApplyBareFeet(activeScene.Id, actorsById[activeScene.Id == "EP03_C05" ? "dinjer" : "djanira_pobre"]);
        }
        // A pose isolada tambem precisa passar pela barreira assincrona.
        awaitingPoses = activeScene.Poses.Count > 0;
        if (activeScene.Veiculos.Count > 0)
        {
            directorVehicles = new VeiculosSerieDiretor(WriteLog, activeScene.Id);
            directorVehicles.Prepare(new Dictionary<string, Ped>(actorsById, StringComparer.OrdinalIgnoreCase), activeScene.Veiculos, anchor, right, forward);
            awaitingVehicles = true;
        }
        if (activeScene.Portas.Count > 0)
        {
            if (activeScene.Id == "EP15_C07")
            {
                string bodySourceHash = C7BodySourceHash(Path.Combine(scenesDirectory, selectedSceneFile));
                if (activeScene.DurationMs != 12000 || bodySourceHash != "AD6D831041B35B2A19F95BDB0D87702AE48F8D5C2D6CE6D0703305AE41746CC3")
                    throw new InvalidOperationException("C7 exige corpo/falas/atores/porta12s exatos; somente camera JSON pode variar e exige nova revisao visual.");
                WriteLog("C7_FONTE_CORPO_CONFERIDA", "SHA256=" + bodySourceHash + "; ExcludedOnlyTopLevelCamera=True; FullPrearmSnapshotStillRequired=True; CameraApprovalPending=True");
            }
            directorDoors = new PortasSerieDiretor(WriteLog, Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "menyooStuff", "PropList.txt"));
            directorDoors.Prepare(new Dictionary<string, Ped>(actorsById, StringComparer.Ordinal), activeScene.Portas, anchor, right, forward, activeScene.Id);
            awaitingDoors = true;
        }
        if (EpisodiosTresSeteDiretor.Supports(activeScene.Id))
        {
            if (activeScene.DurationMs != EpisodiosTresSeteDiretor.ExpectedDurationMs(activeScene.Id))
                throw new InvalidOperationException("A duração do desembarque não corresponde à execução física.");
            directorThreeSeven = new EpisodiosTresSeteDiretor(WriteLog);
            Vector3 busOrigin = activeScene.Id == "EP04_C01" ? actorsById["djanira_pobre"].Position : anchor;
            directorThreeSeven.Prepare(activeScene.Id, new Dictionary<string, Ped>(actorsById, StringComparer.Ordinal), busOrigin, right, forward, activeScene.LocalId);
            awaitingThreeSeven = true;
        }
        if (activeScene.EmpurraoAviao)
        {
            directorPlanePush = new CenaEmpurraoAviaoDiretor(WriteLog, Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "menyooStuff", "PedAnimList.txt"));
            directorPlanePush.Prepare(new Dictionary<string, Ped>(actorsById, StringComparer.OrdinalIgnoreCase), episodeOneFallSpec);
            awaitingPlanePush = true;
        }
        else if (CenaVooTemporadaDiretor.Supports(activeScene.Id))
        {
            if (activeScene.DurationMs != CenaVooTemporadaDiretor.ExpectedDurationMs(activeScene.Id))
                throw new InvalidOperationException("A duração do voo não corresponde à execução física.");
            awaitingSeasonFlight = true;
        }
        if (activeScene.SaidaAgua)
        {
            directorSeasonWater = new CenaTemporadaDiretor(WriteLog);
            directorSeasonWater.PrepareSaidaAgua(actorsById["djanira_pobre"], new CenaTemporadaDiretor.SaidaAguaSpec {
                Agua = episodeOneFallSpec.Agua,
                EntradaRampa = directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), "CANAL_LS_RAMPA_ENTRADA").Position,
                TopoRampa = directorLocations.Load(Path.Combine(dataRoot, "catalogos", "locais_marcados.xml"), "CANAL_LS_RAMPA_TOPO").Position,
                DuracaoMs = activeScene.DurationMs });
            awaitingSeasonWater = true;
        }
        if (EpisodioUmDiretor.Supports(activeScene.Id))
        {
            directorEpisodeOne = new EpisodioUmDiretor(WriteLog);
            directorEpisodeOne.Prepare(activeScene.Id, new Dictionary<string, Ped>(actorsById, StringComparer.Ordinal), anchor, right, forward,
                activeScene.LocalId, episodeOneFallSpec);
            awaitingEpisodeOne = true;
        }
        if (activeScene.Id == "teste_fila_90")
        {
            directorRehearsal = new EnsaioAcaoDiretor(WriteLog);
            directorRehearsal.PrepareQueda(actorsById["djanira_pobre"], technicalWaterMark, forward);
            awaitingCollision = false; awaitingRehearsal = true;
            ShowMessage("Ensaio separado: preparando avião próprio e proteção sobre a marca real de água.");
            return;
        }
        if (activeScene.Objetos.Count > 0)
        {
            directorProps = new ObjetosSerieDiretor(WriteLog);
            Dictionary<string, Ped> propActors = new Dictionary<string, Ped>(StringComparer.Ordinal);
            foreach (ActorData actor in activeScene.Actors) propActors.Add(actor.Id.ToUpperInvariant(), actorsById[actor.Id]);
            directorProps.Prepare(propActors, activeScene.Objetos, anchor, right, forward, activeScene.LocalId,
                activeScene.Id == "teste_fila_88" ? activeScene.Id : null, activeScene.Id == "teste_fila_86" ? activeScene.Id : null,
                activeScene.Objetos.Exists(delegate(ObjetosSerieDiretor.PropSpec item) { return ObjetosSerieDiretor.IsProductionBoxResourceC2(item.Recurso); }) ? activeScene.Id : null,
                activeScene.DurationMs, activeScene.Id == "EP13_C02" ? activeScene.Id : null,
                activeScene.Id == "EP13_C02" ? DismissalPartName(dismissalSubtake) : null,
                activeScene.Id == "EP10_C01" || activeScene.Id == "teste_fila_84" ? activeScene.Id : null,
                activeScene.Objetos.Exists(delegate(ObjetosSerieDiretor.PropSpec item) { return item.Recurso == "mesa_grande_c8_autor"; }) ? activeScene.Id : null,
                IsAuthoredPaperC5Source(activeScene) ? activeScene.Id : null,
                activeScene.Id == "teste_fila_98" ? activeScene.Id : null);
            awaitingProps = true;
        }
        if (activeScene.ArmarioModular) awaitingWardrobe = true;
        if (activeScene.Id == "EP08_C03" && activeScene.Objetos.Exists(delegate(ObjetosSerieDiretor.PropSpec item) {
            return item.Codigo == "CADEIRA_DJANIRA" && item.Recurso == "cadeira_entrada_autor"; }))
        {
            if (activeScene.DurationMs != 42000 || activeScene.LocalId != "CASA_VO_INTERIOR" ||
                activeScene.Segments.Count < 3 || activeScene.Segments[0].ActorId != "dona_iraci" ||
                activeScene.Segments[1].ActorId != "dona_iraci" || activeScene.Segments[2].ActorId != "djanira_pobre" ||
                activeScene.Segments[1].EndMs != 14000 ||
                !actorsById.ContainsKey("djanira_pobre") ||
                activeScene.Poses.Exists(delegate(PosesSerieDiretor.PoseSpec pose) { return pose.Ator == "DJANIRA_POBRE"; }) ||
                activeScene.TransicoesPose.Exists(delegate(PosesSerieDiretor.TransitionSpec transition) { return transition.Ator == "DJANIRA_POBRE"; }))
                throw new InvalidOperationException("Entrada autor exige8C3 original42s e um unico dono do corpo da Djanira.");
            awaitingEntry = true;
        }
        if (activeScene.Id == "EP13_C05")
        {
            if (activeScene.DurationMs != 12000 || activeScene.Actors.Count != 1 || !actorsById.ContainsKey("dinjer") ||
                activeScene.Poses.Count != 0 || activeScene.TransicoesPose.Count != 0 ||
                !activeScene.Objetos.Exists(delegate(ObjetosSerieDiretor.PropSpec prop) {
                    return prop.Codigo == "MESA_LIMPEZA" && (prop.Recurso == "mesa_limpeza_bar" || prop.Recurso == "bancada_limpeza_candidata") && prop.AssentarNoChao && prop.VisivelInicial; }) ||
                !activeScene.Direcao.Exists(delegate(AcoesSerieDiretor.ActorAction action) {
                    return action.Ator == "dinjer" && action.Tipo == "clipe" && action.Recurso == "limpar_mesa_superficie_autor" &&
                        action.Dicionario == "timetable@maid@cleaning_surface@base" && action.Clipe == "base" &&
                        action.InicioMs == 0 && action.DuracaoMs == 4000 && action.Loop && action.CorpoInteiro && action.ManterPoseFinal; }))
                throw new InvalidOperationException("Limpeza13C5 exige mesa propria e par original completo0..4s, sem dono corporal concorrente.");
            awaitingTableWipe = true;
        }
        if (activeScene.Interpretacao.Count > 0)
        {
            directorInterpretation = new InterpretacaoDiretor(WriteLog);
            if (!Object.ReferenceEquals(directorProps, null)) directorInterpretation.BindLookObjects(directorProps);
            Dictionary<string, Ped> interpretedActors = new Dictionary<string, Ped>(actorsById, StringComparer.Ordinal);
            directorInterpretation.Prepare(interpretedActors, activeScene.Interpretacao, anchor, right, forward);
            awaitingInterpretation = true;
        }
        if (activeScene.Encenacao == "serie_v1" && activeScene.Direcao.Count > 0)
        {
            directorStaging = new AcoesSerieDiretor(WriteLog);
            if (activeScene.Id == "EP13_C02") directorStaging.EnableDismissalHandoffTelemetry(activeScene.Id);
            if(activeScene.Id=="teste_fila_98")
            {
                RequirePocket98Contract(activeScene,selectedSceneFile);
                directorStaging.EnablePocket98AttachmentStart(activeScene.Id,FlightSourceHash(Path.Combine(scenesDirectory,selectedSceneFile)));
            }
            Dictionary<string, Ped> owned = new Dictionary<string, Ped>(StringComparer.Ordinal);
            foreach (ActorData actor in activeScene.Actors) owned.Add(actor.Id, actorsById[actor.Id]);
            directorStaging.Prepare(owned, activeScene.Direcao, anchor, right, forward);
            awaitingCollision = false;
            awaitingStaging = true;
            ShowMessage("Diretor: preparando movimentos e objetos da cena...");
            return;
        }
        if (activeScene.Encenacao == "ensaio_coreografia_v1")
        {
            directorRehearsal = new EnsaioAcaoDiretor(WriteLog);
            directorRehearsal.PrepareCoreografia(actorsById["capanga_1"], actorsById["djanira_pobre"], anchor);
            awaitingCollision = false;
            awaitingRehearsal = true;
            ShowMessage("Diretor: carregando o ensaio de coreografia...");
            return;
        }
        if (activeScene.Encenacao == "pedido_v1")
        {
            directorPilot = new PilotoPedidoDiretor(WriteLog);
            directorPilot.Prepare(actorsById["djanira_pobre"], actorsById["dinjer"], anchor, right, forward);
            awaitingCollision = false;
            awaitingPilot = true;
            ShowMessage("O PEDIDO: preparando a moto, a caminhada e o celular...");
            return;
        }
        if (awaitingPoses || awaitingInterpretation || awaitingProps || awaitingEpisodeOne || awaitingThreeSeven || awaitingSeasonFlight || awaitingPlanePush || awaitingVehicles || awaitingDoors || awaitingSeasonWater || awaitingWardrobe || awaitingBoateLights || awaitingEntry || awaitingCartaTrial || awaitingGorjetaTrial || awaitingTableWipe)
        {
            awaitingCollision = false;
            ShowMessage("Diretor: preparando expressões e olhares...");
            return;
        }
        FinishMount();
    }

    // Executado somente depois da aplicação estável, no GTA. O arquivo de origem
    // permanece fixo; o relatório registra getters, sem aprovar a estética.
    private void RecordAppearanceCheck(ActorData actor, Ped ped)
    {
        XmlDocument document = new XmlDocument(); document.XmlResolver = null;
        XmlElement root = document.CreateElement("ConferenciaVisual"); document.AppendChild(root);
        root.SetAttribute("versao", "1"); root.SetAttribute("codigo", actor.Id.ToUpperInvariant());
        root.SetAttribute("cena", activeScene.Id); root.SetAttribute("modelo", actor.Visual.Model);
        root.SetAttribute("visualSHA256", actor.Visual.Sha256); root.SetAttribute("faceArquivoSHA256", actor.Visual.FaceSha256);
        root.SetAttribute("dataUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        root.SetAttribute("aprovacaoVisual", "pendente");
        for (int kind = 0; kind < 2; kind++)
        {
            int[,] expected = kind == 0 ? actor.Visual.Components : actor.Visual.Props;
            for (int slot = 0; slot < (kind == 0 ? 12 : 10); slot++)
            {
                if (kind == 0 && Function.Call<int>(Hash.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS, ped.Handle, slot) == 0)
                {
                    XmlElement unavailable = document.CreateElement("Componente");
                    unavailable.SetAttribute("slot", slot.ToString(CultureInfo.InvariantCulture));
                    unavailable.SetAttribute("estado", "sem_variações_no_modelo"); root.AppendChild(unavailable);
                    continue;
                }
                int drawable = Function.Call<int>(kind == 0 ? Hash.GET_PED_DRAWABLE_VARIATION : Hash.GET_PED_PROP_INDEX, ped.Handle, slot);
                int texture = Function.Call<int>(kind == 0 ? Hash.GET_PED_TEXTURE_VARIATION : Hash.GET_PED_PROP_TEXTURE_INDEX, ped.Handle, slot);
                if (drawable != expected[slot, 0] || (drawable >= 0 && texture != expected[slot, 1]))
                    throw new InvalidOperationException("Conferência do figurino divergiu: " + actor.Id + ", peça " + slot + ".");
                XmlElement piece = document.CreateElement(kind == 0 ? "Componente" : "Acessorio");
                piece.SetAttribute("slot", slot.ToString(CultureInfo.InvariantCulture));
                piece.SetAttribute("drawable", drawable.ToString(CultureInfo.InvariantCulture));
                piece.SetAttribute("textura", texture.ToString(CultureInfo.InvariantCulture)); root.AppendChild(piece);
                if (kind == 1 && drawable < 0) piece.SetAttribute("estado", "ausente; textura não aplicável");
            }
        }
        if (actor.Visual.IsFreemode)
        {
            int eyes = Function.Call<int>(Hash.GET_HEAD_BLEND_EYE_COLOR, ped.Handle);
            if (eyes != actor.Visual.Head.Eyes) throw new InvalidOperationException("Cor dos olhos divergiu: " + actor.Id);
            root.SetAttribute("olhosGetter", eyes.ToString(CultureInfo.InvariantCulture));
            VisualDiretor.HeadState head;
            if (!VisualDiretor.TryReadHeadState(ped, out head)) throw new InvalidOperationException("DNA ainda não disponível na conferência de " + actor.Id);
            XmlElement dna = document.CreateElement("DNAGetter"); root.AppendChild(dna);
            dna.SetAttribute("pais", String.Join(",", Array.ConvertAll(head.Parents, delegate(int n) { return n.ToString(CultureInfo.InvariantCulture); })));
            dna.SetAttribute("misturas", String.Join(",", Array.ConvertAll(head.Mix, delegate(float n) { return n.ToString("R", CultureInfo.InvariantCulture); })));
            dna.SetAttribute("isParent", head.IsParent ? "true" : "false");
            for (int overlay = 0; overlay < 13; overlay++)
            {
                XmlElement item = document.CreateElement("Overlay"); root.AppendChild(item);
                item.SetAttribute("slot", overlay.ToString(CultureInfo.InvariantCulture));
                item.SetAttribute("indiceGetter", head.OverlayIndexes[overlay].ToString(CultureInfo.InvariantCulture));
                item.SetAttribute("opacidadeXML", actor.Visual.Head.Opacity[overlay].ToString("R", CultureInfo.InvariantCulture));
                item.SetAttribute("corXML", actor.Visual.Head.Overlays[overlay, 1].ToString(CultureInfo.InvariantCulture));
                item.SetAttribute("segundaCorXML", actor.Visual.Head.Overlays[overlay, 2].ToString(CultureInfo.InvariantCulture));
                item.SetAttribute("evidencia", "indice: getter; opacidade e cores: XML/setter, sem getter no Legacy");
            }
            root.SetAttribute("tracosEvidencia", "20 valores preservados no XML e enviados ao setter; sem getter no GTA V Legacy");
            root.SetAttribute("sobrancelhaCorXML", actor.Visual.Head.Overlays[2, 1].ToString(CultureInfo.InvariantCulture));
            root.SetAttribute("sobrancelhaCorEvidencia", "XML e setter com paleta1; aparência depende da prévia");
        }
        string path = Path.Combine(dataRoot, "logs", "conferencia_visual_" + actor.Id + "_" + Guid.NewGuid().ToString("N") + ".xml");
        RequirePlainPath(path);
        using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { document.Save(stream); stream.Flush(true); }
        WriteLog("VISUAL_GETTERS_CONFERIDOS", "Codigo=" + actor.Id + "; SHA256=" + actor.Visual.Sha256 + "; relatório=" + path + "; aprovação visual pendente");
    }

    private void FinishMount()
    {
        if (activeScene.Id == "EP10_C06")
        {
            RequireWritingC6Contract(activeScene, selectedSceneFile);
            if (Object.ReferenceEquals(directorWritingC6, null))
                directorWritingC6 = new EscritaFullTracks85Diretor(actorsById["djanira_pobre"], WriteLog, false, true, "EP10_C06");
            directorWritingC6.PollLoading(); if (!directorWritingC6.IsReady) return;
        }
        if (activeScene.Id == "teste_fila_85")
        {
            RequireWriting85Contract(activeScene, selectedSceneFile);
            if (Object.ReferenceEquals(directorWriting85, null))
            {
                string writingScenario85 = CenaJsonDiretor.LoadWriting85Scenario(Path.Combine(dataRoot, "catalogos", "trial-escrita85.json"));
                if (writingScenario85 == "FULLTRACK_WEED") RequireWeed85TechnicalState();
                directorWriting85 = new EscritaCenario85Diretor(actorsById["djanira_pobre"], writingScenario85, WriteLog);
            }
            directorWriting85.PollLoading();
            if (!directorWriting85.IsReady) return;
        }
        loadingTimer = null;
        awaitingCollision = false;
        areaPreparationTimer = Stopwatch.StartNew();
        sceneReady = true;
        mountedAt = unchecked((uint)Game.GameTime);
        WriteLog("CENA_MONTADA", "Id=" + activeScene.Id + "; personagens=" + createdPeds.Count + "; hora=" + activeScene.Hour + ":" + activeScene.Minute + "; clima=" + activeScene.WeatherName);
        ShowMessage("Diretor: personagens montados; posicionando câmera...");
    }

    private void RecordWardrobeEntities()
    {
        List<string> handles = new List<string>();
        foreach (Entity entity in directorWardrobe.OwnedEntities)
        {
            // A limpeza ambiental so varre peds/veiculos. Estas sete entidades
            // sao props persistentes proprios, fora dessas duas colecoes.
            if (Object.ReferenceEquals(entity, null) || !entity.Exists() ||
                Function.Call<int>(Hash.GET_ENTITY_TYPE, entity.Handle) != 3 || !entity.IsPersistent)
                throw new InvalidOperationException("Uma entidade propria do armario perdeu tipo/propriedade durante o preparo.");
            handles.Add(entity.Handle.ToString(CultureInfo.InvariantCulture));
        }
        if (handles.Count != 7) throw new InvalidOperationException("O armario modular exige sete props proprios prontos.");
        WriteLog("ARMARIO_ENTIDADES_PROPRIAS", "Handles=" + String.Join(",", handles.ToArray()) + "; props persistentes; nao elegiveis para a limpeza ambiental.");
    }

    private void StartSceneCamera()
    {
        foreach (KeyValuePair<string, Ped> actor in actorsById)
        {
            initialPositions[actor.Key] = actor.Value.Position;
            initialHeadings[actor.Key] = actor.Value.Heading;
        }
        cameraTargetIndex = 0;
        for (int i = 0; i < activeScene.Actors.Count; i++)
            if (activeScene.Actors[i].Id == activeScene.CameraActor)
                cameraTargetIndex = i;
        cameraPresetIndex = Array.IndexOf(cameraPresets, activeScene.CameraPreset);
        if (cameraPresetIndex < 0)
            throw new InvalidOperationException("Plano de câmera desconhecido: " + activeScene.CameraPreset);
        SaveAndHidePlayer();
        List<Ped> cameraActors = new List<Ped>();
        foreach (ActorData actor in activeScene.Actors) cameraActors.Add(actorsById[actor.Id]);
        if ((!Object.ReferenceEquals(directorSeasonFlight, null) && directorSeasonFlight.UsesOwnCamera) ||
            (!Object.ReferenceEquals(directorPlanePush, null) && directorPlanePush.UsesOwnCamera)) cameraActors.Clear();
        directorCamera.Initialize(cameraActors, anchor, forward, activeScene.InvertAxis);
        directorCamera.FocusOwnActorFromBehind = activeScene.Id == "EP11_C01";
        if (activeScene.Id == "EP07_C02")
            directorCamera.PrepareGalpaoWitnessClose(activeScene.Id, activeScene.LocalId, "DJANIRA_POBRE", actorsById["djanira_pobre"],
                directorProps, activeScene.CameraSide, activeScene.CameraHeight, activeScene.CameraNear);
        ApplyCurrentPreset(false);
        if (!Object.ReferenceEquals(directorStaging, null) && Object.ReferenceEquals(directorPoses, null)) directorStaging.Reset();
        if (!Object.ReferenceEquals(directorProps, null) && Object.ReferenceEquals(directorPoses, null)) directorProps.Reset();
        if (!Object.ReferenceEquals(directorInterpretation, null)) directorInterpretation.Reset();
        if (!Object.ReferenceEquals(directorDoors, null)) directorDoors.Reset();
        ResetSceneVehicles(true);
        if (IsPaperPoly86Source(activeScene, selectedSceneFile))
        {
            RequirePaperPoly86Contract(activeScene, selectedSceneFile);
            directorFoldPaper86 = new PapelDobradoPoly86Diretor(directorProps.GetProp("PAPEL_PROVA"),
                actorsById["djanira_pobre"], PaperPoly86SourceHash, WriteLog);
            directorFoldPaper86.StartPreview(); // Instance is Main-owned before camera/visibility mutations.
            return; // Actual rendered-camera readiness is polled next frame; no generic CAMERA_PRONTA yet.
        }
        if (IsAuthoredOpening99Source(activeScene, selectedSceneFile))
        {
            RequireAuthoredOpening99Contract(activeScene, selectedSceneFile);
            directorAuthoredOpening99 = new PapelAberturaAutoral99Diretor(actorsById["djanira_pobre"], directorProps.GetProp("PAPEL_HERANCA"),
                directorCamera.GetAuthoredOpening99OwnCamera(activeScene.Id, actorsById["djanira_pobre"]), PapelAberturaAutoral99Diretor.ExactSourceHash, captureEnabled, false, WriteLog);
            directorAuthoredOpening99.StartPreview(); // Main owns reference before held body/own visibility changes.
            return; // Next-frame actual clip/phase0/camera/hand proof precedes READY.
        }
        if (IsAuthoredPaperC5Source(activeScene))
        {
            RequireAuthoredPaperC5Contract(activeScene, selectedSceneFile);
            GTA.Prop parent = directorProps.GetProp("PAPEL_IRACI");
            directorAuthoredPaperC5 = new PapelDobradoAutoralC5Diretor(parent, actorsById["dona_iraci"], actorsById["djanira_pobre"],
                directorCamera.GetAuthoredPaperC5OwnCamera(activeScene.Id, parent), PapelDobradoAutoralC5Diretor.ExactSourceHash, WriteLog);
            directorProps.BindAuthoredPaperC5(directorAuthoredPaperC5, PapelDobradoAutoralC5Diretor.ExactSourceHash);
            directorAuthoredPaperC5.Start();
            return; // No READY before next-frame actual own rendering confirmation.
        }
        if (activeScene.Id == "EP10_C06") return; // Actual own close is checked on a later frame.
        if (activeScene.Id == "teste_fila_98")
        {
            RequirePocket98Contract(activeScene, selectedSceneFile);
            directorPocket98 = new PapelBolso98Diretor(directorProps, actorsById["djanira_rica"],
                directorCamera.GetPocket98OwnCamera(activeScene.Id, actorsById["djanira_rica"]), PapelBolso98Diretor.ExactSourceHash, WriteLog);
            directorProps.BindPocket98(directorPocket98, PapelBolso98Diretor.ExactSourceHash);
            directorPocket98.Start();
            return; // No READY before next-frame actual own rendering.
        }
        if (activeScene.Id == "teste_fila_99")
        {
            RequireOpening99Contract(activeScene, selectedSceneFile);
            if (FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)) == MapaEntrada99Diretor.ExactSourceSHA)
                directorMapOpening99 = new MapaEntrada99Diretor(activeScene.Id, MapaEntrada99Diretor.ExactSourceSHA,
                    actorsById["djanira_pobre"], captureEnabled, false, WriteLog);
            else
            {
                directorOpening99 = new ObservadorAbertura99(activeScene.Id, ObservadorAbertura99.ExactSourceSHA,
                    actorsById["djanira_pobre"], directorProps.GetReading99OwnProxy(activeScene.Id, ObservadorAbertura99.ExactSourceSHA),
                    captureEnabled, false, WriteLog); // No-writer proof above; constructor is getter-only.
                WriteLog("EP09_OBSERVER99_PREPARADO", "Scene=teste_fila_99; SourceSHA256=" + ObservadorAbertura99.ExactSourceSHA +
                    "; WindowMs=9000; Actor=DJANIRA_POBRE; SourcePairPHRightHand=True; Capture=False; OpeningProved=False; Draw=False");
            }
        }
        PrepareC4Viewport();
        if (IsRoomC4Scene && c4ViewportPending) return;
        areaPreparationTimer = null;
        ShowMessage(awaitingEpisodeReset ? "Diretor: preparando assentos; aguarde. " + clearKey + " cancela." :
            activeScene.AutomaticCamera || activeScene.Cuts.Count > 0 ? "Diretor: cena pronta. " + ReadyControls() :
            "Diretor: câmera pronta. " + presetKey + " muda plano; " + targetKey + " troca ator; " + actionKey + " ação; " + clearKey + " limpa.");
        WriteLog("CAMERA_PRONTA", "Plano=" + activeScene.CameraPreset + "; alvo=" + activeScene.CameraActor);
    }

    private void CameraCommand(Keys key)
    {
        if (HasOwnEpisodeCamera)
        { ShowMessage("A QUEDA usa câmera automática no avião e nos olhos de Djanira. F7 executa; F10 limpa."); return; }
        if (!sceneReady || !directorCamera.IsInitialized)
        {
            ShowMessage("Diretor: monte com " + mountKey + " e espere a câmera ficar pronta.");
            return;
        }
        if (countingDown || awaitingEpisodeReset || awaitingPoseReset)
        {
            ShowMessage("Diretor: espere a contagem terminar para ajustar a câmera. " + clearKey + " cancela.");
            return;
        }
        if (playing && (activeScene.AutomaticCamera || activeScene.Cuts.Count > 0))
        {
            ShowMessage("Diretor: a câmera acompanha o roteiro durante a ação. " + clearKey + " cancela.");
            return;
        }
        takeFinished = false;
        if (key == markCameraKey)
        {
            string name = cameraMarkName + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            directorCamera.SavePose(Path.Combine(dataRoot, "catalogos", "cameras_marcadas.xml"), name);
            ShowMessage("Câmera salva: " + name);
            return;
        }
        if (key == freeCameraKey)
        {
            directorCamera.ToggleFree();
            ShowMessage(directorCamera.IsFree ? "Câmera livre: WASD move; E sobe; Q desce; setas giram; Shift acelera; " + freeCameraKey + " fixa." : "Câmera fixada neste ângulo.");
            return;
        }
        if (key == presetKey)
        {
            do { cameraPresetIndex = (cameraPresetIndex + 1) % cameraPresets.Length; }
            while (activeScene.Actors.Count == 0 && !IsEnvironmentPreset(cameraPresets[cameraPresetIndex]));
        }
        else
        {
            if (activeScene.Actors.Count == 0) { ShowMessage("Este plano mostra o ambiente; não há ator para trocar."); return; }
            cameraTargetIndex = (cameraTargetIndex + 1) % activeScene.Actors.Count;
            // Em planos da dupla, escolher um ator precisa abrir um plano de rosto para ser visível.
            if (cameraPresets[cameraPresetIndex] == "DOIS" || cameraPresets[cameraPresetIndex] == "LONGE")
                cameraPresetIndex = Array.IndexOf(cameraPresets, "MEDIA");
        }
        ApplyCurrentPreset(activeScene.SmoothCamera);
        ShowMessage("Plano " + cameraPresets[cameraPresetIndex] + " em " +
            (activeScene.Actors.Count == 0 ? "ambiente" : activeScene.Actors[cameraTargetIndex].Name) + ".");
    }

    private void ApplyCurrentPreset(bool smooth)
    {
        if (HasOwnEpisodeCamera) return;
        if (activeScene.Id == "EP10_C06")
        {
            RequireWritingC6Contract(activeScene, selectedSceneFile);
            if (Object.ReferenceEquals(directorWritingC6, null) || !directorWritingC6.IsReady)
                throw new InvalidOperationException("C6: tres tracks proprias ainda nao preparadas.");
            directorCamera.ApplyWritingC6Close(directorWritingC6, actorsById["djanira_pobre"]); return;
        }
        if (activeScene.Id == "EP12_C01" || (activeScene.Id == "EP06_C02" && directorCamera.RestoredPoseName == "EP06_C02_BASE"))
        {
            directorCamera.RestorePose(Path.Combine(dataRoot, "catalogos", "cameras_marcadas.xml"), "EP06_C02_BASE");
            return;
        }
        if (!String.IsNullOrEmpty(activeScene.CameraObjeto))
        {
            if (activeScene.Id == "EP08_C05" && activeScene.CameraObjeto == "PAPEL_IRACI")
            {
                directorCamera.ApplyHandoffObjectClose(directorProps, activeScene.CameraObjeto,
                    actorsById["dona_iraci"], actorsById["djanira_pobre"],
                    activeScene.CameraFovObjeto, activeScene.CameraElevacaoObjeto);
                return;
            }
            directorCamera.ApplyObjectClose(directorProps, activeScene.CameraObjeto,
                activeScene.CameraFovObjeto, activeScene.CameraElevacaoObjeto, smooth);
            return;
        }
        if (activeScene.Actors.Count == 0)
        {
            directorCamera.ApplyEnvironmentPreset(cameraPresets[cameraPresetIndex], activeScene.CameraSide,
                activeScene.CameraHeight, activeScene.CameraNear, smooth);
            return;
        }
        if (activeScene.LocalId == "HOSPITAL_SANDY" && (activeScene.Id == "EP01_C04" || activeScene.Id == "EP01_C05"))
        {
            directorCamera.ApplyArrivalWide(anchor, forward, smooth);
            return;
        }
        if (activeScene.Id == "EP01_C06" && cameraPresets[cameraPresetIndex] == "LONGE")
        {
            directorCamera.ApplyArrivalWide(anchor, forward, smooth);
            return;
        }
        if (activeScene.Id == "EP01_C03" && cameraPresets[cameraPresetIndex] == "MEDIA")
        {
            directorCamera.ApplyOpticalMedium(actorsById[activeScene.Actors[cameraTargetIndex].Id], 22f);
            return;
        }
        if (activeScene.Id == "EP07_C02" && cameraPresets[cameraPresetIndex] == "PERTO" && activeScene.Actors[cameraTargetIndex].Id == "djanira_pobre")
        {
            directorCamera.ApplyGalpaoWitnessClose(activeScene.Id, activeScene.LocalId, "DJANIRA_POBRE", "PERTO", actorsById["djanira_pobre"],
                directorProps, activeScene.CameraSide, activeScene.CameraHeight, activeScene.CameraNear, smooth);
            return;
        }
        directorCamera.ApplyPreset(cameraPresets[cameraPresetIndex], actorsById[activeScene.Actors[cameraTargetIndex].Id],
            activeScene.CameraSide, activeScene.CameraHeight, activeScene.CameraNear, smooth);
    }

    private void SetSceneShot(string preset, string actorId, bool smooth)
    {
        if (HasOwnEpisodeCamera) return;
        if (activeScene.Id == "EP12_C01" || (activeScene.Id == "EP06_C02" && directorCamera.RestoredPoseName == "EP06_C02_BASE"))
        {
            directorCamera.RestorePose(Path.Combine(dataRoot, "catalogos", "cameras_marcadas.xml"), "EP06_C02_BASE");
            return;
        }
        int presetIndex = Array.IndexOf(cameraPresets, preset);
        int actorIndex = -1;
        for (int i = 0; i < activeScene.Actors.Count; i++)
            if (activeScene.Actors[i].Id == actorId)
                actorIndex = i;
        if (presetIndex < 0 || (activeScene.Actors.Count > 0 && actorIndex < 0) ||
            (activeScene.Actors.Count == 0 && (!String.IsNullOrEmpty(actorId) || !IsEnvironmentPreset(preset))))
            throw new InvalidOperationException("Câmera: plano ou interlocutor não encontrado: " + preset + "/" + actorId);
        cameraPresetIndex = presetIndex;
        cameraTargetIndex = System.Math.Max(0, actorIndex);
        ApplyCurrentPreset(smooth);
    }

    private void FrameSegment(SegmentData segment)
    {
        if (HasOwnEpisodeCamera) return;
        if (!activeScene.AutomaticCamera)
            return;
        string actorId = segment.Pause && segment.ActorId.Length > 0 ? segment.ActorId :
            (segment.Narration ? activeScene.CameraActor : segment.ActorId);
        string preset = segment.CameraPreset.Length > 0 ? segment.CameraPreset :
            (segment.Narration ? activeScene.NarrationPreset : activeScene.SpeechPreset);
        SetSceneShot(preset, actorId, activeScene.SmoothCamera);
        WriteLog("CAMERA_CORTE", "Índice=" + (segmentIndex + 1) + "; plano=" + preset +
            "; alvo=" + actorId + "; narração=" + segment.Narration + "; início_ms=" + segment.StartMs);
    }

    private void SaveAndHidePlayer()
    {
        if (playerStateSaved)
        {
            if (Object.ReferenceEquals(hiddenPlayer, null) || !hiddenPlayer.Exists())
                throw new InvalidOperationException("Jogador removido durante a montagem.");
            hiddenPlayer.IsVisible = false;
            hiddenPlayer.IsPositionFrozen = true;
            Function.Call(Hash.SET_ENTITY_COLLISION, hiddenPlayer.Handle, false, true);
            return;
        }
        hiddenPlayer = Game.Player.Character;
        if (Object.ReferenceEquals(hiddenPlayer, null) || !hiddenPlayer.Exists())
            throw new InvalidOperationException("Jogador não encontrado para preparar a câmera.");
        savedPlayerVisible = hiddenPlayer.IsVisible;
        savedPlayerFrozen = hiddenPlayer.IsPositionFrozen;
        savedPlayerCollision = hiddenPlayer.IsCollisionEnabled;
        savedPlayerPosition = hiddenPlayer.Position;
        savedPlayerHeading = hiddenPlayer.Heading;
        playerStateSaved = true;
        hiddenPlayer.IsVisible = false;
        hiddenPlayer.IsPositionFrozen = true;
        Function.Call(Hash.SET_ENTITY_COLLISION, hiddenPlayer.Handle, false, true);
        if (hiddenPlayer.IsCollisionEnabled)
            throw new InvalidOperationException("O jogador oculto ainda tem colisão. A montagem foi interrompida antes da ação.");
        WriteLog("JOGADOR_OCULTO", "Handle=" + hiddenPlayer.Handle + "; colisao_anterior=" + savedPlayerCollision + "; colisao_observada=" + hiddenPlayer.IsCollisionEnabled);
    }

    private void MaintainPlayerAndHud()
    {
        if (playerStateSaved && !Object.ReferenceEquals(hiddenPlayer, null) && hiddenPlayer.Exists())
        {
            hiddenPlayer.IsVisible = false;
            hiddenPlayer.IsPositionFrozen = true;
            Function.Call(Hash.SET_ENTITY_COLLISION, hiddenPlayer.Handle, false, true);
        }
        foreach (GTA.Control control in new GTA.Control[] { GTA.Control.Attack, GTA.Control.Attack2, GTA.Control.Aim, GTA.Control.MoveLeftRight,
            GTA.Control.MoveUpDown, GTA.Control.Jump, GTA.Control.Sprint, GTA.Control.Duck, GTA.Control.Enter, GTA.Control.MeleeAttackLight,
            GTA.Control.MeleeAttackHeavy, GTA.Control.MeleeAttackAlternate })
            Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)control, true);
        Function.Call(Hash.HIDE_HUD_AND_RADAR_THIS_FRAME);
        Function.Call(Hash.HIDE_HELP_TEXT_THIS_FRAME);
    }

    private bool RestorePlayer()
    {
        if (!playerStateSaved)
            return true;
        bool result = TryRestore("jogador", delegate {
            if (!Object.ReferenceEquals(hiddenPlayer, null) && hiddenPlayer.Exists())
            {
                Function.Call(Hash.REQUEST_COLLISION_AT_COORD, savedPlayerPosition.X, savedPlayerPosition.Y, savedPlayerPosition.Z);
                hiddenPlayer.PositionNoOffset = savedPlayerPosition;
                hiddenPlayer.Heading = savedPlayerHeading;
                hiddenPlayer.IsVisible = savedPlayerVisible;
                Function.Call(Hash.SET_ENTITY_COLLISION, hiddenPlayer.Handle, savedPlayerCollision, true);
                if (hiddenPlayer.IsCollisionEnabled != savedPlayerCollision)
                    throw new InvalidOperationException("A colisão anterior do jogador ainda não foi restaurada.");
                hiddenPlayer.IsPositionFrozen = savedPlayerFrozen;
                WriteLog("JOGADOR_RESTAURADO", "Handle=" + hiddenPlayer.Handle + "; colisao_observada=" + hiddenPlayer.IsCollisionEnabled + "; visivel=" + hiddenPlayer.IsVisible + "; congelado=" + hiddenPlayer.IsPositionFrozen);
            }
        });
        if (result)
        {
            playerStateSaved = false;
            hiddenPlayer = null;
        }
        return result;
    }

    private void RequestAnimation(string dictionary)
    {
        if (requestedAnimations.Contains(dictionary))
            return;
        if (!Function.Call<bool>(Hash.DOES_ANIM_DICT_EXIST, dictionary))
            throw new InvalidOperationException("Dicionário de animação ausente no jogo: " + dictionary);
        requestedAnimations.Add(dictionary);
        Function.Call(Hash.REQUEST_ANIM_DICT, dictionary);
        WriteLog("ANIMACAO_SOLICITADA", dictionary);
    }

    private void LoadGestures()
    {
        gestures.Clear();
        string path = Path.Combine(dataRoot, "catalogos", "gestos_fala.xml");
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Prohibit;
        settings.XmlResolver = null;
        settings.MaxCharactersInDocument = 100000;
        XmlDocument document = new XmlDocument();
        document.XmlResolver = null;
        using (XmlReader reader = XmlReader.Create(path, settings))
            document.Load(reader);
        if (document.DocumentElement.Name != "GestosFala" || document.DocumentElement.GetAttribute("versao") != "1")
            throw new InvalidOperationException("Catálogo de gestos inválido: esperado GestosFala, versão 1.");
        foreach (XmlElement element in document.DocumentElement.SelectNodes("Gesto"))
        {
            string id = element.GetAttribute("id");
            if (!IsIntention(id) || gestures.ContainsKey(id))
                throw new InvalidOperationException("Intenção inválida ou repetida no catálogo: " + id);
            GestureData gesture = new GestureData();
            XmlElement male = element["Homem"], female = element["Mulher"];
            if (Object.ReferenceEquals(male, null) || Object.ReferenceEquals(female, null))
                throw new InvalidOperationException("Gesto " + id + ": faltam as variantes Homem e Mulher.");
            gesture.MaleDictionary = RequiredAnimationAttribute(male, "dicionario");
            gesture.MaleClip = RequiredAnimationAttribute(male, "animacao");
            gesture.FemaleDictionary = RequiredAnimationAttribute(female, "dicionario");
            gesture.FemaleClip = RequiredAnimationAttribute(female, "animacao");
            gestures.Add(id, gesture);
        }
    }

    private static void ValidateBodyClip(string dictionary, string clip)
    {
        float duration = Function.Call<float>(Hash.GET_ANIM_DURATION, dictionary, clip);
        if (Single.IsNaN(duration) || Single.IsInfinity(duration) || duration <= 0f)
            throw new InvalidOperationException("Animação corporal não encontrada: " + dictionary + " / " + clip);
    }

    private static string RequiredAnimationAttribute(XmlElement element, string attribute)
    {
        string value = element.GetAttribute(attribute);
        if (String.IsNullOrWhiteSpace(value) || value.Length > 160 || value != value.Trim())
            throw new InvalidOperationException("Catálogo de gestos: valor inválido em " + element.Name + "/" + attribute);
        return value;
    }

    private static bool IsIntention(string intention)
    {
        return intention == "neutro" || intention == "explicando" || intention == "negando" ||
            intention == "implorando" || intention == "irritado" || intention == "machucado";
    }

    private static bool IsMale(Ped ped)
    {
        return Function.Call<bool>(Hash.IS_PED_MALE, ped.Handle);
    }

    private void ResetMouth(Ped ped)
    {
        Function.Call(Hash.PLAY_FACIAL_ANIM, ped.Handle, "mood_normal_1",
            IsMale(ped) ? MaleNeutralDictionary : FemaleNeutralDictionary);
        if (!Object.ReferenceEquals(directorInterpretation, null))
            foreach (KeyValuePair<string, Ped> actor in actorsById)
                if (Object.ReferenceEquals(actor.Value, ped)) directorInterpretation.InvalidateFace(actor.Key);
    }

    private void RestartTake()
    {
        if (!sceneReady || !directorCamera.IsInitialized)
        {
            ShowMessage("Diretor: monte com " + mountKey + " e espere a câmera ficar pronta.");
            return;
        }
        bool interrupted = playing || countingDown || awaitingCaptureStart || awaitingCaptureEnd;
        StopAutomaticRecording();
        if (!Object.ReferenceEquals(directorCapture, null)) directorCapture.Cancel("reinício de tomada");
        ResetTakeState(true);
        actionAfterPoseReset = false;
        WriteLog(awaitingPoseReset || awaitingEpisodeReset ? "TOMADA_REINICIO_PENDENTE" : "TOMADA_REINICIADA",
            "Cena=" + activeScene.Id + "; interrompida=" + interrupted + "; personagens mantidos=" + createdPeds.Count +
            "; retorno concluído=" + (!awaitingPoseReset && !awaitingEpisodeReset));
        ShowMessage(awaitingPoseReset || awaitingEpisodeReset ? "Diretor: restaurando atores e apoios..." : "Diretor: tomada reiniciada. " + ReadyControls());
    }

    private void ResetTakeState(bool restoreCamera)
    {
        RequirePaperPoly86NoAction();
        if (!Object.ReferenceEquals(directorAuthoredOpening99, null)) throw new InvalidOperationException("Papel99 autoral: nova pose somente F10/F6; Reset/reattach nao permitidos.");
        if (!Object.ReferenceEquals(directorCapture, null) && directorCapture.IsArmed &&
            !Object.ReferenceEquals(directorSeasonFlight, null) &&
            directorSeasonFlight.Part == CenaVooTemporadaDiretor.BoardingPart.EmbarqueDjaniraDecolagem)
            throw new InvalidOperationException("C3 parte B: cancelar/parar captura antes de reiniciar assentos de preparo.");
        RequireActors();
        if (activeScene.Id == "EP10_C06") RequireWritingC6Prepared();
        playing = false; countingDown = false; subtitleText = ""; takeFinished = false;
        awaitingCaptureStart = false; captureStartTimer = null; preparedCaptureSrt = null;
        awaitingCaptureEnd = false; captureEndTimer = null;
        if ((!Object.ReferenceEquals(directorEpisodeOne, null) && !directorEpisodeOne.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorPilot, null) && !directorPilot.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorRehearsal, null) && !directorRehearsal.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorWriting85, null) && !directorWriting85.PrepareForTakeReset()) ||
            (!Object.ReferenceEquals(directorThreeSeven, null) && !directorThreeSeven.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorSeasonFlight, null) && !directorSeasonFlight.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorPlanePush, null) && !directorPlanePush.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorWardrobe, null) && !directorWardrobe.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorEntry, null) && !directorEntry.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorTableWipe, null) && !directorTableWipe.ReleaseActorsForReset()) ||
            (!Object.ReferenceEquals(directorCartaTrial, null) && !ReleaseCartaTrialForReset()) ||
            (!Object.ReferenceEquals(directorGorjetaTrial, null) && !ReleaseGorjetaTrialForReset()) ||
            (!Object.ReferenceEquals(directorVehicles, null) && !directorVehicles.ReleaseActorsForReset()))
        {
            awaitingEpisodeReset = true; episodeResetNeedsRelease = true;
            episodeResetRestoreCamera = restoreCamera;
            if (Object.ReferenceEquals(episodeResetTimer, null)) episodeResetTimer = Stopwatch.StartNew();
            directorCamera.FixCurrentPose();
            return;
        }
        awaitingEpisodeReset = false;
        if (!Object.ReferenceEquals(directorStaging, null)) directorStaging.ReleaseActorsForReset();
        if (!Object.ReferenceEquals(directorPoses, null)) directorPoses.ReleaseActorsForReset();
        if (!Object.ReferenceEquals(directorDoors, null)) directorDoors.ReleaseActorsForReset();
        foreach (KeyValuePair<string, Ped> actor in actorsById)
            if (!initialPositions.ContainsKey(actor.Key) || !initialHeadings.ContainsKey(actor.Key))
                throw new InvalidOperationException("As marcas iniciais não estão prontas. Monte novamente com " + mountKey + ".");

        playing = false;
        countingDown = false;
        nextWaterCameraTime = 0;
        takeFinished = false;
        countdownStart = 0;
        actionStartTime = 0;
        segmentIndex = -1;
        nextMouthTime = 0;
        mouthInterval = 600;
        nextTerminalCameraTime = 0;
        subtitleText = "";
        statusText = "";
        statusUntil = 0;
        foreach (KeyValuePair<string, Ped> actor in actorsById)
        {
            Ped ped = actor.Value;
            if (activeScene.Id == "EP10_C06")
            {
                RequireWritingC6Prepared();
                if (actor.Key != "djanira_pobre" || actorsById.Count != 1 ||
                    (ped.Position - initialPositions[actor.Key]).Length() > 0.01f ||
                    System.Math.Abs(ped.Heading - initialHeadings[actor.Key]) > 0.1f)
                    throw new InvalidOperationException("C6: manter mesma pose phase0 ate contagem; apos acao F10/F6.");
                WriteLog("ESCRITA_C6_PREPARO_PRESERVADO", "OwnHeldPhase0=True; ActorTaskCleared=False; ActorTransformSetter=False");
                continue;
            }
            if (activeScene.Id == "teste_fila_85" && !Object.ReferenceEquals(directorWriting85, null) && directorWriting85.IsWeedFullTrack)
            {
                RequireWeed85TechnicalState(); directorWriting85.RequirePreparedWeedPose();
                if (actor.Key != "djanira_pobre" || actorsById.Count != 1 ||
                    (ped.Position - initialPositions[actor.Key]).Length() > 0.01f ||
                    System.Math.Abs(ped.Heading - initialHeadings[actor.Key]) > 0.1f)
                    throw new InvalidOperationException("WEED85 reset exige a marca real da mesma pose preparada; sem reposicionar/limpar tarefa.");
                WriteLog("ESCRITA85_WEED_MARCA_PRESERVADA", "Handle=" + ped.Handle + "; ActualRoot=" + ped.Position +
                    "; OwnPhase0SceneHeld=True; ActorTaskCleared=False; ActorTransformSetter=False; Capture=False");
                continue;
            }
            ped.IsPositionFrozen = true;
            ped.Task.ClearAllImmediately();
            ped.Task.ClearLookAt();
            ped.Velocity = Vector3.Zero;
            ped.PositionNoOffset = initialPositions[actor.Key];
            ped.Heading = initialHeadings[actor.Key];
            if (activeScene.Segments.Count > 0)
                ResetMouth(ped);
            ped.Task.StandStill(-1);
            WriteLog("MARCA_RESTAURADA", "Cena=" + activeScene.Id + "; ator=" + actor.Key +
                "; handle=" + ped.Handle + "; posição=" + ped.Position + "; direção=" + ped.Heading.ToString(CultureInfo.InvariantCulture));
        }
        startedActions.Clear();
        stagingTaskRevisions.Clear();
        finishedActions.Clear();
        turnStartHeadings.Clear();
        turnTargetHeadings.Clear();
        cutIndex = -1;
        // Conservar os destinos de caminhada e a câmera existentes para a próxima tomada.
        if (restoreCamera)
            SetSceneShot(activeScene.CameraPreset, activeScene.CameraActor, false);
        directorCamera.FixCurrentPose();
        if (activeScene.Id == "teste_fila_90") SetSceneShot(activeScene.CameraPreset, activeScene.CameraActor, false);
        if (!Object.ReferenceEquals(directorStaging, null)) directorStaging.Reset();
        if (!Object.ReferenceEquals(directorProps, null)) directorProps.Reset();
        if (!Object.ReferenceEquals(directorPoses, null))
        {
            directorPoses.Reset();
            awaitingPoseReset = !directorPoses.IsReady;
            cameraAfterPoseReset = restoreCamera;
        }
        if (!Object.ReferenceEquals(directorInterpretation, null)) directorInterpretation.Reset();
        if (!Object.ReferenceEquals(directorDoors, null)) directorDoors.Reset();
        ResetSceneVehicles(restoreCamera);
        if (!awaitingEpisodeReset) episodeResetTimer = null;
    }

    private void ResetSceneVehicles(bool restoreCamera)
    {
        bool ready = true;
        if (!Object.ReferenceEquals(directorPilot, null)) ready = directorPilot.Reset() && ready;
        if (!Object.ReferenceEquals(directorEpisodeOne, null)) ready = directorEpisodeOne.Reset() && ready;
        if (!Object.ReferenceEquals(directorRehearsal, null)) ready = directorRehearsal.Reset() && ready;
        if (!Object.ReferenceEquals(directorThreeSeven, null)) ready = directorThreeSeven.Reset() && ready;
        if (!Object.ReferenceEquals(directorSeasonFlight, null)) ready = directorSeasonFlight.Reset() && ready;
        if (!Object.ReferenceEquals(directorPlanePush, null)) ready = directorPlanePush.Reset() && ready;
        if (!Object.ReferenceEquals(directorWardrobe, null)) ready = directorWardrobe.Reset() && ready;
        if (!Object.ReferenceEquals(directorEntry, null)) ready = directorEntry.Reset() && ready;
        if (!Object.ReferenceEquals(directorTableWipe, null)) ready = directorTableWipe.Reset() && ready;
        if (activeScene.Id == CartaFisicaDiretor.SceneId) ready = ResetCartaTrial() && ready;
        if (activeScene.Id == GorjetaFisicaTrialDiretor.SceneId) ready = ResetGorjetaTrial() && ready;
        if (!Object.ReferenceEquals(directorVehicles, null)) ready = directorVehicles.Reset() && ready;
        if (!Object.ReferenceEquals(directorSeasonWater, null)) ready = directorSeasonWater.Reset() && ready;
        awaitingEpisodeReset = !ready;
        if (!ready)
        {
            episodeResetNeedsRelease = false;
            episodeResetRestoreCamera = restoreCamera;
            if (Object.ReferenceEquals(episodeResetTimer, null)) episodeResetTimer = Stopwatch.StartNew();
        }
        else
        {
            episodeResetTimer = null;
            // Reset pode sentar/mover o ator no mesmo frame; reenquadrar também
            // nesse caminho, antes dos três segundos de contagem.
            if (restoreCamera && directorCamera.IsInitialized && !awaitingPoseReset)
            {
                SetSceneShot(activeScene.CameraPreset, activeScene.CameraActor, false);
                directorCamera.FixCurrentPose();
            }
        }
    }

    private void AdvanceEpisodeReset()
    {
        bool technicalTrial = activeScene.Id == CartaFisicaDiretor.SceneId || activeScene.Id == GorjetaFisicaTrialDiretor.SceneId;
        int resetMaximum = technicalTrial ? 15000 : 5000;
        if (Object.ReferenceEquals(episodeResetTimer, null) || episodeResetTimer.ElapsedMilliseconds > resetMaximum)
            throw new InvalidOperationException(technicalTrial ?
                "O streaming do ensaio tecnico nao terminou em15s." :
                "O retorno aos assentos próprios não terminou em cinco segundos. A tomada foi interrompida.");
        if (episodeResetNeedsRelease) ResetTakeState(episodeResetRestoreCamera);
        else ResetSceneVehicles(episodeResetRestoreCamera);
        if (awaitingEpisodeReset) return;
        WriteLog("RETORNO_VEICULOS_CONCLUIDO", "Cena=" + activeScene.Id + "; assentos conferidos fora da ação/contagem.");
        if (episodeResetRestoreCamera) SetSceneShot(activeScene.CameraPreset, activeScene.CameraActor, false);
        directorCamera.FixCurrentPose();
        if (actionAfterPoseReset && !awaitingPoseReset) { actionAfterPoseReset = false; BeginPreparedAction(); }
        else if (!awaitingPoseReset) ShowMessage("Diretor: tomada reiniciada. " + ReadyControls());
    }

    private string QueueCaption()
    {
        if (selectedEpisode == 7 && productionSceneIndex == 2)
            return "EP07_C03 — Parte " + CenaVooTemporadaDiretor.PartName((CenaVooTemporadaDiretor.BoardingPart)flightBoardingSubtake) +
                " | T: Integral / embarque Dinjer-Chefe / Djanira-decolagem | partes exigem montagem45s";
        if (selectedEpisode == 1 && activeScene.Id == "EP01_C05")
            return "EP01 — Cena 5 de 9 — EP01_C05 — Subtomada " + (secretGrandmaSubtake == 0 ? "hospital" : "cassino") + " | T alterna fora da tomada";
        if (selectedEpisode == 2 && activeScene.Id == "EP02_C01")
            return "EP02 — Cena 1 — EP02_C01 — " + activeScene.LocalId + " | T alterna dentro/fora antes da tomada";
        if (selectedEpisode > 0 && !Object.ReferenceEquals(season, null))
            return "EP" + activeScene.Episode.ToString("D2", CultureInfo.InvariantCulture) + " — Cena " + activeScene.Number + " de " +
                season.GetEpisode(selectedEpisode).Count + " — " + activeScene.Id + " — " + activeScene.Name;
        if (Object.ReferenceEquals(activeQueue, null))
            return activeScene.Id + " — " + activeScene.Name;
        string episode = activeQueue.Episode == 0 ? "TESTE" : "EP" + activeQueue.Episode.ToString("D2", CultureInfo.InvariantCulture);
        return episode + " — Cena " + (queueIndex + 1) + " de " + activeQueue.Arquivos.Count +
            " — " + activeScene.Id + " — " + activeScene.Name;
    }

    private string ReadyControls()
    {
        if (awaitingPoseReset || awaitingEpisodeReset) return "Preparando posições e apoios; " + clearKey + " cancela";
        if (!Object.ReferenceEquals(directorCapture, null) && directorCapture.IsPending)
            return "Pare a gravação com Win+Alt+R. Organizando a tomada...";
        return actionKey + (CanRecordAutomatically() ? " gravar e rodar | " : " ação | ") + (captureEnabled ? KeyLabel(recordKey) + " gravar manualmente | " : "") + KeyLabel(restartKey) + " reiniciar | " +
            (queueFile.Length == 0 ? "" : KeyLabel(nextSceneKey) + " próxima | " + KeyLabel(previousSceneKey) + " anterior | ") + clearKey + " limpar";
    }

    // C7 only: ReadScene already validated exact JSON/schema. Preserve all non-camera bytes.
    private static string C7BodySourceHash(string path)
    {
        RequirePlainPath(path);
        string text = new System.Text.UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
        int depth = 0, token = -1, start = -1, end = -1, opening = -1, found = 0;
        bool quoted = false, escaped = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"')
                {
                    quoted = false;
                    if (depth == 1 && text.Substring(token, i - token + 1) == "\"camera\"")
                    {
                        int cursor = i + 1;
                        while (cursor < text.Length && Char.IsWhiteSpace(text[cursor])) cursor++;
                        if (cursor < text.Length && text[cursor] == ':')
                        {
                            if (++found != 1) throw new InvalidOperationException("C7 camera top-level duplicada.");
                            start = token; cursor++;
                            while (cursor < text.Length && Char.IsWhiteSpace(text[cursor])) cursor++;
                            if (cursor >= text.Length || text[cursor] != '{') throw new InvalidOperationException("C7 camera deve ser objeto.");
                            opening = cursor;
                        }
                    }
                }
            }
            else if (c == '"') { quoted = true; token = i; }
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']')
            {
                if (--depth < 0) throw new InvalidOperationException("C7 projecao JSON invalida.");
                if (c == '}' && depth == 1 && opening >= 0 && end < 0 && i > opening) end = i + 1;
            }
        }
        if (quoted || depth != 0 || found != 1 || start < 0 || end <= opening)
            throw new InvalidOperationException("C7 exige um objeto camera top-level exato.");
        int separator = end;
        while (separator < text.Length && Char.IsWhiteSpace(text[separator])) separator++;
        if (separator < text.Length && text[separator] == ',') end = separator + 1;
        else
        {
            separator = start - 1;
            while (separator >= 0 && Char.IsWhiteSpace(text[separator])) separator--;
            if (separator < 0 || text[separator] != ',') throw new InvalidOperationException("C7 separador camera ausente.");
            start = separator;
        }
        byte[] projection = new System.Text.UTF8Encoding(false, true).GetBytes(text.Remove(start, end - start));
        using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(projection)).Replace("-", "");
    }


    private static string FlightSourceHash(string path)
    {
        RequirePlainPath(path);
        using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }
    private string FlightSourceStatus()
    {
        if (Object.ReferenceEquals(activeScene, null) || activeScene.Id != "EP07_C03") return "";
        return "; parte=" + CenaVooTemporadaDiretor.PartName(CenaVooTemporadaDiretor.PartFromSource(activeScene.Id, selectedSceneFile)) +
            "; arquivo=" + selectedSceneFile.Replace('\\', '/') + "; sourceSHA256=" + mountedFlightSourceHash;
    }
    private void RequireFlightSource()
    {
        if (activeScene.Id != "EP07_C03") return;
        CenaVooTemporadaDiretor.BoardingPart part = CenaVooTemporadaDiretor.PartFromSource(activeScene.Id, selectedSceneFile);
        if (Object.ReferenceEquals(directorSeasonFlight, null) || directorSeasonFlight.Part != part ||
            String.IsNullOrEmpty(mountedFlightSourceHash) ||
            mountedFlightSourceHash != FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)))
            throw new InvalidOperationException("Parte/fonte C3 difere da montagem; remonte antes de gravar.");
    }
    private static string DismissalPartName(int part)
    {
        if (part == 1) return "A_PRIMEIRA";
        if (part == 2) return "B_SEGUNDA";
        if (part == 3) return "C_TERCEIRA";
        throw new InvalidOperationException("EP13_C02 exige parte A/B/C, nenhuma principal integral.");
    }
    private static int DismissalPartIndex(string source)
    {
        string normalized = (source ?? "").Replace('\\', '/');
        for (int part = 1; part <= 3; part++)
            if (normalized == "subtomadas/EP13_C02/" + DismissalPartName(part) + "/EP13_C02.json") return part;
        throw new InvalidOperationException("EP13_C02 somente caminhos literais das tres partes, sem principal.");
    }
    private static void RequirePurchaseSceneContract(SceneData scene, string source, string sourceHash)
    {
        string part = TemporadaDiretor.PurchasePartFromSourceHash(sourceHash);
        if (source != "EP13_C03.json" || scene.Id != "EP13_C03" || scene.Episode != 13 || scene.Number != 3 ||
            scene.LocalId != "GALPAO_CYPRESS" || !scene.MarkedLocation || scene.Encenacao != "serie_v1" ||
            scene.DurationMs != 13000 || scene.Actors.Count != 0 || scene.Actions.Count != 0 || scene.Direcao.Count != 0 ||
            scene.Interpretacao.Count != 0 || scene.Objetos.Count != 0 || scene.Poses.Count != 0 || scene.TransicoesPose.Count != 0 ||
            scene.SaidaAgua || scene.EmpurraoAviao || scene.ArmarioModular || scene.Cuts.Count != 0 ||
            !scene.AutomaticCamera || !String.IsNullOrEmpty(scene.CameraActor) || !String.IsNullOrEmpty(scene.CameraObjeto) ||
            scene.CameraPreset != "LONGE" || scene.NarrationPreset != "LONGE" || scene.FinalPreset != "LONGE" ||
            scene.Segments.Count != 1 || !scene.Segments[0].Narration || scene.Segments[0].Pause ||
            scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 13000 ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("EP13_C03 exige fonte literal13s, marca real, modo historia e nenhum ator/acao duplicada.");
        bool trucks = part == "B_CAMINHOES_SAINDO", gate = part == "C_PORTAO_FECHANDO";
        if (scene.Veiculos.Count != (trucks ? 2 : 0) || scene.Portas.Count != (gate ? 2 : 0))
            throw new InvalidOperationException("EP13_C03 parte difere dos controladores genericos previstos; nao duplicar eventos.");
        if (trucks)
            for (int i = 0; i < 2; i++)
            {
                VeiculosSerieDiretor.VehicleSpec vehicle = scene.Veiculos[i];
                float lane = i == 0 ? -3f : 3f;
                if (vehicle.Codigo != "CAMINHAO_" + (i + 1) || vehicle.Tipo != "CAMINHAO" || vehicle.Modelo != (i == 0 ? "packer" : "hauler") ||
                    !vehicle.MotoristaExtra || !vehicle.TrajetoDireto || vehicle.ActorInitialSeats.Count != 0 || vehicle.HeadingRelativo != 180f ||
                    vehicle.PosicaoRelativa.X != lane || vehicle.PosicaoRelativa.Y != -5f || vehicle.PosicaoRelativa.Z != 0f || vehicle.Actions.Count != 1)
                    throw new InvalidOperationException("EP13_C03 exige os dois trator units literais e motoristas extras proprios.");
                VeiculosSerieDiretor.VehicleAction action = vehicle.Actions[0];
                if (action.Tipo != "dirigir" || action.Ator != null || action.InicioMs != 0 || action.DuracaoMs != 12000 || action.Assento != -1 ||
                    action.Velocidade != 4f || action.RaioChegada != 1.5f || !action.RaioParada.HasValue || action.RaioParada.Value != 1f ||
                    action.RotaRelativa.Count != 1 || action.RotaRelativa[0].X != lane || action.RotaRelativa[0].Y != -17f || action.RotaRelativa[0].Z != 0f)
                    throw new InvalidOperationException("EP13_C03 exige saidas fisicas simultaneas12m/12s, sem reassento ou evento alternativo.");
            }
        if (gate)
            for (int i = 0; i < 2; i++)
            {
                PortasSerieDiretor.DoorSpec door = scene.Portas[i];
                if (door.Tipo != (i == 0 ? "abrir" : "fechar") || door.Ator != null || door.InicioMs != i * 3000 || door.DuracaoMs != (i == 0 ? 3000 : 4500))
                    throw new InvalidOperationException("EP13_C03 exige abrir0..3 e fechar3..7.5 na folha real, sem ator inventado.");
            }
    }
    private void LogPurchaseSource(string stage)
    {
        if (Object.ReferenceEquals(activeScene, null) || activeScene.Id != "EP13_C03") return;
        RequirePurchaseSceneContract(activeScene, selectedSceneFile, mountedPurchaseSourceHash);
        if (mountedPurchaseSourceHash != FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)))
            throw new InvalidOperationException("Fonte EP13_C03 difere da montagem; remonte antes de continuar.");
        WriteLog("EP13_COMPRA_FONTE_CONFERIDA", "Cena=EP13_C03; Etapa=" + stage + "; Parte=" +
            TemporadaDiretor.PurchasePartFromSourceHash(mountedPurchaseSourceHash) + "; Arquivo=" + selectedSceneFile +
            "; SHA256=" + mountedPurchaseSourceHash + "; DuracaoMs=13000; IntegralNarrativa=False; PartesNecessarias=3;" +
            " ControladoresGenericosUnicos=True; CapturaComumSemSidecarDeParte=True; AprovacaoVisual=False");
    }
    private static void RequireDismissalSceneContract(SceneData scene, string source)
    {
        int part = DismissalPartIndex(source);
        string receiver = "CAPANGA_" + part;
        string directionReceiver = receiver.ToLowerInvariant(); // CenaJsonDiretor.ReadDirection normaliza Ator.
        if (scene.Id != "EP13_C02" || scene.LocalId != "BOATE_DENTRO" || scene.DurationMs != 14000 ||
            scene.Encenacao != "serie_v1" || scene.Actors.Count != 5 || scene.Actions.Count != 0 ||
            scene.Objetos.Count != part || scene.Direcao.Count != (part < 3 ? 3 : 2) ||
            scene.Segments.Count != 2 || !scene.Segments[0].Narration || scene.Segments[0].Pause ||
            scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 10000 ||
            !scene.Segments[1].Pause || scene.Segments[1].StartMs != 10000 || scene.Segments[1].EndMs != 14000)
            throw new InvalidOperationException("EP13_C02 exige14s, BOATE_DENTRO, cinco identidades e uma entrega por parte.");
        foreach (string actor in new string[] { "dinjer", "capanga_1", "capanga_2", "capanga_3", "capanga_4" })
            if (!scene.Actors.Exists(delegate(ActorData item) { return item.Id == actor; }))
                throw new InvalidOperationException("EP13_C02 perdeu identidade original; nao recriar visuais.");
        for (int number = 1; number <= part; number++)
        {
            string code = "ENVELOPE_CAPANGA_" + number;
            ObjetosSerieDiretor.PropSpec prop = scene.Objetos.Find(delegate(ObjetosSerieDiretor.PropSpec item) { return item.Codigo == code; });
            if (prop == null || prop.Recurso != "envelope" || prop.Modelo != "prop_cs_envolope_01" ||
                !prop.VisivelInicial || prop.AssentarNoChao || prop.MaoInicial != "direita" ||
                prop.AtorInicial != (number == part ? "CAPANGA_4" : "CAPANGA_" + number) ||
                prop.Eventos.Count != (number == part ? 1 : 0))
                throw new InvalidOperationException("Gerente deve ter so1 envelope, anteriores com os empregados; Dinjer fica sem.");
            if (number != part) continue;
            ObjetosSerieDiretor.PropEvent transfer = prop.Eventos[0];
            if (transfer.Tipo != "transferir" || transfer.Ator != receiver || transfer.Doador != "CAPANGA_4" ||
                transfer.Mao != "direita" || transfer.InicioMs != 1000 || transfer.EsperaMaximaMs != 2000 ||
                transfer.Dicionario != "mp_common" || transfer.ClipeDoador != "givetake1_a" || transfer.Clipe != "givetake1_b")
                throw new InvalidOperationException("Entrega EP13 exige pares/maos/tempo/clipes guardados originais.");
        }
        int giverCount = 0, takerCount = 0, walkCount = 0;
        foreach (AcoesSerieDiretor.ActorAction action in scene.Direcao)
        {
            bool giver = action.Ator == "capanga_4" && action.Recurso == "entrega_a" && action.Clipe == "givetake1_a";
            bool taker = action.Ator == directionReceiver && action.Recurso == "entrega_b" && action.Clipe == "givetake1_b";
            if (action.Tipo == "clipe" && (giver || taker) && action.Dicionario == "mp_common" &&
                action.InicioMs == 0 && action.DuracaoMs == 3500 && !action.Loop && !action.ManterPoseFinal &&
                action.CorpoInteiro && action.PreservarDuranteFala)
            { if (giver) giverCount++; else takerCount++; continue; }
            if (part < 3 && action.Tipo == "andar" && action.Ator == "capanga_4" &&
                action.InicioMs == 3500 && action.DuracaoMs == 1500 && action.Velocidade == 1f &&
                action.Deslocamento.X == part * 1.2f && action.Deslocamento.Y == -0.9f && action.Deslocamento.Z == 0f)
            { walkCount++; continue; }
            throw new InvalidOperationException("EP13 parte nao pode ocultar/reposicionar/envelopes extras nem substituir entrega/caminhada.");
        }
        if (giverCount != 1 || takerCount != 1 || walkCount != (part < 3 ? 1 : 0))
            throw new InvalidOperationException("EP13 exige par doador/receptor unico e caminhada para proximo empregado.");
    }
    private bool IsPaperPoly86Source(SceneData scene, string source)
    {
        return !Object.ReferenceEquals(scene, null) && scene.Id == "teste_fila_86" && source == "teste_fila_86.json" &&
            FlightSourceHash(Path.Combine(scenesDirectory, source)) == PaperPoly86SourceHash;
    }
    private void RequirePaperPoly86NoAction()
    {
        if (!Object.ReferenceEquals(directorFoldPaper86, null) || IsPaperPoly86Source(activeScene, selectedSceneFile))
            throw new InvalidOperationException("PapelPoly86 somente F6: F7/Reset/ARM/SAVE vetados; superficie visual autoral, collider dobrado NAO provado.");
    }
    private void RequirePaperPoly86Contract(SceneData scene, string source)
    {
        if (!IsPaperPoly86Source(scene, source) || scene.Episode != 0 || scene.Number != 0 ||
            scene.LocalId != "CASA_VO_INTERIOR" || !scene.MarkedLocation || scene.Encenacao != "serie_v1" || scene.DurationMs != 5000 ||
            scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" || scene.Actions.Count != 0 || scene.Direcao.Count != 0 ||
            scene.Interpretacao.Count != 0 || scene.Poses.Count != 0 || scene.TransicoesPose.Count != 0 || scene.Veiculos.Count != 0 || scene.Portas.Count != 0 ||
            scene.Cuts.Count != 0 || scene.SaidaAgua || scene.EmpurraoAviao || scene.ArmarioModular ||
            scene.Segments.Count != 1 || !scene.Segments[0].Pause || scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 5000 ||
            scene.Objetos.Count != 1 || scene.CameraObjeto != "PAPEL_PROVA" || !scene.AutomaticCamera ||
            scene.Objetos[0].Codigo != "PAPEL_PROVA" || scene.Objetos[0].Recurso != "papel_preview86_nota_s" ||
            scene.Objetos[0].Modelo != "p_amanda_note_01_s" || scene.Objetos[0].AssentarNoChao || !scene.Objetos[0].VisivelInicial ||
            scene.Objetos[0].AtorInicial != "DJANIRA_POBRE" || scene.Objetos[0].MaoInicial != "direita" ||
            scene.Objetos[0].Morph86 != null || scene.Objetos[0].Eventos.Count != 0 || !String.IsNullOrEmpty(scene.Objetos[0].ApoioInicial) ||
            File.Exists(Path.Combine(dataRoot, "catalogos", "trial_morph86.json")) ||
            pilotSelected || captureEnabled || queueFile.Length != 0 || playing || countingDown ||
            automaticShortcutActive || automaticCapturePending || awaitingCaptureStart || awaitingCaptureEnd ||
            !Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending) ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("PapelPoly86 exige fonte exata, Story/INI tecnico carregado false, um proxy proprio/uma Djanira, semMorph/acoes/writer/ARM.");
    }

    private void RequireOpening99Contract(SceneData scene, string source)
    {
        if (IsAuthoredOpening99Source(scene, source)) { RequireAuthoredOpening99Contract(scene, source); return; }
        if (source == "teste_fila_99.json" && FlightSourceHash(Path.Combine(scenesDirectory, source)) == MapaEntrada99Diretor.ExactSourceSHA)
        { RequireMap99Contract(scene, source); return; }
        if (source != "teste_fila_99.json" || scene == null || scene.Id != "teste_fila_99" || scene.Episode != 0 || scene.Number != 0 ||
            scene.DurationMs != 9000 || scene.LocalId != "CASA_VO" || !scene.MarkedLocation || scene.Encenacao != "serie_v1" ||
            scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" || scene.Actions.Count != 0 || scene.Direcao.Count != 1 ||
            scene.Objetos.Count != 1 || scene.Poses.Count != 0 || scene.TransicoesPose.Count != 0 || scene.Veiculos.Count != 0 || scene.Portas.Count != 0 ||
            scene.Interpretacao.Count != 1 || scene.Segments.Count != 1 || !scene.Segments[0].Pause || scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 9000 ||
            scene.CameraPreset != "MEDIA" || scene.FinalPreset != "MEDIA" || scene.Cuts.Count != 0 ||
            FlightSourceHash(Path.Combine(scenesDirectory, source)) != ObservadorAbertura99.ExactSourceSHA ||
            File.Exists(Path.Combine(dataRoot, "catalogos", "trial_morph86.json")) ||
            pilotSelected || captureEnabled || queueFile.Length != 0 || automaticShortcutActive || automaticCapturePending || awaitingCaptureStart || awaitingCaptureEnd ||
            !Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending) ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("Leitura99 exige fonte exata/9s/Poor/CASA_VO/INI tecnico false; sem ARM/SAVE/desenho/abertura declarada.");
        AcoesSerieDiretor.ActorAction action = scene.Direcao[0]; ObjetosSerieDiretor.PropSpec prop = scene.Objetos[0];
        if (action.Ator != "djanira_pobre" || action.Tipo != "clipe" || action.Recurso != "leitura_documento_autor" ||
            action.Dicionario != "switch@michael@reads_script" || action.Clipe != "001404_01_mics3_16_reads_script_idle" ||
            action.InicioMs != 0 || action.DuracaoMs != 9000 || !action.Loop || action.CorpoInteiro || action.ManterPoseFinal || action.PreservarDuranteFala ||
            prop.Codigo != "PAPEL_HERANCA" || prop.Recurso != "documento_script" || prop.Modelo != "p_cs_script_s" ||
            prop.AtorInicial != "DJANIRA_POBRE" || prop.MaoInicial != "direita" || prop.AssentarNoChao || !prop.VisivelInicial || prop.Morph86 != null ||
            prop.Eventos.Count != 0 || prop.OffsetInicial != Vector3.Zero || prop.RotacaoInicial != Vector3.Zero)
            throw new InvalidOperationException("Leitura99 exige par autor exato/upper/PHRightHand zero; nenhuma transferencia/manipulacao de folha.");
        CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
    }

    private bool IsAuthoredOpening99Source(SceneData scene, string source)
    { return scene != null && scene.Id == "teste_fila_99" && source == "teste_fila_99.json" && FlightSourceHash(Path.Combine(scenesDirectory, source)) == PapelAberturaAutoral99Diretor.ExactSourceHash; }
    private void RequireAuthoredOpening99Contract(SceneData scene, string source)
    {
        if (!IsAuthoredOpening99Source(scene, source) || scene.Episode != 0 || scene.Number != 0 || scene.DurationMs != 9000 || scene.LocalId != "CASA_VO" ||
            !scene.MarkedLocation || scene.Encenacao != "serie_v1" || scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" ||
            scene.Actions.Count != 0 || scene.Direcao.Count != 0 || scene.Interpretacao.Count != 0 || scene.Objetos.Count != 1 ||
            scene.Poses.Count != 0 || scene.TransicoesPose.Count != 0 || scene.Veiculos.Count != 0 || scene.Portas.Count != 0 || scene.Cuts.Count != 0 ||
            scene.Segments.Count != 1 || !scene.Segments[0].Pause || scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 9000 ||
            scene.CameraPreset != "MEDIA" || scene.FinalPreset != "MEDIA" || !scene.AutomaticCamera ||
            pilotSelected || captureEnabled || queueFile.Length != 0 || automaticShortcutActive || automaticCapturePending || awaitingCaptureStart || awaitingCaptureEnd ||
            !Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending) || Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("Papel99 abertura autoral exige fonte9/Poor/CASA_VO/tecnicofalse/Story, nenhumARM/SAVE ou producao.");
        ObjetosSerieDiretor.PropSpec prop = scene.Objetos[0];
        if (prop.Codigo != "PAPEL_HERANCA" || prop.Recurso != "carta_simples" || prop.Modelo != "prop_amanda_note_01" || prop.AssentarNoChao ||
            !prop.VisivelInicial || prop.AtorInicial != "DJANIRA_POBRE" || prop.MaoInicial != "direita" || prop.OffsetInicial != Vector3.Zero ||
            prop.RotacaoInicial != Vector3.Zero || prop.Eventos.Count != 0 || prop.Morph86 != null || !String.IsNullOrEmpty(prop.ApoioInicial))
            throw new InvalidOperationException("Papel99 exige proxy real carta01/right attachment inicial unico; nenhuma troca de owner/track.");
        CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
    }

    private void RequireMap99Contract(SceneData scene, string source)
    {
        if (source != "teste_fila_99.json" || scene == null || scene.Id != "teste_fila_99" || scene.Episode != 0 || scene.Number != 0 ||
            scene.DurationMs != 9000 || scene.LocalId != "CASA_VO" || !scene.MarkedLocation || scene.Encenacao != "serie_v1" ||
            scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" || scene.Actions.Count != 0 || scene.Direcao.Count != 0 ||
            scene.Objetos.Count != 0 || scene.Poses.Count != 0 || scene.TransicoesPose.Count != 0 || scene.Veiculos.Count != 0 || scene.Portas.Count != 0 ||
            scene.Interpretacao.Count != 0 || scene.Segments.Count != 1 || !scene.Segments[0].Pause || scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 9000 ||
            scene.CameraPreset != "MEDIA" || scene.FinalPreset != "MEDIA" || scene.Cuts.Count != 0 ||
            FlightSourceHash(Path.Combine(scenesDirectory, source)) != MapaEntrada99Diretor.ExactSourceSHA ||
            File.Exists(Path.Combine(dataRoot, "catalogos", "trial_morph86.json")) ||
            pilotSelected || captureEnabled || queueFile.Length != 0 || automaticShortcutActive || automaticCapturePending || awaitingCaptureStart || awaitingCaptureEnd ||
            !Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending) ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("Mapa99 exige fonte exata9s/Poor/CASA_VO/INI false/Story; sem objetos manuais, ARM/SAVE/desenho ou abertura declarada.");
        CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
    }

    private void RequirePocket98Contract(SceneData scene, string source)
    {
        if(source!="teste_fila_98.json"||scene==null||scene.Id!="teste_fila_98"||scene.Episode!=0||scene.Number!=0||
           scene.DurationMs!=10000||scene.LocalId!="MANSAO_CHEFE"||!scene.MarkedLocation||scene.Encenacao!="serie_v1"||
           scene.Actors.Count!=1||scene.Actors[0].Id!="djanira_rica"||scene.Actions.Count!=0||scene.Direcao.Count!=2||
           scene.Objetos.Count!=2||scene.Poses.Count!=0||scene.Veiculos.Count!=0||scene.Portas.Count!=0||scene.Interpretacao.Count!=0||
           FlightSourceHash(Path.Combine(scenesDirectory,source))!=PapelBolso98Diretor.ExactSourceHash||
           pilotSelected||captureEnabled||queueFile.Length!=0||automaticShortcutActive||automaticCapturePending||awaitingCaptureStart||awaitingCaptureEnd||
           !Object.ReferenceEquals(directorCapture,null)&&(directorCapture.IsArmed||directorCapture.IsPending)||Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("Bolso98 requires exact10s/registeredRich/technicalINI false/Story; noARM/SAVE/production");
        CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot,WriteLog);
    }

    private static bool IsAuthoredPaperC5Source(SceneData scene)
    { return scene != null && scene.Objetos.Exists(delegate(ObjetosSerieDiretor.PropSpec item) { return item.Recurso == "papel_dobrado_autoral"; }); }
    private void RequireAuthoredPaperC5Contract(SceneData scene, string source)
    {
        if (!IsAuthoredPaperC5Source(scene) || source != "EP08_C05.json" || scene.Id != "EP08_C05" || scene.Episode != 8 || scene.Number != 5 ||
            scene.DurationMs != 12000 || scene.LocalId != "CASA_VO_INTERIOR" || scene.Actors.Count != 2 ||
            scene.Actors[0].Id != "dona_iraci" || scene.Actors[1].Id != "djanira_pobre" || scene.Objetos.Count != 3 || scene.Poses.Count != 2 ||
            scene.Actions.Count != 0 || scene.Direcao.Count != 2 || scene.Veiculos.Count != 0 || scene.Portas.Count != 0 ||
            FlightSourceHash(Path.Combine(scenesDirectory, source)) != PapelDobradoAutoralC5Diretor.ExactSourceHash ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("Papel autoral apenas fonte exata EP08_C05/12s/IraciDjanira/CASA_VO_INTERIOR, sem mudar roteiro/poses/handoff.");
    }
    private void RequireAuthoredPaperC5ActionReady()
    {
        if (!IsAuthoredPaperC5Source(activeScene)) return;
        RequireAuthoredPaperC5Contract(activeScene, selectedSceneFile);
        if (Object.ReferenceEquals(directorAuthoredPaperC5, null) || Object.ReferenceEquals(directorProps, null) || !directorAuthoredPaperC5.IsReady)
            throw new InvalidOperationException("Papel autoral exige renderer proprio realmente pronto antes ARM/acao.");
        directorProps.RequireAuthoredPaperC5ActionReady();
    }

    private void RequireOfficeCell84Contract(SceneData scene, string source)
    {
        string sourceHash = FlightSourceHash(Path.Combine(scenesDirectory, source));
        bool paperPreview = sourceHash == "2EE0F53B1A558B6DEDEB86D811C340E27E2DFF99FECFDC4E08C935823D7DA991";
        bool compactPreview = paperPreview || sourceHash == "56A3CD09AE6F418C7F00A60CA5823D44CA534769FDA0593CE528BD58F36A4105";
        if (source != "teste_fila_84.json" || scene.Id != "teste_fila_84" || scene.Episode != 0 || scene.Number != 0 ||
            scene.LocalId != "CENTRO_LEGION" || !scene.MarkedLocation || scene.Encenacao != "serie_v1" || scene.DurationMs != 10000 ||
            scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" || scene.Actions.Count != 0 || scene.Direcao.Count != 0 ||
            scene.Interpretacao.Count != 0 || scene.TransicoesPose.Count != 0 || scene.Veiculos.Count != 0 || scene.Portas.Count != 0 ||
            scene.Objetos.Count != (compactPreview ? 2 : 1) || scene.Objetos[0].Codigo != "BANCO_PRAIA" || scene.Objetos[0].Recurso != "banco_sono_c1_02" ||
            scene.Poses.Count != 1 || scene.Poses[0].Ator != "DJANIRA_POBRE" || scene.Poses[0].Objeto != "BANCO_PRAIA" ||
            scene.Poses[0].Recurso != "office_cell_sleep_f" || scene.Poses[0].Tipo != "deitada" || scene.Poses[0].RotacaoRelativa != 90f || scene.Poses[0].ObjetoAbracado != null ||
            scene.Segments.Count != 1 || !scene.Segments[0].Pause || scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 10000 ||
            captureEnabled || automaticShortcutActive || automaticCapturePending || Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS) ||
            (!compactPreview && sourceHash != "0ADC3989F05B647E2571451809CD65CAB2CDFC118F7F7DFA01CEC578E2AF65CA"))
            throw new InvalidOperationException("Sono84 exclusivo fonte congelada/Story/CENTRO/10s/bench02/Djanira: apenas previa tecnica, sem ARM/F7/SAVE.");
        if (compactPreview && (scene.Objetos[1].Codigo != "SACOLA_CALIBRACAO84" || scene.Objetos[1].Recurso != (paperPreview ? "sacola_papel_sono84_preview" : "bolsa_sono84_preview") ||
            scene.Objetos[1].Modelo != (paperPreview ? "prop_paper_bag_small" : "prop_ld_handbag") || scene.Objetos[1].AtorInicial != "DJANIRA_POBRE" || scene.Objetos[1].MaoInicial != "direita" ||
            scene.Objetos[1].Eventos.Count != 0 || scene.Objetos[1].AssentarNoChao || !scene.Objetos[1].VisivelInicial))
            throw new InvalidOperationException("Sono84 compact preview requires sole own handbag/normal factory/zero events; hug remains unproved.");
    }

    private void RequireWritingC6Contract(SceneData scene, string source)
    {
        if (source != "EP10_C06.json" || scene.Id != "EP10_C06" || scene.Episode != 10 || scene.Number != 6 ||
            scene.LocalId != "VIADUTO_LAPUERTA" || !scene.MarkedLocation || scene.Encenacao != "serie_v1" || scene.DurationMs != 14000 ||
            scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" || scene.Actions.Count != 0 || scene.Direcao.Count != 0 ||
            scene.Interpretacao.Count != 0 || scene.Objetos.Count != 0 || scene.Poses.Count != 0 || scene.TransicoesPose.Count != 0 ||
            scene.Veiculos.Count != 0 || scene.Portas.Count != 0 || scene.Cuts.Count != 0 || !scene.AutomaticCamera ||
            scene.CameraObjeto != null || scene.CameraActor != "djanira_pobre" || scene.Segments.Count != 1 ||
            !scene.Segments[0].Narration || scene.Segments[0].Pause || scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 14000 ||
            FlightSourceHash(Path.Combine(scenesDirectory, source)) != "EA23034CC7175E68E11A0DAFAF8FA60616E6FE915764ADC8D72E2CB840E073D1" || Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("C6: fonte14/Poor/VIADUTO/Story exata, tres tracks WEED proprias; sem canais concorrentes.");
    }
    private void RequireWritingC6Prepared()
    {
        RequireWritingC6Contract(activeScene, selectedSceneFile);
        if (Object.ReferenceEquals(directorWritingC6, null) || !directorWritingC6.IsReady)
            throw new InvalidOperationException("C6: aguarde recursos proprios; apos acao F10/F6.");
        directorWritingC6.RequirePreparedWeedPose();
        directorCamera.RequireWritingC6RecordingReady(directorWritingC6, actorsById["djanira_pobre"]);
    }

    private static void RequireWriting85Contract(SceneData scene, string source)
    {
        if (source != "teste_fila_85.json" || scene.Id != "teste_fila_85" || scene.Episode != 0 || scene.Number != 0 ||
            scene.LocalId != "VIADUTO_LAPUERTA" || !scene.MarkedLocation || scene.Encenacao != "serie_v1" || scene.DurationMs != 14000 ||
            scene.Actors.Count != 1 || scene.Actors[0].Id != "djanira_pobre" || scene.Actions.Count != 0 || scene.Direcao.Count != 0 ||
            scene.Interpretacao.Count != 0 || scene.Objetos.Count != 0 || scene.Poses.Count != 0 || scene.TransicoesPose.Count != 0 ||
            scene.Veiculos.Count != 0 || scene.Portas.Count != 0 || scene.Cuts.Count != 0 || !scene.AutomaticCamera ||
            scene.CameraObjeto != null || scene.CameraActor != "djanira_pobre" || scene.Segments.Count != 1 ||
            !scene.Segments[0].Pause || scene.Segments[0].StartMs != 0 || scene.Segments[0].EndMs != 14000 ||
            Function.Call<bool>(Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw new InvalidOperationException("Escrita85 apenas tecnica: Djanira propria,14s pausa,marcaVIADUTO,semprops/acoes/canais concorrentes; nunca producao.");
    }

    private void RequireWeed85TechnicalState()
    {
        RequireWriting85Contract(activeScene, selectedSceneFile);
        if (pilotSelected || captureEnabled || queueFile.Length != 0 ||
            FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)) != "961403AAF68F4B2A4F0000B1A100869E7AED0826968B419B2717F6A3092AFFF3" ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)) ||
            automaticShortcutActive || automaticCapturePending || awaitingCaptureStart || awaitingCaptureEnd)
            throw new InvalidOperationException("WEED85 exige hash tecnico85 exato, INI carregado PilotoFalse/CaptureFalse/fila vazia e nenhuma captura propria ativa; nunca ARM/SAVE.");
    }

    private void ArmCurrentCapture()
    {
        if (!Object.ReferenceEquals(directorGorjetaTrial, null) && directorGorjetaTrial.IsChair03SupportPreview)
            throw new InvalidOperationException("Cadeira03/87 somente F6 apoio PREP; F7/ARM/SAVE/rate1 vetados ate prova Root.");
        RequirePaperPoly86NoAction();
        RequireAuthoredPaperC5ActionReady();
        if (activeScene.Id == "teste_fila_84") throw new InvalidOperationException("Sono84 corpo sem sacola: ARM/SAVE vetados.");
        RequireC4SofaTestNoCapture();
        if (IsRoomC4Scene) RequireC4Viewport();
        if (activeScene.Id == "teste_fila_85") throw new InvalidOperationException("Escrita85 ensaio tecnico sem writer; ARM/SAVE vetados.");
        if (activeScene.Id == "teste_fila_98") throw new InvalidOperationException("Bolso98 tecnico: ARM/SAVE vetados.");
        if (activeScene.Id == "teste_fila_99") throw new InvalidOperationException("Leitura99 tecnica readonly: ARM/SAVE vetados.");
        if (activeScene.Id == "EP10_C06") RequireWritingC6Prepared();
        if (activeScene.Id == "EP07_C02") directorCamera.RequireGalpaoWitnessRecordingReady();
        if (activeScene.Id == "EP13_C03") LogPurchaseSource("ANTES_ARM");
        if (activeScene.Id == "teste_fila_86")
            foreach (ObjetosSerieDiretor.PropSpec prop in activeScene.Objetos)
                if (prop.Morph86 != null) throw new InvalidOperationException("Morfologia86 apenas F6; ARM/F7/SAVE vetados.");
        if (activeScene.Id == "EP13_C02")
        {
            RequireDismissalSceneContract(activeScene, selectedSceneFile);
            if (String.IsNullOrEmpty(mountedDismissalSourceHash) ||
                mountedDismissalSourceHash != FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)))
                throw new InvalidOperationException("Fonte da parte EP13 mudou; remonte antes ARM.");
            directorCapture.Arm(activeScene.Id, activeScene.Episode, activeScene.Number, DismissalPartName(dismissalSubtake),
                Path.Combine(scenesDirectory, selectedSceneFile), mountedDismissalSourceHash,
                typeof(DiretorGTA).Assembly.GetName().Version.ToString());
            return;
        }
        if (activeScene.Id == "EP07_C03")
        {
            RequireFlightSource(); directorSeasonFlight.RequireReadyForCountdown();
            directorSeasonFlight.VerifyAppearanceEvidenceBeforeArm();
            if (directorSeasonFlight.IsEditorialPart)
            {
                directorCapture.Arm(activeScene.Id, activeScene.Episode, activeScene.Number,
                    CenaVooTemporadaDiretor.PartName(directorSeasonFlight.Part),
                    Path.Combine(scenesDirectory, selectedSceneFile), mountedFlightSourceHash,
                    typeof(DiretorGTA).Assembly.GetName().Version.ToString());
                return;
            }
        }
        directorCapture.Arm(activeScene.Id, activeScene.Episode, activeScene.Number);
    }
    private void BeginAction()
    {
        if (!Object.ReferenceEquals(directorGorjetaTrial, null) && directorGorjetaTrial.IsChair03SupportPreview)
            throw new InvalidOperationException("Cadeira03/87 somente F6 apoio PREP; F7/ARM/SAVE/rate1 vetados ate prova Root.");
        RequirePaperPoly86NoAction();
        if (IsAuthoredOpening99Source(activeScene, selectedSceneFile))
        {
            RequireAuthoredOpening99Contract(activeScene, selectedSceneFile);
            if (Object.ReferenceEquals(directorAuthoredOpening99, null) || !directorAuthoredOpening99.IsObservationPrepared)
            { ShowMessage("Diretor: aguarde fase0 da observacao99; F7 nao inicia narrativa/captura."); return; }
            if (playing || countingDown || takeFinished || loadingTimer != null || awaitingEpisodeReset || awaitingPoseReset)
                throw new InvalidOperationException("Observacao99 exige preparo proprio parado, sem count/acao/reset.");
            CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
            directorAuthoredOpening99.StartObservation();
            return; // Exactly one explicit F7; no countdown, SRT, capture or narrative action.
        }
        if (activeScene.Id == "teste_fila_84") throw new InvalidOperationException("Sono84 apenas F6; F7/acao/ARM/SAVE vetados, abraco nao provado.");
        if (loadingTimer != null || awaitingEpisodeReset || awaitingPoseReset)
        {
            ShowMessage("Diretor: preparando posições e assentos. Espere a câmera ficar pronta antes de gravar; " + clearKey + " cancela.");
            return;
        }
        if (!sceneReady || !directorCamera.IsInitialized)
        {
            ShowMessage("Diretor: aperte " + mountKey + " e espere os personagens ficarem prontos.");
            return;
        }
        if (awaitingCaptureEnd) return;
        if (playing || countingDown)
        {
            ShowMessage("Diretor: teste em andamento. " + clearKey + " cancela.");
            return;
        }
        if (!Object.ReferenceEquals(directorCapture, null) && directorCapture.IsPending)
            throw new InvalidOperationException("Pare a gravação anterior com Win+Alt+R e espere a organização terminar.");
        if (activeScene.DurationMs <= 0)
            throw new InvalidOperationException("Esta cena não contém roteiro nem ações com duração.");
        if (IsAuthoredPaperC5Source(activeScene))
        {
            if (Object.ReferenceEquals(directorAuthoredPaperC5, null) || !directorAuthoredPaperC5.IsReady)
            { ShowMessage("Diretor: aguarde papel e camera proprios ficarem prontos."); return; }
            RequireAuthoredPaperC5ActionReady();
        }
        if (activeScene.Episode > 0 && captureEnabled && !Object.ReferenceEquals(directorCapture, null) &&
            !directorCapture.IsArmed && !CanRecordAutomatically())
        {
            ArmCurrentCapture();
            takeFinished = false;
            WriteLog("CAPTURA_PRIMEIRA_ACAO_PREPARADA", "Cena=" + activeScene.Id + "; aguardando Win+Alt+R manual e nova F7; nenhuma ação iniciada.");
            ShowMessage("Gravação preparada. Win+Alt+R começa; confira o indicador gravando e aperte " + actionKey + " para rodar. Ao terminar, Win+Alt+R para e o Diretor salva o MP4.");
            return;
        }
        bool armedPreparationB = activeScene.Id == "EP07_C03" &&
            !Object.ReferenceEquals(directorSeasonFlight, null) &&
            directorSeasonFlight.Part == CenaVooTemporadaDiretor.BoardingPart.EmbarqueDjaniraDecolagem &&
            !Object.ReferenceEquals(directorCapture, null) && directorCapture.IsArmed;
        bool unstartedPreparationB = activeScene.Id == "EP07_C03" &&
            !Object.ReferenceEquals(directorSeasonFlight, null) && directorSeasonFlight.CanPreserveUnstartedPreparation &&
            (Object.ReferenceEquals(directorCapture, null) ? !captureEnabled : !directorCapture.IsArmed);
        // B ja preparada e nunca iniciada: conferir antes ARM, sem desmontar os assentos proprios.
        // Primeira captura manual ja possui writer: nunca refazer SetIntoVehicle entre ARM e CONTAGEM.
        if (!Object.ReferenceEquals(directorAuthoredOpening99, null))
        { RequireAuthoredOpening99Contract(activeScene, selectedSceneFile); directorAuthoredOpening99.RequirePrepared(); }
        else if (armedPreparationB || unstartedPreparationB)
        {
            RequireFlightSource();
            if (unstartedPreparationB) directorSeasonFlight.RequireUnstartedPreparation();
            else directorSeasonFlight.RequireReadyForCountdown();
        }
        else ResetTakeState(activeScene.AutomaticCamera || activeScene.Cuts.Count > 0);
        if (awaitingPoseReset || awaitingEpisodeReset)
        {
            actionAfterPoseReset = true;
            ShowMessage("Diretor: restaurando as poses; a contagem começa quando ficarem prontas.");
            return;
        }
        BeginPreparedAction();
    }

    private void BeginPreparedAction()
    {
        if (IsAuthoredOpening99Source(activeScene, selectedSceneFile) || !Object.ReferenceEquals(directorAuthoredOpening99, null))
            throw new InvalidOperationException("Observacao99 nao entra BeginPreparedAction/narrativa/SAVE, independente do INI.");
        if (!Object.ReferenceEquals(directorGorjetaTrial, null) && directorGorjetaTrial.IsChair03SupportPreview)
            throw new InvalidOperationException("Cadeira03/87 somente F6 apoio PREP; F7/ARM/SAVE/rate1 vetados ate prova Root.");
        RequirePaperPoly86NoAction();
        if (activeScene.Id == "teste_fila_99")
        {
            RequireOpening99Contract(activeScene, selectedSceneFile);
            if (!Object.ReferenceEquals(directorAuthoredOpening99, null)) directorAuthoredOpening99.RequirePrepared();
            else if (!Object.ReferenceEquals(directorMapOpening99, null)) directorMapOpening99.BeginWindow();
            else
            {
                if (Object.ReferenceEquals(directorOpening99, null)) throw new InvalidOperationException("Leitura99 own observer ausente.");
                directorOpening99.BeginWindow();
            }
        }
        if (activeScene.Id == "teste_fila_98")
        {
            RequirePocket98Contract(activeScene, selectedSceneFile);
            if (Object.ReferenceEquals(directorPocket98, null)) throw new InvalidOperationException("Bolso98 renderer missing");
            if (!directorPocket98.IsReady) { actionAfterPoseReset = true; return; }
            directorPocket98.RequireActionReady();
        }
        if (IsAuthoredPaperC5Source(activeScene) && !directorAuthoredPaperC5.IsReady)
        { actionAfterPoseReset = true; return; }
        RequireAuthoredPaperC5ActionReady();
        if (activeScene.Id == "teste_fila_84") throw new InvalidOperationException("Sono84 apenas previa; acao/ARM/SAVE vetados.");
        RequireC4SofaTestNoCapture();
        if (IsRoomC4Scene) RequireC4Viewport();
        if (activeScene.Id == "teste_fila_85")
        {
            RequireWriting85Contract(activeScene, selectedSceneFile);
            if (!Object.ReferenceEquals(directorWriting85, null) && directorWriting85.IsWeedFullTrack) RequireWeed85TechnicalState();
            if (directorWriting85 == null || (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)) || automaticShortcutActive || automaticCapturePending)
                throw new InvalidOperationException("Escrita85 exige ensaio sem captura propria armada/pendente.");
        }
        if (activeScene.Id == "EP10_C06") RequireWritingC6Prepared();
        if (activeScene.Id == "EP07_C02") directorCamera.RequireGalpaoWitnessRecordingReady();
        if (activeScene.Id == "EP13_C03") LogPurchaseSource("ANTES_PREPARAR_ACAO");
        if (activeScene.Id == "EP07_C03")
        {
            RequireFlightSource(); directorSeasonFlight.RequireReadyForCountdown();
            WriteLog("SUBTOMADA_GRAVACAO", "Id=EP07_C03; parte=" +
                CenaVooTemporadaDiretor.PartName(directorSeasonFlight.Part) +
                "; arquivo=" + selectedSceneFile + "; SHA256=" + mountedFlightSourceHash +
                "; Integral=" + !directorSeasonFlight.IsEditorialPart + "; narracao10s unica na montagem45s");
        }
        if (!Object.ReferenceEquals(directorTableWipe, null)) directorTableWipe.RequireReadyForCountdown();
        string srtPath = WriteSrt();
        bool useAutomaticRecording = activeScene.Id != "teste_fila_85" && CanRecordAutomatically() && !directorCapture.IsArmed;
        if (useAutomaticRecording) ArmCurrentCapture();
        if (activeScene.Id == "EP01_C05")
            WriteLog("SUBTOMADA_GRAVACAO", "Id=EP01_C05; parte=" + (secretGrandmaSubtake == 0 ? "hospital" : "cassino") + "; arquivo=" + selectedSceneFile + "; SRT=" + srtPath + "; voz original8s única na edição final");
        if (useAutomaticRecording)
        {
            try
            {
                AtalhoGravacaoDiretor.Toggle();
                automaticCapturePending = true;
                awaitingCaptureStart = true;
                captureStartTimer = Stopwatch.StartNew();
                captureStartNextPoll = 0;
                preparedCaptureSrt = srtPath;
                statusText = ""; statusUntil = 0;
                WriteLog("ATALHO_GRAVACAO_INICIO", "Atalho enviado uma vez; aguardando MP4 novo antes da contagem.");
                return;
            }
            catch
            {
                automaticRecordingDisabled = true;
                directorCapture.Cancel("atalho automático recusado; usar gravação manual");
                throw;
            }
        }
        StartPreparedCountdown(srtPath);
    }

    private void AdvanceCaptureStart()
    {
        if (!awaitingCaptureStart) return;
        if (captureStartTimer.ElapsedMilliseconds < captureStartNextPoll) return;
        captureStartNextPoll = captureStartTimer.ElapsedMilliseconds + 200;
        string started = directorCapture.GetStartedCapture();
        if (!String.IsNullOrEmpty(started))
        {
            awaitingCaptureStart = false;
            automaticShortcutActive = true;
            WriteLog("CAPTURA_INICIO_CONFIRMADO", "MP4=" + started + "; espera_ms=" + captureStartTimer.ElapsedMilliseconds);
            string srt = preparedCaptureSrt;
            captureStartTimer = null; preparedCaptureSrt = null;
            StartPreparedCountdown(srt);
            return;
        }
        if (captureStartTimer.ElapsedMilliseconds >= 30000)
        {
            // Sem evidência de captura ativa, um segundo toggle poderia iniciá-la.
            automaticRecordingDisabled = true;
            automaticCapturePending = false;
            throw new InvalidOperationException("A Game Bar não criou um MP4 novo em30s; ação preservada e atalho não repetido. Consulte logs/capturas.");
        }
    }

    private void StartPreparedCountdown(string srtPath)
    {
        if (activeScene.Id == "EP13_C03") LogPurchaseSource("ANTES_CONTAGEM");
        if (!Object.ReferenceEquals(directorTableWipe, null)) directorTableWipe.RequireReadyForCountdown();
        if (activeScene.Id == "EP06_C02")
        {
            string path = Path.Combine(dataRoot, "catalogos", "cameras_marcadas.xml");
            XmlDocument poses = new XmlDocument(); poses.XmlResolver = null;
            if (File.Exists(path))
            {
                RequirePlainPath(path);
                using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1048576 })) poses.Load(reader);
            }
            if (Object.ReferenceEquals(poses.SelectSingleNode("/CamerasMarcadas/Camera[@nome='EP06_C02_BASE']"), null)) directorCamera.SavePose(path, "EP06_C02_BASE");
            directorCamera.RestorePose(path, "EP06_C02_BASE");
            WriteLog("CAMERA_CONTINUIDADE_SALVA", "EP06_C02_BASE; usar exatamente na chegada EP12_C01.");
        }
        if (!Object.ReferenceEquals(directorCapture, null)) directorCapture.Begin(activeScene.Id, srtPath, activeScene.DurationMs + CountdownMs);
        ClearDirectorNotices();
        // Gravar SRT antes de iniciar o relógio. A tomada inclui a contagem inicial.
        countdownStart = unchecked((uint)Game.GameTime);
        actionStartTime = countdownStart + CountdownMs;
        segmentIndex = -1;
        subtitleText = "";
        takeFinished = false;
        countingDown = true;
        WriteLog("CONTAGEM_INICIADA", "Cena=" + activeScene.Id + "; contagem_ms=" + CountdownMs +
            "; duração_cena_ms=" + activeScene.DurationMs + "; SRT=" + srtPath);
    }

    private void ClearDirectorNotices()
    {
        int cleared = 0;
        foreach (GTA.UI.FeedPost notice in ownNotifications)
        {
            try { notice.Delete(); cleared++; }
            catch (Exception ex) { WriteLog("ERRO_AVISO_LIMPEZA", ex.ToString()); }
        }
        ownNotifications.Clear();
        if (ownHelpText)
        {
            try { GTA.UI.Screen.ClearHelpText(); }
            catch (Exception ex) { WriteLog("ERRO_AJUDA_LIMPEZA", ex.ToString()); }
            ownHelpText = false;
        }
        statusText = ""; statusUntil = 0;
        WriteLog("AVISOS_TOMADA_LIMPOS", "Notificações próprias=" + cleared + "; cena=" + activeScene.Id);
    }

    private void AdvanceCountdown()
    {
        if (countingDown && activeScene.Id == "EP07_C03")
        { RequireFlightSource(); directorSeasonFlight.RequireReadyForCountdown(); }
        if (!countingDown || unchecked((uint)Game.GameTime - countdownStart) < CountdownMs)
            return;
        RequireActors();
        countingDown = false;
        if (activeScene.Id == "EP13_C03") LogPurchaseSource("INICIO_ACAO");
        foreach (KeyValuePair<string, Ped> actor in actorsById)
            actor.Value.IsPositionFrozen = activeScene.Poses.Exists(delegate(PosesSerieDiretor.PoseSpec pose) { return pose.Ator == actor.Key.ToUpperInvariant(); });
        if (!Object.ReferenceEquals(directorVehicles, null)) directorVehicles.Start();
        if (!Object.ReferenceEquals(directorThreeSeven, null)) directorThreeSeven.Start();
        if (!Object.ReferenceEquals(directorSeasonFlight, null)) directorSeasonFlight.Start();
        if (!Object.ReferenceEquals(directorPlanePush, null)) directorPlanePush.Start();
        if (!Object.ReferenceEquals(directorWardrobe, null)) directorWardrobe.Start();
        if (!Object.ReferenceEquals(directorEntry, null)) directorEntry.Start();
        if (!Object.ReferenceEquals(directorTableWipe, null)) directorTableWipe.Start();
        if (!Object.ReferenceEquals(directorCartaTrial, null)) directorCartaTrial.Start();
        if (!Object.ReferenceEquals(directorGorjetaTrial, null))
        {
            directorGorjetaTrial.Start();
            // O ensaio integral usa o Tick efetivo de Start, sem descontar atraso da contagem.
            actionStartTime = unchecked((uint)Game.GameTime);
        }
        if (!Object.ReferenceEquals(directorSeasonWater, null)) directorSeasonWater.Start();
        if (!Object.ReferenceEquals(directorWriting85, null)) { directorWriting85.Start(); actionStartTime = unchecked((uint)Game.GameTime); }
        if (activeScene.Id == "EP10_C06") { RequireWritingC6Prepared(); directorWritingC6.Start(); actionStartTime = unchecked((uint)Game.GameTime); }
        if (!Object.ReferenceEquals(directorAuthoredOpening99, null))
        { RequireAuthoredOpening99Contract(activeScene, selectedSceneFile); directorAuthoredOpening99.StartAction(); actionStartTime = unchecked((uint)Game.GameTime); }
        if (!Object.ReferenceEquals(directorMapOpening99, null))
        { RequireMap99Contract(activeScene, selectedSceneFile); directorMapOpening99.Start(); actionStartTime = unchecked((uint)Game.GameTime); }
        playing = true;
        WriteLog("ACAO_INICIADA", "Cena=" + activeScene.Id + "; contagem concluída; duração_ms=" + activeScene.DurationMs);
    }

    private void RequireActors()
    {
        foreach (Ped ped in createdPeds)
            if (Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead)
                throw new InvalidOperationException("Um personagem do teste foi removido. Monte novamente com " + mountKey + ".");
    }

    private void AdvanceAction()
    {
        if (!playing)
            return;
        RequireActors();
        int elapsed = unchecked((int)(unchecked((uint)Game.GameTime) - actionStartTime));
        if (!Object.ReferenceEquals(directorWriting85, null)) directorWriting85.Update(elapsed);
        if (!Object.ReferenceEquals(directorWritingC6, null)) directorWritingC6.Update(elapsed);
        if (activeScene.Id == "EP07_C02")
        {
            AdvanceScheduledCuts(elapsed);
            if (directorCamera.IsGalpaoWitnessPosePending) return;
        }
        if (elapsed >= activeScene.DurationMs)
        {
            if (!Object.ReferenceEquals(directorAuthoredOpening99, null)) directorAuthoredOpening99.Finish(elapsed);
            if (!Object.ReferenceEquals(directorOpening99, null)) directorOpening99.FinishWindow(elapsed);
            if (!Object.ReferenceEquals(directorMapOpening99, null)) directorMapOpening99.FinishWindow(elapsed);
            LogBusStage("ENTRY", elapsed);
        AdvanceTimedActions(elapsed);
        LogBusStage("TIMED_ACTIONS", elapsed);
            if (!Object.ReferenceEquals(directorPilot, null)) directorPilot.Update(elapsed);
            if (!Object.ReferenceEquals(directorRehearsal, null)) directorRehearsal.Update(elapsed);
            if (!Object.ReferenceEquals(directorRehearsal, null) && !directorRehearsal.IsFinished)
                throw new InvalidOperationException("O ensaio físico não terminou; a tomada não será marcada como concluída.");
            if (!Object.ReferenceEquals(directorEpisodeOne, null)) directorEpisodeOne.Update(elapsed);
            if (!Object.ReferenceEquals(directorEpisodeOne, null) && directorEpisodeOne.UsesOwnCamera && !directorEpisodeOne.IsSuccess)
                throw new InvalidOperationException("A queda, saída da água, subida da rampa e fuga não terminaram fisicamente. A tomada não será marcada como concluída.");
            UpdateSeasonControllers(elapsed);
        LogBusStage("SEASON_CONTROLLERS", elapsed);
            if (!Object.ReferenceEquals(directorPoses, null)) directorPoses.Update(activeScene.DurationMs);
            if (!Object.ReferenceEquals(directorStaging, null)) directorStaging.Update(elapsed);
        LogBusStage("STAGING", elapsed);
        if (!Object.ReferenceEquals(directorTableWipe, null)) directorTableWipe.Update(elapsed);
            if (!Object.ReferenceEquals(directorStaging, null) && !directorStaging.IsFinished)
                throw new InvalidOperationException("A atuação corporal não terminou; a tomada não será marcada como concluída.");
            if (!Object.ReferenceEquals(directorPocket98, null)) directorPocket98.Update(activeScene.DurationMs);
            if (!Object.ReferenceEquals(directorProps, null) && !FlightHandlesObjects) directorProps.Update(activeScene.DurationMs);
            if (!Object.ReferenceEquals(directorPocket98, null) && !directorPocket98.IsFinished) throw new InvalidOperationException("Bolso98 actual acquisition/placement incomplete; no technical completion");
            if (!Object.ReferenceEquals(directorProps, null) && !directorProps.IsFinished)
                throw new InvalidOperationException("A sequência física dos objetos não terminou; a tomada não será marcada como concluída.");
            RequireSeasonControllersFinished();
            if (!Object.ReferenceEquals(directorWriting85, null) && !directorWriting85.IsFinished) throw new InvalidOperationException("Escrita85 janela tecnica incompleta; nunca salvar producao.");
            if (activeScene.Id == "EP10_C06" && (Object.ReferenceEquals(directorWritingC6, null) || !directorWritingC6.IsFinished))
                throw new InvalidOperationException("C6: tres tracks integrais/pausa medida incompletas; nao concluir/salvar.");
            // Validar os canais fisicos antes de StopSegment limpar o gesto da fala.
            StopSegment();
            if (!Object.ReferenceEquals(directorCartaTrial, null))
                WriteLog("CARTA_TRIAL_JANELA_CONCLUIDA", "Scene=" + activeScene.Id + "; FullTracksObserved=" +
                    directorCartaTrial.TracksCompletos + "; OpeningVisualConfirmed=false; PocketRetrievalConfirmed=false; ProductionApproval=false");
            if (!Object.ReferenceEquals(directorInterpretation, null)) directorInterpretation.Update(activeScene.DurationMs - 1, new string[0]);
            if (!HasOwnEpisodeCamera && (activeScene.AutomaticCamera || activeScene.Cuts.Count > 0))
            {
                string finalCameraActor = activeScene.Id == "EP07_C02" && activeScene.FinalPreset == "PERTO" && activeScene.Cuts.Count > 0 &&
                    activeScene.Cuts[activeScene.Cuts.Count - 1].Personagem == "djanira_pobre" ? "djanira_pobre" : activeScene.CameraActor;
                SetSceneShot(activeScene.FinalPreset, finalCameraActor, false);
                WriteLog("CAMERA_FINAL", "Plano=" + activeScene.FinalPreset + "; alvo=" + finalCameraActor);
            }
            directorCamera.FixCurrentPose();
            playing = false;
            takeFinished = true;
            segmentIndex = -1;
            subtitleText = "";
            foreach (Ped ped in createdPeds)
                ped.IsPositionFrozen = true;
            if (activeScene.Id == "EP13_C03") LogPurchaseSource("FIM_ACAO");
            WriteLog("ACAO_CONCLUIDA", "Cena=" + activeScene.Id + "; tempo_ms=" + elapsed);
            if (automaticShortcutActive)
            {
                awaitingCaptureEnd = true;
                captureEndTimer = Stopwatch.StartNew();
                statusText = ""; statusUntil = 0;
                WriteLog("CAPTURA_QUADRO_FINAL_INICIADO", "Cena=" + activeScene.Id + "; espera_ms=" + CaptureFinalHoldMs +
                    "; duração_cena_ms=" + activeScene.DurationMs + "; captura automática; roteiro e SRT preservados.");
            }
            else CompleteTakeCapture();
            return;
        }
        int nextIndex = -1;
        for (int i = 0; i < activeScene.Segments.Count; i++)
            if (elapsed >= activeScene.Segments[i].StartMs && elapsed < activeScene.Segments[i].EndMs)
            {
                nextIndex = i;
                break;
            }
        bool changedSegment = nextIndex != segmentIndex;
        List<string> releasedLooks = Object.ReferenceEquals(directorInterpretation, null) ? null : directorInterpretation.ReleaseEndedLooks(elapsed);
        if (changedSegment)
        {
            StopSegment();
            segmentIndex = nextIndex;
            subtitleText = "";
        }
        LogBusStage("ENTRY", elapsed);
        AdvanceTimedActions(elapsed);
        LogBusStage("TIMED_ACTIONS", elapsed);
        if (activeScene.Id != "EP07_C02") AdvanceScheduledCuts(elapsed);
        LogBusStage("CAMERA_CUTS", elapsed);
        if (changedSegment && segmentIndex >= 0)
            StartSegment(elapsed);
        if (!Object.ReferenceEquals(releasedLooks, null) && segmentIndex >= 0 && !activeScene.Segments[segmentIndex].Narration)
        {
            SegmentData speech = activeScene.Segments[segmentIndex];
            foreach (string actor in releasedLooks)
                if (actor != speech.ActorId) actorsById[actor].Task.LookAt(actorsById[speech.ActorId], System.Math.Max(1, speech.EndMs - elapsed));
        }
        if (!Object.ReferenceEquals(directorPilot, null)) directorPilot.Update(elapsed);
        if (!Object.ReferenceEquals(directorRehearsal, null)) directorRehearsal.Update(elapsed);
        if (!Object.ReferenceEquals(directorEpisodeOne, null)) directorEpisodeOne.Update(elapsed);
        UpdateSeasonControllers(elapsed);
        LogBusStage("SEASON_CONTROLLERS", elapsed);
        if (!Object.ReferenceEquals(directorPoses, null)) directorPoses.Update(elapsed);
        LogBusStage("POSES", elapsed);
        if (activeScene.Id == "EP01_C06" && elapsed >= 3000 && elapsed >= nextTerminalCameraTime && !actorsById["dinjer"].IsInVehicle())
        {
            SetSceneShot("ATRAS", "dinjer", false);
            nextTerminalCameraTime = elapsed + 200;
        }
        if ((activeScene.Id == "teste_fila_90" || activeScene.SaidaAgua) && elapsed >= nextWaterCameraTime)
        {
            SetSceneShot(activeScene.CameraPreset, activeScene.CameraActor, false);
            nextWaterCameraTime = elapsed + 150;
        }
        if (!Object.ReferenceEquals(directorStaging, null)) directorStaging.Update(elapsed);
        LogBusStage("STAGING", elapsed);
        if (!Object.ReferenceEquals(directorTableWipe, null)) directorTableWipe.Update(elapsed);
        // C3 segue a Djanira propria somente durante a caminhada; demais cenas ficam identicas.
        if (activeScene.Id == "EP11_C03" && elapsed >= 6000)
            directorCamera.ApplyLuxuryWalkFollow(activeScene.Id, activeScene.LocalId, activeScene.Actors[cameraTargetIndex].Id,
                cameraPresets[cameraPresetIndex], actorsById[activeScene.Actors[cameraTargetIndex].Id],
                activeScene.CameraSide, activeScene.CameraHeight, activeScene.CameraNear);
        RestoreSpeechAfterBodyChanges(elapsed);
        LogBusStage("SPEECH_RESTORE", elapsed);
        if (!Object.ReferenceEquals(directorPocket98, null)) directorPocket98.Update(elapsed);
        if (!Object.ReferenceEquals(directorProps, null) && !FlightHandlesObjects) directorProps.Update(elapsed);
        LogBusStage("PROPS", elapsed);
        if (!Object.ReferenceEquals(directorAuthoredOpening99, null)) directorAuthoredOpening99.Update(elapsed);
        if (!Object.ReferenceEquals(directorOpening99, null)) directorOpening99.Observe(elapsed);
        if (!Object.ReferenceEquals(directorMapOpening99, null)) directorMapOpening99.Observe(elapsed);
        if (!Object.ReferenceEquals(directorInterpretation, null))
        {
            bool hasSpeaker = segmentIndex >= 0 && !activeScene.Segments[segmentIndex].Narration;
            directorInterpretation.Update(elapsed, hasSpeaker ? new string[] { activeScene.Segments[segmentIndex].ActorId } : new string[0]);
        }
        if (segmentIndex < 0)
            return;
        SegmentData segment = activeScene.Segments[segmentIndex];
        if (!segment.Narration && elapsed >= nextMouthTime)
        {
            Ped speaker = actorsById[segment.ActorId];
            Function.Call(Hash.PLAY_FACIAL_ANIM, speaker.Handle, MouthClip, MouthDictionary);
            nextMouthTime = elapsed + mouthInterval;
        }
    }

    private bool FlightHandlesObjects
    { get { return (!Object.ReferenceEquals(directorSeasonFlight, null) && directorSeasonFlight.HandlesObjectEvents) ||
        (!Object.ReferenceEquals(directorThreeSeven, null) && !directorThreeSeven.HasLeftBus); } }

    private void LogBusStage(string stage, int elapsed)
    {
        if (!Object.ReferenceEquals(directorThreeSeven, null) && !Object.ReferenceEquals(directorThreeSeven.OwnVehicles, null))
            directorThreeSeven.OwnVehicles.LogNormalExitCheckpoint(stage, elapsed);
        if (!Object.ReferenceEquals(directorVehicles, null)) directorVehicles.LogNormalExitCheckpoint(stage, elapsed);
    }

    private void UpdateSeasonControllers(int elapsed)
    {
        if (!Object.ReferenceEquals(directorVehicles, null)) directorVehicles.Update(elapsed);
        if (!Object.ReferenceEquals(directorThreeSeven, null)) directorThreeSeven.Update(elapsed);
        if (!Object.ReferenceEquals(directorSeasonFlight, null)) directorSeasonFlight.Update(elapsed);
        if (!Object.ReferenceEquals(directorPlanePush, null)) directorPlanePush.Update(elapsed);
        if (!Object.ReferenceEquals(directorWardrobe, null)) directorWardrobe.Update(elapsed);
        if (!Object.ReferenceEquals(directorEntry, null)) directorEntry.Update(elapsed);
        if (!Object.ReferenceEquals(directorCartaTrial, null)) directorCartaTrial.Update(elapsed);
        if (!Object.ReferenceEquals(directorGorjetaTrial, null)) directorGorjetaTrial.Update(elapsed);
        if (!Object.ReferenceEquals(directorDoors, null)) directorDoors.Update(elapsed);
        if (!Object.ReferenceEquals(directorSeasonWater, null)) directorSeasonWater.Update(elapsed);
    }

    private void RequireSeasonControllersFinished()
    {
        if ((!Object.ReferenceEquals(directorVehicles, null) && !directorVehicles.IsFinished) ||
            (!Object.ReferenceEquals(directorThreeSeven, null) && !directorThreeSeven.IsFinished) ||
            (!Object.ReferenceEquals(directorSeasonFlight, null) && !directorSeasonFlight.IsFinished) ||
            (!Object.ReferenceEquals(directorPlanePush, null) && !directorPlanePush.IsFinished) ||
            (!Object.ReferenceEquals(directorWardrobe, null) && !directorWardrobe.IsFinished) ||
            (!Object.ReferenceEquals(directorEntry, null) && !directorEntry.IsFinished) ||
            (!Object.ReferenceEquals(directorTableWipe, null) && !directorTableWipe.IsFinished) ||
            (!Object.ReferenceEquals(directorCartaTrial, null) && !directorCartaTrial.IsFinished) ||
            (!Object.ReferenceEquals(directorGorjetaTrial, null) && !directorGorjetaTrial.IsFinished) ||
            (!Object.ReferenceEquals(directorDoors, null) && !directorDoors.IsFinished) ||
            (!Object.ReferenceEquals(directorSeasonWater, null) && !directorSeasonWater.IsFinished) ||
            (!Object.ReferenceEquals(directorPoses, null) && !directorPoses.AreTransitionsComplete))
            throw new InvalidOperationException("Uma ação física da temporada não terminou; a tomada foi interrompida.");
    }

    private bool ControlsActorBody(string actorId, int elapsed)
    {
        foreach (PosesSerieDiretor.PoseSpec pose in activeScene.Poses)
            if (pose.Tipo == "deitada" && String.Equals(pose.Ator, actorId, StringComparison.OrdinalIgnoreCase)) return true;
        if ((!Object.ReferenceEquals(directorStaging, null) && directorStaging.ControlsBody(actorId, elapsed)) ||
            (!Object.ReferenceEquals(directorThreeSeven, null) && directorThreeSeven.ControlsBody(actorId, elapsed)) ||
            (!Object.ReferenceEquals(directorSeasonFlight, null) && directorSeasonFlight.ControlsBody(actorId, elapsed)) ||
            (!Object.ReferenceEquals(directorPlanePush, null) && directorPlanePush.ControlsBody(actorId.ToUpperInvariant(), elapsed)) ||
            (!Object.ReferenceEquals(directorWardrobe, null) && directorWardrobe.ControlsBody(actorId.ToUpperInvariant(), elapsed)) ||
            (!Object.ReferenceEquals(directorEntry, null) && directorEntry.ControlsBody(actorId, elapsed)) ||
            (!Object.ReferenceEquals(directorTableWipe, null) && directorTableWipe.ControlsBody(actorId, elapsed)) ||
            (!Object.ReferenceEquals(directorCartaTrial, null) && directorCartaTrial.ControlsBody(actorId, elapsed)) ||
            (!Object.ReferenceEquals(directorGorjetaTrial, null) && directorGorjetaTrial.ControlsBody(actorId, elapsed)) ||
            (!Object.ReferenceEquals(directorDoors, null) && directorDoors.ControlsBody(actorId, elapsed))) return true;
        foreach (PosesSerieDiretor.TransitionSpec transition in activeScene.TransicoesPose)
            if (String.Equals(transition.Ator, actorId, StringComparison.OrdinalIgnoreCase) && elapsed >= transition.InicioMs && elapsed < transition.InicioMs + transition.DuracaoMs) return true;
        foreach (VeiculosSerieDiretor.VehicleSpec vehicle in activeScene.Veiculos)
            foreach (VeiculosSerieDiretor.VehicleAction action in vehicle.Actions)
                if (String.Equals(action.Ator, actorId, StringComparison.OrdinalIgnoreCase) && elapsed >= action.InicioMs && elapsed < action.InicioMs + action.DuracaoMs) return true;
        return false;
    }

    private void AdvanceCaptureEnd()
    {
        if (!awaitingCaptureEnd) return;
        if (Object.ReferenceEquals(captureEndTimer, null) || !sceneReady || !directorCamera.IsInitialized || !takeFinished ||
            !automaticShortcutActive || Object.ReferenceEquals(directorCapture, null) || !directorCapture.IsArmed)
            throw new InvalidOperationException("O estado da captura mudou durante o quadro final; a gravação será interrompida.");
        RequireActors();
        if (captureEndTimer.ElapsedMilliseconds < CaptureFinalHoldMs) return;
        WriteLog("CAPTURA_QUADRO_FINAL_CONCLUIDO", "Cena=" + activeScene.Id + "; real_ms=" + captureEndTimer.ElapsedMilliseconds);
        CompleteTakeCapture();
    }

    private void CompleteTakeCapture()
    {
        if (activeScene.Id == "teste_fila_85")
        {
            WriteLog("ESCRITA85_SEM_CAPTURA", "WindowEnded=True; ARM=False; SAVE=False; ContactProven=False; ProductionApproval=False");
            return;
        }
        if (!Object.ReferenceEquals(directorGorjetaTrial, null))
        {
            if (!directorGorjetaTrial.IsFinished) throw new InvalidOperationException("Tracks/contato fisico do ensaio gorjeta incompletos; nao salvar tomada como completa.");
            WriteLog("GORJETA_TRIAL_CAPTURE_GATE", "TracksComplete=" + directorGorjetaTrial.TracksCompletos +
                "; ContactProved=" + directorGorjetaTrial.ContatoComprovado + "; OpeningVisualConfirmed=false; CountingVisualConfirmed=false; ProductionApproval=false");
        }
        if (!Object.ReferenceEquals(directorTableWipe, null) && !directorTableWipe.IsFinished)
            throw new InvalidOperationException("Sem limpeza fisica concluida, a tomada nao sera salva como completa.");
        StopAutomaticRecording();
        if (!Object.ReferenceEquals(directorCapture, null))
        {
            try { directorCapture.Finish(); }
            catch (Exception ex) { WriteLog("ERRO_CAPTURA", ex.ToString()); }
        }
        ShowMessage("Diretor: tomada terminou; plano final mantido. " + ReadyControls());
    }

    private void AdvanceScheduledCuts(int elapsed)
    {
        if (HasOwnEpisodeCamera) return;
        int next = cutIndex;
        while (next + 1 < activeScene.Cuts.Count && activeScene.Cuts[next + 1].InicioMs <= elapsed)
            next++;
        if (next == cutIndex)
            return;
        // O corte para Dinjer deve aguardar sua saída física, para a câmera não ficar dentro do ônibus.
        if (activeScene.Id == "EP01_C06" && (activeScene.Cuts[next].Plano == "MEDIA" || activeScene.Cuts[next].Plano == "ATRAS") && actorsById["dinjer"].IsInVehicle())
            return;
        cutIndex = next;
        CenaJsonDiretor.Corte cut = activeScene.Cuts[cutIndex];
        SetSceneShot(cut.Plano, cut.Personagem, activeScene.SmoothCamera);
        WriteLog("CAMERA_CORTE_PROGRAMADO", "Índice=" + (cutIndex + 1) + "; plano=" + cut.Plano +
            "; alvo=" + cut.Personagem + "; início_ms=" + cut.InicioMs + "; real_ms=" + elapsed);
    }

    private void RestoreSpeechAfterBodyChanges(int elapsed)
    {
        if (Object.ReferenceEquals(directorStaging, null)) return;
        foreach (KeyValuePair<string, Ped> actor in actorsById)
        {
            int revision = directorStaging.GetTaskRevision(actor.Key), previous;
            stagingTaskRevisions.TryGetValue(actor.Key, out previous);
            if (revision == previous) continue;
            stagingTaskRevisions[actor.Key] = revision;
            if (!Object.ReferenceEquals(directorInterpretation, null)) directorInterpretation.InvalidateLook(actor.Key);
            if (segmentIndex < 0 || activeScene.Segments[segmentIndex].Narration) continue;
            SegmentData speech = activeScene.Segments[segmentIndex];
            int remaining = System.Math.Max(1, speech.EndMs - elapsed);
            if (actor.Key == speech.ActorId)
            {
                if (!ControlsActorBody(actor.Key, elapsed))
                {
                    GestureData gesture = gestures[speech.Intention];
                    bool male = IsMale(actor.Value);
                    actor.Value.Task.PlayAnimation(male ? gesture.MaleDictionary : gesture.FemaleDictionary,
                        male ? gesture.MaleClip : gesture.FemaleClip, 4f, -4f, remaining,
                        AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.Secondary, 0f);
                }
            }
            else if (Object.ReferenceEquals(directorInterpretation, null) || !directorInterpretation.HasExplicitLook(actor.Key, elapsed))
                actor.Value.Task.LookAt(actorsById[speech.ActorId], remaining);
        }
    }

    private void AdvanceTimedActions(int elapsed)
    {
        // Terminar antes de iniciar permite duas ações adjacentes no mesmo ator.
        for (int i = 0; i < activeScene.Actions.Count; i++)
        {
            CenaJsonDiretor.Acao action = activeScene.Actions[i];
            if (startedActions.Contains(i) && !finishedActions.Contains(i) && elapsed >= action.InicioMs + action.DuracaoMs)
                FinishTimedAction(i);
        }
        for (int i = 0; i < activeScene.Actions.Count; i++)
        {
            CenaJsonDiretor.Acao action = activeScene.Actions[i];
            if (finishedActions.Contains(i) || elapsed < action.InicioMs)
                continue;
            if (elapsed >= action.InicioMs + action.DuracaoMs)
            {
                startedActions.Add(i);
                finishedActions.Add(i);
                WriteLog("ACAO_PULADA", "Índice=" + (i + 1) + "; tipo=" + action.Tipo + "; real_ms=" + elapsed);
                continue;
            }
            Ped ped = actorsById[action.Personagem];
            if (!startedActions.Contains(i))
            {
                int remaining = action.InicioMs + action.DuracaoMs - elapsed;
                if (action.Tipo == "andarAte")
                {
                    ped.Task.ClearAllImmediately();
                    // -1 impede a tarefa de teleportar o ped ao vencer seu próprio timeout.
                    ped.Task.GoStraightTo(actionDestinations[i], -1, new PedMoveBlendRatio(action.Velocidade), ped.Heading, 0.25f);
                }
                else if (action.Tipo == "virarPara")
                {
                    ped.Task.ClearAllImmediately();
                    ped.Task.StandStill(-1);
                    turnStartHeadings[i] = ped.Heading;
                    Vector3 delta = actorsById[action.Alvo].Position - ped.Position;
                    turnTargetHeadings[i] = Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
                }
                else if (action.Tipo == "animacao")
                {
                    GestureData gesture = gestures[action.Animacao];
                    bool male = IsMale(ped);
                    ped.Task.PlayAnimation(male ? gesture.MaleDictionary : gesture.FemaleDictionary,
                        male ? gesture.MaleClip : gesture.FemaleClip, 4f, -4f, remaining,
                        AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.Secondary, 0f);
                }
                else
                    ped.Task.StandStill(remaining);
                startedActions.Add(i);
                WriteLog("ACAO_JSON_INICIADA", "Índice=" + (i + 1) + "; tipo=" + action.Tipo + "; personagem=" + action.Personagem + "; real_ms=" + elapsed);
            }
            if (action.Tipo == "virarPara")
                ApplyTimedTurn(i, elapsed);
        }
    }

    private void ApplyTimedTurn(int index, int elapsed)
    {
        CenaJsonDiretor.Acao action = activeScene.Actions[index];
        Ped ped = actorsById[action.Personagem];
        // Fixar o alvo inicial evita inverter a volta se o outro ator cruza o eixo.
        float targetHeading = turnTargetHeadings[index];
        float start = turnStartHeadings[index];
        float angle = (targetHeading - start + 540f) % 360f - 180f;
        float progress = System.Math.Min(1f, System.Math.Max(0f, (elapsed - action.InicioMs) / (float)action.DuracaoMs));
        ped.Heading = (start + angle * progress + 360f) % 360f;
    }

    private void FinishTimedAction(int index)
    {
        CenaJsonDiretor.Acao action = activeScene.Actions[index];
        Ped ped = actorsById[action.Personagem];
        if (action.Tipo == "andarAte")
        {
            Vector3 delta = actionDestinations[index] - ped.Position;
            if (delta.X * delta.X + delta.Y * delta.Y > 0.36f)
                throw new InvalidOperationException("andarAte: " + ActorName(action.Personagem) + " não chegou ao destino. Aumente a duração ou escolha um trajeto sem obstáculos.");
            ped.Task.ClearAllImmediately();
            ped.Task.StandStill(-1);
        }
        else if (action.Tipo == "virarPara")
        {
            ApplyTimedTurn(index, action.InicioMs + action.DuracaoMs);
            ped.Task.StandStill(-1);
        }
        else if (action.Tipo == "animacao")
            ped.Task.ClearSecondary();
        // Esperar não limpa o gesto de uma fala simultânea.
        finishedActions.Add(index);
        WriteLog("ACAO_JSON_CONCLUIDA", "Índice=" + (index + 1) + "; tipo=" + action.Tipo);
    }

    private void StartSegment(int elapsed)
    {
        SegmentData segment = activeScene.Segments[segmentIndex];
        FrameSegment(segment);
        if (segment.Pause)
        {
            subtitleText = "";
            WriteLog("PAUSA_INICIADA", "Índice=" + (segmentIndex + 1) + "; início_ms=" + segment.StartMs + "; fim_ms=" + segment.EndMs);
            return;
        }
        string label = segment.NarratorName;
        if (!segment.Narration)
        {
            Ped speaker = actorsById[segment.ActorId];
            GestureData gesture = gestures[segment.Intention];
            bool male = IsMale(speaker);
            string dictionary = male ? gesture.MaleDictionary : gesture.FemaleDictionary;
            string clip = male ? gesture.MaleClip : gesture.FemaleClip;
            int remaining = System.Math.Max(1, segment.EndMs - elapsed);
            // Tarefa secundária no tronco: os pés ficam na marca e o olhar permanece livre.
            if (!ControlsActorBody(segment.ActorId, elapsed))
                speaker.Task.PlayAnimation(dictionary, clip, 4f, -4f, remaining,
                    AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.Secondary, 0f);
            if (!Object.ReferenceEquals(directorSeasonFlight, null)) directorSeasonFlight.SetSpeaker(segment.ActorId);
            foreach (KeyValuePair<string, Ped> actor in actorsById)
            {
                if (actor.Key != segment.ActorId && (Object.ReferenceEquals(directorInterpretation, null) || !directorInterpretation.HasExplicitLook(actor.Key, elapsed)))
                    actor.Value.Task.LookAt(speaker, remaining);
            }
            Function.Call(Hash.PLAY_FACIAL_ANIM, speaker.Handle, MouthClip, MouthDictionary);
            // Não há consulta confiável de "facial terminou". Reaplicar antes do fim natural.
            float facialDuration = Function.Call<float>(Hash.GET_ANIM_DURATION, MouthDictionary, MouthClip);
            mouthInterval = Single.IsNaN(facialDuration) || Single.IsInfinity(facialDuration) || facialDuration <= 0f ? 600 :
                (int)System.Math.Max(100.0, System.Math.Min(600.0, facialDuration * 750.0));
            nextMouthTime = elapsed + mouthInterval;
            label = ActorName(segment.ActorId);
        }
        subtitleText = (!segment.Narration || narrationSubtitles) ? label + ": " + segment.Text : "";
        WriteLog(segment.Narration ? "NARRACAO_INICIADA" : "FALA_INICIADA", "Índice=" + (segmentIndex + 1) +
            "; personagem=" + segment.ActorId + "; início_ms=" + segment.StartMs + "; fim_ms=" + segment.EndMs +
            "; real_ms=" + elapsed + "; gesto=" + segment.Intention);
    }

    private string ActorName(string id)
    {
        foreach (ActorData actor in activeScene.Actors)
            if (actor.Id == id)
                return actor.Name;
        return id;
    }

    private void DrawSceneInterface()
    {
        try
        {
            float width = GTA.UI.Screen.ScaledWidth;
            if (Object.ReferenceEquals(subtitleElement, null))
            {
                subtitleElement = CreateTextElement(subtitleScale, width * 0.84f);
                statusElement = CreateTextElement(0.32f, width * 0.92f);
                countdownElement = CreateTextElement(1.3f, width * 0.9f);
            }
            if (countingDown)
            {
                uint elapsed = unchecked((uint)Game.GameTime - countdownStart);
                int remaining = System.Math.Max(1, (CountdownMs - (int)elapsed + 999) / 1000);
                countdownElement.Caption = remaining.ToString(CultureInfo.InvariantCulture);
                countdownElement.Position = new System.Drawing.PointF(width * 0.5f, 300f);
                countdownElement.ScaledDraw();
            }
            else if (subtitlesEnabled && playing && subtitleText.Length > 0)
            {
                subtitleElement.Caption = subtitleText;
                subtitleElement.WrapWidth = width * 0.84f;
                subtitleElement.Position = new System.Drawing.PointF(width * 0.5f, 600f);
                int lines = System.Math.Max(1, System.Math.Min(8, subtitleElement.ScaledLineCount));
                float height = lines * subtitleScale * 47f + 20f;
                float top = 690f - height;
                DrawPanel(width * 0.9f, top, height);
                subtitleElement.Position = new System.Drawing.PointF(width * 0.5f, top + 7f);
                subtitleElement.ScaledDraw();
            }
            if (!playing && !countingDown && !awaitingCaptureStart && !awaitingCaptureEnd && !takeFinished && showMessages)
            {
                string text = directorCamera.IsFree ? "Câmera livre: WASD move | E sobe; Q desce | Setas giram | Shift acelera | " + freeCameraKey + " fixa | " + markCameraKey + " salva" :
                    (unchecked((int)(statusUntil - (uint)Game.GameTime)) > 0 && statusText.Length > 0 ? statusText :
                    (activeScene.AutomaticCamera || activeScene.Cuts.Count > 0 ? ReadyControls() :
                    directorCamera.CurrentPreset + " em " + (activeScene.Actors.Count == 0 ? "ambiente" : activeScene.Actors[cameraTargetIndex].Name) + " | " + presetKey + " plano | " + targetKey + " ator | " +
                    freeCameraKey + " livre | " + actionKey + " ação | " + clearKey + " limpar"));
                statusElement.Caption = QueueCaption() + " | " + text;
                statusElement.WrapWidth = width * 0.92f;
                statusElement.Position = new System.Drawing.PointF(width * 0.5f, 14f);
                DrawPanel(width * 0.97f, 7f, System.Math.Max(1, statusElement.ScaledLineCount) * 18f + 12f);
                statusElement.ScaledDraw();
            }
        }
        catch (Exception ex)
        {
            if (!interfaceErrorReported)
                WriteLog("ERRO_INTERFACE", ex.ToString());
            interfaceErrorReported = true;
        }
    }

    private static GTA.UI.TextElement CreateTextElement(float scale, float wrapWidth)
    {
        return new GTA.UI.TextElement("", new System.Drawing.PointF(0f, 0f), scale, System.Drawing.Color.White,
            GTA.UI.Font.ChaletLondon, GTA.UI.Alignment.Center, true, true, wrapWidth);
    }

    private static void DrawPanel(float width, float top, float height)
    {
        Function.Call(Hash.DRAW_RECT, 0.5f, (top + height * 0.5f) / 720f,
            width / GTA.UI.Screen.ScaledWidth, height / 720f, 0, 0, 0, 180, false);
    }

    private void StopSegment()
    {
        if (segmentIndex < 0)
            return;
        foreach (Ped ped in createdPeds)
        {
            if (!ped.Exists())
                continue;
            SegmentData previous = activeScene.Segments[segmentIndex];
            if (!previous.Narration && Object.ReferenceEquals(ped, actorsById[previous.ActorId]) &&
                !ControlsActorBody(previous.ActorId, unchecked((int)(unchecked((uint)Game.GameTime) - actionStartTime))))
                ped.Task.ClearSecondary();
            ped.Task.ClearLookAt();
            if (!Object.ReferenceEquals(directorInterpretation, null))
                foreach (KeyValuePair<string, Ped> actor in actorsById)
                    if (Object.ReferenceEquals(actor.Value, ped)) directorInterpretation.InvalidateLook(actor.Key);
            ResetMouth(ped);
        }
    }

    private string WriteSrt()
    {
        StringBuilder subtitles = new StringBuilder();
        int subtitleNumber = 0;
        for (int i = 0; i < activeScene.Segments.Count; i++)
        {
            SegmentData segment = activeScene.Segments[i];
            if (segment.Pause) continue;
            subtitles.AppendLine((++subtitleNumber).ToString(CultureInfo.InvariantCulture));
            subtitles.AppendLine(SrtTime(segment.StartMs + CountdownMs) + " --> " + SrtTime(segment.EndMs + CountdownMs));
            subtitles.AppendLine((segment.Narration ? segment.NarratorName : ActorName(segment.ActorId)) + ": " + segment.Text);
            subtitles.AppendLine();
        }
        string basePath = Path.Combine(scenesDirectory, activeScene.Id);
        for (int version = 1; version <= 10000; version++)
        {
            string path = basePath + (version == 1 ? "" : "_v" + version.ToString(CultureInfo.InvariantCulture)) + ".srt";
            if (File.Exists(path))
                continue;
            FileStream stream;
            try
            {
                stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            }
            catch (IOException)
            {
                if (!File.Exists(path))
                    throw;
                continue; // Outra escrita criou esta versão entre a consulta e CreateNew.
            }
            using (stream)
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(subtitles.ToString());
            WriteLog("SRT_SALVO", path);
            return path;
        }
        throw new InvalidOperationException("Há muitas versões de SRT para esta cena. Use outro ID.");
    }

    private static string SrtTime(int milliseconds)
    {
        return (milliseconds / 3600000).ToString("00", CultureInfo.InvariantCulture) + ":" +
            (milliseconds / 60000 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
            (milliseconds / 1000 % 60).ToString("00", CultureInfo.InvariantCulture) + "," +
            (milliseconds % 1000).ToString("000", CultureInfo.InvariantCulture);
    }

    private void SaveAndApplyEnvironment()
    {
        if (environmentSaved)
            return;
        savedClock = GameClock.Now;
        savedClockPaused = GameClock.IsPaused;
        using (OutputArgument weatherFrom = new OutputArgument())
        using (OutputArgument weatherTo = new OutputArgument())
        using (OutputArgument weatherBlend = new OutputArgument())
        {
            Function.Call(Hash.GET_CURR_WEATHER_STATE, weatherFrom, weatherTo, weatherBlend);
            savedWeatherFrom = weatherFrom.GetResult<uint>();
            savedWeatherTo = weatherTo.GetResult<uint>();
            savedWeatherBlend = weatherBlend.GetResult<float>();
        }
        savedPoliceIgnore = Game.Player.Wanted.PoliceBackOff;
        savedDispatchCops = Game.Player.Wanted.DispatchesCopsForPlayer;
        savedWantedLevel = Game.Player.Wanted.WantedLevel;
        restorePolice = activeScene.BlockPolice;
        environmentSaved = true;
        GameClock.TimeOfDay = GameClockTime.FromHms(activeScene.Hour, activeScene.Minute, 0);
        GameClock.IsPaused = true;
        World.Weather = WeatherFromName(activeScene.WeatherName);
        if (activeScene.BlockPolice)
        {
            Game.Player.Wanted.SetPoliceIgnorePlayer(true);
            Game.Player.Wanted.DispatchesCopsForPlayer = false;
            Game.Player.Wanted.SetWantedLevel(0, false);
            Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
        }
        WriteLog("AMBIENTE_APLICADO", "Relógio pausado; clima=" + activeScene.WeatherName + "; bloquear polícia=" + activeScene.BlockPolice);
    }

    private void ApplyDensity()
    {
        float traffic = activeScene.ClearArea ? 0f : activeScene.Traffic;
        float pedestrians = activeScene.ClearArea ? 0f : activeScene.PedestrianDensity;
        Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, traffic);
        Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, traffic);
        Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, traffic);
        Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, pedestrians);
        Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, pedestrians, pedestrians);
        World.Weather = WeatherFromName(activeScene.WeatherName);
    }

    private void ReleaseModels()
    {
        foreach (KeyValuePair<string, Model> entry in pendingModels)
            entry.Value.MarkAsNoLongerNeeded();
        pendingModels.Clear();
    }

    private bool IsRoomC4Scene
    { get { return activeScene != null && activeScene.Id == "EP11_C04"; } }

    // GET_INTERIOR_FROM_PRIMARY_VIEW, NativeDB v1604; não confundir com gameplay-camera memory.
    private static int C4ViewportInterior()
    { return Function.Call<int>((Hash)0xE7D267EC6CA966C3UL); }


    // API docs:0; Native Legacy159 observado: -1. Somente o par SEM proxy e exterior.
    // Hash negativo de sala real (ex. limbo) com interior existente nao e exterior.
    private static bool C4ExteriorViewport(int interior, int room)
    { return interior == 0 && (room == 0 || room == -1); }

    private static bool C4RestorableViewport(int interior, int room)
    { return C4ExteriorViewport(interior, room) || (room != 0 && room != -1 && C4UsableInterior(interior)); }

    private static bool C4UsableInterior(int handle)
    {
        InteriorProxy proxy = InteriorProxy.FromHandle(handle);
        return !Object.ReferenceEquals(proxy, null) && proxy.Exists() &&
            Function.Call<bool>(Hash.IS_VALID_INTERIOR, handle) && Function.Call<bool>(Hash.IS_INTERIOR_READY, handle) &&
            !proxy.IsDisabled && !proxy.IsCapped;
    }

    private Ped C4OwnedActor()
    {
        Ped actor;
        if (!IsRoomC4Scene || activeScene.DurationMs != 8000 || activeScene.LocalId != "MANSAO_DJANIRA" ||
            activeScene.Actors.Count != 1 || activeScene.Actors[0].Id != "djanira_rica" ||
            !actorsById.TryGetValue("djanira_rica", out actor) || Object.ReferenceEquals(actor, null) ||
            !actor.Exists() || actor.IsPlayer || actor.IsDead || !createdPeds.Contains(actor))
            throw new InvalidOperationException("C04 room: exige Djanira rica propria viva e contrato original8s.");
        return actor;
    }

    private void SaveC4Viewport()
    {
        if (!IsRoomC4Scene) return;
        if (c4ViewportChanged) throw new InvalidOperationException("C04 room: viewport propria anterior ainda nao restaurada.");
        c4SavedViewportInterior = C4ViewportInterior();
        c4SavedViewportRoom = Interior.GetRoomKeyForGameViewport();
        c4ViewportSaved = true;
        c4BeforePoseInterior = c4BeforePoseRoom = c4RoomActorHandle = 0;
        WriteLog("C04_VIEWPORT_SALVA", "Interior=" + c4SavedViewportInterior + "; Room=" + c4SavedViewportRoom +
            "; Restorable=" + C4RestorableViewport(c4SavedViewportInterior, c4SavedViewportRoom) +
            "; ExteriorPair=" + C4ExteriorViewport(c4SavedViewportInterior, c4SavedViewportRoom));
    }

    private void ObserveC4Room(string stage, bool beforePose)
    {
        if (!IsRoomC4Scene) return;
        Ped actor = C4OwnedActor();
        int actorInterior = Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle);
        int actorRoom = actor.CurrentInteriorRoomKey;
        if (beforePose)
        {
            c4RoomActorHandle = actor.Handle;
            c4BeforePoseInterior = actorInterior; c4BeforePoseRoom = actorRoom;
        }
        Ped player = Game.Player.Character;
        bool playerExists = !Object.ReferenceEquals(player, null) && player.Exists();
        string camera = "SemCameraScript";
        Camera render = ScriptCameraDirector.RenderingCam;
        if (!Object.ReferenceEquals(render, null) && render.Exists())
        {
            Vector3 position = render.Position;
            camera = "Pos=" + position + "; Rot=" + render.Rotation + "; Fov=" + render.FieldOfView +
                "; InteriorAtCam=" + Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, position.X, position.Y, position.Z);
        }
        WriteLog("C04_ROOM_GEOMETRIA", "Stage=" + stage + "; Actor=" + actor.Handle + "; Root=" + actor.Position +
            "; Pelvis=" + C4Bone(actor, Bone.SkelPelvis) + "; Head=" + C4Bone(actor, Bone.SkelHead) +
            "; FootL=" + C4Bone(actor, Bone.SkelLeftFoot) + "; FootR=" + C4Bone(actor, Bone.SkelRightFoot) +
            "; ActorInterior=" + actorInterior + "; ActorRoom=" + actorRoom +
            "; PlayerInterior=" + (playerExists ? Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, player.Handle) : 0) +
            "; PlayerRoom=" + (playerExists ? player.CurrentInteriorRoomKey : 0) +
            "; ViewInterior=" + C4ViewportInterior() + "; ViewRoom=" + Interior.GetRoomKeyForGameViewport() +
            "; " + camera);
    }

    private static string C4Bone(Ped actor, Bone id)
    {
        PedBone bone = actor.Bones[id];
        return Object.ReferenceEquals(bone, null) || !bone.IsValid ? "Invalid" : bone.Position.ToString();
    }

    private void PrepareC4Viewport()
    {
        if (!IsRoomC4Scene) return;
        ObserveC4Room("DEPOIS_POSE_CAMERA_ANTES_FORCE", false);
        Ped actor = C4OwnedActor();
        if (!c4ViewportSaved || actor.Handle != c4RoomActorHandle)
            throw new InvalidOperationException("C04 room: faltou snapshot real antes da pose/camera.");
        if (!C4RestorableViewport(c4SavedViewportInterior, c4SavedViewportRoom))
            throw new InvalidOperationException("C04 room: viewport original nao tem proxy real restauravel; nenhum Force realizado.");
        int ownInterior = Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle);
        int ownRoom = actor.CurrentInteriorRoomKey;
        string proof = "ACTOR_ATUAL";
        if (!C4UsableInterior(ownInterior) || ownRoom == 0)
        {
            Ped player = Game.Player.Character;
            int playerInterior = !Object.ReferenceEquals(player, null) && player.Exists() ? Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, player.Handle) : 0;
            int playerRoom = !Object.ReferenceEquals(player, null) && player.Exists() ? player.CurrentInteriorRoomKey : 0;
            if (playerInterior != c4BeforePoseInterior || playerRoom != c4BeforePoseRoom ||
                playerRoom == 0 || !C4UsableInterior(playerInterior))
                throw new InvalidOperationException("C04 room: ator sem sala atual e jogador nao confirma a mesma sala real anterior; nenhum Force realizado.");
            ownInterior = playerInterior; ownRoom = playerRoom;
            proof = "PLAYER_ATUAL_IGUAL_ACTOR_ANTES_POSE";
        }
        c4OwnViewportInterior = ownInterior; c4OwnViewportRoom = ownRoom;
        c4RoomCameraHandle = directorCamera.RequireRoomC4OwnCameraHandle(activeScene.Id, actor);
        c4ViewportRequestedFrame = unchecked((uint)Game.FrameCount);
        c4ViewportRequestedTime = unchecked((uint)Game.GameTime);
        c4ViewportPending = true;
        c4ViewportForceReturned = false;
        c4ViewportChanged = true; // Set antes do Native: falha tambem exige tentativa de restauro.
        LogC4ViewportRaw("BEFORE_NATIVE_FORCE");
        Interior.ForceRoomForGameViewport(InteriorProxy.FromHandle(ownInterior), ownRoom);
        c4ViewportForceReturned = true;
        LogC4ViewportRaw("RAW_IMMEDIATE"); // Medir agora, mas nao concluir cameraReady no mesmo frame.
        c4NextRoomLog = unchecked((uint)Game.GameTime + 1000);
        WriteLog("C04_VIEWPORT_FORCE", "Interior=" + ownInterior + "; Room=" + ownRoom + "; Proof=" + proof +
            "; ActorRoomOverride=False; BodyTransformChanged=False; VisualApproval=False");
        ObserveC4Room("DEPOIS_FORCE", false);
    }

    private bool C4ObservedOwnedRoom(int primaryInterior, int room)
    {
        // PrimaryView pode continuar exterior. Nao afirmar proxy forçado observado quando getter retorna0.
        return c4ViewportForceReturned && room == c4OwnViewportRoom &&
            (primaryInterior == c4OwnViewportInterior || primaryInterior == 0);
    }

    private bool C4ObservedOwnedRendering()
    {
        Ped actor = C4OwnedActor();
        return Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle) == c4OwnViewportInterior &&
            actor.CurrentInteriorRoomKey == c4OwnViewportRoom &&
            directorCamera.RequireRoomC4OwnCameraHandle("EP11_C04", actor) == c4RoomCameraHandle &&
            Function.Call<int>(Hash.GET_RENDERING_CAM) == c4RoomCameraHandle &&
            Function.Call<bool>(Hash.IS_CAM_ACTIVE, c4RoomCameraHandle);
    }

    private void LogC4ViewportRaw(string stage)
    {
        int primary = C4ViewportInterior(), room = Interior.GetRoomKeyForGameViewport();
        int render = Function.Call<int>(Hash.GET_RENDERING_CAM);
        WriteLog("C04_VIEWPORT_RAW", "Stage=" + stage + "; PrimaryInterior=" + primary + "; RoomRaw=" + room +
            "; ForceInteriorArgument=" + c4OwnViewportInterior + "; ForceRoomArgument=" + c4OwnViewportRoom +
            "; NativeForceReturned=" + c4ViewportForceReturned + "; OwnCamera=" + c4RoomCameraHandle +
            "; RenderingCamera=" + render + "; OwnCameraActive=" +
            (c4RoomCameraHandle != 0 && Function.Call<bool>(Hash.IS_CAM_ACTIVE, c4RoomCameraHandle)) +
            "; Frame=" + Game.FrameCount + "; RequestedFrame=" + c4ViewportRequestedFrame +
            "; ElapsedMs=" + unchecked((uint)Game.GameTime - c4ViewportRequestedTime) +
            "; PrimaryMatchesArgument=" + (primary == c4OwnViewportInterior) +
            "; RoomMatchesArgument=" + (room == c4OwnViewportRoom) + "; VisualApproval=False");
    }

    private void RequireC4Viewport()
    {
        int primary = C4ViewportInterior(), room = Interior.GetRoomKeyForGameViewport();
        if (c4ViewportPending || !C4ObservedOwnedRoom(primary, room) || !C4ObservedOwnedRendering())
        {
            LogC4ViewportRaw("REQUIRE_FAIL");
            throw new InvalidOperationException("C04 room: faltou room proprio/camera renderizada propria em frame posterior.");
        }
    }

    private void MaintainC4Viewport()
    {
        if (!IsRoomC4Scene || !c4ViewportChanged) return;
        Ped actor = C4OwnedActor();
        if (actor.Handle != c4RoomActorHandle || !C4UsableInterior(c4OwnViewportInterior))
            throw new InvalidOperationException("C04 room: identidade/interior medidos deixaram de existir.");
        uint now = unchecked((uint)Game.GameTime);
        if (c4ViewportPending)
        {
            LogC4ViewportRaw("RAW_TICK_PENDING");
            uint elapsed = unchecked(now - c4ViewportRequestedTime);
            if (elapsed > 1000)
                throw new InvalidOperationException("C04 room: sem coerencia room/camera propria apos1s; nenhuma captura aceita.");
            if (unchecked((uint)Game.FrameCount - c4ViewportRequestedFrame) == 0) return;
            int primary = C4ViewportInterior(), room = Interior.GetRoomKeyForGameViewport();
            if (!C4ObservedOwnedRoom(primary, room) || !C4ObservedOwnedRendering()) return;
            c4ViewportPending = false;
            RequireC4Viewport();
            if (c4OwnViewportInterior == 166657 && c4OwnViewportRoom == -1537723083)
            {
                QueryC4FoyerPropsReadOnly();
                QueryC4DecorByLiteralReadOnly();
                BeginC4SofaHideTest();
            }
            else
            {
                if (c4OwnViewportInterior != 227329 || c4OwnViewportRoom == 0 || c4OwnViewportRoom == -1 ||
                    c4BeforePoseInterior != c4OwnViewportInterior || c4BeforePoseRoom != c4OwnViewportRoom ||
                    Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle) != c4OwnViewportInterior ||
                    actor.CurrentInteriorRoomKey != c4OwnViewportRoom ||
                    FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)) != "F74235C9A2DD9505606805B6EEAA55253A11199020A9E8ACAB4ED2E173EE0DE1" ||
                    c4SofaHideRestore.Count != 0 || !Function.Call<bool>(Hash.IS_IPL_ACTIVE, "apa_v_mp_h_01_a"))
                    throw new InvalidOperationException("C04 Modern1 corner requires original Rich8/F742, actual same room227329 and no hide; visual approval still pending.");
                string[] conflictingThemes = { "apa_v_mp_h_02_a", "apa_v_mp_h_03_a", "apa_v_mp_h_04_a", "apa_v_mp_h_05_a", "apa_v_mp_h_06_a", "apa_v_mp_h_07_a", "apa_v_mp_h_08_a" };
                foreach (string theme in conflictingThemes) if (Function.Call<bool>(Hash.IS_IPL_ACTIVE, theme))
                    throw new InvalidOperationException("C04 Modern1 theme conflict; no automatic removal in production.");
                WriteLog("C04_MODERN1_CORNER_NATIVE", "OriginalScene=EP11_C04; DurationMs=8000; Actor=" + actor.Handle +
                    "; Interior=" + c4OwnViewportInterior + "; Room=" + c4OwnViewportRoom + "; CapturedLocation=MANSAO_DJANIRA; OtherRoomsMayRemainFurnished=True;" +
                    " NoFurnitureHide=True; ArchitectureUntouched=True; FrameRoomEmpty=False; RootPixelsRequired=True; ProductionVisualApproval=False");
            }
            areaPreparationTimer = null;
            c4NextRoomLog = unchecked(now + 1000);
            WriteLog("C04_VIEWPORT_POS_FRAME", "PrimaryInterior=" + primary + "; RoomRaw=" + room +
                "; GateMode=" + (primary == c4OwnViewportInterior ? "ROOM_AND_PRIMARY_OWN_RENDER_OWN" : "ROOM_OWN_PRIMARY_ZERO_RENDER_OWN") +
                "; ForcedInteriorGetterVerified=" + (primary == c4OwnViewportInterior) +
                "; ForceArgumentInteriorVerified=True; ActorRoomOverride=False; BodyTransformChanged=False; VisualApproval=False");
            ShowMessage("Diretor: camera pronta. " + ReadyControls());
            WriteLog("CAMERA_PRONTA", "Plano=" + activeScene.CameraPreset + "; alvo=" + activeScene.CameraActor);
            ObserveC4Room("DEPOIS_FORCE_POS_FRAME", false);
            return;
        }
        RequireC4Viewport(); // Sala/camera perdida depoisReady interrompe; nao mascara com Force durante filmagem.
        if (unchecked((int)(now - c4NextRoomLog)) >= 0)
        {
            LogC4ViewportRaw("RAW_TICK_READY");
            ObserveC4Room("TICK", false);
            c4NextRoomLog = unchecked(now + 1000);
        }
    }

    // Diagnostic only: once in the first coherent C4 viewport frame, before CAMERA_PRONTA.
    private void QueryC4FoyerPropsReadOnly()
    {
        if (!IsRoomC4Scene) return;
        const string expectedSource = "F74235C9A2DD9505606805B6EEAA55253A11199020A9E8ACAB4ED2E173EE0DE1";
        string path = Path.Combine(scenesDirectory, selectedSceneFile);
        Ped actor = C4OwnedActor();
        if (countingDown || playing || (!Object.ReferenceEquals(directorCapture, null) &&
            (directorCapture.IsArmed || directorCapture.IsPending)) ||
            FlightSourceHash(path) != expectedSource || c4ViewportPending ||
            c4OwnViewportInterior != 166657 || c4OwnViewportRoom != -1537723083 ||
            Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle) != 166657 ||
            actor.CurrentInteriorRoomKey != -1537723083 || !C4ObservedOwnedRendering())
            throw new InvalidOperationException("C04 props readonly: exige sourceF742/Rich8s/room real proprio, antes ARM/acao.");
        Vector3 origin = actor.Position;
        if (!C4PropsFinite(origin)) throw new InvalidOperationException("C04 props readonly: origem invalida.");
        Prop[] nearby = World.GetNearbyProps(origin, 6f);
        int poolCount = Object.ReferenceEquals(nearby, null) ? 0 : nearby.Length;
        int inspected = 0, nullCount = 0, unavailable = 0, nonphysical = 0, identityChanged = 0;
        int validPhysical = 0, otherRoom = 0, sameRoomPhysical = 0;
        List<KeyValuePair<float, string>> details = new List<KeyValuePair<float, string>>();
        for (int index = 0; index < poolCount && inspected < 64; index++)
        {
            inspected++;
            Prop prop = nearby[index];
            if (Object.ReferenceEquals(prop, null)) { nullCount++; continue; }
            int handle = prop.Handle;
            if (!prop.Exists() || !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle)) { unavailable++; continue; }
            int type = Function.Call<int>(Hash.GET_ENTITY_TYPE, handle);
            if (type != 3) { nonphysical++; continue; }
            IntPtr address = prop.MemoryAddress;
            if (address == IntPtr.Zero) { unavailable++; continue; }
            // No Building cast/Model getter. Raw Native is reached only after physical type3/address/Exists.
            int model = Function.Call<int>(Hash.GET_ENTITY_MODEL, handle);
            Vector3 position = prop.Position, rotation = prop.Rotation;
            int interior = Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, handle);
            int room = Function.Call<int>(Hash.GET_ROOM_KEY_FROM_ENTITY, handle);
            bool visible = Function.Call<bool>(Hash.IS_ENTITY_VISIBLE, handle);
            Vector3 min, max;
            new Model(model).GetDimensions(out min, out max);
            float distance = (position - origin).Length();
            if (model == 0 || !C4PropsFinite(position) || !C4PropsFinite(rotation) ||
                !C4PropsFinite(min) || !C4PropsFinite(max) || float.IsNaN(distance) || float.IsInfinity(distance) ||
                distance > 6f || min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            { unavailable++; continue; }
            if (!prop.Exists() || prop.Handle != handle || prop.MemoryAddress != address ||
                !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle) ||
                Function.Call<int>(Hash.GET_ENTITY_TYPE, handle) != 3 ||
                Function.Call<int>(Hash.GET_ENTITY_MODEL, handle) != model)
            { identityChanged++; continue; }
            validPhysical++;
            if (interior != 166657 || room != -1537723083) otherRoom++; else sameRoomPhysical++;
            details.Add(new KeyValuePair<float, string>(distance,
                "Cena=EP11_C04; SourceSHA256=" + expectedSource + "; Actor=" + actor.Handle +
                "; Handle=" + handle + "; Address=" + address + "; Type=3; ModelRaw=" + model +
                "; Pos=" + position + "; Rot=" + rotation + "; Min=" + min + "; Max=" + max +
                "; Distance=" + distance.ToString("R", CultureInfo.InvariantCulture) +
                "; Interior=" + interior + "; Room=" + room + "; SameActorRoom=" + (interior == 166657 && room == -1537723083) +
                "; VisibleGetter=" + visible + "; IdentityStable=True; ReadOnly=True; HideRequested=False; ModelIsFurnitureUnproven=True; VisualApproval=False"));
        }
        if (!actor.Exists() || actor.Handle != c4RoomActorHandle ||
            Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle) != 166657 ||
            actor.CurrentInteriorRoomKey != -1537723083 || FlightSourceHash(path) != expectedSource ||
            !C4ObservedOwnedRendering())
            throw new InvalidOperationException("C04 props readonly: identidade/source/room proprio mudou durante consulta.");
        details.Sort(delegate(KeyValuePair<float, string> left, KeyValuePair<float, string> right) { return left.Key.CompareTo(right.Key); });
        int reported = System.Math.Min(details.Count, 64);
        WriteLog("C04_MOBILIARIO_QUERY", "Cena=EP11_C04; SourceSHA256=" + expectedSource + "; Origin=" + origin +
            "; Radius=6; PoolProps=" + poolCount + "; Inspected=" + inspected + "; MaxInspected=64; ValidPhysical=" + validPhysical + "; SameRoomPhysical=" + sameRoomPhysical + "; OtherRoom=" + otherRoom +
            "; Reported=" + reported + "; MaxDetailedLogs=64; SameRoomOnly=False; Null=" + nullCount + "; Unavailable=" + unavailable +
            "; NonPhysical=" + nonphysical + "; IdentityChanged=" + identityChanged +
            "; PoolTruncated=" + (poolCount > inspected) + "; DetailsTruncated=" + (details.Count > reported) +
            "; Order=nearest_measured_pivot; ReadOnly=True; HideRequested=False; FurnitureIdentityApproved=False; EmptyRoomApproved=False");
        for (int index = 0; index < reported; index++) WriteLog("C04_MOBILIARIO_PROP", details[index].Value);
    }

    // Read-only Native lookup beyond the World.GetNearbyProps enumeration; PREP only.
    private void QueryC4DecorByLiteralReadOnly()
    {
        const string source = "F74235C9A2DD9505606805B6EEAA55253A11199020A9E8ACAB4ED2E173EE0DE1";
        Ped actor = C4OwnedActor(); RequireC4Viewport();
        if (countingDown || playing || c4ViewportPending ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)) ||
            FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)) != source ||
            c4OwnViewportInterior != 166657 || c4OwnViewportRoom != -1537723083 ||
            actor.CurrentInteriorRoomKey != -1537723083 || Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle) != 166657)
            throw new InvalidOperationException("C04 decor query: exige F742/Rich8s/room proprio antes ARM/acao.");
        if (!Object.ReferenceEquals(directorCapture, null)) directorCapture.RequireNoWriterForFinalCleanup();
        else CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
        Vector3 actorRoot = actor.Position;
        string[] names = { "v_res_m_h_console", "v_res_m_console", "v_res_m_sidetable", "v_res_m_lampstand", "v_res_m_lampstand2", "v_res_m_lamptbl", "v_res_m_palmstairs", "v_res_m_bananaplant", "v_16_high_hal_mesh_plant", "v_16_high_lng_mesh_plant", "v_16_low_lng_mesh_coffeetable", "v_16_low_lng_mesh_rugs", "v_16_study_rug" };
        Prop[] originalPool = World.GetNearbyProps(actorRoot, 6f);
        if (!C4PropsFinite(actorRoot) || Object.ReferenceEquals(originalPool, null) || originalPool.Length > 64)
            throw new InvalidOperationException("C04 decor query: origem/pool propria invalida/truncada.");
        foreach (string name in names)
        {
            Model requested = new Model(name);
            if (!requested.IsInCdImage || !requested.IsValid)
            { WriteLog("C04_DECOR_LITERAL_AUSENTE", "Nome=" + name + "; ModelRaw=" + requested.Hash + "; NoRequest=True; NoHide=True"); continue; }
            int handle = Function.Call<int>(Hash.GET_CLOSEST_OBJECT_OF_TYPE, actorRoot.X, actorRoot.Y, actorRoot.Z, 6f, requested.Hash, false, false, false);
            if (handle == 0 || !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle) || Function.Call<int>(Hash.GET_ENTITY_TYPE, handle) != 3)
            { WriteLog("C04_DECOR_LITERAL_NAO_FISICO", "Nome=" + name + "; ModelRaw=" + requested.Hash + "; NativeHandle=" + handle + "; NoModelGetter=True; NoHide=True"); continue; }
            Prop prop = Entity.FromHandle(handle) as Prop;
            int model;
            if (!C4SofaPhysical(prop, out model) || model != requested.Hash)
            { WriteLog("C04_DECOR_LITERAL_IDENTIDADE_RECUSADA", "Nome=" + name + "; NativeHandle=" + handle + "; NoHide=True"); continue; }
            IntPtr address = prop.MemoryAddress;
            Vector3 position = prop.Position, rotation = prop.Rotation, min, max;
            requested.GetDimensions(out min, out max);
            if (!C4PropsFinite(position) || !C4PropsFinite(rotation) || !C4PropsFinite(min) || !C4PropsFinite(max) ||
                min.X > max.X || min.Y > max.Y || min.Z > max.Z || (position - actorRoot).Length() > 6f)
                throw new InvalidOperationException("C04 decor query: getters/bounds fora da origem medida.");
            int interior = Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, handle), room = Function.Call<int>(Hash.GET_ROOM_KEY_FROM_ENTITY, handle);
            bool inPool = false;
            foreach (Prop old in originalPool) if (!Object.ReferenceEquals(old, null) && old.Handle == handle) inPool = true;
            int afterModel;
            if (!C4SofaPhysical(prop, out afterModel) || afterModel != model || prop.MemoryAddress != address || prop.Handle != handle)
                throw new InvalidOperationException("C04 decor query: identidade mudou entre getters.");
            WriteLog("C04_DECOR_LITERAL_MEDIDO", "Nome=" + name + "; ModelRaw=" + model + "; Handle=" + handle + "; Address=" + address +
                "; Pos=" + position + "; Rot=" + rotation + "; Min=" + min + "; Max=" + max + "; Interior=" + interior + "; Room=" + room +
                "; SameActorRoom=" + (interior == 166657 && room == -1537723083) + "; InOriginalPool=" + inPool +
                "; BoundsKind=ModelRestBounds; ReadOnly=True; NoHide=True; FurnitureSemanticApproval=False; EmptyRoomApproval=False");
        }
        RequireC4Viewport();
        if ((actor.Position - actorRoot).Length() > 0.005f || FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)) != source)
            throw new InvalidOperationException("C04 decor query: ator/source mudou durante consulta.");
    }

    private static bool C4PropsFinite(Vector3 value)
    {
        return !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y) && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
    }
    private sealed class C4SofaHideRecord
    {
        internal int Handle, Model;
        internal IntPtr Address;
        internal Vector3 Pivot, Rotation, Min, Max, Center;
        internal bool Physics, Collision, Frozen, RemoveReturned;
        internal float Radius;
        internal string Journal;
    }
    private readonly List<C4SofaHideRecord> c4SofaHideRestore = new List<C4SofaHideRecord>();

    private void RequireC4SofaTestNoCapture()
    {
        if (IsRoomC4Scene && c4SofaHideRestore.Count != 0)
            throw new InvalidOperationException("C04 sofa: ensaio de hide/undo somente PREP; ARM/F7 vetados, sala vazia ainda nao comprovada.");
    }

    private static bool C4SofaPhysical(Prop prop, out int model)
    {
        model = 0;
        if (Object.ReferenceEquals(prop, null) || !prop.Exists() || prop.MemoryAddress == IntPtr.Zero ||
            !Function.Call<bool>(Hash.DOES_ENTITY_EXIST, prop.Handle) || Function.Call<int>(Hash.GET_ENTITY_TYPE, prop.Handle) != 3)
            return false;
        model = Function.Call<int>(Hash.GET_ENTITY_MODEL, prop.Handle); // Physical type3 only; never Building.Model.
        return model != 0;
    }

    private static bool C4SofaSphereIntersects(Prop prop, Vector3 center, float radius, Vector3 min, Vector3 max)
    {
        Vector3 local = prop.GetPositionOffset(center);
        if (!C4PropsFinite(local) || !C4PropsFinite(min) || !C4PropsFinite(max))
            throw new InvalidOperationException("C04 sofa: bounds/offset nao finitos.");
        float x = System.Math.Max(min.X - local.X, System.Math.Max(0f, local.X - max.X));
        float y = System.Math.Max(min.Y - local.Y, System.Math.Max(0f, local.Y - max.Y));
        float z = System.Math.Max(min.Z - local.Z, System.Math.Max(0f, local.Z - max.Z));
        return x * x + y * y + z * z <= radius * radius;
    }

    private void BeginC4SofaHideTest()
    {
        const int modelHash = 1181479993; // v_res_m_h_sofa: Native166/167 + installed PropList/Jenkins.
        const string sourceHash = "F74235C9A2DD9505606805B6EEAA55253A11199020A9E8ACAB4ED2E173EE0DE1";
        Ped actor = C4OwnedActor();
        RequireC4Viewport();
        if (c4SofaHideRestore.Count != 0 || countingDown || playing ||
            (!Object.ReferenceEquals(directorCapture, null) && (directorCapture.IsArmed || directorCapture.IsPending)) ||
            FlightSourceHash(Path.Combine(scenesDirectory, selectedSceneFile)) != sourceHash ||
            c4OwnViewportInterior != 166657 || c4OwnViewportRoom != -1537723083 ||
            actor.CurrentInteriorRoomKey != -1537723083 || Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, actor.Handle) != 166657)
            throw new InvalidOperationException("C04 sofa: exige uma unica montagem F742/Rich8/room proprio PREP sem captura.");
        if (!Object.ReferenceEquals(directorCapture, null)) directorCapture.RequireNoWriterForFinalCleanup();
        else CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
        string folder = Path.Combine(dataRoot, "logs");
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("C04 sofa: pasta de journals nao pode ser vinculo.");
        foreach (string previous in Directory.GetFiles(folder, "c4-sofa-hide-*.before.xml"))
            if (!File.Exists(previous.Substring(0, previous.Length - ".before.xml".Length) + ".restored.xml"))
                throw new InvalidOperationException("C04 sofa: journal anterior sem prova de restauro; preservar e resolver antes de novo hide.");
        Prop[] pool = World.GetNearbyProps(actor.Position, 6f);
        if (Object.ReferenceEquals(pool, null) || pool.Length > 64)
            throw new InvalidOperationException("C04 sofa: pool fisica ausente/truncada; nenhum hide.");
        Prop selected = null;
        foreach (Prop prop in pool)
        {
            int model;
            if (!C4SofaPhysical(prop, out model) || model != modelHash) continue;
            if ((prop.Position - new Vector3(-814.4721f, 181.9439f, 71.15505f)).Length() > 0.03f) continue;
            if (!Object.ReferenceEquals(selected, null)) throw new InvalidOperationException("C04 sofa: pivot medido nao e unico.");
            selected = prop;
        }
        int observedModel;
        if (Object.ReferenceEquals(selected, null) || !C4SofaPhysical(selected, out observedModel) || observedModel != modelHash ||
            Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, selected.Handle) != 166657 ||
            Function.Call<int>(Hash.GET_ROOM_KEY_FROM_ENTITY, selected.Handle) != -1537723083 || !selected.IsVisible)
            throw new InvalidOperationException("C04 sofa: modelo/pivot/sala/visibilidade original nao confirmados.");
        Vector3 min, max; new Model(modelHash).GetDimensions(out min, out max);
        if ((min - new Vector3(-1.56993f, -0.5457183f, -0.000324f)).Length() > 0.01f ||
            (max - new Vector3(1.56993f, 0.5430976f, 1.304868f)).Length() > 0.01f || (max - min).Length() > 4f)
            throw new InvalidOperationException("C04 sofa: bounds reais divergiram do modelo isolado medido; nenhum hide.");
        Vector3 center = selected.GetOffsetPosition((min + max) * 0.5f);
        if (!C4PropsFinite(center)) throw new InvalidOperationException("C04 sofa: centro medido invalido.");
        Prop[] spherePool = World.GetNearbyProps(center, 6f);
        if (Object.ReferenceEquals(spherePool, null) || spherePool.Length > 64)
            throw new InvalidOperationException("C04 sofa: pool da esfera truncada; nenhum hide.");
        int matching = 0;
        foreach (Prop prop in spherePool)
        {
            int model;
            if (!C4SofaPhysical(prop, out model) || model != modelHash) continue;
            if (C4SofaSphereIntersects(prop, center, 0.1f, min, max))
            {
                matching++;
                if (prop.Handle != selected.Handle || prop.MemoryAddress != selected.MemoryAddress)
                    throw new InvalidOperationException("C04 sofa: outro objeto do mesmo modelo intersecta o filtro; nenhum hide.");
            }
        }
        if (matching != 1) throw new InvalidOperationException("C04 sofa: esfera pequena nao tem um unico sofa fisico.");
        var record = new C4SofaHideRecord { Handle = selected.Handle, Address = selected.MemoryAddress, Model = modelHash,
            Pivot = selected.Position, Rotation = selected.Rotation, Min = min, Max = max, Center = center, Radius = 0.1f,
            Physics = selected.HasPhysics, Collision = selected.IsCollisionEnabled, Frozen = selected.IsPositionFrozen,
            Journal = Path.Combine(folder, "c4-sofa-hide-" + Guid.NewGuid().ToString("N")) };
        Vector3 actorPosition = actor.Position, actorRotation = actor.Rotation;
        Vector3 cameraPosition = Function.Call<Vector3>(Hash.GET_CAM_COORD, c4RoomCameraHandle);
        float groundBefore, groundAfter;
        if (!World.GetGroundHeight(actor.Position + new Vector3(0f, 0f, 0.25f), out groundBefore, GetGroundHeightMode.Normal))
            throw new InvalidOperationException("C04 sofa: chao proprio original nao confirmou antes do hide.");
        if (!C4SofaPhysical(selected, out observedModel) || observedModel != record.Model || selected.Handle != record.Handle ||
            selected.MemoryAddress != record.Address || !selected.IsVisible || (selected.Position - record.Pivot).Length() > 0.005f)
            throw new InvalidOperationException("C04 sofa: identidade/visibilidade mudou antes do registro; nenhum hide.");
        C4WriteSofaJournal(record.Journal + ".before.xml", C4SofaJournal(record, "restore_required_before_native", 0));
        c4SofaHideRestore.Add(record); // Obrigacao registrada ANTES da chamada; nao remover no catch.
        WriteLog("C04_SOFA_RESTORE_REGISTRADO", "Model=" + record.Model + "; Handle=" + record.Handle + "; Address=" + record.Address +
            "; Pivot=" + record.Pivot + "; Center=" + record.Center + "; Radius=0.1; Room=-1537723083; PhysicsBefore=" + record.Physics +
            "; CollisionBefore=" + record.Collision + "; FrozenBefore=" + record.Frozen + "; VisibleBefore=True; SameModelIntersections=1; Journal=" + record.Journal +
            "; NoOwnershipGetter=True; PreExistingHideGlobalAbsenceUnproven=True; RenderProofPending=True; TestOnly=True");
        Function.Call((Hash)0x3A52AE588830BF7FUL, center.X, center.Y, center.Z, record.Radius, record.Model, false);
        RequireC4Viewport();
        if ((actor.Position - actorPosition).Length() > 0.005f || (actor.Rotation - actorRotation).Length() > 0.001f ||
            (Function.Call<Vector3>(Hash.GET_CAM_COORD, c4RoomCameraHandle) - cameraPosition).Length() > 0.005f ||
            !World.GetGroundHeight(actorPosition + new Vector3(0f, 0f, 0.25f), out groundAfter, GetGroundHeightMode.Normal) ||
            System.Math.Abs(groundAfter - groundBefore) > 0.01f)
            throw new InvalidOperationException("C04 sofa: ator/camera/chao proprio mudou apos hide; restauro obrigatorio.");
        bool existsAfter = selected.Exists() && Function.Call<bool>(Hash.DOES_ENTITY_EXIST, record.Handle);
        int typeAfter = existsAfter ? Function.Call<int>(Hash.GET_ENTITY_TYPE, record.Handle) : 0;
        WriteLog("C04_SOFA_HIDE_SOLICITADO", "Model=" + record.Model + "; Center=" + center + "; Radius=0.1; SurviveMapReload=False;" +
            " NativeReturnedVoid=True; ExistsAfter=" + existsAfter + "; TypeAfter=" + typeAfter +
            "; VisibleGetterAfter=" + (existsAfter && typeAfter == 3 && selected.IsVisible) +
            "; GroundBefore=" + groundBefore.ToString("R", CultureInfo.InvariantCulture) + "; GroundAfter=" + groundAfter.ToString("R", CultureInfo.InvariantCulture) +
            "; BodyCameraChanged=False; OtherModelsUntouched=True; HideEffectUnprovenUntilRootImage=True; EmptyRoomApproved=False; ARM_F7Blocked=True");
    }

    private void RestoreC4SofaHide()
    {
        if (c4SofaHideRestore.Count == 0) return;
        if (!Object.ReferenceEquals(directorCapture, null)) directorCapture.RequireNoWriterForFinalCleanup();
        else CapturaDiretor.RequireNoWriterInCaptureDirectory(finalCleanupCaptureRoot, WriteLog);
        for (int index = c4SofaHideRestore.Count - 1; index >= 0; index--)
        {
            C4SofaHideRecord record = c4SofaHideRestore[index];
            if (!record.RemoveReturned)
            {
                if (!File.Exists(record.Journal + ".remove.xml"))
                    C4WriteSofaJournal(record.Journal + ".remove.xml", C4SofaJournal(record, "remove_requested_exact_scope_lazy_false", 0));
                Function.Call((Hash)0xD9E3006FB3CBD765UL, record.Center.X, record.Center.Y, record.Center.Z, record.Radius, record.Model, false);
                record.RemoveReturned = true;
                WriteLog("C04_SOFA_REMOVE_SOLICITADO", "Model=" + record.Model + "; Center=" + record.Center + "; Radius=" + record.Radius.ToString("R", CultureInfo.InvariantCulture) +
                    "; Lazy=False; ExactRecordedScope=True; NativeReturnedVoid=True; GetterProofPending=True");
            }
            Prop[] nearby = World.GetNearbyProps(record.Pivot, 2f);
            if (Object.ReferenceEquals(nearby, null) || nearby.Length > 64)
                throw new InvalidOperationException("C04 sofa: pool de restauro nao confirmou; conservar journal/registro para F10.");
            Prop restored = null;
            foreach (Prop prop in nearby)
            {
                int model;
                if (!C4SofaPhysical(prop, out model) || model != record.Model || (prop.Position - record.Pivot).Length() > 0.03f) continue;
                if (!Object.ReferenceEquals(restored, null)) throw new InvalidOperationException("C04 sofa: restauro ambiguo; registro conservado.");
                restored = prop;
            }
            int observedModel;
            if (Object.ReferenceEquals(restored, null) || !C4SofaPhysical(restored, out observedModel) ||
                observedModel != record.Model || !restored.IsVisible ||
                Function.Call<int>(Hash.GET_INTERIOR_FROM_ENTITY, restored.Handle) != 166657 ||
                Function.Call<int>(Hash.GET_ROOM_KEY_FROM_ENTITY, restored.Handle) != -1537723083 ||
                (restored.Rotation - record.Rotation).Length() > 0.02f || restored.HasPhysics != record.Physics ||
                restored.IsCollisionEnabled != record.Collision || restored.IsPositionFrozen != record.Frozen)
                throw new InvalidOperationException("C04 sofa: modelo/pivot/room/visibilidade/flags originais ainda nao restaurados; registro conservado para F10.");
            Vector3 min, max; new Model(record.Model).GetDimensions(out min, out max);
            if ((min - record.Min).Length() > 0.01f || (max - record.Max).Length() > 0.01f)
                throw new InvalidOperationException("C04 sofa: bounds de restauro divergiram; journal conservado.");
            C4WriteSofaJournal(record.Journal + ".restored.xml", C4SofaJournal(record, "restore_getters_verified_root_render_review_pending", restored.Handle));
            WriteLog("C04_SOFA_RESTAURADO_GETTERS", "Model=" + record.Model + "; OriginalHandle=" + record.Handle + "; RestoredHandle=" + restored.Handle +
                "; OriginalAddress=" + record.Address + "; RestoredAddress=" + restored.MemoryAddress + "; Pivot=" + restored.Position +
                "; FlagsOriginal=True; VisibleGetter=True; SameRoom=True; ExactRemoveScope=True; NoOwnershipGetter=True; RenderRestoreApprovalPending=True");
            c4SofaHideRestore.RemoveAt(index);
        }
    }

    private static void C4WriteSofaJournal(string path, string content)
    {
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(content);
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
    }

    private static string C4SofaJournal(C4SofaHideRecord record, string state, int restoredHandle)
    {
        return "<C4SofaHide versao=\"1\" sourceSHA256=\"F74235C9A2DD9505606805B6EEAA55253A11199020A9E8ACAB4ED2E173EE0DE1\" estado=\"" + state +
            "\" modelo=\"" + record.Model + "\" handleOriginal=\"" + record.Handle + "\" restoredHandle=\"" + restoredHandle +
            "\" centerX=\"" + record.Center.X.ToString("R", CultureInfo.InvariantCulture) + "\" centerY=\"" + record.Center.Y.ToString("R", CultureInfo.InvariantCulture) +
            "\" centerZ=\"" + record.Center.Z.ToString("R", CultureInfo.InvariantCulture) + "\" radius=\"0.1\" surviveMapReload=\"false\" lazyRemove=\"false\"" +
            " pivotX=\"" + record.Pivot.X.ToString("R", CultureInfo.InvariantCulture) + "\" pivotY=\"" + record.Pivot.Y.ToString("R", CultureInfo.InvariantCulture) +
            "\" pivotZ=\"" + record.Pivot.Z.ToString("R", CultureInfo.InvariantCulture) + "\" noOwnershipGetter=\"true\"" +
            " originalAddress=\"" + record.Address + "\" physicsOriginal=\"" + record.Physics.ToString().ToLowerInvariant() +
            "\" collisionOriginal=\"" + record.Collision.ToString().ToLowerInvariant() + "\" frozenOriginal=\"" + record.Frozen.ToString().ToLowerInvariant() +
            "\" visibleOriginal=\"true\" interiorOriginal=\"166657\" roomOriginal=\"-1537723083\"" +
            " rotX=\"" + record.Rotation.X.ToString("R", CultureInfo.InvariantCulture) + "\" rotY=\"" + record.Rotation.Y.ToString("R", CultureInfo.InvariantCulture) +
            "\" rotZ=\"" + record.Rotation.Z.ToString("R", CultureInfo.InvariantCulture) + "\" minX=\"" + record.Min.X.ToString("R", CultureInfo.InvariantCulture) +
            "\" minY=\"" + record.Min.Y.ToString("R", CultureInfo.InvariantCulture) + "\" minZ=\"" + record.Min.Z.ToString("R", CultureInfo.InvariantCulture) +
            "\" maxX=\"" + record.Max.X.ToString("R", CultureInfo.InvariantCulture) + "\" maxY=\"" + record.Max.Y.ToString("R", CultureInfo.InvariantCulture) +
            "\" maxZ=\"" + record.Max.Z.ToString("R", CultureInfo.InvariantCulture) + "\"" +
            " dataUtc=\"" + DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\" />";
    }
    private void RestoreC4Viewport()
    {
        if (c4ViewportChanged)
        {
            int currentInterior = C4ViewportInterior(), currentRoom = Interior.GetRoomKeyForGameViewport();
            bool original = currentInterior == c4SavedViewportInterior && currentRoom == c4SavedViewportRoom;
            bool own = C4ObservedOwnedRoom(currentInterior, currentRoom);
            LogC4ViewportRaw("RAW_BEFORE_RESTORE");
            if (!C4ExteriorViewport(currentInterior, currentRoom) && !original && !own)
                throw new InvalidOperationException("C04 room: sala alheia observada; restauro proprio pendente sem modifica-la.");
            bool originalExterior = C4ExteriorViewport(c4SavedViewportInterior, c4SavedViewportRoom);
            if (originalExterior)
                Function.Call((Hash)0x23B59D8912F94246UL); // Retira somente override proprio; nao inventa hash da sala exterior.
            else
            {
                if (!C4RestorableViewport(c4SavedViewportInterior, c4SavedViewportRoom))
                    throw new InvalidOperationException("C04 room: proxy original nao existe/pronto; conservar snapshot para repetir limpeza.");
                Interior.ForceRoomForGameViewport(InteriorProxy.FromHandle(c4SavedViewportInterior), c4SavedViewportRoom);
            }
            int restoredInterior = C4ViewportInterior(), restoredRoom = Interior.GetRoomKeyForGameViewport();
            bool restored = originalExterior ? C4ExteriorViewport(restoredInterior, restoredRoom) :
                restoredInterior == c4SavedViewportInterior && restoredRoom == c4SavedViewportRoom;
            WriteLog("C04_VIEWPORT_RESTAURO_MEDIDO", "InteriorOriginal=" + c4SavedViewportInterior +
                "; RoomOriginalRaw=" + c4SavedViewportRoom + "; InteriorActual=" + restoredInterior +
                "; RoomActualRaw=" + restoredRoom + "; ExteriorEquivalent=" +
                (originalExterior && C4ExteriorViewport(restoredInterior, restoredRoom)) + "; Restored=" + restored);
            if (!restored)
                throw new InvalidOperationException("C04 room: restauro Native ainda nao confirmado; repetir limpeza.");
            WriteLog("C04_VIEWPORT_RESTAURADA", "InteriorOriginal=" + c4SavedViewportInterior +
                "; RoomOriginalRaw=" + c4SavedViewportRoom + "; RoomActualRaw=" + restoredRoom);
        }
        c4ViewportSaved = c4ViewportChanged = false;
        c4ViewportPending = c4ViewportForceReturned = false; c4RoomCameraHandle = 0;
        c4ViewportRequestedFrame = c4ViewportRequestedTime = 0;
        c4SavedViewportInterior = c4SavedViewportRoom = c4OwnViewportInterior = c4OwnViewportRoom = 0;
        c4BeforePoseInterior = c4BeforePoseRoom = c4RoomActorHandle = 0;
    }

    private bool Cleanup()
    {
        finalClearRequestedForOwnedSeats = false;
        if (!RestoreShellModelsTrial("Limpeza/F10/erro/Aborted"))
        { WriteLog("LIMPEZA", "Concluída=False; entidades restantes=" + createdPeds.Count + "; ShellModelRequestsPending=True"); return false; }
        if (!RestoreShellTrial("Limpeza/F10/erro/Aborted"))
        { WriteLog("LIMPEZA", "Concluída=False; entidades restantes=" + createdPeds.Count + "; ShellSnapshotPending=True"); return false; }
        CancelMesaTrial("Ensaio mesa cancelado por limpeza, erro ou recarga.");
        bool boateLightsClean = true;
        if (!Object.ReferenceEquals(directorBoateLights, null))
        {
            try { boateLightsClean = directorBoateLights.Cleanup(); if (boateLightsClean) directorBoateLights = null; }
            catch (Exception ex) { boateLightsClean = false; WriteLog("ERRO_RESTAURO_BANDS_BOATE", ex.ToString()); }
        }
        bool localDoorClean = true;
        if (!Object.ReferenceEquals(directorLocalOperation, null))
        {
            try { directorLocalOperation.Cancel(); if (!directorLocalOperation.CleanupDoors()) { localDoorClean = false; WriteLog("PORTA_LOCAL_RESTAURO_PENDENTE", "O estado original da porta ainda exige restauro pela operacao local."); } }
            catch (Exception ex) { localDoorClean = false; WriteLog("ERRO_CANCELAR_OPERACAO_LOCAL", ex.ToString()); }
        }
        controllerMarkReleased = false;
        controllerMarkHolding = false;
        controllerMarkConsuming = false;
        controllerLocationMode = false;
        StopAutomaticRecording();
        bool castClean = true;
        if (!Object.ReferenceEquals(directorCapture, null))
        {
            try { directorCapture.Cancel("limpeza, erro ou recarga"); }
            catch (Exception ex) { WriteLog("ERRO_CANCELAR_CAPTURA", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorCast, null))
        {
            try { directorCast.Cleanup(); }
            catch (Exception ex) { castClean = false; WriteLog("ERRO_LIMPEZA_PREPARACAO_ELENCO", ex.ToString()); }
        }
        mountAfterCast = false;
        subtitleText = "";
        statusText = "";
        countingDown = false;
        takeFinished = false;
        awaitingCaptureStart = false; captureStartTimer = null; preparedCaptureSrt = null;
        awaitingCaptureEnd = false; captureEndTimer = null;
        playing = false;
        sceneReady = false;
        loadingTimer = null;
        awaitingCollision = false;
        awaitingVisual = false;
        awaitingPilot = false;
        awaitingRehearsal = false;
        awaitingEpisodeOne = false; awaitingEpisodeReset = false; episodeResetTimer = null;
        awaitingThreeSeven = false; awaitingSeasonFlight = false; awaitingVehicles = false; awaitingDoors = false;
        awaitingPlanePush = false; awaitingWardrobe = false; awaitingBoateLights = false; awaitingEntry = false; awaitingCartaTrial = false; awaitingGorjetaTrial = false;
        awaitingSeasonWater = false; awaitingTableWipe = false;
        nextWaterCameraTime = 0;
        awaitingStaging = false;
        awaitingInterpretation = false;
        awaitingProps = false;
        awaitingPoses = false;
        awaitingPoseReset = false;
        actionAfterPoseReset = false;
        cameraAfterPoseReset = false;
        areaPreparationTimer = null;
        bool resourcesClean = boateLightsClean;
        resourcesClean &= TryRestore("sofa proprio C04 model-hide", RestoreC4SofaHide);
        if (!Object.ReferenceEquals(directorWritingC6, null))
        {
            try { directorWritingC6.Dispose(); directorWritingC6 = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_ESCRITA_C6", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorWriting85, null))
        {
            try { directorWriting85.Dispose(); directorWriting85 = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_ESCRITA85", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorTableWipe, null))
        {
            try
            {
                if (!directorTableWipe.Cleanup()) throw new InvalidOperationException("Ray proprio de limpeza pendente; conservar mesa/ator para repetir limpeza.");
                directorTableWipe.Dispose(); directorTableWipe = null;
            }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_MESA", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorCartaTrial, null))
        {
            try
            {
                if (!ReleaseCartaTrialForReset()) throw new InvalidOperationException("Limpeza propria do ensaio carta pendente.");
                cartaTrialSpec = null;
            }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_CARTA_TRIAL", ex.ToString()); }
        }
        else cartaTrialSpec = null;
        if (!Object.ReferenceEquals(directorGorjetaTrial, null))
        {
            try
            {
                if (!ReleaseGorjetaTrialForReset()) throw new InvalidOperationException("Limpeza propria da gorjeta pendente; conservar mesa/atriz.");
                gorjetaTrialSpec = null;
            }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_GORJETA_TRIAL", ex.ToString()); }
        }
        else gorjetaTrialSpec = null;
        if (!Object.ReferenceEquals(directorSeasonWater, null))
        {
            try { directorSeasonWater.Dispose(); directorSeasonWater = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_AGUA_TEMPORADA", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorTransientOutfit, null))
        {
            try { directorTransientOutfit.Dispose(); directorTransientOutfit = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_SAPATO_TRANSITORIO", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorThreeSeven, null))
        {
            try { directorThreeSeven.Dispose(); directorThreeSeven = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_ONIBUS_TEMPORADA", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorSeasonFlight, null))
        {
            try { directorSeasonFlight.Dispose(); directorSeasonFlight = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_VOO_TEMPORADA", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorPlanePush, null))
        {
            try { directorPlanePush.Dispose(); directorPlanePush = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_EMPURRAO_AVIAO", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorWardrobe, null))
        {
            try { directorWardrobe.Dispose(); directorWardrobe = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_ARMARIO", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorEntry, null))
        {
            try { directorEntry.Dispose(); directorEntry = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_ENTRADA_AUTOR", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorVehicles, null))
        {
            try { directorVehicles.Dispose(); directorVehicles = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_VEICULOS_TEMPORADA", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorDoors, null))
        {
            try { directorDoors.Dispose(); directorDoors = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_PORTAS_TEMPORADA", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorEpisodeOne, null))
        {
            try { directorEpisodeOne.Dispose(); directorEpisodeOne = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_EP01", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorPilot, null))
        {
            try { directorPilot.Dispose(); directorPilot = null; }
            catch (Exception ex)
            {
                WriteLog("ERRO_LIMPEZA_PILOTO", ex.ToString());
                resourcesClean = false;
            }
        }
        if (!Object.ReferenceEquals(directorRehearsal, null))
        {
            try { directorRehearsal.Dispose(); directorRehearsal = null; }
            catch (Exception ex)
            {
                WriteLog("ERRO_LIMPEZA_ENSAIO", ex.ToString());
                resourcesClean = false;
            }
        }
        if (!Object.ReferenceEquals(directorStaging, null))
        {
            try { directorStaging.Dispose(); directorStaging = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_DIRECAO", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorInterpretation, null))
        {
            try { directorInterpretation.Dispose(); directorInterpretation = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_INTERPRETACAO", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorPoses, null))
        {
            try { directorPoses.Dispose(); directorPoses = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_POSES", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorAuthoredOpening99, null))
        {
            try { directorAuthoredOpening99.Dispose(); directorAuthoredOpening99 = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_PAPEL_ABRE99", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorMapOpening99, null))
        {
            try { directorMapOpening99.Dispose(); directorMapOpening99 = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_MAPA99", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorOpening99, null)) { directorOpening99.StopWindow(); directorOpening99 = null; }
        if (!Object.ReferenceEquals(directorPocket98,null))
        {
            try { directorPocket98.Dispose(); if (!Object.ReferenceEquals(directorProps,null)) directorProps.UnbindPocket98(directorPocket98); directorPocket98=null; }
            catch (Exception ex) { resourcesClean=false; WriteLog("ERRO_LIMPEZA_BOLSO98",ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorAuthoredPaperC5, null))
        {
            try
            {
                directorAuthoredPaperC5.Dispose();
                if (!Object.ReferenceEquals(directorProps, null)) directorProps.UnbindAuthoredPaperC5(directorAuthoredPaperC5);
                directorAuthoredPaperC5 = null;
            }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_PAPEL_AUTORAL_C5", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorFoldPaper86, null))
        {
            try { directorFoldPaper86.Dispose(); directorFoldPaper86 = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_PAPER_POLY86", ex.ToString()); }
        }
        if (!Object.ReferenceEquals(directorProps, null) && Object.ReferenceEquals(directorPocket98, null) && Object.ReferenceEquals(directorAuthoredPaperC5, null) && Object.ReferenceEquals(directorFoldPaper86, null) && Object.ReferenceEquals(directorAuthoredOpening99, null) && Object.ReferenceEquals(directorPoses, null) && Object.ReferenceEquals(directorWardrobe, null) && Object.ReferenceEquals(directorEntry, null) && Object.ReferenceEquals(directorCartaTrial, null) && Object.ReferenceEquals(directorGorjetaTrial, null) && Object.ReferenceEquals(directorTableWipe, null))
        {
            try { directorProps.Dispose(); directorProps = null; }
            catch (Exception ex) { resourcesClean = false; WriteLog("ERRO_LIMPEZA_OBJETOS", ex.ToString()); }
        }
        if (!resourcesClean)
        {
            if (!Object.ReferenceEquals(directorCamera, null)) TryRestore("câmera", delegate { directorCamera.Cleanup(); });
            if (!Object.ReferenceEquals(directorEnvironment, null)) TryRestore("limpeza ambiental", delegate { directorEnvironment.Cleanup(); });
            RestorePlayer();
            RestoreEnvironment();
            TryRestore("viewport proprio C04", RestoreC4Viewport);
            WriteLog("LIMPEZA_PARCIAL", "Execução desativada; recursos próprios conservados para repetir F10.");
            return false;
        }
        segmentIndex = -1;
        sceneReady = false;
        loadingTimer = null;
        awaitingCollision = false;
        awaitingVisual = false;
        awaitingPilot = false;
        awaitingRehearsal = false;
        awaitingStaging = false;
        areaPreparationTimer = null;
        initialPositions.Clear();
        initialHeadings.Clear();
        stagingTaskRevisions.Clear();
        actionDestinations.Clear();
        turnStartHeadings.Clear();
        turnTargetHeadings.Clear();
        startedActions.Clear();
        finishedActions.Clear();
        cutIndex = -1;
        bool complete = castClean && localDoorClean && boateLightsClean;
        if (!Object.ReferenceEquals(directorCamera, null))
            complete &= TryRestore("câmera", delegate { directorCamera.Cleanup(); });
        if (!Object.ReferenceEquals(directorEnvironment, null))
            complete &= TryRestore("limpeza ambiental", delegate { directorEnvironment.Cleanup(); });
        complete &= RestorePlayer();
        complete &= TryRestore("viewport proprio C04", RestoreC4Viewport);
        for (int i = createdPeds.Count - 1; i >= 0; i--)
        {
            try
            {
                Ped ped = createdPeds[i];
                VariantesDjaniraDiretor.Forget(ped);
                VisualDiretor.Forget(ped);
                int handle = ped.Handle;
                if (ped.Exists())
                    ped.Delete();
                if (handle != 0 && Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle))
                {
                    // Delete zera o wrapper mesmo se a entidade sobreviver; conservar para repetir F10.
                    createdPeds[i] = Entity.FromHandle(handle) as Ped;
                    throw new InvalidOperationException("Entidade ainda existe após remover: " + handle);
                }
                createdPeds.RemoveAt(i);
            }
            catch (Exception ex)
            {
                complete = false;
                WriteLog("ERRO_LIMPEZA_PERSONAGEM", ex.ToString());
            }
        }
        try { ReleaseModels(); }
        catch (Exception ex) { complete = false; WriteLog("ERRO_LIBERAR_MODELOS", ex.ToString()); }
        actorsById.Clear();
        gestures.Clear();
        foreach (string dictionary in requestedAnimations)
        {
            try { Function.Call(Hash.REMOVE_ANIM_DICT, dictionary); }
            catch (Exception ex) { complete = false; WriteLog("ERRO_LIBERAR_ANIMACAO", dictionary + ": " + ex); }
        }
        requestedAnimations.Clear();
        complete &= RestoreEnvironment();
        if (createdPeds.Count == 0) complete &= TryRestore("IPLs proprios do local", RestoreMarkedSceneIpls);
        dismissalInteriorBeforeFloor = 0;
        dismissalInteriorRefreshedBeforeFloor = dismissalInteriorBeforeFloorLogged = false;
        dismissalInteriorBeforeFloorLastLogMs = 0;
        activeScene = null;
        WriteLog("LIMPEZA", "Concluída=" + complete + "; entidades restantes=" + createdPeds.Count);
        return complete;
    }

    private bool RestoreEnvironment()
    {
        bool complete = true;
        if (environmentSaved)
        {
            complete &= TryRestore("hora", delegate { GameClock.Now = savedClock; });
            complete &= TryRestore("pausa do relógio", delegate { GameClock.IsPaused = savedClockPaused; });
            complete &= TryRestore("clima", delegate {
                Function.Call(Hash.SET_CURR_WEATHER_STATE, savedWeatherFrom, savedWeatherTo, savedWeatherBlend);
            });
            if (restorePolice)
                complete &= TryRestore("polícia", delegate {
                    Game.Player.Wanted.SetPoliceIgnorePlayer(savedPoliceIgnore);
                    Game.Player.Wanted.DispatchesCopsForPlayer = savedDispatchCops;
                    Game.Player.Wanted.SetWantedLevel(savedWantedLevel, false);
                    Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
                });
            if (complete)
                environmentSaved = false;
        }
        return complete;
    }

    private bool CanRecordAutomatically()
    {
        return captureEnabled && automaticRecording && !automaticRecordingDisabled &&
            !Object.ReferenceEquals(directorCapture, null) && directorCapture.HasVerifiedSave;
    }

    private void StopAutomaticRecording()
    {
        // Todo cancelamento/erro abandona a cauda imediatamente, mesmo sem captura ativa.
        awaitingCaptureEnd = false; captureEndTimer = null;
        if (awaitingCaptureStart)
        {
            // Cancelamento durante a inicialização também exige prova de captura;
            // um atalho aceito sozinho não autoriza enviar outro toggle.
            try { automaticShortcutActive = !String.IsNullOrEmpty(directorCapture.GetStartedCapture()); }
            catch (Exception ex) { WriteLog("ERRO_CONFERIR_INICIO_CAPTURA", ex.ToString()); automaticShortcutActive = false; }
            if (!automaticShortcutActive) automaticRecordingDisabled = true;
            awaitingCaptureStart = false; captureStartTimer = null; preparedCaptureSrt = null;
            automaticCapturePending = false;
        }
        if (!automaticShortcutActive) return;
        // Uma tentativa só: repetir o toggle pode iniciar outra gravação.
        automaticShortcutActive = false;
        try
        {
            if (String.IsNullOrEmpty(directorCapture.GetStartedCapture()))
            {
                WriteLog("ATALHO_GRAVACAO_FIM_IGNORADO", "Nenhum MP4 novo ainda em escrita; não repetir toggle sobre captura já parada.");
                return;
            }
        }
        catch (Exception ex)
        {
            automaticRecordingDisabled = true;
            WriteLog("ERRO_CONFERIR_FIM_CAPTURA", ex.ToString());
            return;
        }
        try { AtalhoGravacaoDiretor.Toggle(); WriteLog("ATALHO_GRAVACAO_FIM", "Atalho enviado; MP4 real ainda precisa ser verificado."); }
        catch (Exception ex)
        {
            automaticRecordingDisabled = true;
            WriteLog("ATALHO_GRAVACAO_FALHOU", ex.ToString());
            ShowMessage("O atalho automático falhou. Confira a Barra de Jogos e pare com Win+Alt+R; use Home para gravar manualmente.");
        }
    }

    private bool TryRestore(string name, Action action)
    {
        try { action(); return true; }
        catch (Exception ex) { WriteLog("ERRO_RESTAURAR_AMBIENTE", name + ": " + ex); return false; }
    }

    private SceneData ReadScene(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("Arquivo de cena não encontrado: " + path);
        string schemaPath = Path.Combine(projectRoot, "esquemas", "cena-v1.xsd");
        if (!File.Exists(schemaPath))
            throw new InvalidOperationException("Esquema de cena não encontrado: " + schemaPath);
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Prohibit;
        settings.XmlResolver = null;
        settings.MaxCharactersInDocument = 100000;
        settings.ValidationType = ValidationType.Schema;
        settings.Schemas.XmlResolver = null;
        settings.Schemas.Add(null, schemaPath);
        List<string> errors = new List<string>();
        settings.ValidationEventHandler += delegate(object sender, ValidationEventArgs e) {
            errors.Add("Linha " + (e.Exception == null ? 0 : e.Exception.LineNumber) + ": " + e.Message);
        };
        XmlDocument document = new XmlDocument();
        document.XmlResolver = null;
        CenaJsonDiretor.Resultado json = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ?
            CenaJsonDiretor.Load(path, Path.Combine(dataRoot, "catalogos"), cleanupRadius) : null;
        using (XmlReader reader = Object.ReferenceEquals(json, null) ? XmlReader.Create(path, settings) :
            XmlReader.Create(new StringReader(json.Document.OuterXml), settings))
            document.Load(reader);
        if (errors.Count > 0)
            throw new InvalidOperationException("Cena inválida no esquema: " + String.Join("; ", errors));
        XmlElement root = document.DocumentElement;
        XmlElement environment = (XmlElement)root.SelectSingleNode("Ambiente");
        XmlElement local = (XmlElement)root.SelectSingleNode("Local");
        SceneData scene = new SceneData();
        scene.Id = Object.ReferenceEquals(json, null) ? root.GetAttribute("id") : json.Id;
        scene.Name = root.GetAttribute("nome");
        scene.Encenacao = Object.ReferenceEquals(json, null) ? "" : json.Encenacao;
        scene.CleanupRadius = cleanupRadius;
        scene.Hour = IntAttribute(environment, "hora");
        scene.Minute = IntAttribute(environment, "minuto");
        scene.WeatherName = environment.GetAttribute("clima");
        scene.Traffic = FloatAttribute(environment, "densidadeTransito");
        scene.PedestrianDensity = FloatAttribute(environment, "densidadePedestres");
        scene.BlockPolice = environment.GetAttribute("bloquearPolicia") == "true";
        scene.ClearArea = environment.GetAttribute("limparArea") == "true";
        scene.Distance = FloatAttribute(local, "distanciaFrente");
        scene.MarkedLocation = local.GetAttribute("referencia") == "marcado";
        scene.LocalId = local.GetAttribute("id");
        if (scene.MarkedLocation && !IsLocalId(scene.LocalId))
            throw new InvalidOperationException("Local/id: o local marcado exige um código de 1 a 48 letras maiúsculas, números ou _.");
        if (!scene.MarkedLocation && local.HasAttribute("id"))
            throw new InvalidOperationException("Local/id: não use id quando referencia=jogador.");
        foreach (XmlElement element in root.SelectNodes("Personagens/Personagem"))
        {
            ActorData actor = new ActorData();
            actor.Id = element.GetAttribute("id");
            actor.Name = element.GetAttribute("nome");
            actor.ModelName = element.GetAttribute("modelo");
            actor.Right = FloatAttribute(element, "direita");
            actor.Forward = FloatAttribute(element, "frente");
            actor.Up = FloatAttribute(element, "altura");
            actor.LookAt = element.GetAttribute("olharPara");
            if (!Object.ReferenceEquals(json, null)) json.Visuais.TryGetValue(actor.Id, out actor.Visual);
            float rotation;
            if (!Object.ReferenceEquals(json, null) && json.RotacoesIniciais.TryGetValue(actor.Id, out rotation)) actor.Rotation = rotation;
            if (actor.Id == "djanira_rica")
            {
                actor.RichOption = scene.Id == "teste_fila_94" ? 1 : scene.Id == "teste_fila_95" ? 2 :
                    scene.Id == "teste_fila_96" ? 3 : scene.Id == "teste_fila_91" ? 1 : 0;
                if (scene.Id == "teste_fila_91")
                {
                    XmlDocument canonical = new XmlDocument(); canonical.XmlResolver = null;
                    canonical.Load(Path.Combine(dataRoot, "catalogos", "visuais", "DJANIRA_RICA.xml"));
                    if (!Object.ReferenceEquals(canonical.SelectSingleNode("/OutfitPedData/DiretorOpcao"), null)) actor.RichOption = 0;
                }
                if (actor.RichOption > 0 && Object.ReferenceEquals(actor.Visual, null))
                    throw new InvalidOperationException("As opções exigem o XML completo da Djanira rica já registrada.");
            }
            if (actor.LookAt == actor.Id)
                throw new InvalidOperationException("O personagem " + actor.Id + " não pode olhar para si mesmo.");
            foreach (ActorData other in scene.Actors)
            {
                if (System.Math.Abs(other.Right - actor.Right) < 0.5f && System.Math.Abs(other.Forward - actor.Forward) < 0.5f)
                    throw new InvalidOperationException("Personagens muito próximos: " + other.Id + " e " + actor.Id + ".");
            }
            scene.Actors.Add(actor);
        }
        scene.CameraActor = scene.Actors.Count == 0 ? "" : scene.Actors[0].Id;
        string ensemblePreset = scene.Actors.Count >= 2 ? "DOIS" : scene.Actors.Count == 1 ? "MEDIA" : "LONGE";
        scene.CameraPreset = scene.NarrationPreset = scene.FinalPreset = ensemblePreset;
        XmlElement camera = root["Camera"];
        if (!Object.ReferenceEquals(camera, null))
        {
            scene.CameraPreset = ReadCameraPreset(camera, "plano", "DOIS");
            scene.CameraActor = camera.GetAttribute("personagem");
            scene.InvertAxis = camera.GetAttribute("inverterEixo") == "true";
            scene.CameraSide = FloatAttribute(camera, "deslocLado");
            scene.CameraHeight = FloatAttribute(camera, "deslocAltura");
            scene.CameraNear = FloatAttribute(camera, "aproximar");
            scene.SmoothCamera = camera.GetAttribute("transicaoSuave") == "true";
            scene.AutomaticCamera = !camera.HasAttribute("automatica") || camera.GetAttribute("automatica") == "true";
            scene.SpeechPreset = ReadCameraPreset(camera, "planoFala", "MEDIA");
            scene.NarrationPreset = ExplicitCameraPreset(camera, "planoNarracao", ensemblePreset);
            scene.FinalPreset = ExplicitCameraPreset(camera, "planoFinal", ensemblePreset);
            if (scene.Actors.Count > 0 && !scene.Actors.Exists(delegate(ActorData actor) { return actor.Id == scene.CameraActor; }))
                throw new InvalidOperationException("Camera/personagem: escolha um personagem do elenco.");
            if (scene.Actors.Count == 0 && (scene.CameraActor.Length > 0 || !IsEnvironmentPreset(scene.CameraPreset) ||
                !IsEnvironmentPreset(scene.NarrationPreset) || !IsEnvironmentPreset(scene.FinalPreset)))
                throw new InvalidOperationException("Câmera sem elenco: use LONGE, MEDIA, BAIXA ou ALTA, sem personagem.");
        }
        foreach (XmlElement element in root.SelectNodes("Roteiro/*"))
        {
            SegmentData segment = new SegmentData();
            segment.Pause = element.Name == "Pausa";
            segment.Narration = element.Name != "Fala";
            segment.NarratorName = element.HasAttribute("narrador") ? element.GetAttribute("narrador") : "Narrador";
            segment.Text = Object.ReferenceEquals(json, null) ? element.InnerText.Trim() : element.InnerText;
            segment.ActorId = segment.Narration && !segment.Pause ? "" : element.GetAttribute("personagem");
            segment.Intention = segment.Narration ? "" : element.GetAttribute("gesto");
            segment.CameraPreset = ReadCameraPreset(element, "plano", "");
            decimal seconds;
            decimal maximumSeconds = scene.Id == "EP01_C01" && segment.Pause && !Object.ReferenceEquals(json, null) ? 46m : 30m;
            decimal fullV4PauseSeconds;
            bool measuredFullV4Pause33 = !Object.ReferenceEquals(json, null) && scene.Id == "teste_fila_87" && json.Episode == 0 && json.Number == 0 &&
                Path.GetFileName(path) == "teste_fila_87.json" && segment.Pause && root.SelectNodes("Roteiro/*").Count == 1 &&
                scene.Actors.Count == 1 && scene.Actors[0].Id == "dinjer" &&
                Decimal.TryParse(element.GetAttribute("duracaoSegundos").Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out fullV4PauseSeconds) && fullV4PauseSeconds == 33m;
            if (measuredFullV4Pause33)
            {
                string durationConfigPath = Path.Combine(dataRoot, "catalogos", "trial-gorjeta87.json"); RequirePlainPath(durationConfigPath);
                GorjetaFisicaTrialDiretor.EnsaioSpec durationConfig = GorjetaFisicaTrialDiretor.LoadSpec(durationConfigPath);
                if (durationConfig.Tipo != "contagem" || durationConfig.Segmento != 4 || durationConfig.Corpo != "cfm_feminino" || durationConfig.JanelaMs != 33000)
                    throw new InvalidOperationException("Pausa33 exige config87 fullV4/cfm_feminino/33000; todos demais limites continuam30.");
                maximumSeconds = 33m; // Only the sole measured technical pause; full8 Native Fit and later scene contract still apply.
            }
            if (!Decimal.TryParse(element.GetAttribute("duracaoSegundos").Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out seconds) || seconds < 0.5m || seconds > maximumSeconds || seconds * 1000m != Decimal.Truncate(seconds * 1000m))
                throw new InvalidOperationException("Roteiro/duracaoSegundos: use de 0.5 a " + maximumSeconds.ToString(CultureInfo.InvariantCulture) + " segundos, com até três casas decimais.");
            if ((!segment.Pause && segment.Text.Length == 0) || segment.Text.Length > 350 || (segment.Pause && segment.Text.Length > 0))
                throw new InvalidOperationException("Roteiro/texto: use entre 1 e 350 caracteres.");
            if (!segment.Narration)
            {
                bool found = false;
                foreach (ActorData actor in scene.Actors)
                    found |= actor.Id == segment.ActorId;
                if (!found || !IsIntention(segment.Intention))
                    throw new InvalidOperationException("Fala: personagem ou intenção de gesto não encontrado: " + segment.ActorId + "/" + segment.Intention);
            }
            if (segment.Pause && segment.ActorId.Length > 0 &&
                !scene.Actors.Exists(delegate(ActorData actor) { return actor.Id == segment.ActorId; }))
                throw new InvalidOperationException("Pausa/personagem: escolha um ator do elenco.");
            if (scene.Actors.Count == 0 && segment.CameraPreset.Length > 0 && !IsEnvironmentPreset(segment.CameraPreset))
                throw new InvalidOperationException("Roteiro/plano: plano de ambiente incompatível com elenco vazio.");
            segment.StartMs = scene.DurationMs;
            segment.EndMs = segment.StartMs + Decimal.ToInt32(seconds * 1000m);
            scene.DurationMs = segment.EndMs;
            if (scene.DurationMs > 180000)
                throw new InvalidOperationException("O teste de falas deve durar no máximo 180 segundos.");
            scene.Segments.Add(segment);
        }
        if (!Object.ReferenceEquals(json, null))
        {
            scene.Id = json.Id;
            scene.Episode = json.Episode;
            scene.Number = json.Number;
            scene.CleanupRadius = json.CleanupRadius;
            scene.Actions.AddRange(json.Actions);
            scene.Direcao.AddRange(json.Direcao);
            scene.Interpretacao.AddRange(json.Interpretacao);
            scene.Objetos.AddRange(json.Objetos);
            scene.Poses.AddRange(json.Poses);
            scene.TransicoesPose.AddRange(json.TransicoesPose);
            scene.Veiculos.AddRange(json.Veiculos);
            scene.Portas.AddRange(json.Portas);
            scene.SaidaAgua = json.SaidaAgua;
            scene.EmpurraoAviao = json.EmpurraoAviao;
            scene.ArmarioModular = json.ArmarioModular;
            scene.CameraObjeto = json.CameraObjeto;
            scene.CameraFovObjeto = json.CameraFovObjeto;
            scene.CameraElevacaoObjeto = json.CameraElevacaoObjeto;
            scene.Cuts.AddRange(json.Cuts);
            foreach (CenaJsonDiretor.Acao action in scene.Actions)
                scene.DurationMs = System.Math.Max(scene.DurationMs, action.InicioMs + action.DuracaoMs);
            foreach (AcoesSerieDiretor.ActorAction action in scene.Direcao)
                scene.DurationMs = System.Math.Max(scene.DurationMs, action.InicioMs + action.DuracaoMs);
            foreach (InterpretacaoDiretor.ActorSpec action in scene.Interpretacao)
                scene.DurationMs = System.Math.Max(scene.DurationMs, action.InicioMs + action.DuracaoMs);
            if (scene.DurationMs <= 0 || scene.DurationMs > 180000)
                throw new InvalidOperationException("A cena JSON exige roteiro ou ações, com duração total de até 180 segundos.");
        }
        WriteLog("CENA_VALIDADA", "Arquivo=" + path + "; id=" + scene.Id + "; personagens=" + scene.Actors.Count +
            "; ações=" + scene.Actions.Count + "; cortes_programados=" + scene.Cuts.Count + "; duração_ms=" + scene.DurationMs);
        return scene;
    }

    private static int IntAttribute(XmlElement element, string name)
    {
        int value;
        if (!Int32.TryParse(element.GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            throw new InvalidOperationException("Valor inteiro inválido em " + element.Name + "/" + name);
        return value;
    }

    private static bool IsEnvironmentPreset(string preset)
    { return preset == "LONGE" || preset == "MEDIA" || preset == "BAIXA" || preset == "ALTA"; }

    private string ReadCameraPreset(XmlElement element, string name, string fallback)
    {
        if (!element.HasAttribute(name))
            return fallback;
        string value = element.GetAttribute(name);
        if (Array.IndexOf(cameraPresets, value) < 0)
            throw new InvalidOperationException(element.Name + "/" + name + ": use um dos oito planos do catálogo.");
        return value;
    }

    private string ExplicitCameraPreset(XmlElement element, string name, string fallback)
    {
        XmlAttribute attribute = element.GetAttributeNode(name);
        return Object.ReferenceEquals(attribute, null) || !attribute.Specified ? fallback : ReadCameraPreset(element, name, fallback);
    }

    private static float FloatAttribute(XmlElement element, string name)
    {
        float value;
        if (!Single.TryParse(element.GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || Single.IsNaN(value) || Single.IsInfinity(value))
            throw new InvalidOperationException("Número inválido em " + element.Name + "/" + name + ". Use ponto como separador decimal.");
        return value;
    }

    private static Weather WeatherFromName(string name)
    {
        switch (name)
        {
            case "CLEAR": return Weather.Clear;
            case "EXTRASUNNY": return Weather.ExtraSunny;
            case "CLOUDS": return Weather.Clouds;
            case "OVERCAST": return Weather.Overcast;
            case "RAIN": return Weather.Raining;
            case "THUNDER": return Weather.ThunderStorm;
            case "FOGGY": return Weather.Foggy;
            case "SMOG": return Weather.Smog;
            case "CLEARING": return Weather.Clearing;
            case "NEUTRAL": return Weather.Neutral;
            default: throw new InvalidOperationException("Clima desconhecido: " + name);
        }
    }

    private sealed class SceneData
    {
        public string Id, Name, Encenacao;
        public int Episode, Number;
        public float CleanupRadius;
        public int Hour, Minute;
        public string WeatherName;
        public float Traffic, PedestrianDensity, Distance;
        public bool BlockPolice;
        public bool ClearArea, MarkedLocation;
        public string LocalId;
        public bool SaidaAgua;
        public bool EmpurraoAviao;
        public bool ArmarioModular;
        public readonly List<ActorData> Actors = new List<ActorData>();
        public readonly List<SegmentData> Segments = new List<SegmentData>();
        public readonly List<CenaJsonDiretor.Acao> Actions = new List<CenaJsonDiretor.Acao>();
        public readonly List<AcoesSerieDiretor.ActorAction> Direcao = new List<AcoesSerieDiretor.ActorAction>();
        public readonly List<InterpretacaoDiretor.ActorSpec> Interpretacao = new List<InterpretacaoDiretor.ActorSpec>();
        public readonly List<ObjetosSerieDiretor.PropSpec> Objetos = new List<ObjetosSerieDiretor.PropSpec>();
        public readonly List<PosesSerieDiretor.PoseSpec> Poses = new List<PosesSerieDiretor.PoseSpec>();
        public readonly List<PosesSerieDiretor.TransitionSpec> TransicoesPose = new List<PosesSerieDiretor.TransitionSpec>();
        public readonly List<VeiculosSerieDiretor.VehicleSpec> Veiculos = new List<VeiculosSerieDiretor.VehicleSpec>();
        public readonly List<PortasSerieDiretor.DoorSpec> Portas = new List<PortasSerieDiretor.DoorSpec>();
        public readonly List<CenaJsonDiretor.Corte> Cuts = new List<CenaJsonDiretor.Corte>();
        public int DurationMs;
        public string CameraPreset = "DOIS", CameraActor;
        public string CameraObjeto;
        public float CameraFovObjeto = 30f, CameraElevacaoObjeto = 45f;
        public bool AutomaticCamera = true;
        public string SpeechPreset = "MEDIA", NarrationPreset = "DOIS", FinalPreset = "DOIS";
        public bool InvertAxis, SmoothCamera;
        public float CameraSide, CameraHeight, CameraNear;
    }

    private sealed class ActorData
    {
        public int RichOption;
        public string Id, Name, ModelName, LookAt;
        public VisualDiretor.Visual Visual;
        public float Right, Forward, Up;
        public float? Rotation;
    }

    private sealed class SegmentData
    {
        public bool Narration, Pause;
        public string ActorId, Text, Intention, NarratorName;
        public string CameraPreset;
        public int StartMs, EndMs;
    }

    private sealed class GestureData
    {
        public string MaleDictionary, MaleClip, FemaleDictionary, FemaleClip;
    }

    private void WriteLog(string level, string text)
    {
        // Datas em UTC explícito; cada escrita fecha o arquivo para leitura pelo Codex.
        string line = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture) +
            " [" + level + "] " + text + Environment.NewLine;
        lock (logSync) File.AppendAllText(logPath, line, new UTF8Encoding(false));
    }
}

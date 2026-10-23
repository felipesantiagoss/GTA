// EP07: mesmo velum do EP01, assentos reais e tarefas normais; nenhum warp durante Update.
public sealed class CenaVooTemporadaDiretor : System.IDisposable
{
    public enum BoardingPart { Integral, EmbarqueDinjerChefe, EmbarqueDjaniraDecolagem }
    public static BoardingPart PartFromSource(string scene, string selectedFile)
    {
        string path = (selectedFile ?? "").Replace('\\', '/');
        if (scene != "EP07_C03") return BoardingPart.Integral;
        if (path == "EP07_C03.json") return BoardingPart.Integral;
        if (path == "subtomadas/EP07_C03/embarque_dinjer_chefe/EP07_C03.json") return BoardingPart.EmbarqueDinjerChefe;
        if (path == "subtomadas/EP07_C03/embarque_djanira_decolagem/EP07_C03.json") return BoardingPart.EmbarqueDjaniraDecolagem;
        throw Error("fonte de subtomada EP07_C03 fora da lista literal");
    }
    public static string PartName(BoardingPart part)
    {
        if (part == BoardingPart.Integral) return "integral";
        if (part == BoardingPart.EmbarqueDinjerChefe) return "embarque_dinjer_chefe";
        if (part == BoardingPart.EmbarqueDjaniraDecolagem) return "embarque_djanira_decolagem";
        throw Error("parte de embarque desconhecida");
    }
    private BoardingPart boardingPart;
    private bool resetWaitingSeats;
    private bool boardingActionEverStarted;
    private int resetSeatsIssuedAt;
    private GTA.Math.Vector3 parkedPosition;
    private float parkedHeading;
    public BoardingPart Part { get { return boardingPart; } }
    public bool IsEditorialPart { get { return id == "EP07_C03" && boardingPart != BoardingPart.Integral; } }
    public bool CanPreserveUnstartedPreparation
    {
        get { return id == "EP07_C03" && boardingPart == BoardingPart.EmbarqueDjaniraDecolagem &&
            IsReady && airborne == null && resetDone && !resetWaitingSeats && !boardingActionEverStarted &&
            !running && !finished && lastElapsed == -1 && phase == 0 && phaseAt == 0 &&
            boardingActor == 0 && !missionStarted && !taxiStarted; }
    }
    private sealed class Actor
    {
        internal string Id;
        internal GTA.Ped Ped;
        internal int Handle, Model, Health;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Position;
        internal float Heading;
        internal bool Frozen, Invincible, Events, PendingEnterCancelled, FinalCleanupDeleteIssued;
    }
    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.Dictionary<string, Actor> actors = new System.Collections.Generic.Dictionary<string, Actor>(System.StringComparer.OrdinalIgnoreCase);
    private ObjetosDiretor resources;
    private ObjetosSerieDiretor props;
    private CenaQuedaDiretor airborne;
    private GTA.Camera boardingCamera, previousBoardingCamera;
    private int boardingCameraHandle;
    private GTA.Vehicle plane;
    private GTA.Prop glass;
    private int glassHandle, glassModel;
    private System.IntPtr glassAddress;
    private string id;
    private GTA.Math.Vector3 origin, right, forward, planeMin, planeMax;
    private System.Diagnostics.Stopwatch loading;
    private bool prepared, ready, resetDone, running, finished, failed, disposed, serviceStarted, serviceDone, serviceObserved;
    private bool dictRequested, dictWasLoaded, dimensionsKnown, missionStarted;
    private bool finalCleanupPilotMissionCancelled, finalCleanupFlightStopLogged;
    private bool taxiStarted;
    private const int TaxiMaximumMs = 25000, TaxiFlightReserveMs = 10000;
    private int taxiRearGroundSample, taxiRearProbeIndex, taxiRearProbeDone, nextTaxiEnvelopeDiagnostic;
    private readonly float[] taxiRearGround = new float[8];
    private readonly System.Collections.Generic.List<RunwayProbe> taxiRearPending = new System.Collections.Generic.List<RunwayProbe>();
    private sealed class RunwayProbe
    {
        internal int Lane, Segment, Ray;
        internal GTA.Math.Vector3 From, To;
        internal float Radius;
    }
    private int lastElapsed = -1, phase, phaseAt, boardingActor, nextLog, runwaySample, serviceAt;
    private int nextServiceDiagnostic, nextGlassDiagnostic, nextRunwayDiagnostic;
    private int nextBoardingDiagnostic, nextReleaseDiagnostic, nextFlightMotionDiagnostic;
    private int runwayColumn, runwayColumnSample, runwayProbeIndex, runwayProbeDone;
    private float groundZ, previousGround, giveDuration, receiveDuration, givePhase, receivePhase;
    private readonly float[] runwayGround = new float[13];
    private readonly float[,] runwayColumnGround = new float[7, 13];
    private readonly System.Collections.Generic.List<RunwayProbe> runwayPending = new System.Collections.Generic.List<RunwayProbe>();
    private const string GiveDictionary = "mp_common", GiveClip = "givetake1_a", ReceiveClip = "givetake1_b";

    private string appearanceMode = "diagnostico", appearanceChannel, appearanceConfigPath, appearanceConfigHash;
    private string appearanceEvidencePath, appearanceEvidenceHash;
    private int appearanceIndex = -1, appearanceIssuedAt;
    private bool appearanceIssued, appearanceApplied;

    // Config exclusiva C3, lida antes de criar o aviao. Sem arquivo = getter-only.
    // Native/mod48 sao canais distintos; nenhum indice e escolhido automaticamente.
    public void ConfigureAppearance(string path)
    {
        Active(); if (prepared || appearanceConfigPath != null) throw Error("config aparencia so antes do preparo");
        appearanceConfigPath = System.IO.Path.GetFullPath(path);
        if (!System.IO.File.Exists(appearanceConfigPath)) return;
        AppearanceHash(appearanceConfigPath); // validar path/link antes da leitura
        byte[] bytes = System.IO.File.ReadAllBytes(appearanceConfigPath);
        if (bytes.Length == 0 || bytes.Length > 4096) throw Error("config Velum XML fora do limite4KB");
        using (var h = System.Security.Cryptography.SHA256.Create())
            appearanceConfigHash = System.BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "");
        var settings = new System.Xml.XmlReaderSettings(); settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
        settings.XmlResolver = null; settings.MaxCharactersInDocument = 4096;
        var doc = new System.Xml.XmlDocument(); doc.XmlResolver = null;
        using (var reader = System.Xml.XmlReader.Create(new System.IO.MemoryStream(bytes, false), settings)) doc.Load(reader);
        System.Xml.XmlElement root = doc.DocumentElement;
        if (root == null || root.Name != "VelumAparencia" || root.GetAttribute("versao") != "1" || root.HasChildNodes)
            throw Error("config Velum exige raiz vazia versao1");
        var allowed = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (string name in new string[] { "versao", "modo", "canal", "indice", "matricula", "evidenciaArquivo", "evidenciaSha256" }) allowed.Add(name);
        foreach (System.Xml.XmlAttribute attr in root.Attributes) if (!allowed.Contains(attr.Name)) throw Error("atributo Velum desconhecido: " + attr.Name);
        if (AppearanceHash(appearanceConfigPath) != appearanceConfigHash) throw Error("config Velum mudou durante leitura");
        appearanceMode = root.GetAttribute("modo");
        if (appearanceMode == "diagnostico")
        { if (root.Attributes.Count != 2) throw Error("diagnostico Velum nao aceita setter/indice"); return; }
        if (appearanceMode != "ensaio" && appearanceMode != "continuidade") throw Error("modo Velum invalido");
        appearanceChannel = root.GetAttribute("canal");
        if ((appearanceChannel != "native" && appearanceChannel != "mod48") ||
            !System.Int32.TryParse(root.GetAttribute("indice"), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out appearanceIndex) || appearanceIndex < 0)
            throw Error("canal/indice Velum exige recurso observado, inteiro nao negativo");
        if (appearanceMode == "ensaio")
        { if (root.Attributes.Count != 4) throw Error("ensaio Velum so versao/modo/canal/indice"); return; }
        if (root.Attributes.Count != 7 || root.GetAttribute("matricula") != "N-LS281")
            throw Error("continuidade Velum exige matricula original+prova visual exata");
        appearanceEvidencePath = System.IO.Path.GetFullPath(root.GetAttribute("evidenciaArquivo"));
        appearanceEvidenceHash = root.GetAttribute("evidenciaSha256");
        if (!System.Text.RegularExpressions.Regex.IsMatch(appearanceEvidenceHash, "^[0-9A-F]{64}$") ||
            !System.IO.File.Exists(appearanceEvidencePath) || AppearanceHash(appearanceEvidencePath) != appearanceEvidenceHash)
            throw Error("prova visual real da matricula/livery ausente ou mudou");
        if (AppearanceHash(appearanceConfigPath) != appearanceConfigHash) throw Error("config Velum mudou durante leitura");
    }
    private static string AppearanceHash(string path)
    {
        string full = System.IO.Path.GetFullPath(path), cursor = full;
        while (cursor != null)
        {
            if ((System.IO.File.GetAttributes(cursor) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw new System.InvalidOperationException("aparencia Velum nao aceita vinculos");
            cursor = System.IO.Path.GetDirectoryName(cursor);
        }
        using (var hash = System.Security.Cryptography.SHA256.Create())
        using (var stream = new System.IO.FileStream(full, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            return System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }
    private int AppearanceCount(GTA.Vehicle own)
    {
        return appearanceChannel == "native" ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_LIVERY_COUNT, own.Handle) :
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUM_VEHICLE_MODS, own.Handle, 48);
    }
    private int AppearanceActual(GTA.Vehicle own)
    {
        return appearanceChannel == "native" ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_LIVERY, own.Handle) :
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_MOD, own.Handle, 48);
    }
    private bool PollAppearance()
    {
        if (appearanceApplied) return true;
        GTA.Vehicle own = OwnPlane();
        if (!own.IsPositionFrozen || running || resetDone || !prepared || id != "EP07_C03")
            throw Error("livery so no aviao proprio congelado durante PREPARO antes reset/arm/acao");
        if (!appearanceIssued)
        {
            LogPlaneAppearance("antes_setter_preparo");
            if (appearanceMode == "diagnostico") { appearanceApplied = true; return true; }
            if (AppearanceHash(appearanceConfigPath) != appearanceConfigHash) throw Error("config Velum mudou; remonte");
            int count = AppearanceCount(own);
            if (count <= 0 || appearanceIndex >= count) throw Error("indice Velum fora de Count real; nenhum setter");
            if (appearanceChannel == "native") GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_LIVERY, own.Handle, appearanceIndex);
            else
            {
                GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_MOD_KIT, own.Handle, 0);
                GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_MOD, own.Handle, 48, appearanceIndex, false);
            }
            appearanceIssued = true; appearanceIssuedAt = GTA.Game.GameTime;
            Log("EP07_APARENCIA_SETTER_PREPARO", "Modo=" + appearanceMode + "; Canal=" + appearanceChannel + "; Indice=" + appearanceIndex +
                "; Plane=" + own.Handle + "; GameTime=" + appearanceIssuedAt + "; ChamadasLivery=1; Fase=PREPARO; SemWriter=true; ConfigSHA256=" + appearanceConfigHash);
            return false;
        }
        if (GTA.Game.GameTime == appearanceIssuedAt) return false;
        if (AppearanceCount(own) <= appearanceIndex || AppearanceActual(own) != appearanceIndex)
            throw Error("livery nao permaneceu no getter do Tick seguinte; nenhum setter repetido");
        LogPlaneAppearance("getter_tick_seguinte"); appearanceApplied = true; return true;
    }
    public void RequireAppearanceForCapture()
    {
        RequireReady(); if (id != "EP07_C03") return;
        if (appearanceMode != "continuidade" || !appearanceApplied || appearanceConfigHash == null ||
            AppearanceHash(appearanceConfigPath) != appearanceConfigHash || AppearanceCount(OwnPlane()) <= appearanceIndex ||
            AppearanceActual(OwnPlane()) != appearanceIndex)
            throw Error("Velum apenas preview/ensaio; fixe variante N-LS281 conferida e remonte antes ARM");
    }
    public void VerifyAppearanceEvidenceBeforeArm()
    {
        RequireAppearanceForCapture();
        if (AppearanceHash(appearanceEvidencePath) != appearanceEvidenceHash)
            throw Error("prova visual da matricula mudou antes ARM; nenhum writer iniciado");
        LogPlaneAppearance("antes_ARM_arquivo_e_hash_conferidos");
    }
    private void RequireAppearanceStable()
    {
        if (appearanceMode != "continuidade" || !appearanceApplied ||
            AppearanceHash(appearanceConfigPath) != appearanceConfigHash || AppearanceCount(OwnPlane()) <= appearanceIndex ||
            AppearanceActual(OwnPlane()) != appearanceIndex)
            throw Error("Velum perdeu variante de continuidade durante acao; nenhum setter em filmagem");
    }
    private void LogPlaneAppearance(string stage)
    {
        GTA.Vehicle own = OwnPlane();
        var p = new GTA.Native.OutputArgument(); var s = new GTA.Native.OutputArgument();
        GTA.Native.Function.Call(GTA.Native.Hash.GET_VEHICLE_COLOURS, own.Handle, p, s);
        var extras = new System.Text.StringBuilder();
        for (int extra = 1; extra <= 14; extra++)
            if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_EXTRA_EXIST, own.Handle, extra))
                extras.Append(extra).Append(":").Append(GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VEHICLE_EXTRA_TURNED_ON, own.Handle, extra)).Append(",");
        Log("EP07_APARENCIA_GETTERS", "Stage=" + stage + "; Plane=" + own.Handle + "; Model=" + own.Model.Hash +
            "; Livery=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_LIVERY, own.Handle) +
            "; LiveryCount=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_LIVERY_COUNT, own.Handle) +
            "; Mod48=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_MOD, own.Handle, 48) +
            "; Mod48Count=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_NUM_VEHICLE_MODS, own.Handle, 48) +
            "; ModKit=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_MOD_KIT, own.Handle) +
            "; PlateText=" + GTA.Native.Function.Call<string>(GTA.Native.Hash.GET_VEHICLE_NUMBER_PLATE_TEXT, own.Handle) +
            "; Primary=" + p.GetResult<int>() + "; Secondary=" + s.GetResult<int>() +
            "; PrimaryCustom=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_IS_VEHICLE_PRIMARY_COLOUR_CUSTOM, own.Handle) +
            "; SecondaryCustom=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_IS_VEHICLE_SECONDARY_COLOUR_CUSTOM, own.Handle) +
            "; ColorCombination=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_COLOUR_COMBINATION, own.Handle) +
            "; Extras=" + extras + "; Mode=" + appearanceMode + "; ConfigSHA256=" + appearanceConfigHash +
            "; Evidence=" + appearanceEvidencePath + "; EvidenceSHA256=" + appearanceEvidenceHash +
            "; GetterOnly=true; PlateDoesNotProveTailTexture=true; VisualTailStillRequired=true");
    }

    public CenaVooTemporadaDiretor(System.Action<string, string> logger) { log = logger; }
    public static bool Supports(string scene) { return scene == "EP07_C03" || scene == "EP07_C04" || scene == "EP07_C05"; }
    public static int ExpectedDurationMs(string scene) { return scene == "EP07_C03" ? 45000 : scene == "EP07_C04" ? 34000 : scene == "EP07_C05" ? 20000 : 0; }
    public bool IsReady { get { return prepared && ready && !failed && !disposed; } }
    public bool IsFinished { get { return IsReady && finished && (id != "EP07_C04" || serviceDone); } }
    public bool UsesOwnCamera { get { return id == "EP07_C03" || airborne != null; } }
    public bool HandlesObjectEvents { get { return id == "EP07_C04"; } }
    public bool ControlsBody(string actorId, int elapsed)
    {
        if (!IsReady || elapsed < 0 || !actors.ContainsKey(actorId ?? "")) return false;
        if (id == "EP07_C03") return elapsed < 45000;
        return id == "EP07_C04" && elapsed < 6000 && (actorId.Equals("O_CHEFE", System.StringComparison.OrdinalIgnoreCase) || actorId.Equals("DJANIRA_POBRE", System.StringComparison.OrdinalIgnoreCase));
    }
    public GTA.Entity[] OwnedEntities { get { return airborne != null ? airborne.OwnedEntities : resources != null && resources.IsReady ? new GTA.Entity[] { resources.GetVehicle("AVIAO_EP07") } : new GTA.Entity[0]; } }
    public int[] OwnedHandles { get { var result = new System.Collections.Generic.List<int>(); foreach (GTA.Entity entity in OwnedEntities) result.Add(entity.Handle); return result.ToArray(); } }

    // CANAL_LS serve de âncora física do voo no ar, não como substituição de uma marca aérea inventada.
    public void Prepare(string scene, System.Collections.Generic.Dictionary<string, GTA.Ped> ownRoster,
        GTA.Math.Vector3 realOrigin, GTA.Math.Vector3 actualRight, GTA.Math.Vector3 actualForward,
        string realLocation, CenaQuedaDiretor.QuedaSpec realWaterSpec, BoardingPart part = BoardingPart.Integral)
    {
        Active(); if (prepared || !Supports(scene) || ownRoster == null) throw Error("ID/elenco/preparação inválidos");
        Vector(realOrigin); Vector(actualRight); Vector(actualForward);
        if (Horizontal(actualForward) < 0.99f || Horizontal(actualRight) < 0.99f || System.Math.Abs(GTA.Math.Vector3.Dot(actualRight, actualForward)) > 0.01f)
            throw Error("eixos relativos precisam ser unitários e ortogonais");
        if (scene != "EP07_C03" && part != BoardingPart.Integral) throw Error("subtomadas limitadas a EP07_C03");
        PartName(part); boardingPart = part;
        id = scene; origin = realOrigin; right = actualRight; forward = actualForward;
        foreach (var item in ownRoster)
        {
            GTA.Ped ped = item.Value;
            if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsPlayer || ped.IsDead || !ped.IsPersistent || ped.IsInVehicle())
                throw Error("elenco próprio precisa estar vivo a pé antes do preparo");
            actors.Add(item.Key, new Actor { Id = item.Key, Ped = ped, Handle = ped.Handle, Model = ped.Model.Hash, Address = ped.MemoryAddress,
                Position = ped.Position, Heading = ped.Heading, Frozen = ped.IsPositionFrozen, Invincible = ped.IsInvincible,
                Events = ped.BlockPermanentEvents, Health = ped.Health });
        }
        if (!actors.ContainsKey("DJANIRA_POBRE") || actors.Count != (scene == "EP07_C05" ? 1 : 3) ||
            (scene != "EP07_C05" && (!actors.ContainsKey("O_CHEFE") || !actors.ContainsKey("DINJER"))))
            throw Error("o voo exige o elenco exato da cena");
        loading = System.Diagnostics.Stopwatch.StartNew(); prepared = true;
        if (scene == "EP07_C03")
        {
            if (realLocation != "AEROPORTO_HANGAR" || realWaterSpec != null) throw Error("embarque exige marca real do hangar, não água/posição inferida");
            // Esta posição e toda a pista são relativas à marca real; sondas recusam muro, água ou terreno irregular.
            resources = new ObjetosDiretor(log); resources.RequestVehicle("AVIAO_EP07", "velum", origin, forward.ToHeading(), true, true);
        }
        else
        {
            if (realLocation != "CANAL_LS" || realWaterSpec == null || realWaterSpec.Local != "CANAL_LS")
                throw Error("jantar/queda exige a marca real de água já validada pelo backend");
            airborne = new CenaQuedaDiretor(log);
            var spec = new CenaQuedaDiretor.QuedaSpec { Cena = scene, Local = "CANAL_LS", Agua = realWaterSpec.Agua,
                Direcao = realWaterSpec.Direcao, AlturaRelativa = 35f, VelocidadeAviao = 35f,
                CompletarFuga = false, InverterEixo = realWaterSpec.InverterEixo, FilmarQuedaExterna = scene == "EP07_C05",
                DuracaoSolicitadaMs = scene == "EP07_C04" ? 34000 : 13000, QuedaAlvoMs = 4000, TimeoutMs = 60000,
                SomenteVoo = scene == "EP07_C04", PilotoDoElenco = scene == "EP07_C04" ? actors["O_CHEFE"].Ped : null,
                PassageiroTraseiro = scene == "EP07_C04" ? actors["DINJER"].Ped : null };
            airborne.Prepare(actors["DJANIRA_POBRE"].Ped, spec);
            if (scene == "EP07_C04")
            {
                dictWasLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, GiveDictionary);
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, GiveDictionary); dictRequested = true;
            }
        }
    }
    public void BindObjects(ObjetosSerieDiretor ownObjects)
    {
        Active(); if (!prepared || id != "EP07_C04" || ownObjects == null || !ownObjects.IsReady || resetDone) throw Error("vincular taça própria preparada antes de assentar");
        props = ownObjects; glass = props.GetProp("TACA_JANTAR");
        if (glass.Model.Hash != new GTA.Model("prop_wine_glass").Hash || !glass.IsVisible || !glass.IsAttachedTo(actors["O_CHEFE"].Ped))
            throw Error("o serviço exige a taça catalogada inicialmente na mão do Chefe");
        glassHandle = glass.Handle; glassModel = glass.Model.Hash; glassAddress = glass.MemoryAddress;
    }
    public void PollLoading()
    {
        Active(); if (!prepared || failed) throw Error("preparo inválido"); if (ready) return;
        try
        {
            RequireActors(); if (loading.ElapsedMilliseconds >= 15000) throw Error("aeronave/pista/serviço não ficou pronto em15s");
            if (airborne != null)
            {
                airborne.PollLoading(); if (!airborne.IsReady) return;
                if (id == "EP07_C04")
                {
                    if (props == null || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, GiveDictionary)) return;
                    RequireGlass();
                    giveDuration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, GiveDictionary, GiveClip);
                    receiveDuration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, GiveDictionary, ReceiveClip);
                    if (!Finite(giveDuration) || !Finite(receiveDuration) || giveDuration <= 0f || receiveDuration <= 0f || giveDuration > 6f || receiveDuration > 6f)
                        throw Error("entrega/recepção completa não cabe na reserva inicial6s; não truncar clipes");
                }
                ready = true; return;
            }
            resources.Update(); if (!resources.IsReady) return; plane = resources.GetVehicle("AVIAO_EP07");
            if (!PollAppearance()) return;
            if (!dimensionsKnown)
            {
                if (plane.PassengerCapacity < 2) throw Error("velum não disponibiliza os três assentos físicos");
                plane.Model.GetDimensions(out planeMin, out planeMax); Vector(planeMin); Vector(planeMax);
                if (planeMin.X >= planeMax.X || planeMin.Y >= planeMax.Y || planeMin.Z >= planeMax.Z) throw Error("bounding box do avião inválido");
                groundZ = plane.Position.Z - plane.HeightAboveGround; previousGround = groundZ; dimensionsKnown = true;
                Log("EP07_PISTA_ORIGEM_GETTERS", "Plane=" + plane.Handle + "; PlanePos=" + plane.Position +
                    "; PlaneHeading=" + plane.Heading + "; PlaneRotation=" + plane.Rotation +
                    "; HeightAboveGround=" + plane.HeightAboveGround.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; GroundBase=" + groundZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; Origin=" + origin + "; Forward=" + forward + "; Right=" + right +
                    "; BoundsMin=" + planeMin + "; BoundsMax=" + planeMax + "; GettersOnly=true");
            }
            if (!ProbeRunway()) return;
            // Additional PREPARO only: original91 ground nodes/252 capsules remain unchanged.
            // Existing runway capsules start at0; they did not prove the model rear footprint.
            if (boardingPart != BoardingPart.EmbarqueDinjerChefe && !ProbeTaxiRearEnvelope()) return;
            LogBoardingGeometry("preparo");
            LogBoardingAccess("preparo");
            ready = true; Log("EP07_PISTA_CONFERIDA", "Modelo=velum; Origem=" + plane.Position + "; ground+2; rota120m relativa; nenhum embarque/decolagem executado ainda");
        }
        catch { failed = true; throw; }
    }
    private bool ProbeRunway()
    {
        if (runwaySample <= 12)
        {
            GTA.Math.Vector3 point = origin + forward * (runwaySample * 10f);
            float ground; GTA.Math.Vector3 normal;
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
            GTA.Math.Vector3 probe = point + new GTA.Math.Vector3(0f, 0f, 2f);
            bool found = GTA.World.GetGroundHeightAndNormal(probe, out ground, out normal);
            if (found || GTA.Game.GameTime >= nextRunwayDiagnostic)
            {
                nextRunwayDiagnostic = GTA.Game.GameTime + 1000;
                Log("EP07_PISTA_AMOSTRA_GETTERS", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Sample={0}; DistanceForward={1:R}; Point={2}; Probe={3}; Found={4}; Ground={5:R}; Normal={6}; NormalZ={7:R}; GroundBase={8:R}; PreviousGround={9:R}; DeltaBase={10:R}; DeltaPrevious={11:R}; PlanePos={12}; PlaneHeightAboveGround={13:R}; PlaneHeading={14:R}; LoadingMs={15}; GettersOnly=true; ProbesUnchanged=true",
                    runwaySample, runwaySample * 10f, point, probe, found, ground, normal, normal.Z, groundZ, previousGround,
                    ground - groundZ, ground - previousGround, plane.Position, plane.HeightAboveGround, plane.Heading, loading.ElapsedMilliseconds));
            }
            if (!found) return false;
            if (!Finite(ground) || normal.Z < 0.98f || System.Math.Abs(ground - groundZ) > 2f || (runwaySample > 0 && System.Math.Abs(ground - previousGround) > 0.5f))
                throw Error("a marca do hangar não aponta para pista plana livre120m; nenhum XYZ alternativo é fabricado");
            float water; if (CenaQuedaDiretor.TryWater(point, out water) && water > ground + 0.1f) throw Error("pista relativa cruza água");
            runwayGround[runwaySample] = ground;
            previousGround = ground; runwaySample++; return false;
        }
        if (!ProbeRunwayColumns()) return false;
        if (runwayProbeDone == 21 * 12) return true;
        // Mesmas 21 faixas/raio: o maior piso medido do trecho evita cortar um degrau entre os nos.
        // Maximo apenas local (10m/colunas cobertas), nunca elevacao global da pista inteira.
        for (int index = runwayPending.Count - 1; index >= 0; index--)
        {
            RunwayProbe pending = runwayPending[index];
            bool hit; GTA.Math.Vector3 hitPoint, hitNormal; GTA.Entity physical;
            if (!ReadHit(pending.Ray, out hit, out hitPoint, out hitNormal, out physical)) continue;
            bool physicalExists = !System.Object.ReferenceEquals(physical, null) && physical.Exists();
            int safeHandle = physicalExists ? physical.Handle : 0;
            // API191 exclui Building/InteriorInstance: modelo somente do CPhysical validado.
            int safeModel = physicalExists ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, safeHandle) : 0;
            Log("EP07_PISTA_VOLUME_GETTERS", "Lane=" + pending.Lane + "; Segment=" + pending.Segment +
                "; Ray=" + pending.Ray + "; From=" + pending.From + "; To=" + pending.To +
                "; Radius=" + pending.Radius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; DidHit=" + hit + "; Point=" + hitPoint + "; Normal=" + hitNormal +
                "; Physical=" + physicalExists + "; PhysicalType=" + (physicalExists ? physical.GetType().Name : "nonphysical_or_none") +
                "; PhysicalHandle=" + safeHandle + "; PhysicalModel=" + safeModel +
                "; Reader=API191; NoBuildingModel=true; GuardUnchanged=true; FloorFollowing=true");
            runwayPending.RemoveAt(index);
            if (hit) throw Error("pista/volume real do velum bloqueado; nao remover colisores nem saltar a decolagem");
            runwayProbeDone++;
        }
        float halfX = (planeMax.X - planeMin.X + 1f) / 14f, halfZ = (planeMax.Z - planeMin.Z + 0.2f) / 6f;
        float radius = (float)System.Math.Sqrt(halfX * halfX + halfZ * halfZ);
        // No maximo tres sondas assincronas por frame, sempre lidas antes de repor a vaga.
        while (runwayPending.Count < 3 && runwayProbeIndex < 21 * 12)
        {
            int lane = runwayProbeIndex / 12, segment = runwayProbeIndex % 12;
            int firstColumn, lastColumn;
            float clearanceGround = RunwayClearanceGround(lane, segment, halfX, radius, out firstColumn, out lastColumn);
            GTA.Math.Vector3 from = RunwayEndpoint(lane, segment, halfX, halfZ, radius, clearanceGround);
            GTA.Math.Vector3 to = RunwayEndpoint(lane, segment + 1, halfX, halfZ, radius, clearanceGround);
            int ray = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_CAPSULE,
                from.X, from.Y, from.Z, to.X, to.Y, to.Z, radius, 17, plane.Handle, 7);
            if (ray == 0) throw Error("sonda do volume da pista nao iniciou");
            runwayPending.Add(new RunwayProbe { Lane = lane, Segment = segment, Ray = ray, From = from, To = to, Radius = radius });
            Log("EP07_PISTA_VOLUME_PEDIDO", "Lane=" + lane + "; Segment=" + segment + "; From=" + from + "; To=" + to +
                "; Radius=" + radius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; Plane=" + plane.Handle + "; GroundFrom=" + runwayColumnGround[lane % 7, segment].ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; GroundTo=" + runwayColumnGround[lane % 7, segment + 1].ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; ClearanceGround=" + clearanceGround.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; FirstFootprintColumn=" + firstColumn + "; LastFootprintColumn=" + lastColumn +
                "; Flags=17; Options=7; Same21Lanes=true; FloorFollowing=true; LocalMaximumOnly=true");
            runwayProbeIndex++;
        }
        if (runwayProbeDone != 21 * 12) return false;
        Log("EP07_PISTA_VOLUME_CONFERIDO", "Faixas=21; TrechosPorFaixa=12; Sondas=" + runwayProbeDone +
            "; Pendente=" + runwayPending.Count + "; Rota=120m; RaioOriginal=true; DidHitSempreRecusado=true; SemMAXGlobal=true");
        return true;
    }

    // Supplemental negative-Y footprint strip, never a replacement for the original0..120m runway.
    // Same literal21 lanes, capsule radius, flags17/options7, local maximum and fatal DidHit.
    private bool ProbeTaxiRearEnvelope()
    {
        if (id != "EP07_C03" || boardingPart == BoardingPart.EmbarqueDinjerChefe || !dimensionsKnown ||
            runwayProbeDone != 21 * 12 || !plane.IsPositionFrozen)
            throw Error("rear footprint only after own original runway proof during frozen PREPARO");
        float halfX = (planeMax.X - planeMin.X + 1f) / 14f;
        float halfZ = (planeMax.Z - planeMin.Z + 0.2f) / 6f;
        float radius = (float)System.Math.Sqrt(halfX * halfX + halfZ * halfZ);
        float rearY = planeMin.Y - 0.5f;
        if (!Finite(rearY) || rearY >= 0f) throw Error("rear footprint model bounds invalid");
        if (taxiRearGroundSample < 8)
        {
            int sample = taxiRearGroundSample;
            float lateral = sample == 0 ? 0f : planeMin.X - 0.5f + (sample - 1 + 0.5f) * halfX * 2f;
            GTA.Math.Vector3 point = origin + right * lateral + forward * rearY;
            GTA.Math.Vector3 probe = point + new GTA.Math.Vector3(0f, 0f, 2f);
            float ground; GTA.Math.Vector3 normal;
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
            if (!GTA.World.GetGroundHeightAndNormal(probe, out ground, out normal)) return false;
            Vector(normal);
            float frontGround = sample == 0 ? runwayGround[0] : runwayColumnGround[sample - 1, 0];
            if (!Finite(ground) || normal.Z < 0.98f || System.Math.Abs(ground - groundZ) > 2f ||
                System.Math.Abs(ground - frontGround) > 0.5f ||
                (sample > 0 && System.Math.Abs(ground - taxiRearGround[0]) > 0.5f))
                throw Error("supplemental real rear ground not flat; no inferred rear envelope");
            float water; if (CenaQuedaDiretor.TryWater(point, out water) && water > ground + 0.1f)
                throw Error("supplemental rear footprint crosses water");
            taxiRearGround[sample] = ground; taxiRearGroundSample++;
            Log("EP07_TAXI_REAR_GROUND_GETTERS", "Sample=" + sample + "; RearY=" + DiagnosticNumber(rearY) +
                "; Lateral=" + DiagnosticNumber(lateral) + "; Point=" + point + "; Ground=" + DiagnosticNumber(ground) +
                "; Normal=" + normal + "; FrontGround=" + DiagnosticNumber(frontGround) + "; AdditionalOnly=true");
            return false;
        }
        if (taxiRearProbeDone == 21) return true;
        for (int index = taxiRearPending.Count - 1; index >= 0; index--)
        {
            RunwayProbe pending = taxiRearPending[index];
            bool hit; GTA.Math.Vector3 hitPoint, hitNormal; GTA.Entity physical;
            if (!ReadHit(pending.Ray, out hit, out hitPoint, out hitNormal, out physical)) continue;
            Log("EP07_TAXI_REAR_VOLUME_GETTERS", "Lane=" + pending.Lane + "; From=" + pending.From +
                "; To=" + pending.To + "; Radius=" + DiagnosticNumber(pending.Radius) + "; DidHit=" + hit +
                "; Point=" + hitPoint + "; Normal=" + hitNormal + "; AdditionalOnly=true; DidHitFatal=true");
            taxiRearPending.RemoveAt(index);
            if (hit) throw Error("supplemental real rear footprint blocked; no collision exemption");
            taxiRearProbeDone++;
        }
        while (taxiRearPending.Count < 3 && taxiRearProbeIndex < 21)
        {
            int lane = taxiRearProbeIndex;
            float lateral = planeMin.X - 0.5f + (lane % 7 + 0.5f) * halfX * 2f;
            float clearanceGround = float.NegativeInfinity;
            for (int column = 0; column < 7; column++)
            {
                float center = planeMin.X - 0.5f + (column + 0.5f) * halfX * 2f;
                if (center + halfX < lateral - radius || center - halfX > lateral + radius) continue;
                clearanceGround = System.Math.Max(clearanceGround,
                    System.Math.Max(taxiRearGround[column + 1], runwayColumnGround[column, 0]));
            }
            if (!Finite(clearanceGround)) throw Error("supplemental rear local floor missing");
            GTA.Math.Vector3 from = TaxiRearEndpoint(lane, rearY, halfX, halfZ, radius, taxiRearGround[0], clearanceGround);
            GTA.Math.Vector3 to = TaxiRearEndpoint(lane, 0f, halfX, halfZ, radius, runwayGround[0], clearanceGround);
            int ray = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_CAPSULE,
                from.X, from.Y, from.Z, to.X, to.Y, to.Z, radius, 17, plane.Handle, 7);
            if (ray == 0) throw Error("supplemental rear capsule did not start");
            taxiRearPending.Add(new RunwayProbe { Lane = lane, Segment = -1, Ray = ray, From = from, To = to, Radius = radius });
            taxiRearProbeIndex++;
        }
        if (taxiRearProbeDone != 21) return false;
        Log("EP07_TAXI_REAR_VOLUME_CONFERIDO", "GroundNodes=8; ExtraCapsules=21; RearY=" + DiagnosticNumber(rearY) +
            "; ToY=0; OriginalGroundNodes=91; OriginalCapsules=252; OriginalForward120Preserved=true; RadiusUnchanged=true; DidHitFatal=true");
        return true;
    }
    private GTA.Math.Vector3 TaxiRearEndpoint(int lane, float longitudinal, float halfX, float halfZ,
        float radius, float endpointGround, float clearanceGround)
    {
        GTA.Math.Vector3 point = plane.Position + right * (planeMin.X - 0.5f + (lane % 7 + 0.5f) * halfX * 2f) +
            forward * longitudinal + new GTA.Math.Vector3(0f, 0f,
                planeMin.Z + 0.15f + (lane / 7 + 0.5f) * halfZ * 2f + endpointGround - groundZ);
        point.Z = System.Math.Max(point.Z, clearanceGround + radius + 0.1f);
        Vector(point); return point;
    }
    // Actual8 world corners, projected only to XY footprint: model bounds are not collision mesh.
    // Fatal native collision/terrain guards continue independently; no AABB-Z floor exemption.
    private void RequireTaxiEnvelope(int elapsed, float progress, float lateral, int budgetMs)
    {
        bool groundedOwnMissionB = phase == 1 && boardingPart == BoardingPart.EmbarqueDjaniraDecolagem &&
            missionStarted && taxiStarted && !finished && running && plane.HeightAboveGround < 10f;
        if (id != "EP07_C03" || (phase != 4 && !groundedOwnMissionB) || taxiRearProbeDone != 21 || runwayProbeDone != 252)
            throw Error("taxi envelope native preparation proof missing");
        GTA.Math.Vector3 actualForward = plane.ForwardVector;
        Vector(actualForward);
        float yaw = (float)System.Math.Atan2(GTA.Math.Vector3.Dot(actualForward, right), GTA.Math.Vector3.Dot(actualForward, forward));
        float maxModelY = System.Math.Max(System.Math.Abs(planeMin.Y), System.Math.Abs(planeMax.Y));
        float maxYaw = (float)System.Math.Atan2(0.25f, maxModelY); // half the existing0.5m footprint margin, never widen it.
        if (!Finite(yaw) || !Finite(maxYaw) || System.Math.Abs(yaw) > maxYaw)
            throw Error("taxi yaw exceeds measured model footprint margin");
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        for (int xi = 0; xi < 2; xi++) for (int yi = 0; yi < 2; yi++) for (int zi = 0; zi < 2; zi++)
        {
            GTA.Math.Vector3 corner = plane.GetOffsetPosition(new GTA.Math.Vector3(xi == 0 ? planeMin.X : planeMax.X,
                yi == 0 ? planeMin.Y : planeMax.Y, zi == 0 ? planeMin.Z : planeMax.Z));
            Vector(corner);
            GTA.Math.Vector3 relative = corner - origin;
            float x = GTA.Math.Vector3.Dot(relative, right), y = GTA.Math.Vector3.Dot(relative, forward);
            if (!Finite(x) || !Finite(y)) throw Error("taxi footprint nonfinite");
            minX = System.Math.Min(minX, x); maxX = System.Math.Max(maxX, x);
            minY = System.Math.Min(minY, y); maxY = System.Math.Max(maxY, y);
        }
        if (minX < planeMin.X - 0.5f || maxX > planeMax.X + 0.5f || minY < planeMin.Y - 0.5f || maxY > 120f)
            throw Error("actual taxi model XY footprint left measured original+rear runway strip");
        if (elapsed >= nextTaxiEnvelopeDiagnostic)
        {
            nextTaxiEnvelopeDiagnostic = elapsed + 500;
            Log("EP07_TAXI_ENVELOPE_GETTERS", "TempoMs=" + elapsed + "; Progress=" + DiagnosticNumber(progress) +
                "; Lateral=" + DiagnosticNumber(lateral) + "; YawRadians=" + DiagnosticNumber(yaw) + "; MaxYawRadians=" + DiagnosticNumber(maxYaw) +
                "; MinX=" + DiagnosticNumber(minX) + "; MaxX=" + DiagnosticNumber(maxX) + "; MinY=" + DiagnosticNumber(minY) + "; MaxY=" + DiagnosticNumber(maxY) +
                "; RearLimit=" + DiagnosticNumber(planeMin.Y - 0.5f) + "; FrontLimit=120; BudgetMs=" + budgetMs +
                "; OriginalDurationMs=45000; FlightReserveMs=" + TaxiFlightReserveMs + "; ExtraRearNativeProof=true; ModelXYBoundsNotCollisionMesh=true; NoSetters=true");
        }
    }

    private bool ProbeRunwayColumns()
    {
        float halfX = (planeMax.X - planeMin.X + 1f) / 14f;
        if (runwayColumn == 7) return true;
        // Um no por frame reserva ground+TryWater (ate quatro consultas espaciais).
        // O ultimo no tambem retorna pendente: nao misturar pisos e capsulas no mesmo Tick.
        for (int budget = 0; budget < 1 && runwayColumn < 7; budget++)
        {
            float lateral = planeMin.X - 0.5f + (runwayColumn + 0.5f) * halfX * 2f;
            GTA.Math.Vector3 point = origin + right * lateral + forward * (runwayColumnSample * 10f);
            GTA.Math.Vector3 probe = point + new GTA.Math.Vector3(0f, 0f, 2f);
            float ground; GTA.Math.Vector3 normal;
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
            bool found = GTA.World.GetGroundHeightAndNormal(probe, out ground, out normal);
            if (found || GTA.Game.GameTime >= nextRunwayDiagnostic)
            {
                nextRunwayDiagnostic = GTA.Game.GameTime + 1000;
                Log("EP07_PISTA_COLUNA_GETTERS", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Column={0}; Sample={1}; Lateral={2:R}; Forward={3:R}; Point={4}; Probe={5}; Found={6}; Ground={7:R}; Normal={8}; CenterGround={9:R}; LoadingMs={10}; GettersOnly=true",
                    runwayColumn, runwayColumnSample, lateral, runwayColumnSample * 10f, point, probe, found, ground, normal,
                    runwayGround[runwayColumnSample], loading.ElapsedMilliseconds));
            }
            if (!found) return false;
            Vector(normal);
            if (!Finite(ground) || normal.Z < 0.98f || System.Math.Abs(ground - groundZ) > 2f ||
                (runwayColumnSample > 0 && System.Math.Abs(ground - runwayColumnGround[runwayColumn, runwayColumnSample - 1]) > 0.5f) ||
                System.Math.Abs(ground - runwayGround[runwayColumnSample]) > 0.5f)
                throw Error("o piso da largura real do velum nao e plano/livre; nao fabricar pista alternativa");
            float water; if (CenaQuedaDiretor.TryWater(point, out water) && water > ground + 0.1f) throw Error("pista relativa cruza agua");
            runwayColumnGround[runwayColumn, runwayColumnSample] = ground;
            runwayColumnSample++;
            if (runwayColumnSample == 13) { runwayColumnSample = 0; runwayColumn++; }
        }
        return false;
    }
    private float RunwayClearanceGround(int lane, int segment, float halfX, float radius,
        out int firstColumn, out int lastColumn)
    {
        float lateral = planeMin.X - 0.5f + (lane % 7 + 0.5f) * halfX * 2f;
        float maximum = float.NegativeInfinity; firstColumn = 7; lastColumn = -1;
        for (int column = 0; column < 7; column++)
        {
            float center = planeMin.X - 0.5f + (column + 0.5f) * halfX * 2f;
            // Discos da capsula podem cobrir a borda da celula vizinha; so usar pisos ja medidos.
            if (center + halfX < lateral - radius || center - halfX > lateral + radius) continue;
            maximum = System.Math.Max(maximum, System.Math.Max(runwayColumnGround[column, segment], runwayColumnGround[column, segment + 1]));
            firstColumn = System.Math.Min(firstColumn, column); lastColumn = column;
        }
        if (!Finite(maximum) || firstColumn > lastColumn) throw Error("piso local do trecho nao foi medido");
        return maximum;
    }
    private GTA.Math.Vector3 RunwayEndpoint(int lane, int sample, float halfX, float halfZ, float radius, float clearanceGround)
    {
        GTA.Math.Vector3 point = plane.Position + right * (planeMin.X - 0.5f + (lane % 7 + 0.5f) * halfX * 2f) +
            forward * (sample * 10f) + new GTA.Math.Vector3(0f, 0f,
                planeMin.Z + 0.15f + (lane / 7 + 0.5f) * halfZ * 2f + runwayGround[sample] - groundZ);
        // Native116: interpolar12.94475->13.14675 atingiu o degrau real; duas pontas usam o MAX local.
        // Mantem raio/folga originais e todo DidHit continua falha, inclusive piso inesperado.
        point.Z = System.Math.Max(point.Z, clearanceGround + radius + 0.1f);
        Vector(point); return point;
    }

    public bool ReleaseActorsForReset()
    {
        Active(); running = false; resetDone = false; resetWaitingSeats = false;
        if (airborne != null) return airborne.ReleaseActorsForReset();
        if (plane == null || !plane.Exists()) return true;
        OwnPlane(); CheckOccupants(); bool released = true;
        bool diagnostic = GTA.Game.GameTime >= nextReleaseDiagnostic;
        if (diagnostic) nextReleaseDiagnostic = GTA.Game.GameTime + 500;
        foreach (Actor actor in actors.Values)
        {
            if (actor.FinalCleanupDeleteIssued)
            {
                if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, actor.Handle))
                    throw Error("remocao propria final ainda pendente; nao repetir DELETE nem reiniciar tomada");
                continue;
            }
            RequireActor(actor);
            if (diagnostic) LogBoardingActor(actor, -99, lastElapsed, "release_antes");
            if (!actor.Ped.IsInVehicle())
            {
                // A tarefa propria de embarque continua viva fora do assento; cancelar somente essa entrada.
                GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
                actor.Ped.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
                GTA.Vehicle trying = actor.Ped.VehicleTryingToEnter;
                string[] boardingOrder = BoardingOrder();
                bool ownPending = !actor.PendingEnterCancelled && id == "EP07_C03" && phase == 0 && phaseAt != 0 && boardingActor < boardingOrder.Length &&
                    actor.Id.Equals(boardingOrder[boardingActor], System.StringComparison.OrdinalIgnoreCase) &&
                    actor.Ped.IsEnteringVehicle && DiagnosticHandle(trying) == plane.Handle && task == GTA.ScriptTaskNameHash.EnterVehicle;
                if (ownPending)
                { actor.Ped.Task.ClearAllImmediately(); actor.PendingEnterCancelled = true; Log("EP07_EMBARQUE_PROPRIO_CANCELADO", "Ator=" + actor.Id + "; Plane=" + plane.Handle + "; Task=" + task + "; Status=" + status + "; ForaDoAssento=true; SomentePedidoProprio=true; PedidoUnico=true"); }
                continue;
            }
            if (!actor.Ped.IsInVehicle(plane)) throw Error("ator entrou em veículo alheio; não removê-lo");
            actor.Ped.Task.LeaveVehicle(plane, GTA.LeaveVehicleFlags.WarpOut); if (actor.Ped.IsInVehicle(plane)) released = false;
            if (diagnostic) LogBoardingActor(actor, -99, lastElapsed, "release_apos_pedido");
        }
        return released;
    }
    // Exclusivamente F10 final depois do prazo de saida e da prova externa de ausencia de writer.
    // Nenhum ator e apagado por Reset, Start, Update ou pelos outros voos.
    public void ReleaseStuckRearForFinalCleanup()
    {
        Active();
        if (id != "EP07_C03" || boardingPart != BoardingPart.EmbarqueDjaniraDecolagem ||
            airborne != null || running || resetDone || resetWaitingSeats || resources == null)
            throw Error("remocao final restrita a C3B interrompida fora da tomada");
        plane = OwnPlane(); CheckOccupants();
        Actor rear = actors["DINJER"];
        if (rear.FinalCleanupDeleteIssued)
        {
            if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, rear.Handle))
                throw Error("DELETE proprio final ja emitido; entidade conservada para inspecao, nao repetir");
            return;
        }
        RequireActors();
        foreach (Actor actor in actors.Values)
            if (actor != rear && actor.Ped.IsInVehicle())
                throw Error("limpeza final exige os outros atores ja fora dos veiculos; nenhum removido");
        if (!rear.Ped.IsInVehicle(plane) || !SameActor(plane.GetPedOnSeat((GTA.VehicleSeat)1), rear) ||
            rear.Ped.SeatIndex != (GTA.VehicleSeat)1)
            throw Error("limpeza final exige somente Dinjer propria no assento1 exato");
        Log("EP07_REAR_LIMPEZA_DELETE_PRE", "Ator=" + rear.Id + "; Handle=" + rear.Handle +
            "; Model=" + rear.Model + "; Address=" + rear.Address + "; Plane=" + plane.Handle +
            "; Seat=1; F10Final=true; SemReset=true; ForaDaTomada=true; DeleteUnico=true");
        // DELETE_PED recebe pointer in/out. O wrapper original conserva Handle para o getter e caches do Main.
        var pedPointer = new GTA.Native.OutputArgument(rear.Ped);
        rear.FinalCleanupDeleteIssued = true;
        GTA.Native.Function.Call(GTA.Native.Hash.DELETE_PED, pedPointer);
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, rear.Handle))
            throw Error("DELETE final emitido mas entidade ainda existe; preservar recursos e nao repetir");
        Log("EP07_REAR_LIMPEZA_DELETE_CONFIRMED", "Handle=" + rear.Handle + "; Exists=false; SomenteAtorProprio=true; VeiculoNaoApagadoAinda=true");
    }

    public bool Reset()
    {
        foreach (Actor actor in actors.Values)
            if (actor.FinalCleanupDeleteIssued) throw Error("ator removido somente na limpeza final; montar nova cena, nao Reset");
        RequireReady(); running = false; finished = false;
        if (airborne != null)
        {
            if (!airborne.Reset()) return false;
            if (id == "EP07_C04") RebindGlassAfterSeats();
            resetDone = true; serviceStarted = serviceDone = serviceObserved = false; givePhase = receivePhase = 0f;
            lastElapsed = -1; serviceAt = -1; nextServiceDiagnostic = nextGlassDiagnostic = 0; return true;
        }
        if (resetWaitingSeats) return CompletePreparationSeats();
        if (!ReleaseActorsForReset()) return false;
        foreach (Actor actor in actors.Values)
        {
            actor.PendingEnterCancelled = false;
            actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
            actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading;
            actor.Ped.IsInvincible = true; actor.Ped.BlockPermanentEvents = true; actor.Ped.IsPositionFrozen = true;
        }
        resources.Reset(); plane = OwnPlane(); plane.IsEngineRunning = false; plane.IsHandbrakeForcedOn = true;
        phase = phaseAt = boardingActor = nextLog = nextBoardingDiagnostic = nextFlightMotionDiagnostic = 0; missionStarted = false; taxiStarted = false; nextTaxiEnvelopeDiagnostic = 0; lastElapsed = -1;
        parkedPosition = plane.Position; parkedHeading = plane.Heading;
        if (boardingPart == BoardingPart.EmbarqueDjaniraDecolagem)
        {
            if (!plane.IsSeatFree(GTA.VehicleSeat.Driver) || !plane.IsSeatFree((GTA.VehicleSeat)1) ||
                !plane.IsSeatFree(GTA.VehicleSeat.Passenger)) throw Error("preparo B exige os tres assentos livres");
            resetWaitingSeats = true; resetSeatsIssuedAt = GTA.Game.GameTime;
            actors["O_CHEFE"].Ped.IsPositionFrozen = false;
            actors["DINJER"].Ped.IsPositionFrozen = false;
            // Chamadas unicas no PREPARO. O proximo Tick so le getters; Start/Update nunca reassentam.
            actors["O_CHEFE"].Ped.SetIntoVehicle(plane, GTA.VehicleSeat.Driver);
            actors["DINJER"].Ped.SetIntoVehicle(plane, (GTA.VehicleSeat)1);
            Log("EP07_ASSENTAMENTO_PREPARO", "Parte=" + PartName(boardingPart) + "; Plane=" + plane.Handle +
                "; Chefe=" + actors["O_CHEFE"].Handle + "; Dinjer=" + actors["DINJER"].Handle +
                "; GameTime=" + resetSeatsIssuedAt + "; Fase=PREPARO; ChamadasUnicas=2; SemAcao=true; SemWriter=true");
            return false;
        }
        CaptureBoardingCamera(); UpdateBoardingCamera();
        resetDone = true; return true;
    }
    // Somente a primeira F7 da B ja confirmada no Tick seguinte ao preparo.
    // Nao libera/reassenta/move atores nem modifica recursos; reinicios posteriores usam Reset original.
    public void RequireUnstartedPreparation()
    {
        if (!CanPreserveUnstartedPreparation || GTA.Game.GameTime <= resetSeatsIssuedAt)
            throw Error("reuso inicial exige parte B nunca iniciada e assentos confirmados em Tick posterior");
        RequireReadyForCountdown();
        Actor djanira = actors["DJANIRA_POBRE"];
        GTA.Math.Vector3 actualPosition = djanira.Ped.Position;
        Vector(actualPosition); Vector(plane.Position);
        float distance = (actualPosition - djanira.Position).Length();
        float headingDelta = AngleDistance(djanira.Ped.Heading, djanira.Heading);
        float speed = djanira.Ped.Speed;
        if (!Finite(distance) || !Finite(headingDelta) || !Finite(speed) ||
            distance > 0.1f || headingDelta > 0.05f || speed > 0.15f ||
            !djanira.Ped.IsPositionFrozen || djanira.Ped.IsEnteringVehicle ||
            DiagnosticHandle(djanira.Ped.VehicleTryingToEnter) != 0 ||
            actors["O_CHEFE"].Ped.SeatIndex != GTA.VehicleSeat.Driver ||
            actors["DINJER"].Ped.SeatIndex != (GTA.VehicleSeat)1)
            throw Error("preparo B inicial deixou marcas/assentos exatos; nao reassentar ou liberar antes ARM");
        Log("EP07_PREPARO_INICIAL_PRESERVADO", "Parte=" + PartName(boardingPart) +
            "; Plane=" + plane.Handle + "; Chefe=" + actors["O_CHEFE"].Handle +
            "; Dinjer=" + actors["DINJER"].Handle + "; Djanira=" + djanira.Handle +
            "; IssuedAt=" + resetSeatsIssuedAt + "; CheckedAt=" + GTA.Game.GameTime +
            "; DjaniraMarkDistance=" + DiagnosticNumber(distance) +
            "; DjaniraHeadingDelta=" + DiagnosticNumber(headingDelta) +
            "; DjaniraSpeed=" + DiagnosticNumber(speed) +
            "; NeverStarted=true; GettersAfterPreparationTick=true; SemRelease=true; SemReset=true; SemSetter=true; AntesARM=true");
    }

    public void Start()
    {
        RequireReady(); if (!resetDone || running) throw Error("reinicie fora da contagem antes de iniciar");
        if (id == "EP07_C03")
        {
            RequireReadyForCountdown();
            if (boardingPart == BoardingPart.EmbarqueDjaniraDecolagem) boardingActionEverStarted = true;
            LogBoardingAccess("inicio");
            if (boardingPart == BoardingPart.EmbarqueDinjerChefe) actors["DJANIRA_POBRE"].Ped.IsPositionFrozen = true;
            Log("EP07_SUBTOMADA_INICIADA", "Parte=" + PartName(boardingPart) + "; DuracaoMs=45000; Integral=" +
                (boardingPart == BoardingPart.Integral) + "; NenhumWarpNaAcao=true");
        }
        running = true; if (airborne != null) airborne.Start();
    }
    public void SetSpeaker(string actorId)
    {
        RequireReady();
        if (id != "EP07_C04") return;
        bool known = actors.ContainsKey(actorId ?? "");
        Log("EP07_CAMERA_FALANTE_PEDIDO", "ActorId=" + actorId + "; Known=" + known +
            "; FocusHandle=" + (known ? actors[actorId].Ped.Handle : 0) +
            "; Dinjer=" + actors["DINJER"].Ped.Handle + "; Chefe=" + actors["O_CHEFE"].Ped.Handle +
            "; Djanira=" + actors["DJANIRA_POBRE"].Ped.Handle + "; Aircraft=" + airborne.OwnPlane.Handle +
            "; TempoMs=" + lastElapsed + "; GettersOnly=true; VisualPending=true");
        if (known) airborne.SetCabinFocus(actors[actorId].Ped);
    }
    public void Update(int elapsed)
    {
        RequireReady(); if (finished) return;
        if (!running || elapsed < 0 || elapsed < lastElapsed || elapsed > ExpectedDurationMs(id) + 500) throw Error("tempo inválido/fora da reserva");
        try
        {
            RequireActors(); lastElapsed = elapsed;
            if (airborne != null)
            {
                airborne.Update(elapsed);
                if (id == "EP07_C04")
                {
                    UpdateService(elapsed);
                    if (elapsed >= 30000 && elapsed < 32000)
                    {
                        // Piso da cabine: os pés reais da passageira; não consultar terreno dezenas de metros abaixo.
                        GTA.Ped dinjer = actors["DINJER"].Ped;
                        GTA.Math.Vector3 point = (dinjer.Bones[GTA.Bone.SkelLeftFoot].Position + dinjer.Bones[GTA.Bone.SkelRightFoot].Position) * 0.5f;
                        Vector(point); dinjer.Task.LookAt(point, 650, GTA.LookAtFlags.SlowTurnRate | GTA.LookAtFlags.ExtendPitchLimit, GTA.LookAtPriority.VeryHigh);
                    }
                }
                if (elapsed >= ExpectedDurationMs(id))
                {
                    if (!airborne.IsSuccess || (id == "EP07_C04" && !serviceDone)) throw Error("voo/queda/serviço físico não terminou dentro da reserva");
                    finished = true; running = false;
                }
                return;
            }
            UpdateBoarding(elapsed);
            // O velum continua no quadro durante embarque, taxi e decolagem; nenhum Update na contagem.
            UpdateBoardingCamera();
        }
        catch { failed = true; running = false; throw; }
    }
    private string[] BoardingOrder()
    {
        if (boardingPart == BoardingPart.EmbarqueDinjerChefe) return new string[] { "O_CHEFE", "DINJER" };
        if (boardingPart == BoardingPart.EmbarqueDjaniraDecolagem) return new string[] { "DJANIRA_POBRE" };
        return new string[] { "DINJER", "DJANIRA_POBRE", "O_CHEFE" };
    }
    private int[] BoardingSeats()
    {
        if (boardingPart == BoardingPart.EmbarqueDinjerChefe) return new int[] { -1, 0 };
        if (boardingPart == BoardingPart.EmbarqueDjaniraDecolagem) return new int[] { 0 };
        return new int[] { 1, 0, -1 };
    }
    private bool CompletePreparationSeats()
    {
        OwnPlane(); RequireActors(); CheckOccupants();
        if (GTA.Game.GameTime == resetSeatsIssuedAt) return false;
        // Identidade/ocupantes estrangeiros sao recusados mesmo enquanto o setter assenta entre frames.
        GTA.Ped pilot = plane.GetPedOnSeat(GTA.VehicleSeat.Driver), rear = plane.GetPedOnSeat((GTA.VehicleSeat)1);
        if ((!System.Object.ReferenceEquals(pilot, null) && pilot.Exists() && !SameActor(pilot, actors["O_CHEFE"])) ||
            (!System.Object.ReferenceEquals(rear, null) && rear.Exists() && !SameActor(rear, actors["DINJER"])))
            throw Error("ocupante inesperado durante preparo B; nenhum reassentamento");
        if (!SameActor(pilot, actors["O_CHEFE"]) || !SameActor(rear, actors["DINJER"])) return false;
        RequirePreparationSeats();
        LogBoardingActor(actors["O_CHEFE"], -1, -1, "preparo_assento_confirmado");
        LogBoardingActor(actors["DINJER"], 1, -1, "preparo_assento_confirmado");
        resetWaitingSeats = false; resetDone = true;
        CaptureBoardingCamera(); UpdateBoardingCamera();
        Log("EP07_ASSENTAMENTO_PREPARO_CONFERIDO", "Parte=" + PartName(boardingPart) +
            "; Plane=" + plane.Handle + "; IssuedAt=" + resetSeatsIssuedAt + "; ConfirmedAt=" + GTA.Game.GameTime +
            "; GettersEmTickSeguinte=true; DjaniraFora=true; SemNovoSetter=true");
        return true;
    }
    private void RequirePreparationSeats()
    {
        plane = OwnPlane(); RequireActors(); CheckOccupants();
        if (!SameActor(plane.GetPedOnSeat(GTA.VehicleSeat.Driver), actors["O_CHEFE"]) ||
            !SameActor(plane.GetPedOnSeat((GTA.VehicleSeat)1), actors["DINJER"]) ||
            !actors["O_CHEFE"].Ped.IsInVehicle(plane) || !actors["DINJER"].Ped.IsInVehicle(plane) ||
            actors["DJANIRA_POBRE"].Ped.IsInVehicle() || !plane.IsSeatFree(GTA.VehicleSeat.Passenger))
            throw Error("assentos preparados B nao permaneceram exatos; nenhum reassentamento na contagem/acao");
        RequireParkedPlane();
    }
    private void RequireParkedPlane()
    {
        if (!plane.IsPositionFrozen || plane.IsEngineRunning || plane.Speed > 0.15f ||
            (plane.Position - parkedPosition).Length() > 0.05f ||
            AngleDistance(plane.Heading, parkedHeading) > 0.05f)
            throw Error("velum deixou a marca estacionada antes da decolagem");
    }
    private static float AngleDistance(float a, float b)
    { float d = (a - b) % 360f; if (d > 180f) d -= 360f; if (d < -180f) d += 360f; return System.Math.Abs(d); }
    private void RequireParkedPartSeats()
    {
        RequireActors(); CheckOccupants(); RequireParkedPlane();
        if (!SameActor(plane.GetPedOnSeat(GTA.VehicleSeat.Driver), actors["O_CHEFE"]) ||
            !SameActor(plane.GetPedOnSeat(GTA.VehicleSeat.Passenger), actors["DINJER"]) ||
            actors["DJANIRA_POBRE"].Ped.IsInVehicle() || !plane.IsSeatFree((GTA.VehicleSeat)1) ||
            (actors["DJANIRA_POBRE"].Ped.Position - actors["DJANIRA_POBRE"].Position).Length() > 0.1f)
            throw Error("parte A perdeu entradas/espera fora da aeronave");
    }
    public void RequireReadyForCountdown()
    {
        RequireReady();
        if (id != "EP07_C03") return;
        RequireAppearanceForCapture();
        if (!resetDone || resetWaitingSeats || running || finished) throw Error("preparo nao pronto para contagem");
        if (boardingPart == BoardingPart.EmbarqueDjaniraDecolagem) RequirePreparationSeats();
        else
        {
            plane = OwnPlane(); RequireActors(); CheckOccupants();
            foreach (Actor actor in actors.Values)
                if (actor.Ped.IsInVehicle()) throw Error("entrada normal exige tres atores a pe na contagem");
            if (boardingPart == BoardingPart.EmbarqueDinjerChefe) RequireParkedPlane();
        }
        LogFlightMotion(-1, "contagem_preparo", false);
    }
    private void UpdateBoarding(int elapsed)
    {
        plane = OwnPlane(); CheckOccupants();
        RequireAppearanceStable();
        if (phase == 0)
        {
            string[] order = BoardingOrder(); int[] seats = BoardingSeats();
            Actor actor = actors[order[boardingActor]];
            if (!actor.Ped.IsInVehicle(plane))
            {
                if (phaseAt == 0)
                {
                    if (!plane.IsSeatFree((GTA.VehicleSeat)seats[boardingActor])) throw Error("assento ocupado antes do embarque normal");
                    actor.Ped.IsPositionFrozen = false; actor.Ped.Task.ClearAllImmediately();
                    actor.Ped.Task.EnterVehicle(plane, (GTA.VehicleSeat)seats[boardingActor], -1, new GTA.PedMoveBlendRatio(1f),
                        GTA.EnterVehicleFlags.None);
                    nextBoardingDiagnostic = elapsed;
                    phaseAt = elapsed + 1; Log("EP07_EMBARQUE_INICIO", "Ator=" + actor.Id + "; Assento=" + seats[boardingActor] + "; TempoMs=" + elapsed + "; Flags=None; Parte=" + PartName(boardingPart) + "; sem WarpIn; shuffle fisico permitido");
                }
                if (elapsed >= nextBoardingDiagnostic)
                { nextBoardingDiagnostic = elapsed + 500; LogBoardingActor(actor, seats[boardingActor], elapsed, "embarque"); }
                if (elapsed - phaseAt >= 10000 || elapsed >= 30000)
                { LogBoardingActor(actor, seats[boardingActor], elapsed, "timeout"); throw Error("embarque normal não terminou; não reassentar durante ação"); }
                return;
            }
            if (!SameActor(plane.GetPedOnSeat((GTA.VehicleSeat)seats[boardingActor]), actor)) throw Error("o ator embarcou em assento inesperado");
            LogBoardingActor(actor, seats[boardingActor], elapsed, "assento_confirmado");
            Log("EP07_EMBARQUE_GETTER", "Ator=" + actor.Id + "; Assento=" + seats[boardingActor] + "; TempoMs=" + elapsed);
            boardingActor++; phaseAt = 0; if (boardingActor < order.Length) return;
            if (boardingPart == BoardingPart.EmbarqueDinjerChefe)
            {
                phase = 3; RequireParkedPartSeats();
                Log("EP07_SUBTOMADA_EMBARQUE_FISICO_CONCLUIDO", "Parte=" + PartName(boardingPart) +
                    "; TempoMs=" + elapsed + "; Chefe=-1; Dinjer=0; Djanira=fora; VooExecutado=false");
            }
            else
            {
            phase = 4; phaseAt = elapsed;
            LogFlightMotion(elapsed, "antes_liberar_pilotagem", true);
            foreach (Actor own in actors.Values) own.Ped.IsPositionFrozen = false;
            plane.IsEngineRunning = true; plane.IsPositionFrozen = false; plane.IsHandbrakeForcedOn = false;
            // PlaneTaxi normal do piloto, dentro dos120m ja sondados.
            // Nenhuma posicao/velocidade imposta; transicao exige movimento fisico.
            actors["O_CHEFE"].Ped.Task.PlaneTaxi(plane, origin + forward * 100f, 24f, 4f);
            taxiStarted = true;
            Log("EP07_TAXI_TAREFA", "TempoMs=" + elapsed + "; Plane=" + plane.Handle + "; Target=" + (origin + forward * 100f) +
                "; CruiseSpeed=24; TargetReach=4; PistaProbed=120; NoVelocitySetter=true; NoPositionSetter=true; TesteNativePendente=true");
            LogFlightMotion(elapsed, "depois_plane_taxi", true);
            }
        }
        if (phase == 4)
        {
            RequireSeats();
            if (plane.IsDead || GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, plane.Handle))
            { LogFlightMotion(elapsed, "falha_colisao_taxi", true); throw Error("aeronave colidiu durante taxi real"); }
            GTA.Math.Vector3 moved = plane.Position - origin;
            float taxiProgress = GTA.Math.Vector3.Dot(moved, forward);
            float taxiLateral = GTA.Math.Vector3.Dot(moved, right);
            int taxiBudgetMs = System.Math.Min(TaxiMaximumMs, System.Math.Max(0, 45000 - phaseAt - TaxiFlightReserveMs));
            // More time for observed engine warm-up, bounded by original45000ms and >=10s flight reserve.
            // B157 hit a measured floor rise at62.664m before the22m/s gate.
            // Start the same normal aerial task earlier; actual lift, not a setter, must clear it.
            if (!Finite(taxiProgress) || !Finite(taxiLateral) || taxiProgress < -0.05f || taxiProgress > 110f ||
                System.Math.Abs(taxiLateral) > 1f || elapsed - phaseAt > taxiBudgetMs)
                throw Error("taxi fisico saiu da faixa propria ou excedeu janela limitada; sem reposicionar");
            RequireTaxiEnvelope(elapsed, taxiProgress, taxiLateral, taxiBudgetMs);
            LogFlightMotion(elapsed, "taxi_fisico", false);
            float taxiMissionSpeedGate = boardingPart == BoardingPart.EmbarqueDjaniraDecolagem ? 10f : 22f;
            if (plane.Speed >= taxiMissionSpeedGate && taxiProgress >= 15f)
            {
                Log("EP07_TAXI_GETTERS", "TempoMs=" + elapsed + "; Progress=" + DiagnosticNumber(taxiProgress) + "; Lateral=" + DiagnosticNumber(taxiLateral) +
                    "; Speed=" + DiagnosticNumber(plane.Speed) + "; Position=" + plane.Position + "; NoPositionSetter=true; NoVelocitySetter=true");
                Log("EP07_TAXI_TRANSICAO_EXPERIMENTO", "TempoMs=" + elapsed + "; SpeedGate=" + DiagnosticNumber(taxiMissionSpeedGate) + "; ProgressGate=15; TaxiCruise=24; TaxiTarget=100; " +
                    "OneChangedParameter=MissionHandoffSpeedGate; GroundedMissionBUsesSameEnvelope=true; NativeSuccessPending=true; FlightHeightType=Int32; FlightHeightValue=" + (int)System.Math.Round(groundZ + 35f) +
                    "; FlightHeightBitsUInt32=" + ((uint)(int)System.Math.Round(groundZ + 35f)).ToString("X8", System.Globalization.CultureInfo.InvariantCulture) +
                    "; MinHeightType=Int32; MinHeightValue=10; MinHeightBitsUInt32=0000000A; ABIUnchanged=true; NativeSuccessPending=true");
                actors["O_CHEFE"].Ped.Task.StartPlaneMission(plane, origin + forward * 900f + new GTA.Math.Vector3(0f, 0f, 35f),
                    GTA.VehicleMissionType.GoTo, 35f, 10f, (int)System.Math.Round(groundZ + 35f), 10, -1f, false);
                missionStarted = true; phase = 1;
                LogFlightMotion(elapsed, "depois_taxi_plane_mission", true);
            }
        }
        if (phase == 3) RequireParkedPartSeats();
        if (phase == 1)
        {
            RequireSeats();
            if (plane.IsDead || GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, plane.Handle))
            { LogFlightMotion(elapsed, "falha_colisao_decolagem", true); throw Error("aeronave colidiu durante decolagem real"); }
            if (boardingPart == BoardingPart.EmbarqueDjaniraDecolagem && plane.HeightAboveGround < 10f)
            {
                GTA.Math.Vector3 missionMoved = plane.Position - origin;
                float missionProgress = GTA.Math.Vector3.Dot(missionMoved, forward);
                float missionLateral = GTA.Math.Vector3.Dot(missionMoved, right);
                int missionGroundBudgetMs = System.Math.Min(TaxiMaximumMs, System.Math.Max(0, 45000 - phaseAt - TaxiFlightReserveMs));
                if (!Finite(missionProgress) || !Finite(missionLateral) || missionProgress < -0.05f || missionProgress > 110f ||
                    System.Math.Abs(missionLateral) > 1f || elapsed - phaseAt > missionGroundBudgetMs)
                    throw Error("B aerial task still below10m left own runway or bounded ground budget; no repositioning");
                RequireTaxiEnvelope(elapsed, missionProgress, missionLateral, missionGroundBudgetMs);
            }
            if (plane.HeightAboveGround >= 10f && plane.Speed >= 20f) { phase = 2; Log("EP07_DECOLAGEM_GETTERS", "TempoMs=" + elapsed + "; Plane=" + plane.Position + "; HeightAboveGround=" + plane.HeightAboveGround + "; Speed=" + plane.Speed + "; Mission=" + missionStarted); }
        }
        if (phase == 2)
        {
            RequireSeats();
            if (plane.HeightAboveGround < 8f || plane.IsDead || GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, plane.Handle))
            { LogFlightMotion(elapsed, "falha_voo", true); throw Error("velum não permaneceu voando depois da decolagem"); }
        }
        if (phase == 1 || phase == 2) LogFlightMotion(elapsed, "pilotagem", false);
        if (elapsed >= nextLog) { nextLog = elapsed + 1000; Log("EP07_AVIAO_ESTADO", "TempoMs=" + elapsed + "; Fase=" + phase + "; Pos=" + plane.Position + "; Speed=" + plane.Speed + "; HeightAboveGround=" + plane.HeightAboveGround); }
        if (elapsed >= 45000)
        {
            if (boardingPart == BoardingPart.EmbarqueDinjerChefe)
            { if (phase != 3) throw Error("parte A nao completou duas entradas fisicas"); RequireParkedPartSeats(); }
            else if (phase != 2) throw Error("não decolou em45s físicos; não cortar a voz10s nem inventar voo");
            finished = true; running = false;
            Log("EP07_SUBTOMADA_CONCLUIDA", "Parte=" + PartName(boardingPart) +
                "; TempoMs=" + elapsed + "; Fase=" + phase + "; Integral=" + (boardingPart == BoardingPart.Integral) +
                "; Montagem45sAindaNecessaria=" + IsEditorialPart);
        }
    }
    // Somente getters C3, 500ms por GameTime; falha original continua fatal.
    // API191 collision record: consultar apenas Entity fisica, nunca Building.Model.
    private void LogFlightMotion(int elapsed, string stage, bool force)
    {
        if (id != "EP07_C03") return;
        int now = GTA.Game.GameTime;
        if (!force && now < nextFlightMotionDiagnostic) return;
        nextFlightMotionDiagnostic = now + 500;
        try
        {
            GTA.Vehicle own = OwnPlane(); Actor pilot = actors["O_CHEFE"]; RequireActor(pilot);
            GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
            pilot.Ped.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
            bool collided = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, own.Handle);
            int gear = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_LANDING_GEAR_STATE, own.Handle);
            GTA.Math.Vector3 normal = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_COLLISION_NORMAL_OF_LAST_HIT_FOR_ENTITY, own.Handle);
            GTA.Entity physical; bool physicalFound = own.TryGetPhysicalEntityFromLastCollisionRecord(out physical);
            int physicalHandle = 0, physicalType = 0, physicalModel = 0;
            if (physicalFound && !System.Object.ReferenceEquals(physical, null) && physical.Exists())
            {
                physicalHandle = physical.Handle;
                physicalType = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, physicalHandle);
                if (physicalType == 1 || physicalType == 2 || physicalType == 3)
                    physicalModel = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, physicalHandle);
            }
            float lowestModelBound = float.PositiveInfinity;
            for (int xi = 0; xi < 2; xi++) for (int yi = 0; yi < 2; yi++) for (int zi = 0; zi < 2; zi++)
            {
                GTA.Math.Vector3 corner = own.GetOffsetPosition(new GTA.Math.Vector3(xi == 0 ? planeMin.X : planeMax.X,
                    yi == 0 ? planeMin.Y : planeMax.Y, zi == 0 ? planeMin.Z : planeMax.Z));
                lowestModelBound = System.Math.Min(lowestModelBound, corner.Z);
            }
            Log("EP07_PILOTAGEM_DIAGNOSTICO", "Stage=" + stage + "; TempoMs=" + elapsed + "; GameTime=" + now +
                "; Parte=" + PartName(boardingPart) + "; Phase=" + phase + "; Plane=" + own.Handle +
                "; Pos=" + own.Position + "; Rotation=" + own.Rotation + "; Velocity=" + own.Velocity +
                "; Frozen=" + own.IsPositionFrozen + "; PilotFrozen=" + pilot.Ped.IsPositionFrozen +
                "; OwnDriver=" + SameActor(own.GetPedOnSeat(GTA.VehicleSeat.Driver), pilot) +
                "; PilotTask=" + task + "; TaskStatus=" + status + "; EngineRunning=" + own.IsEngineRunning +
                "; Driveable=" + own.IsDriveable + "; EngineHealth=" + DiagnosticNumber(own.EngineHealth) +
                "; PetrolHealth=" + DiagnosticNumber(own.PetrolTankHealth) + "; BodyHealth=" + DiagnosticNumber(own.BodyHealth) +
                "; RPM=" + DiagnosticNumber(own.CurrentRPM) + "; Throttle=" + DiagnosticNumber(own.ThrottlePower) +
                "; Brake=" + DiagnosticNumber(own.BrakePower) + "; Acceleration=" + DiagnosticNumber(own.Acceleration) +
                "; Speed=" + DiagnosticNumber(own.Speed) + "; HAG=" + DiagnosticNumber(own.HeightAboveGround) +
                "; Gear=" + gear + "; HasCollided=" + collided + "; RecordingCollisions=" + own.IsRecordingCollisions +
                "; BuildingRecord=" + own.HasCollidedWithBuildingOrAnimatedBuilding + "; Material=" + own.MaterialCollidingWith +
                "; CollisionNormal=" + normal + "; PhysicalRecord=" + physicalFound + "; PhysicalHandle=" + physicalHandle +
                "; PhysicalType=" + physicalType + "; PhysicalModel=" + physicalModel +
                "; ModelBoundsBottom=" + DiagnosticNumber(lowestModelBound) + "; GroundBase=" + DiagnosticNumber(groundZ) +
                "; ModelBoundsBottomMinusGroundBase=" + DiagnosticNumber(lowestModelBound - groundZ) +
                "; ModelBoundsNotCollisionMesh=true; GetterOnly=true; CollisionGuardUnchanged=true; NoBuildingModel=true");
            if (boardingPart == BoardingPart.EmbarqueDjaniraDecolagem && phase == 4)
            {
                float progress = GTA.Math.Vector3.Dot(own.Position - origin, forward);
                if (collided || (progress >= 45f && progress <= 75f))
                    LogTaxiGroundProjections(own, elapsed, stage, progress, lowestModelBound);
            }
        }
        catch (System.Exception error)
        { Log("EP07_PILOTAGEM_DIAGNOSTICO_ERRO", "Stage=" + stage + "; TempoMs=" + elapsed + "; GetterOnly=true; CollisionGuardUnchanged=true; " + error.GetType().Name + ": " + error.Message); }
    }

    // Diagnostico: somente getters. Este candidato altera flags/ordem; guardas e prazo permanecem.
    // Primario https://github.com/alloc8or/gta5-nativedb-data/blob/master/natives.json
    // CAN_SHUFFLE_SEAT 0x30785D90C956BF35(Vehicle,int); IS_ENTRY_POINT_FOR_SEAT_CLEAR 0x639431E895B9AA57(Ped,Vehicle,int,bool,bool).
    // Getters so em preparo/inicio: nenhum TASK/shuffle/warp ou novo criterio de aceite.
    // Five actual-ground getters only; model projections are NOT body collision/contact points.
    // Called from the existing500ms/forced diagnostic; no setter, task, retry or changed acceptance.
    private void LogTaxiGroundProjections(GTA.Vehicle own, int elapsed, string stage, float progress, float modelBottom)
    {
        GTA.Math.Vector3[] offsets = { GTA.Math.Vector3.Zero,
            new GTA.Math.Vector3(planeMin.X, 0f, 0f), new GTA.Math.Vector3(planeMax.X, 0f, 0f),
            new GTA.Math.Vector3(0f, planeMin.Y, 0f), new GTA.Math.Vector3(0f, planeMax.Y, 0f) };
        string[] labels = { "center", "left_model_X", "right_model_X", "rear_model_Y", "front_model_Y" };
        for (int index = 0; index < offsets.Length; index++)
        {
            GTA.Math.Vector3 point = own.GetOffsetPosition(offsets[index]);
            GTA.Math.Vector3 probe = point + new GTA.Math.Vector3(0f, 0f, 2f);
            Vector(point); Vector(probe);
            float ground; GTA.Math.Vector3 normal;
            bool found = GTA.World.GetGroundHeightAndNormal(probe, out ground, out normal);
            float along = GTA.Math.Vector3.Dot(point - origin, forward);
            int sample = System.Math.Max(0, System.Math.Min(11, (int)System.Math.Floor(along / 10f)));
            Log("EP07_TAXI_CHAO_PROJECAO", "Stage=" + stage + "; TempoMs=" + elapsed +
                "; GameTime=" + GTA.Game.GameTime + "; Plane=" + own.Handle + "; Index=" + index +
                "; Projection=" + labels[index] + "; Point=" + point + "; Probe=" + probe +
                "; Found=" + found + "; Ground=" + DiagnosticNumber(ground) + "; Normal=" + normal +
                "; GroundOutputValid=" + (found && Finite(ground) && Finite(normal.X) && Finite(normal.Y) && Finite(normal.Z)) +
                "; Progress=" + DiagnosticNumber(progress) + "; ProjectionForward=" + DiagnosticNumber(along) +
                "; CachedSample0=" + sample + "; CachedGround0=" + DiagnosticNumber(runwayGround[sample]) +
                "; CachedSample1=" + (sample + 1) + "; CachedGround1=" + DiagnosticNumber(runwayGround[sample + 1]) +
                "; AllModelBoundsBottom=" + DiagnosticNumber(modelBottom) +
                "; BoundsMinusActualGround=" + (found && Finite(ground) ? DiagnosticNumber(modelBottom - ground) : "unavailable") +
                "; NotBodyMesh=true; NotExactContactPoint=true; NotMaterialAtProjection=true; GroundGetterOnly=true; NoRequestCollision=true; NoSetters=true; CollisionGuardUnchanged=true");
        }
    }

    private void LogBoardingAccess(string stage)
    {
        try
        {
            if (id != "EP07_C03") return;
            GTA.Vehicle own = OwnPlane(); int[] seats = { -1, 0, 1 };
            foreach (int seat in seats)
            {
                bool canShuffle = GTA.Native.Function.Call<bool>(GTA.Native.Hash.CAN_SHUFFLE_SEAT, own.Handle, seat);
                Log("EP07_EMBARQUE_SHUFFLE_GETTER", "Stage=" + stage + "; Plane=" + own.Handle + "; Seat=" + seat +
                    "; CanShuffleSeat=" + canShuffle + "; GetterOnly=true; NoTaskIssued=true; GuardUnchanged=true; DoesNotProveTargetLink=true");
            }
            foreach (Actor actor in actors.Values)
            {
                RequireActor(actor);
                foreach (int seat in seats)
                {
                    bool clear = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTRY_POINT_FOR_SEAT_CLEAR,
                        actor.Ped.Handle, own.Handle, seat, false, true);
                    Log("EP07_EMBARQUE_ACCESS_GETTER", "Stage=" + stage + "; Plane=" + own.Handle + "; Actor=" + actor.Id +
                        "; ActorHandle=" + actor.Ped.Handle + "; Seat=" + seat + "; Side=false; OnEnter=true; EntryPointClear=" + clear +
                        "; SideOnlyRelevantToBikes=true; GetterOnly=true; NoTaskIssued=true; GuardUnchanged=true; DoesNotProveReachableSeat=true");
                }
            }
        }
        catch (System.Exception ex)
        { Log("EP07_EMBARQUE_ACCESS_DIAG_ERRO", "Stage=" + stage + "; " + ex.Message + "; GetterOnly=true; GuardUnchanged=true"); }
    }
    private void LogBoardingGeometry(string stage)
    {
        try
        {
            GTA.Vehicle own = OwnPlane();
            string[] names = { "door_dside_f", "door_pside_f", "door_dside_r", "door_pside_r",
                "seat_dside_f", "seat_pside_f", "seat_dside_r", "seat_pside_r" };
            foreach (string name in names)
            {
                GTA.EntityBone bone = own.Bones[name];
                bool valid = bone.IsValid;
                Log("EP07_EMBARQUE_BONE_GETTER", "Stage=" + stage + "; Plane=" + own.Handle +
                    "; BoneQuery=" + name + "; Index=" + bone.Index + "; Valid=" + valid +
                    "; World=" + (valid ? bone.Position.ToString() : "absent") +
                    "; PlaneLocal=" + (valid ? own.GetPositionOffset(bone.Position).ToString() : "absent") +
                    "; GettersOnly=true; QueryDoesNotAssertBoneExists=true");
            }
            foreach (Actor actor in actors.Values) LogBoardingActor(actor, -99, -1, stage);
        }
        catch (System.Exception ex) { Log("EP07_EMBARQUE_DIAG_ERRO", stage + "; " + ex.Message + "; guards unchanged"); }
    }
    private void LogBoardingActor(Actor actor, int requestedSeat, int elapsed, string stage)
    {
        try
        {
            RequireActor(actor); GTA.Vehicle own = OwnPlane(); GTA.Ped ped = actor.Ped;
            GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
            ped.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
            GTA.Vehicle trying = ped.VehicleTryingToEnter;
            int tryingHandle = DiagnosticHandle(trying);
            GTA.Math.Vector3 position = ped.Position, head = ped.Bones[GTA.Bone.SkelHead].Position;
            int enteringSeat = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER, ped.Handle);
            Log("EP07_EMBARQUE_ESTADO_GETTERS", "Stage=" + stage + "; TempoMs=" + elapsed +
                "; SinceRequestMs=" + (phaseAt == 0 ? -1 : elapsed - phaseAt) + "; Ator=" + actor.Id +
                "; Handle=" + ped.Handle + "; Pos=" + position + "; PlaneLocal=" + own.GetPositionOffset(position) +
                "; Head=" + head + "; HeadPlaneLocal=" + own.GetPositionOffset(head) +
                "; Heading=" + DiagnosticNumber(ped.Heading) + "; Speed=" + DiagnosticNumber(ped.Speed) +
                "; Frozen=" + ped.IsPositionFrozen + "; Health=" + ped.Health +
                "; InOwnPlane=" + ped.IsInVehicle(own) + "; ActualSeat=" + ped.SeatIndex +
                "; RequestedSeat=" + requestedSeat + "; Entering=" + ped.IsEnteringVehicle +
                "; TryingVehicle=" + tryingHandle + "; TryingOwn=" + (tryingHandle == own.Handle) +
                "; TryingSeat=" + enteringSeat + "; Task=" + task + "; TaskHash=" + (uint)task +
                "; Status=" + status + "; Plane=" + own.Handle + "; PlanePos=" + own.Position +
                "; PlaneHeading=" + DiagnosticNumber(own.Heading) + "; PlaneFrozen=" + own.IsPositionFrozen +
                "; PlaneSpeed=" + DiagnosticNumber(own.Speed) + "; PlaneHAG=" + DiagnosticNumber(own.HeightAboveGround) +
                "; Flags=None; Ordem=DINJER1,DJANIRA0,CHEFE-1; CadenceMs=500; GettersOnly=true; AcceptanceUnchanged=true");
            for (int door = 0; door < 4; door++)
            {
                // Hashes literais conferidos no repositorio primario citizenfx/natives (URLs no freeze).
                bool valid = GTA.Native.Function.Call<bool>((GTA.Native.Hash)0x645F4B6E8499F632UL, own.Handle, door);
                if (!valid)
                { Log("EP07_EMBARQUE_PORTA_GETTERS", "Stage=" + stage + "; TempoMs=" + elapsed + "; Ator=" + actor.Id + "; Door=" + door + "; Valid=false; NoEntryQuery=true"); continue; }
                GTA.Math.Vector3 entry = GTA.Native.Function.Call<GTA.Math.Vector3>((GTA.Native.Hash)0xC0572928C0ABFDA3UL, own.Handle, door);
                float ratio = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, own.Handle, door);
                Vector(entry);
                Log("EP07_EMBARQUE_PORTA_GETTERS", "Stage=" + stage + "; TempoMs=" + elapsed + "; Ator=" + actor.Id +
                    "; Door=" + door + "; Valid=true; Ratio=" + DiagnosticNumber(ratio) +
                    "; Entry=" + entry + "; EntryPlaneLocal=" + own.GetPositionOffset(entry) +
                    "; DistanceActor=" + DiagnosticNumber((position - entry).Length()) + "; GettersOnly=true");
            }
        }
        catch (System.Exception ex) { Log("EP07_EMBARQUE_DIAG_ERRO", stage + "; Ator=" + actor.Id + "; " + ex.Message + "; guards unchanged"); }
    }
    private void UpdateService(int elapsed)
    {
        RequireGlass(); GTA.Ped chef = actors["O_CHEFE"].Ped, djanira = actors["DJANIRA_POBRE"].Ped;
        ProcessServiceGlass(elapsed);
        if (!serviceStarted)
        {
            serviceStarted = true; serviceAt = GTA.Game.GameTime;
            LogServiceDiagnostic(elapsed, "antesPlay"); nextServiceDiagnostic = elapsed + 200;
            GTA.AnimationFlags flags = GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary | GTA.AnimationFlags.ProcessAttachmentsOnStart;
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, chef.Handle, GiveDictionary, GiveClip, 4f, -4f, -1, (int)flags, 0f, false, false, false);
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, djanira.Handle, GiveDictionary, ReceiveClip, 4f, -4f, -1, (int)flags, 0f, false, false, false);
            Log("EP07_SERVICO_INICIADO", "Chefe→Djanira; serviço secundário preserva assentos e missão primária; duração real=" + giveDuration + "/" + receiveDuration);
            return;
        }
        if (serviceDone) return;
        if (GTA.Game.GameTime == serviceAt) return;
        uint since = unchecked((uint)(GTA.Game.GameTime - serviceAt));
        if (elapsed >= nextServiceDiagnostic)
        {
            nextServiceDiagnostic = elapsed + 200;
            LogServiceDiagnostic(elapsed, "duranteServico");
        }
        ObserveServicePhase(chef, GiveClip, giveDuration, since, ref givePhase);
        ObserveServicePhase(djanira, ReceiveClip, receiveDuration, since, ref receivePhase);
        float currentPhase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, djanira.Handle, GiveDictionary, ReceiveClip);
        if (!serviceObserved && Finite(currentPhase) && currentPhase >= 0.05f && currentPhase <= 0.95f &&
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, chef.Handle, GiveDictionary, GiveClip, 3) &&
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, djanira.Handle, GiveDictionary, ReceiveClip, 3))
        {
            // Native109: ChefRH -> DjaniraLH=0.05367258m emfase0.9156; a taça deve chegar realmente à LH.
            GTA.PedBone hand = djanira.Bones[GTA.Bone.SkelLeftHand];
            float distance = (glass.Position - hand.Position).Length();
            if (!Finite(distance)) throw Error("distância física da taça inválida");
            if (distance <= 0.35f)
            {
                if (!glass.IsAttachedTo(chef)) throw Error("taça perdeu o doador antes do contato");
                glass.Detach(false, false); glass.IsPositionFrozen = false; glass.IsCollisionEnabled = false;
                glass.AttachTo(hand, GTA.Math.Vector3.Zero, GTA.Math.Vector3.Zero, false, false, false, false, GTA.EulerRotationOrder.YXZ, true, false);
                glass.ProcessEntityAttachments();
                if (!glass.IsAttachedTo(djanira)) throw Error("attachment ao receptor ainda não confirmado");
                serviceObserved = true; Log("EP07_SERVICO_CONTATO_REAL", "TempoMs=" + elapsed + "; Distancia=" + distance + "; Phase=" + receivePhase + "; DonorBone=SkelRightHand57005; ReceiverBone=SkelLeftHand18905; osso/mão real, nenhum objeto atravessa a cabine");
            }
        }
        if (since + 1 >= System.Math.Max(giveDuration, receiveDuration) * 1000f && givePhase >= 0.9f && receivePhase >= 0.9f && serviceObserved)
        {
            chef.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(GiveDictionary, GiveClip)); djanira.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(GiveDictionary, ReceiveClip));
            serviceDone = true;
        }
        if (elapsed >= 6000 && !serviceDone) throw Error("serviço real não atingiu contato e fim dos clipes em6s; sem entrega por relógio");
    }
    private static void ObserveServicePhase(GTA.Ped ped, string clip, float duration, uint since, ref float maximum)
    {
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, ped.Handle, GiveDictionary, clip, 3);
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, ped.Handle, GiveDictionary, clip);
        if ((!playing || phase == -1f) && since < 500) return;
        if (!playing && maximum >= 0.9f && since + 1 >= duration * 1000f) return;
        if (!playing || !Finite(phase) || phase < 0f || phase > 1f) throw Error("fase/execução da entrega real inválida; Clipe=" + clip + "; Phase=" + phase + "; SinceStart=" + since);
        maximum = System.Math.Max(maximum, phase);
    }
    // Vinculo110 e exclusivo do servico7C4: recolocar a mesma taca no mesmo osso depois do assentamento.
    // Nunca reposiciona atriz/prop por XYZ; ProcessEntityAttachments atualiza o vinculo pelo motor (API191).
    private void RebindGlassAfterSeats()
    {
        RequireGlass(); RequireActor(actors["O_CHEFE"]);
        GTA.Ped chef = actors["O_CHEFE"].Ped;
        if (!chef.IsInVehicle() || !glass.IsAttachedTo(chef)) throw Error("rebind da taca exige doador assentado e attachment inicial proprio");
        GTA.PedBone hand = chef.Bones[GTA.Bone.SkelRightHand];
        if (System.Object.ReferenceEquals(hand, null) || !hand.IsValid) throw Error("osso SKEL_R_Hand real indisponivel depois do assentamento");
        GTA.Math.Vector3 before = glass.Position;
        bool frozenBefore = glass.IsPositionFrozen;
        glass.Detach(false, false); glass.IsPositionFrozen = false; glass.IsCollisionEnabled = false;
        glass.AttachTo(hand, GTA.Math.Vector3.Zero, GTA.Math.Vector3.Zero, false, false, false, false, GTA.EulerRotationOrder.YXZ, true, false);
        glass.ProcessEntityAttachments();
        GTA.Math.Vector3 anchor = hand.GetOffsetPosition(GTA.Math.Vector3.Zero), after = glass.Position;
        float distance = (after - anchor).Length(); Vector(anchor); Vector(after);
        Log("EP07_SERVICO_REBIND_ASSENTADO", "Donor=" + chef.Handle + "; Bone=SkelRightHand57005; Offset=(0,0,0); Rotation=(0,0,0); Before=" + before +
            "; After=" + after + "; Anchor=" + anchor + "; Distance=" + DiagnosticNumber(distance) + "; FrozenBefore=" + frozenBefore + "; FrozenAfter=" + glass.IsPositionFrozen +
            "; AttachedDonor=" + glass.IsAttachedTo(chef) + "; Collision=" + glass.IsCollisionEnabled + "; Visible=" + glass.IsVisible + "; PreparationOnly=true; NoPositionSetter=true; VisualPending=true");
        if (!Finite(distance) || distance > 0.35f || glass.IsPositionFrozen || !glass.IsAttachedTo(chef))
            throw Error("vinculo real da taca nao confirmou mao propria depois do assentamento");
    }
    private void ProcessServiceGlass(int elapsed)
    {
        GTA.Ped owner = serviceObserved ? actors["DJANIRA_POBRE"].Ped : actors["O_CHEFE"].Ped;
        GTA.Bone bone = serviceObserved ? GTA.Bone.SkelLeftHand : GTA.Bone.SkelRightHand;
        GTA.PedBone hand = owner.Bones[bone];
        if (!hand.IsValid || !glass.IsAttachedTo(owner) || glass.IsPositionFrozen) throw Error("taca do servico perdeu vinculo/mao propria ou ficou congelada");
        bool diagnostic = elapsed < 6000 && elapsed >= nextGlassDiagnostic;
        GTA.Math.Vector3 before = glass.Position, beforeAnchor = hand.Position;
        glass.ProcessEntityAttachments();
        GTA.Math.Vector3 after = glass.Position, afterAnchor = hand.Position;
        Vector(before); Vector(after); Vector(beforeAnchor); Vector(afterAnchor);
        if (glass.IsPositionFrozen || !glass.IsAttachedTo(owner)) throw Error("processamento do attachment nao preservou taca/owner");
        if (!diagnostic) return;
        nextGlassDiagnostic = elapsed + 200;
        GTA.Math.Vector3 minimum, maximum; glass.Model.GetDimensions(out minimum, out maximum); Vector(minimum); Vector(maximum);
        GTA.Math.Vector3 phR = owner.Bones[GTA.Bone.PHRightHand].Position, phL = owner.Bones[GTA.Bone.PHLeftHand].Position;
        Log("EP07_SERVICO_ATTACHMENT", "TempoMs=" + elapsed + "; Owner=" + owner.Handle + "; Bone=" + bone + "; Prop=" + glass.Handle + "; Model=" + glass.Model.Hash +
            "; BoundsMin=" + minimum + "; BoundsMax=" + maximum + "; Offset=(0,0,0); Rotation=(0,0,0); BeforePropOrigin=" + before + "; BeforeHandAnchor=" + beforeAnchor +
            "; BeforeDistance=" + DiagnosticNumber((before - beforeAnchor).Length()) + "; AfterPropOrigin=" + after + "; AfterHandAnchor=" + afterAnchor +
            "; AfterDistance=" + DiagnosticNumber((after - afterAnchor).Length()) + "; PHRightBaseline=" + phR + "; PHLeftBaseline=" + phL +
            "; PropToPHRight=" + DiagnosticNumber((after - phR).Length()) + "; PropToPHLeft=" + DiagnosticNumber((after - phL).Length()) +
            "; Frozen=" + glass.IsPositionFrozen + "; Collision=" + glass.IsCollisionEnabled + "; Visible=" + glass.IsVisible + "; AttachedDonor=" + glass.IsAttachedTo(actors["O_CHEFE"].Ped) +
            "; AttachedReceiver=" + glass.IsAttachedTo(actors["DJANIRA_POBRE"].Ped) + "; ServiceObserved=" + serviceObserved + "; ProcessOwnAttachment=true; NoPositionSetter=true; VisualPending=true");
    }
    // Diagnostico110: apenas getters/log, sem modificar contato, animacao, assentos ou reserva6s.
    private void LogServiceDiagnostic(int elapsed, string stage)
    {
        try
        {
            GTA.Vehicle aircraft = null;
            foreach (GTA.Entity entity in airborne.OwnedEntities)
            {
                GTA.Vehicle candidate = entity as GTA.Vehicle;
                if (candidate != null && candidate.Exists() && candidate.Model.Hash == new GTA.Model("velum").Hash)
                { aircraft = candidate; break; }
            }
            if (aircraft == null) { Log("EP07_SERVICO_DIAG_INDISPONIVEL", "TempoMs=" + elapsed + "; Fase=" + stage + "; VelumProprio=false"); return; }
            GTA.Ped donor = actors["O_CHEFE"].Ped, receiver = actors["DJANIRA_POBRE"].Ped;
            GTA.Math.Vector3 donorR = donor.Bones[GTA.Bone.SkelRightHand].Position, donorL = donor.Bones[GTA.Bone.SkelLeftHand].Position;
            GTA.Math.Vector3 receiverR = receiver.Bones[GTA.Bone.SkelRightHand].Position, receiverL = receiver.Bones[GTA.Bone.SkelLeftHand].Position;
            GTA.Math.Vector3 glassWorld = glass.Position;
            GTA.Entity owner = glass.AttachedEntity;
            GTA.Ped driver = aircraft.GetPedOnSeat(GTA.VehicleSeat.Driver), passenger = aircraft.GetPedOnSeat(GTA.VehicleSeat.Passenger);
            bool donorPlaying = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, donor.Handle, GiveDictionary, GiveClip, 3);
            bool receiverPlaying = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, receiver.Handle, GiveDictionary, ReceiveClip, 3);
            float donorPhase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, donor.Handle, GiveDictionary, GiveClip);
            float receiverPhase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, receiver.Handle, GiveDictionary, ReceiveClip);
            Log("EP07_SERVICO_GEOMETRIA", "TempoMs=" + elapsed + "; Fase=" + stage + "; SinceStart=" + unchecked((uint)(GTA.Game.GameTime - serviceAt)) +
                "; Dict=" + GiveDictionary + "; GiveClip=" + GiveClip + "; GivePlaying=" + donorPlaying + "; GivePhase=" + DiagnosticNumber(donorPhase) +
                "; GiveMax=" + DiagnosticNumber(givePhase) + "; GiveDuration=" + DiagnosticNumber(giveDuration) +
                "; ReceiveClip=" + ReceiveClip + "; ReceivePlaying=" + receiverPlaying + "; ReceivePhase=" + DiagnosticNumber(receiverPhase) +
                "; ReceiveMax=" + DiagnosticNumber(receivePhase) + "; ReceiveDuration=" + DiagnosticNumber(receiveDuration) +
                "; ContactObserved=" + serviceObserved + "; ServiceDone=" + serviceDone +
                "; Plane=" + aircraft.Handle + "; PlaneWorld=" + aircraft.Position + "; PlaneHeading=" + DiagnosticNumber(aircraft.Heading) +
                "; DriverSeat=" + DiagnosticHandle(driver) + "; PassengerSeat=" + DiagnosticHandle(passenger) +
                "; Donor=" + donor.Handle + "; DonorHeading=" + DiagnosticNumber(donor.Heading) + "; DonorYawPlane=" + DiagnosticNumber(DiagnosticYaw(donor.Heading - aircraft.Heading)) +
                "; Receiver=" + receiver.Handle + "; ReceiverHeading=" + DiagnosticNumber(receiver.Heading) + "; ReceiverYawPlane=" + DiagnosticNumber(DiagnosticYaw(receiver.Heading - aircraft.Heading)) +
                "; DonorRightWorld=" + donorR + "; DonorRightPlane=" + aircraft.GetPositionOffset(donorR) +
                "; DonorLeftWorld=" + donorL + "; DonorLeftPlane=" + aircraft.GetPositionOffset(donorL) +
                "; ReceiverRightWorld=" + receiverR + "; ReceiverRightPlane=" + aircraft.GetPositionOffset(receiverR) +
                "; ReceiverLeftWorld=" + receiverL + "; ReceiverLeftPlane=" + aircraft.GetPositionOffset(receiverL) +
                "; GlassWorld=" + glassWorld + "; GlassPlane=" + aircraft.GetPositionOffset(glassWorld) +
                "; GlassOwner=" + DiagnosticHandle(owner) + "; AttachedDonor=" + glass.IsAttachedTo(donor) + "; AttachedReceiver=" + glass.IsAttachedTo(receiver) +
                "; GlassToReceiverRH=" + DiagnosticNumber((glassWorld - receiverR).Length()) + "; GlassToReceiverLH=" + DiagnosticNumber((glassWorld - receiverL).Length()) +
                "; DonorRHToReceiverRH=" + DiagnosticNumber((donorR - receiverR).Length()) + "; DonorRHToReceiverLH=" + DiagnosticNumber((donorR - receiverL).Length()) +
                "; DonorLHToReceiverRH=" + DiagnosticNumber((donorL - receiverR).Length()) + "; DonorLHToReceiverLH=" + DiagnosticNumber((donorL - receiverL).Length()) +
                "; DonorTask=" + donor.CurrentScriptTaskNameHash + "; DonorTaskStatus=" + donor.CurrentScriptTaskStatus +
                "; ReceiverTask=" + receiver.CurrentScriptTaskNameHash + "; ReceiverTaskStatus=" + receiver.CurrentScriptTaskStatus +
                "; CadenceMs=200; GettersOnly=true; AcceptanceUnchanged=true");
        }
        catch (System.Exception ex)
        { Log("EP07_SERVICO_DIAG_ERRO", "TempoMs=" + elapsed + "; Fase=" + stage + "; " + ex.Message + "; fluxo/guards originais preservados"); }
    }
    private static int DiagnosticHandle(GTA.Entity entity)
    { return System.Object.ReferenceEquals(entity, null) || !entity.Exists() ? 0 : entity.Handle; }
    private static float DiagnosticYaw(float value)
    { value %= 360f; if (value > 180f) value -= 360f; if (value < -180f) value += 360f; return value; }
    private static string DiagnosticNumber(float value)
    { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    // Chamada terminal somente por F10/Dispose/Cleanup, fora de Start/Update/Reset.
    // Nao reassenta, nao muda posicao/velocidade e nao apaga atores nesta etapa.
    public void StopOwnFlightForFinalCleanup()
    {
        Active(); running = false;
        if (id != "EP07_C03" || airborne != null || (!missionStarted && !taxiStarted) || resources == null || plane == null) return;
        plane = OwnPlane(); CheckOccupants();
        Actor pilot = actors["O_CHEFE"]; RequireActor(pilot);
        GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
        pilot.Ped.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
        bool ownDriver = pilot.Ped.IsInVehicle(plane) && SameActor(plane.GetPedOnSeat(GTA.VehicleSeat.Driver), pilot);
        if (!finalCleanupPilotMissionCancelled && ownDriver && task == GTA.ScriptTaskNameHash.VehicleMission)
        {
            finalCleanupPilotMissionCancelled = true;
            pilot.Ped.Task.ClearAllImmediately();
            Log("EP07_MISSAO_PROPRIA_CANCELADA_LIMPEZA", "Ator=O_CHEFE; Plane=" + plane.Handle + "; Task=" + task + "; Status=" + status + "; OwnDriver=true; ForaDaAcao=true; PedidoUnico=true");
        }
        if (!plane.IsPositionFrozen) plane.IsPositionFrozen = true;
        if (!plane.IsPositionFrozen) throw Error("aviao proprio nao congelou na posicao atual durante limpeza terminal");
        if (!finalCleanupFlightStopLogged)
        {
            finalCleanupFlightStopLogged = true;
            Log("EP07_VOO_PROPRIO_PARADO_LIMPEZA", "Plane=" + plane.Handle + "; Pos=" + plane.Position + "; Frozen=" + plane.IsPositionFrozen + "; OwnDriver=" + ownDriver + "; Task=" + task + "; Status=" + status + "; ForaDaAcao=true; SemReposicionar=true; SomenteAeronavePropria=true");
        }
    }

    public void Cleanup()
    {
        if (disposed) return;
        StopOwnFlightForFinalCleanup();
        if (!ReleaseActorsForReset()) throw Error("saída de assentos pendente; chamar limpeza no próximo Tick");
        if (airborne != null) airborne.Dispose();
        RestoreBoardingCamera();
        if (resources != null) resources.Dispose();
        foreach (Actor actor in actors.Values)
            if (Own(actor)) { actor.Ped.IsPositionFrozen = actor.Frozen; actor.Ped.IsInvincible = actor.Invincible; actor.Ped.BlockPermanentEvents = actor.Events; }
        if (dictRequested && !dictWasLoaded) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, GiveDictionary);
        actors.Clear(); props = null; glass = null; plane = null; airborne = null; resources = null;
        prepared = ready = resetDone = running = finished = false;
    }
    public void Dispose() { Cleanup(); disposed = true; }
    private void CaptureBoardingCamera()
    {
        if (id != "EP07_C03") throw Error("camera de decolagem exige apenas7C3");
        if (boardingCameraHandle == 0)
        {
            previousBoardingCamera = GTA.ScriptCameraDirector.RenderingCam;
            boardingCamera = GTA.Camera.Create(GTA.ScriptedCameraNameHash.DefaultScriptedCamera,
                plane.Position, GTA.Math.Vector3.Zero, 50f, false, GTA.EulerRotationOrder.YXZ);
            if (System.Object.ReferenceEquals(boardingCamera, null) || !boardingCamera.Exists())
                throw Error("nao foi possivel criar camera propria da decolagem");
            boardingCameraHandle = boardingCamera.Handle; boardingCamera.NearClip = 0.1f;
        }
        if (System.Object.ReferenceEquals(boardingCamera, null) || !boardingCamera.Exists() || boardingCamera.Handle != boardingCameraHandle)
            throw Error("camera propria da decolagem perdeu identidade");
        boardingCamera.IsActive = true; GTA.ScriptCameraDirector.StartRendering();
        Log("EP07_CAMERA_DECOLAGEM_PRONTA", "Camera=" + boardingCameraHandle + "; Velum=" + plane.Handle + "; LONGE50; ContagemFixa=true; FinalFixo=true; TesteVisual=pendente");
    }
    private void UpdateBoardingCamera()
    {
        plane = OwnPlane();
        if (System.Object.ReferenceEquals(boardingCamera, null) || !boardingCamera.Exists() || boardingCamera.Handle != boardingCameraHandle)
            throw Error("camera propria da decolagem deixou de existir");
        GTA.Math.Vector3 position = plane.Position, actualForward = plane.ForwardVector, actualRight = plane.RightVector;
        Vector(position); Vector(actualForward); Vector(actualRight);
        // LONGE de tres quartos: referencia e eixos do unico velum proprio, inclusive depois de sair do hangar.
        // Parte A apenas: piloto pela porta esquerda; corte para direita quando chega a vez de Dinjer.
        // Reset fixa a esquerda na contagem. Integral/B e hold final conservam o lado direito original.
        float boardingSide = boardingPart == BoardingPart.EmbarqueDinjerChefe && phase == 0 && boardingActor == 0 ? -8f : 8f;
        // A porta esquerda e vista pela frente: atras ha parede real do hangar (preview129 recusado).
        float boardingFront = boardingPart == BoardingPart.EmbarqueDinjerChefe && phase == 0 && boardingActor == 0 ? 12f : -12f;
        boardingCamera.Position = position + actualForward * boardingFront + actualRight * boardingSide + new GTA.Math.Vector3(0f, 0f, 3f);
        boardingCamera.PointAt(position + new GTA.Math.Vector3(0f, 0f, 0.8f));
        boardingCamera.FieldOfView = 50f;
    }
    private void RestoreBoardingCamera()
    {
        if (boardingCameraHandle == 0) return;
        GTA.Camera rendering = GTA.ScriptCameraDirector.RenderingCam;
        if (!System.Object.ReferenceEquals(rendering, null) && rendering.Handle == boardingCameraHandle)
        {
            if (!System.Object.ReferenceEquals(boardingCamera, null) && boardingCamera.Exists()) boardingCamera.IsActive = false;
            if (!System.Object.ReferenceEquals(previousBoardingCamera, null) && previousBoardingCamera.Exists())
            { previousBoardingCamera.IsActive = true; GTA.ScriptCameraDirector.StartRendering(); }
            else GTA.ScriptCameraDirector.StopRendering(false);
        }
        if (!System.Object.ReferenceEquals(boardingCamera, null) && boardingCamera.Exists()) boardingCamera.Delete();
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_CAM_EXIST, boardingCameraHandle))
            throw Error("camera propria da decolagem nao foi removida; preservar referencia para repetir limpeza");
        boardingCamera = previousBoardingCamera = null; boardingCameraHandle = 0;
    }
    private void RequireGlass()
    {
        if (props == null || glass == null || !glass.Exists() || glass.Handle != glassHandle || glass.Model.Hash != glassModel || glass.MemoryAddress != glassAddress || !glass.IsVisible)
            throw Error("taça própria perdeu identidade/visibilidade");
        props.GetProp("TACA_JANTAR");
    }
    private GTA.Vehicle OwnPlane() { GTA.Vehicle value = resources.GetVehicle("AVIAO_EP07"); if (value.IsDead) throw Error("velum próprio deixou de existir vivo"); return value; }
    private void CheckOccupants()
    {
        foreach (GTA.Ped occupant in plane.Occupants)
        {
            bool owned = false; foreach (Actor actor in actors.Values) if (SameActor(occupant, actor)) owned = true;
            if (!owned) throw Error("ocupante alheio no avião próprio; não expulsar nem apagar");
        }
    }
    private void RequireSeats()
    {
        if (!SameActor(plane.GetPedOnSeat(GTA.VehicleSeat.Driver), actors["O_CHEFE"]) ||
            !SameActor(plane.GetPedOnSeat(GTA.VehicleSeat.Passenger), actors["DJANIRA_POBRE"]) || !SameActor(plane.GetPedOnSeat((GTA.VehicleSeat)1), actors["DINJER"]))
            throw Error("ocupantes não permaneceram nos três assentos próprios");
    }
    private static bool SameActor(GTA.Ped ped, Actor actor) { return !System.Object.ReferenceEquals(ped, null) && ped.Exists() && Own(actor) && ped.Handle == actor.Handle && ped.MemoryAddress == actor.Address; }
    private static bool Own(Actor actor) { return actor != null && actor.Ped.Exists() && !actor.Ped.IsPlayer && actor.Ped.Handle == actor.Handle && actor.Ped.MemoryAddress == actor.Address && actor.Ped.Model.Hash == actor.Model; }
    private static void RequireActor(Actor actor) { if (!Own(actor) || actor.Ped.IsDead || actor.Ped.Health < actor.Health) throw Error("identidade/vida do ator de voo mudou"); }
    private void RequireActors() { foreach (Actor actor in actors.Values) RequireActor(actor); }
    private static bool ReadHit(int ray, out bool collided, out GTA.Math.Vector3 point,
        out GTA.Math.Vector3 normal, out GTA.Entity physical)
    {
        collided = false; point = GTA.Math.Vector3.Zero; normal = GTA.Math.Vector3.Zero; physical = null;
        GTA.ShapeTestHandle probe = new GTA.ShapeTestHandle(); probe.NativeValue = unchecked((ulong)(uint)ray);
        GTA.ShapeTestResult result;
        GTA.ShapeTestStatus status = probe.GetResult(out result);
        if (status == GTA.ShapeTestStatus.NotReady) return false;
        if (status != GTA.ShapeTestStatus.Ready) throw Error("sonda da pista nao concluiu");
        collided = result.DidHit; point = result.HitPosition; normal = result.SurfaceNormal; Vector(point); Vector(normal);
        if (collided) result.TryGetHitEntity(out physical);
        return true;
    }
    private static float Horizontal(GTA.Math.Vector3 v) { return (float)System.Math.Sqrt(v.X * v.X + v.Y * v.Y); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Vector(GTA.Math.Vector3 v) { if (!Finite(v.X) || !Finite(v.Y) || !Finite(v.Z)) throw Error("vetor não finito"); }
    private void RequireReady() { Active(); if (!IsReady) throw Error("aguarde preparo real do voo/serviço"); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("CenaVooTemporadaDiretor"); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Voo EP07: " + message); }
    private void Log(string tag, string message) { if (log != null) log(tag, message); }
}

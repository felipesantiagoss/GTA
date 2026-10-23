// Queda física do EP01 e ensaio: integração não equivale a confirmação visual.
// Coordenadas de entrada são marcas reais de CANAL_LS; alturas/deslocamentos são relativos.
public sealed class CenaQuedaDiretor : System.IDisposable
{
    public sealed class QuedaSpec
    {
        public string Cena = "EP01_C01", Local = "CANAL_LS";
        public GTA.Math.Vector3 Agua, Direcao;
        public GTA.Math.Vector3? EntradaRampa, TopoRampa, Fuga;
        public bool CompletarFuga, OlhosDesdeInicio, InverterEixo, EnsaioPassivo, FilmarQuedaExterna;
        // EP07_C04: atores em assentos reais; não cria cabine caminhável.
        public bool SomenteVoo, InsertoEmpurrao;
        public GTA.Ped PilotoDoElenco, PassageiroTraseiro;
        public float AlturaRelativa = 30f, VelocidadeAviao = 35f;
        public int DuracaoSolicitadaMs = 14000, QuedaAlvoMs = 4000, TimeoutMs = 60000;
    }

    private sealed class ActorState
    {
        internal GTA.Ped Ped;
        internal int Handle, Model, Health;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Position;
        internal float Heading;
        internal bool Invincible, Frozen, Ragdoll, Events, Drowning, InstantWater, LeavingPlane;
    }

    private sealed class VolumeProbe
    {
        internal int Ray, Index;
        internal string Stage;
        internal GTA.Math.Vector3 From, To;
        internal float Radius;
    }

    private sealed class FlightVolume
    {
        internal GTA.Math.Vector3 Origin, Right, Up, Forward, From, To;
        internal float Radius;
        internal int Sent, Completed, StartedAt, CompletedAt, Bands;
        internal readonly System.Collections.Generic.List<VolumeProbe> Pending = new System.Collections.Generic.List<VolumeProbe>();
    }

    private readonly System.Action<string, string> log;
    private ObjetosDiretor resources;
    private ActorState actor, pilot, rearPassenger;
    private bool borrowedPilot;
    private GTA.Ped cabinFocus;
    private QuedaSpec spec;
    private GTA.Model pilotModel;
    private bool modelRequested, modelWasLoaded, prepared, ready, resetDone, running, finished, success, failed, disposed, actorMarkCaptured, resetWaitingSeats, routeReady;
    private GTA.Vehicle plane;
    private GTA.Camera camera, previousCamera;
    // Diagnostico113 somente7C4: getters por tick; primeiros300ms de cada foco logam todos os frames.
    private int cabinCameraDiagnosticUntil, nextCabinCameraDiagnostic, lastCabinRenderingHandle;
    private bool cabinCameraDiagnosticRenderKnown;
    // Somente7C4: pontos locais medidos enquanto o velum proprio esta congelado.
    private readonly System.Collections.Generic.Dictionary<int, GTA.Math.Vector3> cabinCameraHeadLocalByActor =
        new System.Collections.Generic.Dictionary<int, GTA.Math.Vector3>();
    private int cabinCameraAttachedFocus;
    private bool IsDialogueCabinCamera { get { return spec != null && spec.Cena == "EP07_C04" && spec.SomenteVoo && !spec.InsertoEmpurrao; } }
    private int cameraHandle, phase, phaseStart, lastElapsed, leftPlaneAt = -1, releaseAt = -1, impactAt = -1;
    private int sampleIndex, surfaceSince = -1, observedFall = -1, observedDuration = -1;
    private int routeSampleIndex, routeRay, routeRayIndex;
    private int airSampleIndex, airSegments, impactPart, impactParts, releaseSections, impactSampleCount;
    private bool volumeBoundsReady, corridorReady, impactWaterKnown, flightVolumeClear;
    private float airRadius, runtimeAirRadius, impactRadius, airFrom, airTo, impactZoneStart, impactProbeHeight;
    private const float FlightPadding = 0.5f, FlightSpan = 40f;
    private const int AirColumns = 7, AirRows = 3, AirTiles = AirColumns * AirRows;
    private const int RuntimeAirProbes = 1;
    private float tileHalfX, tileHalfZ;
    private GTA.Math.Vector3 planeMin, planeMax, planeCenter, impactProbePoint;
    private FlightVolume flightVolume, lastClearFlightVolume;
    private int flightMonitorTick = -1;
    private string flightCoverageFailure = "not_queried";
    private readonly System.Collections.Generic.List<VolumeProbe> volumeProbes = new System.Collections.Generic.List<VolumeProbe>();
    private float previousRouteGround;
    private float waterHeight, heading;
    private float planeStartAlong, releaseZoneStart, releaseZoneEnd;
    private bool cabinHeadCaptured;
    private GTA.Math.Vector3 cabinHeadLocal;
    private GTA.Math.Vector3 forward, right, startPosition;
    private System.Diagnostics.Stopwatch loading;
    private string status = "não preparado";

    public CenaQuedaDiretor(System.Action<string, string> log) { this.log = log; }
    public bool IsReady { get { return prepared && ready && !failed && !disposed; } }
    public bool IsRunning { get { return running; } }
    public bool IsFinished { get { return finished; } }
    public bool IsSuccess { get { return finished && success; } }
    public bool HasFailed { get { return failed; } }
    public string Status { get { return status; } }
    public int ObservedFallMs { get { return observedFall; } }
    public int ObservedDurationMs { get { return observedDuration; } }
    public int RequestedDurationMs { get { return spec == null ? 0 : spec.DuracaoSolicitadaMs; } }
    public GTA.Vehicle OwnPlane { get { RequireReady(); if (!HasPlane()) throw Error("o avião próprio perdeu identidade"); return plane; } }
    public void SetCabinFocus(GTA.Ped focus)
    {
        RequireReady();
        if (!spec.SomenteVoo || (!IsActor(focus, actor) && !IsActor(focus, pilot) && !IsActor(focus, rearPassenger)))
            throw Error("a câmera da cabine só aceita os três atores próprios sentados");
        cabinFocus = focus;
        if (spec.Cena == "EP07_C04" && spec.SomenteVoo && !spec.InsertoEmpurrao)
        {
            cabinCameraDiagnosticUntil = GTA.Game.GameTime + 300;
            nextCabinCameraDiagnostic = 0;
            Log("EP07_CAMERA_FOCO_ATRIBUIDO", "FocusHandle=" + focus.Handle + "; FocusRole=" + CabinFocusRole() +
                "; FocusModel=" + focus.Model.Hash + "; OwnCamera=" + camera.Handle + "; GettersOnly=true");
        }
        UpdateCamera();
    }
    public int ExtraDurationMs { get { return observedDuration < 0 ? 0 : System.Math.Max(0, observedDuration - RequestedDurationMs); } }
    public GTA.Entity[] OwnedEntities
    {
        get
        {
            var result = new System.Collections.Generic.List<GTA.Entity>();
            if (!borrowedPilot && StillOwn(pilot)) result.Add(pilot.Ped);
            if (HasPlane()) result.Add(plane);
            return result.ToArray();
        }
    }
    public int[] OwnedHandles
    {
        get { var result = new System.Collections.Generic.List<int>(); foreach (GTA.Entity entity in OwnedEntities) result.Add(entity.Handle); return result.ToArray(); }
    }

    // O chamador deve fornecer somente ator criado por ele e a marca capturada, nunca Franklin.
    // Nenhuma posição global pode ser inferida pelo nome da locação.
    public void Prepare(GTA.Ped djaniraPropria, QuedaSpec value)
    {
        Active();
        if (prepared || resources != null || StillOwn(pilot)) throw Error("limpe a preparação anterior antes de criar outra");
        if (value == null || value.Local != "CANAL_LS" ||
            (value.Cena != "EP01_C01" && value.Cena != "EP07_C05" && value.Cena != "EP07_C04"))
            throw Error("a tomada precisa identificar CANAL_LS real e EP01_C01/EP07_C04/EP07_C05");
        bool pushInsert = value.InsertoEmpurrao && value.Cena == "EP07_C05" && value.DuracaoSolicitadaMs == 5000;
        if (value.InsertoEmpurrao && !pushInsert || value.SomenteVoo != (value.Cena == "EP07_C04" || pushInsert) ||
            (value.SomenteVoo && (value.CompletarFuga || value.EnsaioPassivo || value.DuracaoSolicitadaMs != (pushInsert ? 5000 : 34000) ||
                System.Object.ReferenceEquals(value.PilotoDoElenco, null) || System.Object.ReferenceEquals(value.PassageiroTraseiro, null))) ||
            (!value.SomenteVoo && (!System.Object.ReferenceEquals(value.PilotoDoElenco, null) || !System.Object.ReferenceEquals(value.PassageiroTraseiro, null))))
            throw Error("somenteVoo exige jantar34s ou inserto de contatoEP07_C05 de5s e três atores próprios");
        Vector(value.Agua); Vector(value.Direcao);
        if (Horizontal(value.Direcao) < 0.01f || !Finite(value.AlturaRelativa) || !Finite(value.VelocidadeAviao) ||
            (!value.EnsaioPassivo && (value.AlturaRelativa < 20f || value.AlturaRelativa > 45f || value.VelocidadeAviao < 25f || value.VelocidadeAviao > 40f)) ||
            (value.EnsaioPassivo && (value.AlturaRelativa != 30f || value.VelocidadeAviao != 6f)) ||
            value.DuracaoSolicitadaMs < 1000 || value.DuracaoSolicitadaMs > 60000 || value.QuedaAlvoMs < 1000 || value.QuedaAlvoMs > 10000 ||
            value.TimeoutMs < 15000 || value.TimeoutMs > 120000)
            throw Error("direção, velocidade, altura ou tempos fora dos limites técnicos");
        if (value.CompletarFuga && (!value.EntradaRampa.HasValue || !value.TopoRampa.HasValue || !value.Fuga.HasValue))
            throw Error("a saída completa exige marcas reais da entrada, topo da rampa e fim da fuga");
        actor = Capture(djaniraPropria);
        borrowedPilot = value.SomenteVoo;
        if (borrowedPilot)
        {
            pilot = Capture(value.PilotoDoElenco); rearPassenger = Capture(value.PassageiroTraseiro);
            if (pilot.Handle == actor.Handle || rearPassenger.Handle == actor.Handle || rearPassenger.Handle == pilot.Handle)
                throw Error("o voo da cabine exige três atores distintos");
        }
        cabinFocus = djaniraPropria;
        // Copiar impede alterações do objeto de configuração no meio da tomada.
        spec = new QuedaSpec { Cena = value.Cena, Local = value.Local, Agua = value.Agua, Direcao = value.Direcao,
            EntradaRampa = value.EntradaRampa, TopoRampa = value.TopoRampa, Fuga = value.Fuga,
            CompletarFuga = value.CompletarFuga, OlhosDesdeInicio = value.OlhosDesdeInicio, InverterEixo = value.InverterEixo, EnsaioPassivo = value.EnsaioPassivo, FilmarQuedaExterna = value.FilmarQuedaExterna,
            SomenteVoo = value.SomenteVoo, InsertoEmpurrao = value.InsertoEmpurrao, PilotoDoElenco = value.PilotoDoElenco, PassageiroTraseiro = value.PassageiroTraseiro,
            AlturaRelativa = value.AlturaRelativa, VelocidadeAviao = value.VelocidadeAviao,
            DuracaoSolicitadaMs = value.DuracaoSolicitadaMs, QuedaAlvoMs = value.QuedaAlvoMs, TimeoutMs = value.TimeoutMs };
        var diagnostic = new WaterQueryDiagnostic();
        if (!TryWater(spec.Agua, out waterHeight, diagnostic) || System.Math.Abs(spec.Agua.Z - waterHeight) > 3f)
        {
            Log("QUEDA_AGUA_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Amostra=-1; Secao=marca; Lado=0; Ponto=({0:R},{1:R},{2:R}); Height={3:R}; MarkZ={4:R}; Diff={5:R}; {6}",
                spec.Agua.X, spec.Agua.Y, spec.Agua.Z, waterHeight, spec.Agua.Z, System.Math.Abs(spec.Agua.Z - waterHeight), diagnostic.ToLog()));
            throw Error("a marca CANAL_LS precisa estar na água real; uma marca terrestre não serve de ponto de impacto");
        }
        forward = new GTA.Math.Vector3(spec.Direcao.X, spec.Direcao.Y, 0f) / Horizontal(spec.Direcao);
        right = new GTA.Math.Vector3(forward.Y, -forward.X, 0f);
        if (spec.InverterEixo) right = -right;
        heading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, forward.X, forward.Y);
        planeStartAlong = spec.EnsaioPassivo ? 0f : -70f;
        releaseZoneStart = -8f; releaseZoneEnd = 25f;
        if (spec.FilmarQuedaExterna && !spec.EnsaioPassivo)
        {
            // Saída física observada em57:1754ms. A projeção é planejamento, nunca prova de impacto.
            // Incluir os10m de desvio vertical já permitidos evita usar só a altitude nominal30m.
            float exitLeadSeconds = 1.75f;
            float fallLeadSeconds = (float)System.Math.Sqrt(2f * (spec.AlturaRelativa + 10f) / 9.81f);
            releaseZoneStart = -spec.VelocidadeAviao * (exitLeadSeconds + fallLeadSeconds);
            releaseZoneEnd = releaseZoneStart + 25f;
            planeStartAlong = releaseZoneStart - spec.VelocidadeAviao * 1.5f;
            Log("QUEDA_SOLTURA_PLANEJADA", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "InicioAviao={0:R}; ZoneStart={1:R}; ZoneEnd={2:R}; ExitLeadSeconds={3:R}; FallLeadSeconds={4:R}; AlturaProjetada={5:R}; Velocidade={6:R}; valores relativos à marca real; saída/queda/impacto ainda precisam dos getters",
                planeStartAlong, releaseZoneStart, releaseZoneEnd, exitLeadSeconds, fallLeadSeconds, spec.AlturaRelativa + 10f, spec.VelocidadeAviao));
        }
        startPosition = new GTA.Math.Vector3(spec.Agua.X, spec.Agua.Y, waterHeight + spec.AlturaRelativa) + forward * planeStartAlong;
        if (spec.CompletarFuga) ValidateRouteDefinition();
        ProtectActor(); // Proteger também durante o streaming da montagem na água.
        if (borrowedPilot) { Protect(pilot); Protect(rearPassenger); }
        GTA.Model planeModel = new GTA.Model("velum");
        if (!planeModel.IsInCdImage || !planeModel.IsVehicle || !planeModel.IsPlane)
            throw Error("o modelo velum conferido na API não está disponível como avião nesta instalação");
        pilotModel = new GTA.Model("s_m_m_pilot_01"); // PedList.xml instalado, linha949.
        if (!pilotModel.IsInCdImage || !pilotModel.IsPed) throw Error("o modelo do piloto não está disponível");
        resources = new ObjetosDiretor(log);
        prepared = true; ready = false; failed = false; finished = false; success = false;
        sampleIndex = 0; routeSampleIndex = 0; routeRay = 0; routeRayIndex = 0; routeReady = false;
        volumeBoundsReady = false; corridorReady = false; impactWaterKnown = false;
        airSampleIndex = 0; volumeProbes.Clear(); flightVolume = null; lastClearFlightVolume = null; flightMonitorTick = -1; flightVolumeClear = false;
        if (!borrowedPilot) { modelWasLoaded = pilotModel.IsLoaded; pilotModel.Request(); modelRequested = true; }
        resources.RequestVehicle("AVIAO_QUEDA", "velum", startPosition, heading, true, false);
        loading = System.Diagnostics.Stopwatch.StartNew();
        status = "carregando avião, piloto e corredor de água";
        Log("QUEDA_PREPARANDO", "Cena=" + spec.Cena + "; Local=CANAL_LS; Física real; Estado=não testado visualmente");
    }

    // Até seis consultas espaciais por Tick; handles assíncronos só são lidos em chamadas seguintes.
    public void PollLoading()
    {
        Active();
        if (!prepared || failed) throw Error("não existe preparação válida");
        if (ready) return;
        try
        {
            RequireActor();
            if (borrowedPilot) { RequirePilot(); RequireRearPassenger(); }
            if (loading.ElapsedMilliseconds >= 10000) throw Error("recursos, colisão ou corredor não ficaram disponíveis em dez segundos");
            resources.Update();
            if (!corridorReady) { ProbeCorridor(); return; } // A rota usa seu orçamento em outro Tick.
            if (!resources.IsReady || (!borrowedPilot && !pilotModel.IsLoaded)) return;
            plane = resources.GetVehicle("AVIAO_QUEDA");
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, startPosition.X, startPosition.Y, startPosition.Z);
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, plane.Handle)) return;
            if (spec.CompletarFuga && !RouteGroundAvailable()) return;
            if (pilot != null && pilot.Handle != 0 && !StillOwn(pilot))
                throw Error("o piloto registrado perdeu identidade; preservar referências e não recriar");
            if (!borrowedPilot && (pilot == null || pilot.Handle == 0))
            {
                GTA.Ped created = GTA.Ped.Create(pilotModel, startPosition + right * 10f, heading);
                if (System.Object.ReferenceEquals(created, null) || !created.Exists()) throw Error("não foi possível criar o piloto próprio");
                // Registrar antes de configurar, para Cleanup poder apagar caso a configuração falhe.
                pilot = new ActorState { Ped = created, Handle = created.Handle, Model = created.Model.Hash, Address = created.MemoryAddress };
                created.IsPersistent = true; created.IsInvincible = true; created.IsPositionFrozen = true;
                created.BlockPermanentEvents = true; created.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, false);
                created.SetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming, false);
                return;
            }
            ready = true; status = "recursos carregados; ensaio visual pendente";
            Log("QUEDA_RECURSOS_PRONTOS", "Avião/piloto próprios; volume aéreo e água/queda na zona de soltura conferidos; nenhum voo executado ainda");
        }
        catch { failed = true; status = "preparação falhou"; throw; }
    }

    // Apenas antes de filmar: retirar ocupantes pode precisar do próximo Tick; false não é sucesso.
    public bool Reset()
    {
        RequireReady();
        if (resetWaitingSeats) return CompleteResetSeats();
        running = false; resetDone = false;
        if (!ReleaseActorsForReset()) return false;
        RequireActor();
        RequirePilot();
        if (actor.Ped.IsInVehicle() || pilot.Ped.IsInVehicle()) throw Error("um ator está em veículo alheio; não mover nem trocar seu assento");
        if (borrowedPilot && rearPassenger.Ped.IsInVehicle()) throw Error("a passageira traseira está em veículo alheio");
        if (!actorMarkCaptured)
        {
            // A primeira marca é obtida depois do assentamento dos atores pelo chamador.
            actor.Position = actor.Ped.Position; actor.Heading = actor.Ped.Heading; actor.Health = actor.Ped.Health;
            actorMarkCaptured = true;
        }
        resources.Reset(); plane = resources.GetVehicle("AVIAO_QUEDA");
        actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
        actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading;
        ProtectActor();
        if (borrowedPilot)
        {
            RestoreBorrowedMark(pilot); RestoreBorrowedMark(rearPassenger);
            Protect(pilot); Protect(rearPassenger);
        }
        plane.IsEngineRunning = true; plane.IsInvincible = true; plane.IsPositionFrozen = true;
        if (!plane.IsSeatFree(GTA.VehicleSeat.Driver) || !plane.IsSeatFree(GTA.VehicleSeat.Passenger))
            throw Error("assentos iniciais ocupados; não remover nem substituir ocupantes");
        if (borrowedPilot && (plane.PassengerCapacity < 2 || !plane.IsSeatFree((GTA.VehicleSeat)1)))
            throw Error("o velum real não possui assento traseiro1 livre; não criar cabine fictícia");
        resetWaitingSeats = true;
        pilot.Ped.IsPositionFrozen = false; pilot.Ped.SetIntoVehicle(plane, GTA.VehicleSeat.Driver);
        actor.Ped.IsPositionFrozen = false; actor.Ped.SetIntoVehicle(plane, GTA.VehicleSeat.Passenger);
        if (borrowedPilot) { rearPassenger.Ped.IsPositionFrozen = false; rearPassenger.Ped.SetIntoVehicle(plane, (GTA.VehicleSeat)1); }
        return CompleteResetSeats();
    }

    private bool CompleteResetSeats()
    {
        RequireActor(); RequirePilot(); plane = resources.GetVehicle("AVIAO_QUEDA");
        GTA.Ped driver = plane.GetPedOnSeat(GTA.VehicleSeat.Driver);
        GTA.Ped passenger = plane.GetPedOnSeat(GTA.VehicleSeat.Passenger);
        if ((!System.Object.ReferenceEquals(driver, null) && driver.Exists() && !IsActor(driver, pilot)) ||
            (!System.Object.ReferenceEquals(passenger, null) && passenger.Exists() && !IsActor(passenger, actor)))
            throw Error("outro ocupante tomou o assento inicial; preservar avião/atores");
        if (!IsActor(driver, pilot) || !IsActor(passenger, actor) || !InPlane(actor.Ped) || !InPlane(pilot.Ped)) return false;
        if (borrowedPilot)
        {
            RequireRearPassenger(); GTA.Ped rear = plane.GetPedOnSeat((GTA.VehicleSeat)1);
            if (!System.Object.ReferenceEquals(rear, null) && rear.Exists() && !IsActor(rear, rearPassenger))
                throw Error("ocupante alheio tomou o assento traseiro; não substituir");
            if (!IsActor(rear, rearPassenger) || !InPlane(rearPassenger.Ped)) return false;
        }
        resetWaitingSeats = false;
        cabinHeadCaptured = false;
        flightVolume = null; lastClearFlightVolume = null; flightMonitorTick = -1; flightVolumeClear = false; flightCoverageFailure = "not_queried";
        CaptureCamera();
        if (IsDialogueCabinCamera)
        {
            camera.Detach(); camera.StopPointing();
            cabinCameraHeadLocalByActor.Clear(); cabinCameraAttachedFocus = 0;
        }
        phase = 0; lastElapsed = -1; phaseStart = 0; releaseAt = -1; leftPlaneAt = -1; impactAt = -1; surfaceSince = -1;
        observedFall = -1; observedDuration = -1; finished = false; success = false; failed = false;
        resetDone = true; status = "assentos/câmera prontos para contagem";
        LogPlaneInitialState("ResetReady", MissionFlightHeight());
        UpdateCamera();
        return true;
    }

    // Chamar depois da contagem, com câmera principal suspensa. Velocidade inicial é física, não deslocamento de posição.
    public void Start()
    {
        RequireReady();
        if (!resetDone || running) throw Error("prepare assentos/câmera com Reset antes de iniciar a tomada");
        RequireActor(); RequirePilot(); plane = resources.GetVehicle("AVIAO_QUEDA");
        if (!InPlane(actor.Ped) || !InPlane(pilot.Ped)) throw Error("os ocupantes próprios saíram dos assentos antes da tomada");
        int missionHeight = MissionFlightHeight();
        LogPlaneInitialState("StartBeforeFlight", missionHeight);
        if (spec.FilmarQuedaExterna && !spec.SomenteVoo) CaptureCabinHeadOffset();
        if (IsDialogueCabinCamera) { CaptureDialogueCabinHeads(); UpdateCamera(); }
        plane.IsPositionFrozen = false;
        plane.Velocity = forward * spec.VelocidadeAviao;
        if (!spec.EnsaioPassivo)
            pilot.Ped.Task.StartPlaneMission(plane, startPosition + forward * 900f, GTA.VehicleMissionType.GoTo,
                spec.VelocidadeAviao, 10f, missionHeight, 10, -1f, false);
        Log("QUEDA_PERFIL_FISICO", "EnsaioPassivo=" + spec.EnsaioPassivo + "; VelocidadeInicial=" + spec.VelocidadeAviao + "; AlturaRelativa=" + spec.AlturaRelativa + "; PlaneMission=" + !spec.EnsaioPassivo + "; MissionFlightHeightASL=" + missionHeight + "; MinHeightAboveTerrain=10; ProductionHeightTune=" + (spec.FilmarQuedaExterna && !spec.EnsaioPassivo ? -5 : 0));
        running = true; status = "travessia aérea";
        Log("QUEDA_TOMADA_INICIADA", "MetaQuedaMs=" + spec.QuedaAlvoMs + "; NarracaoMs=" + RequestedDurationMs + "; Tempo físico não comprimido");
    }

    private int MissionFlightHeight()
    {
        // API191: flightHeight éZ absoluto. Ajuste de pilotagem da produção; limites físicos não mudam.
        return (int)System.Math.Round(waterHeight + spec.AlturaRelativa -
            (spec.FilmarQuedaExterna && !spec.EnsaioPassivo ? 5f : 0f));
    }

    private void LogPlaneInitialState(string stage, int missionHeight)
    {
        GTA.Math.Vector3 position = plane.Position;
        Log("QUEDA_AVIAO_ESTADO_INICIAL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Etapa={0}; Plane={1}; InicioPlanejado={2}; DeltaInicioZ={3:R}; HeightAboveGround={4:R}; Velocity={5}; Rotation={6}; Frozen={7}; TargetZ={8:R}; MissionFlightHeightASL={9}; MinHeightAboveTerrain=10; Actor={10}; PilotInPlane={11}",
            stage, FlightVector(position), FlightVector(startPosition), position.Z - startPosition.Z, plane.HeightAboveGround,
            FlightVector(plane.Velocity), FlightVector(plane.Rotation), plane.IsPositionFrozen, waterHeight + spec.AlturaRelativa,
            missionHeight, FlightVector(actor.Ped.Position), InPlane(pilot.Ped)));
    }

    public void Update(int elapsedMs)
    {
        RequireReady();
        if (finished) return;
        if (!running || elapsedMs < 0 || elapsedMs < lastElapsed) throw Error("a tomada exige tempo crescente desde Start");
        try
        {
            RequireActor(); RequirePilot(); plane = resources.GetVehicle("AVIAO_QUEDA");
            if (elapsedMs > spec.TimeoutMs) throw Error("a sequência física excedeu o limite; não teleportar para concluir");
            lastElapsed = elapsedMs;
            switch (phase)
            {
                case 0: if (spec.SomenteVoo) UpdateCabinFlight(elapsedMs); else UpdateFlight(elapsedMs); break;
                case 1: UpdateDeparture(elapsedMs); break;
                case 2: UpdateFall(elapsedMs); break;
                case 3: UpdateSurface(elapsedMs); break;
                case 4: UpdateSwim(elapsedMs); break;
                case 5: UpdateRamp(elapsedMs); break;
                case 6: UpdateEscape(elapsedMs); break;
            }
            if (spec.FilmarQuedaExterna && phase >= 2) VerifySustainedFlight(elapsedMs);
            UpdateCamera();
        }
        catch { running = false; failed = true; status = "tomada falhou; preservar recursos para limpeza"; throw; }
    }

    private void UpdateCabinFlight(int elapsed)
    {
        RequireRearPassenger();
        if (!InPlane(actor.Ped) || !InPlane(pilot.Ped) || !InPlane(rearPassenger.Ped) ||
            !IsActor(plane.GetPedOnSeat(GTA.VehicleSeat.Driver), pilot) ||
            !IsActor(plane.GetPedOnSeat(GTA.VehicleSeat.Passenger), actor) ||
            !IsActor(plane.GetPedOnSeat((GTA.VehicleSeat)1), rearPassenger))
            throw Error("um ator saiu de seu assento real durante o jantar");
        VerifySustainedFlight(elapsed);
        bool clear = MonitorFlightVolume();
        if (!clear && !FlightBoxCovered(lastClearFlightVolume) && elapsed > 1000)
            throw Error("o volume aéreo atual do jantar não possui prova de colisão concluída");
        if (elapsed < spec.DuracaoSolicitadaMs) return;
        observedDuration = elapsed; finished = true; success = true; running = false;
        status = spec.InsertoEmpurrao ? "inserto de contato5s em voo real; atores e avião preservados" : "jantar sentado34s em voo real; atores e avião preservados";
        Log("VOO_CABINE_FINALIZADO", "TempoMs=" + elapsed + "; Modelo=velum; ChefePiloto=True; DinjerAssentoTraseiro=1; sem teleporte durante ação");
    }

    private void VerifySustainedFlight(int elapsed)
    {
        RequirePilot();
        if (!InPlane(pilot.Ped) || plane.IsDead ||
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, plane.Handle) ||
            plane.Position.Z < waterHeight + 15f)
            throw Error("o avi?o n?o permaneceu voando durante a queda; esta tomada n?o serve ao pedido");
        if (elapsed == leftPlaneAt || elapsed == impactAt)
            Log("QUEDA_AVIAO_CONTINUA_VOANDO", "TempoMs=" + elapsed + "; Plane=" + plane.Position + "; Velocity=" + plane.Velocity +
                "; HeightAboveWater=" + (plane.Position.Z - waterHeight).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; PilotInPlane=" + InPlane(pilot.Ped) + "; PlaneMission=True; sem colis?o/queda do avi?o");
    }

    private void UpdateFlight(int elapsed)
    {
        if (!InPlane(actor.Ped) || !InPlane(pilot.Ped)) throw Error("um ocupante saiu do avião antes da zona de queda");
        GTA.Math.Vector3 difference = plane.Position - spec.Agua;
        float distance = Dot(difference, forward), lateral = System.Math.Abs(Dot(difference, right));
        if (elapsed > 7000 || distance > releaseZoneEnd) throw Error("o avião não atravessou a zona de soltura real como planejado");
        bool eligible = elapsed >= 1000 && distance >= releaseZoneStart;
        // Reservar no máximo3 queries de água somente quando já há volume aéreo atual concluído.
        bool queryRelease = eligible && FlightBoxCovered(lastClearFlightVolume);
        bool volumeClear = MonitorFlightVolume(queryRelease ? 3 : 6);
        if (!eligible) return;
        if (lateral > 6f || System.Math.Abs(plane.Position.Z - (waterHeight + spec.AlturaRelativa)) > 10f)
        {
            Log("QUEDA_VOO_CORREDOR_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "TempoMs={0}; Actor={1}; Plane={2}; DistanciaLongitudinal={3:R}; Lateral={4:R}; LateralLimit=6; TargetZ={5:R}; DeltaZ={6:R}; DeltaZLimit=10; Velocity={7}; ZoneStart={8:R}; ZoneEnd={9:R}",
                elapsed, FlightVector(actor.Ped.Position), FlightVector(plane.Position), distance, lateral,
                waterHeight + spec.AlturaRelativa, System.Math.Abs(plane.Position.Z - (waterHeight + spec.AlturaRelativa)),
                FlightVector(plane.Velocity), releaseZoneStart, releaseZoneEnd));
            throw Error("o avião desviou do corredor; não iniciar queda fora da água validada");
        }
        if (!volumeClear || !queryRelease) return; // Uma prova concluída neste frame reserva água no próximo.
        float height;
        GTA.Math.Vector3 releasePoint = actor.Ped.Position;
        var diagnostic = new WaterQueryDiagnostic();
        if (!TryWater(releasePoint, out height, diagnostic) || System.Math.Abs(height - waterHeight) > 1.5f)
        {
            Log("QUEDA_LANCAMENTO_AGUA_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "TempoMs={0}; Actor=({1:R},{2:R},{3:R}); Plane={4}; Height={5:R}; WaterHeight={6:R}; {7}",
                elapsed, releasePoint.X, releasePoint.Y, releasePoint.Z, plane.Position, height, waterHeight, diagnostic.ToLog()));
            throw Error("água real sob Djanira não confirmada imediatamente antes da saída; não lançar sobre terreno seco");
        }
        Log("QUEDA_LANCAMENTO_AGUA_CONFERIDA", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
            "TempoMs={0}; Actor=({1:R},{2:R},{3:R}); Height={4:R}; RaioCorporal={5:R}; {6}",
            elapsed, releasePoint.X, releasePoint.Y, releasePoint.Z, height, impactRadius, diagnostic.ToLog()));
        actor.Ped.Task.LeaveVehicle(plane, GTA.LeaveVehicleFlags.BailOut | GTA.LeaveVehicleFlags.DontWaitForVehicleToStop |
            GTA.LeaveVehicleFlags.DontDefaultWarpIfDoorBlocked);
        releaseAt = elapsed; phaseStart = elapsed; phase = 1; status = "saída física do avião";
        Log("QUEDA_SAIDA_PEDIDA", "TempoMs=" + elapsed + "; Actor=" + actor.Ped.Position + "; Plane=" + plane.Position + "; Sem Warp/SetPosition na ação");
    }

    private void UpdateDeparture(int elapsed)
    {
        if (!InPlane(actor.Ped))
        {
            leftPlaneAt = elapsed; actor.Ped.Ragdoll(4500, GTA.RagdollType.Relax);
            phase = 2; phaseStart = elapsed; status = "queda física";
            Log("QUEDA_FORA_AVIAO", "TempoMs=" + elapsed + "; SaidaMs=" + (elapsed - releaseAt) + "; Actor=" + actor.Ped.Position + "; Plane=" + plane.Position);
            return;
        }
        if (!MonitorFlightVolume())
        {
            Log("QUEDA_COBERTURA_ATUAL_FAIL", "TempoMs=" + elapsed + "; SaidaPedidaMs=" + releaseAt +
                "; FlightTick=" + flightMonitorTick + "; " + flightCoverageFailure +
                "; Atual=" + FlightCertificateDescription(flightVolume, flightMonitorTick));
            throw Error("o volume aéreo atual da saída não está confirmado por colisão real");
        }
        if (elapsed - phaseStart > 3500) throw Error("a animação de saída não liberou o assento em3,5s; nenhum Warp permitido na tomada");
    }

    private void UpdateFall(int elapsed)
    {
        float height;
        GTA.Math.Vector3 point = actor.Ped.Position;
        var diagnostic = new WaterQueryDiagnostic();
        if (!TryWater(point, out height, diagnostic) || System.Math.Abs(height - waterHeight) > 2f || System.Math.Abs(Dot(point - spec.Agua, right)) > 10f)
        {
            Log("QUEDA_AGUA_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Secao=queda; TempoMs={0}; Ponto=({1:R},{2:R},{3:R}); Height={4:R}; WaterHeight={5:R}; Diff={6:R}; Lateral={7:R}; {8}",
                elapsed, point.X, point.Y, point.Z, height, waterHeight, System.Math.Abs(height - waterHeight),
                System.Math.Abs(Dot(point - spec.Agua, right)), diagnostic.ToLog()));
            throw Error("a trajetória deixou a faixa de água real; ensaio recusado");
        }
        if (actor.Ped.IsInWater)
        {
            impactAt = elapsed; observedFall = elapsed - leftPlaneAt; phase = 3; phaseStart = elapsed; status = "impacto na água; emergindo";
            Log("QUEDA_IMPACTO_REAL", "TempoMs=" + elapsed + "; QuedaFisicaMs=" + observedFall + "; MetaMs=" + spec.QuedaAlvoMs +
                "; WaterHeight=" + height.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; " + PhysicalLocation(point, spec.EntradaRampa));
            return;
        }
        if (elapsed - leftPlaneAt > 10000 || point.Z < height - 4f) throw Error("o impacto real na água não foi detectado no limite");
    }

    private void UpdateSurface(int elapsed)
    {
        if (elapsed - impactAt > 10000) throw Error("Djanira não emergiu fisicamente em dez segundos");
        if (actor.Ped.IsSwimmingUnderWater || actor.Ped.IsRagdoll || !actor.Ped.IsInWater)
        { surfaceSince = -1; return; }
        if (surfaceSince < 0) surfaceSince = elapsed;
        if (elapsed - surfaceSince < 500) return;
        Log("QUEDA_EMERSAO_REAL", "TempoMs=" + elapsed + "; ImpactoMs=" + impactAt + "; " + PhysicalLocation(null, spec.EntradaRampa) + "; Não confirma roupa molhada visualmente");
        if (!spec.CompletarFuga) { Complete(elapsed, "voo, queda e emersão; rampa/fuga não solicitadas"); return; }
        MoveTo(spec.EntradaRampa.Value, 1f); phase = 4; phaseStart = elapsed; status = "nadando até entrada real da rampa";
    }

    private void UpdateSwim(int elapsed)
    {
        if (Horizontal(actor.Ped.Position - spec.EntradaRampa.Value) <= 1.5f)
        {
            MoveTo(spec.TopoRampa.Value, 1f); phase = 5; phaseStart = elapsed; status = "subindo rampa real"; return;
        }
        if (elapsed - phaseStart > 15000)
        {
            Log("QUEDA_NADO_FAIL", "TempoMs=" + elapsed + "; FaseMs=" + (elapsed - phaseStart) +
                "; LimiteMs=15000; " + PhysicalLocation(null, spec.EntradaRampa));
            throw Error("não alcançou a entrada da rampa; direção/rota de nado exigem ensaio real");
        }
    }

    private void UpdateRamp(int elapsed)
    {
        if (!actor.Ped.IsInWater && Horizontal(actor.Ped.Position - spec.TopoRampa.Value) <= 1f &&
            System.Math.Abs(actor.Ped.Position.Z - spec.TopoRampa.Value.Z) <= 1.5f)
        {
            MoveTo(spec.Fuga.Value, 2f); phase = 6; phaseStart = elapsed; status = "correndo no trajeto real"; return;
        }
        if (elapsed - phaseStart > 15000)
        {
            Log("QUEDA_RAMPA_FAIL", "TempoMs=" + elapsed + "; FaseMs=" + (elapsed - phaseStart) +
                "; LimiteMs=15000; " + PhysicalLocation(null, spec.TopoRampa));
            throw Error("a rampa não foi vencida fisicamente; não substituir por clipe de escalada ou teleporte");
        }
    }

    private void UpdateEscape(int elapsed)
    {
        if (!actor.Ped.IsInWater && Horizontal(actor.Ped.Position - spec.Fuga.Value) <= 1f &&
            System.Math.Abs(actor.Ped.Position.Z - spec.Fuga.Value.Z) <= 1.5f)
        { actor.Ped.Task.StandStill(-1); Complete(elapsed, "voo, queda, emersão, rampa e corrida físicas"); return; }
        if (elapsed - phaseStart > 15000)
        {
            Log("QUEDA_FUGA_FAIL", "TempoMs=" + elapsed + "; FaseMs=" + (elapsed - phaseStart) +
                "; LimiteMs=15000; " + PhysicalLocation(null, spec.Fuga));
            throw Error("a corrida não alcançou sua marca no limite; não teleportar");
        }
    }

    // Somente leituras para preservar coordenadas físicas antes de eventual Cleanup.
    private string PhysicalLocation(GTA.Math.Vector3? observed, GTA.Math.Vector3? destination)
    {
        try
        {
            GTA.Math.Vector3 point = observed.HasValue ? observed.Value : actor.Ped.Position;
            string result = "Actor=" + FlightVector(point) + "; Plane=" + FlightVector(plane.Position);
            if (spec.EntradaRampa.HasValue)
                result += "; EntradaRampa=" + FlightVector(spec.EntradaRampa.Value) + "; DistanciaEntradaXY=" +
                    Horizontal(point - spec.EntradaRampa.Value).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (destination.HasValue)
                result += "; Destino=" + FlightVector(destination.Value) + "; DistanciaDestinoXY=" +
                    Horizontal(point - destination.Value).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; DiferencaDestinoZ=" + System.Math.Abs(point.Z - destination.Value.Z).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return result;
        }
        catch (System.Exception exception) { return "TelemetryReadFailed=" + exception.GetType().Name; }
    }

    private void Complete(int elapsed, string result)
    {
        actor.Ped.Task.ClearAllImmediately(); actor.Ped.Task.StandStill(-1);
        actor.Ped.Velocity = GTA.Math.Vector3.Zero; actor.Ped.IsPositionFrozen = true;
        observedDuration = elapsed; running = false; finished = true; success = true; status = result;
        Log("QUEDA_SEQUENCIA_FINALIZADA", "ObservadoMs=" + elapsed + "; SolicitadoMs=" + RequestedDurationMs + "; ExtraMs=" + ExtraDurationMs + "; " + result + "; Teste visual ainda necessário");
    }

    private void MoveTo(GTA.Math.Vector3 destination, float speed)
    {
        // -1 desliga o teleporte de timeout do contrato TaskInvoker. Travamento é erro, não sucesso.
        actor.Ped.Task.FollowNavMeshTo(destination, new GTA.PedMoveBlendRatio(speed), -1, 0.5f,
            GTA.FollowNavMeshFlags.Default);
    }

    private void CaptureCamera()
    {
        if (!System.Object.ReferenceEquals(camera, null) && camera.Exists()) return;
        previousCamera = GTA.ScriptCameraDirector.RenderingCam;
        camera = GTA.Camera.Create(GTA.ScriptedCameraNameHash.DefaultScriptedCamera,
            startPosition + right * 12f + new GTA.Math.Vector3(0f, 0f, 3f), GTA.Math.Vector3.Zero, 50f, false, GTA.EulerRotationOrder.YXZ);
        if (System.Object.ReferenceEquals(camera, null) || !camera.Exists()) throw Error("não foi possível criar a câmera própria");
        cameraHandle = camera.Handle; camera.NearClip = 0.04f; camera.IsActive = true;
        GTA.ScriptCameraDirector.StartRendering();
    }

    private string CabinFocusRole()
    {
        if (IsActor(cabinFocus, actor)) return "DJANIRA_POBRE";
        if (IsActor(cabinFocus, pilot)) return "O_CHEFE";
        if (IsActor(cabinFocus, rearPassenger)) return "DINJER";
        return "INVALID";
    }
    private GTA.Math.Vector3 DialogueCabinCameraOffset(GTA.Ped focus)
    {
        // Somente Dinjer7C4: espelhar a lateral para o centro da cabine; nao mover atores/aviao.
        bool mirrorDinjer = IsDialogueCabinCamera && IsActor(focus, rearPassenger);
        return new GTA.Math.Vector3(mirrorDinjer ? 0.3f : -0.3f, 0.62f, 0.1f);
    }
    private void DiagnoseCabinCamera(GTA.Math.Vector3 head)
    {
        // Nenhum setter: investigar handle/seat/head/offset/render depois da posicao original aplicada acima.
        GTA.Camera rendering = GTA.ScriptCameraDirector.RenderingCam;
        int renderingHandle = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM);
        int directorRendering = System.Object.ReferenceEquals(rendering, null) ? 0 : rendering.Handle;
        GTA.Math.Vector3 actualCamera = camera.Position;
        GTA.Math.Vector3 headLocal = plane.GetPositionOffset(head), cameraLocal = plane.GetPositionOffset(actualCamera);
        Vector(headLocal); Vector(cameraLocal); Vector(actualCamera);
        bool renderChanged = !cabinCameraDiagnosticRenderKnown || lastCabinRenderingHandle != renderingHandle;
        lastCabinRenderingHandle = renderingHandle; cabinCameraDiagnosticRenderKnown = true;
        int now = GTA.Game.GameTime;
        if (!renderChanged && now > cabinCameraDiagnosticUntil && now < nextCabinCameraDiagnostic) return;
        nextCabinCameraDiagnostic = now + 150;
        GTA.Math.Vector3 ownForward = plane.ForwardVector, ownRight = plane.RightVector, ownUp = plane.UpVector;
        GTA.Math.Vector3 target = head - ownUp * 0.08f;
        GTA.Math.Vector3 measuredHeadLocal;
        bool hasMeasuredHead = cabinCameraHeadLocalByActor.TryGetValue(cabinFocus.Handle, out measuredHeadLocal);
        GTA.Math.Vector3 presetOffset = DialogueCabinCameraOffset(cabinFocus);
        GTA.Math.Vector3 expected = hasMeasuredHead ? plane.GetOffsetPosition(measuredHeadLocal + presetOffset) :
            head + ownForward * presetOffset.Y + ownRight * presetOffset.X + ownUp * presetOffset.Z;
        Vector(target); Vector(expected);
        Log("EP07_CAMERA_FOCO_GETTERS", "TempoMs=" + lastElapsed + "; GameTime=" + now +
            "; FocusRole=" + CabinFocusRole() + "; FocusHandle=" + cabinFocus.Handle + "; FocusModel=" + cabinFocus.Model.Hash +
            "; FocusPosition=" + cabinFocus.Position + "; FocusHeadWorld=" + head + "; FocusHeadPlane=" + headLocal +
            "; CameraHandle=" + camera.Handle + "; CameraActive=" + camera.IsActive + "; NativeRenderingCam=" + renderingHandle +
            "; DirectorRenderingCam=" + directorRendering + "; RenderingOwn=" + (renderingHandle == camera.Handle) +
            "; CameraPos=" + actualCamera + "; CameraPlane=" + cameraLocal + "; ExpectedPos=" + expected +
            "; PositionDelta=" + (actualCamera - expected).Length() + "; PointAtRawBoneWorld=" + target + "; CameraRotation=" + camera.Rotation +
            "; MeasuredHeadLocalAvailable=" + hasMeasuredHead + "; MeasuredHeadLocal=" + measuredHeadLocal +
            "; PresetOffset=" + presetOffset + "; DinjerLateralMirrored=" + (IsDialogueCabinCamera && IsActor(cabinFocus, rearPassenger)) +
            "; EngineAttachedFocus=" + cabinCameraAttachedFocus + "; EngineBonePointing=" + (cabinCameraAttachedFocus != 0) +
            "; Plane=" + plane.Handle + "; PlanePos=" + plane.Position + "; PlaneRotation=" + plane.Rotation +
            "; Forward=" + ownForward + "; Right=" + ownRight + "; Up=" + ownUp +
            "; Djanira=" + actor.Ped.Handle + "; DjaniraHeadPlane=" + plane.GetPositionOffset(actor.Ped.Bones[GTA.Bone.SkelHead].Position) +
            "; Chefe=" + pilot.Ped.Handle + "; ChefeHeadPlane=" + plane.GetPositionOffset(pilot.Ped.Bones[GTA.Bone.SkelHead].Position) +
            "; Dinjer=" + rearPassenger.Ped.Handle + "; DinjerHeadPlane=" + plane.GetPositionOffset(rearPassenger.Ped.Bones[GTA.Bone.SkelHead].Position) +
            "; FrameGetters=true; AllFramesFirst300ms=true; RegularCadenceMs=150; GettersOnly=true; VisualPending=true");
    }
    private void CaptureDialogueCabinHeads()
    {
        if (!IsDialogueCabinCamera || !plane.IsPositionFrozen || !borrowedPilot)
            throw Error("capturar camera7C4 exige tres atores proprios assentados e aviao congelado antes de Start");
        cabinCameraHeadLocalByActor.Clear(); cabinCameraAttachedFocus = 0;
        ActorState[] own = { actor, pilot, rearPassenger };
        foreach (ActorState state in own)
        {
            if (!StillOwn(state) || !InPlane(state.Ped)) throw Error("ator proprio perdeu seu assento antes da ancoragem da camera");
            GTA.PedBone bone = state.Ped.Bones[GTA.Bone.SkelHead];
            if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("cabeca real indisponivel antes de iniciar camera7C4");
            GTA.Math.Vector3 world = bone.Position, local = plane.GetPositionOffset(world); Vector(world); Vector(local);
            if (local.Length() > 8f || local.X < planeMin.X - FlightPadding || local.X > planeMax.X + FlightPadding ||
                local.Y < planeMin.Y - FlightPadding || local.Y > planeMax.Y + FlightPadding ||
                local.Z < planeMin.Z - FlightPadding || local.Z > planeMax.Z + FlightPadding)
                throw Error("cabeca medida7C4 esta fora da caixa fisica do velum; nao inventar ancoragem");
            cabinCameraHeadLocalByActor.Add(state.Handle, local);
            Log("EP07_CAMERA_CABECA_LOCAL_CAPTURADA", "Actor=" + state.Handle + "; HeadWorld=" + world +
                "; HeadPlane=" + local + "; Plane=" + plane.Handle + "; Frozen=" + plane.IsPositionFrozen +
                "; BeforeFlight=true; MeasuredOnly=true; PresetOffset=" + DialogueCabinCameraOffset(state.Ped) + "; VisualPending=true");
        }
    }

    private void UpdateAttachedDialogueCabinCamera(GTA.Math.Vector3 rawHead)
    {
        GTA.Math.Vector3 local;
        if (!IsDialogueCabinCamera || !cabinCameraHeadLocalByActor.TryGetValue(cabinFocus.Handle, out local))
            throw Error("o foco7C4 nao possui cabeca local fisica medida antes do voo");
        GTA.PedBone bone = cabinFocus.Bones[GTA.Bone.SkelHead];
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("osso de foco proprio7C4 perdeu validade");
        if (cabinCameraAttachedFocus != cabinFocus.Handle)
        {
            // API191: motor segue a entidade e mira o osso em render; nao usar dois world-getters atrasados a35m/s.
            GTA.Math.Vector3 presetOffset = DialogueCabinCameraOffset(cabinFocus);
            GTA.Math.Vector3 offset = local + presetOffset;
            camera.StopPointing(); camera.Detach(); camera.AttachTo(plane, offset);
            camera.PointAt(bone, new GTA.Math.Vector3(0f, 0f, -0.08f)); camera.FieldOfView = 38f;
            cabinCameraAttachedFocus = cabinFocus.Handle;
            Log("EP07_CAMERA_ANCORAGEM_NATIVA", "FocusRole=" + CabinFocusRole() + "; Focus=" + cabinFocus.Handle +
                "; Plane=" + plane.Handle + "; HeadLocalMeasured=" + local + "; CameraLocalRequested=" + offset +
                "; ExpectedWorld=" + plane.GetOffsetPosition(offset) + "; CameraGetter=" + camera.Position +
                "; PresetOffset=" + presetOffset + "; DinjerLateralMirrored=" + IsActor(cabinFocus, rearPassenger) +
                "; PointAtBone=SkelHead; EngineAttachment=true; FOV=38; BodyServiceUnchanged=true; VisualPending=true");
        }
        DiagnoseCabinCamera(rawHead);
    }

    private void UpdateCamera()
    {
        if (System.Object.ReferenceEquals(camera, null) || !camera.Exists()) throw Error("a câmera própria deixou de existir");
        if (spec.SomenteVoo)
        {
            if (!IsActor(cabinFocus, actor) && !IsActor(cabinFocus, pilot) && !IsActor(cabinFocus, rearPassenger))
                throw Error("o alvo próprio da cabine perdeu identidade");
            if (spec.InsertoEmpurrao)
            {
                GTA.Math.Vector3 hand = pilot.Ped.Bones[GTA.Bone.SkelRightHand].Position;
                GTA.Math.Vector3 shoulder = actor.Ped.Bones[GTA.Bone.SkelLeftClavicle].Position;
                Vector(hand); Vector(shoulder);
                GTA.Math.Vector3 contact = (hand + shoulder) * 0.5f;
                camera.Position = contact + plane.ForwardVector * 0.85f + plane.RightVector * 0.12f + plane.UpVector * 0.16f;
                camera.PointAt(contact); camera.FieldOfView = 42f; return;
            }
            GTA.Math.Vector3 head = cabinFocus.Bones[GTA.Bone.SkelHead].Position;
            Vector(head);
            if (IsDialogueCabinCamera && cabinCameraHeadLocalByActor.Count == 3)
            {
                UpdateAttachedDialogueCabinCamera(head); return;
            }
            GTA.Math.Vector3 presetOffset = DialogueCabinCameraOffset(cabinFocus);
            camera.Position = head + plane.ForwardVector * presetOffset.Y + plane.RightVector * presetOffset.X + plane.UpVector * presetOffset.Z;
            camera.PointAt(head - plane.UpVector * 0.08f); camera.FieldOfView = 38f;
            if (spec.Cena == "EP07_C04" && !spec.InsertoEmpurrao) DiagnoseCabinCamera(head);
            return;
        }
        if (spec.FilmarQuedaExterna && phase == 0 && lastElapsed < 1500)
        {
            // Na contagem conservar o enquadramento anterior; Start mede o osso real antes de soltar o avião.
            if (running && !cabinHeadCaptured) throw Error("a cabeça real na cabine não foi registrada antes do voo");
            GTA.Math.Vector3 head = cabinHeadCaptured ? GTA.Native.Function.Call<GTA.Math.Vector3>(
                GTA.Native.Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, plane.Handle, cabinHeadLocal.X, cabinHeadLocal.Y, cabinHeadLocal.Z) :
                actor.Ped.Bones[GTA.Bone.SkelHead].Position;
            Vector(head);
            camera.Position = head + plane.ForwardVector * 0.62f - plane.RightVector * 0.3f + plane.UpVector * 0.1f;
            camera.PointAt(head - plane.UpVector * 0.08f); camera.FieldOfView = 55f;
        }
        else if (spec.FilmarQuedaExterna && phase >= 2)
        {
            GTA.Math.Vector3 focus = actor.Ped.Position + new GTA.Math.Vector3(0f, 0f, 0.3f);
            GTA.Math.Vector3 position = focus - forward * 6f + right * 4f + new GTA.Math.Vector3(0f, 0f, 2.2f);
            position.Z = System.Math.Max(position.Z, waterHeight + 1.2f);
            camera.Position = position; camera.PointAt(focus); camera.FieldOfView = 55f;
        }
        else if (spec.OlhosDesdeInicio || (!spec.FilmarQuedaExterna && phase >= 2))
        {
            // Câmera pertence à Djanira, inclusive durante a física/ragdoll; não usar visão de Franklin.
            GTA.Math.Vector3 head = actor.Ped.Bones[GTA.Bone.SkelHead].Position;
            double cameraYaw = actor.Ped.Heading * System.Math.PI / 180.0;
            GTA.Math.Vector3 cameraForward = new GTA.Math.Vector3((float)-System.Math.Sin(cameraYaw), (float)System.Math.Cos(cameraYaw), 0f);
            camera.Position = head + right * 0.05f + cameraForward * 0.45f + new GTA.Math.Vector3(0f, 0f, 0.12f);
            camera.PointAt(camera.Position + cameraForward * 12f + new GTA.Math.Vector3(0f, 0f, phase == 2 ? -3f : 0f)); camera.FieldOfView = 45f;
        }
        else if (phase <= 1)
        {
            camera.Position = plane.Position - forward * 10f + right * 14f + new GTA.Math.Vector3(0f, 0f, 4f);
            camera.PointAt(plane.Position + forward * 2f); camera.FieldOfView = 50f;
        }
        else
        {
            GTA.Math.Vector3 focus = actor.Ped.Position + new GTA.Math.Vector3(0f, 0f, 0.4f);
            GTA.Math.Vector3 cameraPosition = focus - forward * 5f + right * 6f + new GTA.Math.Vector3(0f, 0f, 2.3f);
            cameraPosition.Z = System.Math.Max(cameraPosition.Z, waterHeight + 1f);
            camera.Position = cameraPosition; camera.PointAt(focus); camera.FieldOfView = 40f;
        }
    }

    private void CaptureCabinHeadOffset()
    {
        if (!plane.IsPositionFrozen || !InPlane(actor.Ped) || !IsActor(plane.GetPedOnSeat(GTA.VehicleSeat.Passenger), actor))
            throw Error("a câmera interna exige Djanira no assento próprio, antes de iniciar o voo");
        GTA.PedBone bone = actor.Ped.Bones[GTA.Bone.SkelHead];
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("o osso real da cabeça não está disponível na cabine");
        GTA.Math.Vector3 head = bone.Position; Vector(head);
        GTA.Math.Vector3 local = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS,
            plane.Handle, head.X, head.Y, head.Z);
        Vector(local);
        if (local.Length() > 8f || local.X < planeMin.X - FlightPadding || local.X > planeMax.X + FlightPadding ||
            local.Y < planeMin.Y - FlightPadding || local.Y > planeMax.Y + FlightPadding ||
            local.Z < planeMin.Z - FlightPadding || local.Z > planeMax.Z + FlightPadding)
            throw Error("o osso medido não corresponde a uma cabeça dentro da caixa real da cabine");
        cabinHeadLocal = local; cabinHeadCaptured = true;
        Log("QUEDA_CABINE_CABECA_CAPTURADA", "Etapa=StartBeforeUnfreeze; HeadReal=" + FlightVector(head) +
            "; LocalAviao=" + FlightVector(local) + "; Plane=" + FlightVector(plane.Position) +
            "; PassengerIsDjanira=True; LimiteMs=1500; ancoragem medida, não coordenada inventada");
    }

    // Fora da gravação. Não muda posições: root pode restaurar suas marcas depois deste método.
    public bool ReleaseActorsForReset()
    {
        Active(); running = false; resetWaitingSeats = false;
        if (!HasPlane()) return true;
        plane.IsPositionFrozen = true; plane.Velocity = GTA.Math.Vector3.Zero;
        foreach (GTA.Ped occupant in plane.Occupants)
            if (!IsActor(occupant, actor) && !IsActor(occupant, pilot) && !IsActor(occupant, rearPassenger)) throw Error("ocupante alheio no avião; preservar recurso");
        bool pending = ReleasePassenger(actor) | ReleasePassenger(pilot);
        if (borrowedPilot) pending = ReleasePassenger(rearPassenger) | pending;
        return !pending && plane.Occupants.Length == 0;
    }

    private bool ReleasePassenger(ActorState value)
    {
        if (!StillOwn(value)) return false;
        if (InPlane(value.Ped))
        {
            if (!value.LeavingPlane)
            {
                value.Ped.IsPositionFrozen = false; value.Ped.Task.ClearAllImmediately();
                value.Ped.Task.WarpOutOfVehicle(plane); // exclusivamente reinício/limpeza, nunca em Update/Start.
                value.LeavingPlane = true;
            }
            return InPlane(value.Ped);
        }
        value.LeavingPlane = false;
        value.Ped.Task.ClearAllImmediately(); value.Ped.Velocity = GTA.Math.Vector3.Zero;
        return false;
    }

    public void Cleanup()
    {
        if (disposed) return;
        running = false; ready = false; resetDone = false;
        System.Exception failure = null;
        try { RestoreCamera(); } catch (System.Exception ex) { failure = ex; }
        try
        {
            if (!ReleaseActorsForReset()) throw Error("saída dos assentos pendente; chamar Cleanup no próximo frame");
            RestoreActor();
            if (borrowedPilot) { RestoreBorrowed(pilot); RestoreBorrowed(rearPassenger); }
            else DeletePilot();
            if (resources != null) { resources.Cleanup(); resources.Dispose(); resources = null; }
            plane = null;
            if (modelRequested && !modelWasLoaded) pilotModel.MarkAsNoLongerNeeded();
            modelRequested = false;
        }
        catch (System.Exception ex) { if (failure == null) failure = ex; }
        if (failure != null) { status = "limpeza pendente; entidades próprias preservadas para nova tentativa"; throw failure; }
        actor = null; pilot = null; rearPassenger = null; cabinFocus = null; borrowedPilot = false;
        spec = null; prepared = false; failed = false; actorMarkCaptured = false; loading = null;
        routeReady = false; routeSampleIndex = 0; routeRay = 0; routeRayIndex = 0;
        volumeProbes.Clear(); flightVolume = null; lastClearFlightVolume = null; flightMonitorTick = -1;
        volumeBoundsReady = false; corridorReady = false; impactWaterKnown = false; flightVolumeClear = false;
        status = "limpo"; Log("QUEDA_LIMPA", "Flags/câmera anteriores restauradas; apenas avião/piloto próprios removidos");
    }
    public void Dispose() { Cleanup(); disposed = true; }

    private void RestoreCamera()
    {
        if (cameraHandle == 0) return;
        GTA.Camera rendering = GTA.ScriptCameraDirector.RenderingCam;
        if (!System.Object.ReferenceEquals(rendering, null) && rendering.Handle == cameraHandle)
        {
            if (!System.Object.ReferenceEquals(camera, null) && camera.Exists()) camera.IsActive = false;
            if (!System.Object.ReferenceEquals(previousCamera, null) && previousCamera.Exists())
            { previousCamera.IsActive = true; GTA.ScriptCameraDirector.StartRendering(); }
            else GTA.ScriptCameraDirector.StopRendering(false);
        }
        if (!System.Object.ReferenceEquals(camera, null) && camera.Exists()) camera.Delete();
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_CAM_EXIST, cameraHandle)) throw Error("a câmera própria não foi removida; preservar referência para tentar novamente");
        camera = null; previousCamera = null; cameraHandle = 0;
    }

    private void RestoreActor()
    {
        if (actor == null || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, actor.Handle)) return;
        if (!StillOwn(actor)) throw Error("identidade de Djanira mudou; não restaurar entidade alheia");
        if (actor.Ped.IsInVehicle()) throw Error("Djanira ainda está em veículo; não restaurar posição até a saída");
        actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
        actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading;
        actor.Ped.IsInvincible = actor.Invincible; actor.Ped.IsPositionFrozen = actor.Frozen;
        actor.Ped.BlockPermanentEvents = actor.Events; actor.Ped.CanRagdoll = actor.Ragdoll;
        actor.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, actor.Drowning);
        actor.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming, actor.InstantWater);
    }

    private void DeletePilot()
    {
        if (pilot == null) return;
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, pilot.Handle)) { pilot = null; return; }
        RequirePilot(); pilot.Ped.Delete();
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, pilot.Handle))
        {
            pilot.Ped = GTA.Entity.FromHandle(pilot.Handle) as GTA.Ped;
            throw Error("remoção do piloto próprio falhou; preservar identidade para tentar novamente");
        }
        pilot = null;
    }

    private void ProtectActor()
    {
        Protect(actor);
    }
    private static void Protect(ActorState value)
    {
        value.Ped.IsInvincible = true; value.Ped.BlockPermanentEvents = true; value.Ped.CanRagdoll = true;
        value.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, false);
        value.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming, false);
    }
    private static void RestoreBorrowedMark(ActorState value)
    {
        if (!StillOwn(value) || value.Ped.IsDead || value.Ped.IsInVehicle()) throw Error("ator emprestado não está livre para reinício");
        value.Ped.Task.ClearAllImmediately(); value.Ped.Velocity = GTA.Math.Vector3.Zero;
        value.Ped.PositionNoOffset = value.Position; value.Ped.Heading = value.Heading;
    }
    private static void RestoreBorrowed(ActorState value)
    {
        if (value == null || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, value.Handle)) return;
        RestoreBorrowedMark(value);
        value.Ped.IsInvincible = value.Invincible; value.Ped.IsPositionFrozen = value.Frozen;
        value.Ped.BlockPermanentEvents = value.Events; value.Ped.CanRagdoll = value.Ragdoll;
        value.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, value.Drowning);
        value.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming, value.InstantWater);
    }

    private void ProbeCorridor()
    {
        int budget = 6;
        if (!volumeBoundsReady)
        {
            if (!resources.IsReady) return;
            plane = resources.GetVehicle("AVIAO_QUEDA");
            plane.Model.GetDimensions(out planeMin, out planeMax); budget--;
            GTA.Math.Vector3 actorMin, actorMax;
            actor.Ped.Model.GetDimensions(out actorMin, out actorMax); budget--;
            Vector(planeMin); Vector(planeMax); Vector(actorMin); Vector(actorMax);
            if (planeMin.X >= planeMax.X || planeMin.Y >= planeMax.Y || planeMin.Z >= planeMax.Z ||
                actorMin.X >= actorMax.X || actorMin.Y >= actorMax.Y || actorMin.Z >= actorMax.Z || planeMax.Y - planeMin.Y > 24f)
                throw Error("dimensões reais inválidas para conferir o volume do avião/atriz");
            planeCenter = (planeMin + planeMax) * 0.5f;
            tileHalfX = (planeMax.X - planeMin.X + 2f * FlightPadding) / (2f * AirColumns);
            tileHalfZ = (planeMax.Z - planeMin.Z + 2f * FlightPadding) / (2f * AirRows);
            // Cada célula retangular inteira cabe em seu círculo; cobertura sem lacunas diagonais.
            airRadius = (float)System.Math.Sqrt(tileHalfX * tileHalfX + tileHalfZ * tileHalfZ) + 0.001f;
            if (airRadius - tileHalfX + FlightPadding > 1f || airRadius - tileHalfZ + FlightPadding > 1f)
                throw Error("o mosaico do volume excede1m de margem da caixa real do avião");
            // Monitor dinâmico convexo: envolver o bbox inteiro e conferir os8cantos atuais.
            float modelX = System.Math.Max(System.Math.Abs(planeMin.X), System.Math.Abs(planeMax.X));
            float modelZ = System.Math.Max(System.Math.Abs(planeMin.Z), System.Math.Abs(planeMax.Z));
            float runtimeX = spec.EnsaioPassivo ? (planeMax.X - planeMin.X) / 8f : modelX;
            runtimeAirRadius = (float)System.Math.Sqrt(runtimeX * runtimeX + modelZ * modelZ) + FlightPadding;
            if (!Finite(runtimeAirRadius) || runtimeAirRadius <= 0f) throw Error("raio real do volume aéreo dinâmico inválido");
            float actorX = (actorMax.X - actorMin.X) * 0.5f, actorY = (actorMax.Y - actorMin.Y) * 0.5f;
            impactRadius = (float)System.Math.Sqrt(actorX * actorX + actorY * actorY) + 0.25f;
            airFrom = (spec.EnsaioPassivo ? 0f : (spec.FilmarQuedaExterna ? planeStartAlong - 10f : -80f)) + planeMin.Y - 1f;
            airTo = (spec.EnsaioPassivo ? spec.VelocidadeAviao * ((7000f + 3500f) / 1000f) : 220f) + planeMax.Y + 1f;
            airSegments = System.Math.Max(1, (int)System.Math.Ceiling((airTo - airFrom) / 24f));
            // Mesmos limites de UpdateFlight: toda a zona planejada recebe água e clearance corporal.
            impactZoneStart = releaseZoneStart;
            releaseSections = (int)System.Math.Ceiling((releaseZoneEnd - impactZoneStart) / 4f) + 1;
            impactSampleCount = releaseSections * 3;
            sampleIndex = 0;
            volumeBoundsReady = true;
            Log("QUEDA_VOLUME_DIMENSOES", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Modelo=velum; Min=({0:R},{1:R},{2:R}); Max=({3:R},{4:R},{5:R}); AirRadius={6:R}; Padding={7:R}; ImpactRadius={8:R}; AirFrom={9:R}; AirTo={10:R}; AirSegments={11}; Tiles=7x3; ZoneStart={12:R}; PrimeiraAmostraAgua={13}; ZoneEnd={20:R}; InicioAviao={21:R}; FlightZ={14:R}; ExtraX={15:R}; ExtraZ={16:R}; AguaCorporalRaio={8:R}; RuntimeShape=convex_capsule; RuntimeSpan={18:R}; RuntimeRadius={17:R}; RuntimeBands={19}",
                planeMin.X, planeMin.Y, planeMin.Z, planeMax.X, planeMax.Y, planeMax.Z, airRadius, FlightPadding, impactRadius,
                airFrom, airTo, airSegments, impactZoneStart, sampleIndex, waterHeight + spec.AlturaRelativa,
                airRadius - tileHalfX + FlightPadding, airRadius - tileHalfZ + FlightPadding, runtimeAirRadius,
                spec.EnsaioPassivo ? planeMax.Y - planeMin.Y + spec.VelocidadeAviao * 0.5f + 1f : FlightSpan, spec.EnsaioPassivo ? 4 : RuntimeAirProbes, releaseZoneEnd, planeStartAlong));
        }
        // Ler só handles criados em PollLoading anterior; pending nunca equivale a espaço livre.
        for (int i = 0; i < volumeProbes.Count && budget > 0; )
        {
            budget--;
            if (PollVolumeProbe(volumeProbes[i])) volumeProbes.RemoveAt(i);
            else i++;
        }
        while (airSampleIndex < airSegments * AirTiles && budget >= 2 && volumeProbes.Count < 6)
        {
            int segment = airSampleIndex / AirTiles, tile = airSampleIndex % AirTiles;
            float fromAlong = airFrom + (airTo - airFrom) * segment / airSegments;
            float toAlong = airFrom + (airTo - airFrom) * (segment + 1) / airSegments;
            GTA.Math.Vector3 modelRight = spec.InverterEixo ? -right : right;
            GTA.Math.Vector3 offset = modelRight * TileX(tile) +
                new GTA.Math.Vector3(0f, 0f, waterHeight + spec.AlturaRelativa + TileZ(tile) - spec.Agua.Z);
            GTA.Math.Vector3 from = spec.Agua + forward * fromAlong + offset, to = spec.Agua + forward * toAlong + offset;
            GTA.Math.Vector3 middle = (from + to) * 0.5f;
            RequestVolumeCollision(from); RequestVolumeCollision(middle); RequestVolumeCollision(to);
            float ground;
            budget--;
            // Solo finito confirma streaming local. Sua altura não decide se o espaço aéreo está livre.
            if (!GTA.World.GetGroundHeight(middle + new GTA.Math.Vector3(0f, 0f, 60f), out ground, GTA.GetGroundHeightMode.Normal)) return;
            if (!Finite(ground)) throw Error("o terreno usado para confirmar streaming do voo não é finito");
            budget--;
            volumeProbes.Add(StartVolumeProbe("voo_preparo", airSampleIndex, from, to, airRadius));
            airSampleIndex++;
        }
        if (airSampleIndex < airSegments * AirTiles) return;
        while (sampleIndex < impactSampleCount && budget > 0 && volumeProbes.Count < 6)
        {
            if (!impactWaterKnown)
            {
                if (budget < 3) return; // Reservar o pior caso do mesmo TryWater: getter/probe/ground.
                int section = sampleIndex / 3, side = sampleIndex % 3 - 1;
                float along = System.Math.Min(releaseZoneEnd, impactZoneStart + section * 4f);
                GTA.Math.Vector3 point = spec.Agua + forward * along + right * (side * impactRadius);
                float height;
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
                var diagnostic = new WaterQueryDiagnostic();
                bool found = TryWater(point, out height, diagnostic);
                budget -= (diagnostic.DirectQueried ? 1 : 0) + (diagnostic.ProbeQueried ? 1 : 0) + (diagnostic.GroundQueried ? 1 : 0);
                if (!found || System.Math.Abs(height - waterHeight) > 1.5f)
                {
                    Log("QUEDA_AGUA_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Amostra={0}; Secao={1}; Lado={2}; Ponto=({3:R},{4:R},{5:R}); Height={6:R}; WaterHeight={7:R}; Diff={8:R}; {9}",
                        sampleIndex, section, side, point.X, point.Y, point.Z, height, waterHeight, System.Math.Abs(height - waterHeight), diagnostic.ToLog()));
                    throw Error("o lançamento não oferece água/clearance no raio corporal real; escolha trecho real aberto");
                }
                impactProbePoint = point; impactProbeHeight = height; impactPart = 0; impactWaterKnown = true;
                float bottom = height + impactRadius + 0.1f;
                float top = waterHeight + spec.AlturaRelativa + System.Math.Max(0f, planeMax.Z) + 1f;
                impactParts = System.Math.Max(1, (int)System.Math.Ceiling((top - bottom) / 24f));
            }
            if (budget < 1) return;
            float low = impactProbeHeight + impactRadius + 0.1f;
            float high = waterHeight + spec.AlturaRelativa + System.Math.Max(0f, planeMax.Z) + 1f;
            GTA.Math.Vector3 fromImpact = new GTA.Math.Vector3(impactProbePoint.X, impactProbePoint.Y,
                low + (high - low) * impactPart / impactParts);
            GTA.Math.Vector3 toImpact = new GTA.Math.Vector3(impactProbePoint.X, impactProbePoint.Y,
                low + (high - low) * (impactPart + 1) / impactParts);
            RequestVolumeCollision(fromImpact); RequestVolumeCollision(toImpact);
            budget--;
            volumeProbes.Add(StartVolumeProbe("queda_preparo", sampleIndex, fromImpact, toImpact, impactRadius));
            impactPart++;
            if (impactPart >= impactParts) { impactWaterKnown = false; sampleIndex++; }
        }
        if (sampleIndex >= impactSampleCount && volumeProbes.Count == 0)
        {
            corridorReady = true;
            Log("QUEDA_VOLUMES_CONFERIDOS", "Aereo=" + airSampleIndex + "; UltimaAmostraAgua=" + (sampleIndex - 1) +
                "; Lancamento=" + releaseZoneStart.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ".." + releaseZoneEnd.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "m; InicioAviao=" + planeStartAlong.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; RaioCorporal=" + impactRadius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; agua/clearance corporal amostrados; impacto real permanece sob guarda por frame; sem autorizar warp");
        }
    }

    private static void RequestVolumeCollision(GTA.Math.Vector3 point)
    { GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z); }

    private VolumeProbe StartVolumeProbe(string stage, int index, GTA.Math.Vector3 from, GTA.Math.Vector3 to, float radius)
    {
        var probe = new VolumeProbe { Stage = stage, Index = index, From = from, To = to, Radius = radius };
        probe.Ray = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_CAPSULE,
            from.X, from.Y, from.Z, to.X, to.Y, to.Z, radius, 19, plane.Handle, 4);
        if (probe.Ray == 0) throw Error("não foi possível iniciar cápsula de colisão real: " + stage);
        return probe;
    }

    private bool PollVolumeProbe(VolumeProbe probe)
    {
        System.IntPtr hit = System.IntPtr.Zero, point = System.IntPtr.Zero, normal = System.IntPtr.Zero, entity = System.IntPtr.Zero;
        try
        {
            byte[] zero = new byte[24];
            hit = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
            point = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
            normal = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
            entity = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
            System.Runtime.InteropServices.Marshal.Copy(zero, 0, hit, 24);
            System.Runtime.InteropServices.Marshal.Copy(zero, 0, point, 24);
            System.Runtime.InteropServices.Marshal.Copy(zero, 0, normal, 24);
            System.Runtime.InteropServices.Marshal.Copy(zero, 0, entity, 24);
            int result = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, probe.Ray, hit, point, normal, entity);
            if (result == 1) return false;
            int collided = System.Runtime.InteropServices.Marshal.ReadInt32(hit);
            if (result != 2 || collided != 0)
            {
                float[] coordinates = new float[6];
                System.Runtime.InteropServices.Marshal.Copy(point, coordinates, 0, 6);
                Log("QUEDA_VOLUME_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Etapa={0}; Amostra={1}; Status={2}; Hit={3}; From=({4:R},{5:R},{6:R}); To=({7:R},{8:R},{9:R}); Radius={10:R}; HitPoint=({11:R},{12:R},{13:R}); Entity={14}",
                    probe.Stage, probe.Index, result, collided, probe.From.X, probe.From.Y, probe.From.Z, probe.To.X, probe.To.Y, probe.To.Z,
                    probe.Radius, coordinates[0], coordinates[2], coordinates[4], System.Runtime.InteropServices.Marshal.ReadInt32(entity)));
                throw Error("volume de voo/queda bloqueado ou medição inválida: " + probe.Stage);
            }
            probe.Ray = 0; return true;
        }
        finally
        {
            if (hit != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(hit);
            if (point != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(point);
            if (normal != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(normal);
            if (entity != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(entity);
        }
    }

    private float TileX(int tile)
    { return planeMin.X - FlightPadding + tileHalfX * (1f + 2f * (tile % AirColumns)); }
    private float TileZ(int tile)
    { return planeMin.Z - FlightPadding + tileHalfZ * (1f + 2f * (tile / AirColumns)); }

    private bool MonitorFlightVolume(int budget = 6)
    {
        // UpdateFlight e RequestLeave podem consultar no mesmo frame: orçamento único.
        if (flightMonitorTick == GTA.Game.GameTime) return flightVolumeClear;
        flightMonitorTick = GTA.Game.GameTime;
        if (flightVolume != null)
        {
            for (int i = 0; i < flightVolume.Pending.Count && budget > 0; )
            {
                budget--;
                if (PollVolumeProbe(flightVolume.Pending[i]))
                { flightVolume.Pending.RemoveAt(i); flightVolume.Completed++; }
                else i++;
            }
            if (flightVolume.Completed == flightVolume.Bands)
            { flightVolume.CompletedAt = flightMonitorTick; lastClearFlightVolume = flightVolume; flightVolume = null; }
        }
        flightVolumeClear = FlightBoxCovered(lastClearFlightVolume);
        if (flightVolume == null)
        {
            flightVolume = new FlightVolume { Origin = plane.Position, Right = plane.RightVector,
                Up = plane.UpVector, Forward = plane.ForwardVector, StartedAt = flightMonitorTick, Radius = runtimeAirRadius, Bands = spec.EnsaioPassivo ? 4 : RuntimeAirProbes };
            flightVolume.From = flightVolume.Origin + flightVolume.Forward * planeMin.Y;
            float span = spec.EnsaioPassivo ? planeMax.Y - planeMin.Y + spec.VelocidadeAviao * 0.5f + 1f : FlightSpan;
            flightVolume.To = flightVolume.From + flightVolume.Forward * span;
        }
        while (budget > 0 && flightVolume.Sent < flightVolume.Bands && flightVolume.Pending.Count < 6)
        {
            float bandX = flightVolume.Bands == 1 ? 0f : planeMin.X + (planeMax.X - planeMin.X) * (flightVolume.Sent + 0.5f) / flightVolume.Bands;
            GTA.Math.Vector3 from = flightVolume.From + flightVolume.Right * bandX, to = flightVolume.To + flightVolume.Right * bandX;
            RequestVolumeCollision(from); RequestVolumeCollision(to);
            budget--;
            flightVolume.Pending.Add(StartVolumeProbe("voo_atual_capsula", flightVolume.Sent, from, to, flightVolume.Radius));
            flightVolume.Sent++;
        }
        return flightVolumeClear;
    }

    private bool FlightBoxCovered(FlightVolume clear)
    {
        if (clear == null || clear.Bands < 1 || clear.Completed != clear.Bands)
        {
            flightCoverageFailure = "Reason=no_completed_certificate; Certificado=" + FlightCertificateDescription(clear, flightMonitorTick);
            return false;
        }
        GTA.Math.Vector3 segment = clear.To - clear.From;
        float lengthSquared = Dot(segment, segment), radiusSquared = clear.Radius * clear.Radius;
        if (!Finite(lengthSquared) || lengthSquared <= 0f || !Finite(radiusSquared) || radiusSquared <= 0f)
        { flightCoverageFailure = "Reason=invalid_capsule_certificate"; return false; }
        GTA.Math.Vector3 position = plane.Position, currentRight = plane.RightVector,
            currentForward = plane.ForwardVector, currentUp = plane.UpVector;
        float[] ys = new float[] { planeMin.Y, planeMax.Y };
        float[] zs = new float[] { planeMin.Z, planeMax.Z };
        for (int band = 0; band < clear.Bands; band++)
        {
        float xFrom = planeMin.X + (planeMax.X - planeMin.X) * band / clear.Bands;
        float xTo = planeMin.X + (planeMax.X - planeMin.X) * (band + 1) / clear.Bands;
        float centerX = clear.Bands == 1 ? 0f : (xFrom + xTo) * 0.5f;
        GTA.Math.Vector3 capsuleFrom = clear.From + clear.Right * centerX;
        float[] xs = new float[] { xFrom, xTo };
        foreach (float x in xs) foreach (float y in ys) foreach (float z in zs)
        {
            GTA.Math.Vector3 corner = position + currentRight * x + currentForward * y + currentUp * z;
            float parameter = Dot(corner - capsuleFrom, segment) / lengthSquared;
            parameter = System.Math.Max(0f, System.Math.Min(1f, parameter));
            GTA.Math.Vector3 closest = capsuleFrom + segment * parameter, difference = corner - closest;
            float distanceSquared = Dot(difference, difference);
            if (!Finite(parameter) || !Finite(distanceSquared) || distanceSquared > radiusSquared)
            {
                flightCoverageFailure = System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Reason=corner_outside_or_nonfinite_capsule; Corner=({0:R},{1:R},{2:R}); CornerWorld={3}; Closest={4}; Parameter={5:R}; DistanceSquared={6:R}; RadiusSquared={7:R}; PlanePosition={8}; PlaneRight={9}; PlaneForward={10}; PlaneUp={11}; Certificado={12}",
                    x, y, z, FlightVector(corner), FlightVector(closest), parameter, distanceSquared, radiusSquared,
                    FlightVector(position), FlightVector(currentRight), FlightVector(currentForward), FlightVector(currentUp),
                    FlightCertificateDescription(clear, flightMonitorTick));
                return false;
            }
        }
        }
        // Cada capsula concluida e convexa: oito vertices por faixa provam cada subcaixa e a caixa inteira.
        flightCoverageFailure = "covered";
        return true;
    }

    // Somente valores já capturados pelas mesmas consultas/batches; nenhuma query adicional.
    private static string FlightCertificateDescription(FlightVolume volume, int now)
    {
        if (volume == null) return "none";
        return "{StartedAt=" + volume.StartedAt + "; StartedAgeMs=" + unchecked(now - volume.StartedAt) +
            "; CompletedAt=" + volume.CompletedAt + "; CompletedAgeMs=" +
            (volume.CompletedAt == 0 ? "pending" : unchecked(now - volume.CompletedAt).ToString(System.Globalization.CultureInfo.InvariantCulture)) +
            "; Sent=" + volume.Sent + "; Completed=" + volume.Completed + "; Pending=" + volume.Pending.Count +
            "; Origin=" + FlightVector(volume.Origin) + "; Right=" + FlightVector(volume.Right) +
            "; Forward=" + FlightVector(volume.Forward) + "; Up=" + FlightVector(volume.Up) +
            "; From=" + FlightVector(volume.From) + "; To=" + FlightVector(volume.To) +
            "; Radius=" + volume.Radius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";
    }
    private static string FlightVector(GTA.Math.Vector3 value)
    { return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:R},{1:R},{2:R})", value.X, value.Y, value.Z); }

    private void ValidateRouteDefinition()
    {
        GTA.Math.Vector3 entry = spec.EntradaRampa.Value, top = spec.TopoRampa.Value, escape = spec.Fuga.Value;
        Vector(entry); Vector(top); Vector(escape);
        if (Horizontal(entry - spec.Agua) > 160f || Horizontal(top - entry) < 1f || Horizontal(top - entry) > 25f ||
            top.Z < entry.Z - 1f || top.Z - entry.Z > 8f || Horizontal(escape - top) < 2f || Horizontal(escape - top) > 40f)
            throw Error("as marcas de água/rampa/fuga reais não formam um percurso técnico curto");
        float height;
        if (!TryWater(entry, out height) || System.Math.Abs(entry.Z - height) > 3f)
            throw Error("a entrada real da rampa deve estar junto à água");
    }

    private bool RouteGroundAvailable()
    {
        if (routeReady) return true;
        GTA.Math.Vector3 entry = spec.EntradaRampa.Value, top = spec.TopoRampa.Value, escape = spec.Fuga.Value;
        int rampSteps = System.Math.Max(1, (int)System.Math.Ceiling(Horizontal(top - entry) / 2f));
        int escapeSteps = System.Math.Max(1, (int)System.Math.Ceiling(Horizontal(escape - top) / 2f));
        // Conferir continuidade do piso real, não só duas extremidades válidas.
        for (int budget = 0; budget < 1 && routeSampleIndex <= rampSteps + escapeSteps; budget++)
        {
            bool ramp = routeSampleIndex <= rampSteps;
            GTA.Math.Vector3 point = ramp ? entry + (top - entry) * ((float)routeSampleIndex / rampSteps) :
                top + (escape - top) * ((float)(routeSampleIndex - rampSteps) / escapeSteps);
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
            float ground; GTA.Math.Vector3 normal;
            if (!GTA.World.GetGroundHeightAndNormal(point + new GTA.Math.Vector3(0f, 0f, 5f), out ground, out normal)) return false;
            if (!Finite(ground) || !Finite(normal.Z) || normal.Z < 0.65f || System.Math.Abs(point.Z - ground) > 2f ||
                (routeSampleIndex > 0 && System.Math.Abs(ground - previousRouteGround) > 1.8f))
            {
                Log("QUEDA_ROUTE_GROUND_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Amostra={0}; Trecho={1}; Ponto=({2:R},{3:R},{4:R}); Ground={5:R}; Normal=({6:R},{7:R},{8:R}); DiffZ={9:R}; PreviousGround={10:R}; DiffGround={11}",
                    routeSampleIndex, ramp ? "rampa" : "fuga", point.X, point.Y, point.Z, ground, normal.X, normal.Y, normal.Z,
                    System.Math.Abs(point.Z - ground), previousRouteGround,
                    routeSampleIndex > 0 ? System.Math.Abs(ground - previousRouteGround).ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "n/a"));
                throw Error("as marcas não seguem rampa/piso reais contínuos; não simular degrau ou escalar parede");
            }
            float surface;
            if (!ramp && TryWater(point, out surface) && surface > ground + 0.1f)
                throw Error("a fuga marcada volta à água; marque trajeto terrestre real depois da rampa");
            previousRouteGround = ground; routeSampleIndex++;
        }
        if (routeSampleIndex <= rampSteps + escapeSteps) return false;
        // Três linhas em cada segmento: medir paredes/portões sem alterar mapa.
        if (routeRayIndex < 6)
        {
            bool ramp = routeRayIndex < 3;
            GTA.Math.Vector3 from = ramp ? entry : top, to = ramp ? top : escape;
            GTA.Math.Vector3 direction = to - from;
            float length = Horizontal(direction);
            GTA.Math.Vector3 side = new GTA.Math.Vector3(direction.Y / length, -direction.X / length, 0f);
            GTA.Math.Vector3 offset = side * ((routeRayIndex % 3 - 1) * 0.35f) + new GTA.Math.Vector3(0f, 0f, 0.1f);
            if (routeRay == 0)
            {
                from += offset; to += offset;
                routeRay = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE,
                    from.X, from.Y, from.Z, to.X, to.Y, to.Z, 17, 0, 7);
                if (routeRay == 0) throw Error("não foi possível medir colisão da rampa/fuga");
                return false;
            }
            // Ler apenas o escalar nativo em buffers zerados; GetResult<bool> leria padding de oito bytes.
            System.IntPtr hit = System.IntPtr.Zero, point = System.IntPtr.Zero, normal = System.IntPtr.Zero, entity = System.IntPtr.Zero;
            try
            {
                byte[] zero = new byte[24];
                hit = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
                point = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
                normal = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
                entity = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24);
                System.Runtime.InteropServices.Marshal.Copy(zero, 0, hit, 24);
                System.Runtime.InteropServices.Marshal.Copy(zero, 0, point, 24);
                System.Runtime.InteropServices.Marshal.Copy(zero, 0, normal, 24);
                System.Runtime.InteropServices.Marshal.Copy(zero, 0, entity, 24);
                int result = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, routeRay, hit, point, normal, entity);
                if (result == 1) return false;
                routeRay = 0;
                if (result != 2 || System.Runtime.InteropServices.Marshal.ReadInt32(hit) != 0) throw Error("rampa/fuga bloqueadas por colisão real; escolha passagem aberta, sem remover objetos do mapa");
            }
            finally
            {
                if (hit != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(hit);
                if (point != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(point);
                if (normal != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(normal);
                if (entity != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(entity);
            }
            routeRayIndex++; return false;
        }
        routeReady = true;
        Log("QUEDA_RAMPA_FUGA_CONFERIDAS", "Piso/normal/continuidade e seis linhas de colisão; material concreto/encaixe visual ainda exigem confirmação");
        return true;
    }

    private static ActorState Capture(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsPlayer || ped.IsDead || ped.IsInVehicle() || !ped.IsPersistent)
            throw Error("forneça ator próprio persistente, vivo, fora de veículo e diferente do jogador");
        return new ActorState { Ped = ped, Handle = ped.Handle, Model = ped.Model.Hash, Address = ped.MemoryAddress,
            Position = ped.Position, Heading = ped.Heading, Health = ped.Health, Invincible = ped.IsInvincible,
            Frozen = ped.IsPositionFrozen, Ragdoll = ped.CanRagdoll, Events = ped.BlockPermanentEvents,
            Drowning = ped.GetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater),
            InstantWater = ped.GetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming) };
    }
    private static bool StillOwn(ActorState value)
    { return value != null && !System.Object.ReferenceEquals(value.Ped, null) && value.Ped.Exists() && value.Ped.Handle == value.Handle &&
        value.Ped.MemoryAddress == value.Address && value.Ped.Model.Hash == value.Model && !value.Ped.IsPlayer; }
    private static bool IsActor(GTA.Ped ped, ActorState value)
    { return StillOwn(value) && !System.Object.ReferenceEquals(ped, null) && ped.Handle == value.Handle && ped.MemoryAddress == value.Address; }
    private bool HasPlane()
    {
        if (resources == null || !resources.IsReady || System.Object.ReferenceEquals(plane, null) || !plane.Exists()) return false;
        plane = resources.GetVehicle("AVIAO_QUEDA"); // Confere handle/modelo/endereço antes de tocar o recurso.
        return true;
    }
    private bool InPlane(GTA.Ped ped)
    { return !System.Object.ReferenceEquals(plane, null) && plane.Exists() && ped.IsInVehicle(plane); }
    private void RequireActor()
    { if (!StillOwn(actor) || actor.Ped.IsDead || actor.Ped.Health < actor.Health) throw Error("identidade/vida de Djanira mudou durante o ensaio"); }
    private void RequirePilot()
    { if (!StillOwn(pilot) || pilot.Ped.IsDead) throw Error("identidade/vida do piloto próprio mudou"); }
    private void RequireRearPassenger()
    { if (!StillOwn(rearPassenger) || rearPassenger.Ped.IsDead || rearPassenger.Ped.Health < rearPassenger.Health) throw Error("identidade/vida da passageira traseira mudou"); }
    private void RequireReady()
    { Active(); if (!IsReady) throw Error("aguarde uma preparação válida antes da tomada"); }
    internal sealed class WaterQueryDiagnostic
    {
        internal string Reason = "not_completed";
        internal bool PointValid, DirectQueried, DirectFound, DirectRead, ProbeQueried, ProbeFound, ProbeRead, GroundQueried, GroundFound;
        internal float Top, Bottom, DirectHeight, GroundHeight;
        internal GTA.Math.Vector3 ProbeWater;

        internal string ToLog()
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
            return "TryWaterReason=" + Reason +
                "; WaterTopZ=" + (PointValid ? Top.ToString("R", culture) : "nao_calculado") +
                "; WaterBottomZ=" + (PointValid ? Bottom.ToString("R", culture) : "nao_calculado") +
                "; GetWaterQueried=" + DirectQueried + "; GetWaterFound=" + DirectFound +
                "; GetWaterHeight=" + (DirectRead ? DirectHeight.ToString("R", culture) : "nao_lido") +
                "; ProbeQueried=" + ProbeQueried + "; ProbeFound=" + ProbeFound + "; ProbeFlag=0" +
                "; ProbeWater=" + (ProbeRead ? System.String.Format(culture, "({0:R},{1:R},{2:R})", ProbeWater.X, ProbeWater.Y, ProbeWater.Z) : "nao_lido") +
                "; GroundQueried=" + GroundQueried + "; GroundFound=" + GroundFound +
                "; GroundProbeZ=" + (GroundQueried ? Top.ToString("R", culture) : "nao_consultado") +
                "; GroundHeight=" + (GroundFound ? GroundHeight.ToString("R", culture) : "nao_encontrado");
        }
    }
    internal static bool TryWater(GTA.Math.Vector3 point, out float height)
    {
        return TryWater(point, out height, null);
    }
    internal static bool TryWater(GTA.Math.Vector3 point, out float height, WaterQueryDiagnostic diagnostic)
    {
        height = 0f;
        if (!Finite(point.X) || !Finite(point.Y) || !Finite(point.Z))
        {
            if (diagnostic != null) diagnostic.Reason = "point_nonfinite";
            return false;
        }
        float top = point.Z + 60f, bottom = point.Z - 60f;
        if (diagnostic != null) { diagnostic.PointValid = true; diagnostic.Top = top; diagnostic.Bottom = bottom; }
        using (var output = new GTA.Native.OutputArgument())
        {
            bool found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_WATER_HEIGHT, point.X, point.Y, top, output);
            if (diagnostic != null) { diagnostic.DirectQueried = true; diagnostic.DirectFound = found; }
            if (found)
            {
                float direct = output.GetResult<float>();
                if (diagnostic != null) { diagnostic.DirectRead = true; diagnostic.DirectHeight = direct; }
                if (Finite(direct))
                {
                    height = direct;
                    if (diagnostic != null) diagnostic.Reason = "direct_water";
                    return true;
                }
            }
        }
        // Rio observado no GTA: GET_WATER_HEIGHT=false, mas o probe vertical detectou a superfície real.
        using (var output = new GTA.Native.OutputArgument())
        {
            bool found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.TEST_PROBE_AGAINST_ALL_WATER,
                point.X, point.Y, top, point.X, point.Y, bottom, 0, output);
            if (diagnostic != null) { diagnostic.ProbeQueried = true; diagnostic.ProbeFound = found; }
            if (!found)
            {
                if (diagnostic != null) diagnostic.Reason = "probe_not_found";
                return false;
            }
            GTA.Math.Vector3 water = output.GetResult<GTA.Math.Vector3>();
            if (diagnostic != null) { diagnostic.ProbeRead = true; diagnostic.ProbeWater = water; }
            if (!Finite(water.X) || !Finite(water.Y) || !Finite(water.Z) ||
                System.Math.Abs(water.X - point.X) > 0.01f || System.Math.Abs(water.Y - point.Y) > 0.01f ||
                water.Z < bottom || water.Z > top)
            {
                if (diagnostic != null) diagnostic.Reason = "probe_invalid_point";
                return false;
            }
            float ground;
            bool groundFound = GTA.World.GetGroundHeight(new GTA.Math.Vector3(point.X, point.Y, top), out ground, GTA.GetGroundHeightMode.Normal);
            if (diagnostic != null) { diagnostic.GroundQueried = true; diagnostic.GroundFound = groundFound; diagnostic.GroundHeight = ground; }
            if (!groundFound || !Finite(ground) || ground > water.Z + 0.1f)
            {
                if (diagnostic != null) diagnostic.Reason = !groundFound ? "ground_not_found" : !Finite(ground) ? "ground_nonfinite" : "ground_above_water";
                return false;
            }
            height = water.Z;
            if (diagnostic != null) diagnostic.Reason = "probe_water";
            return true;
        }
    }
    private static float Horizontal(GTA.Math.Vector3 value) { return (float)System.Math.Sqrt(value.X * value.X + value.Y * value.Y); }
    private static float Dot(GTA.Math.Vector3 a, GTA.Math.Vector3 b) { return a.X * b.X + a.Y * b.Y; }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Vector(GTA.Math.Vector3 value) { if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("marcas/direções precisam conter números finitos"); }
    private void Active() { if (disposed) throw Error("helper encerrado"); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Queda técnica: " + message + "."); }
    private void Log(string code, string message) { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
}

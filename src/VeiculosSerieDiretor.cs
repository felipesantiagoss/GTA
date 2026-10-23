// Backend isolado C#5; modelos identificados, integração e teste visual pendentes.
// Somente veículos/motoristas criados aqui e atores próprios recebidos do Diretor.
public sealed class VeiculosSerieDiretor : System.IDisposable
{
    public sealed class VehicleAction
    {
        public string Tipo, Ator;
        public int InicioMs, DuracaoMs = 4000, Assento;
        public float Velocidade = 8f, RaioChegada = 1.5f;
        public float? RaioParada;
        public System.Collections.Generic.List<GTA.Math.Vector3> RotaRelativa = new System.Collections.Generic.List<GTA.Math.Vector3>();
    }
    public sealed class VehicleSpec
    {
        public string Codigo, Tipo, Modelo;
        public GTA.Math.Vector3 PosicaoRelativa;
        public float HeadingRelativo;
        public bool MotoristaExtra, TrajetoDireto;
        public System.Collections.Generic.Dictionary<string, int> ActorInitialSeats = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
        public System.Collections.Generic.List<VehicleAction> Actions = new System.Collections.Generic.List<VehicleAction>();
    }
    private sealed class ActorState
    {
        internal string Code;
        internal GTA.Ped Ped;
        internal int Handle, Model, LeavingVehicle;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Position;
        internal float Heading;
        internal bool Extra, Frozen, Invincible, Events, MarkSaved, AutoHelmet, AvoidAutoHelmet, Collision;
    }
    private sealed class VehicleState
    {
        internal VehicleSpec Spec;
        internal GTA.Vehicle Vehicle;
        internal GTA.Math.Vector3 Spawn;
        internal float Heading;
        internal ActorState Extra;
        internal float MeasuredGround;
        internal bool GroundMeasured, CoachLayerApplied, CoachLayerSettled;
        internal int CoachLayerAppliedAt, CoachStableAt = -1, CoachNextLog, CoachNextSupportLog, CoachNextColliderLog;
        internal string CoachPendingEvidence;
        internal GTA.Math.Vector3 CoachLastPosition;
    }
    private sealed class Operation
    {
        internal VehicleState Vehicle;
        internal VehicleAction Spec;
        internal ActorState Actor;
        internal System.Collections.Generic.List<GTA.Math.Vector3> Route = new System.Collections.Generic.List<GTA.Math.Vector3>();
        internal bool Started, Done;
        internal bool C3NativeGoalPrepared, C3NativeGoalGroundMeasured;
        internal GTA.Math.Vector3 C3NativeGoal;
        internal bool C1NativeGoalPrepared, C1NativeGoalGroundMeasured;
        internal GTA.Math.Vector3 C1NativeGoal;
        internal bool CaronaNativeGoalPrepared, CaronaNativeGoalGroundMeasured;
        internal GTA.Math.Vector3 CaronaNativeGoal;
        internal bool C9NativeGoalsPrepared;
        internal readonly System.Collections.Generic.List<GTA.Math.Vector3> C9NativeGoals = new System.Collections.Generic.List<GTA.Math.Vector3>();
        internal readonly System.Collections.Generic.List<bool> C9NativeGoalsGroundMeasured = new System.Collections.Generic.List<bool>();
        internal bool LuxuryOwnDriverArrivalHold;
        internal bool CaronaOwnDriverArrivalHold;
        internal bool CaronaPassByObserved, CaronaPassByPreviousSample;
        internal float CaronaPassByPreviousProjection;
        internal int Point;
        internal long NextLog;
        internal int ExitRay, ExitRayIssuedAt;
        internal string ExitProbeState = "not_requested";
        internal GTA.Math.Vector3 ExitFrom, ExitTo, ExitHit, ExitNormal;
        internal int ExitHitEntity;
        internal bool ExitNativeSent, ExitTaskObserved, ExitMovementObserved, ExitNextTickLogged;
        internal int ExitIssuedAt, ExitLastSampleAt, ExitSamples, ExitFirstOutsideAt = -1, ExitInitialHealth;
        internal bool ExitCollisionRestored;
        internal int ExitCheckpointElapsed = -1, ExitCheckpointFrames;
        internal GTA.Math.Vector3 ExitInitialHead, ExitInitialPelvis, ExitLastHead, ExitLastPelvis;
        internal float ExitPeakBoneStep, ExitPeakBoneSpeed;
    }
    private sealed class GroundSample { internal GTA.Math.Vector3 Point; internal Operation Operation; internal VehicleState Vehicle; internal int RouteIndex = -1; internal bool C3NativeGoalEndpoint, C1NativeGoalEndpoint, C9NativeGoalEndpoint, CaronaNativeGoalEndpoint; }
    private readonly System.Action<string, string> log;
    private readonly string sceneId;
    private readonly System.Collections.Generic.Dictionary<string, ActorState> actors = new System.Collections.Generic.Dictionary<string, ActorState>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.List<VehicleState> vehicles = new System.Collections.Generic.List<VehicleState>();
    private readonly System.Collections.Generic.List<Operation> operations = new System.Collections.Generic.List<Operation>();
    private readonly System.Collections.Generic.List<GroundSample> samples = new System.Collections.Generic.List<GroundSample>();
    private ObjetosDiretor resources;
    private GTA.Math.Vector3 anchor, right, forward;
    private GTA.Model driverModel;
    private bool driverRequested, driverWasLoaded, ready, prepared, running, resetDone, failed, finished, disposed, resetWaitingSeats;
    private int sampleIndex, lastElapsed = -1, totalDuration;
    private bool resetSettlingCoach;
    private System.Diagnostics.Stopwatch coachResetLoading;
    private System.Diagnostics.Stopwatch loading;

    public VeiculosSerieDiretor(System.Action<string, string> log, string sceneId = null) { this.log = log; this.sceneId = sceneId; }
    public bool IsReady { get { return prepared && ready && !failed && !disposed; } }
    public bool IsFinished { get { return finished && !failed; } }
    public bool HasFailed { get { return failed; } }
    public int TotalDurationMs { get { return totalDuration; } }
    public GTA.Vehicle GetVehicle(string codigo)
    {
        RequireReady();
        foreach (VehicleState vehicle in vehicles)
            if (vehicle.Spec.Codigo == codigo) return OwnVehicle(vehicle);
        throw Error("veículo não pertence à tomada: " + codigo);
    }
    public bool OwnsVehicleContaining(GTA.Ped ped)
    {
        if (!IsReady || System.Object.ReferenceEquals(ped, null)) return false;
        bool ownActor = false;
        foreach (ActorState actor in actors.Values)
            if (actor.Ped == ped && StillOwn(actor)) { ownActor = true; break; }
        if (!ownActor) return false;
        foreach (VehicleState vehicle in vehicles) if (ped.IsInVehicle(OwnVehicle(vehicle))) return true;
        return false;
    }
    // Para wrappers: nao iniciar caminhada/ClearAll apenas porque IsInVehicle ficou false.
    // Exige a conclusao registrada com tarefa, continuidade e pes no chao.
    public bool HasCompletedNormalExit(GTA.Ped ped)
    {
        if (!IsReady || System.Object.ReferenceEquals(ped, null) || ped.IsInVehicle()) return false;
        foreach (Operation operation in operations)
            if (operation.Spec.Tipo == "sair" && operation.Done && StillOwn(operation.Actor) &&
                operation.Actor.Handle == ped.Handle && operation.Actor.Address == ped.MemoryAddress)
                return true;
        return false;
    }
    // Diagnostico passivo: Main chama apos cada estagio, apenas nos dois primeiros Ticks apos envio.
    public void LogNormalExitCheckpoint(string stage, int elapsedMs)
    {
        if (!IsReady || !running || stage == null || stage.Length > 48) return;
        foreach (Operation operation in operations)
        {
            if (operation.Spec.Tipo != "sair" || !operation.Started || operation.Done ||
                !operation.ExitNativeSent || elapsedMs <= operation.ExitIssuedAt || !StillOwn(operation.Actor)) continue;
            if (elapsedMs != operation.ExitCheckpointElapsed)
            {
                if (operation.ExitCheckpointFrames >= 2) continue;
                operation.ExitCheckpointElapsed = elapsedMs; operation.ExitCheckpointFrames++;
            }
            try
            {
                GTA.ScriptTaskNameHash name; GTA.ScriptTaskStatus status;
                operation.Actor.Ped.GetCurrentScriptTaskNameHashAndStatus(out name, out status);
                Log("VEICULO_SAIDA_CHECKPOINT", "Stage=" + stage + "; Frame=" + operation.ExitCheckpointFrames +
                    "; TempoMs=" + elapsedMs + "; Ator=" + operation.Actor.Code + "; Handle=" + operation.Actor.Handle +
                    "; ScriptTask=" + name + "; ScriptTaskStatus=" + status +
                    "; InOwnVehicle=" + operation.Actor.Ped.IsInVehicle(operation.Vehicle.Vehicle));
            }
            catch (System.Exception ex) { Log("VEICULO_DIAGNOSTICO_ERRO", "Checkpoint=" + stage + "; " + ex.Message); }
        }
    }

    public GTA.Entity[] OwnedEntities
    {
        get
        {
            var values = new System.Collections.Generic.List<GTA.Entity>();
            foreach (VehicleState vehicle in vehicles)
                if (!System.Object.ReferenceEquals(vehicle.Vehicle, null) && vehicle.Vehicle.Exists()) values.Add(OwnVehicle(vehicle));
            foreach (ActorState actor in actors.Values) if (actor.Extra && StillOwn(actor)) values.Add(actor.Ped);
            return values.ToArray();
        }
    }
    public int[] OwnedHandles
    { get { var values = new System.Collections.Generic.List<int>(); foreach (GTA.Entity entity in OwnedEntities) values.Add(entity.Handle); return values.ToArray(); } }

    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> roster,
                        System.Collections.Generic.IEnumerable<VehicleSpec> specs,
                        GTA.Math.Vector3 realAnchor, GTA.Math.Vector3 actualRight, GTA.Math.Vector3 actualForward)
    {
        Active();
        if (prepared || resources != null || actors.Count != 0) throw Error("limpe o preparo anterior antes de montar veículos");
        if (roster == null || specs == null) throw Error("elenco e veículos precisam ser fornecidos");
        Vector(realAnchor); Vector(actualRight); Vector(actualForward);
        anchor = realAnchor; right = Unit(actualRight); forward = Unit(actualForward);
        if (System.Math.Abs(Dot(right, forward)) > 0.01f) throw Error("os eixos relativos precisam ser perpendiculares");
        resources = new ObjetosDiretor(log); prepared = true;
        try
        {
            var codes = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var seatedActors = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (VehicleSpec input in specs)
            {
                if (vehicles.Count >= 2) throw Error("limite de dois veículos por tomada; montagens usam subtomadas");
                if (input == null || !Code(input.Codigo) || !codes.Add(input.Codigo) || !AllowedModel(input.Tipo, input.Modelo))
                    throw Error("código repetido ou modelo/tipo terrestre sem origem conferida");
                Vector(input.PosicaoRelativa);
                if (!Finite(input.HeadingRelativo) || Horizontal(input.PosicaoRelativa) > 150f || System.Math.Abs(input.PosicaoRelativa.Z) > 60f)
                    throw Error("posição/direção relativa do veículo fora dos limites");
                GTA.Model model = new GTA.Model(input.Modelo);
                if (!model.IsInCdImage || !model.IsVehicle || (input.Tipo == "MOTO" ? !model.IsBike : !model.IsCar))
                    throw Error("modelo terrestre indisponível ou de tipo incorreto: " + input.Modelo);
                var vehicle = new VehicleState();
                vehicle.Spec = new VehicleSpec { Codigo = input.Codigo, Tipo = input.Tipo, Modelo = input.Modelo,
                    PosicaoRelativa = input.PosicaoRelativa, HeadingRelativo = input.HeadingRelativo, MotoristaExtra = input.MotoristaExtra,
                    TrajetoDireto = input.TrajetoDireto };
                vehicle.Spawn = Resolve(input.PosicaoRelativa);
                vehicle.Heading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, forward.X, forward.Y) + input.HeadingRelativo;
                vehicles.Add(vehicle); samples.Add(new GroundSample { Point = vehicle.Spawn, Vehicle = vehicle });
                if (input.ActorInitialSeats == null || input.Actions == null) throw Error("assentos e ações não podem ser nulos");
                var seats = new System.Collections.Generic.HashSet<int>();
                foreach (var entry in input.ActorInitialSeats)
                {
                    if (!seats.Add(entry.Value) || !seatedActors.Add(entry.Key) || entry.Value < -1 || entry.Value > 15 ||
                        (input.MotoristaExtra && entry.Value == -1)) throw Error("ator/assento inicial duplicado ou inválido");
                    ActorState seatedActor = RequireRosterActor(roster, entry.Key);
                    if (input.Tipo == "MOTO") seatedActor.AvoidAutoHelmet = true;
                    vehicle.Spec.ActorInitialSeats.Add(entry.Key, entry.Value);
                }
                if (input.MotoristaExtra)
                {
                    string extraCode = "MOTORISTA_" + input.Codigo;
                    if (actors.ContainsKey(extraCode) || roster.ContainsKey(extraCode)) throw Error("código de motorista extra conflita com o elenco");
                    vehicle.Extra = new ActorState { Code = extraCode, Extra = true, AvoidAutoHelmet = input.Tipo == "MOTO" };
                    actors.Add(extraCode, vehicle.Extra);
                    vehicle.Spec.ActorInitialSeats.Add(extraCode, -1);
                }
                var orderedActions = new System.Collections.Generic.List<VehicleAction>(input.Actions);
                orderedActions.Sort(delegate(VehicleAction a, VehicleAction b) {
                    return (a == null ? int.MinValue : a.InicioMs).CompareTo(b == null ? int.MinValue : b.InicioMs); });
                GTA.Math.Vector3 before = vehicle.Spawn;
                foreach (VehicleAction a in orderedActions)
                {
                    if (operations.Count >= 32 || a == null || (a.Tipo != "entrar" && a.Tipo != "sair" && a.Tipo != "dirigir") ||
                        a.InicioMs < 0 || a.DuracaoMs < 250 || a.DuracaoMs > 60000 || a.InicioMs > 180000 - a.DuracaoMs ||
                        a.Assento < -1 || a.Assento > 15 || !Finite(a.Velocidade) || a.Velocidade < 1f || a.Velocidade > 20f ||
                        !Finite(a.RaioChegada) || a.RaioChegada < 0.5f || a.RaioChegada > 3f ||
                        (a.RaioParada.HasValue && (!Finite(a.RaioParada.Value) || a.RaioParada.Value < 0.5f || a.RaioParada.Value > a.RaioChegada)) || a.RotaRelativa == null)
                        throw Error("ação/tempo/velocidade/raio inválidos");
                    string actorCode = a.Ator;
                    if (string.IsNullOrEmpty(actorCode) && vehicle.Extra != null) actorCode = vehicle.Extra.Code;
                    if (vehicle.Extra == null || actorCode != vehicle.Extra.Code) RequireRosterActor(roster, actorCode);
                    ActorState actor = actors[actorCode];
                    if (input.Tipo == "MOTO") actor.AvoidAutoHelmet = true;
                    var op = new Operation { Vehicle = vehicle, Actor = actor,
                        Spec = new VehicleAction { Tipo = a.Tipo, Ator = actorCode, InicioMs = a.InicioMs, DuracaoMs = a.DuracaoMs,
                            Assento = a.Assento, Velocidade = a.Velocidade, RaioChegada = a.RaioChegada, RaioParada = a.RaioParada } };
                    if (a.Tipo == "dirigir" && (a.RotaRelativa.Count < 1 || a.RotaRelativa.Count > 16)) throw Error("dirigir exige1–16 waypoints relativos reais");
                    if (a.Tipo != "dirigir" && a.RotaRelativa.Count != 0) throw Error("entrar/sair não aceita rota do veículo");
                    foreach (GTA.Math.Vector3 offset in a.RotaRelativa)
                    {
                        Vector(offset);
                        if (Horizontal(offset) > 150f || System.Math.Abs(offset.Z) > 60f) throw Error("waypoint relativo fora da área de150m/altura medida60m");
                        // C1 keeps a private copy of the already validated relative route for its strict prepare contract.
                        if (sceneId == "EP15_C01" || sceneId == "EP15_C09" || sceneId == "EP03_C01" || sceneId == "EP12_C01") op.Spec.RotaRelativa.Add(offset);
                        GTA.Math.Vector3 point = Resolve(offset); int index = op.Route.Count; op.Route.Add(point);
                        float length = Horizontal(point - before);
                        if (length > 80f) throw Error("subdivida o trajeto em trechos medidos de até80m");
                        int steps = System.Math.Max(1, (int)System.Math.Ceiling(length / 10f));
                        for (int s = 1; s <= steps; s++)
                            samples.Add(new GroundSample { Point = before + (point - before) * ((float)s / steps),
                                Operation = s == steps ? op : null, RouteIndex = s == steps ? index : -1 });
                        before = point;
                    }
                    foreach (Operation old in operations)
                        if (Overlap(old.Spec, op.Spec) && (old.Actor == op.Actor ||
                            (old.Vehicle == op.Vehicle && (old.Spec.Tipo == "dirigir" || op.Spec.Tipo == "dirigir"))))
                            throw Error("ações sobrepostas no mesmo ator ou direção durante entrada/saída");
                    operations.Add(op); totalDuration = System.Math.Max(totalDuration, a.InicioMs + a.DuracaoMs);
                }
                resources.RequestVehicle(input.Codigo, input.Modelo, vehicle.Spawn, vehicle.Heading, true, !LayerBoundCoach(vehicle));
            }
            if (vehicles.Count == 0 || samples.Count > 256) throw Error("forneça1–2 veículos e no máximo256 amostras de chão");
            operations.Sort(delegate(Operation a, Operation b) { return a.Spec.InicioMs.CompareTo(b.Spec.InicioMs); });
            foreach (VehicleState vehicle in vehicles) if (vehicle.Extra != null)
            {
                driverModel = new GTA.Model("s_m_m_gentransport"); // PedList.xml instalado, linha927.
                if (!driverModel.IsInCdImage || !driverModel.IsPed) throw Error("motorista extra não disponível");
                driverWasLoaded = driverModel.IsLoaded; driverModel.Request(); driverRequested = true; break;
            }
            loading = System.Diagnostics.Stopwatch.StartNew(); sampleIndex = 0; ready = false; failed = false;
            Log("VEICULOS_PREPARANDO", "Veiculos=" + vehicles.Count + "; Acoes=" + operations.Count + "; Estado=não testado visualmente");
        }
        catch { failed = true; throw; }
    }

    public void PollLoading()
    {
        Active(); if (!prepared || failed) throw Error("não existe preparação válida"); if (ready) return;
        try
        {
            if (!CaronaExitProofBudgetActive() && loading.ElapsedMilliseconds >= 5000) throw Error("modelos/chão/colisão não disponíveis em cinco segundos" + CoachWaitEvidence());
            resources.Update();
            for (int budget = 0; budget < 8 && sampleIndex < samples.Count; budget++)
            {
                GroundSample sample = samples[sampleIndex]; GTA.Math.Vector3 point = sample.Point;
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
                // A consulta retorna o chão mais alto abaixo da sonda; não subir até um viaduto.
                GTA.Math.Vector3 probe = point + new GTA.Math.Vector3(0f, 0f, sample.Vehicle != null && LayerBoundCoach(sample.Vehicle) ? 0.25f : 2f);
                float ground;
                if (!GTA.World.GetGroundHeight(probe, out ground, GTA.GetGroundHeightMode.Normal)) return;
                float water;
                bool hasWater = TryWater(point, out water);
                string evidence = "Amostra=" + (sampleIndex + 1) + "/" + samples.Count + "; Point=" + point + "; Probe=" + probe +
                    "; Ground=" + ground.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; DeltaZ=" + System.Math.Abs(ground - point.Z).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; Water=" + (hasWater ? water.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "nao_obtida");
                if (System.Math.Abs(ground - point.Z) > 8f || (hasWater && water > ground + 0.1f))
                    throw Error("rota relativa não acompanha chão real seco; capture subpontos do percurso; " + evidence);
                Log("VEICULOS_CHAO_AMOSTRA", evidence);
                if (sample.Vehicle != null && LayerBoundCoach(sample.Vehicle))
                { sample.Vehicle.MeasuredGround = ground; sample.Vehicle.GroundMeasured = true; }
                if (sample.C9NativeGoalEndpoint)
                {
                    sample.Operation.C9NativeGoals[sample.RouteIndex] = new GTA.Math.Vector3(point.X, point.Y, ground + 0.1f);
                    sample.Operation.C9NativeGoalsGroundMeasured[sample.RouteIndex] = true;
                }
                else if (sample.CaronaNativeGoalEndpoint)
                {
                    sample.Operation.CaronaNativeGoal = new GTA.Math.Vector3(point.X, point.Y, ground + 0.1f);
                    sample.Operation.CaronaNativeGoalGroundMeasured = true;
                }
                else if (sample.C1NativeGoalEndpoint)
                {
                    sample.Operation.C1NativeGoal = new GTA.Math.Vector3(point.X, point.Y, ground + 0.1f);
                    sample.Operation.C1NativeGoalGroundMeasured = true;
                }
                else if (sample.C3NativeGoalEndpoint)
                {
                    sample.Operation.C3NativeGoal = new GTA.Math.Vector3(point.X, point.Y, ground + 0.1f);
                    sample.Operation.C3NativeGoalGroundMeasured = true;
                }
                else if (sample.Operation != null) sample.Operation.Route[sample.RouteIndex] = new GTA.Math.Vector3(point.X, point.Y, ground + 0.1f);
                sampleIndex++;
            }
            if (!resources.IsReady || sampleIndex < samples.Count || (driverRequested && !driverModel.IsLoaded)) return;
            foreach (VehicleState vehicle in vehicles)
            {
                vehicle.Vehicle = resources.GetVehicle(vehicle.Spec.Codigo);
                if (LayerBoundCoach(vehicle) && !PrepareCoachLayer(vehicle)) return;
                foreach (var seat in vehicle.Spec.ActorInitialSeats) CheckSeat(vehicle.Vehicle, seat.Value);
                if (vehicle.Extra != null && vehicle.Extra.Handle != 0 && !StillOwn(vehicle.Extra))
                    throw Error("motorista registrado perdeu identidade; não substituir referências nem recriar");
                if (vehicle.Extra != null && vehicle.Extra.Handle == 0)
                {
                    GTA.Math.Vector3 driverPosition = vehicle.Vehicle.Position + right * 3f;
                    float driverGround;
                    GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, driverPosition.X, driverPosition.Y, driverPosition.Z);
                    if (!GTA.World.GetGroundHeight(driverPosition + new GTA.Math.Vector3(0f, 0f, 5f), out driverGround, GTA.GetGroundHeightMode.Normal)) return;
                    driverPosition.Z = driverGround + 1f;
                    GTA.Ped created = GTA.Ped.Create(driverModel, driverPosition, vehicle.Heading);
                    if (System.Object.ReferenceEquals(created, null) || !created.Exists()) throw Error("não foi possível criar motorista próprio");
                    vehicle.Extra.Ped = created; vehicle.Extra.Handle = created.Handle; vehicle.Extra.Address = created.MemoryAddress; vehicle.Extra.Model = created.Model.Hash;
                    vehicle.Extra.AutoHelmet = created.GetConfigFlag(GTA.PedConfigFlagToggles.DisableAutoEquipHelmetsInBikes);
                    created.IsPersistent = true; created.IsInvincible = true; created.IsPositionFrozen = true; created.BlockPermanentEvents = true;
                    return; // Uma criação de ped por Tick.
                }
            }
            foreach (Operation operation in operations) if (operation.Spec.Tipo == "entrar") CheckSeat(operation.Vehicle.Vehicle, operation.Spec.Assento);
            foreach (ActorState actor in actors.Values) RequireActor(actor);
            if (!PrepareC3NativeCenterGoals()) return;
            if (!PrepareC1NativeCenterGoal()) return;
            if (!PrepareCaronaNativeCenterGoals()) return;
            if (!PrepareC9NativeCenterGoals()) return;
            if (!PrepareC12NativeCenterGoal()) return;
            if (!PrepareCaronaFirstExitLane()) return;
            if (!PrepareCaronaStartingClearance()) return;
            ready = true; Log("VEICULOS_RECURSOS_PRONTOS", "Modelos, assentos, chão e colisão conferidos; rotas ainda não testadas visualmente");
        }
        catch { failed = true; throw; }
    }

    public bool Reset()
    {
        RequireReady();
        if (resetWaitingSeats) return CompleteInitialSeats();
        if (resetSettlingCoach)
        {
            if (coachResetLoading.ElapsedMilliseconds >= 5000) throw Error("coach nao assentou fisicamente em5s no reset antescontagem" + CoachWaitEvidence());
            foreach (VehicleState state in vehicles)
                if (LayerBoundCoach(state) && !PrepareCoachLayer(state)) return false;
            resetSettlingCoach = false;
            return SeatInitialActors();
        }
        running = false; resetDone = false;
        if (!ReleaseActorsForReset()) return false;
        foreach (ActorState actor in actors.Values)
        {
            RequireActor(actor);
            if (actor.Ped.IsInVehicle()) throw Error("ator entrou em veículo alheio; não alterar posição");
            if (!actor.MarkSaved)
            { actor.Position = actor.Ped.Position; actor.Heading = actor.Ped.Heading; actor.MarkSaved = true; }
            actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
            if (!actor.Extra) { actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading; }
            actor.Ped.IsInvincible = true; actor.Ped.BlockPermanentEvents = true; actor.Ped.IsPositionFrozen = false;
            if (actor.AvoidAutoHelmet) actor.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DisableAutoEquipHelmetsInBikes, true);
        }
        resources.Reset();
        caronaFirstExitLane = null; // Outside action; Ready had consumed all own requests. Reset needs fresh road proof.
        bool settleCoach = false;
        foreach (VehicleState state in vehicles)
        {
            if (!LayerBoundCoach(state)) continue;
            GTA.Vehicle entity = OwnVehicle(state);
            entity.PositionNoOffset = state.Spawn; // Somente veiculo proprio fora da filmagem, ultimo assentamento real.
            entity.Velocity = GTA.Math.Vector3.Zero; entity.HasGravity = true;
            entity.IsHandbrakeForcedOn = true; entity.IsPositionFrozen = false;
            state.CoachLayerApplied = true; state.CoachLayerSettled = false;
            state.CoachLayerAppliedAt = GTA.Game.GameTime; state.CoachStableAt = -1; state.CoachNextLog = 0;
            state.CoachNextSupportLog = 0; state.CoachNextColliderLog = 0; state.CoachPendingEvidence = null;
            state.CoachLastPosition = entity.Position;
            settleCoach = true;
        }
        if (settleCoach)
        {
            resetSettlingCoach = true; coachResetLoading = System.Diagnostics.Stopwatch.StartNew();
            return false;
        }
        return SeatInitialActors();
    }

    private bool SeatInitialActors()
    {
        resetWaitingSeats = true;
        foreach (VehicleState vehicle in vehicles)
        {
            GTA.Vehicle entity = OwnVehicle(vehicle);
            entity.IsEngineRunning = true;
            entity.IsHandbrakeForcedOn = false;
            foreach (var seat in vehicle.Spec.ActorInitialSeats)
            {
                ActorState actor = actors[seat.Key];
                if (!entity.IsSeatFree((GTA.VehicleSeat)seat.Value)) throw Error("assento ocupado antes da montagem");
                actor.Ped.SetIntoVehicle(entity, (GTA.VehicleSeat)seat.Value); // Somente fora da filmagem.
            }
        }
        return CompleteInitialSeats();
    }

    private bool CompleteInitialSeats()
    {
        bool pending = false;
        foreach (VehicleState vehicle in vehicles)
        {
            CheckOccupants(vehicle); GTA.Vehicle entity = OwnVehicle(vehicle);
            foreach (var seat in vehicle.Spec.ActorInitialSeats)
            {
                ActorState actor = actors[seat.Key]; RequireActor(actor);
                GTA.Ped occupant = entity.GetPedOnSeat((GTA.VehicleSeat)seat.Value);
                if (!System.Object.ReferenceEquals(occupant, null) && occupant.Exists() &&
                    (occupant.Handle != actor.Handle || occupant.MemoryAddress != actor.Address))
                    throw Error("outro ator ocupou assento inicial; não remover nem reassentar ninguém");
                if (!InSeat(actor, entity, seat.Value)) pending = true;
            }
        }
        if (pending) return false;
        if (!PrepareCaronaFirstExitLane()) return false;
        if (!PrepareCaronaStartingClearance()) return false;
        resetWaitingSeats = false;
        foreach (Operation operation in operations)
        {
            operation.Started = false; operation.Done = false; operation.Point = 0; operation.NextLog = 0;
            operation.LuxuryOwnDriverArrivalHold = false;
            operation.CaronaOwnDriverArrivalHold = false;
            operation.CaronaPassByObserved = operation.CaronaPassByPreviousSample = false;
            operation.CaronaPassByPreviousProjection = 0f;
            operation.ExitRay = 0; operation.ExitProbeState = "not_requested"; operation.ExitHitEntity = 0;
            operation.ExitNativeSent = operation.ExitTaskObserved = operation.ExitMovementObserved = operation.ExitNextTickLogged = false;
            operation.ExitCheckpointElapsed = -1; operation.ExitCheckpointFrames = 0;
            operation.ExitIssuedAt = operation.ExitLastSampleAt = operation.ExitSamples = operation.ExitInitialHealth = 0;
            operation.ExitFirstOutsideAt = -1; operation.ExitPeakBoneStep = operation.ExitPeakBoneSpeed = 0f;
            operation.ExitCollisionRestored = false;
        }
        lastElapsed = -1; finished = false; resetDone = true; LogLuxuryDriverEntryPrepared(); return true;
    }

    public void Start()
    { RequireReady(); if (!resetDone || running) throw Error("restaure assentos/marcas antes da contagem e Start"); running = true; }

    public void Update(int elapsedMs)
    {
        RequireReady(); if (finished) return;
        if (!running || elapsedMs < 0 || elapsedMs < lastElapsed) throw Error("a ação exige tempo crescente desde Start");
        try
        {
            lastElapsed = elapsedMs;
            foreach (ActorState actor in actors.Values) RequireActor(actor);
            foreach (VehicleState vehicle in vehicles) CheckOccupants(vehicle);
            foreach (Operation operation in operations) if (IsC12NativeCenterGoal(operation) && operation.LuxuryOwnDriverArrivalHold)
                RequireC12OwnDriverArrivalHold(operation, OwnVehicle(operation.Vehicle));
            foreach (Operation operation in operations) if (operation.CaronaOwnDriverArrivalHold)
                RequireCaronaOwnDriverArrivalHold(operation, OwnVehicle(operation.Vehicle));
            foreach (Operation operation in operations) if (operation.CaronaPassByObserved)
                RequireCaronaPassByObserved(operation, OwnVehicle(operation.Vehicle));
            foreach (Operation operation in operations) if (operation.Started && !operation.Done) Step(operation, elapsedMs);
            foreach (Operation operation in operations)
                if (!operation.Started && elapsedMs >= operation.Spec.InicioMs)
                {
                    if (elapsedMs >= operation.Spec.InicioMs + operation.Spec.DuracaoMs) throw Error("frame perdido ultrapassou uma ação inteira; repetir tomada");
                    Begin(operation); Step(operation, elapsedMs);
                }
            bool done = true; foreach (Operation operation in operations) if (!operation.Done) done = false;
            if (done && elapsedMs >= totalDuration) { Finish(); Log("VEICULOS_TOMADA_FINALIZADA", "Rotas/entradas/saídas concluídas fisicamente; TempoMs=" + elapsedMs); }
        }
        catch { failed = true; running = false; throw; }
    }

    private void Begin(Operation operation)
    {
        GTA.Vehicle vehicle = OwnVehicle(operation.Vehicle); GTA.Ped ped = operation.Actor.Ped;
        LogOperationState("VEICULO_ACAO_INICIO_ESTADO", operation, vehicle, lastElapsed);
        operation.Started = true;
        if (operation.Spec.Tipo == "entrar")
        {
            if (ped.IsInVehicle() || vehicle.Speed > 0.5f || !vehicle.IsSeatFree((GTA.VehicleSeat)operation.Spec.Assento))
                throw Error("entrada exige ator a pé, veículo parado e assento livre");
            ped.Task.EnterVehicle(vehicle, (GTA.VehicleSeat)operation.Spec.Assento, -1, new GTA.PedMoveBlendRatio(1f),
                GTA.EnterVehicleFlags.DontJackAnyone | GTA.EnterVehicleFlags.BlockSeatShuffling);
        }
        else if (operation.Spec.Tipo == "sair")
        {
            if (!ped.IsInVehicle(vehicle) || vehicle.Speed > 0.5f) throw Error("saída exige ator no veículo próprio já parado");
            // A física das portas precisa do veículo ativo; freio mantém o apoio próprio parado.
            vehicle.Velocity = GTA.Math.Vector3.Zero;
            vehicle.IsHandbrakeForcedOn = true;
            vehicle.IsPositionFrozen = false;
            StartExitProbe(operation, vehicle);
            // Aguardar a capsule de saida real antes de pedir a tarefa normal.
            // O prazo original continua contando, sem ampliar a cena.
        }
        else
        {
            if (!InSeat(operation.Actor, vehicle, -1)) throw Error("dirigir exige motorista próprio no assento do condutor");
            vehicle.IsHandbrakeForcedOn = false;
            vehicle.IsPositionFrozen = false; DrivePoint(operation);
        }
        Log("VEICULO_ACAO_INICIADA", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; Ator=" + operation.Actor.Code + "; Tipo=" + operation.Spec.Tipo);
    }

    private void Step(Operation operation, int elapsed)
    {
        GTA.Vehicle vehicle = OwnVehicle(operation.Vehicle); bool reached = false;
        PollExitProbe(operation, elapsed);
        if ((long)elapsed >= operation.NextLog)
        {
            operation.NextLog = (long)elapsed + 500;
            LogOperationState("VEICULO_ACAO_ESTADO", operation, vehicle, elapsed);
        }
        if (IsCaronaPhysicalPassBy(operation))
        {
            ObserveCaronaPhysicalPassBy(operation, vehicle, elapsed);
            if (operation.CaronaPassByObserved)
            {
                if (elapsed < operation.Spec.InicioMs + operation.Spec.DuracaoMs) return;
                reached = true; // Both literal cars pass; exact original7s/8.5s deadlines remain, no precise stop.
            }
        }
        else if (operation.CaronaOwnDriverArrivalHold)
        {
            RequireCaronaOwnDriverArrivalHold(operation, vehicle);
            if (elapsed < operation.Spec.InicioMs + operation.Spec.DuracaoMs) return;
            reached = true; // Same arrival deadlines7s/13.5s; own seat held to full scene end in Update.
        }
        else if (operation.LuxuryOwnDriverArrivalHold)
        {
            RequireLuxuryOwnDriverArrivalHold(operation, vehicle);
            if (elapsed < operation.Spec.InicioMs + operation.Spec.DuracaoMs) return;
            reached = true; // Same7s/19s window; own driver verified on every intervening Tick.
        }
        else if (operation.Spec.Tipo == "entrar") reached = InSeat(operation.Actor, vehicle, operation.Spec.Assento);
        else if (operation.Spec.Tipo == "sair") reached = ObserveNormalExit(operation, vehicle, elapsed);
        else
        {
            if (!InSeat(operation.Actor, vehicle, -1)) throw Error("motorista saiu do assento durante direção");
            GTA.Math.Vector3 target = operation.Route[operation.Point];
            if (Horizontal(vehicle.Position - target) <= operation.Spec.RaioChegada && System.Math.Abs(vehicle.Position.Z - target.Z) <= 3f &&
                (operation.Point + 1 < operation.Route.Count || vehicle.Speed <= 1.5f))
            {
                operation.Point++;
                if (operation.Point < operation.Route.Count) DrivePoint(operation);
                else if (IsCaronaNativeCenterGoal(operation))
                {
                    operation.CaronaOwnDriverArrivalHold = true;
                    vehicle.Velocity = GTA.Math.Vector3.Zero; vehicle.IsPositionFrozen = true;
                    RequireCaronaOwnDriverArrivalHold(operation, vehicle);
                    Log("VEICULO_CARONA_CHEGADA_ASSENTO_MANTIDO", "Veiculo=" + operation.Vehicle.Spec.Codigo +
                        "; TempoMs=" + elapsed + "; HoldUntilMs=13500; ArrivalDeadlineMs=" +
                        (operation.Spec.InicioMs + operation.Spec.DuracaoMs) + "; ClearPedTask=False; NoWarpOrReseat=True");
                    if (elapsed < operation.Spec.InicioMs + operation.Spec.DuracaoMs) return;
                    reached = true;
                }
                else if (LuxuryDriverArrivalHoldScope(operation))
                {
                    // ClearAllImmediately173 removed the seated own driver at arrival.
                    // Stop only the own vehicle; keep the physical driver task/seat intact.
                    operation.LuxuryOwnDriverArrivalHold = true;
                    vehicle.Velocity = GTA.Math.Vector3.Zero; vehicle.IsPositionFrozen = true;
                    RequireLuxuryOwnDriverArrivalHold(operation, vehicle);
                    Log("VEICULO_LUXO_CHEGADA_ASSENTO_MANTIDO", "Scene=" + sceneId + "; Veiculo=" + operation.Vehicle.Spec.Codigo +
                        "; TempoMs=" + elapsed + "; DriverSeat=-1; DriverOwnIdentity=True; ClearPedTask=False;" +
                        "; VehicleVelocityZero=True; VehicleFrozen=True; HoldUntilMs=" + totalDuration + "; NoWarpOrReseat=True");
                    if (elapsed < operation.Spec.InicioMs + operation.Spec.DuracaoMs) return;
                    reached = true;
                }
                else { reached = true; operation.Actor.Ped.Task.ClearAllImmediately(); vehicle.Velocity = GTA.Math.Vector3.Zero; vehicle.IsPositionFrozen = true; }
            }
        }
        if (reached)
        { operation.Done = true; Log("VEICULO_ACAO_CONCLUIDA", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; Tipo=" + operation.Spec.Tipo + "; TempoMs=" + elapsed); return; }
        if (elapsed >= operation.Spec.InicioMs + operation.Spec.DuracaoMs)
        {
            if (operation.Spec.Tipo == "sair") LogExitConfiguration("TIMEOUT", operation, elapsed);
            LogOperationState("VEICULO_ACAO_TIMEOUT", operation, vehicle, elapsed);
            throw Error("não concluiu " + operation.Spec.Tipo + " de " + operation.Vehicle.Spec.Codigo + " em " + operation.Spec.DuracaoMs +
                "ms; trânsito/obstáculo/porta/rota podem exigir mais tempo. Não haverá teleporte");
        }
    }

    // Prova corporal entre frames. Somente getters alem da tarefa normal enviada uma vez.
    private bool ObserveNormalExit(Operation operation, GTA.Vehicle vehicle, int elapsed)
    {
        GTA.Ped ped = operation.Actor.Ped;
        if (!operation.ExitNativeSent)
        {
            if (operation.ExitProbeState == "pending") return false;
            if (operation.ExitProbeState != "complete_clear")
                throw Error("a sonda real de saida nao confirmou passagem livre: " + operation.ExitProbeState);
            if (!ped.IsInVehicle(vehicle) || !InSeat(operation.Actor, vehicle, operation.Spec.Assento) ||
                vehicle.Speed > 0.5f || ped.IsInjured)
                throw Error("saida normal exige ator proprio nao ferido no mesmo assento/veiculo parado");
            PrepareAuthorBusExit(operation, vehicle, elapsed);
            operation.ExitInitialHead = ExitBone(ped, GTA.Bone.SkelHead);
            operation.ExitInitialPelvis = ExitBone(ped, GTA.Bone.SkelPelvis);
            operation.ExitLastHead = operation.ExitInitialHead;
            operation.ExitLastPelvis = operation.ExitInitialPelvis;
            operation.ExitInitialHealth = ped.Health;
            operation.ExitIssuedAt = operation.ExitLastSampleAt = elapsed;
            LogExitConfiguration("ANTES_NATIVE", operation, elapsed);
            LogExitBones("VEICULO_SAIDA_ANTES_NATIVE", operation, vehicle, elapsed,
                operation.ExitInitialHead, operation.ExitInitialPelvis);
            // Fonte autor: re_bus_tours.c3446 e assassin_bus.c12731/12742, commit7a800e3.
            // Native especifico com flags0; nenhum overload ANY que possa incluir WarpIfDoorIsBlocked.
            // Movimento/tempo/tarefa/contato permanecem obrigatorios; um salto nao prova saida normal.
            // Autor assassin_bus.c11203/21781: capacidade de sair do veiculo do proprio ped.
            GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 3, true);
            Log("VEICULO_SAIDA_CAPACIDADE_PEDIDA", "Ator=" + operation.Actor.Code + "; AtorHandle=" + ped.Handle +
                "; VeiculoHandle=" + vehicle.Handle + "; CanLeaveVehicleAtributo=3; ValorPedido=True; GetterDisponivel=False;" +
                "; Origem=assassin_bus.c11203,21781@7a800e34172bc50f13b07884ca55942028e8c8d0; FuncionalidadeNative=pendente");
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_LEAVE_VEHICLE, ped.Handle, vehicle.Handle, 0);
            operation.ExitNativeSent = true;
            Log("VEICULO_SAIDA_NATIVE_ENVIADO", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; Ator=" + operation.Actor.Code +
                "; AtorHandle=" + ped.Handle + "; VeiculoHandle=" + vehicle.Handle +
                "; Native=TASK_LEAVE_VEHICLE; Flags=0; Origem=autor_specific_vehicle" +
                "; BitsWarp16e512=False; Probe=complete_clear; PrazoOriginalMs=" + operation.Spec.DuracaoMs +
                "; FuncionalidadeNative=pendente;");
            // O getter imediato registra apenas o retorno/queue; nao confirma execucao.
            LogAuthorBusState("DEPOIS_NATIVE_MESMO_TICK", operation, vehicle, elapsed);
            LogExitBones("VEICULO_SAIDA_DEPOIS_NATIVE_MESMO_TICK", operation, vehicle, elapsed,
                ExitBone(ped, GTA.Bone.SkelHead), ExitBone(ped, GTA.Bone.SkelPelvis));
            return false;
        }
        if (elapsed <= operation.ExitLastSampleAt) return false;
        GTA.Math.Vector3 head = ExitBone(ped, GTA.Bone.SkelHead), pelvis = ExitBone(ped, GTA.Bone.SkelPelvis);
        int sampleDelta = elapsed - operation.ExitLastSampleAt;
        float headStep = (head - operation.ExitLastHead).Length(), pelvisStep = (pelvis - operation.ExitLastPelvis).Length();
        float maxStep = System.Math.Max(headStep, pelvisStep);
        float seconds = sampleDelta / 1000f;
        operation.ExitPeakBoneStep = System.Math.Max(operation.ExitPeakBoneStep, maxStep);
        operation.ExitPeakBoneSpeed = System.Math.Max(operation.ExitPeakBoneSpeed, maxStep / seconds);
        // Intervalos grandes nao provam continuidade. Um deslocamento maior que
        // 1.25m em um frame curto (ou6m/s em ate500ms) rejeita possivel salto.
        float allowedStep = System.Math.Max(1.25f, 6f * seconds);
        if (sampleDelta > 500 || maxStep > allowedStep)
        {
            LogExitBones("VEICULO_SAIDA_CONTINUIDADE_FALHOU", operation, vehicle, elapsed, head, pelvis);
            throw Error("saida sem continuidade corporal verificavel: DeltaMs=" + sampleDelta +
                "; BoneStep=" + Number(maxStep) + "; Limite=" + Number(allowedStep));
        }
        operation.ExitLastSampleAt = elapsed;
        operation.ExitLastHead = head; operation.ExitLastPelvis = pelvis; operation.ExitSamples++;
        GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
        ped.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
        if (task == GTA.ScriptTaskNameHash.LeaveVehicle && status != GTA.ScriptTaskStatus.Vacant &&
            status != GTA.ScriptTaskStatus.Finished) operation.ExitTaskObserved = true;
        if ((head - operation.ExitInitialHead).Length() >= 0.20f &&
            (pelvis - operation.ExitInitialPelvis).Length() >= 0.20f) operation.ExitMovementObserved = true;
        if (!operation.ExitNextTickLogged)
        {
            operation.ExitNextTickLogged = true;
            LogExitConfiguration("PRIMEIRO_TICK_POSTERIOR", operation, elapsed);
            LogExitBones("VEICULO_SAIDA_PRIMEIRO_TICK_POSTERIOR", operation, vehicle, elapsed, head, pelvis);
        }
        if (ped.Health < operation.ExitInitialHealth || ped.IsInjured)
            throw Error("a saida alterou a saude do ator proprio; tomada interrompida");
        if (ped.IsInVehicle()) { operation.ExitFirstOutsideAt = -1; return false; }
        if (!operation.ExitTaskObserved || !operation.ExitMovementObserved || operation.ExitSamples < 2 ||
            elapsed - operation.ExitIssuedAt < 200)
        {
            LogExitBones("VEICULO_SAIDA_INSTANTANEA_REJEITADA", operation, vehicle, elapsed, head, pelvis);
            throw Error("deixou o veiculo sem tarefa/movimento corporal observados entre frames; nao aceitar warp como saida");
        }
        if (AuthorBusExit(operation))
        {
            // Sair do assento nao encerra a tarefa/animacao de saida. Nao cancela-la enquanto Performing.
            bool exiting = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_IS_TASK_ACTIVE, ped.Handle, 2);
            if (exiting || (task == GTA.ScriptTaskNameHash.LeaveVehicle &&
                status != GTA.ScriptTaskStatus.Vacant && status != GTA.ScriptTaskStatus.Finished))
            { operation.ExitFirstOutsideAt = -1; return false; }
            if (!operation.Actor.Collision) throw Error("o ator proprio nao tinha colisao ativa antes do embarque");
            if (!ped.IsCollisionEnabled)
            {
                if (operation.ExitCollisionRestored) throw Error("a colisao propria nao permaneceu ativa apos restauracao da saida");
                ped.IsCollisionEnabled = operation.Actor.Collision;
                operation.ExitCollisionRestored = true; operation.ExitFirstOutsideAt = -1;
                Log("VEICULO_SAIDA_COLISAO_RESTAURADA", "Ator=" + operation.Actor.Code + "; TempoMs=" + elapsed +
                    "; Original=" + operation.Actor.Collision + "; GetterDepois=" + ped.IsCollisionEnabled +
                    "; ForaVeiculo=true; TarefaSaidaTerminada=true; VerificarFisicaProximoTick=true");
                return false;
            }
            if (!ped.HasPhysics) { operation.ExitFirstOutsideAt = -1; return false; }
        }
        if (ped.IsInAir || ped.IsRagdoll) { operation.ExitFirstOutsideAt = -1; return false; }
        GTA.Math.Vector3 leftFoot = ExitBone(ped, GTA.Bone.SkelLeftFoot), rightFoot = ExitBone(ped, GTA.Bone.SkelRightFoot);
        float leftGround, rightGround;
        bool leftFound = GTA.World.GetGroundHeightExcludingProps(leftFoot + new GTA.Math.Vector3(0f, 0f, 0.5f),
            out leftGround, GTA.GetGroundHeightMode.Normal);
        bool rightFound = GTA.World.GetGroundHeightExcludingProps(rightFoot + new GTA.Math.Vector3(0f, 0f, 0.5f),
            out rightGround, GTA.GetGroundHeightMode.Normal);
        if (LayerBoundCoach(operation.Vehicle) && leftFound && rightFound && Finite(leftGround) && Finite(rightGround) &&
            (System.Math.Abs(leftGround - operation.Vehicle.MeasuredGround) > 0.35f ||
             System.Math.Abs(rightGround - operation.Vehicle.MeasuredGround) > 0.35f))
            throw Error("saida chegou a outra camada de chao: ExpectedGround=" + Number(operation.Vehicle.MeasuredGround) +
                "; LeftGround=" + Number(leftGround) + "; RightGround=" + Number(rightGround));
        bool feetSupported = leftFound && rightFound && Finite(leftGround) && Finite(rightGround) &&
            leftFoot.Z >= leftGround - 0.08f && leftFoot.Z <= leftGround + 0.35f &&
            rightFoot.Z >= rightGround - 0.08f && rightFoot.Z <= rightGround + 0.35f &&
            (System.Math.Abs(leftFoot.Z - leftGround) <= 0.15f || System.Math.Abs(rightFoot.Z - rightGround) <= 0.15f);
        if (!feetSupported) { operation.ExitFirstOutsideAt = -1; return false; }
        if (operation.ExitFirstOutsideAt < 0) operation.ExitFirstOutsideAt = elapsed;
        if (elapsed - operation.ExitFirstOutsideAt < 100) return false;
        LogExitBones("VEICULO_SAIDA_FISICA_CONFIRMADA", operation, vehicle, elapsed, head, pelvis);
        Log("VEICULO_SAIDA_PES_NO_CHAO", "Ator=" + operation.Actor.Code + "; TempoMs=" + elapsed +
            "; PeEsquerdo=" + leftFoot + "; GroundEsquerdo=" + Number(leftGround) +
            "; PeDireito=" + rightFoot + "; GroundDireito=" + Number(rightGround) +
            "; ForaVeiculoSustentadoMs=" + (elapsed - operation.ExitFirstOutsideAt) + "; SemReposicaoNaAcao=True;");
        return true;
    }

    // Somente o coach proprio do wrapper: PlaceOnGround foi observado mudando o andar.
    private static bool LayerBoundCoach(VehicleState vehicle)
    { return vehicle.Spec.Codigo == "ONIBUS_TEMPORADA" && vehicle.Spec.Tipo == "ONIBUS" && vehicle.Spec.Modelo == "coach"; }
    private bool PrepareCoachLayer(VehicleState state)
    {
        GTA.Vehicle vehicle = OwnVehicle(state);
        if (!state.GroundMeasured) return false;
        if (state.CoachLayerSettled)
        {
            if (!vehicle.IsPositionFrozen || !vehicle.IsCollisionEnabled ||
                (vehicle.Position - state.Spawn).Length() > 0.08f)
                throw Error("coach perdeu assentamento propio congelado antescontagem");
            return true;
        }
        vehicle.Model.RequestCollision();
        if (!vehicle.Model.IsCollisionLoaded || !vehicle.HasPhysics ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, vehicle.Handle)) return false;
        GTA.Math.Vector3 min, max; vehicle.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
        if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("bounds reais do coach invalidos");
        int now = GTA.Game.GameTime;
        if (!state.CoachLayerApplied)
        {
            // Apenas ponto inicial conservador acima do piso. BBox nao prova contato de rodas.
            GTA.Math.Vector3 point = state.Spawn; point.Z = state.MeasuredGround - min.Z;
            vehicle.PositionNoOffset = point; vehicle.Velocity = GTA.Math.Vector3.Zero;
            vehicle.IsCollisionEnabled = true; vehicle.HasGravity = true;
            vehicle.IsHandbrakeForcedOn = true; vehicle.IsPositionFrozen = false;
            state.CoachLayerApplied = true; state.CoachLayerAppliedAt = now;
            state.CoachLastPosition = vehicle.Position; state.CoachStableAt = -1; state.CoachNextLog = 0;
            state.CoachNextSupportLog = 0; state.CoachNextColliderLog = 0; state.CoachPendingEvidence = null;
            Log("VEICULO_CAMADA_PREPARO", "Codigo=" + state.Spec.Codigo + "; Ground=" + Number(state.MeasuredGround) +
                "; ModelMin=" + min + "; ModelMax=" + max + "; PositionRequested=" + point +
                "; PlaceOnGround=false; Frozen=false; Gravity=true; AguardaRodasReais=true; SomenteVeiculoProprioAntesContagem=true");
            KeepCoachCollider(state, "AJUSTE_INICIAL", true);
            return false;
        }
        if (now == state.CoachLayerAppliedAt) return false;
        if (!KeepCoachCollider(state, "AGUARDA_APOIO", false))
        {
            state.CoachStableAt = -1; state.CoachLastPosition = vehicle.Position;
            return false; // O mesmo limite5s de PollLoading/Reset continua obrigatorio.
        }
        GTA.Math.Vector3 position = vehicle.Position; Vector(position);
        bool allWheels = vehicle.IsOnAllWheels;
        if (now >= state.CoachNextLog)
        {
            state.CoachNextLog = now + 500;
            Log("VEICULO_CAMADA_ASSENTANDO", "Codigo=" + state.Spec.Codigo + "; Position=" + position +
                "; Velocity=" + vehicle.Velocity + "; Speed=" + Number(vehicle.Speed) + "; OnAllWheels=" + allWheels +
                "; Gravity=" + vehicle.HasGravity + "; Collision=" + vehicle.IsCollisionEnabled +
                "; Frozen=" + vehicle.IsPositionFrozen + "; Ground=" + Number(state.MeasuredGround));
        }
        if (!vehicle.HasGravity || !vehicle.IsCollisionEnabled || vehicle.IsPositionFrozen)
            throw Error("coach proprio perdeu gravidade/colisao livre no assentamento antescontagem");
        if (!allWheels) { state.CoachStableAt = -1; state.CoachLastPosition = position; return false; }
        GTA.VehicleWheel[] wheels = vehicle.Wheels.GetAllWheels();
        if (wheels.Length < 4 || wheels.Length > 6) throw Error("coach exige4..6 rodas reais no orcamento6, getter=" + wheels.Length);
        var evidence = new System.Text.StringBuilder();
        float radiusX = System.Math.Max(System.Math.Abs(min.X), System.Math.Abs(max.X));
        float radiusY = System.Math.Max(System.Math.Abs(min.Y), System.Math.Abs(max.Y));
        float radius = (float)System.Math.Sqrt(radiusX * radiusX + radiusY * radiusY);
        foreach (GTA.VehicleWheel wheel in wheels)
        {
            if (wheel.Vehicle.Handle != vehicle.Handle || wheel.Vehicle.MemoryAddress != vehicle.MemoryAddress ||
                wheel.MemoryAddress == System.IntPtr.Zero || !wheel.IsTouchingSurface)
            { state.CoachStableAt = -1; state.CoachLastPosition = position; return false; }
            GTA.Math.Vector3 contact = wheel.LastContactPosition, normal = wheel.HitNormal;
            Vector(contact); Vector(normal);
            if (Horizontal(contact - position) > radius) throw Error("getter contato da roda fora dos bounds fisicos do coach proprio");
            // Sonda fixa na camada marcada: nao aceitar topo de outro chassi como piso do coach.
            GTA.Math.Vector3 probe = new GTA.Math.Vector3(contact.X, contact.Y, state.MeasuredGround + 0.25f);
            float ground;
            if (!GTA.World.GetGroundHeight(probe, out ground, GTA.GetGroundHeightMode.Normal))
            { state.CoachStableAt = -1; state.CoachLastPosition = position; return false; }
            if (!Finite(ground)) throw Error("getter de chao da roda coach retornou valor nao finito");
            if (System.Math.Abs(ground - state.MeasuredGround) > 0.35f ||
                System.Math.Abs(contact.Z - ground) > 0.08f)
            {
                // LastContactPosition nao informa frescor: contato invalido nunca autoriza READY.
                // Esperar somente dentro dos5s originais, sem reposicionar ou congelar a cada Tick.
                state.CoachStableAt = -1; state.CoachLastPosition = position;
                state.CoachPendingEvidence = "Codigo=" + state.Spec.Codigo + "; Bone=" + wheel.BoneId +
                    "; Contact=" + contact + "; HitNormal=" + normal + "; Ground=" + Number(ground) +
                    "; ExpectedGround=" + Number(state.MeasuredGround) + "; Probe=" + probe +
                    "; Position=" + position + "; Velocity=" + vehicle.Velocity + "; Speed=" + Number(vehicle.Speed) +
                    "; HasPhysics=" + vehicle.HasPhysics + "; Gravity=" + vehicle.HasGravity +
                    "; Collision=" + vehicle.IsCollisionEnabled + "; Frozen=" + vehicle.IsPositionFrozen +
                    "; DesdeAjusteMs=" + unchecked(now - state.CoachLayerAppliedAt) +
                    "; ContactoAindaNaoValido=true; FrescorDoUltimoHitNaoDisponivel=true";
                if (now >= state.CoachNextSupportLog)
                {
                    state.CoachNextSupportLog = now + 500;
                    Log("VEICULO_APOIO_PENDENTE", state.CoachPendingEvidence);
                }
                return false;
            }
            evidence.Append("; Wheel=" + wheel.BoneId + "; Contact=" + contact + "; HitNormal=" + normal + "; Ground=" + Number(ground));
        }
        state.CoachPendingEvidence = null; // Todas as rodas passaram; estabilidade ainda e obrigatoria.
        if (vehicle.Speed > 0.1f || System.Math.Abs(position.Z - state.CoachLastPosition.Z) > 0.02f)
        { state.CoachStableAt = -1; state.CoachLastPosition = position; return false; }
        state.CoachLastPosition = position;
        if (state.CoachStableAt < 0) state.CoachStableAt = now;
        if (unchecked(now - state.CoachStableAt) < 100) return false;
        // Cache somente a posicao fisica medida apos gravidade/rodas/piso sustentados.
        vehicle.Velocity = GTA.Math.Vector3.Zero; vehicle.IsPositionFrozen = true;
        state.Spawn = vehicle.Position; state.CoachLayerSettled = true;
        GTA.Math.Vector3 bottom = vehicle.GetOffsetPosition(new GTA.Math.Vector3((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f, min.Z));
        Log("VEICULO_CAMADA_GETTERS", "Codigo=" + state.Spec.Codigo + "; Position=" + state.Spawn +
            "; BoundingBottomDiagnostic=" + bottom + "; Ground=" + Number(state.MeasuredGround) +
            "; OnAllWheels=true; ActualWheelCount=" + wheels.Length + "; Gravity=" + vehicle.HasGravity +
            "; StableMs=" + unchecked(now - state.CoachStableAt) + "; CachedSpawnPhysical=true" + evidence);
        return true;
    }

    private bool KeepCoachCollider(VehicleState state, string stage, bool forceLog)
    {
        if (!LayerBoundCoach(state)) throw Error("manter collider limitado ao coach proprio da temporada");
        GTA.Vehicle vehicle = OwnVehicle(state);
        bool hasPhysics = vehicle.HasPhysics, frozen = vehicle.IsPositionFrozen, staticBefore = vehicle.IsStatic;
        bool gravityBefore = vehicle.HasGravity, collisionBefore = vehicle.IsCollisionEnabled;
        int beforeGuardAt = GTA.Game.GameTime;
        bool invalidPhysics = !hasPhysics || frozen || !gravityBefore || !collisionBefore;
        string beforeEvidence = null;
        // Capturar cada predicado antes da mesma guarda, inclusive a falha no primeiro Tick.
        // GET_ENTITY_COLLISION_DISABLED abaixo e a inversao da mesma query do getter, sem query extra.
        if (forceLog || beforeGuardAt >= state.CoachNextColliderLog || invalidPhysics)
        {
            beforeEvidence = "Fase=" + stage + "; Codigo=" + state.Spec.Codigo + "; Handle=" + vehicle.Handle +
                "; Modelo=" + state.Spec.Modelo + "; ModelHash=" + vehicle.Model.Hash +
                "; MemoryAddress=0x" + vehicle.MemoryAddress.ToInt64().ToString("X", System.Globalization.CultureInfo.InvariantCulture) +
                "; HasPhysics=" + hasPhysics + "; Frozen=" + frozen + "; IsStatic=" + staticBefore +
                "; Gravity=" + gravityBefore + "; Collision=" + collisionBefore +
                "; DOES_ENTITY_HAVE_PHYSICS=" + hasPhysics + "; IS_ENTITY_STATIC=" + staticBefore +
                "; GET_ENTITY_COLLISION_DISABLED=" + (!collisionBefore) + "; GravityGetter=GTA.Entity.HasGravity" +
                "; Position=" + vehicle.Position + "; Velocity=" + vehicle.Velocity + "; Speed=" + Number(vehicle.Speed) +
                "; DesdeAjusteMs=" + unchecked(beforeGuardAt - state.CoachLayerAppliedAt) +
                "; AntesDeSetToRespondToPhysicsSystem=true; GuardaInvalida=" + invalidPhysics;
            Log("VEICULO_COLLIDER_ANTES_GUARDA", beforeEvidence);
        }
        if (!hasPhysics || frozen || !collisionBefore)
            throw Error("coach sem phInst/gravidade/colisao livre no assentamento; " + beforeEvidence);
        // API191: HasPhysics prova phInst; IsStatic=false prova phCollider ativo.
        // Manter por Tick durante o preparo, sem mudar transform/velocidade/rodas.
        vehicle.SetToRespondToPhysicsSystem();
        bool gravityAfterCollider = vehicle.HasGravity;
        // Native99: phCollider ativo, mas o mesmo phInst perdeu a gravidade no Tick seguinte.
        // Restaurar apenas a gravidade normal do coach proprio durante preparo/reset pendente.
        if (!gravityAfterCollider) vehicle.HasGravity = true;
        bool gravityAfter = vehicle.HasGravity, physicsAfter = vehicle.HasPhysics;
        bool frozenAfter = vehicle.IsPositionFrozen, collisionAfter = vehicle.IsCollisionEnabled;
        bool staticAfter = vehicle.IsStatic;
        if (!gravityAfterCollider || !physicsAfter || frozenAfter || !gravityAfter || !collisionAfter)
            Log("VEICULO_GRAVIDADE_APOS_COLLIDER", "Fase=" + stage + "; Codigo=" + state.Spec.Codigo +
                "; Handle=" + vehicle.Handle + "; Modelo=" + state.Spec.Modelo +
                "; MemoryAddress=0x" + vehicle.MemoryAddress.ToInt64().ToString("X", System.Globalization.CultureInfo.InvariantCulture) +
                "; GravityAntes=" + gravityBefore + "; GravityAposCollider=" + gravityAfterCollider +
                "; SetterSolicitado=" + (!gravityAfterCollider) + "; GravityGetterApos=" + gravityAfter +
                "; HasPhysics=" + physicsAfter + "; Frozen=" + frozenAfter + "; Collision=" + collisionAfter +
                "; IsStatic=" + staticAfter + "; Position=" + vehicle.Position + "; Velocity=" + vehicle.Velocity +
                "; Metodo=GTA.Entity.HasGravity_SET_ENTITY_HAS_GRAVITY_true; SemSetterTransform=true");
        if (!physicsAfter || frozenAfter || !gravityAfter || !collisionAfter)
            throw Error("coach sem phInst/gravidade/colisao livre apos restauracao propria; Fase=" + stage +
                "; HasPhysics=" + physicsAfter + "; Frozen=" + frozenAfter + "; Gravity=" + gravityAfter +
                "; Collision=" + collisionAfter + "; IsStatic=" + staticAfter + "; Handle=" + vehicle.Handle);
        int now = GTA.Game.GameTime;
        string evidence = "Fase=" + stage + "; Codigo=" + state.Spec.Codigo + "; Handle=" + vehicle.Handle +
            "; HasPhysics=" + hasPhysics + "; Frozen=" + frozen + "; IsStaticAntes=" + staticBefore +
            "; IsStaticDepois=" + staticAfter + "; Gravity=" + vehicle.HasGravity +
            "; Collision=" + vehicle.IsCollisionEnabled + "; Position=" + vehicle.Position +
            "; Velocity=" + vehicle.Velocity + "; Speed=" + Number(vehicle.Speed) +
            "; DesdeAjusteMs=" + unchecked(now - state.CoachLayerAppliedAt) +
            "; Metodo=GTA.Entity.SetToRespondToPhysicsSystem; SemSetterTransform=true";
        if (staticAfter) state.CoachPendingEvidence = evidence;
        if (forceLog || now >= state.CoachNextColliderLog)
        {
            state.CoachNextColliderLog = now + 500;
            Log("VEICULO_COLLIDER_PREPARO", evidence);
        }
        return !staticAfter;
    }

    private string CoachWaitEvidence()
    {
        // Snapshot das mesmas queries de preparo; timeout nao faz queries extras.
        var evidence = new System.Text.StringBuilder();
        foreach (VehicleState state in vehicles)
            if (LayerBoundCoach(state) && !state.CoachLayerSettled && !string.IsNullOrEmpty(state.CoachPendingEvidence))
                evidence.Append("; UltimoApoioPendente={").Append(state.CoachPendingEvidence).Append("}");
        return evidence.ToString();
    }

    // Escopo fechado: o onibus proprio dos desembarques EP04_C01/EP06_C01.
    // Motorista s_m_m_gentransport ja conferido no PedList.xml instalado:927.
    private static bool AuthorBusExit(Operation operation)
    {
        return operation.Spec.Tipo == "sair" && operation.Spec.Assento >= 0 &&
            operation.Vehicle.Spec.Codigo == "ONIBUS_TEMPORADA" &&
            operation.Vehicle.Spec.Tipo == "ONIBUS" && (operation.Vehicle.Spec.Modelo == "bus" || operation.Vehicle.Spec.Modelo == "coach") &&
            operation.Vehicle.Spec.MotoristaExtra && operation.Vehicle.Extra != null;
    }
    private static bool DoorValid(GTA.Vehicle vehicle, int index)
    {
        // CitizenFX GET_IS_DOOR_VALID, BOOL(vehicle, doorIndex); nao inventar porta.
        return GTA.Native.Function.Call<bool>((GTA.Native.Hash)0x645F4B6E8499F632UL, vehicle.Handle, index);
    }
    private void PrepareAuthorBusExit(Operation operation, GTA.Vehicle vehicle, int elapsed)
    {
        if (!AuthorBusExit(operation)) return;
        ActorState driver = operation.Vehicle.Extra; RequireActor(driver);
        if (driver.Ped.IsInjured || !InSeat(driver, vehicle, -1) ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VEHICLE_DRIVEABLE, vehicle.Handle, false))
            throw Error("preparo autor do onibus exige motorista proprio vivo no assento -1 e veiculo dirigivel");
        LogAuthorBusState("ANTES_PREPARO_AUTOR", operation, vehicle, elapsed);
        // re_bus_tours.c:3425: frear pelo motorista proprio antes da mesma saida flags0.
        // Sem Wait, sem reassentar ator e sem reenviar a tarefa de saida.
        GTA.Native.Function.Call(GTA.Native.Hash.TASK_VEHICLE_TEMP_ACTION, driver.Handle, vehicle.Handle, 24, 5000);
        bool valid0 = DoorValid(vehicle, 0), valid1 = DoorValid(vehicle, 1);
        if (!valid0 && !valid1) throw Error("onibus proprio sem portas reais 0/1 para preparo autor");
        // assassin_bus.c func303:13410/13412. Somente indices validos neste modelo real.
        if (valid1) GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_DOOR_OPEN, vehicle.Handle, 1, false, false);
        if (valid0) GTA.Native.Function.Call(GTA.Native.Hash.SET_VEHICLE_DOOR_OPEN, vehicle.Handle, 0, false, false);
        Log("VEICULO_SAIDA_PREPARO_AUTOR", "Veiculo=" + operation.Vehicle.Spec.Codigo +
            "; MotoristaHandle=" + driver.Handle + "; MotoristaAssento=-1; TempAction=24; TempDurationMs=5000" +
            "; Door0Valid=" + valid0 + "; Door1Valid=" + valid1 +
            "; PortasSomenteValidas=true; SettersAtorNaAcao=false; FuncionalidadeNative=pendente");
    }
    // Chamadas limitadas: antes/depois do preparo/native, primeiro Tick e timeout.
    private void LogAuthorBusState(string stage, Operation operation, GTA.Vehicle vehicle, int elapsed)
    {
        if (!AuthorBusExit(operation)) return;
        try
        {
            GTA.Ped ped = operation.Actor.Ped;
            GTA.Ped seatedDriver = vehicle.GetPedOnSeat(GTA.VehicleSeat.Driver);
            ActorState ownDriver = operation.Vehicle.Extra;
            bool exists = !System.Object.ReferenceEquals(seatedDriver, null) && seatedDriver.Exists();
            bool same = exists && StillOwn(ownDriver) && seatedDriver.Handle == ownDriver.Handle &&
                seatedDriver.MemoryAddress == ownDriver.Address;
            var doors = new System.Collections.Generic.List<string>();
            for (int index = 0; index <= 2; index++)
            {
                bool valid = DoorValid(vehicle, index);
                string ratio = valid ? Number(GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_VEHICLE_DOOR_ANGLE_RATIO,
                    vehicle.Handle, index)) : "not_applicable_invalid_door";
                doors.Add("Door" + index + "Valid=" + valid + ",Ratio=" + ratio);
            }
            Log("VEICULO_SAIDA_FISICA_GETTERS", "Stage=" + stage + "; TempoMs=" + elapsed +
                "; VehicleHandle=" + vehicle.Handle + "; ActorHandle=" + ped.Handle +
                "; VehicleCollision=" + vehicle.IsCollisionEnabled + "; VehiclePhysics=" + vehicle.HasPhysics +
                "; VehicleModelCollisionLoaded=" + vehicle.Model.IsCollisionLoaded +
                "; VehicleDriveable=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_VEHICLE_DRIVEABLE, vehicle.Handle, false) +
                "; VehicleEngineRunning=" + vehicle.IsEngineRunning + "; VehicleFrozen=" + vehicle.IsPositionFrozen +
                "; VehicleSpeed=" + Number(vehicle.Speed) +
                "; ActorCollision=" + ped.IsCollisionEnabled + "; ActorPhysics=" + ped.HasPhysics +
                "; ActorModelCollisionLoaded=" + ped.Model.IsCollisionLoaded +
                "; DriverExists=" + exists + "; DriverOwnIdentity=" + same +
                "; DriverHandle=" + (exists ? seatedDriver.Handle : 0) +
                "; DriverHealth=" + (exists ? seatedDriver.Health.ToString() : "absent") +
                "; DriverInjured=" + (exists ? seatedDriver.IsInjured.ToString() : "absent") +
                "; DriverCollision=" + (exists ? seatedDriver.IsCollisionEnabled.ToString() : "absent") +
                "; DriverPhysics=" + (exists ? seatedDriver.HasPhysics.ToString() : "absent") +
                "; Doors=" + System.String.Join(";", doors.ToArray()) + "; ApenasGetters=true");
        }
        catch (System.Exception ex) { Log("VEICULO_DIAGNOSTICO_ERRO", "BusState=" + stage + "; " + ex.Message); }
    }

    // GET_PED_CONFIG_FLAG usa os indices do enum oficial/API191; nenhum SET ou tarefa aqui.
    private void LogExitConfiguration(string stage, Operation operation, int elapsed)
    {
        try
        {
            GTA.Ped ped = operation.Actor.Ped;
            LogAuthorBusState(stage, operation, OwnVehicle(operation.Vehicle), elapsed);
            LogExitNativeTasks(stage, operation, elapsed);
            int rawGroupIndex = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_GROUP_INDEX, ped.Handle);
            bool inGroup = ped.IsInGroup;
            GTA.PedGroup group = inGroup ? ped.PedGroup : null;
            GTA.Ped leader = group == null ? null : group.Leader;
            string leaderState = leader == null || !leader.Exists() ? "absent" :
                "Handle=" + leader.Handle + ",InActorVehicle=" + leader.IsInVehicle(operation.Vehicle.Vehicle);
            var values = new System.Collections.Generic.List<string>();
            foreach (GTA.PedConfigFlagToggles flag in new GTA.PedConfigFlagToggles[] {
                GTA.PedConfigFlagToggles.DontLeaveVehicleIfLeaderNotInVehicle,
                GTA.PedConfigFlagToggles.StayInCarOnJack,
                GTA.PedConfigFlagToggles.DontDragMeOutCar,
                GTA.PedConfigFlagToggles.KeepTasksAfterCleanUp,
                GTA.PedConfigFlagToggles.BlockNonTemporaryEvents,
                GTA.PedConfigFlagToggles.AllowPedInVehiclesOverrideTaskFlags,
                GTA.PedConfigFlagToggles.AllowPlayerToInterruptVehicleEntryExit,
                GTA.PedConfigFlagToggles.PreventAutoShuffleToDriversSeat,
                GTA.PedConfigFlagToggles.WillFollowLeaderAnyMeans,
                GTA.PedConfigFlagToggles.BlockWeaponSwitching
            })
            {
                bool enabled = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_PED_CONFIG_FLAG, ped.Handle, (int)flag, false);
                values.Add(flag.ToString() + "[" + (int)flag + "]=" + enabled);
            }
            GTA.Vehicle ownVehicle = operation.Vehicle.Vehicle;
            GTA.Math.Vector3 entry0 = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ENTRY_POINT_POSITION, ownVehicle.Handle, 0);
            GTA.Math.Vector3 entry2 = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ENTRY_POINT_POSITION, ownVehicle.Handle, 2);
            float door0 = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, ownVehicle.Handle, 0);
            Log("VEICULO_SAIDA_ENTRY_GETTERS", "Stage=" + stage + "; TempoMs=" + elapsed +
                "; Model=" + operation.Vehicle.Spec.Modelo + "; Assento=" + operation.Spec.Assento +
                "; Entry0=" + entry0 + "; Entry0Finite=" + FiniteVector(entry0) +
                "; Entry2=" + entry2 + "; Entry2Finite=" + FiniteVector(entry2) +
                "; DoorRatio0=" + door0.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; ProbeIndexPreserved=2; ApenasGetters=true; PreparoAutorPortas=" + AuthorBusExit(operation));
            Log("VEICULO_SAIDA_CONFIG_GETTERS", "Stage=" + stage + "; TempoMs=" + elapsed +
                "; Ator=" + operation.Actor.Code + "; Handle=" + ped.Handle + "; GroupIndexRaw=" + rawGroupIndex +
                "; IsInGroup=" + inGroup + "; GroupLeader=" + leaderState +
                "; StaysInVehicleWhenJacked=" + ped.StaysInVehicleWhenJacked + "; Config=" + System.String.Join(",", values.ToArray()));
        }
        catch (System.Exception ex) { Log("VEICULO_DIAGNOSTICO_ERRO", "Config=" + stage + "; " + ex.Message); }
    }

    // Diagnostico passivo limitado aos tres pontos de LogExitConfiguration.
    // API191 Ped.IsExitingVehicle chama GET_IS_TASK_ACTIVE(ped,2), nao ScriptTaskStatus.
    private void LogExitNativeTasks(string stage, Operation operation, int elapsed)
    {
        if (stage != "ANTES_NATIVE" && stage != "PRIMEIRO_TICK_POSTERIOR" && stage != "TIMEOUT") return;
        try
        {
            GTA.Ped ped = operation.Actor.Ped; GTA.Vehicle vehicle = OwnVehicle(operation.Vehicle);
            bool nativeExit = ped.IsExitingVehicle;
            var activeTasks = new System.Collections.Generic.List<string>();
            activeTasks.Add("Task2=" + nativeExit);
            // eTaskType: enter, enter-seat, shuffle, exit-seat, motion-in-vehicle.
            foreach (int index in new int[] { 160, 163, 165, 167, 173 })
                activeTasks.Add("Task" + index + "=" +
                    GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_IS_TASK_ACTIVE, ped.Handle, index));
            uint layout = GTA.Native.Function.Call<uint>(GTA.Native.Hash.GET_VEHICLE_LAYOUT_HASH, vehicle.Handle);
            Log("VEICULO_SAIDA_TASKS_NATIVOS", "Stage=" + stage + "; TempoMs=" + elapsed +
                "; AtorHandle=" + ped.Handle + "; VehicleHandle=" + vehicle.Handle +
                "; AssentoPreservado=" + operation.Spec.Assento +
                "; Model=" + operation.Vehicle.Spec.Modelo +
                "; LayoutHash=0x" + layout.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) +
                "; IsSittingInOwnVehicle=" + ped.IsSittingInVehicle(vehicle) +
                "; IsEnteringVehicle=" + ped.IsEnteringVehicle + "; IsExitingVehicle=" + nativeExit +
                "; InOwnVehicle=" + ped.IsInVehicle(vehicle) +
                "; NativeTasks=" + System.String.Join(",", activeTasks.ToArray()) +
                "; ApenasGetters=true; NenhumaAlteracaoAssentoFlagsTarefas=true");
        }
        catch (System.Exception ex) { Log("VEICULO_DIAGNOSTICO_ERRO", "NativeTasks=" + stage + "; " + ex.Message); }
    }

    private void LogExitBones(string code, Operation operation, GTA.Vehicle vehicle, int elapsed,
        GTA.Math.Vector3 head, GTA.Math.Vector3 pelvis)
    {
        GTA.Ped ped = operation.Actor.Ped;
        GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
        ped.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
        GTA.Ped occupant = vehicle.GetPedOnSeat((GTA.VehicleSeat)operation.Spec.Assento);
        bool occupantValid = !System.Object.ReferenceEquals(occupant, null) && occupant.Exists();
        GTA.Math.Vector3 headLocal = ExitLocal(vehicle, head), pelvisLocal = ExitLocal(vehicle, pelvis);
        Log(code, "Veiculo=" + operation.Vehicle.Spec.Codigo + "; Ator=" + operation.Actor.Code +
            "; TempoMs=" + elapsed + "; ActorHandle=" + ped.Handle + "; VehicleHandle=" + vehicle.Handle +
            "; Health=" + ped.Health + "; MaxHealth=" + ped.MaxHealth +
            "; RawNativeHealth=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_HEALTH, ped.Handle) +
            "; IsInjured=" + ped.IsInjured + "; Assento=" + operation.Spec.Assento +
            "; OccupantHandle=" + (occupantValid ? occupant.Handle : 0) +
            "; OccupantOwnIdentity=" + (occupantValid && occupant.Handle == operation.Actor.Handle && occupant.MemoryAddress == operation.Actor.Address) +
            "; HeadWorld=" + head + "; PelvisWorld=" + pelvis + "; HeadVehicleLocal=" + headLocal +
            "; PelvisVehicleLocal=" + pelvisLocal + "; VehiclePosition=" + vehicle.Position +
            "; ScriptTask=" + task + "; ScriptTaskStatus=" + status + "; InOwnVehicle=" + ped.IsInVehicle(vehicle) +
            "; TaskObserved=" + operation.ExitTaskObserved + "; MovementObserved=" + operation.ExitMovementObserved +
            "; Samples=" + operation.ExitSamples + "; PeakBoneStep=" + Number(operation.ExitPeakBoneStep) +
            "; PeakBoneSpeed=" + Number(operation.ExitPeakBoneSpeed));
    }

    private static GTA.Math.Vector3 ExitBone(GTA.Ped ped, GTA.Bone bone)
    {
        GTA.PedBone actual = ped.Bones[bone];
        if (actual == null || actual.Index < 0) throw Error("osso real de saida nao disponivel: " + bone);
        GTA.Math.Vector3 point = actual.Position;
        Vector(point); return point;
    }
    private static GTA.Math.Vector3 ExitLocal(GTA.Vehicle vehicle, GTA.Math.Vector3 point)
    {
        GTA.Math.Vector3 delta = point - vehicle.Position, axisX = vehicle.RightVector,
            axisY = vehicle.ForwardVector, axisZ = vehicle.UpVector;
        Vector(delta); Vector(axisX); Vector(axisY); Vector(axisZ);
        return new GTA.Math.Vector3(GTA.Math.Vector3.Dot(delta, axisX), GTA.Math.Vector3.Dot(delta, axisY), GTA.Math.Vector3.Dot(delta, axisZ));
    }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }

    // Read-only, bounded to the two owned SuperD driving scenes. No task or setter here.
    private bool LuxuryDriveDiagnosticScope(Operation operation)
    {
        return (sceneId == "EP15_C01" || sceneId == "EP15_C09") && operation != null &&
            operation.Vehicle != null && operation.Actor != null &&
            operation.Vehicle.Spec.Codigo == "CARRO_LUXO" &&
            System.String.Equals(operation.Vehicle.Spec.Modelo, "superd", System.StringComparison.OrdinalIgnoreCase) &&
            System.String.Equals(operation.Actor.Code, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase);
    }

    private string ReadLuxuryDriveState(Operation operation, GTA.Vehicle vehicle)
    {
        if (!LuxuryDriveDiagnosticScope(operation)) return "";
        try
        {
            GTA.Ped ped = operation.Actor.Ped;
            GTA.Ped driver = vehicle.GetPedOnSeat(GTA.VehicleSeat.Driver);
            bool driverExists = !System.Object.ReferenceEquals(driver, null) && driver.Exists();
            bool driverOwn = driverExists && driver.Handle == operation.Actor.Handle &&
                driver.MemoryAddress == operation.Actor.Address;
            GTA.Ped player = GTA.Game.Player.Character;
            bool playerExists = !System.Object.ReferenceEquals(player, null) && player.Exists();
            string playerEvidence = "; PlayerExists=" + playerExists;
            if (playerExists)
            {
                GTA.Math.Vector3 playerPosition = player.Position;
                GTA.Math.Vector3 delta = playerPosition - vehicle.Position;
                GTA.Math.Vector3 local = ExitLocal(vehicle, playerPosition);
                playerEvidence += "; PlayerHandle=" + player.Handle + "; PlayerPos=" + playerPosition +
                    "; PlayerRelativeVehicleLocal=" + local + "; PlayerDistanceXY=" + Number(Horizontal(delta)) +
                    "; PlayerForwardProjection=" + Number(GTA.Math.Vector3.Dot(delta, vehicle.ForwardVector)) +
                    "; PlayerRightProjection=" + Number(GTA.Math.Vector3.Dot(delta, vehicle.RightVector)) +
                    "; PlayerVisible=" + player.IsVisible + "; PlayerCollision=" + player.IsCollisionEnabled +
                    "; PlayerFrozen=" + player.IsPositionFrozen;
            }
            return "; LuxuryDiagnosticScene=" + sceneId + "; VehicleHandle=" + vehicle.Handle +
                "; VehicleEngineRunning=" + vehicle.IsEngineRunning +
                "; VehicleEngineHealth=" + Number(vehicle.EngineHealth) +
                "; VehicleBodyHealth=" + Number(vehicle.BodyHealth) +
                "; VehicleDriveable=" + vehicle.IsDriveable +
                "; VehicleCollision=" + vehicle.IsCollisionEnabled + "; VehiclePhysics=" + vehicle.HasPhysics +
                "; VehicleVelocity=" + vehicle.Velocity + "; VehicleHeading=" + Number(vehicle.Heading) +
                "; DriverExists=" + driverExists + "; DriverOwnIdentity=" + driverOwn +
                "; DriverHandle=" + (driverExists ? driver.Handle : 0) +
                "; ActorHealth=" + ped.Health + "; ActorDead=" + ped.IsDead + "; ActorInjured=" + ped.IsInjured +
                "; ActorBlockPermanentEvents=" + ped.BlockPermanentEvents +
                "; ActorCollision=" + ped.IsCollisionEnabled + "; ActorPhysics=" + ped.HasPhysics +
                "; RequestedSpeed=" + Number(operation.Spec.Velocidade) +
                "; RequestedTrajetoDireto=" + operation.Vehicle.Spec.TrajetoDireto +
                "; RequestedStopForPeds=true; RequestedStopForVehicles=true" +
                "; HandbrakeGetterAvailable=false; ActualDrivingFlagsGetterAvailable=false" +
                "; ActualDrivingSpeedGetterAvailable=false; RequestedValuesAreNotGetters=true" +
                playerEvidence + "; DiagnosticGettersOnly=true";
        }
        catch (System.Exception ex) { return "; LuxuryDiagnosticQueryError=" + ex.Message + "; DiagnosticGettersOnly=true"; }
    }

    // CompleteInitialSeats has settled; before Ready/ARM, and again after an explicit reset.
    // Door 0 is the driver's front door. Existing safe native signatures, owned vehicle only.
    private void LogLuxuryDriverEntryPrepared()
    {
        if (sceneId != "EP15_C09") return;
        foreach (Operation operation in operations)
        {
            if (!LuxuryDriveDiagnosticScope(operation) || operation.Spec.Tipo != "entrar" || operation.Spec.Assento != -1) continue;
            try
            {
                GTA.Vehicle vehicle = OwnVehicle(operation.Vehicle);
                RequireActor(operation.Actor);
                GTA.Ped ped = operation.Actor.Ped;
                GTA.Math.Vector3 minimum, maximum;
                vehicle.Model.GetDimensions(out minimum, out maximum);
                bool doorValid = DoorValid(vehicle, 0);
                string entryEvidence = "; DriverDoorIndex=0; DriverDoorValid=" + doorValid;
                if (doorValid)
                {
                    GTA.Math.Vector3 entry = GTA.Native.Function.Call<GTA.Math.Vector3>((GTA.Native.Hash)0xC0572928C0ABFDA3UL,
                        vehicle.Handle, 0);
                    entryEvidence += "; EntryWorld=" + entry + "; EntryFinite=" + FiniteVector(entry) +
                        "; EntryLocal=" + ExitLocal(vehicle, entry) +
                        "; ActorEntryDistanceXY=" + Number(Horizontal(ped.Position - entry)) +
                        "; ActorEntryDeltaZ=" + Number(ped.Position.Z - entry.Z);
                }
                Log("EP15_C09_ENTRADA_PREPARO_GETTERS", "Scene=" + sceneId + "; Phase=PREP; BeforeARM=true" +
                    "; VehicleHandle=" + vehicle.Handle + "; ActorHandle=" + ped.Handle +
                    "; VehiclePos=" + vehicle.Position + "; ActorPos=" + ped.Position +
                    "; ActorRelativeVehicleLocal=" + ExitLocal(vehicle, ped.Position) +
                    "; VehicleHeading=" + Number(vehicle.Heading) +
                    "; VehicleForward=" + vehicle.ForwardVector + "; VehicleRight=" + vehicle.RightVector +
                    "; ModelMinimum=" + minimum + "; ModelMaximum=" + maximum +
                    "; ModelBoundsFinite=" + (FiniteVector(minimum) && FiniteVector(maximum)) +
                    entryEvidence + ReadLuxuryDriveState(operation, vehicle) +
                    "; NoProbeNoSetter=true; NativeDoorPositionIsNotVisualApproval=true");
            }
            catch (System.Exception ex)
            { Log("EP15_C09_ENTRADA_PREPARO_QUERY_ERRO", "Scene=" + sceneId + "; " + ex.Message + "; NoProbeNoSetter=true"); }
        }
    }

    // Somente getters; falha de diagnostico geral nao altera a acao nem seus limites.
    private void LogOperationState(string code, Operation operation, GTA.Vehicle vehicle, int elapsed)
    {
        try
        {
            GTA.Ped ped = operation.Actor.Ped;
            GTA.Math.Vector3 actorPosition = ped.Position, vehiclePosition = vehicle.Position;
            GTA.ScriptTaskNameHash taskName; GTA.ScriptTaskStatus taskStatus;
            ped.GetCurrentScriptTaskNameHashAndStatus(out taskName, out taskStatus);
            string evidence = "Veiculo=" + operation.Vehicle.Spec.Codigo + "; Ator=" + operation.Actor.Code +
                "; Tipo=" + operation.Spec.Tipo + "; TipoVeiculo=" + operation.Vehicle.Spec.Tipo +
                "; Assento=" + operation.Spec.Assento + "; TempoMs=" + elapsed +
                "; InicioMs=" + operation.Spec.InicioMs + "; DuracaoMs=" + operation.Spec.DuracaoMs +
                "; AtorFrozen=" + ped.IsPositionFrozen + "; VeiculoFrozen=" + vehicle.IsPositionFrozen +
                "; Velocidade=" + vehicle.Speed.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; AtorPos=" + actorPosition + "; VeiculoPos=" + vehiclePosition +
                "; NoVeiculoProprio=" + ped.IsInVehicle(vehicle) +
                "; NoAssentoEsperado=" + InSeat(operation.Actor, vehicle, operation.Spec.Assento) +
                "; ScriptTask=" + taskName + "; ScriptTaskStatus=" + taskStatus;
            evidence += ReadLuxuryDriveState(operation, vehicle);
            if (operation.Spec.Tipo == "sair")
            {
                LogExitBones("VEICULO_SAIDA_BONES_ESTADO", operation, vehicle, elapsed,
                    ExitBone(ped, GTA.Bone.SkelHead), ExitBone(ped, GTA.Bone.SkelPelvis));
                evidence += "; DoorLockStatus=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_VEHICLE_DOOR_LOCK_STATUS, vehicle.Handle) +
                    "; DoorRatio1=" + GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, vehicle.Handle, 1).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; DoorRatio2=" + GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_VEHICLE_DOOR_ANGLE_RATIO, vehicle.Handle, 2).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; ExitProbeDoorIndex=2; ExitProbeState=" + operation.ExitProbeState + "; ExitProbeFrom=" + operation.ExitFrom +
                    "; ExitProbeTo=" + operation.ExitTo + "; ExitProbeHit=" + (operation.ExitProbeState == "complete_hit" ? operation.ExitHit.ToString() : "not_applicable") +
                    "; ExitProbeNormal=" + (operation.ExitProbeState == "complete_hit" ? operation.ExitNormal.ToString() : "not_applicable") +
                    "; ExitProbeEntity=" + (operation.ExitProbeState == "complete_hit" ? operation.ExitHitEntity.ToString() : "not_applicable");
            }
            if (operation.Spec.Tipo == "dirigir" && operation.Point >= 0 && operation.Point < operation.Route.Count)
            {
                GTA.Math.Vector3 target = operation.Route[operation.Point];
                evidence += "; Ponto=" + (operation.Point + 1) + "/" + operation.Route.Count + "; Alvo=" + target +
                    "; DistanciaXY=" + Horizontal(vehiclePosition - target).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; DeltaZ=" + (vehiclePosition.Z - target.Z).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            Log(code, evidence);
        }
        catch (System.Exception ex) { Log("VEICULO_TELEMETRIA_ERRO", "Evento=" + code + "; TempoMs=" + elapsed + "; " + ex.Message); }
    }

    // Diagnose only the external passenger-door path. Ignore the owned vehicle;
    // its door state is logged separately. Neither pending nor invalid means clear.
    private void StartExitProbe(Operation operation, GTA.Vehicle vehicle)
    {
        try
        {
            GTA.Math.Vector3 entry = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ENTRY_POINT_POSITION, vehicle.Handle, 2);
            GTA.Math.Vector3 min, max; vehicle.Model.GetDimensions(out min, out max);
            GTA.Math.Vector3 offset = entry - vehicle.Position;
            float bound = System.Math.Max(min.Length(), max.Length()) + 2f;
            if (!FiniteVector(entry) || !FiniteVector(min) || !FiniteVector(max) || !FiniteVector(vehicle.RightVector) ||
                offset.Length() > bound || offset.Length() < 0.1f)
            { operation.ExitProbeState = "invalid_entry_position"; return; }
            GTA.Math.Vector3 side = Unit(vehicle.RightVector);
            if (Dot(offset, side) < 0f) side = -side;
            operation.ExitFrom = entry + new GTA.Math.Vector3(0f, 0f, 0.4f);
            operation.ExitTo = operation.ExitFrom + side * 1.2f;
            operation.ExitRay = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_CAPSULE,
                operation.ExitFrom.X, operation.ExitFrom.Y, operation.ExitFrom.Z,
                operation.ExitTo.X, operation.ExitTo.Y, operation.ExitTo.Z, 0.3f, 17, vehicle.Handle, 7);
            operation.ExitRayIssuedAt = lastElapsed;
            operation.ExitProbeState = operation.ExitRay == 0 ? "invalid_handle" : "pending";
        }
        catch (System.Exception ex)
        { operation.ExitProbeState = "query_error"; Log("VEICULO_SAIDA_PROBE_ERRO", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; " + ex.Message); }
    }

    private void PollExitProbe(Operation operation, int elapsed)
    {
        if (operation.ExitRay == 0 || elapsed <= operation.ExitRayIssuedAt) return;
        try
        {
            using (var hit = new GTA.Native.OutputArgument())
            using (var point = new GTA.Native.OutputArgument())
            using (var normal = new GTA.Native.OutputArgument())
            using (var entity = new GTA.Native.OutputArgument())
            {
                int status = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, operation.ExitRay, hit, point, normal, entity);
                if (status == 1) return;
                operation.ExitRay = 0;
                if (status != 2) { operation.ExitProbeState = "invalid_result"; return; }
                // Em no-hit os outros outputs nao descrevem um contato valido.
                // Nao ler memoria sem semantica nem inventar ponto/normal de colisao.
                if (hit.GetResult<int>() == 0)
                {
                    operation.ExitProbeState = "complete_clear";
                    operation.ExitHit = operation.ExitNormal = GTA.Math.Vector3.Zero;
                    operation.ExitHitEntity = 0;
                    return;
                }
                operation.ExitHit = point.GetResult<GTA.Math.Vector3>(); operation.ExitNormal = normal.GetResult<GTA.Math.Vector3>();
                operation.ExitHitEntity = entity.GetResult<int>();
                operation.ExitProbeState = !FiniteVector(operation.ExitHit) || !FiniteVector(operation.ExitNormal) ? "invalid_outputs" : "complete_hit";
            }
        }
        catch (System.Exception ex)
        { operation.ExitRay = 0; operation.ExitProbeState = "query_error"; Log("VEICULO_SAIDA_PROBE_ERRO", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; " + ex.Message); }
    }

    // Somente C3: o destino narrativo continua sendo a origem/centro do caminhao.
    // DriveTo150 encerrou antes dele; compensacao candidata medida uma vez no preparo.
    // Exclusive C1 contract. Logical center/arrival radius remain unchanged.
    private bool IsC1NativeCenterGoal(Operation operation)
    {
        if (sceneId != "EP15_C01" || operation == null || operation.Vehicle == null || operation.Actor == null) return false;
        VehicleSpec spec = operation.Vehicle.Spec;
        return operation.Spec.Tipo == "dirigir" && spec.Tipo == "CARRO" && spec.Codigo == "CARRO_LUXO" &&
            System.String.Equals(spec.Modelo, "superd", System.StringComparison.OrdinalIgnoreCase) &&
            System.String.Equals(operation.Actor.Code, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase) && !operation.Actor.Extra;
    }

    private bool PrepareC1NativeCenterGoal()
    {
        if (sceneId != "EP15_C01") return true;
        if (vehicles.Count != 1 || operations.Count != 1 || totalDuration != 7000 || !IsC1NativeCenterGoal(operations[0]))
            throw Error("C1: compensacao Native exclusiva de um SuperD/Rich e uma direcao de7s");
        Operation operation = operations[0]; VehicleSpec spec = operation.Vehicle.Spec;
        bool ownInitialDriver = false;
        foreach (var seat in spec.ActorInitialSeats)
            if (System.String.Equals(seat.Key, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase) && seat.Value == -1) ownInitialDriver = true;
        if (spec.MotoristaExtra || !spec.TrajetoDireto || spec.HeadingRelativo != 0f || spec.PosicaoRelativa != GTA.Math.Vector3.Zero ||
            spec.ActorInitialSeats.Count != 1 || !ownInitialDriver || operation.Route.Count != 1 ||
            operation.Spec.RotaRelativa.Count != 1 || operation.Spec.RotaRelativa[0] != new GTA.Math.Vector3(0f, 8f, 0f) ||
            operation.Spec.Assento != -1 || operation.Spec.InicioMs != 0 || operation.Spec.DuracaoMs != 7000 ||
            operation.Spec.Velocidade != 3f || operation.Spec.RaioChegada != 1.5f ||
            !operation.Spec.RaioParada.HasValue || operation.Spec.RaioParada.Value != 1f)
            throw Error("C1: preservar rotaF8/3mps/7s/centro1.5m/parada1m e motorista inicial proprio");
        if (operation.C1NativeGoalPrepared) return operation.C1NativeGoalGroundMeasured;
        GTA.Math.Vector3 minimum, maximum;
        GTA.Math.Vector3 logical = operation.Route[0];
        operation.C1NativeGoal = MeasureLuxuryNativeCenterGoal(operation, logical, operation.Vehicle.Spawn, out minimum, out maximum);
        if (samples.Count > 254) throw Error("C1: extensao excede256 sondas de chao");
        for (int part = 1; part <= 2; part++)
            samples.Add(new GroundSample { Point = logical + (operation.C1NativeGoal - logical) * (part / 2f),
                Operation = part == 2 ? operation : null, C1NativeGoalEndpoint = part == 2 });
        operation.C1NativeGoalPrepared = true;
        Log("VEICULO_C1_NATIVE_GOAL_PREPARADO", "Veiculo=" + spec.Codigo + "; ModelMin=" + minimum +
            "; ModelMax=" + maximum + "; FrontAdvance=" + maximum.Y + "; LogicalCenterGoal=" + logical +
            "; NativeGoalCandidate=" + operation.C1NativeGoal + "; GroundExtension=pending" +
            "; NativeStopReference=hipotese_frente; NoActionReanchor=True; DurationMs=7000; ArrivalXY=1.5; StopRange=1");
        return false; // Both added samples must pass existing ground guards on a later Tick.
    }

    // Same C1 measured-front computation, reused only by exact C9 preparation.
    private GTA.Math.Vector3 MeasureLuxuryNativeCenterGoal(Operation operation, GTA.Math.Vector3 logical,
        GTA.Math.Vector3 previous, out GTA.Math.Vector3 minimum, out GTA.Math.Vector3 maximum)
    {
        if (!IsC1NativeCenterGoal(operation) && !IsC9NativeCenterGoal(operation) && !IsC12NativeCenterGoal(operation))
            throw Error("C1/C9: measured front requires own reviewed Rich/SuperD drive");
        GTA.Vehicle vehicle = OwnVehicle(operation.Vehicle);
        if (!vehicle.Model.IsLoaded || !vehicle.Model.IsCollisionLoaded || !vehicle.HasPhysics ||
            !vehicle.IsPositionFrozen || vehicle.Speed > 0.1f)
            throw Error("C1/C9: medir a frente exige SuperD proprio carregado, fisico e parado no preparo");
        vehicle.Model.GetDimensions(out minimum, out maximum); Vector(minimum); Vector(maximum);
        if (minimum.X >= maximum.X || minimum.Y >= 0f || maximum.Y <= 0f || maximum.Y > 8f || minimum.Z >= maximum.Z)
            throw Error("C1/C9: bounds Native nao descrevem frente finita do SuperD proprio");
        GTA.Math.Vector3 direction = Unit(logical - previous);
        GTA.Math.Vector3 actualForward = vehicle.ForwardVector; Vector(actualForward);
        if (Dot(Unit(actualForward), direction) < 0.95f)
            throw Error("C1/C9: SuperD nao aponta para destino logico; nao compensar pela traseira");
        GTA.Math.Vector3 goal = logical + direction * maximum.Y; Vector(goal);
        if (Horizontal(goal - anchor) > 150f || System.Math.Abs(goal.Z - anchor.Z) > 60f)
            throw Error("C1/C9: extensao Native saiu da area relativa permitida");
        return goal;
    }

    private bool IsC9NativeCenterGoal(Operation operation)
    {
        if (sceneId != "EP15_C09" || operation == null || operation.Actor == null || operation.Vehicle == null) return false;
        VehicleSpec spec = operation.Vehicle.Spec;
        return operation.Spec.Tipo == "dirigir" && spec.Tipo == "CARRO" && spec.Codigo == "CARRO_LUXO" &&
            System.String.Equals(spec.Modelo, "superd", System.StringComparison.OrdinalIgnoreCase) &&
            System.String.Equals(operation.Actor.Code, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase) && !operation.Actor.Extra;
    }

    private bool PrepareC9NativeCenterGoals()
    {
        if (sceneId != "EP15_C09") return true;
        if (vehicles.Count != 1 || operations.Count != 2 || actors.Count != 1 || totalDuration != 19000 ||
            operations[0].Spec.Tipo != "entrar" || !IsC9NativeCenterGoal(operations[1]))
            throw Error("C9: compensacao exclusiva de entrada5s+direcao14s do proprio Rich/SuperD");
        Operation entry = operations[0], operation = operations[1]; VehicleSpec spec = operation.Vehicle.Spec;
        if (entry.Vehicle != operation.Vehicle || entry.Actor != operation.Actor || entry.Spec.Assento != -1 ||
            entry.Spec.InicioMs != 0 || entry.Spec.DuracaoMs != 5000 || entry.Route.Count != 0 ||
            spec.MotoristaExtra || !spec.TrajetoDireto || spec.HeadingRelativo != 180f ||
            spec.PosicaoRelativa != new GTA.Math.Vector3(1f, -8f, 0f) || spec.ActorInitialSeats.Count != 0 ||
            operation.Spec.Assento != -1 || operation.Spec.InicioMs != 5000 || operation.Spec.DuracaoMs != 14000 ||
            operation.Spec.Velocidade != 3f || operation.Spec.RaioChegada != 1.5f ||
            !operation.Spec.RaioParada.HasValue || operation.Spec.RaioParada.Value != 1f ||
            operation.Route.Count != 2 || operation.Spec.RotaRelativa.Count != 2 ||
            operation.Spec.RotaRelativa[0] != new GTA.Math.Vector3(1f, -18f, 0f) ||
            operation.Spec.RotaRelativa[1] != new GTA.Math.Vector3(1f, -33f, 0f))
            throw Error("C9: preservar fonteSHIFTmenos8/rot180/dois centrosR1F-18,-33/3mps/5+14/centro1.5/parada1");
        if (operation.C9NativeGoalsPrepared)
        {
            if (operation.C9NativeGoals.Count != 2 || operation.C9NativeGoalsGroundMeasured.Count != 2)
                throw Error("C9: duas extensoes Native proprias obrigatorias");
            return operation.C9NativeGoalsGroundMeasured[0] && operation.C9NativeGoalsGroundMeasured[1];
        }
        if (samples.Count > 252) throw Error("C9: quatro novas sondas excedem256 amostras de chao");
        GTA.Math.Vector3 previous = operation.Vehicle.Spawn;
        for (int index = 0; index < 2; index++)
        {
            GTA.Math.Vector3 logical = operation.Route[index], minimum, maximum;
            GTA.Math.Vector3 goal = MeasureLuxuryNativeCenterGoal(operation, logical, previous, out minimum, out maximum);
            operation.C9NativeGoals.Add(goal); operation.C9NativeGoalsGroundMeasured.Add(false);
            for (int part = 1; part <= 2; part++)
                samples.Add(new GroundSample { Point = logical + (goal - logical) * (part / 2f),
                    Operation = part == 2 ? operation : null, RouteIndex = index, C9NativeGoalEndpoint = part == 2 });
            Log("VEICULO_C9_NATIVE_GOAL_PREPARADO", "Veiculo=" + spec.Codigo + "; RouteIndex=" + index +
                "; ModelMin=" + minimum + "; ModelMax=" + maximum + "; FrontAdvance=" + maximum.Y +
                "; LogicalCenterGoal=" + logical + "; NativeGoalCandidate=" + goal +
                "; GroundExtension=pending; DurationMs=14000; SceneMs=19000; ArrivalXY=1.5; StopRange=1; NoActionReanchor=True");
            previous = logical;
        }
        operation.C9NativeGoalsPrepared = true;
        return false; // Four new dry-ground samples must pass on a later PREP Tick.
    }

    // EP12 C1 only: same own extra driver/Rich passenger/SuperD, physical stop0..4 and exit4..8.
    // The scene still has its original8s narration +4s pause and exact EP06_C02_BASE lens.
    private bool IsC12NativeCenterGoal(Operation operation)
    {
        if (sceneId != "EP12_C01" || operation == null || operation.Vehicle == null || operation.Actor == null) return false;
        VehicleSpec spec = operation.Vehicle.Spec;
        return operation.Spec.Tipo == "dirigir" && spec.Codigo == "CARRO_LUXO" && spec.Tipo == "CARRO" &&
            System.String.Equals(spec.Modelo, "superd", System.StringComparison.OrdinalIgnoreCase) &&
            spec.MotoristaExtra && operation.Vehicle.Extra == operation.Actor && operation.Actor.Extra &&
            operation.Actor.Code == "MOTORISTA_CARRO_LUXO";
    }

    private bool PrepareC12NativeCenterGoal()
    {
        if (sceneId != "EP12_C01") return true;
        if (vehicles.Count != 1 || actors.Count != 2 || operations.Count != 2 || totalDuration != 8000 ||
            !IsC12NativeCenterGoal(operations[0]))
            throw Error("EP12 C1: exact own extra-driver/Rich SuperD drive0..4 and passenger exit4..8 required");
        Operation drive = operations[0], exit = operations[1]; VehicleSpec spec = drive.Vehicle.Spec;
        bool richPassenger = false, extraDriver = false;
        foreach (var seat in spec.ActorInitialSeats)
        {
            if (System.String.Equals(seat.Key, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase) && seat.Value == 0) richPassenger = true;
            if (seat.Key == drive.Actor.Code && seat.Value == -1) extraDriver = true;
        }
        if (!richPassenger || !extraDriver || spec.ActorInitialSeats.Count != 2 || spec.TrajetoDireto ||
            spec.HeadingRelativo != 0f || spec.PosicaoRelativa != new GTA.Math.Vector3(0f, -5f, 0f) ||
            drive.Spec.InicioMs != 0 || drive.Spec.DuracaoMs != 4000 || drive.Spec.Assento != -1 ||
            drive.Spec.Velocidade != 3f || drive.Spec.RaioChegada != 1.5f ||
            !drive.Spec.RaioParada.HasValue || drive.Spec.RaioParada.Value != 1f ||
            drive.Route.Count != 1 || drive.Spec.RotaRelativa.Count != 1 || drive.Spec.RotaRelativa[0] != GTA.Math.Vector3.Zero ||
            exit.Vehicle != drive.Vehicle || exit.Actor.Extra ||
            !System.String.Equals(exit.Actor.Code, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase) ||
            exit.Spec.Tipo != "sair" || exit.Spec.Assento != 0 || exit.Spec.InicioMs != 4000 || exit.Spec.DuracaoMs != 4000 || exit.Route.Count != 0)
            throw Error("EP12 C1: preserve SuperD F-5/rot0/extra-1/Rich0/3mps/arrival1.5/stop1/drive4+exit4");
        if (drive.C1NativeGoalPrepared) return drive.C1NativeGoalGroundMeasured;
        GTA.Math.Vector3 minimum, maximum, logical = drive.Route[0];
        drive.C1NativeGoal = MeasureLuxuryNativeCenterGoal(drive, logical, drive.Vehicle.Spawn, out minimum, out maximum);
        if (samples.Count > 254) throw Error("EP12 C1: two fresh extension samples exceed256");
        for (int part = 1; part <= 2; part++)
            samples.Add(new GroundSample { Point = logical + (drive.C1NativeGoal - logical) * (part / 2f),
                Operation = part == 2 ? drive : null, C1NativeGoalEndpoint = part == 2 });
        drive.C1NativeGoalPrepared = true;
        Log("VEICULO_C12_NATIVE_GOAL_PREPARADO", "Veiculo=" + spec.Codigo + "; ModelMin=" + minimum + "; ModelMax=" + maximum +
            "; FrontAdvance=" + Number(maximum.Y) + "; LogicalCenterGoal=" + logical + "; NativeGoalCandidate=" + drive.C1NativeGoal +
            "; GroundExtension=pending; ExtraDriverOwn=True; RichPassengerSeat=0; DriveMs=4000; ExitMs=4000; SceneMs=12000; ArrivalXY=1.5; StopRange=1; NoActionReanchor=True");
        return false; // Both actual ground samples must pass on a later existing PREP Tick.
    }

    private void RequireC12OwnDriverArrivalHold(Operation operation, GTA.Vehicle vehicle)
    {
        if (!IsC12NativeCenterGoal(operation) || !operation.LuxuryOwnDriverArrivalHold || operation.Point != 1 ||
            !operation.C1NativeGoalPrepared || !operation.C1NativeGoalGroundMeasured || operations.Count != 2 || totalDuration != 8000)
            throw Error("EP12 C1: extra-driver hold needs exact prepared own final center arrival");
        Operation exit = operations[1];
        if (exit.Vehicle != operation.Vehicle || exit.Actor.Extra || exit.Spec.Tipo != "sair" || exit.Spec.Assento != 0 ||
            exit.Spec.InicioMs != 4000 || exit.Spec.DuracaoMs != 4000)
            throw Error("EP12 C1: unexpected operation while keeping own extra driver");
        RequireActor(operation.Actor);
        GTA.Math.Vector3 position = vehicle.Position, velocity = vehicle.Velocity; Vector(position); Vector(velocity);
        bool exitActive = exit.Started;
        // Original Begin(sair) releases only vehicle physics and applies its handbrake once.
        // Do not freeze it again during the physical passenger door/exit, or clear/reseat the driver.
        if (!operation.Actor.Ped.IsInVehicle(vehicle) || !InSeat(operation.Actor, vehicle, -1) ||
            !vehicle.HasPhysics || !vehicle.IsCollisionEnabled ||
            (!exitActive && !vehicle.IsPositionFrozen) || (exitActive && vehicle.IsPositionFrozen) ||
            vehicle.Speed > (exitActive ? 0.5f : 0.1f) || velocity.Length() > (exitActive ? 0.5f : 0.1f) ||
            Horizontal(position - operation.Route[0]) > operation.Spec.RaioChegada || System.Math.Abs(position.Z - operation.Route[0].Z) > 3f)
            throw Error("EP12 C1: own stopped center/extra driver/active passenger-exit physics lost; no filmed repair");
    }

    private bool IsC3NativeCenterGoal(Operation operation)
    {
        VehicleSpec spec = operation.Vehicle.Spec;
        return sceneId == "EP13_C03" && operation.Spec.Tipo == "dirigir" && spec.Tipo == "CAMINHAO" &&
            ((spec.Codigo == "CAMINHAO_1" && spec.Modelo == "packer") ||
             (spec.Codigo == "CAMINHAO_2" && spec.Modelo == "hauler"));
    }

    private bool PrepareC3NativeCenterGoals()
    {
        if (sceneId != "EP13_C03") return true;
        bool added = false;
        foreach (Operation operation in operations)
        {
            if (!IsC3NativeCenterGoal(operation) || operation.C3NativeGoalPrepared) continue;
            if (!operation.Vehicle.Spec.MotoristaExtra || !operation.Vehicle.Spec.TrajetoDireto ||
                operation.Route.Count != 1 || operation.Spec.Assento != -1 || operation.Spec.InicioMs != 0 ||
                operation.Spec.DuracaoMs != 12000 || operation.Spec.Velocidade != 4f ||
                operation.Spec.RaioChegada != 1.5f || !operation.Spec.RaioParada.HasValue || operation.Spec.RaioParada.Value != 1f)
                throw Error("C3: compensacao Native exige o contrato direto de12s/4mps/centro1.5m/parada1m");
            GTA.Vehicle vehicle = OwnVehicle(operation.Vehicle);
            if (!vehicle.Model.IsLoaded || !vehicle.Model.IsCollisionLoaded || !vehicle.HasPhysics ||
                !vehicle.IsPositionFrozen || vehicle.Speed > 0.1f)
                throw Error("C3: medir a frente exige veiculo proprio carregado, fisico e parado no preparo");
            GTA.Math.Vector3 min, max; vehicle.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
            if (min.X >= max.X || min.Y >= 0f || max.Y <= 0f || max.Y > 8f || min.Z >= max.Z)
                throw Error("C3: bounds reais nao descrevem uma frente finita do caminhao");
            GTA.Math.Vector3 logical = operation.Route[0];
            GTA.Math.Vector3 direction = Unit(logical - operation.Vehicle.Spawn);
            GTA.Math.Vector3 actualForward = vehicle.ForwardVector; Vector(actualForward);
            if (Dot(Unit(actualForward), direction) < 0.95f)
                throw Error("C3: veiculo proprio nao aponta para o destino logico; nao compensar pela traseira");
            operation.C3NativeGoal = logical + direction * max.Y; Vector(operation.C3NativeGoal);
            if (Horizontal(operation.C3NativeGoal - anchor) > 150f || System.Math.Abs(operation.C3NativeGoal.Z - anchor.Z) > 60f)
                throw Error("C3: extensao Native saiu da area relativa ja permitida");
            if (samples.Count > 254) throw Error("C3: extensao de destino excede256 amostras de chao");
            for (int part = 1; part <= 2; part++)
                samples.Add(new GroundSample { Point = logical + (operation.C3NativeGoal - logical) * (part / 2f),
                    Operation = part == 2 ? operation : null, C3NativeGoalEndpoint = part == 2 });
            operation.C3NativeGoalPrepared = true; added = true;
            Log("VEICULO_C3_NATIVE_GOAL_PREPARADO", "Veiculo=" + operation.Vehicle.Spec.Codigo +
                "; ModelMin=" + min + "; ModelMax=" + max + "; FrontAdvance=" + max.Y +
                "; LogicalCenterGoal=" + logical + "; NativeGoalCandidate=" + operation.C3NativeGoal +
                "; GroundExtension=pending; NativeStopReference=hipotese_frente; NoActionReanchor=True");
        }
        if (added) return false; // As novas sondas passam no PollLoading de um Tick seguinte.
        foreach (Operation operation in operations)
            if (IsC3NativeCenterGoal(operation) && (!operation.C3NativeGoalPrepared || !operation.C3NativeGoalGroundMeasured)) return false;
        return true;
    }

    // EP03 C1 only: two owned extra drivers, physical passing route and distinct logical stops.
    private bool IsCaronaNativeCenterGoal(Operation operation)
    {
        if (sceneId != "EP03_C01" || operation == null || operation.Actor == null || operation.Vehicle == null) return false;
        VehicleSpec spec = operation.Vehicle.Spec;
        return operation.Spec.Tipo == "dirigir" && spec.Tipo == "CARRO" && spec.MotoristaExtra && operation.Actor.Extra &&
            operation.Actor == operation.Vehicle.Extra && operation.Actor.Code == "MOTORISTA_" + spec.Codigo &&
            ((spec.Codigo == "CARRO_PASSAGEM_1" && spec.Modelo == "tailgater") ||
             (spec.Codigo == "CARRO_PASSAGEM_2" && spec.Modelo == "superd"));
    }

    private bool PrepareCaronaNativeCenterGoals()
    {
        if (sceneId != "EP03_C01") return true;
        // Poor belongs to Main's direction roster, not this backend: both vehicle actions name null/extra drivers.
        if (vehicles.Count != 2 || operations.Count != 2 || totalDuration != 13500 || actors.Count != 2)
            throw Error("EP03 C1: preservar dois motoristas proprios efull13.5s");
        bool added = false; int codes = 0;
        foreach (Operation operation in operations)
        {
            if (!IsCaronaNativeCenterGoal(operation)) throw Error("EP03 C1: motorista/modelo/ID fora da dupla propria");
            VehicleSpec spec = operation.Vehicle.Spec;
            bool first = spec.Codigo == "CARRO_PASSAGEM_1";
            int mask = first ? 1 : 2;
            if ((codes & mask) != 0) throw Error("EP03 C1: direcao repetida da mesma passagem");
            codes |= mask;
            GTA.Math.Vector3 expectedSpawn = new GTA.Math.Vector3(4f, first ? -12f : -18f, 0f);
            GTA.Math.Vector3 expectedGoal = new GTA.Math.Vector3(4f, first ? 12f : 6f, 0f);
            if (!spec.TrajetoDireto || spec.HeadingRelativo != 0f || spec.PosicaoRelativa != expectedSpawn ||
                spec.ActorInitialSeats.Count != 1 || !spec.ActorInitialSeats.ContainsKey(operation.Actor.Code) ||
                spec.ActorInitialSeats[operation.Actor.Code] != -1 || operation.Spec.Assento != -1 ||
                operation.Spec.InicioMs != (first ? 0 : 5000) || operation.Spec.DuracaoMs != (first ? 7000 : 8500) ||
                operation.Spec.Velocidade != 9f || operation.Spec.RaioChegada != 2.5f ||
                !operation.Spec.RaioParada.HasValue || operation.Spec.RaioParada.Value != 1f ||
                operation.Route.Count != 1 || operation.Spec.RotaRelativa.Count != 1 || operation.Spec.RotaRelativa[0] != expectedGoal)
                throw Error("EP03 C1: preservar faixaR4/prazos7+8.5/9mps/centro2.5/parada1; destinosF12/F6");
            if (operation.CaronaNativeGoalPrepared) continue;
            GTA.Vehicle vehicle = OwnVehicle(operation.Vehicle);
            if (!vehicle.Model.IsLoaded || !vehicle.Model.IsCollisionLoaded || !vehicle.HasPhysics ||
                !vehicle.IsPositionFrozen || vehicle.Speed > 0.1f)
                throw Error("EP03 C1: medir frente so no preparo de carro proprio carregado/fisico/parado");
            GTA.Math.Vector3 minimum, maximum; vehicle.Model.GetDimensions(out minimum, out maximum);
            Vector(minimum); Vector(maximum);
            if (minimum.X >= maximum.X || minimum.Y >= 0f || maximum.Y <= 0f || maximum.Y > 8f || minimum.Z >= maximum.Z)
                throw Error("EP03 C1: bounds Native nao descrevem frente finita do carro proprio");
            GTA.Math.Vector3 logical = operation.Route[0], direction = Unit(logical - operation.Vehicle.Spawn);
            GTA.Math.Vector3 actualForward = vehicle.ForwardVector; Vector(actualForward);
            if (Dot(Unit(actualForward), direction) < 0.95f)
                throw Error("EP03 C1: carro nao aponta para destino logico; nao compensar pela traseira");
            // Native-only first goal: keep crossing F12/F6; avoid the observed first stop in the second lane.
            // Twelve additional metres are a candidate, never a captured road or an approved motion.
            operation.CaronaNativeGoal = logical + direction * (maximum.Y + (first ? 12f : 0f)); Vector(operation.CaronaNativeGoal);
            if (Horizontal(operation.CaronaNativeGoal - anchor) > 150f || System.Math.Abs(operation.CaronaNativeGoal.Z - anchor.Z) > 60f)
                throw Error("EP03 C1: extensao Native fora da area relativa permitida");
            if (samples.Count > 254) throw Error("EP03 C1: extensao excede256 amostras de chao");
            for (int part = 1; part <= 2; part++)
                samples.Add(new GroundSample { Point = logical + (operation.CaronaNativeGoal - logical) * (part / 2f),
                    Operation = part == 2 ? operation : null, CaronaNativeGoalEndpoint = part == 2 });
            operation.CaronaNativeGoalPrepared = true; added = true;
            Log("VEICULO_CARONA_NATIVE_GOAL_PREPARADO", "Veiculo=" + spec.Codigo + "; ModelMin=" + minimum +
                "; ModelMax=" + maximum + "; FrontAdvance=" + maximum.Y + "; LogicalCenterGoal=" + logical +
                "; NativeGoalCandidate=" + operation.CaronaNativeGoal + "; GroundExtension=pending; ArrivalXY=2.5; StopRange=1" +
                "; DurationMs=" + operation.Spec.DuracaoMs + "; StopSpacingNominal=6; ActualStopClearance=pending; NoActionReanchor=True");
        }
        if (codes != 3) throw Error("EP03 C1: falta uma das duas passagens proprias");
        if (added) return false; // Four added ground samples must pass the existing dry-ground guards on later Ticks.
        foreach (Operation operation in operations)
            if (!operation.CaronaNativeGoalPrepared || !operation.CaronaNativeGoalGroundMeasured) return false;
        return true;
    }

    // EP03 C1 first car only. Six measured seed normals choose a finite smaller mesh.
    // Seeds are not collision/floor approval; every selected footprint is measured afresh.
    private sealed class CaronaExitProbe
    {
        internal GTA.ShapeTestHandle Handle;
        internal GTA.Math.Vector3 From, To;
        internal int Index, IssuedAt;
        internal float Radius;
    }
    private sealed class CaronaExitSlice
    {
        internal GTA.Math.Vector3 From, To;
        internal readonly GTA.Math.Vector3[] Points = new GTA.Math.Vector3[6];
        internal readonly GTA.Math.Vector3[] Normals = new GTA.Math.Vector3[6];
        internal readonly float[] Ground = new float[6];
        internal int Read;
        internal float Lowest = float.MaxValue, Highest = float.MinValue;
    }
    private sealed class CaronaExitLane
    {
        internal Operation Operation;
        internal GTA.Math.Vector3 Start, End, Direction, Side, Minimum, Maximum;
        internal int StartedAt, LastTick = -1, Segments, Columns, Rows, PreparedSlices, GroundReadCount, Issued, Completed;
        internal bool Verified, InitialAssetsCompleted;
        internal long AssetElapsedAtTransition;
        internal System.Diagnostics.Stopwatch ProofTimer;
        internal float HalfX, HalfZ, Radius, SlopeLong, SlopeSide, PredictedSpread;
        internal CaronaExitSlice Seed;
        internal CaronaExitSlice[] Slices;
        internal readonly System.Collections.Generic.List<CaronaExitProbe> Pending = new System.Collections.Generic.List<CaronaExitProbe>();
    }
    private CaronaExitLane caronaFirstExitLane;

    private CaronaExitSlice BuildCaronaExitSlice(CaronaExitLane lane, int segments, int segment, float lateral, float radius)
    {
        CaronaExitSlice slice = new CaronaExitSlice();
        slice.From = lane.Start + (lane.End - lane.Start) * (segment / (float)segments) + lane.Side * lateral;
        slice.To = lane.Start + (lane.End - lane.Start) * ((segment + 1) / (float)segments) + lane.Side * lateral;
        for (int side = 0; side < 3; side++)
        {
            slice.Points[side] = slice.From - lane.Direction * radius + lane.Side * ((side - 1) * radius);
            slice.Points[side + 3] = slice.To + lane.Direction * radius + lane.Side * ((side - 1) * radius);
            Vector(slice.Points[side]); Vector(slice.Points[side + 3]);
        }
        return slice;
    }

    private bool ReadCaronaExitFloor(CaronaExitLane lane, CaronaExitSlice slice, string phase, int sliceIndex)
    {
        GTA.Math.Vector3 point = slice.Points[slice.Read];
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
        float ground, water; GTA.Math.Vector3 normal;
        if (!GTA.World.GetGroundHeightAndNormal(point + new GTA.Math.Vector3(0f, 0f, 2f), out ground, out normal)) return false;
        Vector(normal);
        bool wet = TryWater(point, out water) && water > ground + 0.1f;
        if (!Finite(ground) || wet || normal.Z < 0.98f || System.Math.Abs(ground - point.Z) > 0.35f)
        {
            Log("VEICULO_CARONA_SAIDA_CHAO_INVALIDO", "Phase=" + phase + "; Slice=" + sliceIndex + "; PointIndex=" + slice.Read +
                "; Point=" + point + "; Ground=" + Number(ground) + "; Normal=" + normal + "; WaterAboveGround=" + wet + "; NoSetter=True");
            throw Error("EP03 first extended lane has no dry near flat measured floor; no path invented");
        }
        slice.Ground[slice.Read] = ground; slice.Normals[slice.Read] = normal;
        slice.Lowest = System.Math.Min(slice.Lowest, ground); slice.Highest = System.Math.Max(slice.Highest, ground);
        slice.Read++;
        return true;
    }

    private static string CaronaExitMeasuredFloor(CaronaExitSlice slice)
    {
        System.Text.StringBuilder measured = new System.Text.StringBuilder();
        for (int item = 0; item < 6; item++) measured.Append("; P").Append(item).Append('=').Append(slice.Points[item]).Append("/Ground=")
            .Append(Number(slice.Ground[item])).Append("/Normal=").Append(slice.Normals[item]);
        return measured.ToString();
    }

    private void SelectCaronaExitMesh(CaronaExitLane lane)
    {
        if (lane.Seed.Read != 6 || lane.Pending.Count != 0 || lane.Issued != 0 || lane.Slices != null)
            throw Error("EP03 adaptive mesh requires six actual seed normals before any Native shape handle");
        foreach (GTA.Math.Vector3 normal in lane.Seed.Normals)
        {
            lane.SlopeLong = System.Math.Max(lane.SlopeLong, System.Math.Abs(Dot(normal, lane.Direction) / normal.Z));
            lane.SlopeSide = System.Math.Max(lane.SlopeSide, System.Math.Abs(Dot(normal, lane.Side) / normal.Z));
        }
        float length = Horizontal(lane.End - lane.Start), width = lane.Maximum.X - lane.Minimum.X + 0.1f;
        float height = lane.Maximum.Z - lane.Minimum.Z + 0.06f;
        int[] divisions = new int[] { 3, 4, 6 };
        float[] steps = new float[] { 0.6f, 0.5f, 0.4f, 0.3f, 0.2f };
        int bestCapsules = int.MaxValue, bestFloorCount = int.MaxValue;
        foreach (int columns in divisions) foreach (int rows in divisions) foreach (float step in steps)
        {
            int segments = (int)System.Math.Ceiling(length / step);
            if (segments < 1 || segments > 150) continue;
            float halfX = width / (2f * columns), halfZ = height / (2f * rows);
            float radius = (float)System.Math.Sqrt(halfX * halfX + halfZ * halfZ);
            float prediction = lane.SlopeLong * (length / segments + 2f * radius) + lane.SlopeSide * 2f * radius;
            int capsuleCount = segments * columns * rows, floorCount = segments * columns * 6;
            // A millimetre of planning margin is not a changed observed spread limit.
            // Native floor data and every capsule are still independently checked below.
            if (!Finite(prediction) || prediction > 0.049f || capsuleCount > 900 ||
                capsuleCount > bestCapsules || (capsuleCount == bestCapsules && floorCount >= bestFloorCount)) continue;
            lane.Segments = segments; lane.Columns = columns; lane.Rows = rows;
            lane.HalfX = halfX; lane.HalfZ = halfZ; lane.Radius = radius; lane.PredictedSpread = prediction;
            bestCapsules = capsuleCount; bestFloorCount = floorCount;
        }
        if (lane.Columns == 0)
            throw Error("EP03 no finite adaptive mesh within900 capsules and estimated49mm; keep5cm/global5s, no Native readiness");
        lane.Slices = new CaronaExitSlice[lane.Segments * lane.Columns];
        for (int index = 0; index < lane.Slices.Length; index++)
        {
            int segment = index / lane.Columns, column = index % lane.Columns;
            float lateral = lane.Minimum.X - 0.05f + (column + 0.5f) * lane.HalfX * 2f;
            lane.Slices[index] = BuildCaronaExitSlice(lane, lane.Segments, segment, lateral, lane.Radius);
        }
        // In the first successful Tick6 seeds leave6 attempts: one fine slice, at most Rows handles.
        int firstIssueCount = System.Math.Min(6, lane.Rows);
        int minimumFrames = 1 + (bestCapsules - firstIssueCount + 5) / 6;
        Log("VEICULO_CARONA_SAIDA_MALHA_ESCOLHIDA", "ActualSeedSamples6=True; SeedCollisionApproved=False; SeedFloorSpreadApproved=False;" +
            " SeedSpread=" + Number(lane.Seed.Highest - lane.Seed.Lowest) + "; SlopeLong=" + Number(lane.SlopeLong) +
            "; SlopeSide=" + Number(lane.SlopeSide) + "; Columns=" + lane.Columns + "; Rows=" + lane.Rows + "; Segments=" + lane.Segments +
            "; SegmentLength=" + Number(length / lane.Segments) + "; Radius=" + Number(lane.Radius) + "; EstimatedSpread=" + Number(lane.PredictedSpread) +
            "; FreshFineFloorSamples=" + bestFloorCount + "; Capsules=" + bestCapsules + "; MinimumIssueFrames=" + minimumFrames +
            "; MinimumFramesWithFinalResult=" + (minimumFrames + 1) +
            "; BudgetPhase=" + (ready ? "RESET_FRESH_PROOF" : "INITIAL_EXIT_PROOF_AFTER_ASSETS") +
            "; BudgetElapsedMs=" + CaronaExitBudgetElapsed(lane) + "; BudgetRemainingMs=" + System.Math.Max(0L, 5000L - CaronaExitBudgetElapsed(lane)) +
            "; AssetElapsedAtTransitionMs=" + lane.AssetElapsedAtTransition + "; LoadingElapsedMs=" + loading.ElapsedMilliseconds +
            "; AssetDeadlineMs=5000; SeparateExitProofDeadlineMs=5000; PendingMaximum=6; ActualFineSpreadMaximum=0.05; NoReadyFromPrediction=True; NoNativeHandlesBeforeChoice=True" +
            CaronaExitMeasuredFloor(lane.Seed));
    }

    private long CaronaExitBudgetElapsed(CaronaExitLane lane)
    {
        // Asset readiness and 792 bounded Native probes are different measured workloads.
        // loading keeps its original5s asset deadline; the proof has its own5s timer, never restarted per slice.
        return lane.ProofTimer.ElapsedMilliseconds;
    }

    private void RequireCaronaExitDeadline(CaronaExitLane lane)
    {
        if (lane.ProofTimer == null || lane.ProofTimer.ElapsedMilliseconds >= 5000 ||
            unchecked((uint)(GTA.Game.GameTime - lane.StartedAt)) > 5000u)
            throw Error("EP03 first exit road proof unavailable within its single bounded5s; no ARM or motion");
    }

    private bool CaronaExitProofBudgetActive()
    {
        CaronaExitLane lane = caronaFirstExitLane;
        if (sceneId != "EP03_C01" || ready || lane == null || !lane.InitialAssetsCompleted) return false;
        if (resources == null || !resources.IsReady || sampleIndex != samples.Count ||
            (driverRequested && !driverModel.IsLoaded) || lane.AssetElapsedAtTransition >= 5000)
            throw Error("EP03 asset phase invalid after exit proof started; no deferred streaming deadline or Ready");
        RequireCaronaExitDeadline(lane);
        return true;
    }

    private void FlushCaronaExitBatch(CaronaExitLane lane, System.Text.StringBuilder batch, int records)
    {
        if (records > 0) Log("VEICULO_CARONA_SAIDA_LOTE", "Tick=" + lane.LastTick + "; Records=" + records +
            "; CompletedTotal=" + lane.Completed + "; GroundTotal=" + lane.GroundReadCount + "; IssuedTotal=" + lane.Issued +
            "; Pending=" + lane.Pending.Count + "; BudgetPhase=" + (ready ? "RESET_FRESH_PROOF" : "INITIAL_EXIT_PROOF_AFTER_ASSETS") +
            "; BudgetElapsedMs=" + CaronaExitBudgetElapsed(lane) + "; FullObservedRecords=True;" + batch);
    }

    private bool PrepareCaronaFirstExitLane()
    {
        if (sceneId != "EP03_C01") return true;
        if (running || totalDuration != 13500 || vehicles.Count != 2 || operations.Count != 2)
            throw Error("EP03 first exit-lane proof only before countdown/action for exact own two cars");
        Operation first = null;
        foreach (Operation operation in operations)
            if (IsCaronaPhysicalPassBy(operation) && operation.Vehicle.Spec.Codigo == "CARRO_PASSAGEM_1") first = operation;
        if (first == null || !first.CaronaNativeGoalPrepared || !first.CaronaNativeGoalGroundMeasured)
            throw Error("EP03 first exit lane needs exact own measured extended Native endpoint");
        GTA.Vehicle vehicle = OwnVehicle(first.Vehicle);
        if (!vehicle.HasPhysics || !vehicle.IsCollisionEnabled || !vehicle.IsPositionFrozen || vehicle.Speed > 0.1f ||
            !vehicle.Model.IsLoaded || !vehicle.Model.IsCollisionLoaded)
            throw Error("EP03 exit-lane measurement requires frozen own loaded physical first car");
        if (caronaFirstExitLane == null)
        {
            GTA.Math.Vector3 minimum, maximum; vehicle.Model.GetDimensions(out minimum, out maximum); Vector(minimum); Vector(maximum);
            GTA.Math.Vector3 direction = Unit(first.Route[0] - first.Vehicle.Spawn);
            float width = maximum.X - minimum.X, height = maximum.Z - minimum.Z;
            float advance = Dot(first.CaronaNativeGoal - first.Route[0], direction);
            if (!Finite(width) || !Finite(height) || width <= 0f || width > 4f || height <= 0f || height > 3f ||
                System.Math.Abs(advance - maximum.Y - 12f) > 0.01f || minimum.Y >= 0f || maximum.Y <= 0f)
                throw Error("EP03 first exit lane dimensions/12m Native-only extension changed");
            long assetElapsed = ready ? 0L : loading.ElapsedMilliseconds;
            if (!ready && assetElapsed >= 5000)
                throw Error("EP03 original5s asset deadline expired before exit proof; no second streaming budget");
            CaronaExitLane lane = new CaronaExitLane(); lane.Operation = first;
            lane.Start = first.Route[0] + direction * (minimum.Y - 0.05f);
            lane.End = first.CaronaNativeGoal + direction * (maximum.Y + 0.05f);
            lane.Direction = direction; lane.Side = new GTA.Math.Vector3(direction.Y, -direction.X, 0f);
            lane.Minimum = minimum; lane.Maximum = maximum; lane.StartedAt = GTA.Game.GameTime;
            lane.InitialAssetsCompleted = !ready; lane.AssetElapsedAtTransition = assetElapsed;
            lane.ProofTimer = System.Diagnostics.Stopwatch.StartNew();
            float length = Horizontal(lane.End - lane.Start);
            if (!Finite(length) || length <= 0f || length > 30f || Horizontal(lane.End - anchor) > 150f)
                throw Error("EP03 first exit road proof outside finite relative30m/150m bounds");
            float seedHalfX = (width + 0.1f) / 6f, seedHalfZ = (height + 0.06f) / 6f;
            float seedRadius = (float)System.Math.Sqrt(seedHalfX * seedHalfX + seedHalfZ * seedHalfZ);
            int seedSegments = (int)System.Math.Ceiling(length / 0.6f);
            lane.Seed = BuildCaronaExitSlice(lane, seedSegments, 0, minimum.X - 0.05f + seedHalfX, seedRadius);
            caronaFirstExitLane = lane;
            Log("VEICULO_CARONA_SAIDA_PISTA_PREPARANDO", "LogicalPlane=" + first.Route[0] + "; NativeGoal=" + first.CaronaNativeGoal +
                "; Start=" + lane.Start + "; End=" + lane.End + "; ModelMin=" + minimum + "; ModelMax=" + maximum +
                "; Mesh=FiniteAdaptiveBeforeHandles; SeedActualNormals=6; SeedApproval=False; MaximumCapsules=900; Divisions=3,4,6;" +
                " SegmentQuantization=0.2,0.3,0.4,0.5,0.6; GroundAttemptsPerTick=12; NewHandlesPerTick=6; PendingMaximum=6;" +
                " AssetElapsedAtTransitionMs=" + lane.AssetElapsedAtTransition + "; AssetsCompleted=" + lane.InitialAssetsCompleted +
                "; AssetDeadlineMs=5000; SeparateExitProofDeadlineMs=5000; SameFrozenOwnResourcesRevalidatedEveryTick=True;" +
                " NativeOnlyAdvance=12; SourceAndCrossingUnchanged=True; RootCauseConfirmed=False; NoActionSetter=True");
        }
        CaronaExitLane proof = caronaFirstExitLane;
        if (proof.Operation != first) throw Error("EP03 first exit lane belongs to another own operation");
        RequireCaronaExitDeadline(proof);
        if (proof.Verified) return true;
        if (proof.LastTick == GTA.Game.GameTime) return false;
        proof.LastTick = GTA.Game.GameTime;
        RequireCaronaExitDeadline(proof);
        System.Text.StringBuilder batch = new System.Text.StringBuilder();
        int records = 0;
        try
        {
            // Poll before ground attempts, so a temporarily missing getter cannot starve existing requests.
            for (int index = proof.Pending.Count - 1; index >= 0; index--)
            {
                CaronaExitProbe pending = proof.Pending[index]; GTA.ShapeTestResult result;
                GTA.ShapeTestStatus status = pending.Handle.GetResult(out result);
                if (status == GTA.ShapeTestStatus.NotReady)
                {
                    if (unchecked((uint)(GTA.Game.GameTime - pending.IssuedAt)) > 1000u) throw Error("EP03 first exit capsule timed out");
                    continue;
                }
                if (status != GTA.ShapeTestStatus.Ready) throw Error("EP03 first exit capsule request invalid; no readiness inferred");
                Vector(result.HitPosition); Vector(result.SurfaceNormal);
                GTA.Entity physical; bool exists = result.DidHit && result.TryGetHitEntity(out physical);
                string evidence = "Index=" + pending.Index + "; Slice=" + (pending.Index / proof.Rows) + "; Row=" + (pending.Index % proof.Rows) +
                    "; From=" + pending.From + "; To=" + pending.To + "; Radius=" + Number(pending.Radius) + "; DidHit=" + result.DidHit +
                    "; Hit=" + result.HitPosition + "; Normal=" + result.SurfaceNormal + "; PhysicalReturned=" + exists +
                    "; Reader=API191; MapPhysicalModelRead=False; AnyHitFatal=True; Flags=MapVehiclesPedCapsulesRagdollsObjects; Options=0; PreparationOnly=True";
                proof.Pending.RemoveAt(index);
                if (result.DidHit)
                {
                    Log("VEICULO_CARONA_SAIDA_VOLUME", evidence);
                    throw Error("EP03 first exit road volume blocked; preserve collider, do not move or delete it");
                }
                batch.Append(" Record[").Append(records++).Append("]={Kind=VEICULO_CARONA_SAIDA_VOLUME; ").Append(evidence).Append('}');
                proof.Completed++;
            }
            int groundBudget = 12;
            if (proof.Slices == null)
            {
                while (groundBudget > 0 && proof.Seed.Read < 6)
                {
                    groundBudget--;
                    if (!ReadCaronaExitFloor(proof, proof.Seed, "NORMAL_SEED_NOT_APPROVAL", -1)) return false;
                }
                if (proof.Seed.Read != 6) return false;
                SelectCaronaExitMesh(proof);
            }
            while (groundBudget > 0 && proof.PreparedSlices < proof.Slices.Length)
            {
                groundBudget--;
                CaronaExitSlice slice = proof.Slices[proof.PreparedSlices];
                if (!ReadCaronaExitFloor(proof, slice, "FRESH_FINE_PROOF", proof.PreparedSlices)) break;
                proof.GroundReadCount++;
                if (slice.Read != 6) continue;
                string evidence = "Slice=" + proof.PreparedSlices + "; Segment=" + (proof.PreparedSlices / proof.Columns) +
                    "; Column=" + (proof.PreparedSlices % proof.Columns) + "; From=" + slice.From + "; To=" + slice.To +
                    "; Minimum=" + Number(slice.Lowest) + "; Maximum=" + Number(slice.Highest) + "; Spread=" + Number(slice.Highest - slice.Lowest) +
                    "; Samples6Actual=True; FootprintIncludesEndCaps=True; LocalOnly=True; BoundsReadFromOwnVehicle=True; PreparationOnly=True; NoSetter=True" +
                    CaronaExitMeasuredFloor(slice);
                if (slice.Highest - slice.Lowest > 0.05f)
                {
                    Log("VEICULO_CARONA_SAIDA_CHAO_FATIA", evidence);
                    throw Error("EP03 first exit local floor spread exceeds5cm; above-road capsule cannot prove an uneven low obstacle region");
                }
                batch.Append(" Record[").Append(records++).Append("]={Kind=VEICULO_CARONA_SAIDA_CHAO_FATIA; ").Append(evidence).Append('}');
                proof.PreparedSlices++;
            }
            RequireCaronaExitDeadline(proof);
            int newHandles = 0;
            while (newHandles < 6 && proof.Pending.Count < 6 && proof.Issued < proof.PreparedSlices * proof.Rows)
            {
                CaronaExitSlice slice = proof.Slices[proof.Issued / proof.Rows]; int row = proof.Issued % proof.Rows;
                float z = slice.Highest + proof.Radius + 0.03f + row * proof.HalfZ * 2f;
                GTA.Math.Vector3 from = slice.From, to = slice.To; from.Z = z; to.Z = z; Vector(from); Vector(to);
                CaronaExitProbe probe = new CaronaExitProbe { Index = proof.Issued, From = from, To = to, Radius = proof.Radius, IssuedAt = GTA.Game.GameTime };
                probe.Handle = GTA.ShapeTest.StartTestCapsule(from, to, proof.Radius,
                    GTA.IntersectFlags.Map | GTA.IntersectFlags.Vehicles | GTA.IntersectFlags.PedCapsules | GTA.IntersectFlags.Ragdolls | GTA.IntersectFlags.Objects,
                    vehicle, (GTA.ShapeTestOptions)0);
                if (probe.Handle.IsRequestFailed) throw Error("EP03 first exit capsule not created; no motion/readiness");
                proof.Pending.Add(probe); proof.Issued++; newHandles++;
            }
            if (proof.PreparedSlices != proof.Slices.Length || proof.GroundReadCount != proof.Slices.Length * 6 ||
                proof.Completed != proof.Slices.Length * proof.Rows || proof.Pending.Count != 0) return false;
            // Check after persisting the final batch, so its IO cannot silently overrun the proof into Ready.
            FlushCaronaExitBatch(proof, batch, records); batch.Length = 0; records = 0;
            RequireCaronaExitDeadline(proof);
            proof.Verified = true;
            Log("VEICULO_CARONA_SAIDA_PISTA_PRONTA", "ActualFineGroundSamples=" + proof.GroundReadCount + "; SeedSamples6ExcludedFromProof=True; Capsules=" + proof.Completed +
                "; Segments=" + proof.Segments + "; Columns=" + proof.Columns + "; Rows=" + proof.Rows + "; AnyHit=False; LocalFloorSpreadMaximum=0.05; NativeAdvance12=True;" +
                " LogicalPlanesF12F6=True; SceneMs=13500; SourceUnchanged=True; FreshPreparationOrReset=True; LowRoadContactBandNotObstacleProof=True; RootCauseConfirmed=False");
            return true;
        }
        finally
        {
            // Each real observation keeps its index/getters. One file append per Tick avoids hundreds of IO opens.
            FlushCaronaExitBatch(proof, batch, records);
        }
    }
    private void DrivePoint(Operation operation)
    {
        GTA.VehicleDrivingFlags flags = GTA.VehicleDrivingFlags.StopForVehicles | GTA.VehicleDrivingFlags.StopForPeds |
            GTA.VehicleDrivingFlags.SwerveAroundAllVehicles | GTA.VehicleDrivingFlags.SteerAroundObjects;
        if (operation.Vehicle.Spec.TrajetoDireto) flags |= GTA.VehicleDrivingFlags.ForceStraightLine;
        GTA.Math.Vector3 nativeGoal = operation.Route[operation.Point];
        if (IsCaronaNativeCenterGoal(operation))
        {
            if (operation.Point != 0 || !operation.CaronaNativeGoalPrepared || !operation.CaronaNativeGoalGroundMeasured)
                throw Error("EP03 C1: Nativegoal sem bounds e duas sondas de chao aprovadas");
            if (operation.Vehicle.Spec.Codigo == "CARRO_PASSAGEM_1" &&
                (caronaFirstExitLane == null || !caronaFirstExitLane.Verified || caronaFirstExitLane.Operation != operation))
                throw Error("EP03 first Native extension has no fresh actual ground/volume proof; no DriveTo");
            nativeGoal = operation.CaronaNativeGoal;
            Log("VEICULO_CARONA_NATIVE_GOAL_ENVIADO", "Veiculo=" + operation.Vehicle.Spec.Codigo +
                "; LogicalCenterGoal=" + operation.Route[0] + "; NativeGoal=" + nativeGoal +
                "; ArrivalXY=" + operation.Spec.RaioChegada + "; StopRange=" + operation.Spec.RaioParada.Value +
                "; DurationMs=" + operation.Spec.DuracaoMs + "; NoActionReanchor=True");
        }
        if (IsC1NativeCenterGoal(operation) || IsC12NativeCenterGoal(operation))
        {
            if (operation.Point != 0 || !operation.C1NativeGoalPrepared || !operation.C1NativeGoalGroundMeasured)
                throw Error("C1: Nativegoal sem bounds e duas sondas de chao aprovadas; nao iniciar direcao");
            nativeGoal = operation.C1NativeGoal;
            Log(IsC12NativeCenterGoal(operation) ? "VEICULO_C12_NATIVE_GOAL_ENVIADO" : "VEICULO_C1_NATIVE_GOAL_ENVIADO", "Veiculo=" + operation.Vehicle.Spec.Codigo +
                "; LogicalCenterGoal=" + operation.Route[operation.Point] + "; NativeGoal=" + nativeGoal +
                "; ArrivalXY=" + operation.Spec.RaioChegada + "; StopRange=" + operation.Spec.RaioParada.Value +
                "; DurationMs=" + operation.Spec.DuracaoMs + "; NoActionReanchor=True");
        }
        if (IsC9NativeCenterGoal(operation))
        {
            if (!operation.C9NativeGoalsPrepared || operation.C9NativeGoals.Count != 2 ||
                operation.C9NativeGoalsGroundMeasured.Count != 2 || operation.Point < 0 || operation.Point >= 2 ||
                !operation.C9NativeGoalsGroundMeasured[operation.Point])
                throw Error("C9: alvo Native sem frente e duas sondas reais aprovadas; nao iniciar direcao");
            nativeGoal = operation.C9NativeGoals[operation.Point];
            Log("VEICULO_C9_NATIVE_GOAL_ENVIADO", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; RouteIndex=" + operation.Point +
                "; LogicalCenterGoal=" + operation.Route[operation.Point] + "; NativeGoal=" + nativeGoal +
                "; ArrivalXY=" + operation.Spec.RaioChegada + "; StopRange=" + operation.Spec.RaioParada.Value +
                "; DurationMs=" + operation.Spec.DuracaoMs + "; SceneMs=19000; NoActionReanchor=True");
        }
        if (IsC3NativeCenterGoal(operation))
        {
            if (operation.Point != 0 || !operation.C3NativeGoalPrepared || !operation.C3NativeGoalGroundMeasured)
                throw Error("C3: alvo Native sem bounds/chao preparado; nao iniciar direcao");
            nativeGoal = operation.C3NativeGoal;
            Log("VEICULO_C3_NATIVE_GOAL_ENVIADO", "Veiculo=" + operation.Vehicle.Spec.Codigo +
                "; LogicalCenterGoal=" + operation.Route[operation.Point] + "; NativeGoal=" + nativeGoal +
                "; ArrivalXY=" + operation.Spec.RaioChegada + "; StopRange=" + operation.Spec.RaioParada.Value);
        }
        operation.Actor.Ped.Task.DriveTo(OwnVehicle(operation.Vehicle), nativeGoal, operation.Spec.Velocidade,
            flags, operation.Spec.RaioParada ?? operation.Spec.RaioChegada);
    }

    // The two owned SuperD/Rich scenes, after final logical center arrival.
    // No future exit in either reviewed source; no change to other vehicles or entries.
    private bool LuxuryDriverArrivalHoldScope(Operation operation)
    {
        if (IsC12NativeCenterGoal(operation)) return totalDuration == 8000 && operation.Spec.InicioMs == 0 && operation.Spec.DuracaoMs == 4000;
        if (!LuxuryDriveDiagnosticScope(operation) || operation.Actor.Extra || operation.Spec.Tipo != "dirigir" ||
            operation.Vehicle.Spec.Tipo != "CARRO" || operation.Spec.Assento != -1) return false;
        bool duration = sceneId == "EP15_C01" ? totalDuration == 7000 && operation.Spec.InicioMs == 0 && operation.Spec.DuracaoMs == 7000 :
            totalDuration == 19000 && operation.Spec.InicioMs == 5000 && operation.Spec.DuracaoMs == 14000;
        if (!duration) return false;
        foreach (Operation other in operations)
            if (other.Vehicle == operation.Vehicle && other.Spec.Tipo == "sair" && other.Spec.InicioMs >= operation.Spec.InicioMs)
                return false;
        return true;
    }

    // C1/C9 only: keep exact own driver seat until the full7s/19s scene end.
    // Getter failure cancels rather than repairing/moving/reseating during filming.
    private void RequireLuxuryOwnDriverArrivalHold(Operation operation, GTA.Vehicle vehicle)
    {
        if (IsC12NativeCenterGoal(operation)) { RequireC12OwnDriverArrivalHold(operation, vehicle); return; }
        if (!LuxuryDriverArrivalHoldScope(operation) || !operation.LuxuryOwnDriverArrivalHold || operation.Route.Count == 0 ||
            operation.Point != operation.Route.Count || (sceneId == "EP15_C01" &&
            (!operation.C1NativeGoalPrepared || !operation.C1NativeGoalGroundMeasured)) ||
            (sceneId == "EP15_C09" && (!operation.C9NativeGoalsPrepared || operation.C9NativeGoals.Count != 2 ||
            operation.C9NativeGoalsGroundMeasured.Count != 2 || !operation.C9NativeGoalsGroundMeasured[0] || !operation.C9NativeGoalsGroundMeasured[1])))
            throw Error("C1/C9: driver arrival hold requires exact reviewed own final arrival");
        RequireActor(operation.Actor);
        GTA.Math.Vector3 position = vehicle.Position, velocity = vehicle.Velocity;
        Vector(position); Vector(velocity);
        if (!operation.Actor.Ped.IsInVehicle(vehicle) || !InSeat(operation.Actor, vehicle, -1) ||
            !vehicle.HasPhysics || !vehicle.IsPositionFrozen || vehicle.Speed > 0.1f || velocity.Length() > 0.1f ||
            Horizontal(position - operation.Route[operation.Route.Count-1]) > operation.Spec.RaioChegada ||
            System.Math.Abs(position.Z - operation.Route[operation.Route.Count-1].Z) > 3f)
            throw Error("C1/C9: arrived own vehicle/driver seat/center lost during full scene hold; no filmed repair");
    }

    // Only the exact EP03 two-car source. Read actual settled transforms/bounds/floor in PREP/reset.
    // Source F-18 is a hypothesis; positive nominal spacing never substitutes these Native getters.
    private bool PrepareCaronaStartingClearance()
    {
        if (sceneId != "EP03_C01") return true;
        if (running || totalDuration != 13500 || vehicles.Count != 2 || operations.Count != 2)
            throw Error("EP03 C1 start clearance only before countdown/action for exact two cars");
        VehicleState first = null, second = null;
        foreach (Operation operation in operations)
        {
            if (!IsCaronaNativeCenterGoal(operation) || !operation.CaronaNativeGoalPrepared ||
                !operation.CaronaNativeGoalGroundMeasured)
                throw Error("EP03 C1 starting clearance needs both measured own goals first");
            if (operation.Vehicle.Spec.Codigo == "CARRO_PASSAGEM_1") first = operation.Vehicle;
            else second = operation.Vehicle;
        }
        if (first == null || second == null ||
            first.Spec.PosicaoRelativa != new GTA.Math.Vector3(4f, -12f, 0f) ||
            second.Spec.PosicaoRelativa != new GTA.Math.Vector3(4f, -18f, 0f))
            throw Error("EP03 C1 requires exact firstF-12/secondF-18; no alternate lane or arbitrary spawn");
        float rearProjection = float.MaxValue, frontProjection = float.MinValue;
        foreach (VehicleState state in new VehicleState[] { first, second })
        {
            GTA.Vehicle vehicle = OwnVehicle(state);
            if (!vehicle.Model.IsLoaded || !vehicle.Model.IsCollisionLoaded || !vehicle.HasPhysics ||
                !vehicle.IsCollisionEnabled || !vehicle.IsPositionFrozen || vehicle.Speed > 0.1f)
                throw Error("EP03 C1 start footprint requires actual own loaded/collision/physical/frozen stopped car");
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, vehicle.Handle)) return false;
            GTA.Math.Vector3 minimum, maximum; vehicle.Model.GetDimensions(out minimum, out maximum);
            Vector(minimum); Vector(maximum);
            if (minimum.X >= maximum.X || minimum.Y >= maximum.Y || minimum.Z >= maximum.Z)
                throw Error("EP03 C1 actual footprint has invalid own bounds");
            foreach (float x in new float[] { minimum.X, maximum.X })
                foreach (float y in new float[] { minimum.Y, maximum.Y })
                    foreach (float z in new float[] { minimum.Z, maximum.Z })
                    {
                        GTA.Math.Vector3 corner = vehicle.GetOffsetPosition(new GTA.Math.Vector3(x, y, z)); Vector(corner);
                        float projection = Dot(corner - anchor, forward);
                        if (!Finite(projection)) throw Error("EP03 C1 starting corner projection is nonfinite");
                        if (state == first) rearProjection = System.Math.Min(rearProjection, projection);
                        else frontProjection = System.Math.Max(frontProjection, projection);
                    }
            // Five actual floor results beneath the settled bbox footprint. No actor/vehicle setters.
            GTA.Math.Vector3[] points = new GTA.Math.Vector3[] {
                new GTA.Math.Vector3((minimum.X + maximum.X) * 0.5f, (minimum.Y + maximum.Y) * 0.5f, minimum.Z),
                new GTA.Math.Vector3(minimum.X, minimum.Y, minimum.Z), new GTA.Math.Vector3(minimum.X, maximum.Y, minimum.Z),
                new GTA.Math.Vector3(maximum.X, minimum.Y, minimum.Z), new GTA.Math.Vector3(maximum.X, maximum.Y, minimum.Z) };
            for (int i = 0; i < points.Length; i++)
            {
                GTA.Math.Vector3 point = vehicle.GetOffsetPosition(points[i]); Vector(point);
                float ground, water; GTA.Math.Vector3 normal;
                bool found = GTA.World.GetGroundHeightAndNormal(point + new GTA.Math.Vector3(0f, 0f, 2f), out ground, out normal);
                if (!found) { if (ready) throw Error("EP03 C1 measured starting floor disappeared during reset"); return false; }
                Vector(normal);
                bool wet = TryWater(point, out water) && water > ground + 0.1f;
                float gap = point.Z - ground;
                Log("VEICULO_CARONA_START_FLOOR", "Veiculo=" + state.Spec.Codigo + "; Index=" + i +
                    "; ActualBBoxBottom=" + point + "; Ground=" + ground + "; Normal=" + normal +
                    "; Gap=" + gap + "; FloorFound=True; WaterAboveGround=" + wet +
                    "; OwnCollider=True; PreparedOnly=True; WheelSoleContactProven=False; SourceF18NativeApproval=False");
                if (!Finite(ground) || !Finite(gap) || wet || normal.Z <= 0f || System.Math.Abs(gap) > 0.35f)
                    throw Error("EP03 C1 starting own bbox floor is not dry/near actual settled bottom within0.35; choose a real physical lane, no filmed repair");
            }
        }
        float clearance = rearProjection - frontProjection;
        Log("VEICULO_CARONA_START_BOUNDS", "FirstRearProjection=" + rearProjection +
            "; SecondFrontProjection=" + frontProjection + "; ActualGap=" + clearance +
            "; Corners=8perOwnCar; FiveFloorResultsEach=True; NoVehicleSetters=True; SceneMs=13500; SourceF18NativeApproval=False");
        if (!Finite(clearance) || clearance <= 0f)
            throw Error("EP03 C1 two actual own vehicle bounding volumes overlap before action; nominal gap not accepted");
        if (caronaFirstExitLane == null) throw Error("EP03 first exit proof required before starting clearance readiness");
        RequireCaronaExitDeadline(caronaFirstExitLane); // Includes the ten fresh starting floors/IO before initial or reset Ready.
        return true;
    }

    // EP03 literal: both cars pass the waving actor; neither pickup nor precise stop is scripted.
    // Keep both original Native tasks/deadlines7s/8.5s and driver checks throughfull13.5s.
    private bool IsCaronaPhysicalPassBy(Operation operation)
    {
        return IsCaronaNativeCenterGoal(operation) && totalDuration == 13500 &&
            ((operation.Vehicle.Spec.Codigo == "CARRO_PASSAGEM_1" && operation.Vehicle.Spec.Modelo == "tailgater" &&
                operation.Spec.InicioMs == 0 && operation.Spec.DuracaoMs == 7000) ||
             (operation.Vehicle.Spec.Codigo == "CARRO_PASSAGEM_2" && operation.Vehicle.Spec.Modelo == "superd" &&
                operation.Spec.InicioMs == 5000 && operation.Spec.DuracaoMs == 8500));
    }

    private float RequireCaronaPassGeometry(Operation operation, GTA.Vehicle vehicle)
    {
        if (!IsCaronaPhysicalPassBy(operation) || !operation.CaronaNativeGoalPrepared ||
            !operation.CaronaNativeGoalGroundMeasured || operation.Route.Count != 1 || operation.Point != 0)
            throw Error("EP03 C1: each passing car requires its measured own route");
        RequireActor(operation.Actor);
        GTA.Math.Vector3 position = vehicle.Position, velocity = vehicle.Velocity;
        Vector(position); Vector(velocity);
        GTA.Math.Vector3 direction = Unit(operation.Route[0] - operation.Vehicle.Spawn);
        GTA.Math.Vector3 offset = position - operation.Route[0];
        float projection = Dot(offset, direction);
        float lateral = Horizontal(offset - direction * projection);
        if (!operation.Actor.Ped.IsInVehicle(vehicle) || !InSeat(operation.Actor, vehicle, -1) ||
            !vehicle.HasPhysics || lateral > operation.Spec.RaioChegada || System.Math.Abs(offset.Z) > 3f)
        {
            // Read after the unchanged predicate, before its unchanged throw. No state correction.
            Log("VEICULO_CARONA_GEOMETRIA_FALHA", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; TempoMs=" + lastElapsed +
                "; PassObserved=" + operation.CaronaPassByObserved + "; OperationDone=" + operation.Done +
                "; ActorHandle=" + operation.Actor.Handle + "; VehicleHandle=" + vehicle.Handle +
                "; ActualPosition=" + position + "; LogicalPlane=" + operation.Route[0] +
                "; NativeGoal=" + operation.CaronaNativeGoal + "; GroundMeasured=" + operation.CaronaNativeGoalGroundMeasured +
                "; Projection=" + Number(projection) + "; PreviousProjection=" + Number(operation.CaronaPassByPreviousProjection) +
                "; Lateral=" + Number(lateral) + "; LateralMaximum=" + Number(operation.Spec.RaioChegada) +
                "; DeltaZ=" + Number(System.Math.Abs(offset.Z)) + "; DeltaZMaximum=3; Velocity=" + velocity +
                "; ForwardSpeed=" + Number(Dot(velocity,direction)) + "; OwnVehicle=" + operation.Actor.Ped.IsInVehicle(vehicle) +
                "; OwnDriverSeat=" + InSeat(operation.Actor,vehicle,-1) + "; HasPhysics=" + vehicle.HasPhysics +
                "; Collision=" + vehicle.IsCollisionEnabled + "; Frozen=" + vehicle.IsPositionFrozen +
                "; HasCollidedSticky=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING,vehicle.Handle) +
                "; CollisionOtherIdentityProven=False; SecondaryGetterRead=True; OriginalPredicateUnchanged=True; NoSetter=True");
            LogOperationState("VEICULO_CARONA_GEOMETRIA_FALHA_TASK",operation,vehicle,lastElapsed);
            try
            {
                foreach (VehicleState state in vehicles)
                {
                    GTA.Vehicle own=OwnVehicle(state); GTA.Math.Vector3 min,max;
                    own.Model.GetDimensions(out min,out max);Vector(min);Vector(max);
                    float near=float.MaxValue,far=float.MinValue;
                    foreach(float x in new float[]{min.X,max.X}) foreach(float y in new float[]{min.Y,max.Y}) foreach(float z in new float[]{min.Z,max.Z})
                    {GTA.Math.Vector3 point=own.GetOffsetPosition(new GTA.Math.Vector3(x,y,z));Vector(point);float v=Dot(point-anchor,forward);near=System.Math.Min(near,v);far=System.Math.Max(far,v);}
                    Log("VEICULO_CARONA_GEOMETRIA_FALHA_OWN_BOUNDS", "FailedVehicle=" + operation.Vehicle.Spec.Codigo +
                        "; Vehicle=" + state.Spec.Codigo + "; Handle=" + own.Handle + "; Pos=" + own.Position + "; Heading=" + Number(own.Heading) +
                        "; Velocity=" + own.Velocity + "; Speed=" + Number(own.Speed) + "; Engine=" + own.IsEngineRunning +
                        "; ModelMin=" + min + "; ModelMax=" + max + "; NativeEightCornersProjectionMin=" + Number(near) +
                        "; NativeEightCornersProjectionMax=" + Number(far) + "; DifferenceIsBoundsOnlyNotCollision=True; OwnPhysicalVehicleOnly=True; NoWorldPool=True; NoSetter=True");
                }
            }
            catch(System.Exception diagnosticError){Log("VEICULO_CARONA_GEOMETRIA_DIAG_ERRO",diagnosticError.Message+"; OriginalGuardStillThrows=True");}
            throw Error("EP03 C1: passing driver/physical lane/grounded route lost; no filmed repair");
        }
        return projection;
    }

    private void ObserveCaronaPhysicalPassBy(Operation operation, GTA.Vehicle vehicle, int elapsed)
    {
        float projection = RequireCaronaPassGeometry(operation, vehicle);
        if (operation.CaronaPassByObserved)
        {
            RequireCaronaPassByObserved(operation, vehicle);
            return;
        }
        if (projection >= 0f)
        {
            GTA.Math.Vector3 direction = Unit(operation.Route[0] - operation.Vehicle.Spawn);
            GTA.Math.Vector3 velocity = vehicle.Velocity; Vector(velocity);
            float forwardSpeed = Dot(velocity, direction);
            if (!operation.CaronaPassByPreviousSample || operation.CaronaPassByPreviousProjection >= 0f ||
                forwardSpeed <= 0.5f || vehicle.IsPositionFrozen)
                throw Error("EP03 C1: crossing needs a previous real point behind and physical forward motion");
            operation.CaronaPassByObserved = true;
            Log("VEICULO_CARONA_PASSAGEM_FISICA", "Veiculo=" + operation.Vehicle.Spec.Codigo + "; TempoMs=" + elapsed +
                "; PreviousProjection=" + operation.CaronaPassByPreviousProjection + "; Projection=" + projection +
                "; ForwardSpeed=" + forwardSpeed + "; LogicalPlane=" + operation.Route[0] +
                "; OwnDriverSeat=-1; LaneXY=" + operation.Spec.RaioChegada + "; DeltaZMax=3" +
                "; LogicalGround=" + operation.Route[0].Z + "; NativeGoalGroundMeasured=" + operation.CaronaNativeGoalGroundMeasured +
                "; NativeTaskUnchanged=True; VehicleFreeze=False; ClearPedTask=False; OperationStartMs=" + operation.Spec.InicioMs +
                "; OperationMs=" + operation.Spec.DuracaoMs + "; SceneMs=13500");
        }
        operation.CaronaPassByPreviousProjection = projection;
        operation.CaronaPassByPreviousSample = true;
    }

    private void RequireCaronaPassByObserved(Operation operation, GTA.Vehicle vehicle)
    {
        if (!operation.CaronaPassByObserved || !operation.CaronaPassByPreviousSample ||
            RequireCaronaPassGeometry(operation, vehicle) < 0f)
            throw Error("EP03 C1: physical passage or own driver lost before full13.5s");
    }

    // Historical precise-stop helper retained; both exact C1 cars use physical passage before this branch.
    // Only stop the owned vehicle once; no ClearAllImmediately, actor freeze, Warp or seat reassignment.
    private void RequireCaronaOwnDriverArrivalHold(Operation operation, GTA.Vehicle vehicle)
    {
        if (!IsCaronaNativeCenterGoal(operation) || !operation.CaronaOwnDriverArrivalHold ||
            !operation.CaronaNativeGoalPrepared || !operation.CaronaNativeGoalGroundMeasured ||
            totalDuration != 13500 || operation.Route.Count != 1 || operation.Point != 1)
            throw Error("EP03 C1: arrived extra driver requires exact reviewed own arrival");
        RequireActor(operation.Actor);
        GTA.Math.Vector3 position = vehicle.Position, velocity = vehicle.Velocity; Vector(position); Vector(velocity);
        if (!operation.Actor.Ped.IsInVehicle(vehicle) || !InSeat(operation.Actor, vehicle, -1) ||
            !vehicle.HasPhysics || !vehicle.IsPositionFrozen || vehicle.Speed > 0.1f || velocity.Length() > 0.1f ||
            Horizontal(position - operation.Route[0]) > operation.Spec.RaioChegada ||
            System.Math.Abs(position.Z - operation.Route[0].Z) > 3f)
            throw Error("EP03 C1: own extra driver/physical stop lost beforefull13.5s; no filmed repair");
    }

    public void Finish()
    {
        RequireReady(); foreach (Operation operation in operations) if (!operation.Done) throw Error("não marcar conclusão antes das ações físicas");
        if (sceneId == "EP03_C01")
        {
            if (lastElapsed < 13500 || operations.Count != 2) throw Error("EP03 C1: full13.5s before finish");
            foreach (Operation operation in operations)
            {
                if (!IsCaronaPhysicalPassBy(operation)) throw Error("EP03 C1: unexpected operation before physical passage finish");
                RequireCaronaPassByObserved(operation, OwnVehicle(operation.Vehicle));
            }
        }
        if (sceneId == "EP15_C01" || sceneId == "EP15_C09")
        {
            bool held = false;
            foreach (Operation operation in operations) if (LuxuryDriverArrivalHoldScope(operation))
            { RequireLuxuryOwnDriverArrivalHold(operation, OwnVehicle(operation.Vehicle)); held = true; }
            if (!held || lastElapsed < totalDuration) throw Error("C1/C9: do not finish before full scene driver hold");
        }
        if (sceneId == "EP12_C01")
        {
            if (lastElapsed < 8000 || operations.Count != 2 || !operations[1].Done)
                throw Error("EP12 C1: physical full drive4+exit4 required before vehicle Finish");
            RequireC12OwnDriverArrivalHold(operations[0], OwnVehicle(operations[0].Vehicle));
            Log("VEICULO_C12_CARRO_SAIDA_COMPLETOS", "DriverOwnSeat=-1; RichPassengerPhysicalExit=True; VehicleActionsMs=8000; SceneMs=12000; CameraBaseUntouched=True; SecurityDoorProven=False; NoWarpOrReseat=True");
        }
        foreach (VehicleState vehicle in vehicles) { GTA.Vehicle entity = OwnVehicle(vehicle); entity.Velocity = GTA.Math.Vector3.Zero; entity.IsPositionFrozen = true; }
        running = false; finished = true;
    }

    // Fora da captura. Warp apenas aqui/Reset, conservando referências para tentar no próximo Tick.
    public bool ReleaseActorsForReset()
    {
        Active(); running = false; resetWaitingSeats = false; resetSettlingCoach = false; coachResetLoading = null; bool pending = false;
        // Conferir todos os veículos antes de alterar qualquer ocupante.
        foreach (VehicleState vehicle in vehicles)
            if (!System.Object.ReferenceEquals(vehicle.Vehicle, null) && vehicle.Vehicle.Exists()) CheckOccupants(vehicle);
        foreach (VehicleState vehicle in vehicles)
        {
            if (System.Object.ReferenceEquals(vehicle.Vehicle, null) || !vehicle.Vehicle.Exists()) continue;
            CheckOccupants(vehicle); GTA.Vehicle entity = OwnVehicle(vehicle);
            entity.IsPositionFrozen = true; entity.Velocity = GTA.Math.Vector3.Zero;
            foreach (ActorState actor in actors.Values)
            {
                if (!StillOwn(actor)) continue;
                if (actor.Ped.IsInVehicle(entity))
                {
                    if (actor.LeavingVehicle != entity.Handle)
                    {
                        actor.Ped.IsPositionFrozen = false; actor.Ped.Task.ClearAllImmediately();
                        actor.Ped.Task.WarpOutOfVehicle(entity); actor.LeavingVehicle = entity.Handle;
                    }
                    if (actor.Ped.IsInVehicle(entity)) pending = true;
                }
                if (!actor.Ped.IsInVehicle())
                {
                    actor.LeavingVehicle = 0;
                    actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
                }
            }
            if (entity.Occupants.Length != 0) pending = true;
        }
        return !pending;
    }

    public void Cleanup()
    {
        if (disposed) return; ready = false; running = false; resetDone = false;
        if (!ReleaseActorsForReset()) throw Error("saída dos assentos pendente; preserve o helper para o próximo frame");
        foreach (ActorState actor in actors.Values)
        {
            if (actor.Handle == 0 || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, actor.Handle)) continue;
            if (!StillOwn(actor)) throw Error("identidade de ator/motorista mudou; não restaurar nem excluir entidade alheia");
            if (actor.Ped.IsInVehicle()) throw Error("ator em veículo alheio; não mover nem excluir");
            if (actor.Extra)
            {
                actor.Ped.Delete();
                if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, actor.Handle))
                { actor.Ped = GTA.Entity.FromHandle(actor.Handle) as GTA.Ped; throw Error("remoção do motorista próprio falhou; preservar identidade para tentar novamente"); }
            }
            else
            {
                actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
                if (actor.MarkSaved && !actor.Ped.IsDead) { actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading; }
                actor.Ped.IsPositionFrozen = actor.Frozen; actor.Ped.IsInvincible = actor.Invincible; actor.Ped.BlockPermanentEvents = actor.Events;
                actor.Ped.IsCollisionEnabled = actor.Collision;
                if (actor.AvoidAutoHelmet) actor.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DisableAutoEquipHelmetsInBikes, actor.AutoHelmet);
            }
        }
        if (resources != null) { resources.Cleanup(); resources.Dispose(); resources = null; }
        if (driverRequested && !driverWasLoaded) driverModel.MarkAsNoLongerNeeded();
        driverRequested = false; actors.Clear(); vehicles.Clear(); operations.Clear(); samples.Clear();
        prepared = false; failed = false; finished = false; totalDuration = 0; loading = null;
        caronaFirstExitLane = null;
        Log("VEICULOS_LIMPOS", "Atores próprios restaurados; somente veículos/motoristas criados pelo helper removidos");
    }
    public void Dispose() { Cleanup(); disposed = true; }

    private ActorState RequireRosterActor(System.Collections.Generic.Dictionary<string, GTA.Ped> roster, string code)
    {
        GTA.Ped ped;
        if (string.IsNullOrEmpty(code) || !roster.TryGetValue(code, out ped) || System.Object.ReferenceEquals(ped, null) || !ped.Exists() ||
            ped.IsPlayer || ped.IsDead || ped.IsInVehicle() || !ped.IsPersistent) throw Error("ator precisa vir do elenco próprio, vivo/a pé/persistente e diferente do jogador");
        ActorState found;
        if (actors.TryGetValue(code, out found)) { RequireActor(found); return found; }
        foreach (ActorState existing in actors.Values)
            if (StillOwn(existing) && existing.Handle == ped.Handle) throw Error("o mesmo ator foi recebido com dois códigos");
        found = new ActorState { Code = code, Ped = ped, Handle = ped.Handle, Model = ped.Model.Hash, Address = ped.MemoryAddress,
            Frozen = ped.IsPositionFrozen, Invincible = ped.IsInvincible, Events = ped.BlockPermanentEvents, Collision = ped.IsCollisionEnabled,
            AutoHelmet = ped.GetConfigFlag(GTA.PedConfigFlagToggles.DisableAutoEquipHelmetsInBikes) };
        actors.Add(code, found); return found;
    }
    private void CheckOccupants(VehicleState vehicle)
    {
        foreach (GTA.Ped ped in OwnVehicle(vehicle).Occupants)
        {
            bool own = false;
            foreach (ActorState actor in actors.Values) if (StillOwn(actor) && ped.Handle == actor.Handle && ped.MemoryAddress == actor.Address) { own = true; break; }
            if (!own) throw Error("veículo próprio tem ocupante alheio/jogador; preservar veículo e não remover ninguém");
        }
    }
    private GTA.Vehicle OwnVehicle(VehicleState vehicle) { vehicle.Vehicle = resources.GetVehicle(vehicle.Spec.Codigo); return vehicle.Vehicle; }
    private static bool StillOwn(ActorState actor)
    { return actor != null && !System.Object.ReferenceEquals(actor.Ped, null) && actor.Ped.Exists() && actor.Ped.Handle == actor.Handle &&
        actor.Ped.MemoryAddress == actor.Address && actor.Ped.Model.Hash == actor.Model && !actor.Ped.IsPlayer; }
    private static void RequireActor(ActorState actor) { if (!StillOwn(actor) || actor.Ped.IsDead) throw Error("identidade/vida de ator ou motorista próprio mudou"); }
    private static bool InSeat(ActorState actor, GTA.Vehicle vehicle, int seat)
    { GTA.Ped occupant = vehicle.GetPedOnSeat((GTA.VehicleSeat)seat); return !System.Object.ReferenceEquals(occupant, null) && occupant.Exists() &&
        StillOwn(actor) && occupant.Handle == actor.Handle && occupant.MemoryAddress == actor.Address; }
    private static void CheckSeat(GTA.Vehicle vehicle, int seat)
    { if (seat < -1 || seat >= vehicle.PassengerCapacity) throw Error("o assento solicitado não existe no modelo real"); }
    private GTA.Math.Vector3 Resolve(GTA.Math.Vector3 offset) { return anchor + right * offset.X + forward * offset.Y + new GTA.Math.Vector3(0f, 0f, offset.Z); }
    private static bool AllowedModel(string type, string model)
    {
        return type == "MOTO" && (model == "faggio" || model == "bati" || model == "sanchez") ||
            type == "CARRO" && (model == "tailgater" || model == "superd") ||
            type == "ONIBUS" && (model == "bus" || model == "coach") || type == "CAMINHAO" && (model == "packer" || model == "hauler");
    }
    public static bool IsCatalogModel(string type, string model) { return AllowedModel(type, model); }
    private static bool Overlap(VehicleAction a, VehicleAction b) { return a.InicioMs < b.InicioMs + b.DuracaoMs && b.InicioMs < a.InicioMs + a.DuracaoMs; }
    private static bool Code(string value) { return value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Z][A-Z0-9_]{0,47}$"); }
    private static float Horizontal(GTA.Math.Vector3 v) { return (float)System.Math.Sqrt(v.X * v.X + v.Y * v.Y); }
    private static bool FiniteVector(GTA.Math.Vector3 v)
    { return !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z); }
    private static float Dot(GTA.Math.Vector3 a, GTA.Math.Vector3 b) { return a.X * b.X + a.Y * b.Y; }
    private static GTA.Math.Vector3 Unit(GTA.Math.Vector3 v) { if (Horizontal(v) < 0.01f) throw Error("eixo horizontal nulo"); return new GTA.Math.Vector3(v.X, v.Y, 0f) / Horizontal(v); }
    private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    private static void Vector(GTA.Math.Vector3 v) { if (!Finite(v.X) || !Finite(v.Y) || !Finite(v.Z)) throw Error("vetores/marcas devem conter valores finitos"); }
    private static bool TryWater(GTA.Math.Vector3 point, out float height)
    { using (var arg = new GTA.Native.OutputArgument()) { bool result = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_WATER_HEIGHT, point.X, point.Y, point.Z + 30f, arg); height = result ? arg.GetResult<float>() : 0f; return result && Finite(height); } }
    private void RequireReady() { Active(); if (!IsReady) throw Error("aguarde modelos/chão/assentos de uma preparação válida"); }
    private void Active() { if (disposed) throw Error("helper encerrado"); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Veículos da série: " + text + "."); }
    private void Log(string code, string message) { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
}

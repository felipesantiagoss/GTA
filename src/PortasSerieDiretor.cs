// Portas reais e passagens medidas; só altera o alvo identificado e restaura seu estado.
public sealed class PortasSerieDiretor : System.IDisposable
{
    public sealed class DoorSpec
    {
        public string Tipo, Ator, Recurso, Dicionario, Clipe, OrigemArquivo, OrigemSha256;
        public int InicioMs, DuracaoMs, Batidas, OrigemLinha;
        public GTA.Math.Vector3 DestinoRelativo;
        public float Velocidade = 1f;
    }
    private sealed class DoorState
    {
        internal GTA.Prop Prop;
        internal int Handle, Model, SystemId, OriginalState;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Position, Rotation, Point, Normal;
        internal float OriginalRatio;
        internal bool Added, OriginalSaved;
    }
    private sealed class Operation
    {
        internal DoorSpec Spec;
        internal GTA.Ped Actor;
        internal int Handle, Model, Ray, Sample, StartedAt, KnockCount, LastKnockAt, LastTick = -1;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Destination;
        internal DoorState Door;
        internal GTA.Prop BankSelectedProp;
        internal int BankSelectedHandle;
        internal System.IntPtr BankSelectedAddress;
        internal bool Ready, Started, Done, Approached;
        internal float LastDistance, MinimumDistance, CyclePeakDistance;
        internal int NextKnockLogMs;
        internal bool BankOpening, C7Opening, C7ForceSettling;
        internal int C7ForceTick, C7ForceFrames;
        internal int NextOpenDiagnosticMs, OpeningStartState;
        internal float OpeningStartRatio;
        internal GTA.Math.Vector3 OpeningStartPosition, OpeningStartRotation;
    }
    private readonly System.Action<string, string> log;
    private readonly string propListFile;
    private readonly System.Collections.Generic.List<Operation> operations = new System.Collections.Generic.List<Operation>();
    private readonly System.Collections.Generic.Dictionary<int, DoorState> doors = new System.Collections.Generic.Dictionary<int, DoorState>();
    private readonly System.Collections.Generic.Dictionary<int, string> doorModels = new System.Collections.Generic.Dictionary<int, string>();
    private readonly System.Collections.Generic.Dictionary<string, bool> dictionaries = new System.Collections.Generic.Dictionary<string, bool>(System.StringComparer.Ordinal);
    private GTA.Math.Vector3 origin, right, forward;
    private System.Diagnostics.Stopwatch loading;
    private bool prepared, ready, resetDone, disposed, failed;
    private int elapsedBefore = -1;
    private int rayOptions = 7;
    private string preparedSceneId;
    private readonly System.Collections.Generic.HashSet<string> loadingSteps = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

    public PortasSerieDiretor(System.Action<string, string> logger, string installedPropList)
    { log = logger; propListFile = installedPropList; }
    public bool IsReady { get { return ready && prepared && !failed && !disposed; } }
    public bool IsFinished
    { get { if (!IsReady || !resetDone) return false; foreach (Operation operation in operations) if (!operation.Done) return false; return true; } }
    public GTA.Entity[] OwnedEntities { get { return new GTA.Entity[0]; } }
    public int[] OwnedHandles { get { return new int[0]; } }
    public bool ControlsBody(string actor, int elapsed)
    {
        if (!IsReady || !resetDone) return false;
        foreach (Operation operation in operations)
            if (!operation.Done && System.String.Equals(operation.Spec.Ator, actor, System.StringComparison.OrdinalIgnoreCase) &&
                elapsed >= operation.Spec.InicioMs && elapsed < operation.Spec.InicioMs + operation.Spec.DuracaoMs)
                return operation.Spec.Tipo == "bater" || operation.Spec.Tipo == "atravessar";
        return false;
    }

    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> ownRoster, System.Collections.Generic.IEnumerable<DoorSpec> specs,
        GTA.Math.Vector3 realOrigin, GTA.Math.Vector3 actualRight, GTA.Math.Vector3 actualForward, string sceneId = null)
    {
        Active(); if (prepared || ownRoster == null || specs == null) throw Error("preparação anterior/elenco/specs inválidos");
        Vector(realOrigin); Vector(actualRight); Vector(actualForward);
        origin = realOrigin; right = actualRight; forward = actualForward;
        preparedSceneId = sceneId;
        if (Horizontal(right) < 0.99f || Horizontal(forward) < 0.99f || System.Math.Abs(GTA.Math.Vector3.Dot(right, forward)) > 0.01f)
            throw Error("eixos relativos precisam ser ortogonais");
        LoadingStep("PREPARE_MODELS_BEFORE", "PropList=" + propListFile);
        ReadDoorModels();
        LoadingStep("PREPARE_MODELS_AFTER", "Models=" + doorModels.Count);
        var actors = new System.Collections.Generic.Dictionary<string, GTA.Ped>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var actor in ownRoster) actors.Add(actor.Key, actor.Value);
        foreach (DoorSpec input in specs)
        {
            if (input == null || operations.Count >= 16 || input.InicioMs < 0 || input.DuracaoMs < 100 ||
                input.InicioMs + (long)input.DuracaoMs > 180000 ||
                (input.Tipo != "bater" && input.Tipo != "abrir" && input.Tipo != "fechar" && input.Tipo != "atravessar"))
                throw Error("tipo/tempo/limite da interação inválido");
            GTA.Ped ped = null;
            bool withoutActor = input.Ator == null && (input.Tipo == "abrir" || input.Tipo == "fechar");
            if (!withoutActor)
            { if (!actors.TryGetValue(input.Ator ?? "", out ped)) throw Error("ator da porta não pertence ao elenco"); RequirePed(ped); }
            if (input.Tipo == "bater" && (input.Batidas != 3 || input.Recurso != "bater_porta" ||
                input.Dicionario != "timetable@jimmy@doorknock@" || input.Clipe != "knockdoor_idle"))
                throw Error("batida exige três contatos e o par real catalogado");
            if (input.Tipo != "bater" && (input.Batidas != 0 || !System.String.IsNullOrEmpty(input.Recurso)))
                throw Error("clipe/contagem aplicam-se somente à batida");
            Vector(input.DestinoRelativo);
            if (Horizontal(input.DestinoRelativo) > 15f || System.Math.Abs(input.DestinoRelativo.Z) > 2f ||
                !Finite(input.Velocidade) || input.Velocidade < 0.2f || input.Velocidade > 2f) throw Error("trajeto/velocidade fora da passagem próxima");
            var spec = new DoorSpec { Tipo = input.Tipo, Ator = input.Ator, InicioMs = input.InicioMs, DuracaoMs = input.DuracaoMs,
                Batidas = input.Batidas, Recurso = input.Recurso, Dicionario = input.Dicionario, Clipe = input.Clipe,
                OrigemArquivo = input.OrigemArquivo, OrigemLinha = input.OrigemLinha, OrigemSha256 = input.OrigemSha256,
                DestinoRelativo = input.DestinoRelativo, Velocidade = input.Velocidade };
            foreach (Operation earlier in operations)
                if (input.Ator == earlier.Spec.Ator && input.InicioMs < earlier.Spec.InicioMs + earlier.Spec.DuracaoMs &&
                    earlier.Spec.InicioMs < input.InicioMs + input.DuracaoMs) throw Error("interações sobrepostas no mesmo ator");
            var operation = new Operation { Spec = spec, Actor = ped, Handle = withoutActor ? 0 : ped.Handle, Address = withoutActor ? System.IntPtr.Zero : ped.MemoryAddress,
                Model = withoutActor ? 0 : ped.Model.Hash, Destination = Resolve(input.DestinoRelativo) };
            operations.Add(operation);
            if (spec.Tipo == "bater")
            {
                VerifyKnockSource(spec);
                if (!dictionaries.ContainsKey(spec.Dicionario))
                {
                    bool loaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, spec.Dicionario);
                    dictionaries.Add(spec.Dicionario, !loaded);
                    GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, spec.Dicionario);
                }
            }
        }
        if (operations.Count == 0) throw Error("lista de portas vazia");
        // Options7 ignora see-through. Apenas o portao gradeado desta cena
        // precisa medir tambem essa colisao; manter IgnoreNoCollision(4).
        if (sceneId == "EP14_C01")
        {
            DoorSpec knock = operations[0].Spec;
            if (operations.Count != 1 || knock.Tipo != "bater" || !System.String.Equals(knock.Ator, "DINJER", System.StringComparison.OrdinalIgnoreCase) ||
                knock.InicioMs != 0 || knock.DuracaoMs != 7000 || knock.Batidas != 3)
                throw Error("sonda de grade exige somente Dinjer batendo tres vezes em EP14_C01/7s");
            rayOptions = 4;
        }
        if (sceneId == "EP13_C03")
        {
            if (operations.Count != 2 || ownRoster.Count != 0)
                throw Error("EP13_C03 grade exige somente duas portas sem ator, abrir0..3 e fechar3..7.5");
            for (int i = 0; i < 2; i++)
            {
                DoorSpec gate = operations[i].Spec;
                if (gate.Tipo != (i == 0 ? "abrir" : "fechar") || gate.Ator != null ||
                    gate.InicioMs != i * 3000 || gate.DuracaoMs != (i == 0 ? 3000 : 4500))
                    throw Error("EP13_C03 grade exige o contrato literal de abertura/fechamento sem ator");
            }
            // Chainlink real: inclui see-through; continua ignorando apenas no-collision(4).
            rayOptions = 4;
        }
        if (sceneId == "EP15_C07")
        {
            if (operations.Count != 2 || actors.Count != 2 || !actors.ContainsKey("DJANIRA_RICA") || !actors.ContainsKey("O_CHEFE"))
                throw Error("C7 requires only Rich/Chefe and physical opening0..2/crossing2..5");
            for (int i = 0; i < 2; i++)
            {
                DoorSpec s = operations[i].Spec;
                if (!System.String.Equals(s.Ator, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase) ||
                    s.Tipo != (i == 0 ? "abrir" : "atravessar") || s.InicioMs != (i == 0 ? 0 : 2000) || s.DuracaoMs != (i == 0 ? 2000 : 3000) ||
                    (i == 1 && (s.DestinoRelativo != new GTA.Math.Vector3(0f, 2f, 0f) || s.Velocidade != 1.1f)))
                    throw Error("C7 literal physical door windows/destination changed");
            }
        }
        prepared = true; loading = System.Diagnostics.Stopwatch.StartNew();
    }

    public void PollLoading()
    {
        Active(); if (!prepared || failed) throw Error("preparação inválida"); if (ready) return;
        try
        {
            if (loading.ElapsedMilliseconds >= 8000) throw Error("porta/passagem/clipe não ficaram disponíveis em8s");
            foreach (Operation operation in operations)
            {
                LoadingStep("ACTOR_BEFORE", "Handle=" + operation.Handle + "; Tipo=" + operation.Spec.Tipo);
                RequireActor(operation);
                LoadingStep("ACTOR_AFTER", "Handle=" + operation.Handle + "; Tipo=" + operation.Spec.Tipo);
                if (operation.Ready) continue;
                if (operation.Spec.Tipo == "atravessar")
                {
                    float ground;
                    GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, operation.Destination.X, operation.Destination.Y, operation.Destination.Z);
                    if (!GTA.World.GetGroundHeight(operation.Destination + new GTA.Math.Vector3(0f, 0f, 2f), out ground, GTA.GetGroundHeightMode.Normal)) return;
                    if (!Finite(ground) || System.Math.Abs(ground - operation.Destination.Z) > 2f) throw Error("destino da passagem não possui chão próximo");
                    operation.Destination.Z = ground + 1f;
                    // Abertura anterior modifica fisicamente a mesma passagem; conferir na ação depois de abrir.
                    operation.Ready = true; return;
                }
                if (operation.Spec.Tipo == "bater")
                {
                    LoadingStep("ANIM_BEFORE", "Dictionary=" + operation.Spec.Dicionario);
                    bool loaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, operation.Spec.Dicionario);
                    LoadingStep("ANIM_AFTER", "Loaded=" + loaded);
                    if (!loaded) return;
                }
                if (operation.Door != null)
                {
                    RequireDoor(operation.Door);
                    if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, operation.Door.SystemId)) return;
                    VerifyKnockDuration(operation);
                    if (IsC7Opening(operation)) RequireC7DoorRegistration(operation.Door);
                    operation.Ready = true; return;
                }
                if (operation.Ray == 0)
                {
                    LoadingStep("RAY_VECTORS_BEFORE", "Handle=" + operation.Handle);
                    GTA.Math.Vector3 from = operation.Actor == null ? origin + new GTA.Math.Vector3(0f, 0f, 1.2f) : operation.Actor.Bones[GTA.Bone.SkelPelvis].Position + new GTA.Math.Vector3(0f, 0f, 0.5f);
                    GTA.Math.Vector3 to = from + (operation.Actor == null ? forward : operation.Actor.ForwardVector) * 1.8f;
                    if (IsBankOpening(operation)) to = SelectMeasuredBankRay(operation, from);
                    LoadingStep("RAY_VECTORS_AFTER", "From=" + from + "; To=" + to + "; Ignore=" + operation.Handle);
                    operation.Ray = StartRay(from, to, operation.Handle); return;
                }
                RayResult result;
                if (!ReadRay(operation.Ray, out result)) return;
                operation.Ray = 0;
                if (!result.Hit || result.Entity == 0)
                {
                    LogNearbyPhysicalDoors(operation, result);
                    throw Error("nenhuma porta física foi identificada à frente do ator; marque uma porta real");
                }
                GTA.Prop prop = result.PhysicalEntity as GTA.Prop;
                if (System.Object.ReferenceEquals(prop, null) || !prop.Exists())
                    throw Error("colisor encontrado não é prop físico existente; não consultar modelo de parede/building ou fabricar entrada");
                LoadingStep("PROP_GETTERS_BEFORE", "HitEntity=" + result.Entity);
                Log("PORTA_COLISOR_GETTER", "HitEntity=" + result.Entity + "; EntityType=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, result.Entity) + "; Model=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, result.Entity) + "; Point=" + result.Point + "; Normal=" + result.Normal);
                LoadingStep("PROP_GETTERS_AFTER", "HitEntity=" + result.Entity);
                if (!doorModels.ContainsKey(prop.Model.Hash))
                    throw Error("colisor encontrado não é modelo de porta/portão conferido na PropList; não abrir parede ou fabricar entrada");
                if (preparedSceneId == "EP13_C03" && rayOptions == 4)
                    RequireC13MeasuredGateBeforeRegister(operation, prop, result);
                if (IsBankOpening(operation))
                {
                    RequireSelectedBankProp(operation);
                    if (prop.Handle != operation.BankSelectedHandle || prop.MemoryAddress != operation.BankSelectedAddress || prop.Model.Hash != 110411286)
                        throw Error("EP11_C01 ray did not hit the selected measured bank leaf; no door registered and ARM unavailable");
                    Log("PORTA_BANCO_SELECAO_CONFIRMADA", "Cena=" + preparedSceneId + "; Handle=" + prop.Handle +
                        "; Model=" + prop.Model.Hash + "; Pos=" + prop.Position + "; Hit=" + result.Point +
                        "; PreparedOnly=true; OpeningProven=false; PassageProven=false; OtherLeafModified=false");
                }
                DoorState door;
                if (!doors.TryGetValue(prop.Handle, out door))
                {
                    door = new DoorState { Prop = prop, Handle = prop.Handle, Model = prop.Model.Hash, Address = prop.MemoryAddress,
                        Position = prop.Position, Rotation = prop.Rotation, Point = result.Point, Normal = result.Normal };
                    doors.Add(door.Handle, door); RegisterDoor(door);
                    Log("PORTA_REAL_IDENTIFICADA", "Handle=" + door.Handle + "; Modelo=" + doorModels[door.Model] + "; Pos=" + door.Position + "; Ponto=" + door.Point + "; Normal=" + door.Normal + "; Added=" + door.Added);
                }
                operation.Door = door;
                if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, door.SystemId)) return;
                VerifyKnockDuration(operation);
                if (IsC7Opening(operation)) RequireC7DoorRegistration(operation.Door);
                operation.Ready = true; return;
            }
            ready = true;
        }
        catch { failed = true; throw; }
    }

    public void Reset()
    {
        RequireReady(); StopOwnedClips(); RestoreDoors();
        foreach (Operation operation in operations)
        {
            RequireActor(operation); operation.Started = operation.Done = operation.Approached = false;
            operation.KnockCount = operation.StartedAt = operation.LastKnockAt = operation.Sample = 0;
            operation.Ray = 0; operation.LastTick = -1; operation.MinimumDistance = float.MaxValue;
            operation.CyclePeakDistance = 0f; operation.NextKnockLogMs = 0;
            operation.BankOpening = operation.C7Opening = operation.C7ForceSettling = false;
            operation.C7ForceTick = operation.C7ForceFrames = 0;
            operation.NextOpenDiagnosticMs = operation.OpeningStartState = 0;
            operation.OpeningStartRatio = 0f; operation.OpeningStartPosition = operation.OpeningStartRotation = GTA.Math.Vector3.Zero;
        }
        elapsedBefore = -1; resetDone = true;
    }
    public void ReleaseActorsForReset() { Active(); StopOwnedClips(); resetDone = false; }

    public void Update(int elapsed)
    {
        RequireReady(); if (!resetDone || elapsed < 0 || elapsed < elapsedBefore) throw Error("tempo/reinício inválido"); elapsedBefore = elapsed;
        try
        {
            if (preparedSceneId == "EP15_C07" && elapsed >= 2000 && !operations[1].Done) RequireC7OpenForPassage(elapsed);
            foreach (Operation operation in operations)
            {
                DoorSpec spec = operation.Spec;
                if (operation.Done || elapsed < spec.InicioMs) continue;
                RequireActor(operation);
                if (operation.Door != null) RequireDoor(operation.Door);
                if (!operation.Started) Start(operation, elapsed);
                if (spec.Tipo == "atravessar") UpdatePassage(operation, elapsed);
                else if (spec.Tipo == "bater") UpdateKnocks(operation, elapsed);
                else UpdateOpen(operation, elapsed);
                if (!operation.Done && elapsed >= spec.InicioMs + spec.DuracaoMs)
                    throw Error("interação não terminou fisicamente no prazo: " + spec.Tipo + "; Ator=" + spec.Ator + "; Batidas=" + operation.KnockCount);
            }
        }
        catch { failed = true; throw; }
    }

    private void Start(Operation operation, int elapsed)
    {
        DoorSpec spec = operation.Spec;
        if (elapsed >= spec.InicioMs + spec.DuracaoMs) throw Error("início da interação perdido; reinicie");
        operation.Started = true; operation.StartedAt = GTA.Game.GameTime;
        if (spec.Tipo == "bater")
        {
            operation.Actor.Task.ClearAllImmediately(); operation.Actor.IsPositionFrozen = true;
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, operation.Handle, spec.Dicionario, spec.Clipe, 4f, -4f, -1,
                (int)GTA.AnimationFlags.Loop, 0f, false, false, false);
            operation.LastDistance = HandDistance(operation);
            operation.CyclePeakDistance = operation.MinimumDistance = operation.LastDistance;
        }
        else if (spec.Tipo == "abrir" || spec.Tipo == "fechar")
        {
            // Last bool is forceUpdate; requestDoor remains false. Only this measured bank leaf changes.
            bool bankOpening = IsBankOpening(operation);
            if (bankOpening)
            {
                RequireSelectedBankProp(operation);
                string modelName;
                if (operation.Door.Handle != operation.BankSelectedHandle || operation.Door.Address != operation.BankSelectedAddress ||
                    operation.Door.Model != 110411286 || !doorModels.TryGetValue(operation.Door.Model, out modelName) || modelName != "hei_prop_hei_bankdoor_new")
                    throw Error("EP11_C01 opening requires the measured hei_prop_hei_bankdoor_new; no other leaf modified");
                operation.BankOpening = true;
                operation.OpeningStartPosition = operation.Door.Prop.Position; operation.OpeningStartRotation = operation.Door.Prop.Rotation;
                Vector(operation.OpeningStartPosition); Vector(operation.OpeningStartRotation);
                operation.OpeningStartRatio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, operation.Door.SystemId);
                operation.OpeningStartState = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, operation.Door.SystemId);
                if (!Finite(operation.OpeningStartRatio)) throw Error("EP11_C01 opening baseline ratio is not finite");
                operation.NextOpenDiagnosticMs = elapsed;
                Log("PORTA_BANCO_ABERTURA_PEDIDA", "Cena=" + preparedSceneId + "; TempoMs=" + elapsed + "; Handle=" + operation.Door.Handle +
                    "; Model=" + operation.Door.Model + "; System=" + operation.Door.SystemId + "; Added=" + operation.Door.Added +
                    "; TargetRatio=1; RequestDoor=false; ForceUpdate=true; BaselineRatio=" + OpenNumber(operation.OpeningStartRatio) +
                    "; BaselineState=" + operation.OpeningStartState + "; BaselinePos=" + operation.OpeningStartPosition +
                    "; BaselineRot=" + operation.OpeningStartRotation + "; PhysicalOpeningProven=false; NativeSuccessPending=true");
            }
            bool c7Opening = IsC7Opening(operation);
            if (c7Opening)
            {
                RequireC7DoorRegistration(operation.Door);
                operation.C7Opening = true; operation.C7ForceSettling = false; operation.C7ForceFrames = 0;
                operation.C7ForceTick = GTA.Game.GameTime;
                operation.OpeningStartPosition = operation.Door.Prop.Position; operation.OpeningStartRotation = operation.Door.Prop.Rotation;
                Vector(operation.OpeningStartPosition); Vector(operation.OpeningStartRotation);
                operation.OpeningStartState = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, operation.Door.SystemId);
                operation.OpeningStartRatio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, operation.Door.SystemId);
                if (operation.OpeningStartState != 1 || !Finite(operation.OpeningStartRatio) || System.Math.Abs(operation.OpeningStartRatio) > 0.01f)
                    throw Error("C7 requires original locked closed leaf before opening; no other door modified");
                operation.NextOpenDiagnosticMs = elapsed;
                Log("PORTA_C7_ABERTURA_PEDIDA", "Cena=EP15_C07; Handle=" + operation.Door.Handle + "; System=" + operation.Door.SystemId +
                    "; BaselineState=" + operation.OpeningStartState + "; BaselineRatio=" + OpenNumber(operation.OpeningStartRatio) +
                    "; BaselinePos=" + operation.OpeningStartPosition + "; BaselineRot=" + operation.OpeningStartRotation + "; WindowMs=2000; PhysicalProofPending=true");
            }
            bool c13Facgate = IsC13FacgateOperation(operation);
            if (c13Facgate)
            {
                RequireDoor(operation.Door);
                operation.OpeningStartPosition = operation.Door.Prop.Position; operation.OpeningStartRotation = operation.Door.Prop.Rotation;
                Vector(operation.OpeningStartPosition); Vector(operation.OpeningStartRotation);
                if (spec.Tipo == "fechar") operation.NextOpenDiagnosticMs = elapsed;
                Log("PORTA_C13_FACGATE_PEDIDA", "Cena=EP13_C03; Tipo=" + spec.Tipo + "; Handle=" + operation.Door.Handle +
                    "; System=" + operation.Door.SystemId + "; BaselinePos=" + operation.OpeningStartPosition + "; BaselineRot=" + operation.OpeningStartRotation +
                    "; RequestDoor=false; ForceUpdate=true; PhysicalMotionProven=false");
            }
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, operation.Door.SystemId, 0, false, true);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, operation.Door.SystemId, spec.Tipo == "abrir" ? 1f : 0f, false, bankOpening || c13Facgate || c7Opening);
        }
    }
    private void UpdateOpen(Operation operation, int elapsed)
    {
        float ratio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, operation.Door.SystemId);
        if (!Finite(ratio)) throw Error("ângulo real da porta não é finito");
        if (IsC13FacgateOperation(operation) && operation.Spec.Tipo == "fechar" &&
            (elapsed >= operation.NextOpenDiagnosticMs || elapsed >= operation.Spec.InicioMs + operation.Spec.DuracaoMs))
        {
            operation.NextOpenDiagnosticMs = elapsed + 250;
            LogC13FacgateClosingSample(operation, elapsed, ratio);
        }
        if (operation.C7Opening && !UpdateC7Opening(operation, elapsed)) return;
        if (operation.BankOpening)
        {
            GTA.Math.Vector3 position = operation.Door.Prop.Position, rotation = operation.Door.Prop.Rotation;
            Vector(position); Vector(rotation);
            int state = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, operation.Door.SystemId);
            bool physics = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, operation.Door.SystemId);
            float displacement = (position - operation.OpeningStartPosition).Length();
            float rotationDelta = System.Math.Max(OpenAngleDifference(rotation.X, operation.OpeningStartRotation.X),
                System.Math.Max(OpenAngleDifference(rotation.Y, operation.OpeningStartRotation.Y), OpenAngleDifference(rotation.Z, operation.OpeningStartRotation.Z)));
            if (!Finite(displacement) || !Finite(rotationDelta)) throw Error("EP11_C01 opening physical delta is not finite");
            bool physicallyChanged = displacement >= 0.05f || rotationDelta >= 5f;
            bool processedNextTick = GTA.Game.GameTime != operation.StartedAt;
            bool accepted = System.Math.Abs(ratio) >= 0.85f && state == 0 && physics && physicallyChanged && processedNextTick;
            if (elapsed >= operation.NextOpenDiagnosticMs || accepted)
            {
                operation.NextOpenDiagnosticMs = elapsed + 500;
                Log("PORTA_BANCO_ABERTURA_AMOSTRA", "Cena=" + preparedSceneId + "; TempoMs=" + elapsed +
                    "; Handle=" + operation.Door.Handle + "; Model=" + operation.Door.Model + "; System=" + operation.Door.SystemId +
                    "; Ratio=" + OpenNumber(ratio) + "; State=" + state + "; PhysicsLoaded=" + physics +
                    "; Pos=" + position + "; Rot=" + rotation + "; Displacement=" + OpenNumber(displacement) +
                    "; RotationDeltaDeg=" + OpenNumber(rotationDelta) + "; PhysicallyChanged=" + physicallyChanged +
                    "; ProcessedNextTick=" + processedNextTick + "; Accepted=" + accepted +
                    "; RequestDoor=false; ForceUpdate=true; RatioThresholdPreserved=0.85; WindowMs=" + operation.Spec.DuracaoMs);
            }
            if (!accepted) return;
        }
        if (operation.Spec.Tipo == "abrir" ? System.Math.Abs(ratio) < 0.85f : System.Math.Abs(ratio) > 0.05f) return;
        if (IsC13FacgateOperation(operation))
        {
            RequireDoor(operation.Door);
            GTA.Math.Vector3 position = operation.Door.Prop.Position, rotation = operation.Door.Prop.Rotation;
            Vector(position); Vector(rotation);
            float displacement = (position - operation.OpeningStartPosition).Length();
            float rotationDelta = System.Math.Max(OpenAngleDifference(rotation.X, operation.OpeningStartRotation.X),
                System.Math.Max(OpenAngleDifference(rotation.Y, operation.OpeningStartRotation.Y), OpenAngleDifference(rotation.Z, operation.OpeningStartRotation.Z)));
            if (!Finite(displacement) || !Finite(rotationDelta)) throw Error("C13 SP facgate actual physical delta is nonfinite");
            bool physics = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, operation.Door.SystemId);
            if (GTA.Game.GameTime == operation.StartedAt || !physics || (displacement < 0.05f && rotationDelta < 5f)) return;
            Log("PORTA_C13_FACGATE_MOVIMENTO_REAL", "Cena=EP13_C03; Tipo=" + operation.Spec.Tipo + "; Handle=" + operation.Door.Handle +
                "; TempoMs=" + elapsed + "; Ratio=" + OpenNumber(ratio) + "; Pos=" + position + "; Rot=" + rotation +
                "; Displacement=" + OpenNumber(displacement) + "; RotationDelta=" + OpenNumber(rotationDelta) + "; PhysicsLoaded=true; PhysicalMotionProven=true; VisualApproval=false");
        }
        operation.Done = true;
        Log("PORTA_ANGULO_REAL", "Tipo=" + operation.Spec.Tipo + "; Ratio=" + ratio + "; TempoMs=" + elapsed + "; Handle=" + operation.Door.Handle);
    }
    private bool IsC7Opening(Operation operation)
    {
        return preparedSceneId == "EP15_C07" && operation.Spec.Tipo == "abrir" &&
            System.String.Equals(operation.Spec.Ator, "DJANIRA_RICA", System.StringComparison.OrdinalIgnoreCase) &&
            operation.Spec.InicioMs == 0 && operation.Spec.DuracaoMs == 2000;
    }
    private void RequireC7DoorRegistration(DoorState door)
    {
        if (preparedSceneId != "EP15_C07" || door == null) throw Error("C7 measured leaf scope missing");
        RequireDoor(door); string modelName;
        if (door.Model != 308207762 || !doorModels.TryGetValue(door.Model, out modelName) || modelName != "v_ilev_fh_frontdoor" ||
            (door.Position - new GTA.Math.Vector3(7.518358f, 539.5269f, 176.1776f)).Length() > 0.1f ||
            door.Added || !door.OriginalSaved || door.OriginalState != 1 || !Finite(door.OriginalRatio) || System.Math.Abs(door.OriginalRatio) > 0.01f ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_DOOR_REGISTERED_WITH_SYSTEM, door.SystemId) ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, door.SystemId))
            throw Error("C7 requires exact measured FH/identity/existing registration/physics/original locked closed snapshot");
        System.IntPtr output = Allocate();
        try
        {
            bool found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR,
                door.Position.X, door.Position.Y, door.Position.Z, door.Model, output);
            if (!found || System.Runtime.InteropServices.Marshal.ReadInt32(output) != door.SystemId)
                throw Error("C7 existing registration changed; no replacement or new registration");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(output); }
    }
    private bool ReadC7PhysicalOpening(Operation operation, int elapsed, out float ratio, out int state)
    {
        RequireC7DoorRegistration(operation.Door);
        GTA.Math.Vector3 position = operation.Door.Prop.Position, rotation = operation.Door.Prop.Rotation;
        Vector(position); Vector(rotation);
        ratio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, operation.Door.SystemId);
        state = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, operation.Door.SystemId);
        float displacement = (position - operation.OpeningStartPosition).Length();
        float rotationDelta = System.Math.Max(OpenAngleDifference(rotation.X, operation.OpeningStartRotation.X),
            System.Math.Max(OpenAngleDifference(rotation.Y, operation.OpeningStartRotation.Y), OpenAngleDifference(rotation.Z, operation.OpeningStartRotation.Z)));
        if (!Finite(ratio) || !Finite(displacement) || !Finite(rotationDelta)) throw Error("C7 physical opening getter nonfinite");
        bool opened = System.Math.Abs(ratio) >= 0.9f && (displacement >= 0.05f || rotationDelta >= 5f);
        if (elapsed >= operation.NextOpenDiagnosticMs || (opened && operation.C7ForceFrames > 0 && !operation.Done))
        {
            operation.NextOpenDiagnosticMs = elapsed + 500;
            Log("PORTA_C7_ABERTURA_AMOSTRA", "Cena=EP15_C07; TempoMs=" + elapsed + "; Handle=" + operation.Door.Handle +
                "; State=" + state + "; Ratio=" + OpenNumber(ratio) + "; Pos=" + position + "; Rot=" + rotation +
                "; Displacement=" + OpenNumber(displacement) + "; RotationDelta=" + OpenNumber(rotationDelta) +
                "; PhysicsLoaded=true; PhysicalOpened=" + opened + "; ForceFrames=" + operation.C7ForceFrames + "; Settling=" + operation.C7ForceSettling);
        }
        return opened;
    }
    private bool UpdateC7Opening(Operation operation, int elapsed)
    {
        float ratio; int state; bool opened = ReadC7PhysicalOpening(operation, elapsed, out ratio, out state);
        int tick = GTA.Game.GameTime;
        if (operation.C7ForceSettling)
            return tick != operation.C7ForceTick && state == 0 && opened;
        if (operation.C7ForceFrames > 0 && tick != operation.C7ForceTick && (state == 0 || state == 5) && opened)
        {
            operation.C7ForceSettling = true; operation.C7ForceTick = tick;
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, operation.Door.SystemId, 0, false, true);
            Log("PORTA_C7_FORCE_SETTLE", "TempoMs=" + elapsed + "; Handle=" + operation.Door.Handle + "; TargetState=0; SuccessPendingNextTick=true");
            return false;
        }
        if (elapsed >= operation.Spec.InicioMs + operation.Spec.DuracaoMs) return false;
        if (operation.C7ForceFrames > 0 && operation.C7ForceTick == tick) return false;
        RequireC7DoorRegistration(operation.Door); operation.C7ForceTick = tick; operation.C7ForceFrames++;
        if (operation.C7ForceFrames == 1)
            Log("PORTA_C7_FORCE_PEDIDO", "TempoMs=" + elapsed + "; Handle=" + operation.Door.Handle + "; TargetState=5; ThisFrameOnly=true; RequestDoor=false; ForceUpdate=true");
        GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, operation.Door.SystemId, 5, false, true);
        return false;
    }
    private void RequireC7OpenForPassage(int elapsed)
    {
        Operation opening = operations[0];
        if (!IsC7Opening(opening) || !opening.Done || !opening.C7ForceSettling)
            throw Error("C7 crossing requires completed physical opening and state0 after settle on nextTick");
        float ratio; int state;
        if (!ReadC7PhysicalOpening(opening, elapsed, out ratio, out state) || state != 0)
            throw Error("C7 physical leaf closed/changed during crossing; never pass by warp/invisibility");
    }

    private static float OpenAngleDifference(float a, float b)
    {
        float delta = (a - b) % 360f; if (delta > 180f) delta -= 360f; if (delta < -180f) delta += 360f;
        return System.Math.Abs(delta);
    }
    private static string OpenNumber(float value)
    { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }

    private void UpdatePassage(Operation operation, int elapsed)
    {
        if (operation.Sample < 3)
        {
            if (operation.Ray == 0)
            {
                GTA.Math.Vector3 offset = right * ((operation.Sample - 1) * 0.25f);
                operation.Ray = StartRay(operation.Actor.Position + offset, operation.Destination + offset, operation.Handle);
                return;
            }
            RayResult result;
            if (!ReadRay(operation.Ray, out result)) return;
            operation.Ray = 0;
            if (result.Hit) throw Error("passagem realmente bloqueada; não atravessar porta por invisibilidade/warp");
            operation.Sample++;
            if (operation.Sample < 3) return;
            operation.Actor.Task.ClearAllImmediately(); operation.Actor.Velocity = GTA.Math.Vector3.Zero; operation.Actor.IsPositionFrozen = false;
            GTA.Math.Vector3 delta = operation.Destination - operation.Actor.Position;
            float heading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
            operation.Actor.Task.GoStraightTo(operation.Destination, -1, new GTA.PedMoveBlendRatio(operation.Spec.Velocidade), heading, 0.2f);
        }
        if (Horizontal(operation.Actor.Position - operation.Destination) > 0.4f || System.Math.Abs(operation.Actor.Position.Z - operation.Destination.Z) > 1f) return;
        operation.Actor.Task.ClearAllImmediately(); operation.Actor.Velocity = GTA.Math.Vector3.Zero;
        operation.Actor.IsPositionFrozen = true; operation.Actor.Task.StandStill(-1); operation.Done = true;
        Log("PORTA_PASSAGEM_CHEGADA", "Ator=" + operation.Spec.Ator + "; TempoMs=" + elapsed + "; Pos=" + operation.Actor.Position + "; Destino=" + operation.Destination);
    }
    private void UpdateKnocks(Operation operation, int elapsed)
    {
        if (GTA.Game.GameTime == operation.StartedAt || operation.LastTick == GTA.Game.GameTime) return;
        operation.LastTick = GTA.Game.GameTime;
        uint since = unchecked((uint)(GTA.Game.GameTime - operation.StartedAt));
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, operation.Handle, operation.Spec.Dicionario, operation.Spec.Clipe, 3);
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, operation.Handle, operation.Spec.Dicionario, operation.Spec.Clipe);
        if ((!playing || phase == -1f) && since < 500u) return;
        if (!playing || !Finite(phase) || phase < 0f || phase > 1f) throw Error("clipe de batida/fase real inválido; Phase=" + phase + "; SinceStart=" + since);
        GTA.Math.Vector3 hand; float signed, tangent;
        float distance = HandDistance(operation, out hand, out signed, out tangent);
        // Acumulado ordenado: pico anterior ate a mao atual. Nao exigir 1cm por frame.
        if (distance > operation.CyclePeakDistance)
        {
            operation.CyclePeakDistance = distance;
            if (!operation.Approached) operation.MinimumDistance = distance;
        }
        operation.MinimumDistance = System.Math.Min(operation.MinimumDistance, distance);
        if (operation.CyclePeakDistance - distance >= 0.01f) operation.Approached = true;
        if (elapsed >= operation.NextKnockLogMs)
        {
            operation.NextKnockLogMs = elapsed + 200;
            GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus status;
            operation.Actor.GetCurrentScriptTaskNameHashAndStatus(out task, out status);
            Log("PORTA_BATIDA_AMOSTRA", "Ator=" + operation.Spec.Ator + "; TempoMs=" + elapsed +
                "; Hand=" + hand + "; SignedNormal=" + signed + "; Tangent=" + tangent +
                "; Distance=" + distance + "; LastDistance=" + operation.LastDistance +
                "; CyclePeak=" + operation.CyclePeakDistance + "; Minimum=" + operation.MinimumDistance +
                "; Approached=" + operation.Approached + "; Phase=" + phase + "; BodyPlaying=" + playing +
                "; CurrentTask=" + task + "; TaskStatus=" + status + "; Batidas=" + operation.KnockCount);
        }
        if (operation.Approached && operation.MinimumDistance <= 0.15f && distance > operation.MinimumDistance + 0.035f &&
            elapsed - operation.LastKnockAt >= 180 && phase > 0.01f)
        {
            operation.KnockCount++; operation.LastKnockAt = elapsed; operation.Approached = false;
            operation.CyclePeakDistance = operation.MinimumDistance = distance;
            Log("PORTA_BATIDA_CONTATO", "Ator=" + operation.Spec.Ator + "; Contagem=" + operation.KnockCount + "; TempoMs=" + elapsed + "; Distance=" + distance + "; Phase=" + phase);
            if (operation.KnockCount == 3)
            {
                operation.Actor.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(operation.Spec.Dicionario, operation.Spec.Clipe));
                operation.Actor.Task.StandStill(-1); operation.Done = true;
            }
        }
        operation.LastDistance = distance;
    }
    private static float HandDistance(Operation operation)
    {
        GTA.Math.Vector3 hand; float signed, tangent;
        return HandDistance(operation, out hand, out signed, out tangent);
    }
    private static float HandDistance(Operation operation, out GTA.Math.Vector3 hand, out float signed, out float tangent)
    {
        hand = operation.Actor.Bones[GTA.Bone.SkelRightHand].Position;
        Vector(hand);
        GTA.Math.Vector3 normal = operation.Door.Normal;
        float length = normal.Length(); if (length < 0.5f) throw Error("normal real da porta inválida");
        GTA.Math.Vector3 difference = hand - operation.Door.Point;
        signed = GTA.Math.Vector3.Dot(difference, normal / length);
        tangent = (difference - normal / length * signed).Length();
        if (tangent > 0.8f) throw Error("a mão está fora do painel real identificado");
        return System.Math.Abs(signed);
    }

    private void RegisterDoor(DoorState door)
    {
        if (preparedSceneId == "EP15_C07")
        {
            RequireDoor(door); string expectedName;
            if (door.Model != 308207762 || !doorModels.TryGetValue(door.Model, out expectedName) || expectedName != "v_ilev_fh_frontdoor" ||
                (door.Position - new GTA.Math.Vector3(7.518358f, 539.5269f, 176.1776f)).Length() > 0.1f)
                throw Error("C7 registration requires exact measured FH leaf; no other target read/written");
        }
        System.IntPtr output = Allocate();
        try
        {
            bool found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR, door.Position.X, door.Position.Y, door.Position.Z, door.Model, output);
            if (found) door.SystemId = System.Runtime.InteropServices.Marshal.ReadInt32(output);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(output); }
        if (preparedSceneId == "EP15_C07" && door.SystemId == 0)
            throw Error("C7 requires existing measured FH registration; no new registration permitted");
        if (door.SystemId == 0)
        {
            // Identificador próprio calculado pelo engine, não hash inventado de modelo/asset.
            door.SystemId = unchecked((int)GTA.StringHash.AtStringHash("DIRETOR_PORTA_" + door.Handle.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            door.Added = true;
            GTA.Native.Function.Call(GTA.Native.Hash.ADD_DOOR_TO_SYSTEM, door.SystemId, door.Model, door.Position.X, door.Position.Y, door.Position.Z, false, false, false);
        }
        door.OriginalState = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, door.SystemId);
        door.OriginalRatio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, door.SystemId);
        if (!Finite(door.OriginalRatio)) throw Error("estado original da porta não disponível");
        door.OriginalSaved = true;
    }
    private static void VerifyKnockDuration(Operation operation)
    {
        if (operation.Spec.Tipo != "bater") return;
        float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, operation.Spec.Dicionario, operation.Spec.Clipe);
        if (!Finite(duration) || duration <= 0f) throw Error("clipe de batida sem duração real");
    }
    private void RestoreDoors()
    {
        foreach (DoorState door in doors.Values)
        {
            // C7 register may fail before any writes/snapshot: no invented restoration to system0.
            if (preparedSceneId == "EP15_C07" && !door.OriginalSaved) continue;
            RequireDoor(door);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, door.SystemId, 0, false, true);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, door.SystemId, door.OriginalRatio, false, true);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, door.SystemId, door.OriginalState, false, true);
        }
    }
    private void StopOwnedClips()
    {
        foreach (Operation operation in operations)
            if (operation.Started && operation.Spec.Tipo == "bater" && StillActor(operation))
                operation.Actor.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(operation.Spec.Dicionario, operation.Spec.Clipe));
    }
    public void Cleanup()
    {
        if (disposed) return; StopOwnedClips(); RestoreDoors();
        foreach (DoorState door in doors.Values)
            if (door.Added) { RequireDoor(door); GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_DOOR_FROM_SYSTEM, door.SystemId); door.Prop.Rotation = door.Rotation; }
        foreach (var dictionary in dictionaries) if (dictionary.Value) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, dictionary.Key);
        operations.Clear(); doors.Clear(); dictionaries.Clear(); doorModels.Clear(); loadingSteps.Clear(); prepared = ready = failed = resetDone = false;
        preparedSceneId = null;
    }
    public void Dispose() { Cleanup(); disposed = true; }

    private sealed class RayResult
    {
        internal bool Hit;
        internal int Entity;
        internal GTA.Entity PhysicalEntity;
        internal GTA.Math.Vector3 Point, Normal;
    }
    // Read-only C13/facgate closing samples. Neither setter nor acceptance criteria belongs here.
    private void LogC13FacgateClosingSample(Operation operation, int elapsed, float ratio)
    {
        try
        {
            RequireDoor(operation.Door);
            GTA.Prop prop = operation.Door.Prop;
            GTA.Math.Vector3 position = prop.Position, rotation = prop.Rotation, velocity = prop.Velocity, min, max;
            Vector(position); Vector(rotation); Vector(velocity);
            prop.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
            float displacement = (position - operation.OpeningStartPosition).Length();
            int state = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, operation.Door.SystemId);
            int pending = GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_PENDING_STATE, operation.Door.SystemId);
            float automaticDistance = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_AUTOMATIC_DISTANCE, operation.Door.SystemId);
            bool physics = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, operation.Door.SystemId);
            if (!Finite(displacement) || !Finite(automaticDistance)) throw Error("C13 diagnostic actual distance is nonfinite");
            GTA.Ped player = GTA.Game.Player.Character;
            string playerInfo = "not_physical";
            if (!System.Object.ReferenceEquals(player, null) && player.Exists() && player.MemoryAddress != System.IntPtr.Zero &&
                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, player.Handle) == 1)
            {
                GTA.Math.Vector3 playerPosition = player.Position;
                Vector(playerPosition);
                GTA.Math.Vector3 nearest = prop.GetOffsetPosition(ClampBounds(prop.GetPositionOffset(playerPosition), min, max));
                Vector(nearest);
                playerInfo = "Handle:" + player.Handle + ",Pos:" + playerPosition + ",DistanceToInitialPivot:" + OpenNumber((playerPosition - operation.Door.Position).Length()) +
                    ",DistanceToActualBounds:" + OpenNumber((playerPosition - nearest).Length()) + ",CollisionEnabled:" + player.IsCollisionEnabled + ",Visible:" + player.IsVisible;
            }
            string peds = C13NearbyPhysicalSummary(GTA.World.GetNearbyPeds(operation.Door.Position, 8f), 1, prop, min, max);
            string vehicles = C13NearbyPhysicalSummary(GTA.World.GetNearbyVehicles(operation.Door.Position, 8f), 2, prop, min, max);
            Log("PORTA_C13_FECHAMENTO_AMOSTRA", "Cena=EP13_C03; Tipo=fechar; Handle=" + operation.Door.Handle +
                "; System=" + operation.Door.SystemId + "; TempoMs=" + elapsed + "; OperationElapsedMs=" + (GTA.Game.GameTime - operation.StartedAt) +
                "; Ratio=" + OpenNumber(ratio) + "; TargetRatio=0; State=" + state + "; PendingState=" + pending +
                "; AutomaticDistance=" + OpenNumber(automaticDistance) + "; AutomaticRateGetter=not_available; PhysicsLoaded=" + physics +
                "; Pos=" + position + "; Rot=" + rotation + "; Velocity=" + velocity + "; DisplacementFromCloseStart=" + OpenNumber(displacement) +
                "; CollisionEnabled=" + prop.IsCollisionEnabled + "; Frozen=" + prop.IsPositionFrozen + "; HasPhysics=" + prop.HasPhysics + "; IsStatic=" + prop.IsStatic +
                "; Player=" + playerInfo + "; NearbyPeds=" + peds + "; NearbyVehicles=" + vehicles +
                "; QueryRadius=8; MaximumInspectedPerType=32; BoundsProximityThreshold=1.5; SampleIntervalMs=250; DeadlineMs=" + operation.Spec.DuracaoMs + "; RatioThreshold=0.05; ReadOnly=true; PhysicalObstructionProven=false; ClosingApproval=false");
        }
        catch (System.Exception ex)
        {
            Log("PORTA_C13_FECHAMENTO_DIAGNOSTICO_ERRO", "Cena=EP13_C03; TempoMs=" + elapsed + "; ReadOnly=true; AcceptanceUnchanged=true; Message=" + ex.Message);
        }
    }
    private static string C13NearbyPhysicalSummary(GTA.Entity[] entities, int expectedType, GTA.Prop prop, GTA.Math.Vector3 min, GTA.Math.Vector3 max)
    {
        int inspected = 0, physical = 0, nearBounds = 0, collidingNearBounds = 0, nearestHandle = 0;
        float nearestDistance = float.MaxValue;
        GTA.Math.Vector3 nearestPosition = GTA.Math.Vector3.Zero;
        foreach (GTA.Entity entity in entities)
        {
            if (inspected++ >= 32) break;
            if (System.Object.ReferenceEquals(entity, null) || !entity.Exists() || entity.MemoryAddress == System.IntPtr.Zero ||
                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, entity.Handle) != expectedType) continue;
            GTA.Math.Vector3 position = entity.Position;
            Vector(position);
            GTA.Math.Vector3 near = prop.GetOffsetPosition(ClampBounds(prop.GetPositionOffset(position), min, max));
            Vector(near);
            float distance = (position - near).Length();
            if (!Finite(distance)) throw Error("C13 diagnostic proximity is nonfinite");
            physical++;
            if (distance <= 1.5f) { nearBounds++; if (entity.IsCollisionEnabled) collidingNearBounds++; }
            if (distance < nearestDistance) { nearestDistance = distance; nearestHandle = entity.Handle; nearestPosition = position; }
        }
        return "Returned:" + entities.Length + ",PhysicalInspected:" + physical + ",NearActualBounds:" + nearBounds +
            ",CollisionEnabledNearBounds:" + collidingNearBounds + ",NearestHandle:" + nearestHandle + ",NearestPos:" + nearestPosition +
            ",NearestBoundsDistance:" + (nearestHandle == 0 ? "not_found" : OpenNumber(nearestDistance)) + ",Truncated:" + (entities.Length > 32);
    }
    private bool IsC13FacgateOperation(Operation operation)
    {
        return preparedSceneId == "EP13_C03" && rayOptions == 4 && operation.Actor == null &&
            operation.Spec.Ator == null && (operation.Spec.Tipo == "abrir" || operation.Spec.Tipo == "fechar") &&
            operation.Door != null && operation.Door.Model == 569833973;
    }
    private void RequireC13MeasuredGateBeforeRegister(Operation operation, GTA.Prop prop, RayResult measured)
    {
        // Existing chainlink path is preserved; the SP alternative is one measured leaf, not a nearest-door fallback.
        if (System.Object.ReferenceEquals(prop, null) || !prop.Exists()) throw Error("C13 gate physical prop missing");
        int handle = prop.Handle;
        if (GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) != 3 || prop.MemoryAddress == System.IntPtr.Zero)
            throw Error("C13 gate must map a physical type3/address before model getters");
        System.IntPtr address = prop.MemoryAddress; int model = prop.Model.Hash; string name;
        if (!doorModels.TryGetValue(model, out name)) throw Error("C13 gate not in installed door whitelist");
        if (name == "prop_fnclink_03gate2") return;
        if (name != "prop_facgate_01" || model != 569833973 || operation.Actor != null || operation.Spec.Ator != null ||
            (operation.Spec.Tipo != "abrir" && operation.Spec.Tipo != "fechar"))
            throw Error("EP13_C03 physical gate differs from exact chainlink or measured SP facgate; no substitute registered before ARM");
        // Author SP pivot: InteriorsV commit c621bfd, interiors.cpp L1112. Native physical ray witness: 2026-10-07T05:48:36.
        GTA.Math.Vector3 authorPivot = new GTA.Math.Vector3(547.9453f, -1865.052f, 24.35131f);
        GTA.Math.Vector3 position = prop.Position, rotation = prop.Rotation, min, max;
        Vector(position); Vector(rotation); prop.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
        if ((position - authorPivot).Length() > 0.2f || (origin - authorPivot).Length() > 4f)
            throw Error("C13 SP facgate does not match the exact first authored leaf near this real scene origin");
        if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("C13 SP facgate native bounds are invalid");
        GTA.Math.Vector3 from = origin + new GTA.Math.Vector3(0f, 0f, 1.2f);
        GTA.Math.Vector3 near = prop.GetOffsetPosition(ClampBounds(prop.GetPositionOffset(from), min, max)); Vector(near);
        float boundsReach = (near - from).Length(), hitReach = (measured.Point - from).Length();
        if (!Finite(boundsReach) || !Finite(hitReach) || boundsReach > 1.8f || hitReach < 0.01f || hitReach > 1.8f)
            throw Error("C13 SP facgate is outside the unchanged real1.8m ray reach");
        if (!measured.Hit || measured.Entity != handle || System.Object.ReferenceEquals(measured.PhysicalEntity, null) || measured.PhysicalEntity.Handle != handle ||
            !prop.Exists() || prop.MemoryAddress != address || prop.Model.Hash != model)
            throw Error("C13 SP facgate actual ray/address/model identity changed before registration");
        Log("PORTA_C13_FACGATE_VINCULO", "Cena=EP13_C03; Handle=" + handle + "; Address=" + address.ToInt64().ToString("X", System.Globalization.CultureInfo.InvariantCulture) +
            "; Model=" + model + "; Name=" + name + "; Pos=" + position + "; Rot=" + rotation + "; Min=" + min + "; Max=" + max +
            "; RayPoint=" + measured.Point + "; RayNormal=" + measured.Normal + "; BoundsReach=" + OpenNumber(boundsReach) + "; HitReach=" + OpenNumber(hitReach) +
            "; Actorless=true; ExistsBeforeModel=true; Type3BeforeModel=true; AddressBeforeModel=true; FirstSPLeafOnly=true; Registered=false; OpeningProven=false; ClosingProven=false");
    }
    private bool IsBankOpening(Operation operation)
    {
        return preparedSceneId == "EP11_C01" && operation.Spec.Tipo == "abrir" &&
            System.String.Equals(operation.Spec.Ator, "DJANIRA_POBRE", System.StringComparison.OrdinalIgnoreCase);
    }
    private GTA.Math.Vector3 SelectMeasuredBankRay(Operation operation, GTA.Math.Vector3 from)
    {
        // Current map's physical leaf measured before any registration; not a new location mark.
        // PORTA_BANCO_POOL_MEDIDA 2026-10-07T05:49:15.7296992+00:00: model110411286, yaw-64.99991.
        GTA.Math.Vector3 measuredPosition = new GTA.Math.Vector3(231.5123f, 216.5177f, 106.4049f);
        string modelName;
        if (!doorModels.TryGetValue(110411286, out modelName) || modelName != "hei_prop_hei_bankdoor_new")
            throw Error("EP11_C01 measured bank model is absent from the installed PropList");
        GTA.Prop[] nearby = GTA.World.GetNearbyProps(operation.Actor.Position, 2.5f);
        GTA.Prop selected = null;
        foreach (GTA.Prop prop in nearby)
        {
            if (System.Object.ReferenceEquals(prop, null) || !prop.Exists() || prop.Model.Hash != 110411286) continue;
            GTA.Math.Vector3 position = prop.Position; Vector(position);
            if ((position - measuredPosition).Length() > 0.1f) continue;
            GTA.Math.Vector3 measuredRotation = prop.Rotation; Vector(measuredRotation);
            if (OpenAngleDifference(measuredRotation.Z, -65f) > 1f) continue; // PREP only: do not constrain rotation while opening.
            if (!System.Object.ReferenceEquals(selected, null) && selected.Handle != prop.Handle)
                throw Error("EP11_C01 measured bank leaf is not unique; no candidate adopted");
            selected = prop;
        }
        if (System.Object.ReferenceEquals(selected, null))
        {
            LogBankPhysicalCandidatesBeforeAbort(operation, from);
            throw Error("EP11_C01 measured bank leaf not found near own actor; no substitute door adopted");
        }
        operation.BankSelectedProp = selected; operation.BankSelectedHandle = selected.Handle;
        operation.BankSelectedAddress = selected.MemoryAddress;
        RequireSelectedBankProp(operation);
        GTA.Math.Vector3 min, max; selected.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
        if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("EP11_C01 selected leaf has invalid real bounds");
        GTA.Math.Vector3 near = selected.GetOffsetPosition(ClampBounds(selected.GetPositionOffset(from), min, max));
        Vector(near); GTA.Math.Vector3 delta = near - from; float distance = delta.Length();
        if (!Finite(distance) || distance < 0.01f || distance > 1.8f)
            throw Error("EP11_C01 measured leaf bounds are outside the original 1.8m ray reach; move physically before mounting");
        GTA.Math.Vector3 to = from + delta / distance * 1.8f; Vector(to);
        Log("PORTA_BANCO_SELECAO_PREPARO", "Cena=" + preparedSceneId + "; Handle=" + selected.Handle +
            "; Model=110411286; Pos=" + selected.Position + "; Rot=" + selected.Rotation +
            "; From=" + from + "; NearestBounds=" + near + "; Distance=" + OpenNumber(distance) +
            "; To=" + to + "; RayLength=1.8; RayHitPending=true; Registered=false; OpeningProven=false; OtherLeafModified=false");
        return to;
    }
    // Readonly bank-only evidence if the current measured selector still aborts.
    // Pool CObject guards precede Model; no nonphysical ray GUID is inspected.
    private void LogBankPhysicalCandidatesBeforeAbort(Operation operation, GTA.Math.Vector3 rayFrom)
    {
        try
        {
            if (!IsBankOpening(operation)) throw Error("bank pool diagnostic is restricted to EP11_C01 own opening");
            RequireActor(operation); Vector(rayFrom);
            GTA.Math.Vector3 at = operation.Actor.Position, heading = operation.Actor.ForwardVector;
            Vector(at); Vector(heading);
            GTA.Prop[] nearby = GTA.World.GetNearbyProps(at, 2.5f);
            int inspected = 0, reported = 0;
            Log("PORTA_BANCO_POOL_INICIO", "Cena=EP11_C01; Actor=" + operation.Actor.Handle +
                "; At=" + at + "; RayFrom=" + rayFrom + "; Radius=2.5; PoolProps=" + nearby.Length +
                "; MaxInspected=64; MaxWhitelistReports=24; ReadOnly=true; HistoricalLeafMissing=true; SelectionUnchanged=true; RayNotStarted=true");
            foreach (GTA.Prop prop in nearby)
            {
                if (inspected++ >= 64 || reported >= 24) break;
                if (System.Object.ReferenceEquals(prop, null)) continue;
                int handle = prop.Handle;
                bool exists = handle != 0 && prop.Exists();
                int type = exists ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) : 0;
                System.IntPtr address = exists && type == 3 ? prop.MemoryAddress : System.IntPtr.Zero;
                Log("PORTA_BANCO_POOL_HEADER", "Cena=EP11_C01; Handle=" + handle + "; Exists=" + exists +
                    "; EntityType=" + type + "; AddressMapped=" + (address != System.IntPtr.Zero) +
                    "; ReadOnly=true; ModelGetterPending=" + (exists && type == 3 && address != System.IntPtr.Zero));
                if (!exists || type != 3 || address == System.IntPtr.Zero) continue;
                GTA.Model actualModel = prop.Model; int model = actualModel.Hash; string name;
                bool whitelisted = doorModels.TryGetValue(model, out name);
                Log("PORTA_BANCO_POOL_MODELO", "Cena=EP11_C01; Handle=" + handle + "; Model=" + model +
                    "; InstalledDoorWhitelist=" + whitelisted + "; Name=" + (whitelisted ? name : "not_in_installed_door_whitelist") +
                    "; ExistsBeforeModel=true; Type3BeforeModel=true; AddressBeforeModel=true; ReadOnly=true; BoundsPending=" + whitelisted);
                if (!whitelisted) continue;
                GTA.Math.Vector3 position = prop.Position, rotation = prop.Rotation, min, max;
                Vector(position); Vector(rotation); actualModel.GetDimensions(out min, out max); Vector(min); Vector(max);
                if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("bank diagnostic real bounds are invalid");
                GTA.Math.Vector3 nearActor = prop.GetOffsetPosition(ClampBounds(prop.GetPositionOffset(at), min, max));
                GTA.Math.Vector3 nearRay = prop.GetOffsetPosition(ClampBounds(prop.GetPositionOffset(rayFrom), min, max));
                Vector(nearActor); Vector(nearRay);
                float distanceActor = (nearActor - at).Length(), distanceRay = (nearRay - rayFrom).Length();
                if (!Finite(distanceActor) || !Finite(distanceRay)) throw Error("bank diagnostic distances are nonfinite");
                int system = 0; bool found; System.IntPtr output = Allocate();
                try
                {
                    found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR,
                        position.X, position.Y, position.Z, model, output);
                    if (found) system = System.Runtime.InteropServices.Marshal.ReadInt32(output);
                }
                finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(output); }
                bool physics = found && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, system);
                string state = found ? GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, system).ToString(System.Globalization.CultureInfo.InvariantCulture) : "not_registered";
                string ratio = "not_available";
                if (physics)
                {
                    float measuredRatio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, system);
                    if (!Finite(measuredRatio)) throw Error("bank diagnostic door ratio is nonfinite");
                    ratio = OpenNumber(measuredRatio);
                }
                if (!prop.Exists() || GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) != 3 ||
                    prop.Handle != handle || prop.MemoryAddress != address || prop.Model.Hash != model)
                    throw Error("bank diagnostic physical object changed identity during getters");
                Log("PORTA_BANCO_POOL_MEDIDA", "Cena=EP11_C01; Handle=" + handle + "; Address=" + address.ToInt64().ToString("X", System.Globalization.CultureInfo.InvariantCulture) +
                    "; Model=" + model + "; Name=" + name + "; Pos=" + position + "; Rot=" + rotation +
                    "; Min=" + min + "; Max=" + max + "; NearestActorBounds=" + nearActor + "; DistanceActorBounds=" + OpenNumber(distanceActor) +
                    "; NearestRayBounds=" + nearRay + "; DistanceRayBounds=" + OpenNumber(distanceRay) +
                    "; ForwardProjection=" + OpenNumber(GTA.Math.Vector3.Dot(nearActor - at, heading)) +
                    "; ExistingDoor=" + found + "; System=" + system + "; PhysicsLoaded=" + physics + "; State=" + state + "; Ratio=" + ratio +
                    "; ReadOnly=true; IdentityStable=true; Registered=false; Adopted=false; OpeningProven=false; PassageProven=false");
                reported++;
            }
            Log("PORTA_BANCO_POOL_FIM", "Cena=EP11_C01; Inspected=" + System.Math.Min(inspected, 64) + "; WhitelistReports=" + reported +
                "; PoolProps=" + nearby.Length + "; ReadOnly=true; HistoricalSelectorStillAborts=true; SelectionUnchanged=true");
        }
        catch (System.Exception ex)
        {
            Log("PORTA_BANCO_POOL_ERRO", ex.Message + "; ReadOnly=true; HistoricalSelectorStillAborts=true; SelectionUnchanged=true");
        }
    }
    private static void RequireSelectedBankProp(Operation operation)
    {
        GTA.Prop prop = operation.BankSelectedProp;
        if (System.Object.ReferenceEquals(prop, null) || !prop.Exists() || prop.Handle != operation.BankSelectedHandle ||
            prop.MemoryAddress != operation.BankSelectedAddress || operation.BankSelectedAddress == System.IntPtr.Zero || prop.Model.Hash != 110411286)
            throw Error("EP11_C01 selected physical leaf lost identity; do not adopt a new entity");
    }
    private int StartRay(GTA.Math.Vector3 a, GTA.Math.Vector3 b, int ignore)
    {
        LoadingStep("RAY_START_BEFORE", "From=" + a + "; To=" + b + "; Ignore=" + ignore + "; Flags=17; Options=" + rayOptions);
        int ray = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE, a.X, a.Y, a.Z, b.X, b.Y, b.Z, 17, ignore, rayOptions);
        LoadingStep("RAY_START_AFTER", "Ray=" + ray);
        if (ray == 0) throw Error("raycast não iniciou");
        return ray;
    }
    private bool ReadRay(int ray, out RayResult result)
    {
        result = null;
        GTA.ShapeTestHandle probe = new GTA.ShapeTestHandle();
        probe.NativeValue = unchecked((ulong)(uint)ray);
        GTA.ShapeTestResult measured;
        LoadingStep("RAY_RESULT_BEFORE", "Ray=" + ray + "; Reader=API191");
        GTA.ShapeTestStatus status = probe.GetResult(out measured);
        LoadingStep("RAY_RESULT_" + status, "Ray=" + ray + "; Reader=API191; NonPhysicalGuidCleanup=API");
        if (status == GTA.ShapeTestStatus.NotReady) return false;
        if (status != GTA.ShapeTestStatus.Ready) throw Error("raycast não concluiu");
        GTA.Entity physical = null;
        if (measured.DidHit)
        {
            LoadingStep("HIT_PHYSICAL_BEFORE", "Ray=" + ray);
            measured.TryGetHitEntity(out physical);
            LoadingStep("HIT_PHYSICAL_AFTER", "Ray=" + ray + "; Physical=" + !System.Object.ReferenceEquals(physical, null) +
                "; Handle=" + (System.Object.ReferenceEquals(physical, null) ? 0 : physical.Handle));
        }
        Vector(measured.HitPosition); Vector(measured.SurfaceNormal);
        Log("PORTA_SONDA_MEDIDA", "Ray=" + ray + "; Reader=API191; DidHit=" + measured.DidHit +
            "; Physical=" + !System.Object.ReferenceEquals(physical, null) +
            "; Handle=" + (System.Object.ReferenceEquals(physical, null) ? 0 : physical.Handle) +
            "; Point=" + measured.HitPosition + "; Normal=" + measured.SurfaceNormal +
            "; NonPhysicalEntityGetters=False");
        result = new RayResult { Hit = measured.DidHit,
            Entity = System.Object.ReferenceEquals(physical, null) ? 0 : physical.Handle,
            PhysicalEntity = physical, Point = measured.HitPosition, Normal = measured.SurfaceNormal };
        return true;
    }
    // Somente diagnostico: pool CObject da API191, nunca modelo de guid
    // nao fisico do ray. Uma porta proxima nao substitui o hit exigido.
    private void LogNearbyPhysicalDoors(Operation operation, RayResult measured)
    {
        try
        {
            GTA.Math.Vector3 at = operation.Actor == null ? origin : operation.Actor.Position;
            GTA.Math.Vector3 heading = operation.Actor == null ? forward : operation.Actor.ForwardVector;
            Vector(at); Vector(heading);
            if (preparedSceneId == "EP13_C03" && rayOptions == 4 && operation.Actor == null)
            {
                // Primary resource anchor queries a DoorSystem table; it is NOT a measured prop/mark.
                GTA.Math.Vector3 gateAuthorAnchor = new GTA.Math.Vector3(546.17f, -1869.94f, 24.53f);
                if ((at - gateAuthorAnchor).Length() > 4f)
                {
                    Log("PORTA_C13_SISTEMA_BUSCA_IGNORADA", "Cena=EP13_C03; Source=am_doors.c:253; SceneOrigin=" + at +
                        "; AuthorAnchor=" + gateAuthorAnchor + "; ReadOnly=true; Reason=outside_original4m_vicinity; NoDistantDoorQueried=true; NoHitUnchanged=true");
                }
                else
                {
                int gateAuthorModel = new GTA.Model("prop_fnclink_03gate2").Hash;
                string gateInstalledName;
                if (!doorModels.TryGetValue(gateAuthorModel, out gateInstalledName) || gateInstalledName != "prop_fnclink_03gate2")
                    throw Error("C13 readonly gate table model differs from installed PropList");
                int gatePrimarySystem = -1736447128; // am_doors.c case253 f_0 is system ID, not model.
                int gateFoundSystem = 0; bool gateFound;
                System.IntPtr gateOutput = Allocate(); // Same zeroed 24-byte allocation as existing door reader.
                try
                {
                    gateFound = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR,
                        gateAuthorAnchor.X, gateAuthorAnchor.Y, gateAuthorAnchor.Z, gateAuthorModel, gateOutput);
                    if (gateFound) gateFoundSystem = System.Runtime.InteropServices.Marshal.ReadInt32(gateOutput);
                }
                finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(gateOutput); }
                Log("PORTA_C13_SISTEMA_BUSCA", "Cena=EP13_C03; Source=am_doors.c:253; AuthorAnchor=" + gateAuthorAnchor +
                    "; AuthorAnchorMeasured=false; ModelName=prop_fnclink_03gate2; Model=" + gateAuthorModel +
                    "; PrimarySystem=" + gatePrimarySystem + "; LookupRadius=0.5; Found=" + gateFound + "; FoundSystem=" + gateFoundSystem +
                    "; SceneOrigin=" + at + "; SceneOriginDistanceToAuthor=" + (at - gateAuthorAnchor).Length().ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; ReadOnly=true; RegisteredByDiagnostic=false; Adopted=false; PhysicalEntityMeasured=false; RayReplacement=false; ReadyPermission=false");
                for (int gateIndex = 0; gateIndex < 2; gateIndex++)
                {
                    if (gateIndex == 1 && (!gateFound || gateFoundSystem == gatePrimarySystem)) continue;
                    int gateSystemId = gateIndex == 0 ? gatePrimarySystem : gateFoundSystem;
                    bool gateRegistered = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_DOOR_REGISTERED_WITH_SYSTEM, gateSystemId);
                    bool gatePhysics = gateRegistered && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, gateSystemId);
                    string gateState = gateRegistered ? GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, gateSystemId).ToString(System.Globalization.CultureInfo.InvariantCulture) : "not_registered";
                    string gateRatio = "not_available";
                    if (gatePhysics)
                    {
                        float gateNativeRatio = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, gateSystemId);
                        if (!Finite(gateNativeRatio)) throw Error("C13 readonly DoorSystem ratio is not finite");
                        gateRatio = gateNativeRatio.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    Log("PORTA_C13_SISTEMA_ESTADO", "Cena=EP13_C03; System=" + gateSystemId + "; MatchesPrimarySystem=" + (gateSystemId == gatePrimarySystem) +
                        "; MatchesLookup=" + (gateFound && gateSystemId == gateFoundSystem) + "; TableRegistered=" + gateRegistered +
                        "; PhysicsLoaded=" + gatePhysics + "; DoorState=" + gateState + "; Ratio=" + gateRatio +
                        "; DoorTableGettersOnly=true; PhysicalEntityMeasured=false; ReadOnly=true; RegisteredByDiagnostic=false; Adopted=false; RayReplacement=false; ReadyPermission=false; NoHitUnchanged=true");
                }
                }
                // Readonly diagnosis after no-hit: no model/coord getter on a nonphysical GUID.
                foreach (string expectedName in new string[] { "prop_fnclink_03gate2", "prop_fnclink_03gate1" })
                {
                    int expectedModel = new GTA.Model(expectedName).Hash;
                    string installedName;
                    if (!doorModels.TryGetValue(expectedModel, out installedName) || installedName != expectedName)
                        throw Error("C13 gate diagnostic model differs from installed PropList");
                    int found = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_CLOSEST_OBJECT_OF_TYPE,
                        at.X, at.Y, at.Z, 4f, expectedModel, false, false, false);
                    bool exists = found != 0 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, found);
                    int type = exists ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, found) : 0;
                    Log("PORTA_C13_MODELO_PROXIMO", "Cena=EP13_C03; ExpectedName=" + expectedName + "; ExpectedModel=" + expectedModel +
                        "; Origin=" + at + "; Radius=4; Handle=" + found + "; Exists=" + exists + "; EntityType=" + type +
                        "; ReadOnly=true; Registered=false; Adopted=false; NoHitUnchanged=true; PhysicalGettersPending=" + (exists && type == 3));
                    if (!exists || type != 3) continue;
                    int actualModel = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, found);
                    GTA.Math.Vector3 actualPosition = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ENTITY_COORDS, found, false);
                    GTA.Math.Vector3 actualRotation = GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ENTITY_ROTATION, found, 2);
                    Vector(actualPosition); Vector(actualRotation);
                    GTA.Prop physical = GTA.Entity.FromHandle(found) as GTA.Prop;
                    bool mapped = !System.Object.ReferenceEquals(physical, null) && physical.Exists() && physical.MemoryAddress != System.IntPtr.Zero;
                    Log("PORTA_C13_MODELO_FISICO", "Cena=EP13_C03; ExpectedName=" + expectedName + "; Handle=" + found +
                        "; Model=" + actualModel + "; ModelMatches=" + (actualModel == expectedModel) + "; Pos=" + actualPosition +
                        "; Rot=" + actualRotation + "; PhysicalObjectType=3; AddressMapped=" + mapped +
                        "; ReadOnly=true; Registered=false; Adopted=false; BoundsPending=" + (mapped && actualModel == expectedModel));
                    if (!mapped || actualModel != expectedModel) continue;
                    GTA.Math.Vector3 min, max; physical.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
                    Log("PORTA_C13_MODELO_BOUNDS", "Cena=EP13_C03; ExpectedName=" + expectedName + "; Handle=" + found +
                        "; Model=" + actualModel + "; Min=" + min + "; Max=" + max +
                        "; ExistsBeforeModel=true; Type3BeforeModel=true; AddressBeforeModel=true; ReadOnly=true; Registered=false; Adopted=false");
                }
            }
            GTA.Prop[] nearby = GTA.World.GetNearbyProps(at, 2.5f);
            int reported = 0, nearestHandle = 0; float nearestDistance = float.MaxValue;
            foreach (GTA.Prop prop in nearby)
            {
                if (System.Object.ReferenceEquals(prop, null) || !prop.Exists()) continue;
                int model = prop.Model.Hash;
                string name; if (!doorModels.TryGetValue(model, out name)) continue;
                if (reported >= 24) break;
                GTA.Math.Vector3 min, max; prop.Model.GetDimensions(out min, out max);
                Vector(min); Vector(max);
                GTA.Math.Vector3 near = prop.GetOffsetPosition(ClampBounds(prop.GetPositionOffset(at), min, max));
                GTA.Math.Vector3 hitNearest = prop.GetOffsetPosition(ClampBounds(prop.GetPositionOffset(measured.Point), min, max));
                Vector(near); Vector(hitNearest);
                float distance = (near - at).Length();
                if (distance < nearestDistance) { nearestDistance = distance; nearestHandle = prop.Handle; }
                int system = 0; bool found;
                System.IntPtr output = Allocate();
                try
                {
                    GTA.Math.Vector3 position = prop.Position;
                    found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR,
                        position.X, position.Y, position.Z, model, output);
                    if (found) system = System.Runtime.InteropServices.Marshal.ReadInt32(output);
                }
                finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(output); }
                bool physics = found && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, system);
                string state = found ? GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, system).ToString(System.Globalization.CultureInfo.InvariantCulture) : "not_registered";
                string ratio = physics ? GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, system).ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "not_available";
                Log("PORTA_PROXIMA_FISICA", "Ator=" + operation.Spec.Ator + "; Handle=" + prop.Handle + "; Modelo=" + name +
                    "; Hash=" + model + "; Pos=" + prop.Position + "; Rot=" + prop.Rotation +
                    "; Min=" + min + "; Max=" + max + "; PontoProximo=" + near + "; DistanciaAtriz=" + distance +
                    "; Frente=" + GTA.Math.Vector3.Dot(near - at, heading) + "; HitPoint=" + measured.Point +
                    "; DistanciaHitBounds=" + (measured.Point - hitNearest).Length() +
                    "; CollisionEnabled=" + prop.IsCollisionEnabled + "; ModelCollisionLoaded=" + prop.Model.IsCollisionLoaded +
                    "; HasPhysics=" + prop.HasPhysics + "; IsStatic=" + prop.IsStatic + "; Frozen=" + prop.IsPositionFrozen +
                    "; ExistingDoor=" + found + "; System=" + system + "; PhysicsLoaded=" + physics +
                    "; DoorState=" + state + "; Ratio=" + ratio + "; ReadOnly=True; Adopted=False");
                reported++;
            }
            Log("PORTA_PROXIMAS_RESUMO", "Ator=" + operation.Spec.Ator + "; Raio=2.5; NearbyPhysicalProps=" + nearby.Length +
                "; WhitelistDoorsReported=" + reported + "; NearestHandle=" + nearestHandle +
                "; NearestDistance=" + (nearestHandle == 0 ? "not_found" : nearestDistance.ToString("R", System.Globalization.CultureInfo.InvariantCulture)) +
                "; ReadOnly=True; ActorPosition=" + at + "; ActorHeading=" + (operation.Actor == null ? "not_actor" : operation.Actor.Heading.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }
        catch (System.Exception ex) { Log("PORTA_DIAGNOSTICO_ERRO", ex.Message + "; nenhuma porta adotada/alterada"); }
    }
    private static GTA.Math.Vector3 ClampBounds(GTA.Math.Vector3 point, GTA.Math.Vector3 min, GTA.Math.Vector3 max)
    {
        Vector(point);
        return new GTA.Math.Vector3(System.Math.Max(min.X, System.Math.Min(max.X, point.X)),
            System.Math.Max(min.Y, System.Math.Min(max.Y, point.Y)), System.Math.Max(min.Z, System.Math.Min(max.Z, point.Z)));
    }
    private static System.IntPtr Allocate() { System.IntPtr p = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24); System.Runtime.InteropServices.Marshal.Copy(new byte[24], 0, p, 24); return p; }
    private void ReadDoorModels()
    {
        if (System.String.IsNullOrEmpty(propListFile) || !System.String.Equals(System.IO.Path.GetFileName(propListFile), "PropList.txt", System.StringComparison.OrdinalIgnoreCase)) throw Error("forneça a PropList instalada");
        byte[] raw = System.IO.File.ReadAllBytes(propListFile);
        string hash;
        using (System.Security.Cryptography.SHA256 algorithm = System.Security.Cryptography.SHA256.Create()) hash = System.BitConverter.ToString(algorithm.ComputeHash(raw)).Replace("-", "");
        if (hash != "35BFD924919FC223F5AB131941643B2AE4E0082FCD0ECEF1A5A50FCDEE9B2552") throw Error("PropList mudou; revisar modelos antes de abrir porta");
        foreach (string line in System.Text.Encoding.UTF8.GetString(raw).Split('\n'))
        {
            string name = line.Trim();
            if (name != "v_ilev_trevtraildr" && name.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("gate", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "\\A[A-Za-z0-9_]+\\z")) continue;
            int hashModel = new GTA.Model(name).Hash; if (!doorModels.ContainsKey(hashModel)) doorModels.Add(hashModel, name);
        }
    }
    private static void VerifyKnockSource(DoorSpec spec)
    {
        if (System.String.IsNullOrEmpty(spec.OrigemArquivo) || System.IO.Path.GetFileName(spec.OrigemArquivo) != "PedAnimList.txt") throw Error("batida sem fonte instalada");
        byte[] raw = System.IO.File.ReadAllBytes(spec.OrigemArquivo); string digest;
        using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create()) digest = System.BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "");
        string[] lines = System.Text.Encoding.UTF8.GetString(raw).Split('\n');
        if (!System.String.Equals(digest, spec.OrigemSha256, System.StringComparison.OrdinalIgnoreCase) || spec.OrigemLinha < 1 || spec.OrigemLinha > lines.Length ||
            lines[spec.OrigemLinha - 1].Trim() != spec.Dicionario + " " + spec.Clipe) throw Error("batida diverge da linha/SHA da origem");
    }
    private GTA.Math.Vector3 Resolve(GTA.Math.Vector3 relative) { return origin + right * relative.X + forward * relative.Y + new GTA.Math.Vector3(0f, 0f, relative.Z); }
    private static bool StillActor(Operation operation) { return operation.Actor != null && operation.Actor.Exists() && operation.Actor.Handle == operation.Handle && operation.Actor.MemoryAddress == operation.Address && operation.Actor.Model.Hash == operation.Model && !operation.Actor.IsPlayer; }
    private static void RequireActor(Operation operation) { if (operation.Actor == null && operation.Spec.Ator == null && (operation.Spec.Tipo == "abrir" || operation.Spec.Tipo == "fechar")) return; if (!StillActor(operation) || operation.Actor.IsDead || operation.Actor.IsInVehicle()) throw Error("ator da porta mudou/entrou em veículo"); }
    private static void RequireDoor(DoorState door) { if (!door.Prop.Exists() || door.Prop.Handle != door.Handle || door.Prop.Model.Hash != door.Model || door.Prop.MemoryAddress != door.Address) throw Error("porta real perdeu identidade; não tocar outra entidade"); }
    private static void RequirePed(GTA.Ped ped) { if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsPlayer || ped.IsDead || !ped.IsPersistent || ped.IsInVehicle()) throw Error("ator precisa ser próprio/vivo/a pé"); }
    private void RequireReady() { Active(); if (!IsReady) throw Error("aguarde identificação física da porta/passagem"); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("PortasSerieDiretor"); }
    private static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    private static void Vector(GTA.Math.Vector3 x) { if (!Finite(x.X) || !Finite(x.Y) || !Finite(x.Z)) throw Error("vetor não finito"); }
    private static float Horizontal(GTA.Math.Vector3 x) { return (float)System.Math.Sqrt(x.X * x.X + x.Y * x.Y); }
    private void LoadingStep(string step, string value)
    { if (loadingSteps.Add(step)) Log("PORTA_CARGA_PASSO", "Step=" + step + "; " + value); }
    private void Log(string tag, string value) { if (log != null) log(tag, value); }
    private static System.InvalidOperationException Error(string value) { return new System.InvalidOperationException("Portas da série: " + value); }
}

// EP05_C04: armario cenografico proprio, porta fisica, contato e duas roupas reais.
// O modulo nao substitui o JSON canonico nem adota moveis/portas do mapa.
public sealed class ArmarioSerieDiretor : System.IDisposable
{
    private const string DoorModel = "prop_wardrobe_door_01";
    private const string ClipDictionary = "mini@safe_cracking", ClipName = "door_open_succeed_stand";
    private const int DurationMs = 8000, LoadTimeoutMs = 15000;
    private const int ApproachMaxMs = 1500, HoldMinMs = 500;
    private const int ClipBudgetMs = DurationMs - ApproachMaxMs - HoldMinMs;
    private readonly System.Action<string, string> log;
    private readonly string menyoo;
    private readonly System.Collections.Generic.List<GTA.Model> requested = new System.Collections.Generic.List<GTA.Model>();
    private readonly System.Collections.Generic.Dictionary<string, Mark> marks = new System.Collections.Generic.Dictionary<string, Mark>();
    private readonly System.Collections.Generic.List<Probe> probes = new System.Collections.Generic.List<Probe>();
    private readonly System.Collections.Generic.Dictionary<string, int> sourceModels = new System.Collections.Generic.Dictionary<string, int>
    {
        { DoorModel, 14506 }, { "prop_ld_tshirt_01", 12261 }
    };
    private sealed class Mark { internal GTA.Math.Vector3 Position, Rotation; }
    private sealed class Probe { internal GTA.Math.Vector3 A, B, Point; internal int Ray, Expected, StartedAt; internal float Height; }
    private sealed class Hit { internal bool HasHit; internal int Entity; internal GTA.Math.Vector3 Point, Normal; }
    private sealed class Packing
    {
        internal string BoxName;
        internal float FirstAngle, SecondAngle;
        internal float FirstX, FirstY, SecondX, SecondY;
    }
    private GTA.Ped actor;
    private GTA.Prop door;
    private ObjetosSerieDiretor doorOwner;
    private ObjetosDiretor resources;
    private int actorHandle, actorModel, actorHealth, doorHandle, doorHash, systemId, phase, probeIndex, calibration;
    private int clipStartedAt, resetStartedAt, nextLog, nextLook, contactRay, contactStartedAt, calibrationStartedAt;
    private System.IntPtr actorAddress, doorAddress;
    private System.Diagnostics.Stopwatch loading;
    private long nextLoadingLogMs;
    private GTA.Math.Vector3 actorStart, nearPoint, closedPosition, closedRotation, doorMin, doorMax, center, right, normal;
    private float width, height, thickness, floor, clipDuration, maximumPhase, initialRightDistance, initialLeftDistance, openSign;
    private GTA.Bone contactBone;
    private bool prepared, ready, disposed, running, finished, resetDone, resetPending, contact, clipDone, doorOpen, returning, turningBack, facingClothes, openingRequested;
    private int nextPhysicsLogAt;
    private bool dictionaryRequested, dictionaryWasLoaded, modelsLoaded, built, calibrated, addedDoor;
    private Packing packing;
    private bool doorWidthOnX;
    private float shelfMinRight, shelfMaxRight, shelfMinNormal, shelfMaxNormal, shelfTop;

    public ArmarioSerieDiretor(System.Action<string, string> writeLog, string pastaMenyoo)
    { log = writeLog; menyoo = pastaMenyoo; }
    public static bool Supports(string id) { return id == "EP05_C04"; }
    public static int ExpectedDurationMs { get { return DurationMs; } }
    public bool IsReady { get { return prepared && ready && !disposed; } }
    public bool IsFinished { get { return IsReady && finished; } }
    public bool OwnActorsReleased { get { return !running; } }
    public bool ControlsBody(string id, int elapsed)
    { return IsReady && id == "DJANIRA_POBRE" && elapsed >= 0 && elapsed < DurationMs && !finished; }
    public GTA.Entity[] OwnedEntities
    {
        get
        {
            var all = new System.Collections.Generic.List<GTA.Entity>();
            if (OwnDoor()) all.Add(door);
            if (resources != null && resources.IsReady)
                foreach (string id in marks.Keys) all.Add(resources.GetProp(id));
            return all.ToArray();
        }
    }
    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> roster)
    {
        Active();
        if (prepared || roster == null || roster.Count != 1 || !roster.TryGetValue("DJANIRA_POBRE", out actor) ||
            actor == null || !actor.Exists() || actor.IsDead || actor.IsInVehicle()) throw Error("exige somente a Djanira propria viva, a pe");
        actorHandle = actor.Handle; actorModel = actor.Model.Hash; actorAddress = actor.MemoryAddress; actorHealth = actor.Health;
        actorStart = actor.Position; Vector(actorStart);
        foreach (GTA.Bone bone in new GTA.Bone[] { GTA.Bone.SkelRightHand, GTA.Bone.SkelLeftHand, GTA.Bone.SkelRightFoot })
            if (!actor.Bones[bone].IsValid) throw Error("osso real indisponivel: " + bone);
        VerifySources();
        loading = System.Diagnostics.Stopwatch.StartNew(); prepared = true;
        dictionaryWasLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, ClipDictionary);
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, ClipDictionary); dictionaryRequested = true;
        foreach (string name in sourceModels.Keys)
        {
            GTA.Model model = new GTA.Model(name);
            if (!model.IsInCdImage || !model.IsProp)
            {
                throw Error("modelo real indisponivel: " + name);
            }
            if (!model.IsLoaded) { model.Request(); requested.Add(model); }
            model.RequestCollision();
        }
    }
    public void BindOwnDoor(ObjetosSerieDiretor ownObjects)
    {
        Active();
        if (!prepared || ready || door != null || ownObjects == null || !ownObjects.IsReady) throw Error("vincule a folha propria pronta antes do preparo");
        doorOwner = ownObjects; door = ownObjects.GetProp("ARMARIO_DJANIRA");
        if (door.Model.Hash != new GTA.Model(DoorModel).Hash || !door.Exists()) throw Error("ARMARIO_DJANIRA precisa ser a folha real catalogada");
        doorHandle = door.Handle; doorHash = door.Model.Hash; doorAddress = door.MemoryAddress;
        closedPosition = door.Position; closedRotation = door.Rotation;
    }
    public void PollLoading()
    {
        Active(); if (!prepared || ready) return;
        RequireActor();
        LogLoadingGetters(); // Observar todos os modelos antes de qualquer retorno/timeout.
        if (loading.ElapsedMilliseconds >= LoadTimeoutMs) throw Error("estrutura/roupas/porta nao ficaram fisicamente prontas em15s");
        if (door == null) return;
        RequireDoor();
        if (!modelsLoaded)
        {
            bool streamingReady = true;
            foreach (string name in sourceModels.Keys)
            {
                GTA.Model model = new GTA.Model(name);
                if (!model.IsInCdImage || !model.IsProp) throw Error("modelo obrigatorio indisponivel: " + name);
                bool loaded = model.IsLoaded, collisionLoaded = model.IsCollisionLoaded;
                if (!loaded)
                {
                    model.Request();
                    if (!requested.Contains(model)) requested.Add(model);
                }
                // O pedido inicial pode anteceder o modelo; repetir entre frames,
                // inclusive depois de IsLoaded=true, sem aceitar colisao ausente.
                if (!collisionLoaded) model.RequestCollision();
                if (!loaded || !collisionLoaded) streamingReady = false;
            }
            if (!streamingReady) return;
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, ClipDictionary)) return;
            clipDuration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, ClipDictionary, ClipName);
            if (!Finite(clipDuration) || clipDuration <= 0f || clipDuration * 1000f > ClipBudgetMs)
                throw Error("gesto completo nao cabe no orcamento derivado: DuracaoMs=" + DurationMs + "; AproximacaoMaxMs=" + ApproachMaxMs + "; HoldMinMs=" + HoldMinMs + "; ClipeBudgetMs=" + ClipBudgetMs + "; ClipeDuracaoSegundos=" + Number(clipDuration));
            Geometry(); modelsLoaded = true;
            BeginFloorProbes(); return;
        }
        if (!built)
        {
            if (!PollProbes()) return;
            if (resources == null) { Build(); return; }
            resources.Update(); if (!resources.IsReady) return;
            LayoutAndSupports(); built = true; return;
        }
        if (!PollProbes()) return;
        if (!calibrated) { if (!CalibrateDoor()) return; calibrated = true; }
        ready = true; resetDone = true;
        Log("ARMARIO_PRONTO", "Props=7; roupas=2; apoio/colisao/folha reais; modeloFolha=" + DoorModel + "; ApoioModelo=" + packing.BoxName + "; clipCompleto=" + Number(clipDuration) + "; aberturaSign=" + Number(openSign) + "; visualPendente=true");
    }
    private void LogLoadingGetters()
    {
        long elapsed = loading.ElapsedMilliseconds;
        if (elapsed < nextLoadingLogMs) return;
        nextLoadingLogMs = elapsed + 1000;
        foreach (string name in sourceModels.Keys)
        {
            GTA.Model model = new GTA.Model(name);
            Log("ARMARIO_STREAMING_GETTERS", "TempoCargaMs=" + elapsed + "; Modelo=" + name +
                "; Hash=" + model.Hash + "; IsInCdImage=" + model.IsInCdImage + "; IsProp=" + model.IsProp +
                "; IsLoaded=" + model.IsLoaded + "; IsCollisionLoaded=" + model.IsCollisionLoaded);
        }
        Log("ARMARIO_ANIM_GETTERS", "TempoCargaMs=" + elapsed + "; Dictionary=" + ClipDictionary + "; Clip=" + ClipName +
            "; Loaded=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, ClipDictionary) +
            "; ModelsProntos=" + modelsLoaded + "; FolhaVinculada=" + (door != null) + "; EstruturaPronta=" + built);
        LogDoorPhysics("carga_1s");
    }
    private void LogDoorPhysics(string stage)
    {
        if (!OwnDoor())
        {
            Log("ARMARIO_PORTA_GETTERS", "Fase=" + stage + "; OwnDoor=False; AddedDoor=" + addedDoor);
            return;
        }
        bool registered = addedDoor && GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_DOOR_REGISTERED_WITH_SYSTEM, systemId);
        bool physicsLoaded = registered && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, systemId);
        Log("ARMARIO_PORTA_GETTERS", "Fase=" + stage + "; Handle=" + doorHandle + "; Modelo=" + DoorModel +
            "; SystemId=" + systemId + "; AddedDoor=" + addedDoor + "; Registered=" + registered +
            "; PhysicsLoaded=" + physicsLoaded + "; Calibration=" + calibration +
            "; IsPositionFrozen=" + door.IsPositionFrozen + "; HasPhysics=" + door.HasPhysics +
            "; IsStatic=" + door.IsStatic + "; CollisionEnabled=" + door.IsCollisionEnabled +
            "; ModelCollisionLoaded=" + door.Model.IsCollisionLoaded + "; Pos=" + door.Position +
            "; Rot=" + door.Rotation + "; Speed=" + Number(door.Speed) +
            "; Ratio=" + (physicsLoaded ? Number(Ratio()) : "not_available_before_physics"));
    }
    private void Geometry()
    {
        door.Model.GetDimensions(out doorMin, out doorMax); Vector(doorMin); Vector(doorMax);
        GTA.Math.Vector3 size = doorMax - doorMin;
        bool xWidth = size.X >= size.Y; doorWidthOnX = xWidth;
        width = xWidth ? size.X : size.Y; thickness = xWidth ? size.Y : size.X; height = size.Z;
        if (width < 0.6f || width > 1.8f || height < 1.5f || height > 3f || thickness <= 0f || thickness > 0.35f || System.Math.Abs(closedRotation.X) > 1f || System.Math.Abs(closedRotation.Y) > 1f)
            throw Error("folha nao tem bounds adequados a um armario: Min=" + doorMin + "; Max=" + doorMax);
        center = door.GetOffsetPosition((doorMin + doorMax) * 0.5f);
        right = Unit(door.GetOffsetPosition(xWidth ? new GTA.Math.Vector3(1f, 0f, 0f) : new GTA.Math.Vector3(0f, 1f, 0f)) - door.Position);
        normal = new GTA.Math.Vector3(-right.Y, right.X, 0f);
        if (Dot(normal, center - actorStart) < 0f) normal = -normal;
        if (Horizontal(center - actorStart) < 0.9f || Horizontal(center - actorStart) > 2.5f) throw Error("folha precisa ficar de0.9 a2.5m reais da atriz, com passagem livre");
        // A marca de repouso deve manter os bounds reais do corpo fora de
        // toda a varredura da folha. A distancia ao centro so e diagnostico;
        // nao representa os bounds transformados nem um getter de capsula.
        float sweepRadius = 0f;
        foreach (float x in new float[] { doorMin.X, doorMax.X })
            foreach (float y in new float[] { doorMin.Y, doorMax.Y })
                sweepRadius = System.Math.Max(sweepRadius, Horizontal(new GTA.Math.Vector3(x, y, 0f)));
        float sweepClearance = Horizontal(actorStart - closedPosition) - sweepRadius;
        if (!Finite(sweepClearance))
            throw Error("distancia de repouso/varredura nao e finita; RaioBounds=" + Number(sweepRadius) + "; Folga=" + Number(sweepClearance));
        GTA.Math.Vector3 actorMin, actorMax; actor.Model.GetDimensions(out actorMin, out actorMax);
        Vector(actorMin); Vector(actorMax);
        // Bounds do modelo em seu rumo real, nao raio de capsula presumido.
        GTA.Math.Vector3 pivotInBody = actor.GetPositionOffset(closedPosition); Vector(pivotInBody);
        GTA.Math.Vector3 nearestBody = new GTA.Math.Vector3(Clamp(pivotInBody.X, actorMin.X, actorMax.X),
            Clamp(pivotInBody.Y, actorMin.Y, actorMax.Y), pivotInBody.Z);
        float bodySweepClearance = Horizontal(pivotInBody - nearestBody) - sweepRadius;
        Log("ARMARIO_VARREDURA_CORPO", "RaioFolhaBounds=" + Number(sweepRadius) + "; FolgaCentroDiagnostico=" + Number(sweepClearance) +
            "; BoundsCorpoMin=" + actorMin + "; BoundsCorpoMax=" + actorMax + "; HeadingReal=" + Number(actor.Heading) +
            "; PivoNoCorpo=" + pivotInBody + "; PontoCorpoMaisProximo=" + nearestBody +
            "; FolgaOBB=" + Number(bodySweepClearance) + "; ReservaMadeira=" + Number(thickness + 0.03f));
        if (!Finite(bodySweepClearance) || bodySweepClearance < thickness + 0.03f)
            throw Error("bounds/rumo reais do corpo entram na varredura da folha; FolgaOBB=" + Number(bodySweepClearance) + "; ReservaMadeira=" + Number(thickness + 0.03f) + "; Heading=" + Number(actor.Heading));
        nearPoint = center - normal * 0.65f; nearPoint.Z = actorStart.Z;
        float approachDistance = Horizontal(nearPoint - actorStart);
        // O limite espacial nao afirma velocidade nem garante chegada. Os1.5s
        // reais, o gesto inteiro e o hold continuam obrigatorios em Update.
        if (!Finite(approachDistance) || approachDistance > 1f)
            throw Error("aproximacao precisa de percurso real de no maximo1m para a reserva1.5s; distancia=" + Number(approachDistance));
        packing = FindPacking();
        Log("ARMARIO_BOUNDS", "FolhaMin=" + doorMin + "; FolhaMax=" + doorMax + "; Atriz=" + actorStart + "; Aproximacao=" + nearPoint + "; DistanciaAproximacao=" + Number(approachDistance) + "; AproximacaoMaxMs=" + ApproachMaxMs + "; RaioVarreduraBounds=" + Number(sweepRadius) + "; FolgaVarredura=" + Number(sweepClearance) + "; AtrizBoundsMin=" + actorMin + "; AtrizBoundsMax=" + actorMax + "; AtrizHeading=" + Number(actor.Heading) + "; PivoNoCorpo=" + pivotInBody + "; FolgaOBBVarredura=" + Number(bodySweepClearance) + "; ClipeBudgetMs=" + ClipBudgetMs + "; HoldMinMs=" + HoldMinMs + "; ApoioModelo=" + packing.BoxName + "; gesto=" + ClipDictionary + "/" + ClipName + "; duracao=" + Number(clipDuration));
    }
    private Packing FindPacking()
    {
        // Somente os dois modelos confirmados por getters no diagnostico77/78:
        // madeira da folha e camiseta individual. Nao declarar outra cor/mesh.
        GTA.Math.Vector3 min, max;
        new GTA.Model("prop_ld_tshirt_01").GetDimensions(out min, out max);
        Vector(min); Vector(max);
        GTA.Math.Vector3 size = max - min;
        if (size.Z < 0f || size.Z > 0.25f || size.X <= 0f || size.Y <= 0f)
            throw Error("camiseta precisa ter bounds reais planos: Min=" + min + "; Max=" + max);
        // A folha horizontal tem largura original e profundidade igual a sua
        // altura original. Nao escalar madeira nem inventar bounds de caixa.
        float w = width - 0.06f, d = height - 0.06f;
        for (int ta = 0; ta < 2; ta++) for (int tb = 0; tb < 2; tb++)
        {
            float aw = ta == 0 ? size.X : size.Y, ad = ta == 0 ? size.Y : size.X;
            float bw = tb == 0 ? size.X : size.Y, bd = tb == 0 ? size.Y : size.X;
            bool row = aw + bw + 0.08f <= w && System.Math.Max(ad, bd) + 0.04f <= d;
            bool col = ad + bd + 0.08f <= d && System.Math.Max(aw, bw) + 0.04f <= w;
            if (!row && !col) continue;
            return new Packing { BoxName = DoorModel,
                FirstAngle = ta * 90f, SecondAngle = tb * 90f + 180f,
                FirstX = row ? -(bw + 0.04f) * 0.5f : 0f,
                SecondX = row ? (aw + 0.04f) * 0.5f : 0f,
                FirstY = row ? 0f : -(bd + 0.04f) * 0.5f,
                SecondY = row ? 0f : (ad + 0.04f) * 0.5f };
        }
        throw Error("duas camisetas nao cabem nos bounds reais da folha horizontal sem sobreposicao");
    }
    private void BeginFloorProbes()
    {
        probes.Clear(); probeIndex = 0;
        float feet = actor.Bones[GTA.Bone.SkelRightFoot].Position.Z;
        float front = thickness + 0.03f, back = front + height;
        foreach (GTA.Math.Vector3 p in new GTA.Math.Vector3[] { center,
            center + normal * front - right * width * 0.5f,
            center + normal * front + right * width * 0.5f,
            center + normal * back - right * width * 0.5f,
            center + normal * back + right * width * 0.5f, nearPoint })
            probes.Add(new Probe { A = new GTA.Math.Vector3(p.X, p.Y, feet + 0.4f),
                B = new GTA.Math.Vector3(p.X, p.Y, feet - 1f), Expected = -1, Height = feet });
    }
    private void Build()
    {
        floor = probes[0].Point.Z;
        foreach (Probe p in probes) if (System.Math.Abs(p.Point.Z - floor) > 0.1f)
            throw Error("piso da estrutura/aproximacao nao e plano; nao ajustar por teleporte");
        resources = new ObjetosDiretor(log);
        GTA.Math.Vector3 localCenter = (doorMin + doorMax) * 0.5f;
        GTA.Math.Vector3 delta = door.GetOffsetPosition(localCenter) - door.Position;
        float front = thickness + 0.03f;
        // Armario cenografico aberto com paines laterais curtos e fundo. Cada
        // painel continua sendo a madeira real, sem escala nem mesh presumido.
        AddPanel("ARMARIO_FUNDO", center + normal * (front + height + thickness), 180f, delta);
        AddPanel("ARMARIO_LATERAL_E", center + normal * (front + height * 0.5f) - right * (width * 0.5f + thickness), 90f, delta);
        AddPanel("ARMARIO_LATERAL_D", center + normal * (front + height * 0.5f) + right * (width * 0.5f + thickness), -90f, delta);
        GTA.Math.Vector3 shelf = center + normal * (front + height * 0.5f);
        shelf.Z = floor + height * 0.35f;
        GTA.Math.Vector3 rotation = new GTA.Math.Vector3(doorWidthOnX ? 90f : 0f,
            doorWidthOnX ? 0f : 90f, closedRotation.Z);
        resources.RequestProp("ARMARIO_APOIO", DoorModel, shelf, rotation, true);
        // Criacao ocorre apenas no preparo. O assentamento abaixo usara os
        // vertices transformados e oito raycasts na prateleira propria.
        float yaw = closedRotation.Z + (doorWidthOnX ? 0f : 90f);
        resources.RequestProp("ARMARIO_ROUPA_1", "prop_ld_tshirt_01", shelf,
            new GTA.Math.Vector3(0f, 0f, yaw + packing.FirstAngle), true);
        resources.RequestProp("ARMARIO_ROUPA_2", "prop_ld_tshirt_01", shelf,
            new GTA.Math.Vector3(0f, 0f, yaw + packing.SecondAngle), true);
    }
    private void AddPanel(string id, GTA.Math.Vector3 desiredCenter, float angle, GTA.Math.Vector3 centerDelta)
    { resources.RequestProp(id, DoorModel, desiredCenter - Rotate(centerDelta, angle), new GTA.Math.Vector3(0f, 0f, closedRotation.Z + angle), true); }
    private void LayoutAndSupports()
    {
        probes.Clear(); probeIndex = 0;
        GTA.Prop shelf = resources.GetProp("ARMARIO_APOIO");
        if (!shelf.Model.IsCollisionLoaded || !shelf.IsCollisionEnabled)
            throw Error("prateleira propria nao confirmou colisao real");
        GTA.Math.Vector3 desired = center + normal * (thickness + 0.03f + height * 0.5f);
        desired.Z = floor + height * 0.35f;
        shelf.PositionNoOffset = shelf.Position + desired - shelf.GetOffsetPosition((doorMin + doorMax) * 0.5f);
        float low = System.Single.PositiveInfinity;
        shelfTop = System.Single.NegativeInfinity;
        shelfMinRight = shelfMinNormal = System.Single.PositiveInfinity;
        shelfMaxRight = shelfMaxNormal = System.Single.NegativeInfinity;
        foreach (float x in new float[] { doorMin.X, doorMax.X })
            foreach (float y in new float[] { doorMin.Y, doorMax.Y })
                foreach (float z in new float[] { doorMin.Z, doorMax.Z })
                {
                    GTA.Math.Vector3 p = shelf.GetOffsetPosition(new GTA.Math.Vector3(x, y, z));
                    Vector(p);
                    float r = Dot(p - center, right), n = Dot(p - center, normal);
                    shelfMinRight = System.Math.Min(shelfMinRight, r); shelfMaxRight = System.Math.Max(shelfMaxRight, r);
                    shelfMinNormal = System.Math.Min(shelfMinNormal, n); shelfMaxNormal = System.Math.Max(shelfMaxNormal, n);
                    low = System.Math.Min(low, p.Z); shelfTop = System.Math.Max(shelfTop, p.Z);
                }
        if (System.Math.Abs(shelfMaxRight - shelfMinRight - width) > 0.02f ||
            System.Math.Abs(shelfMaxNormal - shelfMinNormal - height) > 0.02f ||
            System.Math.Abs(shelfTop - low - thickness) > 0.02f ||
            shelfMinNormal < thickness || shelfTop >= floor + height * 0.75f)
            throw Error("rotacao/bounds reais da prateleira nao formam apoio horizontal dentro do armario");
        Log("ARMARIO_PRATELEIRA_BOUNDS", "Modelo=" + DoorModel + "; Rot=" + shelf.Rotation +
            "; MinRight=" + Number(shelfMinRight) + "; MaxRight=" + Number(shelfMaxRight) +
            "; MinNormal=" + Number(shelfMinNormal) + "; MaxNormal=" + Number(shelfMaxNormal) +
            "; TopZ=" + Number(shelfTop) + "; ColisaoModelo=" + shelf.Model.IsCollisionLoaded +
            "; EntidadeColisao=" + shelf.IsCollisionEnabled + "; BoundsDerivados=true");
        PlaceCloth("ARMARIO_ROUPA_1", packing.FirstX, packing.FirstY);
        PlaceCloth("ARMARIO_ROUPA_2", packing.SecondX, packing.SecondY);
        // Distancia entre retangulos medidos: nao aceitar intersecao dos meshes.
        float aMinR, aMaxR, aMinN, aMaxN, bMinR, bMaxR, bMinN, bMaxN;
        ClothProjection(resources.GetProp("ARMARIO_ROUPA_1"), out aMinR, out aMaxR, out aMinN, out aMaxN);
        ClothProjection(resources.GetProp("ARMARIO_ROUPA_2"), out bMinR, out bMaxR, out bMinN, out bMaxN);
        if (!(aMaxR + 0.02f <= bMinR || bMaxR + 0.02f <= aMinR ||
            aMaxN + 0.02f <= bMinN || bMaxN + 0.02f <= aMinN))
            throw Error("bounds reais das duas camisetas se sobrepoem");
        foreach (string id in new string[] { "ARMARIO_FUNDO", "ARMARIO_LATERAL_E", "ARMARIO_LATERAL_D", "ARMARIO_APOIO", "ARMARIO_ROUPA_1", "ARMARIO_ROUPA_2" })
        {
            GTA.Prop p = resources.GetProp(id); marks[id] = new Mark { Position = p.Position, Rotation = p.Rotation };
        }
    }
    private void ClothProjection(GTA.Prop cloth, out float minR, out float maxR, out float minN, out float maxN)
    {
        GTA.Math.Vector3 min, max; cloth.Model.GetDimensions(out min, out max);
        minR = minN = System.Single.PositiveInfinity; maxR = maxN = System.Single.NegativeInfinity;
        foreach (float x in new float[] { min.X, max.X }) foreach (float y in new float[] { min.Y, max.Y })
        {
            GTA.Math.Vector3 p = cloth.GetOffsetPosition(new GTA.Math.Vector3(x, y, min.Z));
            Vector(p); float r = Dot(p - center, right), n = Dot(p - center, normal);
            minR = System.Math.Min(minR, r); maxR = System.Math.Max(maxR, r);
            minN = System.Math.Min(minN, n); maxN = System.Math.Max(maxN, n);
        }
    }
    private void PlaceCloth(string id, float x, float y)
    {
        GTA.Prop shelf = resources.GetProp("ARMARIO_APOIO"), cloth = resources.GetProp(id);
        if (!cloth.Model.IsCollisionLoaded || !cloth.IsCollisionEnabled)
            throw Error("camiseta propria nao confirmou colisao real: " + id);
        GTA.Math.Vector3 min, max; cloth.Model.GetDimensions(out min, out max);
        GTA.Math.Vector3 target = center + right * ((shelfMinRight + shelfMaxRight) * 0.5f + x) +
            normal * ((shelfMinNormal + shelfMaxNormal) * 0.5f + y);
        target.Z = shelfTop;
        GTA.Math.Vector3 bottomCenter = (min + max) * 0.5f; bottomCenter.Z = min.Z;
        cloth.PositionNoOffset = cloth.Position + target - cloth.GetOffsetPosition(bottomCenter);
        foreach (float cx in new float[] { min.X, max.X }) foreach (float cy in new float[] { min.Y, max.Y })
        {
            GTA.Math.Vector3 corner = cloth.GetOffsetPosition(new GTA.Math.Vector3(cx, cy, min.Z));
            float r = Dot(corner - center, right), n = Dot(corner - center, normal);
            if (r < shelfMinRight + 0.01f || r > shelfMaxRight - 0.01f ||
                n < shelfMinNormal + 0.01f || n > shelfMaxNormal - 0.01f)
                throw Error("camiseta nao cabe nos bounds reais da prateleira: " + id + "; Point=" + corner);
            probes.Add(new Probe { A = corner + new GTA.Math.Vector3(0f, 0f, 0.15f),
                B = corner - new GTA.Math.Vector3(0f, 0f, 0.3f), Expected = shelf.Handle, Height = corner.Z });
        }
    }
    private bool PollProbes()
    {
        if (probeIndex >= probes.Count) return true;
        Probe p = probes[probeIndex];
        if (p.Ray == 0) { p.StartedAt = GTA.Game.GameTime; p.Ray = Ray(p.A, p.B, p.Expected < 0 ? doorHandle : resources.GetProp(probeIndex < 4 ? "ARMARIO_ROUPA_1" : "ARMARIO_ROUPA_2").Handle); return false; }
        if (unchecked((uint)(GTA.Game.GameTime - p.StartedAt)) >= 2000u) throw Error("sonda de apoio/piso nao concluiu em2s");
        Hit h; if (!Read(p.Ray, out h)) return false;
        if (!h.HasHit || h.Normal.Z < 0.9f || p.Expected >= 0 && h.Entity != p.Expected || System.Math.Abs(h.Point.Z - p.Height) > (p.Expected < 0 ? 0.25f : 0.03f))
            throw Error("apoio/piso real nao confirmado: Hit=" + h.HasHit + "; Entity=" + h.Entity + "; Esperado=" + p.Expected + "; Point=" + h.Point + "; Normal=" + h.Normal + "; Height=" + Number(p.Height));
        p.Point = h.Point; probeIndex++; return probeIndex >= probes.Count;
    }
    private bool CalibrateDoor()
    {
        if (!addedDoor)
        {
            door.IsPositionFrozen = false;
            systemId = unchecked((int)GTA.StringHash.AtStringHash("DIRETOR_ARMARIO_" + doorHandle + "_" + System.Guid.NewGuid().ToString("N")));
            GTA.Native.Function.Call(GTA.Native.Hash.ADD_DOOR_TO_SYSTEM, systemId, doorHash, closedPosition.X, closedPosition.Y, closedPosition.Z, false, false, false);
            addedDoor = true;
            LogDoorPhysics("registrada_aguardar_proximo_frame");
            return false;
        }
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, systemId)) return false;
        LogCalibrationContacts(false);
        if (calibration == 0)
        {
            // Perfil90: a folha declarada no JSON foi girada180 graus em torno
            // do centro medido. O pivo oposto conserva o plano fechado e faz
            // o sentido positivo nativo abrir para fora do nicho. Nao exigir
            // dois sentidos: o roteiro exige uma abertura fisica comprovada.
            openSign = 1f;
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, systemId, 0, false, true);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, systemId, openSign, false, true);
            calibrationStartedAt = GTA.Game.GameTime;
            Log("ARMARIO_SWING_PEDIDO", "Calibracao=" + calibration + "; Sentido=" + Number(openSign) +
                "; RatioAtual=" + Number(Ratio()) + "; CentroFechado=" + center + "; EixoNormal=" + normal + "; RotacaoReal=" + door.Rotation);
            calibration = 1; return false;
        }
        if (calibration == 1)
        {
            float ratio = Ratio();
            if (ratio * openSign < 0.85f)
            {
                const uint calibrationBudgetMs = 1500u;
                if (unchecked((uint)(GTA.Game.GameTime - calibrationStartedAt)) < calibrationBudgetMs) return false;
                LogCalibrationContacts(true);
                throw Error("folha fisica nao abriu no sentido positivo; Sentido=" + Number(openSign) + "; Ratio=" + Number(ratio) + "; PrazoMs=" + calibrationBudgetMs);
            }
            GTA.Math.Vector3 movedCenter = door.GetOffsetPosition((doorMin + doorMax) * 0.5f);
            float deltaNormal = Dot(movedCenter - center, normal);
            Log("ARMARIO_SWING_REAL", "Calibracao=" + calibration + "; Sentido=" + Number(openSign) + "; Ratio=" + Number(ratio) +
                "; CentroFechado=" + center + "; CentroReal=" + movedCenter + "; EixoNormal=" + normal +
                "; DeltaNormal=" + Number(deltaNormal) + "; LimiteFora=" + Number(-width * 0.1f) + "; RotacaoReal=" + door.Rotation);
            if (deltaNormal >= -width * 0.1f)
                throw Error("sentido positivo nao abriu para fora do nicho; conferir pivo oposto/rumo da folha; nao abrir atraves das roupas");
            if (DoorDistance(actorStart) < 0.4f) throw Error("folha aberta alcanca o corpo da atriz; recalibrar espaco real antes de gravar");
            calibration = 4;
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, systemId, 0f, false, true);
            return false;
        }
        // Native91: Ratio0.0528 ainda voltava a1.198rad/s. A primeira
        // passagem pela tolerancia de ratio nao confirma o rumo fechado.
        // Aguardar os tres getters juntos; PollLoading conserva o prazo15s.
        float closedRatio = Ratio();
        float positionDelta = (door.Position - closedPosition).Length();
        float headingDelta = Angle(door.Heading - closedRotation.Z);
        if (System.Math.Abs(closedRatio) > 0.05f || positionDelta > 0.1f || headingDelta > 2f) return false;
        GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, systemId, 1, false, true);
        Log("ARMARIO_RETORNO_REAL", "Ratio=" + Number(closedRatio) + "; PositionDelta=" + Number(positionDelta) + "; HeadingDelta=" + Number(headingDelta) + "; RotacaoReal=" + door.Rotation + "; PosicaoReal=" + door.Position + "; PrazoCargaMs=" + LoadTimeoutMs);
        return true;
    }
    private int lastCalibrationContactAt;
    private void LogCalibrationContacts(bool force)
    {
        if (!force && unchecked((uint)(GTA.Game.GameTime - lastCalibrationContactAt)) < 200u) return;
        lastCalibrationContactAt = GTA.Game.GameTime;
        // Getters somente. Contato true confirma; false nao exclui colisao de mundo.
        string ownTouches = "";
        foreach (string id in marks.Keys)
            ownTouches += "; " + id + "=" + door.IsTouching(resources.GetProp(id));
        GTA.Ped player = GTA.Game.Player.Character;
        string playerTouch = player != null && player.Exists() ?
            "; PlayerTouch=" + door.IsTouching(player) + "; PlayerPos=" + player.Position + "; PlayerCollision=" + player.IsCollisionEnabled : "; PlayerUnavailable=true";
        Log("ARMARIO_CALIBRACAO_CONTATOS", "Calibration=" + calibration + "; SinceRequestMs=" + unchecked((uint)(GTA.Game.GameTime - calibrationStartedAt)) +
            "; DoorState=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, systemId) +
            "; PendingState=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_PENDING_STATE, systemId) +
            "; AutomaticDistance=" + Number(GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_AUTOMATIC_DISTANCE, systemId)) +
            "; Ratio=" + Number(Ratio()) + "; Rotation=" + door.Rotation + "; RotationVelocityRaw=" + GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ENTITY_ROTATION_VELOCITY, door.Handle) +
            "; ActorTouch=" + door.IsTouching(actor) + "; ActorPos=" + actor.Position + "; ActorHeading=" + Number(actor.Heading) +
            "; ActorDistanceOBB=" + Number(DoorDistance(actor.Position)) + ownTouches + playerTouch);
    }
    public bool ReleaseActorsForReset()
    {
        Active(); running = false;
        if (OwnActor()) actor.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(ClipDictionary, ClipName));
        resetDone = false; return true;
    }
    public bool Reset()
    {
        RequireReady(); RequireActor(); RequireDoor();
        if (!resetPending)
        {
            ReleaseActorsForReset();
            foreach (var item in marks)
            {
                GTA.Prop p = resources.GetProp(item.Key); p.IsPositionFrozen = true; p.PositionNoOffset = item.Value.Position;
                p.Rotation = item.Value.Rotation; p.Velocity = GTA.Math.Vector3.Zero;
            }
            door.IsPositionFrozen = false;
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, systemId, 0, false, true);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, systemId, 0f, false, true);
            resetStartedAt = GTA.Game.GameTime; resetPending = true;
        }
        // O reset tambem espera os tres getters juntos, dentro dos mesmos2s.
        bool colliderReady = KeepLeafCollider("reset", false);
        float closedRatio = Ratio();
        float positionDelta = (door.Position - closedPosition).Length();
        float headingDelta = Angle(door.Heading - closedRotation.Z);
        if (!colliderReady || System.Math.Abs(closedRatio) > 0.05f || positionDelta > 0.1f || headingDelta > 2f)
        {
            uint waited = unchecked((uint)(GTA.Game.GameTime - resetStartedAt));
            if (waited >= 2000u) throw Error("folha nao fechou fisicamente no reset2s; EsperaMs=" + waited + "; Ratio=" + Number(closedRatio) + "; PositionDelta=" + Number(positionDelta) + "; HeadingDelta=" + Number(headingDelta));
            return false;
        }
        GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, systemId, 1, false, true);
        running = finished = contact = clipDone = doorOpen = returning = turningBack = facingClothes = openingRequested = false; phase = 0; nextLog = nextLook = 0; nextPhysicsLogAt = 0;
        maximumPhase = 0f; contactRay = 0; resetPending = false; resetDone = true; return true;
    }
    public void Start()
    { RequireReady(); if (!resetDone || running) throw Error("reinicie antes de iniciar o armario"); running = true; }
    public void Update(int elapsed)
    {
        RequireReady(); if (finished) return;
        if (!running || elapsed < 0) throw Error("acao do armario nao iniciada");
        RequireActor(); RequireDoor(); resources.Update();
        bool leafColliderReady = !contact || KeepLeafCollider("acao", false);
        if (phase == 0)
        {
            actor.Task.ClearAllImmediately(); actor.Velocity = GTA.Math.Vector3.Zero; actor.IsPositionFrozen = false;
            // API191 finalHeading e o rumo FINAL da tarefa, nao o rumo do percurso.
            // Fazer a aproximacao e o giro na mesma tarefa; conservar1.5s.
            GTA.Math.Vector3 face = center - nearPoint;
            float finalHeading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, face.X, face.Y);
            if (!Finite(finalHeading)) throw Error("rumo final da aproximacao nao e finito");
            actor.Task.GoStraightTo(nearPoint, -1, new GTA.PedMoveBlendRatio(0.8f), finalHeading, 0.15f);
            Log("ARMARIO_APROXIMACAO_PEDIDO", "Origem=" + actor.Position + "; Destino=" + nearPoint + "; CentroFolha=" + center + "; FinalHeading=" + Number(finalHeading) + "; HeadingInicial=" + Number(actor.Heading) + "; PrazoMs=" + ApproachMaxMs + "; WarpTimeoutMs=-1");
            phase = 1;
        }
        if (phase == 1)
        {
            if (Horizontal(actor.Position - nearPoint) <= 0.3f)
            {
                // Nao substituir GoStraight antes de sua parada/giro finais.
                // A fase2 continua exigindo Facing e Speed reais, no mesmo prazo.
                phase = 2;
            }
            else if (elapsed >= ApproachMaxMs) throw Error("atriz nao se aproximou da folha em1.5s; destino=" + nearPoint + "; atual=" + actor.Position);
        }
        if (phase == 2)
        {
            if (Facing(center) && actor.Speed <= 0.2f)
            {
                actor.Task.StandStill(-1);
                initialRightDistance = DoorDistance(actor.Bones[GTA.Bone.SkelRightHand].Position);
                initialLeftDistance = DoorDistance(actor.Bones[GTA.Bone.SkelLeftHand].Position);
                clipStartedAt = GTA.Game.GameTime;
                if (elapsed > ApproachMaxMs || DurationMs - elapsed + 1f < clipDuration * 1000f + HoldMinMs)
                    throw Error("inicio tardio cortaria o gesto completo ou a reserva de aproximacao/hold: InicioMs=" + elapsed + "; AproximacaoMaxMs=" + ApproachMaxMs + "; HoldMinMs=" + HoldMinMs);
                GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, actorHandle, ClipDictionary, ClipName, 4f, -4f, -1,
                    (int)(GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary | GTA.AnimationFlags.StayInEndFrame), 0f, false, false, false);
                phase = 3;
            }
            else if (elapsed >= ApproachMaxMs)
            {
                GTA.Math.Vector3 face = center - actor.Position;
                float requiredHeading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, face.X, face.Y);
                throw Error("atriz nao ficou de frente para a folha dentro da aproximacao1.5s; HeadingReal=" + Number(actor.Heading) + "; HeadingExigido=" + Number(requiredHeading) + "; Speed=" + Number(actor.Speed) + "; Pos=" + actor.Position + "; Destino=" + nearPoint + "; Distancia=" + Number(Horizontal(actor.Position - nearPoint)) + "; TempoMs=" + elapsed);
            }
        }
        if (phase >= 3 && !clipDone && GTA.Game.GameTime != clipStartedAt)
        {
            uint since = unchecked((uint)(GTA.Game.GameTime - clipStartedAt));
            bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actorHandle, ClipDictionary, ClipName, 3);
            float current = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actorHandle, ClipDictionary, ClipName);
            if ((!playing || current == -1f || !Finite(current)) && since < 500u) return;
            if (!playing && maximumPhase >= 0.9f && since + 1f >= clipDuration * 1000f) clipDone = true;
            else
            {
                if (!playing || !Finite(current) || current < 0f || current > 1f) throw Error("fase/execucao real do gesto invalidas: Phase=" + Number(current) + "; SinceStart=" + since + "; Playing=" + playing + "; Dictionary=" + ClipDictionary + "; Clip=" + ClipName);
                maximumPhase = System.Math.Max(maximumPhase, current);
                float distance;
                if (!contact && PollHandContact(current, out distance))
                {
                    contact = true;
                    // Reset termina em estado1 (fechada/trancada). Desbloquear a
                    // folha propria somente apos contato antes de testar phCollider.
                    LogDoorPhysics("contato_antes_desbloquear");
                    Log("ARMARIO_DESBLOQUEIO_PEDIDO", "TempoMs=" + elapsed + "; StateAntes=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, systemId) + "; FrozenAntes=" + door.IsPositionFrozen + "; IsStaticAntes=" + door.IsStatic);
                    GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_DOOR_STATE, systemId, 0, false, true);
                    door.IsPositionFrozen = false;
                    leafColliderReady = KeepLeafCollider("contato_depois_desbloquear", true);
                    // O gesto secundario continua inteiro enquanto os pes recuam normalmente.
                    Walk(actorStart); returning = true;
                    Log("ARMARIO_CONTATO_REAL", "TempoMs=" + elapsed + "; MaoFolha=" + Number(distance) + "; Osso=" + contactBone + "; ShapeHitEntity=" + doorHandle + "; Phase=" + Number(current) + "; semAberturaPorRelogio=true");
                }
                // StayInEndFrame conserva o final: nao cortar os ultimos10% pelo relogio
                // quando TASK_PLAY_ANIM demorou um frame para iniciar fisicamente.
                if (maximumPhase >= 0.999f && since + 1f >= clipDuration * 1000f) clipDone = true;
            }
            if (clipDone)
            {
                if (!contact) throw Error("gesto terminou sem contato real da mao com a folha; nao abrir magicamente");
                actor.Task.StopScriptedAnimationTask(new GTA.CrClipAsset(ClipDictionary, ClipName));
            }
        }
        // API191: HasPhysics e phInst; IsStatic=false confirma phCollider ativo.
        // O reset generico congela/transforma a folha. Reativar apenas sua fisica,
        // sem posicao/rotacao cinematicas, e confirmar antes de pedir a abertura.
        if (contact && !openingRequested && leafColliderReady)
        {
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_AUTOMATIC_RATE, systemId, 0.5f, false, false);
            GTA.Native.Function.Call(GTA.Native.Hash.DOOR_SYSTEM_SET_OPEN_RATIO, systemId, openSign, false, true);
            openingRequested = true;
            Log("ARMARIO_ABERTURA_PEDIDA", "TempoMs=" + elapsed + "; RatioPedido=" + Number(openSign) + "; ForceUpdate=true; RatioReal=" + Number(Ratio()) + "; DoorState=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_STATE, systemId) + "; PendingState=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.DOOR_SYSTEM_GET_DOOR_PENDING_STATE, systemId) + "; PhysicsLoaded=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, systemId) + "; Frozen=" + door.IsPositionFrozen + "; HasPhysics=" + door.HasPhysics + "; IsStatic=" + door.IsStatic + "; RotacaoReal=" + door.Rotation);
        }
        if (contact && System.Math.Abs(Ratio()) >= 0.85f && Angle(door.Heading - closedRotation.Z) >= 30f) doorOpen = true;
        // Native99: TurnTo(center) deslocou0.294m apos o clipe completo e
        // deixou Speed0.2046 no fim. Pedir somente o rumo, com os mesmos650ms,
        // sem destino de locomocao nem setter de posicao/heading/velocidade.
        if (returning && clipDone && Horizontal(actor.Position - actorStart) <= 0.3f && actor.Speed <= 0.2f)
        {
            GTA.Math.Vector3 turnDelta = center - actor.Position;
            float turnHeading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, turnDelta.X, turnDelta.Y);
            if (!Finite(turnHeading)) throw Error("rumo real do giro final invalido");
            GTA.ScriptTaskNameHash taskBefore = actor.CurrentScriptTaskNameHash;
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_ACHIEVE_HEADING, actorHandle, turnHeading, 650);
            returning = false; turningBack = true;
            Log("ARMARIO_GIRO_FINAL_PEDIDO", "TempoMs=" + elapsed + "; Tarefa=TASK_ACHIEVE_HEADING; PrazoMs=650; Pos=" + actor.Position + "; RestDistance=" + Number(Horizontal(actor.Position - actorStart)) + "; Speed=" + Number(actor.Speed) + "; HeadingReal=" + Number(actor.Heading) + "; HeadingExigido=" + Number(turnHeading) + "; HeadingDelta=" + Number(Angle(actor.Heading - turnHeading)) + "; TaskAntes=" + taskBefore + "; TaskDepois=" + actor.CurrentScriptTaskNameHash + "; TaskStatus=" + actor.CurrentScriptTaskStatus);
        }
        if (turningBack && clipDone && Facing(center) && actor.Speed <= 0.2f)
        { actor.Task.StandStill(-1); turningBack = false; facingClothes = true; }
        if (elapsed >= nextLook)
        {
            nextLook = elapsed + 150;
            GTA.Math.Vector3 look = contact ? (resources.GetProp("ARMARIO_ROUPA_1").Position + resources.GetProp("ARMARIO_ROUPA_2").Position) * 0.5f : center;
            actor.Task.LookAt(look, 650, GTA.LookAtFlags.SlowTurnRate | GTA.LookAtFlags.NarrowYawLimit, GTA.LookAtPriority.VeryHigh);
        }
        if (elapsed >= nextLog)
        {
            nextLog = elapsed + 1000;
            Log("ARMARIO_ESTADO_REAL", "TempoMs=" + elapsed + "; Pos=" + actor.Position + "; Speed=" + Number(actor.Speed) + "; Phase=" + Number(maximumPhase) + "; Contato=" + contact + "; Ratio=" + Number(Ratio()) + "; Rot=" + door.Rotation + "; GestoCompleto=" + clipDone + "; FrenteRoupas=" + facingClothes);
        }
        if (elapsed >= DurationMs)
        {
            bool facingNow = Facing(center);
            float restDistance = Horizontal(actor.Position - actorStart), actualSpeed = actor.Speed;
            GTA.Math.Vector3 face = center - actor.Position;
            float requiredHeading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, face.X, face.Y);
            string getters = "TempoMs=" + elapsed + "; Contact=" + contact + "; ClipDone=" + clipDone + "; DoorOpen=" + doorOpen +
                "; Returning=" + returning + "; TurningBack=" + turningBack + "; FacingClothes=" + facingClothes + "; FacingNow=" + facingNow +
                "; RestDistance=" + Number(restDistance) + "; Speed=" + Number(actualSpeed) + "; Pos=" + actor.Position + "; ActorStart=" + actorStart +
                "; HeadingReal=" + Number(actor.Heading) + "; HeadingExigido=" + Number(requiredHeading) + "; HeadingDelta=" + Number(Angle(actor.Heading - requiredHeading)) +
                "; SinceClipStartMs=" + unchecked((uint)(GTA.Game.GameTime - clipStartedAt)) + "; ClipDurationMs=" + Number(clipDuration * 1000f) +
                "; MaxPhase=" + Number(maximumPhase) + "; CurrentPhase=" + Number(GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actorHandle, ClipDictionary, ClipName)) +
                "; Playing=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actorHandle, ClipDictionary, ClipName, 3) +
                "; Ratio=" + Number(Ratio()) + "; Frozen=" + door.IsPositionFrozen + "; IsStatic=" + door.IsStatic +
                "; TaskHash=" + actor.CurrentScriptTaskNameHash + "; TaskStatus=" + actor.CurrentScriptTaskStatus;
            Log("ARMARIO_FINAL_GETTERS", getters);
            if (!contact || !clipDone || !doorOpen || returning || !facingClothes || !facingNow || restDistance > 0.35f || actualSpeed > 0.2f)
                throw Error("armario nao completou contato/gesto/abertura/recuo em8s; nenhuma fala foi comprimida; " + getters);
            finished = true; running = false; Log("ARMARIO_CONCLUIDO", "TempoMs=" + elapsed + "; portaAberta=true; roupasReais=2; narracao8sSemBoca=true; visualPendente=true");
        }
    }
    private bool KeepLeafCollider(string stage, bool forceLog)
    {
        RequireDoor();
        bool hasPhysics = door.HasPhysics, frozen = door.IsPositionFrozen, staticBefore = door.IsStatic;
        if (!hasPhysics || frozen) throw Error("folha propria sem phInst ou congelada; Fase=" + stage + "; HasPhysics=" + hasPhysics + "; Frozen=" + frozen);
        // API191 documenta manter phCollider por frame; nao altera transform/colisao.
        door.SetToRespondToPhysicsSystem();
        bool staticAfter = door.IsStatic;
        int now = GTA.Game.GameTime;
        if (forceLog || now >= nextPhysicsLogAt)
        {
            nextPhysicsLogAt = now + 1000;
            Log("ARMARIO_COLLIDER_REAL", "Fase=" + stage + "; HasPhysics=" + hasPhysics + "; Frozen=" + frozen +
                "; IsStaticAntes=" + staticBefore + "; IsStaticDepois=" + staticAfter +
                "; PhysicsLoaded=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOOR_SYSTEM_GET_IS_PHYSICS_LOADED, systemId) +
                "; Velocity=" + door.Velocity + "; RotationVelocity=" + GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ENTITY_ROTATION_VELOCITY, doorHandle) +
                "; Ratio=" + Number(Ratio()) + "; Position=" + door.Position + "; Rotation=" + door.Rotation);
        }
        return !staticAfter;
    }
    private void Walk(GTA.Math.Vector3 target)
    {
        // API191: finalHeading e o rumo ao chegar, nao a direcao do percurso.
        // Native100: o vetor de recuo pedia182.65graus e obrigava outro giro
        // de179graus aos7102ms. Chegar encarando o armario, no mesmo destino.
        GTA.Math.Vector3 delta = center - target;
        float heading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
        if (!Finite(heading)) throw Error("rumo final real do recuo invalido");
        actor.Task.GoStraightTo(target, -1, new GTA.PedMoveBlendRatio(0.8f), heading, 0.15f);
        Log("ARMARIO_RECUO_PEDIDO", "Origem=" + actor.Position + "; Destino=" + target + "; CentroFolha=" + center + "; FinalHeading=" + Number(heading) + "; HeadingInicial=" + Number(actor.Heading) + "; MoveBlendRatio=0.8; DistanceToSlide=0.15; WarpTimeoutMs=-1; Tarefa=" + actor.CurrentScriptTaskNameHash + "; TaskStatus=" + actor.CurrentScriptTaskStatus);
    }
    private bool Facing(GTA.Math.Vector3 target)
    {
        GTA.Math.Vector3 delta = target - actor.Position;
        float heading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
        if (!Finite(heading) || !Finite(actor.Heading)) throw Error("direcao real da atriz nao e finita");
        return Angle(actor.Heading - heading) <= 12f;
    }
    private bool PollHandContact(float current, out float distance)
    {
        distance = -1f;
        if (contactRay != 0)
        {
            if (unchecked((uint)(GTA.Game.GameTime - contactStartedAt)) >= 500u) throw Error("sonda mao/folha nao concluiu em500ms");
            Hit hit; if (!Read(contactRay, out hit)) return false;
            contactRay = 0;
            GTA.Math.Vector3 hand = actor.Bones[contactBone].Position;
            distance = DoorDistance(hand);
            float initial = contactBone == GTA.Bone.SkelRightHand ? initialRightDistance : initialLeftDistance;
            if (hit.HasHit && hit.Entity == doorHandle && current >= 0.05f && current <= 0.9f &&
                distance <= 0.15f && initial - distance >= 0.025f && (hit.Point - hand).Length() <= 0.2f) return true;
        }
        if (current < 0.05f || current > 0.9f) return false;
        foreach (GTA.Bone bone in new GTA.Bone[] { GTA.Bone.SkelRightHand, GTA.Bone.SkelLeftHand })
        {
            GTA.Math.Vector3 hand = actor.Bones[bone].Position; float d = DoorDistance(hand);
            float initial = bone == GTA.Bone.SkelRightHand ? initialRightDistance : initialLeftDistance;
            if (d > 0.15f || initial - d < 0.025f) continue;
            contactBone = bone; contactStartedAt = GTA.Game.GameTime;
            contactRay = Ray(hand - normal * 0.22f, hand + normal * 0.22f, actorHandle);
            break;
        }
        return false;
    }
    private float DoorDistance(GTA.Math.Vector3 point)
    {
        Vector(point); GTA.Math.Vector3 p = door.GetPositionOffset(point);
        GTA.Math.Vector3 nearest = new GTA.Math.Vector3(Clamp(p.X, doorMin.X, doorMax.X), Clamp(p.Y, doorMin.Y, doorMax.Y), Clamp(p.Z, doorMin.Z, doorMax.Z));
        return (point - door.GetOffsetPosition(nearest)).Length();
    }
    private float Ratio() { float v = GTA.Native.Function.Call<float>(GTA.Native.Hash.DOOR_SYSTEM_GET_OPEN_RATIO, systemId); if (!Finite(v)) throw Error("angulo real da folha nao e finito"); return v; }
    private void VerifySources()
    {
        string props = System.IO.Path.Combine(menyoo ?? "", "PropList.txt"), animations = System.IO.Path.Combine(menyoo ?? "", "PedAnimList.txt");
        VerifyFile(props, "35BFD924919FC223F5AB131941643B2AE4E0082FCD0ECEF1A5A50FCDEE9B2552");
        string[] lines = System.IO.File.ReadAllLines(props);
        foreach (var source in sourceModels) if (lines.Length < source.Value || lines[source.Value - 1].Trim() != source.Key) throw Error("modelo diverge da origem instalada: " + source.Key);
        VerifyFile(animations, "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4");
        lines = System.IO.File.ReadAllLines(animations);
        if (lines.Length < 62421 || lines[62421 - 1].Trim() != ClipDictionary + " " + ClipName) throw Error("gesto de abertura diverge da linha62421 instalada");
    }
    private static void VerifyFile(string file, string expected)
    {
        using (var hash = System.Security.Cryptography.SHA256.Create())
            if (!System.String.Equals(System.BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(file))).Replace("-", ""), expected, System.StringComparison.Ordinal)) throw Error("SHA da origem instalada mudou: " + file);
    }
    public void Cleanup()
    {
        if (disposed) return;
        ReleaseActorsForReset();
        // O ID foi criado exclusivamente por este helper. Liberar o registro mesmo
        // se a folha sumiu nao adota, altera nem remove outra entidade do mapa.
        if (addedDoor) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_DOOR_FROM_SYSTEM, systemId);
        addedDoor = false;
        if (resources != null) resources.Dispose();
        if (dictionaryRequested && !dictionaryWasLoaded) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, ClipDictionary);
        foreach (GTA.Model model in requested) model.MarkAsNoLongerNeeded(); requested.Clear();
        resources = null; door = null; doorOwner = null; marks.Clear(); probes.Clear(); prepared = ready = running = false;
    }
    public void Dispose() { Cleanup(); disposed = true; }
    private bool OwnActor() { return actor != null && actor.Exists() && actor.Handle == actorHandle && actor.Model.Hash == actorModel && actor.MemoryAddress == actorAddress; }
    private bool OwnDoor() { return door != null && door.Exists() && door.Handle == doorHandle && door.Model.Hash == doorHash && door.MemoryAddress == doorAddress; }
    private void RequireActor() { if (!OwnActor() || actor.IsDead || actor.Health < actorHealth || actor.IsInVehicle()) throw Error("identidade/vida/estado da atriz mudou"); }
    private void RequireDoor() { if (!OwnDoor() || doorOwner == null || doorOwner.GetProp("ARMARIO_DJANIRA").Handle != doorHandle || !door.IsCollisionEnabled) throw Error("folha propria perdeu identidade/colisao; nao adotar outra porta"); }
    private void RequireReady() { Active(); if (!IsReady) throw Error("aguarde preparacao fisica do armario"); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("ArmarioSerieDiretor"); }
    private void Log(string kind, string message) { if (log != null) log(kind, message); }
    private static System.InvalidOperationException Error(string s) { return new System.InvalidOperationException("Armario EP05_C04: " + s); }
    private static float Horizontal(GTA.Math.Vector3 p) { return (float)System.Math.Sqrt(p.X * p.X + p.Y * p.Y); }
    private static float Dot(GTA.Math.Vector3 a, GTA.Math.Vector3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
    private static GTA.Math.Vector3 Unit(GTA.Math.Vector3 a) { float length = a.Length(); if (!Finite(length) || length < 0.1f) throw Error("eixo real invalido"); return a / length; }
    private static float Clamp(float a, float min, float max) { return System.Math.Max(min, System.Math.Min(max, a)); }
    private static GTA.Math.Vector3 Rotate(GTA.Math.Vector3 p, float degrees) { double r = degrees * System.Math.PI / 180.0; float c = (float)System.Math.Cos(r), s = (float)System.Math.Sin(r); return new GTA.Math.Vector3(p.X * c - p.Y * s, p.X * s + p.Y * c, p.Z); }
    private static float Angle(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return System.Math.Abs(a); }
    private static bool Finite(float f) { return !System.Single.IsNaN(f) && !System.Single.IsInfinity(f); }
    private static void Vector(GTA.Math.Vector3 p) { if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) throw Error("vetor real nao finito"); }
    private static string Number(float f) { return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static int Ray(GTA.Math.Vector3 a, GTA.Math.Vector3 b, int ignore)
    { int result = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE, a.X, a.Y, a.Z, b.X, b.Y, b.Z, 17, ignore, 0); if (result == 0) throw Error("sonda fisica nao iniciou"); return result; }
    private static bool Read(int ray, out Hit result)
    {
        result = null; System.IntPtr hit = System.IntPtr.Zero, point = System.IntPtr.Zero, normal = System.IntPtr.Zero, entity = System.IntPtr.Zero;
        try
        {
            hit = Buffer(); point = Buffer(); normal = Buffer(); entity = Buffer();
            int status = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, ray, hit, point, normal, entity);
            if (status == 1) return false; if (status != 2) throw Error("sonda fisica nao concluiu");
            result = new Hit { HasHit = System.Runtime.InteropServices.Marshal.ReadInt32(hit) != 0, Entity = System.Runtime.InteropServices.Marshal.ReadInt32(entity), Point = ReadVector(point), Normal = ReadVector(normal) };
            Vector(result.Point); Vector(result.Normal);
            if (result.HasHit && result.Normal.Length() < 0.5f) throw Error("normal fisica da sonda e invalida");
            return true;
        }
        finally
        {
            if (hit != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(hit);
            if (point != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(point);
            if (normal != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(normal);
            if (entity != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(entity);
        }
    }
    private static System.IntPtr Buffer() { System.IntPtr p = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(24); System.Runtime.InteropServices.Marshal.Copy(new byte[24], 0, p, 24); return p; }
    private static GTA.Math.Vector3 ReadVector(System.IntPtr p) { float[] a = new float[6]; System.Runtime.InteropServices.Marshal.Copy(p, a, 0, 6); return new GTA.Math.Vector3(a[0], a[2], a[4]); }
}

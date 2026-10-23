// Ações físicas específicas da temporada. Atua só sobre o elenco/recursos próprios.
public sealed class EpisodiosTresSeteDiretor : System.IDisposable
{
    private readonly System.Action<string, string> log;
    private VeiculosSerieDiretor vehicles;
    private GTA.Ped actor;
    private int handle, model, phase, previousElapsed = -1, totalMs;
    private System.IntPtr address;
    private GTA.Math.Vector3 origin, forward, right, destination;
    private float requestedHeading;
    private bool prepared, ready, resetDone, failed, disposed, resetPending;
    private System.Diagnostics.Stopwatch loading;
    private int nextLog;
    private string sceneId;
    private bool walkingBaseline, onFoot;
    private float standingRootClearance, standingNativeHeight, lowerRootClearance, upperRootClearance;
    private float nativeHeight, currentRootClearance;

    public EpisodiosTresSeteDiretor(System.Action<string, string> logger) { log = logger; }
    public static bool Supports(string id) { return id == "EP04_C01" || id == "EP06_C01"; }
    public static int ExpectedDurationMs(string id) { return id == "EP04_C01" ? 12000 : id == "EP06_C01" ? 11000 : 0; }
    public bool IsReady { get { return prepared && ready && !failed && !disposed; } }
    public bool IsFinished { get { return IsReady && resetDone && phase == 2; } }
    public bool UsesOwnCamera { get { return false; } }
    public VeiculosSerieDiretor OwnVehicles { get { return vehicles; } }
    public bool HasLeftBus { get { return IsReady && vehicles.HasCompletedNormalExit(actor); } }
    public GTA.Entity[] OwnedEntities { get { return vehicles == null ? new GTA.Entity[0] : vehicles.OwnedEntities; } }
    public int[] OwnedHandles { get { return vehicles == null ? new int[0] : vehicles.OwnedHandles; } }
    public bool ControlsBody(string actorId, int elapsed) { return IsReady && System.String.Equals(actorId, "djanira_pobre", System.StringComparison.OrdinalIgnoreCase) && elapsed >= 0 && elapsed < totalMs; }

    public void Prepare(string id, System.Collections.Generic.Dictionary<string, GTA.Ped> ownRoster,
        GTA.Math.Vector3 realOrigin, GTA.Math.Vector3 actualRight, GTA.Math.Vector3 actualForward, string realLocation)
    {
        Active();
        if (prepared || vehicles != null || !Supports(id)) throw Error("cena não suportada ou preparação anterior presente");
        string expected = id == "EP04_C01" ? "AEROPORTO_TERMINAL" : "CENTRO_LEGION";
        if (realLocation != expected || ownRoster == null) throw Error("a marca real não corresponde à cena de desembarque");
        var roster = new System.Collections.Generic.Dictionary<string, GTA.Ped>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var item in ownRoster) roster.Add(item.Key, item.Value);
        if (!roster.TryGetValue("DJANIRA_POBRE", out actor)) throw Error("Djanira precisa pertencer ao elenco próprio");
        RequirePed(actor); handle = actor.Handle; address = actor.MemoryAddress; model = actor.Model.Hash;
        Vector(realOrigin); Vector(actualForward); Vector(actualRight);
        if (Horizontal(actualForward) < 0.99f || Horizontal(actualRight) < 0.99f ||
            System.Math.Abs(GTA.Math.Vector3.Dot(actualForward, actualRight)) > 0.01f) throw Error("eixos relativos não são ortogonais");
        origin = realOrigin; right = actualRight; forward = actualForward;
        sceneId = id; totalMs = ExpectedDurationMs(id); destination = origin + forward * 2f;
        // Geometria EP01_C06 testada: ônibus à esquerda; porta direita volta-se ao percurso.
        var spec = new VeiculosSerieDiretor.VehicleSpec { Codigo = "ONIBUS_TEMPORADA", Tipo = "ONIBUS", Modelo = "coach",
            PosicaoRelativa = new GTA.Math.Vector3(-2.8f, -3f, 0f), HeadingRelativo = 0f, MotoristaExtra = true };
        spec.ActorInitialSeats.Add("DJANIRA_POBRE", 0);
        spec.Actions.Add(new VeiculosSerieDiretor.VehicleAction { Tipo = "sair", Ator = "DJANIRA_POBRE", InicioMs = 0, DuracaoMs = 5000, Assento = 0 });
        var actors = new System.Collections.Generic.Dictionary<string, GTA.Ped>(System.StringComparer.Ordinal);
        actors.Add("DJANIRA_POBRE", actor);
        vehicles = new VeiculosSerieDiretor(log); prepared = true;
        vehicles.Prepare(actors, new VeiculosSerieDiretor.VehicleSpec[] { spec }, origin, right, forward);
        loading = System.Diagnostics.Stopwatch.StartNew();
        Log("TEMPORADA_ONIBUS_PREPARANDO", "Cena=" + sceneId + "; Local=" + realLocation + "; VozMs=" + totalMs + "; saída física até5s dentro da cena");
    }

    public void PollLoading()
    {
        Active(); if (!prepared || failed) throw Error("preparação ausente/inválida"); if (ready) return;
        try
        {
            RequireActor();
            if (loading.ElapsedMilliseconds >= 5000) throw Error("ônibus/chão/colisão não carregaram em5s");
            vehicles.PollLoading();
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, destination.X, destination.Y, destination.Z);
            float ground;
            GTA.Math.Vector3 probe = destination + new GTA.Math.Vector3(0f, 0f, 2f);
            if (!GTA.World.GetGroundHeight(probe, out ground, GTA.GetGroundHeightMode.Normal)) return;
            float delta = System.Math.Abs(ground - destination.Z);
            if (!Finite(ground) || delta > 2f) throw Error("trajeto fora do chão próximo: Point=" + destination + "; Probe=" + probe + "; Ground=" + ground + "; Delta=" + delta);
            if (!vehicles.IsReady) return;
            destination.Z = ground + 1f; ready = true;
            Log("TEMPORADA_ONIBUS_CHAO_CONFERIDO", "Cena=" + sceneId + "; Destino=" + destination + "; Probe=" + probe + "; Ground=" + ground + "; DeltaZ=" + delta);
        }
        catch { failed = true; throw; }
    }

    public bool ReleaseActorsForReset()
    {
        Active(); resetDone = false; resetPending = false;
        return vehicles == null || vehicles.ReleaseActorsForReset();
    }
    public bool Reset()
    {
        RequireReady();
        if (!resetPending && !ReleaseActorsForReset()) return false;
        resetPending = true;
        if (!vehicles.Reset()) return false;
        resetPending = false; phase = 0; previousElapsed = -1; nextLog = 0; walkingBaseline = false; resetDone = true;
        return true;
    }

    public void Start() { RequireReady(); if (!resetDone) throw Error("restaurar assentos antes da contagem"); vehicles.Start(); }
    public void Update(int elapsed)
    {
        RequireReady();
        if (!resetDone || elapsed < 0 || elapsed < previousElapsed) throw Error("tempo inválido ou assentos não restaurados");
        try
        {
            RequireActor(); previousElapsed = elapsed;
            if (!vehicles.IsFinished) vehicles.Update(elapsed);
            bool walkingStarted = false, arrivalStarted = false;
            // EP06_C01 pede desembarcar e olhar os predios; nao pede caminhada extra.
            // O ponto real da saida precisa dos mesmos pes/piso certificados antes da espera.
            if (sceneId == "EP06_C01" && phase == 0 && vehicles.HasCompletedNormalExit(actor) && RequireWalkingBody(elapsed, true))
            {
                actor.Task.ClearAllImmediately(); actor.Velocity = GTA.Math.Vector3.Zero;
                actor.ResetMovementClipSet(); actor.Task.StandStill(-1);
                actor.IsPositionFrozen = true; phase = 2; walkingStarted = true;
                Log("TEMPORADA_ONIBUS_OLHAR_CIDADE", "DesembarqueMs=" + elapsed +
                    "; CaminhadaExtra=false; PontoRealDaSaida=true; OlharDeclaradoNaInterpretacao=true; " + State(elapsed));
            }
            if (phase == 0 && vehicles.HasCompletedNormalExit(actor) && RequireWalkingBody(elapsed, true))
            {
                actor.Task.ClearAllImmediately(); actor.Velocity = GTA.Math.Vector3.Zero;
                actor.ResetMovementClipSet(); actor.IsPositionFrozen = false;
                GTA.Math.Vector3 delta = destination - actor.Position;
                requestedHeading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, delta.X, delta.Y);
                actor.Task.GoStraightTo(destination, -1, GTA.PedMoveBlendRatio.Walk, requestedHeading, 0.20f);
                phase = 1; walkingStarted = true;
                Log("TEMPORADA_ONIBUS_CAMINHADA", "DesembarqueMs=" + elapsed + "; " + State(elapsed));
            }
            if (phase == 1 && !walkingStarted) RequireWalkingBody(elapsed, false);
            if (phase == 1 && elapsed >= nextLog) { nextLog = elapsed + 1000; Log("TEMPORADA_ONIBUS_GETTERS", State(elapsed)); }
            if (phase == 1 && Horizontal(actor.Position - destination) <= 0.4f && System.Math.Abs(actor.Position.Z - destination.Z) <= 1f)
            {
                actor.Task.ClearAllImmediately(); actor.Velocity = GTA.Math.Vector3.Zero;
                actor.Task.StandStill(-1); phase = 3; arrivalStarted = true;
                Log("TEMPORADA_ONIBUS_CHEGADA_ESPERANDO_APOIO", State(elapsed));
            }
            // Nao congelar os pes elevados da ultima passada. O prazo total continua o original.
            if (phase == 3 && !arrivalStarted && RequireWalkingBody(elapsed, true))
            {
                actor.IsPositionFrozen = true; phase = 2;
                Log("TEMPORADA_ONIBUS_CHEGOU", State(elapsed));
            }
            // A interpretacao declarada5..11s continua controlando somente o olhar.
            // Corpo, saude e piso reais continuam conferidos na posicao fisica da saida.
            if (sceneId == "EP06_C01" && phase == 2 && !walkingStarted && !RequireWalkingBody(elapsed, true))
                throw Error("espera para olhar a cidade sem contato real dos pes: " + State(elapsed));
            if (elapsed >= totalMs && phase != 2) throw Error("não chegou ao terminal dentro da voz original; sem teleporte: " + State(elapsed));
        }
        catch { failed = true; throw; }
    }

    private bool RequireWalkingBody(int elapsed, bool staticContact)
    {
        onFoot = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_ON_FOOT, actor.Handle);
        if (!onFoot || actor.IsInVehicle() || !actor.IsCollisionEnabled || !actor.HasPhysics || actor.IsRagdoll || actor.IsInAir)
            throw Error("corpo nao esta pronto para caminhada fisica: " + State(elapsed));
        GTA.Math.Vector3 position = actor.Position;
        GTA.Math.Vector3 left = actor.Bones[GTA.Bone.SkelLeftFoot].Position, rightFoot = actor.Bones[GTA.Bone.SkelRightFoot].Position;
        Vector(position); Vector(left); Vector(rightFoot); float leftGround, rightGround, rootGround;
        // Native100: GET_GROUND_Z_EXCLUDING_OBJECTS falhou sob um pe e devolveu a
        // camada inferior14m na media da telemetria. Raio vertical curto mede o
        // primeiro piso Map|Objects real; -8/+35cm continuam sendo as mesmas guardas.
        bool leftFound = ClosestMappedFloor(left, left.Z - 0.36f, "esquerdo", elapsed, out leftGround);
        bool rightFound = ClosestMappedFloor(rightFoot, rightFoot.Z - 0.36f, "direito", elapsed, out rightGround);
        bool rootFound = ClosestMappedFloor(position, System.Math.Min(left.Z, rightFoot.Z) - 0.36f, "raiz", elapsed, out rootGround);
        nativeHeight = actor.HeightAboveGround;
        float leftClearance = left.Z - leftGround, rightClearance = rightFoot.Z - rightGround;
        bool supported = leftFound && rightFound && Finite(leftGround) && Finite(rightGround) &&
            leftClearance >= -0.08f && leftClearance <= 0.35f && rightClearance >= -0.08f && rightClearance <= 0.35f;
        currentRootClearance = rootFound ? position.Z - rootGround : float.NaN;
        // Diagnostico somente no Tick ja rejeitado; nenhum resultado extra aceita a passada.
        if (!leftFound) DiagnoseMissingLeftFloor(left, position, elapsed);
        if (!supported || !rootFound || !Finite(rootGround) || !Finite(nativeHeight) || nativeHeight < 0f || !Finite(currentRootClearance) ||
            System.Math.Abs(leftGround - (destination.Z - 1f)) > 0.35f ||
            System.Math.Abs(rightGround - (destination.Z - 1f)) > 0.35f ||
            System.Math.Abs(rootGround - (destination.Z - 1f)) > 0.35f)
            throw Error("caminhada sem corpo/pes na camada original: Left=" + left + "; LeftFound=" + leftFound + "; LeftGround=" + leftGround +
                "; Right=" + rightFoot + "; RightFound=" + rightFound + "; RightGround=" + rightGround +
                "; RootFound=" + rootFound + "; RootGround=" + rootGround + "; " + State(elapsed));
        bool feetContact = System.Math.Abs(leftClearance) <= 0.15f || System.Math.Abs(rightClearance) <= 0.15f;
        if (!walkingBaseline)
        {
            if (!staticContact || !feetContact) return false;
            GTA.Math.Vector3 min, max; actor.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
            float bodyHeight = max.Z - min.Z;
            if (min.X >= max.X || min.Y >= max.Y || bodyHeight <= 0f ||
                currentRootClearance <= 0f || currentRootClearance > bodyHeight)
                throw Error("bounds/altura do corpo desembarcado invalidos: Min=" + min + "; Max=" + max + "; " + State(elapsed));
            standingRootClearance = currentRootClearance; standingNativeHeight = nativeHeight;
            // A subida possivel do corpo e o restante da faixa original de35cm acima do menor pe medido.
            // O limite inferior conserva os8cm de penetracao e a folga real do menor pe, limitado pelo corpo real.
            float footClearance = System.Math.Max(0f, System.Math.Min(leftClearance, rightClearance));
            float rise = System.Math.Min(bodyHeight, 0.35f - footClearance);
            float drop = System.Math.Min(bodyHeight, 0.08f + footClearance);
            lowerRootClearance = standingRootClearance - drop; upperRootClearance = standingRootClearance + rise;
            walkingBaseline = true;
            Log("TEMPORADA_ONIBUS_CORPO_BASE_MEDIDO", "TempoMs=" + elapsed + "; ModelMin=" + min + "; ModelMax=" + max +
                "; LeftClearance=" + leftClearance + "; RightClearance=" + rightClearance +
                "; NativeHeight=" + standingNativeHeight + "; RootClearance=" + standingRootClearance +
                "; LowerRoot=" + lowerRootClearance + "; UpperRoot=" + upperRootClearance + "; SemSetterCorpo=true");
        }
        float relativeNativeHeight = standingRootClearance + nativeHeight - standingNativeHeight;
        if (currentRootClearance < lowerRootClearance || currentRootClearance > upperRootClearance ||
            relativeNativeHeight < lowerRootClearance || relativeNativeHeight > upperRootClearance)
            throw Error("altura fisica do corpo saiu da faixa medida do desembarque: LeftClearance=" + leftClearance +
                "; RightClearance=" + rightClearance + "; RelativeNativeHeight=" + relativeNativeHeight + "; " + State(elapsed));
        // Durante a passada ambos os ossos podem ultrapassar15cm. Desembarque e chegada exigem contato parado.
        return !staticContact || feetContact;
    }

    private bool ClosestMappedFloor(GTA.Math.Vector3 point, float bottomZ, string label, int elapsed, out float height)
    {
        height = float.NaN;
        // Margem de consulta1cm inclui os extremos da faixa; a aceitacao segue
        // estritamente -0.08..0.35. Nenhum raio alcança a camada inferior distante.
        GTA.Math.Vector3 top = point + new GTA.Math.Vector3(0f, 0f, 0.09f);
        GTA.Math.Vector3 bottom = new GTA.Math.Vector3(point.X, point.Y, bottomZ);
        Vector(top); Vector(bottom);
        if (!Finite(bottomZ) || bottomZ >= top.Z) throw Error("limites do raio de piso invalidos");
        // Consulta desde acima da raiz real; a faixa de aceitacao top..bottom nao muda.
        GTA.Math.Vector3 querySource = new GTA.Math.Vector3(point.X, point.Y,
            System.Math.Max(top.Z, actor.Position.Z + 0.25f));
        Vector(querySource);
        // Options4 conserva IgnoreNoCollision, mas inclui colisores Glass/SeeThrough.
        GTA.ShapeTestHandle probe = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(querySource, bottom,
            GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, actor, (GTA.ShapeTestOptions)4);
        GTA.ShapeTestResult ray;
        GTA.ShapeTestStatus rayStatus = probe.GetResult(out ray);
        bool complete = rayStatus == GTA.ShapeTestStatus.Ready;
        bool hit = complete && ray.DidHit;
        GTA.Math.Vector3 contact = hit ? ray.HitPosition : GTA.Math.Vector3.Zero;
        GTA.Math.Vector3 normal = hit ? ray.SurfaceNormal : GTA.Math.Vector3.Zero;
        bool finite = Finite(contact.X) && Finite(contact.Y) && Finite(contact.Z) &&
            Finite(normal.X) && Finite(normal.Y) && Finite(normal.Z);
        bool valid = hit && finite && System.Math.Abs(contact.X - point.X) <= 0.01f &&
            System.Math.Abs(contact.Y - point.Y) <= 0.01f && contact.Z >= bottom.Z - 0.01f &&
            contact.Z <= top.Z + 0.01f && System.Math.Abs(normal.X) <= 1.01f &&
            System.Math.Abs(normal.Y) <= 1.01f && normal.Z > 0f && normal.Z <= 1.01f;
        if (valid) height = contact.Z;
        if (!valid || elapsed >= nextLog)
            Log("TEMPORADA_ONIBUS_PISO_RAY", "Cena=" + sceneId + "; TempoMs=" + elapsed + "; Ponto=" + label +
                "; Source=" + querySource + "; AcceptedSource=" + top + "; Target=" + bottom +
                "; Result=" + (int)rayStatus + "; Hit=" + hit +
                "; Posicao=" + contact + "; Normal=" + normal + "; Valido=" + valid +
                "; Flags=Map|Objects; Options=4; Ignorado=atorProprio; Metodo=ShapeTest_sincrono; SemModeloEntidade=true");
        return valid;
    }

    private void DiagnoseMissingLeftFloor(GTA.Math.Vector3 foot, GTA.Math.Vector3 root, int elapsed)
    {
        GTA.Math.Vector3 acceptedTop = foot + new GTA.Math.Vector3(0f, 0f, 0.09f);
        GTA.Math.Vector3 bottom = foot - new GTA.Math.Vector3(0f, 0f, 0.36f);
        GTA.Math.Vector3 source = new GTA.Math.Vector3(foot.X, foot.Y, System.Math.Max(acceptedTop.Z, root.Z + 0.25f));
        // Dois raios completos e duas consultas de chao, somente depois do miss original.
        // Options0 inclui Glass/SeeThrough/NoCollision; nao remove a guarda original Options4.
        for (int variant = 0; variant < 2; variant++)
        {
            GTA.IntersectFlags flags = variant == 0 ? GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects : GTA.IntersectFlags.Map;
            GTA.ShapeTestHandle probe = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(source, bottom,
                flags, actor, (GTA.ShapeTestOptions)0);
            GTA.ShapeTestResult result;
            GTA.ShapeTestStatus status = probe.GetResult(out result);
            bool hit = status == GTA.ShapeTestStatus.Ready && result.DidHit;
            GTA.Math.Vector3 contact = hit ? result.HitPosition : GTA.Math.Vector3.Zero;
            GTA.Math.Vector3 normal = hit ? result.SurfaceNormal : GTA.Math.Vector3.Zero;
            bool finite = Finite(contact.X) && Finite(contact.Y) && Finite(contact.Z) &&
                Finite(normal.X) && Finite(normal.Y) && Finite(normal.Z);
            bool within = hit && finite && System.Math.Abs(contact.X - foot.X) <= 0.01f &&
                System.Math.Abs(contact.Y - foot.Y) <= 0.01f && contact.Z >= bottom.Z - 0.01f &&
                contact.Z <= acceptedTop.Z + 0.01f && System.Math.Abs(normal.X) <= 1.01f &&
                System.Math.Abs(normal.Y) <= 1.01f && normal.Z > 0f && normal.Z <= 1.01f;
            Log("TEMPORADA_ONIBUS_PISO_DIAGNOSTICO", "Cena=" + sceneId + "; TempoMs=" + elapsed +
                "; Variante=" + variant + "; Flags=" + (int)flags + "; Options=0; Source=" + source +
                "; AcceptedSource=" + acceptedTop + "; Target=" + bottom + "; Result=" + (int)status +
                "; Hit=" + hit + "; Contact=" + contact + "; Normal=" + normal + "; NaFaixaOriginal=" + within +
                "; SomenteLeitura=true; AceitacaoAlterada=false; SemModeloEntidade=true");
        }
        GTA.Math.Vector3 boneProbe = foot + new GTA.Math.Vector3(0f, 0f, 0.25f);
        GTA.Math.Vector3 rootProbe = new GTA.Math.Vector3(foot.X, foot.Y, root.Z + 0.25f);
        float boneGround, rootGround;
        bool boneFound = GTA.World.GetGroundHeight(boneProbe, out boneGround, GTA.GetGroundHeightMode.Normal);
        bool rootFound = GTA.World.GetGroundHeight(rootProbe, out rootGround, GTA.GetGroundHeightMode.Normal);
        GTA.Vehicle bus = vehicles.GetVehicle("ONIBUS_TEMPORADA");
        GTA.Math.Vector3 busPosition = bus.Position, busVelocity = bus.Velocity;
        Log("TEMPORADA_ONIBUS_PISO_DIAGNOSTICO_NATIVE", "Cena=" + sceneId + "; TempoMs=" + elapsed +
            "; ActorRoot=" + root + "; LeftFoot=" + foot + "; BoneProbe=" + boneProbe +
            "; BoneFound=" + boneFound + "; BoneGround=" + (boneFound ? boneGround.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "nao_obtido") +
            "; RootProbeMesmoXY=" + rootProbe + "; RootFound=" + rootFound +
            "; RootGround=" + (rootFound ? rootGround.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "nao_obtido") +
            "; GroundMode=Normal; MesmaConsultaPreparoCoach=true; CoachProprio=" + bus.Handle +
            "; CoachPosition=" + busPosition + "; CoachHeading=" + bus.Heading + "; CoachVelocity=" + busVelocity +
            "; DistanceFootCoachXY=" + Horizontal(foot - busPosition) + "; Destination=" + destination +
            "; SomenteLeitura=true; AceitacaoAlterada=false; NativeQueriesExtrasSomenteFalha=6");
    }

    private string State(int elapsed)
    {
        GTA.ScriptTaskNameHash task; GTA.ScriptTaskStatus taskStatus;
        actor.GetCurrentScriptTaskNameHashAndStatus(out task, out taskStatus);
        return "Cena=" + sceneId + "; TempoMs=" + elapsed + "; Fase=" + phase + "; Actor=" + actor.Position +
            "; Destination=" + destination + "; DistanceXY=" + Horizontal(actor.Position - destination).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; DeltaZ=" + System.Math.Abs(actor.Position.Z - destination.Z) + "; Speed=" + actor.Speed +
            "; Frozen=" + actor.IsPositionFrozen + "; Collision=" + actor.IsCollisionEnabled + "; Physics=" + actor.HasPhysics +
            "; OnFoot=" + onFoot + "; HeightAboveGround=" + nativeHeight + "; RootClearance=" + currentRootClearance +
            "; StandingRoot=" + standingRootClearance + "; LowerRoot=" + lowerRootClearance + "; UpperRoot=" + upperRootClearance +
            "; Heading=" + actor.Heading + "; RequestedHeading=" + requestedHeading +
            "; Task=" + task + "; TaskStatus=" + taskStatus;
    }
    public void Cleanup()
    {
        if (disposed) return;
        if (vehicles != null) { vehicles.Dispose(); vehicles = null; }
        prepared = ready = resetDone = resetPending = failed = walkingBaseline = false; actor = null; loading = null;
    }
    public void Dispose() { Cleanup(); disposed = true; }
    private void RequireActor() { if (System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.Handle != handle || actor.Model.Hash != model || actor.MemoryAddress != address || actor.IsDead || actor.IsPlayer) throw Error("identidade do ator próprio mudou"); }
    private static void RequirePed(GTA.Ped p) { if (System.Object.ReferenceEquals(p, null) || !p.Exists() || p.IsPlayer || p.IsDead || !p.IsPersistent || p.IsInVehicle()) throw Error("ator precisa ser próprio, vivo e livre"); }
    private void RequireReady() { Active(); if (!IsReady) throw Error("montagem física não está pronta"); }
    private void Active() { if (disposed) throw new System.ObjectDisposedException("EpisodiosTresSeteDiretor"); }
    private static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    private static void Vector(GTA.Math.Vector3 x) { if (!Finite(x.X) || !Finite(x.Y) || !Finite(x.Z)) throw Error("vetor não finito"); }
    private static float Horizontal(GTA.Math.Vector3 x) { return (float)System.Math.Sqrt(x.X * x.X + x.Y * x.Y); }
    private void Log(string tag, string value) { if (log != null) log(tag, value); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Temporada física: " + message); }
}

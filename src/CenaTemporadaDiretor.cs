// Saída de água sem avião. A entrada e o topo vêm exclusivamente de marcas reais.
public sealed class CenaTemporadaDiretor : System.IDisposable
{
    public sealed class SaidaAguaSpec
    {
        public GTA.Math.Vector3 Agua, EntradaRampa, TopoRampa;
        public int DuracaoMs = 42000;
    }
    private readonly System.Action<string, string> log;
    private GTA.Ped actor;
    private int handle, model, health, lastElapsed = -1, phase, phaseAt, surfaceSince = -1, sampleIndex;
    private System.IntPtr address;
    private GTA.Math.Vector3 originalPosition;
    private float originalHeading;
    private bool frozen, invincible, events, drowning, instantWater, prepared, ready, running, finished, failed, disposed;
    private SaidaAguaSpec spec;
    private readonly System.Collections.Generic.List<GTA.Math.Vector3> route = new System.Collections.Generic.List<GTA.Math.Vector3>();
    private System.Diagnostics.Stopwatch loading, waterReset;
    private bool waterResetPending, waterResetDone;
    private int waterSettleAt, waterStableSince = -1, nextWaterDiagnostic;
    private float measuredWaterHeight;
    private string waterPreparationPhase;

    public CenaTemporadaDiretor(System.Action<string, string> log) { this.log = log; }
    public bool IsReady { get { return prepared && ready && !waterResetPending && !failed && !disposed; } }
    public bool IsFinished { get { return finished && !failed; } }
    public bool HasFailed { get { return failed; } }
    public void PrepareSaidaAgua(GTA.Ped ownActor, SaidaAguaSpec value)
    {
        if (disposed || prepared || value == null || value.DuracaoMs < 15000 || value.DuracaoMs > 50000)
            throw Error("saída da água exige preparo único e orçamento15–50s");
        RequirePed(ownActor); Vector(value.Agua); Vector(value.EntradaRampa); Vector(value.TopoRampa);
        if (Horizontal(value.EntradaRampa - value.Agua) > 160f || Horizontal(value.TopoRampa - value.EntradaRampa) > 25f ||
            System.Math.Abs(value.TopoRampa.Z - value.EntradaRampa.Z) > 8f || Horizontal(ownActor.Position - value.Agua) > 2f)
            throw Error("ator/rota fora das marcas físicas da água e rampa");
        actor = ownActor; handle = actor.Handle; model = actor.Model.Hash; address = actor.MemoryAddress;
        health = actor.Health; originalPosition = actor.Position; originalHeading = actor.Heading;
        frozen = actor.IsPositionFrozen; invincible = actor.IsInvincible; events = actor.BlockPermanentEvents;
        drowning = actor.GetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater);
        instantWater = actor.GetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming);
        spec = new SaidaAguaSpec { Agua = value.Agua, EntradaRampa = value.EntradaRampa, TopoRampa = value.TopoRampa, DuracaoMs = value.DuracaoMs };
        prepared = true; loading = System.Diagnostics.Stopwatch.StartNew();
        actor.IsInvincible = true; actor.BlockPermanentEvents = true;
        actor.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, false);
        actor.SetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming, false);
        float water; var diagnostic = new CenaQuedaDiretor.WaterQueryDiagnostic();
        if (!CenaQuedaDiretor.TryWater(spec.Agua, out water, diagnostic) || System.Math.Abs(spec.Agua.Z - water) > 3f)
            throw Error("CANAL_LS não é água real: " + diagnostic.ToLog());
        AddRoute(spec.EntradaRampa, spec.TopoRampa);
        BeginWaterBody(water, "PREPARO");
        Log("TEMPORADA_AGUA_PREPARO", "Actor=" + actor.Position + "; Agua=" + spec.Agua + "; Entrada=" + spec.EntradaRampa + "; Topo=" + spec.TopoRampa + "; SemAviao=true; TesteVisual=pendente");
    }
    private void AddRoute(GTA.Math.Vector3 from, GTA.Math.Vector3 to)
    {
        int count = System.Math.Max(1, (int)System.Math.Ceiling(Horizontal(to - from) / 2f));
        for (int i = 0; i <= count; i++) route.Add(from + (to - from) * ((float)i / count));
    }
    public void PollLoading()
    {
        Owned(); if (ready) return;
        if (loading.ElapsedMilliseconds > 8000)
        {
            LogWaterBody("TIMEOUT_PREPARO", "");
            throw Error("piso/colisao/agua e corpo dentro da agua real nao ficaram prontos em8s");
        }
        // Um ponto por Tick: ground+normal e até3 queries da água, máximo4.
        if (sampleIndex < route.Count)
        {
            GTA.Math.Vector3 point = route[sampleIndex], normal; float ground, water;
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, point.X, point.Y, point.Z);
            if (!GTA.World.GetGroundHeightAndNormal(point + new GTA.Math.Vector3(0f, 0f, 2f), out ground, out normal)) return;
            Vector(normal);
            if (!Finite(ground) || normal.Z < 0.7f || System.Math.Abs(point.Z - ground) > 3f)
                throw Error("rampa não corresponde ao piso/slope real: " + point + "; Ground=" + ground + "; Normal=" + normal);
            if (sampleIndex == route.Count - 1 && CenaQuedaDiretor.TryWater(point, out water) && water > ground + 0.1f)
                throw Error("topo da rampa permanece submerso");
            sampleIndex++; return;
        }
        if (!PollWaterBody()) return;
        ready = true; Log("TEMPORADA_AGUA_PRONTA", "AmostrasRampa=" + route.Count +
            "; ChaoReal=true; Actor=" + actor.Position + "; Water=" + measuredWaterHeight +
            "; InWater=" + actor.IsInWater + "; Frozen=" + actor.IsPositionFrozen +
            "; EstabilidadeRealMs=500; SemPosicaoDuranteAcao=true");
    }
    public bool Reset()
    {
        Owned(); if (!ready || failed) throw Error("aguarde preparo fisico antes de reiniciar");
        if (actor.IsInVehicle()) throw Error("ator entrou em veiculo; preservar sem mover");
        // ResetSceneVehicles pode consultar todos os helpers novamente enquanto outro aguarda.
        // O reset generico de uma NOVA tomada congela o ped; isso invalida este retorno anterior.
        if (!waterResetPending && waterResetDone && !running && !finished &&
            !actor.IsPositionFrozen && actor.IsInWater && Horizontal(actor.Position - spec.Agua) <= 2f)
            return true;
        if (!waterResetPending)
        {
            float water; var diagnostic = new CenaQuedaDiretor.WaterQueryDiagnostic();
            if (!CenaQuedaDiretor.TryWater(spec.Agua, out water, diagnostic) || System.Math.Abs(spec.Agua.Z - water) > 3f)
                throw Error("retorno precisa da mesma agua real: " + diagnostic.ToLog());
            running = false; finished = false; failed = false; lastElapsed = -1; phase = 0; phaseAt = 0; surfaceSince = -1;
            waterResetPending = true; waterResetDone = false;
            waterReset = System.Diagnostics.Stopwatch.StartNew();
            BeginWaterBody(water, "RETORNO");
            return false; // Pelo menos um frame de fisica antes de observar IsInWater.
        }
        if (waterReset.ElapsedMilliseconds > 5000)
        {
            LogWaterBody("TIMEOUT_RETORNO", "");
            throw Error("corpo proprio nao retornou a agua real em5s");
        }
        if (!PollWaterBody()) return false;
        waterResetPending = false; waterResetDone = true; waterReset = null;
        LogWaterBody("RETORNO_CONCLUIDO", "; EstabilidadeRealMs=500");
        return true;
    }
    private void BeginWaterBody(float water, string preparationPhase)
    {
        Owned();
        if (actor.IsInVehicle() || actor.Health != health || !Finite(water) || System.Math.Abs(spec.Agua.Z - water) > 3f)
            throw Error("assentamento exige ator proprio saudavel a pe e nivel da agua medido dentro da marca");
        GTA.Math.Vector3 target = new GTA.Math.Vector3(spec.Agua.X, spec.Agua.Y, water);
        Vector(target);
        // Somente PREPARO/RETORNO: XY da marca, Z do getter real, sem offset de imersao inventado.
        // Nenhuma posicao e aplicada por Tick ou durante Start/Update.
        actor.Task.ClearAllImmediately(); actor.Velocity = GTA.Math.Vector3.Zero;
        actor.PositionNoOffset = target; actor.Heading = originalHeading; actor.IsPositionFrozen = false;
        measuredWaterHeight = water; waterPreparationPhase = preparationPhase;
        waterSettleAt = GTA.Game.GameTime; waterStableSince = -1; nextWaterDiagnostic = 0;
        LogWaterBody("POSICAO_NIVEL_MEDIDO", "; Target=" + target + "; OrigemZ=TryWater; Ready=False");
    }
    private bool PollWaterBody()
    {
        Owned(); Vector(actor.Position);
        if (actor.IsInVehicle() || actor.Health != health || Horizontal(actor.Position - spec.Agua) > 2f)
            throw Error("assentamento perdeu ator saudavel a pe ou a marca real da agua");
        if (!actor.HasPhysics || !actor.IsCollisionEnabled || actor.IsPositionFrozen)
        {
            LogWaterBody("FLAGS_INVALIDAS", "");
            throw Error("assentamento exige physics, colisao e corpo proprio descongelado");
        }
        float water; var diagnostic = new CenaQuedaDiretor.WaterQueryDiagnostic();
        bool waterFound = CenaQuedaDiretor.TryWater(actor.Position, out water, diagnostic);
        if (waterFound)
        {
            if (!Finite(water) || System.Math.Abs(spec.Agua.Z - water) > 3f || System.Math.Abs(actor.Position.Z - water) > 3f)
                throw Error("corpo ou agua real afastou-se da camada medida da marca");
            measuredWaterHeight = water;
        }
        int elapsed = unchecked((int)(unchecked((uint)GTA.Game.GameTime) - unchecked((uint)waterSettleAt)));
        if (elapsed >= nextWaterDiagnostic)
        {
            LogWaterBody("AGUARDA_FISICA", "; WaterFound=" + waterFound + "; " + diagnostic.ToLog());
            nextWaterDiagnostic = elapsed + 500;
        }
        if (!waterFound || !actor.IsInWater || actor.IsSwimmingUnderWater || actor.IsRagdoll)
        { waterStableSince = -1; return false; }
        if (waterStableSince < 0) waterStableSince = elapsed;
        return elapsed - waterStableSince >= 500; // Mesma estabilidade de superficie exigida por Update.
    }
    private void LogWaterBody(string step, string extra)
    {
        Log("TEMPORADA_AGUA_ASSENTAMENTO", "Fase=" + waterPreparationPhase + "; Etapa=" + step +
            "; Actor=" + actor.Position + "; Water=" + measuredWaterHeight + "; Health=" + actor.Health +
            "; InWater=" + actor.IsInWater + "; UnderWater=" + actor.IsSwimmingUnderWater +
            "; Ragdoll=" + actor.IsRagdoll + "; Frozen=" + actor.IsPositionFrozen +
            "; HasPhysics=" + actor.HasPhysics + "; Collision=" + actor.IsCollisionEnabled + extra);
    }
    public void Start()
    {
        Owned(); if (!IsReady || running || finished) throw Error("reinicie antes da ação de água");
        if (!actor.IsInWater || Horizontal(actor.Position - spec.Agua) > 2f) throw Error("início precisa mostrar corpo próprio dentro da água real");
        actor.IsInvincible = true; actor.BlockPermanentEvents = true;
        actor.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, false);
        actor.SetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming, false);
        actor.IsPositionFrozen = false; running = true; phase = 0; phaseAt = 0;
        Log("TEMPORADA_AGUA_INICIO", "Actor=" + actor.Position + "; Health=" + actor.Health + "; InWater=" + actor.IsInWater);
    }
    public void Update(int elapsedMs)
    {
        Owned(); if (finished) return;
        if (!running || elapsedMs < 0 || elapsedMs < lastElapsed) throw Error("água exige relógio crescente após Start");
        lastElapsed = elapsedMs;
        try
        {
            if (elapsedMs >= spec.DuracaoMs || actor.Health != health || actor.IsInVehicle()) throw Error("saída perdeu saúde/tempo/identidade a pé");
            if (phase == 0)
            {
                if (actor.IsSwimmingUnderWater || actor.IsRagdoll || !actor.IsInWater) { surfaceSince = -1; return; }
                float water; var diagnostic = new CenaQuedaDiretor.WaterQueryDiagnostic();
                if (!CenaQuedaDiretor.TryWater(actor.Position, out water, diagnostic)) throw Error("emersão não está sobre água: " + diagnostic.ToLog());
                if (surfaceSince < 0) surfaceSince = elapsedMs;
                if (elapsedMs - surfaceSince < 500) return;
                Move(spec.EntradaRampa); phase = 1; phaseAt = elapsedMs;
                Log("TEMPORADA_EMERSAO_REAL", "TempoMs=" + elapsedMs + "; Actor=" + actor.Position + "; Water=" + water);
            }
            else if (phase == 1)
            {
                if (Horizontal(actor.Position - spec.EntradaRampa) <= 1.5f)
                { Move(spec.TopoRampa); phase = 2; phaseAt = elapsedMs; Log("TEMPORADA_RAMPA_INICIO", "TempoMs=" + elapsedMs + "; Actor=" + actor.Position); }
                else if (elapsedMs - phaseAt > 30000) throw Error("não alcançou entrada física da rampa em30s");
            }
            else
            {
                if (!actor.IsInWater && Horizontal(actor.Position - spec.TopoRampa) <= 1f && System.Math.Abs(actor.Position.Z - spec.TopoRampa.Z) <= 1.5f)
                {
                    actor.Task.StandStill(-1); running = false; finished = true;
                    Log("TEMPORADA_RAMPA_CONCLUIDA", "TempoMs=" + elapsedMs + "; Actor=" + actor.Position + "; Topo=" + spec.TopoRampa + "; Health=" + actor.Health + "; SemWarp=true");
                }
                else if (elapsedMs - phaseAt > 15000) throw Error("rampa não foi vencida fisicamente em15s");
            }
        }
        catch
        {
            failed = true; running = false;
            Log("TEMPORADA_AGUA_FALHA", "TempoMs=" + elapsedMs + "; Fase=" + phase + "; Actor=" + actor.Position + "; Entrada=" + spec.EntradaRampa + "; Topo=" + spec.TopoRampa);
            throw;
        }
    }
    private void Move(GTA.Math.Vector3 point)
    { actor.Task.FollowNavMeshTo(point, new GTA.PedMoveBlendRatio(1f), -1, 0.5f, GTA.FollowNavMeshFlags.Default); }
    public void Cleanup()
    {
        if (disposed || !prepared) return;
        Owned(); if (actor.IsInVehicle()) throw Error("ator em veículo alheio; preservar na limpeza");
        actor.Task.ClearAllImmediately(); actor.Velocity = GTA.Math.Vector3.Zero;
        actor.PositionNoOffset = originalPosition; actor.Heading = originalHeading;
        actor.IsPositionFrozen = frozen; actor.IsInvincible = invincible; actor.BlockPermanentEvents = events;
        actor.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, drowning);
        actor.SetConfigFlag(GTA.PedConfigFlagToggles.DiesInstantlyWhenSwimming, instantWater);
        actor = null; spec = null; prepared = ready = running = finished = failed = false; route.Clear(); sampleIndex = 0; lastElapsed = -1;
        waterResetPending = waterResetDone = false; waterReset = null; waterStableSince = -1;
    }
    public void Dispose() { Cleanup(); disposed = true; }
    private void Owned()
    {
        if (disposed || !prepared) throw Error("saída da água não preparada");
        RequirePed(actor);
        if (actor.Handle != handle || actor.Model.Hash != model || actor.MemoryAddress != address) throw Error("identidade do ator próprio mudou");
    }
    private static void RequirePed(GTA.Ped ped)
    { if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsPlayer || ped.IsDead || !ped.IsPersistent) throw Error("saída exige ator próprio vivo/persistente"); }
    private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    private static void Vector(GTA.Math.Vector3 p)
    { if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z) || System.Math.Abs(p.X) > 30000f || System.Math.Abs(p.Y) > 30000f || System.Math.Abs(p.Z) > 3000f) throw Error("vetor físico fora dos limites"); }
    private static float Horizontal(GTA.Math.Vector3 p) { return (float)System.Math.Sqrt(p.X*p.X+p.Y*p.Y); }
    private static System.InvalidOperationException Error(string m) { return new System.InvalidOperationException("Saída de água: " + m); }
    private void Log(string e, string m) { if (log != null) log(e,m); }
}

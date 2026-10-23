// Ensaios técnicos isolados. Não representam EP01_C01/EP07_C05 completos nem ativam IA de combate.
public sealed class EnsaioAcaoDiretor : System.IDisposable
{
    private sealed class ActorState
    {
        internal GTA.Ped Ped;
        internal int Handle, Health;
        internal System.IntPtr Address;
        internal GTA.Math.Vector3 Position;
        internal float Heading;
        internal bool Invincible, Ragdoll, Drowning, Frozen, Events;
    }
    private const string FightDictionary = "melee@unarmed@streamed_core";
    private const string AttackClip = "shove", ReactionClip = "hit_heavy_punch_a", PlaneCode = "ENSAIO_AVIAO";
    private readonly System.Action<string, string> log;
    private readonly ObjetosDiretor resources;
    private ActorState first, second;
    private GTA.Vehicle plane;
    private GTA.Math.Vector3 waterMark, direction;
    private float waterHeight;
    private string mode = "";
    private bool prepared, ready, finished, disposed, dictionaryRequested, planeRequested, waterProven;
    private bool resetWaitingSeat, passengerLeaving, resetDone;
    private int phase, previousElapsed = -1;
    private System.Diagnostics.Stopwatch loadingTimer, exitTimer, seatTimer;

    public EnsaioAcaoDiretor(System.Action<string, string> log)
    { this.log = log; resources = new ObjetosDiretor(log); }
    public bool IsReady { get { return ready && !disposed; } }
    public bool IsFinished { get { return finished; } }
    public string Mode { get { return mode; } }
    public GTA.Entity[] OwnedEntities
    {
        get { return !System.Object.ReferenceEquals(plane, null) && plane.Exists() ? new GTA.Entity[] { plane } : new GTA.Entity[0]; }
    }
    public int[] OwnedHandles
    {
        get { return !System.Object.ReferenceEquals(plane, null) && plane.Exists() ? new int[] { plane.Handle } : new int[0]; }
    }

    public void PrepareCoreografia(GTA.Ped atacante, GTA.Ped receptor, GTA.Math.Vector3 marcaReal)
    {
        StartPreparation("ensaio_coreografia_v1");
        RequireVector(marcaReal);
        first = Capture(atacante); second = Capture(receptor);
        if (first.Handle == second.Handle) throw Error("a coreografia exige dois atores diferentes");
        float distance = HorizontalDistance(first.Position, second.Position);
        if (distance < 0.7f || distance > 1.5f || HorizontalDistance((first.Position + second.Position) * 0.5f, marcaReal) > 1f)
            throw Error("monte os dois atores nas marcas reais, a aproximadamente1,1m de distância");
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ANIM_DICT_EXIST, FightDictionary))
            throw Error("o dicionário da coreografia não existe neste GTA");
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, FightDictionary); dictionaryRequested = true;
        Log("ENSAIO_COREOGRAFIA_PREPARO", "shove:PedAnimList61481; hit_heavy_punch_a:61437; candidatos separados; sem IA/pareamento visual comprovado");
    }

    // O XY é exatamente a marca capturada sobre água. Nunca procurar água deslocando marca em terra.
    // Este ensaio cobre saída física/queda/água; não promete rampa/fuga nem14s do episódio.
    public void PrepareQueda(GTA.Ped djanira, GTA.Math.Vector3 marcaSobreAguaReal, GTA.Math.Vector3 direcao)
    {
        StartPreparation("ensaio_queda_v1");
        RequireVector(marcaSobreAguaReal); RequireVector(direcao);
        first = Capture(djanira);
        float length = HorizontalLength(direcao);
        if (length < 0.01f) throw Error("a direção real da marca precisa estar disponível");
        direction = new GTA.Math.Vector3(direcao.X / length, direcao.Y / length, 0f);
        waterMark = marcaSobreAguaReal;
        float height;
        var diagnostic = new CenaQuedaDiretor.WaterQueryDiagnostic();
        if (!TryWater(waterMark, out height, diagnostic))
        {
            LogWaterCorridorFailure(-1, "marca", 0f, waterMark, height, waterMark.Z, diagnostic);
            throw Error("a marca CANAL_LS precisa ter água no mesmo XY; capture o local real nadando, sem veículo");
        }
        if (System.Math.Abs(height - waterMark.Z) > 3f)
            throw Error("a marca não está próxima da superfície real da água; não deslocar coordenadas para simular outro lugar");
        waterHeight = height;
        Log("ENSAIO_QUEDA_PREPARO", "XY capturado sobre água; Velum candidato nativo; altitude técnica relativa12m; invencibilidade obrigatória; sem cena de episódio publicada");
    }

    public void PollLoading()
    {
        Active();
        if (!prepared) throw Error("prepare o ensaio antes de carregar os recursos");
        if (ready) return;
        RequireActors();
        if (mode == "ensaio_coreografia_v1")
        {
            if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, FightDictionary))
            {
                ValidateClip(AttackClip); ValidateClip(ReactionClip);
                ValidateGround(first.Position); ValidateGround(second.Position);
                ready = true;
            }
        }
        else
        {
            if (!waterProven)
            {
                ValidateWaterCorridor();
                waterProven = true;
            }
            if (!planeRequested)
            {
                // Velum é o identificador da enumeração nativa VehicleHash.Velum; validar no jogo.
                GTA.Model expected = new GTA.Model(GTA.VehicleHash.Velum);
                GTA.Model named = new GTA.Model("velum");
                if (expected.Hash != named.Hash || !named.IsInCdImage || !named.IsVehicle)
                    throw Error("o avião nativo Velum não está disponível neste GTA");
                resources.RequestVehicle(PlaneCode, "velum", new GTA.Math.Vector3(waterMark.X, waterMark.Y, waterHeight + 12f), direction.ToHeading(), true, false);
                planeRequested = true;
            }
            resources.Update();
            if (resources.IsReady)
            {
                plane = resources.GetVehicle(PlaneCode);
                if (!plane.IsPlane || !plane.IsSeatFree(GTA.VehicleSeat.Passenger))
                    throw Error("o veículo carregado não é avião com assento de passageiro livre");
                ready = true;
            }
        }
        if (ready) Log("ENSAIO_RECURSOS_PRONTOS", "Modo=" + mode + "; validação técnica; resultado visual pendente");
        else if (loadingTimer.ElapsedMilliseconds >= 5000) throw Error("o recurso técnico não carregou em cinco segundos");
    }

    // Chamar fora da filmagem, após restabelecer marcas/câmera. Nenhuma entidade alheia é removida.
    public bool ReleaseActorsForReset()
    {
        Active(); RequireReady(); RequireActors();
        resetDone = false; resetWaitingSeat = false; seatTimer = null;
        if (!RemoveOwnPassenger()) return false;
        first.Ped.Task.ClearAllImmediately(); first.Ped.Task.ClearLookAt(); first.Ped.Velocity = GTA.Math.Vector3.Zero;
        if (second != null)
        { second.Ped.Task.ClearAllImmediately(); second.Ped.Task.ClearLookAt(); second.Ped.Velocity = GTA.Math.Vector3.Zero; }
        phase = 0; finished = false; previousElapsed = -1;
        return true;
    }

    public bool Reset()
    {
        Active(); RequireReady(); RequireActors();
        // Sentar pode aparecer só no próximo frame: não soltar nem recapturar marca no assento.
        if (resetWaitingSeat) return CompleteResetSeat();
        if (!ReleaseActorsForReset()) return false;
        RefreshMark(first); if (second != null) RefreshMark(second);
        PrepareActor(first); if (second != null) PrepareActor(second);
        if (mode == "ensaio_queda_v1")
        {
            ValidateWaterCorridor();
            resources.Reset(); plane = resources.GetVehicle(PlaneCode);
            first.Ped.CanRagdoll = true;
            first.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, false);
            first.Ped.IsPositionFrozen = false;
            resetWaitingSeat = true; seatTimer = System.Diagnostics.Stopwatch.StartNew();
            first.Ped.SetIntoVehicle(plane, GTA.VehicleSeat.Passenger);
            plane.IsEngineRunning = true;
            return CompleteResetSeat();
        }
        resetDone = true;
        Log("ENSAIO_REINICIADO", "Modo=" + mode + "; proteção e marcas próprias restabelecidas");
        return true;
    }

    private bool CompleteResetSeat()
    {
        plane = resources.GetVehicle(PlaneCode);
        RequireOwnOccupants();
        int passenger = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_IN_VEHICLE_SEAT,
            plane.Handle, (int)GTA.VehicleSeat.Passenger, false);
        if (passenger != 0 && passenger != first.Handle)
            throw Error("há ocupante alheio no assento inicial; avião e ator serão preservados");
        if (!InPlane(first.Ped) || passenger != first.Handle)
        {
            if (seatTimer == null || seatTimer.ElapsedMilliseconds >= 5000)
                throw Error("o assento próprio não foi confirmado em cinco segundos; recursos preservados");
            return false;
        }
        resetWaitingSeat = false; seatTimer = null; resetDone = true;
        Log("ENSAIO_REINICIADO", "Modo=" + mode + "; passageiro confirmado no avião próprio; proteção e marcas restabelecidas");
        return true;
    }

    public void Update(int elapsedMs)
    {
        Active(); RequireReady(); RequireActors();
        if (!resetDone || resetWaitingSeat || passengerLeaving) throw Error("aguarde o retorno confirmado antes de executar o ensaio");
        if (elapsedMs < 0 || elapsedMs < previousElapsed) throw Error("reinicie o ensaio antes de voltar o relógio");
        previousElapsed = elapsedMs;
        first.Ped.IsInvincible = true;
        if (second != null) second.Ped.IsInvincible = true;
        if (mode == "ensaio_coreografia_v1")
        {
            if (phase == 0)
            {
                if (elapsedMs >= 2000) throw Error("o ensaio perdeu sua abertura; reinicie para sincronizar os candidatos");
                first.Ped.IsPositionFrozen = true; second.Ped.IsPositionFrozen = true;
                float heading = (second.Ped.Position - first.Ped.Position).ToHeading();
                first.Ped.Heading = heading; second.Ped.Heading = (heading + 180f) % 360f;
                Play(first.Ped, AttackClip); phase = 1;
            }
            if (phase == 1 && elapsedMs >= 150)
            { Play(second.Ped, ReactionClip); phase = 2; }
            if (phase == 2 && elapsedMs >= 2200)
            {
                first.Ped.Task.ClearAllImmediately(); second.Ped.Task.ClearAllImmediately();
                first.Ped.Task.StandStill(-1); second.Ped.Task.StandStill(-1);
                if (first.Ped.Health != first.Health || second.Ped.Health != second.Health)
                    throw Error("a saúde dos atores mudou; interromper o ensaio e conferir os eventos do jogo");
                phase = 3; finished = true;
                Log("ENSAIO_COREOGRAFIA_CONCLUIDA", "Natives executadas; sem alteração de saúde; sincronia/contato continuam sem confirmação visual");
            }
            return;
        }
        plane = resources.GetVehicle(PlaneCode);
        if (phase == 0)
        {
            if (elapsedMs >= 1000 || !InPlane(first.Ped)) throw Error("o ensaio não começou com o ator no avião próprio");
            plane.IsPositionFrozen = false;
            plane.Velocity = direction * 6f;
            phase = 1; Log("ENSAIO_AVIAO_MOVIMENTO", "Velocidade física inicial6m/s; voo completo da cena não comprovado");
        }
        if (phase == 1 && elapsedMs >= 1000)
        {
            first.Ped.Task.LeaveVehicle(plane, GTA.LeaveVehicleFlags.BailOut | GTA.LeaveVehicleFlags.DontWaitForVehicleToStop |
                GTA.LeaveVehicleFlags.DontDefaultWarpIfDoorBlocked);
            phase = 2;
            Log("ENSAIO_SAIDA_AVIAO", "Saída física solicitada; sem teleporte durante a queda");
        }
        if (phase == 2)
        {
            if (!first.Ped.IsInVehicle())
            {
                first.Ped.Ragdoll(3500, GTA.RagdollType.Relax);
                phase = 3;
            }
            else if (elapsedMs >= 4000) throw Error("o ator não saiu fisicamente do avião em três segundos");
        }
        if (phase == 3)
        {
            float height;
            if (!TryWater(first.Ped.Position, out height)) throw Error("a trajetória saiu da água real; ensaio interrompido");
            if (first.Ped.IsInWater)
            {
                phase = 4; finished = true;
                Log("ENSAIO_AGUA_ALCANCADA", "Posição real=" + first.Ped.Position + "; ator vivo/invencível; emersão/rampa/fuga e imagem ainda não confirmadas");
            }
            else if (elapsedMs >= 10000) throw Error("a queda não alcançou água em dez segundos; interromper o ensaio");
        }
    }

    public void Cleanup()
    {
        if (disposed) return;
        resetWaitingSeat = false; seatTimer = null; resetDone = false;
        if (!System.Object.ReferenceEquals(plane, null) && plane.Exists())
        {
            plane = resources.GetVehicle(PlaneCode);
            if (!RemoveOwnPassenger())
                throw Error("a saída do avião está pendente; conserve tarefa, atores e recursos para repetir a limpeza");
        }
        Restore(first); Restore(second);
        resources.Cleanup();
        if (dictionaryRequested) { GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, FightDictionary); dictionaryRequested = false; }
        prepared = false; ready = false; finished = false; planeRequested = false; waterProven = false;
        passengerLeaving = false; exitTimer = null;
        plane = null; first = null; second = null; mode = ""; phase = 0; previousElapsed = -1;
        Log("ENSAIO_LIMPO", "Avião próprio removido; flags e marcas dos atores restauradas; nenhum ator apagado");
    }
    public void Dispose() { Cleanup(); resources.Dispose(); disposed = true; }

    private void StartPreparation(string kind)
    {
        Active();
        if (prepared || resources.Count != 0) throw Error("limpe o ensaio anterior antes de preparar outro");
        prepared = true; ready = false; finished = false; mode = kind;
        resetWaitingSeat = false; passengerLeaving = false; resetDone = false; exitTimer = null; seatTimer = null;
        phase = 0; previousElapsed = -1; loadingTimer = System.Diagnostics.Stopwatch.StartNew();
    }
    private static ActorState Capture(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead || ped.IsInVehicle() || ped.IsPlayer)
            throw Error("forneça somente um ator próprio vivo, fora de veículo e diferente do jogador");
        return new ActorState { Ped = ped, Handle = ped.Handle, Address = ped.MemoryAddress,
            Position = ped.Position, Heading = ped.Heading, Health = ped.Health, Invincible = ped.IsInvincible,
            Ragdoll = ped.CanRagdoll, Frozen = ped.IsPositionFrozen, Events = ped.BlockPermanentEvents,
            Drowning = ped.GetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater) };
    }
    private static bool StillOwn(ActorState actor)
    { return actor != null && actor.Handle != 0 && !System.Object.ReferenceEquals(actor.Ped, null) && actor.Ped.Exists() && actor.Ped.Handle == actor.Handle && actor.Ped.MemoryAddress == actor.Address && !actor.Ped.IsPlayer; }
    private void RequireActors()
    { if (!StillOwn(first) || first.Ped.IsDead || (second != null && (!StillOwn(second) || second.Ped.IsDead))) throw Error("a identidade/vida dos atores mudou"); }
    private static void RefreshMark(ActorState actor)
    { actor.Position = actor.Ped.Position; actor.Heading = actor.Ped.Heading; actor.Health = actor.Ped.Health; }
    private static void PrepareActor(ActorState actor)
    {
        actor.Ped.IsInvincible = true; actor.Ped.BlockPermanentEvents = true;
        actor.Ped.IsPositionFrozen = true; actor.Ped.Velocity = GTA.Math.Vector3.Zero;
        actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading;
        actor.Ped.Task.StandStill(-1);
    }
    private static void Restore(ActorState actor)
    {
        if (actor == null || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, actor.Handle)) return;
        if (!StillOwn(actor)) throw Error("o ator mudou de identidade; não restaurar entidade alheia");
        actor.Ped.Task.ClearAllImmediately(); actor.Ped.Task.ClearLookAt(); actor.Ped.Velocity = GTA.Math.Vector3.Zero;
        actor.Ped.PositionNoOffset = actor.Position; actor.Ped.Heading = actor.Heading;
        actor.Ped.IsInvincible = actor.Invincible; actor.Ped.CanRagdoll = actor.Ragdoll;
        actor.Ped.BlockPermanentEvents = actor.Events; actor.Ped.SetConfigFlag(GTA.PedConfigFlagToggles.DrownsInWater, actor.Drowning);
        actor.Ped.IsPositionFrozen = actor.Frozen;
    }
    private void RequireOwnOccupants()
    {
        foreach (GTA.Ped occupant in plane.Occupants)
            if (!StillOwn(first) || occupant.Handle != first.Handle || occupant.MemoryAddress != first.Address)
                throw Error("há ocupante alheio no avião; o recurso será preservado");
    }
    private bool RemoveOwnPassenger()
    {
        if (System.Object.ReferenceEquals(plane, null) || !plane.Exists())
        { passengerLeaving = false; exitTimer = null; return true; }
        plane = resources.GetVehicle(PlaneCode);
        RequireOwnOccupants();
        if (StillOwn(first) && first.Ped.IsInVehicle() && !InPlane(first.Ped))
            throw Error("o ator entrou em veículo alheio; não restaurar marcas nem remover entidades");
        if (StillOwn(first) && InPlane(first.Ped))
        {
            if (!passengerLeaving)
            {
                first.Ped.IsPositionFrozen = false;
                first.Ped.Task.ClearAllImmediately(); first.Ped.Task.WarpOutOfVehicle(plane);
                passengerLeaving = true; exitTimer = System.Diagnostics.Stopwatch.StartNew();
                return false;
            }
            if (exitTimer == null || exitTimer.ElapsedMilliseconds >= 5000)
                throw Error("a saída do avião não terminou em cinco segundos; preservar tarefa, ator e recurso");
            return false;
        }
        if (plane.Occupants.Length != 0)
        {
            if (exitTimer == null) exitTimer = System.Diagnostics.Stopwatch.StartNew();
            if (exitTimer.ElapsedMilliseconds >= 5000) throw Error("a ocupação do avião não terminou em cinco segundos; não remover");
            return false;
        }
        passengerLeaving = false; exitTimer = null;
        return true;
    }
    private bool InPlane(GTA.Ped ped)
    { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_IN_VEHICLE, ped.Handle, plane.Handle, false); }
    private void ValidateWaterCorridor()
    {
        var right = new GTA.Math.Vector3(direction.Y, -direction.X, 0f);
        for (int i = 0; i <= 12; i++)
        {
            GTA.Math.Vector3 point = waterMark + direction * (i * 2f);
            float height;
            var diagnostic = new CenaQuedaDiretor.WaterQueryDiagnostic();
            if (!TryWater(point, out height, diagnostic) || System.Math.Abs(height - waterHeight) > 1.5f)
            {
                LogWaterCorridorFailure(i, "centro", 0f, point, height, waterHeight, diagnostic);
                throw Error("não há água contínua no trajeto real de24m; direção/marca não servem para o ensaio de queda");
            }
            if (i % 6 != 0) continue;
            foreach (float side in new float[] { -2f, 2f })
            {
                GTA.Math.Vector3 sidePoint = point + right * side;
                diagnostic = new CenaQuedaDiretor.WaterQueryDiagnostic();
                if (!TryWater(sidePoint, out height, diagnostic))
                {
                    LogWaterCorridorFailure(i, "lateral", side, sidePoint, height, waterHeight, diagnostic);
                    throw Error("a faixa de água é estreita para a trajetória de saída do avião");
                }
            }
        }
    }
    private void LogWaterCorridorFailure(int sample, string section, float side, GTA.Math.Vector3 point,
        float height, float referenceHeight, CenaQuedaDiretor.WaterQueryDiagnostic diagnostic)
    {
        try
        {
            Log("ENSAIO_QUEDA_AGUA_FAIL", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Amostra={0}; Secao={1}; Lado={2:R}; Ponto=({3:R},{4:R},{5:R}); Height={6:R}; RefHeight={7:R}; Diff={8:R}; {9}",
                sample, section, side, point.X, point.Y, point.Z, height, referenceHeight,
                System.Math.Abs(height - referenceHeight), diagnostic.ToLog()));
        }
        catch (System.Exception) { }
    }
    private static bool TryWater(GTA.Math.Vector3 point, out float height)
    {
        return CenaQuedaDiretor.TryWater(point, out height);
    }
    private static bool TryWater(GTA.Math.Vector3 point, out float height, CenaQuedaDiretor.WaterQueryDiagnostic diagnostic)
    {
        return CenaQuedaDiretor.TryWater(point, out height, diagnostic);
    }
    private static void ValidateGround(GTA.Math.Vector3 point)
    {
        float ground;
        if (!GTA.World.GetGroundHeight(point + new GTA.Math.Vector3(0f, 0f, 3f), out ground, GTA.GetGroundHeightMode.Normal) || System.Math.Abs(point.Z - ground) > 2f)
            throw Error("o chão real das marcas da coreografia não está disponível");
    }
    private static void ValidateClip(string clip)
    {
        float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, FightDictionary, clip);
        if (!Finite(duration) || duration <= 0f) throw Error("clipe real não disponível: " + FightDictionary + "/" + clip);
    }
    private static void Play(GTA.Ped actor, string clip)
    { GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, actor.Handle, FightDictionary, clip, 4f, -4f, 2000, 0, 0f, false, false, false); }
    private static float HorizontalLength(GTA.Math.Vector3 vector)
    { return (float)System.Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y); }
    private static float HorizontalDistance(GTA.Math.Vector3 first, GTA.Math.Vector3 second) { return HorizontalLength(first - second); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void RequireVector(GTA.Math.Vector3 value)
    { if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("marcas/direções precisam conter números finitos"); }
    private void RequireReady() { if (!IsReady) throw Error("espere os recursos do ensaio carregarem"); }
    private void Active() { if (disposed) throw Error("o helper do ensaio foi encerrado"); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Ensaio técnico: " + message + "."); }
    private void Log(string code, string message) { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
}

// Technical-only scenario observation. Appended to the existing non-Script
// EnsaioAcaoDiretor source; no new bundler entry and no production resource.
public sealed class EscritaCenario85Diretor : System.IDisposable
{
    private readonly string Scenario;
    private readonly GTA.Ped ped;
    private readonly int handle, model;
    private readonly System.IntPtr address;
    private readonly GTA.Math.Vector3 origin;
    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.HashSet<int> before = new System.Collections.Generic.HashSet<int>();
    private bool started, observed, finished, stopRequested, disposed;
    private int lastElapsed = -1, firstObserved = -1, nextLog, observations;
    private readonly EscritaFullTracks85Diretor fullTracks;
    public bool IsFinished { get { return fullTracks == null ? finished : fullTracks.IsFinished; } }
    public bool IsReady { get { return fullTracks == null || fullTracks.IsReady; } }
    public bool IsWeedFullTrack { get { return Scenario == "FULLTRACK_WEED"; } }
    public void PollLoading() { if (fullTracks != null) fullTracks.PollLoading(); }
    public bool PrepareForTakeReset()
    {
        if (IsWeedFullTrack)
        { RequirePreparedWeedPose(); return true; }
        return ReleaseActorsForReset();
    }
    public void RequirePreparedWeedPose()
    {
        if (!IsWeedFullTrack || fullTracks == null) throw Error("only the owned WEED85 preparation can be preserved");
        fullTracks.RequirePreparedWeedPose();
    }

    public EscritaCenario85Diretor(GTA.Ped ownPed, string scenario, System.Action<string, string> logger)
    {
        if (scenario != "CODE_HUMAN_MEDIC_TIME_OF_DEATH" && scenario != "WORLD_HUMAN_CLIPBOARD" && scenario != "FULLTRACK_NOTE_TAKING_01" && scenario != "FULLTRACK_NOTE_TAKING_02" && scenario != "FULLTRACK_WEED")
            throw Error("technical85 selector must be a documented scenario or the exact private fulltracks trial");
        Scenario = scenario;
        if (System.Object.ReferenceEquals(ownPed, null) || !ownPed.Exists()) throw Error("own Djanira missing");
        ped = ownPed; handle = ped.Handle; model = ped.Model.Hash; address = ped.MemoryAddress; origin = ped.Position; log = logger;
        RequireActor();
        if (Scenario == "FULLTRACK_NOTE_TAKING_01" || Scenario == "FULLTRACK_NOTE_TAKING_02" || Scenario == "FULLTRACK_WEED")
        { fullTracks = new EscritaFullTracks85Diretor(ped, logger, Scenario == "FULLTRACK_NOTE_TAKING_02", Scenario == "FULLTRACK_WEED"); return; }
        foreach (GTA.Prop prop in GTA.World.GetNearbyProps(origin, 2f))
            if (prop != null && prop.Exists()) before.Add(prop.Handle);
        Log("ESCRITA85_PREPARADA", "Scene=teste_fila_85; Scenario=" + Scenario + "; Handle=" + handle +
            "; FemaleNative=True; WindowMs=14000; ScenarioStarted=False; ManualProps=0; CaptureAllowed=False; ProductionApproval=False");
    }

    public void Start()
    {
        if (fullTracks != null) { fullTracks.Start(); return; }
        RequireActor();
        if (started) throw Error("reset before starting another trial");
        started = true; observed = finished = stopRequested = false; lastElapsed = -1; firstObserved = -1; nextLog = observations = 0;
        // API191: 0 is indefinite main clip; false skips intro. No coordinate or warp argument.
        ped.Task.StartScenarioInPlace(Scenario, 0, false);
        Log("ESCRITA85_SCENARIO_PEDIDO", "Handle=" + handle + "; Scenario=" + Scenario +
            "; TimeToLeaveMs=0; Intro=False; InPlace=True; WarpArgument=False; AvailabilityProven=False; ContactProven=False; ProductionApproval=False");
    }

    public void Update(int elapsed)
    {
        if (fullTracks != null) { fullTracks.Update(elapsed); return; }
        RequireActor();
        if (!started || elapsed < 0 || elapsed > 180000 || elapsed < lastElapsed) throw Error("trial clock/state invalid");
        lastElapsed = elapsed;
        if (finished) return;
        bool named = UsingNamedScenario();
        if (named)
        {
            if (!observed) firstObserved = elapsed;
            observed = true; observations++;
        }
        else if (observed || elapsed >= 2000)
            throw Error("named scenario not active; female availability not proven or task stopped");
        if (elapsed >= nextLog || elapsed >= 14000)
        {
            nextLog = elapsed + 500;
            Log("ESCRITA85_NATIVE_AMOSTRA", "ElapsedMs=" + elapsed + "; NamedScenario=" + named + "; FirstObservedMs=" + firstObserved +
                "; Observations=" + observations + "; Position=" + ped.Position + "; Velocity=" + ped.Velocity +
                "; Frozen=" + ped.IsPositionFrozen + "; ContactProven=False; ProductionApproval=False");
            ObserveScenarioProps();
        }
        if (elapsed >= 14000)
        {
            if (!observed || observations < 2) throw Error("scenario not actually observed during the trial");
            finished = true;
            Log("ESCRITA85_JANELA_CONCLUIDA", "WindowMs=14000; ActualMs=" + elapsed + "; FirstObservedMs=" + firstObserved +
                "; NamedScenarioObservedMs=" + (elapsed - firstObserved) + "; FemaleTaskObserved=True; PencilContactProven=False; Capture=False; ProductionApproval=False");
            RequestStop();
        }
    }

    private void ObserveScenarioProps()
    {
        int inspected = 0, attached = 0;
        foreach (GTA.Prop prop in GTA.World.GetNearbyProps(ped.Position, 2f))
        {
            if (inspected++ >= 32) break;
            if (prop == null || !prop.Exists() || before.Contains(prop.Handle) ||
                GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, prop.Handle) != 3 || prop.MemoryAddress == System.IntPtr.Zero ||
                !prop.IsAttachedTo(ped)) continue;
            GTA.Math.Vector3 min, max; prop.Model.GetDimensions(out min, out max);
            if (!Finite(min) || !Finite(max) || min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z)
                throw Error("native scenario prop dimensions invalid");
            attached++;
            Log("ESCRITA85_PROP_NATIVE_OBSERVADO", "Handle=" + prop.Handle + "; Model=" + prop.Model.Hash + "; Position=" + prop.Position +
                "; Rotation=" + prop.Rotation + "; BoundsMin=" + min + "; BoundsMax=" + max +
                "; AttachedToOwnPed=True; AbsentBeforeScenario=True; ExistsType3AddressBeforeModel=True; ReadOnly=True; NoManualAttach=True; PencilTipIdentified=False; ContactProven=False");
        }
        Log("ESCRITA85_PROPS_RESUMO", "NewAttachedNativeProps=" + attached + "; MaximumPoolInspected=32; ManualProps=0; NoPropAdoption=True; NoDelete=True; WritingVisualApproved=False");
    }

    private bool UsingNamedScenario()
    {
        // citizenfx/natives/PED/IsPedUsingScenario.md: 0x1BF094736DD62C2E.
        return GTA.Native.Function.Call<bool>((GTA.Native.Hash)0x1BF094736DD62C2EUL, handle, Scenario);
    }

    private void RequestStop()
    {
        RequireIdentity();
        if (!started || stopRequested) return;
        stopRequested = true;
        ped.Task.ClearAll(); // Also cancels the own pending scenario; no ambient prop removal.
        Log("ESCRITA85_STOP_PEDIDO", "OwnPed=" + handle + "; NamedScenarioAfterRequest=" + UsingNamedScenario() + "; ClearAllRequested=True; PropDelete=False");
    }

    public bool ReleaseActorsForReset()
    {
        if (fullTracks != null) return fullTracks.ReleaseActorsForReset();
        if (disposed || !started) return true;
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, handle)) { started = false; return true; }
        RequireIdentity(); RequestStop();
        if (UsingNamedScenario()) return false;
        started = observed = finished = stopRequested = false; lastElapsed = -1;
        return true;
    }

    private void RequireActor()
    {
        RequireIdentity();
        if (ped.IsDead || ped.IsInVehicle() || ped.IsInAir || ped.IsInWater || ped.IsRagdoll || !Finite(ped.Position) || !Finite(ped.Velocity) ||
            (ped.Position - origin).Length() > 0.35f ||
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_MALE, handle) ||
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))
            throw Error("technical trial requires the same own live female on dry measured ground, Story only");
    }

    private void RequireIdentity()
    {
        if (disposed || !ped.Exists() || ped.Handle != handle || ped.Model.Hash != model || ped.MemoryAddress != address || address == System.IntPtr.Zero ||
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) != 1 ||
            (!System.Object.ReferenceEquals(GTA.Game.Player.Character, null) && GTA.Game.Player.Character.Handle == handle))
            throw Error("own actor identity changed; do not cancel any foreign task");
    }

    public void Dispose()
    {
        if (disposed) return;
        if (fullTracks != null) { fullTracks.Dispose(); disposed = true; return; }
        if (!ReleaseActorsForReset()) throw Error("own native scenario cleanup pending; keep own ped for another tick");
        disposed = true;
    }
    private static bool Finite(GTA.Math.Vector3 v)
    { return !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.X) && !float.IsInfinity(v.Y) && !float.IsInfinity(v.Z); }
    private static System.InvalidOperationException Error(string value) { return new System.InvalidOperationException("Escrita85: " + value); }
    private void Log(string name, string value) { if (log != null) { try { log(name, value); } catch (System.Exception) { } } }
}

// Technical85 only. Existing note-taking tracks or authored weed inspector tracks on the same own female.
// AABB endpoint/grip observations are not a mesh-tip or visual writing approval.
public sealed class EscritaFullTracks85Diretor : System.IDisposable
{
    private readonly string Dictionary, BodyClip;
    private readonly bool weedVariant;
    private sealed class Item
    {
        public string Name, Clip;
        public GTA.Model Model;
        public GTA.Prop Entity;
        public int Handle;
        public System.IntPtr Address;
        public GTA.Math.Vector3 Min, Max, InitialPosition, InitialRotation;
        public bool InitiallyLoaded;
        public float Duration, MaxPhase;
    }
    private readonly GTA.Ped ped;
    private readonly int handle, model, health;
    private readonly System.IntPtr address;
    private readonly GTA.Math.Vector3 root, rotation;
    private readonly System.Action<string, string> log;
    private readonly Item[] items;
    private readonly System.Diagnostics.Stopwatch loading = System.Diagnostics.Stopwatch.StartNew();
    private readonly bool dictionaryInitiallyLoaded;
    private readonly bool productionC6;
    private int c6BeforeContact, c6AfterContact, c6LiftSamples;
    private GTA.Math.Vector3 commonOrigin, preparedRoot, stableRoot;
    private GTA.FwSyncedScene scene;
    private bool ready, started, finished, stopping, wasFrozen, disposed, dimensionsMeasured, stabilityStarted, prepFrozen;
    private float bodyDuration, maxBodyPhase;
    private int lastElapsed = -1, nextLog, playingSamples, contactSamples, stableAt;
    private int ownedSceneHandle, sceneStartedAtGameTime;
    // Exclusive technical85 observation. Any endpoint violation remains a failure at14s.
    private int firstEndpointViolationElapsed = -1, endpointSamples, endpointSeparationSamples, endpointSeparationRuns;
    private int endpointClipSamples, endpointClipContactSamples;
    private float firstEndpointViolationGap, firstEndpointViolationPhase, endpointGapMin, endpointGapMax;
    private bool previousEndpointSeparated;
    // WEED85 only. Phase0 foot calibration before Ready; never a hand/prop reanchor during action.
    private int weedPrepStage, weedPrepAt, weedStableFrames;
    private float weedRestLeftGap, weedRestRightGap;
    private GTA.Math.Vector3 weedLastLeft, weedLastRight, weedCommonShift, weedSyncedRoot;
    private bool weedFootProof, weedActionStarted;
    public bool IsReady { get { return ready; } }
    public bool IsFinished { get { return finished; } }

    public EscritaFullTracks85Diretor(GTA.Ped ownPed, System.Action<string, string> logger, bool secondVariant, bool weedInspectorVariant = false, string productionScene = null)
    {
        productionC6 = productionScene == "EP10_C06";
        if (productionScene != null && (!productionC6 || !weedInspectorVariant || secondVariant))
            throw Error("production optin is only C6 exact WEED three-track variant");
        if (secondVariant && weedInspectorVariant) throw Error("only one authored three-track variant may be selected");
        weedVariant = weedInspectorVariant;
        // am_mp_biker_warehouse type103/func_7338: exact3 tracks; func_7385 case4 permits MP_F_Weed_01.
        // Native Djanira retarget, track duration and physical writing remain unproved.
        Dictionary = weedVariant ? "anim@amb@business@weed@weed_inspecting_high_dry@" : "anim@amb@machinery@weapon_test@";
        string prefix = secondVariant ? "note_taking_02_" : "note_taking_01_";
        BodyClip = weedVariant ? "weed_inspecting_high_writing_inspector" : prefix + "amy_skater_01";
        if (ownPed == null || !ownPed.Exists() || GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, ownPed.Handle) != 1 ||
            ownPed.MemoryAddress == System.IntPtr.Zero) throw Error("own female missing before construction");
        ped = ownPed; handle = ped.Handle; model = ped.Model.Hash; address = ped.MemoryAddress; health = ped.Health;
        root = ped.Position; rotation = new GTA.Math.Vector3(0f, 0f, ped.Heading); log = logger;
        RequireActor();
        dictionaryInitiallyLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, Dictionary);
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, Dictionary);
        items = weedVariant ? new Item[] {
            new Item { Name = "bkr_prop_fakeid_clipboard_01a", Clip = "weed_inspecting_high_writing_clipboard" },
            new Item { Name = "bkr_prop_fakeid_penclipboard", Clip = "weed_inspecting_high_writing_pen" }
        } : new Item[] {
            new Item { Name = "prop_notepad_02", Clip = prefix + "prop_notepad_02" },
            new Item { Name = "prop_pencil_01", Clip = prefix + "prop_pencil_01" }
        };
        foreach (Item item in items)
        {
            item.Model = new GTA.Model(item.Name);
            if (!item.Model.IsValid || !item.Model.IsInCdImage) throw Error("installed own model unavailable: " + item.Name);
            item.InitiallyLoaded = item.Model.IsLoaded; item.Model.Request();
        }
        if (weedVariant) Log("ESCRITA85_WEED_RECURSO", "Selector=FULLTRACK_WEED; AuthorSceneType=103; AuthorFemaleModelOption=MP_F_Weed_01;" +
            " PaperModel=" + items[0].Name + "; WritingToolModel=" + items[1].Name + "; AuthoredFemaleOption=True; NativeFemaleRetargetApproved=False;" +
            " HistoricalPencilTelemetryIsWritingToolBounds=True; ActualTipIdentified=False; AuthorLoopProven=False; TableSupportAssumed=False; NoProductionSourceChange=True");
        Log("ESCRITA85_FULLTRACK_PEDIDO", "Body=" + Dictionary + "/" + BodyClip +
            (weedVariant ? "; FemaleNative=True; AuthorFemaleOption=True; RetargetApproved=False; Tracks=3; ManualAttach=False; WeaponProps=0; Capture=False; ProductionApproval=False" :
            "; FemaleNative=True; MaleAuthored=True; RetargetApproved=False; Tracks=3; ManualAttach=False; WeaponProps=0; Capture=False; ProductionApproval=False"));
    }

    public void PollLoading()
    {
        RequireActor();
        if (ready) { RequireItems(); return; }
        if (loading.ElapsedMilliseconds > 5000) throw Error("own models/dictionary loading timeout; no Ready");
        if (weedPrepStage != 0) { PollWeedFootPreparation(); return; }
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, Dictionary)) return;
        foreach (Item item in items) if (!item.Model.IsLoaded) return;
        if (!dimensionsMeasured)
        {
        bodyDuration = Duration(BodyClip);
        foreach (Item item in items)
        {
            item.Duration = Duration(item.Clip); item.Model.GetDimensions(out item.Min, out item.Max);
            Check(item.Min); Check(item.Max);
            if (item.Max.X <= item.Min.X || item.Max.Y <= item.Min.Y || item.Max.Z <= item.Min.Z)
                throw Error("real model bounds invalid: " + item.Name);
        }
        Log("ESCRITA85_FULLTRACK_DURACOES", "BodySeconds=" + Number(bodyDuration) +
            (weedVariant ? "; ClipboardSeconds=" : "; NotepadSeconds=") + Number(items[0].Duration) +
            (weedVariant ? "; PenSeconds=" : "; PencilSeconds=") + Number(items[1].Duration) +
            "; WindowMs=14000; Rate=1; NoLoop=True; FullClipsRequired=True");
        foreach (Item item in items)
            if (System.Math.Abs(item.Duration - bodyDuration) > 0.05f || item.Duration * 1000f > 14000f)
                throw Error("all three real complete tracks must fit14s at rate1 and have matching duration; no clip cut or speed change");
        if (bodyDuration * 1000f > 14000f) throw Error("complete body clip exceeds14s; no Ready");
        if (productionC6)
        {
            if (System.Math.Abs(bodyDuration - 10.4f) > 0.01f) throw Error("C6 requires full10.4s body plus3.6s hold");
            foreach (Item item in items) if (System.Math.Abs(item.Duration - 10.4f) > 0.01f) throw Error("C6 prop duration10.4 changed");
        }
        dimensionsMeasured = true;
        }
        int now = GTA.Game.GameTime;
        if (!stabilityStarted || (ped.Position - stableRoot).Length() > 0.005f || ped.Velocity.Length() > 0.05f)
        { stabilityStarted = true; stableAt = now; stableRoot = ped.Position; return; }
        if (unchecked((int)(now - stableAt)) < 250) return;
        RequireFootFloor(GTA.Bone.SkelLeftFoot); RequireFootFloor(GTA.Bone.SkelRightFoot);
        preparedRoot = ped.Position;
        wasFrozen = ped.IsPositionFrozen; prepFrozen = true; ped.IsPositionFrozen = true;
        GTA.Math.Vector3 zeroRoot = InitialPosition(BodyClip, GTA.Math.Vector3.Zero); Check(zeroRoot);
        commonOrigin = preparedRoot - zeroRoot; Check(commonOrigin);
        if ((InitialPosition(BodyClip, commonOrigin) - preparedRoot).Length() > 0.01f)
            throw Error("one common authored origin does not reproduce the measured own actor root");
        foreach (Item item in items)
        {
            item.InitialPosition = InitialPosition(item.Clip, commonOrigin);
            item.InitialRotation = InitialRotation(item.Clip, commonOrigin);
            Check(item.InitialPosition); Check(item.InitialRotation);
            if ((item.InitialPosition - root).Length() > 3f) throw Error("authored prop offset exceeds3m; do not move props toward a hand");
            item.Entity = GTA.Prop.CreateNoOffset(item.Model, item.InitialPosition, item.InitialRotation, true);
            if (item.Entity == null || !item.Entity.Exists() || GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, item.Entity.Handle) != 3 ||
                item.Entity.MemoryAddress == System.IntPtr.Zero) throw Error("own authored prop creation failed");
            item.Handle = item.Entity.Handle; item.Address = item.Entity.MemoryAddress;
            item.Entity.IsPersistent = true; item.Entity.IsVisible = true; item.Entity.IsPositionFrozen = true;
            if (!Own(item)) throw Error("new prop identity changed");
            Log("ESCRITA85_FULLTRACK_PROP_PREP", "Name=" + item.Name + "; Handle=" + item.Handle + "; Address=" + item.Address +
                "; Position=" + item.Entity.Position + "; Rotation=" + item.Entity.Rotation + "; Min=" + item.Min + "; Max=" + item.Max +
                "; CommonOrigin=" + commonOrigin + "; BodyRootReproduced=True; GripProven=False; WritingContactProven=False; NoHandRebase=True; BeforeReady=True");
        }
        RequireItems();
        if (weedVariant) { BeginWeedFootPreparation(); return; }
        ready = true;
        Log("ESCRITA85_FULLTRACK_RECURSOS_PRONTOS", "Tracks=3; OwnProps=2; Capture=False; NativeGripPending=True; NativeFemaleRetargetPending=True; ProductionApproval=False");
    }

    public void Start()
    {
        if (weedVariant) { StartHeldWeedScene(); return; }
        RequireActor(); RequireItems();
        if (!ready || started || (ped.Position - preparedRoot).Length() > 0.01f || System.Math.Abs(Angle(ped.Heading - rotation.Z)) > 0.1f)
            throw Error("start requires original prepared root/heading and ready own resources; no live rebase");
        started = true; finished = stopping = false; lastElapsed = -1; nextLog = playingSamples = contactSamples = 0;
        maxBodyPhase = 0f; foreach (Item item in items) item.MaxPhase = 0f;
        firstEndpointViolationElapsed = -1; endpointSamples = endpointSeparationSamples = endpointSeparationRuns = 0;
        endpointClipSamples = endpointClipContactSamples = 0; firstEndpointViolationGap = firstEndpointViolationPhase = 0f;
        endpointGapMin = System.Single.MaxValue; endpointGapMax = 0f; previousEndpointSeparated = false;
        ped.IsPositionFrozen = false;
        scene = GTA.FwSyncedScene.Create(commonOrigin, rotation, GTA.EulerRotationOrder.YXZ);
        if (scene == null) throw Error("own common synchronized scene unavailable");
        scene.HoldsLastFrame = true; scene.IsLooped = false; scene.Rate = 1f;
        ownedSceneHandle = scene.Handle; sceneStartedAtGameTime = GTA.Game.GameTime;
        ped.Task.PlaySynchronizedScene(scene, new GTA.CrClipAsset(Dictionary, BodyClip),
            new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f),
            GTA.SyncedSceneFlags.UseKinematicPhysics | GTA.SyncedSceneFlags.AbortOnDeath | GTA.SyncedSceneFlags.OnAbortStopScene,
            (GTA.RagdollBlockingFlags)0, new GTA.AnimationBlendDelta(1.5f), (GTA.AnimationIKControlFlags)0);
        foreach (Item item in items)
        {
            item.Entity.IsPositionFrozen = false;
            if (!item.Entity.PlaySynchronizedAnim(scene, new GTA.CrClipAsset(Dictionary, item.Clip),
                new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f), (GTA.SyncedSceneFlags)0, new GTA.AnimationBlendDelta(1.5f)))
                throw Error("own prop synchronized track refused: " + item.Name);
            // API191 explicitly disables prop collision during synchronized anim. This is geometry observation, not a collider proof.
            item.Entity.IsCollisionEnabled = false;
        }
        Log("ESCRITA85_FULLTRACK_START", "Origin=" + commonOrigin + "; Rotation=" + rotation +
            "; SharedScene=" + scene.Handle + "; AllTracksOneOrigin=True; Rate=1; PerFrameReanchor=False; BodyCollisionSetter=False; Capture=False; WritingContactProven=False");
    }

    // A proved phase0 pose remains alive from preparation through Ready/countdown.
    // No new body task or actor/prop transform is submitted at action start.
    public void RequirePreparedWeedPose()
    {
        RequireActor(); RequireItems();
        if (!weedVariant || !ready || !started || stopping || finished || weedActionStarted ||
            !weedFootProof || weedPrepStage != 3 || scene == null || !scene.Exists() || scene.Handle != ownedSceneHandle ||
            !Finite(scene.Phase) || System.Math.Abs(scene.Phase) > 0.001f || !Finite(scene.Rate) || System.Math.Abs(scene.Rate) > 0.001f ||
            scene.IsLooped || !scene.HoldsLastFrame || (ped.Position - weedSyncedRoot).Length() > 0.01f ||
            System.Math.Abs(Angle(ped.Heading - rotation.Z)) > 0.1f || ped.Velocity.Length() > 0.05f ||
            !SyncedPlaying(handle, BodyClip) || ScriptPlaying(handle, BodyClip))
            throw Error("same grounded phase0 WEED85 preparation required; after action clean F10/remount F6, no standing reblend");
        foreach (Item item in items)
            if (!SyncedPlaying(item.Handle, item.Clip) || ScriptPlaying(item.Handle, item.Clip) ||
                (item.Entity.Position - item.InitialPosition).Length() > 0.025f)
                throw Error("same two phase0 authored prop tracks must remain at their proved initial transforms");
        RequireWeedActionFeet(-1); // Unchanged actual-ground -0.02..0.15; read-only.
    }
    public GTA.Math.Vector3[] GetWritingC6VisualPoints(GTA.Ped actor)
    {
        if (!productionC6 || !ready || actor == null || !actor.Exists() || actor.Handle != handle || actor.MemoryAddress != address)
            throw Error("C6 camera requires its exact own actor and writing resources");
        RequireActor(); RequireItems();
        var points = new System.Collections.Generic.List<GTA.Math.Vector3>();
        points.Add(ped.Bones[GTA.Bone.SkelHead].Position);
        points.Add(ped.Bones[GTA.Bone.PHLeftHand].Position); points.Add(ped.Bones[GTA.Bone.PHRightHand].Position);
        foreach (Item item in items) for (int x=0;x<2;x++) for (int y=0;y<2;y++) for (int z=0;z<2;z++)
            points.Add(item.Entity.GetOffsetPosition(new GTA.Math.Vector3(x==0?item.Min.X:item.Max.X,y==0?item.Min.Y:item.Max.Y,z==0?item.Min.Z:item.Max.Z)));
        foreach (GTA.Math.Vector3 point in points) Check(point);
        return points.ToArray();
    }
    private void StartHeldWeedScene()
    {
        RequirePreparedWeedPose();
        finished = stopping = false; lastElapsed = -1; nextLog = playingSamples = contactSamples = 0;
        maxBodyPhase = 0f; foreach (Item item in items) item.MaxPhase = 0f;
        firstEndpointViolationElapsed = -1; endpointSamples = endpointSeparationSamples = endpointSeparationRuns = 0;
        endpointClipSamples = endpointClipContactSamples = 0; firstEndpointViolationGap = firstEndpointViolationPhase = 0f;
        endpointGapMin = System.Single.MaxValue; endpointGapMax = 0f; previousEndpointSeparated = false;
        c6BeforeContact = c6AfterContact = c6LiftSamples = 0;
        weedActionStarted = true; weedPrepStage = 0;
        sceneStartedAtGameTime = GTA.Game.GameTime;
        scene.Rate = 1f; // Advance the SAME own scene once, without replaying/rebasing a participant.
        if (scene.Handle != ownedSceneHandle || !Finite(scene.Rate) || System.Math.Abs(scene.Rate - 1f) > 0.001f)
            throw Error("same owned WEED85 scene rate1 transition was not observed");
        Log("ESCRITA85_WEED_HELD_START", "SharedScene=" + ownedSceneHandle + "; ActualScenePhase=" + Number(scene.Phase) +
            "; ActualSceneRate=" + Number(scene.Rate) + "; ActualRoot=" + ped.Position + "; ProvedSyncedRoot=" + weedSyncedRoot +
            "; GroundedPreparationPreserved=True; SameBodyAndTwoProps=True; NewScene=False; BodyTaskReplay=False;" +
            " ActorTransformSetter=False; PropTransformSetter=False; WindowMs=14000; ContinuousWriting14Proven=False; Capture=False");
        Log("ESCRITA85_FULLTRACK_START", "Origin=" + commonOrigin + "; Rotation=" + rotation + "; SharedScene=" + ownedSceneHandle +
            "; AllTracksOneOrigin=True; SamePreparedScene=True; Rate=1; PerFrameReanchor=False; BodyCollisionSetter=False; Capture=False; WritingContactProven=False");
    }

    public void Update(int elapsed)
    {
        RequireActor(); RequireItems();
        if (!started || elapsed < 0 || elapsed < lastElapsed || elapsed > 180000) throw Error("trial state/clock invalid");
        lastElapsed = elapsed; if (finished) return;
        if (scene == null || !scene.Exists() || !Finite(scene.Rate) || System.Math.Abs(scene.Rate - 1f) > 0.001f)
            throw Error("own shared scene stopped or changed speed");
        if (scene.Handle != ownedSceneHandle) throw Error("own shared scene identity changed");
        int ownElapsed = unchecked((int)(GTA.Game.GameTime - sceneStartedAtGameTime));
        if (ownElapsed < 0 || ownElapsed > 15000) throw Error("own full-track game clock invalid");
        float phase = Phase(scene.Phase), gameTimePhase = System.Math.Min(1f, ownElapsed / (bodyDuration * 1000f));
        // Native170: all synchronized participants reported EntityCurrent/Total=0, flag1=false/2=true.
        // Use the one owned FwScene clock with each EXACT synchronized clip playing, as UpperCounting does.
        // Keep .03 against the independent elapsed/duration clock; never credit unsupported EntityTime to MaxPhase.
        if (System.Math.Abs(phase - gameTimePhase) > 0.03f)
        {
            LogPhaseMismatchBeforeThrow(elapsed, "OWN_SCENE_VS_GAME_TIME", phase, gameTimePhase, phase);
            throw Error("own shared scene/game clock differs by more than .03; no phase correction");
        }
        float bodyEntityTime = Phase(Current(handle, BodyClip));
        bool bodyPlaying = SyncedPlaying(handle, BodyClip), allPlaying = bodyPlaying;
        if (ScriptPlaying(handle, BodyClip)) throw Error("competing regular body animation is not the owned synchronized track");
        float body = bodyPlaying ? phase : maxBodyPhase;
        if (bodyPlaying) maxBodyPhase = System.Math.Max(maxBodyPhase, phase);
        foreach (Item item in items)
        {
            bool playing = SyncedPlaying(item.Handle, item.Clip); allPlaying &= playing;
            if (ScriptPlaying(item.Handle, item.Clip)) throw Error("competing regular prop animation: " + item.Name);
            if (playing) item.MaxPhase = System.Math.Max(item.MaxPhase, phase);
            if ((item.Entity.Position - root).Length() > 3f) throw Error("authored prop trajectory left the finite own volume");
        }
        if (allPlaying) playingSamples++;
        if (elapsed >= 800 && phase < 0.99f && !allPlaying) throw Error("all three real tracks must remain active, not just a valid scene id");
        GTA.Math.Vector3 left = Bone(GTA.Bone.PHLeftHand), right = Bone(GTA.Bone.PHRightHand);
        float paperGap = System.Math.Min(Gap(items[0], left), Gap(items[0], right));
        float pencilGap = System.Math.Min(Gap(items[1], right), Gap(items[1], Bone((GTA.Bone)58866)));
        GTA.Math.Vector3 a, b; PencilBoundsEnds(out a, out b);
        float endA = Gap(items[0], a), endB = Gap(items[0], b), endpointGap = System.Math.Min(endA, endB);
        if (elapsed >= 800)
        {
            if (weedVariant) RequireWeedActionFeet(elapsed);
            // Existing physical-trial .35m hand/bounds envelope. This never declares a grip or identifies the writing tip.
            if (paperGap > 0.35f || pencilGap > 0.35f) throw Error("authored female hand/prop bounds separated; do not anchor by frame");
            // Only the endpoint observation is deferred. Hand/identity/phase/volume guards above still abort immediately.
            if (productionC6) ObserveC6Endpoint(elapsed, ownElapsed, phase, endpointGap);
            else ObserveEndpointSample(elapsed, ownElapsed, phase, endpointGap, endA, endB, a, b);
            if (endpointGap <= 0.03f) contactSamples++;
        }
        if (elapsed >= nextLog || elapsed >= 14000)
        {
            nextLog = elapsed + 250;
            Log("ESCRITA85_FULLTRACK_NATIVE", "ElapsedMs=" + elapsed + "; OwnGameElapsedMs=" + ownElapsed +
                "; Scene=" + ownedSceneHandle + "; ScenePhase=" + Number(phase) + "; GameTimePhase=" + Number(gameTimePhase) +
                "; ClockDelta=" + Number(System.Math.Abs(phase - gameTimePhase)) + "; ClockGuard=0.03; Rate=" + Number(scene.Rate) +
                "; BodyOwnedScenePhase=" + Number(body) + "; BodyEntityTimeDiagnostic=" + Number(bodyEntityTime) +
                "; PaperEntityTimeDiagnostic=" + Number(Current(items[0].Handle, items[0].Clip)) +
                "; PencilEntityTimeDiagnostic=" + Number(Current(items[1].Handle, items[1].Clip)) +
                "; PhaseSource=OwnedFwSyncedSceneWithExactClipFlag2; EntityTimeCreditedToMax=False; IndependentEntityPhaseAvailable=False" +
                "; BodyMax=" + Number(maxBodyPhase) + "; PaperMax=" + Number(items[0].MaxPhase) + "; PencilMax=" + Number(items[1].MaxPhase) +
                "; AllPlaying=" + allPlaying + "; PlayingSamples=" + playingSamples + "; Root=" + ped.Position +
                "; LeftHand=" + left + "; RightHand=" + right + "; PaperPosition=" + items[0].Entity.Position + "; PaperRotation=" + items[0].Entity.Rotation +
                "; PencilPosition=" + items[1].Entity.Position + "; PencilRotation=" + items[1].Entity.Rotation +
                "; PaperHandBoundsGap=" + Number(paperGap) + "; PencilHandBoundsGap=" + Number(pencilGap) +
                "; PencilBoundsEndA=" + a + "; PencilBoundsEndB=" + b + "; EndAPaperBoundsGap=" + Number(endA) + "; EndBPaperBoundsGap=" + Number(endB) +
                "; ContactSamples=" + contactSamples + "; EndpointGap=" + Number(endpointGap) + "; EndpointGuard=0.03" +
                "; EndpointSamples=" + endpointSamples + "; EndpointSeparatedSamples=" + endpointSeparationSamples +
                "; EndpointSeparationRuns=" + endpointSeparationRuns + "; FirstEndpointViolationMs=" + firstEndpointViolationElapsed +
                "; EndpointViolationDeferredTechnicalOnly=True; EndpointVerdictNeverPassAfterViolation=True" +
                "; BoundsObservationOnly=True; PencilTipIdentified=False; MeshWritingContactProven=False; VisualApproval=False; Capture=False");
        }
        if (elapsed >= 14000)
        {
            Log("ESCRITA85_ENDPOINT_WINDOW_OBSERVED", "WindowMs=14000; ActualMs=" + elapsed + "; ScenePhase=" + Number(phase) +
                "; EndpointGuard=0.03; FirstViolationMs=" + firstEndpointViolationElapsed + "; FirstViolationGap=" + Number(firstEndpointViolationGap) +
                "; FirstViolationPhase=" + Number(firstEndpointViolationPhase) + "; MinGap=" + Number(endpointGapMin) + "; MaxGap=" + Number(endpointGapMax) +
                "; Samples=" + endpointSamples + "; ContactSamples=" + contactSamples + "; SeparatedSamples=" + endpointSeparationSamples +
                "; SeparationRuns=" + endpointSeparationRuns + "; ClipSamples=" + endpointClipSamples + "; ClipContactSamples=" + endpointClipContactSamples +
                "; EndpointViolated=" + (firstEndpointViolationElapsed >= 0) + "; EndpointsAreAABB=True; ActualTipIdentified=False; LiftMotionProven=False" +
                "; Finished=False; ProductionApproval=False; VisualApproval=False; Capture=False");
            if (playingSamples < 2 || contactSamples < 2 || phase < 0.99f || maxBodyPhase < 0.98f || ownElapsed < System.Math.Ceiling(bodyDuration * 1000.0))
                throw Error("complete body and measured bounds observations not proved in14s; no production promotion");
            foreach (Item item in items)
                if (item.MaxPhase < 0.98f || ownElapsed < System.Math.Ceiling(item.Duration * 1000.0))
                    throw Error("own prop full synchronized track/duration did not complete");
            if (productionC6 && (c6BeforeContact < 2 || c6AfterContact < 2 || c6LiftSamples < 1))
                throw Error("C6 needs writing contact before/after the observed lift and actual lift samples");
            if (firstEndpointViolationElapsed >= 0)
            {
                Log("ESCRITA85_ENDPOINT_DIAGNOSTIC_FAIL", "FirstViolationMs=" + firstEndpointViolationElapsed +
                    "; FirstViolationGap=" + Number(firstEndpointViolationGap) + "; EndpointGuard=0.03; WindowMs=14000; ActualMs=" + elapsed +
                    "; FullThreeTrackDurationAndPhaseMeasured=True; EndpointVerdict=FAIL; Finished=False; Capture=False; ProductionApproval=False");
                throw Error("neither measured pencil-bounds endpoint touches the notepad bounds; writing still unproven; first failure was recorded and deferred only for technical14s observation");
            }
            finished = true;
            Log("ESCRITA85_FULLTRACK_JANELA_CONCLUIDA", "WindowMs=14000; ActualMs=" + elapsed + "; NativeBodyDurationMs=" + Number(bodyDuration * 1000f) +
                "; CompleteThreeTracks=True; EndFrameHeldRemainderMs=" + Number(System.Math.Max(0f, 14000f - bodyDuration * 1000f)) +
                "; ContinuousWriting14Proven=False; PencilTipIdentified=False; MeshWritingContactProven=False; VisualApproval=False; Capture=False; ProductionApproval=False");
        }
    }

    public bool ReleaseActorsForReset()
    {
        if (disposed || !started) return true;
        RequireIdentity();
        if (!stopping) { stopping = true; ped.Task.ClearAll(); }
        if (Playing(handle, BodyClip)) return false;
        foreach (Item item in items)
        {
            if (!Own(item)) throw Error("own prop identity changed during cleanup; do not stop/delete a foreign prop");
            if (Playing(item.Handle, item.Clip))
            {
                item.Entity.StopSynchronizedAnim(new GTA.AnimationBlendDelta(8f), false);
                if (Playing(item.Handle, item.Clip)) return false;
            }
        }
        if (scene != null && scene.Exists()) { scene.HoldsLastFrame = false; scene.Rate = 1f; }
        scene = null; ped.IsPositionFrozen = wasFrozen;
        if (weedVariant && (weedPrepStage != 0 || weedFootProof))
        {
            // Cleanup only, after body/props stopped: restore the original own actor outside this no-capture technical action.
            ped.PositionNoOffset = preparedRoot; ped.Heading = rotation.Z; ped.Velocity = GTA.Math.Vector3.Zero;
            if ((ped.Position - preparedRoot).Length() > 0.01f || System.Math.Abs(Angle(ped.Heading - rotation.Z)) > 0.1f)
                throw Error("WEED85 own preparation root restoration not observed; no Ready");
            weedPrepStage = 0; weedActionStarted = weedFootProof = ready = false;
            Log("ESCRITA85_WEED_PREVIEW_STOP", "OutsideAction=True; OwnBodyStopped=True; OriginalRoot=" + preparedRoot + "; OriginalFreezeRestored=" + wasFrozen + "; OwnFootProof=" + weedFootProof + "; ForeignTasksTouched=0");
        }
        foreach (Item item in items)
        {
            item.Entity.IsPositionFrozen = true; item.Entity.PositionNoOffset = item.InitialPosition; item.Entity.Rotation = item.InitialRotation;
        }
        started = stopping = finished = false; lastElapsed = -1;
        Log("ESCRITA85_FULLTRACK_RESET", "OwnBodyStopped=True; OwnProps=2; OutsideAction=True; OriginalAuthoredPrepTransformsRestored=True; ForeignPropsTouched=0");
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        if (!ReleaseActorsForReset()) throw Error("own synchronized animation cleanup pending; retain own actor/props");
        if (prepFrozen) { RequireIdentity(); ped.IsPositionFrozen = wasFrozen; prepFrozen = false; }
        foreach (Item item in items)
        {
            if (item.Entity != null && item.Entity.Exists())
            {
                if (!Own(item)) throw Error("own prop identity changed; no foreign delete");
                item.Entity.Delete(); if (item.Entity.Exists()) throw Error("own prop deletion pending");
            }
            if (!item.InitiallyLoaded) item.Model.MarkAsNoLongerNeeded();
        }
        if (!dictionaryInitiallyLoaded) new GTA.CrClipDictionary(Dictionary).MarkAsNoLongerNeeded();
        ready = false; disposed = true;
    }

    private void RequireActor()
    {
        RequireIdentity(); Check(ped.Position); Check(ped.Velocity);
        if (ped.IsDead || ped.IsInjured || ped.Health != health || ped.IsInVehicle() || ped.IsInAir || ped.IsInWater || ped.IsRagdoll ||
            !ped.HasPhysics || !ped.IsCollisionEnabled || !WithinOwnRootEnvelope() ||
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_MALE, handle) || GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))
        {
            if (weedVariant) LogWeedActorGuardBeforeThrow();
            throw Error("same own healthy grounded physical female required in Story; no actor collision or root setter");
        }
    }
    // WEED85 only: failure evidence after the unchanged predicate, before the unchanged throw.
    // Segment distance is diagnostic; it never replaces either original 0.35m root sphere.
    private void LogWeedActorGuardBeforeThrow()
    {
        try
        {
            GTA.Math.Vector3 actual = ped.Position, velocity = ped.Velocity, segment = weedSyncedRoot - root;
            float lengthSquared = segment.X * segment.X + segment.Y * segment.Y + segment.Z * segment.Z;
            GTA.Math.Vector3 fromRoot = actual - root;
            float fraction = lengthSquared > 0f ? Clamp((fromRoot.X * segment.X + fromRoot.Y * segment.Y + fromRoot.Z * segment.Z) / lengthSquared, 0f, 1f) : 0f;
            GTA.Math.Vector3 closest = root + segment * fraction;
            int actualElapsed = started && weedPrepStage == 0 ? unchecked((int)(GTA.Game.GameTime - sceneStartedAtGameTime)) : -1;
            bool sceneExists = scene != null && scene.Exists();
            Log("ESCRITA85_WEED_ACTOR_GUARD_FAILURE", "Handle=" + handle + "; Frame=" + GTA.Game.FrameCount +
                "; OwnGameElapsedMs=" + actualElapsed + "; PreviousElapsedMs=" + lastElapsed + "; PrepStage=" + weedPrepStage +
                "; Ready=" + ready + "; Started=" + started + "; FootProof=" + weedFootProof + "; ActualRoot=" + actual +
                "; OriginalRoot=" + root + "; ProvedSyncedRoot=" + weedSyncedRoot + "; DistanceOriginal=" + Number((actual - root).Length()) +
                "; DistanceSynced=" + Number((actual - weedSyncedRoot).Length()) + "; RootSphereRadius=0.35; RootEnvelopeSecondRead=" + WithinOwnRootEnvelope() +
                "; MeasuredSegmentFraction=" + Number(fraction) + "; MeasuredSegmentDistanceDiagnostic=" + Number((actual - closest).Length()) +
                "; SegmentNeverAcceptedByThisPatch=True; Velocity=" + velocity + "; Frozen=" + ped.IsPositionFrozen +
                "; Dead=" + ped.IsDead + "; Injured=" + ped.IsInjured + "; ActualHealth=" + ped.Health + "; ExpectedHealth=" + health +
                "; InVehicle=" + ped.IsInVehicle() + "; InAir=" + ped.IsInAir + "; InWater=" + ped.IsInWater + "; Ragdoll=" + ped.IsRagdoll +
                "; HasPhysics=" + ped.HasPhysics + "; CollisionEnabled=" + ped.IsCollisionEnabled +
                "; Male=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_MALE, handle) +
                "; NetworkGame=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS) +
                "; SceneExists=" + sceneExists + "; OwnScene=" + ownedSceneHandle +
                "; ActualScenePhase=" + (sceneExists ? Number(scene.Phase) : "Unavailable") +
                "; ActualSceneRate=" + (sceneExists ? Number(scene.Rate) : "Unavailable") +
                "; BeforeOriginalThrow=True; GuardUnchanged=True; DiagnosticSecondRead=True; NoSetter=True; Capture=False");
            foreach (GTA.Bone bone in new GTA.Bone[] { GTA.Bone.SkelLeftFoot, GTA.Bone.SkelRightFoot })
            {
                GTA.Math.Vector3 foot; float floor;
                ReadWeedFoot(bone, out foot, out floor); float gap = foot.Z - floor;
                Log("ESCRITA85_WEED_ACTOR_GUARD_FOOT", "Bone=" + bone + "; Point=" + foot + "; ActualFloor=" + Number(floor) +
                    "; Gap=" + Number(gap) + "; WithinExistingFloorEnvelope=" + (gap >= -0.02f && gap <= 0.15f) +
                    "; FloorEnvelopeMin=-0.02; FloorEnvelopeMax=0.15; OwnGameElapsedMs=" + actualElapsed + "; ReadOnly=True; Capture=False");
            }
        }
        catch (System.Exception diagnosticError)
        {
            Log("ESCRITA85_WEED_ACTOR_GUARD_DIAG_FAILURE", "Error=" + diagnosticError +
                "; OriginalGuardStillThrows=True; NoSetter=True; Capture=False");
        }
    }
    private void RequireIdentity()
    {
        if (disposed || !ped.Exists() || ped.Handle != handle || ped.MemoryAddress != address || address == System.IntPtr.Zero ||
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) != 1 || ped.Model.Hash != model ||
            (GTA.Game.Player.Character != null && GTA.Game.Player.Character.Handle == handle)) throw Error("own actor identity changed");
    }
    private bool WithinOwnRootEnvelope()
    {
        GTA.Math.Vector3 actual = ped.Position;
        if (weedVariant && weedFootProof)
            return (actual - weedSyncedRoot).Length() <= 0.35f; // Same measured root; no second sphere or enlarged path.
        if (weedVariant && (weedPrepStage == 2 || weedPrepStage == 3))
            return (actual - root).Length() <= 0.35f || (actual - (root + weedCommonShift)).Length() <= 0.35f;
        return (actual - root).Length() <= 0.35f;
    }
    private void ReadWeedFoot(GTA.Bone bone, out GTA.Math.Vector3 foot, out float floor)
    {
        foot = Bone(bone);
        if (!GTA.World.GetGroundHeight(foot + new GTA.Math.Vector3(0f, 0f, 0.25f), out floor, GTA.GetGroundHeightMode.Normal) || !Finite(floor) ||
            (new GTA.Math.Vector3(foot.X, foot.Y, 0f) - new GTA.Math.Vector3(root.X, root.Y, 0f)).Length() > 1f)
            throw Error("WEED85 exact own pose foot floor unavailable or left the one-metre preparation footprint");
    }
    private void BeginWeedFootPreparation()
    {
        RequireActor(); RequireItems();
        if (!weedVariant || ready || started || weedPrepStage != 0) throw Error("WEED85 phase0 preparation state invalid");
        GTA.Math.Vector3 left, right; float leftFloor, rightFloor;
        ReadWeedFoot(GTA.Bone.SkelLeftFoot, out left, out leftFloor);
        ReadWeedFoot(GTA.Bone.SkelRightFoot, out right, out rightFloor);
        weedRestLeftGap = left.Z - leftFloor; weedRestRightGap = right.Z - rightFloor;
        if (weedRestLeftGap < -0.02f || weedRestLeftGap > 0.15f || weedRestRightGap < -0.02f || weedRestRightGap > 0.15f)
            throw Error("WEED85 original own feet need the unchanged measured floor envelope");
        started = true; stopping = false; lastElapsed = -1; weedPrepStage = 1; weedFootProof = false;
        ped.IsPositionFrozen = false;
        scene = GTA.FwSyncedScene.Create(commonOrigin, rotation, GTA.EulerRotationOrder.YXZ);
        if (scene == null) throw Error("WEED85 own phase0 scene creation failed");
        scene.HoldsLastFrame = true; scene.IsLooped = false; scene.Rate = 0f; scene.Phase = 0f;
        ownedSceneHandle = scene.Handle;
        ped.Task.PlaySynchronizedScene(scene, new GTA.CrClipAsset(Dictionary, BodyClip), new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f),
            GTA.SyncedSceneFlags.UseKinematicPhysics | GTA.SyncedSceneFlags.AbortOnDeath | GTA.SyncedSceneFlags.OnAbortStopScene,
            (GTA.RagdollBlockingFlags)0, new GTA.AnimationBlendDelta(1.5f), (GTA.AnimationIKControlFlags)0);
        foreach (Item item in items)
        {
            item.Entity.IsPositionFrozen = false;
            if (!item.Entity.PlaySynchronizedAnim(scene, new GTA.CrClipAsset(Dictionary, item.Clip), new GTA.AnimationBlendDelta(8f), new GTA.AnimationBlendDelta(8f),
                (GTA.SyncedSceneFlags)0, new GTA.AnimationBlendDelta(1.5f))) throw Error("WEED85 own phase0 prop track refused");
            item.Entity.IsCollisionEnabled = false; // Same documented synced-prop behaviour, preparation only.
        }
        weedPrepAt = GTA.Game.GameTime; weedStableFrames = 0;
        Log("ESCRITA85_WEED_FOOT_PREVIEW", "PhaseRequested=0; RateRequested=0; OwnScene=" + ownedSceneHandle + "; OriginalRoot=" + root +
            "; OriginalLeft=" + left + "; LeftFloor=" + Number(leftFloor) + "; LeftGap=" + Number(weedRestLeftGap) + "; OriginalRight=" + right +
            "; RightFloor=" + Number(rightFloor) + "; RightGap=" + Number(weedRestRightGap) + "; Capture=False; Ready=False; PreparationOnly=True");
    }
    private void PollWeedFootPreparation()
    {
        RequireActor(); RequireItems();
        if (!weedVariant || !started || ready || weedPrepStage < 1 || weedPrepStage > 3) throw Error("WEED85 preparation state changed");
        if (weedPrepStage == 3)
        {
            if (!weedFootProof || !WithinOwnRootEnvelope()) throw Error("WEED85 held preparation lost measured own feet/root proof");
            ready = true;
            RequirePreparedWeedPose();
            Log("ESCRITA85_FULLTRACK_RECURSOS_PRONTOS", "Tracks=3; OwnProps=2; OwnPhase0FeetOnMeasuredFloor=True; FootProofPreparationOnly=True;" +
                " SameSceneHeldThroughReady=True; Phase0=True; Rate0=True; NewStandingTask=False;" +
                " Capture=False; NativeGripPending=True; NativeFemaleRetargetPending=True; ProductionApproval=False");
            return;
        }
        if (scene == null || !scene.Exists() || scene.Handle != ownedSceneHandle || System.Math.Abs(scene.Phase) > 0.001f ||
            System.Math.Abs(scene.Rate) > 0.001f || scene.IsLooped)
            throw Error("WEED85 actual own scene must hold phase0/rate0 before measuring any correction");
        int elapsed = unchecked((int)(GTA.Game.GameTime - weedPrepAt));
        if (elapsed < 0 || elapsed > 2000) throw Error("WEED85 bounded phase0 preparation timeout; no Ready");
        if (!SyncedPlaying(handle, BodyClip) || ScriptPlaying(handle, BodyClip)) return;
        foreach (Item item in items) if (!SyncedPlaying(item.Handle, item.Clip) || ScriptPlaying(item.Handle, item.Clip)) return;
        if (elapsed < 800) return; // Native182 body blend still had original hands at541ms, correct pose at806ms.
        GTA.Math.Vector3 left, right; float leftFloor, rightFloor;
        ReadWeedFoot(GTA.Bone.SkelLeftFoot, out left, out leftFloor); ReadWeedFoot(GTA.Bone.SkelRightFoot, out right, out rightFloor);
        if (weedStableFrames == 0 || (left - weedLastLeft).Length() > 0.005f || (right - weedLastRight).Length() > 0.005f)
            weedStableFrames = 1;
        else weedStableFrames++;
        weedLastLeft = left; weedLastRight = right;
        if (weedStableFrames < 3) return;
        float leftCorrection = left.Z - leftFloor - weedRestLeftGap;
        float rightCorrection = right.Z - rightFloor - weedRestRightGap;
        if (weedPrepStage == 1)
        {
            if (!Finite(leftCorrection) || !Finite(rightCorrection) || System.Math.Abs(leftCorrection - rightCorrection) > 0.03f)
                throw Error("WEED85 two actual pose feet disagree; one common vertical origin cannot support both");
            float correction = -(leftCorrection + rightCorrection) * 0.5f;
            if (System.Math.Abs(correction) > 1.25f) throw Error("WEED85 measured common correction exceeds finite1.25m; no guessed origin");
            weedCommonShift = new GTA.Math.Vector3(0f, 0f, correction); commonOrigin += weedCommonShift;
            Check(commonOrigin); weedPrepStage = 2;
            scene.SetOrigin(commonOrigin, rotation, GTA.EulerRotationOrder.YXZ); // Exactly one common body+props correction in preparation.
            weedPrepAt = GTA.Game.GameTime; weedStableFrames = 0;
            Log("ESCRITA85_WEED_COMMON_FLOOR_CORRECTION", "OwnScene=" + ownedSceneHandle + "; ActualLeft=" + left + "; ActualRight=" + right +
                "; ActualLeftFloor=" + Number(leftFloor) + "; ActualRightFloor=" + Number(rightFloor) + "; LeftCorrection=" + Number(leftCorrection) +
                "; RightCorrection=" + Number(rightCorrection) + "; CommonDelta=" + weedCommonShift + "; CommonOrigin=" + commonOrigin +
                "; SetOriginCalls=1; BodyAndTwoPropsSameOrigin=True; HandRebase=False; PreparationOnly=True; Ready=False; Capture=False");
            return;
        }
        if (System.Math.Abs(leftCorrection) > 0.025f || System.Math.Abs(rightCorrection) > 0.025f)
            throw Error("WEED85 actual corrected feet do not reproduce both measured rest clearances; no second correction");
        RequireFootFloor(GTA.Bone.SkelLeftFoot); RequireFootFloor(GTA.Bone.SkelRightFoot);
        weedSyncedRoot = ped.Position;
        foreach (Item item in items)
        {
            GTA.Math.Vector3 expected = InitialPosition(item.Clip, commonOrigin); Check(expected);
            if ((item.Entity.Position - expected).Length() > 0.025f) throw Error("WEED85 own phase0 prop did not follow the corrected common origin");
            item.InitialPosition = item.Entity.Position; item.InitialRotation = item.Entity.Rotation;
        }
        weedFootProof = true; weedPrepStage = 3;
        Log("ESCRITA85_WEED_PHASE0_FOOT_PROOF", "ActualSyncedRoot=" + weedSyncedRoot + "; ActualLeft=" + left + "; ActualRight=" + right +
            "; LeftGap=" + Number(left.Z - leftFloor) + "; RightGap=" + Number(right.Z - rightFloor) + "; Phase=" + Number(scene.Phase) +
            "; Rate=" + Number(scene.Rate) + "; StableSamples=" + weedStableFrames + "; BodyAndPropsSameOrigin=True;" +
            " WritingContactProven=False; PreparationOnly=True; Ready=False; Capture=False");
    }
    private void RequireWeedActionFeet(int elapsed)
    {
        GTA.Math.Vector3 left, right; float leftFloor, rightFloor;
        ReadWeedFoot(GTA.Bone.SkelLeftFoot, out left, out leftFloor); ReadWeedFoot(GTA.Bone.SkelRightFoot, out right, out rightFloor);
        float leftGap = left.Z - leftFloor, rightGap = right.Z - rightFloor;
        if (leftGap < -0.02f || leftGap > 0.15f || rightGap < -0.02f || rightGap > 0.15f)
            throw Error("WEED85 actual action feet left unchanged measured floor envelope; no floating body approved");
        if (elapsed >= nextLog)
            Log("ESCRITA85_WEED_ACTION_FEET", "ElapsedMs=" + elapsed + "; Left=" + left + "; Right=" + right +
                "; LeftFloor=" + Number(leftFloor) + "; RightFloor=" + Number(rightFloor) + "; LeftGap=" + Number(leftGap) + "; RightGap=" + Number(rightGap) +
                "; FloorEnvelopeMin=-0.02; FloorEnvelopeMax=0.15; PreparationOnly=False; ReadOnly=True; Capture=False");
    }

    private bool Own(Item item)
    { return item.Entity != null && item.Entity.Exists() && item.Entity.Handle == item.Handle &&
        GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, item.Handle) == 3 && item.Entity.MemoryAddress == item.Address &&
        item.Address != System.IntPtr.Zero && item.Entity.Model.Hash == item.Model.Hash; }
    private void RequireItems()
    {
        foreach (Item item in items)
        {
            if (!Own(item) || !item.Entity.IsPersistent || !item.Entity.IsVisible || !item.Entity.HasDrawable || item.Entity.IsAttached())
                throw Error("own authored prop lost identity/visible drawable or became attached");
            Check(item.Entity.Position); Check(item.Entity.Rotation);
        }
    }
    // Read-only measured bounds samples, not a claim of a real pencil tip or visible lifting motion.
    private void ObserveC6Endpoint(int elapsed, int ownElapsed, float phase, float gap)
    {
        if (!productionC6 || !weedVariant || !Finite(phase) || !Finite(gap)) throw Error("C6 endpoint policy requires exact owned WEED variant");
        bool lift = phase >= 0.62f && phase <= 0.74f;
        float maximum = lift ? 0.11f : 0.03f;
        if (gap > maximum) throw Error("C6 endpoint exceeded measured writing/lift phase envelope; no reanchor/deferred production error");
        endpointSamples++; endpointGapMin=System.Math.Min(endpointGapMin,gap); endpointGapMax=System.Math.Max(endpointGapMax,gap);
        bool separated=gap>0.03f;
        if (ownElapsed<System.Math.Ceiling(bodyDuration*1000.0)) { endpointClipSamples++; if (!separated) endpointClipContactSamples++; }
        if (separated) { endpointSeparationSamples++; if (!previousEndpointSeparated) endpointSeparationRuns++; c6LiftSamples++; }
        else if (phase<0.62f) c6BeforeContact++;
        else if (phase>0.74f) c6AfterContact++;
        if (separated!=previousEndpointSeparated || elapsed>=14000)
            Log("ESCRITA_C6_PHASE_CONTACT", "ElapsedMs="+elapsed+"; ScenePhase="+Number(phase)+"; EndpointGap="+Number(gap)+
                "; PhaseLiftWindow=.62..74; WritingMaximum=.03; LiftMaximum=.11; RootLiftImageProof=2DE6E744E5A74F3CC45EB23F79A00A590C1A91E761BE6808BADA214743A04DA1;"+
                " HandGuard=.35; BeforeContacts="+c6BeforeContact+"; AfterContacts="+c6AfterContact+"; LiftSamples="+c6LiftSamples+
                "; AllThreeTracksSameScene=True; NoPerFrameReanchor=True; ContinuousWriting14Proven=False; ProductionVisualApproval=False");
        previousEndpointSeparated=separated;
    }
    private void ObserveEndpointSample(int elapsed, int ownElapsed, float phase, float gap, float endA, float endB,
        GTA.Math.Vector3 a, GTA.Math.Vector3 b)
    {
        endpointSamples++; endpointGapMin = System.Math.Min(endpointGapMin, gap); endpointGapMax = System.Math.Max(endpointGapMax, gap);
        bool separated = gap > 0.03f;
        if (ownElapsed < System.Math.Ceiling(bodyDuration * 1000.0))
        { endpointClipSamples++; if (!separated) endpointClipContactSamples++; }
        if (separated)
        {
            endpointSeparationSamples++; if (!previousEndpointSeparated) endpointSeparationRuns++;
            if (firstEndpointViolationElapsed < 0)
            {
                firstEndpointViolationElapsed = elapsed; firstEndpointViolationGap = gap; firstEndpointViolationPhase = phase;
                Log("ESCRITA85_ENDPOINT_FIRST_FAILURE", "ElapsedMs=" + elapsed + "; OwnGameElapsedMs=" + ownElapsed +
                    "; ScenePhase=" + Number(phase) + "; EndpointGap=" + Number(gap) + "; EndAGap=" + Number(endA) + "; EndBGap=" + Number(endB) +
                    "; EndpointA=" + a + "; EndpointB=" + b + "; EndpointGuard=0.03; ErrorDeferredUntil14000=True; Technical85Only=True" +
                    "; AABBEndpointsNotActualTip=True; MotionApproved=False; ProductionApproval=False; Capture=False");
            }
        }
        previousEndpointSeparated = separated;
    }
    private void PencilBoundsEnds(out GTA.Math.Vector3 a, out GTA.Math.Vector3 b)
    {
        Item pencil = items[1]; GTA.Math.Vector3 extent = pencil.Max - pencil.Min, center = (pencil.Max + pencil.Min) * 0.5f;
        a = b = center;
        if (extent.X >= extent.Y && extent.X >= extent.Z) { a.X = pencil.Min.X; b.X = pencil.Max.X; }
        else if (extent.Y >= extent.Z) { a.Y = pencil.Min.Y; b.Y = pencil.Max.Y; }
        else { a.Z = pencil.Min.Z; b.Z = pencil.Max.Z; }
        a = pencil.Entity.GetOffsetPosition(a); b = pencil.Entity.GetOffsetPosition(b); Check(a); Check(b);
    }
    private float Gap(Item item, GTA.Math.Vector3 world)
    {
        Check(world); GTA.Math.Vector3 local = item.Entity.GetPositionOffset(world); Check(local);
        GTA.Math.Vector3 nearest = new GTA.Math.Vector3(Clamp(local.X, item.Min.X, item.Max.X), Clamp(local.Y, item.Min.Y, item.Max.Y), Clamp(local.Z, item.Min.Z, item.Max.Z));
        GTA.Math.Vector3 actual = item.Entity.GetOffsetPosition(nearest); Check(actual);
        float result = (world - actual).Length(); if (!Finite(result)) throw Error("nonfinite transformed bounds gap"); return result;
    }
    private GTA.Math.Vector3 Bone(GTA.Bone bone)
    { GTA.PedBone value = ped.Bones[bone]; if (value == null || !value.IsValid) throw Error("own hand bone unavailable"); Check(value.Position); return value.Position; }
    private void RequireFootFloor(GTA.Bone bone)
    {
        GTA.Math.Vector3 foot = Bone(bone); float floor;
        if (!GTA.World.GetGroundHeight(foot + new GTA.Math.Vector3(0f, 0f, 0.25f), out floor, GTA.GetGroundHeightMode.Normal) ||
            !Finite(floor) || foot.Z - floor < -0.02f || foot.Z - floor > 0.15f)
            throw Error("female foot not on actual nearby floor before own freeze/origin derivation");
        Log("ESCRITA85_FULLTRACK_FOOT_PREP", "Bone=" + bone + "; Point=" + foot + "; ActualFloor=" + Number(floor) +
            "; Gap=" + Number(foot.Z - floor) + "; StableMs=250; PreparationOnly=True; BeforeActorFreeze=True");
    }
    // Read-only failure evidence; never substitutes a scene phase, retimes or moves a track.
    private void LogPhaseMismatchBeforeThrow(int elapsed, string failingTrack, float scenePhaseCompared, float actualPhaseCompared, float bodyPhaseCompared)
    {
        try
        {
        Log("ESCRITA85_PHASE_MISMATCH_PRE_GUARD", "ElapsedMs=" + elapsed + "; Frame=" + GTA.Game.FrameCount +
            "; Scene=" + scene.Handle + "; FailingTrack=" + failingTrack +
            "; ScenePhaseCompared=" + Number(scenePhaseCompared) + "; TrackPhaseCompared=" + Number(actualPhaseCompared) +
            "; BodyPhaseCompared=" + Number(bodyPhaseCompared) + "; AbsoluteDeltaCompared=" + Number(System.Math.Abs(actualPhaseCompared - scenePhaseCompared)) +
            "; ScenePhaseSecondRead=" + Number(scene.Phase) + "; SceneRate=" + Number(scene.Rate) +
            "; PreparedClipSeconds=" + Number(bodyDuration) + "; ThresholdNormalized=0.03; GuardUnchanged=True; BeforeThrow=True; ReadOnly=True");
        Log("ESCRITA85_PHASE_BODY_FAILURE", "ElapsedMs=" + elapsed + "; Handle=" + handle +
            "; RawCurrent=" + Number(Current(handle, BodyClip)) +
            "; EntityTotalTimeRaw=" + Number(GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_TOTAL_TIME, handle, Dictionary, BodyClip)) +
            "; PlayingFlag1=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, handle, Dictionary, BodyClip, 1) +
            "; PlayingFlag2=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, handle, Dictionary, BodyClip, 2) +
            "; PlayingFlag3=" + Playing(handle, BodyClip) + "; Skeleton=" + ped.HasSkeleton + "; AnimDirector=" + ped.HasAnimationDirector +
            "; Position=" + ped.Position + "; Rotation=" + ped.Rotation + "; Frozen=" + ped.IsPositionFrozen + "; Capture=False");
        foreach (Item item in items)
        {
            // RequireItems already proved both exact own Type3/address/model identities in this same Update.
            Log("ESCRITA85_PHASE_PROP_FAILURE", "ElapsedMs=" + elapsed + "; Name=" + item.Name + "; Handle=" + item.Handle +
                "; RawCurrent=" + Number(Current(item.Handle, item.Clip)) +
                "; EntityTotalTimeRaw=" + Number(GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_TOTAL_TIME, item.Handle, Dictionary, item.Clip)) +
                "; PlayingFlag1=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, item.Handle, Dictionary, item.Clip, 1) +
                "; PlayingFlag2=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, item.Handle, Dictionary, item.Clip, 2) +
                "; PlayingFlag3=" + Playing(item.Handle, item.Clip) + "; Skeleton=" + item.Entity.HasSkeleton + "; AnimDirector=" + item.Entity.HasAnimationDirector +
                "; Position=" + item.Entity.Position + "; Rotation=" + item.Entity.Rotation + "; Frozen=" + item.Entity.IsPositionFrozen +
                "; Collision=" + item.Entity.IsCollisionEnabled + "; PreparedClipSeconds=" + Number(item.Duration) + "; ReadOnly=True; Capture=False");
        }
        }
        catch (System.Exception diagnosticError)
        {
            Log("ESCRITA85_PHASE_DIAG_FALHOU", "ElapsedMs=" + elapsed + "; FailingTrack=" + failingTrack +
                "; Error=" + diagnosticError + "; OriginalPhaseGuardStillThrows=True; ReadOnly=True");
        }
    }
    private GTA.Math.Vector3 InitialPosition(string clip, GTA.Math.Vector3 origin)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_POSITION, Dictionary, clip, origin.X, origin.Y, origin.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }
    private GTA.Math.Vector3 InitialRotation(string clip, GTA.Math.Vector3 origin)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_ROTATION, Dictionary, clip, origin.X, origin.Y, origin.Z, rotation.X, rotation.Y, rotation.Z, 0f, 2); }
    private float Duration(string clip)
    { float value = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, Dictionary, clip); if (!Finite(value) || value <= 0f) throw Error("real clip absent: " + clip); return value; }
    private bool Playing(int entity, string clip)
    { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, entity, Dictionary, clip, 3); }
    private bool SyncedPlaying(int entity, string clip)
    { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, entity, Dictionary, clip, 2); }
    private bool ScriptPlaying(int entity, string clip)
    { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, entity, Dictionary, clip, 1); }
    private float Current(int entity, string clip)
    { return GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, entity, Dictionary, clip); }
    private static float Phase(float value) { if (!Finite(value) || value < 0f || value > 1.001f) throw Error("native phase invalid"); return value; }
    private static float Clamp(float value, float minimum, float maximum) { return System.Math.Max(minimum, System.Math.Min(maximum, value)); }
    private static float Angle(float value) { while (value > 180f) value -= 360f; while (value < -180f) value += 360f; return value; }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void Check(GTA.Math.Vector3 v) { if (!Finite(v.X) || !Finite(v.Y) || !Finite(v.Z)) throw Error("nonfinite measured geometry"); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static System.InvalidOperationException Error(string value) { return new System.InvalidOperationException("Escrita85FullTracks: " + value); }
    private void Log(string name, string value)
    {
        if (productionC6)
        {
            name=name.Replace("ESCRITA85","ESCRITA_C6");
            value=value.Replace("Capture=False","Capture=NormalProductionLifecycle")
                .Replace("EndpointViolationDeferredTechnicalOnly=True; EndpointVerdictNeverPassAfterViolation=True","EndpointPolicy=C6MeasuredWritingAndLift; InvalidProductionGapThrowsImmediately=True")
                .Replace("EndpointGuard=0.03","EndpointGuard=Writing.03/LiftPhase.62..74Max.11")
                .Replace("LiftMotionProven=False","PriorRootLiftImageProof=True; CurrentProductionVisualApproval=False");
            value+="; Scene=EP10_C06; ProductionVisualApproval=False; Technical85Unchanged=True";
        }
        if (log != null) { try { log(name, value); } catch (System.Exception) { } }
    }
}

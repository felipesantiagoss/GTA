// Integração pontual do EP01: desembarque, partida e coreografia. Não altera figurinos nem boca.
public sealed class EpisodioUmDiretor : System.IDisposable
{
    private const string WalkSet = "move_f@posh@", FightDictionary = "melee@unarmed@streamed_core", FarewellDictionary = "gestures@f@standing@casual";
    private readonly System.Action<string, string> log;
    private VeiculosSerieDiretor vehicles;
    private CenaQuedaDiretor fall;
    private GTA.Ped dinjer, colleague;
    private int dinjerHandle, colleagueHandle, dinjerModel, colleagueModel, phase, lastElapsed = -1;
    private System.IntPtr dinjerAddress, colleagueAddress;
    private GTA.Math.Vector3 walkDestination;
    private int nextTerminalWalkLogMs;
    private GTA.Math.Vector3 entranceStart, entranceEnd, entranceRight;
    private int entranceRay, entranceSample;
    private float walkHeading;
    private string sceneId;
    private bool requested, ready, resetDone, started, disposed, failed, resetWaitingVehicles;
    private System.Diagnostics.Stopwatch loading;
    private sealed class FightActor
    {
        internal GTA.Ped Ped;
        internal string Code, Clip;
        internal int Handle, Model, Health, ClipStartedMs;
        internal System.IntPtr Address;
        internal bool Invincible, Frozen, CanRagdoll, ClipObserved;
        internal float HighestPhase, InitialHeadZ;
    }
    private FightActor[] fightActors;
    private BarracoComicoC07Diretor nativeFightC07;
    private bool finalContactObserved, fightFallObserved;
    private int fightFallAt = -1;
    private static readonly string[] FightClips = { "shove", "heavy_punch_a", "hit_heavy_punch_a", "heavy_punch_b", "hit_heavy_punch_b",
        "plyr_takedown_front_uppercut", "victim_takedown_front_uppercut" };

    public EpisodioUmDiretor(System.Action<string, string> log) { this.log = log; }
    public static bool Supports(string id) { return id == "EP01_C01" || id == "EP01_C04" || id == "EP01_C05" || id == "EP01_C06" || id == "EP01_C07" || id == "EP01_C09"; }
    public static int ExpectedDurationMs(string id)
    { return id == "EP01_C01" ? 20000 : id == "EP01_C04" ? 11000 : id == "EP01_C09" ? 7000 : id == "EP01_C05" ? 8000 : id == "EP01_C06" ? 9000 : id == "EP01_C07" ? 34000 : 0; }
    public bool IsReady { get { return ready && !failed && !disposed; } }
    public bool UsesOwnCamera { get { return fall != null; } }
    public bool IsFinished { get { return fall != null && fall.IsFinished; } }
    public bool IsSuccess { get { return fall != null && fall.IsSuccess; } }
    public int ObservedDurationMs { get { return fall == null ? -1 : fall.ObservedDurationMs; } }
    public GTA.Entity[] OwnedEntities { get { return fall != null ? fall.OwnedEntities : vehicles == null ? new GTA.Entity[0] : vehicles.OwnedEntities; } }
    public int[] OwnedHandles { get { return fall != null ? fall.OwnedHandles : vehicles == null ? new int[0] : vehicles.OwnedHandles; } }

    // Esta tomada termina na água; somente a marca CANAL_LS é necessária.
    public static CenaQuedaDiretor.QuedaSpec LoadQuedaSpec(LocaisDiretor locations, string path)
    {
        if (System.Object.ReferenceEquals(locations, null)) throw Error("catálogo de marcas ausente");
        LocaisDiretor.MarcaLocal water = locations.Load(path, "CANAL_LS");
        double radians = water.Heading * System.Math.PI / 180.0;
        return new CenaQuedaDiretor.QuedaSpec { Cena = "EP01_C01", Local = "CANAL_LS", Agua = water.Position,
            Direcao = new GTA.Math.Vector3((float)-System.Math.Sin(radians), (float)System.Math.Cos(radians), 0f),
            CompletarFuga = false, FilmarQuedaExterna = true, EnsaioPassivo = false, AlturaRelativa = 35f,
            VelocidadeAviao = 35f, DuracaoSolicitadaMs = 14000, TimeoutMs = 60000, QuedaAlvoMs = 4000 };
    }

    public void Prepare(string id, System.Collections.Generic.Dictionary<string, GTA.Ped> roster,
        GTA.Math.Vector3 anchor, GTA.Math.Vector3 right, GTA.Math.Vector3 forward, string localId = "", CenaQuedaDiretor.QuedaSpec quedaSpec = null)
    {
        Active();
        if (!Supports(id) || sceneId != null || roster == null) throw Error("cena/elenco inválido ou preparação já iniciada");
        var actors = new System.Collections.Generic.Dictionary<string, GTA.Ped>(System.StringComparer.Ordinal);
        foreach (var item in roster) actors.Add(item.Key.ToUpperInvariant(), item.Value);
        string primary = id == "EP01_C04" || id == "EP01_C01" ? "DJANIRA_POBRE" : id == "EP01_C05" ? "DONA_IRACI" : "DINJER";
        if (!actors.TryGetValue(primary, out dinjer)) throw Error(primary + " precisa estar no elenco final");
        RequirePed(dinjer); dinjerHandle = dinjer.Handle; dinjerAddress = dinjer.MemoryAddress; dinjerModel = dinjer.Model.Hash;
        sceneId = id; loading = System.Diagnostics.Stopwatch.StartNew();
        if (id == "EP01_C01")
        {
            if (localId != "CANAL_LS" || actors.Count != 1 || quedaSpec == null || quedaSpec.Cena != "EP01_C01" ||
                quedaSpec.CompletarFuga || quedaSpec.EnsaioPassivo || !quedaSpec.FilmarQuedaExterna ||
                quedaSpec.DuracaoSolicitadaMs != 14000 || quedaSpec.TimeoutMs != 60000)
                throw Error("a queda filmada exige Djanira sozinha, CANAL_LS, piloto em voo e conclusão na água; voz14s/bruto20s");
            fall = new CenaQuedaDiretor(log); fall.Prepare(dinjer, quedaSpec);
            Log("EP01_QUEDA_ADAPTADA", "Avião continua voando; saída, queda, água e emersão; narração14s sem boca; bruto20s; sem rampa/fuga nesta tomada; teste visual pendente");
            return;
        }
        if (id == "EP01_C05")
        {
            if (localId != "HOSPITAL_SANDY" && localId != "CASSINO_FORA") throw Error("a subtomada de C05 precisa identificar hospital ou fachada do cassino");
            bool hospital = localId == "HOSPITAL_SANDY";
            entranceStart = anchor + forward * (hospital ? 2f : -2f);
            entranceEnd = anchor + forward * (hospital ? -2f : 2f);
            entranceRight = right; entranceSample = 0; entranceRay = 0;
        }
        else if (id == "EP01_C04")
        {
            if (!actors.TryGetValue("DONA_IRACI", out colleague)) throw Error("Dona Iraci precisa estar na clínica");
            RequirePed(colleague); colleagueHandle = colleague.Handle; colleagueAddress = colleague.MemoryAddress; colleagueModel = colleague.Model.Hash;
            // A marca de HOSPITAL_SANDY deve apontar pela passagem real para dentro.
            entranceStart = anchor + right * 0.3f - forward * 2f;
            entranceEnd = anchor + right * 0.3f + forward * 2f;
            entranceRight = right; entranceSample = 0; entranceRay = 0;
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ANIM_DICT_EXIST, FarewellDictionary)) throw Error("dicionário de despedida indisponível");
            // PedAnimList.txt instalada, linha60163: gestures@f@standing@casual gesture_bye_soft.
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, FarewellDictionary); requested = true;
        }
        else if (id == "EP01_C07")
        {
            string[] codes = { "DINJER", "COLEGA_1", "COLEGA_2", "COLEGA_3" };
            fightActors = new FightActor[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                GTA.Ped ped;
                if (!actors.TryGetValue(codes[i], out ped)) throw Error(codes[i] + " precisa participar do barraco");
                RequirePed(ped);
                for (int j = 0; j < i; j++) if (fightActors[j].Handle == ped.Handle) throw Error("a coreografia exige quatro atores distintos");
                fightActors[i] = new FightActor { Ped = ped, Code = codes[i], Handle = ped.Handle, Address = ped.MemoryAddress,
                    Model = ped.Model.Hash, Health = ped.Health, Invincible = ped.IsInvincible, Frozen = ped.IsPositionFrozen, CanRagdoll = ped.CanRagdoll };
            }
            colleague = fightActors[1].Ped; colleagueHandle = colleague.Handle; colleagueAddress = colleague.MemoryAddress; colleagueModel = colleague.Model.Hash;
            nativeFightC07 = new BarracoComicoC07Diretor(log);
            nativeFightC07.Prepare(id, localId, new GTA.Ped[] { fightActors[0].Ped, fightActors[1].Ped, fightActors[2].Ped, fightActors[3].Ped });
        }
        else
        {
            vehicles = new VeiculosSerieDiretor(log);
            var spec = new VeiculosSerieDiretor.VehicleSpec();
            if (id == "EP01_C06")
            {
                spec.Codigo = "EP01_ONIBUS"; spec.Tipo = "ONIBUS"; spec.Modelo = "bus";
                spec.PosicaoRelativa = new GTA.Math.Vector3(-2.8f, -3f, 0f); spec.HeadingRelativo = 0f;
                spec.ActorInitialSeats.Add("DINJER", 0);
                spec.Actions.Add(new VeiculosSerieDiretor.VehicleAction { Tipo = "sair", Ator = "DINJER", InicioMs = 0, DuracaoMs = 5000, Assento = 0 });
                walkDestination = anchor + forward * 2f; walkHeading = forward.ToHeading();
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_SET, WalkSet); requested = true;
            }
            else
            {
                spec.Codigo = "EP01_MOTO_SAIDA"; spec.Tipo = "MOTO"; spec.Modelo = "faggio";
                spec.TrajetoDireto = true;
                spec.PosicaoRelativa = new GTA.Math.Vector3(2f, -1f, 0f);
                spec.ActorInitialSeats.Add("DINJER", -1);
                // Teste 04:25 UTC: centro parado a 1,4659m do alvo; chegada medida distinta da parada nativa.
                var drive = new VeiculosSerieDiretor.VehicleAction { Tipo = "dirigir", Ator = "DINJER", InicioMs = 0,
                    DuracaoMs = 7000, Assento = -1, Velocidade = 3f, RaioChegada = 1.6f, RaioParada = 0.8f };
                drive.RotaRelativa.Add(new GTA.Math.Vector3(2f, 10f, 0f)); spec.Actions.Add(drive);
            }
            vehicles.Prepare(actors, new VeiculosSerieDiretor.VehicleSpec[] { spec }, anchor, right, forward);
        }
        Log("EP01_ATUACAO_PREPARO", "Cena=" + id + "; sem combate IA/teleporte na captura; teste_visual_pendente=true");
    }

    public void PollLoading()
    {
        Active(); RequireActors(); if (ready) return;
        if (fall != null) { fall.PollLoading(); ready = fall.IsReady; return; }
        if (loading.ElapsedMilliseconds >= 5000) throw Error("recursos, chão ou passagem não carregaram em cinco segundos");
        if (vehicles != null) vehicles.PollLoading();
        bool loaded = sceneId == "EP01_C04" ? GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, FarewellDictionary) :
            sceneId == "EP01_C07" ? nativeFightC07 != null && nativeFightC07.IsReady :
            sceneId != "EP01_C06" || GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_SET_LOADED, WalkSet);
        if ((vehicles == null || vehicles.IsReady) && loaded)
        {
            if (sceneId == "EP01_C04")
            {
                float duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, FarewellDictionary, "gesture_bye_soft");
                if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f) throw Error("clipe de despedida não existe neste GTA");
                if (!PollEntrance()) return;
            }
            if (sceneId == "EP01_C05" && !PollEntrance()) return;
            if (sceneId == "EP01_C07")
                Log("EP01_BARRACO_RECURSOS_COMICOS_PRONTOS", "Quatro atores proprios/34s; DINJER/COLEGA_1 e COLEGA_2/COLEGA_3 simultaneos22..32s; rescaldo32..34s; pares/clipes reais; imagem ainda pendente");
            if (sceneId == "EP01_C06")
            {
                float height;
                GTA.Math.Vector3 walkPoint = walkDestination;
                GTA.Math.Vector3 probe = walkPoint + new GTA.Math.Vector3(0f, 0f, 2f);
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, walkDestination.X, walkDestination.Y, walkDestination.Z);
                if (!GTA.World.GetGroundHeight(probe, out height, GTA.GetGroundHeightMode.Normal)) return;
                float deltaZ = System.Math.Abs(height - walkPoint.Z);
                string evidence = "WalkPoint=" + walkPoint + "; Probe=" + probe +
                    "; Ground=" + height.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    "; DeltaZ=" + deltaZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                if (deltaZ > 2f)
                {
                    Log("EP01_TERMINAL_SOLO_RECUSADO", evidence);
                    throw Error("o terminal precisa ter chão plano na rota a pé; " + evidence);
                }
                walkDestination.Z = height + 1f;
                Log("EP01_TERMINAL_SOLO_CONFERIDO", evidence + "; Destino=" + walkDestination);
            }
            ready = true; Log("EP01_ATUACAO_RECURSOS_PRONTOS", "Cena=" + sceneId + "; streaming/getters conferidos; imagem ainda pendente");
        }
    }

    // false exige novo Tick fora da contagem/captura; jamais seguir sem sair dos assentos próprios.
    public bool ReleaseActorsForReset()
    {
        Active(); RequireActors(); started = false; resetDone = false; resetWaitingVehicles = false;
        if (fall != null) return fall.ReleaseActorsForReset();
        if (vehicles != null && !vehicles.ReleaseActorsForReset()) return false;
        if (nativeFightC07 != null) nativeFightC07.Restore();
        if (fightActors != null) RestoreFightActors(true);
        dinjer.Task.ClearAllImmediately(); dinjer.Velocity = GTA.Math.Vector3.Zero;
        if (sceneId == "EP01_C06") dinjer.ResetMovementClipSet();
        if (!System.Object.ReferenceEquals(colleague, null)) { colleague.Task.ClearAllImmediately(); colleague.Velocity = GTA.Math.Vector3.Zero; }
        return true;
    }

    public bool Reset()
    {
        Active(); if (!IsReady) throw Error("aguarde os recursos antes de reiniciar"); RequireActors();
        if (fall != null)
        {
            if (!fall.Reset()) return false;
            started = false; resetDone = true; lastElapsed = -1; phase = 0; return true;
        }
        // Na retomada de um assento adiado, preservar a preparação do backend.
        if (!resetWaitingVehicles && !ReleaseActorsForReset()) return false;
        resetWaitingVehicles = vehicles != null;
        if (vehicles != null && !vehicles.Reset()) return false;
        resetWaitingVehicles = false;
        if (fightActors != null)
        {
            foreach (FightActor state in fightActors)
            { state.Health = state.Ped.Health; state.Clip = null; state.ClipObserved = false; state.HighestPhase = 0f; }
            finalContactObserved = false; fightFallObserved = false; fightFallAt = -1;
            if (nativeFightC07 != null) nativeFightC07.Reset();
        }
        phase = 0; lastElapsed = -1; started = false; resetDone = true; nextTerminalWalkLogMs = 0;
        return true;
    }

    public void Update(int elapsedMs)
    {
        Active(); RequireActors();
        if (!IsReady || !resetDone || elapsedMs < 0 || elapsedMs < lastElapsed) throw Error("reinicie antes de executar a tomada");
        try
        {
            lastElapsed = elapsedMs;
            if (fall != null)
            {
                if (!started) { fall.Start(); started = true; }
                fall.Update(elapsedMs);
                if (elapsedMs >= 20000 && !fall.IsSuccess) throw Error("a queda/chegada na água não terminaram no bruto de20s; tomada interrompida sem teleporte");
                return;
            }
            if (nativeFightC07 == null) { dinjer.IsInvincible = true; if (!System.Object.ReferenceEquals(colleague, null)) colleague.IsInvincible = true; }
            if (vehicles != null)
            {
                if (!started) { vehicles.Start(); started = true; }
                vehicles.Update(elapsedMs);
            }
            if (sceneId == "EP01_C06") UpdateTerminal(elapsedMs);
            if (sceneId == "EP01_C07") nativeFightC07.Update(elapsedMs);
            if (sceneId == "EP01_C04") UpdateFarewell(elapsedMs);
            if (sceneId == "EP01_C05")
            {
                if (elapsedMs >= 8000 && Distance(dinjer.Position, entranceEnd) > 0.5f)
                    throw Error("a avó não terminou a travessia física da subtomada; tomada interrompida sem teleporte ou invisibilidade");
            }
        }
        catch { failed = true; throw; }
    }

    private void UpdateTerminal(int elapsed)
    {
        if (phase == 0 && vehicles.HasCompletedNormalExit(dinjer))
        {
            dinjer.Task.ClearAllImmediately(); dinjer.Velocity = GTA.Math.Vector3.Zero;
            GTA.Math.Vector3 walkDirection = walkDestination - dinjer.Position;
            walkHeading = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_HEADING_FROM_VECTOR_2D, walkDirection.X, walkDirection.Y);
            dinjer.IsPositionFrozen = false; dinjer.SetMovementClipSet(new GTA.ClipSet(WalkSet));
            dinjer.Task.GoStraightTo(walkDestination, -1, GTA.PedMoveBlendRatio.Walk, walkHeading, 0.20f);
            phase = 1; nextTerminalWalkLogMs = elapsed + 1000;
            Log("EP01_TERMINAL_CAMINHADA", "Passarela após sair do ônibus real; DesembarqueMs=" + elapsed + "; timeout=-1; sem teleporte; " + TerminalWalkState(elapsed));
        }
        if (phase == 1 && elapsed >= nextTerminalWalkLogMs)
        {
            nextTerminalWalkLogMs = elapsed + 1000;
            Log("EP01_TERMINAL_CAMINHADA_ESTADO", TerminalWalkState(elapsed));
        }
        if (phase == 1 && Distance(dinjer.Position, walkDestination) <= 0.4f && System.Math.Abs(dinjer.Position.Z - walkDestination.Z) <= 1f)
        {
            dinjer.Task.ClearAllImmediately(); dinjer.Velocity = GTA.Math.Vector3.Zero; dinjer.IsPositionFrozen = true;
            dinjer.ResetMovementClipSet(); dinjer.Task.StandStill(-1); phase = 2;
        }
        if (elapsed >= 9000 && phase != 2)
        {
            string state = TerminalWalkState(elapsed);
            Log("EP01_TERMINAL_CAMINHADA_TIMEOUT", state);
            throw Error("Dinjer não chegou ao terminal no tempo; tomada interrompida sem teleporte; " + state);
        }
    }

    private string TerminalWalkState(int elapsed)
    {
        GTA.Math.Vector3 position = dinjer.Position;
        GTA.ScriptTaskNameHash taskName; GTA.ScriptTaskStatus taskStatus;
        dinjer.GetCurrentScriptTaskNameHashAndStatus(out taskName, out taskStatus);
        return "TempoMs=" + elapsed + "; Fase=" + phase + "; AtorPos=" + position + "; Destino=" + walkDestination +
            "; DistanciaXY=" + Distance(position, walkDestination).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; DeltaZ=" + System.Math.Abs(position.Z - walkDestination.Z).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; AtorFrozen=" + dinjer.IsPositionFrozen + "; Velocidade=" + dinjer.Speed.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; ActualHeading=" + dinjer.Heading.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; RequestedHeading=" + walkHeading.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; CurrentScriptTaskNameHash=" + taskName + "(0x" + taskName.ToString("X") + "); CurrentScriptTaskStatus=" + taskStatus;
    }

    private bool PollEntrance()
    {
        if (entranceSample >= 3) return true;
        if (entranceRay == 0)
        {
            GTA.Math.Vector3 offset = entranceRight * ((entranceSample - 1) * 0.25f) + new GTA.Math.Vector3(0f, 0f, 0.1f);
            GTA.Math.Vector3 from = entranceStart + offset, to = entranceEnd + offset;
            // Map/objects somente. Nada desbloqueia ou altera porta/entidade existente.
            entranceRay = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE,
                from.X, from.Y, from.Z, to.X, to.Y, to.Z, 17, 0, 7);
            if (entranceRay == 0) throw Error("não foi possível conferir a passagem da clínica");
            return false;
        }
        // OutputArgument<bool> da API191 le oito bytes; o indicador nativo e escalar.
        // Zerar os buffers evita ler padding/saidas nao escritas quando nao ha colisao.
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
            int result = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, entranceRay, hit, point, normal, entity);
            if (result == 1) return false;
            entranceRay = 0;
            if (result != 2) throw Error("a medição da entrada não terminou corretamente");
            int hitValue = System.Runtime.InteropServices.Marshal.ReadInt32(hit);
            Log("EP01_ENTRADA_LOS_RESULTADO", "Cena=" + sceneId + "; Sample=" + entranceSample + "; Result=" + result + "; HitValue32=" + hitValue);
            if (hitValue != 0)
            {
                GTA.Math.Vector3 offset = entranceRight * ((entranceSample - 1) * 0.25f) + new GTA.Math.Vector3(0f, 0f, 0.1f);
                int handle = System.Runtime.InteropServices.Marshal.ReadInt32(entity);
                float[] coordinates = new float[6], normals = new float[6];
                System.Runtime.InteropServices.Marshal.Copy(point, coordinates, 0, 6);
                System.Runtime.InteropServices.Marshal.Copy(normal, normals, 0, 6);
                GTA.Math.Vector3 hitPoint = new GTA.Math.Vector3(coordinates[0], coordinates[2], coordinates[4]);
                GTA.Math.Vector3 surfaceNormal = new GTA.Math.Vector3(normals[0], normals[2], normals[4]);
                bool exists = handle != 0 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, handle);
                string model = exists ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, handle).ToString(System.Globalization.CultureInfo.InvariantCulture) : "indisponivel";
                Log("EP01_ENTRADA_LOS_BLOQUEADA", "Cena=" + sceneId + "; Sample=" + entranceSample + "; Right=" + entranceRight +
                    "; Offset=" + offset + "; From=" + (entranceStart + offset) + "; To=" + (entranceEnd + offset) +
                    "; HitPoint=" + hitPoint + "; Normal=" + surfaceNormal + "; HitValue32=" + hitValue +
                    "; EntityHandle=" + handle + "; EntityExists=" + exists + "; ModelHash=" + model +
                    "; PrimaryHandle=" + dinjer.Handle + "; PrimaryPosition=" + dinjer.Position +
                    "; ColleagueHandle=" + (System.Object.ReferenceEquals(colleague, null) ? 0 : colleague.Handle) +
                    "; ColleaguePosition=" + (System.Object.ReferenceEquals(colleague, null) ? "ausente" : colleague.Position.ToString()));
                throw Error("a passagem da clínica/cassino está fechada ou bloqueada; marque limiar real aberto apontando para dentro. Não será simulada entrada por invisibilidade");
            }
        }
        finally
        {
            if (hit != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(hit);
            if (point != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(point);
            if (normal != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(normal);
            if (entity != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(entity);
        }
        entranceSample++; return entranceSample >= 3;
    }

    private void UpdateFarewell(int elapsed)
    {
        if (phase == 0 && elapsed >= 4000)
        {
            if (elapsed >= 4500) throw Error("a despedida perdeu seu início; reinicie");
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, dinjer.Handle, FarewellDictionary, "gesture_bye_soft",
                4f, -4f, 2000, (int)(GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary), 0f, false, false, false);
            phase = 1; Log("EP01_HOSPITAL_DESPEDIDA", "Gesto de despedida real; avó entra pela passagem conferida; sem boca na narração");
        }
        if (elapsed >= 11000)
        {
            Log("EP01_HOSPITAL_CHEGADA_GETTERS", "Pos=" + colleague.Position + "; Destino=" + entranceEnd + "; Distancia3D=" + Distance(colleague.Position, entranceEnd).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; Speed=" + colleague.Speed.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (Distance(colleague.Position, entranceEnd) > 0.5f) throw Error("a avó não atravessou a entrada real; tomada interrompida sem ocultar ou teleportar");
            phase = 2;
        }
    }

    private void UpdateCoreography(int elapsed)
    {
        foreach (FightActor state in fightActors)
        {
            if (state.Ped.Health != state.Health) throw Error("saúde mudou durante coreografia: " + state.Code);
            state.Ped.IsInvincible = true;
            ObserveFightClip(state, elapsed);
        }
        if (phase == 0 && elapsed >= 22000)
        {
            if (elapsed >= 22500) throw Error("a coreografia perdeu seu início ao terminar a conversa");
            FaceFightPair(0, 1); FaceFightPair(2, 3);
            foreach (FightActor state in fightActors)
            {
                state.Ped.Task.ClearAllImmediately(); state.Ped.Task.ClearLookAt();
                state.Ped.IsPositionFrozen = true; state.Ped.CanRagdoll = false;
                state.InitialHeadZ = FightBonePosition(state, GTA.Bone.SkelHead).Z;
            }
            PlayFightRound(elapsed, "shove", "hit_heavy_punch_a", "heavy_punch_a", "hit_heavy_punch_a", 2000);
            phase = 1; Log("EP01_BARRACO_COREOGRAFIA", "InicioMs=" + elapsed + "; DINJER/COLEGA_1 e COLEGA_2/COLEGA_3; dez segundos; animações reais, sem combate IA");
        }
        if (phase == 1 && elapsed >= 24000)
        { RequireFightClipsObserved(); PlayFightRound(elapsed, "hit_heavy_punch_b", "heavy_punch_b", "hit_heavy_punch_b", "heavy_punch_b", 2000); phase = 2; }
        if (phase == 2 && elapsed >= 26000)
        { RequireFightClipsObserved(); PlayFightRound(elapsed, "heavy_punch_a", "hit_heavy_punch_a", "heavy_punch_b", "hit_heavy_punch_b", 2000); phase = 3; }
        if (phase == 3 && elapsed >= 28000)
        { RequireFightClipsObserved(); PlayFightRound(elapsed, "hit_heavy_punch_a", "heavy_punch_a", "hit_heavy_punch_a", "heavy_punch_a", 1000); phase = 4; }
        if (phase == 4 && elapsed >= 29000)
        {
            RequireFightClipsObserved(); FaceFightPair(0, 1);
            dinjer.IsPositionFrozen = false; dinjer.CanRagdoll = true;
            PlayFightRound(elapsed, "victim_takedown_front_uppercut", "plyr_takedown_front_uppercut", "hit_heavy_punch_b", "heavy_punch_b", 3000);
            phase = 5; Log("EP01_BARRACO_SOCO_FINAL", "TempoMs=" + elapsed + "; COLEGA_1→DINJER; uppercut atacante/vítima; queda depende de contato corporal medido");
        }
        if (phase == 5)
        {
            float distance = System.Math.Min((FightBonePosition(fightActors[1], GTA.Bone.SkelRightHand) - FightBonePosition(fightActors[0], GTA.Bone.SkelHead)).Length(),
                (FightBonePosition(fightActors[1], GTA.Bone.SkelLeftHand) - FightBonePosition(fightActors[0], GTA.Bone.SkelHead)).Length());
            if (fightActors[0].ClipObserved && fightActors[1].ClipObserved && fightActors[0].HighestPhase >= 0.05f && fightActors[1].HighestPhase >= 0.05f && distance <= 0.65f)
            {
                if (!finalContactObserved) Log("EP01_BARRACO_CONTATO_FINAL", "TempoMs=" + elapsed + "; DistanciaMaoCabeca=" + distance.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; aparência/impacto visual ainda pendentes");
                finalContactObserved = true;
            }
            if (elapsed >= 29500 && finalContactObserved && fightActors[0].HighestPhase >= 0.15f && fightActors[1].HighestPhase >= 0.15f)
            {
                dinjer.Ragdoll(3500, GTA.RagdollType.Relax); fightFallAt = elapsed; phase = 6;
                Log("EP01_BARRACO_QUEDA_PEDIDA", "TempoMs=" + elapsed + "; posição=" + dinjer.Position + "; sem dano, força externa ou teleporte");
            }
            else if (elapsed > 30500)
                throw Error("o soco final não teve contato mão/cabeça e fase reais; distância=" + distance.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        if (phase == 6 && elapsed > fightFallAt && dinjer.IsRagdoll && !fightFallObserved)
        {
            GTA.Math.Vector3 head = FightBonePosition(fightActors[0], GTA.Bone.SkelHead);
            GTA.Math.Vector3 pelvis = FightBonePosition(fightActors[0], GTA.Bone.SkelPelvis);
            float ground;
            bool found = GTA.World.GetGroundHeight(dinjer.Position + new GTA.Math.Vector3(0f, 0f, 0.5f), out ground, GTA.GetGroundHeightMode.Normal);
            if (found && !float.IsNaN(ground) && !float.IsInfinity(ground) && head.Z <= fightActors[0].InitialHeadZ - 0.5f &&
                pelvis.Z >= ground - 0.1f && pelvis.Z <= ground + 1f)
            {
                fightFallObserved = true;
                Log("EP01_BARRACO_QUEDA_CONFERIDA", "TempoMs=" + elapsed + "; Ragdoll=true; Head=" + head + "; Pelvis=" + pelvis + "; Ground=" + ground.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; saúde preservada");
            }
        }
        if (elapsed >= 32000 && phase != 7)
        {
            RequireFightClipsObserved();
            if (!finalContactObserved || !fightFallObserved || !dinjer.IsRagdoll) throw Error("Dinjer não concluiu a queda física após o soco em32s");
            for (int i = 1; i < fightActors.Length; i++) { fightActors[i].Ped.Task.ClearAllImmediately(); fightActors[i].Ped.Task.StandStill(-1); }
            phase = 7; Log("EP01_BARRACO_COREOGRAFIA_CONCLUIDA", "TempoMs=" + elapsed + "; quatro atores/clipes/contacto/queda reais; saúde preservada; teste visual pendente");
        }
    }

    private void FaceFightPair(int first, int second)
    {
        GTA.Ped a = fightActors[first].Ped, b = fightActors[second].Ped;
        float distance = Distance(a.Position, b.Position);
        if (distance < 0.7f || distance > 1.5f || System.Math.Abs(a.Position.Z - b.Position.Z) > 0.4f)
            throw Error("a dupla " + fightActors[first].Code + "/" + fightActors[second].Code + " precisa de marcas a0,7–1,5m no mesmo piso; sem teleporte na ação");
        float heading = (b.Position - a.Position).ToHeading(); a.Heading = heading; b.Heading = (heading + 180f) % 360f;
    }
    private void PlayFightRound(int elapsed, string a, string b, string c, string d, int duration)
    {
        string[] clips = { a, b, c, d };
        for (int i = 0; i < fightActors.Length; i++)
        {
            FightActor state = fightActors[i]; state.Clip = clips[i]; state.ClipStartedMs = elapsed; state.ClipObserved = false; state.HighestPhase = 0f;
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, state.Handle, FightDictionary, state.Clip, 4f, -4f, duration, 0, 0f, false, false, false);
            Log("EP01_BARRACO_CLIPE_PEDIDO", "TempoMs=" + elapsed + "; Ator=" + state.Code + "; Clip=" + state.Clip + "; DuracaoMs=" + duration);
        }
    }
    private void ObserveFightClip(FightActor state, int elapsed)
    {
        // O ragdoll próprio substitui o clipe da vítima; conservar a fase comprovada antes da queda.
        if (fightFallAt >= 0 && System.Object.ReferenceEquals(state, fightActors[0]) &&
            state.Clip == "victim_takedown_front_uppercut") return;
        if (state.Clip == null || elapsed <= state.ClipStartedMs) return;
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, state.Handle, FightDictionary, state.Clip, 3);
        if (playing)
        {
            float value = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, state.Handle, FightDictionary, state.Clip);
            if (value == -1f && elapsed - state.ClipStartedMs < 500) return;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f) throw Error("fase real inválida na coreografia: " + state.Code + "/" + state.Clip);
            state.HighestPhase = System.Math.Max(state.HighestPhase, value);
            if (!state.ClipObserved) Log("EP01_BARRACO_CLIPE_OBSERVADO", "TempoMs=" + elapsed + "; Ator=" + state.Code + "; Clip=" + state.Clip + "; Fase=" + value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            state.ClipObserved = true;
        }
        else if (!state.ClipObserved && elapsed - state.ClipStartedMs >= 500) throw Error("o ator não iniciou o clipe real coreografado: " + state.Code + "/" + state.Clip);
    }
    private void RequireFightClipsObserved()
    {
        foreach (FightActor state in fightActors)
        {
            float requiredPhase = fightFallAt >= 0 && System.Object.ReferenceEquals(state, fightActors[0]) &&
                state.Clip == "victim_takedown_front_uppercut" ? 0.15f : 0.05f;
            if (!state.ClipObserved || state.HighestPhase < requiredPhase) throw Error("clipe coreografado não progrediu: " + state.Code + "/" + state.Clip);
        }
    }
    private static GTA.Math.Vector3 FightBonePosition(FightActor state, GTA.Bone bone)
    {
        GTA.PedBone value = state.Ped.Bones[bone];
        if (System.Object.ReferenceEquals(value, null) || !value.IsValid) throw Error("osso corporal indisponível para coreografia: " + state.Code + "/" + bone);
        GTA.Math.Vector3 position = value.Position;
        if (float.IsNaN(position.X) || float.IsInfinity(position.X) || float.IsNaN(position.Y) || float.IsInfinity(position.Y) || float.IsNaN(position.Z) || float.IsInfinity(position.Z))
            throw Error("posição corporal não finita na coreografia");
        return position;
    }
    private void RestoreFightActors(bool clearTasks)
    {
        foreach (FightActor state in fightActors)
        {
            if (state == null || !Owned(state.Ped, state.Handle, state.Address) || state.Ped.Model.Hash != state.Model) continue;
            if (clearTasks) { state.Ped.Task.ClearAllImmediately(); state.Ped.Task.ClearLookAt(); state.Ped.Velocity = GTA.Math.Vector3.Zero; }
            state.Ped.IsInvincible = state.Invincible; state.Ped.CanRagdoll = state.CanRagdoll; state.Ped.IsPositionFrozen = state.Frozen;
        }
    }
    public void Cleanup()
    {
        if (disposed) return;
        if (fall != null) { fall.Cleanup(); fall.Dispose(); fall = null; }
        if (vehicles != null) { vehicles.Cleanup(); vehicles.Dispose(); vehicles = null; }
        if (nativeFightC07 != null) { nativeFightC07.Cleanup(); nativeFightC07 = null; }
        if (fightActors != null) RestoreFightActors(true);
        if (Owned(dinjer, dinjerHandle, dinjerAddress)) { dinjer.Task.ClearAllImmediately(); if (sceneId == "EP01_C06") dinjer.ResetMovementClipSet(); }
        if (Owned(colleague, colleagueHandle, colleagueAddress)) colleague.Task.ClearAllImmediately();
        if (requested)
        {
            if (sceneId == "EP01_C04") GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, FarewellDictionary);
            else if (sceneId == "EP01_C07") GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, FightDictionary);
            else GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_SET, WalkSet);
        }
        requested = false; ready = false; resetDone = false; sceneId = null; dinjer = null; colleague = null;
        failed = false; started = false; loading = null; dinjerHandle = 0; colleagueHandle = 0; dinjerModel = 0; colleagueModel = 0;
        fightActors = null; finalContactObserved = false; fightFallObserved = false; fightFallAt = -1;
    }
    public void Dispose() { Cleanup(); disposed = true; }
    private void RequireActors()
    {
        if (!Owned(dinjer, dinjerHandle, dinjerAddress) || dinjer.Model.Hash != dinjerModel || dinjer.IsDead ||
            (colleagueHandle != 0 && (!Owned(colleague, colleagueHandle, colleagueAddress) || colleague.Model.Hash != colleagueModel || colleague.IsDead))) throw Error("identidade/vida do elenco mudou");
        if (fightActors != null) foreach (FightActor state in fightActors)
            if (state == null || !Owned(state.Ped, state.Handle, state.Address) || state.Ped.Model.Hash != state.Model || state.Ped.IsDead) throw Error("identidade/vida dos quatro atores da briga mudou");
    }
    private static bool Owned(GTA.Ped actor, int handle, System.IntPtr address)
    { return !System.Object.ReferenceEquals(actor, null) && actor.Exists() && actor.Handle == handle && actor.MemoryAddress == address && !actor.IsPlayer; }
    private static void RequirePed(GTA.Ped actor)
    { if (System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.IsDead || actor.IsPlayer || !actor.IsPersistent || actor.IsInVehicle()) throw Error("somente atores próprios vivos/a pé/persistentes"); }
    private static float Distance(GTA.Math.Vector3 a, GTA.Math.Vector3 b) { float x = a.X - b.X, y = a.Y - b.Y; return (float)System.Math.Sqrt(x * x + y * y); }
    private void Active() { if (disposed) throw Error("helper encerrado"); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Episódio 1: " + message + "."); }
    private void Log(string code, string message) { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
}

// C07: authored paired motion. Shared scenes preserve contact and continuous root motion.
// Pair assets are present in the installed Menyoo PedAnimList, lines 258116..123.
public sealed class BarracoComicoC07Diretor
{
    private const string Fight = "anim@amb@casino@brawl@fights@shop_throw@";
    private const string Hold = "anim@amb@casino@brawl@fights@headlock@";
    private const string Conversation = "anim@amb@casino@brawl@fights@conversation@";
    private const string Guard = "anim@amb@casino@brawl@fights@guard@";
    private const string Female = "gestures@f@standing@casual", Male = "gestures@m@standing@casual";
    private static readonly string[] Codes = { "DINJER", "COLEGA_1", "COLEGA_2", "COLEGA_3" };
    private static readonly string[] Dictionaries = { Fight, Hold, Conversation, Guard, Female, Male };
    private sealed class Actor
    {
        internal GTA.Ped Ped;
        internal string Code, Dictionary, Clip;
        internal int Handle, Model, Health, LastMotionAt, MaximumMotionGap;
        internal System.IntPtr Address;
        internal bool Invincible, Frozen, CanRagdoll, Changed;
        internal GTA.Math.Vector3 Start, PreviousHead, PreviousLeft, PreviousRight;
        internal float RootTravel, BoneTravel;
    }
    private sealed class Pair
    {
        internal Actor A, B;
        internal string Dictionary, ClipA, ClipB;
        internal GTA.Math.Vector3 Origin, Rotation;
        internal GTA.FwSyncedScene Scene;
        internal int Handle, LastAdvanceAt, Wraps;
        internal float Duration, PreviousPhase;
    }
    private Actor[] actors;
    private Pair[] pairs;
    private readonly System.Action<string,string> log;
    private bool requested, ready, reset, running, finishing, complete;
    private int lastElapsed = -1, nextLog;
    public BarracoComicoC07Diretor(System.Action<string,string> logger) { log = logger; }
    public bool IsReady { get { PollLoading(); return ready; } }
    public void Prepare(string scene, string local, GTA.Ped[] ownActors)
    {
        if (actors != null || scene != "EP01_C07" || local != "AEROPORTO_HANGAR" || ownActors == null || ownActors.Length != 4)
            throw Error("quatro atores proprios vivos no hangar obrigatorios");
        actors = new Actor[4];
        for (int i=0; i<4; i++)
        {
            GTA.Ped ped=ownActors[i];
            if (System.Object.ReferenceEquals(ped,null) || !ped.Exists() || ped.IsPlayer || !ped.IsPersistent || ped.IsDead || ped.IsInVehicle())
                throw Error("ator invalido: "+Codes[i]);
            for (int j=0; j<i; j++) if (actors[j].Handle==ped.Handle) throw Error("ator repetido");
            actors[i]=new Actor { Ped=ped, Code=Codes[i], Handle=ped.Handle, Address=ped.MemoryAddress, Model=ped.Model.Hash,
                Invincible=ped.IsInvincible, Frozen=ped.IsPositionFrozen, CanRagdoll=ped.CanRagdoll };
        }
        foreach (string dictionary in Dictionaries)
        {
            if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ANIM_DICT_EXIST,dictionary)) throw Error("dicionario ausente: "+dictionary);
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT,dictionary);
        }
        requested=true;
        Log("EP01_BARRACO_AUTORAL_PREPARADO","FalasOriginais=18s; Entrada=4s; Briga=22..32s; Rescaldo=32..34s; CenasSincronizadas=2; RootMotionLivre=True; CombateIA=False; AprovacaoVisual=False");
    }
    private void PollLoading()
    {
        if (ready || !requested) return;
        foreach (string dictionary in Dictionaries) if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED,dictionary)) return;
        pairs=new Pair[] {
            new Pair { A=actors[0], B=actors[1], Dictionary=Conversation, ClipA="breakout_bothfight_mp_m_brawler", ClipB="breakout_bothfight_mp_m_pedestrian" },
            new Pair { A=actors[3], B=actors[2], Dictionary=Hold, ClipA="headlock_loop_mp_m_brawler", ClipB="headlock_loop_mp_m_pedestrian" }
        };
        foreach (Pair pair in pairs)
        {
            float a=Duration(pair.Dictionary,pair.ClipA), b=Duration(pair.Dictionary,pair.ClipB);
            if (System.Math.Abs(a-b)>0.035f) throw Error("tracks do par tem duracoes diferentes");
            pair.Duration=a;
            Log("EP01_BARRACO_AUTORAL_RECURSO","Dicionario="+pair.Dictionary+"; A="+pair.ClipA+"; B="+pair.ClipB+"; DuracaoSegundos="+N(a)+"; ParOriginalDoJogo=True; AprovacaoVisual=False");
        }
        // Read-only alternative metadata makes a failed artistic trial actionable.
        Probe(Conversation,"breakout_bothfight_mp_m_brawler"); Probe(Conversation,"breakout_bothfight_mp_m_pedestrian");
        Probe(Guard,"breakout_mp_m_brawler_01"); Probe(Guard,"breakout_mp_m_brawler_02"); Probe(Guard,"breakout_mp_m_guard");
        Duration(Female,"gesture_displeased"); Duration(Male,"gesture_easy_now"); Duration(Male,"gesture_why");
        ready=true;
    }
    private void Probe(string dictionary,string clip)
    { Log("EP01_BARRACO_AUTORAL_ALTERNATIVA","Dicionario="+dictionary+"; Clipe="+clip+"; DuracaoSegundos="+N(Duration(dictionary,clip))+"; Offset="+InitialPosition(dictionary,clip,GTA.Math.Vector3.Zero,GTA.Math.Vector3.Zero)+"; SomenteLeitura=True"); }
    public void Reset()
    {
        if (!IsReady) throw Error("aguarde recursos");
        Restore();
        foreach (Actor actor in actors)
        {
            Require(actor); actor.Health=actor.Ped.Health; actor.Changed=true;
            actor.Ped.IsInvincible=true; actor.Ped.CanRagdoll=false; actor.Ped.IsPositionFrozen=true;
            actor.LastMotionAt=22000; actor.MaximumMotionGap=0; actor.RootTravel=0f; actor.BoneTravel=0f;
        }
        foreach (Pair pair in pairs)
        {
            // Exact authored offsets are established before the countdown. There are no position setters in Update.
            GTA.Math.Vector3 zeroRotation=InitialRotation(pair.Dictionary,pair.ClipA,GTA.Math.Vector3.Zero,GTA.Math.Vector3.Zero);
            pair.Rotation=new GTA.Math.Vector3(0f,0f,pair.A.Ped.Heading-zeroRotation.Z);
            GTA.Math.Vector3 offset=InitialPosition(pair.Dictionary,pair.ClipA,GTA.Math.Vector3.Zero,pair.Rotation);
            pair.Origin=pair.A.Ped.Position-offset;
            PlaceForPreparation(pair.A,pair,pair.ClipA); PlaceForPreparation(pair.B,pair,pair.ClipB);
            pair.PreviousPhase=0f; pair.LastAdvanceAt=22000; pair.Wraps=0;
        }
        foreach (Actor actor in actors) actor.Start=actor.Ped.Position;
        lastElapsed=-1; nextLog=22000; reset=true; running=false; finishing=false; complete=false;
    }
    private void PlaceForPreparation(Actor actor,Pair pair,string clip)
    {
        GTA.Math.Vector3 point=InitialPosition(pair.Dictionary,clip,pair.Origin,pair.Rotation);
        GTA.Math.Vector3 rotation=InitialRotation(pair.Dictionary,clip,pair.Origin,pair.Rotation);
        Check(point); Check(rotation);
        if ((point-pair.A.Ped.Position).Length()>3f) throw Error("offset autoral excede area reservada");
        actor.Ped.Task.ClearAllImmediately(); actor.Ped.Velocity=GTA.Math.Vector3.Zero;
        actor.Ped.PositionNoOffset=point; actor.Ped.Heading=rotation.Z;
        actor.Ped.Task.StandStill(-1);
        actor.Dictionary=pair.Dictionary; actor.Clip=clip;
        Log("EP01_BARRACO_AUTORAL_MARCA","Ator="+actor.Code+"; Posicao="+point+"; Rotacao="+rotation+"; OrigemComum="+pair.Origin+"; AntesDaContagem=True; OffsetNativoReal=True");
    }
    public void Update(int elapsed)
    {
        if (!ready || !reset || elapsed<0 || elapsed<lastElapsed) throw Error("reinicie antes da tomada");
        if (complete) return;
        lastElapsed=elapsed;
        foreach (Actor actor in actors)
        {
            Require(actor);
            if (actor.Ped.Health!=actor.Health || actor.Ped.IsRagdoll || !actor.Ped.IsInvincible || ((running || finishing) && actor.Ped.IsPositionFrozen))
                throw Error("vida/postura/raiz propria mudou: "+actor.Code);
            if (actor.Ped.IsInMeleeCombat || actor.Ped.IsPerformingMeleeAction) throw Error("combate IA inesperado: "+actor.Code);
        }
        if (elapsed<22000) return;
        if (!running && !finishing)
        {
            if (elapsed>=22500) throw Error("inicio22s perdido");
            foreach (Actor actor in actors)
            {
                actor.Ped.Task.ClearLookAt(); actor.LastMotionAt=elapsed;
                actor.PreviousHead=Bone(actor,GTA.Bone.SkelHead); actor.PreviousLeft=Bone(actor,GTA.Bone.SkelLeftHand); actor.PreviousRight=Bone(actor,GTA.Bone.SkelRightHand);
            }
            foreach (Pair pair in pairs) StartPair(pair,elapsed);
            running=true;
            Log("EP01_BARRACO_AUTORAL_INICIO","TempoMs="+elapsed+"; Atores=4; CenasNativasPareadas=2; NenhumaRaizCongelada=True; SemMurrosRecortados=True; CombateIA=False; AprovacaoVisual=False");
        }
        if (running && elapsed<32000)
        {
            foreach (Pair pair in pairs) ObservePair(pair,elapsed);
            foreach (Actor actor in actors) ObserveMotion(actor,elapsed);
            if (elapsed>=nextLog)
            {
                nextLog=elapsed+500;
                foreach (Pair pair in pairs)
                    Log("EP01_BARRACO_AUTORAL_FASE","TempoMs="+elapsed+"; A="+pair.A.Code+"; B="+pair.B.Code+"; Cena="+pair.Handle+"; Fase="+N(pair.Scene.Phase)+"; VoltasNativas="+pair.Wraps+"; RaizA="+pair.A.Ped.Position+"; RaizB="+pair.B.Ped.Position+"; AprovacaoVisual=False");
            }
        }
        else if (running)
        {
            foreach (Actor actor in actors) if (actor.BoneTravel<0.6f) throw Error("movimento corporal nao observado: "+actor.Code);
            EndScenes();
            for (int i=0; i<actors.Length; i++)
                GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM,actors[i].Handle,i==0?Female:Male,i==0?"gesture_displeased":i==3?"gesture_easy_now":"gesture_why",3f,-3f,-1,2,0f,false,false,false);
            running=false; finishing=true;
            Log("EP01_BARRACO_AUTORAL_BRIGA_CONCLUIDA","TempoMs="+elapsed+"; Intervalo=22..32s; ContatoCoreografadoNativo=True; CombateIA=False; SetterPosicaoNaAcao=False; AprovacaoVisual=False");
            foreach (Actor actor in actors)
                Log("EP01_BARRACO_AUTORAL_MOVIMENTO","Ator="+actor.Code+"; PercursoOssos="+N(actor.BoneTravel)+"; MaiorIntervaloSemMovimentoMs="+actor.MaximumMotionGap+"; DeslocamentoRaiz="+N(actor.RootTravel)+"; AprovacaoVisual=False");
        }
        if (elapsed>=34000) { if (!finishing) throw Error("briga nao concluiu"); complete=true; Log("EP01_BARRACO_AUTORAL_CONCLUIDO","TempoMs="+elapsed+"; DuracaoCena=34s; DuracaoBriga=10s; SaudePreservada=True; AprovacaoVisual=False"); }
    }
    private void StartPair(Pair pair,int elapsed)
    {
        foreach (Actor actor in new Actor[] {pair.A,pair.B})
        {
            GTA.Math.Vector3 expected=InitialPosition(pair.Dictionary,actor.Clip,pair.Origin,pair.Rotation);
            if ((actor.Ped.Position-expected).Length()>0.22f) throw Error("marca inicial mudou antes da luta: "+actor.Code);
            // Roots are protected only while talking; the complete paired fight owns their motion from this Tick.
            actor.Ped.IsPositionFrozen=false;
        }
        pair.Scene=GTA.FwSyncedScene.Create(pair.Origin,pair.Rotation,GTA.EulerRotationOrder.YXZ);
        if (pair.Scene==null || !pair.Scene.Exists()) throw Error("cena sincronizada nao criada");
        pair.Handle=pair.Scene.Handle; pair.Scene.IsLooped=true; pair.Scene.HoldsLastFrame=false; pair.Scene.Rate=0f; pair.Scene.Phase=0f;
        foreach (Actor actor in new Actor[] {pair.A,pair.B})
            actor.Ped.Task.PlaySynchronizedScene(pair.Scene,new GTA.CrClipAsset(pair.Dictionary,actor.Clip),new GTA.AnimationBlendDelta(4f),new GTA.AnimationBlendDelta(4f),
                GTA.SyncedSceneFlags.DontInterrupt | GTA.SyncedSceneFlags.AbortOnDeath | GTA.SyncedSceneFlags.OnAbortStopScene,
                (GTA.RagdollBlockingFlags)0,new GTA.AnimationBlendDelta(4f),(GTA.AnimationIKControlFlags)0);
        pair.Scene.Rate=1f; pair.LastAdvanceAt=elapsed;
    }
    private void ObservePair(Pair pair,int elapsed)
    {
        if (pair.Scene==null || !pair.Scene.Exists() || pair.Scene.Handle!=pair.Handle || !pair.Scene.IsLooped || System.Math.Abs(pair.Scene.Rate-1f)>0.001f)
            throw Error("cena pareada perdeu identidade/ritmo");
        float phase=pair.Scene.Phase;
        if (!Finite(phase) || phase<0f || phase>1f) throw Error("fase nativa invalida");
        if (phase+0.3f<pair.PreviousPhase) { pair.Wraps++; pair.LastAdvanceAt=elapsed; }
        else if (phase>pair.PreviousPhase+0.0001f) pair.LastAdvanceAt=elapsed;
        pair.PreviousPhase=phase;
        if (elapsed-pair.LastAdvanceAt>650) throw Error("cena pareada sem progredir");
        foreach (Actor actor in new Actor[] {pair.A,pair.B})
            if (elapsed>22500 && !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,actor.Handle,pair.Dictionary,actor.Clip,2))
                throw Error("ator fora do mesmo par nativo: "+actor.Code);
    }
    private void ObserveMotion(Actor actor,int elapsed)
    {
        GTA.Math.Vector3 head=Bone(actor,GTA.Bone.SkelHead),left=Bone(actor,GTA.Bone.SkelLeftHand),right=Bone(actor,GTA.Bone.SkelRightHand);
        float delta=(head-actor.PreviousHead).Length()+(left-actor.PreviousLeft).Length()+(right-actor.PreviousRight).Length();
        actor.BoneTravel+=delta; if (delta>=0.003f) actor.LastMotionAt=elapsed;
        actor.MaximumMotionGap=System.Math.Max(actor.MaximumMotionGap,elapsed-actor.LastMotionAt);
        if (elapsed-actor.LastMotionAt>1000) throw Error("ator sem movimento corporal por1s: "+actor.Code);
        actor.RootTravel=System.Math.Max(actor.RootTravel,(actor.Ped.Position-actor.Start).Length());
        if (actor.RootTravel>3f) throw Error("root motion saiu da area reservada");
        actor.PreviousHead=head; actor.PreviousLeft=left; actor.PreviousRight=right;
    }
    private void EndScenes()
    {
        if (pairs==null) return;
        foreach (Pair pair in pairs)
        {
            if (pair.Scene!=null && pair.Scene.Exists()) { pair.Scene.IsLooped=false; pair.Scene.HoldsLastFrame=false; pair.Scene.Rate=1f; }
            pair.Scene=null;
        }
    }
    public void Restore()
    {
        EndScenes();
        if (actors!=null) foreach (Actor actor in actors)
        {
            if (actor==null || !actor.Changed || !Owned(actor)) continue;
            actor.Ped.Task.ClearAllImmediately(); actor.Ped.Task.ClearLookAt(); actor.Ped.Task.StandStill(-1);
            actor.Ped.IsInvincible=actor.Invincible; actor.Ped.CanRagdoll=actor.CanRagdoll; actor.Ped.IsPositionFrozen=actor.Frozen; actor.Changed=false;
        }
        reset=false; running=false;
    }
    public void Cleanup()
    {
        Restore();
        if (requested) foreach (string dictionary in Dictionaries) GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT,dictionary);
        actors=null; pairs=null; requested=false; ready=false;
    }
    private static bool Owned(Actor actor)
    { return !System.Object.ReferenceEquals(actor.Ped,null) && actor.Ped.Exists() && actor.Ped.Handle==actor.Handle && actor.Ped.MemoryAddress==actor.Address && actor.Ped.Model.Hash==actor.Model && actor.Ped.IsPersistent && !actor.Ped.IsPlayer; }
    private static void Require(Actor actor)
    { if (actor==null || !Owned(actor) || actor.Ped.IsDead || actor.Ped.IsInVehicle()) throw Error("identidade propria mudou"); }
    private static GTA.Math.Vector3 Bone(Actor actor,GTA.Bone bone)
    { GTA.PedBone b=actor.Ped.Bones[bone]; if (b==null || !b.IsValid) throw Error("osso real indisponivel"); GTA.Math.Vector3 p=b.Position; Check(p); return p; }
    private static GTA.Math.Vector3 InitialPosition(string dictionary,string clip,GTA.Math.Vector3 origin,GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_POSITION,dictionary,clip,origin.X,origin.Y,origin.Z,rotation.X,rotation.Y,rotation.Z,0f,2); }
    private static GTA.Math.Vector3 InitialRotation(string dictionary,string clip,GTA.Math.Vector3 origin,GTA.Math.Vector3 rotation)
    { return GTA.Native.Function.Call<GTA.Math.Vector3>(GTA.Native.Hash.GET_ANIM_INITIAL_OFFSET_ROTATION,dictionary,clip,origin.X,origin.Y,origin.Z,rotation.X,rotation.Y,rotation.Z,0f,2); }
    private static float Duration(string dictionary,string clip)
    { float n=GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION,dictionary,clip); if (!Finite(n) || n<=0f || n>120f) throw Error("clipe real ausente: "+dictionary+"/"+clip); return n; }
    private static void Check(GTA.Math.Vector3 p) { if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) throw Error("vetor nao finito"); }
    private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
    private static string N(float n) { return n.ToString("R",System.Globalization.CultureInfo.InvariantCulture); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("Barraco autoral C07: "+message+"."); }
    private void Log(string code,string message) { if (log!=null) { try { log(code,message); } catch (System.Exception) { } } }
}


public sealed class BarracoNativoC07Diretor
{
    private sealed class Actor
    {
        internal GTA.Ped Ped;
        internal int Handle, Model, Health, MaxHealth, Armor, LastHealth, MeleeAt = -1, PendingAt = -1, Hits;
        internal float MinimumHealth;
        internal bool HealthPrepared;
        internal System.IntPtr Address;
        internal string Code;
        internal bool Invincible, Frozen, Ragdoll, MeleeProof, SwitchWeapons, Changed;
        internal GTA.WeaponHash Weapon;
        internal GTA.Math.Vector3 StartRoot, PreviousRoot, PreviousHead, PendingRoot, PendingHead;
        internal float Movement;
        internal readonly float[] Gaps = new float[16];
        internal readonly int[] GapTimes = new int[16];
        internal int GapIndex;
    }
    private readonly System.Action<string, string> log;
    private Actor[] states;
    private bool ready, running, complete;
    private int nextLog, lastElapsed = -1;
    private static readonly string[] Codes = { "DINJER", "COLEGA_1", "COLEGA_2", "COLEGA_3" };
    public BarracoNativoC07Diretor(System.Action<string, string> log) { this.log = log; }
    public bool IsReady { get { return ready; } }

    public void Prepare(string scene, string local, GTA.Ped[] actors)
    {
        if (ready || states != null || scene != "EP01_C07" || local != "AEROPORTO_HANGAR" || actors == null || actors.Length != 4)
            throw Error("somente os quatro atores proprios de EP01_C07/AEROPORTO_HANGAR");
        states = new Actor[4];
        for (int i = 0; i < actors.Length; i++)
        {
            GTA.Ped ped = actors[i]; RequirePed(ped);
            for (int j = 0; j < i; j++) if (states[j].Handle == ped.Handle) throw Error("atores distintos obrigatorios");
            GTA.Weapon weapon = ped.Weapons.Current;
            if (System.Object.ReferenceEquals(weapon, null)) throw Error("getter da arma original indisponivel");
            states[i] = new Actor { Ped = ped, Code = Codes[i], Handle = ped.Handle, Address = ped.MemoryAddress,
                Model = ped.Model.Hash, Health = ped.Health, MaxHealth = ped.MaxHealth, Armor = ped.Armor, Invincible = ped.IsInvincible,
                Frozen = ped.IsPositionFrozen, Ragdoll = ped.CanRagdoll, MeleeProof = ped.IsMeleeProof,
                SwitchWeapons = ped.CanSwitchWeapons, Weapon = weapon.Hash };
            Bone(ped, GTA.Bone.SkelLeftHand); Bone(ped, GTA.Bone.SkelRightHand); Bone(ped, GTA.Bone.SkelHead);
            Bone(ped, GTA.Bone.SkelSpine2); Bone(ped, GTA.Bone.SkelPelvis);
            ResetObservations(states[i]);
        }
        foreach (Actor state in states) PrepareHealthBeforeArm(state);
        ready = true;
        Log("EP01_BARRACO_NATIVO_PREPARADO", "Cena=EP01_C07; Atores=4; Entrada4s; Falas18s; Combate22..32s; Pares=DINJER/COLEGA_1,COLEGA_2/COLEGA_3; ResourceApproval=False; NativeActionProved=False; ImageApproval=False");
    }

    public void Reset()
    {
        if (!ready) throw Error("preparo ausente");
        Restore();
        foreach (Actor state in states)
        {
            RequireActor(state);
            if (!state.HealthPrepared || state.Ped.MaxHealth != 1000 || state.Ped.Health != 1000 || state.Ped.Armor != state.Armor)
                throw Error("tomada recebeu dano; remontar antes de armar, sem refill na captura: " + state.Code);
            ResetObservations(state);
        }
        running = false; complete = false; nextLog = 0; lastElapsed = -1;
    }

    public void Update(int elapsed)
    {
        if (!ready || elapsed < 0 || elapsed < lastElapsed) throw Error("tempo monotono/preparo obrigatorios");
        lastElapsed = elapsed;
        foreach (Actor state in states)
        {
            RequireActor(state);
            if (!state.HealthPrepared || state.Ped.MaxHealth != 1000 || state.Ped.Armor != state.Armor || state.Ped.Health <= state.MinimumHealth)
                throw Error("reserva propria/armadura/limite preventivo divergiu; sem refill durante filmagem: " + state.Code);
            if (elapsed < 22000 && (state.Ped.Health != 1000 || !state.Ped.IsInvincible))
                throw Error("reserva/ invencibilidade anterior ao combate mudou: " + state.Code);
        }
        if (elapsed < 22000 || complete) return;
        if (!running)
        {
            if (elapsed >= 22500) throw Error("inicio do combate22s perdido");
            RequirePair(0,1,1.6f); RequirePair(2,3,1.6f);
            foreach (Actor state in states)
            {
                state.Changed = true; // Conservar ownership para restaurar mesmo se o setter seguinte falhar.
                state.StartRoot = state.Ped.Position; state.PreviousRoot = state.StartRoot;
                state.PreviousHead = Bone(state.Ped, GTA.Bone.SkelHead);
                state.Ped.Task.ClearAllImmediately(); state.Ped.Task.ClearLookAt();
                state.Ped.IsInvincible = false; state.Ped.IsMeleeProof = false;
                state.Ped.IsPositionFrozen = false; state.Ped.CanRagdoll = true;
                if (!state.Ped.Weapons.Select(GTA.WeaponHash.Unarmed,true)) throw Error("unarmed recusado: " + state.Code);
                state.Ped.CanSwitchWeapons = false;
                ClearNativeDamage(state);
                RequireFightFlags(state);
            }
            for (int i = 0; i < states.Length; i++)
                states[i].Ped.Task.PutDirectlyIntoMelee(states[i ^ 1].Ped,GTA.AnimationBlendDuration.Normal,0f,GTA.TaskCombatFlags.None);
            running = true; nextLog = elapsed;
            Log("EP01_BARRACO_NATIVO_INICIADO", "TempoMs=" + elapsed + "; Task=PutDirectlyIntoMelee; Blend=Normal; StrafePhase=0; CombatFlags=None; Invincible=False; HealthPreparedBeforeArm=1000; MeleeProof=False; Frozen=False; Armas=UNARMED; SwitchWeapons=False; TaskRequests=4; SemPosicaoHeadingForcaDanoRagdollSetters=True");
        }
        RequirePair(0,1,3f); RequirePair(2,3,3f);
        for (int i = 0; i < states.Length; i++) Observe(states[i],states[i ^ 1],elapsed);
        if (elapsed >= nextLog)
        {
            nextLog = elapsed + 500;
            for (int i = 0; i < states.Length; i++)
            {
                Actor state = states[i], partner = states[i ^ 1];
                Log("EP01_BARRACO_NATIVO_ESTADO", "TempoMs=" + elapsed + "; Ator=" + state.Code + "; Handle=" + state.Handle +
                    "; Parceiro=" + partner.Code + "; Root=" + state.Ped.Position + "; Speed=" + N(state.Ped.Speed) +
                    "; NativeMelee=" + state.Ped.IsInMeleeCombat + "; NativeMeleeAction=" + state.Ped.IsPerformingMeleeAction +
                    "; DamagedByOwnPartner=" + state.Ped.HasBeenDamagedBy(partner.Ped) + "; UnarmedDamage=" + state.Ped.HasBeenDamagedBy(GTA.WeaponHash.Unarmed) +
                    "; MinGap250ms=" + N(RecentGap(state,elapsed)) + "; CreditedHits=" + state.Hits + "; Movement=" + N(state.Movement) +
                    "; Ragdoll=" + state.Ped.IsRagdoll + "; Health=" + state.Ped.Health + "; MaxHealth=" + state.Ped.MaxHealth + "; MinimumHealth=" + N(state.MinimumHealth) + "; Armor=" + state.Ped.Armor + "; ImageApproval=False");
            }
        }
        if (elapsed >= 27000 && ((states[0].Hits + states[1].Hits == 0 && states[0].PendingAt < 0 && states[1].PendingAt < 0) ||
            (states[2].Hits + states[3].Hits == 0 && states[2].PendingAt < 0 && states[3].PendingAt < 0)))
            throw Error("uma dupla nao registrou impacto nativo+reacao em5s; ameaca/soco no ar nao fecham a cena");
        if (elapsed >= 32000)
        {
            if (states[0].Hits + states[1].Hits < 1 || states[2].Hits + states[3].Hits < 1 ||
                System.Math.Max(states[0].Movement,states[1].Movement) < 0.15f || System.Math.Max(states[2].Movement,states[3].Movement) < 0.15f)
                throw Error("faltou golpe/reacao/deslocamento de uma dupla em32s");
            foreach (Actor state in states) { state.Ped.Task.ClearAllImmediately(); state.Ped.IsInvincible = true; if (!state.Ped.IsRagdoll) state.Ped.Task.StandStill(-1); }
            running = false; complete = true;
            Log("EP01_BARRACO_NATIVO_CONCLUIDO", "TempoMs=" + elapsed + "; HitsDupla1=" + (states[0].Hits + states[1].Hits) +
                "; HitsDupla2=" + (states[2].Hits + states[3].Hits) + "; CorteControlado=True; QuedaObrigatoria=False; DanoNativoSomenteParceiroDesarmado=True; ArmorPreserved=True; HealthRefillDuringAction=False; NativeImpactReactionMovementObserved=True; ImageApproval=False");
        }
    }

    private void Observe(Actor state, Actor partner, int elapsed)
    {
        RequireFightFlags(state);
        int health = state.Ped.Health;
        int nativeHealthDrop = state.LastHealth - health; // Amostra do mesmo tick ANTES de atualizar LastHealth.
        if (health > state.LastHealth || health <= state.MinimumHealth)
            throw Error("saude aumentou ou atingiu limite preventivo; sem refill: " + state.Code);
        if (health < state.LastHealth)
        {
            if (!state.Ped.HasBeenDamagedBy(partner.Ped) || !state.Ped.HasBeenDamagedBy(GTA.WeaponHash.Unarmed))
                throw Error("perda de saude sem atribuicao nativa ao parceiro desarmado: " + state.Code);
            Log("EP01_BARRACO_NATIVO_DANO_SAUDE", "TempoMs=" + elapsed + "; Vitima=" + state.Code + "; Atacante=" + partner.Code +
                "; HealthBefore=" + state.LastHealth + "; HealthAfter=" + health + "; NativeOwnPartnerUnarmed=True; HealthSetter=False; ImageApproval=False");
            state.LastHealth = health;
        }
        GTA.Math.Vector3 root = state.Ped.Position, head = Bone(state.Ped,GTA.Bone.SkelHead);
        float moved = (root-state.StartRoot).Length(); state.Movement = System.Math.Max(state.Movement,moved);
        if (!Finite(moved) || moved > 4f || System.Math.Abs(root.Z-state.StartRoot.Z) > 1.5f)
            throw Error("ator saiu do volume proprio limitado; sem reassentamento: " + state.Code);
        GTA.Ped combatTarget = state.Ped.CombatTarget;
        if (!System.Object.ReferenceEquals(combatTarget,null) && combatTarget.Exists() && combatTarget.Handle != partner.Handle)
            throw Error("IA mudou alvo para fora da dupla propria: " + state.Code);
        if (state.Ped.IsInMeleeCombat && state.MeleeAt < 0) state.MeleeAt = elapsed;
        if (elapsed >= 23500 && state.MeleeAt < 0) throw Error("CTaskMelee nao observado; nao aceitar so postura/ameaca: " + state.Code);
        float gap = HandBodyGap(partner.Ped,state.Ped);
        state.Gaps[state.GapIndex] = gap; state.GapTimes[state.GapIndex] = elapsed; state.GapIndex = (state.GapIndex+1)%state.Gaps.Length;
        if (state.PendingAt < 0 && state.Ped.HasBeenDamagedBy(partner.Ped) && state.Ped.HasBeenDamagedBy(GTA.WeaponHash.Unarmed))
        {
            var damaged = new GTA.Native.OutputArgument();
            bool boneObserved = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_PED_LAST_DAMAGE_BONE,state.Handle,damaged);
            int tag = boneObserved ? damaged.GetResult<int>() : -1;
            float recent = RecentGap(state,elapsed);
            if (recent > 0.65f || (boneObserved && tag < 0) ||
                (!boneObserved && (nativeHealthDrop <= 0 || state.MeleeAt < 0 || partner.MeleeAt < 0)))
                throw Error("dano nativo sem bone ou perda de saude no mesmo tick/melee/contato proprio: " + state.Code +
                    "; NativeBoneObserved=" + boneObserved + "; Bone=" + tag + "; HealthDropSameTick=" + nativeHealthDrop + "; Gap=" + N(recent));
            if (boneObserved) Bone(state.Ped,(GTA.Bone)tag);
            state.PendingAt = elapsed; state.PendingHead = state.PreviousHead; state.PendingRoot = state.PreviousRoot;
            Log("EP01_BARRACO_NATIVO_IMPACTO", "TempoMs=" + elapsed + "; Atacante=" + partner.Code + "; Vitima=" + state.Code +
                "; NativeDamageByOwnPartner=True; NativeWeapon=UNARMED; NativeBoneObserved=" + boneObserved +
                "; NativeDamageBone=" + tag + "; NativeHealthDropSameTick=" + nativeHealthDrop +
                "; ImpactProof=" + (boneObserved ? "NativeBone" : "NativeSameTickHealthOwnUnarmedMelee") + "; MinHandBodyGap250ms=" + N(recent) +
                "; Health=" + state.Ped.Health + "; ReactionStillPending=True; ContactGeometryInference=True; ImageApproval=False");
        }
        if (state.PendingAt >= 0)
        {
            float headMove = (head-state.PendingHead).Length(), rootMove = (root-state.PendingRoot).Length();
            if (elapsed > state.PendingAt && (state.Ped.IsRagdoll || headMove >= 0.08f || rootMove >= 0.10f))
            {
                state.Hits++;
                Log("EP01_BARRACO_NATIVO_REACAO", "TempoMs=" + elapsed + "; Ator=" + state.Code + "; Parceiro=" + partner.Code +
                    "; ImpactoMs=" + state.PendingAt + "; HeadDelta=" + N(headMove) + "; RootDelta=" + N(rootMove) +
                    "; Ragdoll=" + state.Ped.IsRagdoll + "; CreditedHit=" + state.Hits + "; TemporalReactionInference=True; ImageApproval=False");
                state.PendingAt = -1; ClearNativeDamage(state);
            }
            else if (elapsed-state.PendingAt > 750) throw Error("impacto sem reacao/deslocamento observado em750ms: " + state.Code);
        }
        state.PreviousRoot = root; state.PreviousHead = head;
    }
    private static float HandBodyGap(GTA.Ped attacker, GTA.Ped victim)
    {
        GTA.Math.Vector3 left = Bone(attacker,GTA.Bone.SkelLeftHand), right = Bone(attacker,GTA.Bone.SkelRightHand);
        float result = float.MaxValue;
        foreach (GTA.Bone bone in new GTA.Bone[] { GTA.Bone.SkelHead,GTA.Bone.SkelSpine2,GTA.Bone.SkelPelvis })
        { GTA.Math.Vector3 point = Bone(victim,bone); result = System.Math.Min(result,System.Math.Min((left-point).Length(),(right-point).Length())); }
        return result;
    }
    private static float RecentGap(Actor state,int elapsed)
    {
        float result = float.MaxValue;
        for (int i = 0; i < state.Gaps.Length; i++)
            if (state.GapTimes[i] >= 22000 && elapsed-state.GapTimes[i] >= 0 && elapsed-state.GapTimes[i] <= 250)
                result = System.Math.Min(result,state.Gaps[i]);
        return result;
    }
    private void RequirePair(int first,int second,float maximum)
    {
        GTA.Math.Vector3 a = states[first].Ped.Position, b = states[second].Ped.Position;
        if ((a-b).Length() < 0.3f || (a-b).Length() > maximum || System.Math.Abs(a.Z-b.Z) > 1.5f)
            throw Error("dupla nao esta em alcance fisico no mesmo volume; sem teleporte: " + Codes[first] + "/" + Codes[second]);
    }
    private static void RequireFightFlags(Actor state)
    {
        GTA.Weapon weapon = state.Ped.Weapons.Current;
        if (state.Ped.IsInvincible || state.Ped.IsMeleeProof || state.Ped.IsPositionFrozen || !state.Ped.CanRagdoll ||
            state.Ped.CanSwitchWeapons || System.Object.ReferenceEquals(weapon,null) || weapon.Hash != GTA.WeaponHash.Unarmed)
            throw Error("flags/getter de arma do combate proprio divergiu: " + state.Code);
    }
    private static void ClearNativeDamage(Actor state)
    {
        state.Ped.ClearLastWeaponDamage(); state.Ped.Bones.ClearLastDamaged();
        GTA.Native.Function.Call(GTA.Native.Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY,state.Handle);
    }
    private static void ResetObservations(Actor state)
    {
        state.MeleeAt = -1; state.PendingAt = -1; state.Hits = 0; state.Movement = 0f; state.GapIndex = 0; state.LastHealth = state.Ped.Health;
        for (int i = 0; i < state.GapTimes.Length; i++) { state.GapTimes[i] = -1; state.Gaps[i] = float.MaxValue; }
    }
    public void Restore()
    {
        if (states == null) return;
        foreach (Actor state in states)
        {
            if (state == null || !Owned(state)) continue;
            if (!state.Changed) continue;
            state.Ped.Task.ClearAllImmediately(); state.Ped.Task.ClearLookAt();
            state.Ped.CanSwitchWeapons = state.SwitchWeapons;
            if (!state.Ped.Weapons.Select(state.Weapon,true)) throw Error("arma original indisponivel para restaurar: " + state.Code);
            state.Ped.IsMeleeProof = state.MeleeProof; state.Ped.CanRagdoll = state.Ragdoll;
            state.Ped.IsPositionFrozen = state.Frozen; state.Ped.IsInvincible = state.Invincible;
            if (state.Ped.IsMeleeProof != state.MeleeProof || state.Ped.CanRagdoll != state.Ragdoll ||
                state.Ped.IsPositionFrozen != state.Frozen || state.Ped.IsInvincible != state.Invincible ||
                state.Ped.CanSwitchWeapons != state.SwitchWeapons || state.Ped.Weapons.Current.Hash != state.Weapon)
                throw Error("getters da restauracao propria divergiram: " + state.Code);
            state.Changed = false;
            Log("EP01_BARRACO_NATIVO_RESTAURADO", "Ator=" + state.Code + "; Handle=" + state.Handle + "; FlagsArmaOriginaisConferidas=True; HealthSetter=False; PlayerOuAmbienteAlterados=False");
        }
        running = false;
    }
    private void PrepareHealthBeforeArm(Actor state)
    {
        RequireActor(state);
        float injury = state.Ped.InjuryHealthThreshold, fatal = state.Ped.FatalInjuryHealthThreshold;
        if (!Finite(injury) || !Finite(fatal) || state.Health <= 0 || state.MaxHealth <= 0 || state.Health > 1000 || state.MaxHealth > 1000)
            throw Error("reservatorio1000/originais/thresholds invalidos: " + state.Code);
        state.MinimumHealth = System.Math.Max(300f,System.Math.Max(injury,fatal)+200f);
        if (state.MinimumHealth >= 1000f) throw Error("threshold preventivo nao cabe no reservatorio: " + state.Code);
        state.HealthPrepared = true; // Ownership do snapshot antes de qualquer setter, para recuperar preparo parcial.
        state.Ped.IsInvincible = true;
        state.Ped.MaxHealth = 1000; state.Ped.Health = 1000;
        if (state.Ped.MaxHealth != 1000 || state.Ped.Health != 1000 || !state.Ped.IsInvincible)
            throw Error("getters do reservatorio preARM recusados: " + state.Code);
        state.LastHealth = 1000;
        Log("EP01_BARRACO_NATIVO_RESERVA_PREARM", "Ator=" + state.Code + "; OriginalHealth=" + state.Health +
            "; OriginalMaxHealth=" + state.MaxHealth + "; PreparedHealth=1000; PreparedMaxHealth=1000; InjuryThreshold=" + N(injury) +
            "; FatalThreshold=" + N(fatal) + "; MinimumHealth=" + N(state.MinimumHealth) + "; AntesARM=True; RefillDuringAction=False");
    }
    private void RestorePreparedHealthOutsideCapture()
    {
        if (states == null) return;
        foreach (Actor state in states)
        {
            if (state == null || !state.HealthPrepared || !Owned(state)) continue;
            if (state.Ped.IsDead) throw Error("ator morreu; nao ressuscitar/fabricar recuperacao: " + state.Code);
            state.Ped.IsInvincible = true;
            state.Ped.MaxHealth = state.MaxHealth; state.Ped.Health = state.Health;
            state.Ped.IsInvincible = state.Invincible;
            if (state.Ped.MaxHealth != state.MaxHealth || state.Ped.Health != state.Health || state.Ped.IsInvincible != state.Invincible)
                throw Error("getters da restauracao preARM propria divergiram: " + state.Code);
            state.HealthPrepared = false;
            Log("EP01_BARRACO_NATIVO_RESERVA_RESTAURADA", "Ator=" + state.Code + "; OriginalHealth=" + state.Health +
                "; OriginalMaxHealth=" + state.MaxHealth + "; ForaDaAcao=True; SnapshotOriginalConferido=True");
        }
    }
    public void Cleanup() { Restore(); RestorePreparedHealthOutsideCapture(); ready = false; states = null; }
    private static void RequireActor(Actor state)
    { if (state == null || !Owned(state) || state.Ped.IsDead || state.Ped.IsInVehicle()) throw Error("identidade/vida/ownership mudou"); }
    private static bool Owned(Actor state)
    { return !System.Object.ReferenceEquals(state.Ped,null) && state.Ped.Exists() && !state.Ped.IsPlayer && state.Ped.IsPersistent && state.Ped.Handle == state.Handle && state.Ped.MemoryAddress == state.Address && state.Ped.Model.Hash == state.Model; }
    private static void RequirePed(GTA.Ped ped)
    { if (System.Object.ReferenceEquals(ped,null) || !ped.Exists() || ped.IsPlayer || !ped.IsPersistent || ped.IsDead || ped.IsInVehicle()) throw Error("apenas ator proprio vivo/persistente/a pe"); }
    private static GTA.Math.Vector3 Bone(GTA.Ped ped,GTA.Bone tag)
    {
        GTA.PedBone bone = ped.Bones[tag];
        if (System.Object.ReferenceEquals(bone,null) || !bone.IsValid) throw Error("bone corporal indisponivel: " + tag);
        GTA.Math.Vector3 value = bone.Position;
        if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("bone nao finito");
        return value;
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static string N(float value) { return value.ToString("R",System.Globalization.CultureInfo.InvariantCulture); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Barraco nativo C07: " + text + "."); }
    private void Log(string code,string text) { if (log != null) { try { log(code,text); } catch (System.Exception) { } } }
}

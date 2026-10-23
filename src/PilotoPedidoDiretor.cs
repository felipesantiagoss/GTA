// EP01_C08: movimento real na introdução e celular no fechamento. Sem novas Script classes.
public sealed class PilotoPedidoDiretor : System.IDisposable
{
    // eTempAction confirmado no catálogo nativo do CitizenFX:
    // https://raw.githubusercontent.com/citizenfx/natives/master/TASK/TaskVehicleTempAction.md
    private const int AccelerateVehicleAction = 9;
    private const int StrongBrakeVehicleAction = 19;
    private const string MotoCode = "PILOTO_MOTO", PhoneCode = "PILOTO_CELULAR";
    private const string WalkSet = "move_f@posh@", PhoneDictionary = "cellphone@female", PhoneClip = "cellphone_text_read_base";
    private readonly System.Action<string, string> log;
    private readonly ObjetosDiretor resources;
    private GTA.Ped djanira, dinjer;
    private GTA.Vehicle moto;
    private GTA.Prop phone;
    private GTA.Math.Vector3 djaniraMark, dinjerMark, stopMark, motoStart;
    private float djaniraHeading, dinjerHeading;
    private System.IntPtr djaniraAddress, dinjerAddress;
    private int djaniraHandle, dinjerHandle, djaniraModel, dinjerModel, phase, previousElapsed, routeSample;
    private int nextMotoTelemetry;
    private float previousRouteHeight;
    private bool prepared, ready, disposed, walkRequested, phoneRequested, groundReady;
    private bool resetWaitingSeat, dinjerLeaving, djaniraLeaving;
    private System.Diagnostics.Stopwatch loadingTimer;

    public PilotoPedidoDiretor(System.Action<string, string> log)
    {
        this.log = log;
        resources = new ObjetosDiretor(log);
    }

    public bool IsReady { get { return ready && !disposed; } }
    public GTA.Entity[] OwnedEntities
    {
        get
        {
            var output = new System.Collections.Generic.List<GTA.Entity>();
            if (!System.Object.ReferenceEquals(moto, null) && moto.Exists()) output.Add(moto);
            if (!System.Object.ReferenceEquals(phone, null) && phone.Exists()) output.Add(phone);
            return output.ToArray();
        }
    }
    public int[] OwnedHandles
    {
        get
        {
            var output = new System.Collections.Generic.List<int>();
            foreach (GTA.Entity entity in OwnedEntities) output.Add(entity.Handle);
            return output.ToArray();
        }
    }

    // Capturar marcas de conversa antes de colocar Dinjer no veículo; o eixo é do diálogo.
    public void Prepare(GTA.Ped djanira, GTA.Ped dinjer, GTA.Math.Vector3 anchor,
                        GTA.Math.Vector3 right, GTA.Math.Vector3 forward)
    {
        Active();
        if (prepared || resources.Count != 0) throw Error("o piloto já foi preparado; limpe antes de montar outra cena");
        RequirePed(djanira, "Djanira"); RequirePed(dinjer, "Dinjer");
        if (djanira.Handle == dinjer.Handle) throw Error("Djanira e Dinjer precisam ser atores diferentes");
        RequireVector(anchor); RequireVector(right); RequireVector(forward);
        float rightLength = HorizontalLength(right), forwardLength = HorizontalLength(forward);
        if (rightLength < 0.01f || forwardLength < 0.01f) throw Error("o eixo de montagem não está disponível");
        right = new GTA.Math.Vector3(right.X / rightLength, right.Y / rightLength, 0f);
        forward = new GTA.Math.Vector3(forward.X / forwardLength, forward.Y / forwardLength, 0f);
        if (System.Math.Abs(right.X * forward.X + right.Y * forward.Y) > 0.05f)
            throw Error("os eixos lateral/frontal precisam ser perpendiculares");
        this.djanira = djanira; this.dinjer = dinjer;
        djaniraHandle = djanira.Handle; dinjerHandle = dinjer.Handle;
        djaniraModel = djanira.Model.Hash; dinjerModel = dinjer.Model.Hash;
        djaniraAddress = djanira.MemoryAddress; dinjerAddress = dinjer.MemoryAddress;
        djaniraMark = djanira.Position; dinjerMark = dinjer.Position;
        djaniraHeading = djanira.Heading; dinjerHeading = dinjer.Heading;
        GTA.Math.Vector3 start = anchor + right * 1.1f - forward * 7f;
        motoStart = start; routeSample = 0;
        stopMark = anchor + right * 1.1f - forward * 2f;
        float heading = forward.ToHeading();
        prepared = true; ready = false; groundReady = false; phase = 0; previousElapsed = -1;
        resetWaitingSeat = false; dinjerLeaving = false; djaniraLeaving = false;
        loadingTimer = System.Diagnostics.Stopwatch.StartNew();
        // Identificadores nativos conferidos pelo modelo real antes de Request/Create.
        resources.RequestVehicle(MotoCode, "faggio", start, heading, true, true);
        resources.RequestProp(PhoneCode, "prop_npc_phone_02", djaniraMark, GTA.Math.Vector3.Zero, true);
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_SET, WalkSet); walkRequested = true;
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_ANIM_DICT, PhoneDictionary); phoneRequested = true;
        Log("PILOTO_PREPARO", "EP01_C08; moto=faggio; celular=prop_npc_phone_02; intro=12s; fechamento=4s; teste_visual_pendente=true");
    }

    // Streaming e chão são aguardados entre frames, nunca em laço bloqueante.
    public void PollLoading()
    {
        Active();
        if (!prepared) throw Error("prepare os atores do piloto antes de carregar os recursos");
        if (ready) return;
        RequireActors();
        resources.Update();
        bool walkLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_SET_LOADED, WalkSet);
        bool phoneLoaded = GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_ANIM_DICT_LOADED, PhoneDictionary);
        if (!groundReady)
        {
            GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, stopMark.X, stopMark.Y, stopMark.Z);
            using (var ground = new GTA.Native.OutputArgument())
            {
                if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_GROUND_Z_FOR_3D_COORD,
                    stopMark.X, stopMark.Y, stopMark.Z + 20f, ground, false))
                {
                    float height = ground.GetResult<float>();
                    if (!Finite(height) || System.Math.Abs(height - stopMark.Z) > 4f)
                        throw Error("a parada da moto não tem chão próximo da marca; marque um espaço plano real");
                    stopMark.Z = height;
                    groundReady = true;
                }
            }
        }
        if (groundReady && routeSample < 6)
        {
            // Conferir o percurso inteiro em pontos relativos antes de gravar, dois por frame.
            for (int budget = 0; budget < 2 && routeSample < 6; budget++)
            {
                GTA.Math.Vector3 sample = motoStart + (stopMark - motoStart) * ((float)routeSample / 5f);
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, sample.X, sample.Y, sample.Z);
                float height; GTA.Math.Vector3 normal;
                if (!GTA.World.GetGroundHeightAndNormal(sample + new GTA.Math.Vector3(0f, 0f, 5f), out height, out normal)) break;
                if (!Finite(height) || !Finite(normal.Z) || normal.Z < 0.9f || System.Math.Abs(height - sample.Z) > 4f ||
                    (routeSample > 0 && System.Math.Abs(height - previousRouteHeight) > 0.5f))
                    throw Error("a rota da moto tem desnível ou piso inadequado; marque passagem plana real para a chegada");
                using (var water = new GTA.Native.OutputArgument())
                    if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_WATER_HEIGHT, sample.X, sample.Y, sample.Z + 20f, water) &&
                        water.GetResult<float>() > height + 0.1f)
                        throw Error("a rota da moto passa por água; marque passagem seca real");
                previousRouteHeight = height; routeSample++;
            }
        }
        if (resources.IsReady && walkLoaded && phoneLoaded && groundReady && routeSample == 6)
        {
            float phoneDuration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, PhoneDictionary, PhoneClip);
            if (!Finite(phoneDuration) || phoneDuration <= 0f) throw Error("o clipe do celular não existe neste GTA");
            moto = resources.GetVehicle(MotoCode); phone = resources.GetProp(PhoneCode);
            if (System.Math.Abs(moto.Position.Z - stopMark.Z) > 3f)
                throw Error("a chegada da moto exige um caminho plano entre as duas marcas");
            ready = true;
            Log("PILOTO_RECURSOS_PRONTOS", "Streaming/chão conferidos; movimento e encaixe do telefone ainda sem teste visual");
        }
        else if (loadingTimer.ElapsedMilliseconds >= 5000)
            throw Error("moto, animação ou chão não carregaram em cinco segundos; preserve o local marcado e tente montar novamente");
    }

    // Fora da filmagem: restaurar instantaneamente a preparação e sentar apenas nossa Dinjer.
    // Main restaura as marcas/câmera antes desta chamada; nunca chamar durante a ação.
    public bool Reset()
    {
        Active(); RequireReady(); RequireActors();
        // Sentar pode ficar visível só no próximo frame. Não soltar de novo nem
        // recapturar uma marca dentro da moto enquanto essa preparação estiver pendente.
        if (resetWaitingSeat) return CompleteResetSeat();
        if (!ReleaseActorsForReset()) return false;
        // Main chamou Reset depois de restaurar/capturar suas marcas já assentadas.
        // Prepare ocorreu durante streaming e não é a referência final da física.
        djaniraMark = djanira.Position; dinjerMark = dinjer.Position;
        djaniraHeading = djanira.Heading; dinjerHeading = dinjer.Heading;
        RestoreActor(djanira, djaniraMark, djaniraHeading);
        RestoreActor(dinjer, dinjerMark, dinjerHeading);
        resources.Reset();
        moto = resources.GetVehicle(MotoCode); phone = resources.GetProp(PhoneCode);
        moto.IsPositionFrozen = true; moto.Velocity = GTA.Math.Vector3.Zero;
        moto.IsEngineRunning = true;
        dinjer.IsPositionFrozen = false;
        GTA.Native.Function.Call(GTA.Native.Hash.SET_PED_HELMET, dinjer.Handle, false);
        resetWaitingSeat = true;
        dinjer.SetIntoVehicle(moto, GTA.VehicleSeat.Driver);
        return CompleteResetSeat();
    }

    private bool CompleteResetSeat()
    {
        int driver = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_IN_VEHICLE_SEAT, moto.Handle, -1);
        if (driver != 0 && driver != dinjer.Handle)
            throw Error("há ocupante alheio no assento inicial de Dinjer; não remover ator nem veículo");
        if (!InMoto(dinjer) || driver != dinjer.Handle) return false;
        resetWaitingSeat = false;
        phase = 0; previousElapsed = -1; nextMotoTelemetry = 0;
        Log("PILOTO_REINICIADO", "Dinjer na moto; celular oculto; relógio de encenação zerado");
        return true;
    }

    // Antes do reset genérico do Diretor: liberar os atores, sem colocá-los na moto.
    public bool ReleaseActorsForReset()
    {
        Active(); RequireReady(); RequireActors();
        resetWaitingSeat = false;
        if (!RemoveOwnedOccupants()) return false;
        djanira.Task.ClearAllImmediately(); dinjer.Task.ClearAllImmediately();
        djanira.Task.ClearLookAt(); dinjer.Task.ClearLookAt();
        dinjer.ResetMovementClipSet();
        djanira.Velocity = GTA.Math.Vector3.Zero; dinjer.Velocity = GTA.Math.Vector3.Zero;
        resources.HidePhone(PhoneCode);
        phase = 0; previousElapsed = -1;
        Log("PILOTO_ATORES_LIBERADOS", "Fora da filmagem; moto sem nossos ocupantes; celular oculto");
        return true;
    }

    public void Update(int elapsedMs)
    {
        Active(); RequireReady(); RequireActors();
        if (elapsedMs < 0 || elapsedMs < previousElapsed) throw Error("o relógio voltou; reinicie a tomada antes de executar novamente");
        previousElapsed = elapsedMs;
        // Na filmagem não se usa Position/warp. Ausência de chegada cancela a tomada.
        if (phase == 0)
        {
            if (elapsedMs >= 4000 || !InMoto(dinjer)) throw Error("a introdução não começou com Dinjer na moto");
            moto.IsPositionFrozen = false; dinjer.IsPositionFrozen = false;
            moto.IsHandbrakeForcedOn = false;
            moto.MaxSpeed = 2f;
            if (!moto.IsDriveable || !moto.IsCollisionEnabled || dinjer.IsPositionFrozen)
                throw Error("a moto ou a motorista não estão fisicamente livres para a chegada. " + MotoState(elapsedMs));
            // Trajeto curto já medido no chão: a tarefa de trânsito permaneceu
            // parada nos getters. Acelerar a moto própria sem navegação rodoviária.
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_VEHICLE_TEMP_ACTION,
                dinjer.Handle, moto.Handle, AccelerateVehicleAction, 4000);
            phase = 1; Log("PILOTO_MOTO_INICIADA", "aceleracao_fisica=TA_ACCELERATE; velocidade_max=2m/s; trajeto=5m; sem teleporte na tomada");
        }
        if (phase == 1)
        {
            int driver = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_IN_VEHICLE_SEAT, moto.Handle, -1);
            if (!InMoto(dinjer) || driver != dinjer.Handle)
            {
                moto.ForwardSpeed = 0f;
                moto.Velocity = GTA.Math.Vector3.Zero;
                throw Error("Dinjer perdeu o assento de motorista; propulsão interrompida. " + MotoState(elapsedMs));
            }
            if (elapsedMs >= nextMotoTelemetry)
            {
                Log("PILOTO_MOTO_GETTERS", MotoState(elapsedMs));
                nextMotoTelemetry = elapsedMs + 1000;
            }
            bool arrived = HorizontalDistance(moto.Position, stopMark) <= 1.1f &&
                System.Math.Abs(moto.Position.Z - stopMark.Z) <= 1f;
            if (arrived)
            {
                Log("PILOTO_MOTO_MARCA_ALCANCADA", MotoState(elapsedMs));
                GTA.Native.Function.Call(GTA.Native.Hash.TASK_VEHICLE_TEMP_ACTION,
                    dinjer.Handle, moto.Handle, StrongBrakeVehicleAction, 250);
                moto.ForwardSpeed = 0f;
                moto.Velocity = GTA.Math.Vector3.Zero;
                dinjer.Task.LeaveVehicle(moto, GTA.LeaveVehicleFlags.DontDefaultWarpIfDoorBlocked | GTA.LeaveVehicleFlags.FromLeftSide);
                phase = 2; Log("PILOTO_DESCIDA_INICIADA", "saída física da moto depois da chegada medida; warp desativado");
            }
            else if (elapsedMs >= 4000)
                throw Error("a moto não alcançou a marca em quatro segundos; sem teleporte. " + MotoState(elapsedMs));
            else
            {
                // Velocidade é física; posição/rotação nunca são interpoladas.
                // Rampa curta limita arrancada e impede aceleração excessiva.
                float speed = 2f * System.Math.Min(1f, elapsedMs / 500f);
                moto.ForwardSpeed = speed;
            }
        }
        if (phase == 2 && elapsedMs >= 7000)
        {
            if (dinjer.IsInVehicle()) throw Error("Dinjer não terminou de descer em três segundos; tomada interrompida");
            moto.Velocity = GTA.Math.Vector3.Zero; moto.IsPositionFrozen = true;
            dinjer.SetMovementClipSet(new GTA.ClipSet(WalkSet));
            dinjer.Task.GoStraightTo(dinjerMark, -1, GTA.PedMoveBlendRatio.Walk, dinjerHeading, 0.15f);
            phase = 3; Log("PILOTO_CAMINHADA_INICIADA", "move_f@posh@; timeout=-1 impede teleporte de tarefa; chegada antes12s");
        }
        if (phase == 3)
        {
            bool arrived = HorizontalDistance(dinjer.Position, dinjerMark) <= 0.25f && System.Math.Abs(dinjer.Position.Z - dinjerMark.Z) <= 0.8f;
            if (arrived)
            {
                dinjer.Task.ClearAllImmediately(); dinjer.Velocity = GTA.Math.Vector3.Zero;
                dinjer.Heading = dinjerHeading; dinjer.IsPositionFrozen = true;
                dinjer.Task.StandStill(-1);
                phase = 4; Log("PILOTO_MARCA_ALCANCADA", "posição real=" + dinjer.Position + "; sem reposicionamento na ação");
            }
            else if (elapsedMs >= 12000)
                throw Error("Dinjer não alcançou a marca antes da narração; a tomada foi interrompida sem teleporte");
        }
        if (elapsedMs >= 12000 && phase < 4) throw Error("a introdução física não terminou antes da narração");
        if (phase == 4 && elapsedMs >= 42000)
        {
            resources.AttachPhone(PhoneCode, djanira);
            GTA.Native.Function.Call(GTA.Native.Hash.TASK_PLAY_ANIM, djanira.Handle, PhoneDictionary, PhoneClip,
                4f, -4f, -1, (int)(GTA.AnimationFlags.Loop | GTA.AnimationFlags.UpperBodyOnly | GTA.AnimationFlags.Secondary), 0f, false, false, false);
            phase = 5; Log("PILOTO_CELULAR_INICIADO", "cellphone@female/cellphone_text_read_base; boca desligada pelo bloco Pausa");
        }
        // Conservar o último quadro e o celular após46s, até reiniciar/limpar.
    }

    public void Cleanup()
    {
        if (disposed) return;
        ready = false;
        resetWaitingSeat = false;
        if (!System.Object.ReferenceEquals(moto, null) && moto.Exists())
        {
            moto = resources.GetVehicle(MotoCode); // validar endereço/modelo antes de tocar nos ocupantes.
            if (!RemoveOwnedOccupants()) throw Error("a saída da moto ainda está pendente; preserve recursos/atores e tente limpar no próximo frame");
        }
        if (ActorStillOwned(dinjer, dinjerHandle, dinjerAddress))
        {
            dinjer.Task.ClearAllImmediately(); dinjer.Task.ClearLookAt(); dinjer.ResetMovementClipSet();
        }
        if (ActorStillOwned(djanira, djaniraHandle, djaniraAddress)) djanira.Task.ClearSecondary();
        resources.Cleanup(); // recusa apagar qualquer veículo ainda ocupado.
        if (walkRequested) { GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_SET, WalkSet); walkRequested = false; }
        if (phoneRequested) { GTA.Native.Function.Call(GTA.Native.Hash.REMOVE_ANIM_DICT, PhoneDictionary); phoneRequested = false; }
        prepared = false; groundReady = false; phase = 0; previousElapsed = -1;
        dinjerLeaving = false; djaniraLeaving = false;
        moto = null; phone = null; djanira = null; dinjer = null;
        djaniraHandle = 0; dinjerHandle = 0; djaniraAddress = System.IntPtr.Zero; dinjerAddress = System.IntPtr.Zero;
        Log("PILOTO_LIMPO", "Somente moto/celular próprios removidos; atores continuam sob responsabilidade do Diretor");
    }
    public void Dispose() { Cleanup(); resources.Dispose(); disposed = true; }

    private bool RemoveOwnedOccupants()
    {
        if (System.Object.ReferenceEquals(moto, null) || !moto.Exists()) return true;
        // Segurança: primeiro conferir todos os ocupantes, antes de modificar um deles.
        foreach (GTA.Ped occupant in moto.Occupants)
            if (!ActorStillOwned(occupant, dinjerHandle, dinjerAddress) && !ActorStillOwned(occupant, djaniraHandle, djaniraAddress))
                throw Error("há ocupante alheio na moto própria; não remover ator nem veículo");
        bool first = RemoveFromMoto(dinjer, dinjerHandle, dinjerAddress, dinjerMark, dinjerHeading, ref dinjerLeaving);
        bool second = RemoveFromMoto(djanira, djaniraHandle, djaniraAddress, djaniraMark, djaniraHeading, ref djaniraLeaving);
        return first && second && moto.Occupants.Length == 0;
    }
    private bool RemoveFromMoto(GTA.Ped actor, int handle, System.IntPtr address, GTA.Math.Vector3 mark, float heading, ref bool leaving)
    {
        if (!ActorStillOwned(actor, handle, address)) { leaving = false; return true; }
        if (!InMoto(actor) && !leaving) return true;
        if (!leaving)
        {
            actor.IsPositionFrozen = false;
            actor.Task.ClearAllImmediately();
            actor.Task.WarpOutOfVehicle(moto); // permitido somente Reset/Cleanup fora da filmagem.
            leaving = true;
        }
        // Se o motor adiar a saída, manter a tarefa para o próximo frame. Não a cancelar.
        if (InMoto(actor)) return false;
        actor.Task.ClearAllImmediately();
        actor.Velocity = GTA.Math.Vector3.Zero;
        actor.PositionNoOffset = mark;
        actor.Heading = heading;
        leaving = false;
        return true;
    }
    private static void RestoreActor(GTA.Ped actor, GTA.Math.Vector3 mark, float heading)
    {
        actor.IsPositionFrozen = true; actor.Velocity = GTA.Math.Vector3.Zero;
        actor.PositionNoOffset = mark; actor.Heading = heading; actor.Task.StandStill(-1);
    }
    private bool InMoto(GTA.Ped actor)
    { return GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_IN_VEHICLE, actor.Handle, moto.Handle, false); }
    private void RequireActors()
    {
        if (!ActorStillOwned(djanira, djaniraHandle, djaniraAddress) || !ActorStillOwned(dinjer, dinjerHandle, dinjerAddress) ||
            djanira.Model.Hash != djaniraModel || dinjer.Model.Hash != dinjerModel || djanira.IsDead || dinjer.IsDead)
            throw Error("a identidade dos atores mudou; montar novamente antes de executar");
    }
    private static bool ActorStillOwned(GTA.Ped actor, int handle, System.IntPtr address)
    {
        if (handle == 0 || System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.Handle != handle || actor.MemoryAddress != address) return false;
        GTA.Ped player = GTA.Game.Player.Character;
        return System.Object.ReferenceEquals(player, null) || actor.Handle != player.Handle;
    }
    private static void RequirePed(GTA.Ped actor, string name)
    {
        if (System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.IsDead || actor.IsInVehicle() || !actor.IsPersistent)
            throw Error(name + " precisa ser um ator vivo, a pé, na marca de conversa");
        GTA.Ped player = GTA.Game.Player.Character;
        if (!System.Object.ReferenceEquals(player, null) && actor.Handle == player.Handle) throw Error("o jogador não pode ser ator do piloto");
    }
    private static float HorizontalLength(GTA.Math.Vector3 value)
    { return (float)System.Math.Sqrt(value.X * value.X + value.Y * value.Y); }
    private string MotoState(int elapsedMs)
    {
        return "tempo_ms=" + elapsedMs + "; inicio=" + motoStart + "; atual=" + moto.Position + "; alvo=" + stopMark +
            "; distanciaXY=" + HorizontalDistance(moto.Position, stopMark) + "; deslocamentoXY=" + HorizontalDistance(moto.Position, motoStart) +
            "; velocidade=" + moto.Speed + "; motor=" + moto.IsEngineRunning + "; congelada=" + moto.IsPositionFrozen +
            "; colisao=" + moto.IsCollisionEnabled + "; dirigivel=" + moto.IsDriveable + "; rodas=" + moto.WheelSpeed +
            "; gtaPausado=" + GTA.Game.IsPaused + "; gtaPrimeiroPlano=" + AtalhoGravacaoDiretor.IsGameForeground +
            "; dinjerCongelada=" + dinjer.IsPositionFrozen + "; dinjerNaMoto=" + InMoto(dinjer) + "; motorista=" +
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_IN_VEHICLE_SEAT, moto.Handle, -1) + "; dinjer=" + dinjer.Handle;
    }

    private static float HorizontalDistance(GTA.Math.Vector3 first, GTA.Math.Vector3 second)
    { return HorizontalLength(first - second); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void RequireVector(GTA.Math.Vector3 value)
    { if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("a posição/eixo precisa ter números finitos"); }
    private void RequireReady()
    {
        if (!IsReady) throw Error("aguarde todos os recursos do piloto carregarem");
        moto = resources.GetVehicle(MotoCode); phone = resources.GetProp(PhoneCode);
    }
    private void Active() { if (disposed) throw Error("o helper do piloto foi encerrado"); }
    private static System.InvalidOperationException Error(string message) { return new System.InvalidOperationException("EP01_C08: " + message + "."); }
    private void Log(string code, string message)
    { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
}

// Concatenado ao .3.cs publicado: não adicionar diretivas using neste arquivo.
// Enquadramento horizontal 16:9; todos os valores são pontos de partida para teste visual.
public sealed class CameraDiretor : System.IDisposable
{
    private readonly System.Action<string, string> log;
    private GTA.Ped actorA, actorB;
    private readonly System.Collections.Generic.List<GTA.Ped> actors = new System.Collections.Generic.List<GTA.Ped>();
    private bool hasAxis;
    private GTA.Math.Vector3 environmentForward;
    private GTA.Camera camera, previousCamera;
    private GTA.Math.Vector3 axisOrigin, axisNormal;
    private float axisSide = 1f;
    private bool initialized, free, restorePending, tween;
    private bool opticalMedium;
    private bool witnessC2Known;
    private bool witnessC2PosePending, witnessC2PoseApplied, witnessC2ActiveVerified;
    private uint witnessC2RequestedFrame, witnessC2RequestedTime;
    private int witnessC2PendingCameraHandle;
    private System.IntPtr witnessC2PendingCameraAddress;
    private GTA.Ped witnessC2Target;
    private ObjetosSerieDiretor witnessC2Objects;
    private GTA.Math.Vector3 witnessC2Position, witnessC2Rotation, witnessC2ActorPosition, witnessC2AxisOrigin, witnessC2AxisNormal;
    private float witnessC2ActorHeading, witnessC2AxisSide, witnessC2Side, witnessC2Height, witnessC2Closer;
    private int witnessC2Handle, witnessC2Model;
    private System.IntPtr witnessC2Address;
    private readonly int[] witnessC2PropHandles = new int[3];
    private readonly int[] witnessC2PropModels = new int[3];
    private readonly System.IntPtr[] witnessC2PropAddresses = new System.IntPtr[3];
    private static readonly string[] WitnessC2Props = new string[] { "CONTAINER_CYPRESS", "CAIXAS_NEGOCIO_1", "CAIXAS_NEGOCIO_2" };
    // Main habilita FocusOwnActorFromBehind exclusivamente EP11_C01.
    // Guardar apenas a pose nativa inicial, nunca recalcular pelo ator que caminha ate a porta.
    private bool ownBehindFixedKnown;
    private GTA.Math.Vector3 ownBehindFixedPosition, ownBehindFixedRotation, ownBehindFixedAxisOrigin, ownBehindFixedAxisNormal;
    private float ownBehindFixedFov, ownBehindFixedAxisSide, ownBehindFixedSide, ownBehindFixedHeight, ownBehindFixedCloser;
    private int ownBehindFixedActorHandle, ownBehindFixedActorModel;
    private System.IntPtr ownBehindFixedActorAddress;
    // Somente o wrapper EP11_C03 arma esta flag durante uma unica reaplicacao.
    private bool luxuryWalkFrame;
    private string luxuryWalkLoggedPreset = "";
    private bool writingC6CloseKnown, writingC6CloseVerified;
    private GTA.Math.Vector3 writingC6Position, writingC6Rotation;
    private int writingC6ActorHandle; private System.IntPtr writingC6ActorAddress;
    private uint writingC6Frame, writingC6Time;
    private bool handoffCloseKnown;
    private GTA.Math.Vector3 handoffClosePosition, handoffCloseCenter, handoffCloseAxisOrigin, handoffCloseAxisNormal;
    private float handoffCloseFov, handoffCloseElevation, handoffCloseAxisSide;
    private int handoffClosePropHandle, handoffCloseDonorHandle, handoffCloseReceiverHandle;
    private System.IntPtr handoffClosePropAddress, handoffCloseDonorAddress, handoffCloseReceiverAddress;
    private GTA.Math.Vector3 fromPosition, toPosition, fromRotation, toRotation;
    private float fromFov, toFov, tweenElapsed;
    private const float TweenSeconds = 0.75f;
    private const float AxisMargin = 0.12f;
    private const float AspectRatio = 16f / 9f;
    private static readonly GTA.Input.ControlAction[] FreeControls = new GTA.Input.ControlAction[] {
        GTA.Input.ControlAction.MoveLeftRight, GTA.Input.ControlAction.MoveUpDown,
        GTA.Input.ControlAction.LookLeftRight, GTA.Input.ControlAction.LookUpDown,
        GTA.Input.ControlAction.Jump, GTA.Input.ControlAction.Duck, GTA.Input.ControlAction.Sprint,
        GTA.Input.ControlAction.Attack, GTA.Input.ControlAction.Aim,
        GTA.Input.ControlAction.Cover, GTA.Input.ControlAction.Context };

    public CameraDiretor(System.Action<string, string> log)
    {
        this.log = log;
        CurrentPreset = "";
        RestoredPoseName = "";
    }

    // Main usa somente C04 e apenas para conferir identidade da camera propria; nenhum setter.
    public int RequireRoomC4OwnCameraHandle(string sceneId, GTA.Ped actor)
    {
        if (sceneId != "EP11_C04" || !initialized || System.Object.ReferenceEquals(camera, null) || !camera.Exists() ||
            System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.IsPlayer || actors.Count != 1 ||
            actors[0].Handle != actor.Handle || actors[0].MemoryAddress != actor.MemoryAddress)
            throw new System.InvalidOperationException("C04 room: identidade da camera/ator proprios nao confere.");
        return camera.Handle;
    }

    public bool IsInitialized { get { return initialized; } }
    public bool IsFree { get { return free; } }
    public bool IsGalpaoWitnessPosePending { get { return witnessC2PosePending; } }
    public string CurrentPreset { get; private set; }
    public string RestoredPoseName { get; private set; }
    public bool FocusOwnActorFromBehind { get; set; }

    public void Initialize(GTA.Ped a, GTA.Ped b, bool invertAxis)
    {
        RequirePed(a);
        RequirePed(b);
        Cleanup();
        GTA.Math.Vector3 separation = b.Position - a.Position;
        if (HorizontalLength(separation) < 0.5f)
            throw new System.InvalidOperationException("Câmera: os dois personagens estão próximos demais para definir o eixo.");
        actorA = a;
        actorB = b;
        actors.Add(a);
        actors.Add(b);
        hasAxis = true;
        axisOrigin = (a.Position + b.Position) * 0.5f;
        GTA.Math.Vector3 direction = HorizontalUnit(separation);
        axisNormal = new GTA.Math.Vector3(-direction.Y, direction.X, 0f);
        axisSide = 1f;
        GTA.Ped player = GTA.Game.Player.Character;
        float playerProjection = 0f;
        bool hasPlayer = !System.Object.ReferenceEquals(player, null) && player.Exists();
        if (hasPlayer) playerProjection = Dot(player.Position - axisOrigin, axisNormal);
        GTA.Math.Vector3 actorForward = HorizontalUnit(a.GetOffsetPosition(new GTA.Math.Vector3(0f, 1f, 0f)) - a.Position);
        float actorProjection = Dot(actorForward, axisNormal);
        bool useActorDirection = !hasPlayer || System.Math.Abs(playerProjection) <= AxisMargin;
        GTA.Math.Vector3 actorRight = new GTA.Math.Vector3(actorForward.Y, -actorForward.X, 0f);
        float actorRightProjection = Dot(actorRight, axisNormal);
        bool useActorRight = useActorDirection && System.Math.Abs(actorProjection) <= AxisMargin;
        float sideProjection = useActorDirection ? (useActorRight ? -actorRightProjection : actorProjection) : playerProjection;
        axisSide = sideProjection < 0f ? -1f : 1f;
        Log("CAMERA_EIXO_LADO_MEDIDO", "PlayerProjection=" + Number(playerProjection) +
            "; ActorForwardProjection=" + Number(actorProjection) + "; PlayerOnAxis=" + useActorDirection +
            "; ActorRightProjection=" + Number(actorRightProjection) + "; ActorLeftFallback=" + useActorRight +
            "; SideProjection=" + Number(sideProjection) + "; AxisMargin=" + Number(AxisMargin) + "; BaseSide=" + Number(axisSide));
        if (invertAxis)
            axisSide = -axisSide;
        previousCamera = GTA.ScriptCameraDirector.RenderingCam;
        try
        {
            GTA.Math.Vector3 initialPosition = KeepSide(axisOrigin + axisNormal * (axisSide * 4.5f), true);
            camera = GTA.Camera.Create(GTA.ScriptedCameraNameHash.DefaultScriptedCamera,
                initialPosition, new GTA.Math.Vector3(0f, 0f, 0f), 45f, false, GTA.EulerRotationOrder.YXZ);
            if (System.Object.ReferenceEquals(camera, null) || !camera.Exists())
                throw new System.InvalidOperationException("Câmera: o jogo não conseguiu criar a câmera do Diretor.");
            restorePending = true;
            initialized = true;
            camera.NearClip = 0.04f;
            ApplyPreset("DOIS", a, 0f, 0f, 0f, false);
            camera.IsActive = true;
            GTA.ScriptCameraDirector.StartRendering();
            Log("CAMERA_INICIADA", "Eixo definido; lado=" + Number(axisSide) + "; formato=16:9.");
        }
        catch
        {
            Cleanup();
            throw;
        }
    }

    // Solos, conjuntos e planos sem ator usam a mesma câmera própria. O eixo da
    // dupla permanece fixo nos conjuntos, inclusive quando o alvo é o terceiro ator.
    public void Initialize(System.Collections.Generic.IList<GTA.Ped> cast,
        GTA.Math.Vector3 origin, GTA.Math.Vector3 forward, bool invertAxis)
    {
        if (System.Object.ReferenceEquals(cast, null) || cast.Count > 5)
            throw new System.InvalidOperationException("Câmera: use de zero a cinco atores.");
        foreach (GTA.Ped ped in cast) RequirePed(ped);
        if (cast.Count >= 2)
        {
            Initialize(cast[0], cast[1], invertAxis);
            for (int i = 2; i < cast.Count; i++) actors.Add(cast[i]);
            ApplyPreset("LONGE", cast[0], 0f, 0f, 0f, false);
            return;
        }
        Cleanup();
        environmentForward = HorizontalUnit(forward);
        axisOrigin = cast.Count == 0 ? origin : cast[0].Position;
        axisNormal = new GTA.Math.Vector3(-environmentForward.Y, environmentForward.X, 0f);
        axisSide = invertAxis ? -1f : 1f;
        hasAxis = false;
        if (cast.Count == 1) { actorA = cast[0]; actors.Add(cast[0]); }
        previousCamera = GTA.ScriptCameraDirector.RenderingCam;
        try
        {
            camera = GTA.Camera.Create(GTA.ScriptedCameraNameHash.DefaultScriptedCamera,
                axisOrigin - environmentForward * 10f + new GTA.Math.Vector3(0f, 0f, 1.8f),
                new GTA.Math.Vector3(0f, 0f, 0f), 50f, false, GTA.EulerRotationOrder.YXZ);
            if (System.Object.ReferenceEquals(camera, null) || !camera.Exists())
                throw new System.InvalidOperationException("Câmera: o jogo não conseguiu criar a câmera.");
            restorePending = true;
            initialized = true;
            camera.NearClip = 0.04f;
            if (cast.Count == 0) ApplyEnvironmentPreset("LONGE", 0f, 0f, 0f, false);
            else ApplyPreset("LONGE", cast[0], 0f, 0f, 0f, false);
            camera.IsActive = true;
            GTA.ScriptCameraDirector.StartRendering();
            Log("CAMERA_INICIADA", "Atores=" + cast.Count + "; formato=16:9.");
        }
        catch { Cleanup(); throw; }
    }

    // Entrada de hospital/terminal: fachada acima das pessoas e caminho visivel desde a rua.
    public void ApplyArrivalWide(GTA.Math.Vector3 origin, GTA.Math.Vector3 forward, bool smooth)
    {
        RequireCamera();
        GTA.Math.Vector3 direction = HorizontalUnit(forward);
        GTA.Math.Vector3 position = origin - direction * 10f + new GTA.Math.Vector3(0f, 0f, 2.6f);
        GTA.Math.Vector3 aim = origin + direction * 3f + new GTA.Math.Vector3(0f, 0f, 2f);
        // O conjunto mantem o lado da primeira camera, escolhido na montagem.
        position = KeepSide(position, true);
        SetDestination(position, LookRotation(aim - position), 55f, smooth);
        CurrentPreset = "LONGE";
        Log("CAMERA_CHEGADA_FACHADA", "Origem=" + origin + "; Direcao=" + direction + "; Camera=" + position + "; FOV=55");
    }

    public void ApplyEnvironmentPreset(string preset, float side, float height,
        float closer, bool smooth)
    {
        RequireCamera();
        if (actors.Count != 0)
            throw new System.InvalidOperationException("Câmera: plano de ambiente exige elenco vazio.");
        RequireAdjustment(side, "lateral"); RequireAdjustment(height, "altura"); RequireAdjustment(closer, "aproximação");
        string name = (preset ?? "").Trim().ToUpperInvariant();
        float distance, elevation, fov;
        switch (name)
        {
            case "LONGE": distance = 10f; elevation = 1.8f; fov = 50f; break;
            case "MEDIA": distance = 5f; elevation = 1.6f; fov = 40f; break;
            case "BAIXA": distance = 5f; elevation = 0.4f; fov = 35f; break;
            case "ALTA": distance = 6f; elevation = 5f; fov = 45f; break;
            default: throw new System.InvalidOperationException("Câmera: ambiente sem ator aceita LONGE, MEDIA, BAIXA ou ALTA.");
        }
        GTA.Math.Vector3 right = new GTA.Math.Vector3(environmentForward.Y, -environmentForward.X, 0f);
        GTA.Math.Vector3 position = axisOrigin - environmentForward * System.Math.Max(0.35f, distance - closer)
            + right * side + new GTA.Math.Vector3(0f, 0f, elevation + height);
        GTA.Math.Vector3 aim = axisOrigin + environmentForward * 5f + new GTA.Math.Vector3(0f, 0f, 1.2f);
        SetDestination(position, LookRotation(aim - position), fov, smooth);
        CurrentPreset = name;
        Log("CAMERA_AMBIENTE", "Plano=" + name + "; posição=" + position);
    }

    // Ajustes em metros: lateral relativa ao ator, altura sobre o preset,
    // aproximar positivo reduz a distância. Presets e ajustes espelham o lado incorreto do eixo.
    public void ApplyPreset(string preset, GTA.Ped target, float deslocLado, float altura,
        float aproximar, bool smooth)
    {
        RequireCamera();
        RequirePed(target);
        if (witnessC2Known) witnessC2PoseApplied = false;
        if (!actors.Exists(delegate(GTA.Ped actor) { return actor.Handle == target.Handle; }))
            throw new System.InvalidOperationException("Câmera: escolha um personagem do elenco da cena.");
        RequireAdjustment(deslocLado, "deslocamento lateral");
        RequireAdjustment(altura, "altura");
        RequireAdjustment(aproximar, "aproximação");
        string name = (preset ?? "").Trim().ToUpperInvariant();
        float distance, height, fov;
        switch (name)
        {
            case "LONGE": distance = 10f; height = 1.8f; fov = 50f; break;
            case "DOIS": distance = 4.5f; height = 1.6f; fov = 45f; break;
            case "MEDIA": distance = 3.5f; height = 1.6f; fov = 40f; break;
            case "PERTO": distance = 1.5f; height = 1.65f; fov = 30f; break;
            case "BAIXA": distance = 2.5f; height = 0.4f; fov = 35f; break;
            case "ALTA": distance = 3.5f; height = 3.5f; fov = 35f; break;
            case "ATRAS": distance = 1.5f; height = 1.7f; fov = 35f; break;
            case "OLHOS": distance = 0.2f; height = 0f; fov = 45f; break;
            default: throw new System.InvalidOperationException("Câmera: plano desconhecido: " + name + ".");
        }
        if (name == "ATRAS" && FocusOwnActorFromBehind && ownBehindFixedKnown)
        {
            if (target.Handle != ownBehindFixedActorHandle || target.MemoryAddress != ownBehindFixedActorAddress ||
                target.Model.Hash != ownBehindFixedActorModel || deslocLado != ownBehindFixedSide ||
                altura != ownBehindFixedHeight || aproximar != ownBehindFixedCloser)
                throw new System.InvalidOperationException("Camera banco fixa: ator/ajustes mudaram; remontar sem reutilizar pose de outro ator.");
            RequireOwnBehindFixedAxis();
            SetDestination(ownBehindFixedPosition, ownBehindFixedRotation, ownBehindFixedFov, false);
            CurrentPreset = "ATRAS"; opticalMedium = false;
            RequireOwnBehindFixedPose();
            Log("CAMERA_BANCO_ATRAS_FIXA", "Fase=reaplicar; Alvo=" + target.Handle + "; Posicao=" + camera.Position +
                "; Rotacao=" + camera.Rotation + "; FOV=" + Number(camera.FieldOfView) +
                "; EixoPreservado=True; SegueAtor=False; Suave=False; GettersConfirmados=True; OclusaoVisual=pendente");
            return; // Antes de qualquer getter de posicao/direcao/cabeca do ator em movimento.
        }
        if (name == "ATRAS" && FocusOwnActorFromBehind && smooth)
            throw new System.InvalidOperationException("Camera banco fixa: primeiro quadro exige aplicacao imediata sem interpolacao.");
        if (luxuryWalkFrame && name == "BAIXA")
        {
            distance = 1.5f; height = 0.35f; fov = 35f;
            GTA.PedBone left = target.Bones[GTA.Bone.SkelLeftFoot], right = target.Bones[GTA.Bone.SkelRightFoot];
            if (!left.IsValid || !right.IsValid || !PlausibleBone(target.Position, left.Position) || !PlausibleBone(target.Position, right.Position))
                throw new System.InvalidOperationException("Camera C3 BAIXA: dois pes proprios reais devem estar validos.");
        }
        GTA.Math.Vector3 actorForward = HorizontalUnit(target.GetOffsetPosition(
            new GTA.Math.Vector3(0f, 1f, 0f)) - target.Position);
        GTA.Math.Vector3 actorRight = new GTA.Math.Vector3(actorForward.Y, -actorForward.X, 0f);
        float lateralSign = Dot(actorRight, axisNormal) * axisSide < 0f ? -1f : 1f;
        GTA.Math.Vector3 foot = FootPosition(target);
        GTA.Math.Vector3 aim = luxuryWalkFrame && name == "BAIXA" ?
            foot + new GTA.Math.Vector3(0f, 0f, 0.15f) : HeadPosition(target) - new GTA.Math.Vector3(0f, 0f, 0.08f);
        GTA.Math.Vector3 position;
        distance = System.Math.Max(0.35f, distance - aproximar);
        if (name == "DOIS" || name == "LONGE")
        {
            // A dupla fica no mesmo plano; a direção do ator inclina levemente a vista de conjunto.
            GTA.Math.Vector3 center = new GTA.Math.Vector3(0f, 0f, 0f);
            aim = center;
            foreach (GTA.Ped actor in actors) { center += FootPosition(actor); aim += HeadPosition(actor); }
            center *= 1f / actors.Count; aim *= 1f / actors.Count;
            if (actors.Count > 2)
            {
                float radius = 0f;
                foreach (GTA.Ped actor in actors) radius = System.Math.Max(radius, HorizontalLength(actor.Position - center));
                float halfAngle = (float)System.Math.Tan(fov * System.Math.PI / 360.0);
                distance = System.Math.Max(distance, (radius + 0.7f) / (halfAngle * AspectRatio) + radius);
            }
            GTA.Math.Vector3 groupDirection = hasAxis ? HorizontalUnit(axisNormal * axisSide + actorForward * 0.25f) : actorForward;
            position = center + groupDirection * distance + new GTA.Math.Vector3(0f, 0f, height);
            aim -= new GTA.Math.Vector3(0f, 0f, 0.15f);
        }
        else if (name == "ATRAS")
        {
            position = foot - actorForward * distance + actorRight * (0.6f * lateralSign)
                + new GTA.Math.Vector3(0f, 0f, height);
            if (FocusOwnActorFromBehind)
                aim = HeadPosition(target) - new GTA.Math.Vector3(0f, 0f, 0.08f);
            else
            {
                GTA.Ped other = target.Handle == actorA.Handle ? actorB : actorA;
                aim = System.Object.ReferenceEquals(other, null) ? HeadPosition(target) + actorForward * 10f : HeadPosition(other);
            }
        }
        else if (name == "OLHOS")
        {
            // Olhos do ator, nunca do jogador. A margem lateral de 12 cm preserva o eixo;
            // o mínimo frontal de 15 cm impede que a aproximação passe para trás da cabeça.
            position = HeadPosition(target) + actorForward * System.Math.Max(0.15f, 0.2f - aproximar);
            aim = position + actorForward * 10f;
        }
        else
        {
            // Vista de três quartos, com o tronco voltado para a lente e sem ficar sobre o eixo.
            position = foot + actorForward * (distance * 0.8660254f)
                + actorRight * (distance * 0.5f * lateralSign) + new GTA.Math.Vector3(0f, 0f, height);
        }
        position += actorRight * deslocLado + new GTA.Math.Vector3(0f, 0f, altura);
        position = KeepSide(position, true);
        if (name != "DOIS" && name != "LONGE" && name != "OLHOS" && !(luxuryWalkFrame && name == "BAIXA"))
            aim = ComposeThird(position, aim, actorForward, fov);
        GTA.Math.Vector3 rotation = LookRotation(aim - position);
        SetDestination(position, rotation, fov, smooth);
        CurrentPreset = name;
        opticalMedium = false;
        if (name == "ATRAS" && FocusOwnActorFromBehind)
        {
            // Getters reais depois de SetDestination(false): nao usar XYZ publicado nem alias interior.
            ownBehindFixedPosition = camera.Position; ownBehindFixedRotation = camera.Rotation; ownBehindFixedFov = camera.FieldOfView;
            ownBehindFixedAxisOrigin = axisOrigin; ownBehindFixedAxisNormal = axisNormal; ownBehindFixedAxisSide = axisSide;
            ownBehindFixedActorHandle = target.Handle; ownBehindFixedActorAddress = target.MemoryAddress; ownBehindFixedActorModel = target.Model.Hash;
            ownBehindFixedSide = deslocLado; ownBehindFixedHeight = altura; ownBehindFixedCloser = aproximar;
            ownBehindFixedKnown = true;
            RequireOwnBehindFixedAxis(); RequireOwnBehindFixedPose();
            Log("CAMERA_BANCO_ATRAS_FIXA", "Fase=inicial; Alvo=" + target.Handle + "; Posicao=" + ownBehindFixedPosition +
                "; Rotacao=" + ownBehindFixedRotation + "; FOV=" + Number(ownBehindFixedFov) + "; Lado=" + Number(axisSide) +
                "; EixoPreservado=True; SegueAtor=False; Suave=False; GettersConfirmados=True; OclusaoVisual=pendente");
        }
        if (luxuryWalkFrame)
        {
            if (luxuryWalkLoggedPreset != name)
            {
                luxuryWalkLoggedPreset = name;
                Log("CAMERA_C3_ACOMPANHAMENTO", "Cena=EP11_C03; Plano=" + name + "; Alvo=" + target.Handle +
                    "; PosicaoPed=" + target.Position + "; Pe=" + foot + "; Aim=" + aim + "; DestinoCamera=" + position +
                    "; FOV=" + Number(fov) + "; SegueAtor=True; ReaplicaPorFrame=True; Suave=False; EixoAlterado=False;" +
                    " GettersPoseConfirmados=False; VisibilidadeVisual=pendente");
            }
        }
        else
            Log("CAMERA_PLANO", "Plano=" + name + "; alvo=" + target.Handle + "; suave=" + smooth +
                "; posição=" + position + "; FOV=" + Number(fov) +
                "; ATRASAlvoProprio=" + (name == "ATRAS" && FocusOwnActorFromBehind));
    }

    // Nao guarda nova marca/eixo. Usa os mesmos presets relativos ao ator atual,
    // e baixa exclusivamente C3 mira os dois pes/saltos em vez da cabeca.
    public void ApplyLuxuryWalkFollow(string sceneId, string localId, string actorId,
        string preset, GTA.Ped target, float side, float height, float closer)
    {
        RequireCamera(); RequirePed(target);
        if (sceneId != "EP11_C03" || localId != "MANSAO_DJANIRA" || actorId != "djanira_rica" ||
            actors.Count != 1 || actors[0].Handle != target.Handle || actors[0].MemoryAddress != target.MemoryAddress ||
            hasAxis || FocusOwnActorFromBehind || witnessC2Known || RestoredPoseName.Length > 0 || free ||
            (preset != "LONGE" && preset != "MEDIA" && preset != "BAIXA" && preset != "PERTO"))
            throw new System.InvalidOperationException("Camera C3: contexto/ator/preset exclusivo mudou; nao seguir outra cena.");
        if (luxuryWalkFrame) throw new System.InvalidOperationException("Camera C3: reaplicacao recursiva recusada.");
        luxuryWalkFrame = true;
        try { ApplyPreset(preset, target, side, height, closer, false); }
        finally { luxuryWalkFrame = false; }
    }

    // C2: testemunha silenciosa fora do eixo do dialogo. Plano PARALELO passando
    // pelo proprio ator; normal/lado globais ficam intactos para LONGE/dialogo.
    // Nenhum XYZ publicado, setter de ator/prop ou relaxamento de colisao.
    public void PrepareGalpaoWitnessClose(string sceneId, string localId, string actorId,
        GTA.Ped target, ObjetosSerieDiretor objects, float side, float height, float closer)
    {
        RequireGalpaoWitnessScope(sceneId, localId, actorId, target, objects);
        RequireAdjustment(side, "lateral testemunha"); RequireAdjustment(height, "altura testemunha"); RequireAdjustment(closer, "aproximacao testemunha");
        if (System.Math.Abs(side) > 2f || System.Math.Abs(height) > 2f || System.Math.Abs(closer) > 2f)
            throw new System.InvalidOperationException("Camera C2 testemunha: ajustes excedem contrato JSON2m.");
        GTA.Math.Vector3 foot = FootPosition(target), head = HeadPosition(target);
        GTA.Math.Vector3 direction = HorizontalUnit(target.GetOffsetPosition(new GTA.Math.Vector3(0f, 1f, 0f)) - target.Position);
        GTA.Math.Vector3 right = new GTA.Math.Vector3(direction.Y, -direction.X, 0f);
        float lateralSign = Dot(right, axisNormal) * axisSide < 0f ? -1f : 1f;
        float distance = System.Math.Max(0.35f, 1.5f - closer);
        GTA.Math.Vector3 raw = foot + direction * (distance * 0.8660254f) + right * (distance * 0.5f * lateralSign + side) +
            new GTA.Math.Vector3(0f, 0f, 1.65f + height);
        GTA.Math.Vector3 position = raw;
        float projection = Dot(position - foot, axisNormal) * axisSide;
        if (projection < 0f) { position += axisNormal * (axisSide * -2f * projection); projection = -projection; }
        if (projection < AxisMargin) position += axisNormal * (axisSide * (AxisMargin - projection));
        if (!IsFinite(Length(position - head)) || Length(position - head) > 4f ||
            Dot(position - foot, axisNormal) * axisSide < AxisMargin - 0.001f)
            throw new System.InvalidOperationException("Camera C2 testemunha: PERTO perdeu distancia/plano local; nenhuma captura.");
        RequireWitnessC2LOS(position, target, right, "preparo");
        GTA.Math.Vector3 aim = ComposeThird(position, head - new GTA.Math.Vector3(0f, 0f, 0.08f), direction, 30f);
        witnessC2Position = position; witnessC2Rotation = LookRotation(aim - position);
        witnessC2ActorPosition = target.Position; witnessC2ActorHeading = target.Heading;
        witnessC2AxisOrigin = axisOrigin; witnessC2AxisNormal = axisNormal; witnessC2AxisSide = axisSide;
        witnessC2Side = side; witnessC2Height = height; witnessC2Closer = closer;
        witnessC2Handle = target.Handle; witnessC2Model = target.Model.Hash; witnessC2Address = target.MemoryAddress;
        for (int i = 0; i < WitnessC2Props.Length; i++)
        {
            GTA.Prop prop = objects.GetProp(WitnessC2Props[i]);
            witnessC2PropHandles[i] = prop.Handle; witnessC2PropModels[i] = prop.Model.Hash; witnessC2PropAddresses[i] = prop.MemoryAddress;
        }
        ProbeWitnessC2PoseBeforeReady(); // Sem escritor/acao: aplicar, conferir e restaurar inicial no mesmo preparo.
        witnessC2Target = target; witnessC2Objects = objects;
        witnessC2PosePending = false; witnessC2PoseApplied = false; witnessC2ActiveVerified = false;
        witnessC2Known = true; // Cache geometrico; nao aprova aplicacao com render ativo.
        Log("CAMERA_C2_TESTEMUNHA_PREPARADA", "Cena=EP07_C02; Ator=DJANIRA_POBRE; Handle=" + target.Handle +
            "; ActorPosition=" + target.Position + "; Foot=" + foot + "; Head=" + head + "; Forward=" + direction +
            "; RawClose=" + raw + "; Position=" + position + "; Rotacao=" + witnessC2Rotation + "; FOV=30; Lado=" + Number(axisSide) +
            "; LocalProjection=" + Number(Dot(position - foot, axisNormal) * axisSide) + "; GlobalProjection=" + Number(Dot(position - axisOrigin, axisNormal) * axisSide) +
            "; PlanoParaleloNoAtor=True; EixoGlobalAlterado=False; TeleporteAtor=False; ImageApproval=False");
    }

    public void ApplyGalpaoWitnessClose(string sceneId, string localId, string actorId, string preset,
        GTA.Ped target, ObjetosSerieDiretor objects, float side, float height, float closer, bool smooth)
    {
        RequireGalpaoWitnessScope(sceneId, localId, actorId, target, objects);
        RequireWitnessC2Cache(target, objects, side, height, closer, smooth);
        if (preset != "PERTO" || witnessC2PosePending)
            throw new System.InvalidOperationException("Camera C2 testemunha: plano invalido ou leitura pos-frame ainda pendente.");
        RequireWitnessC2RenderingOwned("antes_set_ou_reuso");
        GTA.Math.Vector3 direction = HorizontalUnit(target.GetOffsetPosition(new GTA.Math.Vector3(0f, 1f, 0f)) - target.Position);
        RequireWitnessC2LOS(witnessC2Position, target, new GTA.Math.Vector3(direction.Y, -direction.X, 0f), "corte");
        if (witnessC2PoseApplied && CurrentPreset == "PERTO")
        {
            // Final16 conserva a pose ja conferida; nenhum segundo setter/retry.
            RequireWitnessC2Pose(witnessC2Position, witnessC2Rotation, 30f, "corte_reuso_conferido");
            Log("CAMERA_C2_TESTEMUNHA_REUSADA", "Cena=EP07_C02; CameraHandle=" + camera.Handle +
                "; SetterEnviado=False; PosePosFrameJaConferida=True; ImageApproval=False");
            return;
        }
        SetWitnessC2Frame(witnessC2Position, witnessC2Rotation, 30f);
        witnessC2PoseApplied = false;
        witnessC2RequestedFrame = unchecked((uint)GTA.Game.FrameCount);
        witnessC2RequestedTime = unchecked((uint)GTA.Game.GameTime);
        witnessC2PendingCameraHandle = camera.Handle; witnessC2PendingCameraAddress = camera.MemoryAddress;
        witnessC2PosePending = true;
        Log("CAMERA_C2_TESTEMUNHA_POSE_PENDENTE", "Cena=EP07_C02; CameraHandle=" + camera.Handle +
            "; RequestedFrame=" + witnessC2RequestedFrame + "; RequestedGameTime=" + witnessC2RequestedTime +
            "; SetterEnviadoUmaVez=True; ExactAindaNaoConferido=True; MaxFrameDelta=2; MaxGameTimeMs=250; ImageApproval=False");
    }

    private void RequireWitnessC2Cache(GTA.Ped target, ObjetosSerieDiretor objects, float side, float height, float closer, bool smooth)
    {
        if (!witnessC2Known || smooth || side != witnessC2Side || height != witnessC2Height || closer != witnessC2Closer ||
            target.Handle != witnessC2Handle || target.Model.Hash != witnessC2Model || target.MemoryAddress != witnessC2Address ||
            Length(target.Position - witnessC2ActorPosition) > 0.05f || System.Math.Abs(AngleDelta(target.Heading, witnessC2ActorHeading)) > 2f ||
            Length(axisOrigin - witnessC2AxisOrigin) > 0.00001f || Length(axisNormal - witnessC2AxisNormal) > 0.00001f || axisSide != witnessC2AxisSide)
            throw new System.InvalidOperationException("Camera C2 testemunha: cache/ator/eixo/ajustes mudou; remonte, sem seguir/corrigir ator.");
        for (int i = 0; i < WitnessC2Props.Length; i++)
        {
            GTA.Prop prop = objects.GetProp(WitnessC2Props[i]);
            if (prop.Handle != witnessC2PropHandles[i] || prop.Model.Hash != witnessC2PropModels[i] || prop.MemoryAddress != witnessC2PropAddresses[i])
                throw new System.InvalidOperationException("Camera C2 testemunha: identidade de prop proprio mudou.");
        }
    }

    private void RequireWitnessC2RenderingOwned(string stage)
    {
        int rendering = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM);
        bool active = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_CAM_ACTIVE, camera.Handle);
        Log("CAMERA_C2_TESTEMUNHA_RENDER_GUARDA", "Cena=EP07_C02; Fase=" + stage + "; OwnCamera=" + camera.Handle +
            "; RenderingCamera=" + rendering + "; IsActive=" + active + "; ImageApproval=False");
        if (rendering != camera.Handle || !active)
            throw new System.InvalidOperationException("Camera C2 testemunha: render pertence a outra camera; nenhum setter/retry forcado.");
    }

    public void RequireGalpaoWitnessRecordingReady()
    {
        RequireGalpaoWitnessScope("EP07_C02", "GALPAO_CYPRESS", "DJANIRA_POBRE", witnessC2Target, witnessC2Objects);
        RequireWitnessC2Cache(witnessC2Target, witnessC2Objects, witnessC2Side, witnessC2Height, witnessC2Closer, false);
        RequireWitnessC2RenderingOwned("antes_arm_ou_acao");
        if (witnessC2PosePending || !witnessC2ActiveVerified)
            throw new System.InvalidOperationException("Camera C2 testemunha: previa ativa PERTO precisa getter exato no frame seguinte antes ARM/acao.");
    }

    private void AdvanceWitnessC2PoseRead()
    {
        if (!witnessC2PosePending) return;
        uint frameDelta = unchecked((uint)GTA.Game.FrameCount - witnessC2RequestedFrame);
        uint elapsed = unchecked((uint)GTA.Game.GameTime - witnessC2RequestedTime);
        // Primeira leitura elegivel no Update de frame posterior. Uma falha nao
        // aguarda outras leituras nem reenvia SET_CAM_PARAMS. Dois frames permitem
        // apenas um Tick do host perdido; a timeline16s continua correndo.
        if (frameDelta == 0)
        {
            if (elapsed > 250) throw new System.InvalidOperationException("Camera C2 testemunha: nenhum frame posterior dentro do prazo.");
            return;
        }
        if (frameDelta > 2 || elapsed > 250)
            throw new System.InvalidOperationException("Camera C2 testemunha: leitura pos-frame perdeu prazo2frames/250ms; nenhuma captura aceita.");
        if (camera.Handle != witnessC2PendingCameraHandle || camera.MemoryAddress != witnessC2PendingCameraAddress)
            throw new System.InvalidOperationException("Camera C2 testemunha: identidade da camera mudou entre setter e pos-frame.");
        RequireGalpaoWitnessScope("EP07_C02", "GALPAO_CYPRESS", "DJANIRA_POBRE", witnessC2Target, witnessC2Objects);
        RequireWitnessC2Cache(witnessC2Target, witnessC2Objects, witnessC2Side, witnessC2Height, witnessC2Closer, false);
        RequireWitnessC2RenderingOwned("pos_frame");
        Log("CAMERA_C2_TESTEMUNHA_POS_FRAME", "Cena=EP07_C02; RequestedFrame=" + witnessC2RequestedFrame +
            "; ActualFrame=" + unchecked((uint)GTA.Game.FrameCount) + "; FrameDelta=" + frameDelta +
            "; GameTimeDeltaMs=" + elapsed + "; SetterReenviado=False; ImageApproval=False");
        RequireWitnessC2Pose(witnessC2Position, witnessC2Rotation, 30f, "corte_pos_frame");
        // Somente depois de todos os getters exatos o estado e declarado aplicado.
        witnessC2PosePending = false; witnessC2PoseApplied = true; witnessC2ActiveVerified = true;
        CurrentPreset = "PERTO"; opticalMedium = false;
        Log("CAMERA_C2_TESTEMUNHA_APLICADA", "Cena=EP07_C02; Ator=DJANIRA_POBRE; Handle=" + witnessC2Target.Handle +
            "; ActorPosition=" + witnessC2Target.Position + "; Position=" + camera.Position + "; Rotacao=" + camera.Rotation + "; FOV=" + Number(camera.FieldOfView) +
            "; Plano=PERTO; EixoGlobalAlterado=False; Suave=False; PosFrameConferido=True; ImageApproval=False");
        Log("CAMERA_PLANO", "Plano=PERTO; alvo=" + witnessC2Target.Handle + "; suave=False; posicao=" + camera.Position +
            "; FOV=30; TestemunhaC2=True; PosFrameConferido=True; EixoGlobalAlterado=False; ImageApproval=False");
    }

    // Somente os tres caminhos C2 guardados usam SET_CAM_PARAMS com duracao zero.
    // API191 SetCamFrameParameters cancela interpolacao; nunca tenta novamente em silencio.
    private void SetWitnessC2Frame(GTA.Math.Vector3 position, GTA.Math.Vector3 rotation, float fov)
    {
        RestoredPoseName = ""; free = false; tween = false;
        camera.StopPointing();
        camera.SetCamFrameParameters(position, rotation, fov,
            GTA.CamFrameInterpolatorCurveType.SinAccelDecel, GTA.CamFrameInterpolatorCurveType.SinAccelDecel, GTA.EulerRotationOrder.YXZ);
        Log("CAMERA_C2_TESTEMUNHA_FRAME_PARAMS", "Cena=EP07_C02; Native=SET_CAM_PARAMS; DurationMs=0; RotOrder=2;" +
            " PositionRequested=" + position + "; RotationRequested=" + rotation + "; FOVRequested=" + Number(fov) +
            "; RenderingCamera=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM) + "; GettersRequiredNext=True; ImageApproval=False");
    }

    // Diagnostico C2 somente; nao altera nenhum setter ou tolerancia de banco/base/outros planos.
    private void RequireWitnessC2Pose(GTA.Math.Vector3 expectedPosition, GTA.Math.Vector3 expectedRotation, float expectedFov, string stage)
    {
        GTA.Math.Vector3 actualPosition = camera.Position, actualRotation = camera.Rotation;
        float actualFov = camera.FieldOfView;
        GTA.Math.Vector3 deltaPosition = actualPosition - expectedPosition;
        float positionDistance = Length(deltaPosition), fovDelta = System.Math.Abs(actualFov - expectedFov);
        float deltaX = System.Math.Abs(AngleDelta(actualRotation.X, expectedRotation.X));
        float deltaY = System.Math.Abs(AngleDelta(actualRotation.Y, expectedRotation.Y));
        float deltaZ = System.Math.Abs(AngleDelta(actualRotation.Z, expectedRotation.Z));
        bool finite = IsFinite(actualPosition.X) && IsFinite(actualPosition.Y) && IsFinite(actualPosition.Z) &&
            IsFinite(actualRotation.X) && IsFinite(actualRotation.Y) && IsFinite(actualRotation.Z) && IsFinite(actualFov) &&
            IsFinite(positionDistance) && IsFinite(fovDelta) && IsFinite(deltaX) && IsFinite(deltaY) && IsFinite(deltaZ);
        bool exact = finite && positionDistance <= 0.005f && fovDelta <= 0.001f && deltaX <= 0.005f && deltaY <= 0.005f && deltaZ <= 0.005f;
        Log("CAMERA_C2_TESTEMUNHA_POSE_GETTERS", "Cena=EP07_C02; Fase=" + stage + "; CameraHandle=" + camera.Handle +
            "; ExpectedPosition=" + expectedPosition + "; ActualPosition=" + actualPosition + "; DeltaPosition=" + deltaPosition +
            "; PositionDistance=" + Number(positionDistance) + "; ExpectedRotation=" + expectedRotation + "; ActualRotation=" + actualRotation +
            "; DeltaRotX=" + Number(deltaX) + "; DeltaRotY=" + Number(deltaY) + "; DeltaRotZ=" + Number(deltaZ) +
            "; ExpectedFOV=" + Number(expectedFov) + "; ActualFOV=" + Number(actualFov) + "; DeltaFOV=" + Number(fovDelta) +
            "; PosTolerance=0.005; RotTolerance=0.005; FOVTolerance=0.001; Finite=" + finite + "; Exact=" + exact +
            "; NativeRotOrder=2; Tween=" + tween + "; Free=" + free +
            "; IsActive=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_CAM_ACTIVE, camera.Handle) +
            "; IsInterpolating=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_CAM_INTERPOLATING, camera.Handle) +
            "; IsShaking=" + GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_CAM_SHAKING, camera.Handle) +
            "; RenderingCamera=" + GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM) +
            "; ImageApproval=False");
        if (!exact)
            throw new System.InvalidOperationException("Camera C2 testemunha: getter da pose aplicada divergiu; veja Expected/Actual/deltas da fase " + stage + ".");
    }

    private void ProbeWitnessC2PoseBeforeReady()
    {
        if (tween || free || FocusOwnActorFromBehind || RestoredPoseName.Length > 0)
            throw new System.InvalidOperationException("Camera C2 teste PREP exige inicial fixa, sem alias/banco/livre.");
        GTA.Math.Vector3 initialPosition = camera.Position, initialRotation = camera.Rotation;
        float initialFov = camera.FieldOfView;
        string initialPreset = CurrentPreset;
        bool initialOpticalMedium = opticalMedium;
        try
        {
            SetWitnessC2Frame(witnessC2Position, witnessC2Rotation, 30f);
            RequireWitnessC2Pose(witnessC2Position, witnessC2Rotation, 30f, "preparo_teste_antes_ready");
        }
        finally
        {
            SetWitnessC2Frame(initialPosition, initialRotation, initialFov);
            CurrentPreset = initialPreset; opticalMedium = initialOpticalMedium;
            RequireWitnessC2Pose(initialPosition, initialRotation, initialFov, "preparo_restaura_inicial");
        }
    }

    private void RequireGalpaoWitnessScope(string sceneId, string localId, string actorId, GTA.Ped target, ObjetosSerieDiretor objects)
    {
        RequireCamera(); RequirePed(target);
        if (sceneId != "EP07_C02" || localId != "GALPAO_CYPRESS" || actorId != "DJANIRA_POBRE" || !hasAxis || actors.Count != 4 ||
            actors[3].Handle != target.Handle || objects == null || !objects.IsReady || free ||
            FocusOwnActorFromBehind || RestoredPoseName.Length > 0)
            throw new System.InvalidOperationException("Camera testemunha somente EP07_C02/GALPAO_CYPRESS/Dja quarto ator proprio, sem base/banco/livre.");
    }

    private void RequireWitnessC2LOS(GTA.Math.Vector3 position, GTA.Ped target, GTA.Math.Vector3 right, string stage)
    {
        GTA.Math.Vector3 head = HeadPosition(target);
        for (int i = 0; i < 3; i++)
        {
            GTA.Math.Vector3 end = head + right * ((i - 1) * 0.1f);
            int ray = GTA.Native.Function.Call<int>((GTA.Native.Hash)0x377906D8A31E5586UL,
                position.X, position.Y, position.Z, end.X, end.Y, end.Z, 17, target.Handle, 0); // Map|Objects; ignore somente Dja propria.
            // Mesmo contrato escalar dos medidores EP01/OwnBox: BOOL Native escreve quatro bytes.
            // OutputArgument.GetResult<bool>() le oito; buffers vazios deixam padding/saidas nao escritas indefinidos.
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
                int status = ray == 0 ? 0 : GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, ray, hit, point, normal, entity);
                int hitValue32 = System.Runtime.InteropServices.Marshal.ReadInt32(hit);
                int handle = System.Runtime.InteropServices.Marshal.ReadInt32(entity);
                bool blocked = status == 2 && hitValue32 != 0;
                float[] coordinates = new float[6], normals = new float[6];
                System.Runtime.InteropServices.Marshal.Copy(point, coordinates, 0, 6);
                System.Runtime.InteropServices.Marshal.Copy(normal, normals, 0, 6);
                GTA.Math.Vector3 hitPoint = new GTA.Math.Vector3(coordinates[0], coordinates[2], coordinates[4]);
                GTA.Math.Vector3 surfaceNormal = new GTA.Math.Vector3(normals[0], normals[2], normals[4]);
                bool exists = status == 2 && handle != 0 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, handle);
                int entityType = exists ? GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, handle) : -1;
                Log("CAMERA_C2_TESTEMUNHA_LOS", "Cena=EP07_C02; Fase=" + stage + "; Index=" + i + "; Status=" + status +
                    "; Hit=" + blocked + "; HitValue32=" + hitValue32 + "; Entity=" + handle + "; EntityHex=0x" + handle.ToString("X8") +
                    "; HitPoint=" + hitPoint + "; Normal=" + surfaceNormal + "; HitOutputsMeaningful=" + blocked +
                    "; EntityExists=" + exists + "; EntityType=" + entityType + "; EntityTypeQueried=" + exists + "; From=" + position + "; To=" + end +
                    "; IgnoredOwnActor=" + target.Handle + "; Flags=17; Options=0; SameCallSynchronous=True; PendingHandleReused=False; BuffersZeroed=True; NativeBoolWidth=32; ImageApproval=False");
                if (status != 2 || blocked)
                    throw new System.InvalidOperationException("Camera C2 testemunha: LOS Native pendente/invalida/obstruida, nenhuma pose filmada aceita.");
            }
            finally
            {
                if (hit != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(hit);
                if (point != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(point);
                if (normal != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(normal);
                if (entity != System.IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(entity);
            }
        }
    }

    private void RequireOwnBehindFixedAxis()
    {
        if (!ownBehindFixedKnown || !hasAxis || axisSide != ownBehindFixedAxisSide ||
            Length(axisOrigin - ownBehindFixedAxisOrigin) > 0.00001f || Length(axisNormal - ownBehindFixedAxisNormal) > 0.00001f ||
            Dot(ownBehindFixedPosition - axisOrigin, axisNormal) * axisSide < AxisMargin - 0.001f)
            throw new System.InvalidOperationException("Camera banco fixa: eixo atual mudou/cruzado; nao restaurar lado de alias interior.");
    }

    // Mesmas tolerancias dos getters usados para restauracao exata de cameras salvas.
    private void RequireOwnBehindFixedPose()
    {
        RequireOwnBehindFixedAxis();
        GTA.Math.Vector3 actualPosition = camera.Position, actualRotation = camera.Rotation;
        float actualFov = camera.FieldOfView;
        if (!IsFinite(actualPosition.X) || !IsFinite(actualPosition.Y) || !IsFinite(actualPosition.Z) ||
            !IsFinite(actualRotation.X) || !IsFinite(actualRotation.Y) || !IsFinite(actualRotation.Z) || !IsFinite(actualFov) ||
            Length(actualPosition - ownBehindFixedPosition) > 0.001f ||
            System.Math.Abs(AngleDelta(actualRotation.X, ownBehindFixedRotation.X)) > 0.001f ||
            System.Math.Abs(AngleDelta(actualRotation.Y, ownBehindFixedRotation.Y)) > 0.001f ||
            System.Math.Abs(AngleDelta(actualRotation.Z, ownBehindFixedRotation.Z)) > 0.001f ||
            System.Math.Abs(actualFov - ownBehindFixedFov) > 0.001f || tween || free)
            throw new System.InvalidOperationException("Camera banco fixa: getters nao confirmaram a mesma pose nativa inicial.");
    }

    // C03 pode fechar a lente a partir do DOIS visualmente livre, sem mover a camera.
    // Repetir no MEDIA final conserva o mesmo ponto; outros presets invalidam a continuidade optica.
    public void ApplyOpticalMedium(GTA.Ped target, float fieldOfView)
    {
        RequireCamera(); RequirePed(target);
        if (!actors.Exists(delegate(GTA.Ped actor) { return actor.Handle == target.Handle; }))
            throw new System.InvalidOperationException("Camera optica: escolha um personagem do elenco da cena.");
        if (free || tween || (CurrentPreset != "DOIS" && !(opticalMedium && CurrentPreset == "MEDIA")))
            throw new System.InvalidOperationException("Camera optica: fixe primeiro um DOIS livre; nao deslocar para um MEDIA nao conferido.");
        if (!IsFinite(fieldOfView) || fieldOfView < 15f || fieldOfView > 40f)
            throw new System.InvalidOperationException("Camera optica: FOV precisa estar entre15 e40graus.");
        GTA.Math.Vector3 position = camera.Position;
        GTA.Math.Vector3 aim = HeadPosition(target) - new GTA.Math.Vector3(0f, 0f, 0.08f);
        GTA.Math.Vector3 rotation = LookRotation(aim - position);
        RestoredPoseName = "";
        camera.StopPointing();
        camera.Rotation = rotation;
        camera.FieldOfView = fieldOfView;
        CurrentPreset = "MEDIA"; opticalMedium = true;
        Log("CAMERA_PLANO", "Plano=MEDIA; alvo=" + target.Handle + "; suave=False; optica=True; posicao_conservada=" +
            position + "; FOV=" + Number(fieldOfView) + "; origem=DOIS_conferido_visualmente; teste_closeup=pendente");
    }

    // Detalhe do prop proprio: centro/cantos dos bounds medidos e transform atual real.
    // Enquadramento nao comprova texto, abertura, contato ou ausencia de oclusao do cenario.
    public void ApplyObjectClose(ObjetosSerieDiretor objects, string codigo, float fieldOfView,
        float elevationDegrees, bool smooth)
    {
        RequireCamera();
        if (objects == null || !objects.IsReady)
            throw new System.InvalidOperationException("Camera objeto: aguarde os props proprios preparados.");
        if (!IsFinite(fieldOfView) || fieldOfView < 15f || fieldOfView > 55f ||
            !IsFinite(elevationDegrees) || elevationDegrees < 10f || elevationDegrees > 80f)
            throw new System.InvalidOperationException("Camera objeto: FOV15-55 e elevacao10-80 graus.");
        GTA.Prop prop = objects.GetProp(codigo);
        if (!prop.IsVisible) throw new System.InvalidOperationException("Camera objeto: prop proprio precisa estar visivel.");
        GTA.Math.Vector3 min, max;
        objects.GetMeasuredBounds(codigo, out min, out max);
        if (!IsFinite(min.X) || !IsFinite(min.Y) || !IsFinite(min.Z) ||
            !IsFinite(max.X) || !IsFinite(max.Y) || !IsFinite(max.Z) ||
            max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z)
            throw new System.InvalidOperationException("Camera objeto: bounds medidos invalidos.");
        GTA.Math.Vector3 center = prop.GetOffsetPosition((min + max) * 0.5f);
        float radius = Length(max - min) * 0.5f;
        if (!IsFinite(center.X) || !IsFinite(center.Y) || !IsFinite(center.Z) || !IsFinite(radius) || radius <= 0f)
            throw new System.InvalidOperationException("Camera objeto: centro/raio real invalido.");
        GTA.Math.Vector3 horizontal = HorizontalUnit(camera.Position - center);
        double elevation = elevationDegrees * System.Math.PI / 180.0;
        GTA.Math.Vector3 direction = horizontal * (float)System.Math.Cos(elevation) +
            new GTA.Math.Vector3(0f, 0f, (float)System.Math.Sin(elevation));
        float distance = System.Math.Max(0.35f, radius * 1.1f /
            (float)System.Math.Sin(fieldOfView * System.Math.PI / 360.0));
        GTA.Math.Vector3 position = KeepSide(center + direction * distance, true);
        float actualDistance = Length(center - position);
        if (!IsFinite(actualDistance) || actualDistance <= radius + camera.NearClip)
            throw new System.InvalidOperationException("Camera objeto: lente precisa ficar fora dos bounds reais.");
        GTA.Math.Vector3 view = (center - position) * (1f / actualDistance);
        GTA.Math.Vector3 screenRight = HorizontalUnit(new GTA.Math.Vector3(view.Y, -view.X, 0f));
        GTA.Math.Vector3 screenUp = new GTA.Math.Vector3(screenRight.Y * view.Z, -screenRight.X * view.Z,
            screenRight.X * view.Y - screenRight.Y * view.X);
        float halfHeight = (float)System.Math.Tan(fieldOfView * System.Math.PI / 360.0);
        // Validar o enquadramento final depois da regra de eixo, antes de qualquer setter.
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
        {
            GTA.Math.Vector3 corner = prop.GetOffsetPosition(new GTA.Math.Vector3(
                x == 0 ? min.X : max.X, y == 0 ? min.Y : max.Y, z == 0 ? min.Z : max.Z));
            GTA.Math.Vector3 relative = corner - position;
            float depth = Dot(relative, view);
            if (!IsFinite(depth) || depth <= camera.NearClip ||
                System.Math.Abs(Dot(relative, screenRight)) > depth * halfHeight * AspectRatio ||
                System.Math.Abs(Dot(relative, screenUp)) > depth * halfHeight)
                throw new System.InvalidOperationException("Camera objeto: cantos reais nao cabem no quadro deste lado do eixo.");
        }
        SetDestination(position, LookRotation(center - position), fieldOfView, smooth);
        CurrentPreset = "PERTO"; opticalMedium = false;
        Log("CAMERA_OBJETO_CLOSE", "Codigo=" + codigo + "; Handle=" + prop.Handle + "; Modelo=" + prop.Model.Hash +
            "; BoundsMin=" + min + "; BoundsMax=" + max + "; Centro=" + center + "; Raio=" + Number(radius) +
            "; Posicao=" + position + "; Elevacao=" + Number(elevationDegrees) + "; FOV=" + Number(fieldOfView) +
            "; Suave=" + smooth + "; Origem=prop_proprio_bounds_medidos; TesteVisual=pendente");
    }

    public void ApplyWritingC6Close(EscritaFullTracks85Diretor writing, GTA.Ped actor)
    {
        RequireCamera(); RequirePed(actor);
        if (writing==null || actors.Count!=1 || actors[0].Handle!=actor.Handle || actors[0].MemoryAddress!=actor.MemoryAddress || actor.IsPlayer)
            throw new System.InvalidOperationException("C6 close requires its one registered actor and writing controller");
        GTA.Math.Vector3[] points=writing.GetWritingC6VisualPoints(actor);
        if (writingC6CloseKnown)
        {
            if (actor.Handle!=writingC6ActorHandle || actor.MemoryAddress!=writingC6ActorAddress) throw new System.InvalidOperationException("C6 close identity changed");
            if (!writingC6CloseVerified) return; // PREP waits for PollWritingC6Close on a later Tick; no setters or early approval.
            RequireWritingC6RecordingReady(writing,actor); return; // Fixed camera, no per-frame following.
        }
        GTA.Math.Vector3 min=points[0],max=points[0];
        foreach (GTA.Math.Vector3 p in points)
        { min=new GTA.Math.Vector3(System.Math.Min(min.X,p.X),System.Math.Min(min.Y,p.Y),System.Math.Min(min.Z,p.Z));
          max=new GTA.Math.Vector3(System.Math.Max(max.X,p.X),System.Math.Max(max.Y,p.Y),System.Math.Max(max.Z,p.Z)); }
        GTA.Math.Vector3 center=(min+max)*.5f;float radius=0f;
        foreach (GTA.Math.Vector3 p in points) radius=System.Math.Max(radius,Length(p-center));
        GTA.Math.Vector3 front=HorizontalUnit(actor.GetOffsetPosition(new GTA.Math.Vector3(0f,1f,0f))-actor.Position);
        float distance=System.Math.Max(1.5f,(radius+.20f)/(float)System.Math.Sin(35f*System.Math.PI/360.0));
        if (!IsFinite(distance) || distance>4f) throw new System.InvalidOperationException("C6 measured close exceeds4m");
        GTA.Math.Vector3 pos=KeepSide(center+(front*.9659258f+new GTA.Math.Vector3(0f,0f,.258819f))*distance,true);
        GTA.Math.Vector3 actorMin,actorMax;actor.Model.GetDimensions(out actorMin,out actorMax);GTA.Math.Vector3 local=actor.GetPositionOffset(pos);
        if (local.X>=actorMin.X-camera.NearClip && local.X<=actorMax.X+camera.NearClip &&
            local.Y>=actorMin.Y-camera.NearClip && local.Y<=actorMax.Y+camera.NearClip &&
            local.Z>=actorMin.Z-camera.NearClip && local.Z<=actorMax.Z+camera.NearClip) throw new System.InvalidOperationException("C6 close lens inside actor bounds");
        writingC6Position=pos;writingC6Rotation=LookRotation(center-pos);
        writingC6ActorHandle=actor.Handle;writingC6ActorAddress=actor.MemoryAddress;
        writingC6Frame=unchecked((uint)GTA.Game.FrameCount);writingC6Time=unchecked((uint)GTA.Game.GameTime);
        writingC6CloseKnown=true;writingC6CloseVerified=false;
        SetDestination(pos,writingC6Rotation,35f,false);CurrentPreset="PERTO";opticalMedium=false;
        Log("CAMERA_ESCRITA_C6_PEDIDA","OwnActor="+actor.Handle+"; Center="+center+"; Position="+pos+"; Rotation="+writingC6Rotation+
            "; FOV=35; FixedMeasuredHeadHandsBoardPen=True; OpticalFramingAllowance=.20; NoContactMarginChanged=True; VisualApproval=False");
    }
    public bool PollWritingC6Close(EscritaFullTracks85Diretor writing,GTA.Ped actor)
    {
        RequireCamera();RequirePed(actor);
        if (!writingC6CloseKnown || actor.Handle!=writingC6ActorHandle || actor.MemoryAddress!=writingC6ActorAddress || writing==null)
            throw new System.InvalidOperationException("C6 close was not prepared");
        writing.GetWritingC6VisualPoints(actor);
        if (unchecked((uint)GTA.Game.FrameCount)==writingC6Frame) return false;
        int rendering=GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM);
        GTA.Math.Vector3 actual=camera.Rotation;
        float posDelta=Length(camera.Position-writingC6Position),rotDelta=System.Math.Max(System.Math.Abs(AngleDelta(actual.X,writingC6Rotation.X)),
            System.Math.Max(System.Math.Abs(AngleDelta(actual.Y,writingC6Rotation.Y)),System.Math.Abs(AngleDelta(actual.Z,writingC6Rotation.Z))));
        bool exact=rendering==camera.Handle && posDelta<=.005f && rotDelta<=.001f && System.Math.Abs(camera.FieldOfView-35f)<=.001f;
        if (!exact && (writingC6CloseVerified || unchecked((uint)GTA.Game.GameTime-writingC6Time)>250))
            throw new System.InvalidOperationException("C6 rendered close diverged: renderer="+rendering+" own="+camera.Handle+" pos="+posDelta+" rot="+rotDelta+" FOV="+camera.FieldOfView);
        if (!exact)return false;
        if (!writingC6CloseVerified) Log("CAMERA_ESCRITA_C6_CONFERIDA","OwnRendering=True; PostTick=True; PosDelta="+Number(posDelta)+"; RotDelta="+Number(rotDelta)+"; FOV=35; VisualApproval=False");
        writingC6CloseVerified=true;return true;
    }
    public void RequireWritingC6RecordingReady(EscritaFullTracks85Diretor writing,GTA.Ped actor)
    {
        if (!writingC6CloseVerified || !PollWritingC6Close(writing,actor))throw new System.InvalidOperationException("C6 close requires next-frame own render before ARM/countdown");
    }

    public GTA.Camera GetAuthoredOpening99OwnCamera(string sceneId, GTA.Ped ownPoor)
    {
        RequireCamera();RequirePed(ownPoor);
        if(sceneId!="teste_fila_99"||actors.Count!=1||!actors.Exists(delegate(GTA.Ped item){return item.Handle==ownPoor.Handle&&item.MemoryAddress==ownPoor.MemoryAddress;}))
            throw new System.InvalidOperationException("Authored99 only own technical camera/registered Poor");
        Log("CAMERA_PAPEL_ABRE99_REFERENCIA", "Scene=teste_fila_99; OwnCamera="+camera.Handle+"; OwnAddress="+camera.MemoryAddress+"; OwnActive="+camera.IsActive+
            "; NativeRenderingHandle="+GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_RENDERING_CAM)+"; OwnReferenceReturned=True; SameTickRenderingNotAssumed=True; NoSetter=True");
        return camera;
    }

    public GTA.Camera GetPocket98OwnCamera(string sceneId, GTA.Ped ownRich)
    {
        RequireCamera();RequirePed(ownRich);
        if(sceneId!="teste_fila_98"||actors.Count!=1||!actors.Exists(delegate(GTA.Ped item){return item.Handle==ownRich.Handle&&item.MemoryAddress==ownRich.MemoryAddress;}))
            throw new System.InvalidOperationException("Pocket98 only own technical camera/registered Rich");
        return camera;
    }

    public GTA.Camera GetAuthoredPaperC5OwnCamera(string sceneId, GTA.Prop ownProxy)
    {
        RequireCamera();
        if (sceneId != "EP08_C05" || !handoffCloseKnown || System.Object.ReferenceEquals(ownProxy, null) || !ownProxy.Exists() ||
            ownProxy.Handle != handoffClosePropHandle || ownProxy.MemoryAddress != handoffClosePropAddress ||
            ownProxy.Model.Hash != unchecked((int)GTA.StringHash.AtStringHash("prop_amanda_note_01")))
            throw new System.InvalidOperationException("Authored C5 requires the existing own measured handoff camera/proxy identity.");
        return camera;
    }
    // C8C5 somente: camera fixa derivada das maos e dos bounds reais do papel.
    public void ApplyHandoffObjectClose(ObjetosSerieDiretor objects, string codigo, GTA.Ped donor,
        GTA.Ped receiver, float fieldOfView, float elevationDegrees)
    {
        RequireCamera(); RequirePed(donor); RequirePed(receiver);
        if (objects == null || !objects.IsReady || codigo != "PAPEL_IRACI" || actors.Count != 2 ||
            donor.IsPlayer || receiver.IsPlayer || donor.Handle == receiver.Handle ||
            !actors.Exists(delegate(GTA.Ped a) { return a.Handle == donor.Handle && a.MemoryAddress == donor.MemoryAddress; }) ||
            !actors.Exists(delegate(GTA.Ped a) { return a.Handle == receiver.Handle && a.MemoryAddress == receiver.MemoryAddress; }))
            throw new System.InvalidOperationException("Camera handoff: exige dois atores registrados e papel proprio preparado.");
        if (!hasAxis || !IsFinite(fieldOfView) || fieldOfView < 15f || fieldOfView > 55f ||
            !IsFinite(elevationDegrees) || elevationDegrees < 10f || elevationDegrees > 80f)
            throw new System.InvalidOperationException("Camera handoff: exige eixo, FOV15-55 e elevacao10-80.");
        GTA.Prop prop = objects.GetProp(codigo);
        if (!objects.IsObjectVisualSourceConfigured(codigo)) throw new System.InvalidOperationException("Camera handoff: papel precisa ter fonte visual propria configurada.");
        // PHRightHand28422, o mesmo bone primario do handoff/attachment em ObjetosSerieDiretor.
        GTA.PedBone donorBone = donor.Bones[(GTA.Bone)28422], receiverBone = receiver.Bones[(GTA.Bone)28422];
        if (System.Object.ReferenceEquals(donorBone, null) || !donorBone.IsValid ||
            System.Object.ReferenceEquals(receiverBone, null) || !receiverBone.IsValid)
            throw new System.InvalidOperationException("Camera handoff: PHRightHand real indisponivel.");
        GTA.Math.Vector3 donorHand = donorBone.Position, receiverHand = receiverBone.Position;
        GTA.Math.Vector3 min, max; objects.GetMeasuredBounds(codigo, out min, out max);
        if (!IsFinite(min.X) || !IsFinite(min.Y) || !IsFinite(min.Z) || !IsFinite(max.X) || !IsFinite(max.Y) || !IsFinite(max.Z) ||
            max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z)
            throw new System.InvalidOperationException("Camera handoff: bounds medidos invalidos.");
        var points = new System.Collections.Generic.List<GTA.Math.Vector3>(); points.Add(donorHand); points.Add(receiverHand);
        GTA.Math.Vector3[] authoredPoints;
        if (objects.TryGetAuthoredVisualPoints(codigo, out authoredPoints)) points.AddRange(authoredPoints);
        else for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            points.Add(prop.GetOffsetPosition(new GTA.Math.Vector3(x == 0 ? min.X : max.X, y == 0 ? min.Y : max.Y, z == 0 ? min.Z : max.Z)));
        foreach (GTA.Math.Vector3 point in points)
            if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z))
                throw new System.InvalidOperationException("Camera handoff: ponto nao finito.");
        GTA.Math.Vector3 center, position; float radius = 0f;
        if (handoffCloseKnown)
        {
            if (prop.Handle != handoffClosePropHandle || prop.MemoryAddress != handoffClosePropAddress ||
                donor.Handle != handoffCloseDonorHandle || donor.MemoryAddress != handoffCloseDonorAddress ||
                receiver.Handle != handoffCloseReceiverHandle || receiver.MemoryAddress != handoffCloseReceiverAddress ||
                fieldOfView != handoffCloseFov || elevationDegrees != handoffCloseElevation || axisSide != handoffCloseAxisSide ||
                Length(axisOrigin - handoffCloseAxisOrigin) > 0.00001f || Length(axisNormal - handoffCloseAxisNormal) > 0.00001f)
                throw new System.InvalidOperationException("Camera handoff: identidade/eixo/optica mudou; remontar.");
            center = handoffCloseCenter; position = handoffClosePosition;
        }
        else
        {
            center = (donorHand + receiverHand) * 0.5f;
            foreach (GTA.Math.Vector3 point in points) radius = System.Math.Max(radius, Length(point - center));
            if (!IsFinite(radius) || radius <= 0f) throw new System.InvalidOperationException("Camera handoff: raio invalido.");
            GTA.Math.Vector3 horizontal = HorizontalUnit(camera.Position - center);
            double elevation = elevationDegrees * System.Math.PI / 180.0;
            GTA.Math.Vector3 direction = horizontal * (float)System.Math.Cos(elevation) +
                new GTA.Math.Vector3(0f, 0f, (float)System.Math.Sin(elevation));
            float distance = System.Math.Max(1.5f, radius * 1.1f / (float)System.Math.Sin(fieldOfView * System.Math.PI / 360.0));
            position = KeepSide(center + direction * distance, true);
        }
        float actualDistance = Length(center - position);
        if (!IsFinite(actualDistance) || actualDistance < 1.5f || actualDistance <= camera.NearClip)
            throw new System.InvalidOperationException("Camera handoff: distancia minima de1.5m exigida.");
        // Lente fora dos bounds locais dos modelos de ambos. Nao altera corpo/mao/prop.
        foreach (GTA.Ped actor in new GTA.Ped[] { donor, receiver })
        {
            GTA.Math.Vector3 actorMin, actorMax; actor.Model.GetDimensions(out actorMin, out actorMax);
            GTA.Math.Vector3 local = actor.GetPositionOffset(position);
            if (!IsFinite(local.X) || !IsFinite(local.Y) || !IsFinite(local.Z) ||
                !IsFinite(actorMin.X) || !IsFinite(actorMin.Y) || !IsFinite(actorMin.Z) ||
                !IsFinite(actorMax.X) || !IsFinite(actorMax.Y) || !IsFinite(actorMax.Z) ||
                actorMax.X <= actorMin.X || actorMax.Y <= actorMin.Y || actorMax.Z <= actorMin.Z)
                throw new System.InvalidOperationException("Camera handoff: volume do ator invalido.");
            float margin = camera.NearClip;
            if (local.X >= actorMin.X - margin && local.X <= actorMax.X + margin &&
                local.Y >= actorMin.Y - margin && local.Y <= actorMax.Y + margin &&
                local.Z >= actorMin.Z - margin && local.Z <= actorMax.Z + margin)
                throw new System.InvalidOperationException("Camera handoff: lente dentro dos bounds do ator.");
        }
        GTA.Math.Vector3 view = (center - position) * (1f / actualDistance);
        GTA.Math.Vector3 screenRight = HorizontalUnit(new GTA.Math.Vector3(view.Y, -view.X, 0f));
        GTA.Math.Vector3 screenUp = new GTA.Math.Vector3(screenRight.Y * view.Z, -screenRight.X * view.Z,
            screenRight.X * view.Y - screenRight.Y * view.X);
        float halfHeight = (float)System.Math.Tan(fieldOfView * System.Math.PI / 360.0);
        foreach (GTA.Math.Vector3 point in points)
        {
            GTA.Math.Vector3 relative = point - position; float depth = Dot(relative, view);
            if (!IsFinite(depth) || depth <= camera.NearClip ||
                System.Math.Abs(Dot(relative, screenRight)) > depth * halfHeight * AspectRatio ||
                System.Math.Abs(Dot(relative, screenUp)) > depth * halfHeight)
                throw new System.InvalidOperationException("Camera handoff: mao/papel fora do quadro fixo.");
        }
        bool first = !handoffCloseKnown;
        if (first)
        {
            handoffClosePosition = position; handoffCloseCenter = center; handoffCloseFov = fieldOfView;
            handoffCloseElevation = elevationDegrees; handoffCloseAxisOrigin = axisOrigin;
            handoffCloseAxisNormal = axisNormal; handoffCloseAxisSide = axisSide;
            handoffClosePropHandle = prop.Handle; handoffClosePropAddress = prop.MemoryAddress;
            handoffCloseDonorHandle = donor.Handle; handoffCloseDonorAddress = donor.MemoryAddress;
            handoffCloseReceiverHandle = receiver.Handle; handoffCloseReceiverAddress = receiver.MemoryAddress;
            handoffCloseKnown = true;
        }
        SetDestination(position, LookRotation(center - position), fieldOfView, false);
        CurrentPreset = "PERTO"; opticalMedium = false;
        Log("CAMERA_HANDOFF_CLOSE", "Codigo=" + codigo + "; Prop=" + prop.Handle + "; Donor=" + donor.Handle +
            "; Receiver=" + receiver.Handle + "; DonorPHRight=" + donorHand + "; ReceiverPHRight=" + receiverHand +
            "; CentroFixo=" + center + "; RaioUniaoInicial=" + Number(radius) + "; Distancia=" + Number(actualDistance) +
            "; Posicao=" + position + "; FOV=" + Number(fieldOfView) + "; Elevacao=" + Number(elevationDegrees) +
            "; PrimeiroQuadro=" + first + "; CameraFixa=true; EixoPreservado=true; CorpoPropSemSetter=true; TesteVisual=pendente");
    }

    // Fixar exatamente o quadro atual para a contagem e para o fim da tomada.
    public void FixCurrentPose()
    {
        RequireCamera();
        tween = false;
        free = false;
    }

    public void ToggleFree()
    {
        RequireCamera();
        RestoredPoseName = "";
        tween = false;
        free = !free;
        camera.StopPointing();
        if (free)
            CurrentPreset = "LIVRE";
        Log("CAMERA_LIVRE", "Ativa=" + free);
    }

    // Chamar a cada Tick mesmo fora do modo livre: a transição também é atualizada aqui.
    public void Update(float seconds)
    {
        if (!initialized)
            return;
        RequireCamera();
        if (!IsFinite(seconds) || seconds < 0f)
            throw new System.InvalidOperationException("Câmera: intervalo de atualização inválido.");
        if (witnessC2PosePending) AdvanceWitnessC2PoseRead();
        if (FocusOwnActorFromBehind && ownBehindFixedKnown && CurrentPreset == "ATRAS" && !free)
            RequireOwnBehindFixedPose(); // Leitura apenas; nao seguir atores nem corrigir transform por frame.
        float step = System.Math.Min(seconds, 0.1f);
        if (free)
            AdvanceFree(step);
        else if (tween)
        {
            tweenElapsed += step;
            float t = System.Math.Min(1f, tweenElapsed / TweenSeconds);
            float blend = t * t * (3f - 2f * t);
            camera.Position = KeepSide(fromPosition + (toPosition - fromPosition) * blend, false);
            camera.Rotation = new GTA.Math.Vector3(
                fromRotation.X + AngleDelta(fromRotation.X, toRotation.X) * blend,
                fromRotation.Y + AngleDelta(fromRotation.Y, toRotation.Y) * blend,
                fromRotation.Z + AngleDelta(fromRotation.Z, toRotation.Z) * blend);
            camera.FieldOfView = fromFov + (toFov - fromFov) * blend;
            if (t >= 1f)
                tween = false;
        }
    }

    public void UpdateFree(float seconds) { Update(seconds); }

    private void AdvanceFree(float seconds)
    {
        foreach (GTA.Input.ControlAction action in FreeControls)
            GTA.Input.Controls.DisableControlActionThisFrame(GTA.Input.ControlType.PlayerControl, action, true);
        if (Key(System.Windows.Forms.Keys.Menu) || Key(System.Windows.Forms.Keys.ControlKey) ||
            Key(System.Windows.Forms.Keys.LWin) || Key(System.Windows.Forms.Keys.RWin))
            return;
        float sideways = GTA.Input.Controls.GetDisabledControlNormal(
            GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.MoveLeftRight);
        float ahead = -GTA.Input.Controls.GetDisabledControlNormal(
            GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.MoveUpDown);
        if (System.Math.Abs(sideways) < 0.001f)
            sideways = KeyValue(System.Windows.Forms.Keys.D) - KeyValue(System.Windows.Forms.Keys.A);
        if (System.Math.Abs(ahead) < 0.001f)
            ahead = KeyValue(System.Windows.Forms.Keys.W) - KeyValue(System.Windows.Forms.Keys.S);
        float up = KeyValue(System.Windows.Forms.Keys.E) - KeyValue(System.Windows.Forms.Keys.Q);
        float yaw = KeyValue(System.Windows.Forms.Keys.Left) - KeyValue(System.Windows.Forms.Keys.Right);
        float pitch = KeyValue(System.Windows.Forms.Keys.Up) - KeyValue(System.Windows.Forms.Keys.Down);
        yaw -= GTA.Input.Controls.GetDisabledControlNormal(
            GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.LookLeftRight) * 1.5f;
        pitch -= GTA.Input.Controls.GetDisabledControlNormal(
            GTA.Input.ControlType.PlayerControl, GTA.Input.ControlAction.LookUpDown) * 1.5f;
        GTA.Math.Vector3 rotation = camera.Rotation;
        rotation.X = Clamp(rotation.X + pitch * 90f * seconds, -85f, 85f);
        rotation.Y = 0f;
        rotation.Z = NormalizeAngle(rotation.Z + yaw * 90f * seconds);
        camera.Rotation = rotation;
        double yawRadians = rotation.Z * System.Math.PI / 180.0;
        double pitchRadians = rotation.X * System.Math.PI / 180.0;
        GTA.Math.Vector3 direction = new GTA.Math.Vector3(
            (float)(-System.Math.Sin(yawRadians) * System.Math.Cos(pitchRadians)),
            (float)(System.Math.Cos(yawRadians) * System.Math.Cos(pitchRadians)),
            (float)System.Math.Sin(pitchRadians));
        GTA.Math.Vector3 right = new GTA.Math.Vector3((float)System.Math.Cos(yawRadians),
            (float)System.Math.Sin(yawRadians), 0f);
        GTA.Math.Vector3 movement = direction * ahead + right * sideways + new GTA.Math.Vector3(0f, 0f, up);
        float magnitude = Length(movement);
        if (magnitude > 1f)
            movement *= 1f / magnitude;
        float speed = Key(System.Windows.Forms.Keys.ShiftKey) ? 7.5f : 2.5f;
        camera.Position = KeepSide(camera.Position + movement * (speed * seconds), false);
    }

    public void SavePose(string path, string name)
    {
        RequireCamera();
        if (System.String.IsNullOrWhiteSpace(path) || System.String.IsNullOrWhiteSpace(name) ||
            name.Length > 100 || name != name.Trim())
            throw new System.InvalidOperationException("Câmera: informe um caminho e um nome de marca válidos.");
        System.Xml.XmlConvert.VerifyXmlChars(name);
        string fullPath = System.IO.Path.GetFullPath(path);
        System.Xml.XmlDocument document = new System.Xml.XmlDocument();
        document.XmlResolver = null;
        bool existing = System.IO.File.Exists(fullPath);
        if (existing)
        {
            System.Xml.XmlReaderSettings readerSettings = new System.Xml.XmlReaderSettings();
            readerSettings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
            readerSettings.XmlResolver = null;
            readerSettings.MaxCharactersInDocument = 1000000;
            using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(fullPath, readerSettings))
                document.Load(reader);
            if (document.DocumentElement == null || document.DocumentElement.Name != "CamerasMarcadas" ||
                document.DocumentElement.GetAttribute("versao") != "1")
                throw new System.InvalidOperationException("Câmera: catálogo inválido; esperado CamerasMarcadas, versão 1.");
        }
        else
        {
            document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));
            System.Xml.XmlElement root = document.CreateElement("CamerasMarcadas");
            root.SetAttribute("versao", "1");
            document.AppendChild(root);
        }
        foreach (System.Xml.XmlElement entry in document.DocumentElement.SelectNodes("Camera"))
            if (entry.GetAttribute("nome") == name)
                throw new System.InvalidOperationException("Câmera: já existe uma marca chamada " + name + ". Use outro nome.");
        System.Xml.XmlElement element = document.CreateElement("Camera");
        element.SetAttribute("nome", name);
        element.SetAttribute("gravadoUtc", System.DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        element.SetAttribute("preset", CurrentPreset);
        element.SetAttribute("fov", Number(camera.FieldOfView));
        element.SetAttribute("formato", "16:9");
        element.AppendChild(VectorElement(document, "Posicao", camera.Position));
        element.AppendChild(VectorElement(document, "Rotacao", camera.Rotation));
        System.Xml.XmlElement axis = VectorElement(document, "OrigemEixo", axisOrigin);
        axis.SetAttribute("lado", Number(axisSide));
        element.AppendChild(axis);
        element.AppendChild(VectorElement(document, "NormalEixo", axisNormal));
        document.DocumentElement.AppendChild(element);
        string directory = System.IO.Path.GetDirectoryName(fullPath);
        System.IO.Directory.CreateDirectory(directory);
        if (existing)
        {
            string backup = fullPath + ".backup-" + System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ",
                System.Globalization.CultureInfo.InvariantCulture) + "-" + System.Guid.NewGuid().ToString("N") + ".xml";
            System.IO.File.Copy(fullPath, backup, false);
            Log("BACKUP_CAMERA", backup);
        }
        string temporary = fullPath + ".tmp-" + System.Guid.NewGuid().ToString("N");
        try
        {
            System.Xml.XmlWriterSettings writerSettings = new System.Xml.XmlWriterSettings();
            writerSettings.Encoding = new System.Text.UTF8Encoding(false);
            writerSettings.Indent = true;
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(temporary, writerSettings))
                document.Save(writer);
            if (existing)
                System.IO.File.Replace(temporary, fullPath, null);
            else
                System.IO.File.Move(temporary, fullPath);
        }
        finally
        {
            if (System.IO.File.Exists(temporary))
                System.IO.File.Delete(temporary);
        }
        Log("CAMERA_MARCADA", "Nome=" + name + "; arquivo=" + fullPath);
    }

    // Repetir um quadro salvo sem recalcular alvo/terços, espelhar o eixo ou interpolar.
    // O chamador mantém esta pose durante a ação, sem enviar novos presets/cortes.
    public void RestorePose(string path, string name)
    {
        RequireCamera();
        if (System.String.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 100)
            throw new System.InvalidOperationException("Câmera: nome da pose salva inválido.");
        System.Xml.XmlConvert.VerifyXmlChars(name);
        string fullPath = CameraPosePath(path);
        System.Xml.XmlDocument document = new System.Xml.XmlDocument();
        document.XmlResolver = null;
        System.Xml.XmlReaderSettings settings = new System.Xml.XmlReaderSettings();
        settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
        settings.XmlResolver = null;
        settings.MaxCharactersInDocument = 1000000;
        using (var stream = new System.IO.FileStream(fullPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
        {
            if (stream.Length > 1000000) throw new System.InvalidOperationException("Câmera: catálogo de poses excede1MB.");
            using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(stream, settings)) document.Load(reader);
        }
        NoCameraPoseLinks(fullPath);
        System.Xml.XmlElement root = document.DocumentElement;
        if (root == null || root.Name != "CamerasMarcadas") throw new System.InvalidOperationException("Câmera: esperado CamerasMarcadas versão1.");
        PoseAttributes(root, "versao");
        if (root.GetAttribute("versao") != "1") throw new System.InvalidOperationException("Câmera: versão de catálogo incompatível.");
        var names = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        SavedCameraPose selected = null;
        foreach (System.Xml.XmlNode node in root.ChildNodes)
        {
            if (PoseWhitespace(node)) continue;
            var entry = node as System.Xml.XmlElement;
            if (entry == null || entry.Name != "Camera") throw new System.InvalidOperationException("Câmera: elemento desconhecido no catálogo.");
            SavedCameraPose pose = ReadCameraPose(entry);
            if (!names.Add(pose.Name)) throw new System.InvalidOperationException("Câmera: nomes de poses repetidos no catálogo.");
            if (pose.Name == name) selected = pose;
        }
        if (selected == null) throw new System.InvalidOperationException("Câmera: pose salva não encontrada: " + name + ".");
        if (hasAxis && Dot(selected.Position - selected.AxisOrigin, selected.AxisNormal) * selected.AxisSide < AxisMargin - 0.001f)
            throw new System.InvalidOperationException("Câmera: pose salva viola o lado/margem do eixo; não será deslocada.");
        // Nenhum setter ocorre antes de validar o catálogo inteiro e a pose selecionada.
        SetDestination(selected.Position, selected.Rotation, selected.Fov, false);
        FixCurrentPose();
        axisOrigin = selected.AxisOrigin; axisNormal = selected.AxisNormal; axisSide = selected.AxisSide;
        CurrentPreset = selected.Preset; opticalMedium = false;
        GTA.Math.Vector3 actualPosition = camera.Position, actualRotation = camera.Rotation;
        float actualFov = camera.FieldOfView;
        if (!IsFinite(actualPosition.X) || !IsFinite(actualPosition.Y) || !IsFinite(actualPosition.Z) ||
            !IsFinite(actualRotation.X) || !IsFinite(actualRotation.Y) || !IsFinite(actualRotation.Z) || !IsFinite(actualFov) ||
            Length(actualPosition - selected.Position) > 0.0001f ||
            System.Math.Abs(AngleDelta(actualRotation.X, selected.Rotation.X)) > 0.001f ||
            System.Math.Abs(AngleDelta(actualRotation.Y, selected.Rotation.Y)) > 0.001f ||
            System.Math.Abs(AngleDelta(actualRotation.Z, selected.Rotation.Z)) > 0.001f ||
            System.Math.Abs(actualFov - selected.Fov) > 0.001f)
            throw new System.InvalidOperationException("Câmera: getters não confirmaram o quadro salvo.");
        RestoredPoseName = selected.Name;
        Log("CAMERA_POSE_RESTAURADA", "Nome=" + selected.Name + "; arquivo=" + fullPath + "; Preset=" + selected.Preset +
            "; Posicao=" + selected.Position + "; Rotacao=" + selected.Rotation + "; FOV=" + Number(selected.Fov) +
            "; OrigemEixo=" + selected.AxisOrigin + "; NormalEixo=" + selected.AxisNormal + "; Lado=" + Number(selected.AxisSide) +
            "; Suave=False; GettersConfirmados=True");
    }

    private sealed class SavedCameraPose
    {
        public string Name, Preset;
        public float Fov, AxisSide;
        public GTA.Math.Vector3 Position, Rotation, AxisOrigin, AxisNormal;
    }

    private static SavedCameraPose ReadCameraPose(System.Xml.XmlElement entry)
    {
        PoseAttributes(entry, "nome,gravadoUtc,preset,fov,formato");
        string name = entry.GetAttribute("nome"), preset = entry.GetAttribute("preset");
        if (System.String.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 100 ||
            entry.GetAttribute("formato") != "16:9" ||
            System.Array.IndexOf(new string[] { "LONGE", "DOIS", "MEDIA", "PERTO", "BAIXA", "ALTA", "ATRAS", "OLHOS", "LIVRE" }, preset) < 0)
            throw new System.InvalidOperationException("Câmera: nome/preset/formato da pose inválido.");
        System.DateTimeOffset timestamp;
        if (!System.DateTimeOffset.TryParseExact(entry.GetAttribute("gravadoUtc"), "o", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out timestamp) || timestamp.Offset != System.TimeSpan.Zero)
            throw new System.InvalidOperationException("Câmera: gravadoUtc exige instante UTC roundtrip.");
        var vectors = new System.Collections.Generic.Dictionary<string, GTA.Math.Vector3>(System.StringComparer.Ordinal);
        float side = 0f;
        foreach (System.Xml.XmlNode node in entry.ChildNodes)
        {
            if (PoseWhitespace(node)) continue;
            var vector = node as System.Xml.XmlElement;
            if (vector == null || (vector.Name != "Posicao" && vector.Name != "Rotacao" && vector.Name != "OrigemEixo" && vector.Name != "NormalEixo") || vectors.ContainsKey(vector.Name))
                throw new System.InvalidOperationException("Câmera: vetores ausentes/desconhecidos/repetidos na pose.");
            PoseAttributes(vector, vector.Name == "OrigemEixo" ? "x,y,z,lado" : "x,y,z");
            foreach (System.Xml.XmlNode child in vector.ChildNodes)
                if (!PoseWhitespace(child)) throw new System.InvalidOperationException("Câmera: vetor não admite conteúdo/elementos internos.");
            float bound = vector.Name == "Rotacao" ? 360f : vector.Name == "NormalEixo" ? 1.01f : 30000f;
            vectors.Add(vector.Name, new GTA.Math.Vector3(PoseNumber(vector.GetAttribute("x"), -bound, bound),
                PoseNumber(vector.GetAttribute("y"), -bound, bound), PoseNumber(vector.GetAttribute("z"), -bound, bound)));
            if (vector.Name == "OrigemEixo") side = PoseNumber(vector.GetAttribute("lado"), -1f, 1f);
        }
        if (vectors.Count != 4 || (side != -1f && side != 1f)) throw new System.InvalidOperationException("Câmera: quatro vetores e lado±1 são obrigatórios.");
        GTA.Math.Vector3 normal = vectors["NormalEixo"];
        if (System.Math.Abs(normal.Z) > 0.0001f || System.Math.Abs(Length(normal) - 1f) > 0.001f)
            throw new System.InvalidOperationException("Câmera: normal do eixo precisa ser horizontal e unitária.");
        return new SavedCameraPose { Name = name, Preset = preset, Fov = PoseNumber(entry.GetAttribute("fov"), 5f, 120f),
            AxisSide = side, Position = vectors["Posicao"], Rotation = vectors["Rotacao"],
            AxisOrigin = vectors["OrigemEixo"], AxisNormal = normal };
    }

    private static void PoseAttributes(System.Xml.XmlElement element, string allowed)
    {
        if (element.NamespaceURI.Length != 0 || element.Prefix.Length != 0)
            throw new System.InvalidOperationException("Câmera: namespaces não pertencem ao contrato da pose.");
        var expected = new System.Collections.Generic.HashSet<string>(allowed.Split(','), System.StringComparer.Ordinal);
        if (element.Attributes.Count != expected.Count) throw new System.InvalidOperationException("Câmera: atributos ausentes/desconhecidos na pose.");
        foreach (System.Xml.XmlAttribute attribute in element.Attributes)
            if (attribute.NamespaceURI.Length != 0 || !expected.Remove(attribute.Name))
                throw new System.InvalidOperationException("Câmera: atributo desconhecido/repetido na pose.");
    }

    private static bool PoseWhitespace(System.Xml.XmlNode node)
    {
        return node.NodeType == System.Xml.XmlNodeType.Comment ||
            ((node.NodeType == System.Xml.XmlNodeType.Whitespace || node.NodeType == System.Xml.XmlNodeType.SignificantWhitespace ||
              node.NodeType == System.Xml.XmlNodeType.Text) && System.String.IsNullOrWhiteSpace(node.Value));
    }

    private static float PoseNumber(string text, float minimum, float maximum)
    {
        float value;
        if (System.String.IsNullOrWhiteSpace(text) || text != text.Trim() ||
            !System.Single.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ||
            !IsFinite(value) || value < minimum || value > maximum)
            throw new System.InvalidOperationException("Câmera: valor numérico da pose inválido/não finito.");
        return value;
    }

    private static string CameraPosePath(string path)
    {
        if (System.String.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path))
            throw new System.InvalidOperationException("Câmera: catálogo exige caminho absoluto local.");
        string full = System.IO.Path.GetFullPath(path);
        if (full.StartsWith("\\\\", System.StringComparison.Ordinal) || full.Length < 3 || full.Substring(2).IndexOf(':') >= 0 ||
            !System.String.Equals(System.IO.Path.GetExtension(full), ".xml", System.StringComparison.OrdinalIgnoreCase))
            throw new System.InvalidOperationException("Câmera: catálogo deve ser XML local, sem rede/stream alternativo.");
        NoCameraPoseLinks(full); return full;
    }

    private static void NoCameraPoseLinks(string path)
    {
        string current = path;
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) &&
                (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw new System.InvalidOperationException("Câmera: catálogo não admite links/junctions.");
            current = System.IO.Path.GetDirectoryName(current);
        }
    }

    public void Cleanup()
    {
        writingC6CloseKnown=writingC6CloseVerified=false;
        handoffCloseKnown = false;
        witnessC2PosePending = witnessC2PoseApplied = witnessC2ActiveVerified = false;
        witnessC2Target = null; witnessC2Objects = null;
        witnessC2RequestedFrame = witnessC2RequestedTime = 0; witnessC2PendingCameraHandle = 0; witnessC2PendingCameraAddress = System.IntPtr.Zero;
        free = false;
        tween = false;
        initialized = false;
        System.Exception error = null;
        if (!System.Object.ReferenceEquals(camera, null))
        {
            try
            {
                if (camera.Exists())
                {
                    camera.IsActive = false;
                    camera.Delete();
                    if (camera.Exists())
                        throw new System.InvalidOperationException("A câmera própria ainda existe após a remoção.");
                }
                camera = null;
            }
            catch (System.Exception ex) { error = ex; Log("ERRO_LIMPEZA_CAMERA", ex.ToString()); }
        }
        if (restorePending)
        {
            try
            {
                if (!System.Object.ReferenceEquals(previousCamera, null) && previousCamera.Exists())
                {
                    previousCamera.IsActive = true;
                    GTA.ScriptCameraDirector.StartRendering();
                }
                else
                    GTA.ScriptCameraDirector.StopRendering(false);
                restorePending = false;
                previousCamera = null;
            }
            catch (System.Exception ex) { error = ex; Log("ERRO_RESTAURAR_CAMERA", ex.ToString()); }
        }
        actorA = null;
        actorB = null;
        actors.Clear();
        hasAxis = false;
        FocusOwnActorFromBehind = false; ownBehindFixedKnown = false; witnessC2Known = false;
        luxuryWalkFrame = false; luxuryWalkLoggedPreset = "";
        opticalMedium = false;
        CurrentPreset = "";
        RestoredPoseName = "";
        if (!System.Object.ReferenceEquals(error, null))
            throw new System.InvalidOperationException("Câmera: restauração incompleta. Consulte o log e tente limpar novamente.", error);
    }

    public void Dispose() { Cleanup(); }

    private void SetDestination(GTA.Math.Vector3 position, GTA.Math.Vector3 rotation, float fov, bool smooth)
    {
        RestoredPoseName = "";
        free = false;
        tween = false;
        camera.StopPointing();
        if (smooth)
        {
            fromPosition = camera.Position;
            fromRotation = camera.Rotation;
            fromFov = camera.FieldOfView;
            toPosition = position;
            toRotation = rotation;
            toFov = fov;
            tweenElapsed = 0f;
            tween = true;
        }
        else
        {
            camera.Position = position;
            camera.Rotation = rotation;
            camera.FieldOfView = fov;
        }
    }

    private GTA.Math.Vector3 KeepSide(GTA.Math.Vector3 position, bool mirror)
    {
        if (!hasAxis) return position;
        float distance = Dot(position - axisOrigin, axisNormal) * axisSide;
        if (mirror && distance < 0f)
        {
            position += axisNormal * (axisSide * -2f * distance);
            distance = -distance;
        }
        if (distance < AxisMargin)
            position += axisNormal * (axisSide * (AxisMargin - distance));
        return position;
    }

    private GTA.Math.Vector3 ComposeThird(GTA.Math.Vector3 position, GTA.Math.Vector3 aim,
        GTA.Math.Vector3 lookDirection, float fov)
    {
        GTA.Math.Vector3 view = HorizontalUnit(aim - position);
        GTA.Math.Vector3 screenRight = new GTA.Math.Vector3(view.Y, -view.X, 0f);
        float look = Dot(lookDirection, screenRight);
        float sign = look < 0f ? -1f : 1f;
        float halfWidth = Length(aim - position) * (float)System.Math.Tan(fov * System.Math.PI / 360.0) * AspectRatio;
        // Deslocar o centro para o lado do olhar deixa o personagem no terço oposto.
        return aim + screenRight * (halfWidth * sign / 3f);
    }

    private static GTA.Math.Vector3 FootPosition(GTA.Ped ped)
    {
        RequirePed(ped);
        GTA.Math.Vector3 result = ped.Position;
        GTA.PedBone left = ped.Bones[GTA.Bone.SkelLeftFoot];
        GTA.PedBone right = ped.Bones[GTA.Bone.SkelRightFoot];
        bool leftValid = left.IsValid && PlausibleBone(result, left.Position);
        bool rightValid = right.IsValid && PlausibleBone(result, right.Position);
        if (leftValid || rightValid)
            result.Z = leftValid && rightValid ? System.Math.Min(left.Position.Z, right.Position.Z) :
                leftValid ? left.Position.Z : right.Position.Z;
        else
            result.Z -= System.Math.Max(0f, ped.HeightAboveGround);
        return result;
    }

    private static GTA.Math.Vector3 HeadPosition(GTA.Ped ped)
    {
        RequirePed(ped);
        GTA.PedBone head = ped.Bones[GTA.Bone.SkelHead];
        if (head.IsValid && PlausibleBone(ped.Position, head.Position))
            return head.Position;
        return FootPosition(ped) + new GTA.Math.Vector3(0f, 0f, 1.65f);
    }

    private static bool PlausibleBone(GTA.Math.Vector3 origin, GTA.Math.Vector3 bone)
    {
        return IsFinite(bone.X) && IsFinite(bone.Y) && IsFinite(bone.Z) && Length(bone - origin) < 3f;
    }

    private void RequireCamera()
    {
        if (!initialized || System.Object.ReferenceEquals(camera, null) || !camera.Exists())
            throw new System.InvalidOperationException("Câmera: monte a cena antes de escolher um plano.");
    }

    private static void RequirePed(GTA.Ped ped)
    {
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists())
            throw new System.InvalidOperationException("Câmera: um personagem não está disponível. Monte a cena novamente.");
    }

    private static void RequireAdjustment(float value, string name)
    {
        if (!IsFinite(value) || System.Math.Abs(value) > 10f)
            throw new System.InvalidOperationException("Câmera: " + name + " deve ficar entre -10 e 10 metros.");
    }

    private static GTA.Math.Vector3 HorizontalUnit(GTA.Math.Vector3 value)
    {
        float length = HorizontalLength(value);
        if (length < 0.0001f)
            throw new System.InvalidOperationException("Câmera: não foi possível calcular a direção horizontal.");
        return new GTA.Math.Vector3(value.X / length, value.Y / length, 0f);
    }

    private static GTA.Math.Vector3 LookRotation(GTA.Math.Vector3 direction)
    {
        float horizontal = HorizontalLength(direction);
        if (Length(direction) < 0.0001f)
            throw new System.InvalidOperationException("Câmera: posição e alvo não podem coincidir.");
        return new GTA.Math.Vector3((float)(System.Math.Atan2(direction.Z, horizontal) * 180.0 / System.Math.PI),
            0f, (float)(System.Math.Atan2(-direction.X, direction.Y) * 180.0 / System.Math.PI));
    }

    private static float HorizontalLength(GTA.Math.Vector3 value)
    {
        return (float)System.Math.Sqrt(value.X * value.X + value.Y * value.Y);
    }

    private static float Length(GTA.Math.Vector3 value)
    {
        return (float)System.Math.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
    }

    private static float Dot(GTA.Math.Vector3 a, GTA.Math.Vector3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
    private static bool IsFinite(float value) { return !System.Single.IsNaN(value) && !System.Single.IsInfinity(value); }
    private static float Clamp(float value, float minimum, float maximum) { return System.Math.Max(minimum, System.Math.Min(maximum, value)); }
    private static bool Key(System.Windows.Forms.Keys key) { return GTA.Game.IsKeyPressed(key); }
    private static float KeyValue(System.Windows.Forms.Keys key) { return Key(key) ? 1f : 0f; }
    private static float NormalizeAngle(float value)
    {
        while (value > 180f) value -= 360f;
        while (value < -180f) value += 360f;
        return value;
    }
    private static float AngleDelta(float from, float to) { return NormalizeAngle(to - from); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static System.Xml.XmlElement VectorElement(System.Xml.XmlDocument document, string name, GTA.Math.Vector3 value)
    {
        System.Xml.XmlElement element = document.CreateElement(name);
        element.SetAttribute("x", Number(value.X));
        element.SetAttribute("y", Number(value.Y));
        element.SetAttribute("z", Number(value.Z));
        return element;
    }
    private void Log(string level, string message)
    {
        if (System.Object.ReferenceEquals(log, null)) return;
        // Uma falha de escrita de log não deve impedir a restauração da câmera.
        try { log(level, message); } catch (System.Exception) { }
    }
}

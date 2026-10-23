// Fonte combinável C#5: sem using e sem classe Script adicional.
// Controla somente props criados por ObjetosDiretor; não adota entidades do mapa.
public sealed class ObjetosSerieDiretor : System.IDisposable
{
    public sealed class PropEvent
    {
        public string Tipo, Ator, Mao, Dicionario, Clipe, Doador, ClipeDoador, Apoio;
        public int InicioMs, EsperaMaximaMs, GestoInicioMs, GestoFimMs;
        internal int IndiceSuperficie;
        public GTA.Math.Vector3 Offset, Rotacao, SuperficieRelativa;
        public bool UsarCentroMedido = true;
    }

    public sealed class PropSpec
    {
        public string Codigo, Recurso, Modelo, OrigemArquivo, OrigemSha256;
        public int OrigemLinha;
        public GTA.Math.Vector3 Deslocamento;
        public float RotacaoZ;
        public bool AssentarNoChao, VisivelInicial;
        public string AtorInicial, MaoInicial;
        public GTA.Math.Vector3 OffsetInicial, RotacaoInicial;
        public string ApoioInicial;
        internal MorfologiaPapel86Diretor.Spec Morph86;
        public GTA.Math.Vector3 SuperficieInicialRelativa;
        public bool UsarCentroInicialMedido = true;
        public System.Collections.Generic.List<PropEvent> Eventos;
        public PropSpec() { AssentarNoChao = true; VisivelInicial = true; Eventos = new System.Collections.Generic.List<PropEvent>(); }
    }

    private sealed class Actor
    {
        public string Id;
        public GTA.Ped Ped;
        public int Handle, Model;
        public System.IntPtr Address;
    }

    private sealed class State
    {
        public PropSpec Spec;
        public GTA.Math.Vector3 Position, Rotation, Min, Max;
        public string Owner, Hand;
        public int PreparedAt;
        public bool Ready;
        public bool SleepBagGripPrepared;
        public GTA.Math.Vector3 SleepBagGripOffset;
        public bool BoundsKnown, GroundTelemetryStarted;
        public int GroundTelemetryAt;
        public bool InitialSupportKnown;
        public bool PickupAnchorKnown;
        public Timed PickupPlacement;
        public GTA.Math.Vector3 PickupPosition, PickupRotation;
        public int[] InitialSurfaceIndices;
        public GTA.Math.Vector3 InitialSupportPoint, InitialSupportNormal, InitialSupportPosition;
    }

    private sealed class Timed
    {
        public State State;
        public PropEvent Event;
        public bool Started, Completed, Measured, PhasePendingLogged;
        public float InitialDistance, InitialSpineDistance;
        public bool SpineInitialMeasured;
        public bool ContactMeasured;
        public int NextContactLog, ContactStartedMs, ContactMinimumMs, NextGuardarLog;
        public float ContactStartDistance, ContactMinimumDistance;
        public string ContactDonor, ContactDonorHand;
        public bool PlacementKnown;
        public GTA.Math.Vector3 PlacementPoint, PlacementNormal;
        public int NextPlacementLog;
        public bool PickupPhaseKnown, PickupProgressObserved;
        public float PickupPreviousPhase;
        public int NextPickupLog;
        public bool PaperHandoffPhaseKnown;
        public float PaperHandoffDonorPhase, PaperHandoffReceiverPhase;
        public int NextPaperHandoffLog;
    }

    private sealed class Source
    {
        public string Hash;
        public readonly System.Collections.Generic.Dictionary<int, string> Lines = new System.Collections.Generic.Dictionary<int, string>();
    }

    private sealed class SurfaceProbe
    {
        public State State;
        public int Ray, StartedAt, RetryStartedAt;
        public int OptionFlags = 7, TraceFlags = 16;
        public bool Known, Synchronous;
        public GTA.Math.Vector3 Point, Normal, Top, Bottom;
        public float LocalX, LocalY;
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> Models =
        new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
    {
        { "celular", "prop_npc_phone_02" }, { "papel", "p_cs_papers_01" },
        { "envelope", "prop_cs_envolope_01" }, { "documentos", "prop_cs_documents_01" },
        { "sacola", "prop_cs_shopping_bag" }, { "bolsa_sono84_preview", "prop_ld_handbag" }, { "sacola_papel_sono84_preview", "prop_paper_bag_small" }, { "bandeja", "prop_food_bs_tray_01" },
        { "comprimidos", "prop_cs_pills" }, { "prancheta", "p_amb_clipboard_01" },
        { "ferramenta_jardim", "prop_tool_shovel" }, { "taca", "prop_wine_glass" },
        { "garrafa_cerveja", "prop_amb_beer_bottle" },
        { "cadeira_simples", "prop_chair_01a" }, { "poltrona", "prop_armchair_01" },
        { "banco", "prop_bench_01a" }, { "banco_sono_c1_02", "prop_bench_02" }, { "mesa", "prop_table_01" }, { "mesa_grande_c8_autor", "v_res_m_dinetble_replace" }, { "mesa_limpeza_bar", "prop_table_07" }, { "bancada_limpeza_candidata", "prop_ff_counter_02" }, { "mesa_banco_candidata", "prop_table_03" },
        { "cadeira_casa", "v_res_fa_chair01" }, { "cama_casa", "v_res_d_bed" },
        { "cama_quarto", "v_res_msonbed" }, { "cama_simples", "v_res_tre_bed1" },
        { "bandeja_servico", "prop_food_bs_tray_03" }, { "bandeja_champanhe_autor", "h4_prop_h4_champ_tray_01b" }, { "container_serie", "prop_container_01a" },
        { "caixas_serie", "prop_box_wood02a_pu" }, { "caixas_madeira_candidata", "prop_box_wood02a" }, { "guarda_roupa", "v_res_tre_wardrobe" },
        { "caixas_ensaio88_wood01a", "prop_box_wood01a" },
        { "caixas_negocio_madeira_01a", "prop_box_wood01a" },
        { "caixas_ensaio88_wood03a", "prop_box_wood03a" },
        { "caixas_ensaio88_wood04a", "prop_box_wood04a" },
        { "caixas_ensaio88_wood05a", "prop_box_wood05a" },
        { "caixas_ensaio88_wood05b", "prop_box_wood05b" },
        { "sapato_mao", "prop_cs_amanda_shoe" }, { "laptop", "prop_laptop_01a" }, { "monitor_crt", "prop_monitor_01b" }, { "teclado_pc", "prop_keyboard_01a" }, { "gabinete_pc", "v_res_pctower" }, { "monitor_crt_a", "prop_monitor_01a" }, { "monitor_crt_b", "v_res_monitor" }, { "monitor_crt_c", "prop_rub_monitor" }, { "monitor_crt_d", "prop_ld_monitor_01" }, { "monitor_crt_e", "prop_monitor_01c" }, { "monitor_crt_f", "prop_monitor_01d" }, { "monitor_crt_g", "v_res_lest_monitor" }, { "monitor_crt_h", "v_res_monitorsquare" }, { "monitor_crt_i", "m23_2_prop_m32_crt_mon_01a" },
        { "foto_quadro", "prop_cs_photoframe_01" }, { "bloco_anotacao", "prop_notepad_01" },
        { "lapis", "prop_pencil_01" }, { "dinheiro", "prop_cash_pile_01" },
        { "lapide", "prop_gravestones_01a" }, { "caixa_remedio", "v_res_tt_pharm2" },
        { "guarda_roupa_aberto_candidato", "v_24_wrd_mesh_wardrobe" }, { "carta_simples", "prop_amanda_note_01" }, { "papel_dobrado_autoral", "prop_amanda_note_01" },
        { "armario_classico_candidato", "v_res_m_armoire" },
        { "porta_armario", "prop_wardrobe_door_01" },
        { "documento_script", "p_cs_script_s" },
        { "papel_preview86_nota_b", "prop_amanda_note_01b" },
        { "papel_preview86_nota_s", "p_amanda_note_01_s" },
        { "papel_preview86_rcss", "p_rcss_folded" },
        { "papel_preview86_x17", "xm_prop_x17_note_paper_01a" },
        { "papel_preview86_rolled", "prop_cs_rolled_paper" },
        { "papel_preview86_envelope", "p_cash_envelope_01_s" },
        { "papel_preview86_file_a", "h4_prop_h4_files_paper_01a" },
        { "papel_preview86_file_b", "h4_prop_h4_files_paper_01b" },
        { "papel_preview86_ng01", "ng_proc_paper_01a" },
        { "papel_preview86_ng02", "ng_proc_paper_02a" },
        { "papel_preview86_ng03", "ng_proc_paper_03a" },
        { "papel_preview86_ng031", "ng_proc_paper_03a001" },
        { "papel_preview86_cs01", "p_cs_papers_01" },
        { "papel_preview86_cs02", "p_cs_papers_02" },
        { "papel_preview86_cs03", "p_cs_papers_03" },
        { "papel_preview86_pamphlet", "prop_cs_pamphlet_01" },
        { "papel_preview86_pamphlet_s", "p_cs_pamphlet_01_s" },
        { "papel_preview86_brochure", "sf_prop_sf_brochure_01a" },
        { "cadeira_entrada_autor", "ex_prop_offchair_exec_02" }
    };
    private static readonly System.Collections.Generic.HashSet<string> Supports =
        new System.Collections.Generic.HashSet<string>(new string[] { "cadeira_entrada_autor", "cadeira_simples", "poltrona", "banco", "banco_sono_c1_02", "mesa", "mesa_grande_c8_autor", "mesa_limpeza_bar", "bancada_limpeza_candidata", "mesa_banco_candidata", "cadeira_casa", "cama_casa", "cama_quarto", "cama_simples" }, System.StringComparer.Ordinal);

    private readonly System.Action<string, string> log;
    private readonly ObjetosDiretor owned;
    private VeiculosSerieDiretor sceneVehicles;
    private readonly System.Collections.Generic.Dictionary<string, Actor> actors = new System.Collections.Generic.Dictionary<string, Actor>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, State> states = new System.Collections.Generic.Dictionary<string, State>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.List<Timed> events = new System.Collections.Generic.List<Timed>();
    private readonly System.Collections.Generic.Dictionary<int, SurfaceProbe> surfaces = new System.Collections.Generic.Dictionary<int, SurfaceProbe>();
    private int nextSurface;
    private bool prepared, ready, disposed, supportCollisionSuppressed;
    private bool initialSupportsPending;
    private MorfologiaPapel86Diretor paperMorph86;
    private bool paperMorph86FinishResetAccepted;
    private bool authoredPaperC5Scope;
    private PapelDobradoAutoralC5Diretor authoredPaperC5;
    private bool pocket98Scope, pocket98OffsetKnown;
    private GTA.Math.Vector3 pocket98Offset;
    private PapelBolso98Diretor pocket98;
    private bool measuredTablesRecheckPending, measuredTablesRecheckDone;
    private bool restrictHandOwnership;
    private bool technicalBoxes, productionBoxes, productionVolumesLogged;
    private bool dismissalHandoffs, sleepBenchC1, officeCellBodyPreview84, largeTableC8, largeTableC8Logged;
public bool IsOfficeCellBodyPreview84 { get { return officeCellBodyPreview84 && IsReady; } }
public bool HasOfficeBag84Preview { get { return officeCellBodyPreview84 && states.ContainsKey("SACOLA_CALIBRACAO84"); } }
private bool officeBag84Prepared, officeBag84Verified;
private int officeBag84Frame = -1, officeBag84NextLog;
private GTA.Math.Vector3 officeBag84Offset;
    private int dismissalPartNumber;
    private State initialSupportState;
    private int lastElapsed;
    private GTA.Math.Vector3 anchor, right, forward;
    private float groundProbeRise = 2f;
    private const int TimeoutMs = 5000;

    public ObjetosSerieDiretor(System.Action<string, string> log) { this.log = log; owned = new ObjetosDiretor(log); }
    public int Count { get { return states.Count; } }
    public bool IsReady
    {
        get
        {
            if (!prepared || !ready || disposed || initialSupportsPending) return false;
            if (paperMorph86 != null && !paperMorph86.IsReady) return false;
            foreach (Timed timed in events) if (timed.Event.Tipo == "colocar" && !timed.PlacementKnown) return false;
            return true;
        }
    }
    public bool IsFinished
    { get { if (!IsReady) return false; foreach (Timed timed in events) if (!timed.Completed) return false; return true; } }

    // Ligar somente antes de Update; a consulta confirma identidade e veiculo do helper proprio.
    public void BindVehicles(VeiculosSerieDiretor vehicles)
    {
        Active();
        if (vehicles == null || (IsReady && lastElapsed >= 0)) throw Error("ligue veiculos proprios antes da acao");
        sceneVehicles = vehicles;
    }

    public void Prepare(System.Collections.Generic.Dictionary<string, GTA.Ped> ownActors,
                        System.Collections.Generic.IEnumerable<PropSpec> resourceSpecs,
                        GTA.Math.Vector3 sceneAnchor, GTA.Math.Vector3 sceneRight, GTA.Math.Vector3 sceneForward, string sceneLocal = null, string technicalBoxSceneId = null, string paperPreviewSceneId = null, string productionBoxSceneId = null, int productionBoxDurationMs = 0, string dismissalSceneId = null, string dismissalPart = null, string sleepBenchSceneId = null, string largeTableC8SceneId = null, string authoredPaperC5SceneId = null, string pocket98SceneId = null)
    {
        Active();
        if (prepared || states.Count != 0 || owned.Count != 0) throw Error("limpe os recursos anteriores antes de preparar outra cena");
        Vector(sceneAnchor, 30000f, "âncora"); Vector(sceneRight, 1.01f, "eixo lateral"); Vector(sceneForward, 1.01f, "eixo frontal");
        if (System.Math.Abs(sceneRight.Length() - 1f) > 0.01f || System.Math.Abs(sceneForward.Length() - 1f) > 0.01f ||
            System.Math.Abs(sceneRight.Z) > 0.01f || System.Math.Abs(sceneForward.Z) > 0.01f ||
            System.Math.Abs(GTA.Math.Vector3.Dot(sceneRight, sceneForward)) > 0.01f)
            throw Error("eixos precisam ser horizontais, unitários e perpendiculares");
        pocket98Scope = pocket98SceneId == "teste_fila_98";
        if (pocket98SceneId != null && (!pocket98Scope || sceneLocal != "MANSAO_CHEFE" || productionBoxDurationMs != 10000 ||
            authoredPaperC5SceneId != null || technicalBoxSceneId != null || paperPreviewSceneId != null || productionBoxSceneId != null ||
            dismissalSceneId != null || sleepBenchSceneId != null || largeTableC8SceneId != null))
            throw Error("Bolso98 only own technical10s/MANSAO_CHEFE, no other capability");
        authoredPaperC5Scope = authoredPaperC5SceneId == "EP08_C05";
        if (authoredPaperC5SceneId != null && (!authoredPaperC5Scope || sceneLocal != "CASA_VO_INTERIOR" || productionBoxDurationMs != 12000 ||
            technicalBoxSceneId != null || paperPreviewSceneId != null || productionBoxSceneId != null || dismissalSceneId != null ||
            sleepBenchSceneId != null || largeTableC8SceneId != null))
            throw Error("authored folded visual exclusive EP08_C05/CASA_VO_INTERIOR/12s, separate from all technical/other capabilities");
        technicalBoxes = technicalBoxSceneId == "teste_fila_88";
        if (technicalBoxSceneId != null && (!technicalBoxes || sceneLocal != "AEROPORTO_HANGAR"))
            throw Error("ensaio88 de caixas exige ID tecnico e marca real AEROPORTO_HANGAR");
        productionBoxes = productionBoxSceneId == "EP07_C02";
        if (productionBoxSceneId != null && (!productionBoxes || sceneLocal != "GALPAO_CYPRESS" ||
            productionBoxDurationMs != 16000 || technicalBoxSceneId != null || paperPreviewSceneId != null))
            throw Error("caixas producao somente EP07_C02/GALPAO_CYPRESS/16s, opt-in separado de todos ensaios");
        productionVolumesLogged = false;
        dismissalHandoffs = dismissalSceneId == "EP13_C02";
        dismissalPartNumber = dismissalPart == "A_PRIMEIRA" ? 1 : dismissalPart == "B_SEGUNDA" ? 2 : dismissalPart == "C_TERCEIRA" ? 3 : 0;
        if (dismissalSceneId != null || dismissalPart != null)
            if (!dismissalHandoffs || dismissalPartNumber == 0 || sceneLocal != "BOATE_DENTRO" || productionBoxDurationMs != 14000 ||
                technicalBoxSceneId != null || paperPreviewSceneId != null || productionBoxSceneId != null)
                throw Error("demissao somente EP13_C02/BOATE_DENTRO/14s/parte A-B-C, opt-in isolado");
        officeCellBodyPreview84 = sleepBenchSceneId == "teste_fila_84";
        sleepBenchC1 = sleepBenchSceneId == "EP10_C01" || officeCellBodyPreview84;
        if (sleepBenchSceneId != null && (!sleepBenchC1 || (sceneLocal != "PRAIA_DELPERRO" && sceneLocal != "CENTRO_LEGION") || productionBoxDurationMs != 10000 ||
            technicalBoxSceneId != null || paperPreviewSceneId != null || productionBoxSceneId != null || dismissalSceneId != null || dismissalPart != null))
            throw Error("sleep bench capability only EP10_C01/PRAIA_DELPERRO-or-CENTRO_LEGION/10s, separate from all other scenes");
        if (officeCellBodyPreview84 && sceneLocal != "CENTRO_LEGION") throw Error("Sono84 own bench only actual CENTRO_LEGION mark.");
        largeTableC8 = largeTableC8SceneId == "EP15_C08"; largeTableC8Logged = false;
        if (largeTableC8SceneId != null && (!largeTableC8 || sceneLocal != "MANSAO_CHEFE" || productionBoxDurationMs != 12000 ||
            technicalBoxSceneId != null || paperPreviewSceneId != null || productionBoxSceneId != null ||
            dismissalSceneId != null || dismissalPart != null || sleepBenchSceneId != null))
            throw Error("large table capability only EP15_C08/MANSAO_CHEFE/12s, separate from all other scenes");
        anchor = sceneAnchor; right = sceneRight; forward = sceneForward;
        groundProbeRise = sceneLocal == "BOATE_DENTRO" || sceneLocal == "CASA_VO_INTERIOR" ? 0.5f : 2f;
        if (ownActors == null || ownActors.Count > 5 || resourceSpecs == null) throw Error("informe elenco próprio0–5 e lista de props0–8");
        System.Collections.Generic.HashSet<int> handles = new System.Collections.Generic.HashSet<int>();
        foreach (var entry in ownActors)
        {
            string id = (entry.Key ?? "").ToUpperInvariant(); Identifier(id, "ator");
            GTA.Ped ped = entry.Value; RequireActor(ped);
            if (actors.ContainsKey(id) || !handles.Add(ped.Handle)) throw Error("ator/handle repetido");
            actors.Add(id, new Actor { Id = id, Ped = ped, Handle = ped.Handle, Model = ped.Model.Hash, Address = ped.MemoryAddress });
        }
        try
        {
            foreach (PropSpec original in resourceSpecs)
            {
                if (states.Count >= 8) throw Error("máximo de8props por cena");
                if (original == null) throw Error("especificação nula de prop");
                PropSpec spec = Copy(original); Validate(spec);
                if (states.ContainsKey(spec.Codigo)) throw Error("código de objeto repetido: " + spec.Codigo);
                State state = new State { Spec = spec, Position = ToWorld(spec.Deslocamento),
                    Rotation = new GTA.Math.Vector3(0f, 0f, Heading() + spec.RotacaoZ), PreparedAt = GTA.Game.GameTime };
                states.Add(spec.Codigo, state);
                foreach (PropEvent item in spec.Eventos) events.Add(new Timed { State = state, Event = item });
            }
            foreach (Timed timed in events)
                if (timed.Event.Tipo == "colocar")
                {
                    State support;
                    if (!states.TryGetValue(timed.Event.Apoio, out support) || support == timed.State || !Supports.Contains(support.Spec.Recurso))
                        throw Error("colocar exige outro apoio próprio catalogado, visível e assentado");
                }
            foreach (State state in states.Values)
                if (!System.String.IsNullOrEmpty(state.Spec.ApoioInicial))
                {
                    State support;
                    if (!states.TryGetValue(state.Spec.ApoioInicial, out support) || support == state ||
                        !Supports.Contains(support.Spec.Recurso) || !support.Spec.AssentarNoChao || !support.Spec.VisivelInicial)
                        throw Error("apoio inicial exige outro suporte próprio catalogado, visível e assentado");
                }
            if (dismissalHandoffs) RequireDismissalEnvelopeContract();
            foreach (Timed timed in events) if (timed.Event.Tipo == "pegar") restrictHandOwnership = true;
            ValidatePickupHands(GetSpecs(), 180001);
            VerifySourceEvidence(GetSpecs()); // Toda fonte validada antes de pedir qualquer modelo/criar entidade.
            if (technicalBoxes)
            {
                if (states.Count != 2 || actors.Count != 1 || !actors.ContainsKey("DJANIRA_POBRE"))
                    throw Error("ensaio88 exige duas caixas proprias e Djanira tecnica parada");
                string sameModel = null;
                foreach (State state in states.Values)
                {
                    if (!TechnicalBox(state) || !state.Spec.AssentarNoChao || !state.Spec.VisivelInicial ||
                        !System.String.IsNullOrEmpty(state.Spec.AtorInicial) || state.Spec.Eventos.Count != 0 ||
                        !System.String.IsNullOrEmpty(state.Spec.ApoioInicial))
                        throw Error("ensaio88 aceita somente dois modelos literais iguais, livres/visiveis/assentados sem eventos");
                    if (sameModel != null && sameModel != state.Spec.Modelo)
                        throw Error("ensaio88 exige o mesmo modelo nas duas caixas proprias");
                    sameModel = state.Spec.Modelo;
                }
            }
            if (productionBoxes)
            {
                if (actors.Count != 4 || !actors.ContainsKey("O_CHEFE") || !actors.ContainsKey("CAPANGA_1") ||
                    !actors.ContainsKey("CAPANGA_2") || !actors.ContainsKey("DJANIRA_POBRE") || states.Count != 3 ||
                    !states.ContainsKey("CONTAINER_CYPRESS") || !states.ContainsKey("CAIXAS_NEGOCIO_1") || !states.ContainsKey("CAIXAS_NEGOCIO_2"))
                    throw Error("EP07_C02 exige Chefe/CAP1/CAP2/Djanira e exatamente um container e duas caixas proprias");
                foreach (State state in states.Values)
                    if (!ProductionNegotiationProp(state) || !state.Spec.AssentarNoChao || !state.Spec.VisivelInicial ||
                        !System.String.IsNullOrEmpty(state.Spec.AtorInicial) || state.Spec.Eventos.Count != 0 ||
                        !System.String.IsNullOrEmpty(state.Spec.ApoioInicial) || state.Spec.Deslocamento.Z != 0f)
                        throw Error("EP07_C02 somente container literal e duas wood01a livres/visiveis/chao sem eventos/attachment/apoio");
            }
            events.Sort(delegate(Timed a, Timed b) { int n = a.Event.InicioMs.CompareTo(b.Event.InicioMs); return n != 0 ? n : System.StringComparer.Ordinal.Compare(a.State.Spec.Codigo, b.State.Spec.Codigo); });
            if (paperPreviewSceneId != null)
            {
                if (paperPreviewSceneId != "teste_fila_86" || technicalBoxes || productionBoxes || states.Count != 1 || actors.Count != 1 || !actors.ContainsKey("DJANIRA_POBRE"))
                    throw Error("preview86 literal: um ator e um prop, nunca producao");
                foreach (State state in states.Values)
                    if (state.Spec.Codigo != "PAPEL_PROVA" || !IsPaperPreviewResource86(state.Spec.Recurso) ||
                        state.Spec.AssentarNoChao || !state.Spec.VisivelInicial || state.Spec.AtorInicial != "DJANIRA_POBRE" ||
                        state.Spec.Eventos.Count != 0 || !System.String.IsNullOrEmpty(state.Spec.ApoioInicial))
                        throw Error("preview86: prop visivel preso na propria mao, sem chao/eventos/apoio");
            }
            foreach (State state in states.Values)
                if (state.Spec.Morph86 != null && (paperPreviewSceneId != "teste_fila_86" || states.Count != 1 || actors.Count != 1 ||
                    state.Spec.Codigo != "PAPEL_PROVA" || !MorfologiaPapel86Diretor.IsLiteralPaper(state.Spec.Recurso, state.Spec.Modelo) ||
                    state.Spec.AtorInicial != "DJANIRA_POBRE" || state.Spec.MaoInicial != "direita" || state.Spec.AssentarNoChao ||
                    state.Spec.Eventos.Count != 0 || !state.Spec.VisivelInicial || !System.String.IsNullOrEmpty(state.Spec.ApoioInicial)))
                    throw Error("morfologia86 exclusiva nota_s propria, parada na mao direita, sem producao");
            if (authoredPaperC5Scope)
            {
                State paper;
                if (actors.Count != 2 || !actors.ContainsKey("DONA_IRACI") || !actors.ContainsKey("DJANIRA_POBRE") || states.Count != 3 ||
                    !states.ContainsKey("CADEIRA_DONA_IRACI") || !states.ContainsKey("CADEIRA_DJANIRA_POBRE") || !states.TryGetValue("PAPEL_IRACI", out paper) ||
                    paper.Spec.Recurso != "papel_dobrado_autoral" || paper.Spec.Modelo != "prop_amanda_note_01" || paper.Spec.Morph86 != null ||
                    paper.Spec.AssentarNoChao || !paper.Spec.VisivelInicial || paper.Spec.AtorInicial != "DONA_IRACI" || paper.Spec.MaoInicial != "direita" ||
                    paper.Spec.ApoioInicial != null || paper.Spec.Eventos.Count != 1 || events.Count != 1 ||
                    !IsGrandmaPaperHandoff(events[0]) || events[0].Event.InicioMs != 1000 || events[0].Event.EsperaMaximaMs != 2000)
                    throw Error("C5 folded visual requires the two registered seated actors/chairs and one own donor-right paper transfer1..3");
            }
            if (largeTableC8)
            {
                State table, companion;
                bool chef = actors.Count == 1 && actors.ContainsKey("O_CHEFE");
                bool dinjer = actors.Count == 1 && actors.ContainsKey("DINJER");
                if ((!chef && !dinjer) || states.Count != 2 || !states.TryGetValue("MESA_JANTAR", out table) ||
                    table.Spec.Recurso != "mesa_grande_c8_autor" || table.Spec.Modelo != "v_res_m_dinetble_replace" ||
                    !table.Spec.AssentarNoChao || !table.Spec.VisivelInicial || table.Spec.AtorInicial != null ||
                    table.Spec.Deslocamento.Z != 0f || table.Spec.Eventos.Count != 0 || table.Spec.ApoioInicial != null ||
                    !states.TryGetValue(chef ? "CADEIRA_CHEFE" : "BANDEJA_SERVICO", out companion))
                    throw Error("C8 large table requires one literal own table and sole Chefe/chair or sole Dinjer/tray");
                if (chef && (companion.Spec.Recurso != "cadeira_simples" || !companion.Spec.AssentarNoChao ||
                    !companion.Spec.VisivelInicial || companion.Spec.AtorInicial != null || companion.Spec.Eventos.Count != 0))
                    throw Error("C8 Chefe requires one actual own chair, no event/attachment");
                if (dinjer && (companion.Spec.Recurso != "bandeja_champanhe_autor" || companion.Spec.AssentarNoChao ||
                    !companion.Spec.VisivelInicial || companion.Spec.AtorInicial != "DINJER" || companion.Spec.Eventos.Count != 0))
                    throw Error("C8 Dinjer requires the own champagne tray in her hand for the full window");
            }
            if (officeCellBodyPreview84)
            {
                State bench;
                if ((states.Count != 1 && states.Count != 2) || actors.Count != 1 || !actors.ContainsKey("DJANIRA_POBRE") ||
                    !states.TryGetValue("BANCO_PRAIA", out bench) || bench.Spec.Recurso != "banco_sono_c1_02" ||
                    bench.Spec.Modelo != "prop_bench_02" || !bench.Spec.AssentarNoChao || !bench.Spec.VisivelInicial ||
                    bench.Spec.AtorInicial != null || bench.Spec.Eventos.Count != 0)
    throw Error("Sono84 requires exact own bench02/Djanira and at most the literal technical compact bag.");
if (states.Count == 2)
{
    State bag;
    if (!states.TryGetValue("SACOLA_CALIBRACAO84", out bag) || !OfficeBag84Spec(bag.Spec) || bag.Spec.AssentarNoChao || !bag.Spec.VisivelInicial ||
        bag.Spec.AtorInicial != "DJANIRA_POBRE" || bag.Spec.MaoInicial != "direita" ||
        bag.Spec.OffsetInicial != GTA.Math.Vector3.Zero || bag.Spec.RotacaoInicial != GTA.Math.Vector3.Zero ||
        bag.Spec.Deslocamento != GTA.Math.Vector3.Zero || bag.Spec.RotacaoZ != 0f ||
        bag.Spec.ApoioInicial != null || bag.Spec.Eventos.Count != 0 || bag.Spec.Morph86 != null)
        throw Error("Sono84 optional bag is exact own handbag/normal initial right hand/zero offsets/no events, technical only.");
}
Log("SONO84_BANCO_CANDIDATO", "Scene=teste_fila_84; Model=prop_bench_02; PhysicsAndFiveOwnHitsRequired=True; OptionalOwnBag=" + HasOfficeBag84Preview + "; Abraco=False; ProductionReady=False");
            }
            if (sleepBenchC1 && !officeCellBodyPreview84)
            {
                State bench, bag;
                if (states.Count != 2 || actors.Count != 1 || !actors.ContainsKey("DJANIRA_POBRE") ||
                    !states.TryGetValue("BANCO_PRAIA", out bench) || !states.TryGetValue("SACOLA_DJANIRA", out bag) ||
                    bench.Spec.Recurso != "banco_sono_c1_02" || bench.Spec.Modelo != "prop_bench_02" ||
                    !bench.Spec.AssentarNoChao || !bench.Spec.VisivelInicial || bench.Spec.AtorInicial != null || bench.Spec.Eventos.Count != 0 ||
                    bag.Spec.Recurso != "sacola" || bag.Spec.Modelo != "prop_cs_shopping_bag" || bag.Spec.AssentarNoChao ||
                    !bag.Spec.VisivelInicial || bag.Spec.AtorInicial != "DJANIRA_POBRE" || bag.Spec.Eventos.Count != 0)
                    throw Error("C1 sleep bench requires exact own bench02 and Djanira shopping bag, no extra prop/event");
                Log("BANCO_SONO_C1_CANDIDATA_FISICA", "Scope=EP10_C01; Model=prop_bench_02; BeforePose=True; Ready=False; PhysicsAndFiveOwnHitsRequired=True; COLFalseIsNotLoaded=True");
            }
            foreach (State state in states.Values) owned.RequestProp(state.Spec.Codigo, state.Spec.Modelo, state.Position, state.Rotation, true, technicalBoxSceneId, paperPreviewSceneId,
                productionBoxes && ProductionBox(state) ? productionBoxSceneId : null,
                sleepBenchC1 && state.Spec.Codigo == "BANCO_PRAIA" ? sleepBenchSceneId : null,
                largeTableC8 && state.Spec.Recurso == "mesa_grande_c8_autor" ? largeTableC8SceneId : null,
                officeCellBodyPreview84 && state.Spec.Recurso == "sacola_papel_sono84_preview" ? "teste_fila_84" : null);
            prepared = true; ready = states.Count == 0; lastElapsed = -1;
            Log("OBJETOS_SERIE_PREPARO", "Quantidade=" + states.Count + "; Estado=teste visual pendente");
        }
        catch
        {
            if (owned.Count == 0) { states.Clear(); actors.Clear(); events.Clear(); }
            throw;
        }
    }

    // Um Tick: streaming/criação são delegados ao helper que limita uma criação/frame.
    public void PollLoading()
    {
        Active();
        if (!prepared) throw Error("Prepare precisa ser chamado antes do carregamento");
        RequireActors();
        owned.Update();
        if (ready)
        {
            RequireProps(); PollSurfaces();
            if (paperMorph86 != null) { paperMorph86.Poll(); return; }
            if (technicalBoxes) RequireTechnicalBoxesGeometry();
            if (productionBoxes) RequireProductionNegotiationGeometry();
            if (initialSupportsPending)
            {
                if (!PollInitialSupports()) return;
                initialSupportsPending = false;
                foreach (State state in states.Values) owned.GetProp(state.Spec.Codigo).IsCollisionEnabled = true;
                ResetCore();
                Log("OBJETOS_SERIE_PRONTOS", "Quantidade=" + states.Count + "; ApoiosIniciais=mesh real antes da contagem");
                return;
            }
            PollPlacementSurfaces(); return;
        }
        if (!owned.IsReady) return;
        if (!supportCollisionSuppressed)
        {
            // As consultas de chão devem enxergar o mapa, sem o assento dos props próprios.
            foreach (State state in states.Values) owned.GetProp(state.Spec.Codigo).IsCollisionEnabled = false;
            supportCollisionSuppressed = true;
            Log("OBJETOS_SERIE_GROUND_COLLISION", "PropsProprios=" + states.Count + "; Colisao=False; Medicao=proximo_frame");
            return;
        }
        bool all = true;
        foreach (State state in states.Values)
        {
            if (state.Ready) continue;
            GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
            prop.IsVisible = false;
            prop.Model.GetDimensions(out state.Min, out state.Max);
            Bounds(state.Min, state.Max);
            state.BoundsKnown = true;
            if (state.Spec.AssentarNoChao && !Ground(state, prop)) { all = false; continue; }
            state.Position = prop.Position; state.Rotation = prop.Rotation;
            state.Ready = true;
        }
        if (!all) return;
        if (!measuredTablesRecheckDone)
        {
            if (!measuredTablesRecheckPending)
            {
                bool any = false;
                foreach (State state in states.Values)
                    if (state.Spec.Modelo == "prop_table_01" ||
                        (state.Spec.Codigo == "MESA_LIMPEZA" && state.Spec.Modelo == "prop_ff_counter_02") ||
                        (technicalBoxes && TechnicalBox(state)) || (productionBoxes && ProductionBox(state)) ||
                        (sleepBenchC1 && state.Spec.Codigo == "BANCO_PRAIA" && state.Spec.Recurso == "banco_sono_c1_02") ||
                        (largeTableC8 && state.Spec.Recurso == "mesa_grande_c8_autor"))
                    {
                        GTA.Prop table = owned.GetProp(state.Spec.Codigo);
                        table.IsCollisionEnabled = true; table.IsVisible = true;
                        owned.RecheckMeasuredTableSurface(state.Spec.Codigo); any = true;
                    }
                if (any)
                {
                    measuredTablesRecheckPending = true;
                    return; // Nao soma cinco starts aos cinco probes de chao deste Tick.
                }
            }
            // owned.IsReady acima so passa depois dos cinco hits no transform assentado.
            measuredTablesRecheckPending = false; measuredTablesRecheckDone = true;
        }
        foreach (State state in states.Values)
            if (!System.String.IsNullOrEmpty(state.Spec.ApoioInicial)) initialSupportsPending = true;
        if (initialSupportsPending)
        {
            // Chão original já medido. Só os apoios entram nos raios; props sobre eles não causam auto-hit.
            foreach (State state in states.Values)
            {
                GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
                prop.IsCollisionEnabled = Supports.Contains(state.Spec.Recurso);
                prop.IsVisible = Supports.Contains(state.Spec.Recurso) && state.Spec.VisivelInicial;
            }
            ready = true;
            return; // As cinco novas consultas começam no Tick seguinte, sem somar ao chão deste Tick.
        }
        // Só depois de todos os apoios medidos, antes de Reset/Attach e suas regras de colisão.
        foreach (State state in states.Values) owned.GetProp(state.Spec.Codigo).IsCollisionEnabled = true;
        Log("OBJETOS_SERIE_GROUND_COLLISION", "PropsProprios=" + states.Count + "; Colisao=True; Apoios=concluidos");
        ready = true;
        ResetCore();
        foreach (State state in states.Values)
            if (state.Spec.Morph86 != null)
            {
                paperMorph86 = new MorfologiaPapel86Diretor(log, state.Spec.Morph86, owned.GetProp(state.Spec.Codigo), actors["DJANIRA_POBRE"].Ped);
                Log("OBJETOS_SERIE_MORPH86_PENDING", "Scope=PREPARACAO86; IsReady=False; ActorTask=False; ColliderProved=False; ProductionApproval=False");
                return;
            }
        if (technicalBoxes) RequireTechnicalBoxesGeometry();
        if (productionBoxes) RequireProductionNegotiationGeometry();
        if (largeTableC8) RequireLargeTableC8Geometry();
        Log("OBJETOS_SERIE_PRONTOS", "Quantidade=" + states.Count + "; Apoios=geometria real; assento/pose não comprovados visualmente");
    }

    // Recursos literais para inspecao86; parser rejeita uso em producao.
    public static bool IsPaperPreviewResource86(string resource)
    {
        return resource == "papel_preview86_nota_b" || resource == "papel_preview86_nota_s" || resource == "papel_preview86_rcss" ||
            resource == "papel_preview86_x17" || resource == "papel_preview86_rolled" || resource == "papel_preview86_envelope" || resource == "papel_preview86_file_a" || resource == "papel_preview86_file_b" ||
            resource == "papel_preview86_ng01" || resource == "papel_preview86_ng02" || resource == "papel_preview86_ng03" || resource == "papel_preview86_ng031" || resource == "papel_preview86_cs01" || resource == "papel_preview86_cs02" || resource == "papel_preview86_cs03" ||
            resource == "papel_preview86_pamphlet" || resource == "papel_preview86_pamphlet_s" || resource == "papel_preview86_brochure";
    }

    public static bool IsPaperPreviewModel86(string model)
    {
        foreach (var item in Models) if (IsPaperPreviewResource86(item.Key) && item.Value == model) return true;
        return false;
    }

    public static bool IsTechnicalOnlyBoxResource88(string resource)
    {
        return resource == "caixas_ensaio88_wood01a" || resource == "caixas_ensaio88_wood03a" ||
            resource == "caixas_ensaio88_wood04a" || resource == "caixas_ensaio88_wood05a" ||
            resource == "caixas_ensaio88_wood05b";
    }

    public static bool IsProductionBoxResourceC2(string resource)
    { return resource == "caixas_negocio_madeira_01a"; }

    private static bool ProductionBox(State state)
    {
        return IsProductionBoxResourceC2(state.Spec.Recurso) && state.Spec.Modelo == "prop_box_wood01a" &&
            (state.Spec.Codigo == "CAIXAS_NEGOCIO_1" || state.Spec.Codigo == "CAIXAS_NEGOCIO_2") && state.Spec.RotacaoZ == 0f;
    }

    private static bool ProductionNegotiationProp(State state)
    {
        return ProductionBox(state) || (state.Spec.Codigo == "CONTAINER_CYPRESS" &&
            state.Spec.Recurso == "container_serie" && state.Spec.Modelo == "prop_container_01a" && state.Spec.RotacaoZ == 90f);
    }

    // Read-only native dimensions/transforms: nao desloca ator, container ou caixa para encobrir cruzamento.
    // Cada caixa continua exigindo as mesmas cinco own rays antes/depois do assentamento.
    // Container mantem o caminho original de streaming/COL; este opt-in nao o flexibiliza.
    private void RequireProductionNegotiationGeometry()
    {
        string intersection = null;
        foreach (State state in states.Values)
        {
            if (!ProductionNegotiationProp(state) || !state.BoundsKnown || !state.Ready)
                throw Error("EP07_C02 perdeu contrato/bounds/assentamento dos tres props proprios");
            if (ProductionBox(state)) owned.RequireProductionBoxGeometry(state.Spec.Codigo);
            GTA.Prop own = owned.GetProp(state.Spec.Codigo);
            if (!own.HasPhysics || !own.IsPositionFrozen || !own.IsVisible || !own.IsCollisionEnabled || own.IsAttached())
                throw Error("EP07_C02 perdeu physics/visibilidade/colisao/free-transform de prop proprio");
            Vector(own.Position, 30000f, "posicao propria EP07_C02"); Vector(own.Rotation, 360f, "rotacao propria EP07_C02");
            float headingDifference = (own.Rotation.Z - state.Rotation.Z + 540f) % 360f - 180f;
            if ((own.Position - state.Position).Length() > 0.01f ||
                System.Math.Abs(own.Rotation.X - state.Rotation.X) > 0.1f ||
                System.Math.Abs(own.Rotation.Y - state.Rotation.Y) > 0.1f || System.Math.Abs(headingDifference) > 0.1f)
                throw Error("EP07_C02 transform proprio mudou depois do assentamento medido");
            if (!productionVolumesLogged)
                Log("CAIXA_PRODUCAO_C2_BOUNDS", "Codigo=" + state.Spec.Codigo + "; Modelo=" + state.Spec.Modelo +
                    "; Handle=" + own.Handle + "; Address=" + own.MemoryAddress + "; Position=" + GroundVector(own.Position) +
                    "; Rotation=" + GroundVector(own.Rotation) + "; BoundsMin=" + GroundVector(state.Min) + "; BoundsMax=" + GroundVector(state.Max));
            foreach (Actor actor in actors.Values)
            {
                GTA.Math.Vector3 min, max, localMin, localMax;
                actor.Ped.Model.GetDimensions(out min, out max); Bounds(min, max);
                RelativeNativeBounds(own, actor.Ped, min, max, out localMin, out localMax);
                bool outside = BoundsOutside(localMin, localMax, state.Min, state.Max);
                if (!productionVolumesLogged || !outside)
                    Log("CAIXA_PRODUCAO_C2_ACTOR_BOUNDS", "Codigo=" + state.Spec.Codigo + "; Ator=" + actor.Id +
                        "; ActorHandle=" + actor.Handle + "; ActorPosition=" + GroundVector(actor.Ped.Position) +
                        "; ActorRotation=" + GroundVector(actor.Ped.Rotation) + "; LocalMin=" + GroundVector(localMin) +
                        "; LocalMax=" + GroundVector(localMax) + "; Outside=" + outside + "; SetterTransform=False");
                if (!outside && intersection == null) intersection = "ator " + actor.Id + " cruza " + state.Spec.Codigo;
            }
            foreach (State other in states.Values)
            {
                if (other == state) continue;
                GTA.Prop otherProp = owned.GetProp(other.Spec.Codigo);
                GTA.Math.Vector3 localMin, localMax;
                RelativeNativeBounds(own, otherProp, other.Min, other.Max, out localMin, out localMax);
                bool outside = BoundsOutside(localMin, localMax, state.Min, state.Max);
                if (!productionVolumesLogged || !outside)
                    Log("CAIXA_PRODUCAO_C2_PROP_BOUNDS", "Codigo=" + state.Spec.Codigo + "; Outro=" + other.Spec.Codigo +
                        "; LocalMin=" + GroundVector(localMin) + "; LocalMax=" + GroundVector(localMax) +
                        "; Outside=" + outside + "; SetterTransform=False");
                if (!outside && intersection == null) intersection = "prop " + other.Spec.Codigo + " cruza " + state.Spec.Codigo;
            }
        }
        productionVolumesLogged = true;
        if (intersection != null) throw Error("EP07_C02 volume conservativo nativo rejeitado: " + intersection + "; corrigir marcas no preparo apos diagnostico, sem reposicionamento por frame");
    }

    // Somente geometria nativa medida; nenhum XYZ absoluto/modelo de Building/ajuste de corpo.
    private void RequireLargeTableC8Geometry()
    {
        State table = states["MESA_JANTAR"];
        GTA.Prop prop = owned.GetProp(table.Spec.Codigo);
        if (!table.BoundsKnown || !prop.HasPhysics || !prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled)
            throw Error("C8 large table lost actual measured bounds/physics/visibility/collision");
        if (!largeTableC8Logged)
            Log("MESA_C8_DIMENSOES_NATIVAS", "Model=" + table.Spec.Modelo + "; Position=" + prop.Position +
                "; Rotation=" + prop.Rotation + "; Min=" + table.Min + "; Max=" + table.Max +
                "; ModelCollisionLoaded=" + prop.Model.IsCollisionLoaded + "; SizeSemanticApproval=False; VisualApproval=False");
        foreach (Actor actor in actors.Values)
        {
            GTA.Math.Vector3 minimum, maximum, localMinimum, localMaximum;
            actor.Ped.Model.GetDimensions(out minimum, out maximum);
            Bounds(minimum, maximum);
            RelativeNativeBounds(prop, actor.Ped, minimum, maximum, out localMinimum, out localMaximum);
            bool outside = BoundsOutside(localMinimum, localMaximum, table.Min, table.Max);
            if (!largeTableC8Logged || !outside)
                Log("MESA_C8_ATOR_VOLUME", "Ator=" + actor.Id + "; Root=" + actor.Ped.Position +
                    "; LocalMin=" + localMinimum + "; LocalMax=" + localMaximum + "; Outside=" + outside +
                    "; SetterTransform=False; VisualApproval=False");
            if (!outside) throw Error("C8 actor conservative native volume crosses large own table; choose physical mark after measured bounds");
        }
        largeTableC8Logged = true;
    }

    private static bool BoundsOutside(GTA.Math.Vector3 min, GTA.Math.Vector3 max, GTA.Math.Vector3 targetMin, GTA.Math.Vector3 targetMax)
    {
        return max.X < targetMin.X || min.X > targetMax.X || max.Y < targetMin.Y ||
            min.Y > targetMax.Y || max.Z < targetMin.Z || min.Z > targetMax.Z;
    }

    private static void RelativeNativeBounds(GTA.Prop target, GTA.Entity entity, GTA.Math.Vector3 min, GTA.Math.Vector3 max,
        out GTA.Math.Vector3 localMin, out GTA.Math.Vector3 localMax)
    {
        Bounds(min, max);
        localMin = new GTA.Math.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        localMax = new GTA.Math.Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (float x in new float[] { min.X, max.X })
            foreach (float y in new float[] { min.Y, max.Y })
                foreach (float z in new float[] { min.Z, max.Z })
                {
                    GTA.Math.Vector3 local = target.GetPositionOffset(entity.GetOffsetPosition(new GTA.Math.Vector3(x, y, z)));
                    Vector(local, 100f, "canto nativo relativo ao prop EP07_C02");
                    localMin.X = System.Math.Min(localMin.X, local.X); localMin.Y = System.Math.Min(localMin.Y, local.Y); localMin.Z = System.Math.Min(localMin.Z, local.Z);
                    localMax.X = System.Math.Max(localMax.X, local.X); localMax.Y = System.Math.Max(localMax.Y, local.Y); localMax.Z = System.Math.Max(localMax.Z, local.Z);
                }
    }

    private static bool TechnicalBox(State state)
    {
        string expected;
        bool pair = (state.Spec.Recurso == "caixas_madeira_candidata" || IsTechnicalOnlyBoxResource88(state.Spec.Recurso)) &&
            Models.TryGetValue(state.Spec.Recurso, out expected) && expected == state.Spec.Modelo;
        return pair && ObjetosDiretor.IsTechnicalBoxModel88(state.Spec.Modelo) &&
            (state.Spec.Codigo == "CAIXAS_NEGOCIO_1" || state.Spec.Codigo == "CAIXAS_NEGOCIO_2");
    }

    // Modelo/raios proprios nao autorizam colocar um corpo dentro da caixa.
    // AABB conservativa derivada dos oito cantos nativos do modelo de cada entidade.
    private void RequireTechnicalBoxesGeometry()
    {
        foreach (State state in states.Values)
        {
            if (!TechnicalBox(state)) throw Error("caixa tecnica mudou contrato literal");
            owned.RequireTechnicalBoxGeometry(state.Spec.Codigo);
            GTA.Prop box = owned.GetProp(state.Spec.Codigo);
            foreach (Actor actor in actors.Values)
            {
                GTA.Math.Vector3 min, max;
                actor.Ped.Model.GetDimensions(out min, out max); Bounds(min, max);
                GTA.Math.Vector3 localMin = new GTA.Math.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                GTA.Math.Vector3 localMax = new GTA.Math.Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (float x in new float[] { min.X, max.X })
                    foreach (float y in new float[] { min.Y, max.Y })
                        foreach (float z in new float[] { min.Z, max.Z })
                        {
                            GTA.Math.Vector3 local = box.GetPositionOffset(actor.Ped.GetOffsetPosition(new GTA.Math.Vector3(x, y, z)));
                            Vector(local, 100f, "canto do ator relativo a caixa");
                            localMin.X = System.Math.Min(localMin.X, local.X); localMin.Y = System.Math.Min(localMin.Y, local.Y); localMin.Z = System.Math.Min(localMin.Z, local.Z);
                            localMax.X = System.Math.Max(localMax.X, local.X); localMax.Y = System.Math.Max(localMax.Y, local.Y); localMax.Z = System.Math.Max(localMax.Z, local.Z);
                        }
                bool outside = localMax.X < state.Min.X || localMin.X > state.Max.X ||
                    localMax.Y < state.Min.Y || localMin.Y > state.Max.Y || localMax.Z < state.Min.Z || localMin.Z > state.Max.Z;
                if (!outside) throw Error("volume conservativo do ator cruza caixa propria; sem reposicionamento por frame");
            }
            foreach (State other in states.Values)
            {
                if (other == state) continue;
                GTA.Prop prop = owned.GetProp(other.Spec.Codigo);
                GTA.Math.Vector3 localMin = new GTA.Math.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                GTA.Math.Vector3 localMax = new GTA.Math.Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (float x in new float[] { other.Min.X, other.Max.X })
                    foreach (float y in new float[] { other.Min.Y, other.Max.Y })
                        foreach (float z in new float[] { other.Min.Z, other.Max.Z })
                        {
                            GTA.Math.Vector3 local = box.GetPositionOffset(prop.GetOffsetPosition(new GTA.Math.Vector3(x, y, z)));
                            Vector(local, 100f, "canto da outra caixa");
                            localMin.X = System.Math.Min(localMin.X, local.X); localMin.Y = System.Math.Min(localMin.Y, local.Y); localMin.Z = System.Math.Min(localMin.Z, local.Z);
                            localMax.X = System.Math.Max(localMax.X, local.X); localMax.Y = System.Math.Max(localMax.Y, local.Y); localMax.Z = System.Math.Max(localMax.Z, local.Z);
                        }
                if (!(localMax.X < state.Min.X || localMin.X > state.Max.X || localMax.Y < state.Min.Y ||
                    localMin.Y > state.Max.Y || localMax.Z < state.Min.Z || localMin.Z > state.Max.Z))
                    throw Error("as duas caixas proprias cruzam volumes conservativos");
            }
        }
    }

    private bool Ground(State state, GTA.Prop prop)
    {
        GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD, state.Position.X, state.Position.Y, state.Position.Z);
        if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, prop.Handle)) return GroundPending(state);
        float ground; GTA.Math.Vector3 normal;
        bool trace = !state.GroundTelemetryStarted || unchecked((uint)(GTA.Game.GameTime - state.GroundTelemetryAt)) >= 500;
        if (trace) { state.GroundTelemetryStarted = true; state.GroundTelemetryAt = GTA.Game.GameTime; }
        GTA.Math.Vector3 center = state.Position + new GTA.Math.Vector3(0f, 0f, groundProbeRise);
        bool found = GTA.World.GetGroundHeightAndNormal(center, out ground, out normal);
        bool rejected = found && (!Finite(ground) || !Finite(normal.Z) || normal.Z < 0.98f || System.Math.Abs(ground - state.Position.Z) > 3f);
        if (trace || rejected) LogGroundProbe(state, prop, "centro", center, found, ground, normal, ground, rejected);
        if (!found) return GroundPending(state);
        if (!Finite(ground) || !Finite(normal.Z) || normal.Z < 0.98f || System.Math.Abs(ground - state.Position.Z) > 3f)
            throw Error("chão plano/próximo não confirmado para " + state.Spec.Codigo);
        float radians = state.Rotation.Z * (float)(System.Math.PI / 180.0);
        float cosine = (float)System.Math.Cos(radians), sine = (float)System.Math.Sin(radians);
        float[] xs = new float[] { state.Min.X, state.Max.X };
        float[] ys = new float[] { state.Min.Y, state.Max.Y };
        bool c8AllCorners = largeTableC8 && state.Spec.Codigo == "MESA_JANTAR" && state.Spec.Modelo == "v_res_m_dinetble_replace";
        bool c8Rejected = false, c8Missing = false;
        foreach (float x in xs) foreach (float y in ys)
        {
            GTA.Math.Vector3 corner = state.Position + new GTA.Math.Vector3(x * cosine - y * sine, x * sine + y * cosine, groundProbeRise);
            float height; GTA.Math.Vector3 surface;
            bool cornerFound = GTA.World.GetGroundHeightAndNormal(corner, out height, out surface);
            bool cornerRejected = cornerFound && (!Finite(height) || !Finite(surface.Z) || surface.Z < 0.98f || System.Math.Abs(height - ground) > 0.08f);
            if (trace || cornerRejected) LogGroundProbe(state, prop, "canto", corner, cornerFound, height, surface, ground, cornerRejected);
            if (!cornerFound)
            {
                if (c8AllCorners) { c8Missing = true; continue; }
                return GroundPending(state);
            }
            if (!Finite(height) || !Finite(surface.Z) || surface.Z < 0.98f || System.Math.Abs(height - ground) > 0.08f)
            {
                if (c8AllCorners) { c8Rejected = true; continue; }
                throw Error("apoio irregular/suspenso nos limites reais do modelo: " + state.Spec.Codigo);
            }
        }
        if (c8AllCorners && (trace || c8Rejected || c8Missing))
            Log("MESA_C8_CHAO_CINCO_CONSULTAS", "CenterAndFourBBoxCorners=True; MaximumGroundCalls=5; Rejected=" + c8Rejected +
                "; Missing=" + c8Missing + "; Ready=False; OwnTransformSetter=False; GroundTolerance=0.08; NormalMinimum=0.98");
        if (c8Rejected) throw Error("apoio irregular/suspenso nos limites reais do modelo: " + state.Spec.Codigo);
        if (c8Missing) return GroundPending(state);
        prop.IsPositionFrozen = true;
        prop.PositionNoOffset = new GTA.Math.Vector3(state.Position.X, state.Position.Y, ground - state.Min.Z);
        prop.Velocity = GTA.Math.Vector3.Zero;
        return true;
    }

    // Telemetria somente leitura das mesmas consultas; falhas de log não alteram guardas/exceções.
    private void LogGroundProbe(State state, GTA.Prop prop, string phase, GTA.Math.Vector3 point, bool found,
        float height, GTA.Math.Vector3 normal, float reference, bool rejected)
    {
        try
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
            string message = System.String.Format(culture,
                "Codigo={0}; Modelo={1}; Fase={2}; Query=({3:R},{4:R},{5:R}); Found={6}; Height={7}; Normal=({8:R},{9:R},{10:R}); ReferenceGround={11:R}; Diff={12:R}; ProbeRise={13:R}; Rejected={14}; StatePosition=({15:R},{16:R},{17:R}); PropPosition={18}; Rotation={19}; BoundsMin={20}; BoundsMax={21}",
                state.Spec.Codigo, state.Spec.Modelo, phase, point.X, point.Y, point.Z, found,
                found ? height.ToString("R", culture) : "nao_encontrado", normal.X, normal.Y, normal.Z, reference,
                System.Math.Abs(height - reference), groundProbeRise, rejected, state.Position.X, state.Position.Y, state.Position.Z,
                GroundVector(prop.Position), GroundVector(prop.Rotation), GroundVector(state.Min), GroundVector(state.Max));
            Log(rejected ? "OBJETOS_SERIE_GROUND_FAIL" : "OBJETOS_SERIE_GROUND_PROBE", message);
            if (!rejected) return;
            foreach (State other in states.Values)
            {
                GTA.Prop ownProp = owned.GetProp(other.Spec.Codigo);
                Log("OBJETOS_SERIE_GROUND_OWN_PROP", "Codigo=" + other.Spec.Codigo + "; Modelo=" + other.Spec.Modelo +
                    "; Handle=" + ownProp.Handle + "; Ready=" + other.Ready + "; BoundsKnown=" + other.BoundsKnown +
                    "; Position=" + GroundVector(ownProp.Position) + "; Rotation=" + GroundVector(ownProp.Rotation) +
                    "; StatePosition=" + GroundVector(other.Position) + "; BoundsMin=" + GroundVector(other.Min) + "; BoundsMax=" + GroundVector(other.Max));
            }
        }
        catch (System.Exception) { }
    }
    private static string GroundVector(GTA.Math.Vector3 value)
    { return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:R},{1:R},{2:R})", value.X, value.Y, value.Z); }

    private bool GroundPending(State state)
    {
        if (unchecked((uint)(GTA.Game.GameTime - state.PreparedAt)) >= TimeoutMs)
            throw Error("chão/colisão não carregou em5s: " + state.Spec.Codigo + "; escolha marca real plana");
        return false;
    }

    // Só antes da ação/contagem ou após cancelamento. Teleporte de reset não ocorre durante Update.
    public void Reset()
    {
        Ready();
        if (officeBag84Prepared) throw Error("Sono84 compact preview is F6-only; F10/F6 required, no reset/recalibration/action.");
        if (paperMorph86 != null)
        {
            if (paperMorph86FinishResetAccepted) throw Error("morfologia86 apenasF6; F7/Delete/reset adicional vetados");
            paperMorph86.RequireHeld("FinishLoadingResetNoop"); paperMorph86FinishResetAccepted = true; return;
        }
        if (authoredPaperC5 != null) authoredPaperC5.BeforeReset(GetAuthoredPaperC5Owner());
        if (pocket98 != null) pocket98.BeforeReset();
        ResetCore();
        if (authoredPaperC5 != null) authoredPaperC5.AfterReset();
        if (pocket98 != null) pocket98.AfterReset();
    }

    private void ResetCore()
    {
        RequireActors();
        foreach (State state in states.Values)
            if (state.InitialSupportKnown) RequireInitialSupport(state);
        // Só reutiliza contato de colocação no mesmo apoio próprio que permaneceu imóvel.
        // Probes de poses são descartados e solicitados novamente pelo helper de poses.
        foreach (Timed timed in events) if (timed.Event.Tipo == "colocar" && timed.PlacementKnown) RequirePlacementSupport(timed);
        owned.Reset();
        surfaces.Clear(); // Novas poses revalidam o contato; não reutilizar um probe de outra tomada.
        foreach (State state in states.Values)
        {
            GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
            prop.Detach(false, false); prop.IsPositionFrozen = true;
            prop.PositionNoOffset = state.Position; prop.Rotation = state.Rotation; prop.Velocity = GTA.Math.Vector3.Zero;
            prop.IsCollisionEnabled = true; prop.IsVisible = false; state.Owner = null; state.Hand = null;
            if (!System.String.IsNullOrEmpty(state.Spec.AtorInicial))
            {
                bool usePreparedGrip = sleepBenchC1 && state.Spec.Codigo == "SACOLA_DJANIRA" && state.SleepBagGripPrepared;
                Attach(state, state.Spec.AtorInicial, state.Spec.MaoInicial,
                    usePreparedGrip ? state.SleepBagGripOffset : state.Spec.OffsetInicial, state.Spec.RotacaoInicial, false);
                if (usePreparedGrip) Log("SLEEP_BAG_RIGID_RESET", "Codigo=SACOLA_DJANIRA; Offset=" + state.SleepBagGripOffset + "; SameMeasuredOffset=True; Recalibrated=False; SpecChanged=False; BeforeAction=True");
            }
            prop.IsVisible = state.Spec.VisivelInicial;
            state.PickupAnchorKnown = state.InitialSupportKnown && state.Owner == null; state.PickupPlacement = null;
            state.PickupPosition = prop.Position; state.PickupRotation = prop.Rotation;
        }
        lastElapsed = -1;
        foreach (Timed timed in events) { timed.Started = false; timed.Completed = false; timed.Measured = false; timed.PhasePendingLogged = false; timed.InitialDistance = 0f; timed.SpineInitialMeasured = false; timed.InitialSpineDistance = 0f; }
        foreach (Timed timed in events)
        {
            timed.ContactMeasured = false; timed.NextContactLog = 0; timed.ContactStartedMs = 0; timed.ContactMinimumMs = 0; timed.NextGuardarLog = 0;
            timed.ContactStartDistance = 0f; timed.ContactMinimumDistance = 0f; timed.ContactDonor = null; timed.ContactDonorHand = null;
            timed.NextPlacementLog = 0; timed.Event.IndiceSuperficie = 0;
            timed.PickupPhaseKnown = timed.PickupProgressObserved = false; timed.PickupPreviousPhase = 0f; timed.NextPickupLog = 0;
            timed.PaperHandoffPhaseKnown = false; timed.PaperHandoffDonorPhase = timed.PaperHandoffReceiverPhase = 0f; timed.NextPaperHandoffLog = 0;
        }
        foreach (Timed timed in events)
        {
            if (timed.Event.Tipo != "colocar" || timed.PlacementKnown) continue;
            State support = states[timed.Event.Apoio];
            float x = timed.Event.UsarCentroMedido ? (support.Min.X + support.Max.X) * 0.5f : timed.Event.SuperficieRelativa.X;
            float y = timed.Event.UsarCentroMedido ? (support.Min.Y + support.Max.Y) * 0.5f : timed.Event.SuperficieRelativa.Y;
            timed.Event.IndiceSuperficie = RequestSurfaceCore(timed.Event.Apoio,
                x, y);
        }
        if (sleepBenchC1) owned.RequireSleepBenchGeometry("BANCO_PRAIA");
        Log("OBJETOS_SERIE_RESET", "Marcas físicas/owners/eventos restaurados; quantidade=" + states.Count);
    }

    public void Update(int elapsedMs)
    {
        if (paperMorph86 != null) throw Error("morfologia86 nao executa acao/F7/SAVE de producao");
        Ready(); RequireActorsDuringUpdate(elapsedMs); RequireProps();
        if (largeTableC8) RequireLargeTableC8Geometry();
        if (technicalBoxes) RequireTechnicalBoxesGeometry();
        if (productionBoxes) RequireProductionNegotiationGeometry();
        if (elapsedMs < 0 || elapsedMs > 180000 || elapsedMs < lastElapsed) throw Error("tempo fora do intervalo ou retornou sem Reset");
        foreach (Timed timed in events)
        {
            if (timed.Completed) continue;
            PropEvent item = timed.Event;
            ObserveContact(timed, elapsedMs, false, "medicao");
            if (item.Tipo == "guardar" && !timed.Measured && elapsedMs >= item.GestoInicioMs && timed.State.Owner == item.Ator)
            { timed.InitialDistance = HandTargetDistance(timed); timed.Measured = true; ObserveSpineContact(timed, elapsedMs, 0f, true); }
            if (item.InicioMs > elapsedMs) continue;
            bool previousPending = false;
            foreach (Timed previous in events)
                if (previous.State == timed.State && previous.Event.InicioMs < item.InicioMs && !previous.Completed) previousPending = true;
            if (previousPending) continue;
            if (!timed.Started)
            {
                if (elapsedMs - item.InicioMs > 500)
                { ObserveContact(timed, elapsedMs, true, "inicio_perdido"); throw Error("evento de prop perdeu a janela; reinicie a tomada"); }
                timed.Started = true;
            }
            if (item.EsperaMaximaMs > 0 && elapsedMs > item.InicioMs + item.EsperaMaximaMs)
            { ObserveContact(timed, elapsedMs, true, "janela_expirada"); throw Error("gesto de prop não atingiu contato dentro da janela; sem teleporte/ocultação: " + timed.State.Spec.Codigo); }
            if (!TryApply(timed, elapsedMs))
            {
                if (item.EsperaMaximaMs == 0 || elapsedMs >= item.InicioMs + item.EsperaMaximaMs)
                { ObserveContact(timed, elapsedMs, true, "contato_recusado"); throw Error("mão/objeto não atingiu contato no gesto real; sem teleporte/ocultação: " + timed.State.Spec.Codigo); }
                continue;
            }
            timed.Completed = true;
            ObserveContact(timed, elapsedMs, true, "evento_concluido");
        }
        lastElapsed = elapsedMs;
    }

    // Diagnostico somente leitura: mede o primeiro evento pendente de cada prop desde o gesto,
    // guarda o minimo observado por frame e limita as fases/logs a uma amostra a cada 500 ms.
    // As leituras nunca autorizam transferencia, alteram poses ou ampliam a janela de contato.
    private void ObserveContact(Timed timed, int elapsedMs, bool force, string reason)
    {
        PropEvent item = timed.Event; State state = timed.State;
        if (item.Tipo != "anexar" && item.Tipo != "transferir" && item.Tipo != "guardar") return;
        if (!force)
        {
            foreach (Timed previous in events)
                if (previous.State == state && previous.Event.InicioMs < item.InicioMs && !previous.Completed) return;
            if (item.EsperaMaximaMs > 0 && elapsedMs < item.GestoInicioMs) return;
        }
        bool emit = force || elapsedMs >= timed.NextContactLog;
        if (emit) timed.NextContactLog = elapsedMs + 500;
        try
        {
            GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
            Actor receiver = actors[item.Ator];
            string receiverHand = item.Tipo == "guardar" ? state.Hand : item.Mao;
            if (receiverHand == null) receiverHand = timed.ContactDonorHand;
            if (receiverHand != "direita" && receiverHand != "esquerda") throw Error("mao de contato nao disponivel para telemetria");
            GTA.PedBone receivingBone = receiver.Ped.Bones[HandBone(state, receiverHand)];
            if (System.Object.ReferenceEquals(receivingBone, null) || !receivingBone.IsValid) throw Error("osso recebedor nao disponivel para telemetria");
            GTA.Math.Vector3 objectPoint = prop.Position, receivingHand = receivingBone.Position;
            GTA.Math.Vector3 receivingPoint = receivingBone.GetOffsetPosition(item.Offset);
            float distance = (objectPoint - receivingPoint).Length();
            if (!Finite(distance)) throw Error("distancia de contato nao finita para telemetria");
            if (!timed.ContactMeasured)
            {
                timed.ContactMeasured = true; timed.ContactStartedMs = elapsedMs; timed.ContactMinimumMs = elapsedMs;
                timed.ContactStartDistance = distance; timed.ContactMinimumDistance = distance;
                timed.ContactDonor = state.Owner; timed.ContactDonorHand = state.Hand;
            }
            if (distance < timed.ContactMinimumDistance)
            { timed.ContactMinimumDistance = distance; timed.ContactMinimumMs = elapsedMs; }
            if (!emit) return;
            Actor donor = null; GTA.Math.Vector3 donatingHand = GTA.Math.Vector3.Zero; bool donorBoneValid = false;
            if (timed.ContactDonor != null && actors.TryGetValue(timed.ContactDonor, out donor) &&
                (timed.ContactDonorHand == "direita" || timed.ContactDonorHand == "esquerda"))
            {
                GTA.PedBone donatingBone = donor.Ped.Bones[HandBone(state, timed.ContactDonorHand)];
                donorBoneValid = !System.Object.ReferenceEquals(donatingBone, null) && donatingBone.IsValid;
                if (donorBoneValid) donatingHand = donatingBone.Position;
            }
            Log("OBJETO_CONTATO_ESTADO", "Codigo=" + state.Spec.Codigo + "; Tipo=" + item.Tipo + "; Motivo=" + reason +
                "; TempoMs=" + elapsedMs + "; InicioMs=" + item.InicioMs + "; EsperaMaximaMs=" + item.EsperaMaximaMs +
                "; Doador=" + (timed.ContactDonor ?? "nenhum") + "; DonoAtual=" + (state.Owner ?? "nenhum") +
                "; Recebedor=" + item.Ator + "; RecebedorModelo=" + receiver.Model +
                "; Objeto=" + ContactVector(objectPoint) + "; MaoRecebedor=" + ContactVector(receivingHand) +
                "; PontoRecebedor=" + ContactVector(receivingPoint) + "; MaoDoador=" + (donorBoneValid ? ContactVector(donatingHand) : "indisponivel") +
                "; DistanciaObjetoRecebedor=" + ContactNumber(distance) +
                "; DistanciaMaos=" + (donorBoneValid ? ContactNumber((donatingHand - receivingHand).Length()) : "indisponivel") +
                "; DistanciaInicial=" + ContactNumber(timed.ContactStartDistance) + "; MedicaoInicioMs=" + timed.ContactStartedMs +
                "; DistanciaMinima=" + ContactNumber(timed.ContactMinimumDistance) + "; MinimoMs=" + timed.ContactMinimumMs +
                "; LimiteContato=0.35; Visivel=" + prop.IsVisible + "; AnexadoDoador=" + (donor != null && prop.IsAttachedTo(donor.Ped)) +
                "; RecebedorPosicao=" + ContactVector(receiver.Ped.Position) + "; RecebedorDirecao=" + ContactNumber(receiver.Ped.Heading) +
                "; DoadorPosicao=" + (donor == null ? "indisponivel" : ContactVector(donor.Ped.Position)) +
                "; DoadorDirecao=" + (donor == null ? "indisponivel" : ContactNumber(donor.Ped.Heading)) +
                "; RecebedorAnim=" + ContactAnimation(receiver, item.Dicionario, item.Clipe) +
                "; DoadorAnim=" + ContactAnimation(donor, item.Dicionario, item.ClipeDoador));
        }
        catch (System.Exception exception)
        { if (emit) Log("OBJETO_CONTATO_LEITURA_FALHOU", "Codigo=" + state.Spec.Codigo + "; TempoMs=" + elapsedMs + "; Erro=" + exception.GetType().Name + ": " + exception.Message); }
    }
    private static string ContactAnimation(Actor actor, string dictionary, string clip)
    {
        if (actor == null) return "nenhum";
        if (!System.String.IsNullOrEmpty(dictionary) && !System.String.IsNullOrEmpty(clip))
        {
            bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actor.Handle, dictionary, clip, 3);
            return dictionary + "/" + clip + "; Playing=" + playing + "; Fase=" + (playing ?
                ContactNumber(GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actor.Handle, dictionary, clip)) : "indisponivel");
        }
        // Eventos legados nao declaram clipe: consultar somente os dois pares reais do catalogo instalado.
        var result = new System.Text.StringBuilder();
        foreach (string candidate in new string[] { "givetake1_a", "givetake1_b" })
        {
            bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actor.Handle, "mp_common", candidate, 3);
            if (result.Length != 0) result.Append("|");
            result.Append("mp_common/").Append(candidate).Append(":Playing=").Append(playing).Append(",Fase=");
            result.Append(playing ? ContactNumber(GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actor.Handle, "mp_common", candidate)) : "indisponivel");
        }
        return result.ToString();
    }
    private static string ContactNumber(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static string ContactVector(GTA.Math.Vector3 value)
    { return "(" + ContactNumber(value.X) + "," + ContactNumber(value.Y) + "," + ContactNumber(value.Z) + ")"; }

    private bool TryApply(Timed timed, int elapsedMs)
    {
        State state = timed.State; PropEvent item = timed.Event;
        GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
        if (item.Tipo == "colocar") return TryPlace(timed, elapsedMs, prop);
        if (item.Tipo == "pegar") return TryPickup(timed, elapsedMs, prop);
        if (item.Tipo == "anexar" || item.Tipo == "transferir")
        {
            if (item.Tipo == "anexar" && state.Owner != null) throw Error("anexar exige objeto sem dono; use transferir");
            if (item.Tipo == "transferir" && (state.Owner == null || state.Owner == item.Ator)) throw Error("transferência exige dono anterior diferente");
            if (item.EsperaMaximaMs > 0 && (!IsObjectVisualSourceConfigured(state.Spec.Codigo) || !prop.IsAttachedTo(actors[item.Doador].Ped)))
                throw Error("transferência exige envelope visível ainda na mão real do doador");
            if (item.EsperaMaximaMs > 0 && !GesturePlaying(item, elapsedMs)) return false;
            if (item.EsperaMaximaMs > 0 && (state.Owner != item.Doador ||
                !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actors[item.Doador].Handle, item.Dicionario, item.ClipeDoador, 3))) return false;
            if (IsGrandmaPaperHandoff(timed) && !CanTransferGrandmaPaper(timed, elapsedMs)) return false;
            if (IsDismissalEnvelopeHandoff(timed) && !CanTransferDismissalEnvelope(timed, elapsedMs)) return false;
            if (!CanAttach(state, item.Ator, item.Mao, item.Offset)) return false;
            GTA.Math.Vector3 paperBefore = prop.Position;
            Attach(state, item.Ator, item.Mao, item.Offset, item.Rotacao, true);
            if (IsDismissalEnvelopeHandoff(timed))
                Log("ENVELOPE_DEMISSAO_ANEXADO", "Codigo=" + state.Spec.Codigo + "; TempoMs=" + elapsedMs +
                    "; Doador=" + item.Doador + "; Recebedor=" + state.Owner + "; Antes=" + ContactVector(paperBefore) +
                    "; Depois=" + ContactVector(prop.Position) + "; DeslocamentoTroca=" + ContactNumber((prop.Position - paperBefore).Length()) +
                    "; AnexadoRecebedor=" + prop.IsAttachedTo(actors[item.Ator].Ped) + "; AprovacaoProducao=false");
            if (IsGrandmaPaperHandoff(timed))
                Log("PAPEL_ENTREGA_ANEXADO", "Codigo=" + state.Spec.Codigo + "; TempoMs=" + elapsedMs +
                    "; Doador=" + item.Doador + "; Recebedor=" + state.Owner + "; Antes=" + ContactVector(paperBefore) +
                    "; Depois=" + ContactVector(prop.Position) + "; DeslocamentoTroca=" + ContactNumber((prop.Position - paperBefore).Length()) +
                    "; AnexadoRecebedor=" + prop.IsAttachedTo(actors[item.Ator].Ped) + "; DobraVisual=pendente; AprovacaoProducao=false");
        }
        else if (item.Tipo == "guardar")
        {
            if (state.Owner != item.Ator || state.Hand == null || !prop.IsVisible || !prop.IsAttachedTo(actors[item.Ator].Ped))
                throw Error("guardar exige envelope visível ainda anexado à mão do próprio dono");
            if (!timed.Measured || !GesturePlaying(item, elapsedMs)) return false;
            float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actors[item.Ator].Handle, item.Dicionario, item.Clipe);
            float distance = HandTargetDistance(timed);
            GTA.PedBone target = actors[item.Ator].Ped.Bones[GuardarTargetBone(timed)];
            if (System.Object.ReferenceEquals(target, null) || !target.IsValid) throw Error("alvo corporal real não disponível para guardar");
            float objectDistance = (prop.Position - target.Position).Length();
            if (!Finite(objectDistance)) throw Error("distância real do envelope ao alvo corporal inválida");
            // Mesmas medidas que as guardas abaixo; nenhum getter ou setter adicional.
            if (elapsedMs >= timed.NextGuardarLog)
            {
                timed.NextGuardarLog = elapsedMs + 100;
                Log("OBJETO_GUARDAR_MEDICAO", "Codigo=" + state.Spec.Codigo + "; Ator=" + item.Ator +
                    "; Alvo=" + GuardarTargetName(timed) + "; Bone=" + GuardarTargetBone(timed) +
                    "; TempoMs=" + elapsedMs + "; InicioMs=" + item.InicioMs + "; GestoFimMs=" + item.GestoFimMs +
                    "; EsperaMaximaMs=" + item.EsperaMaximaMs + "; Fase=" + ContactNumber(phase) +
                    "; DistanciaMaoAlvo=" + ContactNumber(distance) + "; DistanciaObjetoAlvo=" + ContactNumber(objectDistance) +
                    "; DistanciaMaoAlvoInicial=" + ContactNumber(timed.InitialDistance) +
                    "; AproximacaoMaoAlvo=" + ContactNumber(timed.InitialDistance - distance) +
                    "; LimiteDistancia=0.35; AproximacaoMinima=0.06; FaseMinima=0.1; FaseMaxima=0.95; SemOcultar=true");
                ObserveSpineContact(timed, elapsedMs, phase, false);
            }
            if (!Finite(phase) || phase < 0f || phase > 1f)
            {
                if (!timed.PhasePendingLogged)
                {
                    timed.PhasePendingLogged = true;
                    Log("OBJETO_FASE_AGUARDANDO", "Codigo=" + state.Spec.Codigo + "; Ator=" + item.Ator +
                        "; Dicionario=" + item.Dicionario + "; Clipe=" + item.Clipe + "; Fase=" + ContactNumber(phase) +
                        "; TempoMs=" + elapsedMs + "; InicioMs=" + item.InicioMs + "; EsperaMaximaMs=" + item.EsperaMaximaMs +
                        "; GestoFimMs=" + item.GestoFimMs + "; SemOcultar=true");
                }
                return false;
            }
            if (phase < 0.1f || phase > 0.95f || distance > 0.35f || objectDistance > 0.35f || timed.InitialDistance - distance < 0.06f) return false;
            prop.Detach(false, false); prop.IsVisible = false; prop.IsCollisionEnabled = false;
            prop.IsPositionFrozen = true; prop.Velocity = GTA.Math.Vector3.Zero;
            state.PickupAnchorKnown = false; state.PickupPlacement = null;
            state.Owner = null; state.Hand = null; // Guarda no ponto real do gesto, sem mover o objeto.
            Log("OBJETO_SERIE_GUARDADO", "Codigo=" + state.Spec.Codigo + "; Fase=" + phase.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; Alvo=" + GuardarTargetName(timed) + "; Bone=" + GuardarTargetBone(timed) +
                "; DistanciaMaoAlvo=" + distance.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; TesteVisualBolso=pendente");
        }
        else if (item.Tipo == "soltar")
        {
            if (state.Owner == null) throw Error("soltar exige objeto anexado a um ator");
            prop.Detach(false, true); prop.Velocity = GTA.Math.Vector3.Zero;
            prop.IsCollisionEnabled = true; prop.IsPositionFrozen = false;
            state.PickupAnchorKnown = false; state.PickupPlacement = null;
            state.Owner = null; state.Hand = null; // Solta no ponto real; gravidade, sem reposicionamento.
        }
        else if (item.Tipo == "ocultar") prop.IsVisible = false;
        else if (item.Tipo == "mostrar") prop.IsVisible = true;
        else throw Error("evento desconhecido");
        Log("OBJETO_SERIE_EVENTO", "Codigo=" + state.Spec.Codigo + "; Tipo=" + item.Tipo + "; Dono=" + (state.Owner ?? "nenhum") + "; InicioMs=" + item.InicioMs);
        return true;
    }

    // Alvo de diagnóstico do mesmo corpo: não autoriza guardar nem substitui a guarda da pelve.
    private void ObserveSpineContact(Timed timed, int elapsedMs, float phase, bool initial)
    {
        try
        {
            State state = timed.State; PropEvent item = timed.Event;
            GTA.Ped ped = actors[item.Ator].Ped;
            GTA.PedBone hand = ped.Bones[HandBone(state, state.Hand)];
            GTA.PedBone target = ped.Bones[GTA.Bone.SkelSpine3];
            if (System.Object.ReferenceEquals(hand, null) || System.Object.ReferenceEquals(target, null) || !hand.IsValid || !target.IsValid)
            {
                Log("OBJETO_SPINE_LEITURA", "Codigo=" + state.Spec.Codigo + "; TempoMs=" + elapsedMs + "; Found=false; SemOcultar=true");
                return;
            }
            GTA.Math.Vector3 handPoint = hand.Position, targetPoint = target.Position, objectPoint = owned.GetProp(state.Spec.Codigo).Position;
            float handDistance = (handPoint - targetPoint).Length(), objectDistance = (objectPoint - targetPoint).Length();
            if (!Finite(handDistance) || !Finite(objectDistance))
            {
                Log("OBJETO_SPINE_LEITURA", "Codigo=" + state.Spec.Codigo + "; TempoMs=" + elapsedMs + "; Found=false; Reason=nonfinite; SemOcultar=true");
                return;
            }
            if (initial) { timed.InitialSpineDistance = handDistance; timed.SpineInitialMeasured = true; }
            Log("OBJETO_SPINE_MEDICAO", "Codigo=" + state.Spec.Codigo + "; Ator=" + item.Ator + "; TempoMs=" + elapsedMs +
                "; Inicial=" + initial + "; Fase=" + (initial ? "nao_consultada_inicial" : ContactNumber(phase)) +
                "; Bone=SkelSpine3; Found=true; Target=" + ContactVector(targetPoint) + "; Mao=" + ContactVector(handPoint) +
                "; Objeto=" + ContactVector(objectPoint) + "; DistanciaMaoSpine3=" + ContactNumber(handDistance) +
                "; DistanciaObjetoSpine3=" + ContactNumber(objectDistance) + "; InicialMedida=" + timed.SpineInitialMeasured +
                "; DistanciaInicial=" + ContactNumber(timed.InitialSpineDistance) +
                "; Aproximacao=" + ContactNumber(timed.InitialSpineDistance - handDistance) +
                "; SemOcultar=true; GuardaAtual=" + GuardarTargetName(timed) + "; NaoConfirmaBolsoVisual");
        }
        catch (System.Exception exception)
        { Log("OBJETO_SPINE_LEITURA", "Codigo=" + timed.State.Spec.Codigo + "; TempoMs=" + elapsedMs + "; Found=false; Erro=" + exception.GetType().Name + "; SemOcultar=true"); }
    }

    // Guarda exclusiva da entrega do papel da avo. Getters complementam os limites
    // existentes de contato; nao reposicionam maos, corpo, objeto ou clipes.
    private static bool IsGrandmaPaperHandoff(Timed timed)
    {
        return timed.State.Spec.Codigo == "PAPEL_IRACI" && (timed.State.Spec.Recurso == "carta_simples" || timed.State.Spec.Recurso == "papel_dobrado_autoral") &&
            timed.Event.Tipo == "transferir" && timed.Event.Doador == "DONA_IRACI" &&
            timed.Event.Ator == "DJANIRA_POBRE";
    }
    private bool CanTransferGrandmaPaper(Timed timed, int elapsedMs)
    {
        State state = timed.State; PropEvent item = timed.Event;
        if (item.EsperaMaximaMs <= 0 || item.Dicionario != "mp_common" ||
            item.ClipeDoador != "givetake1_a" || item.Clipe != "givetake1_b" ||
            state.Owner != item.Doador || state.Hand != "direita" || item.Mao != "direita")
            throw Error("papel da avo exige os dois clipes/maos/donos declarados; sem transferencia isolada");
        GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
        Actor donor = actors[item.Doador], receiver = actors[item.Ator];
        GTA.PedBone donorBone = donor.Ped.Bones[HandBone(state, state.Hand)];
        GTA.PedBone receiverBone = receiver.Ped.Bones[HandBone(state, item.Mao)];
        if (System.Object.ReferenceEquals(donorBone, null) || System.Object.ReferenceEquals(receiverBone, null) ||
            !donorBone.IsValid || !receiverBone.IsValid || !state.BoundsKnown || !IsObjectVisualSourceConfigured(state.Spec.Codigo) ||
            !prop.IsAttachedTo(donor.Ped)) throw Error("papel da avo perdeu bones/bounds/dono fisico antes da entrega");
        float donorPhase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,
            donor.Handle, item.Dicionario, item.ClipeDoador);
        float receiverPhase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,
            receiver.Handle, item.Dicionario, item.Clipe);
        float hands = (donorBone.Position - receiverBone.Position).Length();
        float paperDonor = (prop.Position - donorBone.Position).Length();
        float paperReceiver = (prop.Position - receiverBone.Position).Length();
        if (!Finite(hands) || !Finite(paperDonor) || !Finite(paperReceiver)) throw Error("distancia de maos/papel nao finita");
        bool phasesValid = Finite(donorPhase) && Finite(receiverPhase) && donorPhase >= 0f && donorPhase <= 1f &&
            receiverPhase >= 0f && receiverPhase <= 1f;
        bool progressed = phasesValid && timed.PaperHandoffPhaseKnown &&
            donorPhase > timed.PaperHandoffDonorPhase + 0.001f && receiverPhase > timed.PaperHandoffReceiverPhase + 0.001f;
        bool contact = phasesValid && progressed && donorPhase >= 0.1f && donorPhase <= 0.95f &&
            receiverPhase >= 0.1f && receiverPhase <= 0.95f && System.Math.Abs(donorPhase - receiverPhase) <= 0.05f &&
            hands <= 0.35f && paperDonor <= 0.35f && paperReceiver <= 0.35f;
        if (state.Spec.Recurso == "papel_dobrado_autoral")
        {
            if (!authoredPaperC5Scope || authoredPaperC5 == null) throw Error("authored paper needs bound own renderer before handoff");
            if (contact) contact = authoredPaperC5.TransferSurfaceContact(donorBone.Position, receiverBone.Position, elapsedMs, state.Owner);
        }
        if (!timed.PaperHandoffPhaseKnown && phasesValid)
        {
            timed.PaperHandoffPhaseKnown = true;
            timed.PaperHandoffDonorPhase = donorPhase; timed.PaperHandoffReceiverPhase = receiverPhase;
        }
        if (contact || elapsedMs >= timed.NextPaperHandoffLog)
        {
            timed.NextPaperHandoffLog = elapsedMs + 250;
            Log("PAPEL_ENTREGA_CONTATO", "Codigo=" + state.Spec.Codigo + "; TempoMs=" + elapsedMs +
                "; MaoDoador=" + ContactVector(donorBone.Position) + "; MaoRecebedor=" + ContactVector(receiverBone.Position) +
                "; Papel=" + ContactVector(prop.Position) + "; DistanciaMaos=" + ContactNumber(hands) +
                "; PapelMaoDoador=" + ContactNumber(paperDonor) + "; PapelMaoRecebedor=" + ContactNumber(paperReceiver) +
                "; FaseDoador=" + ContactNumber(donorPhase) + "; FaseRecebedor=" + ContactNumber(receiverPhase) +
                "; ProgressoAmbos=" + progressed + "; Contato=" + contact + "; LimiteContato=0.35; FaseMin=0.1; FaseMax=0.95; DeltaFaseMax=0.05; DobraVisual=pendente");
        }
        return contact;
    }

    private void RequireDismissalEnvelopeContract()
    {
        if (!dismissalHandoffs || dismissalPartNumber < 1 || dismissalPartNumber > 3 || actors.Count != 5 ||
            !actors.ContainsKey("DINJER") || !actors.ContainsKey("CAPANGA_1") || !actors.ContainsKey("CAPANGA_2") ||
            !actors.ContainsKey("CAPANGA_3") || !actors.ContainsKey("CAPANGA_4") || states.Count != dismissalPartNumber || events.Count != 1)
            throw Error("demissao exige mesmos cinco atores, uma entrega real e gerente com um envelope");
        for (int number = 1; number <= dismissalPartNumber; number++)
        {
            State state; string code = "ENVELOPE_CAPANGA_" + number;
            if (!states.TryGetValue(code, out state) || state.Spec.Recurso != "envelope" || state.Spec.Modelo != "prop_cs_envolope_01" ||
                !state.Spec.VisivelInicial || state.Spec.AssentarNoChao || state.Spec.MaoInicial != "direita" ||
                state.Spec.AtorInicial != (number == dismissalPartNumber ? "CAPANGA_4" : "CAPANGA_" + number) ||
                state.Spec.Eventos.Count != (number == dismissalPartNumber ? 1 : 0))
                throw Error("envelopes anteriores com receptores, novo unico com CAP4; Dinjer fica sem");
        }
        if (!IsDismissalEnvelopeHandoff(events[0])) throw Error("demissao nao aceita transferencia para Dinjer/outro receptor");
    }
    private bool IsDismissalEnvelopeHandoff(Timed timed)
    {
        return dismissalHandoffs && timed.State.Spec.Codigo == "ENVELOPE_CAPANGA_" + dismissalPartNumber &&
            timed.State.Spec.Recurso == "envelope" && timed.Event.Tipo == "transferir" &&
            timed.Event.Doador == "CAPANGA_4" && timed.Event.Ator == "CAPANGA_" + dismissalPartNumber;
    }
    private bool CanTransferDismissalEnvelope(Timed timed, int elapsedMs)
    {
        State state = timed.State; PropEvent item = timed.Event;
        if (item.EsperaMaximaMs <= 0 || item.Dicionario != "mp_common" ||
            item.ClipeDoador != "givetake1_a" || item.Clipe != "givetake1_b" ||
            state.Owner != item.Doador || state.Hand != "direita" || item.Mao != "direita")
            throw Error("envelope de demissao exige os dois clipes/maos/donos declarados; sem transferencia isolada");
        GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
        Actor donor = actors[item.Doador], receiver = actors[item.Ator];
        GTA.PedBone donorBone = donor.Ped.Bones[HandBone(state, state.Hand)];
        GTA.PedBone receiverBone = receiver.Ped.Bones[HandBone(state, item.Mao)];
        if (System.Object.ReferenceEquals(donorBone, null) || System.Object.ReferenceEquals(receiverBone, null) ||
            !donorBone.IsValid || !receiverBone.IsValid || !state.BoundsKnown || !prop.IsVisible ||
            !prop.IsAttachedTo(donor.Ped)) throw Error("envelope de demissao perdeu bones/bounds/dono fisico antes da entrega");
        float donorPhase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,
            donor.Handle, item.Dicionario, item.ClipeDoador);
        float receiverPhase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME,
            receiver.Handle, item.Dicionario, item.Clipe);
        float hands = (donorBone.Position - receiverBone.Position).Length();
        float paperDonor = (prop.Position - donorBone.Position).Length();
        float paperReceiver = (prop.Position - receiverBone.Position).Length();
        if (!Finite(hands) || !Finite(paperDonor) || !Finite(paperReceiver)) throw Error("distancia de maos/papel nao finita");
        bool phasesValid = Finite(donorPhase) && Finite(receiverPhase) && donorPhase >= 0f && donorPhase <= 1f &&
            receiverPhase >= 0f && receiverPhase <= 1f;
        bool progressed = phasesValid && timed.PaperHandoffPhaseKnown &&
            donorPhase > timed.PaperHandoffDonorPhase + 0.001f && receiverPhase > timed.PaperHandoffReceiverPhase + 0.001f;
        bool contact = phasesValid && progressed && donorPhase >= 0.1f && donorPhase <= 0.95f &&
            receiverPhase >= 0.1f && receiverPhase <= 0.95f && System.Math.Abs(donorPhase - receiverPhase) <= 0.05f &&
            hands <= 0.35f && paperDonor <= 0.35f && paperReceiver <= 0.35f;
        if (!timed.PaperHandoffPhaseKnown && phasesValid)
        {
            timed.PaperHandoffPhaseKnown = true;
            timed.PaperHandoffDonorPhase = donorPhase; timed.PaperHandoffReceiverPhase = receiverPhase;
        }
        if (contact || elapsedMs >= timed.NextPaperHandoffLog)
        {
            timed.NextPaperHandoffLog = elapsedMs + 250;
            Log("ENVELOPE_DEMISSAO_CONTATO", "Codigo=" + state.Spec.Codigo + "; TempoMs=" + elapsedMs +
                "; MaoDoador=" + ContactVector(donorBone.Position) + "; MaoRecebedor=" + ContactVector(receiverBone.Position) +
                "; Papel=" + ContactVector(prop.Position) + "; DistanciaMaos=" + ContactNumber(hands) +
                "; PapelMaoDoador=" + ContactNumber(paperDonor) + "; PapelMaoRecebedor=" + ContactNumber(paperReceiver) +
                "; FaseDoador=" + ContactNumber(donorPhase) + "; FaseRecebedor=" + ContactNumber(receiverPhase) +
                "; ProgressoAmbos=" + progressed + "; Contato=" + contact + "; LimiteContato=0.35; FaseMin=0.1; FaseMax=0.95; DeltaFaseMax=0.05; PartesNecessarias=3; SemSetterTransform=true");
        }
        return contact;
    }

    private bool GesturePlaying(PropEvent item, int elapsedMs)
    {
        return elapsedMs >= item.GestoInicioMs && elapsedMs < item.GestoFimMs &&
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, actors[item.Ator].Handle, item.Dicionario, item.Clipe, 3);
    }
    // O clipe real leva o envelope de Dinjer ao casaco, não ao colo da pose sentada.
    // A mesma escolha vale para a referência inicial e para todas as guardas atuais.
    private static GTA.Bone GuardarTargetBone(Timed timed)
    {
        PropEvent item = timed.Event;
        if (timed.State.Spec.Codigo == "ENVELOPE_DINJER" && item.Tipo == "guardar" && item.Ator == "DINJER" &&
            item.Dicionario == "mp_common_miss" && item.Clipe == "put_away_coke") return GTA.Bone.SkelSpine3;
        return GTA.Bone.SkelPelvis;
    }
    private static string GuardarTargetName(Timed timed)
    { return GuardarTargetBone(timed) == GTA.Bone.SkelSpine3 ? "casaco" : "pelve"; }
    private float HandTargetDistance(Timed timed)
    {
        State state = timed.State;
        if (state.Owner == null || state.Hand == null) throw Error("guarda exige dono e mão reais");
        GTA.Ped ped = actors[state.Owner].Ped;
        GTA.PedBone hand = ped.Bones[HandBone(state, state.Hand)];
        GTA.PedBone target = ped.Bones[GuardarTargetBone(timed)];
        if (System.Object.ReferenceEquals(hand, null) || System.Object.ReferenceEquals(target, null) || !hand.IsValid || !target.IsValid)
            throw Error("ossos reais da mão/alvo corporal não disponíveis");
        float distance = (hand.Position - target.Position).Length();
        if (!Finite(distance)) throw Error("distância da mão/alvo corporal inválida");
        return distance;
    }
    private bool CanAttach(State state, string actorId, string hand, GTA.Math.Vector3 offset)
    {
        GTA.PedBone bone = actors[actorId].Ped.Bones[HandBone(state, hand)];
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("osso real da mão não disponível");
        return (bone.GetOffsetPosition(offset) - owned.GetProp(state.Spec.Codigo).Position).Length() <= 0.35f;
    }

    private void Attach(State state, string actorId, string hand, GTA.Math.Vector3 offset, GTA.Math.Vector3 rotation, bool duringAction)
    {
        if (restrictHandOwnership)
            foreach (State other in states.Values)
                if (other != state && other.Owner == actorId && other.Hand == hand)
                    throw Error("cena com pegar nao permite segundo prop na mesma mao");
        GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
        Actor actor = actors[actorId];
        GTA.PedBone bone = actor.Ped.Bones[HandBone(state, hand)];
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("osso real da mão não disponível");
        GTA.Math.Vector3 point = bone.GetOffsetPosition(offset);
        if (duringAction && (point - prop.Position).Length() > 0.35f)
            throw Error("mãos/objeto não estão próximos para pickup/transferência; sem teleporte: " + state.Spec.Codigo);
        prop.Detach(false, false); prop.IsPositionFrozen = false; prop.IsCollisionEnabled = false;
        prop.AttachTo(bone, offset, rotation, false, false, false, false, GTA.EulerRotationOrder.YXZ, true, false);
        if (!prop.IsAttachedTo(actor.Ped)) throw Error("attachment não confirmado pelo motor: " + state.Spec.Codigo);
        state.Owner = actorId; state.Hand = hand;
        state.PickupAnchorKnown = false; state.PickupPlacement = null;
    }

// Technical84 only: one rigid Spine3 fit in PREP, then getters only. Normal
// ownFactory retains deletion/identity ownership. No scale, actor/phase setter,
// action reanchor, production permission or claim of skin/mesh contact.
private static bool OfficeBag84Spec(PropSpec spec)
{ return (spec.Recurso == "bolsa_sono84_preview" && spec.Modelo == "prop_ld_handbag") ||
         (spec.Recurso == "sacola_papel_sono84_preview" && spec.Modelo == "prop_paper_bag_small"); }

public void PollOfficeBag84RigidPreview()
{
    if (!HasOfficeBag84Preview) return;
    Ready(); RequireActors(); RequireProps();
    State state = states["SACOLA_CALIBRACAO84"];
    Actor actor = actors["DJANIRA_POBRE"]; GTA.Ped ped = actor.Ped;
    GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
    if (!officeCellBodyPreview84 || lastElapsed != -1 || states.Count != 2 || actors.Count != 1 ||
        !OfficeBag84Spec(state.Spec) ||
        !state.BoundsKnown || !state.Ready || !ped.IsPositionFrozen || !prop.IsVisible ||
        state.Owner != "DJANIRA_POBRE" || !prop.IsAttachedTo(ped) ||
        GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS) ||
        !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, ped.Handle,
            "anim@scripted@freemode@ig6_office_cell_sleep@female@", "base_pose_02", 3))
        throw Error("84 own compact preview requires exact prepared frozen Office body/own handbag/Story, no action.");
    GTA.PedBone torso = ped.Bones[GTA.Bone.SkelSpine3], left = ped.Bones[GTA.Bone.SkelLeftHand],
        rightHand = ped.Bones[GTA.Bone.SkelRightHand], pelvis = ped.Bones[GTA.Bone.SkelPelvis], spine0 = ped.Bones[GTA.Bone.SkelSpine0];
    foreach (GTA.PedBone bone in new GTA.PedBone[] { torso, left, rightHand, pelvis, spine0 })
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("84 compact fit actual bones unavailable.");
    GTA.Math.Vector3 l = torso.GetPositionOffset(left.Position), r = torso.GetPositionOffset(rightHand.Position);
    Vector(l, 3f, "84 left hand Spine3"); Vector(r, 3f, "84 right hand Spine3");
    if (!officeBag84Prepared)
    {
        GTA.Math.Vector3 extent = state.Max - state.Min;
        bool paperBag = state.Spec.Recurso == "sacola_papel_sono84_preview";
        if (!paperBag && ((state.Min - new GTA.Math.Vector3(-.214329f,-.060144f,-.557557f)).Length() > .002f ||
            (state.Max - new GTA.Math.Vector3(.214329f,.060144f,.061225f)).Length() > .002f))
            throw Error("84 loaded handbag bounds differ from actual193 literal model proof.");
        if (paperBag)
        {
            GTA.Math.Vector3 head = torso.GetPositionOffset(ped.Bones[GTA.Bone.SkelHead].Position);
            GTA.Math.Vector3 hipBefore = torso.GetPositionOffset(pelvis.Position);
            float separation = System.Math.Abs(l.Z-r.Z), midX = (l.X+r.X)*.5f;
            bool betweenPalms = extent.X < separation;
            bool belowNeckAboveHip = midX+extent.Z*.5f < head.X && midX-extent.Z*.5f > hipBefore.X;
            Log("SONO84_PAPERBAG_FIT_CANDIDATE", "Model=prop_paper_bag_small; NativeLoadedBounds="+GroundVector(extent)+
                "; PalmsSeparation="+separation.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
                "; BetweenPalms="+betweenPalms+"; LongitudinalInsideHeadHip="+belowNeckAboveHip+
                "; LeftHand="+GroundVector(l)+"; RightHand="+GroundVector(r)+"; Head="+GroundVector(head)+"; Pelvis="+GroundVector(hipBefore)+
                "; NativeBoundsBeforeCreate=True; Scale=1; BodySetter=False; AttachmentSetter=False; SkinUnknown=True; ImagePending=True; HugFalse=True");
            if (!betweenPalms || !belowNeckAboveHip)
                throw Error("84 actual paper bag cannot fit this measured palm/head/hip envelope; no scale/axis retry or tolerance exception.");
        }
        GTA.Math.Vector3 hip = torso.GetPositionOffset(pelvis.Position), lower = torso.GetPositionOffset(spine0.Position);
        Vector(hip, 3f, "84 pelvis Spine3"); Vector(lower, 3f, "84 spine0 Spine3");
        // Native194 image rejected under-palms panel/neck handles. Place bottom
        // at the actual lower palm minus5mm, and centre longitudinally on both
        // actual palms (abdomen), retaining scale/axes/body and all nextTick guards.
        // This is a rigid PREP candidate, not a measured skin clearance/hug claim.
        GTA.Math.Vector3 center = new GTA.Math.Vector3((l.X+r.X)*.5f,
            System.Math.Min(l.Y,r.Y) - .005f + extent.Y*.5f, (l.Z+r.Z)*.5f);
        GTA.Math.Vector3 nativeCenter = (state.Min+state.Max)*.5f;
        // RY+90 maps bagX to -torsoZ, bagY to torsoY, bagZ to torsoX.
        // Correct noncentral native pivot; model dimensions are never scaled.
        officeBag84Offset = center - new GTA.Math.Vector3(nativeCenter.Z,nativeCenter.Y,-nativeCenter.X);
        Vector(officeBag84Offset, 1f, "84 one PREP rigid attachment offset");
        GTA.Math.Vector3 world = torso.GetOffsetPosition(officeBag84Offset);
        if ((torso.GetPositionOffset(world)-officeBag84Offset).Length() > .001f)
            throw Error("84 measured bone-local rigid transform round trip failed.");
        Log("SONO84_BOLSA_PREP_RIGIDA", "Scene=teste_fila_84; Model=" + state.Spec.Modelo + "; PropHandle=" + prop.Handle +
            "; PropAddress=" + prop.MemoryAddress + "; BoundsMin=" + GroundVector(state.Min) + "; BoundsMax=" + GroundVector(state.Max) +
            "; LeftHandSpine3=" + GroundVector(l) + "; RightHandSpine3=" + GroundVector(r) + "; PelvisSpine3=" + GroundVector(hip) +
            "; Spine0Local=" + GroundVector(lower) + "; CenterSpine3=" + GroundVector(center) + "; OffsetSpine3=" + GroundVector(officeBag84Offset) +
            "; RotationBoneLocal=(0,90,0); Scale=1; Derivation=actualBoundsAndBones; BeforeAction=True; SetterOnce=True;" +
            " ActorSetter=False; PhaseSetter=False; MeshFitPending=True; SkinUnknown=True; HugFalse=True; ARM=False; ProductionReady=False");
        prop.Detach(false,false); prop.IsPositionFrozen=false; prop.IsCollisionEnabled=false;
        prop.AttachTo(torso, officeBag84Offset, new GTA.Math.Vector3(0f,90f,0f), false,false,false,false,GTA.EulerRotationOrder.YXZ,true,false);
        if (!prop.IsAttachedTo(ped)) throw Error("84 actual own Spine3 attachment missing.");
        state.Hand="spine3_preview"; officeBag84Prepared=true; officeBag84Frame=GTA.Game.FrameCount;
        return; // No same-frame success: actual rigid axes and contacts on a later frame.
    }
    if (state.Hand != "spine3_preview") throw Error("84 compact preview ownership changed.");
    if (GTA.Game.FrameCount == officeBag84Frame) return;
    GTA.Math.Vector3 x = prop.GetOffsetPosition(new GTA.Math.Vector3(1f,0f,0f))-prop.Position;
    GTA.Math.Vector3 y = prop.GetOffsetPosition(new GTA.Math.Vector3(0f,1f,0f))-prop.Position;
    GTA.Math.Vector3 z = prop.GetOffsetPosition(new GTA.Math.Vector3(0f,0f,1f))-prop.Position;
    GTA.Math.Vector3 tx = torso.GetOffsetPosition(new GTA.Math.Vector3(1f,0f,0f))-torso.Position;
    GTA.Math.Vector3 ty = torso.GetOffsetPosition(new GTA.Math.Vector3(0f,1f,0f))-torso.Position;
    GTA.Math.Vector3 tz = torso.GetOffsetPosition(new GTA.Math.Vector3(0f,0f,1f))-torso.Position;
    float axisDelta=System.Math.Max((x+tz).Length(),System.Math.Max((y-ty).Length(),(z-tx).Length()));
    float pivotDelta=(prop.Position-torso.GetOffsetPosition(officeBag84Offset)).Length();
    GTA.Math.Vector3 lp=prop.GetPositionOffset(left.Position), rp=prop.GetPositionOffset(rightHand.Position), sp=prop.GetPositionOffset(torso.Position);
    Vector(lp, 3f, "84 actual prop-local left hand"); Vector(rp, 3f, "84 actual prop-local right hand"); Vector(sp,3f,"84 actual prop-local torso");
    float lg=SleepGripMaximumGap(new GTA.Math.Vector3[]{lp},GTA.Math.Vector3.Zero,state.Min,state.Max);
    float rg=SleepGripMaximumGap(new GTA.Math.Vector3[]{rp},GTA.Math.Vector3.Zero,state.Min,state.Max);
    float sg=SleepGripMaximumGap(new GTA.Math.Vector3[]{sp},GTA.Math.Vector3.Zero,state.Min,state.Max);
    float bottom=float.MaxValue, top=float.MinValue;
    System.Text.StringBuilder vertices=new System.Text.StringBuilder();
    for(int i=0;i<8;i++)
    {
        GTA.Math.Vector3 local=new GTA.Math.Vector3((i&1)==0?state.Min.X:state.Max.X,(i&2)==0?state.Min.Y:state.Max.Y,(i&4)==0?state.Min.Z:state.Max.Z);
        GTA.Math.Vector3 actual=torso.GetPositionOffset(prop.GetOffsetPosition(local));Vector(actual,3f,"84 actual handbag corner torso");
        bottom=System.Math.Min(bottom,actual.Y);top=System.Math.Max(top,actual.Y);vertices.Append("; Corner"+i+"Spine3="+GroundVector(actual));
    }
    bool transformExact=Finite(axisDelta)&&Finite(pivotDelta)&&axisDelta<=.01f&&pivotDelta<=.01f;
    bool contactWithinOldBound=lg<=.35f&&rg<=.35f&&sg<=.35f;
    if (!officeBag84Verified || GTA.Game.GameTime>=officeBag84NextLog || !transformExact || !contactWithinOldBound)
    {
        Log("SONO84_BOLSA_RIGIDA_GETTERS", "Scene=teste_fila_84; PropHandle="+prop.Handle+"; PropAddress="+prop.MemoryAddress+
            "; Position="+GroundVector(prop.Position)+"; OffsetSpine3="+GroundVector(officeBag84Offset)+"; AxisDelta="+axisDelta.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
            "; PivotDelta="+pivotDelta.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; FrameDelta="+unchecked((uint)(GTA.Game.FrameCount-officeBag84Frame))+
            "; LeftOBBGap="+lg.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; RightOBBGap="+rg.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
            "; Spine3OBBGap="+sg.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; BottomSpine3Y="+bottom.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+
            "; TopSpine3Y="+top.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; LeftHandSpine3="+GroundVector(l)+"; RightHandSpine3="+GroundVector(r)+
            "; TransformExact="+transformExact+"; ProximityWithinExisting035="+contactWithinOldBound+vertices+
            "; PropVisible="+prop.IsVisible+"; Physics="+prop.HasPhysics+"; Attached=True; Scale=1; AttachmentSetterThisFrame=False;"+
            " MeshVoidUnknown=True; SkinUnknown=True; HugFalse=True; RootImageRequired=True; ARM=False; ProductionReady=False");
        officeBag84NextLog=GTA.Game.GameTime+250;
    }
    if(!transformExact||!contactWithinOldBound)throw Error("84 compact candidate rigid transform/contact failed; no tolerance increase or retry attachment.");
    officeBag84Verified=true; // Rigid transform/proximity only, NEVER hug/skin/image approval.
}

    // One rigid own attachment calibration in PREP. Reset reuses the same bone-local offset.
    // No actor/clip/bone/rotation edits and never called from Update/count/action.
    public bool PrepareSleepBagRigidGrip(string codigo)
    {
        Ready(); RequireActors(); RequireProps();
        State state, bench;
        if (!sleepBenchC1 || lastElapsed != -1 || codigo != "SACOLA_DJANIRA" || states.Count != 2 ||
            actors.Count != 1 || !actors.ContainsKey("DJANIRA_POBRE") || !states.TryGetValue(codigo, out state) ||
            !states.TryGetValue("BANCO_PRAIA", out bench) || bench.Spec.Recurso != "banco_sono_c1_02" ||
            bench.Spec.Modelo != "prop_bench_02" || state.Spec.Recurso != "sacola" ||
            state.Spec.Modelo != "prop_cs_shopping_bag" || state.Spec.AssentarNoChao ||
            state.Spec.AtorInicial != "DJANIRA_POBRE" || state.Spec.MaoInicial != "esquerda" ||
            state.Owner != "DJANIRA_POBRE" || state.Hand != "esquerda" || state.Spec.Eventos.Count != 0 ||
            !state.BoundsKnown || !state.Spec.VisivelInicial)
            throw Error("sleep grip exclusive own C1 bench02/Poor/bag left hand in PREP before action");
        GTA.Ped ped = actors[state.Owner].Ped;
        if (!ped.IsPositionFrozen || !GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM,
            ped.Handle, "timetable@tracy@sleep@", "base", 3))
            throw Error("sleep grip requires actual frozen Tracy body after support alignment");
        if (state.SleepBagGripPrepared) return false; // Never recalibrate at F7/reset/Update.
        GTA.Prop prop = owned.GetProp(codigo);
        GTA.PedBone hand = ped.Bones[HandBone(state, "esquerda")];
        GTA.PedBone rightHand = ped.Bones[GTA.Bone.SkelRightHand], torso = ped.Bones[GTA.Bone.SkelSpine3];
        if (System.Object.ReferenceEquals(hand, null) || !hand.IsValid ||
            System.Object.ReferenceEquals(rightHand, null) || !rightHand.IsValid ||
            System.Object.ReferenceEquals(torso, null) || !torso.IsValid || !prop.IsAttachedTo(ped) || !prop.IsVisible)
            throw Error("sleep grip actual own bone/attachment/visibility missing");
        GTA.Math.Vector3[] points = new GTA.Math.Vector3[] { hand.Position, rightHand.Position, torso.Position };
        GTA.Math.Vector3[] local = new GTA.Math.Vector3[3];
        for (int i = 0; i < 3; i++) { Vector(points[i], 30000f, "sleep grip bone"); local[i] = prop.GetPositionOffset(points[i]); Vector(local[i], 10f, "sleep grip local bone"); }
        float before = SleepGripMaximumGap(local, GTA.Math.Vector3.Zero, state.Min, state.Max);
        if (before <= 0.35f)
        { state.SleepBagGripOffset = state.Spec.OffsetInicial; state.SleepBagGripPrepared = true; return false; }
        int first = 0, second = 1; float farthest = (points[0] - points[1]).Length();
        for (int i = 0; i < 3; i++) for (int j = i + 1; j < 3; j++)
        {
            float distance = (points[i] - points[j]).Length();
            if (distance > farthest) { farthest = distance; first = i; second = j; }
        }
        GTA.Math.Vector3 boundsCenter = (state.Min + state.Max) * 0.5f;
        GTA.Math.Vector3 actualCenter = prop.GetOffsetPosition(boundsCenter);
        GTA.Math.Vector3 targetCenter = (points[first] + points[second]) * 0.5f;
        GTA.Math.Vector3 fullWorldDelta = targetCenter - actualCenter;
        GTA.Math.Vector3 fullLocalDelta = prop.GetPositionOffset(prop.Position + fullWorldDelta);
        Vector(fullWorldDelta, 1f, "sleep grip world translation"); Vector(fullLocalDelta, 1f, "sleep grip local translation");
        float endpoint = SleepGripMaximumGap(local, fullLocalDelta, state.Min, state.Max);
        if (!Finite(endpoint) || endpoint >= 0.35f)
            throw Error("sleep grip rigid translation has no measured solution inside original contact guard");
        // Keep half of the measured endpoint slack. This is a placement target, not a relaxed guard.
        float targetGap = (0.35f + endpoint) * 0.5f, low = 0f, high = 1f;
        for (int i = 0; i < 20; i++)
        {
            float middle = (low + high) * 0.5f;
            if (SleepGripMaximumGap(local, fullLocalDelta * middle, state.Min, state.Max) <= targetGap) high = middle;
            else low = middle;
        }
        GTA.Math.Vector3 pivot = prop.Position + fullWorldDelta * high;
        GTA.Math.Vector3 offset = hand.GetPositionOffset(pivot);
        Attachment(state.Owner, state.Hand, offset, state.Spec.RotacaoInicial); // Existing <=.5m/rotation limits.
        if ((hand.GetOffsetPosition(offset) - pivot).Length() > 0.001f)
            throw Error("sleep grip bone transform failed the .001m round trip");
        float predicted = SleepGripMaximumGap(local, fullLocalDelta * high, state.Min, state.Max);
        if (!Finite(predicted) || predicted > targetGap || predicted > 0.35f)
            throw Error("sleep grip numerical candidate failed original contact guard");
        Log("SLEEP_BAG_RIGID_PREP", "Codigo=" + codigo + "; Hand=SkelLeftHand; OwnHandle=" + prop.Handle +
            "; Left=" + points[0] + "; Right=" + points[1] + "; Spine3=" + points[2] + "; Min=" + state.Min +
            "; Max=" + state.Max + "; LocalLeft=" + local[0] + "; LocalRight=" + local[1] + "; LocalSpine=" + local[2] +
            "; ActualBBoxCenter=" + actualCenter + "; FarthestPair=" + first + "," + second + "; TargetCenter=" + targetCenter +
            "; PivotCandidate=" + pivot + "; BoneLocalOffset=" + offset + "; Rotation=" + state.Spec.RotacaoInicial +
            "; BeforeMaximumGap=" + ContactNumber(before) + "; PredictedMaximumGap=" + ContactNumber(predicted) +
            "; TargetGap=" + ContactNumber(targetGap) + "; Fraction=" + ContactNumber(high) +
            "; ContactGuard=0.35; OffsetGuard=0.5; RotationChanged=False; SpecChanged=False; OnePREPAttach=True; PostTickAndFullActionContactPending=True; VisualApproval=False");
        Attach(state, state.Owner, state.Hand, offset, state.Spec.RotacaoInicial, false);
        state.SleepBagGripOffset = offset; state.SleepBagGripPrepared = true;
        return true; // Caller leaves Ready false and remeasures actual contact in the next Tick.
    }
    private static float SleepGripMaximumGap(GTA.Math.Vector3[] points, GTA.Math.Vector3 translation,
        GTA.Math.Vector3 min, GTA.Math.Vector3 max)
    {
        float maximum = 0f;
        foreach (GTA.Math.Vector3 original in points)
        {
            GTA.Math.Vector3 point = original - translation;
            float x = System.Math.Max(System.Math.Max(min.X - point.X, point.X - max.X), 0f);
            float y = System.Math.Max(System.Math.Max(min.Y - point.Y, point.Y - max.Y), 0f);
            float z = System.Math.Max(System.Math.Max(min.Z - point.Z, point.Z - max.Z), 0f);
            maximum = System.Math.Max(maximum, (float)System.Math.Sqrt(x*x + y*y + z*z));
        }
        return maximum;
    }

    // beerf/foodtraya do autor usam PHRightHand (28422); demais props mantêm SKEL.
    private static GTA.Bone HandBone(State state, string hand)
    {
        // notepad do autor RPEmotes prende somente o lápis direito ao dedo00 (58866).
        if (state.Spec.Recurso == "sacola" && state.Spec.Modelo == "prop_cs_shopping_bag" && hand == "direita") return GTA.Bone.PHRightHand;
        if (state.Spec.Recurso == "documento_script" && hand == "direita") return GTA.Bone.PHRightHand;
        if (state.Spec.Recurso == "lapis" && hand == "direita") return GTA.Bone.SkelRightFinger00;
        if (state.Spec.Recurso == "bandeja_champanhe_autor" && hand == "direita") return GTA.Bone.PHRightHand;
        if (state.Spec.Recurso == "garrafa_cerveja" || state.Spec.Recurso == "celular" || state.Spec.Recurso == "bandeja_servico")
            return hand == "direita" ? GTA.Bone.PHRightHand : GTA.Bone.PHLeftHand;
        return hand == "direita" ? GTA.Bone.SkelRightHand : GTA.Bone.SkelLeftHand;
    }

    public GTA.Prop GetReading99OwnProxy(string sceneId, string sourceSHA)
    {
        Ready(); State state; Actor actor;
        if (sceneId != "teste_fila_99" || sourceSHA != ObservadorAbertura99.ExactSourceSHA || states.Count != 1 || actors.Count != 1 ||
            !states.TryGetValue("PAPEL_HERANCA", out state) || !actors.TryGetValue("DJANIRA_POBRE", out actor) ||
            state.Owner != "DJANIRA_POBRE" || state.Hand != "direita" || state.Spec.Recurso != "documento_script" ||
            state.Spec.Modelo != "p_cs_script_s" || state.Spec.Eventos.Count != 0 || state.Spec.Morph86 != null ||
            state.Spec.OffsetInicial != GTA.Math.Vector3.Zero || state.Spec.RotacaoInicial != GTA.Math.Vector3.Zero ||
            HandBone(state, "direita") != GTA.Bone.PHRightHand)
            throw Error("Leitura99 readonly exige proprio documento_script/Poor/PHRightHand sem eventos.");
        RequireActors(); RequireProps(); GTA.Prop prop = GetProp("PAPEL_HERANCA");
        if (!prop.IsAttachedTo(actor.Ped) || prop.Model.Hash != unchecked((int)GTA.StringHash.AtStringHash("p_cs_script_s")))
            throw Error("Leitura99 proxy/dono reais divergem.");
        return prop; // Read-only normal owned manager lookup, never adoption/attachment setter.
    }

    public GTA.Prop GetProp(string codigo) { Ready(); if (!states.ContainsKey(codigo ?? "")) throw Error("objeto desconhecido"); return owned.GetProp(codigo); }
    public void BindPocket98(PapelBolso98Diretor renderer, string sourceHash)
    {
        Ready();State paper;
        if(!pocket98Scope||pocket98!=null||renderer==null||sourceHash!=PapelBolso98Diretor.ExactSourceHash||lastElapsed>=0||
            actors.Count!=1||!actors.ContainsKey("DJANIRA_RICA")||states.Count!=2||!states.ContainsKey("MESA_LIMPEZA")||
            !states.TryGetValue("PAPEL_BOLSO",out paper)||paper.Spec.Recurso!="carta_simples"||paper.Owner!="DJANIRA_RICA"||paper.Hand!="direita"||
            paper.Spec.Eventos.Count!=1||paper.Spec.Eventos[0].Tipo!="colocar"||paper.Spec.Eventos[0].Apoio!="MESA_LIMPEZA"||
            states["MESA_LIMPEZA"].Spec.Recurso!="bancada_limpeza_candidata"||states["MESA_LIMPEZA"].Spec.Modelo!="prop_ff_counter_02")
            throw Error("bind98 requires exact own Rich/one paper/one measured table before action");
        renderer.RequireParent(owned.GetProp("PAPEL_BOLSO"));pocket98=renderer;
    }
    public void UnbindPocket98(PapelBolso98Diretor renderer)
    {if(pocket98==null)return;if(!System.Object.ReferenceEquals(renderer,pocket98))throw Error("unbind98 different renderer");pocket98=null;}
    public int Pocket98Stage
    {
        get {Ready();if(!pocket98Scope||pocket98==null)throw Error("98 not bound");State s=states["PAPEL_BOLSO"];
            if(s.Owner=="DJANIRA_RICA"&&s.Hand=="bolso")return 0;
            if(s.Owner=="DJANIRA_RICA"&&s.Hand=="direita")return 1;
            if(s.Owner==null&&s.Hand==null&&s.PickupAnchorKnown&&s.PickupPlacement!=null)return 2;
            throw Error("unexpected98 ownership state");}
    }
    public void PreparePocket98(string sourceHash)
    {
        Ready();if(!pocket98Scope||pocket98==null||sourceHash!=PapelBolso98Diretor.ExactSourceHash||lastElapsed>=0)throw Error("98 PREP only");
        RequireActors();State s=states["PAPEL_BOLSO"];GTA.Ped ped=actors["DJANIRA_RICA"].Ped;
        GTA.PedBone pelvis=ped.Bones[GTA.Bone.SkelPelvis],thigh=ped.Bones[GTA.Bone.SkelRightThigh];
        if(System.Object.ReferenceEquals(pelvis,null)||!pelvis.IsValid||System.Object.ReferenceEquals(thigh,null)||!thigh.IsValid)throw Error("98 actual pelvis/thigh unavailable");
        GTA.Math.Vector3 measured=(pelvis.Position+thigh.Position)*.5f;Vector(measured,30000,"98 measured pocket region");
        if(!pocket98OffsetKnown){pocket98Offset=pelvis.GetPositionOffset(measured);Vector(pocket98Offset,1,"98 PREP bone-local pocket anchor");pocket98OffsetKnown=true;}
        GTA.Math.Vector3 target=pelvis.GetOffsetPosition(pocket98Offset);if((target-measured).Length()>.35f)throw Error("98 stored pocket anchor changed after reset");
        GTA.Prop prop=owned.GetProp("PAPEL_BOLSO");prop.Detach(false,false);prop.IsPositionFrozen=false;prop.IsCollisionEnabled=false;
        prop.AttachTo(pelvis,pocket98Offset,GTA.Math.Vector3.Zero,false,false,false,false,GTA.EulerRotationOrder.YXZ,true,false);
        if(!prop.IsAttachedTo(ped)||(prop.Position-target).Length()>.01f)throw Error("98 actual PREP pelvis attachment not confirmed");
        s.Owner="DJANIRA_RICA";s.Hand="bolso";s.PickupAnchorKnown=false;s.PickupPlacement=null;
        Log("BOLSO98_ANCORA_PREP", "Proxy="+prop.Position+"; Pelvis="+pelvis.Position+"; RightThigh="+thigh.Position+"; MeasuredPocketRegion="+measured+"; LocalOffset="+pocket98Offset+"; ResetReuse=True; BeforeAction=True; PaperInsideClothingVisualPending=True; HandAttach=False; Scope=technical98");
    }
    public void AcquirePocket98(string sourceHash)
    {
        Ready();if(!pocket98Scope||pocket98==null||sourceHash!=PapelBolso98Diretor.ExactSourceHash||Pocket98Stage!=0)throw Error("98 unique pocket acquisition required");
        pocket98.RequireAcquisitionNow();
        Attach(states["PAPEL_BOLSO"],"DJANIRA_RICA","direita",GTA.Math.Vector3.Zero,GTA.Math.Vector3.Zero,true);
    }
    public void RequirePocket98RestedSurface(GTA.Math.Vector3[] vertices)
    {
        Ready();if(!pocket98Scope||pocket98==null||Pocket98Stage!=2||vertices==null||vertices.Length!=8)throw Error("98 actual placed eight vertices required");
        State s=states["PAPEL_BOLSO"];Timed place=s.PickupPlacement;RequirePlacementSupport(place);
        GTA.Prop table=owned.GetProp(place.Event.Apoio);State tableState=states[place.Event.Apoio];float lowest=float.MaxValue;
        foreach(GTA.Math.Vector3 point in vertices){Vector(point,30000,"98 actual rendered vertex");GTA.Math.Vector3 local=table.GetPositionOffset(point);
            if(local.X<tableState.Min.X||local.X>tableState.Max.X||local.Y<tableState.Min.Y||local.Y>tableState.Max.Y)throw Error("98 rendered face left actual own table bounds");
            lowest=System.Math.Min(lowest,point.Z);}
        float gap=lowest-place.PlacementPoint.Z;if(!Finite(gap)||gap<-.005f||gap>.03f)throw Error("98 actual rendered face is not resting on measured own table");
    }

    public void BindAuthoredPaperC5(PapelDobradoAutoralC5Diretor renderer, string sourceSha)
    {
        Ready();
        State paper;
        if (!authoredPaperC5Scope || renderer == null || authoredPaperC5 != null || lastElapsed >= 0 ||
            sourceSha != PapelDobradoAutoralC5Diretor.ExactSourceHash || !states.TryGetValue("PAPEL_IRACI", out paper) ||
            paper.Spec.Recurso != "papel_dobrado_autoral" || paper.Owner != "DONA_IRACI" || paper.Hand != "direita")
            throw Error("bind authored C5 requires exact prepared own donor-right proxy before action");
        renderer.RequireParent(owned.GetProp("PAPEL_IRACI")); authoredPaperC5 = renderer;
    }
    public void UnbindAuthoredPaperC5(PapelDobradoAutoralC5Diretor renderer)
    { if (authoredPaperC5 == null) return; if (!System.Object.ReferenceEquals(authoredPaperC5, renderer)) throw Error("unbind different authored renderer"); authoredPaperC5 = null; }
    public string GetAuthoredPaperC5Owner()
    { Ready(); if (!authoredPaperC5Scope || authoredPaperC5 == null) throw Error("authored C5 renderer absent"); return states["PAPEL_IRACI"].Owner; }
    public void RequireAuthoredPaperC5ActionReady()
    { authoredPaperC5.RequireActionReady(GetAuthoredPaperC5Owner()); }
    private bool IsAuthoredVisual(string codigo)
    { return authoredPaperC5Scope && codigo == "PAPEL_IRACI" && authoredPaperC5 != null; }
    public bool IsObjectVisualSourceConfigured(string codigo)
    {
        GTA.Prop prop = GetProp(codigo);
        if (IsAuthoredVisual(codigo)) { authoredPaperC5.RequireParent(prop); authoredPaperC5.RequireConfigured(states[codigo].Owner); return true; }
        if (pocket98Scope && codigo == "PAPEL_BOLSO" && pocket98 != null) { pocket98.RequireParent(prop); pocket98.RequireConfigured(); return true; }
        return prop.IsVisible;
    }
    public GTA.Math.Vector3 GetObjectVisualCenter(string codigo, GTA.Math.Vector3 fallbackLocalCenter)
    { return IsAuthoredVisual(codigo) ? authoredPaperC5.GetVisualCenter(states[codigo].Owner) : GetProp(codigo).GetOffsetPosition(fallbackLocalCenter); }
    public bool TryGetAuthoredVisualPoints(string codigo, out GTA.Math.Vector3[] points)
    { if (IsAuthoredVisual(codigo)) { points = authoredPaperC5.GetSurfaceWorld(states[codigo].Owner); return true; } points = null; return false; }
    public void GetMeasuredBounds(string codigo, out GTA.Math.Vector3 minimum, out GTA.Math.Vector3 maximum)
    { GetProp(codigo); State state = states[codigo]; minimum = state.Min; maximum = state.Max; }
    public bool IsSupport(string codigo)
    { GetProp(codigo); return Supports.Contains(states[codigo].Spec.Recurso); }

    public bool TryGetSleepBenchSurfaceXY(string codigo, out float x, out float y)
    {
        x = y = 0f;
        if (!sleepBenchC1 || codigo != "BANCO_PRAIA") return false;
        Ready(); owned.GetSleepBenchMeasuredCenterXY(codigo, out x, out y);
        return true;
    }

    public void RequireSleepBenchBodyProjection(string codigo, GTA.Math.Vector3[] bones)
    {
        if (!sleepBenchC1 || codigo != "BANCO_PRAIA") return;
        Ready(); if (bones == null || bones.Length != 5) throw Error("sleep bench requires five actual core body bone projections");
        GTA.Prop prop = owned.GetProp(codigo); GTA.Math.Vector3 min, max;
        owned.GetSleepBenchMeasuredRectangle(codigo, out min, out max);
        bool prep = lastElapsed < 0, outside = false;
        float bodyMinX = float.MaxValue, bodyMaxX = float.MinValue, bodyMinY = float.MaxValue, bodyMaxY = float.MinValue;
        string[] names = new string[] { "Pelvis", "Head", "LeftFoot", "RightFoot", "Spine2" };
        for (int i = 0; i < bones.Length; i++)
        {
            Vector(bones[i], 20000f, "actual lying bone"); GTA.Math.Vector3 local = prop.GetPositionOffset(bones[i]);
            Vector(local, 20000f, "actual lying bone local");
            bool inside = local.X >= min.X && local.X <= max.X && local.Y >= min.Y && local.Y <= max.Y;
            if (prep || !inside)
                Log("BANCO_C1_CORPO_PROJECAO", "BoneIndex=" + i + "; World=" + bones[i] + "; Local=" + local +
                    "; SeatMin=" + min + "; SeatMax=" + max + "; InsideMeasuredSeat=" + inside + "; XYMargin=0; SkinContactProven=False; VisualApproval=False" +
                    "; BoneName=" + names[i] + "; AllFiveBeforePrepFailure=True; TransformSetter=False");
            if (!inside)
            {
                if (!prep) throw Error("lying body projection outside actual measured bench seat; no full-bbox support or extra contact margin");
                outside = true;
            }
            if (prep)
            {
                bodyMinX = System.Math.Min(bodyMinX, local.X); bodyMaxX = System.Math.Max(bodyMaxX, local.X);
                bodyMinY = System.Math.Min(bodyMinY, local.Y); bodyMaxY = System.Math.Max(bodyMaxY, local.Y);
            }
        }
        if (prep)
            Log("BANCO_C1_CORPO_PROJECAO_ENVELOPE", System.String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Bones=5; BodyMinXY=({0:R},{1:R}); BodyMaxXY=({2:R},{3:R}); BodySpanXY=({4:R},{5:R}); SeatSpanXY=({6:R},{7:R}); TranslationAllowedX=({8:R},{9:R}); TranslationAllowedY=({10:R},{11:R}); RigidTranslationFits={12}; AnyOutside={13}; XYMargin=0; TransformSetter=False; PhaseOrHeadingNotChanged=True; SkinContactProven=False; VisualApproval=False",
                bodyMinX, bodyMinY, bodyMaxX, bodyMaxY, bodyMaxX - bodyMinX, bodyMaxY - bodyMinY,
                max.X - min.X, max.Y - min.Y, min.X - bodyMinX, max.X - bodyMaxX, min.Y - bodyMinY, max.Y - bodyMaxY,
                min.X - bodyMinX <= max.X - bodyMaxX && min.Y - bodyMinY <= max.Y - bodyMaxY, outside));
        if (outside) throw Error("lying body projection outside actual measured bench seat; no full-bbox support or extra contact margin");
    }

    // Pesquisa assincrônica, só fora da filmagem. XY é relativo ao modelo, nunca XYZ absoluto.
    public int RequestSurface(string codigo, float localX, float localY, bool sameCallProof = false)
    {
        Ready(); return RequestSurfaceCore(codigo, localX, localY, sameCallProof);
    }

    private int RequestSurfaceCore(string codigo, float localX, float localY, bool sameCallProof = false)
    {
        State state;
        if (!prepared || !ready || !states.TryGetValue(codigo ?? "", out state)) throw Error("apoio próprio preparado é necessário para medir superfície");
        GTA.Prop prop = owned.GetProp(codigo);
        if (!Supports.Contains(state.Spec.Recurso) || lastElapsed >= 0) throw Error("superfície só pode ser medida em apoio próprio, antes da ação");
        if (!Finite(localX) || !Finite(localY) || localX < state.Min.X || localX > state.Max.X || localY < state.Min.Y || localY > state.Max.Y)
            throw Error("ponto candidato da superfície precisa ficar nos bounds reais do modelo");
        if (surfaces.Count >= 16) throw Error("limite de16medições de superfície por preparo");
        GTA.Math.Vector3 top = prop.GetOffsetPosition(new GTA.Math.Vector3(localX, localY, state.Max.Z + 0.2f));
        GTA.Math.Vector3 bottom = prop.GetOffsetPosition(new GTA.Math.Vector3(localX, localY, state.Min.Z - 0.2f));
        if (sameCallProof) foreach (SurfaceProbe other in surfaces.Values)
            if (!other.Known) throw Error("same-call proof cannot consume another pending asynchronous surface");
        int traceFlags = sameCallProof ? 17 : 16, optionFlags = sameCallProof ? 0 : 7;
        // 0x377906D8A31E5586 is the documented synchronous native; exclusive preparation opt-in.
        GTA.Native.Hash startHash = sameCallProof ? (GTA.Native.Hash)0x377906D8A31E5586UL : GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE;
        int ray = GTA.Native.Function.Call<int>(startHash,
            top.X, top.Y, top.Z, bottom.X, bottom.Y, bottom.Z, traceFlags, 0, optionFlags);
        if (ray == 0) throw Error("o motor recusou a medição de superfície do apoio");
        int key = ++nextSurface;
        SurfaceProbe probe = new SurfaceProbe { State = state, Ray = ray, StartedAt = GTA.Game.GameTime,
            LocalX = localX, LocalY = localY, Top = top, Bottom = bottom, Synchronous = sameCallProof,
            TraceFlags = traceFlags, OptionFlags = optionFlags };
        surfaces.Add(key, probe);
        LogSurfaceProbe(probe, prop, "inicio", -1, System.IntPtr.Zero, System.IntPtr.Zero, System.IntPtr.Zero, System.IntPtr.Zero);
        if (sameCallProof)
        {
            PollSurfaces(); // Same script call; only this fresh probe is unknown. All original geometry guards run.
            if (!probe.Known) throw Error("synchronous own surface did not complete in the same call; no fabricated proof");
        }
        return key;
    }

    // Preparação exclusiva de props sobre outro apoio próprio: cinco raios, sem espera bloqueante.
    // Nunca é chamada por Update, que conserva somente os eventos físicos durante a ação.
    private bool PollInitialSupports()
    {
        if (initialSupportState == null)
        {
            foreach (State candidate in states.Values)
                if (!System.String.IsNullOrEmpty(candidate.Spec.ApoioInicial) && !candidate.InitialSupportKnown)
                {
                    BeginInitialSupport(candidate);
                    return false;
                }
            return true;
        }
        State state = initialSupportState;
        if (unchecked((uint)(GTA.Game.GameTime - state.PreparedAt)) >= TimeoutMs)
            throw Error("superfície inicial não foi confirmada no preparo limitado de5s: " + state.Spec.Codigo);
        State support = RequireInitialSupport(state);
        float highest = float.MinValue, lowest = float.MaxValue;
        foreach (int id in state.InitialSurfaceIndices)
        {
            SurfaceProbe probe;
            if (!surfaces.TryGetValue(id, out probe)) throw Error("medição inicial de apoio ausente");
            if (!probe.Known) return false;
            highest = System.Math.Max(highest, probe.Point.Z);
            lowest = System.Math.Min(lowest, probe.Point.Z);
        }
        if (!Finite(highest) || !Finite(lowest) || highest - lowest > 0.08f)
            throw Error("superfície inicial irregular sob os limites reais do objeto");
        SurfaceProbe center = surfaces[state.InitialSurfaceIndices[0]];
        GTA.Math.Vector3 position = new GTA.Math.Vector3(state.InitialSupportPosition.X, state.InitialSupportPosition.Y, highest - state.Min.Z);
        Vector(position, 30000f, "posição do prop sobre apoio inicial");
        GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
        if (prop.IsAttached() || !prop.IsPositionFrozen || prop.IsCollisionEnabled)
            throw Error("prop inicial precisa próprio, livre, congelado e sem colisão durante os raios");
        prop.PositionNoOffset = position; prop.Rotation = state.Rotation; prop.Velocity = GTA.Math.Vector3.Zero;
        Vector(prop.Position, 30000f, "posição real sobre apoio inicial");
        Vector(prop.Rotation, 360f, "rotação real sobre apoio inicial");
        if ((prop.Position - position).Length() > 0.01f || (prop.Rotation - state.Rotation).Length() > 0.01f)
            throw Error("o motor não confirmou transform do prop sobre apoio inicial");
        state.Position = prop.Position; state.Rotation = prop.Rotation;
        state.InitialSupportPoint = center.Point; state.InitialSupportNormal = center.Normal;
        state.InitialSupportKnown = true;
        foreach (int id in state.InitialSurfaceIndices) surfaces.Remove(id);
        state.InitialSurfaceIndices = null; initialSupportState = null;
        Log("OBJETO_APOIO_INICIAL_MEDIDO", "Codigo=" + state.Spec.Codigo + "; Apoio=" + support.Spec.Codigo +
            "; Superficie=" + GroundVector(center.Point) + "; Normal=" + GroundVector(center.Normal) +
            "; BoundsMin=" + GroundVector(state.Min) + "; BoundsMax=" + GroundVector(state.Max) +
            "; Posicao=" + GroundVector(state.Position) + "; AlturaMaiorMesh=" + ContactNumber(highest) +
            "; VariacaoMesh=" + ContactNumber(highest - lowest) + "; Raios=centro+4cantos; AntesDaContagem=true; SemMoverAtor=true");
        return false; // Próximo prop/Reset só no Tick seguinte; não soma starts aos cinco resultados atuais.
    }

    private void BeginInitialSupport(State state)
    {
        if (unchecked((uint)(GTA.Game.GameTime - state.PreparedAt)) >= TimeoutMs)
            throw Error("apoio inicial não começou no preparo limitado de5s: " + state.Spec.Codigo);
        State support = RequireInitialSupport(state);
        GTA.Prop supportProp = owned.GetProp(support.Spec.Codigo);
        Vector(state.Rotation, 360f, "rotação inicial do prop apoiado");
        if (System.Math.Abs(state.Rotation.X) > 0.01f || System.Math.Abs(state.Rotation.Y) > 0.01f)
            throw Error("prop sobre apoio inicial exige bounds horizontais sem inclinação arbitrária");
        float localX = state.Spec.UsarCentroInicialMedido ? (support.Min.X + support.Max.X) * 0.5f : state.Spec.SuperficieInicialRelativa.X;
        float localY = state.Spec.UsarCentroInicialMedido ? (support.Min.Y + support.Max.Y) * 0.5f : state.Spec.SuperficieInicialRelativa.Y;
        GTA.Math.Vector3 center = supportProp.GetOffsetPosition(new GTA.Math.Vector3(localX, localY, 0f));
        float radians = state.Rotation.Z * (float)(System.Math.PI / 180.0);
        float cosine = (float)System.Math.Cos(radians), sine = (float)System.Math.Sin(radians);
        float middleX = (state.Min.X + state.Max.X) * 0.5f, middleY = (state.Min.Y + state.Max.Y) * 0.5f;
        state.InitialSupportPosition = center - new GTA.Math.Vector3(middleX * cosine - middleY * sine, middleX * sine + middleY * cosine, 0f);
        state.InitialSurfaceIndices = new int[5];
        state.InitialSurfaceIndices[0] = RequestSurfaceCore(support.Spec.Codigo, localX, localY);
        int index = 1;
        foreach (float x in new float[] { state.Min.X, state.Max.X })
            foreach (float y in new float[] { state.Min.Y, state.Max.Y })
            {
                GTA.Math.Vector3 corner = state.InitialSupportPosition + new GTA.Math.Vector3(x * cosine - y * sine, x * sine + y * cosine, 0f);
                GTA.Math.Vector3 local = supportProp.GetPositionOffset(corner);
                state.InitialSurfaceIndices[index++] = RequestSurfaceCore(support.Spec.Codigo, local.X, local.Y);
            }
        initialSupportState = state;
    }

    private State RequireInitialSupport(State state)
    {
        State support;
        if (!states.TryGetValue(state.Spec.ApoioInicial ?? "", out support) || support == state ||
            !Supports.Contains(support.Spec.Recurso) || !support.Ready || !support.BoundsKnown || !state.BoundsKnown || support.Owner != null)
            throw Error("apoio inicial exige superfície do suporte próprio medido, sem dono");
        GTA.Prop prop = owned.GetProp(support.Spec.Codigo);
        Vector(prop.Position, 30000f, "posição real do apoio inicial");
        Vector(prop.Rotation, 360f, "rotação real do apoio inicial");
        if (!prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled || prop.IsAttached() ||
            (prop.Position - support.Position).Length() > 0.01f || (prop.Rotation - support.Rotation).Length() > 0.01f ||
            System.Math.Abs(prop.Rotation.X) > 0.01f || System.Math.Abs(prop.Rotation.Y) > 0.01f)
            throw Error("apoio inicial mudou identidade, visibilidade, colisão ou transform desde a medição");
        if (state.InitialSupportKnown)
        {
            Vector(state.InitialSupportPoint, 30000f, "contato inicial em cache");
            Vector(state.InitialSupportNormal, 1.01f, "normal inicial em cache");
            if (state.InitialSupportNormal.Z < 0.98f) throw Error("contato inicial em cache não é horizontal");
        }
        return support;
    }

    private void PollPlacementSurfaces()
    {
        foreach (Timed timed in events)
        {
            if (timed.Event.Tipo != "colocar" || timed.PlacementKnown) continue;
            SurfaceProbe probe;
            if (!surfaces.TryGetValue(timed.Event.IndiceSuperficie, out probe)) throw Error("medição de colocação não foi solicitada antes da ação");
            if (!probe.Known) continue;
            timed.PlacementPoint = probe.Point; timed.PlacementNormal = probe.Normal;
            timed.PlacementKnown = true;
            RequirePlacementSupport(timed);
            Log("OBJETO_COLOCAR_SUPERFICIE", "Codigo=" + timed.State.Spec.Codigo + "; Apoio=" + timed.Event.Apoio +
                "; Point=" + GroundVector(probe.Point) + "; Normal=" + GroundVector(probe.Normal) + "; AntesDaContagem=true");
        }
    }

    private void RequirePlacementSupport(Timed timed)
    {
        State support;
        if (!timed.PlacementKnown || !states.TryGetValue(timed.Event.Apoio, out support) ||
            !Supports.Contains(support.Spec.Recurso) || support.Owner != null || !support.Ready)
            throw Error("colocação exige superfície medida do apoio próprio sem dono");
        GTA.Prop prop = owned.GetProp(support.Spec.Codigo);
        if (!prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled ||
            (prop.Position - support.Position).Length() > 0.01f || (prop.Rotation - support.Rotation).Length() > 0.01f)
            throw Error("apoio de colocação mudou visibilidade, colisão ou transform desde a medição");
        Vector(timed.PlacementPoint, 30000f, "ponto medido de colocação");
        Vector(timed.PlacementNormal, 1.01f, "normal medida de colocação");
        if (timed.PlacementNormal.Z < 0.98f) throw Error("colocação exige superfície real horizontal");
    }

    // Capacidade isolada: somente um prop proprio ja colocado/assentado em apoio medido.
    private bool TryPickup(Timed timed, int elapsedMs, GTA.Prop prop)
    {
        State state = timed.State; PropEvent item = timed.Event;
        if (state.Owner != null || !prop.IsVisible || prop.IsAttached() || !prop.IsPositionFrozen || !prop.IsCollisionEnabled)
            throw Error("pegar exige prop proprio visivel, livre e imovel sobre apoio medido");
        RequirePickupSupport(state, prop);
        foreach (State other in states.Values)
            if (other != state && other.Owner == item.Ator && other.Hand == item.Mao)
                return false; // Nenhum segundo prop na mesma mao, inclusive enquanto outro evento esta pendente.
        if (!GesturePlaying(item, elapsedMs)) return false;
        Actor actor = actors[item.Ator];
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actor.Handle, item.Dicionario, item.Clipe);
        if (!Finite(phase) || phase < 0.1f || phase > 0.95f) return false;
        if (!timed.PickupPhaseKnown)
        { timed.PickupPhaseKnown = true; timed.PickupPreviousPhase = phase; return false; }
        if (phase > timed.PickupPreviousPhase) timed.PickupProgressObserved = true;
        timed.PickupPreviousPhase = phase;
        GTA.PedBone bone = actor.Ped.Bones[HandBone(state, item.Mao)];
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("pegar exige osso real da mao do attachment");
        GTA.Math.Vector3 hand = bone.Position, local = prop.GetPositionOffset(hand);
        Vector(hand, 30000f, "mao real de pegar"); Vector(local, 30000f, "mao local de pegar");
        GTA.Math.Vector3 closest = new GTA.Math.Vector3(
            System.Math.Max(state.Min.X, System.Math.Min(state.Max.X, local.X)),
            System.Math.Max(state.Min.Y, System.Math.Min(state.Max.Y, local.Y)),
            System.Math.Max(state.Min.Z, System.Math.Min(state.Max.Z, local.Z)));
        GTA.Math.Vector3 contact = prop.GetOffsetPosition(closest);
        float handDistance = (hand - contact).Length(), pivotDistance = (bone.GetOffsetPosition(item.Offset) - prop.Position).Length();
        if (!Finite(handDistance) || !Finite(pivotDistance)) throw Error("distancias reais de pegar nao finitas");
        if (elapsedMs >= timed.NextPickupLog)
        {
            timed.NextPickupLog = elapsedMs + 500;
            Log("OBJETO_PEGAR_MEDICAO", "Codigo=" + state.Spec.Codigo + "; Ator=" + item.Ator + "; Mao=" + item.Mao +
                "; Bone=" + HandBone(state, item.Mao) + "; Phase=" + ContactNumber(phase) +
                "; ProgressObserved=" + timed.PickupProgressObserved + "; Hand=" + GroundVector(hand) +
                "; AabbContact=" + GroundVector(contact) + "; DistanceHandAabb=" + ContactNumber(handDistance) +
                "; DistancePivot=" + ContactNumber(pivotDistance) + "; Limite=0.35; ApoioProprioMedido=true; SemAttach=true");
        }
        if (!timed.PickupProgressObserved || handDistance > 0.35f || pivotDistance > 0.35f ||
            !CanAttach(state, item.Ator, item.Mao, item.Offset)) return false;
        Attach(state, item.Ator, item.Mao, item.Offset, item.Rotacao, true);
        Log("OBJETO_PEGADO", "Codigo=" + state.Spec.Codigo + "; Ator=" + item.Ator + "; Mao=" + item.Mao +
            "; Phase=" + ContactNumber(phase) + "; HandAabb=" + ContactNumber(handDistance) +
            "; Pivot=" + ContactNumber(pivotDistance) + "; ApoioProprioMedido=true; SemMoverAtor=true");
        return true;
    }

    private void RequirePickupSupport(State state, GTA.Prop prop)
    {
        if (!state.PickupAnchorKnown) throw Error("pegar exige apoio inicial medido ou ultimo colocar confirmado");
        if (state.PickupPlacement != null)
        {
            if (!state.PickupPlacement.Completed || state.PickupPlacement.State != state)
                throw Error("colocacao ainda nao foi confirmada para pegar");
            RequirePlacementSupport(state.PickupPlacement);
        }
        else
        {
            if (!state.InitialSupportKnown) throw Error("apoio inicial nao foi medido para pegar");
            RequireInitialSupport(state);
        }
        if ((prop.Position - state.PickupPosition).Length() > 0.01f || (prop.Rotation - state.PickupRotation).Length() > 0.01f)
            throw Error("prop apoiado mudou transform desde o contato proprio medido");
    }

    private sealed class HandInterval
    {
        internal string Code, Actor, Hand;
        internal int Start, End;
        internal bool Pickup;
    }
    // Cena com pegar exige uma prop por mao; cenas legadas sem pegar preservadas.
    public static void ValidatePickupHands(System.Collections.Generic.IEnumerable<PropSpec> specs, int durationMs)
    {
        var spans = new System.Collections.Generic.List<HandInterval>(); bool hasPickup = false;
        foreach (PropSpec spec in specs)
        {
            HandInterval current = System.String.IsNullOrEmpty(spec.AtorInicial) ? null :
                new HandInterval { Code = spec.Codigo, Actor = spec.AtorInicial, Hand = spec.MaoInicial, Start = 0 };
            foreach (PropEvent item in spec.Eventos)
            {
                bool acquire = item.Tipo == "anexar" || item.Tipo == "transferir" || item.Tipo == "pegar";
                bool release = acquire || item.Tipo == "colocar" || item.Tipo == "guardar" || item.Tipo == "soltar";
                if (release && current != null)
                { current.End = item.InicioMs + item.EsperaMaximaMs; spans.Add(current); current = null; }
                if (acquire)
                {
                    current = new HandInterval { Code = spec.Codigo, Actor = item.Ator, Hand = item.Mao,
                        Start = item.InicioMs, Pickup = item.Tipo == "pegar" };
                    hasPickup = hasPickup || current.Pickup;
                }
            }
            if (current != null) { current.End = durationMs; spans.Add(current); }
        }
        if (!hasPickup) return;
        for (int a = 0; a < spans.Count; a++) for (int b = a + 1; b < spans.Count; b++)
        {
            HandInterval x = spans[a], y = spans[b];
            if (x.Code != y.Code && x.Actor == y.Actor && x.Hand == y.Hand &&
                x.Start < y.End && y.Start < x.End) throw Error("pegar conflita com outro prop na mesma mao: " + x.Code + "/" + y.Code);
        }
    }

    private bool TryPlace(Timed timed, int elapsedMs, GTA.Prop prop)
    {
        State state = timed.State; PropEvent item = timed.Event;
        RequirePlacementSupport(timed);
        Actor actor = actors[item.Ator];
        if (state.Owner != item.Ator || state.Hand != item.Mao || !(pocket98Scope && state.Spec.Codigo == "PAPEL_BOLSO" ? IsObjectVisualSourceConfigured(state.Spec.Codigo) : prop.IsVisible) || !prop.IsAttachedTo(actor.Ped))
            throw Error("colocar exige objeto visível anexado à mão declarada do próprio dono");
        if (!GesturePlaying(item, elapsedMs)) return false;
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, actor.Handle, item.Dicionario, item.Clipe);
        GTA.PedBone bone = actor.Ped.Bones[HandBone(state, state.Hand)];
        if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("colocar exige osso real da mão");
        float handDistance = (bone.Position - timed.PlacementPoint).Length();
        float objectDistance = (prop.Position - timed.PlacementPoint).Length();
        if (!Finite(handDistance) || !Finite(objectDistance)) throw Error("distância de colocação não finita");
        if (elapsedMs >= timed.NextPlacementLog)
        {
            timed.NextPlacementLog = elapsedMs + 500;
            Log("OBJETO_COLOCAR_MEDICAO", "Codigo=" + state.Spec.Codigo + "; Apoio=" + item.Apoio + "; TempoMs=" + elapsedMs +
                "; Phase=" + phase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "; Mao=" + GroundVector(bone.Position) +
                "; Objeto=" + GroundVector(prop.Position) + "; Superficie=" + GroundVector(timed.PlacementPoint) +
                "; DistanciaMao=" + handDistance.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; DistanciaObjeto=" + objectDistance.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        // Fase fria/pendente não vira contato nem erro imediato: a mesma janela física limita a espera.
        if (!Finite(phase) || phase < 0.1f || phase > 0.95f || handDistance > 0.35f || objectDistance > 0.35f) return false;
        GTA.Math.Vector3 position = timed.PlacementPoint - new GTA.Math.Vector3(0f, 0f, state.Min.Z);
        float heading = prop.Heading;
        if (!Finite(heading) || (position - prop.Position).Length() > 0.35f) return false;
        GTA.Prop support = owned.GetProp(item.Apoio); State supportState = states[item.Apoio];
        float radians = heading * (float)(System.Math.PI / 180.0);
        float cosine = (float)System.Math.Cos(radians), sine = (float)System.Math.Sin(radians);
        foreach (float x in new float[] { state.Min.X, state.Max.X })
            foreach (float y in new float[] { state.Min.Y, state.Max.Y })
            {
                GTA.Math.Vector3 corner = position + new GTA.Math.Vector3(x * cosine - y * sine, x * sine + y * cosine, 0f);
                GTA.Math.Vector3 local = support.GetPositionOffset(corner);
                if (!Finite(local.X) || !Finite(local.Y) || local.X < supportState.Min.X || local.X > supportState.Max.X ||
                    local.Y < supportState.Min.Y || local.Y > supportState.Max.Y)
                    throw Error("objeto não cabe nos limites reais do apoio no ponto de contato");
            }
        if (pocket98Scope && state.Spec.Codigo == "PAPEL_BOLSO" && (pocket98 == null ||
            !pocket98.PlacementContact(bone.Position, timed.PlacementPoint, position, heading, support, supportState.Min, supportState.Max))) return false;
        prop.Detach(false, false);
        prop.PositionNoOffset = position; prop.Rotation = new GTA.Math.Vector3(0f, 0f, heading);
        prop.Velocity = GTA.Math.Vector3.Zero; prop.IsPositionFrozen = true; prop.IsCollisionEnabled = true;
        if (prop.IsAttached() || (prop.Position - position).Length() > 0.01f)
            throw Error("o motor não confirmou destacamento/contato do objeto colocado");
        state.Owner = null; state.Hand = null;
        state.PickupAnchorKnown = true; state.PickupPlacement = timed;
        state.PickupPosition = prop.Position; state.PickupRotation = prop.Rotation;
        Log("OBJETO_COLOCADO", "Codigo=" + state.Spec.Codigo + "; Apoio=" + item.Apoio + "; TempoMs=" + elapsedMs +
            "; MaoDistancia=" + handDistance.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; ObjetoDistancia=" + objectDistance.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; Posicao=" + GroundVector(position) + "; ContatoReal=true; SemMoverObjetoADistancia=true");
        return true;
    }

    public bool TryGetMeasuredSurface(int probeId, out GTA.Math.Vector3 point, out GTA.Math.Vector3 normal)
    {
        Ready(); SurfaceProbe probe;
        if (!surfaces.TryGetValue(probeId, out probe)) throw Error("medição desconhecida; solicite novamente depois do reset");
        point = probe.Point; normal = probe.Normal;
        return probe.Known;
    }

    // Telemetria das mesmas consultas; resultado pendente nao e contato e logging nunca muda guardas.
    private void LogSurfaceProbe(SurfaceProbe probe, GTA.Prop prop, string phase, int status,
        System.IntPtr hit, System.IntPtr point, System.IntPtr normal, System.IntPtr entity)
    {
        try
        {
            string hitText = "nao_consultado", entityText = "nao_consultado", pointText = "nao_consultado", normalText = "nao_consultado";
            if (hit != System.IntPtr.Zero)
            {
                hitText = System.Runtime.InteropServices.Marshal.ReadInt32(hit).ToString(System.Globalization.CultureInfo.InvariantCulture);
                entityText = System.Runtime.InteropServices.Marshal.ReadInt32(entity).ToString(System.Globalization.CultureInfo.InvariantCulture);
                float[] coordinates = new float[6], normals = new float[6];
                System.Runtime.InteropServices.Marshal.Copy(point, coordinates, 0, 6);
                System.Runtime.InteropServices.Marshal.Copy(normal, normals, 0, 6);
                pointText = GroundVector(new GTA.Math.Vector3(coordinates[0], coordinates[2], coordinates[4]));
                normalText = GroundVector(new GTA.Math.Vector3(normals[0], normals[2], normals[4]));
            }
            Log("APOIO_SUPERFICIE_PROBE", "Codigo=" + probe.State.Spec.Codigo + "; Modelo=" + probe.State.Spec.Modelo +
                "; Fase=" + phase + "; Ray=" + probe.Ray + "; SameCallSynchronous=" + probe.Synchronous + "; Flags=" + probe.TraceFlags + "; OptionFlags=" + probe.OptionFlags + "; LocalXY=(" + ContactNumber(probe.LocalX) + "," + ContactNumber(probe.LocalY) +
                "); Top=" + GroundVector(probe.Top) + "; Bottom=" + GroundVector(probe.Bottom) + "; Status=" + status +
                "; HitRaw=" + hitText + "; HitEntity=" + entityText + "; ExpectedEntity=" + prop.Handle +
                "; PointRaw=" + pointText + "; NormalRaw=" + normalText + "; PropPosition=" + GroundVector(prop.Position) +
                "; BoundsMin=" + GroundVector(probe.State.Min) + "; BoundsMax=" + GroundVector(probe.State.Max) +
                "; Visible=" + prop.IsVisible + "; Collision=" + prop.IsCollisionEnabled + "; Frozen=" + prop.IsPositionFrozen);
        }
        catch { } // Falha de diagnostico nao transforma hit/guard/timeout.
    }

    private void PollSurfaces()
    {
        foreach (SurfaceProbe probe in surfaces.Values)
        {
            if (probe.Known) continue;
            if (probe.OptionFlags == 0 && GTA.Game.GameTime == probe.RetryStartedAt) continue;
            GTA.Prop prop = owned.GetProp(probe.State.Spec.Codigo);
            if (!prop.IsPositionFrozen || (prop.Position - probe.State.Position).Length() > 0.01f)
                throw Error("apoio se moveu durante a medição de superfície");
            // A API191 le bool em oito bytes; o hit nativo e escalar e exige padding zerado.
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
                int status = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_SHAPE_TEST_RESULT, probe.Ray, hit, point, normal, entity);
                if (status == 1)
                {
                    if (unchecked((uint)(GTA.Game.GameTime - probe.StartedAt)) > 2000u)
                    {
                        LogSurfaceProbe(probe, prop, "timeout", status, hit, point, normal, entity);
                        throw Error("medição da superfície não terminou em2s");
                    }
                    continue;
                }
                LogSurfaceProbe(probe, prop, "resultado", status, hit, point, normal, entity);
                if (status == 2 && System.Runtime.InteropServices.Marshal.ReadInt32(hit) == 0 && probe.OptionFlags == 7)
                {
                    if (unchecked((uint)(GTA.Game.GameTime - probe.StartedAt)) > 2000u)
                    {
                        LogSurfaceProbe(probe, prop, "timeout", status, hit, point, normal, entity);
                        throw Error("medição da superfície não terminou em2s");
                    }
                    int retry = GTA.Native.Function.Call<int>(GTA.Native.Hash.START_SHAPE_TEST_LOS_PROBE,
                        probe.Top.X, probe.Top.Y, probe.Top.Z, probe.Bottom.X, probe.Bottom.Y, probe.Bottom.Z, 17, 0, 0);
                    if (retry == 0) throw Error("o motor recusou a medição de superfície do apoio");
                    probe.Ray = retry; probe.OptionFlags = 0; probe.TraceFlags = 17; probe.RetryStartedAt = GTA.Game.GameTime;
                    LogSurfaceProbe(probe, prop, "repeticao_material", -1, System.IntPtr.Zero, System.IntPtr.Zero, System.IntPtr.Zero, System.IntPtr.Zero);
                    continue;
                }
                if (status != 2 || System.Runtime.InteropServices.Marshal.ReadInt32(hit) == 0 || System.Runtime.InteropServices.Marshal.ReadInt32(entity) != prop.Handle)
                    throw Error("raio não atingiu superfície do próprio apoio; escolha outro XY real");
                float[] coordinates = new float[6], normals = new float[6];
                System.Runtime.InteropServices.Marshal.Copy(point, coordinates, 0, 6);
                System.Runtime.InteropServices.Marshal.Copy(normal, normals, 0, 6);
                GTA.Math.Vector3 p = new GTA.Math.Vector3(coordinates[0], coordinates[2], coordinates[4]);
                GTA.Math.Vector3 n = new GTA.Math.Vector3(normals[0], normals[2], normals[4]);
                Vector(p, 30000f, "superfície medida"); Vector(n, 1.01f, "normal medida");
                GTA.Math.Vector3 local = prop.GetPositionOffset(p);
                if (n.Z < 0.98f || local.Z < probe.State.Min.Z - 0.02f || local.Z > probe.State.Max.Z + 0.02f)
                    throw Error("superfície do apoio não é horizontal/utilizável no ponto candidato");
                probe.Point = p; probe.Normal = n; probe.Known = true;
                Log("APOIO_SUPERFICIE_MEDIDA", "Codigo=" + probe.State.Spec.Codigo + "; Z=" + p.Z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "; Semantica=assento/cama aguardam pose e teste visual");
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
    public void Release() { Cleanup(); }
    public void Cleanup()
    {
        if (authoredPaperC5 != null) throw Error("restore own authored proxy and unbind renderer before prop cleanup; retain forF10");
        if (paperMorph86 != null) paperMorph86.Release();
        owned.Cleanup(); // Se falhar, metadados são mantidos para repetir a limpeza.
        paperMorph86 = null; paperMorph86FinishResetAccepted = false;
        states.Clear(); actors.Clear(); events.Clear(); surfaces.Clear(); prepared = false; ready = false; lastElapsed = -1;
        initialSupportsPending = false; initialSupportState = null; sceneVehicles = null; restrictHandOwnership = false; authoredPaperC5Scope = false;
        officeBag84Prepared = officeBag84Verified = false; officeBag84Frame = -1; officeBag84NextLog = 0; officeBag84Offset = GTA.Math.Vector3.Zero;
        measuredTablesRecheckPending = measuredTablesRecheckDone = false; technicalBoxes = productionBoxes = productionVolumesLogged = false; dismissalHandoffs = sleepBenchC1 = officeCellBodyPreview84 = largeTableC8 = largeTableC8Logged = false; dismissalPartNumber = 0;
    }
    public void Dispose() { Cleanup(); owned.Dispose(); disposed = true; }

    private void Validate(PropSpec spec)
    {
        Identifier(spec.Codigo, "objeto");
        string expected;
        if (spec.Recurso == null || !Models.TryGetValue(spec.Recurso, out expected) || spec.Modelo != expected)
            throw Error("recurso/modelo precisa ser um dos pares do catálogo conferido");
        if (IsTechnicalOnlyBoxResource88(spec.Recurso) && !technicalBoxes)
            throw Error("alias de caixa do ensaio88 nao pode ser usado em producao ou em outro teste");
        if (IsProductionBoxResourceC2(spec.Recurso) && !productionBoxes)
            throw Error("alias wood01a producao exclusivo do opt-in EP07_C02; nao ensaio nem demais cenas");
        if ((spec.Recurso == "bolsa_sono84_preview" || spec.Recurso == "sacola_papel_sono84_preview") && (!officeCellBodyPreview84 || spec.Codigo != "SACOLA_CALIBRACAO84" || !OfficeBag84Spec(spec)))
            throw Error("compact handbag alias exclusive technical84 preview, never production/other scene");
        if (spec.Recurso == "papel_dobrado_autoral" && (!authoredPaperC5Scope || spec.Codigo != "PAPEL_IRACI" || spec.Modelo != "prop_amanda_note_01"))
            throw Error("authored folded visual alias exclusive exactC5/PAPEL_IRACI with real static_01 proxy; not folded native collider");
        if (spec.Recurso == "mesa_grande_c8_autor" && !largeTableC8)
            throw Error("large dining alias requires explicit EP15_C08 capability, never another scene");
        if (spec.Recurso == "banco_sono_c1_02" && !sleepBenchC1)
            throw Error("bench02 sleep alias requires explicit EP10_C01 capability, never another scene");
        Vector(spec.Deslocamento, 10f, "marca do objeto");
        if (System.Math.Abs(spec.Deslocamento.Z) > 2f || !Finite(spec.RotacaoZ) || System.Math.Abs(spec.RotacaoZ) > 180f)
            throw Error("marca admite±10m lateral/frente,±2m altura e rotação±180graus");
        if (spec.OrigemLinha < 1 || spec.OrigemLinha > 500000 || !System.Text.RegularExpressions.Regex.IsMatch(spec.OrigemSha256 ?? "", "\\A[0-9A-Fa-f]{64}\\z"))
            throw Error("recurso exige linha e SHA256 reais da PropList");
        bool support = Supports.Contains(spec.Recurso);
        if (support && (!spec.AssentarNoChao || !spec.VisivelInicial || !System.String.IsNullOrEmpty(spec.AtorInicial)))
            throw Error("apoio precisa visível, assentado no chão e sem attachment");
        if (!System.String.IsNullOrEmpty(spec.ApoioInicial))
        {
            Identifier(spec.ApoioInicial, "apoio inicial");
            Vector(spec.SuperficieInicialRelativa, 10f, "superfície inicial relativa");
            if (support || spec.AssentarNoChao || !spec.VisivelInicial || !System.String.IsNullOrEmpty(spec.AtorInicial) ||
                spec.SuperficieInicialRelativa.Z != 0f || (spec.UsarCentroInicialMedido && spec.SuperficieInicialRelativa.Length() != 0f))
                throw Error("apoio inicial só admite prop visível sem attachment/chão, superfície XY medida e outro suporte próprio");
        }
        else if (spec.SuperficieInicialRelativa.Length() != 0f || !spec.UsarCentroInicialMedido)
            throw Error("superfície inicial exige apoio inicial declarado");
        if (!System.String.IsNullOrEmpty(spec.AtorInicial)) Attachment(spec.AtorInicial, spec.MaoInicial, spec.OffsetInicial, spec.RotacaoInicial);
        else if (!System.String.IsNullOrEmpty(spec.MaoInicial) || spec.OffsetInicial.Length() != 0f || spec.RotacaoInicial.Length() != 0f)
            throw Error("ajuste/mão inicial exige ator inicial");
        if (spec.Eventos == null || spec.Eventos.Count > 32) throw Error("cada prop admite0–32eventos");
        int previous = -1; string owner = System.String.IsNullOrEmpty(spec.AtorInicial) ? null : spec.AtorInicial;
        string ownerHand = owner == null ? null : spec.MaoInicial;
        bool pickupSupportAvailable = !System.String.IsNullOrEmpty(spec.ApoioInicial), visible = spec.VisivelInicial;
        foreach (PropEvent item in spec.Eventos)
        {
            if (item == null || item.InicioMs < 0 || item.InicioMs > 180000 || item.InicioMs <= previous)
                throw Error("eventos do objeto precisam de tempos únicos crescentes0–180s");
            previous = item.InicioMs;
            if (item.EsperaMaximaMs < 0 || item.EsperaMaximaMs > 2000 ||
                (item.EsperaMaximaMs > 0 && item.Tipo != "transferir" && item.Tipo != "guardar" && item.Tipo != "colocar" && item.Tipo != "pegar"))
                throw Error("espera de contato só admite transferir/guardar/colocar com limite de2s");
            if (item.Tipo != "colocar" && (!System.String.IsNullOrEmpty(item.Apoio) || item.SuperficieRelativa.Length() != 0f))
                throw Error("apoio/superfície só se aplicam a colocar");
            if (item.EsperaMaximaMs > 0 && (item.GestoInicioMs > item.InicioMs || item.GestoInicioMs < 0 ||
                item.GestoFimMs < item.InicioMs + item.EsperaMaximaMs || item.GestoFimMs > 180000 ||
                !PosesSerieDiretor.IsOverlayClip(item.Dicionario, item.Clipe)))
                throw Error("janela de contato precisa estar inteiramente dentro do clipe candidato catalogado");
            if (item.Tipo == "anexar" || item.Tipo == "transferir")
            {
                if (support) throw Error("móvel de apoio não pode ser anexado à mão");
                Attachment(item.Ator, item.Mao, item.Offset, item.Rotacao);
                if (item.Tipo == "anexar" && owner != null) throw Error("anexar exige prop sem dono");
                if (item.Tipo == "transferir" && (owner == null || owner == item.Ator)) throw Error("transferir exige outro dono anterior");
                if (item.EsperaMaximaMs > 0 && (item.Doador != owner || !actors.ContainsKey(item.Doador) ||
                    item.Dicionario != "mp_common" || (item.Clipe != "givetake1_a" && item.Clipe != "givetake1_b") ||
                    (item.ClipeDoador != "givetake1_a" && item.ClipeDoador != "givetake1_b")))
                    throw Error("transferência com espera exige os dois gestos de entrega catalogados e dono real anterior");
                owner = item.Ator; ownerHand = item.Mao; pickupSupportAvailable = false;
            }
            else if (item.Tipo == "pegar")
            {
                Attachment(item.Ator, item.Mao, item.Offset, item.Rotacao);
                if (support || owner != null || !visible || !pickupSupportAvailable || item.EsperaMaximaMs <= 0 ||
                    item.Dicionario != "mp_common" || (item.Clipe != "givetake1_a" && item.Clipe != "givetake1_b") ||
                    !System.String.IsNullOrEmpty(item.Doador) || !System.String.IsNullOrEmpty(item.ClipeDoador))
                    throw Error("pegar exige prop visivel sem dono em apoio proprio medido e gesto givetake");
                owner = item.Ator; ownerHand = item.Mao; pickupSupportAvailable = false;
            }
            else if (item.Tipo == "colocar")
            {
                Identifier(item.Apoio, "apoio de colocação");
                Vector(item.SuperficieRelativa, 10f, "superfície relativa de colocação");
                if (support || owner == null || item.Ator != owner || item.EsperaMaximaMs <= 0 ||
                    (item.Mao != "direita" && item.Mao != "esquerda") || item.Mao != ownerHand || item.SuperficieRelativa.Z != 0f ||
                    (item.UsarCentroMedido && item.SuperficieRelativa.Length() != 0f) ||
                    item.Offset.Length() != 0f || item.Rotacao.Length() != 0f ||
                    item.Dicionario != "mp_common" || (item.Clipe != "givetake1_a" && item.Clipe != "givetake1_b"))
                    throw Error("colocar exige dono/mão, gesto de entrega real e superfície relativa medida, sem offsets arbitrários");
                owner = null; ownerHand = null; pickupSupportAvailable = true;
            }
            else if (item.Tipo == "guardar")
            {
                if (spec.Recurso != "envelope" || support || item.Ator != owner || item.EsperaMaximaMs <= 0 ||
                    item.Dicionario != "mp_common_miss" || item.Clipe != "put_away_coke" ||
                    !System.String.IsNullOrEmpty(item.Mao) || item.Offset.Length() != 0f || item.Rotacao.Length() != 0f)
                    throw Error("guardar exige envelope do dono, gesto put_away_coke e janela física limitada, sem mão/ajustes novos");
                owner = null; ownerHand = null; pickupSupportAvailable = false;
            }
            else
            {
                if (item.Tipo != "soltar" && item.Tipo != "ocultar" && item.Tipo != "mostrar") throw Error("tipo de evento inválido");
                if (!System.String.IsNullOrEmpty(item.Ator) || !System.String.IsNullOrEmpty(item.Mao) || item.Offset.Length() != 0f || item.Rotacao.Length() != 0f)
                    throw Error("ator/mão/offset/rotação só se aplicam a anexar/transferir");
                if (item.Tipo == "soltar") { if (owner == null) throw Error("soltar exige dono anterior"); owner = null; ownerHand = null; pickupSupportAvailable = false; }
                if (item.Tipo == "ocultar") visible = false;
                if (item.Tipo == "mostrar") visible = true;
            }
        }
    }

    private void Attachment(string actor, string hand, GTA.Math.Vector3 offset, GTA.Math.Vector3 rotation)
    {
        if (actor == null || !actors.ContainsKey(actor) || (hand != "direita" && hand != "esquerda")) throw Error("attachment exige ator próprio e mão direita/esquerda");
        Vector(offset, 0.5f, "ajuste da mão"); if (offset.Length() > 0.5f) throw Error("ajuste deve ficar até0,5m da mão");
        Vector(rotation, 180f, "rotação da mão");
    }

    public static string GetCatalogModel(string recurso)
    {
        string model;
        if (recurso == null || !Models.TryGetValue(recurso, out model)) throw Error("código de recurso fora do catálogo conferido");
        return model;
    }

    private System.Collections.Generic.IEnumerable<PropSpec> GetSpecs()
    { foreach (State state in states.Values) yield return state.Spec; }

    // Leitura pura de arquivos: o parser pode conferir evidência antes de limpar a cena atual.
    public static void VerifySourceEvidence(System.Collections.Generic.IEnumerable<PropSpec> specs)
    {
        if (specs == null) throw Error("lista de evidências ausente");
        var sources = new System.Collections.Generic.Dictionary<string, Source>(System.StringComparer.OrdinalIgnoreCase);
        var codes = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        int count = 0;
        foreach (PropSpec spec in specs)
        {
            if (spec == null || ++count > 8) throw Error("evidências admitem0–8props não nulos");
            Identifier(spec.Codigo, "objeto");
            if (!codes.Add(spec.Codigo) || GetCatalogModel(spec.Recurso) != spec.Modelo) throw Error("objeto repetido ou recurso/modelo divergente");
            if (spec.OrigemLinha < 1 || spec.OrigemLinha > 500000 || !System.Text.RegularExpressions.Regex.IsMatch(spec.OrigemSha256 ?? "", "\\A[0-9A-Fa-f]{64}\\z"))
                throw Error("evidência exige linha e SHA256 válidos");
            if (System.String.IsNullOrEmpty(spec.OrigemArquivo) || !System.IO.Path.IsPathRooted(spec.OrigemArquivo)) throw Error("origem precisa ser arquivo absoluto local");
            string path = System.IO.Path.GetFullPath(spec.OrigemArquivo);
            if (path.StartsWith("\\\\", System.StringComparison.Ordinal) || path.Length < 3 || path.Substring(2).IndexOf(':') >= 0 ||
                !System.String.Equals(System.IO.Path.GetFileName(path), "PropList.txt", System.StringComparison.OrdinalIgnoreCase) ||
                !System.String.Equals(new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(path)).Name, "menyooStuff", System.StringComparison.OrdinalIgnoreCase))
                throw Error("origem precisa ser PropList.txt instalada em menyooStuff, sem rede/stream alternativo");
            Source source;
            if (!sources.TryGetValue(path, out source)) { source = new Source { Hash = spec.OrigemSha256.ToUpperInvariant() }; sources.Add(path, source); }
            if (source.Hash != spec.OrigemSha256.ToUpperInvariant()) throw Error("SHA conflitante para a mesma origem");
            string old;
            if (source.Lines.TryGetValue(spec.OrigemLinha, out old) && old != spec.Modelo) throw Error("linha de origem conflitante");
            source.Lines[spec.OrigemLinha] = spec.Modelo;
        }
        foreach (var entry in sources)
        {
            NoLinks(entry.Key);
            using (var stream = new System.IO.FileStream(entry.Key, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > 64L * 1024 * 1024) throw Error("PropList excede64MB");
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    if (System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != entry.Value.Hash) throw Error("SHA256 da PropList diverge da evidência");
                stream.Position = 0; int number = 0, found = 0;
                using (var reader = new System.IO.StreamReader(stream, new System.Text.UTF8Encoding(false, true), true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    { number++; string expected; if (entry.Value.Lines.TryGetValue(number, out expected)) { if (line.Trim() != expected) throw Error("modelo diverge da linha real da PropList"); found++; } }
                }
                if (found != entry.Value.Lines.Count) throw Error("linha de origem ausente");
            }
            NoLinks(entry.Key);
        }
    }

    private static PropSpec Copy(PropSpec source)
    {
        var result = new PropSpec { Codigo = source.Codigo, Recurso = source.Recurso, Modelo = source.Modelo,
            OrigemArquivo = source.OrigemArquivo, OrigemSha256 = source.OrigemSha256, OrigemLinha = source.OrigemLinha,
            Deslocamento = source.Deslocamento, RotacaoZ = source.RotacaoZ, AssentarNoChao = source.AssentarNoChao,
            VisivelInicial = source.VisivelInicial, AtorInicial = source.AtorInicial, MaoInicial = source.MaoInicial,
            OffsetInicial = source.OffsetInicial, RotacaoInicial = source.RotacaoInicial, ApoioInicial = source.ApoioInicial,
            SuperficieInicialRelativa = source.SuperficieInicialRelativa, UsarCentroInicialMedido = source.UsarCentroInicialMedido, Morph86 = source.Morph86 };
        if (source.Eventos == null) result.Eventos = null;
        else foreach (PropEvent item in source.Eventos) result.Eventos.Add(item == null ? null : new PropEvent {
            Tipo = item.Tipo, Ator = item.Ator, Mao = item.Mao, InicioMs = item.InicioMs, Offset = item.Offset, Rotacao = item.Rotacao,
            EsperaMaximaMs = item.EsperaMaximaMs, Dicionario = item.Dicionario, Clipe = item.Clipe, GestoInicioMs = item.GestoInicioMs, GestoFimMs = item.GestoFimMs,
            Doador = item.Doador, ClipeDoador = item.ClipeDoador, Apoio = item.Apoio, SuperficieRelativa = item.SuperficieRelativa, UsarCentroMedido = item.UsarCentroMedido });
        return result;
    }
    private GTA.Math.Vector3 ToWorld(GTA.Math.Vector3 p) { return anchor + right * p.X + forward * p.Y + new GTA.Math.Vector3(0f, 0f, p.Z); }
    private float Heading() { return (float)(System.Math.Atan2(-forward.X, forward.Y) * 180.0 / System.Math.PI); }
    private void RequireActors()
    {
        foreach (Actor a in actors.Values)
        {
            RequireActor(a.Ped);
            if (a.Ped.Handle != a.Handle || a.Ped.Model.Hash != a.Model || a.Ped.MemoryAddress != a.Address)
                throw Error("identidade do ator mudou: " + a.Id);
        }
    }
    private void RequireActorsDuringUpdate(int elapsedMs)
    {
        foreach (Actor a in actors.Values)
        {
            GTA.Ped ped = a.Ped;
            if (!System.Object.ReferenceEquals(ped, null) && ped.Exists() && ped.IsInVehicle())
            {
                GTA.Ped player = GTA.Game.Player.Character;
                if (ped.IsDead || (!System.Object.ReferenceEquals(player, null) && player.Handle == ped.Handle) ||
                    sceneVehicles == null || !sceneVehicles.OwnsVehicleContaining(ped))
                    throw Error("ator em veiculo exige identidade e veiculo do helper proprio: " + a.Id);
                foreach (Timed timed in events)
                {
                    PropEvent item = timed.Event;
                    if (timed.Completed || (item.Ator != a.Id && item.Doador != a.Id && timed.State.Owner != a.Id)) continue;
                    int start = item.EsperaMaximaMs > 0 ? System.Math.Min(item.InicioMs, item.GestoInicioMs) : item.InicioMs;
                    if (timed.Started || elapsedMs >= start)
                        throw Error("evento de objeto exige ator a pe antes do gesto/contato: " + a.Id + "; Objeto=" + timed.State.Spec.Codigo);
                }
            }
            else RequireActor(ped);
            if (ped.Handle != a.Handle || ped.Model.Hash != a.Model || ped.MemoryAddress != a.Address)
                throw Error("identidade do ator mudou: " + a.Id);
        }
    }
    private static void RequireActor(GTA.Ped ped)
    {
        GTA.Ped player = GTA.Game.Player.Character;
        if (System.Object.ReferenceEquals(ped, null) || !ped.Exists() || ped.IsDead || ped.IsInVehicle() ||
            (!System.Object.ReferenceEquals(player, null) && player.Handle == ped.Handle)) throw Error("somente atores próprios vivos a pé, sem o jogador");
    }
    private void RequireProps()
    {
        foreach (State state in states.Values)
        {
            GTA.Prop prop = owned.GetProp(state.Spec.Codigo);
            if (state.Owner != null && !prop.IsAttachedTo(actors[state.Owner].Ped)) throw Error("objeto perdeu attachment ao dono previsto");
            if (state.Owner == null && prop.IsAttached()) throw Error("objeto sem dono foi anexado externamente");
        }
        if (sleepBenchC1) owned.RequireSleepBenchGeometry("BANCO_PRAIA");
    }
    private static void NoLinks(string path)
    {
        string current = path;
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) && (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw Error("origem não pode usar links/junctions");
            current = System.IO.Path.GetDirectoryName(current);
        }
    }
    private static void Bounds(GTA.Math.Vector3 min, GTA.Math.Vector3 max)
    {
        Vector(min, 10f, "limite mínimo do modelo"); Vector(max, 10f, "limite máximo do modelo");
        if (max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z) throw Error("model bounds reais inválidos");
    }
    private static void Identifier(string s, string field)
    { if (!System.Text.RegularExpressions.Regex.IsMatch(s ?? "", "\\A[A-Z][A-Z0-9_]{0,47}\\z")) throw Error("código maiúsculo inválido: " + field); }
    private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
    private static void Vector(GTA.Math.Vector3 p, float bound, string field)
    { if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z) || System.Math.Abs(p.X) > bound || System.Math.Abs(p.Y) > bound || System.Math.Abs(p.Z) > bound) throw Error("vetor fora dos limites: " + field); }
    private void Active() { if (disposed) throw Error("helper encerrado"); }
    private void Ready() { Active(); if (!IsReady) throw Error("aguarde preparo dos objetos"); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Objetos da série: " + text); }
    private void Log(string code, string message) { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
}

// Diagnostico de morfologia: somente PREPARACAO86 de um prop proprio nota_s.
// Nao cria Script, corpo, prop, camera, captura, actor task ou nova referencia de producao.
public sealed class MorfologiaPapel86Diretor
{
    public sealed class Spec
    {
        public readonly string Id, ScenePath, SceneSha, SidecarPath, SidecarSha, AnimSource;
        public readonly int Segment;
        public readonly float Phase;
        public readonly bool Pamphlet;
        public string ModelName { get { return Pamphlet ? "p_cs_pamphlet_01_s" : "p_amanda_note_01_s"; } }
        public Spec(string id, string scenePath, string sceneSha, string sidecarPath, string sidecarSha,
                    string animSource, int segment, float phase, bool pamphlet = false)
        {
            Id = id; ScenePath = scenePath; SceneSha = sceneSha; SidecarPath = sidecarPath;
            SidecarSha = sidecarSha; AnimSource = animSource; Segment = segment; Phase = phase; Pamphlet = pamphlet;
        }
    }

    private readonly System.Action<string, string> log;
    private readonly Spec spec;
    private readonly GTA.Prop paper;
    private readonly GTA.Ped actor;
    private readonly int paperHandle, paperModel, actorHandle, actorModel;
    private readonly System.IntPtr paperAddress, actorAddress;
    private readonly GTA.Math.Vector3 paperStart, actorStart;
    private readonly float actorHeading;
    private readonly string dictionary, clip;
    private readonly GTA.CrClipAsset asset;
    private readonly GTA.CrClipDictionary clipDictionary;
    private int startedAt, heldAt, stableAt, samples;
    private bool requested, played, held, ready, released;
    private float duration, lastPhase;
    private const int TimeoutMs = 5000;
    private const string AnimSourceSha = "2C537060BD97FCF0DACA407DB1436A5D54E4B8DD5274DBC9EF0D8466EF6F21E4";
    private static readonly int[] AnimLines = { 24838, 24843, 24846, 24850, 24854, 24858, 24860 };
    private static readonly int[] PamphletAnimLines = { 43912, 43922, 43929, 43935 };
    private int AnimLine { get { return (spec.Pamphlet ? PamphletAnimLines : AnimLines)[spec.Segment]; } }

    internal static bool IsLiteralPaper(string resource, string model)
    {
        return (resource == "papel_preview86_nota_s" && model == "p_amanda_note_01_s") ||
            (resource == "papel_preview86_pamphlet_s" && model == "p_cs_pamphlet_01_s");
    }

    public bool IsReady { get { return ready && !released; } }

    public MorfologiaPapel86Diretor(System.Action<string, string> logger, Spec request, GTA.Prop ownPaper, GTA.Ped ownActor)
    {
        log = logger; spec = request; paper = ownPaper; actor = ownActor;
        ValidateSpec(spec);
        dictionary = (spec.Pamphlet ? "ep_1_rcm_concat-" : "family_5_mcs_5_p4-") + spec.Segment.ToString(System.Globalization.CultureInfo.InvariantCulture);
        clip = spec.ModelName + "-" + spec.Segment.ToString(System.Globalization.CultureInfo.InvariantCulture);
        asset = new GTA.CrClipAsset(dictionary, clip);
        clipDictionary = new GTA.CrClipDictionary(dictionary);
        if (System.Object.ReferenceEquals(paper, null) || !paper.Exists() ||
            System.Object.ReferenceEquals(actor, null) || !actor.Exists() || actor.IsDead || actor.IsInVehicle() ||
            paper.Model.Hash != new GTA.Model(spec.ModelName).Hash ||
            actor.Model.Hash != new GTA.Model("mp_f_freemode_01").Hash || !paper.IsAttachedTo(actor) || !paper.IsVisible)
            throw Error("identidade/attachment proprio nota_s e Djanira feminina nao comprovados");
        GTA.Ped player = GTA.Game.Player.Character;
        if (!System.Object.ReferenceEquals(player, null) && actor.Handle == player.Handle)
            throw Error("jogador nunca e ator do ensaio");
        paperHandle = paper.Handle; paperModel = paper.Model.Hash; paperAddress = paper.MemoryAddress;
        actorHandle = actor.Handle; actorModel = actor.Model.Hash; actorAddress = actor.MemoryAddress;
        if (paperAddress == System.IntPtr.Zero || actorAddress == System.IntPtr.Zero)
            throw Error("endereco proprio ausente");
        paperStart = paper.Position; actorStart = actor.Position; actorHeading = actor.Heading;
        Vector(paperStart); Vector(actorStart); Finite(actorHeading);
        VerifyAnimSource(); RequireFingerprints();
        startedAt = GTA.Game.GameTime;
        Log("PAPER_MORPH86_BEGIN", Identity() + "; Method=Entity.PlayAnimation; Attached=True; ActorTask=False;" +
            " ActorSetter=False; PropCreate=False; PropTransformSetter=False; ColliderProved=False; ProductionApproval=False; PreparationOnly=True");
    }

    public void Poll()
    {
        if (released) throw Error("ensaio ja encerrado");
        RequireFingerprints(); RequireOwnership();
        if (Elapsed(startedAt) > TimeoutMs) throw Error("preparo/binding/pausa nao comprovado em5s");
        if (!requested)
        {
            if (!clipDictionary.Exists) throw Error("dicionario original ausente no Legacy");
            clipDictionary.Request(); requested = true;
            Log("PAPER_MORPH86_DICT", Identity() + "; Exists=True; Requested=True; Loaded=" + clipDictionary.IsLoaded);
            return;
        }
        if (!clipDictionary.IsLoaded) return;
        if (!played)
        {
            duration = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ANIM_DURATION, dictionary, clip);
            Finite(duration);
            if (duration <= 0f || duration > 120f) throw Error("duracao original ausente/nao finita/fora do intervalo");
            bool accepted = paper.PlayAnimation(asset, GTA.AnimationBlendDelta.NormalBlendIn,
                false, true, false, spec.Phase, GTA.AnimationFlags.None);
            Log("PAPER_MORPH86_PLAY", Identity() + "; DurationSeconds=" + Number(duration) + "; NativeAccepted=" + accepted +
                "; Attached=" + paper.IsAttachedTo(actor) + "; DriveToPose=False; Loop=False; HoldLastFrame=True; PlayCalledOnce=True; BodyAnim=False");
            if (!accepted) throw Error("PLAY_ENTITY_ANIM recusou o prop anexado; sem detach/reancoragem alternativa automatica");
            played = true; return; // O getter exato vem em outro Tick.
        }
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, paperHandle, dictionary, clip, 3);
        if (!playing)
        {
            Log("PAPER_MORPH86_BIND_PENDING", Identity() + "; ExactPlaying=False; Held=" + held + "; ElapsedMs=" + Elapsed(startedAt));
            if (held) throw Error("track propria deixou de tocar apos pausa");
            return;
        }
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, paperHandle, dictionary, clip);
        Phase(phase);
        if (!held)
        {
            paper.SetAnimationSpeed(asset, 0f);
            paper.SetAnimationCurrentTime(asset, spec.Phase);
            held = true; heldAt = GTA.Game.GameTime; stableAt = 0; samples = 0;
            Log("PAPER_MORPH86_HOLD_ONCE", Identity() + "; ExactPlaying=True; PhaseBefore=" + Number(phase) +
                "; RequestedPhase=" + Number(spec.Phase) + "; SpeedRequested=0; SetSpeedCalledOnce=True; SetPhaseCalledOnce=True; NoRateGetter=True");
            return; // Nao confundir o pedido com a fase realmente lida.
        }
        LogGeometry(phase);
        if (System.Math.Abs(phase - spec.Phase) > 0.002f)
        {
            stableAt = 0; samples = 0;
            if (Elapsed(heldAt) > 1000) throw Error("fase pausada nao atingiu pedido em1s; sem reaplicacao de setters");
            return;
        }
        if (samples == 0) { stableAt = GTA.Game.GameTime; lastPhase = phase; }
        if (System.Math.Abs(phase - lastPhase) > 0.001f)
            throw Error("fase pausada mudou entre getters reais; nenhum rate getter foi simulado");
        lastPhase = phase; samples++;
        if (samples < 4 || Elapsed(stableAt) < 250) return;
        ready = true;
        Log("PAPER_MORPH86_READY", Identity() + "; ExactPlaying=True; Phase=" + Number(phase) +
            "; StableSamples=" + samples + "; StableMs=" + Elapsed(stableAt) + "; PauseProvedByStablePhase=True; RateGetterAvailable=False;" +
            " BonesObserved=True; RestModelBoundsOnly=True; ColliderProved=False; FoldApproved=False; ProductionApproval=False; PreparationOnly=True; NoAction=True");
    }

    // FinishLoading chama Reset uma vez. Preservar a animacao propria ja pausada.
    public void RequireHeld(string reason)
    {
        if (!IsReady) throw Error("morfologia ainda nao pronta");
        RequireFingerprints(); RequireOwnership();
        bool playing = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_ENTITY_PLAYING_ANIM, paperHandle, dictionary, clip, 3);
        float phase = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_ANIM_CURRENT_TIME, paperHandle, dictionary, clip);
        Phase(phase);
        if (!playing || System.Math.Abs(phase - spec.Phase) > 0.002f)
            throw Error("binding/phase pausados mudaram antes da camera");
        LogGeometry(phase);
        Log("PAPER_MORPH86_HELD_CHECK", Identity() + "; Reason=" + reason + "; ExactPlaying=True; Phase=" + Number(phase) + "; NoReset=True");
    }

    public void Release()
    {
        if (released) return;
        // So a animacao do handle/model/address proprio. owned.Cleanup apaga o mesmo prop.
        if (played && !System.Object.ReferenceEquals(paper, null) && paper.Exists() &&
            paper.Handle == paperHandle && paper.Model.Hash == paperModel && paper.MemoryAddress == paperAddress)
        {
            bool stopped = paper.StopAnimation(asset, GTA.AnimationBlendDelta.NormalBlendOut);
            Log("PAPER_MORPH86_STOP", Identity() + "; NativeStopped=" + stopped + "; OwnOnly=True; BodyStop=False");
        }
        if (requested) clipDictionary.MarkAsNoLongerNeeded();
        released = true; ready = false;
    }

    private void RequireOwnership()
    {
        if (!paper.Exists() || paper.Handle != paperHandle || paper.Model.Hash != paperModel || paper.MemoryAddress != paperAddress ||
            !actor.Exists() || actor.Handle != actorHandle || actor.Model.Hash != actorModel || actor.MemoryAddress != actorAddress ||
            actor.IsDead || actor.IsInVehicle() || !paper.IsAttachedTo(actor) || !paper.IsVisible || !paper.Model.IsLoaded || !paper.HasDrawable)
            throw Error("identidade/attachment/drawable carregado mudou");
        Vector(paper.Position); Vector(actor.Position); Finite(actor.Heading);
        float actorDelta = (actor.Position - actorStart).Length();
        float paperDelta = (paper.Position - paperStart).Length();
        float headingDelta = System.Math.Abs((actor.Heading - actorHeading + 540f) % 360f - 180f);
        if (actorDelta > 0.02f || headingDelta > 0.5f || paperDelta > 0.05f)
        {
            Log("PAPER_MORPH86_ROOT_DISTANT", Identity() + "; ActorDelta=" + Number(actorDelta) + "; PaperDelta=" + Number(paperDelta) +
                "; HeadingDelta=" + Number(headingDelta) + "; Root=" + V(paper.Position) + "; Start=" + V(paperStart) + "; NoReanchor=True");
            throw Error("movimento de root/ator excedeu preparo estatico; sem reancoragem por frame");
        }
    }

    private void LogGeometry(float phase)
    {
        if (!paper.HasSkeleton || paper.Bones.Count < 1 || paper.Bones.Count > 64)
            throw Error("track nao produziu skeleton/bones reais validos");
        GTA.Math.Vector3 min, max; paper.Model.GetDimensions(out min, out max); Vector(min); Vector(max);
        GTA.Math.Vector3 propRotation = paper.Rotation, ownerPosition = actor.Position;
        Vector(propRotation); Vector(ownerPosition); Finite(actor.Heading);
        if (max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z) throw Error("rest model bounds invalidos");
        Log("PAPER_MORPH86_SAMPLE", Identity() + "; Phase=" + Number(phase) + "; ExactPlaying=True; DurationSeconds=" + Number(duration) +
            "; Root=" + V(paper.Position) + "; PropRotation=" + V(propRotation) + "; ActorRoot=" + V(ownerPosition) + "; ActorHeading=" + Number(actor.Heading) +
            "; ModelBoundsMin=" + V(min) + "; ModelBoundsMax=" + V(max) + "; BoundsKind=RestModelNotSkinnedMesh;" +
            " BoneCount=" + paper.Bones.Count + "; HasSkeleton=True; HasAnimDirector=" + paper.HasAnimationDirector + "; ModelCollisionLoaded=" + paper.Model.IsCollisionLoaded +
            "; HasPhysics=" + paper.HasPhysics + "; ColliderProved=False; FoldApproved=False; ProductionApproval=False; PreparationOnly=True");
        for (int index = 0; index < System.Math.Min(paper.Bones.Count, 24); index++)
        {
            GTA.EntityBone bone = paper.Bones[index];
            if (System.Object.ReferenceEquals(bone, null) || !bone.IsValid) throw Error("bone proprio invalido");
            GTA.Math.Vector3 position = bone.Position, relative = bone.RelativePosition, rotation = bone.RelativeRotation, worldRotation = bone.Rotation;
            Vector(position); Vector(relative); Vector(rotation); Vector(worldRotation);
            float distance = (position - paper.Position).Length(); Finite(distance);
            Log("PAPER_MORPH86_BONE", Identity() + "; Phase=" + Number(phase) + "; Index=" + index + "; Name=" + bone.Name +
                "; World=" + V(position) + "; WorldRotation=" + V(worldRotation) + "; Relative=" + V(relative) + "; RelativeRotation=" + V(rotation) + "; DistanceRoot=" + Number(distance) +
                "; GetterOnly=True; BoneTransformSetter=False; SampleCap=24; FoldApproved=False");
            if (distance > 2f || relative.Length() > 2f)
                throw Error("bone/track distante do prop proprio; sem reancorar mesh ou reduzir escala");
        }
    }

    private void VerifyAnimSource()
    {
        if (System.IO.Path.GetFileName(spec.AnimSource) != "PedAnimList.txt" ||
            new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(spec.AnimSource)).Name != "menyooStuff")
            throw Error("origem deve ser PedAnimList.txt real instalada");
        if (FileHash(spec.AnimSource, 64L * 1024 * 1024) != AnimSourceSha) throw Error("hash da animlist real diverge da fonte primaria");
        int line = 0; bool matched = false;
        using (var reader = new System.IO.StreamReader(spec.AnimSource, new System.Text.UTF8Encoding(false, true), true))
        {
            string actual;
            while ((actual = reader.ReadLine()) != null)
                if (++line == AnimLine) { matched = actual.Trim() == dictionary + " " + clip; break; }
        }
        if (!matched) throw Error("track literal diverge da linha primaria Menyoo");
        Log("PAPER_MORPH86_SOURCE", Identity() + "; AnimFile=" + spec.AnimSource + "; AnimSHA256=" + AnimSourceSha + "; AnimLine=" + AnimLine);
    }

    private void RequireFingerprints()
    {
        if (FileHash(spec.ScenePath, 1048576) != spec.SceneSha || FileHash(spec.SidecarPath, 4096) != spec.SidecarSha)
            throw Error("fonte86/sidecar mudaram desde parse: remonte por novo F6");
    }

    private static void ValidateSpec(Spec request)
    {
        if (request == null || !System.Text.RegularExpressions.Regex.IsMatch(request.Id ?? "", "\\A[0-9a-f]{32}\\z") ||
            request.Segment < 0 || request.Segment > (request.Pamphlet ? 3 : 6) || System.Single.IsNaN(request.Phase) || System.Single.IsInfinity(request.Phase) || request.Phase < 0f || request.Phase > 1f ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.SceneSha ?? "", "\\A[0-9A-F]{64}\\z") ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.SidecarSha ?? "", "\\A[0-9A-F]{64}\\z") ||
            System.IO.Path.GetFileName(request.ScenePath) != "teste_fila_86.json" || System.IO.Path.GetFileName(request.SidecarPath) != "trial_morph86.json")
            throw Error("spec86 literal/hash/segmento/fase invalidos");
    }

    private static string FileHash(string path, long maximum)
    {
        string full = System.IO.Path.GetFullPath(path);
        if (!System.IO.Path.IsPathRooted(path) || full.StartsWith("\\\\", System.StringComparison.Ordinal) || full.Substring(2).IndexOf(':') >= 0)
            throw Error("fonte local absoluta sem rede/stream exigida");
        string current = full;
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0) throw Error("links/junctions nao permitidos no ensaio");
            current = System.IO.Path.GetDirectoryName(current);
        }
        using (var stream = new System.IO.FileStream(full, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
        using (var hash = System.Security.Cryptography.SHA256.Create())
        {
            if (stream.Length < 1 || stream.Length > maximum) throw Error("tamanho da fonte/sidecar fora do limite");
            return System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }
    }

    internal static string EvidenceHash(string path, long maximum) { return FileHash(path, maximum); }

    private string Identity()
    {
        return "Id=" + spec.Id + "; Scene=teste_fila_86; OwnCode=PAPEL_PROVA; Model=" + spec.ModelName + "; Handle=" + paperHandle +
            "; Segment=" + spec.Segment + "; Dictionary=" + dictionary + "; Clip=" + clip + "; RequestedPhase=" + Number(spec.Phase) +
            "; SceneSHA256=" + spec.SceneSha + "; SidecarSHA256=" + spec.SidecarSha;
    }
    private static uint Elapsed(int time) { return unchecked((uint)(GTA.Game.GameTime - time)); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    private static string V(GTA.Math.Vector3 v) { return "(" + Number(v.X) + "," + Number(v.Y) + "," + Number(v.Z) + ")"; }
    private static void Finite(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw Error("getter nao finito"); }
    private static void Phase(float phase) { Finite(phase); if (phase < 0f || phase > 1f) throw Error("fase fora de0..1"); }
    private static void Vector(GTA.Math.Vector3 v) { Finite(v.X); Finite(v.Y); Finite(v.Z); if (System.Math.Abs(v.X) > 30000f || System.Math.Abs(v.Y) > 30000f || System.Math.Abs(v.Z) > 30000f) throw Error("vetor nativo fora dos limites"); }
    private static System.InvalidOperationException Error(string text) { return new System.InvalidOperationException("Morfologia86: " + text); }
    private void Log(string code, string message) { if (log != null) { try { log(code, message); } catch (System.Exception) { } } }
}

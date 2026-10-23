// Concatenar à fonte .3.cs: sem using, sem nova classe Script e sem tarefas de condução.
// Modelos são conferidos no jogo; nenhum objeto/veículo do mapa é adotado pelo helper.
public sealed class ObjetosDiretor : System.IDisposable
{
    private sealed class MesaProbe
    {
        public GTA.ShapeTestHandle Handle;
        public GTA.Math.Vector3 From, To, Point;
        public bool Started, Done, OwnSurface;
    }

    private sealed class Recurso
    {
        public string Codigo, ModeloNome;
        public GTA.Model Modelo;
        public GTA.Entity Entidade;
        public GTA.Math.Vector3 Posicao, Rotacao;
        public System.IntPtr Endereco;
        public int Handle, Tipo, Inicio, CriadoEm;
        public bool Veiculo, Congelado, NoChao, Pedido, Pronto, Telefone, ColisaoPronta;
        public bool MesaMedida, LaptopMedido, VinhoMedido, BandejaMedida, MesaSuperficiePronta;
        public bool CaixaTecnicaMedida, CaixaProducaoMedida, PaperPreview86, BancoSonoMedidoC1, MesaGrandeMedidaC8, OfficePaperBag84;
        public int CaixaNextPhysicsLog, CaixaNextSurfaceLog;
        public MesaProbe[] MesaProbes;
        public MesaProbe[] BancoSeatGrid;
        public MesaProbe[] MesaC8SurfaceGrid;
        public GTA.Math.Vector3 MesaPosition, MesaRotation;
    }

    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.Dictionary<string, Recurso> recursos =
        new System.Collections.Generic.Dictionary<string, Recurso>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<int> modelosPedidos =
        new System.Collections.Generic.HashSet<int>();
    private bool disposed;
    private const int LimiteRecursos = 8;
    private const int TimeoutMs = 5000;

    public ObjetosDiretor(System.Action<string, string> log) { this.log = log; }

    public int Count { get { return recursos.Count; } }
    public int PendingCount
    {
        get
        {
            int pending = 0;
            foreach (Recurso recurso in recursos.Values) if (!recurso.Pronto) pending++;
            return pending;
        }
    }
    public bool IsReady { get { return !disposed && PendingCount == 0; } }

    public void RequestProp(string codigo, string modelo, GTA.Math.Vector3 posicao,
                            GTA.Math.Vector3 rotacao, bool congelado, string technicalBoxSceneId = null, string paperPreviewSceneId = null, string productionBoxSceneId = null, string sleepBenchSceneId = null, string largeTableC8SceneId = null, string officePaperBag84SceneId = null)
    {
        Request(codigo, modelo, posicao, rotacao, congelado, false, false, technicalBoxSceneId, paperPreviewSceneId, productionBoxSceneId, sleepBenchSceneId, largeTableC8SceneId, officePaperBag84SceneId);
    }

    public void RequestVehicle(string codigo, string modelo, GTA.Math.Vector3 posicao,
                               float heading, bool congelado, bool colocarNoChao = true)
    {
        if (!Finite(heading)) throw Error("Direção do veículo precisa ser finita.");
        Request(codigo, modelo, posicao, new GTA.Math.Vector3(0f, 0f, heading), congelado, true, colocarNoChao);
    }

    private void Request(string codigo, string modelo, GTA.Math.Vector3 posicao, GTA.Math.Vector3 rotacao,
                         bool congelado, bool veiculo, bool noChao, string technicalBoxSceneId = null, string paperPreviewSceneId = null, string productionBoxSceneId = null, string sleepBenchSceneId = null, string largeTableC8SceneId = null, string officePaperBag84SceneId = null)
    {
        RequireActive();
        if (codigo == null || !System.Text.RegularExpressions.Regex.IsMatch(codigo, "^[A-Z][A-Z0-9_]{0,63}$"))
            throw Error("Código do recurso deve conter letras maiúsculas, números e sublinhado.");
        if (modelo == null || !System.Text.RegularExpressions.Regex.IsMatch(modelo, "^[a-z][a-z0-9_]{0,63}$"))
            throw Error("Nome do modelo deve ser um identificador técnico conferido.");
        if (recursos.ContainsKey(codigo)) throw Error("Código de recurso repetido: " + codigo);
        if (recursos.Count >= LimiteRecursos) throw Error("Limite de oito recursos por cena atingido.");
        RequireVector(posicao, "posição");
        RequireVector(rotacao, "rotação");
        GTA.Model model = new GTA.Model(modelo);
        if (!model.IsInCdImage || (veiculo ? !model.IsVehicle : !model.IsProp))
            throw Error("Modelo não disponível ou de tipo incorreto: " + modelo);
        Recurso recurso = new Recurso();
        recurso.Codigo = codigo;
        recurso.ModeloNome = modelo;
        recurso.Modelo = model;
        recurso.Posicao = posicao;
        recurso.Rotacao = rotacao;
        recurso.Congelado = congelado;
        recurso.Veiculo = veiculo;
        recurso.NoChao = noChao;
        recurso.Telefone = modelo == "prop_npc_phone_02";
        recurso.OfficePaperBag84 = officePaperBag84SceneId == "teste_fila_84" && codigo == "SACOLA_CALIBRACAO84" &&
            modelo == "prop_paper_bag_small" && !veiculo && congelado && !noChao && technicalBoxSceneId == null &&
            paperPreviewSceneId == null && productionBoxSceneId == null && sleepBenchSceneId == null && largeTableC8SceneId == null;
        if (officePaperBag84SceneId != null && !recurso.OfficePaperBag84)
            throw Error("84 paper bag precreate scope requires sole literal owned technical prop; no other capability.");
        // Native104 confirmou cinco hits proprios para este unico modelo, com getter COL=false.
        // Nenhum outro modelo recebe esta capacidade; COL=false nunca e chamado de carregado.
        // Ensaio115 exclusivo da bancada propria MESA_LIMPEZA. COL=false continua falso;
        // nenhum Ready antes da mesma prova fisica de cinco superficies proprias.
        // Opt-in exclusivo do ensaio88: nao altera readiness de nenhuma cena de producao.
        recurso.PaperPreview86 = paperPreviewSceneId == "teste_fila_86" && !veiculo && congelado && !noChao &&
            codigo == "PAPEL_PROVA" && technicalBoxSceneId == null && productionBoxSceneId == null && ObjetosSerieDiretor.IsPaperPreviewModel86(modelo);
        if (paperPreviewSceneId != null && !recurso.PaperPreview86)
            throw Error("preview86 apenas modelo literal proprio e sem collider aprovado");
        recurso.CaixaTecnicaMedida = technicalBoxSceneId == "teste_fila_88" && !veiculo && congelado &&
            IsTechnicalBoxModel88(modelo) && (codigo == "CAIXAS_NEGOCIO_1" || codigo == "CAIXAS_NEGOCIO_2");
        if (technicalBoxSceneId != null && !recurso.CaixaTecnicaMedida)
            throw Error("ensaio88 exige apenas as duas caixas literais proprias congeladas");
        recurso.CaixaProducaoMedida = productionBoxSceneId == "EP07_C02" && technicalBoxSceneId == null &&
            paperPreviewSceneId == null && !veiculo && congelado && modelo == "prop_box_wood01a" &&
            (codigo == "CAIXAS_NEGOCIO_1" || codigo == "CAIXAS_NEGOCIO_2");
        if (productionBoxSceneId != null && !recurso.CaixaProducaoMedida)
            throw Error("opt-in EP07_C02 somente para as duas caixas wood01a proprias congeladas");
        // Exact C1 opt-in. COL=false remains false: creation is only a candidate,
        // five own horizontal surfaces plus actual physics must pass before Ready.
        recurso.BancoSonoMedidoC1 = (sleepBenchSceneId == "EP10_C01" || sleepBenchSceneId == "teste_fila_84") && !veiculo && congelado && !noChao &&
            codigo == "BANCO_PRAIA" && modelo == "prop_bench_02" && technicalBoxSceneId == null &&
            paperPreviewSceneId == null && productionBoxSceneId == null;
        if (sleepBenchSceneId != null && !recurso.BancoSonoMedidoC1)
            throw Error("sleep bench opt-in exclusive EP10_C01/BANCO_PRAIA/prop_bench_02 own frozen prop");
        // Novo candidato Menyoo literal. COL=false continua falso; mesmos5hits e physics antesReady.
        recurso.MesaGrandeMedidaC8 = largeTableC8SceneId == "EP15_C08" && !veiculo && congelado && !noChao &&
            codigo == "MESA_JANTAR" && modelo == "v_res_m_dinetble_replace" && technicalBoxSceneId == null &&
            paperPreviewSceneId == null && productionBoxSceneId == null && sleepBenchSceneId == null;
        if (largeTableC8SceneId != null && !recurso.MesaGrandeMedidaC8)
            throw Error("large dining table opt-in exclusive EP15_C08/MESA_JANTAR/literal model, no other technical capability");
        if (modelo == "v_res_m_dinetble_replace" && !recurso.MesaGrandeMedidaC8)
            throw Error("large dining model requires explicit EP15_C08 own-table capability");
        recurso.MesaMedida = !veiculo && congelado && (modelo == "prop_table_01" ||
            (codigo == "MESA_LIMPEZA" && modelo == "prop_ff_counter_02")) || recurso.CaixaTecnicaMedida || recurso.CaixaProducaoMedida || recurso.BancoSonoMedidoC1 || recurso.MesaGrandeMedidaC8;
        // Native105 laptop01a tem Loaded=true/COL=false antesCreate. Inscricao exclusiva,
        // exige prova da propria geometria antesReady; nenhum outro prop e flexibilizado.
        recurso.LaptopMedido = !veiculo && congelado && modelo == "prop_laptop_01a";
        // Native106: wine_glass Loaded/COL=false antesCreate. Inscricao exclusiva:
        // provar collider da propria taca antesReady, sem chamar COL=false de carregado.
        recurso.VinhoMedido = !veiculo && congelado && modelo == "prop_wine_glass";
        // Native112: esta bandeja literal RPEmotes Loaded=true/COL=false antesCreate.
        // Excecao por modelo proprio, nao por papel generico: provar Physics/collider
        // do proprio handle antesReady, com os mesmos raios/bounds/prazo do vinho/laptop.
        // Champw2 e um ensaio de modelo literal do autor: exige a mesma prova
        // fisica do proprio handle; disponibilidade/visual ainda pendentes.
        recurso.BandejaMedida = !veiculo && congelado && (modelo == "prop_food_bs_tray_03" ||
            modelo == "h4_prop_h4_champ_tray_01b");
        recurso.Inicio = GTA.Game.GameTime;
        recursos.Add(codigo, recurso);
        Log("RECURSO_PEDIDO", "Codigo=" + codigo + "; Modelo=" + modelo + "; Estado=não testado visualmente");
    }

    // Lista finita de fontes Menyoo conferidas. Nome nunca prova collider/visual.
    // Somente RequestProp com ID tecnico88 e dois codigos proprios chega a esta porta.
    public static bool IsTechnicalBoxModel88(string model)
    {
        return model == "prop_box_wood02a" || model == "prop_box_wood01a" ||
            model == "prop_box_wood03a" || model == "prop_box_wood04a" ||
            model == "prop_box_wood05a" || model == "prop_box_wood05b";
    }

    // Chamar por Tick; no máximo uma criação por chamada. Não usa Script.Wait nem Request(timeout).
    public void Update()
    {
        RequireActive();
        bool criou = false;
        int mesaPending = 0;
        foreach (Recurso entry in recursos.Values)
            if (entry.MesaProbes != null)
                foreach (MesaProbe probe in entry.MesaProbes) if (probe.Started && !probe.Done) mesaPending++;
        foreach (Recurso entry in recursos.Values)
            if (entry.BancoSeatGrid != null)
                foreach (MesaProbe probe in entry.BancoSeatGrid) if (probe.Started && !probe.Done) mesaPending++;
        foreach (Recurso entry in recursos.Values)
            if (entry.MesaC8SurfaceGrid != null)
                foreach (MesaProbe probe in entry.MesaC8SurfaceGrid) if (probe.Started && !probe.Done) mesaPending++;
        foreach (Recurso recurso in recursos.Values)
        {
            if (recurso.Pronto)
            {
                RequireOwned(recurso);
                continue;
            }
            if (System.Object.ReferenceEquals(recurso.Entidade, null))
            {
                if (!recurso.Pedido)
                {
                    recurso.Modelo.Request();
                    if (!recurso.Veiculo)
                    {
                        bool collisionBefore = recurso.Modelo.IsCollisionLoaded;
                        recurso.Modelo.RequestCollision();
                        Log("RECURSO_COLISAO_MODELO", "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
                            "; Fase=pedido; Getter=GTA.Model.IsCollisionLoaded; Loaded=" + collisionBefore +
                            "; EsperaMs=" + Elapsed(recurso.Inicio));
                    }
                    modelosPedidos.Add(recurso.Modelo.Hash);
                    recurso.Pedido = true;
                    continue; // Streaming tem pelo menos um frame antes de criar.
                }
                if (!recurso.Modelo.IsLoaded)
                {
                    if (Elapsed(recurso.Inicio) >= TimeoutMs)
                        throw Error("O modelo não carregou em cinco segundos: " + recurso.ModeloNome);
                    continue;
                }
                if (!recurso.Veiculo && !recurso.ColisaoPronta && !recurso.MesaMedida && !recurso.LaptopMedido && !recurso.VinhoMedido && !recurso.BandejaMedida && !recurso.PaperPreview86)
                {
                    bool collisionLoaded = recurso.Modelo.IsCollisionLoaded;
                    if (!collisionLoaded)
                    {
                        // O modelo ja carregou: renovar o pedido entre frames ate o getter confirmar.
                        recurso.Modelo.RequestCollision();
                        if (Elapsed(recurso.Inicio) >= TimeoutMs)
                        {
                            Log("RECURSO_COLISAO_MODELO", "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
                                "; Fase=timeout; Getter=GTA.Model.IsCollisionLoaded; Loaded=" + collisionLoaded +
                                "; EsperaMs=" + Elapsed(recurso.Inicio));
                            throw Error("A colisao do modelo nao carregou em cinco segundos: " + recurso.ModeloNome);
                        }
                        continue;
                    }
                    recurso.ColisaoPronta = true;
                    Log("RECURSO_COLISAO_MODELO", "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
                        "; Fase=carregada_antes_create; Getter=GTA.Model.IsCollisionLoaded; Loaded=" + collisionLoaded +
                        "; EsperaMs=" + Elapsed(recurso.Inicio));
                }
                if (criou) continue;
                if (recurso.MesaMedida || recurso.LaptopMedido || recurso.VinhoMedido || recurso.BandejaMedida)
                {
                    if (Elapsed(recurso.Inicio) >= TimeoutMs) throw Error("Modelo inscrito excedeu cinco segundos antes da criacao fisica.");
                    Log((recurso.CaixaTecnicaMedida || recurso.CaixaProducaoMedida) ? BoxGeometryLog(recurso, "CREATE") : recurso.BandejaMedida ? "BANDEJA_PRODUCAO_CREATE" : recurso.MesaMedida ? "MESA_PRODUCAO_CREATE" : recurso.VinhoMedido ? "VINHO_PRODUCAO_CREATE" : "LAPTOP_PRODUCAO_CREATE", "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
                        "; ModelLoaded=" + recurso.Modelo.IsLoaded + "; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded +
                        "; Fonte=GTA.Prop.CreateNoOffset; Ready=False; ExigeCincoPlanosProprios=" + recurso.MesaMedida + "; ExigeOwnGeometry=True");
                }
                if (recurso.OfficePaperBag84)
                {
                    GTA.Math.Vector3 min, max; recurso.Modelo.GetDimensions(out min,out max);
                    RequireVector(min,"84 paper model native minimum"); RequireVector(max,"84 paper model native maximum");
                    GTA.Math.Vector3 size=max-min;
                    bool known=size.X>0f && size.Y>0f && size.Z>0f && size.Length()<=3f;
                    Log("SONO84_PAPERBAG_BOUNDS_BEFORE_CREATE", "Model="+recurso.ModeloNome+"; NativeHash="+recurso.Modelo.Hash+
                        "; Loaded="+recurso.Modelo.IsLoaded+"; CollisionLoaded="+recurso.Modelo.IsCollisionLoaded+
                        "; Min="+min+"; Max="+max+"; Extents="+size+"; BoundsKnown="+known+
                        "; NativeEntityCreated=False; Scale=1; FitPending=True; SkinUnknown=True; HugFalse=True; ARM=False");
                    if(!known)throw Error("84 loaded actual paper bag bounds unavailable; stop before create.");
                }
                // API191: dynamic=true forca CObject, mesmo para modelo de porta.
                // Somente esta folha propria precisa ser CDoor; as outras pecas
                // do armario usam o mesmo modelo como madeira fixa (CObject).
                bool createAsDoor = !recurso.Veiculo && recurso.Codigo == "ARMARIO_DJANIRA" &&
                    recurso.ModeloNome == "prop_wardrobe_door_01";
                if (recurso.Veiculo)
                    recurso.Entidade = GTA.Vehicle.Create(recurso.Modelo, recurso.Posicao, recurso.Rotacao.Z);
                else
                    recurso.Entidade = GTA.Prop.CreateNoOffset(recurso.Modelo, recurso.Posicao, recurso.Rotacao, !createAsDoor);
                if (System.Object.ReferenceEquals(recurso.Entidade, null) || !recurso.Entidade.Exists())
                    throw Error("Não foi possível criar o recurso: " + recurso.Codigo);
                // Registrar a propriedade antes de qualquer configuração que possa falhar.
                recurso.Handle = recurso.Entidade.Handle;
                recurso.Tipo = recurso.Veiculo ? 2 : 3;
                recurso.Endereco = recurso.Entidade.MemoryAddress;
                recurso.CriadoEm = GTA.Game.GameTime;
                recurso.Entidade.IsPersistent = true;
                recurso.Entidade.IsInvincible = true;
                recurso.Entidade.IsPositionFrozen = true;
                recurso.Entidade.IsVisible = !recurso.Telefone;
                recurso.Entidade.IsCollisionEnabled = !recurso.Telefone;
                if (createAsDoor)
                    Log("RECURSO_CRIACAO_PORTA", "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
                        "; Handle=" + recurso.Handle + "; DynamicArgument=False; CriacaoComoPortaSolicitada=true; PhysicsDoorSystemAindaPendente=true");
                criou = true;
            }
            RequireOwned(recurso);
            if (recurso.PaperPreview86)
                Log("PAPER_PREVIEW_MESH_ONLY", "Codigo=" + recurso.Codigo + "; Handle=" + recurso.Handle + "; Model=" + recurso.ModeloNome +
                    "; ModelLoaded=" + recurso.Modelo.IsLoaded + "; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded +
                    "; HasPhysics=" + ((GTA.Prop)recurso.Entidade).HasPhysics + "; ColliderProved=False; ProductionApproval=False; PreviewOnly=True");
            if (recurso.CaixaTecnicaMedida || recurso.CaixaProducaoMedida)
            { if (!PollTechnicalBoxSurface(recurso)) continue; }
            else if ((recurso.MesaMedida || recurso.LaptopMedido || recurso.VinhoMedido || recurso.BandejaMedida) && !PollMesaSurface(recurso, ref mesaPending)) continue;
            if (recurso.Veiculo && recurso.NoChao)
            {
                GTA.Native.Function.Call(GTA.Native.Hash.REQUEST_COLLISION_AT_COORD,
                    recurso.Posicao.X, recurso.Posicao.Y, recurso.Posicao.Z);
                if (!GTA.Native.Function.Call<bool>(GTA.Native.Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, recurso.Handle) ||
                    !((GTA.Vehicle)recurso.Entidade).PlaceOnGround())
                {
                    if (Elapsed(recurso.CriadoEm) >= TimeoutMs)
                        throw Error("Chão/colisão não disponíveis para o veículo: " + recurso.Codigo);
                    continue;
                }
                // A marca do veículo é a posição real depois de assentar, não a altura inicial.
                recurso.Posicao = recurso.Entidade.Position;
                recurso.Rotacao = recurso.Entidade.Rotation;
            }
            recurso.Entidade.IsPositionFrozen = recurso.Telefone || recurso.Congelado;
            recurso.Pronto = true;
            Log("RECURSO_CRIADO", "Codigo=" + recurso.Codigo + "; Handle=" + recurso.Handle + "; Modelo=" + recurso.ModeloNome);
        }
    }

    // Repetir apenas durante preparo: o assentamento autorizado mudou o transform da mesa propria.
    // Volta a pendente; nenhum consumidor obtem um GetProp pronto ate os cinco novos hits reais.
    public void RecheckMeasuredTableSurface(string codigo)
    {
        Recurso recurso = Ready(codigo);
        if (!recurso.MesaMedida || !recurso.MesaSuperficiePronta)
            throw Error("Recheck exige apoio proprio inscrito e cinco superficies ja medidas.");
        recurso.MesaProbes = null; recurso.MesaSuperficiePronta = false; recurso.Pronto = false;
        // Inicio original e preservado: a segunda prova nao renova o prazo de cinco segundos.
        Log((recurso.CaixaTecnicaMedida || recurso.CaixaProducaoMedida) ? BoxGeometryLog(recurso, "RECHECK") : "MESA_PRODUCAO_RECHECK", "Codigo=" + codigo + "; Position=" + recurso.Entidade.Position +
            "; DeadlineMs=5000; EsperaMs=" + Elapsed(recurso.Inicio) + "; Ready=False; AposAssentamento=True");
    }

    // Mesma prova fisica88, com opt-in separado EP07_C02 para wood01a. Um raio por Tick e por caixa.
    // Nenhum status0/1, piso, terceiro, bbox visual ou getter COL=false e aceito como collider.
    // NotReady/noHit aguarda somente PREPARO antes do deadline original, sem aproveitar lote parcial.
    private bool PollTechnicalBoxSurface(Recurso recurso)
    {
        RequireOwned(recurso);
        if (!recurso.CaixaTecnicaMedida && !recurso.CaixaProducaoMedida) throw Error("prova de caixa propria nao inscrita");
        GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        if (Elapsed(recurso.Inicio) >= TimeoutMs) throw Error("caixa tecnica nao provou collider proprio em cinco segundos");
        recurso.Modelo.RequestCollision(); // Pedido normal entre frames; nao certifica collider nem Ready.
        if (!prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled || prop.IsAttached())
            throw Error("caixa tecnica perdeu visibilidade/colisao/free-transform");
        if (GTA.Game.GameTime == recurso.CriadoEm) return false; // Streaming precisa de um Tick apos criar.
        if (!prop.HasPhysics)
        {
            if (recurso.MesaProbes != null) throw Error("caixa tecnica perdeu physics depois de iniciar a prova");
            if (Elapsed(recurso.Inicio) >= recurso.CaixaNextPhysicsLog)
            {
                recurso.CaixaNextPhysicsLog = (int)Elapsed(recurso.Inicio) + 500;
                Log(BoxGeometryLog(recurso, "PHYSICS_PENDING"), "Codigo=" + recurso.Codigo + "; Handle=" + recurso.Handle +
                    "; HasPhysics=false; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded +
                    "; Ready=false; DeadlineMs=5000; EsperaMs=" + Elapsed(recurso.Inicio));
            }
            return false; // Somente PREPARO limitado; nunca Ready sem physics e cinco ownhits.
        }
        if (recurso.MesaProbes == null)
        {
            GTA.Math.Vector3 min, max;
            recurso.Modelo.GetDimensions(out min, out max);
            RequireVector(min, "bbox minimo da caixa"); RequireVector(max, "bbox maximo da caixa");
            if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("bbox real da caixa invalido");
            recurso.MesaPosition = prop.Position; recurso.MesaRotation = prop.Rotation;
            RequireVector(recurso.MesaPosition, "posicao da caixa"); RequireVector(recurso.MesaRotation, "rotacao da caixa");
            if (System.Math.Abs(prop.Rotation.X) > 0.1f || System.Math.Abs(prop.Rotation.Y) > 0.1f)
                throw Error("caixa tecnica exige apoio horizontal; sem ajustar tilt para esconder erro");
            float cx = (min.X + max.X) * 0.5f, cy = (min.Y + max.Y) * 0.5f;
            float ex = (max.X - min.X) * 0.4f, ey = (max.Y - min.Y) * 0.4f;
            float[] xs = { 0f, -ex, ex, -ex, ex }, ys = { 0f, -ey, -ey, ey, ey };
            recurso.MesaProbes = new MesaProbe[5];
            for (int i = 0; i < 5; i++)
            {
                MesaProbe probe = new MesaProbe();
                probe.From = prop.GetOffsetPosition(new GTA.Math.Vector3(cx + xs[i], cy + ys[i], max.Z + 0.25f));
                probe.To = prop.GetOffsetPosition(new GTA.Math.Vector3(cx + xs[i], cy + ys[i], min.Z - 0.10f));
                RequireVector(probe.From, "inicio do raio da caixa"); RequireVector(probe.To, "fim do raio da caixa");
                recurso.MesaProbes[i] = probe;
            }
            return false; // Um Tick real entre criacao/transform e os primeiros raios.
        }
        RequireTechnicalBoxGeometryState(recurso, false);
        int firstProbe = 0;
        for (int pendingIndex = 0; pendingIndex < 5; pendingIndex++)
            if (recurso.MesaProbes[pendingIndex].Started && !recurso.MesaProbes[pendingIndex].Done)
            { firstProbe = pendingIndex; break; }
        for (int step = 0; step < 5; step++)
        {
            int i = (firstProbe + step) % 5;
            MesaProbe probe = recurso.MesaProbes[i];
            if (probe.Done) continue;
            GTA.ShapeTestResult result;
            bool resumedPending = probe.Started;
            if (!probe.Started)
            {
                probe.Handle = GTA.ShapeTest.StartExpensiveSyncTestLOSProbe(probe.From, probe.To,
                    GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, null, (GTA.ShapeTestOptions)0);
                probe.Started = true;
            }
            GTA.ShapeTestStatus status = probe.Handle.GetResult(out result);
            // Ready consome o request; NotReady conserva o MESMO handle para o proximo frame.
            if (status != GTA.ShapeTestStatus.NotReady) probe.Started = false;
            GTA.Entity hit;
            bool own = status == GTA.ShapeTestStatus.Ready && result.DidHit && result.TryGetHitEntity(out hit) &&
                !System.Object.ReferenceEquals(hit, null) && hit.Handle == recurso.Handle && hit.MemoryAddress == recurso.Endereco &&
                hit.Model.Hash == recurso.Modelo.Hash;
            GTA.Math.Vector3 p = result.HitPosition, n = result.SurfaceNormal;
            GTA.Math.Vector3 local = prop.GetPositionOffset(p), min, max;
            recurso.Modelo.GetDimensions(out min, out max);
            bool valid = own && MesaVectorFinite(p) && MesaVectorFinite(n) && MesaVectorFinite(local) &&
                MesaVectorFinite(min) && MesaVectorFinite(max) &&
                System.Math.Abs(p.X - probe.From.X) <= 0.01f && System.Math.Abs(p.Y - probe.From.Y) <= 0.01f &&
                p.Z <= probe.From.Z && p.Z >= probe.To.Z && n.Z >= 0.98f &&
                System.Math.Abs(n.Length() - 1f) <= 0.02f &&
                local.X >= min.X - 0.02f && local.X <= max.X + 0.02f &&
                local.Y >= min.Y - 0.02f && local.Y <= max.Y + 0.02f &&
                local.Z >= min.Z - 0.02f && local.Z <= max.Z + 0.02f;
            bool streamingPending = !result.DidHit &&
                (status == GTA.ShapeTestStatus.NotReady || status == GTA.ShapeTestStatus.Ready);
            if (streamingPending)
            {
                // Ready sem hit reprove todo o lote anterior. NotReady conserva o request ainda em andamento.
                if (status == GTA.ShapeTestStatus.Ready)
                    foreach (MesaProbe previous in recurso.MesaProbes)
                    { previous.Done = false; previous.OwnSurface = false; previous.Point = GTA.Math.Vector3.Zero; }
                if (Elapsed(recurso.Inicio) >= recurso.CaixaNextSurfaceLog)
                {
                    recurso.CaixaNextSurfaceLog = (int)Elapsed(recurso.Inicio) + 500;
                    Log(BoxGeometryLog(recurso, "COLLIDER_PENDING"), "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
                        "; Index=" + i + "; Status=" + status + "; DidHit=" + result.DidHit +
                        "; OwnHit=false; Ready=false; LoteCincoInvalidado=" + (status == GTA.ShapeTestStatus.Ready) +
                        "; MesmoAlvo=true; SemSetterTransform=true; DeadlineMs=5000; EsperaMs=" + Elapsed(recurso.Inicio) +
                        "; HasPhysics=" + prop.HasPhysics + "; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded +
                        "; Handle=" + recurso.Handle + "; SameCallSynchronous=" + !resumedPending +
                        "; PendingHandleReused=" + resumedPending + "; Flags=17; Options=0; TrialOnly=" + recurso.CaixaTecnicaMedida + "; ProductionScopeEP07C02=" + recurso.CaixaProducaoMedida);
                }
                return false;
            }
            Log(BoxGeometryLog(recurso, "RAY"), "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
                "; Index=" + i + "; Status=" + status + "; DidHit=" + result.DidHit + "; OwnHit=" + own +
                "; Point=" + p + "; Normal=" + n + "; Local=" + local + "; OwnSurface=" + valid +
                "; ExpectedHandle=" + recurso.Handle + "; ExpectedAddress=" + recurso.Endereco +
                "; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded + "; SameCallSynchronous=" + !resumedPending +
                "; PendingHandleReused=" + resumedPending + "; Flags=17; Options=0; TrialOnly=" + recurso.CaixaTecnicaMedida + "; ProductionScopeEP07C02=" + recurso.CaixaProducaoMedida);
            if (!valid) throw Error("raio nao provou superficie horizontal da caixa propria; sem substituicao por piso/terceiro");
            probe.Done = true; probe.OwnSurface = true; probe.Point = p;
            return false; // Um raio por caixa e por Tick; nunca usa xyz de procura.
        }
        float low = float.MaxValue, high = float.MinValue;
        foreach (MesaProbe probe in recurso.MesaProbes)
        {
            if (!probe.Done || !probe.OwnSurface) throw Error("caixa tecnica perdeu uma das cinco provas");
            low = System.Math.Min(low, probe.Point.Z); high = System.Math.Max(high, probe.Point.Z);
        }
        if (!Finite(low) || !Finite(high) || high - low > 0.08f) throw Error("cinco superfices da caixa nao confirmaram plano proprio");
        recurso.MesaSuperficiePronta = true;
        Log(BoxGeometryLog(recurso, "GEOMETRIA"), "Codigo=" + recurso.Codigo + "; Modelo=" + recurso.ModeloNome +
            "; OwnHits=5; Centro=" + recurso.MesaProbes[0].Point + "; VariacaoZ=" + (high - low) +
            "; HasPhysics=" + prop.HasPhysics + "; Collision=" + prop.IsCollisionEnabled + "; Frozen=" + prop.IsPositionFrozen +
            "; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded + "; Position=" + prop.Position +
            "; Rotation=" + prop.Rotation + "; EsperaMs=" + Elapsed(recurso.Inicio) + "; ProductionApproval=false; TrialOnly=" + recurso.CaixaTecnicaMedida + "; ProductionScopeEP07C02=" + recurso.CaixaProducaoMedida);
        return true;
    }

    private static string BoxGeometryLog(Recurso recurso, string suffix)
    { return (recurso.CaixaProducaoMedida ? "CAIXA_PRODUCAO_C2_" : "CAIXA_TRIAL_") + suffix; }

    public void RequireProductionBoxGeometry(string codigo)
    {
        Recurso recurso = Ready(codigo);
        if (!recurso.CaixaProducaoMedida || recurso.CaixaTecnicaMedida || recurso.ModeloNome != "prop_box_wood01a")
            throw Error("somente wood01a propria inscrita pelo contrato EP07_C02 pode consultar esta prova");
        RequireTechnicalBoxGeometryState(recurso, true);
    }

    public void RequireTechnicalBoxGeometry(string codigo)
    {
        Recurso recurso = Ready(codigo);
        if (!recurso.CaixaTecnicaMedida) throw Error("somente caixa propria do ensaio88 pode consultar esta prova");
        RequireTechnicalBoxGeometryState(recurso, true);
    }

    private void RequireTechnicalBoxGeometryState(Recurso recurso, bool requireProof)
    {
        RequireOwned(recurso);
        GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        if ((!recurso.CaixaTecnicaMedida && !recurso.CaixaProducaoMedida) || (requireProof && !recurso.MesaSuperficiePronta) || recurso.MesaProbes == null ||
            !prop.HasPhysics || !prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled || prop.IsAttached() ||
            (prop.Position - recurso.MesaPosition).Length() > 0.01f ||
            System.Math.Abs(prop.Rotation.X - recurso.MesaRotation.X) > 0.1f ||
            System.Math.Abs(prop.Rotation.Y - recurso.MesaRotation.Y) > 0.1f ||
            System.Math.Abs(MesaAngle(prop.Rotation.Z - recurso.MesaRotation.Z)) > 0.1f)
            throw Error("caixa propria perdeu identidade/physics/transform/colisao da prova real");
    }

    private bool PollMesaSurface(Recurso recurso, ref int pending)
    {
        if (recurso.MesaSuperficiePronta) return true;
        RequireOwned(recurso);
        GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        RequireVector(prop.Position, "posicao da mesa"); RequireVector(prop.Rotation, "rotacao da mesa");
        if (!prop.HasPhysics || !prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled || prop.IsAttached())
            throw Error("Prop inscrito exige physics, visibilidade, colisao e transform livre congelado antes da prova: " + recurso.ModeloNome);
        if (Elapsed(recurso.Inicio) >= TimeoutMs) throw Error("Geometria propria nao terminou em cinco segundos: " + recurso.ModeloNome);
        recurso.Modelo.RequestCollision(); // Renovacao nao bloqueante; getter falso nunca e declarado carregado.
        if (recurso.MesaProbes == null)
        {
            GTA.Math.Vector3 min, max;
            recurso.Modelo.GetDimensions(out min, out max);
            RequireVector(min, "bbox minimo da mesa"); RequireVector(max, "bbox maximo da mesa");
            if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("Bounds reais do apoio inscrito invalidos.");
            recurso.MesaPosition = prop.Position; recurso.MesaRotation = prop.Rotation;
            float cx = (min.X + max.X) * 0.5f, cy = (min.Y + max.Y) * 0.5f;
            float ex = (max.X - min.X) * 0.4f, ey = (max.Y - min.Y) * 0.4f;
            float[] xs = { 0f, -ex, ex, -ex, ex }, ys = { 0f, -ey, -ey, ey, ey };
            if (recurso.MesaGrandeMedidaC8)
            {
                // Native173 grid indices12/6/8/16/18 are owned horizontal top samples.
                // The full oval bbox corners are not solid; fresh five proofs still required.
                if (recurso.Codigo != "MESA_JANTAR" || recurso.ModeloNome != "v_res_m_dinetble_replace" ||
                    (min - new GTA.Math.Vector3(-0.9785295f, -0.938943f, 0.00002297759f)).Length() > 0.002f ||
                    (max - new GTA.Math.Vector3(0.9867694f, 0.938943f, 0.7329441f)).Length() > 0.002f)
                    throw Error("C8 table dimensions differ from actual173 own surface grid; no reuse of support samples");
                ex = (max.X - min.X) * 0.225f; ey = (max.Y - min.Y) * 0.225f;
                xs = new float[] { 0f, -ex, ex, -ex, ex };
                ys = new float[] { 0f, -ey, -ey, ey, ey };
                Log("MESA_C8_SUPORTE_CANDIDATO", "OwnEntity=" + recurso.Handle + "; BoundsMin=" + min + "; BoundsMax=" + max +
                    "; LocalMinXY=(" + (cx-ex) + "," + (cy-ey) + "); LocalMaxXY=(" + (cx+ex) + "," + (cy+ey) +
                    "); Source=Native173OwnGrid25; Indices=12,6,8,16,18; Fractions=.275..725; FreshOwnRaysRequired=5; Ready=False; ContinuousSurfaceProven=False; ActorVolumeProven=False");
            }
            if (recurso.BancoSonoMedidoC1)
            {
                // Local XY comes from actual157 own collider samples; never full model bbox.
                RequireVector(min, "sleep bench bbox min"); RequireVector(max, "sleep bench bbox max");
                if ((min - new GTA.Math.Vector3(-1.20795f, -0.448685f, -0.043214f)).Length() > 0.002f ||
                    (max - new GTA.Math.Vector3(1.178813f, 0.417857f, 0.895945f)).Length() > 0.002f)
                    throw Error("bench02 dimensions differ from actual measured collider; do not reuse seat samples");
                float left = -1.088591f, rightSeat = 1.05944f;
                // Preserve actual157 central anchor; do not shift the pose with rectangle midpoint.
                cx = (left + rightSeat) * 0.5f; cy = (-0.2103419f - 0.0154507f) * 0.5f;
                // Native174 upperY+.06487 hit tilted backrest; never accept it as flat seat.
                // Candidate stays near actual157 flat rows, only5mm beyond each sampled row.
                // No body margin: all five fresh corners must prove the same horizontal seat Z.
                float frontSeat = -0.2103419f - 0.005f, backSeat = -0.0154507f + 0.005f;
                xs = new float[] { 0f, left-cx, rightSeat-cx, left-cx, rightSeat-cx };
                ys = new float[] { 0f, frontSeat-cy, frontSeat-cy, backSeat-cy, backSeat-cy };
                Log("BANCO_C1_ASSENTO_CANDIDATO", "OwnEntity=" + recurso.Handle + "; LocalMinXY=(" + left + "," + frontSeat +
                    "); LocalMaxXY=(" + rightSeat + "," + backSeat + "); CenterLocalXY=(" + cx + "," + cy +
                    "); Source=Native157FlatRowsPlus5mmCandidate; PreservedCenterSource=Native157OwnGrid14Samples; CandidateRegionExtensionY=0.005; BodyXYMargin=0; FreshOwnRaysRequired=5; RecheckAfterGroundRequired=True; Ready=False; BodySupported=False; ContinuousSurfaceProven=False");
            }
            recurso.MesaProbes = new MesaProbe[5];
            for (int i = 0; i < 5; i++)
            {
                MesaProbe probe = new MesaProbe();
                probe.From = prop.GetOffsetPosition(new GTA.Math.Vector3(cx + xs[i], cy + ys[i], max.Z + 0.25f));
                probe.To = prop.GetOffsetPosition(new GTA.Math.Vector3(cx + xs[i], cy + ys[i], min.Z - 0.10f));
                RequireVector(probe.From, "inicio do raio da mesa"); RequireVector(probe.To, "fim do raio da mesa");
                recurso.MesaProbes[i] = probe;
            }
            return false; // Primeiro frame estabiliza collider/transform; Native104 iniciou raios no seguinte.
        }
        if ((prop.Position - recurso.MesaPosition).Length() > 0.01f ||
            System.Math.Abs(prop.Rotation.X - recurso.MesaRotation.X) > 0.1f ||
            System.Math.Abs(prop.Rotation.Y - recurso.MesaRotation.Y) > 0.1f ||
            System.Math.Abs(MesaAngle(prop.Rotation.Z - recurso.MesaRotation.Z)) > 0.1f)
            throw Error("Prop inscrito mudou transform durante a prova: " + recurso.ModeloNome);
        foreach (MesaProbe probe in recurso.MesaProbes)
        {
            if (!probe.Started || probe.Done) continue;
            GTA.ShapeTestResult measured;
            GTA.ShapeTestStatus status = probe.Handle.GetResult(out measured);
            if (status == GTA.ShapeTestStatus.NotReady) continue;
            pending--;
            GTA.Entity physical = null;
            bool ownHit = status == GTA.ShapeTestStatus.Ready && measured.DidHit &&
                measured.TryGetHitEntity(out physical) && !System.Object.ReferenceEquals(physical, null) && physical.Handle == recurso.Handle;
            GTA.Math.Vector3 p = measured.HitPosition, n = measured.SurfaceNormal;
            bool valid = ownHit && MesaVectorFinite(p) && MesaVectorFinite(n) &&
                System.Math.Abs(p.X - probe.From.X) <= 0.01f && System.Math.Abs(p.Y - probe.From.Y) <= 0.01f &&
                p.Z <= probe.From.Z && p.Z >= probe.To.Z &&
                System.Math.Abs(n.X) <= 1.01f && System.Math.Abs(n.Y) <= 1.01f && System.Math.Abs(n.Z) <= 1.01f;
            if (recurso.BancoSonoMedidoC1 && valid)
            {
                GTA.Math.Vector3 localSeat = prop.GetPositionOffset(p);
                valid = MesaVectorFinite(localSeat) && localSeat.Z >= 0.4432149f - 0.01f && localSeat.Z <= 0.4449139f + 0.01f;
            }
            if (recurso.MesaGrandeMedidaC8 && valid)
            {
                GTA.Math.Vector3 localTop = prop.GetPositionOffset(p), actualMin, actualMax;
                recurso.Modelo.GetDimensions(out actualMin, out actualMax);
                valid = MesaVectorFinite(localTop) && MesaVectorFinite(actualMax) &&
                    System.Math.Abs(localTop.Z - actualMax.Z) <= 0.01f;
            }
            if (recurso.MesaMedida) valid &= n.Z >= 0.98f;
            else if (valid)
            {
                // Tela/teclado nao sao uma mesa plana: provar collider proprio dentro do bbox.
                GTA.Math.Vector3 local = prop.GetPositionOffset(p), min, max;
                recurso.Modelo.GetDimensions(out min, out max);
                valid = MesaVectorFinite(local) && MesaVectorFinite(min) && MesaVectorFinite(max) &&
                    System.Math.Abs(n.Length() - 1f) <= 0.02f &&
                    local.X >= min.X - 0.02f && local.X <= max.X + 0.02f &&
                    local.Y >= min.Y - 0.02f && local.Y <= max.Y + 0.02f &&
                    local.Z >= min.Z - 0.02f && local.Z <= max.Z + 0.02f;
            }
            Log(recurso.BandejaMedida ? "BANDEJA_PRODUCAO_RAY" : recurso.MesaMedida ? "MESA_PRODUCAO_RAY" : recurso.VinhoMedido ? "VINHO_PRODUCAO_RAY" : "LAPTOP_PRODUCAO_RAY", "Codigo=" + recurso.Codigo +
                "; Modelo=" + recurso.ModeloNome + "; Status=" + status + "; DidHit=" + measured.DidHit +
                "; HitEntity=" + (System.Object.ReferenceEquals(physical, null) ? 0 : physical.Handle) +
                "; OwnEntity=" + recurso.Handle + "; Point=" + p + "; Normal=" + n +
                "; Flags=17; Options=0; OwnSurface=" + valid + "; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded);
            if (status != GTA.ShapeTestStatus.Ready || (recurso.MesaMedida && !valid) || (ownHit && !valid))
                throw Error("Raio nao confirmou geometria valida do prop inscrito: " + recurso.ModeloNome + "; piso/terceiros nao sao evidencia.");
            probe.Point = p; probe.Done = true; probe.OwnSurface = valid;
        }
        bool all = true; int ownHits = 0; float low = float.MaxValue, high = float.MinValue;
        foreach (MesaProbe probe in recurso.MesaProbes)
        {
            all &= probe.Done;
            if (probe.Done && probe.OwnSurface) { ownHits++; low = System.Math.Min(low, probe.Point.Z); high = System.Math.Max(high, probe.Point.Z); }
        }
        if (all)
        {
            if (!Finite(high) || !Finite(low) || ownHits < (recurso.MesaMedida ? 5 : 1) ||
                (recurso.MesaMedida && high - low > 0.08f))
                throw Error("Prop inscrito nao confirmou a geometria propria exigida: " + recurso.ModeloNome);
            recurso.MesaSuperficiePronta = true;
            Log(recurso.BandejaMedida ? "BANDEJA_PRODUCAO_GEOMETRIA" : recurso.MesaMedida ? "MESA_PRODUCAO_SUPERFICIE" : recurso.VinhoMedido ? "VINHO_PRODUCAO_GEOMETRIA" : "LAPTOP_PRODUCAO_GEOMETRIA", "Codigo=" + recurso.Codigo +
                "; Modelo=" + recurso.ModeloNome + "; OwnHits=" + ownHits + "; ExigePlano=" + recurso.MesaMedida +
                "; CentroAtingiuOwn=" + recurso.MesaProbes[0].OwnSurface + "; Centro=" + recurso.MesaProbes[0].Point +
                "; VariacaoZ=" + (high - low) + "; CincoHitsProprios=" + (ownHits == 5) + "; HasPhysics=" + prop.HasPhysics +
                "; Collision=" + prop.IsCollisionEnabled + "; Frozen=" + prop.IsPositionFrozen +
                "; Visible=" + prop.IsVisible + "; ModelCollisionLoaded=" + recurso.Modelo.IsCollisionLoaded +
                "; Position=" + prop.Position + "; Rotation=" + prop.Rotation + "; EsperaMs=" + Elapsed(recurso.Inicio));
            return true;
        }
        foreach (MesaProbe probe in recurso.MesaProbes)
        {
            if (pending >= 2) break;
            if (probe.Started) continue;
            probe.Handle = GTA.ShapeTest.StartTestLOSProbe(probe.From, probe.To,
                GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, null, (GTA.ShapeTestOptions)0);
            probe.Started = true; pending++;
        }
        return false;
    }
    // Before poses and throughout C1: cached rays only remain valid for the
    // same own collider and final measured transform. No setters in this guard.
    public void RequireSleepBenchGeometry(string codigo)
    {
        Recurso recurso = Ready(codigo);
        if (!recurso.BancoSonoMedidoC1 || recurso.Codigo != "BANCO_PRAIA" || recurso.ModeloNome != "prop_bench_02" ||
            !recurso.MesaMedida || !recurso.MesaSuperficiePronta || recurso.MesaProbes == null || recurso.MesaProbes.Length != 5)
            throw Error("C1 sleep bench requires five own surface proofs for exact bench02");
        RequireOwned(recurso);
        GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        RequireVector(prop.Position, "sleep bench position"); RequireVector(prop.Rotation, "sleep bench rotation");
        if (!prop.HasPhysics || !prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled || prop.IsAttached() ||
            (prop.Position - recurso.MesaPosition).Length() > 0.01f ||
            System.Math.Abs(prop.Rotation.X - recurso.MesaRotation.X) > 0.1f ||
            System.Math.Abs(prop.Rotation.Y - recurso.MesaRotation.Y) > 0.1f ||
            System.Math.Abs(MesaAngle(prop.Rotation.Z - recurso.MesaRotation.Z)) > 0.1f)
            throw Error("C1 sleep bench lost actual physics/collider or final measured transform");
        foreach (MesaProbe probe in recurso.MesaProbes)
            if (!probe.Done || !probe.OwnSurface || !MesaVectorFinite(probe.Point))
                throw Error("C1 sleep bench lost one of the five own surface proofs");
    }

    // C1 only: actual central Own ray anchors the unchanged body preparation.
    // The candidate rectangle may be asymmetric; its midpoint must not move the actor.
    public void GetSleepBenchMeasuredCenterXY(string codigo, out float x, out float y)
    {
        RequireSleepBenchGeometry(codigo);
        Recurso recurso = Ready(codigo); GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        GTA.Math.Vector3 local = prop.GetPositionOffset(recurso.MesaProbes[0].Point);
        RequireVector(local, "C1 actual central own seat point");
        x = local.X; y = local.Y;
    }

    public void GetSleepBenchMeasuredRectangle(string codigo, out GTA.Math.Vector3 min, out GTA.Math.Vector3 max)
    {
        RequireSleepBenchGeometry(codigo);
        Recurso recurso = Ready(codigo); GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        min = new GTA.Math.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new GTA.Math.Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (MesaProbe probe in recurso.MesaProbes)
        {
            GTA.Math.Vector3 local = prop.GetPositionOffset(probe.Point); RequireVector(local, "own sleep bench sample local");
            min.X = System.Math.Min(min.X, local.X); min.Y = System.Math.Min(min.Y, local.Y); min.Z = System.Math.Min(min.Z, local.Z);
            max.X = System.Math.Max(max.X, local.X); max.Y = System.Math.Max(max.Y, local.Y); max.Z = System.Math.Max(max.Z, local.Z);
        }
        if (min.X >= max.X || min.Y >= max.Y || max.Z - min.Z > 0.08f)
            throw Error("five own seat samples did not confirm a usable rectangle/plane");
    }

    // Exact own bench02 diagnostic only. Never grants Ready or pose support.
    private bool PollSleepBenchSeatGrid(Recurso recurso, ref int pending)
    {
        RequireOwned(recurso);
        if (!recurso.BancoSonoMedidoC1 || recurso.Codigo != "BANCO_PRAIA" || recurso.ModeloNome != "prop_bench_02" ||
            recurso.Pronto || recurso.MesaSuperficiePronta)
            throw Error("bench seat grid requires exact C1 own bench02 and Ready=false");
        GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        RequireVector(prop.Position, "bench grid position"); RequireVector(prop.Rotation, "bench grid rotation");
        if (!prop.HasPhysics || !prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled || prop.IsAttached())
            throw Error("bench seat grid lost own physics/collider or frozen free transform");
        if (Elapsed(recurso.Inicio) >= TimeoutMs) throw Error("bench seat grid did not finish inside original5s PREP deadline");
        if (recurso.BancoSeatGrid == null)
        {
            GTA.Math.Vector3 min, max;
            recurso.Modelo.GetDimensions(out min, out max);
            RequireVector(min, "bench grid actual bbox min"); RequireVector(max, "bench grid actual bbox max");
            if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("bench grid actual bbox invalid");
            recurso.MesaPosition = prop.Position; recurso.MesaRotation = prop.Rotation;
            recurso.BancoSeatGrid = new MesaProbe[35];
            for (int iy = 0; iy < 5; iy++) for (int ix = 0; ix < 7; ix++)
            {
                float x = min.X + (max.X - min.X) * (0.05f + ix * 0.15f);
                float y = min.Y + (max.Y - min.Y) * (0.05f + iy * 0.225f);
                MesaProbe probe = new MesaProbe();
                probe.From = prop.GetOffsetPosition(new GTA.Math.Vector3(x, y, max.Z + 0.25f));
                probe.To = prop.GetOffsetPosition(new GTA.Math.Vector3(x, y, min.Z - 0.10f));
                RequireVector(probe.From, "bench grid ray from"); RequireVector(probe.To, "bench grid ray to");
                recurso.BancoSeatGrid[iy * 7 + ix] = probe;
            }
            Log("BANCO_C1_GRID_INICIO", "Scene=EP10_C01; Codigo=" + recurso.Codigo + "; Handle=" + recurso.Handle +
                "; Model=" + recurso.ModeloNome + "; BoundsMin=" + min + "; BoundsMax=" + max +
                "; Position=" + prop.Position + "; Rotation=" + prop.Rotation +
                "; Rows=5; Columns=7; Rays=35; Fractions=.05..95; Flags=17; Options=0; MaximumPending=2; ModelCollisionLoaded=" +
                recurso.Modelo.IsCollisionLoaded + "; Physics=True; OwnCandidateOnly=True; ReadOnlyGrid=True; Ready=False; SeatPlaneProven=False; PoseSupportProven=False");
            return false;
        }
        if ((prop.Position - recurso.MesaPosition).Length() > 0.01f ||
            System.Math.Abs(prop.Rotation.X - recurso.MesaRotation.X) > 0.1f ||
            System.Math.Abs(prop.Rotation.Y - recurso.MesaRotation.Y) > 0.1f ||
            System.Math.Abs(MesaAngle(prop.Rotation.Z - recurso.MesaRotation.Z)) > 0.1f)
            throw Error("bench seat grid transform changed; do not re-anchor rays or support");
        int done = 0, horizontal = 0;
        for (int i = 0; i < recurso.BancoSeatGrid.Length; i++)
        {
            MesaProbe probe = recurso.BancoSeatGrid[i];
            if (probe.Started && !probe.Done)
            {
                GTA.ShapeTestResult measured;
                GTA.ShapeTestStatus status = probe.Handle.GetResult(out measured);
                if (status == GTA.ShapeTestStatus.NotReady) continue;
                pending--;
                if (status != GTA.ShapeTestStatus.Ready) throw Error("bench diagnostic grid shape result invalid; no support proof");
                GTA.Entity physical = null;
                bool own = measured.DidHit && measured.TryGetHitEntity(out physical) &&
                    !System.Object.ReferenceEquals(physical, null) && physical.Handle == recurso.Handle;
                GTA.Math.Vector3 point = measured.HitPosition, normal = measured.SurfaceNormal;
                GTA.Math.Vector3 local = GTA.Math.Vector3.Zero;
                bool valid = own && MesaVectorFinite(point) && MesaVectorFinite(normal) &&
                    System.Math.Abs(point.X - probe.From.X) <= 0.01f && System.Math.Abs(point.Y - probe.From.Y) <= 0.01f &&
                    point.Z <= probe.From.Z && point.Z >= probe.To.Z && System.Math.Abs(normal.Length() - 1f) <= 0.02f;
                if (valid) { local = prop.GetPositionOffset(point); valid = MesaVectorFinite(local); }
                probe.Point = point; probe.OwnSurface = valid && normal.Z >= 0.98f; probe.Done = true;
                Log("BANCO_C1_GRID_RAY", "Index=" + i + "; Row=" + (i / 7) + "; Column=" + (i % 7) +
                    "; Status=" + status + "; DidHit=" + measured.DidHit + "; HitEntity=" +
                    (System.Object.ReferenceEquals(physical, null) ? 0 : physical.Handle) + "; OwnEntity=" + recurso.Handle +
                    "; From=" + probe.From + "; To=" + probe.To + "; Point=" + point + "; LocalPoint=" + local +
                    "; LocalPointValid=" + valid + "; Normal=" + normal + "; OwnHorizontalSample=" + probe.OwnSurface +
                    "; Flags=17; Options=0; ReadOnly=True; Ready=False; ContinuousSurfaceProven=False; PoseSupportProven=False");
            }
            if (probe.Done) { done++; if (probe.OwnSurface) horizontal++; }
        }
        if (done == 35)
        {
            Log("BANCO_C1_GRID_CONCLUIDO", "Scene=EP10_C01; OwnEntity=" + recurso.Handle + "; Samples=35; OwnHorizontalSamples=" + horizontal +
                "; BBoxNotSolidAssumption=True; GridRectangleNotBodyProof=True; FutureFiveOwnSupportsRequired=True; Ready=False; SeatPlaneProven=False; PoseSupportProven=False; Capture=False");
            throw Error("C1 seat grid diagnostic complete; read actual samples before five own support proofs. Ready remains false; no pose or capture.");
        }
        foreach (MesaProbe probe in recurso.BancoSeatGrid)
        {
            if (pending >= 2) break;
            if (probe.Started) continue;
            probe.Handle = GTA.ShapeTest.StartTestLOSProbe(probe.From, probe.To,
                GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, null, (GTA.ShapeTestOptions)0);
            probe.Started = true; pending++;
        }
        return false;
    }

    // Exact owned C8 dining-table diagnostic only. Never grants Ready or pose support.
    private bool PollLargeTableC8SurfaceGrid(Recurso recurso, ref int pending)
    {
        RequireOwned(recurso);
        if (!recurso.MesaGrandeMedidaC8 || recurso.Codigo != "MESA_JANTAR" || recurso.ModeloNome != "v_res_m_dinetble_replace" ||
            recurso.Pronto || recurso.MesaSuperficiePronta)
            throw Error("C8 table surface grid requires exact C1 own C8 table and Ready=false");
        GTA.Prop prop = (GTA.Prop)recurso.Entidade;
        RequireVector(prop.Position, "C8 table grid position"); RequireVector(prop.Rotation, "C8 table grid rotation");
        if (!prop.HasPhysics || !prop.IsVisible || !prop.IsPositionFrozen || !prop.IsCollisionEnabled || prop.IsAttached())
            throw Error("C8 table surface grid lost own physics/collider or frozen free transform");
        if (Elapsed(recurso.Inicio) >= TimeoutMs) throw Error("C8 table surface grid did not finish inside original5s PREP deadline");
        if (recurso.MesaC8SurfaceGrid == null)
        {
            GTA.Math.Vector3 min, max;
            recurso.Modelo.GetDimensions(out min, out max);
            RequireVector(min, "C8 table grid actual bbox min"); RequireVector(max, "C8 table grid actual bbox max");
            if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw Error("C8 table grid actual bbox invalid");
            recurso.MesaPosition = prop.Position; recurso.MesaRotation = prop.Rotation;
            recurso.MesaC8SurfaceGrid = new MesaProbe[25];
            for (int iy = 0; iy < 5; iy++) for (int ix = 0; ix < 5; ix++)
            {
                float x = min.X + (max.X - min.X) * (0.05f + ix * 0.225f);
                float y = min.Y + (max.Y - min.Y) * (0.05f + iy * 0.225f);
                MesaProbe probe = new MesaProbe();
                probe.From = prop.GetOffsetPosition(new GTA.Math.Vector3(x, y, max.Z + 0.25f));
                probe.To = prop.GetOffsetPosition(new GTA.Math.Vector3(x, y, min.Z - 0.10f));
                RequireVector(probe.From, "C8 table grid ray from"); RequireVector(probe.To, "C8 table grid ray to");
                recurso.MesaC8SurfaceGrid[iy * 5 + ix] = probe;
            }
            Log("MESA_C8_GRID_INICIO", "Scene=EP15_C08; Codigo=" + recurso.Codigo + "; Handle=" + recurso.Handle +
                "; Model=" + recurso.ModeloNome + "; BoundsMin=" + min + "; BoundsMax=" + max +
                "; Position=" + prop.Position + "; Rotation=" + prop.Rotation +
                "; Rows=5; Columns=5; Rays=25; Fractions=.05..95; Flags=17; Options=0; MaximumPending=2; ModelCollisionLoaded=" +
                recurso.Modelo.IsCollisionLoaded + "; Physics=True; OwnCandidateOnly=True; ReadOnlyGrid=True; Ready=False; TablePlaneProven=False; SupportRectangleProven=False");
            return false;
        }
        if ((prop.Position - recurso.MesaPosition).Length() > 0.01f ||
            System.Math.Abs(prop.Rotation.X - recurso.MesaRotation.X) > 0.1f ||
            System.Math.Abs(prop.Rotation.Y - recurso.MesaRotation.Y) > 0.1f ||
            System.Math.Abs(MesaAngle(prop.Rotation.Z - recurso.MesaRotation.Z)) > 0.1f)
            throw Error("C8 table surface grid transform changed; do not re-anchor rays or support");
        int done = 0, horizontal = 0;
        for (int i = 0; i < recurso.MesaC8SurfaceGrid.Length; i++)
        {
            MesaProbe probe = recurso.MesaC8SurfaceGrid[i];
            if (probe.Started && !probe.Done)
            {
                GTA.ShapeTestResult measured;
                GTA.ShapeTestStatus status = probe.Handle.GetResult(out measured);
                if (status == GTA.ShapeTestStatus.NotReady) continue;
                pending--;
                if (status != GTA.ShapeTestStatus.Ready) throw Error("C8 diagnostic grid shape result invalid; no support proof");
                GTA.Entity physical = null;
                bool own = measured.DidHit && measured.TryGetHitEntity(out physical) &&
                    !System.Object.ReferenceEquals(physical, null) && physical.Handle == recurso.Handle;
                GTA.Math.Vector3 point = measured.HitPosition, normal = measured.SurfaceNormal;
                GTA.Math.Vector3 local = GTA.Math.Vector3.Zero;
                bool valid = own && MesaVectorFinite(point) && MesaVectorFinite(normal) &&
                    System.Math.Abs(point.X - probe.From.X) <= 0.01f && System.Math.Abs(point.Y - probe.From.Y) <= 0.01f &&
                    point.Z <= probe.From.Z && point.Z >= probe.To.Z && System.Math.Abs(normal.Length() - 1f) <= 0.02f;
                if (valid) { local = prop.GetPositionOffset(point); valid = MesaVectorFinite(local); }
                probe.Point = point; probe.OwnSurface = valid && normal.Z >= 0.98f; probe.Done = true;
                Log("MESA_C8_GRID_RAY", "Index=" + i + "; Row=" + (i / 5) + "; Column=" + (i % 5) +
                    "; Status=" + status + "; DidHit=" + measured.DidHit + "; HitEntity=" +
                    (System.Object.ReferenceEquals(physical, null) ? 0 : physical.Handle) + "; OwnEntity=" + recurso.Handle +
                    "; From=" + probe.From + "; To=" + probe.To + "; Point=" + point + "; LocalPoint=" + local +
                    "; LocalPointValid=" + valid + "; Normal=" + normal + "; OwnHorizontalSample=" + probe.OwnSurface +
                    "; Flags=17; Options=0; ReadOnly=True; Ready=False; ContinuousSurfaceProven=False; SupportRectangleProven=False");
            }
            if (probe.Done) { done++; if (probe.OwnSurface) horizontal++; }
        }
        if (done == 25)
        {
            Log("MESA_C8_GRID_CONCLUIDO", "Scene=EP15_C08; OwnEntity=" + recurso.Handle + "; Samples=25; OwnHorizontalSamples=" + horizontal +
                "; BBoxNotSolidAssumption=True; GridRectangleNotSolidProof=True; FutureFiveOwnSupportsRequired=True; Ready=False; TablePlaneProven=False; SupportRectangleProven=False; Capture=False");
            throw Error("C8 table grid diagnostic complete; read actual samples before five own support proofs. Ready remains false; no pose or capture.");
        }
        foreach (MesaProbe probe in recurso.MesaC8SurfaceGrid)
        {
            if (pending >= 2) break;
            if (probe.Started) continue;
            probe.Handle = GTA.ShapeTest.StartTestLOSProbe(probe.From, probe.To,
                GTA.IntersectFlags.Map | GTA.IntersectFlags.Objects, null, (GTA.ShapeTestOptions)0);
            probe.Started = true; pending++;
        }
        return false;
    }

    private static float MesaAngle(float value)
    { while (value > 180f) value -= 360f; while (value < -180f) value += 360f; return value; }
    private static bool MesaVectorFinite(GTA.Math.Vector3 value)
    { return Finite(value.X) && Finite(value.Y) && Finite(value.Z); }

    public GTA.Prop GetProp(string codigo)
    {
        Recurso recurso = Ready(codigo);
        if (recurso.Veiculo) throw Error("O recurso solicitado é um veículo: " + codigo);
        return (GTA.Prop)recurso.Entidade;
    }

    public GTA.Vehicle GetVehicle(string codigo)
    {
        Recurso recurso = Ready(codigo);
        if (!recurso.Veiculo) throw Error("O recurso solicitado é um objeto: " + codigo);
        return (GTA.Vehicle)recurso.Entidade;
    }

    public void AttachPhone(string codigo, GTA.Ped ator)
    {
        // Ponto inicial neutro, ainda sujeito à calibração visual do modelo e da animação.
        AttachPhone(codigo, ator, GTA.Math.Vector3.Zero, GTA.Math.Vector3.Zero);
    }

    public void AttachPhone(string codigo, GTA.Ped ator, GTA.Math.Vector3 offset, GTA.Math.Vector3 rotacao)
    {
        Recurso recurso = Ready(codigo);
        if (!recurso.Telefone) throw Error("AttachPhone aceita somente prop_npc_phone_02.");
        if (System.Object.ReferenceEquals(ator, null) || !ator.Exists()) throw Error("Ator do telefone não existe.");
        RequireVector(offset, "ajuste do telefone");
        RequireVector(rotacao, "rotação do telefone");
        if (offset.Length() > 0.5f) throw Error("Ajuste do telefone deve ficar até meio metro da mão.");
        GTA.PedBone hand = ator.Bones[GTA.Bone.PHRightHand];
        if (System.Object.ReferenceEquals(hand, null) || !hand.IsValid) throw Error("O ator não tem osso válido da mão direita.");
        recurso.Entidade.IsPositionFrozen = false;
        recurso.Entidade.IsCollisionEnabled = false;
        recurso.Entidade.AttachTo(hand, offset, rotacao, false, false, false, false,
                                 GTA.EulerRotationOrder.YXZ, true, false);
        recurso.Entidade.IsVisible = true;
        Log("CELULAR_ANEXADO", "Codigo=" + codigo + "; Ator=" + ator.Handle + "; Osso=PHRightHand");
    }

    public void HidePhone(string codigo)
    {
        Recurso recurso = Ready(codigo);
        if (!recurso.Telefone) throw Error("HidePhone aceita somente o telefone próprio.");
        Restore(recurso);
        Log("CELULAR_OCULTO", "Codigo=" + codigo);
    }

    // Antes de F7/Delete: chamar após interromper tarefas dos atores, fora da ação.
    public void Reset()
    {
        RequireActive();
        if (!IsReady) throw Error("Aguarde o preparo dos recursos antes de reiniciar a tomada.");
        foreach (Recurso recurso in recursos.Values)
        {
            RequireOwned(recurso);
            RequireEmptyVehicle(recurso);
            Restore(recurso);
        }
        Log("RECURSOS_REINICIADOS", "Quantidade=" + recursos.Count);
    }

    private void Restore(Recurso recurso)
    {
        RequireOwned(recurso);
        recurso.Entidade.Detach(false, false);
        recurso.Entidade.IsPositionFrozen = true;
        recurso.Entidade.Velocity = GTA.Math.Vector3.Zero;
        recurso.Entidade.PositionNoOffset = recurso.Posicao;
        recurso.Entidade.Rotation = recurso.Rotacao;
        recurso.Entidade.IsCollisionEnabled = !recurso.Telefone;
        recurso.Entidade.IsVisible = !recurso.Telefone;
        recurso.Entidade.IsPositionFrozen = recurso.Telefone || recurso.Congelado;
    }

    public void Cleanup()
    {
        System.Collections.Generic.List<string> removidos = new System.Collections.Generic.List<string>();
        System.Collections.Generic.List<string> falhas = new System.Collections.Generic.List<string>();
        foreach (Recurso recurso in recursos.Values)
        {
            try
            {
                if (recurso.Handle != 0 && GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, recurso.Handle))
                {
                    RequireOwned(recurso);
                    RequireEmptyVehicle(recurso);
                    recurso.Entidade.Detach(false, false);
                    recurso.Entidade.Delete();
                    if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, recurso.Handle))
                    {
                        // Delete zera o wrapper mesmo se o motor conservar a entidade.
                        recurso.Entidade = GTA.Entity.FromHandle(recurso.Handle);
                        throw Error("O jogo manteve o recurso depois de Delete: " + recurso.Codigo);
                    }
                }
                removidos.Add(recurso.Codigo);
            }
            catch (System.Exception error)
            {
                falhas.Add(recurso.Codigo + ": " + error.Message);
                Log("ERRO_LIMPEZA_RECURSO", recurso.Codigo + "; " + error.Message);
            }
        }
        foreach (string codigo in removidos) recursos.Remove(codigo);
        if (recursos.Count == 0)
        {
            foreach (int hash in modelosPedidos)
            {
                try { new GTA.Model(hash).MarkAsNoLongerNeeded(); }
                catch (System.Exception error) { Log("ERRO_LIBERAR_MODELO", error.Message); }
            }
            modelosPedidos.Clear();
        }
        Log("RECURSOS_LIMPOS", "Removidos=" + removidos.Count + "; Pendentes=" + recursos.Count);
        if (falhas.Count != 0) throw Error("Limpeza incompleta; recursos preservados para nova tentativa. " + string.Join(" | ", falhas.ToArray()));
    }

    public void Dispose()
    {
        Cleanup();
        disposed = true;
    }

    private Recurso Ready(string codigo)
    {
        RequireActive();
        Recurso recurso;
        if (codigo == null || !recursos.TryGetValue(codigo, out recurso)) throw Error("Recurso desconhecido: " + codigo);
        if (!recurso.Pronto) throw Error("Recurso ainda em preparo: " + codigo);
        RequireOwned(recurso);
        return recurso;
    }

    private static void RequireOwned(Recurso recurso)
    {
        if (recurso.Handle != 0 &&
            (System.Object.ReferenceEquals(recurso.Entidade, null) || recurso.Entidade.Handle == 0) &&
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, recurso.Handle))
            recurso.Entidade = GTA.Entity.FromHandle(recurso.Handle);
        if (recurso.Handle == 0 || System.Object.ReferenceEquals(recurso.Entidade, null) ||
            !GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, recurso.Handle) ||
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_TYPE, recurso.Handle) != recurso.Tipo ||
            GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_ENTITY_MODEL, recurso.Handle) != recurso.Modelo.Hash ||
            recurso.Entidade.Handle != recurso.Handle || recurso.Entidade.MemoryAddress != recurso.Endereco)
            throw Error("Identidade do recurso mudou ou ele foi removido: " + recurso.Codigo);
    }

    private static void RequireEmptyVehicle(Recurso recurso)
    {
        if (!recurso.Veiculo) return;
        GTA.Vehicle vehicle = (GTA.Vehicle)recurso.Entidade;
        if (vehicle.Occupants.Length != 0)
            throw Error("Retire os ocupantes do veículo próprio antes de reiniciar/limpar: " + recurso.Codigo);
    }

    private void RequireActive()
    {
        if (disposed) throw Error("O helper de recursos já foi encerrado.");
    }

    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static void RequireVector(GTA.Math.Vector3 value, string label)
    {
        if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z)) throw Error("A " + label + " precisa ter valores finitos.");
    }
    private static uint Elapsed(int start) { return unchecked((uint)(GTA.Game.GameTime - start)); }
    private static System.InvalidOperationException Error(string message)
    {
        return new System.InvalidOperationException("Recursos: " + message);
    }
    private void Log(string code, string message)
    {
        if (log == null) return;
        try { log(code, message); } catch (System.Exception) { }
    }
}

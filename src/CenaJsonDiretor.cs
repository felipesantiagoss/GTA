// Concatenado ao .3.cs publicado: sem diretivas using e sem outra classe Script.
// JSON editável valida referências; XML de marcas e gestos permanece canônico.
public static class CenaJsonDiretor
{
    // Technical85 only; called after its fixed contract and before Ready.
    public static string LoadWriting85Scenario(string path)
    {
        const string label = "trial-escrita85";
        var config = Obj(ReadJson(path, label), label, "versao,cenario", "");
        Int(config["versao"], label + ".versao", 1, 1);
        return Choice(config["cenario"], label + ".cenario", "CODE_HUMAN_MEDIC_TIME_OF_DEATH,WORLD_HUMAN_CLIPBOARD,FULLTRACK_NOTE_TAKING_01,FULLTRACK_NOTE_TAKING_02,FULLTRACK_WEED");
    }

    public sealed class Resultado
    {
        public System.Xml.XmlDocument Document;
        public string Id;
        public string Encenacao;
        public string CameraObjeto;
        public float CameraFovObjeto = 30f, CameraElevacaoObjeto = 45f;
        public int Episode, Number;
        public float CleanupRadius;
        public System.Collections.Generic.List<Acao> Actions;
        public System.Collections.Generic.List<Corte> Cuts;
        public System.Collections.Generic.Dictionary<string, VisualDiretor.Visual> Visuais;
        public System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> Direcao;
        public System.Collections.Generic.List<InterpretacaoDiretor.ActorSpec> Interpretacao;
        public System.Collections.Generic.List<ObjetosSerieDiretor.PropSpec> Objetos;
        public System.Collections.Generic.List<PosesSerieDiretor.PoseSpec> Poses;
        public System.Collections.Generic.List<PosesSerieDiretor.TransitionSpec> TransicoesPose;
        public System.Collections.Generic.List<VeiculosSerieDiretor.VehicleSpec> Veiculos;
        public System.Collections.Generic.List<PortasSerieDiretor.DoorSpec> Portas;
        public bool SaidaAgua;
        public bool EmpurraoAviao;
        public bool ArmarioModular;
        public System.Collections.Generic.Dictionary<string, float> RotacoesIniciais;
    }

    public sealed class Acao
    {
        public string Tipo, Personagem, Alvo, Animacao;
        public int InicioMs, DuracaoMs;
        public float Direita, Frente, Altura, Velocidade;
    }

    public sealed class Corte
    {
        public int InicioMs;
        public string Plano, Personagem;
    }

    public sealed class Fila
    {
        public string Nome;
        public int Episode;
        public System.Collections.Generic.List<string> Arquivos;
    }

    private sealed class CatalogActor
    {
        public string Name, Model, VisualFile, VisualSha;
        public bool Ready;
    }

    private sealed class SpeechInterval
    {
        public string Actor;
        public int Start, End;
    }

    private sealed class BodyResource
    {
        public string Type, Dictionary, Clip, File, Sha;
        public int Line;
    }

    private sealed class BodySource
    {
        public string File, Sha;
        public System.Collections.Generic.Dictionary<int, string> Lines = new System.Collections.Generic.Dictionary<int, string>();
    }

    private sealed class PropResource
    {
        public string Type, Model, File, Sha;
        public int Line;
    }

    private sealed class JsonNumber
    {
        public string Raw;
        public double Value;
        public bool Integer;
    }

    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;
    private const int MaxChars = 100000;
    private const int MaxBytes = 1048576;

    public static Fila LoadFila(string path, string catalogDirectory, float defaultRadius)
    {
        if (System.String.IsNullOrWhiteSpace(path) || System.String.IsNullOrWhiteSpace(catalogDirectory))
            throw Error("fila", "informe o arquivo da fila e a pasta dos catálogos");
        var root = Obj(ReadJson(path, "fila"), "fila", "versao,episodio,nome,cenas", "");
        Int(root["versao"], "fila.versao", 1, 1);
        int episode = Int(root["episodio"], "fila.episodio", 0, 15);
        string name = Text(root["nome"], "fila.nome", 1, 100);
        string expected = episode == 0 ? "fila_teste.json" : "fila_EP" + episode.ToString("00", Invariant) + ".json";
        bool pilotQueue = episode == 1 && System.IO.Path.GetFileName(path) == "fila_piloto.json";
        if (System.IO.Path.GetFileName(path) != expected && !(episode == 0 && System.IO.Path.GetFileName(path) == "fila_elenco.json") && !pilotQueue)
            throw Error("fila.episodio", "o arquivo precisa se chamar " + expected);
        var entries = Array(root["cenas"], "fila.cenas", 1, 20);
        if (pilotQueue && (entries.Count != 1 || !(entries[0] is string) || (string)entries[0] != "EP01_C08.json"))
            throw Error("fila.cenas", "fila_piloto permite somente EP01_C08.json");
        var files = new System.Collections.Generic.List<string>();
        var unique = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        string pattern = episode == 0
            ? "\\A(?:teste_json|teste_coreografia|teste_fila_(?:0[1-9]|[1-9][0-9])|teste_elenco_(?:pobre|rica))\\.json\\z"
            : "\\AEP" + episode.ToString("00", Invariant) + "_C(?:0[1-9]|[1-9][0-9])\\.json\\z";
        for (int i = 0; i < entries.Count; i++)
        {
            string field = "fila.cenas[" + i + "]";
            string file = Text(entries[i], field, 1, 53);
            if (!unique.Add(file)) throw Error(field, "esse arquivo aparece mais de uma vez na fila, inclusive com variação de maiúsculas/minúsculas");
            if (!System.Text.RegularExpressions.Regex.IsMatch(file, pattern))
                throw Error(field, episode == 0
                    ? "use somente teste_json.json ou teste_fila_NN.json (01–99), sem caminho"
                    : "use somente EP" + episode.ToString("00", Invariant) + "_Cnn.json (cena 01–99), sem caminho");
            files.Add(file);
        }
        string directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        for (int i = 0; i < files.Count; i++)
        {
            string field = "fila.cenas[" + i + "]";
            Resultado scene;
            try { scene = Load(System.IO.Path.Combine(directory, files[i]), catalogDirectory, defaultRadius); }
            catch (System.InvalidOperationException ex) { throw Error(field, files[i] + ": " + ex.Message); }
            if (scene.Episode != episode)
                throw Error(field, "o episódio da cena " + files[i] + " precisa corresponder ao episódio da fila");
        }
        return new Fila { Nome = name, Episode = episode, Arquivos = files };
    }

    public static Resultado Load(string path, string catalogDirectory, float defaultRadius)
    {
        if (System.Single.IsNaN(defaultRadius) || System.Single.IsInfinity(defaultRadius) || defaultRadius < 5f || defaultRadius > 100f)
            throw Error("ambiente.raioLimpeza", "o padrão do INI deve ficar entre 5 e 100 metros");
        if (System.String.IsNullOrWhiteSpace(path) || System.String.IsNullOrWhiteSpace(catalogDirectory))
            throw Error("cena", "informe o arquivo da cena e a pasta dos catálogos");
        string morph86HashBeforeRead = System.IO.Path.GetFileName(path) == "teste_fila_86.json" &&
            System.IO.File.Exists(System.IO.Path.Combine(catalogDirectory, "trial_morph86.json")) ?
            MorfologiaPapel86Diretor.EvidenceHash(path, 1048576) : null;
        var scene = Obj(ReadJson(path, "cena"), "cena", "versao,id,episodio,numero,nome,ambiente,elenco,camera,roteiro", "acoes,encenacao,direcao,interpretacao,objetos,poses,transicoesPose,veiculos,portas,saidaAgua,empurraoAviao,armarioModular");
        Int(Required(scene, "versao", "cena"), "cena.versao", 1, 1);
        string id = Text(Required(scene, "id", "cena"), "cena.id", 1, 48);
        int episode = Int(Required(scene, "episodio", "cena"), "cena.episodio", 0, 15);
        int number = Int(Required(scene, "numero", "cena"), "cena.numero", 0, 99);
        if (id == "teste_json" || id == "teste_coreografia" || id == "VINHETA_01" || id == "teste_elenco_pobre" || id == "teste_elenco_rica" || System.Text.RegularExpressions.Regex.IsMatch(id, "\\Ateste_fila_(?:0[1-9]|[1-9][0-9])\\z"))
        {
            if (episode != 0 || number != 0) throw Error("cena.id", "cena técnica exige episodio=0 e numero=0");
        }
        else if (id == "EP01_EXTRA_IRACI_BAR" || id == "EP01_EXTRA_DINJER_DEDO")
        {
            if (episode != 1 || number != 0) throw Error("cena.id", "extra autorizado exige episodio=1 e numero=0");
        }
        else
        {
            if (episode < 1 || number < 1 || id != "EP" + episode.ToString("00", Invariant) + "_C" + number.ToString("00", Invariant))
                throw Error("cena.id", "use teste_json, teste_fila_NN (01–99), VINHETA_01 ou EPnn_Cnn correspondente ao episódio 01–15 e à cena 01–99");
        }
        if (System.IO.Path.GetFileName(path) != id + ".json")
            throw Error("cena.id", "o arquivo precisa se chamar " + id + ".json");
        string name = Text(Required(scene, "nome", "cena"), "cena.nome", 1, 100);
        string staging = scene.ContainsKey("encenacao") ? Choice(scene["encenacao"], "cena.encenacao", "pedido_v1,ensaio_coreografia_v1,serie_v1") : "";
        if (staging == "serie_v1" && !scene.ContainsKey("direcao")) throw Error("cena.direcao", "serie_v1 exige a lista de direção, mesmo vazia");
        if (staging != "serie_v1" && scene.ContainsKey("direcao")) throw Error("cena.direcao", "permitida somente com encenacao=serie_v1");
        if (staging != "serie_v1" && scene.ContainsKey("interpretacao")) throw Error("cena.interpretacao", "permitida somente com encenacao=serie_v1");
        if (staging != "serie_v1" && scene.ContainsKey("objetos")) throw Error("cena.objetos", "permitida somente com encenacao=serie_v1");
        if (staging != "serie_v1" && scene.ContainsKey("poses")) throw Error("cena.poses", "permitida somente com encenacao=serie_v1");
        if (staging != "serie_v1" && (scene.ContainsKey("transicoesPose") || scene.ContainsKey("veiculos") || scene.ContainsKey("portas") || scene.ContainsKey("saidaAgua") || scene.ContainsKey("armarioModular"))) throw Error("cena.encenacao", "extensões físicas exigem serie_v1");
        if (id == "teste_coreografia" && staging != "ensaio_coreografia_v1") throw Error("cena.encenacao", "teste_coreografia exige ensaio_coreografia_v1");
        var characters = ReadCharacters(System.IO.Path.Combine(catalogDirectory, "personagens.json"));
        var places = ReadPlaces(System.IO.Path.Combine(catalogDirectory, "locais.json"));
        var animations = ReadAnimations(catalogDirectory);

        var environment = Obj(Required(scene, "ambiente", "cena"), "cena.ambiente", "local,hora,clima", "raioLimpeza,distanciaFrente");
        string location = Code(Required(environment, "local", "cena.ambiente"), "cena.ambiente.local");
        if (!places.Contains(location)) throw Error("cena.ambiente.local", "o código " + location + " não existe no catálogo de locais");
        string time = Text(Required(environment, "hora", "cena.ambiente"), "cena.ambiente.hora", 5, 5);
        if (!System.Text.RegularExpressions.Regex.IsMatch(time, "\\A([01][0-9]|2[0-3]):[0-5][0-9]\\z"))
            throw Error("cena.ambiente.hora", "use HH:mm, entre 00:00 e 23:59");
        string weather = Weather(Text(Required(environment, "clima", "cena.ambiente"), "cena.ambiente.clima", 1, 20));
        float radius = OptionalNumber(environment, "raioLimpeza", "cena.ambiente", 5, 100, defaultRadius);
        float distance = OptionalNumber(environment, "distanciaFrente", "cena.ambiente", 1, 10, 4);

        var cast = Array(Required(scene, "elenco", "cena"), "cena.elenco", 0, 5);
        var actorIds = new System.Collections.Generic.List<string>();
        var castEntries = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
        var visuals = new System.Collections.Generic.Dictionary<string, VisualDiretor.Visual>(System.StringComparer.Ordinal);
        var initialRotations = new System.Collections.Generic.Dictionary<string, float>(System.StringComparer.Ordinal);
        for (int i = 0; i < cast.Count; i++)
        {
            string field = "cena.elenco[" + i + "]";
            var entry = Obj(cast[i], field, "personagem,marca", "olharPara,rotacao");
            string code = Code(Required(entry, "personagem", field), field + ".personagem");
            if (actorIds.Contains(code)) throw Error(field + ".personagem", "esse personagem aparece mais de uma vez no elenco");
            if (entry.ContainsKey("rotacao")) initialRotations.Add(code.ToLowerInvariant(), (float)Numeric(entry["rotacao"], field + ".rotacao", -180, 180));
            if (!characters.ContainsKey(code)) throw Error(field + ".personagem", "o personagem " + code + " não existe no catálogo");
            if (!characters[code].Ready) throw Error(field + ".personagem", "o visual de " + code + " ainda não está pronto/aprovado; preserve o figurino pendente e use os atores de teste");
            CatalogActor character = characters[code];
            bool freemode = character.Model == "mp_f_freemode_01" || character.Model == "mp_m_freemode_01";
            if (freemode && character.VisualFile == null)
                throw Error(field + ".personagem", "personagem freemode pronto exige visualArquivo e visualSHA256 aprovados");
            if (character.VisualFile != null)
            {
                string visualPath = System.IO.Path.Combine(catalogDirectory, "visuais", character.VisualFile);
                RequireVisualPath(visualPath);
                VisualDiretor.Visual visual = VisualDiretor.Load(visualPath, character.Model);
                if (visual.Sha256 != character.VisualSha)
                    throw Error(field + ".personagem", "SHA256 do visual difere do catálogo; preservar a aprovação pendente");
                visuals.Add(code.ToLowerInvariant(), visual);
            }
            actorIds.Add(code);
            castEntries.Add(entry);
        }

        var camera = Obj(Required(scene, "camera", "cena"), "cena.camera", "modo,planoInicial,personagem",
            "planoFala,planoNarracao,planoFinal,inverterEixo,transicaoSuave,ajustes,cortes,objeto,fovObjeto,elevacaoObjetoGraus");
        string mode = Choice(Required(camera, "modo", "cena.camera"), "cena.camera.modo", "automatico,programado");
        string initialShot = Shot(Required(camera, "planoInicial", "cena.camera"), "cena.camera.planoInicial");
        string cameraActor = CameraActor(Required(camera, "personagem", "cena.camera"), "cena.camera.personagem", actorIds);
        string speechShot = OptionalShot(camera, "planoFala", "MEDIA");
        string defaultWideShot = actorIds.Count >= 2 ? "DOIS" : actorIds.Count == 1 ? "MEDIA" : "LONGE";
        string narrationShot = OptionalShot(camera, "planoNarracao", defaultWideShot);
        string finalShot = OptionalShot(camera, "planoFinal", defaultWideShot);
        EnvironmentShot(initialShot, "cena.camera.planoInicial", actorIds);
        EnvironmentShot(speechShot, "cena.camera.planoFala", actorIds);
        EnvironmentShot(narrationShot, "cena.camera.planoNarracao", actorIds);
        EnvironmentShot(finalShot, "cena.camera.planoFinal", actorIds);
        bool invert = OptionalBool(camera, "inverterEixo", "cena.camera", false);
        bool smooth = OptionalBool(camera, "transicaoSuave", "cena.camera", false);
        string cameraObject = camera.ContainsKey("objeto") ? Code(camera["objeto"], "cena.camera.objeto") : null;
        if (cameraObject == null && (camera.ContainsKey("fovObjeto") || camera.ContainsKey("elevacaoObjetoGraus")))
            throw Error("cena.camera.objeto", "fov/elevacao do detalhe exigem objeto próprio declarado");
        if (cameraObject != null && mode != "automatico") throw Error("cena.camera.objeto", "detalhe de objeto ocupa a tomada inteira no modo automatico");
        float objectFov = OptionalNumber(camera, "fovObjeto", "cena.camera", 15, 55, 30);
        float objectElevation = OptionalNumber(camera, "elevacaoObjetoGraus", "cena.camera", 10, 80, 45);
        float side = 0, height = 0, closer = 0;
        if (camera.ContainsKey("ajustes"))
        {
            var adjustments = Obj(camera["ajustes"], "cena.camera.ajustes", "", "lado,altura,aproximar");
            side = OptionalNumber(adjustments, "lado", "cena.camera.ajustes", -2, 2, 0);
            height = OptionalNumber(adjustments, "altura", "cena.camera.ajustes", -2, 2, 0);
            closer = OptionalNumber(adjustments, "aproximar", "cena.camera.ajustes", -2, 2, 0);
        }
        if (mode == "automatico" && camera.ContainsKey("cortes"))
            throw Error("cena.camera.cortes", "cortes só são permitidos no modo programado");

        var document = new System.Xml.XmlDocument();
        document.XmlResolver = null;
        document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));
        var root = document.CreateElement("Cena");
        document.AppendChild(root);
        Set(root, "versao", "1", "id", id.ToLowerInvariant(), "nome", name);
        var xmlEnvironment = Element(root, "Ambiente");
        Set(xmlEnvironment, "hora", System.Int32.Parse(time.Substring(0, 2), Invariant).ToString(Invariant),
            "minuto", System.Int32.Parse(time.Substring(3, 2), Invariant).ToString(Invariant), "clima", weather,
            "densidadeTransito", "0", "densidadePedestres", "0", "bloquearPolicia", "true", "limparArea", "true");
        Set(Element(root, "Local"), "referencia", "marcado", "id", location, "distanciaFrente", Number(distance));
        var xmlCast = Element(root, "Personagens");
        var positions = new System.Collections.Generic.List<float[]>();
        for (int i = 0; i < castEntries.Count; i++)
        {
            string field = "cena.elenco[" + i + "]";
            var entry = castEntries[i];
            float[] mark = Mark(Required(entry, "marca", field), field + ".marca");
            for (int j = 0; j < positions.Count; j++)
                if (System.Math.Abs(mark[0] - positions[j][0]) < 0.5f && System.Math.Abs(mark[1] - positions[j][1]) < 0.5f)
                    throw Error(field + ".marca", "os personagens estão próximos demais; separe suas marcas horizontais");
            positions.Add(mark);
            string looking = !entry.ContainsKey("olharPara") || entry["olharPara"] == null ? null : Actor(entry["olharPara"], field + ".olharPara", actorIds, false);
            if (looking == actorIds[i]) throw Error(field + ".olharPara", "escolha outro personagem do elenco");
            CatalogActor resource = characters[actorIds[i]];
            var xmlActor = Element(xmlCast, "Personagem");
            Set(xmlActor, "id", actorIds[i].ToLowerInvariant(), "nome", resource.Name,
                "modelo", resource.Model, "direita", Number(mark[0]), "frente", Number(mark[1]), "altura", Number(mark[2]));
            if (looking != null) xmlActor.SetAttribute("olharPara", looking.ToLowerInvariant());
        }
        var xmlCamera = Element(root, "Camera");
        Set(xmlCamera, "plano", initialShot,
            "inverterEixo", Boolean(invert), "deslocLado", Number(side), "deslocAltura", Number(height), "aproximar", Number(closer),
            "transicaoSuave", Boolean(smooth), "automatica", Boolean(mode == "automatico"), "planoFala", speechShot,
            "planoNarracao", narrationShot, "planoFinal", finalShot);
        if (cameraActor.Length > 0) xmlCamera.SetAttribute("personagem", cameraActor.ToLowerInvariant());

        var script = Array(Required(scene, "roteiro", "cena"), "cena.roteiro", 0, 20);
        System.Xml.XmlElement xmlScript = script.Count == 0 ? null : Element(root, "Roteiro");
        var speechIntervals = new System.Collections.Generic.List<SpeechInterval>();
        int totalMs = 0;
        for (int i = 0; i < script.Count; i++)
        {
            string field = "cena.roteiro[" + i + "]";
            var part = Obj(script[i], field, "tipo,duracaoSegundos", "texto,plano,personagem,gesto,narrador");
            string kind = Choice(Required(part, "tipo", field), field + ".tipo", "fala,narracao,pausa");
            string text = kind == "pausa" ? null : Text(Required(part, "texto", field), field + ".texto", 1, 350);
            bool measuredCfmV4Pause33 = id == "teste_fila_87" && kind == "pausa" && script.Count == 1 && Numeric(Required(part, "duracaoSegundos", field), field + ".duracaoSegundos", 0.5, 180) == 33;
            int duration = Ms(Required(part, "duracaoSegundos", field), field + ".duracaoSegundos", 0.5, id == "EP01_C01" && kind == "pausa" ? 180 : measuredCfmV4Pause33 ? 33 : 30);
            if (totalMs + duration > 180000) throw Error(field + ".duracaoSegundos", "a soma do roteiro ultrapassa 180 segundos");
            var xmlPart = Element(xmlScript, kind == "fala" ? "Fala" : kind == "pausa" ? "Pausa" : "Narracao");
            xmlPart.SetAttribute("duracaoSegundos", Seconds(duration));
            if (part.ContainsKey("plano"))
            {
                string shot = Shot(part["plano"], field + ".plano");
                EnvironmentShot(shot, field + ".plano", actorIds);
                xmlPart.SetAttribute("plano", shot);
            }
            if (kind == "fala")
            {
                Prohibit(part, "narrador", field);
                string speaker = Actor(Required(part, "personagem", field), field + ".personagem", actorIds, false);
                string gesture = Choice(Required(part, "gesto", field), field + ".gesto", "neutro,explicando,negando,implorando,irritado,machucado");
                RequireAnimation(gesture, field + ".gesto", animations);
                Set(xmlPart, "personagem", speaker.ToLowerInvariant(), "gesto", gesture);
                speechIntervals.Add(new SpeechInterval { Actor = speaker.ToLowerInvariant(), Start = totalMs, End = totalMs + duration });
            }
            else if (kind == "pausa")
            {
                Prohibit(part, "texto", field); Prohibit(part, "gesto", field); Prohibit(part, "narrador", field);
                if (part.ContainsKey("personagem"))
                {
                    string target = CameraActor(part["personagem"], field + ".personagem", actorIds);
                    if (target.Length > 0) xmlPart.SetAttribute("personagem", target.ToLowerInvariant());
                }
            }
            else
            {
                Prohibit(part, "personagem", field);
                Prohibit(part, "gesto", field);
                xmlPart.SetAttribute("narrador", part.ContainsKey("narrador") ? Text(part["narrador"], field + ".narrador", 1, 48) : "Narrador");
            }
            if (text != null) xmlPart.InnerText = text;
            totalMs += duration;
        }

        var actions = new System.Collections.Generic.List<Acao>();
        var actionValues = scene.ContainsKey("acoes") ? Array(scene["acoes"], "cena.acoes", 0, 20) : new System.Collections.Generic.List<object>();
        for (int i = 0; i < actionValues.Count; i++)
        {
            string field = "cena.acoes[" + i + "]";
            var entry = Obj(actionValues[i], field, "tipo,personagem,inicioSegundos,duracaoSegundos", "destino,velocidade,alvo,animacao");
            var action = new Acao();
            action.Tipo = Choice(Required(entry, "tipo", field), field + ".tipo", "esperar,andarAte,virarPara,animacao");
            action.Personagem = Actor(Required(entry, "personagem", field), field + ".personagem", actorIds, false).ToLowerInvariant();
            action.InicioMs = Ms(Required(entry, "inicioSegundos", field), field + ".inicioSegundos", 0, 180);
            action.DuracaoMs = Ms(Required(entry, "duracaoSegundos", field), field + ".duracaoSegundos", 0.1, 30);
            int end = action.InicioMs + action.DuracaoMs;
            if (end > 180000) throw Error(field + ".duracaoSegundos", "o fim da ação ultrapassa 180 segundos");
            if (action.Tipo == "andarAte")
            {
                Prohibit(entry, "alvo", field); Prohibit(entry, "animacao", field);
                float[] destination = Mark(Required(entry, "destino", field), field + ".destino");
                action.Direita = destination[0]; action.Frente = destination[1]; action.Altura = destination[2];
                action.Velocidade = OptionalNumber(entry, "velocidade", field, 0.2, 2, 1);
            }
            else
            {
                Prohibit(entry, "destino", field); Prohibit(entry, "velocidade", field);
                if (action.Tipo == "virarPara")
                {
                    Prohibit(entry, "animacao", field);
                    action.Alvo = Actor(Required(entry, "alvo", field), field + ".alvo", actorIds, false).ToLowerInvariant();
                    if (action.Alvo == action.Personagem) throw Error(field + ".alvo", "escolha outro ator");
                }
                else if (action.Tipo == "animacao")
                {
                    Prohibit(entry, "alvo", field);
                    action.Animacao = Text(Required(entry, "animacao", field), field + ".animacao", 1, 48);
                    RequireAnimation(action.Animacao, field + ".animacao", animations);
                }
                else { Prohibit(entry, "alvo", field); Prohibit(entry, "animacao", field); }
            }
            foreach (Acao earlier in actions)
                if (earlier.Personagem == action.Personagem && Overlap(action.InicioMs, end, earlier.InicioMs, earlier.InicioMs + earlier.DuracaoMs))
                    throw Error(field, "há ações simultâneas para o mesmo personagem");
            if (action.Tipo != "esperar")
                foreach (SpeechInterval speech in speechIntervals)
                    if (speech.Actor == action.Personagem && Overlap(action.InicioMs, end, speech.Start, speech.End))
                        throw Error(field, "andar, virar ou animar não pode coincidir com a fala desse personagem");
            totalMs = System.Math.Max(totalMs, end);
            actions.Add(action);
        }
        var direction = ReadDirection(scene, catalogDirectory, actorIds, speechIntervals, actions, ref totalMs);
        var interpretation = ReadInterpretation(scene, actorIds, ref totalMs);
        var props = ReadObjects(scene, catalogDirectory, actorIds, direction, totalMs);
        if (cameraObject != null)
        {
            ObjetosSerieDiretor.PropSpec targetObject = null;
            foreach (var prop in props) if (prop.Codigo == cameraObject) targetObject = prop;
            if (targetObject == null || !targetObject.VisivelInicial) throw Error("cena.camera.objeto", "declare objeto próprio inicialmente visível em cena.objetos");
        }
        foreach (var spec in interpretation)
            if (spec.OlharTipo == "objeto")
            {
                bool found = false;
                foreach (var prop in props) if (prop.Codigo == spec.Alvo) { found = true; break; }
                if (!found) throw Error("cena.interpretacao.olhar.alvo", "declare esse objeto próprio em cena.objetos: " + spec.Alvo);
            }
        var transitions = ReadPoseTransitions(scene, catalogDirectory, actorIds, speechIntervals, direction, actions, ref totalMs);
        var poses = ReadPoses(scene, catalogDirectory, actorIds, props, direction, actions, transitions);
        var vehicles = ReadVehicles(scene, catalogDirectory, actorIds, speechIntervals, direction, actions, poses, ref totalMs);
        var doors = ReadDoors(scene, catalogDirectory, actorIds, speechIntervals, direction, actions, transitions, poses, vehicles, ref totalMs);
        bool waterExit = OptionalBool(scene, "saidaAgua", "cena", false);
        bool planePush = OptionalBool(scene, "empurraoAviao", "cena", false);
        bool wardrobe = OptionalBool(scene, "armarioModular", "cena", false);
        if (scene.ContainsKey("armarioModular"))
        {
            if (!wardrobe || id != "EP05_C04" || staging != "serie_v1" || location != "CASA_VO_INTERIOR" ||
                actorIds.Count != 1 || actorIds[0] != "DJANIRA_POBRE" || script.Count != 1 || totalMs != 8000 ||
                actions.Count != 0 || direction.Count != 0 || poses.Count != 0 || transitions.Count != 0 || vehicles.Count != 0 || doors.Count != 0 ||
                props.Count != 1 || interpretation.Count != 1 || waterExit || planePush || cameraObject != null)
                throw Error("cena.armarioModular", "somente EP05_C04 de8s, Djanira pobre no interior, uma folha própria e nenhum controlador corporal concorrente");
            var narration = (System.Collections.Generic.Dictionary<string, object>)script[0];
            const string original = "Minha melhor roupa. Eu abri o armário e fiquei olhando pra ele um tempo. Eu tinha duas roupas. Uma de trabalhar e uma de ir na igreja.";
            if ((string)narration["tipo"] != "narracao" || Ms(narration["duracaoSegundos"], "cena.roteiro[0].duracaoSegundos", 0.1, 180) != 8000 ||
                (string)narration["texto"] != original || !narration.ContainsKey("narrador") || (string)narration["narrador"] != "Narrador")
                throw Error("cena.roteiro", "armarioModular conserva a única narração original integral de8s");
            var leaf = props[0]; var face = interpretation[0];
            if (leaf.Codigo != "ARMARIO_DJANIRA" || leaf.Recurso != "porta_armario" || leaf.Modelo != "prop_wardrobe_door_01" ||
                !leaf.AssentarNoChao || !leaf.VisivelInicial || leaf.AtorInicial != null || leaf.ApoioInicial != null || leaf.Eventos.Count != 0)
                throw Error("cena.objetos", "declare somente ARMARIO_DJANIRA/porta_armario visível no chão, sem attachment, apoio ou eventos");
            if (face.Ator != "djanira_pobre" || face.Expressao != "chateado" || face.InicioMs != 0 || face.DuracaoMs != 8000 || face.OlharTipo != "padrao" ||
                ((System.Collections.Generic.Dictionary<string, object>)Array(scene["interpretacao"], "cena.interpretacao", 1, 1)[0]).ContainsKey("olhar") ||
                (castEntries[0].ContainsKey("olharPara") && castEntries[0]["olharPara"] != null))
                throw Error("cena.interpretacao", "chateado0–8s; o helper controla o olhar para o conteúdo real do armário");
        }
        if (scene.ContainsKey("empurraoAviao") && (!planePush || id != "EP07_C05" || staging != "serie_v1" || location != "CANAL_LS" || actorIds.Count != 3 ||
            !actorIds.Contains("DJANIRA_POBRE") || !actorIds.Contains("O_CHEFE") || !actorIds.Contains("DINJER") || script.Count != 1 ||
            (string)Obj(script[0], "cena.roteiro[0]", "tipo,duracaoSegundos", "plano,personagem")["tipo"] != "pausa" || totalMs != 5000 || mode != "automatico" ||
            actions.Count != 0 || direction.Count != 0 || interpretation.Count != 0 || props.Count != 0 || poses.Count != 0 || transitions.Count != 0 || vehicles.Count != 0 || doors.Count != 0 || waterExit))
            throw Error("cena.empurraoAviao", "somente inserto EP07_C05 de5s, três atores originais, CANAL_LS e nenhum controlador/voz concorrente");
        if (scene.ContainsKey("saidaAgua") && (!waterExit || id != "EP08_C01" || location != "CANAL_LS" || actorIds.Count != 1 || actorIds[0] != "DJANIRA_POBRE" ||
            actions.Count != 0 || direction.Count != 0 || props.Count != 0 || poses.Count != 0 || transitions.Count != 0 || vehicles.Count != 0 || doors.Count != 0 || totalMs < 15000 || totalMs > 50000))
            throw Error("cena.saidaAgua", "somente EP08_C01, Djanira na água real, 15–50s e sem controladores corporais concorrentes");
        if (waterExit)
        {
            float[] waterMark = Mark(castEntries[0]["marca"], "cena.elenco[0].marca");
            if (System.Math.Abs(waterMark[0]) > 0.001f || System.Math.Abs(waterMark[1]) > 0.001f || System.Math.Abs(waterMark[2]) > 0.001f)
                throw Error("cena.elenco[0].marca", "a saída da água parte da marca real, sem offsets");
        }
        if (totalMs <= 0) throw Error("cena.roteiro", "declare pelo menos um trecho ou uma ação para a cena ter duração");

        var cuts = new System.Collections.Generic.List<Corte>();
        if (mode == "programado")
        {
            var values = Array(Required(camera, "cortes", "cena.camera"), "cena.camera.cortes", 1, 20);
            for (int i = 0; i < values.Count; i++)
            {
                string field = "cena.camera.cortes[" + i + "]";
                var entry = Obj(values[i], field, "inicioSegundos,plano,personagem", "");
                var cut = new Corte { InicioMs = Ms(Required(entry, "inicioSegundos", field), field + ".inicioSegundos", 0, 180),
                    Plano = Shot(Required(entry, "plano", field), field + ".plano"),
                    Personagem = CameraActor(Required(entry, "personagem", field), field + ".personagem", actorIds).ToLowerInvariant() };
                EnvironmentShot(cut.Plano, field + ".plano", actorIds);
                if ((i == 0 && cut.InicioMs != 0) || (i > 0 && cut.InicioMs <= cuts[i - 1].InicioMs) || cut.InicioMs >= totalMs)
                    throw Error(field + ".inicioSegundos", "os cortes começam em 0, crescem sem repetição e ficam antes do fim da cena");
                cuts.Add(cut);
            }
        }
        if (id == "EP01_C01")
        {
            if (staging != "serie_v1" || location != "CANAL_LS" || actorIds.Count != 1 || actorIds[0] != "DJANIRA_POBRE" ||
                actions.Count != 0 || direction.Count != 0 || interpretation.Count != 0 || props.Count != 0 || poses.Count != 0 || transitions.Count != 0 || vehicles.Count != 0 || doors.Count != 0)
                throw Error("cena.encenacao", "EP01_C01 exige serie_v1, CANAL_LS, Djanira sozinha e nenhuma ação/prop/pose/interpretação adicional");
            float[] mark = positions[0];
            if (System.Math.Abs(mark[0]) > 0.001f || System.Math.Abs(mark[1]) > 0.001f || System.Math.Abs(mark[2]) > 0.001f)
                throw Error("cena.elenco[0].marca", "EP01_C01 usa diretamente a marca real da água; offsets precisam ser zero");
            var parts = Array(scene["roteiro"], "cena.roteiro", 2, 2);
            var narration = (System.Collections.Generic.Dictionary<string, object>)parts[0];
            var reserve = (System.Collections.Generic.Dictionary<string, object>)parts[1];
            if ((string)narration["tipo"] != "narracao" || Ms(narration["duracaoSegundos"], "cena.roteiro[0].duracaoSegundos", 0.1, 180) != 14000 ||
                (string)reserve["tipo"] != "pausa" || Ms(reserve["duracaoSegundos"], "cena.roteiro[1].duracaoSegundos", 0.1, 180) != 6000)
                throw Error("cena.roteiro", "EP01_C01 preserva narração14s e reserva silenciosa explícita46s, sem boca");
            const string expected = "Meu nome é Djanira. O que fizeram comigo foi tão deplorável que, se eu tô contando essa história, é porque eu sobrevivi. E sobreviver não foi sorte, foi teimosia.";
            if ((string)narration["texto"] != expected) throw Error("cena.roteiro[0].texto", "EP01_C01 deve conservar a narração original integral");
            if (mode != "automatico" || (string)camera["planoInicial"] != "LONGE" || !camera.ContainsKey("planoFinal") || (string)camera["planoFinal"] != "OLHOS")
                throw Error("cena.camera", "EP01_C01 usa câmera própria LONGE→OLHOS nas fases físicas; não aceita cortes substitutos");
        }
        if (staging == "pedido_v1")
        {
            if (id != "EP01_C08" || location != "ROCA_GRAPESEED" || actorIds.Count != 2 ||
                actorIds[0] != "DJANIRA_POBRE" || actorIds[1] != "DINJER" || actions.Count != 0)
                throw Error("cena.encenacao", "pedido_v1 exige EP01_C08, ROCA_GRAPESEED e Djanira/Dinjer nas marcas de conversa");
            float[] firstMark = Mark(castEntries[0]["marca"], "cena.elenco[0].marca");
            float[] secondMark = Mark(castEntries[1]["marca"], "cena.elenco[1].marca");
            if (System.Math.Abs(firstMark[0] + 1.1f) > 0.001f || System.Math.Abs(secondMark[0] - 1.1f) > 0.001f ||
                System.Math.Abs(firstMark[1]) > 0.001f || System.Math.Abs(secondMark[1]) > 0.001f ||
                System.Math.Abs(firstMark[2]) > 0.001f || System.Math.Abs(secondMark[2]) > 0.001f)
                throw Error("cena.encenacao", "pedido_v1 exige marcas direita -1.1/+1.1, frente=0 e altura=0");
            int[] expectedDurations = { 12000, 6000, 9000, 6000, 3000, 3000, 3000, 4000 };
            string[] expectedTypes = { "pausa", "narracao", "fala", "fala", "fala", "fala", "fala", "pausa" };
            string[] expectedSpeakers = { "", "", "DINJER", "DJANIRA_POBRE", "DINJER", "DJANIRA_POBRE", "DINJER", "" };
            var parts = Array(scene["roteiro"], "cena.roteiro", 8, 8);
            for (int i = 0; i < parts.Count; i++)
            {
                var part = (System.Collections.Generic.Dictionary<string, object>)parts[i];
                if ((string)part["tipo"] != expectedTypes[i] || Ms(part["duracaoSegundos"], "cena.roteiro.duracaoSegundos", 0.1, 180) != expectedDurations[i] ||
                    (expectedSpeakers[i].Length > 0 && (string)part["personagem"] != expectedSpeakers[i]))
                    throw Error("cena.encenacao", "pedido_v1 preserva introdução12s, narração6s, falas9/6/3/3/3s e celular4s");
            }
        }
        else if (staging == "ensaio_coreografia_v1")
        {
            if (id != "teste_coreografia" || location != "TESTE_AMBIENTE" || actorIds.Count != 2 ||
                actorIds[0] != "CAPANGA_1" || actorIds[1] != "DJANIRA_POBRE" || actions.Count != 0)
                throw Error("cena.encenacao", "ensaio_coreografia_v1 exige teste_coreografia, TESTE_AMBIENTE, CAPANGA_1/DJANIRA_POBRE e nenhuma ação adicional");
            for (int i = 0; i < 2; i++)
            {
                float[] mark = positions[i];
                if (System.Math.Abs(mark[0] - (i == 0 ? -0.55f : 0.55f)) > 0.001f || System.Math.Abs(mark[1]) > 0.001f || System.Math.Abs(mark[2]) > 0.001f)
                    throw Error("cena.elenco[" + i + "].marca", "ensaio_coreografia_v1 exige direita -0.55/+0.55, frente=0 e altura=0");
            }
            if (script.Count != 1 || (string)((System.Collections.Generic.Dictionary<string, object>)script[0])["tipo"] != "narracao" || totalMs != 10000)
                throw Error("cena.roteiro", "ensaio_coreografia_v1 exige exatamente uma narração de10 segundos, sem fala");
        }
        BindMorph86(id, path, catalogDirectory, morph86HashBeforeRead, props);
        return new Resultado { Document = document, Id = id, Episode = episode, Number = number, Encenacao = staging,
            CleanupRadius = radius, Actions = actions, Cuts = cuts, Visuais = visuals, Direcao = direction, Interpretacao = interpretation, Objetos = props, Poses = poses, TransicoesPose = transitions, Veiculos = vehicles, Portas = doors, SaidaAgua = waterExit, EmpurraoAviao = planePush, ArmarioModular = wardrobe, RotacoesIniciais = initialRotations,
            CameraObjeto = cameraObject, CameraFovObjeto = objectFov, CameraElevacaoObjeto = objectElevation };
    }

    private static System.Collections.Generic.List<PortasSerieDiretor.DoorSpec> ReadDoors(
        System.Collections.Generic.Dictionary<string, object> scene, string directory, System.Collections.Generic.List<string> actors,
        System.Collections.Generic.List<SpeechInterval> speeches, System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> direction,
        System.Collections.Generic.List<Acao> actions, System.Collections.Generic.List<PosesSerieDiretor.TransitionSpec> transitions,
        System.Collections.Generic.List<PosesSerieDiretor.PoseSpec> poses, System.Collections.Generic.List<VeiculosSerieDiretor.VehicleSpec> vehicles, ref int totalMs)
    {
        var result = new System.Collections.Generic.List<PortasSerieDiretor.DoorSpec>();
        if (!scene.ContainsKey("portas")) return result;
        var entries = Array(scene["portas"], "cena.portas", 0, 16);
        System.Collections.Generic.Dictionary<string, BodyResource> bodies = null;
        var evidence = new System.Collections.Generic.List<AcoesSerieDiretor.ActorAction>();
        for (int i = 0; i < entries.Count; i++)
        {
            string field = "cena.portas[" + i + "]";
            var entry = Obj(entries[i], field, "personagem,tipo,inicioSegundos,duracaoSegundos", "batidas,recurso,destino,velocidade");
            string type = Choice(entry["tipo"], field + ".tipo", "bater,abrir,fechar,atravessar");
            string actor = entry["personagem"] == null && (type == "abrir" || type == "fechar") ? null : Actor(entry["personagem"], field + ".personagem", actors, false).ToLowerInvariant();
            var spec = new PortasSerieDiretor.DoorSpec { Tipo = type, Ator = actor,
                InicioMs = Ms(entry["inicioSegundos"], field + ".inicioSegundos", 0, 180),
                DuracaoMs = Ms(entry["duracaoSegundos"], field + ".duracaoSegundos", 0.1, 30), DestinoRelativo = GTA.Math.Vector3.Zero, Velocidade = 1f };
            if (type == "bater")
            {
                Prohibit(entry, "destino", field); Prohibit(entry, "velocidade", field);
                spec.Batidas = Int(Required(entry, "batidas", field), field + ".batidas", 3, 3);
                spec.Recurso = Choice(Required(entry, "recurso", field), field + ".recurso", "bater_porta");
                if (bodies == null) bodies = ReadBodies(System.IO.Path.Combine(directory, "corpos_serie.json"));
                BodyResource body;
                if (!bodies.TryGetValue(spec.Recurso, out body) || body.Type != "clipe" || body.Dictionary != "timetable@jimmy@doorknock@" || body.Clip != "knockdoor_idle")
                    throw Error(field + ".recurso", "exige o par literal instalado de bater na porta");
                spec.Dicionario = body.Dictionary; spec.Clipe = body.Clip; spec.OrigemLinha = body.Line; spec.OrigemSha256 = body.Sha;
                spec.OrigemArquivo = ListedPath(body.File, field + ".recurso", "PedAnimList.txt", true);
                evidence.Add(new AcoesSerieDiretor.ActorAction { Dicionario = body.Dictionary, Clipe = body.Clip, OrigemArquivo = spec.OrigemArquivo, OrigemLinha = body.Line, OrigemSha256 = body.Sha });
            }
            else
            {
                Prohibit(entry, "batidas", field); Prohibit(entry, "recurso", field);
                if (type == "atravessar")
                {
                    float[] destination = Mark(Required(entry, "destino", field), field + ".destino");
                    spec.DestinoRelativo = new GTA.Math.Vector3(destination[0], destination[1], destination[2]);
                    spec.Velocidade = OptionalNumber(entry, "velocidade", field, 0.2, 2, 1);
                }
                else { Prohibit(entry, "destino", field); Prohibit(entry, "velocidade", field); }
            }
            int end = spec.InicioMs + spec.DuracaoMs;
            if (end > 180000) throw Error(field, "porta termina depois de180s");
            if (actor != null)
            {
                foreach (var action in direction) if (action.Ator == actor && Overlap(spec.InicioMs, end, action.InicioMs, action.InicioMs + action.DuracaoMs)) throw Error(field, "porta sobrepõe direção do próprio ator");
                foreach (var action in actions) if (action.Personagem == actor && Overlap(spec.InicioMs, end, action.InicioMs, action.InicioMs + action.DuracaoMs)) throw Error(field, "porta sobrepõe ação legada do próprio ator");
                foreach (var transition in transitions) if (System.String.Equals(transition.Ator, actor, System.StringComparison.OrdinalIgnoreCase) && Overlap(spec.InicioMs, end, transition.InicioMs, transition.InicioMs + transition.DuracaoMs)) throw Error(field, "porta sobrepõe saída da pose");
                foreach (var pose in poses) if (System.String.Equals(pose.Ator, actor, System.StringComparison.OrdinalIgnoreCase))
                {
                    bool exited = false;
                    foreach (var transition in transitions) if (transition.Tipo == "levantar" && System.String.Equals(transition.Ator, actor, System.StringComparison.OrdinalIgnoreCase) && transition.InicioMs + transition.DuracaoMs <= spec.InicioMs) exited = true;
                    if (!exited) throw Error(field, "porta exige sair da pose inicial antes de começar");
                }
                foreach (var vehicle in vehicles) foreach (var action in vehicle.Actions)
                    if (action.Ator == actor && Overlap(spec.InicioMs, end, action.InicioMs, action.InicioMs + action.DuracaoMs)) throw Error(field, "porta sobrepõe operação de veículo");
                if (type == "bater" || type == "atravessar") foreach (var speech in speeches)
                    if (speech.Actor == actor && Overlap(spec.InicioMs, end, speech.Start, speech.End)) throw Error(field, "batida/passagem exige intervalo sem fala do próprio ator");
            }
            foreach (var earlier in result) if (earlier.Ator == actor && Overlap(spec.InicioMs, end, earlier.InicioMs, earlier.InicioMs + earlier.DuracaoMs))
                throw Error(field, "portas simultâneas do mesmo ator/âncora");
            result.Add(spec); totalMs = System.Math.Max(totalMs, end);
        }
        VerifyBodySources(evidence);
        return result;
    }

    private static System.Collections.Generic.List<PosesSerieDiretor.TransitionSpec> ReadPoseTransitions(
        System.Collections.Generic.Dictionary<string, object> scene, string directory, System.Collections.Generic.List<string> actors,
        System.Collections.Generic.List<SpeechInterval> speeches, System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> direction,
        System.Collections.Generic.List<Acao> actions, ref int totalMs)
    {
        var result = new System.Collections.Generic.List<PosesSerieDiretor.TransitionSpec>();
        if (!scene.ContainsKey("transicoesPose")) return result;
        var bodies = ReadBodies(System.IO.Path.Combine(directory, "corpos_serie.json"));
        var entries = Array(scene["transicoesPose"], "cena.transicoesPose", 0, 5);
        var unique = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var evidence = new System.Collections.Generic.List<AcoesSerieDiretor.ActorAction>();
        for (int i = 0; i < entries.Count; i++)
        {
            string field = "cena.transicoesPose[" + i + "]";
            var entry = Obj(entries[i], field, "personagem,tipo,recurso,inicioSegundos,duracaoSegundos", "");
            string actor = Actor(entry["personagem"], field + ".personagem", actors, false);
            if (!unique.Add(actor)) throw Error(field + ".personagem", "uma saída por pose inicial");
            string transitionType = Choice(entry["tipo"], field + ".tipo", "levantar,sentar");
            string code = Choice(entry["recurso"], field + ".recurso", "levantar_cadeira_f,levantar_cadeira_livre_f,levantar_cadeira_m,levantar_chao_f,sentar_cadeira_f,sentar_cadeira_livre_f,sentar_cadeira_m,sentar_chao_f");
            bool sitting = code.StartsWith("sentar_", System.StringComparison.Ordinal);
            if (sitting != (transitionType == "sentar")) throw Error(field, "tipo/recurso da transi??o divergem");
            string exitCode = sitting ? code.Replace("sentar_", "levantar_") : code;
            string poseCode = exitCode == "levantar_cadeira_f" ? "sentada_cadeira_f" : exitCode == "levantar_cadeira_livre_f" ? "sentada_cadeira_livre_f" :
                exitCode == "levantar_cadeira_m" ? "sentado_cadeira_m" : "sentada_chao_f";
            bool hasPose = false;
            foreach (object raw in scene.ContainsKey("poses") ? Array(scene["poses"], "cena.poses", 0, 5) : new System.Collections.Generic.List<object>())
            {
                var pose = Obj(raw, "cena.poses", "personagem,objeto,recurso,tipo", "superficie,rotacao");
                if (Actor(pose["personagem"], "cena.poses.personagem", actors, false) == actor && (string)pose["recurso"] == poseCode) hasPose = true;
            }
            if (!hasPose) throw Error(field + ".recurso", "saída exige a base inicial correspondente do mesmo ator");
            string dictionary = exitCode == "levantar_cadeira_f" ? "amb@prop_human_seat_chair@female@arms_folded@exit" :
                exitCode == "levantar_cadeira_livre_f" ? "amb@prop_human_seat_chair@female@proper@exit" :
                exitCode == "levantar_cadeira_m" ? "amb@prop_human_seat_chair@male@generic@exit" : "amb@world_human_picnic@female@exit";
            string clip = exitCode == "levantar_cadeira_f" ? "exit_fwd" : exitCode == "levantar_chao_f" ? "exit" : "exit_forward";
            if (sitting) { dictionary = dictionary.Replace("@exit", "@enter"); clip = exitCode == "levantar_cadeira_m" ? "enter_forward" : exitCode == "levantar_chao_f" ? "enter" : "enter_fwd"; }
            BodyResource resource;
            if (!bodies.TryGetValue(code, out resource) || resource.Type != "clipe" || resource.Dictionary != dictionary || resource.Clip != clip)
                throw Error(field + ".recurso", "saída exige par literal no catálogo de corpos");
            var spec = new PosesSerieDiretor.TransitionSpec { Ator = actor, Tipo = transitionType, Recurso = code,
                InicioMs = Ms(entry["inicioSegundos"], field + ".inicioSegundos", 0, 180), DuracaoMs = Ms(entry["duracaoSegundos"], field + ".duracaoSegundos", 0.5, 8),
                Dicionario = dictionary, Clipe = clip, OrigemArquivo = ListedPath(resource.File, field + ".recurso", "PedAnimList.txt", true),
                OrigemLinha = resource.Line, OrigemSha256 = resource.Sha };
            int end = spec.InicioMs + spec.DuracaoMs;
            if (end > 180000) throw Error(field, "saída ultrapassa180s");
            foreach (var speech in speeches) if (speech.Actor == actor.ToLowerInvariant() && Overlap(spec.InicioMs, end, speech.Start, speech.End))
                throw Error(field, "saída primária precisa de intervalo sem fala do próprio ator");
            foreach (var action in actions) if (action.Personagem == actor.ToLowerInvariant() && Overlap(spec.InicioMs, end, action.InicioMs, action.InicioMs + action.DuracaoMs))
                throw Error(field, "saída sobrepõe ação legada");
            foreach (var action in direction) if (action.Ator == actor.ToLowerInvariant() && Overlap(spec.InicioMs, end, action.InicioMs, action.InicioMs + action.DuracaoMs))
                throw Error(field, "saída sobrepõe direção do mesmo ator");
            result.Add(spec); totalMs = System.Math.Max(totalMs, end);
            evidence.Add(new AcoesSerieDiretor.ActorAction { Dicionario = dictionary, Clipe = clip, OrigemArquivo = spec.OrigemArquivo, OrigemLinha = resource.Line, OrigemSha256 = resource.Sha });
        }
        VerifyBodySources(evidence);
        return result;
    }

    private static System.Collections.Generic.List<VeiculosSerieDiretor.VehicleSpec> ReadVehicles(
        System.Collections.Generic.Dictionary<string, object> scene, string directory, System.Collections.Generic.List<string> actors,
        System.Collections.Generic.List<SpeechInterval> speeches, System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> direction,
        System.Collections.Generic.List<Acao> actions, System.Collections.Generic.List<PosesSerieDiretor.PoseSpec> poses, ref int totalMs)
    {
        var result = new System.Collections.Generic.List<VeiculosSerieDiretor.VehicleSpec>();
        if (!scene.ContainsKey("veiculos")) return result;
        var values = Array(scene["veiculos"], "cena.veiculos", 0, 2);
        if (values.Count == 0) return result;
        var source = Obj(ReadJson(System.IO.Path.Combine(directory, "veiculos_serie_candidatos.json"), "catalogos.veiculos"), "catalogos.veiculos", "versao,tipoDocumento,estado,pronto,fonteDLL,observacao,veiculos", "limites,motoristaExtra");
        Int(source["versao"], "catalogos.veiculos.versao", 1, 1);
        var resources = new System.Collections.Generic.Dictionary<string, string[]>(System.StringComparer.Ordinal);
        foreach (object raw in Array(source["veiculos"], "catalogos.veiculos.veiculos", 1, 32))
        {
            var item = Obj(raw, "catalogos.veiculos.item", "codigo,modelo,tipo,pronto,estado,origem", "");
            string code = Code(item["codigo"], "catalogos.veiculos.codigo"), model = Text(item["modelo"], "catalogos.veiculos.modelo", 1, 64);
            string type = Choice(item["tipo"], "catalogos.veiculos.tipo", "MOTO,CARRO,ONIBUS,CAMINHAO");
            if (!VeiculosSerieDiretor.IsCatalogModel(type, model) || code != model.ToUpperInvariant() || resources.ContainsKey(code)) throw Error("catalogos.veiculos", "modelo/tipo/código deve permanecer na whitelist literal");
            resources.Add(code, new string[] { type, model });
        }
        var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var initialActors = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var ops = new System.Collections.Generic.List<VeiculosSerieDiretor.VehicleAction>();
        var opVehicles = new System.Collections.Generic.List<string>();
        for (int i = 0; i < values.Count; i++)
        {
            string field = "cena.veiculos[" + i + "]";
            var entry = Obj(values[i], field, "id,recurso,marca,acoes", "rotacao,motoristaExtra,trajetoDireto,assentosIniciais");
            string id = Code(entry["id"], field + ".id"), code = Code(entry["recurso"], field + ".recurso");
            string[] resource;
            if (!ids.Add(id) || !resources.TryGetValue(code, out resource)) throw Error(field, "ID único e recurso terrestre existente são necessários");
            var spec = new VeiculosSerieDiretor.VehicleSpec { Codigo = id, Tipo = resource[0], Modelo = resource[1],
                PosicaoRelativa = VehicleMark(entry["marca"], field + ".marca"), HeadingRelativo = OptionalNumber(entry, "rotacao", field, -180, 180, 0),
                MotoristaExtra = OptionalBool(entry, "motoristaExtra", field, false), TrajetoDireto = OptionalBool(entry, "trajetoDireto", field, false) };
            var occupied = new System.Collections.Generic.HashSet<int>();
            foreach (object raw in entry.ContainsKey("assentosIniciais") ? Array(entry["assentosIniciais"], field + ".assentosIniciais", 0, 5) : new System.Collections.Generic.List<object>())
            {
                var seat = Obj(raw, field + ".assentosIniciais", "personagem,assento", "");
                string actor = Actor(seat["personagem"], field + ".assentosIniciais.personagem", actors, false);
                int number = Int(seat["assento"], field + ".assentosIniciais.assento", -1, 15);
                if (!initialActors.Add(actor) || !occupied.Add(number) || (spec.MotoristaExtra && number == -1)) throw Error(field, "ator/assento inicial duplicado ou reservado ao motorista extra");
                foreach (var pose in poses) if (pose.Ator == actor) throw Error(field, "ocupante inicial não pode ter pose de móvel/chão");
                spec.ActorInitialSeats.Add(actor, number);
            }
            var actionEntries = Array(entry["acoes"], field + ".acoes", 0, 32);
            foreach (object raw in actionEntries)
            {
                var item = Obj(raw, field + ".acoes", "tipo,personagem,inicioSegundos,duracaoSegundos", "assento,rota,velocidade,raioChegada,raioParada");
                string type = Choice(item["tipo"], field + ".acoes.tipo", "entrar,sair,dirigir");
                string actor = item["personagem"] == null ? null : Actor(item["personagem"], field + ".acoes.personagem", actors, false);
                if (actor == null && !spec.MotoristaExtra) throw Error(field, "personagem=null exige motoristaExtra");
                var action = new VeiculosSerieDiretor.VehicleAction { Tipo = type, Ator = actor,
                    InicioMs = Ms(item["inicioSegundos"], field + ".acoes.inicioSegundos", 0, 180), DuracaoMs = Ms(item["duracaoSegundos"], field + ".acoes.duracaoSegundos", 0.25, 60),
                    Assento = item.ContainsKey("assento") ? Int(item["assento"], field + ".acoes.assento", -1, 15) : -1,
                    Velocidade = OptionalNumber(item, "velocidade", field + ".acoes", 1, 20, 8), RaioChegada = OptionalNumber(item, "raioChegada", field + ".acoes", 0.5, 3, 1.5f) };
                if (item.ContainsKey("raioParada")) action.RaioParada = (float)Numeric(item["raioParada"], field + ".acoes.raioParada", 0.5, action.RaioChegada);
                if (type == "dirigir")
                {
                    foreach (object point in Array(Required(item, "rota", field + ".acoes"), field + ".acoes.rota", 1, 16)) action.RotaRelativa.Add(VehicleMark(point, field + ".acoes.rota"));
                    if (action.Assento != -1) throw Error(field, "dirigir usa assento motorista=-1");
                }
                else { Prohibit(item, "rota", field + ".acoes"); Prohibit(item, "velocidade", field + ".acoes"); Prohibit(item, "raioChegada", field + ".acoes"); Prohibit(item, "raioParada", field + ".acoes"); }
                int end = action.InicioMs + action.DuracaoMs;
                if (end > 180000 || ops.Count >= 32) throw Error(field, "até32operações que terminam até180s");
                if (actor != null)
                {
                    foreach (var speech in speeches) if (speech.Actor == actor.ToLowerInvariant() && Overlap(action.InicioMs, end, speech.Start, speech.End)) throw Error(field, "operação de veículo sobrepõe fala do mesmo ator");
                    foreach (var body in direction) if (body.Ator == actor.ToLowerInvariant() && Overlap(action.InicioMs, end, body.InicioMs, body.InicioMs + body.DuracaoMs)) throw Error(field, "operação de veículo sobrepõe direção do mesmo ator");
                    foreach (var body in actions) if (body.Personagem == actor.ToLowerInvariant() && Overlap(action.InicioMs, end, body.InicioMs, body.InicioMs + body.DuracaoMs)) throw Error(field, "operação de veículo sobrepõe ação legada");
                }
                for (int n = 0; n < ops.Count; n++) if (Overlap(action.InicioMs, end, ops[n].InicioMs, ops[n].InicioMs + ops[n].DuracaoMs) &&
                    ((actor != null && ops[n].Ator == actor) || (opVehicles[n] == id && (type == "dirigir" || ops[n].Tipo == "dirigir")))) throw Error(field, "operações conflitantes no mesmo ator/veículo");
                spec.Actions.Add(action); ops.Add(action); opVehicles.Add(id); totalMs = System.Math.Max(totalMs, end);
            }
            result.Add(spec);
        }
        return result;
    }
    private static GTA.Math.Vector3 VehicleMark(object raw, string field)
    {
        var mark = Obj(raw, field, "direita,frente,altura", "");
        var value = new GTA.Math.Vector3((float)Numeric(mark["direita"], field + ".direita", -150, 150),
            (float)Numeric(mark["frente"], field + ".frente", -150, 150), (float)Numeric(mark["altura"], field + ".altura", -60, 60));
        if (value.X * value.X + value.Y * value.Y > 22500f) throw Error(field, "marca de veículo fora do raio150m");
        return value;
    }

    private static System.Collections.Generic.List<PosesSerieDiretor.PoseSpec> ReadPoses(
        System.Collections.Generic.Dictionary<string, object> scene, string directory, System.Collections.Generic.List<string> actors,
        System.Collections.Generic.List<ObjetosSerieDiretor.PropSpec> props, System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> direction,
        System.Collections.Generic.List<Acao> actions, System.Collections.Generic.List<PosesSerieDiretor.TransitionSpec> transitions)
    {
        var result = new System.Collections.Generic.List<PosesSerieDiretor.PoseSpec>();
        var values = scene.ContainsKey("poses") ? Array(scene["poses"], "cena.poses", 0, 5) : new System.Collections.Generic.List<object>();
        if (values.Count == 0)
        {
            foreach (var action in direction) if (action.SobrePoseSentada) throw Error("cena.direcao.sobrePoseSentada", "exige pose sentada declarada para esse ator");
            return result;
        }
        var resources = ReadBodies(System.IO.Path.Combine(directory, "corpos_serie.json"));
        var unique = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            string field = "cena.poses[" + i + "]";
            var entry = Obj(values[i], field, "personagem,objeto,recurso,tipo", "superficie,rotacao,abracarObjeto");
            string actor = Actor(entry["personagem"], field + ".personagem", actors, false);
            if (!unique.Add(actor)) throw Error(field + ".personagem", "um ator admite somente uma pose inicial");
            int exitEnd = 180001;
            foreach (var transition in transitions) if (transition.Ator == actor && transition.Tipo == "levantar") exitEnd = transition.InicioMs + transition.DuracaoMs;
            foreach (var action in actions)
                if (action.Personagem == actor.ToLowerInvariant() && action.InicioMs < exitEnd) throw Error(field + ".personagem", "ação legada só é permitida após levantar da pose");
            string type = Choice(entry["tipo"], field + ".tipo", "sentada,deitada,chao");
            string objectId = type == "chao" && entry["objeto"] == null ? null : Code(entry["objeto"], field + ".objeto");
            ObjetosSerieDiretor.PropSpec support = null;
            foreach (var prop in props) if (prop.Codigo == objectId) support = prop;
            if (type == "chao")
            { if (objectId != null || entry.ContainsKey("superficie")) throw Error(field + ".objeto", "chão usa objeto=null e piso medido na marca real"); }
            else
            {
                if (support == null) throw Error(field + ".objeto", "declare esse objeto próprio em cena.objetos");
                bool isSupport = System.Array.IndexOf("cadeira_simples,poltrona,banco,banco_sono_c1_02,mesa,mesa_grande_c8_autor,cadeira_casa,cama_casa,cama_quarto,cama_simples,mesa_banco_candidata,mesa_limpeza_bar,bancada_limpeza_candidata".Split(','), support.Recurso) >= 0;
                if (!isSupport || !support.AssentarNoChao || !support.VisivelInicial || !System.String.IsNullOrEmpty(support.AtorInicial))
                    throw Error(field + ".objeto", "pose exige móvel de apoio próprio, assentado, visível e sem attachment");
                foreach (var propEvent in support.Eventos)
                    if (propEvent.Tipo != "mostrar") throw Error(field + ".objeto", "o apoio da pose não pode receber evento que o mova ou oculte");
            }
            string code = Text(entry["recurso"], field + ".recurso", 1, 48);
            if (code == "office_cell_sleep_f" &&
                (Text(scene["id"], "cena.id", 1, 48) != "teste_fila_84" || Int(scene["episodio"], "cena.episodio", 0, 15) != 0 ||
                 Int(scene["numero"], "cena.numero", 0, 99) != 0 || actors.Count != 1 || actor != "DJANIRA_POBRE" ||
                 values.Count != 1 || (props.Count != 1 && (props.Count != 2 || !props.Exists(delegate(ObjetosSerieDiretor.PropSpec p) { return p.Codigo == "SACOLA_CALIBRACAO84" && (p.Recurso == "bolsa_sono84_preview" || p.Recurso == "sacola_papel_sono84_preview"); }))) || objectId != "BANCO_PRAIA" || support == null || support.Recurso != "banco_sono_c1_02" ||
                 type != "deitada" || OptionalNumber(entry, "rotacao", field, -180, 180, 0) != 90f || entry.ContainsKey("abracarObjeto")))
                throw Error(field + ".recurso", "office_cell_sleep_f somente pose tecnica84/bench02/Djanira, optional exact own compact preview, never production.");
            if (code == "sono_banco_tracy_f" &&
                (Text(scene["id"], "cena.id", 1, 48) != "EP10_C01" || actors.Count != 1 || actor != "DJANIRA_POBRE" ||
                 values.Count != 1 || objectId != "BANCO_PRAIA" || support == null || support.Recurso != "banco_sono_c1_02" ||
                 type != "deitada" || !entry.ContainsKey("abracarObjeto") || Code(entry["abracarObjeto"], field + ".abracarObjeto") != "SACOLA_DJANIRA"))
                throw Error(field + ".recurso", "Tracy sleep loop exclusive EP10_C01 measured bench02/Djanira/bag");
            string expectedDictionary, expectedClip, expectedType;
            try { expectedDictionary = PosesSerieDiretor.GetCatalogDictionary(code); expectedClip = PosesSerieDiretor.GetCatalogClip(code); expectedType = PosesSerieDiretor.GetCatalogType(code); }
            catch (System.InvalidOperationException) { throw Error(field + ".recurso", "use uma das seis poses canônicas do helper"); }
            BodyResource resource;
            if (!resources.TryGetValue(code, out resource) || resource.Type != "clipe" || resource.Dictionary != expectedDictionary || resource.Clip != expectedClip)
                throw Error(field + ".recurso", "o catálogo deve conservar o par literal da pose canônica");
            if (type != expectedType && !(type == "chao" && code == "sentada_meio_fio_f")) throw Error(field + ".tipo", "o tipo diverge do recurso de pose");
            string embracedId = null;
            if (entry.ContainsKey("abracarObjeto"))
            {
                embracedId = Code(entry["abracarObjeto"], field + ".abracarObjeto");
                ObjetosSerieDiretor.PropSpec embraced = null;
                foreach (var prop in props) if (prop.Codigo == embracedId) embraced = prop;
                if ((code != "sono_f" && code != "sono_banco_tracy_f") || type != "deitada" || embracedId == objectId || embraced == null ||
                    embraced.Recurso != "sacola" || !embraced.VisivelInicial || embraced.AtorInicial != actor ||
                    (embraced.MaoInicial != "direita" && embraced.MaoInicial != "esquerda") || embraced.Eventos.Count != 0)
                    throw Error(field + ".abracarObjeto", "sono_f deitada exige sacola própria visível, anexada ao mesmo ator, sem eventos");
            }
            foreach (var action in direction)
                if (action.Ator == actor.ToLowerInvariant() && action.InicioMs < exitEnd && (!action.SobrePoseSentada ||
                    !PosesSerieDiretor.CanOverlay(code, action.Dicionario, action.Clipe)))
                    throw Error(field + ".personagem", "pose inicial não pode receber direção corporal fora do gesto sentado compatível");
            var spec = new PosesSerieDiretor.PoseSpec { Ator = actor, Objeto = objectId, Recurso = code, Tipo = type, ObjetoAbracado = embracedId,
                Dicionario = resource.Dictionary, Clipe = resource.Clip, OrigemArquivo = ListedPath(resource.File, field + ".recurso", "PedAnimList.txt", true),
                OrigemSha256 = resource.Sha, OrigemLinha = resource.Line, UsarCentroMedido = !entry.ContainsKey("superficie"),
                SuperficieRelativa = GTA.Math.Vector3.Zero, RotacaoRelativa = OptionalNumber(entry, "rotacao", field, -180, 180, 0) };
            if (entry.ContainsKey("superficie"))
            {
                var surface = Obj(entry["superficie"], field + ".superficie", "direita,frente", "");
                spec.SuperficieRelativa = new GTA.Math.Vector3((float)Numeric(surface["direita"], field + ".superficie.direita", -10, 10),
                    (float)Numeric(surface["frente"], field + ".superficie.frente", -10, 10), 0f);
            }
            result.Add(spec);
        }
        foreach (var action in direction)
            if (action.SobrePoseSentada && !unique.Contains(action.Ator.ToUpperInvariant()))
                throw Error("cena.direcao.sobrePoseSentada", "exige pose sentada declarada para esse ator");
        try { PosesSerieDiretor.VerifySourceEvidence(result); }
        catch (System.InvalidOperationException ex) { throw Error("cena.poses.recurso", ex.Message); }
        catch (System.IO.IOException ex) { throw Error("cena.poses.recurso", "não foi possível ler a fonte: " + ex.Message); }
        catch (System.UnauthorizedAccessException) { throw Error("cena.poses.recurso", "acesso à fonte não permitido"); }
        catch (System.Text.DecoderFallbackException) { throw Error("cena.poses.recurso", "a fonte precisa estar em UTF-8 válido"); }
        catch (System.ArgumentException) { throw Error("cena.poses.recurso", "caminho de origem inválido"); }
        catch (System.NotSupportedException) { throw Error("cena.poses.recurso", "caminho de origem inválido"); }
        return result;
    }

    private static System.Collections.Generic.List<ObjetosSerieDiretor.PropSpec> ReadObjects(
        System.Collections.Generic.Dictionary<string, object> scene, string directory, System.Collections.Generic.List<string> actors,
        System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> direction, int durationMs)
    {
        var result = new System.Collections.Generic.List<ObjetosSerieDiretor.PropSpec>();
        if (Text(scene["id"], "cena.id", 1, 48) == "teste_fila_88" && !scene.ContainsKey("objetos"))
            throw Error("cena.objetos", "ensaio88 exige as duas caixas proprias declaradas");
        if (!scene.ContainsKey("objetos")) return result;
        var values = Array(scene["objetos"], "cena.objetos", 0, 8);
        if (Text(scene["id"], "cena.id", 1, 48) == "teste_fila_88")
        {
            var trialEnvironment = (System.Collections.Generic.Dictionary<string, object>)scene["ambiente"];
            if (values.Count != 2 || actors.Count != 1 || actors[0] != "DJANIRA_POBRE" ||
                Text(trialEnvironment["local"], "cena.ambiente.local", 1, 48) != "AEROPORTO_HANGAR" || direction.Count != 0)
                throw Error("cena.objetos", "ensaio88 exige Hangar, Djanira parada e duas caixas proprias");
            foreach (string trialOnlyEmpty in "acoes,poses,transicoesPose,veiculos,portas".Split(','))
                if (scene.ContainsKey(trialOnlyEmpty) && Array(scene[trialOnlyEmpty], "cena." + trialOnlyEmpty, 0, 128).Count != 0)
                    throw Error("cena." + trialOnlyEmpty, "ensaio88 nao aceita acoes/poses/veiculos/portas adicionais");
        }
        bool productionBoxesC2 = false;
        foreach (object raw in values)
        {
            var maybe = raw as System.Collections.Generic.Dictionary<string, object>;
            if (maybe != null && maybe.ContainsKey("recurso") && ObjetosSerieDiretor.IsProductionBoxResourceC2(maybe["recurso"] as string))
                productionBoxesC2 = true;
        }
        if (productionBoxesC2)
        {
            var environmentC2 = (System.Collections.Generic.Dictionary<string, object>)scene["ambiente"];
            if (Text(scene["id"], "cena.id", 1, 48) != "EP07_C02" || Int(scene["episodio"], "cena.episodio", 0, 15) != 7 ||
                Int(scene["numero"], "cena.numero", 0, 99) != 2 || durationMs != 16000 ||
                Text(environmentC2["local"], "cena.ambiente.local", 1, 48) != "GALPAO_CYPRESS" || values.Count != 3 ||
                actors.Count != 4 || !actors.Contains("O_CHEFE") || !actors.Contains("CAPANGA_1") ||
                !actors.Contains("CAPANGA_2") || !actors.Contains("DJANIRA_POBRE") || direction.Count != 0)
                throw Error("cena.objetos", "wood01a producao somente EP07_C02/GALPAO_CYPRESS/16s/ChefeCAP1CAP2Djanira/umcontainer+duascaixas");
            foreach (string emptyC2 in "acoes,poses,transicoesPose,veiculos,portas".Split(','))
                if (scene.ContainsKey(emptyC2) && Array(scene[emptyC2], "cena." + emptyC2, 0, 128).Count != 0)
                    throw Error("cena." + emptyC2, "wood01a EP07_C02 nao aceita encenacao adicional");
            var uniqueC2 = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (object raw in values)
            {
                var itemC2 = Obj(raw, "cena.objetos", "id,recurso,marca", "rotacao,assentarNoChao,visivelInicial,attachmentInicial,eventos,apoioInicial,superficieInicial");
                string idC2 = Code(itemC2["id"], "cena.objetos.id"), resourceC2 = Text(itemC2["recurso"], "cena.objetos.recurso", 1, 48);
                bool boxC2 = (idC2 == "CAIXAS_NEGOCIO_1" || idC2 == "CAIXAS_NEGOCIO_2") && ObjetosSerieDiretor.IsProductionBoxResourceC2(resourceC2);
                bool containerC2 = idC2 == "CONTAINER_CYPRESS" && resourceC2 == "container_serie";
                float[] markC2 = Mark(itemC2["marca"], "cena.objetos.marca");
                if (!uniqueC2.Add(idC2) || (!boxC2 && !containerC2) || markC2[2] != 0f ||
                    OptionalNumber(itemC2, "rotacao", "cena.objetos", -180, 180, 0) != (containerC2 ? 90f : 0f) ||
                    !OptionalBool(itemC2, "assentarNoChao", "cena.objetos", true) || !OptionalBool(itemC2, "visivelInicial", "cena.objetos", true) ||
                    itemC2.ContainsKey("attachmentInicial") || itemC2.ContainsKey("apoioInicial") || itemC2.ContainsKey("superficieInicial") ||
                    (itemC2.ContainsKey("eventos") && Array(itemC2["eventos"], "cena.objetos.eventos", 0, 128).Count != 0))
                    throw Error("cena.objetos", "wood01a EP07_C02 pares/codigos literais visiveis/chao, sem altura/tilt/eventos/attachment/apoio");
            }
        }
        bool previewPaper86 = Text(scene["id"], "cena.id", 1, 48) == "teste_fila_86";
        if (previewPaper86)
        {
            if (values.Count != 1 || actors.Count != 1 || actors[0] != "DJANIRA_POBRE" || direction.Count != 0)
                throw Error("cena.objetos", "preview86 exige somente Djanira parada e um papel proprio");
            foreach (string empty in "acoes,poses,transicoesPose,veiculos,portas".Split(','))
                if (scene.ContainsKey(empty) && Array(scene[empty], "cena." + empty, 0, 128).Count != 0)
                    throw Error("cena." + empty, "preview86 sem encenacao adicional");
        }
        var resources = ReadPropResources(System.IO.Path.Combine(directory, "objetos_serie.json"));
        var unique = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            string field = "cena.objetos[" + i + "]";
            var entry = Obj(values[i], field, "id,recurso,marca", "rotacao,assentarNoChao,visivelInicial,attachmentInicial,eventos,apoioInicial,superficieInicial");
            string id = Code(entry["id"], field + ".id");
            if (!unique.Add(id)) throw Error(field + ".id", "objeto repetido");
            string code = Text(entry["recurso"], field + ".recurso", 1, 48);
            PropResource resource;
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "\\A[a-z][a-z0-9_]{0,47}\\z") || !resources.TryGetValue(code, out resource))
                throw Error(field + ".recurso", "use código minúsculo existente no catálogo de objetos");
            if (ObjetosSerieDiretor.IsTechnicalOnlyBoxResource88(code) &&
                Text(scene["id"], "cena.id", 1, 48) != "teste_fila_88")
                throw Error(field + ".recurso", "alias tecnico de caixa exclusivo do teste_fila_88, nunca producao");
            if (ObjetosSerieDiretor.IsPaperPreviewResource86(code) != previewPaper86 ||
                (previewPaper86 && (id != "PAPEL_PROVA" || entry.ContainsKey("apoioInicial") ||
                    (entry.ContainsKey("eventos") && Array(entry["eventos"], field + ".eventos", 0, 128).Count != 0))))
                throw Error(field + ".recurso", "papel preview86 exclusivo de um objeto tecnico sem eventos; nunca producao");
            if (ObjetosSerieDiretor.IsProductionBoxResourceC2(code) && !productionBoxesC2)
                throw Error(field + ".recurso", "wood01a producao exclusivo do contrato integral EP07_C02");
if ((code == "bolsa_sono84_preview" || code == "sacola_papel_sono84_preview") &&
    (Text(scene["id"], "cena.id", 1, 48) != "teste_fila_84" || Int(scene["episodio"], "cena.episodio", 0, 15) != 0 ||
     Int(scene["numero"], "cena.numero", 0, 99) != 0 || durationMs != 10000 || values.Count != 2 || id != "SACOLA_CALIBRACAO84" ||
     Text(((System.Collections.Generic.Dictionary<string, object>)scene["ambiente"])["local"], "cena.ambiente.local", 1, 48) != "CENTRO_LEGION" ||
     actors.Count != 1 || actors[0] != "DJANIRA_POBRE" || OptionalBool(entry, "assentarNoChao", field, true) ||
     !OptionalBool(entry, "visivelInicial", field, true) || !entry.ContainsKey("attachmentInicial") ||
     entry.ContainsKey("apoioInicial") || entry.ContainsKey("superficieInicial") ||
     (entry.ContainsKey("eventos") && Array(entry["eventos"], field+".eventos",0,128).Count != 0)))
    throw Error(field+".recurso", "compact own handbag preview exclusive technical84/CENTRO/10s/Poor/sole bench plus bag; never production");
            if (code == "papel_dobrado_autoral" &&
                (Text(scene["id"], "cena.id", 1, 48) != "EP08_C05" || Int(scene["episodio"], "cena.episodio", 0, 15) != 8 ||
                 Int(scene["numero"], "cena.numero", 0, 99) != 5 || durationMs != 12000 || id != "PAPEL_IRACI" || values.Count != 3 ||
                 Text(((System.Collections.Generic.Dictionary<string, object>)scene["ambiente"])["local"], "cena.ambiente.local", 1, 48) != "CASA_VO_INTERIOR" ||
                 actors.Count != 2 || !actors.Contains("DONA_IRACI") || !actors.Contains("DJANIRA_POBRE") ||
                 OptionalBool(entry, "assentarNoChao", field, true) || !OptionalBool(entry, "visivelInicial", field, true) ||
                 !entry.ContainsKey("attachmentInicial") || entry.ContainsKey("apoioInicial") || entry.ContainsKey("superficieInicial") ||
                 previewPaper86))
                throw Error(field + ".recurso", "authored visual exclusive EP08_C05/CASA_VO_INTERIOR/PAPEL_IRACI/12s/two registered actors/three own props; real proxy is not folded collider");
            if (code == "mesa_grande_c8_autor")
            {
                var environmentC8 = (System.Collections.Generic.Dictionary<string, object>)scene["ambiente"];
                if (Text(scene["id"], "cena.id", 1, 48) != "EP15_C08" || Int(scene["episodio"], "cena.episodio", 0, 15) != 15 ||
                    Int(scene["numero"], "cena.numero", 0, 99) != 8 || durationMs != 12000 || id != "MESA_JANTAR" ||
                    Text(environmentC8["local"], "cena.ambiente.local", 1, 48) != "MANSAO_CHEFE" || values.Count != 2 ||
                    actors.Count != 1 || (actors[0] != "O_CHEFE" && actors[0] != "DINJER") ||
                    !OptionalBool(entry, "assentarNoChao", field, true) || !OptionalBool(entry, "visivelInicial", field, true) ||
                    entry.ContainsKey("attachmentInicial") || entry.ContainsKey("apoioInicial") || entry.ContainsKey("superficieInicial") ||
                    Mark(entry["marca"], field + ".marca")[2] != 0f ||
                    (entry.ContainsKey("eventos") && Array(entry["eventos"], field + ".eventos", 0, 128).Count != 0))
                    throw Error(field + ".recurso", "large table exclusive EP15_C08/MANSAO_CHEFE/12s/one Chefe-or-Dinjer/MESA_JANTAR+own chair-or-tray");
            }
            if (code == "banco_sono_c1_02" &&
                (!((Text(scene["id"], "cena.id", 1, 48) == "EP10_C01" && Int(scene["episodio"], "cena.episodio", 0, 15) == 10 && Int(scene["numero"], "cena.numero", 0, 99) == 1) ||
                   (Text(scene["id"], "cena.id", 1, 48) == "teste_fila_84" && Int(scene["episodio"], "cena.episodio", 0, 15) == 0 && Int(scene["numero"], "cena.numero", 0, 99) == 0 &&
                    (values.Count == 1 || values.Count == 2) && Text(((System.Collections.Generic.Dictionary<string, object>)scene["ambiente"])["local"], "cena.ambiente.local", 1, 48) == "CENTRO_LEGION")) || durationMs != 10000 || id != "BANCO_PRAIA" ||
                 System.Array.IndexOf(new string[] { "PRAIA_DELPERRO", "CENTRO_LEGION" }, Text(((System.Collections.Generic.Dictionary<string, object>)scene["ambiente"])["local"], "cena.ambiente.local", 1, 48)) < 0 ||
                 actors.Count != 1 || actors[0] != "DJANIRA_POBRE"))
                throw Error(field + ".recurso", "banco_sono_c1_02 exclusivo EP10_C01/PRAIA_DELPERRO-or-CENTRO_LEGION/DJANIRA_POBRE/BANCO_PRAIA/10s");
            if (code == "celular")
                foreach (var bodyAction in direction)
                    if (bodyAction.Tipo == "celular") throw Error(field + ".recurso", "não combinar objeto celular com direção do tipo celular na mesma cena; isso duplicaria o telefone");
            float[] mark = Mark(entry["marca"], field + ".marca");
            var spec = new ObjetosSerieDiretor.PropSpec { Codigo = id, Recurso = code, Modelo = resource.Model, OrigemArquivo = resource.File,
                OrigemSha256 = resource.Sha, OrigemLinha = resource.Line, Deslocamento = new GTA.Math.Vector3(mark[0], mark[1], mark[2]),
                RotacaoZ = OptionalNumber(entry, "rotacao", field, -180, 180, 0), AssentarNoChao = OptionalBool(entry, "assentarNoChao", field, true),
                VisivelInicial = OptionalBool(entry, "visivelInicial", field, true) };
            bool support = resource.Type == "apoio";
            if (entry.ContainsKey("apoioInicial"))
            {
                spec.ApoioInicial = Code(entry["apoioInicial"], field + ".apoioInicial");
                if (support || spec.ApoioInicial == id || spec.AssentarNoChao || !spec.VisivelInicial || entry.ContainsKey("attachmentInicial"))
                    throw Error(field, "apoio inicial exige prop visível sem chão/attachment, em apoio próprio diferente");
                spec.UsarCentroInicialMedido = !entry.ContainsKey("superficieInicial");
                if (entry.ContainsKey("superficieInicial"))
                {
                    var initialSurface = Obj(entry["superficieInicial"], field + ".superficieInicial", "direita,frente", "");
                    spec.SuperficieInicialRelativa = new GTA.Math.Vector3((float)Numeric(initialSurface["direita"], field + ".superficieInicial.direita", -10, 10),
                        (float)Numeric(initialSurface["frente"], field + ".superficieInicial.frente", -10, 10), 0f);
                }
            }
            else Prohibit(entry, "superficieInicial", field);
            if (support && (!spec.AssentarNoChao || !spec.VisivelInicial || entry.ContainsKey("attachmentInicial")))
                throw Error(field, "móvel de apoio exige chão, visibilidade inicial e nenhum attachment");
            string owner = null, ownerHand = null;
            bool pickupSupportAvailable = spec.ApoioInicial != null, pickupVisible = spec.VisivelInicial;
            if (entry.ContainsKey("attachmentInicial"))
            {
                string attachmentField = field + ".attachmentInicial";
                var attachment = Obj(entry["attachmentInicial"], attachmentField, "personagem,mao,offset,rotacao", "");
                spec.AtorInicial = Actor(attachment["personagem"], attachmentField + ".personagem", actors, false);
                spec.MaoInicial = Choice(attachment["mao"], attachmentField + ".mao", "direita,esquerda");
                spec.OffsetInicial = AttachmentVector(attachment["offset"], attachmentField + ".offset", 0.5, true);
                spec.RotacaoInicial = AttachmentVector(attachment["rotacao"], attachmentField + ".rotacao", 180, false);
                owner = spec.AtorInicial;
                ownerHand = spec.MaoInicial;
            }
            var events = entry.ContainsKey("eventos") ? Array(entry["eventos"], field + ".eventos", 0, 32) : new System.Collections.Generic.List<object>();
            int previous = -1;
            for (int j = 0; j < events.Count; j++)
            {
                string eventField = field + ".eventos[" + j + "]";
                var item = Obj(events[j], eventField, "tipo,inicioSegundos", "personagem,mao,offset,rotacao,recurso,esperaMaximaSegundos,apoio,superficie");
                var action = new ObjetosSerieDiretor.PropEvent { Tipo = Choice(item["tipo"], eventField + ".tipo", "anexar,transferir,soltar,ocultar,mostrar,guardar,colocar,pegar"),
                    InicioMs = Ms(item["inicioSegundos"], eventField + ".inicioSegundos", 0, 180) };
                if (action.InicioMs <= previous || action.InicioMs >= durationMs)
                    throw Error(eventField + ".inicioSegundos", "eventos exigem tempos únicos crescentes, antes do fim da cena; não prolongam a tomada");
                previous = action.InicioMs;
                if (item.ContainsKey("esperaMaximaSegundos"))
                {
                    if (action.Tipo != "transferir" && action.Tipo != "guardar" && action.Tipo != "colocar" && action.Tipo != "pegar") throw Error(eventField, "espera só se aplica a transferir/guardar/colocar");
                    action.EsperaMaximaMs = Ms(item["esperaMaximaSegundos"], eventField + ".esperaMaximaSegundos", 0.001, 2);
                    int deadline = action.InicioMs + action.EsperaMaximaMs;
                    int next = j + 1 < events.Count ? Ms(Required(Obj(events[j + 1], eventField + ".proximo", "tipo,inicioSegundos", "personagem,mao,offset,rotacao,recurso,esperaMaximaSegundos,apoio,superficie"), "inicioSegundos", eventField), eventField + ".proximo.inicioSegundos", 0, 180) : durationMs;
                    if (deadline >= durationMs || deadline >= next) throw Error(eventField, "janela de contato deve terminar antes da próxima ação desse objeto e do fim da cena");
                }
                if (action.Tipo != "colocar") { Prohibit(item, "apoio", eventField); Prohibit(item, "superficie", eventField); }
                if (action.Tipo == "anexar" || action.Tipo == "transferir")
                {
                    Prohibit(item, "recurso", eventField);
                    if (support) throw Error(eventField, "móvel de apoio não pode ser anexado à mão");
                    action.Ator = Actor(Required(item, "personagem", eventField), eventField + ".personagem", actors, false);
                    action.Mao = Choice(Required(item, "mao", eventField), eventField + ".mao", "direita,esquerda");
                    action.Offset = AttachmentVector(Required(item, "offset", eventField), eventField + ".offset", 0.5, true);
                    action.Rotacao = AttachmentVector(Required(item, "rotacao", eventField), eventField + ".rotacao", 180, false);
                    if (action.Tipo == "anexar" ? owner != null : owner == null || owner == action.Ator)
                        throw Error(eventField, action.Tipo == "anexar" ? "anexar exige objeto sem dono" : "transferir exige dono anterior diferente");
                    if (action.EsperaMaximaMs > 0)
                    {
                        var receiver = PropGesture(direction, action.Ator, action.InicioMs, action.EsperaMaximaMs, null, eventField);
                        var donor = PropGesture(direction, owner, action.InicioMs, action.EsperaMaximaMs, null, eventField);
                        if (receiver.Dicionario != "mp_common" || donor.Dicionario != "mp_common" || receiver.Clipe == "put_away_coke" || donor.Clipe == "put_away_coke")
                            throw Error(eventField, "transferência exige os dois clipes givetake1 catalogados");
                        action.Dicionario = receiver.Dicionario; action.Clipe = receiver.Clipe;
                        action.Doador = owner; action.ClipeDoador = donor.Clipe;
                        action.GestoInicioMs = System.Math.Max(receiver.InicioMs, donor.InicioMs);
                        action.GestoFimMs = System.Math.Min(receiver.InicioMs + receiver.DuracaoMs, donor.InicioMs + donor.DuracaoMs);
                    }
                    owner = action.Ator;
                    ownerHand = action.Mao; pickupSupportAvailable = false;
                }
                else if (action.Tipo == "pegar")
                {
                    action.Ator = Actor(Required(item, "personagem", eventField), eventField + ".personagem", actors, false);
                    action.Mao = Choice(Required(item, "mao", eventField), eventField + ".mao", "direita,esquerda");
                    action.Offset = item.ContainsKey("offset") ? AttachmentVector(item["offset"], eventField + ".offset", 0.5, true) : GTA.Math.Vector3.Zero;
                    action.Rotacao = item.ContainsKey("rotacao") ? AttachmentVector(item["rotacao"], eventField + ".rotacao", 180, false) : GTA.Math.Vector3.Zero;
                    string gestureCode = Text(Required(item, "recurso", eventField), eventField + ".recurso", 1, 48);
                    if (gestureCode != "entrega_a" && gestureCode != "entrega_b")
                        throw Error(eventField, "pegar exige recurso entrega_a ou entrega_b");
                    if (support || owner != null || !pickupVisible || !pickupSupportAvailable || action.EsperaMaximaMs <= 0)
                        throw Error(eventField, "pegar exige prop visivel sem dono em apoio proprio inicialmente medido ou colocado");
                    var gesture = PropGesture(direction, action.Ator, action.InicioMs, action.EsperaMaximaMs, gestureCode, eventField);
                    if (gesture.Dicionario != "mp_common" || (gesture.Clipe != "givetake1_a" && gesture.Clipe != "givetake1_b"))
                        throw Error(eventField, "pegar exige gesto real givetake catalogado");
                    action.Dicionario = gesture.Dicionario; action.Clipe = gesture.Clipe;
                    action.GestoInicioMs = gesture.InicioMs; action.GestoFimMs = gesture.InicioMs + gesture.DuracaoMs;
                    owner = action.Ator; ownerHand = action.Mao; pickupSupportAvailable = false;
                }
                else if (action.Tipo == "colocar")
                {
                    Prohibit(item, "offset", eventField); Prohibit(item, "rotacao", eventField);
                    action.Ator = Actor(Required(item, "personagem", eventField), eventField + ".personagem", actors, false);
                    action.Mao = item.ContainsKey("mao") ? Choice(item["mao"], eventField + ".mao", "direita,esquerda") : "direita";
                    if (action.Mao != ownerHand) throw Error(eventField + ".mao", "a mão deve corresponder ao attachment atual do dono");
                    action.Apoio = Code(Required(item, "apoio", eventField), eventField + ".apoio");
                    string gestureCode = Text(Required(item, "recurso", eventField), eventField + ".recurso", 1, 48);
                    if (support || action.Ator != owner || action.Apoio == id || action.EsperaMaximaMs <= 0) throw Error(eventField, "colocar exige objeto na mão do dono e apoio próprio diferente");
                    action.UsarCentroMedido = !item.ContainsKey("superficie");
                    if (item.ContainsKey("superficie"))
                    {
                        var surface = Obj(item["superficie"], eventField + ".superficie", "direita,frente", "");
                        action.SuperficieRelativa = new GTA.Math.Vector3((float)Numeric(surface["direita"], eventField + ".superficie.direita", -10, 10),
                            (float)Numeric(surface["frente"], eventField + ".superficie.frente", -10, 10), 0f);
                    }
                    var gesture = PropGesture(direction, action.Ator, action.InicioMs, action.EsperaMaximaMs, gestureCode, eventField);
                    if (gesture.Dicionario != "mp_common" || (gesture.Clipe != "givetake1_a" && gesture.Clipe != "givetake1_b")) throw Error(eventField, "colocar exige givetake real");
                    action.Dicionario = gesture.Dicionario; action.Clipe = gesture.Clipe;
                    action.GestoInicioMs = gesture.InicioMs; action.GestoFimMs = gesture.InicioMs + gesture.DuracaoMs;
                    owner = null; ownerHand = null; pickupSupportAvailable = true;
                }
                else if (action.Tipo == "guardar")
                {
                    Prohibit(item, "mao", eventField); Prohibit(item, "offset", eventField); Prohibit(item, "rotacao", eventField);
                    action.Ator = Actor(Required(item, "personagem", eventField), eventField + ".personagem", actors, false);
                    string gestureCode = Text(Required(item, "recurso", eventField), eventField + ".recurso", 1, 48);
                    if (code != "envelope" || action.Ator != owner || action.EsperaMaximaMs <= 0)
                        throw Error(eventField, "guardar exige envelope do próprio dono e esperaMaximaSegundos limitada");
                    var gesture = PropGesture(direction, action.Ator, action.InicioMs, action.EsperaMaximaMs, gestureCode, eventField);
                    if (gesture.Dicionario != "mp_common_miss" || gesture.Clipe != "put_away_coke") throw Error(eventField, "guardar exige o clipe candidato put_away_coke conferido");
                    action.Dicionario = gesture.Dicionario; action.Clipe = gesture.Clipe;
                    action.GestoInicioMs = gesture.InicioMs; action.GestoFimMs = gesture.InicioMs + gesture.DuracaoMs;
                    owner = null; ownerHand = null; pickupSupportAvailable = false;
                }
                else
                {
                    Prohibit(item, "recurso", eventField); Prohibit(item, "esperaMaximaSegundos", eventField);
                    Prohibit(item, "personagem", eventField); Prohibit(item, "mao", eventField); Prohibit(item, "offset", eventField); Prohibit(item, "rotacao", eventField);
                    if (action.Tipo == "soltar")
                    {
                        if (owner == null) throw Error(eventField, "soltar exige dono anterior");
                        owner = null; ownerHand = null; pickupSupportAvailable = false;
                    }
                    if (action.Tipo == "ocultar") pickupVisible = false;
                    if (action.Tipo == "mostrar") pickupVisible = true;
                }
                spec.Eventos.Add(action);
            }
            result.Add(spec);
        }
        foreach (var spec in result)
        {
            if (spec.ApoioInicial != null)
            {
                ObjetosSerieDiretor.PropSpec initialSupport = null;
                foreach (var candidate in result) if (candidate.Codigo == spec.ApoioInicial) initialSupport = candidate;
                if (initialSupport == null || System.Array.IndexOf("cadeira_simples,poltrona,banco,banco_sono_c1_02,mesa,mesa_grande_c8_autor,cadeira_casa,cama_casa,cama_quarto,cama_simples,mesa_banco_candidata,mesa_limpeza_bar,bancada_limpeza_candidata".Split(','), initialSupport.Recurso) < 0 || !initialSupport.AssentarNoChao || !initialSupport.VisivelInicial || !System.String.IsNullOrEmpty(initialSupport.AtorInicial))
                    throw Error("cena.objetos.apoioInicial", "apoio inicial exige móvel próprio visível assentado");
                foreach (var movement in initialSupport.Eventos) if (movement.Tipo != "mostrar") throw Error("cena.objetos.apoioInicial", "apoio inicial não pode mover/ocultar durante a cena");
            }
            foreach (var placement in spec.Eventos)
                if (placement.Tipo == "colocar")
                {
                    ObjetosSerieDiretor.PropSpec support = null;
                    foreach (var candidate in result) if (candidate.Codigo == placement.Apoio) support = candidate;
                    if (support == null || System.Array.IndexOf("cadeira_simples,poltrona,banco,banco_sono_c1_02,mesa,mesa_grande_c8_autor,cadeira_casa,cama_casa,cama_quarto,cama_simples,mesa_banco_candidata,mesa_limpeza_bar,bancada_limpeza_candidata".Split(','), support.Recurso) < 0 || !support.AssentarNoChao || !support.VisivelInicial || !System.String.IsNullOrEmpty(support.AtorInicial))
                        throw Error("cena.objetos.eventos.apoio", "colocar exige apoio próprio assentado e visível");
                }
        }
        if (result.Count > 0)
        {
            try { ObjetosSerieDiretor.ValidatePickupHands(result, durationMs); ObjetosSerieDiretor.VerifySourceEvidence(result); }
            catch (System.InvalidOperationException ex) { throw Error("cena.objetos.recurso", ex.Message); }
            catch (System.IO.IOException ex) { throw Error("cena.objetos.recurso", "não foi possível ler a fonte: " + ex.Message); }
            catch (System.UnauthorizedAccessException) { throw Error("cena.objetos.recurso", "acesso à fonte não permitido"); }
            catch (System.Text.DecoderFallbackException) { throw Error("cena.objetos.recurso", "a fonte precisa estar em UTF-8 válido"); }
            catch (System.ArgumentException) { throw Error("cena.objetos.recurso", "caminho de origem inválido"); }
            catch (System.NotSupportedException) { throw Error("cena.objetos.recurso", "caminho de origem inválido"); }
        }
        return result;
    }

    private static AcoesSerieDiretor.ActorAction PropGesture(System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> direction,
        string actor, int start, int wait, string resource, string field)
    {
        foreach (var gesture in direction)
            if (gesture.Ator == (actor ?? "").ToLowerInvariant() && gesture.Tipo == "clipe" &&
                (gesture.SobrePoseSentada || gesture.Recurso == "entrega_a" || gesture.Recurso == "entrega_b") &&
                (resource == null || gesture.Recurso == resource) && gesture.InicioMs <= start && gesture.InicioMs + gesture.DuracaoMs >= start + wait)
                return gesture;
        throw Error(field, "a janela inteira exige gesto de entrega catalogado ou gesto sentado compatível declarado para esse ator");
    }

    private static GTA.Math.Vector3 AttachmentVector(object value, string field, double limit, bool lengthLimit)
    {
        var vector = Obj(value, field, "x,y,z", "");
        double x = Numeric(vector["x"], field + ".x", -limit, limit), y = Numeric(vector["y"], field + ".y", -limit, limit), z = Numeric(vector["z"], field + ".z", -limit, limit);
        if (lengthLimit && x * x + y * y + z * z > limit * limit + 1e-12) throw Error(field, "o ajuste completo deve ficar até0,5m da mão");
        return new GTA.Math.Vector3((float)x, (float)y, (float)z);
    }

    private static System.Collections.Generic.Dictionary<string, PropResource> ReadPropResources(string path)
    {
        var root = Obj(ReadJson(path, "catalogos.objetos_serie"), "catalogos.objetos_serie", "versao,objetos", "");
        Int(root["versao"], "catalogos.objetos_serie.versao", 1, 1);
        var values = Array(root["objetos"], "catalogos.objetos_serie.objetos", 0, 80);
        var result = new System.Collections.Generic.Dictionary<string, PropResource>(System.StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            string field = "catalogos.objetos_serie.objetos[" + i + "]";
            var entry = Obj(values[i], field, "codigo,tipo,modelo,origem,estado", "");
            string code = Text(entry["codigo"], field + ".codigo", 1, 48);
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "\\A[a-z][a-z0-9_]{0,47}\\z") || result.ContainsKey(code)) throw Error(field + ".codigo", "código minúsculo inválido ou repetido");
            string expected;
            try { expected = ObjetosSerieDiretor.GetCatalogModel(code); }
            catch (System.InvalidOperationException) { throw Error(field + ".codigo", "recurso não reconhecido pelo helper de objetos"); }
            string model = Text(entry["modelo"], field + ".modelo", 1, 64);
            if (model != expected) throw Error(field + ".modelo", "modelo diverge do recurso conferido do helper");
            string type = Choice(entry["tipo"], field + ".tipo", "prop,apoio");
            bool support = System.Array.IndexOf("cadeira_simples,poltrona,banco,banco_sono_c1_02,mesa,mesa_grande_c8_autor,cadeira_casa,cama_casa,cama_quarto,cama_simples,mesa_banco_candidata,mesa_limpeza_bar,bancada_limpeza_candidata".Split(','), code) >= 0 || code == "cadeira_entrada_autor";
            if ((type == "apoio") != support) throw Error(field + ".tipo", "tipo diverge da classificação do recurso");
            Text(entry["estado"], field + ".estado", 1, MaxChars);
            var origin = Obj(entry["origem"], field + ".origem", "arquivo,linha,sha256,texto", "fonte");
            if (origin.ContainsKey("fonte") && Choice(origin["fonte"], field + ".origem.fonte", "objetos,apoios") != (support ? "apoios" : "objetos"))
                throw Error(field + ".origem.fonte", "fonte precisa corresponder ao tipo de recurso");
            var resource = new PropResource { Type = type, Model = model, File = ListedPath(Text(origin["arquivo"], field + ".origem.arquivo", 1, MaxChars), field + ".origem.arquivo", "PropList.txt", true),
                Line = Int(origin["linha"], field + ".origem.linha", 1, 500000), Sha = Text(origin["sha256"], field + ".origem.sha256", 64, 64).ToUpperInvariant() };
            if (!System.Text.RegularExpressions.Regex.IsMatch(resource.Sha, "\\A[0-9A-F]{64}\\z")) throw Error(field + ".origem.sha256", "use SHA256 hexadecimal de64 caracteres");
            if (Text(origin["texto"], field + ".origem.texto", 1, 64) != model) throw Error(field + ".origem.texto", "texto de origem precisa ser o modelo literal");
            result.Add(code, resource);
        }
        return result;
    }

    private static System.Collections.Generic.List<InterpretacaoDiretor.ActorSpec> ReadInterpretation(
        System.Collections.Generic.Dictionary<string, object> scene, System.Collections.Generic.List<string> actors, ref int totalMs)
    {
        var result = new System.Collections.Generic.List<InterpretacaoDiretor.ActorSpec>();
        if (!scene.ContainsKey("interpretacao")) return result;
        var values = Array(scene["interpretacao"], "cena.interpretacao", 0, 64);
        for (int i = 0; i < values.Count; i++)
        {
            string field = "cena.interpretacao[" + i + "]";
            var entry = Obj(values[i], field, "personagem,inicioSegundos,duracaoSegundos", "expressao,olhar");
            var spec = new InterpretacaoDiretor.ActorSpec { Ator = Actor(entry["personagem"], field + ".personagem", actors, false).ToLowerInvariant(),
                InicioMs = Ms(entry["inicioSegundos"], field + ".inicioSegundos", 0, 180), DuracaoMs = Ms(entry["duracaoSegundos"], field + ".duracaoSegundos", 0.1, 180),
                OlharTipo = "padrao", Deslocamento = GTA.Math.Vector3.Zero };
            if (entry.ContainsKey("expressao")) spec.Expressao = Choice(entry["expressao"], field + ".expressao", "normal,chateado,feliz,machucado,comraiva,estressado,convencido");
            if (entry.ContainsKey("olhar"))
            {
                var look = Obj(entry["olhar"], field + ".olhar", "tipo", "alvo,destino");
                spec.OlharTipo = Choice(look["tipo"], field + ".olhar.tipo", "padrao,ator,objeto,chao,horizonte,marca,desvio");
                if (spec.OlharTipo == "ator")
                {
                    Prohibit(look, "destino", field + ".olhar");
                    spec.Alvo = Actor(Required(look, "alvo", field + ".olhar"), field + ".olhar.alvo", actors, false).ToLowerInvariant();
                    if (spec.Alvo == spec.Ator) throw Error(field + ".olhar.alvo", "escolha outro ator");
                }
                else if (spec.OlharTipo == "objeto")
                {
                    Prohibit(look, "destino", field + ".olhar");
                    spec.Alvo = Code(Required(look, "alvo", field + ".olhar"), field + ".olhar.alvo");
                }
                else
                {
                    Prohibit(look, "alvo", field + ".olhar");
                    if (spec.OlharTipo == "marca")
                    {
                        float[] mark = Mark(Required(look, "destino", field + ".olhar"), field + ".olhar.destino");
                        spec.Deslocamento = new GTA.Math.Vector3(mark[0], mark[1], mark[2]);
                    }
                    else Prohibit(look, "destino", field + ".olhar");
                }
            }
            int end = spec.InicioMs + spec.DuracaoMs;
            if (end > 180000) throw Error(field + ".duracaoSegundos", "o fim da interpretação ultrapassa180 segundos");
            if (spec.Expressao == null && spec.OlharTipo == "padrao") throw Error(field, "declare ao menos uma expressão ou um olhar específico");
            foreach (var earlier in result)
                if (earlier.Ator == spec.Ator && Overlap(spec.InicioMs, end, earlier.InicioMs, earlier.InicioMs + earlier.DuracaoMs))
                    throw Error(field, "há interpretações simultâneas para o mesmo ator");
            totalMs = System.Math.Max(totalMs, end);
            result.Add(spec);
        }
        if (result.Count > 0)
        {
            InterpretacaoDiretor.ValidateSpecs(result);
            InterpretacaoDiretor.CheckSources();
        }
        return result;
    }

    private static System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> ReadDirection(
        System.Collections.Generic.Dictionary<string, object> scene, string catalogDirectory, System.Collections.Generic.List<string> actors,
        System.Collections.Generic.List<SpeechInterval> speeches, System.Collections.Generic.List<Acao> legacy, ref int totalMs)
    {
        var result = new System.Collections.Generic.List<AcoesSerieDiretor.ActorAction>();
        if (!scene.ContainsKey("direcao")) return result;
        var values = Array(scene["direcao"], "cena.direcao", 0, 64);
        var resources = ReadBodies(System.IO.Path.Combine(catalogDirectory, "corpos_serie.json"));
        var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<AcoesSerieDiretor.ActorAction>>(System.StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            string field = "cena.direcao[" + i + "]";
            var entry = Obj(values[i], field, "tipo,personagem,inicioSegundos,duracaoSegundos", "recurso,loop,corpoInteiro,grupoPareado,destino,velocidade,alvo,manterPoseFinal,sobrePoseSentada,preservarDuranteFala,clipeDurantePercurso");
            string type = Choice(entry["tipo"], field + ".tipo", "clipe,andar,virar,celular");
            var action = new AcoesSerieDiretor.ActorAction { Tipo = type, Ator = Actor(entry["personagem"], field + ".personagem", actors, false).ToLowerInvariant(),
                InicioMs = Ms(entry["inicioSegundos"], field + ".inicioSegundos", 0, 180), DuracaoMs = Ms(entry["duracaoSegundos"], field + ".duracaoSegundos", 0.1, 180),
                Deslocamento = GTA.Math.Vector3.Zero, Rotacao = GTA.Math.Vector3.Zero, Velocidade = 1f };
            int end = action.InicioMs + action.DuracaoMs;
            if (end > 180000) throw Error(field + ".duracaoSegundos", "o fim da direção ultrapassa180 segundos");
            if (type == "clipe" || type == "celular")
            {
                Prohibit(entry, "clipeDurantePercurso", field);
                Prohibit(entry, "destino", field); Prohibit(entry, "velocidade", field); Prohibit(entry, "alvo", field);
                action.Loop = OptionalBool(entry, "loop", field, true);
                action.CorpoInteiro = OptionalBool(entry, "corpoInteiro", field, type == "clipe");
                action.ManterPoseFinal = OptionalBool(entry, "manterPoseFinal", field, true);
                AssignBody(action, Required(entry, "recurso", field), field + ".recurso", resources, "clipe");
                action.PreservarDuranteFala = OptionalBool(entry, "preservarDuranteFala", field, false);
                if (action.PreservarDuranteFala && (type != "clipe" || !AcoesSerieDiretor.CanControlSpeechBody(action.Dicionario, action.Clipe)))
                    throw Error(field + ".preservarDuranteFala", "exige clipe físico real da whitelist durante fala");
                if (type != "clipe") Prohibit(entry, "sobrePoseSentada", field);
                action.SobrePoseSentada = OptionalBool(entry, "sobrePoseSentada", field, false);
                if (action.SobrePoseSentada && (action.CorpoInteiro || action.Loop || action.ManterPoseFinal || entry.ContainsKey("grupoPareado") ||
                    !PosesSerieDiretor.IsOverlayClip(action.Dicionario, action.Clipe)))
                    throw Error(field + ".sobrePoseSentada", "exige clipe permitido, corpoInteiro/loop/manterPoseFinal false, sem grupoPareado");
                if (entry.ContainsKey("grupoPareado"))
                {
                    action.GrupoPareado = Text(entry["grupoPareado"], field + ".grupoPareado", 1, 48);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(action.GrupoPareado, "\\A[A-Za-z][A-Za-z0-9_]{0,47}\\z") || type != "clipe" || !action.CorpoInteiro)
                        throw Error(field + ".grupoPareado", "use identificador ASCII de até48 caracteres somente em clipes de corpo inteiro");
                    System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> group;
                    if (!groups.TryGetValue(action.GrupoPareado, out group)) { group = new System.Collections.Generic.List<AcoesSerieDiretor.ActorAction>(); groups.Add(action.GrupoPareado, group); }
                    group.Add(action);
                }
            }
            else
            {
                Prohibit(entry, "loop", field); Prohibit(entry, "corpoInteiro", field); Prohibit(entry, "grupoPareado", field); Prohibit(entry, "manterPoseFinal", field);
                Prohibit(entry, "sobrePoseSentada", field);
                if (type == "andar")
                {
                    Prohibit(entry, "alvo", field);
                    float[] mark = Mark(Required(entry, "destino", field), field + ".destino");
                    action.Deslocamento = new GTA.Math.Vector3(mark[0], mark[1], mark[2]);
                    action.Velocidade = OptionalNumber(entry, "velocidade", field, 0.2, 2, 1);
                    if (entry.ContainsKey("recurso")) { AssignBody(action, entry["recurso"], field + ".recurso", resources, "clipset"); action.ClipSet = action.Dicionario; }
                    if (entry.ContainsKey("clipeDurantePercurso"))
                    {
                        string code = Choice(entry["clipeDurantePercurso"], field + ".clipeDurantePercurso", "carregar_bandeja,ligacao_f,ligacao_m,carregar_sacola_autor,servir_champanhe_autor");
                        var secondary = new AcoesSerieDiretor.ActorAction(); AssignBody(secondary, code, field + ".clipeDurantePercurso", resources, "clipe");
                        string expectedDictionary = code == "carregar_bandeja" ? "anim@heists@box_carry@" : code == "servir_champanhe_autor" ? "anim@move_f@waitress" : code == "carregar_sacola_autor" ? "move_weapon@jerrycan@generic" : code == "ligacao_f" ? "cellphone@female" : "cellphone@";
                        string expectedClip = (code == "carregar_bandeja" || code == "carregar_sacola_autor" || code == "servir_champanhe_autor") ? "idle" : "cellphone_call_listen_base";
                        if (secondary.Dicionario != expectedDictionary || secondary.Clipe != expectedClip) throw Error(field, "canal corporal diverge do par literal instalado");
                        VerifyBodySources(new System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> { secondary });
                        action.ClipeDurantePercurso = code; action.PercursoDicionario = secondary.Dicionario; action.PercursoClipe = secondary.Clipe;
                        action.PercursoOrigemArquivo = secondary.OrigemArquivo; action.PercursoOrigemLinha = secondary.OrigemLinha; action.PercursoOrigemSha256 = secondary.OrigemSha256;
                        action.PreservarDuranteFala = OptionalBool(entry, "preservarDuranteFala", field, false);
                    }
                    else Prohibit(entry, "preservarDuranteFala", field);
                }
                else
                {
                    Prohibit(entry, "recurso", field); Prohibit(entry, "velocidade", field); Prohibit(entry, "preservarDuranteFala", field); Prohibit(entry, "clipeDurantePercurso", field);
                    if (entry.ContainsKey("alvo") == entry.ContainsKey("destino")) throw Error(field, "virar exige exatamente alvo ou destino relativo");
                    if (entry.ContainsKey("alvo"))
                    {
                        action.Alvo = Actor(entry["alvo"], field + ".alvo", actors, false).ToLowerInvariant();
                        if (action.Alvo == action.Ator) throw Error(field + ".alvo", "escolha outro ator");
                    }
                    else { float[] destination = Mark(entry["destino"], field + ".destino"); action.Deslocamento = new GTA.Math.Vector3(destination[0], destination[1], destination[2]); }
                }
            }
            foreach (AcoesSerieDiretor.ActorAction earlier in result)
                if (earlier.Ator == action.Ator && Overlap(action.InicioMs, end, earlier.InicioMs, earlier.InicioMs + earlier.DuracaoMs))
                    throw Error(field, "há direções simultâneas para o mesmo ator");
            foreach (Acao earlier in legacy)
                if (earlier.Personagem == action.Ator && Overlap(action.InicioMs, end, earlier.InicioMs, earlier.InicioMs + earlier.DuracaoMs))
                    throw Error(field, "direção coincide com uma ação legada do mesmo ator");
            if (type != "virar" && !action.PreservarDuranteFala)
                foreach (SpeechInterval speech in speeches)
                    if (speech.Actor == action.Ator && Overlap(action.InicioMs, end, speech.Start, speech.End))
                        throw Error(field, "direção corporal não pode coincidir com a fala desse ator");
            totalMs = System.Math.Max(totalMs, end);
            result.Add(action);
        }
        foreach (var item in groups)
        {
            var group = item.Value;
            if (group.Count != 2) throw Error("cena.direcao.grupoPareado", "cada grupo exige exatamente2 atores distintos");
            var a = group[0]; var b = group[1];
            if (a.Ator == b.Ator || a.InicioMs != b.InicioMs || a.DuracaoMs != b.DuracaoMs || a.Dicionario != b.Dicionario || a.Loop != b.Loop || a.ManterPoseFinal != b.ManterPoseFinal)
                throw Error("cena.direcao.grupoPareado", "o par exige atores distintos, mesmo dicionário, intervalo, loop e pose final");
        }
        VerifyBodySources(result);
        return result;
    }

    private static void AssignBody(AcoesSerieDiretor.ActorAction action, object value, string field,
        System.Collections.Generic.Dictionary<string, BodyResource> resources, string type)
    {
        string code = Text(value, field, 1, 48);
        if (code == "office_cell_sleep_f") throw Error(field, "office_cell_sleep_f apenas pose tecnico84, nunca direcao/andar/producao.");
        BodyResource resource;
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "\\A[a-z][a-z0-9_]{0,47}\\z") || !resources.TryGetValue(code, out resource) || resource.Type != type)
            throw Error(field, "use código minúsculo existente no catálogo de corpos, do tipo " + type);
        action.Recurso = code; action.Dicionario = resource.Dictionary; action.Clipe = resource.Clip; action.OrigemArquivo = resource.File;
        action.OrigemLinha = resource.Line; action.OrigemSha256 = resource.Sha;
    }

    private static System.Collections.Generic.Dictionary<string, BodyResource> ReadBodies(string path)
    {
        var root = Obj(ReadJson(path, "catalogos.corpos_serie"), "catalogos.corpos_serie", "versao,estado,animacoes", "");
        Int(root["versao"], "catalogos.corpos_serie.versao", 1, 1); Text(root["estado"], "catalogos.corpos_serie.estado", 1, 100);
        var entries = Array(root["animacoes"], "catalogos.corpos_serie.animacoes", 0, 256);
        var result = new System.Collections.Generic.Dictionary<string, BodyResource>(System.StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            string field = "catalogos.corpos_serie.animacoes[" + i + "]";
            var entry = Obj(entries[i], field, "codigo,tipo,dicionario,animacao,origem,restricoes", "");
            string code = Text(entry["codigo"], field + ".codigo", 1, 48);
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "\\A[a-z][a-z0-9_]{0,47}\\z") || result.ContainsKey(code)) throw Error(field + ".codigo", "código minúsculo inválido ou repetido");
            var resource = new BodyResource { Type = Choice(entry["tipo"], field + ".tipo", "clipe,clipset"),
                Dictionary = BodyName(entry["dicionario"], field + ".dicionario"), Clip = BodyName(entry["animacao"], field + ".animacao") };
            if (resource.Type == "clipset" && resource.Clip != "walk") throw Error(field + ".animacao", "clipset de caminhada exige o par literal com walk");
            var origin = Obj(entry["origem"], field + ".origem", "arquivo,linha,sha256,texto", "");
            resource.File = BodyPath(Text(origin["arquivo"], field + ".origem.arquivo", 1, 1024), field + ".origem.arquivo");
            resource.Line = Int(origin["linha"], field + ".origem.linha", 1, 500000);
            resource.Sha = Text(origin["sha256"], field + ".origem.sha256", 64, 64);
            if (!System.Text.RegularExpressions.Regex.IsMatch(resource.Sha, "\\A[0-9A-Fa-f]{64}\\z")) throw Error(field + ".origem.sha256", "use SHA256 hexadecimal de64 caracteres");
            resource.Sha = resource.Sha.ToUpperInvariant();
            if (Text(origin["texto"], field + ".origem.texto", 1, 321) != resource.Dictionary + " " + resource.Clip) throw Error(field + ".origem.texto", "o texto da origem deve ser o par literal dicionário/clipe");
            var restrictions = Array(entry["restricoes"], field + ".restricoes", 0, 32);
            foreach (object restriction in restrictions) Text(restriction, field + ".restricoes", 1, 500);
            result.Add(code, resource);
        }
        return result;
    }

    private static string BodyName(object value, string field)
    {
        string name = Text(value, field, 1, 160);
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "\\A[A-Za-z0-9_@.\\-]+\\z")) throw Error(field, "use o nome literal ASCII da lista de animações");
        return name;
    }

    private static string BodyPath(string raw, string field)
    { return ListedPath(raw, field, "PedAnimList.txt", false); }

    private static string ListedPath(string raw, string field, string filename, bool requireMenyoo)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(raw, "\\A[A-Za-z]:[\\\\/]" ) || raw.Substring(2).IndexOf(':') >= 0 ||
            raw.IndexOfAny(new char[] { '<', '>', '"', '|', '?', '*', '\r', '\n', '\t', '\0' }) >= 0)
            throw Error(field, "use " + filename + " em caminho local absoluto, sem stream alternativo");
        foreach (string part in raw.Split(new char[] { '/', '\\' }))
            if (part == "." || part == "..") throw Error(field, "não usar componentes . ou .. no caminho da fonte");
        string path;
        try { path = System.IO.Path.GetFullPath(raw); }
        catch (System.ArgumentException) { throw Error(field, "caminho de origem inválido"); }
        catch (System.NotSupportedException) { throw Error(field, "caminho de origem inválido"); }
        catch (System.IO.IOException) { throw Error(field, "caminho de origem inválido ou longo demais"); }
        if (!System.String.Equals(System.IO.Path.GetFileName(path), filename, System.StringComparison.OrdinalIgnoreCase) ||
            (requireMenyoo && !System.String.Equals(new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(path)).Name, "menyooStuff", System.StringComparison.OrdinalIgnoreCase)))
            throw Error(field, "a origem deve ser a " + filename + " instalada" + (requireMenyoo ? " em menyooStuff" : ""));
        return path;
    }

    private static void VerifyBodySources(System.Collections.Generic.List<AcoesSerieDiretor.ActorAction> direction)
    {
        var sources = new System.Collections.Generic.Dictionary<string, BodySource>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var action in direction)
        {
            if (System.String.IsNullOrEmpty(action.OrigemArquivo)) continue;
            string path = BodyPath(action.OrigemArquivo, "cena.direcao.recurso");
            BodySource source;
            if (!sources.TryGetValue(path, out source)) { source = new BodySource { File = path, Sha = action.OrigemSha256 }; sources.Add(path, source); }
            if (source.Sha != action.OrigemSha256) throw Error("cena.direcao.recurso", "hashes conflitantes para a mesma origem");
            string pair = action.Dicionario + " " + action.Clipe, previous;
            if (source.Lines.TryGetValue(action.OrigemLinha, out previous) && previous != pair) throw Error("cena.direcao.recurso", "uma linha de origem não pode indicar pares diferentes");
            source.Lines[action.OrigemLinha] = pair;
        }
        foreach (BodySource source in sources.Values)
        {
            try
            {
                RequireBodyPath(source.File);
                using (var stream = new System.IO.FileStream(source.File, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    if (stream.Length > 64L * 1024 * 1024) throw Error("cena.direcao.recurso", "a lista de origem excede64 MiB");
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        if (System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != source.Sha) throw Error("cena.direcao.recurso", "SHA256 da lista de origem diverge do catálogo");
                    stream.Position = 0;
                    using (var reader = new System.IO.StreamReader(stream, new System.Text.UTF8Encoding(false, true), true))
                    {
                        int line = 0, found = 0; string row, expected;
                        while ((row = reader.ReadLine()) != null)
                        {
                            if (++line > 500000) throw Error("cena.direcao.recurso", "a lista excede500000 linhas");
                            if (!source.Lines.TryGetValue(line, out expected)) continue;
                            if (row.Trim() != expected) throw Error("cena.direcao.recurso", "o par literal diverge da linha de origem");
                            if (++found == source.Lines.Count) break;
                        }
                        if (found != source.Lines.Count) throw Error("cena.direcao.recurso", "linha de origem ausente na lista");
                    }
                }
                RequireBodyPath(source.File);
            }
            catch (System.IO.IOException ex) { throw Error("cena.direcao.recurso", "não foi possível ler a fonte: " + ex.Message); }
            catch (System.UnauthorizedAccessException) { throw Error("cena.direcao.recurso", "acesso à fonte não permitido"); }
            catch (System.Text.DecoderFallbackException) { throw Error("cena.direcao.recurso", "a fonte precisa estar em UTF-8 válido"); }
        }
    }

    private static void RequireBodyPath(string path)
    {
        string current = path;
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) && (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw Error("cena.direcao.recurso", "não usar fonte redirecionada por vínculo");
            current = System.IO.Path.GetDirectoryName(current);
        }
    }

    public static void ActivateVisuals(string pathCatalog, System.Collections.Generic.Dictionary<string, VisualDiretor.Visual> visuals)
    {
        string profile = System.Environment.GetEnvironmentVariable("USERPROFILE");
        if (System.String.IsNullOrWhiteSpace(profile))
            profile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (System.String.IsNullOrWhiteSpace(profile)) throw Error("elenco", "perfil do usuário não encontrado");
        string data = System.IO.Path.Combine(profile, "GTA_Diretor");
        string path = System.IO.Path.GetFullPath(pathCatalog);
        if (!System.String.Equals(path, System.IO.Path.Combine(data, "catalogos", "personagens.json"), System.StringComparison.OrdinalIgnoreCase))
            throw Error("elenco", "ativação permitida somente no catálogo de personagens do runtime do perfil");
        RequireVisualPath(path);
        string[] codes = new string[] { "DJANIRA_POBRE", "DJANIRA_RICA", "DINJER", "DONA_IRACI", "O_CHEFE",
            "CAPANGA_1", "CAPANGA_2", "CAPANGA_3", "CAPANGA_4", "COLEGA_1", "COLEGA_2", "COLEGA_3", "ATENDENTE" };
        if (visuals == null || visuals.Count != codes.Length) throw Error("elenco", "ativação exige exatamente os13 visuais reais da série");
        var verified = new System.Collections.Generic.Dictionary<string, VisualDiretor.Visual>(System.StringComparer.Ordinal);
        string directory = System.IO.Path.GetDirectoryName(path);
        foreach (string code in codes)
        {
            if (!visuals.ContainsKey(code) || visuals[code] == null) throw Error("elenco." + code, "visual real ausente");
            string visualPath = System.IO.Path.Combine(directory, "visuais", code + ".xml");
            RequireVisualPath(visualPath);
            VisualDiretor.Visual supplied = visuals[code];
            VisualDiretor.Visual actual = VisualDiretor.Load(visualPath, supplied.Model);
            if (actual.Sha256 != supplied.Sha256 || actual.FaceSha256 != supplied.FaceSha256)
                throw Error("elenco." + code, "XML salvo diverge do visual gerado; não ativar");
            verified.Add(code, actual);
        }
        if (verified["DJANIRA_POBRE"].Model != "mp_f_freemode_01" || verified["DJANIRA_RICA"].Model != "mp_f_freemode_01" ||
            verified["DJANIRA_POBRE"].FaceSha256 != verified["DJANIRA_RICA"].FaceSha256 ||
            !VisualDiretor.SameFaceDetails(verified["DJANIRA_POBRE"], verified["DJANIRA_RICA"]))
            throw Error("elenco.DJANIRA", "os dois figurinos precisam manter o mesmo modelo e fingerprint facial");
        // Ler e validar tudo antes de alterar; TESTE_HOMEM/TESTE_MULHER e a origem existente ficam preservados.
        string originalHash = VisualCatalogHash(path);
        ReadCharacters(path);
        var root = CatalogRoot(path, "personagens");
        var entries = Array(root["personagens"], "catalogos.personagens", 0, MaxChars);
        var changed = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (object value in entries)
        {
            var entry = Obj(value, "catalogos.personagens", "codigo,nome,modelo,pronto,estado,origem", "visualArquivo,visualSHA256");
            string code = Code(entry["codigo"], "catalogos.personagens.codigo");
            if (!verified.ContainsKey(code)) continue;
            VisualDiretor.Visual visual = verified[code];
            if (entry["modelo"] != null && Text(entry["modelo"], "catalogos.personagens.modelo", 1, 64) != visual.Model)
                throw Error("elenco." + code, "modelo gerado diverge do modelo anteriormente declarado");
            entry["modelo"] = visual.Model;
            entry["pronto"] = true;
            entry["estado"] = "gerado_no_jogo;teste_visual_pendente";
            entry["visualArquivo"] = code + ".xml";
            entry["visualSHA256"] = visual.Sha256;
            string provenance = "Usuário autorizou criação aleatória; modelo conferido no PedList; aparência capturada por natives do GTA e XML real; teste visual pendente.";
            string original = Text(entry["origem"], "catalogos.personagens.origem", 1, MaxChars);
            if (!original.Contains(provenance)) entry["origem"] = original + " | " + provenance;
            changed.Add(code);
        }
        if (changed.Count != codes.Length) throw Error("elenco", "os13 códigos precisam existir no catálogo original");
        var builder = new System.Text.StringBuilder();
        WriteCatalogJson(builder, root); builder.AppendLine();
        if (builder.Length > MaxChars) throw Error("elenco", "catálogo atualizado excede100000 caracteres");
        byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(builder.ToString());
        if (bytes.Length > MaxBytes) throw Error("elenco", "catálogo atualizado excede1MiB");
        string stamp = System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", Invariant) + "-" + System.Guid.NewGuid().ToString("N");
        string backup = System.IO.Path.Combine(data, "backups", "elenco", stamp);
        RequireVisualPath(backup); System.IO.Directory.CreateDirectory(backup); RequireVisualPath(backup);
        string previous = System.IO.Path.Combine(backup, "personagens-anterior.json");
        System.IO.File.Copy(path, previous, false);
        if (VisualCatalogHash(previous) != originalHash) throw Error("elenco", "backup do catálogo não conferiu; ativação cancelada");
        string temporary = path + ".ativar-" + System.Guid.NewGuid().ToString("N") + ".tmp";
        RequireVisualPath(temporary);
        using (var stream = new System.IO.FileStream(temporary, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        string newHash = VisualCatalogHash(temporary);
        var evidence = new System.Collections.Generic.Dictionary<string, object>();
        evidence.Add("modo", "ativar13visuais_reais"); evidence.Add("catalogo", path); evidence.Add("backup", previous);
        evidence.Add("sha256Anterior", originalHash); evidence.Add("sha256Novo", newHash);
        evidence.Add("djaniraFaceSHA256", verified["DJANIRA_POBRE"].FaceSha256);
        evidence.Add("estado", "gerado_no_jogo;teste_visual_pendente");
        var manifest = new System.Text.StringBuilder(); WriteCatalogJson(manifest, evidence);
        VisualCatalogWriteNew(System.IO.Path.Combine(backup, "manifesto.json"), manifest.ToString());
        if (VisualCatalogHash(path) != originalHash) throw Error("elenco", "catálogo mudou durante a geração; preservar edição e não ativar");
        RequireVisualPath(path); RequireVisualPath(temporary);
        System.IO.File.Replace(temporary, path, null);
        if (VisualCatalogHash(path) != newHash) throw Error("elenco", "catálogo ativado não conferiu; backup preservado");
        VisualCatalogWriteNew(System.IO.Path.Combine(backup, "conclusao.json"), "{\"estado\":\"ativado\",\"testeVisual\":false,\"sha256\":\"" + newHash + "\"}\n");
    }

    // Sidecar opcional apenas86; nenhuma chave nova e aceita em cena de producao.
    private static void BindMorph86(string sceneId, string scenePath, string catalogDirectory,
        string sourceHashBeforeRead, System.Collections.Generic.List<ObjetosSerieDiretor.PropSpec> props)
    {
        if (sceneId != "teste_fila_86") return;
        string path = System.IO.Path.Combine(System.IO.Path.GetFullPath(catalogDirectory), "trial_morph86.json");
        if (!System.IO.File.Exists(path))
        {
            if (sourceHashBeforeRead != null) throw Error("trial_morph86", "sidecar removido durante parse");
            return;
        }
        if (sourceHashBeforeRead == null || props.Count != 1 || props[0].Codigo != "PAPEL_PROVA" ||
            !MorfologiaPapel86Diretor.IsLiteralPaper(props[0].Recurso, props[0].Modelo) ||
            props[0].AssentarNoChao || !props[0].VisivelInicial || props[0].AtorInicial != "DJANIRA_POBRE" ||
            props[0].MaoInicial != "direita" || props[0].Eventos.Count != 0 || !System.String.IsNullOrEmpty(props[0].ApoioInicial) ||
            props[0].OffsetInicial.Length() != 0f || props[0].RotacaoInicial.Length() != 0f)
            throw Error("trial_morph86", "somente fonte86 nota_s propria e zero transform de mao");
        string sidecarHash = MorfologiaPapel86Diretor.EvidenceHash(path, 4096);
        var raw = Obj(ReadJson(path, "trial_morph86"), "trial_morph86", "versao,id,cena,fonteSHA256,segmento,fase", "");
        Int(raw["versao"], "trial_morph86.versao", 1, 1);
        string id = Text(raw["id"], "trial_morph86.id", 32, 32);
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "\\A[0-9a-f]{32}\\z")) throw Error("trial_morph86.id", "GUID32 minusculo unico exigido");
        if (Text(raw["cena"], "trial_morph86.cena", 1, 48) != "teste_fila_86") throw Error("trial_morph86.cena", "somenteID86 literal");
        string sourceHash = Text(raw["fonteSHA256"], "trial_morph86.fonteSHA256", 64, 64).ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(sourceHash, "\\A[0-9A-F]{64}\\z") || sourceHash != sourceHashBeforeRead ||
            sourceHash != MorfologiaPapel86Diretor.EvidenceHash(scenePath, 1048576) || sidecarHash != MorfologiaPapel86Diretor.EvidenceHash(path, 4096))
            throw Error("trial_morph86.fonteSHA256", "source/sidecar mudaram ou nao correspondem ao snapshot86");
        bool pamphlet = props[0].Recurso == "papel_preview86_pamphlet_s";
        int segment = Int(raw["segmento"], "trial_morph86.segmento", 0, pamphlet ? 3 : 6);
        float phase = (float)Numeric(raw["fase"], "trial_morph86.fase", 0, 1);
        string animFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(props[0].OrigemArquivo), "PedAnimList.txt");
        props[0].Morph86 = new MorfologiaPapel86Diretor.Spec(id, System.IO.Path.GetFullPath(scenePath), sourceHash,
            path, sidecarHash, animFile, segment, phase, pamphlet);
    }

    private static string VisualCatalogHash(string path)
    {
        using (var hash = System.Security.Cryptography.SHA256.Create())
        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            return System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }

    private static void VisualCatalogWriteNew(string path, string text)
    {
        RequireVisualPath(path);
        byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(text);
        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
    }

    private static void WriteCatalogJson(System.Text.StringBuilder output, object value)
    {
        if (value == null) { output.Append("null"); return; }
        if (value is bool) { output.Append((bool)value ? "true" : "false"); return; }
        var number = value as JsonNumber;
        if (number != null) { output.Append(number.Raw); return; }
        var dictionary = value as System.Collections.Generic.Dictionary<string, object>;
        if (dictionary != null)
        {
            output.Append('{'); bool first = true;
            foreach (var pair in dictionary)
            { if (!first) output.Append(','); first = false; WriteCatalogJson(output, pair.Key); output.Append(':'); WriteCatalogJson(output, pair.Value); }
            output.Append('}'); return;
        }
        var list = value as System.Collections.Generic.List<object>;
        if (list != null)
        {
            output.Append('['); for (int i = 0; i < list.Count; i++) { if (i > 0) output.Append(','); WriteCatalogJson(output, list[i]); }
            output.Append(']'); return;
        }
        string text = value as string;
        if (text == null) throw Error("elenco", "tipo não suportado ao preservar catálogo JSON");
        output.Append('"');
        foreach (char c in text)
        {
            if (c == '"' || c == '\\') { output.Append('\\'); output.Append(c); }
            else if (c < 32) { output.Append("\\u"); output.Append(((int)c).ToString("x4", Invariant)); }
            else output.Append(c);
        }
        output.Append('"');
    }

    private static System.Collections.Generic.Dictionary<string, CatalogActor> ReadCharacters(string path)
    {
        var root = CatalogRoot(path, "personagens");
        var result = new System.Collections.Generic.Dictionary<string, CatalogActor>(System.StringComparer.Ordinal);
        var entries = Array(root["personagens"], "catalogos.personagens", 0, MaxChars);
        for (int i = 0; i < entries.Count; i++)
        {
            string field = "catalogos.personagens[" + i + "]";
            var item = Obj(entries[i], field, "codigo,nome,modelo,pronto,estado,origem", "visualArquivo,visualSHA256");
            string code = Code(item["codigo"], field + ".codigo");
            if (result.ContainsKey(code)) throw Error(field + ".codigo", "código duplicado no catálogo");
            bool ready = Bool(item["pronto"], field + ".pronto");
            string model = item["modelo"] == null ? null : Text(item["modelo"], field + ".modelo", 1, 64);
            if (model != null && !System.Text.RegularExpressions.Regex.IsMatch(model, "\\A[a-z0-9_]{1,64}\\z"))
                throw Error(field + ".modelo", "o identificador do modelo é inválido");
            if (ready && model == null) throw Error(field + ".modelo", "um personagem pronto exige um modelo conferido");
            Text(item["estado"], field + ".estado", 1, MaxChars); Text(item["origem"], field + ".origem", 1, MaxChars);
            string visualFile = null, visualSha = null;
            if (item.ContainsKey("visualArquivo") != item.ContainsKey("visualSHA256"))
                throw Error(field, "visualArquivo e visualSHA256 devem aparecer juntos");
            if (item.ContainsKey("visualArquivo"))
            {
                visualFile = Text(item["visualArquivo"], field + ".visualArquivo", 1, 52);
                if (visualFile != code + ".xml")
                    throw Error(field + ".visualArquivo", "use somente " + code + ".xml, sem caminho, em catalogos/visuais");
                visualSha = Text(item["visualSHA256"], field + ".visualSHA256", 64, 64).ToUpperInvariant();
                if (!System.Text.RegularExpressions.Regex.IsMatch(visualSha, "\\A[0-9A-F]{64}\\z"))
                    throw Error(field + ".visualSHA256", "use SHA256 hexadecimal de64 caracteres");
            }
            result.Add(code, new CatalogActor { Name = Text(item["nome"], field + ".nome", 1, 100), Model = model,
                Ready = ready, VisualFile = visualFile, VisualSha = visualSha });
        }
        return result;
    }

    private static System.Collections.Generic.HashSet<string> ReadPlaces(string path)
    {
        var root = CatalogRoot(path, "locais");
        var result = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var entries = Array(root["locais"], "catalogos.locais", 0, MaxChars);
        for (int i = 0; i < entries.Count; i++)
        {
            string field = "catalogos.locais[" + i + "]";
            var item = Obj(entries[i], field, "codigo,nome,estado,origem", "pronto");
            string code = Code(item["codigo"], field + ".codigo");
            if (!result.Add(code)) throw Error(field + ".codigo", "código duplicado no catálogo");
            Text(item["nome"], field + ".nome", 1, 100); Text(item["estado"], field + ".estado", 1, MaxChars);
            Text(item["origem"], field + ".origem", 1, MaxChars);
            if (item.ContainsKey("pronto")) Bool(item["pronto"], field + ".pronto");
        }
        return result;
    }

    private static System.Collections.Generic.HashSet<string> ReadAnimations(string directory)
    {
        var root = CatalogRoot(System.IO.Path.Combine(directory, "animacoes.json"), "animacoes");
        var canonical = ReadCanonicalGestures(System.IO.Path.Combine(directory, "gestos_fala.xml"));
        var result = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var entries = Array(root["animacoes"], "catalogos.animacoes", 0, MaxChars);
        for (int i = 0; i < entries.Count; i++)
        {
            string field = "catalogos.animacoes[" + i + "]";
            var item = Obj(entries[i], field, "codigo,tipo,estado,origem,homem,mulher", "");
            string code = Choice(item["codigo"], field + ".codigo", "neutro,explicando,negando,implorando,irritado,machucado");
            if (!result.Add(code)) throw Error(field + ".codigo", "código duplicado no catálogo");
            Choice(item["tipo"], field + ".tipo", "gesto");
            Text(item["origem"], field + ".origem", 1, MaxChars);
            if (!canonical.ContainsKey(code)) throw Error(field + ".codigo", "o gesto não está no XML canônico gestos_fala.xml");
            System.Xml.XmlElement source = canonical[code];
            string state = Text(item["estado"], field + ".estado", 1, MaxChars);
            if (state != source.GetAttribute("estado")) throw Error(field + ".estado", "o estado diverge do XML canônico");
            VerifyVariant(item["homem"], field + ".homem", source, "Homem");
            VerifyVariant(item["mulher"], field + ".mulher", source, "Mulher");
        }
        return result;
    }

    private static System.Collections.Generic.Dictionary<string, System.Xml.XmlElement> ReadCanonicalGestures(string path)
    {
        var document = new System.Xml.XmlDocument(); document.XmlResolver = null;
        try
        {
            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > MaxBytes) throw Error("catalogos.gestos_fala.xml", "arquivo maior que 1 MiB");
                var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                    XmlResolver = null, MaxCharactersInDocument = MaxChars };
                using (var reader = System.Xml.XmlReader.Create(stream, settings)) document.Load(reader);
            }
        }
        catch (System.Xml.XmlException ex) { throw Error("catalogos.gestos_fala.xml", "XML inválido ou DTD proibido: linha " + ex.LineNumber); }
        catch (System.IO.IOException) { throw Error("catalogos.gestos_fala.xml", "não foi possível ler o catálogo canônico"); }
        var root = document.DocumentElement;
        if (root == null || root.Name != "GestosFala" || root.GetAttribute("versao") != "1")
            throw Error("catalogos.gestos_fala.xml", "esperado GestosFala, versão 1");
        var result = new System.Collections.Generic.Dictionary<string, System.Xml.XmlElement>(System.StringComparer.Ordinal);
        foreach (System.Xml.XmlElement element in root.SelectNodes("Gesto"))
        {
            string id = element.GetAttribute("id");
            if (result.ContainsKey(id)) throw Error("catalogos.gestos_fala.xml", "gesto duplicado: " + id);
            result.Add(id, element);
        }
        return result;
    }

    private static void VerifyVariant(object value, string path, System.Xml.XmlElement canonical, string sex)
    {
        var variant = Obj(value, path, "dicionario,animacao,estado", "modeloTestado,cenaTeste,confirmadoEm");
        var nodes = canonical.SelectNodes(sex);
        if (nodes.Count != 1) throw Error(path, "a variante precisa existir uma única vez no XML canônico");
        var source = (System.Xml.XmlElement)nodes[0];
        foreach (string field in variant.Keys)
        {
            string text = Text(variant[field], path + "." + field, 1, MaxChars);
            if (text != source.GetAttribute(field)) throw Error(path + "." + field, "o valor diverge do XML canônico; não substituir pares ou confirmação silenciosamente");
        }
    }

    private static System.Collections.Generic.Dictionary<string, object> CatalogRoot(string path, string kind)
    {
        string field = "catalogos." + kind;
        var root = Obj(ReadJson(path, field), field, "versao," + kind, "");
        Int(root["versao"], field + ".versao", 1, 1);
        return root;
    }

    private static object ReadJson(string path, string label)
    {
        byte[] bytes;
        try
        {
            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > MaxBytes) throw Error(label, "arquivo maior que 1 MiB");
                bytes = new byte[(int)stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0) throw Error(label, "o arquivo foi interrompido durante a leitura");
                    read += count;
                }
            }
        }
        catch (System.IO.IOException) { throw Error(label, "não foi possível ler " + System.IO.Path.GetFileName(path)); }
        catch (System.UnauthorizedAccessException) { throw Error(label, "acesso ao arquivo não permitido"); }
        string content;
        try { content = new System.Text.UTF8Encoding(false, true).GetString(bytes); }
        catch (System.Text.DecoderFallbackException) { throw Error(label, "o arquivo precisa estar em UTF-8 válido"); }
        if (content.Length > MaxChars) throw Error(label, "arquivo maior que 100000 caracteres");
        if (content.Length > 0 && content[0] == '\ufeff') content = content.Substring(1);
        return new JsonParser(content, label).Parse();
    }

    private static System.Collections.Generic.Dictionary<string, object> Obj(object value, string path, string required, string optional)
    {
        var result = value as System.Collections.Generic.Dictionary<string, object>;
        if (result == null) throw Error(path, "esperado um objeto JSON");
        var known = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (string key in (required + "," + optional).Split(',')) if (key.Length > 0) known.Add(key);
        foreach (string key in result.Keys) if (!known.Contains(key)) throw Error(path + "." + key, "campo desconhecido");
        foreach (string key in required.Split(',')) if (key.Length > 0 && !result.ContainsKey(key)) throw Error(path + "." + key, "campo obrigatório ausente");
        return result;
    }

    private static object Required(System.Collections.Generic.Dictionary<string, object> obj, string key, string path)
    {
        object value;
        if (!obj.TryGetValue(key, out value)) throw Error(path + "." + key, "campo obrigatório ausente");
        return value;
    }

    private static System.Collections.Generic.List<object> Array(object value, string path, int min, int max)
    {
        var result = value as System.Collections.Generic.List<object>;
        if (result == null || result.Count < min || result.Count > max) throw Error(path, "esperada uma lista com " + min + " a " + max + " itens");
        return result;
    }

    private static string Text(object value, string path, int min, int max)
    {
        string result = value as string;
        if (result == null || result.Length < min || result.Length > max || System.String.IsNullOrWhiteSpace(result))
            throw Error(path, "esperado texto com " + min + " a " + max + " caracteres, sem ficar vazio");
        return result;
    }

    private static string Code(object value, string path)
    {
        string code = Text(value, path, 1, 48);
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "\\A[A-Z][A-Z0-9_]{0,47}\\z"))
            throw Error(path, "use código iniciado por letra maiúscula, com maiúsculas, números ou sublinhado");
        return code;
    }

    private static bool Bool(object value, string path)
    {
        if (!(value is bool)) throw Error(path, "esperado true ou false");
        return (bool)value;
    }

    private static bool OptionalBool(System.Collections.Generic.Dictionary<string, object> obj, string key, string path, bool fallback)
    { return obj.ContainsKey(key) ? Bool(obj[key], path + "." + key) : fallback; }

    private static double Numeric(object value, string path, double min, double max)
    {
        var number = value as JsonNumber;
        if (number == null || System.Double.IsNaN(number.Value) || System.Double.IsInfinity(number.Value) || number.Value < min || number.Value > max)
            throw Error(path, "esperado número finito entre " + min.ToString(Invariant) + " e " + max.ToString(Invariant));
        return number.Value;
    }

    private static int Int(object value, string path, int min, int max)
    {
        double number = Numeric(value, path, min, max);
        if (!((JsonNumber)value).Integer) throw Error(path, "esperado inteiro, sem casas decimais ou expoente");
        return (int)number;
    }

    private static float OptionalNumber(System.Collections.Generic.Dictionary<string, object> obj, string key, string path, double min, double max, float fallback)
    { return obj.ContainsKey(key) ? (float)Numeric(obj[key], path + "." + key, min, max) : fallback; }

    private static int Ms(object value, string path, double min, double max)
    {
        Numeric(value, path, min, max);
        string raw = ((JsonNumber)value).Raw;
        bool negative = raw[0] == '-';
        if (negative) raw = raw.Substring(1);
        int exponentAt = raw.IndexOfAny(new char[] { 'e', 'E' });
        string mantissa = exponentAt < 0 ? raw : raw.Substring(0, exponentAt);
        int point = mantissa.IndexOf('.');
        int fractions = point < 0 ? 0 : mantissa.Length - point - 1;
        string digits = mantissa.Replace(".", "").TrimStart('0');
        if (digits.Length == 0) return 0;
        int exponent = 0;
        if (exponentAt >= 0 && !System.Int32.TryParse(raw.Substring(exponentAt + 1), System.Globalization.NumberStyles.AllowLeadingSign, Invariant, out exponent))
            throw Error(path, "o tempo deve ter resolução exata de milissegundos");
        long shift = (long)exponent - fractions + 3;
        if (shift < 0)
        {
            if (-shift >= digits.Length) throw Error(path, "o tempo deve ter resolução exata de milissegundos");
            int keep = digits.Length + (int)shift;
            for (int i = keep; i < digits.Length; i++) if (digits[i] != '0') throw Error(path, "o tempo deve ter resolução exata de milissegundos");
            digits = digits.Substring(0, keep);
        }
        else
        {
            if (shift + digits.Length > 6) throw Error(path, "o tempo ultrapassa o limite permitido");
            digits += new string('0', (int)shift);
        }
        int milliseconds;
        if (!System.Int32.TryParse(digits, System.Globalization.NumberStyles.None, Invariant, out milliseconds))
            throw Error(path, "o tempo deve ter resolução exata de milissegundos");
        if (negative) milliseconds = -milliseconds;
        if (milliseconds < min * 1000 || milliseconds > max * 1000) throw Error(path, "o tempo ultrapassa o limite permitido");
        return milliseconds;
    }

    private static string Choice(object value, string path, string choices)
    {
        string text = Text(value, path, 1, 64);
        foreach (string choice in choices.Split(',')) if (text == choice) return text;
        throw Error(path, "use uma destas opções: " + choices.Replace(",", ", "));
    }

    private static string Shot(object value, string path)
    { return Choice(value, path, "LONGE,DOIS,MEDIA,PERTO,BAIXA,ALTA,ATRAS,OLHOS"); }

    private static string OptionalShot(System.Collections.Generic.Dictionary<string, object> obj, string key, string fallback)
    { return obj.ContainsKey(key) ? Shot(obj[key], "cena.camera." + key) : fallback; }

    private static string Actor(object value, string path, System.Collections.Generic.List<string> actors, bool ignoredLegacyLimit)
    {
        string code = Code(value, path);
        int index = actors.IndexOf(code);
        if (index < 0) throw Error(path, "o personagem precisa pertencer ao elenco da cena");
        return code;
    }

    private static string CameraActor(object value, string path, System.Collections.Generic.List<string> actors)
    {
        if (actors.Count == 0)
        {
            if (value != null) throw Error(path, "sem elenco, use null para enquadrar o ambiente");
            return "";
        }
        return Actor(value, path, actors, false);
    }

    private static void RequireVisualPath(string path)
    {
        string current = System.IO.Path.GetFullPath(path);
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) &&
                (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw Error("catalogos.visuais", "não usar caminho redirecionado por vínculo: " + current);
            current = System.IO.Path.GetDirectoryName(current);
        }
    }

    private static void EnvironmentShot(string shot, string path, System.Collections.Generic.List<string> actors)
    {
        if (actors.Count == 0 && shot != "LONGE" && shot != "MEDIA" && shot != "BAIXA" && shot != "ALTA")
            throw Error(path, "sem elenco, use LONGE, MEDIA, BAIXA ou ALTA");
    }

    private static float[] Mark(object value, string path)
    {
        var mark = Obj(value, path, "direita,frente", "altura");
        return new float[] { (float)Numeric(mark["direita"], path + ".direita", -10, 10),
            (float)Numeric(mark["frente"], path + ".frente", -10, 10), OptionalNumber(mark, "altura", path, -2, 2, 0) };
    }

    private static void RequireAnimation(string code, string path, System.Collections.Generic.HashSet<string> available)
    { if (!available.Contains(code)) throw Error(path, "a animação " + code + " não está no catálogo canônico de gestos"); }

    private static void Prohibit(System.Collections.Generic.Dictionary<string, object> obj, string key, string path)
    { if (obj.ContainsKey(key)) throw Error(path + "." + key, "este campo não é permitido para esse tipo"); }

    private static bool Overlap(int startA, int endA, int startB, int endB) { return startA < endB && startB < endA; }
    private static string Weather(string value)
    {
        switch (value)
        {
            case "limpo": return "CLEAR"; case "ensolarado": return "EXTRASUNNY"; case "nublado": return "CLOUDS";
            case "encoberto": return "OVERCAST"; case "chuva": return "RAIN"; case "trovoada": return "THUNDER";
            case "nevoa": return "FOGGY"; case "smog": return "SMOG"; case "abrindo": return "CLEARING"; case "neutro": return "NEUTRAL";
            default: throw Error("cena.ambiente.clima", "clima desconhecido; use limpo, ensolarado, nublado, encoberto, chuva, trovoada, nevoa, smog, abrindo ou neutro");
        }
    }

    private static System.Xml.XmlElement Element(System.Xml.XmlElement parent, string name)
    {
        var child = parent.OwnerDocument.CreateElement(name); parent.AppendChild(child); return child;
    }
    private static void Set(System.Xml.XmlElement element, params string[] pairs)
    { for (int i = 0; i < pairs.Length; i += 2) element.SetAttribute(pairs[i], pairs[i + 1]); }
    private static string Number(float number) { return number.ToString("R", Invariant); }
    private static string Seconds(int milliseconds) { return (milliseconds / 1000m).ToString("0.###", Invariant); }
    private static string Boolean(bool value) { return value ? "true" : "false"; }
    private static System.InvalidOperationException Error(string path, string message)
    { return new System.InvalidOperationException(path + ": " + message + "."); }

    private sealed class JsonParser
    {
        private readonly string text, label;
        private int at;
        public JsonParser(string text, string label) { this.text = text; this.label = label; }
        public object Parse()
        {
            object result = Value(label, 0); White();
            if (at != text.Length) throw Error(label, "conteúdo extra após o JSON, posição " + at);
            return result;
        }
        private object Value(string path, int depth)
        {
            if (depth > 32) throw Error(path, "JSON aninhado demais");
            White();
            if (at >= text.Length) throw Error(path, "JSON interrompido");
            char c = text[at];
            if (c == '{') return ObjectValue(path, depth + 1);
            if (c == '[') return ArrayValue(path, depth + 1);
            if (c == '"') return StringValue(path);
            if (c == '-' || (c >= '0' && c <= '9')) return NumberValue(path);
            if (Literal("true")) return true;
            if (Literal("false")) return false;
            if (Literal("null")) return null;
            throw Error(path, "valor JSON inválido, posição " + at + "; comentários, NaN e infinito são proibidos");
        }
        private System.Collections.Generic.Dictionary<string, object> ObjectValue(string path, int depth)
        {
            at++; White(); var result = new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.Ordinal);
            if (Take('}')) return result;
            while (true)
            {
                White(); if (at >= text.Length || text[at] != '"') throw Error(path, "a chave precisa de aspas, posição " + at);
                string key = StringValue(path); string field = path + "." + key;
                if (result.ContainsKey(key)) throw Error(field, "chave duplicada");
                White(); Expect(':', field); result.Add(key, Value(field, depth)); White();
                if (Take('}')) return result;
                Expect(',', path);
            }
        }
        private System.Collections.Generic.List<object> ArrayValue(string path, int depth)
        {
            at++; White(); var result = new System.Collections.Generic.List<object>();
            if (Take(']')) return result;
            while (true)
            {
                result.Add(Value(path + "[" + result.Count + "]", depth)); White();
                if (Take(']')) return result;
                Expect(',', path);
            }
        }
        private string StringValue(string path)
        {
            Expect('"', path); var builder = new System.Text.StringBuilder();
            while (at < text.Length)
            {
                char c = text[at++];
                if (c == '"')
                {
                    string value = builder.ToString();
                    try { System.Xml.XmlConvert.VerifyXmlChars(value); }
                    catch (System.Xml.XmlException) { throw Error(path, "texto contém caractere incompatível com o XML interno"); }
                    return value;
                }
                if (c < 32) throw Error(path, "caractere de controle sem escape no texto");
                if (c != '\\') { builder.Append(c); continue; }
                if (at >= text.Length) throw Error(path, "escape interrompido");
                char escape = text[at++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break; case '\\': builder.Append('\\'); break; case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break; case 'f': builder.Append('\f'); break; case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break; case 't': builder.Append('\t'); break;
                    case 'u':
                        char first = Unicode(path); builder.Append(first);
                        if (System.Char.IsHighSurrogate(first))
                        {
                            if (at + 2 > text.Length || text[at] != '\\' || text[at + 1] != 'u') throw Error(path, "par Unicode incompleto");
                            at += 2; char second = Unicode(path);
                            if (!System.Char.IsLowSurrogate(second)) throw Error(path, "par Unicode inválido");
                            builder.Append(second);
                        }
                        else if (System.Char.IsLowSurrogate(first)) throw Error(path, "par Unicode inválido");
                        break;
                    default: throw Error(path, "escape JSON inválido");
                }
            }
            throw Error(path, "texto sem aspas de fechamento");
        }
        private char Unicode(string path)
        {
            if (at + 4 > text.Length) throw Error(path, "escape Unicode interrompido");
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                char c = text[at++]; int digit = c >= '0' && c <= '9' ? c - '0' :
                    c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0) throw Error(path, "escape Unicode inválido");
                value = value * 16 + digit;
            }
            return (char)value;
        }
        private JsonNumber NumberValue(string path)
        {
            int start = at; Take('-');
            if (at >= text.Length) throw Error(path, "número interrompido");
            if (Take('0')) { }
            else { if (text[at] < '1' || text[at] > '9') throw Error(path, "número JSON inválido"); Digits(); }
            bool integer = true;
            if (Take('.')) { integer = false; int before = at; Digits(); if (at == before) throw Error(path, "faltam casas após o ponto decimal"); }
            if (at < text.Length && (text[at] == 'e' || text[at] == 'E'))
            {
                integer = false; at++;
                if (at < text.Length && (text[at] == '+' || text[at] == '-')) at++;
                int before = at; Digits(); if (at == before) throw Error(path, "expoente JSON inválido");
            }
            string raw = text.Substring(start, at - start); double number;
            if (!System.Double.TryParse(raw, System.Globalization.NumberStyles.Float, Invariant, out number) || System.Double.IsNaN(number) || System.Double.IsInfinity(number))
                throw Error(path, "número precisa ser finito");
            return new JsonNumber { Raw = raw, Value = number, Integer = integer };
        }
        private void Digits() { while (at < text.Length && text[at] >= '0' && text[at] <= '9') at++; }
        private bool Literal(string word)
        {
            if (at + word.Length > text.Length || System.String.CompareOrdinal(text, at, word, 0, word.Length) != 0) return false;
            at += word.Length; return true;
        }
        private void White() { while (at < text.Length && (text[at] == ' ' || text[at] == '\t' || text[at] == '\r' || text[at] == '\n')) at++; }
        private bool Take(char c) { if (at < text.Length && text[at] == c) { at++; return true; } return false; }
        private void Expect(char c, string path) { if (!Take(c)) throw Error(path, "esperado '" + c + "', posição " + at); }
    }
}

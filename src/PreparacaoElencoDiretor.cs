// Concatenar à fonte principal. Prepara um ped temporário por vez, sem alterar o jogador.
// Os XML reais ficam fixos entre episódios/temporadas; nenhum visual existente é sobrescrito.
public sealed class PreparacaoElencoDiretor : System.IDisposable
{
    private static readonly string[] Codigos = new string[] {
        "DJANIRA_POBRE", "DJANIRA_RICA", "DINJER", "DONA_IRACI", "O_CHEFE",
        "CAPANGA_1", "CAPANGA_2", "CAPANGA_3", "CAPANGA_4", "COLEGA_1", "COLEGA_2", "COLEGA_3", "ATENDENTE" };
    private static readonly string[] Modelos = new string[] {
        "mp_f_freemode_01", "mp_f_freemode_01", "mp_f_freemode_01", "a_f_o_genstreet_01", "a_m_m_business_01",
        "s_m_m_highsec_01", "s_m_m_highsec_02", "a_m_y_business_02", "a_m_y_business_03",
        "a_m_y_genstreet_01", "a_m_y_genstreet_02", "a_m_y_stbla_01", "u_m_m_bankman" };
    private readonly string dataRoot, directory;
    private readonly System.Action<string, string> log;
    private readonly int configuredSeed;
    private readonly System.Collections.Generic.Dictionary<string, VisualDiretor.Visual> visuals =
        new System.Collections.Generic.Dictionary<string, VisualDiretor.Visual>(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<string> reused =
        new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
    private GTA.Ped temporaryPed;
    private GTA.Model currentModel;
    private GTA.Math.Vector3 position;
    private System.IntPtr ownedAddress;
    private int ownedHandle, index;
    private bool modelRequested, busy, ready, disposed;
    private System.Diagnostics.Stopwatch timer;
    private string state = "inativo", pendingStatus;
    private const int ModelTimeoutMs = 5000;
    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    public PreparacaoElencoDiretor(string dataRoot, System.Action<string, string> log, int seed)
    {
        string profile = System.Environment.GetEnvironmentVariable("USERPROFILE");
        if (System.String.IsNullOrWhiteSpace(profile))
            profile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (System.String.IsNullOrWhiteSpace(profile)) throw Error("Perfil do usuário não disponível.");
        this.dataRoot = CheckedPath(dataRoot);
        if (!System.String.Equals(this.dataRoot, System.IO.Path.Combine(CheckedPath(profile), "GTA_Diretor"), System.StringComparison.OrdinalIgnoreCase))
            throw Error("A preparação usa somente %USERPROFILE%\\GTA_Diretor.");
        this.directory = Inside(System.IO.Path.Combine(this.dataRoot, "catalogos", "visuais"));
        this.log = log;
        configuredSeed = seed;
        Seed = seed;
    }

    public bool IsBusy { get { return busy; } }
    public bool IsReady { get { return ready && !disposed; } }
    public int CompletedCount { get { return visuals.Count; } }
    public int TotalCount { get { return Codigos.Length; } }
    public string CurrentCode { get { return index < Codigos.Length ? Codigos[index] : ""; } }
    public int Seed { get; private set; }
    public System.Collections.Generic.Dictionary<string, VisualDiretor.Visual> Visuais
    {
        get { return new System.Collections.Generic.Dictionary<string, VisualDiretor.Visual>(visuals, System.StringComparer.Ordinal); }
    }

    public void Start(GTA.Math.Vector3 position)
    {
        if (disposed) throw Error("A preparação do elenco já foi encerrada.");
        if (busy) throw Error("A preparação do elenco já está em andamento.");
        if (!Finite(position.X) || !Finite(position.Y) || !Finite(position.Z)) throw Error("Posição de preparo inválida.");
        Cleanup();
        ready = false;
        this.position = position;
        visuals.Clear(); reused.Clear();
        RequireDirectory(directory);
        Seed = LoadOrSaveSeed();
        // Ler todos os arquivos presentes antes de criar qualquer ped temporário.
        for (int i = 0; i < Codigos.Length; i++)
        {
            string path = Inside(System.IO.Path.Combine(directory, Codigos[i] + ".xml"));
            if (!System.IO.File.Exists(path)) continue;
            VisualDiretor.Visual visual = VisualDiretor.Load(path, Modelos[i]);
            visuals.Add(Codigos[i], visual); reused.Add(Codigos[i]);
            Log("VISUAL_REUTILIZADO", "Codigo=" + Codigos[i] + "; modelo=" + visual.Model + "; SHA256=" + visual.Sha256);
        }
        CheckDjanira();
        VisualDiretor.Visual djanira;
        if (visuals.TryGetValue("DJANIRA_POBRE", out djanira) || visuals.TryGetValue("DJANIRA_RICA", out djanira))
            ElencoAutomaticoDiretor.SetDjaniraBase(djanira);
        // Manifesto anterior é prova de identidade fixa, inclusive quando um XML sumiu.
        string manifest = Inside(System.IO.Path.Combine(directory, "elenco-manifesto.xml"));
        if (System.IO.File.Exists(manifest)) VerifyManifest(manifest);
        index = 0;
        busy = true; ready = false;
        state = "proximo";
        SetStatus("Preparando elenco: " + visuals.Count + "/13 visuais já salvos.");
    }

    // Uma etapa por Tick. O texto retornado muda só na transição, para evitar spam.
    public string Update()
    {
        string status = pendingStatus;
        pendingStatus = null;
        if (!busy) return status;
        if (state == "proximo")
        {
            while (index < Codigos.Length && visuals.ContainsKey(Codigos[index])) index++;
            if (index == Codigos.Length)
            {
                Finish();
                return TakeStatus(status);
            }
            currentModel = new GTA.Model(Modelos[index]);
            if (!currentModel.IsInCdImage || !currentModel.IsPed) throw Error("Modelo de personagem indisponível: " + Modelos[index]);
            currentModel.Request();
            modelRequested = true;
            timer = System.Diagnostics.Stopwatch.StartNew();
            state = "modelo";
            SetStatus("Elenco " + (index + 1) + "/13: carregando " + Codigos[index] + ".");
            return TakeStatus(status);
        }
        if (state == "modelo")
        {
            if (!currentModel.IsLoaded)
            {
                if (timer.ElapsedMilliseconds >= ModelTimeoutMs) throw Error("Modelo não carregou em cinco segundos: " + Modelos[index]);
                return status;
            }
            temporaryPed = GTA.Ped.Create(currentModel, position, 0f);
            if (System.Object.ReferenceEquals(temporaryPed, null) || !temporaryPed.Exists())
                throw Error("Não foi possível criar o personagem temporário: " + Codigos[index]);
            ownedHandle = temporaryPed.Handle;
            ownedAddress = temporaryPed.MemoryAddress;
            temporaryPed.IsPersistent = true;
            temporaryPed.IsVisible = false;
            temporaryPed.IsCollisionEnabled = false;
            temporaryPed.IsPositionFrozen = true;
            temporaryPed.IsInvincible = true;
            temporaryPed.CanRagdoll = false;
            temporaryPed.BlockPermanentEvents = true;
            temporaryPed.Task.ClearAllImmediately();
            temporaryPed.Task.StandStill(-1);
            ElencoAutomaticoDiretor.Generate(temporaryPed, Codigos[index], Seed, log);
            timer.Restart();
            state = "aparencia";
            SetStatus("Elenco " + (index + 1) + "/13: preparando rosto e figurino de " + Codigos[index] + ".");
            return TakeStatus(status);
        }
        if (state == "aparencia")
        {
            RequireOwned();
            bool appearanceReady = ElencoAutomaticoDiretor.IsReady(temporaryPed);
            string adjustment = ElencoAutomaticoDiretor.TakeStatus(temporaryPed);
            if (!System.String.IsNullOrEmpty(adjustment)) SetStatus("Elenco " + (index + 1) + "/13: " + adjustment);
            if (!appearanceReady)
            {
                if (timer.ElapsedMilliseconds >= ElencoAutomaticoDiretor.GenerationTimeoutMs)
                    throw Error("A aparência não terminou de preparar em dois minutos: " + Codigos[index]);
                return TakeStatus(status);
            }
            state = "salvar";
            return status;
        }
        if (state == "salvar")
        {
            RequireOwned();
            string path = Inside(System.IO.Path.Combine(directory, Codigos[index] + ".xml"));
            if (System.IO.File.Exists(path)) throw Error("Um visual apareceu durante o preparo; arquivo preservado. Reinicie: " + Codigos[index]);
            VisualDiretor.Visual generated = ElencoAutomaticoDiretor.Save(path, temporaryPed, Codigos[index], Seed);
            VisualDiretor.Visual actual = VisualDiretor.Load(path, Modelos[index]);
            if (generated == null || generated.Sha256 != actual.Sha256 || generated.FaceSha256 != actual.FaceSha256)
                throw Error("A conferência do XML real salvo falhou: " + Codigos[index]);
            visuals.Add(Codigos[index], actual);
            CheckDjanira();
            if (Codigos[index] == "DJANIRA_POBRE" || Codigos[index] == "DJANIRA_RICA")
                ElencoAutomaticoDiretor.SetDjaniraBase(actual);
            Log("VISUAL_GERADO", "Codigo=" + Codigos[index] + "; modelo=" + actual.Model + "; SHA256=" + actual.Sha256 +
                "; faceSHA256=" + actual.FaceSha256 + "; seed=" + Seed.ToString(Inv) + "; teste_visual_pendente=true");
            state = "remover";
            return status;
        }
        if (state == "remover")
        {
            RemoveTemporaryPed();
            ReleaseModel();
            index++;
            state = "proximo";
            SetStatus("Elenco: " + visuals.Count + "/13 visuais reais salvos.");
            return TakeStatus(status);
        }
        throw Error("Estado de preparação desconhecido.");
    }

    private void Finish()
    {
        if (ownedHandle != 0 || !System.Object.ReferenceEquals(temporaryPed, null))
            throw Error("O personagem temporário precisa ser removido antes de ativar o elenco.");
        if (visuals.Count != Codigos.Length) throw Error("Elenco incompleto; preservar os arquivos já salvos.");
        CheckDjanira();
        string manifest = Inside(System.IO.Path.Combine(directory, "elenco-manifesto.xml"));
        if (System.IO.File.Exists(manifest)) VerifyManifest(manifest);
        else SaveManifest(manifest);
        // ActivateVisuals faz seu próprio backup/conferência antes de alterar o catálogo.
        CenaJsonDiretor.ActivateVisuals(Inside(System.IO.Path.Combine(dataRoot, "catalogos", "personagens.json")), Visuais);
        ready = true; busy = false; state = "pronto";
        SetStatus("Elenco preparado: 13 visuais salvos e fixos. A aparência ainda será vista no jogo.");
        Log("ELENCO_PREPARADO", "Visuais=13; seed=" + Seed.ToString(Inv) + "; manifesto=" + manifest + "; teste_visual_pendente=true");
    }

    private int LoadOrSaveSeed()
    {
        string path = Inside(System.IO.Path.Combine(directory, "elenco-semente.xml"));
        if (System.IO.File.Exists(path))
        {
            System.Xml.XmlElement root = ReadXml(path).DocumentElement;
            int actual;
            if (root.Name != "sementeElenco" || root.GetAttribute("versao") != "1" || root.GetAttribute("temporada") != "1" ||
                !System.Int32.TryParse(root.GetAttribute("seed"), System.Globalization.NumberStyles.AllowLeadingSign, Inv, out actual))
                throw Error("Registro de semente do elenco inválido; não regenerar visuais.");
            if (actual != configuredSeed) Log("SEMENTE_REUTILIZADA", "Semente registrada=" + actual.ToString(Inv) + "; configuração nova não muda o elenco salvo.");
            return actual;
        }
        System.Xml.XmlDocument doc = NewDocument("sementeElenco");
        Set(doc.DocumentElement, "versao", "1", "temporada", "1", "seed", configuredSeed.ToString(Inv),
            "dataUtc", System.DateTime.UtcNow.ToString("o", Inv), "estado", "geracao_autorizada_visual_pendente");
        SaveNewAtomic(path, doc);
        return configuredSeed;
    }

    private void SaveManifest(string path)
    {
        System.Xml.XmlDocument doc = NewDocument("elencoGerado");
        Set(doc.DocumentElement, "versao", "1", "temporada", "1", "seed", Seed.ToString(Inv),
            "dataUtc", System.DateTime.UtcNow.ToString("o", Inv), "estado", "gerado_no_jogo_teste_visual_pendente",
            "djaniraFaceSHA256", visuals["DJANIRA_POBRE"].FaceSha256);
        foreach (string code in Codigos)
        {
            VisualDiretor.Visual visual = visuals[code];
            System.Xml.XmlElement element = doc.CreateElement("visual");
            Set(element, "codigo", code, "modelo", visual.Model, "arquivo", code + ".xml", "sha256", visual.Sha256,
                "faceSHA256", visual.FaceSha256, "origem", reused.Contains(code) ? "XML_existente_reutilizado" : "capturado_natives_nesta_preparacao");
            doc.DocumentElement.AppendChild(element);
        }
        SaveNewAtomic(path, doc);
        VerifyManifest(path);
    }

    private void VerifyManifest(string path)
    {
        System.Xml.XmlElement root = ReadXml(path).DocumentElement;
        if (root.Name != "elencoGerado" || root.GetAttribute("versao") != "1" || root.GetAttribute("temporada") != "1" ||
            root.GetAttribute("seed") != Seed.ToString(Inv))
            throw Error("Manifesto do elenco difere da identidade já registrada; preservar os XML.");
        System.Collections.Generic.HashSet<string> entries = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (System.Xml.XmlNode node in root.ChildNodes)
        {
            System.Xml.XmlElement element = node as System.Xml.XmlElement;
            if (element == null) continue;
            string code = element.GetAttribute("codigo");
            VisualDiretor.Visual visual;
            if (element.Name != "visual" || !entries.Add(code) || !visuals.TryGetValue(code, out visual) ||
                element.GetAttribute("arquivo") != code + ".xml" || element.GetAttribute("modelo") != visual.Model ||
                element.GetAttribute("sha256") != visual.Sha256 || element.GetAttribute("faceSHA256") != visual.FaceSha256)
                throw Error("Visual salvo mudou ou está ausente: " + code + ". Não regerar nem substituir automaticamente.");
        }
        if (entries.Count != Codigos.Length || root.GetAttribute("djaniraFaceSHA256") != visuals["DJANIRA_POBRE"].FaceSha256)
            throw Error("Manifesto do elenco incompleto ou rosto de Djanira divergente.");
    }

    private void CheckDjanira()
    {
        VisualDiretor.Visual poor, rich;
        if (!visuals.TryGetValue("DJANIRA_POBRE", out poor) || !visuals.TryGetValue("DJANIRA_RICA", out rich)) return;
        if (!VisualDiretor.SameFaceDetails(poor, rich))
            throw Error("Os dois figurinos de Djanira precisam manter exatamente o mesmo rosto; arquivos preservados.");
        if (VisualDiretor.SameClothes(poor, rich) || VisualDiretor.SameHair(poor, rich))
            throw Error("Os dois figurinos de Djanira precisam usar roupa e cabelo diferentes; arquivos existentes preservados.");
    }

    public void Cleanup()
    {
        // Se Delete falhar, manter o handle/modelo e permitir outra tentativa em F10.
        busy = false; state = ready ? "pronto" : "inativo"; pendingStatus = null;
        RemoveTemporaryPed();
        ReleaseModel();
        // F6/navegação limpam a cena; o elenco já ativado permanece preparado.
        busy = false; state = ready ? "pronto" : "inativo"; timer = null; pendingStatus = null;
    }

    public void Dispose()
    {
        Cleanup(); disposed = true;
    }

    private void RemoveTemporaryPed()
    {
        if (ownedHandle == 0)
        {
            temporaryPed = null;
            return;
        }
        // Remover também planos cujo ped foi apagado externamente; nenhum native é chamado.
        if (!System.Object.ReferenceEquals(temporaryPed, null) && temporaryPed.Handle == ownedHandle)
        {
            ElencoAutomaticoDiretor.Forget(temporaryPed);
            VisualDiretor.Forget(temporaryPed);
        }
        if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, ownedHandle))
        {
            RequireOwned();
            ElencoAutomaticoDiretor.Forget(temporaryPed);
            VisualDiretor.Forget(temporaryPed);
            temporaryPed.Delete();
            if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, ownedHandle))
            {
                temporaryPed = GTA.Entity.FromHandle(ownedHandle) as GTA.Ped;
                throw Error("O jogo manteve o personagem temporário após Delete; tente limpar novamente.");
            }
        }
        temporaryPed = null; ownedHandle = 0; ownedAddress = System.IntPtr.Zero;
    }

    private void RequireOwned()
    {
        if (ownedHandle != 0 &&
            (System.Object.ReferenceEquals(temporaryPed, null) || temporaryPed.Handle == 0) &&
            GTA.Native.Function.Call<bool>(GTA.Native.Hash.DOES_ENTITY_EXIST, ownedHandle))
            temporaryPed = GTA.Entity.FromHandle(ownedHandle) as GTA.Ped;
        GTA.Ped player = GTA.Game.Player.Character;
        if (ownedHandle == 0 || System.Object.ReferenceEquals(temporaryPed, null) || !temporaryPed.Exists() ||
            temporaryPed.Handle != ownedHandle || temporaryPed.MemoryAddress != ownedAddress ||
            temporaryPed.Model.Hash != currentModel.Hash ||
            (!System.Object.ReferenceEquals(player, null) && temporaryPed.Handle == player.Handle))
            throw Error("Identidade do personagem temporário mudou; não alterar/remover entidade alheia.");
    }

    private void ReleaseModel()
    {
        if (!modelRequested) return;
        if (ownedHandle != 0) throw Error("Modelo permanece pedido enquanto o personagem temporário existe.");
        currentModel.MarkAsNoLongerNeeded(); modelRequested = false;
    }

    private string Inside(string path)
    {
        string full = CheckedPath(path);
        if (!System.String.Equals(full, dataRoot, System.StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(dataRoot + System.IO.Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase))
            throw Error("Caminho fora da pasta de dados do perfil.");
        return full;
    }

    private static string CheckedPath(string path)
    {
        if (System.String.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path) || path.StartsWith("\\\\", System.StringComparison.Ordinal))
            throw Error("Use caminho absoluto em disco local.");
        string[] parts = path.Split(new char[] { '\\', '/' });
        foreach (string part in parts)
            if (part == ".." || part.StartsWith("OneDrive", System.StringComparison.OrdinalIgnoreCase))
                throw Error("OneDrive e saída por '..' não são permitidos.");
        if (path.IndexOf(':', 2) >= 0) throw Error("Fluxos alternativos não são permitidos.");
        string full = System.IO.Path.GetFullPath(path);
        if (new System.IO.DriveInfo(System.IO.Path.GetPathRoot(full)).DriveType != System.IO.DriveType.Fixed)
            throw Error("A preparação exige disco local fixo.");
        string current = full;
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) &&
                (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw Error("Não usar vínculo, junction ou reparse point: " + current);
            current = System.IO.Path.GetDirectoryName(current);
        }
        return full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }

    private void RequireDirectory(string path)
    {
        path = Inside(path);
        System.IO.Directory.CreateDirectory(path);
        Inside(path);
    }

    private static System.Xml.XmlDocument ReadXml(string path)
    {
        CheckedPath(path);
        if (new System.IO.FileInfo(path).Length > 1048576) throw Error("Registro do elenco excede1MiB.");
        System.Xml.XmlReaderSettings settings = new System.Xml.XmlReaderSettings();
        settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
        settings.XmlResolver = null;
        settings.MaxCharactersInDocument = 100000;
        System.Xml.XmlDocument doc = new System.Xml.XmlDocument(); doc.XmlResolver = null;
        using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(path, settings)) doc.Load(reader);
        if (doc.DocumentElement == null) throw Error("Registro XML do elenco vazio.");
        return doc;
    }

    private static System.Xml.XmlDocument NewDocument(string root)
    {
        System.Xml.XmlDocument doc = new System.Xml.XmlDocument(); doc.XmlResolver = null;
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
        doc.AppendChild(doc.CreateElement(root));
        return doc;
    }

    private void SaveNewAtomic(string path, System.Xml.XmlDocument doc)
    {
        path = Inside(path);
        string temporary = Inside(path + ".tmp-" + System.Guid.NewGuid().ToString("N"));
        using (System.IO.FileStream stream = new System.IO.FileStream(temporary, System.IO.FileMode.CreateNew,
            System.IO.FileAccess.Write, System.IO.FileShare.None))
        {
            System.Xml.XmlWriterSettings settings = new System.Xml.XmlWriterSettings();
            settings.Encoding = new System.Text.UTF8Encoding(false); settings.Indent = true;
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(stream, settings)) doc.Save(writer);
            stream.Flush(true);
        }
        Inside(path); Inside(temporary);
        if (System.IO.File.Exists(path)) throw Error("Registro apareceu durante o preparo; arquivo preservado.");
        System.IO.File.Move(temporary, path);
    }

    private void SetStatus(string message) { pendingStatus = message; Log("PREPARO_ELENCO", message); }
    private static void Set(System.Xml.XmlElement element, params string[] attributes)
    {
        for (int i = 0; i < attributes.Length; i += 2) element.SetAttribute(attributes[i], attributes[i + 1]);
    }
    private string TakeStatus(string fallback) { string message = pendingStatus ?? fallback; pendingStatus = null; return message; }
    private void Log(string code, string message)
    {
        if (log == null) return;
        try { log(code, message); } catch (System.Exception) { }
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static System.InvalidOperationException Error(string message)
    {
        return new System.InvalidOperationException("Elenco: " + message);
    }
}

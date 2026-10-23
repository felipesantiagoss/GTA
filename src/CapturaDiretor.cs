// Uma sessão por tomada. O worker Python organiza somente uma captura nova e finalizada.
public sealed class CapturaDiretor
{
    private readonly string dataRoot, captureRoot, pythonPath, workerPath;
    private readonly System.Action<string, string> log;
    private System.Xml.XmlDocument document;
    private string sessionDirectory, sessionId;
    private System.Diagnostics.Process worker;
    private System.Threading.Tasks.Task<string> verification;
    private bool armed, bound, retry;
    private volatile bool hasVerifiedSave;

    public CapturaDiretor(string dataRoot, string captureRoot, string pythonPath, string workerPath,
        System.Action<string, string> log)
    {
        this.dataRoot = System.IO.Path.GetFullPath(dataRoot);
        this.captureRoot = System.IO.Path.GetFullPath(captureRoot);
        this.pythonPath = System.IO.Path.GetFullPath(pythonPath);
        this.workerPath = System.IO.Path.GetFullPath(workerPath);
        this.log = log;
    }

    public bool IsArmed { get { return armed; } }
    public bool IsPending { get { return !System.Object.ReferenceEquals(worker, null) || !System.Object.ReferenceEquals(verification, null); } }
    // Somente status: identificarARM preparado, ainda semBegin/count/sessao concluida.
    // Nenhuma entrada/tarefa/arquivo/captura e alterada. GUID e cena vemda mesma propria XmlDocument.
    public string GetManualCountdownPreparedSession(string sceneId)
    {
        if (!armed || bound || retry || IsPending || System.Object.ReferenceEquals(document, null) ||
            System.String.IsNullOrEmpty(sceneId) || System.String.IsNullOrEmpty(sessionId)) return "";
        System.Xml.XmlElement root = document.DocumentElement;
        if (System.Object.ReferenceEquals(root, null) || root.Name != "captura" ||
            root.GetAttribute("versao") != "1" || root.GetAttribute("estado") != "preparada" ||
            root.GetAttribute("id") != sessionId || root.GetAttribute("cenaId") != sceneId ||
            root.HasAttribute("contagemUtc") || root.HasAttribute("fimUtc") ||
            !System.Text.RegularExpressions.Regex.IsMatch(sessionId, "\\A[0-9a-f]{32}\\z")) return "";
        return sessionId;
    }

    public bool CanRetry { get { return retry && !IsPending; } }
    public bool HasVerifiedSave { get { return hasVerifiedSave; } }

    // Evidência de arquivo inicial escrito e ainda em uso, sem inferir duração da mídia.
    // Não envia atalhos, não altera a sessão e não escreve bytes no MP4.
    public string GetStartedCapture()
    {
        if (!armed || System.Object.ReferenceEquals(document, null)) return null;
        System.Xml.XmlElement root = document.DocumentElement;
        System.DateTime armedUtc;
        if (System.Object.ReferenceEquals(root, null) ||
            !System.DateTime.TryParse(root.GetAttribute("armadaUtc"), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out armedUtc))
            throw new System.InvalidOperationException("A sessão de captura não tem data de preparação válida.");
        System.Xml.XmlElement before = root["antes"];
        if (System.Object.ReferenceEquals(before, null))
            throw new System.InvalidOperationException("A sessão de captura não tem o inventário anterior.");
        System.Collections.Generic.HashSet<string> baseline = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (System.Xml.XmlNode entry in before.ChildNodes)
        {
            System.Xml.XmlElement file = entry as System.Xml.XmlElement;
            if (!System.Object.ReferenceEquals(file, null) && file.Name == "arquivo") baseline.Add(file.GetAttribute("nome"));
        }
        RequireNoLinks(captureRoot);
        string[] files = System.IO.Directory.GetFiles(captureRoot, "*.mp4", System.IO.SearchOption.TopDirectoryOnly);
        if (files.Length > 5000)
            throw new System.InvalidOperationException("Há capturas demais para conferir o início desta tomada.");
        string started = null;
        long length = 0;
        foreach (string path in files)
        {
            string name = System.IO.Path.GetFileName(path);
            if (baseline.Contains(name) || !System.Text.RegularExpressions.Regex.IsMatch(name,
                "\\A(?:GTA5|Grand Theft Auto V)(?:[ _.-].*)?\\.mp4\\z",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)) continue;
            System.IO.FileInfo info = new System.IO.FileInfo(path);
            long candidateLength;
            try
            {
                RequireNoLinks(path);
                info.Refresh();
                if (!info.Exists || info.CreationTimeUtc < armedUtc) continue;
                candidateLength = info.Length;
            }
            catch (System.IO.FileNotFoundException) { continue; }
            catch (System.IO.DirectoryNotFoundException) { continue; }
            // Um segundo arquivo novo, mesmo ainda vazio, torna o início ambíguo.
            if (!System.String.IsNullOrEmpty(started))
                throw new System.InvalidOperationException("Captura ambígua: apareceram dois MP4 novos do GTA; arquivos preservados.");
            started = info.FullName;
            length = candidateLength;
        }
        if (length < 80) return null;
        try
        {
            RequireNoLinks(started);
            // Permite leitores comuns; negar Share.Write detecta handle de escrita.
            // Nenhum Write, truncamento ou criação; falha de acesso não é evidência.
            using (System.IO.FileStream check = new System.IO.FileStream(started, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.Read | System.IO.FileShare.Delete)) { }
        }
        catch (System.IO.FileNotFoundException) { return null; }
        catch (System.IO.DirectoryNotFoundException) { return null; }
        catch (System.IO.IOException ex)
        {
            int code = ex.HResult & 0xFFFF;
            if (code == 32 || code == 33) return started;
            throw;
        }
        // Sem conflito com escritor, pode já ter sido parado; não alternar gravação.
        return null;
    }

    // Somente leitura: nenhum toggle/STOP, nenhum arquivo alterado. Ocupacao ambigua bloqueia.
    public void RequireNoWriterForFinalCleanup()
    {
        if (IsArmed || IsPending || bound) throw new System.InvalidOperationException("Limpeza final exige captura desarmada e organizacao concluida.");
        RequireNoWriterInCaptureDirectory(captureRoot, log);
    }

    // Prova global identica tambem quando Gravacao/Habilitada=false: nenhum objeto de sessao simulado.
    public static void RequireNoWriterInCaptureDirectory(string captureRoot, System.Action<string, string> log)
    {
        RequireNoLinks(captureRoot);
        string[] files = System.IO.Directory.GetFiles(captureRoot, "*.mp4", System.IO.SearchOption.TopDirectoryOnly);
        if (files.Length > 5000) throw new System.InvalidOperationException("Inventario excessivo para confirmar ausencia de writer; nenhum ator removido.");
        foreach (string path in files)
        {
            try
            {
                RequireNoLinks(path);
                using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open,
                    System.IO.FileAccess.Read, System.IO.FileShare.Read | System.IO.FileShare.Delete)) { }
            }
            catch (System.IO.FileNotFoundException) { }
            catch (System.IO.DirectoryNotFoundException) { }
        }
        if (!System.Object.ReferenceEquals(log, null)) log("CAPTURA_SEM_WRITER_LIMPEZA", "Arquivos=" + files.Length + "; SemSetter=true; SomenteLeitura=true; TodosMP4SemCompartilhamentoWrite=true");
    }

    public static int EditorialPartDuration(string sceneId, string part)
    {
        if (sceneId == "EP07_C03" && (part == "embarque_dinjer_chefe" || part == "embarque_djanira_decolagem")) return 45000;
        if (sceneId == "EP13_C02" && (part == "A_PRIMEIRA" || part == "B_SEGUNDA" || part == "C_TERCEIRA")) return 14000;
        return 0; // Nao aceita nome arbitrario, principal ou parte de outro ID.
    }
    public void Arm(string sceneId, int episode, int number, string part = null,
        string sourcePath = null, string sourceSha256 = null, string codeVersion = null)
    {
        bool production = episode >= 1 && episode <= 15 && number >= 1 && number <= 99 &&
            sceneId == "EP" + episode.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "_C" + number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
        bool technical = episode == 0 && number == 0 &&
            (System.Text.RegularExpressions.Regex.IsMatch(sceneId ?? "", "\\A(?:teste_json|teste_fila_(?:0[1-9]|[1-9][0-9])|teste_elenco_pobre|teste_elenco_rica|teste_dupla|teste_falas|teste_camera|teste_cortes|teste_ambiente|teste_coreografia|VINHETA_01)\\z"));
        bool supplemental = episode == 1 && number == 0 &&
            (sceneId == "EP01_EXTRA_IRACI_BAR" || sceneId == "EP01_EXTRA_DINJER_DEDO");
        if (!production && !technical && !supplemental)
            throw new System.InvalidOperationException("Gravação: use um ID técnico conhecido, VINHETA_01 ou EPnn_Cnn correspondente à cena.");
        if (IsPending) throw new System.InvalidOperationException("Espere a organização da gravação terminar.");
        if (!System.IO.Directory.Exists(captureRoot))
            throw new System.InvalidOperationException("A pasta de capturas da Barra de Jogos não existe: " + captureRoot);
        RequireNoLinks(captureRoot);
        RequireNoLinks(dataRoot);
        if (!System.IO.File.Exists(pythonPath) || !System.IO.File.Exists(workerPath))
            throw new System.InvalidOperationException("A ferramenta de organização da gravação não foi encontrada.");
        bool editorialPart = !System.String.IsNullOrEmpty(part);
        if (editorialPart)
        {
            if (EditorialPartDuration(sceneId, part) == 0 ||
                System.String.IsNullOrEmpty(sourcePath) || !System.Text.RegularExpressions.Regex.IsMatch(sourceSha256 ?? "", "\\A[0-9A-F]{64}\\z") ||
                System.String.IsNullOrEmpty(codeVersion)) throw new System.InvalidOperationException("Proveniencia de subtomada invalida.");
            string expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(dataRoot, "cenas", "subtomadas", sceneId, part, sceneId + ".json"));
            if (!System.String.Equals(System.IO.Path.GetFullPath(sourcePath), expected, System.StringComparison.OrdinalIgnoreCase))
                throw new System.InvalidOperationException("Fonte de subtomada fora do caminho literal.");
            RequireNoLinks(expected);
            if (HashFile(expected) != sourceSha256) throw new System.InvalidOperationException("Fonte da subtomada mudou desde montagem.");
        }
        else if (sourcePath != null || sourceSha256 != null || codeVersion != null)
            throw new System.InvalidOperationException("Proveniencia sem parte explicita.");
        Cancel("nova preparação");
        string[] files = System.IO.Directory.GetFiles(captureRoot, "*.mp4", System.IO.SearchOption.TopDirectoryOnly);
        if (files.Length > 5000) throw new System.InvalidOperationException("Há capturas demais para preparar esta tomada; organize a pasta primeiro.");
        sessionId = System.Guid.NewGuid().ToString("N");
        sessionDirectory = System.IO.Path.Combine(dataRoot, "logs", "capturas", sessionId);
        RequireNoLinks(sessionDirectory);
        System.IO.Directory.CreateDirectory(sessionDirectory);
        document = new System.Xml.XmlDocument();
        document.XmlResolver = null;
        document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));
        System.Xml.XmlElement root = document.CreateElement("captura");
        document.AppendChild(root);
        Set(root, "versao", "1", "id", sessionId, "estado", "preparada", "cenaId", sceneId,
            "episodio", Number(episode), "numero", Number(number), "pastaDados", dataRoot,
            "pastaCapturas", captureRoot, "armadaUtc", UtcNow());
        System.Xml.XmlElement before = document.CreateElement("antes");
        root.AppendChild(before);
        foreach (string file in files)
        {
            RequireNoLinks(file);
            System.IO.FileInfo info = new System.IO.FileInfo(file);
            System.Xml.XmlElement entry = document.CreateElement("arquivo");
            Set(entry, "nome", info.Name, "tamanho", info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "criadoUtc", info.CreationTimeUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                "alteradoUtc", info.LastWriteTimeUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            before.AppendChild(entry);
        }
        if (editorialPart)
        {
            string snapshot = System.IO.Path.Combine(sessionDirectory, "source-snapshot.json");
            // FileShare.Read impede mudanca da fonte durante copia; nunca bloqueia input ou processo.
            using (System.IO.FileStream input = new System.IO.FileStream(sourcePath, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.Read))
            using (System.IO.FileStream output = new System.IO.FileStream(snapshot, System.IO.FileMode.CreateNew,
                System.IO.FileAccess.Write, System.IO.FileShare.Read))
            { input.CopyTo(output); output.Flush(true); }
            if (HashFile(snapshot) != sourceSha256 || HashFile(sourcePath) != sourceSha256)
                throw new System.InvalidOperationException("Snapshot native antes ARM divergiu da fonte montada.");
            var proof = new System.Xml.XmlDocument(); proof.XmlResolver = null;
            var metadata = proof.CreateElement("subtomada"); proof.AppendChild(metadata);
            Set(metadata, "versao", "1", "id", sessionId, "cenaId", sceneId, "parte", part,
                "estado", "armada", "arquivoFonte", System.IO.Path.GetFullPath(sourcePath),
                "sha256Fonte", sourceSha256, "snapshot", snapshot, "versaoCodigo", codeVersion,
                "duracaoConteudoMs", Number(EditorialPartDuration(sceneId, part)), "contagemMs", "3000", "dataUtc", UtcNow(),
                "integral", "false", "exigeMontagemDuasPartes", sceneId == "EP07_C03" ? "true" : "false");
            if (sceneId == "EP13_C02") Set(metadata, "exigeMontagemTresPartes", "true", "partesNecessarias", "3");
            SaveNew(System.IO.Path.Combine(sessionDirectory, "proveniencia-subtomada-armada.xml"), proof);
            Log("CAPTURA_SUBTOMADA_ARMADA", "Sessao=" + sessionId + "; Cena=" + sceneId + "; Parte=" + part +
                "; Arquivo=" + sourcePath + "; SHA256=" + sourceSha256 + "; Versao=" + codeVersion + "; Integral=false");
        }
        // Somente partes: o instante ARM vem depois do snapshot/sidecar realmente persistidos.
        // Preserva mtime natural do snapshot; nunca retrodata um arquivo para simular prearm.
        if (editorialPart) root.SetAttribute("armadaUtc", UtcNow());
        SaveNew(System.IO.Path.Combine(sessionDirectory, "armada.xml"), document);
        armed = true;
        bound = false;
        retry = false;
        Log("CAPTURA_PREPARADA", "Sessão=" + sessionId + "; cena=" + sceneId + "; pasta=" + captureRoot + "; anteriores=" + files.Length);
    }

    public void Begin(string sceneId, string srtPath, int totalDurationMs)
    {
        if (!armed) return;
        if (IsPending || bound) throw new System.InvalidOperationException("Uma gravação anterior ainda está sendo organizada.");
        if (document.DocumentElement.GetAttribute("cenaId") != sceneId)
            throw new System.InvalidOperationException("A gravação foi preparada para outra cena. Prepare novamente com Home.");
        RequireNoLinks(srtPath);
        System.Xml.XmlElement root = document.DocumentElement;
        root.SetAttribute("contagemUtc", UtcNow());
        root.SetAttribute("duracaoMs", Number(totalDurationMs));
        System.Xml.XmlElement srt = document.CreateElement("srt");
        Set(srt, "caminho", System.IO.Path.GetFullPath(srtPath), "sha256", HashFile(srtPath));
        root.InsertBefore(srt, root.FirstChild);
        SaveNew(System.IO.Path.Combine(sessionDirectory, "iniciada.xml"), document);
        bound = true;
        Log("CAPTURA_VINCULADA", "Sessão=" + sessionId + "; cena=" + sceneId + "; SRT=" + srtPath);
    }

    public void Finish()
    {
        if (!armed || !bound) return;
        System.Xml.XmlElement root = document.DocumentElement;
        root.SetAttribute("estado", "concluida");
        root.SetAttribute("fimUtc", UtcNow());
        root.SetAttribute("prazoUtc", System.DateTime.UtcNow.AddMinutes(3).ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        SaveNew(System.IO.Path.Combine(sessionDirectory, "sessao.xml"), document);
        armed = false;
        bound = false;
        retry = true;
        StartWorker();
    }

    public void Retry()
    {
        if (IsPending) throw new System.InvalidOperationException("Espere a organização da gravação terminar.");
        if (!CanRetry) RestorePendingSession();
        if (!CanRetry) throw new System.InvalidOperationException("Não há gravação finalizada aguardando organização.");
        // O worker valida o XML inteiro antes de ampliar o prazo com backup.
        // Assim End depois de Reload conserva baseline, legenda e GUID originais.
        Log("CAPTURA_REPETIR_ORGANIZACAO", "Sessão=" + sessionId + "; contexto original preservado.");
        StartWorker(true);
    }

    private void RestorePendingSession()
    {
        string sessions = System.IO.Path.Combine(dataRoot, "logs", "capturas");
        RequireNoLinks(sessions);
        if (!System.IO.Directory.Exists(sessions)) return;
        string[] folders = System.IO.Directory.GetDirectories(sessions);
        if (folders.Length > 5000) throw new System.InvalidOperationException("Há sessões demais para retomar automaticamente; conferir o log de capturas.");
        System.Xml.XmlDocument found = null;
        string foundFolder = null;
        foreach (string folder in folders)
        {
            RequireNoLinks(folder);
            string id = System.IO.Path.GetFileName(folder);
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "\\A[0-9a-f]{32}\\z")) continue;
            string path = System.IO.Path.Combine(folder, "sessao.xml");
            if (!System.IO.File.Exists(path) || System.IO.File.Exists(System.IO.Path.Combine(folder, "cancelada.xml"))) continue;
            RequireNoLinks(path);
            System.Xml.XmlDocument candidate;
            try { candidate = ReadXml(path, 1024 * 1024); }
            catch (System.Exception ex) { Log("CAPTURA_SESSAO_IGNORADA", "Arquivo=" + path + "; " + ex.Message); continue; }
            System.Xml.XmlElement root = candidate.DocumentElement;
            if (System.Object.ReferenceEquals(root, null) || root.Name != "captura" ||
                root.GetAttribute("versao") != "1" || root.GetAttribute("estado") != "concluida" ||
                root.GetAttribute("id") != id ||
                !System.String.Equals(root.GetAttribute("pastaDados"), dataRoot, System.StringComparison.OrdinalIgnoreCase) ||
                !System.String.Equals(root.GetAttribute("pastaCapturas"), captureRoot, System.StringComparison.OrdinalIgnoreCase)) continue;
            System.DateTime finished;
            if (!System.DateTime.TryParse(root.GetAttribute("fimUtc"), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out finished) ||
                finished > System.DateTime.UtcNow || System.DateTime.UtcNow >= finished.AddMinutes(10)) continue;
            string resultPath = System.IO.Path.Combine(folder, "resultado.xml");
            if (System.IO.File.Exists(resultPath))
            {
                RequireNoLinks(resultPath);
                System.Xml.XmlElement result = ReadXml(resultPath, 12000).DocumentElement;
                if (System.Object.ReferenceEquals(result, null) || result.Name != "resultado" || result.GetAttribute("id") != id)
                    throw new System.InvalidOperationException("Há uma resposta de captura divergente; conferir logs/capturas.");
                if (result.GetAttribute("status") == "salvo" || result.GetAttribute("status") == "cancelado") continue;
            }
            if (!System.Object.ReferenceEquals(found, null))
                throw new System.InvalidOperationException("Há duas tomadas concluídas pendentes; End não escolherá um vídeo arbitrariamente. Confira logs/capturas.");
            found = candidate;
            foundFolder = folder;
        }
        if (System.Object.ReferenceEquals(found, null)) return;
        document = found;
        sessionDirectory = foundFolder;
        sessionId = found.DocumentElement.GetAttribute("id");
        armed = false;
        bound = false;
        retry = true;
        Log("CAPTURA_RETOMADA", "Sessão=" + sessionId + "; cena=" + found.DocumentElement.GetAttribute("cenaId") + "; retomada por End após limpeza/Reload.");
    }

    private static System.Xml.XmlDocument ReadXml(string path, int maxCharacters)
    {
        System.Xml.XmlReaderSettings settings = new System.Xml.XmlReaderSettings();
        settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
        settings.XmlResolver = null;
        settings.MaxCharactersInDocument = maxCharacters;
        System.Xml.XmlDocument value = new System.Xml.XmlDocument();
        value.XmlResolver = null;
        using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(path, settings)) value.Load(reader);
        return value;
    }

    private void StartWorker(bool repeating = false)
    {
        System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
        start.FileName = pythonPath;
        start.Arguments = "-X utf8 \"" + workerPath + "\" " + (repeating ? "--repetir" : "--sessao") + " \"" + System.IO.Path.Combine(sessionDirectory, "sessao.xml") + "\"";
        start.WorkingDirectory = System.IO.Path.GetDirectoryName(workerPath);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        worker = System.Diagnostics.Process.Start(start);
        if (System.Object.ReferenceEquals(worker, null))
            throw new System.InvalidOperationException("Não foi possível iniciar a organização da gravação.");
        Log("CAPTURA_AGUARDANDO", "Sessão=" + sessionId + "; pare a captura com Win+Alt+R; worker=" + worker.Id);
    }

    public string Poll()
    {
        try
        {
            if (!System.Object.ReferenceEquals(verification, null))
            {
                if (!verification.IsCompleted) return null;
                System.Threading.Tasks.Task<string> completed = verification;
                verification = null;
                return completed.GetAwaiter().GetResult();
            }
            if (System.Object.ReferenceEquals(worker, null) || !worker.HasExited) return null;
            int exitCode = worker.ExitCode;
            worker.Dispose();
            worker = null;
            // Conferir um MP4 inteiro no Tick bloqueia o GTA. Este trabalho usa
            // somente arquivos/.NET em uma thread separada, sem natives do jogo.
            verification = System.Threading.Tasks.Task.Factory.StartNew(delegate { return ReadResult(exitCode); });
            return null;
        }
        catch (System.Exception ex)
        {
            if (!System.Object.ReferenceEquals(worker, null)) { worker.Dispose(); worker = null; }
            verification = null;
            Log("ERRO_CAPTURA", ex.ToString());
            return "Gravação: " + ex.Message;
        }
    }

    private string ReadResult(int exitCode)
    {
            string path = System.IO.Path.Combine(sessionDirectory, "resultado.xml");
            RequireNoLinks(path);
            System.Xml.XmlReaderSettings settings = new System.Xml.XmlReaderSettings();
            settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.MaxCharactersInDocument = 12000;
            System.Xml.XmlDocument result = new System.Xml.XmlDocument();
            result.XmlResolver = null;
            using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(path, settings)) result.Load(reader);
            System.Xml.XmlElement root = result.DocumentElement;
            if (System.Object.ReferenceEquals(root, null) || root.Name != "resultado" || root.GetAttribute("versao") != "1" || root.GetAttribute("id") != sessionId)
                throw new System.InvalidOperationException("A resposta da gravação não corresponde à tomada atual.");
            string status = root.GetAttribute("status");
            if (status == "salvo" && exitCode == 0)
            {
                string video = root.GetAttribute("video");
                string srt = root.GetAttribute("legenda");
                RequireOutput(video); RequireOutput(srt);
                if (HashFile(video) != root.GetAttribute("sha256Video") || HashFile(srt) != root.GetAttribute("sha256Legenda"))
                    throw new System.InvalidOperationException("A conferência final do vídeo ou legenda falhou.");
                PersistPartSave(root);
                retry = false;
                hasVerifiedSave = true;
                Log("CAPTURA_SALVA", "Sessão=" + sessionId + "; vídeo=" + video + "; legenda=" + srt);
                return "Salvo: " + System.IO.Path.GetFileName(video);
            }
            if (status == "cancelado") retry = false;
            Log("CAPTURA_RESULTADO", "Sessão=" + sessionId + "; estado=" + status + "; exit=" + exitCode + "; " + root.GetAttribute("mensagem"));
            return "Gravação: " + root.GetAttribute("mensagem");
    }

    private void PersistPartSave(System.Xml.XmlElement result)
    {
        string path = System.IO.Path.Combine(sessionDirectory, "proveniencia-subtomada-armada.xml");
        if (!System.IO.File.Exists(path)) return; // Fluxos antigos/integral preservados.
        RequireNoLinks(path);
        System.Xml.XmlDocument proof = ReadXml(path, 12000);
        System.Xml.XmlElement metadata = proof.DocumentElement;
        string part = metadata.GetAttribute("parte"), source = metadata.GetAttribute("arquivoFonte");
        string sceneId = metadata.GetAttribute("cenaId");
        int duration = EditorialPartDuration(sceneId, part);
        string expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(dataRoot, "cenas", "subtomadas", sceneId, part, sceneId + ".json"));
        string snapshot = System.IO.Path.Combine(sessionDirectory, "source-snapshot.json");
        if (metadata.Name != "subtomada" || metadata.GetAttribute("versao") != "1" ||
            metadata.GetAttribute("id") != sessionId || duration == 0 ||
            document.DocumentElement.GetAttribute("cenaId") != sceneId ||
            metadata.GetAttribute("integral") != "false" ||
            metadata.GetAttribute("exigeMontagemDuasPartes") != (sceneId == "EP07_C03" ? "true" : "false") ||
            (sceneId == "EP13_C02" && (metadata.GetAttribute("exigeMontagemTresPartes") != "true" || metadata.GetAttribute("partesNecessarias") != "3")) ||
            !System.String.Equals(source, expected, System.StringComparison.OrdinalIgnoreCase) ||
            metadata.GetAttribute("snapshot") != snapshot || metadata.GetAttribute("duracaoConteudoMs") != Number(duration) ||
            document.DocumentElement.GetAttribute("duracaoMs") != Number(duration + 3000))
            throw new System.InvalidOperationException("Proveniencia de subtomada nao corresponde ao GUID/duracao literal da parte.");
        RequireNoLinks(source); RequireNoLinks(snapshot);
        string sha = metadata.GetAttribute("sha256Fonte");
        if (HashFile(snapshot) != sha || HashFile(source) != sha)
            throw new System.InvalidOperationException("Fonte/snapshot da subtomada mudou antes SAVE; bruto preservado.");
        metadata.SetAttribute("estado", "salva"); metadata.SetAttribute("salvaUtc", UtcNow());
        metadata.SetAttribute("video", result.GetAttribute("video")); metadata.SetAttribute("sha256Video", result.GetAttribute("sha256Video"));
        metadata.SetAttribute("legenda", result.GetAttribute("legenda")); metadata.SetAttribute("sha256Legenda", result.GetAttribute("sha256Legenda"));
        string saved = System.IO.Path.Combine(sessionDirectory, "proveniencia-subtomada-salva.xml");
        if (!System.IO.File.Exists(saved)) SaveNew(saved, proof);
        Log("CAPTURA_SUBTOMADA_SALVA", "Sessao=" + sessionId + "; Cena=" + sceneId + "; Parte=" + part +
            "; Arquivo=" + source + "; SHA256=" + sha + "; Versao=" + metadata.GetAttribute("versaoCodigo") +
            "; Integral=false; Prova=" + saved);
    }
    public void Cancel(string reason)
    {
        if (!System.String.IsNullOrEmpty(sessionDirectory) && !armed && !bound && (retry || IsPending) &&
            System.IO.File.Exists(System.IO.Path.Combine(sessionDirectory, "sessao.xml")))
        {
            // A ação já terminou. Limpar/recarregar o GTA não cancela sua
            // organização; End pode retomar esse contexto exato por dez minutos.
            retry = true;
            Log("CAPTURA_CONTEXTO_PRESERVADO", "Sessão=" + sessionId + "; motivo=" + reason + "; tomada concluída disponível para End.");
            return;
        }
        if (!System.String.IsNullOrEmpty(sessionDirectory) && (armed || bound || IsPending || retry))
        {
            string path = System.IO.Path.Combine(sessionDirectory, "cancelada.xml");
            if (!System.IO.File.Exists(path))
            {
                System.Xml.XmlDocument cancel = new System.Xml.XmlDocument();
                System.Xml.XmlElement root = cancel.CreateElement("cancelada");
                cancel.AppendChild(root);
                Set(root, "versao", "1", "id", sessionId, "motivo", reason, "dataUtc", UtcNow());
                SaveNew(path, cancel);
            }
            Log("CAPTURA_CANCELADA", "Sessão=" + sessionId + "; motivo=" + reason + "; vídeos originais preservados.");
        }
        armed = false;
        bound = false;
        retry = false;
    }

    private void RequireOutput(string path)
    {
        string full = System.IO.Path.GetFullPath(path);
        string prefix = System.IO.Path.Combine(dataRoot, "Serie", "A_CALOTEIRA_1_TEMP") + System.IO.Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            throw new System.InvalidOperationException("A saída da gravação está fora da pasta da série.");
        RequireNoLinks(full);
    }

    private static void RequireNoLinks(string path)
    {
        string current = System.IO.Path.GetFullPath(path);
        while (!System.String.IsNullOrEmpty(current))
        {
            if ((System.IO.File.Exists(current) || System.IO.Directory.Exists(current)) &&
                (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw new System.InvalidOperationException("Gravação: não usar caminho redirecionado por vínculo: " + current);
            current = System.IO.Path.GetDirectoryName(current);
        }
    }

    private static void SaveNew(string path, System.Xml.XmlDocument document)
    {
        RequireNoLinks(path);
        using (System.IO.FileStream stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew,
            System.IO.FileAccess.Write, System.IO.FileShare.Read))
        {
            System.Xml.XmlWriterSettings settings = new System.Xml.XmlWriterSettings();
            settings.Encoding = new System.Text.UTF8Encoding(false);
            settings.Indent = true;
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(stream, settings)) document.Save(writer);
            stream.Flush(true);
        }
    }

    private static string HashFile(string path)
    {
        using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
        using (System.IO.FileStream stream = System.IO.File.OpenRead(path))
            return System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }

    private static void Set(System.Xml.XmlElement element, params string[] attributes)
    {
        for (int i = 0; i < attributes.Length; i += 2) element.SetAttribute(attributes[i], attributes[i + 1]);
    }
    private static string UtcNow() { return System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture); }
    private static string Number(int value) { return value.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    private void Log(string kind, string value) { if (!System.Object.ReferenceEquals(log, null)) log(kind, value); }
}

// Controle local de producao. Usa os mesmos metodos/guardas da interface do Diretor.
// Nenhuma tecla global, comando de shell ou caminho recebido e executado pelo jogo.
public sealed class OperacaoProducaoDiretor
{
    public sealed class Command
    {
        public string Id, Action, Scene, Local, Reference;
    }
    private readonly string inbox, results;
    private readonly System.Action<string, string> log;
    private readonly System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();
    private Command pendingMesaTrial;
    private Command pendingShellTrial;
    private string lastHash;
    private uint polledAt;
    private bool polled, waitingInboxShare;
    public OperacaoProducaoDiretor(string root, System.Action<string, string> logger)
    {
        inbox = System.IO.Path.Combine(root, "operacao_producao.xml");
        results = System.IO.Path.Combine(root, "logs"); log = logger;
    }
    public void Update(bool allow, System.Func<Command, string> execute, bool allowStatus = false)
    {
        if (pendingMesaTrial != null || pendingShellTrial != null) return; // Explicit technical trials only.
        uint now = unchecked((uint)GTA.Game.GameTime);
        if (polled && unchecked(now - polledAt) < 250) return;
        polled = true; polledAt = now;
        if (!System.IO.File.Exists(inbox)) return;
        Command command = null;
        try
        {
            byte[] bytes;
            try { bytes = ReadInboxSnapshot(); }
            catch (System.IO.IOException ex)
            {
                int nativeError = ex.HResult & 0xFFFF;
                if (nativeError != 32 && nativeError != 33) throw;
                // Somente conflito de leitura, antes de hash/GUID/seen/execucao: tentar no proximo polling normal.
                if (!waitingInboxShare) log("OPERACAO_PRODUCAO_ENTRADA_AGUARDANDO", "Win32=" + nativeError +
                    "; ComandoLido=false; HashAlterado=false; IdConsumido=false; ProximoPolling=true");
                waitingInboxShare = true;
                return;
            }
            if (waitingInboxShare)
            {
                waitingInboxShare = false;
                log("OPERACAO_PRODUCAO_ENTRADA_RETOMADA", "SnapshotCompleto=true; ComandoAindaNaoConsumido=true");
            }
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                hash = System.BitConverter.ToString(sha.ComputeHash(bytes));
            if (lastHash == hash) return;
            lastHash = hash;
            var settings = new System.Xml.XmlReaderSettings();
            settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit; settings.XmlResolver = null;
            settings.MaxCharactersInDocument = 4096;
            var doc = new System.Xml.XmlDocument(); doc.XmlResolver = null;
            using (var input = new System.IO.MemoryStream(bytes))
            using (var reader = System.Xml.XmlReader.Create(input, settings)) doc.Load(reader);
            var element = doc.DocumentElement;
            if (element == null || element.Name != "Operacao" || element.GetAttribute("versao") != "1" || element.ChildNodes.Count != 0)
                throw new System.InvalidOperationException("Contrato de operacao XML invalido.");
            foreach (System.Xml.XmlAttribute attr in element.Attributes)
                if (attr.Name != "versao" && attr.Name != "id" && attr.Name != "acao" && attr.Name != "cena" && attr.Name != "local" && attr.Name != "referencia")
                    throw new System.InvalidOperationException("Campo desconhecido na operacao: " + attr.Name);
            command = new Command { Id = element.GetAttribute("id"), Action = element.GetAttribute("acao"),
                Scene = element.GetAttribute("cena"), Local = element.GetAttribute("local"), Reference = element.GetAttribute("referencia") };
            System.Guid guid;
            if (!System.Guid.TryParseExact(command.Id, "N", out guid)) throw new System.InvalidOperationException("Operacao exige ID unico GUID.");
            string result = System.IO.Path.Combine(results, "operacao_producao_" + command.Id + ".xml");
            if (seen.Contains(command.Id) || System.IO.File.Exists(result)) return;
            seen.Add(command.Id);
            if (command.Action != "status" && command.Action != "montar" && command.Action != "limpar" && command.Action != "marcar" && command.Action != "visitar" && command.Action != "ensaiomesa" && command.Action != "ensaioshell")
                throw new System.InvalidOperationException("Acao de producao desconhecida.");
            if (command.Reference == "viaProxima" && command.Action != "visitar")
                throw new System.InvalidOperationException("Referencia viaProxima pertence somente a visitar.");
            // Apenas status pode consultar durante preparo/reset; guardas de sessao do Main
            // e modo historia continuam obrigatorios. Acoes de escrita conservam allow original.
            bool allowed = command.Action == "status" ? allowStatus : allow;
            if (!allowed || GTA.Native.Function.Call<bool>(GTA.Native.Hash.NETWORK_IS_GAME_IN_PROGRESS))
                throw new System.InvalidOperationException("Operacao requer GTA modo historia ativo e tomada fora de gravacao/acao/preparo.");
            if (command.Action == "ensaiomesa" &&
                ((command.Scene != "prop_table_01" && command.Scene != "prop_table_03") ||
                 command.Local != "BANCO_MAZE" || !System.String.IsNullOrEmpty(command.Reference)))
                throw new System.InvalidOperationException("Ensaio mesa exige modelo 01/03, BANCO_MAZE e referencia vazia.");
            if (command.Action == "ensaioshell" &&
                !(command.Scene == "c4_room_models" && command.Local == "MANSAO_DJANIRA" && command.Reference == "stream_only") &&
                !(command.Scene == "oneil_farm" && command.Local == "CASA_VO" && command.Reference == "farm_original") &&
                !(command.Scene == "modern1_room" && command.Local == "MANSAO_DJANIRA" && command.Reference == "apartment_modern1") &&
                (command.Scene != "vinewood_none" || command.Local != "MANSAO_DJANIRA" ||
                 (!System.String.IsNullOrEmpty(command.Reference) && command.Reference != "restaurar" && command.Reference != "cali_sem_mobiliario" && command.Reference != "madrazo")))
                throw new System.InvalidOperationException("Shell trial contract invalid; Farm requires exact oneil_farm/CASA_VO/farm_original.");
            string message = execute(command);
            if (command.Action == "ensaioshell" && message == null)
            {
                pendingShellTrial = command;
                log("OPERACAO_PRODUCAO_AGUARDANDO", "Id=" + command.Id + "; acao=ensaioshell; ResultadoDiferido=True; ProductionReady=False");
                return;
            }
            if (command.Action == "ensaiomesa" && message == null)
            {
                pendingMesaTrial = command;
                log("OPERACAO_PRODUCAO_AGUARDANDO", "Id=" + command.Id + "; acao=ensaiomesa; ResultadoDiferido=True; ProductionReady=False");
                return;
            }
            Respond(command, "concluida", message);
        }
        catch (System.Exception ex)
        {
            log("OPERACAO_PRODUCAO_ERRO", ex.Message);
            System.Guid validId;
            if (command != null && System.Guid.TryParseExact(command.Id, "N", out validId)) Respond(command, "erro", ex.Message);
        }
    }
    public void CompleteMesaTrial(Command command, string diagnosticConclusion, bool error)
    {
        if (command == null || !System.Object.ReferenceEquals(command, pendingMesaTrial) || command.Action != "ensaiomesa")
            throw new System.InvalidOperationException("Conclusao diferida nao pertence ao ensaio mesa pendente.");
        Respond(command, error ? "erro" : "concluida", diagnosticConclusion);
        pendingMesaTrial = null;
    }
    public void CompleteShellTrial(Command command, string message, bool error)
    {
        if (command == null || !System.Object.ReferenceEquals(command, pendingShellTrial) || command.Action != "ensaioshell")
            throw new System.InvalidOperationException("Shell completion is not the pending GUID.");
        Respond(command, error ? "erro" : "concluida", message); pendingShellTrial = null;
    }
    private byte[] ReadInboxSnapshot()
    {
        if ((System.IO.File.GetAttributes(inbox) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new System.InvalidOperationException("Entrada de producao nao aceita vinculos.");
        // O publicador usa replace atomico: manter o handle do arquivo aberto permite ler a versao inteira.
        using (var stream = new System.IO.FileStream(inbox, System.IO.FileMode.Open, System.IO.FileAccess.Read,
            System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete))
        {
            long length = stream.Length;
            if (length < 20 || length > 4096) throw new System.InvalidOperationException("Entrada XML fora do limite.");
            byte[] bytes = new byte[(int)length]; int offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) throw new System.InvalidOperationException("Snapshot da entrada XML terminou antes do tamanho informado.");
                offset += read;
            }
            if (stream.ReadByte() != -1 || stream.Length != length)
                throw new System.InvalidOperationException("Tamanho da entrada XML mudou durante a leitura.");
            // Releitura limitada do mesmo handle: recusar alteracao in-place, sem registrar hash de conteudo parcial.
            stream.Position = 0;
            for (int i = 0; i < bytes.Length; i++)
                if (stream.ReadByte() != bytes[i])
                    throw new System.InvalidOperationException("Conteudo da entrada XML mudou durante a leitura.");
            if (stream.ReadByte() != -1 || stream.Length != length)
                throw new System.InvalidOperationException("Tamanho da entrada XML mudou durante a conferencia.");
            return bytes;
        }
    }
    // Consulta somente leitura. O main aplica os mesmos guardas e a visita de PREP
    // existente; este destino nativo nunca e publicado como marca/XYZ do autor.
    public static bool ResolveClosestRoad(GTA.Math.Vector3 origin, out GTA.Math.Vector3 position, out float heading)
    {
        position = new GTA.Math.Vector3(); heading = 0f;
        if (!ValidPosition(origin)) throw new System.InvalidOperationException("Origem da via proxima invalida.");
        using (var nativePosition = new GTA.Native.OutputArgument())
        using (var nativeHeading = new GTA.Native.OutputArgument())
        {
            bool found = GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                origin.X, origin.Y, origin.Z, nativePosition, nativeHeading, 0, 3f, 0);
            if (!found) return false;
            GTA.Math.Vector3 measured = nativePosition.GetResult<GTA.Math.Vector3>();
            float measuredHeading = nativeHeading.GetResult<float>();
            if (!ValidPosition(measured) || !Finite(measuredHeading) || measuredHeading < 0f || measuredHeading >= 360f)
                throw new System.InvalidOperationException("Getter da via proxima devolveu posicao/direcao invalida.");
            position = measured; heading = measuredHeading; return true;
        }
    }
    private static bool ValidPosition(GTA.Math.Vector3 value)
    {
        return Finite(value.X) && Finite(value.Y) && Finite(value.Z) &&
            System.Math.Abs(value.X) <= 15000f && System.Math.Abs(value.Y) <= 15000f && System.Math.Abs(value.Z) <= 3000f;
    }
    private static bool Finite(float value) { return !System.Single.IsNaN(value) && !System.Single.IsInfinity(value); }
    private void Respond(Command command, string state, string message)
    {
        string path = System.IO.Path.Combine(results, "operacao_producao_" + command.Id + ".xml");
        if (System.IO.File.Exists(path)) return;
        System.IO.Directory.CreateDirectory(results);
        var settings = new System.Xml.XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = true };
        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
        {
            using (var writer = System.Xml.XmlWriter.Create(stream, settings))
            {
                writer.WriteStartElement("Resultado"); writer.WriteAttributeString("id", command.Id);
                writer.WriteAttributeString("acao", command.Action); writer.WriteAttributeString("estado", state);
                writer.WriteAttributeString("dataUtc", System.DateTimeOffset.UtcNow.ToString("o"));
                writer.WriteString(message ?? ""); writer.WriteEndElement(); writer.Flush(); stream.Flush(true);
            }
        }
        log("OPERACAO_PRODUCAO_RESULTADO", "Id=" + command.Id + "; acao=" + command.Action + "; estado=" + state + "; " + message);
    }
}

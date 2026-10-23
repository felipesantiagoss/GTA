// Concatenado ao .3.cs publicado: não adicionar diretivas using neste arquivo.
// As coordenadas vêm somente de uma marca capturada pelo usuário dentro do jogo.
public sealed class LocaisDiretor
{
    private const long MaxCatalogSize = 1000000;
    private readonly System.Action<string, string> log;
    private readonly object catalogLock = new object();

    public sealed class MarcaLocal
    {
        public GTA.Math.Vector3 Position;
        public float Heading;
    }

    private sealed class CatalogSnapshot
    {
        public System.Xml.XmlDocument Document;
        public string Hash;
        public bool Exists;
    }

    public LocaisDiretor(System.Action<string, string> log)
    {
        this.log = log;
    }

    public void Save(string path, string id, GTA.Ped player)
    {
        RequireId(id);
        string fullPath = RequirePath(path);
        if (System.Object.ReferenceEquals(player, null) || !player.Exists() || player.IsDead || player.IsInVehicle())
            throw new System.InvalidOperationException("Locais: fique a pé e com o personagem vivo para marcar o local.");

        GTA.Math.Vector3 position = player.Position;
        float heading = player.Heading;
        RequireCoordinate(position.X, "x", id);
        RequireCoordinate(position.Y, "y", id);
        RequireCoordinate(position.Z, "z", id);
        RequireHeading(heading, id);

        lock (catalogLock)
        {
            CatalogSnapshot snapshot = ReadCatalog(fullPath, true);
            System.Xml.XmlDocument document = snapshot.Document;
            System.Xml.XmlElement root = document.DocumentElement;
            System.Xml.XmlElement previous = FindLocal(root, id);
            System.Xml.XmlElement local = document.CreateElement("Local");
            local.SetAttribute("id", id);
            local.SetAttribute("x", Number(position.X));
            local.SetAttribute("y", Number(position.Y));
            local.SetAttribute("z", Number(position.Z));
            local.SetAttribute("direcao", Number(heading));
            local.SetAttribute("dataUtc", System.DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            local.SetAttribute("origem", "capturado_no_jogo");
            if (System.Object.ReferenceEquals(previous, null))
                root.AppendChild(local);
            else
                root.ReplaceChild(local, previous);
            ValidateCatalog(document);

            string directory = System.IO.Path.GetDirectoryName(fullPath);
            System.IO.Directory.CreateDirectory(directory);
            if (snapshot.Exists)
                CreateVerifiedBackup(fullPath, snapshot.Hash);
            string temporary = fullPath + ".tmp-" + System.Guid.NewGuid().ToString("N");
            bool ownsTemporary = false;
            try
            {
                using (System.IO.FileStream stream = new System.IO.FileStream(temporary,
                    System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
                {
                    ownsTemporary = true;
                    System.Xml.XmlWriterSettings settings = new System.Xml.XmlWriterSettings();
                    settings.Encoding = new System.Text.UTF8Encoding(false);
                    settings.Indent = true;
                    settings.CloseOutput = false;
                    using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(stream, settings))
                        document.Save(writer);
                    stream.Flush(true);
                }
                // Validar o arquivo escrito antes de substituir o catálogo anterior.
                CatalogSnapshot written = ReadCatalog(temporary, false);
                if (snapshot.Exists)
                {
                    if (!System.IO.File.Exists(fullPath) || HashFile(fullPath) != snapshot.Hash)
                        throw new System.InvalidOperationException("Locais: o catálogo mudou durante a marcação. Nenhuma marca foi substituída; tente marcar novamente.");
                    System.IO.File.Replace(temporary, fullPath, null);
                }
                else
                    System.IO.File.Move(temporary, fullPath);
                ownsTemporary = false;
                if (HashFile(fullPath) != written.Hash)
                    throw new System.InvalidOperationException("Locais: a conferência do catálogo salvo falhou. Consulte o log e preserve o backup.");
                Log("LOCAL_MARCADO", "Id=" + id + "; substituiu=" + !System.Object.ReferenceEquals(previous, null) +
                    "; arquivo=" + fullPath + "; SHA256=" + written.Hash + "; origem=capturado_no_jogo.");
            }
            finally
            {
                if (ownsTemporary && System.IO.File.Exists(temporary))
                    System.IO.File.Delete(temporary);
            }
        }
    }

    public MarcaLocal Load(string path, string id)
    {
        RequireId(id);
        string fullPath = RequirePath(path);
        lock (catalogLock)
        {
            if (!System.IO.File.Exists(fullPath))
                throw MissingLocal(id);
            CatalogSnapshot snapshot = ReadCatalog(fullPath, false);
            System.Xml.XmlElement local = FindLocal(snapshot.Document.DocumentElement, id);
            if (System.Object.ReferenceEquals(local, null))
                throw MissingLocal(id);
            MarcaLocal result = ReadMark(local);
            Log("LOCAL_CARREGADO", "Id=" + id + "; arquivo=" + fullPath + "; origem=capturado_no_jogo.");
            return result;
        }
    }

    private static CatalogSnapshot ReadCatalog(string path, bool createIfMissing)
    {
        CatalogSnapshot result = new CatalogSnapshot();
        result.Document = new System.Xml.XmlDocument();
        result.Document.XmlResolver = null;
        if (createIfMissing && !System.IO.File.Exists(path))
        {
            result.Document.AppendChild(result.Document.CreateXmlDeclaration("1.0", "utf-8", null));
            System.Xml.XmlElement root = result.Document.CreateElement("LocaisMarcados");
            root.SetAttribute("versao", "1");
            result.Document.AppendChild(root);
            return result;
        }
        try
        {
            RequireRegularFile(path);
            using (System.IO.FileStream stream = new System.IO.FileStream(path,
                System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            {
                if (stream.Length > MaxCatalogSize)
                    throw new System.InvalidOperationException("Locais: o catálogo ultrapassa o limite de 1 MB.");
                result.Hash = HashStream(stream);
                stream.Position = 0;
                System.Xml.XmlReaderSettings settings = new System.Xml.XmlReaderSettings();
                settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
                settings.XmlResolver = null;
                settings.MaxCharactersInDocument = MaxCatalogSize;
                using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(stream, settings))
                    result.Document.Load(reader);
            }
            result.Exists = true;
            ValidateCatalog(result.Document);
            return result;
        }
        catch (System.Xml.XmlException ex)
        {
            throw new System.InvalidOperationException("Locais: XML inválido no catálogo, linha " + ex.LineNumber +
                ". Corrija o arquivo antes de marcar ou montar.", ex);
        }
    }

    private static void ValidateCatalog(System.Xml.XmlDocument document)
    {
        System.Xml.XmlElement root = document.DocumentElement;
        if (System.Object.ReferenceEquals(root, null) || root.Name != "LocaisMarcados" || root.NamespaceURI.Length != 0)
            throw new System.InvalidOperationException("Locais: o catálogo precisa da raiz LocaisMarcados, sem namespace.");
        RequireAttributes(root, new string[] { "versao" }, "Catálogo");
        if (root.GetAttribute("versao") != "1")
            throw new System.InvalidOperationException("Locais: a versão do catálogo precisa ser 1.");
        System.Collections.Generic.HashSet<string> ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (System.Xml.XmlNode node in root.ChildNodes)
        {
            if (IsDecoration(node))
                continue;
            System.Xml.XmlElement local = node as System.Xml.XmlElement;
            if (System.Object.ReferenceEquals(local, null) || local.Name != "Local" || local.NamespaceURI.Length != 0)
                throw new System.InvalidOperationException("Locais: a raiz do catálogo aceita somente elementos Local.");
            RequireAttributes(local, new string[] { "id", "x", "y", "z", "direcao", "dataUtc", "origem" }, "Local");
            string id = local.GetAttribute("id");
            RequireId(id);
            if (!ids.Add(id))
                throw new System.InvalidOperationException("Locais: o código " + id + " aparece mais de uma vez no catálogo.");
            if (local.GetAttribute("origem") != "capturado_no_jogo")
                throw new System.InvalidOperationException("Locais: " + id + " precisa da origem capturado_no_jogo.");
            ReadMark(local);
            RequireUtc(local.GetAttribute("dataUtc"), id);
            foreach (System.Xml.XmlNode child in local.ChildNodes)
                if (!IsDecoration(child))
                    throw new System.InvalidOperationException("Locais: " + id + " deve guardar os dados somente nos atributos, sem conteúdo interno.");
        }
    }

    private static void RequireAttributes(System.Xml.XmlElement element, string[] expected, string label)
    {
        foreach (System.Xml.XmlAttribute attribute in element.Attributes)
        {
            bool known = false;
            foreach (string name in expected)
                if (attribute.Name == name && attribute.NamespaceURI.Length == 0)
                    known = true;
            if (!known)
                throw new System.InvalidOperationException("Locais: " + label + " tem um atributo desconhecido: " + attribute.Name + ".");
        }
        foreach (string name in expected)
            if (!element.HasAttribute(name))
                throw new System.InvalidOperationException("Locais: falta o atributo " + name + " em " + label + ".");
    }

    private static bool IsDecoration(System.Xml.XmlNode node)
    {
        return node.NodeType == System.Xml.XmlNodeType.Comment || node.NodeType == System.Xml.XmlNodeType.Whitespace ||
            node.NodeType == System.Xml.XmlNodeType.SignificantWhitespace ||
            (node.NodeType == System.Xml.XmlNodeType.Text && System.String.IsNullOrWhiteSpace(node.Value));
    }

    private static MarcaLocal ReadMark(System.Xml.XmlElement local)
    {
        string id = local.GetAttribute("id");
        float x = ReadNumber(local, "x");
        float y = ReadNumber(local, "y");
        float z = ReadNumber(local, "z");
        float heading = ReadNumber(local, "direcao");
        RequireCoordinate(x, "x", id);
        RequireCoordinate(y, "y", id);
        RequireCoordinate(z, "z", id);
        RequireHeading(heading, id);
        return new MarcaLocal { Position = new GTA.Math.Vector3(x, y, z), Heading = heading };
    }

    private static System.Xml.XmlElement FindLocal(System.Xml.XmlElement root, string id)
    {
        foreach (System.Xml.XmlNode node in root.ChildNodes)
        {
            System.Xml.XmlElement local = node as System.Xml.XmlElement;
            if (!System.Object.ReferenceEquals(local, null) && local.Name == "Local" && local.GetAttribute("id") == id)
                return local;
        }
        return null;
    }

    private static float ReadNumber(System.Xml.XmlElement local, string field)
    {
        float result;
        if (!System.Single.TryParse(local.GetAttribute(field), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out result) || !IsFinite(result))
            throw new System.InvalidOperationException("Locais: " + local.GetAttribute("id") + ", campo " + field +
                ", precisa de um número finito com ponto decimal.");
        return result;
    }

    private static void RequireCoordinate(float value, string field, string id)
    {
        if (!IsFinite(value) || System.Math.Abs(value) > 20000f)
            throw new System.InvalidOperationException("Locais: " + id + ", campo " + field + ", precisa ficar entre -20000 e 20000.");
    }

    private static void RequireHeading(float value, string id)
    {
        if (!IsFinite(value) || value < 0f || value > 360f)
            throw new System.InvalidOperationException("Locais: a direção de " + id + " precisa ficar entre 0 e 360 graus.");
    }

    private static void RequireUtc(string value, string id)
    {
        bool valid = value.EndsWith("Z", System.StringComparison.Ordinal) || value.EndsWith("+00:00", System.StringComparison.Ordinal);
        try
        {
            if (!valid || System.Xml.XmlConvert.ToDateTimeOffset(value).Offset != System.TimeSpan.Zero)
                throw new System.FormatException();
        }
        catch (System.FormatException ex)
        {
            throw new System.InvalidOperationException("Locais: dataUtc de " + id + " precisa ser uma data UTC válida, terminada em Z ou +00:00.", ex);
        }
    }

    private static void RequireId(string id)
    {
        if (System.String.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id,
            "\\A[A-Z][A-Z0-9_]{0,47}\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new System.InvalidOperationException("Locais: use um código de até 48 caracteres, iniciado por uma letra maiúscula, com letras maiúsculas, números ou sublinhado.");
    }

    private static string RequirePath(string path)
    {
        if (System.String.IsNullOrWhiteSpace(path))
            throw new System.InvalidOperationException("Locais: informe o caminho do catálogo de locais.");
        return System.IO.Path.GetFullPath(path);
    }

    private static void RequireRegularFile(string path)
    {
        if ((System.IO.File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new System.InvalidOperationException("Locais: o catálogo é um vínculo. Use um arquivo normal para preservar os backups.");
    }

    private void CreateVerifiedBackup(string path, string expectedHash)
    {
        RequireRegularFile(path);
        string backup = path + ".backup-" + System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ",
            System.Globalization.CultureInfo.InvariantCulture) + "-" + System.Guid.NewGuid().ToString("N") + ".xml";
        using (System.IO.FileStream source = new System.IO.FileStream(path,
            System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
        {
            if (HashStream(source) != expectedHash)
                throw new System.InvalidOperationException("Locais: o catálogo mudou antes do backup. Tente marcar novamente.");
            source.Position = 0;
            using (System.IO.FileStream destination = new System.IO.FileStream(backup,
                System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
            {
                source.CopyTo(destination);
                destination.Flush(true);
            }
            if (HashFile(backup) != expectedHash)
                throw new System.InvalidOperationException("Locais: o backup não conferiu. O catálogo anterior foi preservado.");
        }
        Log("BACKUP_LOCAL", "Arquivo=" + backup + "; SHA256=" + expectedHash + "; conferido=True.");
    }

    private static string HashFile(string path)
    {
        using (System.IO.FileStream stream = new System.IO.FileStream(path,
            System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            return HashStream(stream);
    }

    private static string HashStream(System.IO.Stream stream)
    {
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            return System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static System.InvalidOperationException MissingLocal(string id)
    {
        return new System.InvalidOperationException("O local " + id + " ainda não foi marcado. Vá ao lugar a pé e aperte F3 para marcar.");
    }

    private static bool IsFinite(float value) { return !System.Single.IsNaN(value) && !System.Single.IsInfinity(value); }
    private static string Number(float value) { return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }

    private void Log(string name, string message)
    {
        if (System.Object.ReferenceEquals(log, null))
            return;
        try { log(name, message); }
        catch (System.Exception) { }
    }
}

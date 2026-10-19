using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YLFramework.Services.Save
{
    /// <summary>
    /// 仅显式调用的离线迁移。来源只读，目标必须尚不存在；失败 staging 保留用于诊断。
    /// 合成格式验证不代表已验证任何真实历史存档。无自动发现、DES 解密或原地升级。
    /// </summary>
    public static class SaveMigration
    {
        /// <summary>
        /// 将 YL v1 根复制到全新 v2 根。源/目标不得相同或互相包含，原索引及备份另存 .legacy-v1。
        /// 来源必须是已停写的复制样本；逐文件复制不承诺运行中游戏的跨文件一致快照。
        /// 在最终 Directory.Move 前可取消；发布后不再报告取消。
        /// </summary>
        public static void ImportVersion1(string sourceRoot, string destinationRoot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = SaveFileIO.NormalizeRoot(sourceRoot);
            var destination = SaveFileIO.NormalizeRoot(destinationRoot);
            var comparison = StringComparison.OrdinalIgnoreCase;
            var separator = Path.DirectorySeparatorChar.ToString();
            if (source.Equals(destination, comparison) || source.StartsWith(destination + separator, comparison) || destination.StartsWith(source + separator, comparison))
                throw new ArgumentException("Migration source and destination must be separate roots.");
            if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Migration destination already exists: " + destination);
            var parent = Path.GetDirectoryName(destination);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Create the destination parent explicitly: " + parent);
            var indexPath = Path.Combine(source, "SaveData/SaveSystemData.json");
            SaveFileIO.EnsureNoLinks(indexPath);
            var status = SaveFileIO.ReadJsonFile<SaveIndex>(indexPath, null, out var index, out var error, out var sourceJson);
            if (status != SaveReadStatus.Success) throw new InvalidDataException("Migration requires an intact primary v1 index. Copy/repair a sample explicitly first: " + error);
            if (index.DataVersion != 1) throw new SaveVersionException(index.DataVersion);
            index.DataVersion = SaveStore.FormatVersion;
            index.CurrentSaveID = null;
            index.Validate();
            var staging = destination + ".importing-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            // 全量保留源树中的数据、备份和未知文件，不对原游戏路径作任何推断或清理。
            CopyTree(source, staging, cancellationToken);
            var copiedIndex = Path.Combine(staging, "SaveData/SaveSystemData.json");
            var copiedStatus = SaveFileIO.ReadJsonFile<SaveIndex>(copiedIndex, null, out var copied, out _, out var copiedJson);
            if (copiedStatus != SaveReadStatus.Success || copied.DataVersion != 1 || copiedJson != sourceJson || File.ReadAllText(indexPath) != sourceJson)
                throw new InvalidDataException("Source changed during migration; unpublished copy retained at " + staging);
            File.Move(copiedIndex, copiedIndex + ".legacy-v1");
            if (File.Exists(copiedIndex + ".bak")) File.Move(copiedIndex + ".bak", copiedIndex + ".bak.legacy-v1");
            SaveFileIO.WriteTextAtomic(copiedIndex, SaveFileIO.Serialize(index, null, Formatting.Indented), cancellationToken, false);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
        }

        /// <summary>
        /// 导入调用方明确选中的明文 ArchiveModel*.sav 复制样本，映射到一个尚不存在的 YL 槽。
        /// 不读取 current/list.sav，不覆盖原档，不支持加密输入。模型具体类型由调用方提供，未映射字段拒绝导入；必要字段由DTO的Required约束声明。
        /// </summary>
        public static T ImportArchiveModel<T>(string sourceFile, SaveStore destination, int destinationId,
            CancellationToken cancellationToken = default) where T : class, ISaveModel
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.GetFullPath(sourceFile);
            SaveFileIO.EnsureNoLinks(source);
            var text = File.ReadAllText(source, new System.Text.UTF8Encoding(false, true));
            T model;
            try
            {
                JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                using (var raw = new StringReader(text))
                using (var reader = new JsonTextReader(raw))
                    model = JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }).Deserialize<T>(reader);
                if (model == null) throw new JsonSerializationException("Archive model is null.");
            }
            catch (JsonException error)
            { throw new InvalidDataException("Only explicitly selected plain JSON archive models are supported; encrypted/invalid input was not modified.", error); }
            model.Id = destinationId;
            return destination.ImportModelAsync(model, destinationId, cancellationToken).GetAwaiter().GetResult();
        }

        private static void CopyTree(string source, string destination, CancellationToken token)
        {
            SaveFileIO.EnsureNoLinks(source);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                token.ThrowIfCancellationRequested();
                SaveFileIO.EnsureNoLinks(file);
                var target = Path.Combine(destination, Path.GetFileName(file));
                var modified = File.GetLastWriteTimeUtc(file);
                using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                    { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
                    output.Flush(true);
                }
                File.SetLastWriteTimeUtc(target, modified);
            }
            foreach (var directory in Directory.EnumerateDirectories(source))
            {
                token.ThrowIfCancellationRequested();
                SaveFileIO.EnsureNoLinks(directory);
                var target = Path.Combine(destination, Path.GetFileName(directory));
                Directory.CreateDirectory(target);
                CopyTree(directory, target, token);
            }
        }
    }
}

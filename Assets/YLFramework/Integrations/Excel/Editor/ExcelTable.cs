// Adapted from XF ExcelSystem 1.0.7 (MIT, Copyright (c) 2025 弦风课堂).
// YL: MiniExcel backend, strict diagnostics and atomic file replacement.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;

namespace YLFramework.Excel.Editor
{
    internal sealed class ExcelColumn
    {
        internal string Cell;
        internal string Name;
        internal ExcelFieldsType Type;
        internal string Notes;
    }

    internal sealed class ExcelTable
    {
        internal readonly List<ExcelColumn> Columns = new List<ExcelColumn>();
        internal readonly List<KeyValuePair<int, IDictionary<string, object>>> Rows = new List<KeyValuePair<int, IDictionary<string, object>>>();
        internal string Source;
        internal string Hash;

        internal static ExcelTable Read(string path, string sheetName, string excludePrefix = "")
        {
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Only .xlsx workbooks are supported: " + path);
            var table = new ExcelTable { Source = Path.GetFullPath(path), Hash = ExcelFiles.Hash(path) };
            var rows = MiniExcel.Query(path, false, sheetName, ExcelType.XLSX, configuration: new OpenXmlConfiguration { EnableSharedStringCache = false })
                .Cast<IDictionary<string, object>>().ToList();
            if (rows.Count < 3) throw new FormatException(path + ": expected field/type/description headers in rows 1-3.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in rows[0])
            {
                string name = Text(item.Value);
                if (name.Length == 0 || (!string.IsNullOrEmpty(excludePrefix) && name.StartsWith(excludePrefix, StringComparison.Ordinal))) continue;
                if (!names.Add(name)) throw new FormatException(path + "!" + item.Key + "1: duplicate field " + name);
                rows[1].TryGetValue(item.Key, out var rawType);
                string typeText = Text(rawType);
                if (typeText.Equals("boolean", StringComparison.OrdinalIgnoreCase)) typeText = "Bool";
                if (typeText.Equals("int64", StringComparison.OrdinalIgnoreCase)) typeText = "Long";
                if (!Enum.TryParse(typeText, true, out ExcelFieldsType type) || type == ExcelFieldsType.None || !Enum.IsDefined(typeof(ExcelFieldsType), type))
                    throw new FormatException(path + "!" + item.Key + "2: invalid field type " + typeText);
                rows[2].TryGetValue(item.Key, out var notes);
                table.Columns.Add(new ExcelColumn { Cell = item.Key, Name = name, Type = type, Notes = Text(notes) });
            }
            if (table.Columns.Count == 0) throw new FormatException(path + ": no fields found.");
            for (int i = 3; i < rows.Count; i++)
            {
                if (rows[i].Values.All(v => v == null || Text(v).Length == 0)) continue;
                table.Rows.Add(new KeyValuePair<int, IDictionary<string, object>>(i + 1, rows[i]));
            }
            table.AssertUnchanged();
            return table;
        }

        internal void AssertUnchanged()
        {
            if (Hash != ExcelFiles.Hash(Source)) throw new InvalidOperationException("Workbook changed after preview; preview again: " + Source);
        }
        internal static string Text(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        internal object Value(IDictionary<string, object> row, ExcelColumn column) => row.TryGetValue(column.Cell, out var value) ? value : null;
        internal string Location(int row, ExcelColumn column) => Source + "!" + column.Cell + row + " (" + column.Name + ")";

        internal static void Write(string path, IReadOnlyList<ExcelColumn> columns, IEnumerable<IDictionary<string, object>> data, string sheetName)
        {
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Only .xlsx output is supported.");
            var rows = new List<IDictionary<string, object>>();
            rows.Add(columns.ToDictionary(c => c.Name, c => (object)c.Name));
            rows.Add(columns.ToDictionary(c => c.Name, c => (object)c.Type.ToString().ToLowerInvariant()));
            rows.Add(columns.ToDictionary(c => c.Name, c => (object)(c.Notes ?? string.Empty)));
            rows.AddRange(data);
            ExcelFiles.WriteAtomic(path, temporary => MiniExcel.SaveAs(temporary, rows, false, sheetName, ExcelType.XLSX,
                new OpenXmlConfiguration { AutoFilter = false, TableStyles = TableStyles.None }));
        }
    }

    internal static class ExcelFiles
    {
        internal static string Hash(string path)
        {
            if (!File.Exists(path)) return null;
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream));
        }

        internal static void WriteAtomic(string path, Action<string> writer)
        {
            path = Path.GetFullPath(path);
            string parent = Path.GetDirectoryName(path);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
            string temporary = Path.Combine(parent, "." + Path.GetFileNameWithoutExtension(path) + "." + Guid.NewGuid().ToString("N") + ".tmp" + Path.GetExtension(path));
            try
            {
                writer(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}

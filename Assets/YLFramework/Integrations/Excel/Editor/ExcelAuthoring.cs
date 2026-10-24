using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using YLFramework.Excel;

namespace YLFramework.Integrations.Excel.Editor
{
    public static partial class ExcelAuthoring
    {
        public const string DataTypeColumn = "DATA_TYPE_FULL_CLASS_NAME";

        public static void ListToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1")
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            Export(values.Cast<object>().ToList(), typeof(T), path, sheetName, false);
        }

        public static ExcelImportPlan PreviewList<T>(string path, List<T> values, string sheetName = null) where T : class, new()
        {
            var plan = new ExcelImportPlan();
            try
            {
                if (values == null)
                {
                    throw new ArgumentNullException(nameof(values));
                }

                var table = ExcelTable.Read(path, sheetName);
                var previous = values.ToArray();
                var previousStates = previous.Select(item => ExcelValues.State(item)).ToArray();
                var before = Snapshots(previous.Cast<object>());
                var staged = new List<T>();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in table.Rows)
                {
                    var target = new T();
                    ImportRow(table, row, target, plan);
                    try
                    {
                        if (!keys.Add(ExcelValues.Key(target, staged.Count)))
                        {
                            plan.Error(table.Source + ": duplicate ID at row " + row.Key);
                        }
                    }
                    catch (Exception e)
                    {
                        plan.Error(table.Source + ": row " + row.Key + ": " + e.Message);
                    }
                    staged.Add(target);
                }
                if (plan.Errors.Count == 0)
                {
                    plan.Diff(before, Snapshots(staged.Cast<object>()));
                }

                var stagedStates = staged.Select(item => ExcelValues.State(item)).ToArray();
                plan.Prepare(() =>
                {
                    table.AssertUnchanged();
                    if (!values.SequenceEqual(previous) || !values.Select(item => ExcelValues.State(item)).SequenceEqual(previousStates))
                    {
                        throw new InvalidOperationException("Target changed after preview; preview again.");
                    }

                    if (!staged.Select(item => ExcelValues.State(item)).SequenceEqual(stagedStates))
                    {
                        throw new InvalidOperationException("Staged data or asset references changed after preview; preview again.");
                    }

                    if (values.Capacity < staged.Count)
                    {
                        values.Capacity = staged.Count;
                    }

                    values.Clear();
                    values.AddRange(staged);
                });
            }
            catch (Exception e)
            {
                plan.Error(path + ": " + e.Message);
            }
            return plan;
        }

        public static ExcelImportPlan PreviewJson(string path, string jsonPath, string excludePrefix = "", string sheetName = null)
        {
            var plan = new ExcelImportPlan();
            try
            {
                if (!string.Equals(Path.GetExtension(jsonPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("JSON output must end with .json.");
                }

                if (Path.GetFullPath(path).Equals(Path.GetFullPath(jsonPath), StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("Input and output must differ.");
                }

                var table = ExcelTable.Read(path, sheetName, excludePrefix);
                string originalHash = ExcelFiles.Hash(jsonPath);
                string original = File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : null;
                var data = new List<Dictionary<string, object>>();
                foreach (var row in table.Rows)
                {
                    var item = new Dictionary<string, object>();
                    foreach (var column in table.Columns)
                    {
                        try
                        {
                            item.Add(column.Name, ExcelValues.Primitive(table.Value(row.Value, column), column.Type));
                        }
                        catch (Exception e)
                        {
                            plan.Error(table.Location(row.Key, column) + ": " + e.Message);
                        }
                    }
                    data.Add(item);
                }
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                plan.Diff(original == null ? new Dictionary<string, string>() : new Dictionary<string, string> { [jsonPath] = original }, new Dictionary<string, string> { [jsonPath] = json });
                plan.Prepare(() =>
                {
                    table.AssertUnchanged();
                    if (originalHash != ExcelFiles.Hash(jsonPath))
                    {
                        throw new InvalidOperationException("JSON target changed after preview; preview again.");
                    }

                    ExcelFiles.WriteAtomic(jsonPath, temporary => File.WriteAllText(temporary, json, new UTF8Encoding(false)));
                });
            }
            catch (Exception e)
            {
                plan.Error(path + ": " + e.Message);
            }
            return plan;
        }

        private static void ImportRow(ExcelTable table, KeyValuePair<int, IDictionary<string, object>> row, object target, ExcelImportPlan plan)
        {
            var fields = ExcelValues.Fields(target.GetType()).ToDictionary(ExcelValues.ColumnName);
            foreach (var column in table.Columns)
            {
                if (column.Name == DataTypeColumn)
                {
                    continue;
                }

                if (!fields.TryGetValue(column.Name, out var field))
                {
                    if (!string.IsNullOrEmpty(ExcelTable.Text(table.Value(row.Value, column))))
                    {
                        plan.Error(table.Location(row.Key, column) + ": unknown or ignored field on " + target.GetType().FullName);
                    }

                    continue;
                }
                try
                {
                    ExcelValues.Import(target, field, table.Value(row.Value, column), column.Type);
                }
                catch (Exception e)
                {
                    plan.Error(table.Location(row.Key, column) + ": " + e.Message);
                }
            }
            var id = ExcelValues.Id(target.GetType());
            if (id != null && !table.Columns.Any(c => c.Name == ExcelValues.ColumnName(id)))
            {
                plan.Error(table.Source + ": missing ID column " + ExcelValues.ColumnName(id));
            }
        }

        private static Dictionary<string, string> Snapshots(IEnumerable<object> values, bool requireId = false)
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            int index = 0;
            foreach (var value in values)
            {
                string key = ExcelValues.Key(value, index++, requireId);
                if (snapshot.ContainsKey(key))
                {
                    throw new InvalidOperationException("Duplicate target ID: " + key);
                }

                snapshot.Add(key, value.GetType().FullName + ":" + ExcelValues.State(value));
            }
            return snapshot;
        }
        private static void Export(IReadOnlyList<object> values, Type baseType, string path, string sheetName, bool includeType)
        {
            if (values.Any(value => value == null))
            {
                throw new InvalidOperationException("Cannot export null rows.");
            }

            var columns = new List<ExcelColumn>();
            if (includeType)
            {
                columns.Add(new ExcelColumn { Name = DataTypeColumn, Type = ExcelFieldsType.String, Notes = "ScriptableObject full type name" });
            }

            foreach (var type in (includeType ? values.Select(v => v.GetType()).Concat(new[] { baseType }).Distinct() : new[] { baseType }))
            {
                foreach (var field in ExcelValues.Fields(type))
                {
                    var name = ExcelValues.ColumnName(field);
                    var existing = columns.FirstOrDefault(c => c.Name == name);
                    var fieldType = ExcelValues.FieldType(field.FieldType);
                    if (existing != null && existing.Type != fieldType)
                    {
                        throw new InvalidOperationException("Conflicting field types: " + name);
                    }

                    if (existing == null)
                    {
                        columns.Add(new ExcelColumn { Name = name, Type = fieldType, Notes = field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? string.Empty });
                    }
                }
            }

            if (columns.Count == 0)
            {
                throw new InvalidOperationException("No serializable fields on " + baseType.FullName);
            }

            var data = new List<IDictionary<string, object>>();
            foreach (var target in values)
            {
                var fields = ExcelValues.Fields(target.GetType()).ToDictionary(ExcelValues.ColumnName);
                var row = new Dictionary<string, object>();
                foreach (var column in columns)
                {
                    row.Add(column.Name, column.Name == DataTypeColumn ? target.GetType().FullName : fields.TryGetValue(column.Name, out var field) ? ExcelValues.Export(target, field) : null);
                }

                data.Add(row);
            }
            ExcelTable.Write(path, columns, data, sheetName);
        }
    }
}

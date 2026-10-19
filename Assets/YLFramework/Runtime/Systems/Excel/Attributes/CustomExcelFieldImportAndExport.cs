using System;
using System.Reflection;

namespace YLFramework.Excel
{
    /// <summary>字段作者工具的转换与赋值边界；保留来源扩展方式，不引入解析器。</summary>
    public abstract class CustomExcelFieldImportAndExport : Attribute
    {
        /// <summary>将输入单元值转换为领域字段所需的数据。</summary>
        public abstract object Import(object scriptableObject, object value, ExcelFieldsType type);

        /// <summary>将领域字段转换为作者工具可写入的值。</summary>
        public abstract object Export(object scriptableObject, object value, ExcelFieldsType type);

        /// <summary>将Import的结果赋给目标字段；由具体转换器处理领域引用。</summary>
        public abstract void SetValue(object scriptableObject, FieldInfo fieldInfo, object value);
    }
}

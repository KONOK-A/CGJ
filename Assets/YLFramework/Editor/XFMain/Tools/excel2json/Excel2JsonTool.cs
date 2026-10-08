// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/Tools/excel2json/Excel2JsonTool.cs
using System;
using System.IO; 
using System.Text; 
using UnityEngine;

namespace YLFramework.Core.Editor
{
    /// <summary>
    /// Excel转换Json的工具类
    /// </summary>
    public class Excel2JsonTool
    {
        /// <summary>
        /// 把excel转成json
        /// </summary>
        /// <param name="excelPath">excel文件路径</param>
        /// <param name="jsonPath">转换后json文件的路径</param>
        /// <param name="header">表中有几行是表头</param>
        /// <param name="exclude_prefix">导出时，排除掉包含指定前缀的列</param>
        [Obsolete("已过时,请安装ExcelSystem拓展包,使用ExcelManager.ExcelToJson代替!",true)]
        public static void ExcelToJson(string excelPath, string jsonPath, int header, string exclude_prefix)
        { 
        }


        /// <summary>
        /// 把某个目录下所有的 excel 文件转成 json
        /// </summary>
        /// <param name="excelDir">excel所在的目录</param>
        /// <param name="pattern">匹配excel的模式,例如:  "*.xls" 或  "*.xlsx"</param>
        /// <param name="jsonDir">转换结束存放json的目录</param>
        /// <param name="header">表中有几行是表头</param>
        /// <param name="exclude_prefix">导出时，排除掉包含指定前缀的列</param>
        /// <exception cref="System.Exception"></exception>
        [Obsolete("已过时,请安装ExcelSystem拓展包,使用ExcelManager.ExcelToJson代替!", true)]
        public static void ExcelToJson(string excelDir, string pattern ,string jsonDir ,int header, string exclude_prefix) {

             
        }

         


    }
}

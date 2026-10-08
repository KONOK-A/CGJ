using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CSharp;
using UnityEditor;
using YLFramework.Utilities.Extensions;

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// 资源名常量类生成器：根据工程内的 AssetBundle 标记生成 Bundle 名与资源名常量类源码。
    /// </summary>
    public static class ResDataCodeGenerator
    {
        /// <summary>
        /// 生成资源名常量类源码并写入 writer：每个 Bundle 生成一个类
        /// （类名 = Bundle 名首字母大写，BundleName 常量 = 原 Bundle 名，资源名成员首字母大写）；
        /// 首字符为数字的 Bundle 名与重名的资源名会被跳过。
        /// </summary>
        public static void WriteClass(TextWriter writer, string @namespace)
        {
             var assetBundleInfos = new List<AssetBundleInfo>();
             
             var assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
             foreach (var assetBundleName in assetBundleNames)
             {
                 //获得该AssetBundle记录的所有资源的存储路径
                 var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
                 
                 assetBundleInfos.Add(new AssetBundleInfo(assetBundleName)
                 {
                     //记录的资源名称
                     Assets = assetPaths
                         .Select(Path.GetFileNameWithoutExtension)
                         .ToArray()
                 });

                 var compileUnit = new CodeCompileUnit();
                 var codeNamespace = new CodeNamespace(@namespace);
                 compileUnit.Namespaces.Add(codeNamespace);

                 foreach (var assetBundleInfo in assetBundleInfos)
                 {
                     var className = assetBundleInfo.Name;
                     //记录的包名首字母要写小写
                     var bundleName = className[..1].ToLower() + className[1..];
                     //包名的首字母为数字则不能记录
                     if (int.TryParse(bundleName[0].ToString(), out _))
                     {
                         continue;
                     }
                     //类名首字母要大写
                     className = className[..1].ToUpper() + className[1..]
                         .RemoveInvalidCharsFromClassName();
                     var codeType = new CodeTypeDeclaration(className);
                     codeNamespace.Types.Add(codeType);
                     //代表AssetBundle名字的类成员变量 ：public const string BundleName = 包名
                     var bundleNameField = new CodeMemberField
                     {
                         Name = "BundleName",
                         Attributes = MemberAttributes.Public | MemberAttributes.Const,
                         Type = new CodeTypeReference(typeof(string))
                     };
                     codeType.Members.Add(bundleNameField);
                     //初始化变量值
                     bundleNameField.InitExpression = 
                         new CodePrimitiveExpression(bundleName.ToLowerInvariant());
                     //检查资源名变量命名重复的字典
                     var checkRepeatDict = new Dictionary<string, string>();
                     //记录包里所有的资源名
                     foreach (var asset in assetBundleInfo.Assets)
                     {
                         var assetNameField = new CodeMemberField
                         {
                             Attributes = MemberAttributes.Public | MemberAttributes.Const
                         };
                         var content = Path.GetFileNameWithoutExtension(asset);

                         //将资源名去除无用字符后首字母大写
                         var memberName = content.RemoveInvalidCharsFromClassName();
                         memberName = memberName[..1].ToUpper() + content[1..];

                         assetNameField.Name = memberName;
                         assetNameField.Type = new CodeTypeReference(typeof(string));

                         if (!assetNameField.Name.StartsWith("[") && !assetNameField.Name.EndsWith(" [") &&
                             checkRepeatDict.TryAdd(assetNameField.Name, asset))
                         {
                             codeType.Members.Add(assetNameField);
                         }
                         
                         assetNameField.InitExpression = new CodePrimitiveExpression(content);
                     }
                     checkRepeatDict.Clear();
                 }

                 var provider = new CSharpCodeProvider();
                 var options = new CodeGeneratorOptions()
                 {
                     BlankLinesBetweenMembers = false,
                     BracingStyle = "C",
                 };  
                 
                 provider.GenerateCodeFromCompileUnit(compileUnit, writer, options);
             }
             
        }
        
        
    }
}
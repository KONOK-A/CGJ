using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Utilities.Extensions;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// 资源服务设置窗口（菜单：YLFramework/Service/ResService）。
    /// </summary>
    public class ResServiceEditorWindow : EditorWindow
    {
        
        [MenuItem("YLFramework/Service/ResService %#r")]
        public static void OpenResServiceWindow()
        {
            //创建一个浮动的窗口
            var window = (ResServiceEditorWindow)GetWindow(typeof(ResServiceEditorWindow),true);
            window.titleContent = new GUIContent("YL Resource");
            window.minSize = new Vector2(600, 500);
            window.position = new Rect(100, 100, 760, 700);
            window.Show();
        }
        
        private ResServiceView _resServiceView;
        private Vector2 _scroll;

        private void OnEnable()
        {
            _resServiceView = new ResServiceView
            {
                EditorWindow = this
            };
            _resServiceView.Init();
            EditorApplication.projectChanged += RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged += RefreshView;
            Selection.selectionChanged += Repaint;
        }

        private void RefreshView()
        {
            _resServiceView?.Init();
            Repaint();
        }

        /// <summary>是否在构建时自动生成资源名常量类（由窗口开关持久化到 EditorPrefs）。</summary>
        public static bool EnableGenerateClass =>
            EditorPrefs.GetBool(ResServiceView.KEY_AUTOGENERATE_RES_REFERENCE, false);

        public void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged -= RefreshView;
            Selection.selectionChanged -= Repaint;
            _resServiceView?.OnDispose();
            _resServiceView = null;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.BeginVertical();
            _resServiceView.OnGUI();
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>资源服务窗口的 GUI 视图。</summary>
    public class ResServiceView
    {
        private string _preview;
        private string _precheckError;
        private string _status;
        private bool _hasBuilds;
        private bool _hasOutputs;
        private EditorApplication.CallbackFunction _pending;
        //设定标记的AssetBundle的显示格式
        private readonly Lazy<GUIStyle> _markABStyle = new(() => new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 15
        });

        private readonly Lazy<GUIStyle> _windowNameStyle = new(() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        });
        
        public EditorWindow EditorWindow { get; set; }
        
        
        public const string KEY_AUTOGENERATE_RES_REFERENCE = "KEY_AUTOGENERATE_RES_REFERENCE";

        private bool EnableAutoGenerateResReference
        {
            get => EditorPrefs.GetBool(KEY_AUTOGENERATE_RES_REFERENCE,false);
            set => EditorPrefs.SetBool(KEY_AUTOGENERATE_RES_REFERENCE, value);
        }

        /// <summary>更新实际平台与只读预检快照，不创建输出目录。</summary>
        public void Init()
        {
            _precheckError = null;
            _hasBuilds = _hasOutputs = false;
            try
            {
                var build = ResourceBuildOperation.CreatePlan(EditorUserBuildSettings.activeBuildTarget, true);
                var clear = ResourceBuildOperation.CreatePlan(EditorUserBuildSettings.activeBuildTarget, false);
                _hasBuilds = build.Outputs.Count > 0;
                _hasOutputs = clear.Outputs.Any(output => System.IO.Directory.Exists(output.Path));
                _preview = _hasBuilds ? build.Summary : "没有可构建的 Bundle。\n" + clear.Summary;
            }
            catch (Exception exception) { _precheckError = exception.Message; _preview = null; }
        }

        public void OnGUI()
        {
            //标题
            GUILayout.Label(LocalText.ResService,_windowNameStyle.Value);
            
            GUILayout.BeginVertical();
            
            //保存路径
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("PersistentPath:",GUILayout.Width(100));
                    GUILayout.TextField(Application.persistentDataPath);
                }
                GUILayout.EndHorizontal();
                if (GUILayout.Button(LocalText.OpenPersistent))
                {
                    EditorUtility.RevealInFinder(Application.persistentDataPath);
                }
            }
            
            EditorGUILayout.LabelField("实际构建平台", EditorUserBuildSettings.activeBuildTarget.ToString());
            if (GUILayout.Button("打开 Unity Build Settings")) EditorWindow.GetWindow<BuildPlayerWindow>().Show();
            EditorGUILayout.HelpBox("平台来自 Unity Build Settings；YL Resource 不切换平台，不操作 XFAB。标记/取消会保存并重导入，不支持 importer Undo。", MessageType.Info);
            if (_precheckError != null) EditorGUILayout.HelpBox("操作已禁用：" + _precheckError, MessageType.Error);
            else EditorGUILayout.HelpBox(_preview ?? string.Empty, MessageType.None);
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
            
            //绘制模式选择弹窗
            GUILayout.BeginHorizontal();
            {
                EnableAutoGenerateResReference =
                    GUILayout.Toggle(EnableAutoGenerateResReference, LocalText.AutoGenerateResReference);
            }
            GUILayout.EndHorizontal();
            //环境模式选择
            GUILayout.BeginHorizontal();
            {
                var index = ResServiceEditorAPI.SimulationMode ? 0 : 1;
                index = EditorGUILayout.Popup(index, LocalText.ModeMenu,GUILayout.Width(100));
                if (index != (ResServiceEditorAPI.SimulationMode ? 0 : 1))
                {
                    ResServiceEditorAPI.SimulationMode = index == 0;
                }
            }
            GUILayout.EndHorizontal();
            
            //生成资源名引用文件
            if (GUILayout.Button(LocalText.GenerateResReference))
            {
                RunOperation(() => { BuildScript.WriteResReferenceClass(); return true; }, "资源常量已生成到 Assets/YLFrameworkData/YLResData.cs。业务 asmdef 请引用 YLFramework.Generated.ResourceNames；编译结果以 Unity Console 为准。");
            }
            //构建按钮
            using (new EditorGUI.DisabledScope(_pending != null || _precheckError != null || !_hasBuilds))
            if (GUILayout.Button(LocalText.Build))
            {
                QueueOperation(() => BuildScript.BuildWithConfirmation(EditorUserBuildSettings.activeBuildTarget), "Bundle 与 ResData 构建、发布成功。");
            }
            //清除按钮
            using (new EditorGUI.DisabledScope(_pending != null || _precheckError != null || !_hasOutputs))
            if (GUILayout.Button(LocalText.ForceClear))
            {
                QueueOperation(ResServiceEditorAPI.ClearWithConfirmation, "当前平台明确归属的 YL 产物已清理。");
            }
            GUILayout.Space(10);
            
            //标记的资源展示
            {
                GUILayout.Label(LocalText.MarkedAb,_markABStyle.Value);
                {
                    GUILayout.BeginVertical("box");
                    {
                        foreach (var assetPath in AssetDatabase.GetAllAssetBundleNames()
                                     .SelectMany(ab =>
                                     {
                                         var assetPathsOfAB = AssetDatabase.GetAssetPathsFromAssetBundle(ab);
                                         return assetPathsOfAB.Select(asset =>
                                         {
                                             if (ResServiceAssetsMenu.IsMarked(asset))
                                             {
                                                 return asset;
                                             }
                                             return ResServiceAssetsMenu.IsMarked(asset.GetFolderPath())
                                                 ? asset.GetFolderPath()
                                                 : null;
                                         }).Where(r => r != null).Distinct();//去重
                                     }))
                            
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(assetPath);
                                if(GUILayout.Button(LocalText.Select,GUILayout.Width(50)))
                                {
                                    Selection.objects = new[]
                                    {
                                        AssetDatabase.LoadAssetAtPath<Object>(assetPath)
                                    };
                                }
                                if (GUILayout.Button(LocalText.CancelMark, GUILayout.Width(75)))
                                {
                                    //再次标记即可取消标记
                                     RunOperation(() => { ResServiceAssetsMenu.MarkAB(assetPath); return true; }, "已取消标记并重新导入：" + assetPath);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
        }

        private void RunOperation(Func<bool> operation, string success)
        {
            try { _status = operation() ? success : "操作已取消，或没有可操作的产物；未修改旧输出。"; }
            catch (Exception exception) { _status = "操作未完成：" + exception.Message; }
            Init();
            if (EditorWindow) EditorWindow.Repaint();
        }

        private void QueueOperation(Func<bool> operation, string success)
        {
            if (_pending != null) return;
            _pending = () =>
            {
                _pending = null;
                RunOperation(operation, success);
            };
            EditorApplication.delayCall += _pending;
        }

        /// <summary>关闭时取消本窗口尚未执行的操作；不删除旧版本号偏好。</summary>
        public void OnDispose()
        {
            if (_pending != null) EditorApplication.delayCall -= _pending;
            _pending = null;
        }

        private static class LocalText
        {
            private static bool IsCn => true;
            
            public static string ResService => IsCn ? "资源服务设置" : "Res Service Settings";
            public static string OpenPersistent => IsCn ? "打开 Persistent 目录" : "Open Persistent Folder";
            public static string GenerateResReference => IsCn ? "生成资源名常量文件" : "Generate Res Reference File";
            public static string Build => IsCn ? "构建" : "Build";
            public static string ForceClear => IsCn ? "清理当前平台 YL 产物" : "Clear Current YL Outputs";

            public static string AutoGenerateResReference =>
                IsCn ? "打包时，自动生成资源名常量文件" : "Auto Generate Res Reference File";
            
            private static readonly string[] _modeMenuCN = {
                "模拟模式",
                "真机模式"
            };

            private static readonly string[] _modeMenuEN = {
                "SimulationMode",
                "DeviceMode"
            };
            
            public static string[] ModeMenu => IsCn ? _modeMenuCN : _modeMenuEN;
            public static string CancelMark =>
                IsCn ? "取消标记" : "Cancel Mark";

            public static string Select =>
                IsCn ? "选择" : "Select";

            public static string MarkedAb =>
                IsCn ? "已标记的 AB" : "Marked AB";
        }
    }

    
}

using System;
using System.Collections.Generic;
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
            window.position = new Rect(100, 100, 600, 400);
            window.Show();
        }
        
        private ResServiceView _resServiceView;

        private void OnEnable()
        {
            _resServiceView = new ResServiceView
            {
                EditorWindow = this
            };
            _resServiceView.Init();
        }

        /// <summary>是否在构建时自动生成资源名常量类（由窗口开关持久化到 EditorPrefs）。</summary>
        public static bool EnableGenerateClass =>
            EditorPrefs.GetBool(ResServiceView.KEY_AUTOGENERATE_RES_REFERENCE, false);

        public void OnDisable()
        {
            _resServiceView.OnDispose();
            _resServiceView = null;
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            _resServiceView.OnGUI();
            GUILayout.EndVertical();
            GUILayout.Space(50);
        }
    }

    /// <summary>资源服务窗口的 GUI 视图。</summary>
    public class ResServiceView
    {
        private string _resVersion = "100";
        private int _buildTargetPlatformIndex;
        private Vector2 _scrollViewPosition;
        //设定标记的AssetBundle的显示格式
        private readonly Lazy<GUIStyle> _markABStyle = new(() => new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 15
        });
        private string[] _buildTargetPlatforms = {"Windows/MacOS","iOS","Android","WebGL","WSAPlayer"};

        private readonly Lazy<GUIStyle> _windowNameStyle = new(() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        });
        
        public EditorWindow EditorWindow { get; set; }
        
        
        private const string KEY_YLAssetBundleBuilder_RES_VERSION = "KEY_YLAssetBundleBuilder_RES_VERSION";
        public const string KEY_AUTOGENERATE_RES_REFERENCE = "KEY_AUTOGENERATE_RES_REFERENCE";

        private bool EnableAutoGenerateResReference
        {
            get => EditorPrefs.GetBool(KEY_AUTOGENERATE_RES_REFERENCE,false);
            set => EditorPrefs.SetBool(KEY_AUTOGENERATE_RES_REFERENCE, value);
        }

        /// <summary>初始化视图：读取保存的资源版本号，并按当前构建目标选中平台下拉项。</summary>
        public void Init()
        {
            _resVersion = EditorPrefs.GetString(KEY_YLAssetBundleBuilder_RES_VERSION,"100");
            _buildTargetPlatformIndex = EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.WSAPlayer => 4,
                BuildTarget.WebGL => 3,
                BuildTarget.Android => 2,
                BuildTarget.iOS => 1,
                _ => 0
            };
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
            
            //构建平台选择
            _buildTargetPlatformIndex = GUILayout.Toolbar(_buildTargetPlatformIndex, _buildTargetPlatforms);
            
            //绘制模式选择弹窗
            GUILayout.BeginHorizontal();
            {
                EnableAutoGenerateResReference =
                    GUILayout.Toggle(EnableAutoGenerateResReference, LocalText.AutoGenerateResReference);
                if (EnableAutoGenerateResReference)
                {
                    //绘制个弹窗
                }
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
            
            //资源版本号绘制
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(LocalText.ResService,GUILayout.Width(100));
                _resVersion = EditorGUILayout.TextField(_resVersion);
            }
            GUILayout.EndHorizontal();
            //生成资源名引用文件
            if (GUILayout.Button(LocalText.GenerateResReference))
            {
                BuildScript.WriteResReferenceClass();
            }
            //构建按钮
            if (GUILayout.Button(LocalText.Build))
            {
                EditorLifecycle.AddCommend(() =>
                {
                    if (EditorWindow)
                    {
                        EditorWindow.Close();
                    }
                    //实际生成AssetBundle
                    ResServiceEditorAPI.BuildAssetBundles();
                });
            }
            //清除按钮
            if (GUILayout.Button(LocalText.ForceClear))
            {
                ResServiceEditorAPI.ForceClearAssetBundlesHaveBuilt();
            }
            GUILayout.Space(10);
            
            //标记的资源展示
            {
                GUILayout.Label(LocalText.MarkedAb,_markABStyle.Value);
                _scrollViewPosition = GUILayout.BeginScrollView(_scrollViewPosition);
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
                                    ResServiceAssetsMenu.MarkAB(assetPath);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }

        /// <summary>关闭前把资源版本号保存到 EditorPrefs。</summary>
        public void OnDispose()
        {
            EditorPrefs.SetString(KEY_YLAssetBundleBuilder_RES_VERSION, _resVersion);
        }

        private static class LocalText
        {
            private static bool IsCn => true;
            
            public static string ResService => IsCn ? "资源服务设置" : "Res Service Settings";
            public static string OpenPersistent => IsCn ? "打开 Persistent 目录" : "Open Persistent Folder";
            public static string GenerateResReference => IsCn ? "生成资源名常量文件" : "Generate Res Reference File";
            public static string Build => IsCn ? "构建" : "Build";
            public static string ForceClear => IsCn ? "清空AssetBundles" : "Clear AssetBundles";

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
            public static string ResVersion => IsCn ? "资源版本号" : "Res Version";
            public static string CancelMark =>
                IsCn ? "取消标记" : "Cancel Mark";

            public static string Select =>
                IsCn ? "选择" : "Select";

            public static string MarkedAb =>
                IsCn ? "已标记的 AB" : "Marked AB";
        }
    }

    /// <summary>
    /// 在 EditorApplication.update 中逐条执行入队的编辑器操作（供按钮回调延迟执行构建等操作）。
    /// </summary>
    internal static class EditorLifecycle
    {
        static EditorLifecycle()
        {
            EditorApplication.update += Update;
        }

        private static Queue<Action> Commends { get; } = new();

        private static void Update()
        {
            ExecuteCommand();
        }

        /// <summary>把操作加入队列，等待 EditorApplication.update 回调时执行。</summary>
        public static void AddCommend(Action action)
        {
            Commends.Enqueue(action);
        }

        private static void ExecuteCommand()
        {
            if (Commends.Count > 0)
            {
                Commends?.Dequeue()?.Invoke();
            }
        }
    }
    
}
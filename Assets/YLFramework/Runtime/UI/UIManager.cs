using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Services.UI
{
    [MonoSingletonPath("YLFramework/UISystem/UIManager")]
    public class UIManager : MonoSingleton<UIManager>
    {
        #region 内部类

        [Serializable]
        public class UILayer
        {
            public Transform root;
            public bool enableMask;
            public Image maskImage;
            private int _count;

            public void OnWindowShow()
            {
                _count += 1;
                Update();
            }

            public void OnWindowClose()
            {
                _count -= 1;
                Update();
            }

            private void Update()
            {
                if (enableMask == false) return;
                maskImage.raycastTarget = _count != 0;
                //遮罩在最上面的Ui的下一层
                var maskPos = root.childCount - 2;
                maskImage.transform.SetSiblingIndex(maskPos < 0 ? 0 : maskPos);
            }

            public void Reset()
            {
                _count = 0;
                Update();
            }
        }

        #endregion

        public override void OnSingletonInit()
        {
            InitTipsPool();
            base.OnSingletonInit();
        }

        protected override void OnDestroy()
        {
            if (_tipsPool != null)
            {
                _tipsPool.Clear(tip => tip.DestroyGameObjGracefully());
                _tipsPool = null;
            }
            base.OnDestroy();
        }

        #region 字段

        public Dictionary<string, UIWindowData> UIWindowDataDict { get; set; }
        [SerializeField, LabelText("所有UI层级")] private UILayer[] uiLayers;
        [SerializeField, LabelText("拖拽层级")] private RectTransform dragLayer;
        [SerializeField, LabelText("提示预制体")] private GameObject uiTipItemPrefab;

        [FoldoutGroup("TipRoot"), SerializeField, LabelText("提示左上根节点")]
        private RectTransform leftTopTipRoot;

        [FoldoutGroup("TipRoot"), SerializeField, LabelText("提示左下根节点")]
        private RectTransform leftDownTipRoot;

        [FoldoutGroup("TipRoot"), SerializeField, LabelText("提示右上根节点")]
        private RectTransform rightDownTipRoot;

        [FormerlySerializedAs("rightUpTipRoot")] [FoldoutGroup("TipRoot"), SerializeField, LabelText("提示右下根节点")]
        private RectTransform rightTopTipRoot;

        [FoldoutGroup("TipRoot"), SerializeField, LabelText("提示中心根节点")]
        private RectTransform middleTipRoot;
        
        [SerializeField, LabelText("提示对象池根节点")] private Transform uiTipItemPool;

        #endregion

        #region 属性

        public UILayer[] UILayers => uiLayers;
        public RectTransform DragLayer => dragLayer;
        public static UISystemConfig UISystemConfig { get; private set; } = new();

        #endregion

        #region 加载/移除 窗口数据

        /// <summary>
        /// 向UISystem中保存一个UI窗口的信息，之后便可用UIManager控制该窗口
        /// </summary>
        /// <param name="windowKey">自定义的名称，可以是资源路径或类型名称或其他自定义</param>
        /// <param name="uiWindowData">窗口的重要数据</param>
        /// <param name="instantiateAtOnce">是否立刻实例化一个该窗口，前提是该窗口应用缓存</param>
        /// <param name="args">UI初始化外部参数</param>
        public void AddUIWindowData(string windowKey, UIWindowData uiWindowData, bool instantiateAtOnce = false,
            params object[] args)
        {
            //能添加成功，即当前保存的UI数据没有该窗口才能添加窗口数据
            if (!UIWindowDataDict.TryAdd(windowKey, uiWindowData)) return;
            if (!instantiateAtOnce) return;
            if (uiWindowData.IsCache)
            {
                var window = UISystemConfig.LoadWindow(uiWindowData.BundleName, uiWindowData.AssetName);
                window.Parent(UILayers[uiWindowData.LayerNum].root);
                uiWindowData.Instance = window;
                window.OnInit(args);
                window.WithWindowData(uiWindowData).Hide();
            }
            else
            {
                Debug.LogWarning(
                    "#YLFramework: UIWindowData中的isCache=false，但instantiateAtOnce=true!提前实例化对于不需要缓存的窗口来说没有意义");
            }
        }

        public void AddUIWindowData(Type type, UIWindowData uiWindowData, bool instantiateAtOnce = false)
        {
            AddUIWindowData(type.FullName, uiWindowData, instantiateAtOnce);
        }

        /// <summary>
        /// 获取UI窗口数据
        /// </summary>
        public UIWindowData GetUIWindowData(string windowKey)
        {
            return UIWindowDataDict.GetValueOrDefault(windowKey);
        }

        public UIWindowData GetUIWindowData(Type windowType)
        {
            return GetUIWindowData(windowType.FullName);
        }

        public UIWindowData GetUIWindowData<T>()
        {
            return GetUIWindowData(typeof(T));
        }

        /// <summary>
        /// 尝试获取UI窗口数据
        /// </summary>
        public bool TryGetUIWindowData(string windowKey, out UIWindowData windowData)
        {
            return UIWindowDataDict.TryGetValue(windowKey, out windowData);
        }

        /// <summary>
        /// 移除UI窗口数据,已存在的相关窗口会被强行删除
        /// </summary>
        public bool RemoveUIWindowData(string windowKey)
        {
            if (TryGetUIWindowData(windowKey, out var windowData))
            {
                if (windowData.Instance)
                {
                    windowData.Instance.gameObject.DestroySelfGracefully();
                }
            }

            return UIWindowDataDict.Remove(windowKey);
        }

        /// <summary>
        /// 清除所有UI窗口数据,同时会销毁所有现有窗口。
        /// 谨慎使用
        /// </summary>
        public void ClearAllUIWindowData()
        {
            var enumerator = UIWindowDataDict.GetEnumerator();
            while (enumerator.MoveNext())
            {
                enumerator.Current.Value?.Instance.gameObject.DestroySelfGracefully();
            }

            UIWindowDataDict.Clear();
        }

        #endregion

        #region UI窗口生命周期管理

        /// <summary>
        /// 显示窗口
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="layer">层级 -1等于不设置</param>
        /// <param name="args">外部参数</param>
        public T Open<T>(int layer = -1, params object[] args) where T : UIWindowBase
        {
            return Open(typeof(T), layer, args) as T;
        }

        public T Open<T>(string windowKey, int layer = -1, params object[] args) where T : UIWindowBase
        {
            return Open(windowKey, layer, args) as T;
        }

        public UIWindowBase Open(Type type, int layer = -1, params object[] args)
        {
            return Open(type.FullName, layer, args);
        }

        public UIWindowBase Open(string windowKey, int layer = -1, params object[] args)
        {
            if (TryGetUIWindowData(windowKey, out var windowData))
            {
                return ShowReally(windowData, layer, args);
            }

            Debug.LogError($"#YLFramework: 不存在UIWindowData的记录 Key:{windowKey}");
            return null;
        }

        private UIWindowBase ShowReally(UIWindowData uiWindowData, int layer = -1, params object[] args)
        {
            int layerNum = layer == -1 ? uiWindowData.LayerNum : layer;
            if (uiWindowData.Instance)
            {
                if (uiWindowData.Instance.UIEnable)
                {
                    // 原本就激活使用状态，避免内部计数问题，进行一次层UI关闭
                    uiLayers[uiWindowData.LayerNum].OnWindowClose();
                }

                //显示UI窗口
                uiWindowData.Instance.Show();
                uiWindowData.Instance.transform.SetParent(UILayers[layerNum].root);
                uiWindowData.Instance.transform.SetAsLastSibling();
                uiWindowData.Instance.ShowUI(layerNum, args);
            }
            else
            {
                //没有该窗口的实例则创建一个
                var window = UISystemConfig.LoadWindow(uiWindowData.BundleName, uiWindowData.AssetName);
                window.Parent(UILayers[layerNum].root);
                UISystemConfig.SetDefaultSizeOfWindow(window);
                uiWindowData.Instance = window;
                if (!window)
                {
                    Debug.LogError("#YLFramework: Fail to create UIWindow! Name:" + uiWindowData.AssetName);
                }
                window.OnInit(args);
                window.WithWindowData(uiWindowData).ShowUI(layerNum, args);
            }

            uiWindowData.LayerNum = layerNum;
            //该UI层进行一次窗口显示计数
            UILayers[layerNum].OnWindowShow();
            return uiWindowData.Instance;
        }

        public void ShowAsync<T>(Action<T> callback = null, int layer = -1, params object[] args) where T : UIWindowBase
        {
            ShowAsync(typeof(T), window => { callback?.Invoke(window as T); }, layer, args);
        }

        public void ShowAsync<T>(string windowKey, Action<T> callback = null, int layer = -1, params object[] args)
            where T : UIWindowBase
        {
            ShowAsync(windowKey, window => { callback?.Invoke(window as T); }, layer, args);
        }

        public void ShowAsync(Type type, Action<UIWindowBase> callback, int layer = -1, params object[] args)
        {
            ShowAsync(type.FullName, callback, layer, args);
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        /// <param name="windowKey">窗口的key</param>
        /// <param name="callback">完成回调</param>
        /// <param name="layer">层级 -1等于不设置</param>
        /// <param name="args">外部参数</param>
        public void ShowAsync(string windowKey, Action<UIWindowBase> callback = null, int layer = -1,
            params object[] args)
        {
            if (TryGetUIWindowData(windowKey, out var windowData))
            {
                ShowAsyncReally(windowData, callback, layer, args);
            }
            else
            {
                // 资源库中没有意味着不允许显示
                Debug.LogError($"#YLFramework: 不存在UIWindowData的记录 Key:{windowKey}");
            }
        }

        private void ShowAsyncReally(UIWindowData uiWindowData, Action<UIWindowBase> callback = null, int layer = -1,
            params object[] args)
        {
            int layerNum = layer == -1 ? uiWindowData.LayerNum : layer;
            if (uiWindowData.Instance != null)
            {
                if (uiWindowData.Instance.UIEnable)
                {
                    // 原本就激活使用状态，避免内部计数问题，进行一次层UI关闭
                    uiLayers[uiWindowData.LayerNum].OnWindowClose();
                }

                //显示UI窗口
                uiWindowData.Instance.gameObject.SetActive(true);
                uiWindowData.Instance.transform.SetParent(UILayers[layerNum].root);
                uiWindowData.Instance.transform.SetAsLastSibling();
                uiWindowData.Instance.ShowUI(layerNum, args);
                callback?.Invoke(uiWindowData.Instance);
            }
            else
            {
                //没有窗口的记录则创建一个新的UI窗口
                UISystemConfig.LoadWindowAsync(uiWindowData.BundleName, uiWindowData.AssetName, window =>
                {
                    if (window)
                    {
                        window.Parent(UILayers[layerNum].root);
                        UISystemConfig.SetDefaultSizeOfWindow(window);
                        uiWindowData.Instance = window;
                        window.OnInit(args);
                        window.WithWindowData(uiWindowData).ShowUI(layerNum, args);
                        callback?.Invoke(window);
                    }
                    else
                    {
                        Debug.LogError("#YLFramework: Fail to create UIWindow! Name:" + uiWindowData.AssetName);
                    }
                });
            }

            uiWindowData.LayerNum = layerNum;
            UILayers[layerNum].OnWindowShow();
        }

        #endregion

        #region 获得与销毁窗口

        /// <summary>
        /// 获取窗口
        /// </summary>
        /// <param name="windowKey">窗口Key</param>
        /// <returns>没找到会为Null</returns>
        public UIWindowBase GetUIWindow(string windowKey)
        {
            return TryGetUIWindowData(windowKey, out var windowData) ? windowData.Instance : null;
        }

        /// <summary>
        /// 获取窗口
        /// </summary>
        /// <param name="windowKey">窗口Key</param>
        /// <returns>没找到会为Null</returns>
        public T GetUIWindow<T>(string windowKey) where T : UIWindowBase
        {
            return GetUIWindow(windowKey) as T;
        }

        public T GetUIWindow<T>() where T : UIWindowBase
        {
            return GetUIWindow(typeof(T).FullName) as T;
        }

        public UIWindowBase GetUIWindow(Type windowType)
        {
            return GetUIWindow(windowType.FullName);
        }

        /// <summary>
        /// 尝试获取窗口
        /// </summary>
        public bool TryGetUIWindow(string windowKey, out UIWindowBase window)
        {
            UIWindowDataDict.TryGetValue(windowKey, out var windowData);
            window = windowData?.Instance;
            return window != null;
        }

        /// <summary>
        /// 尝试获取窗口
        /// </summary>
        public bool TryGetUIWindow<T>(string windowKey, out T window) where T : UIWindowBase
        {
            UIWindowDataDict.TryGetValue(windowKey, out UIWindowData windowData);
            window = windowData?.Instance as T;
            return window != null;
        }

        public void DestroyUIWindow(string windowKey)
        {
            var window = GetUIWindow(windowKey);
            if (window != null)
            {
                window.DestroyGameObjGracefully();
            }
        }

        #endregion

        #region 关闭窗口

        public void Close<T>()
        {
            Close(typeof(T).FullName);
        }

        public void Close(Type windowType)
        {
            Close(windowType.FullName);
        }

        public void Close(string windowKey)
        {
            if (!TryGetUIWindowData(windowKey, out var windowData)) return;
            if (windowData.Instance && CloseUIWindow(windowData))
            {
                UILayers[windowData.LayerNum].OnWindowClose();
            }
        }

        /// <summary>
        /// 尝试关闭窗口
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        public void TryClose<T>()
        {
            TryClose(typeof(T));
        }

        /// <summary>
        /// 尝试关闭窗口
        /// </summary>
        private void TryClose(Type type)
        {
            TryClose(type.FullName);
        }

        /// <summary>
        /// 尝试关闭窗口
        /// </summary>
        public bool TryClose(string windowKey)
        {
            if (!TryGetUIWindowData(windowKey, out var windowData)) return false;
            if (!windowData.Instance || !CloseUIWindow(windowData)) return false;
            UILayers[windowData.LayerNum].OnWindowClose();
            return true;
        }

        private static bool CloseUIWindow(UIWindowData uiWindowData)
        {
            if (!uiWindowData.Instance)
            {
                return false;
            }

            //是启动状态才关闭
            if (!uiWindowData.Instance.UIEnable) return false;
            uiWindowData.Instance.CloseUI();
            //缓存则只隐藏
            if (uiWindowData.IsCache)
            {
                uiWindowData.Instance.transform.SetAsFirstSibling();
                uiWindowData.Instance.Hide();
            }
            // 不缓存则销毁
            else
            {
                //TODO 卸载该物体的资源
                uiWindowData.Instance.gameObject.DestroySelfGracefully();
                uiWindowData.Instance = null;
            }

            return true;

        }

        public void CloseAll()
        {
            foreach (var windowData in UIWindowDataDict.Values.Where(windowData =>
                         windowData.Instance != null && windowData.Instance.gameObject.activeInHierarchy))
            {
                CloseUIWindow(windowData);
            }

            foreach (var uiLayer in UILayers)
            {
                uiLayer.Reset();
            }
        }

        #endregion

        #region UITip相关

        private SimpleObjectPool<UITipsItem> _tipsPool;

        private void InitTipsPool()
        {
            if (_tipsPool != null)
            {
                _tipsPool.Clear(tip => tip.DestroyGameObjGracefully());
                _tipsPool = null;
            }

            _tipsPool = new SimpleObjectPool<UITipsItem>(() =>
            {
                var go = uiTipItemPrefab.InstantiateWithParent(uiTipItemPool);
                return go?.GetComponent<UITipsItem>().Hide();
            }, tip =>
            {
                tip.OnReset();
                tip.Parent(uiTipItemPool);
            });
        }

        public void ShowTips(string content, UITipPos uiTipPos, Color textColor = default, float showTime = 2f,
            bool isFade = true, Sprite bgSprite = null)
        {
            var tip = _tipsPool.Allocate();

            var uiTipRoot = uiTipPos switch
            {
                UITipPos.LeftTop => leftTopTipRoot,
                UITipPos.LeftDown => leftDownTipRoot,
                UITipPos.RightTop => rightTopTipRoot,
                UITipPos.RightDown => rightDownTipRoot,
                UITipPos.Middle => middleTipRoot,
                _ => throw new ArgumentOutOfRangeException(nameof(uiTipPos), uiTipPos, null)
            };

            tip.Parent(uiTipRoot).OnInit(content, () => { _tipsPool.Recycle(tip); }, textColor, showTime, isFade,
                bgSprite);
        }

        #endregion

        #region 工具
        
        public Camera UICamera => UIRoot.Instance.UICamera;

        public void SetResolution(int width, int height, float matchOnWidthOrHeight)
        {
            UIRoot.Instance.SetResolution(width, height, matchOnWidthOrHeight);
        }

        public Vector2 GetResolution()
        {
            return UIRoot.Instance.GetResolution();
        }

        public float GetMatchOnWidthOrHeight()
        {
            return UIRoot.Instance.GetMatchOnWidthOrHeight();
        }
        public void ScreenSpaceOverlayRenderMode()
        {
            UIRoot.Instance.ScreenSpaceOverlayRenderMode();
        }

        public void ScreenSpaceCameraRenderMode()
        {
            UIRoot.Instance.ScreenSpaceCameraRenderMode();
        }
        

        private static readonly List<RaycastResult> RaycastResultList = new();
        private static UnityEngine.EventSystems.EventSystem _eventSystem;
        private static PointerEventData _pointerEventData;

        public static bool CheckMouseOnUI()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return CheckPositionOnUI(Input.mousePosition);
#elif ENABLE_INPUT_SYSTEM
            return CheckPositionOnUI(InputSystemBridge.GetMousePosition());
#else
            return false;
#endif
        }


        /// <summary>
        /// 检查一个坐标是否在UI上,会屏蔽名称为Mask的物体
        /// </summary>
        public static bool CheckPositionOnUI(Vector2 pos)
        {
            if (_eventSystem == null)
            {
                _eventSystem = UnityEngine.EventSystems.EventSystem.current;
                _pointerEventData = null;
            }

            if (_eventSystem == null)
            {
                return false;
            }

            if (_pointerEventData == null)
            {
                _pointerEventData = new PointerEventData(_eventSystem);
            }

            _pointerEventData.position = pos;
            // 射线去检测有没有除了Mask以外的任何UI物体
            _eventSystem.RaycastAll(_pointerEventData, RaycastResultList);
            for (int i = 0; i < RaycastResultList.Count; i++)
            {
                // 是UI，同时还不是Mask作用的物体
                if (RaycastResultList[i].gameObject.name != "Mask")
                {
                    RaycastResultList.Clear();
                    return true;
                }
            }

            RaycastResultList.Clear();
            return false;
        }

        #endregion
    }
}
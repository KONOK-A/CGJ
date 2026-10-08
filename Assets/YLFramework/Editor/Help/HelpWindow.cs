using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Help
{
    /// <summary>项目模块共用的只读帮助窗口，展示通过 <see cref="HelpRegistry"/> 注册的模块内容。</summary>
    /// <remarks>窗口只读取模块提供的展示数据，不执行示例代码；多个模块共用一个窗口实例，内容在切换到该模块时创建。</remarks>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Project.HelpKit.Editor",
        sourceAssembly: "Project.HelpKit.Editor", sourceClassName: "HelpWindow")]
    public sealed class HelpWindow : EditorWindow
    {
        internal const string MenuPath = "YLFramework/帮助";
        private static readonly string[] Tabs = { "使用指南", "API 参考", "关于与许可" };
        private static readonly string[] MemberGroups = { "构造函数", "属性", "事件", "字段与常量", "枚举值", "方法", "扩展方法" };
        [SerializeField] private string mModuleId = "", mSearch = "", mGuideId = "", mApiId = "", mAboutId = "";
        [SerializeField] private string mMemberSearch = "", mApiCategory = "";
        [SerializeField] private int mTab;
        [SerializeField] private float mSidebarWidth = 230;
        [SerializeField] private List<string> mCollapsedGroups = new List<string>();
        [SerializeField] private bool mInitialGroupsSet;
        private Vector2 mNavScroll, mReaderScroll;
        private HelpApiType[] mIndex = Array.Empty<HelpApiType>(), mVisibleTypes = Array.Empty<HelpApiType>();
        private HelpPage[] mVisibleTopics = Array.Empty<HelpPage>(), mVisibleAbout = Array.Empty<HelpPage>();
        private readonly Dictionary<string, HelpApiMember[]> mVisibleMembers = new Dictionary<string, HelpApiMember[]>();
        private HelpRegistration[] mRegistrations = Array.Empty<HelpRegistration>();
        private HelpRegistration mRegistration;
        private HelpModule mModule;
        private bool mNeedsReload;
        private string[] mShortNamespaces = Array.Empty<string>();
        private string mFilterKey;
        private string mRevealMemberKey;
        private string mMemberFilterKey;
        private HelpApiMember[] mMemberFiltered = Array.Empty<HelpApiMember>();
        [NonSerialized] private GUIStyle mTitle, mHeaderTitle, mHeading, mSubheading, mBody, mSmall, mNav, mSelectedNav, mLink, mCode, mSignature;
        [NonSerialized] private GUIStyle mPopup, mTabButton, mTabCurrent, mSearchField, mButton, mNavGroup, mApiGroup, mDetailFoldout, mNotice, mTag;
        [NonSerialized] private GUIStyle mSectionTitle, mSectionFoldout, mMemberName, mMemberBrief, mMemberSummary, mBadge, mChip, mChipSelected,
            mCodeWrap, mBodyClamp, mSmallLink, mMemberBox, mMemberBoxHover, mMemberBoxExpanded, mMemberDetailBox, mDetailHeading;
        private HelpPalette mPalette;
        private Font mMonoFont;
        private Texture2D mMemberTexture, mMemberHoverTexture, mMemberExpandedTexture, mDetailTexture;
        private string mHoverBlockKey;
        private bool mStylesDark;
        private float mReadingWidth;
        private readonly HelpSearchHighlight mHighlights = new HelpSearchHighlight();
        private readonly HashSet<string> mSearchCollapsed = new HashSet<string>();
        private readonly HashSet<string> mDetailExpanded = new HashSet<string>();
        private readonly HashSet<string> mApiSections = new HashSet<string>();
        private readonly HashSet<string> mApiSummaryExpanded = new HashSet<string>();
        private string mApiStateId, mOverloadKey;
        private readonly Dictionary<string, Vector2> mCodeScroll = new Dictionary<string, Vector2>();
        private readonly Stack<Navigation> mHistory = new Stack<Navigation>();
        private string mCopiedKey;
        private double mCopyUntil;
        private bool mRevealInNav;
        private float mNavViewportHeight;

        // “返回”使用的阅读状态快照：跳转前把页签、选中项、搜索、折叠与滚动位置整体压栈。
        private struct Navigation
        {
            internal int Tab;
            internal string Guide, Api, About, Search;
            internal string[] Collapsed, SearchCollapsed;
            internal Vector2 Reader, Nav;
        }

        [MenuItem(MenuPath)]
        private static void OpenMenu() => Open();

        /// <summary>打开公共窗口；可指定模块标识作为模块菜单的快捷入口。</summary>
        /// <param name="moduleId">已注册的模块标识；null 保留当前选择，未注册标识回退到第一个模块。</param>
        /// <returns>已显示的公共帮助窗口，重复打开时复用现有窗口。</returns>
        public static HelpWindow Open(string moduleId = null)
        {
            var window = GetWindow<HelpWindow>();
            if (moduleId != null) window.SelectModule(moduleId);
            window.minSize = new Vector2(440, 360);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("项目帮助");
            minSize = new Vector2(440, 360);
            HelpRegistry.Changed += RegistryChanged;
            mRegistration = null;
            mNeedsReload = true;
        }

        private void RegistryChanged() { mNeedsReload = true; Repaint(); }

        /// <summary>切换当前模块；与当前模块不同时清空阅读状态与返回历史。</summary>
        /// <remarks>未注册的标识由加载流程回退到第一个已注册模块。</remarks>
        internal void SelectModule(string id)
        {
            if (mModuleId != id) ResetReading();
            mModuleId = id;
            mNeedsReload = true;
            LoadModule();
            Repaint();
        }

        private void ResetReading()
        {
            mSearch = mGuideId = mApiId = mAboutId = ""; mTab = 0;
            mNavScroll = mReaderScroll = Vector2.zero;
            mCollapsedGroups.Clear(); mInitialGroupsSet = false;
            mSearchCollapsed.Clear(); mDetailExpanded.Clear(); mCodeScroll.Clear(); mHistory.Clear();
            mMemberSearch = ""; mApiCategory = ""; mApiStateId = null; mOverloadKey = null;
            mApiSections.Clear(); mApiSummaryExpanded.Clear();
            mVisibleMembers.Clear(); mRevealMemberKey = null; mMemberFilterKey = null;
            mCopiedKey = null; mFilterKey = null; mRevealInNav = true;
            mHighlights.Clear();
        }

        private void LoadModule()
        {
            if (!mNeedsReload) return;
            mNeedsReload = false;
            mRegistrations = HelpRegistry.GetModules();
            // 请求的模块未注册时回退到第一个模块；注册表为空时没有可加载的内容。
            var next = mRegistrations.FirstOrDefault(module => module.Id == mModuleId) ?? mRegistrations.FirstOrDefault();
            // 注册项未变且内容已加载时直接复用，避免重复创建模块。
            if (next == mRegistration && (next != null || mModule == null)) return;
            // 实际选中的模块与请求标识不同（首次加载或回退）时按切换模块重置阅读状态。
            if (next?.Id != mModuleId) ResetReading();
            mRegistration = next;
            mModuleId = next?.Id ?? "";
            mModule = next?.Create();
            mIndex = mModule == null ? Array.Empty<HelpApiType>() : HelpApiIndex.Build(mModule);
            mShortNamespaces = mIndex.Select(entry => entry.Type.Namespace).Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray();
            titleContent = new GUIContent(next == null ? "项目帮助" : next.DisplayName + " 帮助");
            mFilterKey = null;
            mHistory.Clear(); mDetailExpanded.Clear(); mCodeScroll.Clear(); mSearchCollapsed.Clear(); mCopiedKey = null;
            mMemberSearch = ""; mApiCategory = ""; mApiStateId = null; mOverloadKey = null;
            mVisibleMembers.Clear(); mRevealMemberKey = null; mMemberFilterKey = null;
            mApiSections.Clear(); mApiSummaryExpanded.Clear();
            mHighlights.Clear();
            mTab = Mathf.Clamp(mTab, 0, 2);
            if (!mInitialGroupsSet)
            {
                mCollapsedGroups.AddRange((mModule?.CollapsedGuideGroups ?? Array.Empty<string>()).Select(group => "guide:" + group));
                mCollapsedGroups.AddRange((mModule?.CollapsedApiGroups ?? Array.Empty<string>()).Select(group => "api:" + group));
                mInitialGroupsSet = true;
            }
        }

        private void OnDisable()
        {
            HelpRegistry.Changed -= RegistryChanged;
            // 动态字体与成员块纹理由当前窗口创建和销毁；GUIStyle 是窗口副本，不修改 EditorStyles 的共享样式。
            if (mMonoFont) DestroyImmediate(mMonoFont);
            mMonoFont = null;
            if (mMemberTexture) DestroyImmediate(mMemberTexture);
            if (mMemberHoverTexture) DestroyImmediate(mMemberHoverTexture);
            if (mMemberExpandedTexture) DestroyImmediate(mMemberExpandedTexture);
            if (mDetailTexture) DestroyImmediate(mDetailTexture);
            mMemberTexture = mMemberHoverTexture = mMemberExpandedTexture = mDetailTexture = null;
            mBody = null;
            mTabCurrent = null;
            mTag = null;
            mPalette = null;
            mHighlights.Clear();
        }

        private void OnInspectorUpdate()
        {
            if (mCopiedKey == null) return;
            if (EditorApplication.timeSinceStartup >= mCopyUntil) mCopiedKey = null;
            Repaint();
        }

        private void EnsureStyles()
        {
            var dark = EditorGUIUtility.isProSkin;
            // 样式字段是 NonSerialized：域重载后会变 null 需要重建；皮肤切换时配色也要更新。
            if (mBody != null && mHeaderTitle != null && mNotice != null && mStylesDark == dark) return;
            mStylesDark = dark;
            mPalette = new HelpPalette(dark);
            if (!mMonoFont)
                mMonoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Microsoft YaHei UI" }, 13);
            mBody = new GUIStyle(EditorStyles.label) { fontSize = 14, wordWrap = true, richText = false,
                alignment = TextAnchor.UpperLeft, padding = new RectOffset(0, 0, 3, 6) };
            mBody.normal.textColor = dark ? new Color(0.86f, 0.87f, 0.88f) : new Color(0.17f, 0.18f, 0.20f);
            mTitle = new GUIStyle(mBody) { fontSize = 24, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 3, 10) };
            mTitle.normal.textColor = dark ? new Color(0.96f, 0.97f, 0.98f) : new Color(0.10f, 0.11f, 0.13f);
            mHeaderTitle = new GUIStyle(mTitle) { fontSize = 20, wordWrap = false, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0) };
            mHeading = new GUIStyle(mBody) { fontSize = 18, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 20, 8) };
            mHeading.normal.textColor = dark ? new Color(0.92f, 0.93f, 0.95f) : new Color(0.12f, 0.13f, 0.15f);
            mSubheading = new GUIStyle(mBody) { fontSize = 15, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 12, 5) };
            mSubheading.normal.textColor = dark ? new Color(0.88f, 0.89f, 0.91f) : new Color(0.15f, 0.16f, 0.18f);
            mSmall = new GUIStyle(mBody) { fontSize = 12, padding = new RectOffset(0, 0, 1, 3) };
            mSmall.normal.textColor = mPalette.Muted;
            mNav = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 13, richText = false,
                padding = new RectOffset(22, 10, 0, 0), clipping = TextClipping.Clip, wordWrap = false };
            mNav.normal.textColor = dark ? new Color(0.82f, 0.83f, 0.85f) : new Color(0.22f, 0.23f, 0.25f);
            mSelectedNav = new GUIStyle(mNav) { fontStyle = FontStyle.Bold };
            mSelectedNav.normal.textColor = dark ? Color.white : new Color(0.06f, 0.13f, 0.20f);
            mLink = new GUIStyle(EditorStyles.linkLabel) { wordWrap = true, richText = false, fontSize = 14, alignment = TextAnchor.MiddleLeft };
            mCode = new GUIStyle(EditorStyles.label) { font = mMonoFont, fontSize = 13, wordWrap = false,
                richText = false, alignment = TextAnchor.UpperLeft, padding = new RectOffset(0, 0, 0, 0) };
            mCode.normal.textColor = dark ? new Color(0.88f, 0.89f, 0.90f) : new Color(0.12f, 0.14f, 0.16f);
            mSignature = new GUIStyle(mBody) { font = mMonoFont, fontSize = 13, padding = new RectOffset(0, 0, 2, 4) };
            mSignature.normal.textColor = dark ? new Color(0.84f, 0.88f, 0.94f) : new Color(0.12f, 0.20f, 0.32f);
            mNavGroup = new GUIStyle(EditorStyles.foldout) { fontSize = 13, richText = false, fixedHeight = 0,
                alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            mApiGroup = new GUIStyle(mNavGroup) { fontSize = 18, fontStyle = FontStyle.Bold };
            mDetailFoldout = new GUIStyle(mNavGroup) { fontSize = 15, fontStyle = FontStyle.Bold };
            mPopup = new GUIStyle(EditorStyles.popup) { fixedHeight = 0, fontSize = 13, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 20, 4, 4) };
            // label 基础样式不自带 Toolbar 皮肤变体查找，纯自绘背景；名称保持唯一避免命中皮肤样式。
            mTabButton = new GUIStyle(EditorStyles.label) { fixedHeight = 0, fixedWidth = 0, fontSize = 13,
                name = "HelpKitTabButton", alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false,
                padding = new RectOffset(6, 6, 4, 4) };
            mTabButton.normal.textColor = dark ? new Color(0.72f, 0.73f, 0.75f) : new Color(0.35f, 0.36f, 0.38f);
            mTabButton.hover.textColor = dark ? new Color(0.90f, 0.91f, 0.93f) : new Color(0.16f, 0.17f, 0.20f);
            mTabCurrent = new GUIStyle(mTabButton) { fontStyle = FontStyle.Bold };
            mTabCurrent.normal.textColor = dark ? new Color(0.97f, 0.98f, 1f) : new Color(0.09f, 0.15f, 0.23f);
            mSearchField = new GUIStyle(EditorStyles.toolbarSearchField) { fixedHeight = 0, fontSize = 14,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset(Mathf.Max(20, EditorStyles.toolbarSearchField.padding.left), 6, 4, 4) };
            mButton = new GUIStyle(GUI.skin.button) { fixedHeight = 0, fontSize = 13, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4) };
            mNotice = new GUIStyle(EditorStyles.helpBox) { fixedHeight = 0, richText = false, padding = new RectOffset(14, 12, 10, 10) };
            mTag = new GUIStyle(mSmall) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(7, 7, 1, 2) };
            mTag.normal.textColor = dark ? new Color(0.72f, 0.75f, 0.79f) : new Color(0.35f, 0.38f, 0.42f);
            mSectionTitle = new GUIStyle(mBody) { fontSize = 16, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 6, 4) };
            mSectionTitle.normal.textColor = dark ? new Color(0.90f, 0.91f, 0.93f) : new Color(0.14f, 0.15f, 0.17f);
            mSectionFoldout = new GUIStyle(EditorStyles.foldout) { fixedHeight = 0, fontSize = 16, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft, richText = false };
            mSectionFoldout.normal.textColor = dark ? new Color(0.88f, 0.89f, 0.92f) : new Color(0.16f, 0.17f, 0.20f);
            mMemberName = new GUIStyle(EditorStyles.label) { font = mMonoFont, fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = false,
                richText = false, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft, padding = new RectOffset(0, 0, 0, 0) };
            mMemberName.normal.textColor = dark ? new Color(0.93f, 0.94f, 0.96f) : new Color(0.10f, 0.12f, 0.15f);
            mMemberBrief = new GUIStyle(mMemberName) { fontStyle = FontStyle.Normal, fontSize = 13 };
            mMemberBrief.normal.textColor = mPalette.Muted;
            mMemberSummary = new GUIStyle(mBody) { fontSize = 13, wordWrap = false, richText = false, clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0) };
            mMemberSummary.normal.textColor = dark ? new Color(0.66f, 0.68f, 0.71f) : new Color(0.38f, 0.40f, 0.44f);
            mBadge = new GUIStyle(mSmall) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = false,
                clipping = TextClipping.Clip, padding = new RectOffset(6, 6, 1, 1) };
            mBadge.normal.textColor = dark ? new Color(0.74f, 0.76f, 0.80f) : new Color(0.36f, 0.39f, 0.44f);
            mChip = new GUIStyle(EditorStyles.label) { fixedHeight = 0, fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = false,
                wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(8, 8, 3, 3) };
            mChip.normal.textColor = dark ? new Color(0.80f, 0.81f, 0.84f) : new Color(0.22f, 0.24f, 0.27f);
            mChip.hover.textColor = dark ? new Color(0.94f, 0.95f, 0.97f) : new Color(0.10f, 0.12f, 0.16f);
            mChipSelected = new GUIStyle(mChip) { fontStyle = FontStyle.Bold };
            mChipSelected.normal.textColor = dark ? Color.white : new Color(0.06f, 0.13f, 0.20f);
            mCodeWrap = new GUIStyle(mCode) { wordWrap = true };
            mBodyClamp = new GUIStyle(mBody) { clipping = TextClipping.Clip };
            mSmallLink = new GUIStyle(EditorStyles.linkLabel) { fontSize = 13, wordWrap = false, richText = false };
            // 成员信息块：3 个状态各一张九宫格圆角纹理（底色、细边框、左侧强调线）。
            if (mMemberTexture) DestroyImmediate(mMemberTexture);
            if (mMemberHoverTexture) DestroyImmediate(mMemberHoverTexture);
            if (mMemberExpandedTexture) DestroyImmediate(mMemberExpandedTexture);
            if (mDetailTexture) DestroyImmediate(mDetailTexture);
            const int boxSize = 12, boxRadius = 4, boxSlice = 4;
            mMemberTexture = HelpTexture.RoundedBox(boxSize, boxRadius, mPalette.MemberBg, mPalette.MemberBorder, mPalette.MemberAccent);
            mMemberHoverTexture = HelpTexture.RoundedBox(boxSize, boxRadius, mPalette.MemberHoverBg, mPalette.MemberHoverBorder, mPalette.MemberHoverAccent);
            mMemberExpandedTexture = HelpTexture.RoundedBox(boxSize, boxRadius, mPalette.MemberExpandedBg, mPalette.MemberExpandedBorder, mPalette.MemberExpandedAccent);
            mMemberBox = new GUIStyle { padding = new RectOffset(12, 12, 8, 10), border = new RectOffset(boxSlice, boxSlice, boxSlice, boxSlice) };
            mMemberBox.normal.background = mMemberTexture;
            mMemberBoxHover = new GUIStyle(mMemberBox);
            mMemberBoxHover.normal.background = mMemberHoverTexture;
            mMemberBoxExpanded = new GUIStyle(mMemberBox);
            mMemberBoxExpanded.normal.background = mMemberExpandedTexture;
            // 详情内嵌区：无强调线的圆角浅底层，与外层成员块形成两级层次。
            mDetailTexture = HelpTexture.RoundedBox(boxSize, boxRadius, mPalette.DetailInsetBg, mPalette.DetailInsetBorder);
            mMemberDetailBox = new GUIStyle { padding = new RectOffset(14, 12, 11, 12), border = new RectOffset(boxSlice, boxSlice, boxSlice, boxSlice) };
            mMemberDetailBox.normal.background = mDetailTexture;
            mDetailHeading = new GUIStyle(mBody) { fontSize = 14, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 12, 4) };
            mDetailHeading.normal.textColor = dark ? new Color(0.90f, 0.91f, 0.94f) : new Color(0.14f, 0.15f, 0.18f);
            mHighlights.Clear();
        }

        private void OnGUI()
        {
            EnsureStyles();
            LoadModule();
            // 悬停态（页签、目录行、分隔条）需要鼠标移动时重绘。
            if (Event.current.type == EventType.MouseMove) Repaint();
            var header = new HelpHeaderLayout(position.width);
            if (position.width < 360 || position.height < header.BodyY + 36)
            {
                GUI.Label(new Rect(6, 6, Mathf.Max(0, position.width - 12), Mathf.Max(0, position.height - 12)),
                    "窗口较小，请放大或调整停靠布局。", mBody);
                return;
            }
            if (Event.current.type == EventType.KeyDown && Event.current.alt && Event.current.keyCode == KeyCode.LeftArrow && mHistory.Count != 0)
                GoBack();
            DrawHeader(header);
            UpdateResults();
            var body = new Rect(0, header.BodyY, position.width, Mathf.Max(0, position.height - header.BodyY));
            mSidebarWidth = ClampSidebar(mSidebarWidth, body.width);
            var nav = new Rect(body.x, body.y, mSidebarWidth, body.height);
            var splitter = new Rect(nav.xMax, body.y, 6, body.height);
            var reading = new Rect(splitter.xMax, body.y, Mathf.Max(0, body.width - splitter.xMax), body.height);
            DrawSplitter(splitter, body.width);
            DrawNavigation(nav);
            DrawReader(reading);
        }

        /// <summary>限制侧栏宽度：下限 132；窗口够宽时为正文区保留至少 240 像素。</summary>
        internal static float ClampSidebar(float width, float windowWidth) => Mathf.Clamp(width, 132, Mathf.Max(132, windowWidth - 240));

        private void DrawHeader(HelpHeaderLayout layout)
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, layout.BodyY), mPalette.HeaderBg);
            HelpDraw.Rule(new Rect(0, layout.BodyY - 1, position.width, 1), mPalette.HeaderRule);
            EditorGUI.DrawRect(new Rect(layout.Title.x - 8, layout.Title.y + 4, 3, layout.Title.height - 8), mPalette.TabAccent);
            GUI.Label(layout.Title, "项目帮助", mHeaderTitle);
            using (new EditorGUI.DisabledScope(mRegistrations.Length == 0))
            {
                var selected = Array.FindIndex(mRegistrations, module => module.Id == mModuleId);
                var next = EditorGUI.Popup(layout.Module, selected,
                    mRegistrations.Select(module => module.DisplayName).ToArray(), mPopup);
                if (next >= 0 && next != selected)
                { SelectModule(mRegistrations[next].Id); GUIUtility.ExitGUI(); }
            }
            using (new EditorGUI.DisabledScope(mHistory.Count == 0))
                if (GUI.Button(layout.Back, new GUIContent("返回", "返回上一阅读位置（Alt+左箭头）"), mButton)) GoBack();
            DrawTabs(layout);
            GUI.SetNextControlName("ProjectHelpSearch");
            var search = GUI.TextField(layout.Search, mSearch, mSearchField);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(mSearch)))
                if (GUI.Button(layout.Clear, "清空", mButton)) search = "";
            if (search != mSearch)
            {
                mSearch = search; mSearchCollapsed.Clear(); mNavScroll = mReaderScroll = Vector2.zero; mFilterKey = null; mRevealInNav = true;
                mHighlights.Clear();
                // 过滤条件变化会改变后续布局与控件数量，终止本次事件后重绘。
                Repaint(); GUIUtility.ExitGUI();
            }
            var scope = mTab == 0 ? "搜索全部指南：标题、分组与正文；标题命中优先。" :
                mTab == 1 ? "搜索类型与成员：名称命中优先，支持 ActionKit.Sequence 限定名。" : "搜索关于信息、来源与许可。";
            GUI.Label(layout.Scope, new GUIContent(scope + " 空格分词同时匹配。"), mSmall);
        }

        private void DrawTabs(HelpHeaderLayout layout)
        {
            EditorGUI.DrawRect(layout.Tabs, mPalette.TabBarBg);
            for (var index = 1; index < Tabs.Length; index++)
            {
                var edge = layout.Tab(index);
                EditorGUI.DrawRect(new Rect(edge.x, edge.y + 7, 1, edge.height - 14), mPalette.HeaderRule);
            }
            for (var index = 0; index < Tabs.Length; index++)
            {
                var rect = layout.Tab(index);
                var current = index == mTab;
                var inner = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 3);
                if (current) EditorGUI.DrawRect(inner, mPalette.TabSelectedBg);
                else if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(inner, mPalette.TabHoverBg);
                if (current) EditorGUI.DrawRect(new Rect(rect.x + 3, rect.yMax - 3, rect.width - 6, 2), mPalette.TabAccent);
                if (GUI.Button(rect, Tabs[index], current ? mTabCurrent : mTabButton) && !current)
                    Navigate(index, index == 0 ? mGuideId : index == 1 ? mApiId : mAboutId, false);
            }
        }

        private void UpdateResults()
        {
            // 只在模块、页签或搜索词变化时重建过滤结果，避免每次 OnGUI 都扫描全部条目。
            var key = mModuleId + "\n" + mTab + "\n" + mSearch;
            if (mFilterKey == key) return;
            mFilterKey = key;
            mHighlights.Clear(); mHighlights.SetQuery(mSearch);
            var terms = HelpSearch.Tokenize(mSearch);
            var guides = mModule?.Guides ?? Array.Empty<HelpPage>();
            var about = mModule?.About ?? Array.Empty<HelpPage>();
            if (terms.Length == 0)
            {
                mVisibleTopics = guides.ToArray();
                mVisibleTypes = mIndex.ToArray();
                mVisibleAbout = about.ToArray();
                mVisibleMembers.Clear();
            }
            else
            {
                mVisibleTopics = Rank(guides, terms);
                mVisibleTypes = Rank(mIndex, terms);
                mVisibleAbout = Rank(about, terms);
                BuildVisibleMembers(terms);
            }
            if (mTab == 0 && !mVisibleTopics.Any(topic => topic.Id == mGuideId))
            { mGuideId = mVisibleTopics.FirstOrDefault()?.Id ?? ""; mRevealInNav = true; }
            if (mTab == 1 && !mVisibleTypes.Any(type => type.Id == mApiId))
            { mApiId = mVisibleTypes.FirstOrDefault()?.Id ?? ""; mRevealInNav = true; }
            if (mTab == 2 && !mVisibleAbout.Any(page => page.Id == mAboutId))
            { mAboutId = mVisibleAbout.FirstOrDefault()?.Id ?? ""; mRevealInNav = true; }
            if (mRevealInNav) RevealSelectedGroup();
        }

        /// <summary>按加权分数过滤页面；同分保持原有顺序，避免结果抖动。</summary>
        private static HelpPage[] Rank(IEnumerable<HelpPage> pages, string[] terms)
        {
            var fields = new List<HelpSearchField>();
            return pages.Select((page, index) => (page, index, score: PageScore(page, terms, fields)))
                .Where(item => item.score >= 0)
                .OrderByDescending(item => item.score).ThenBy(item => item.index)
                .Select(item => item.page).ToArray();
        }

        private static int PageScore(HelpPage page, string[] terms, List<HelpSearchField> fields)
        {
            fields.Clear(); page.AppendSearchFields(fields); return HelpSearch.Score(fields, terms);
        }

        /// <summary>按加权分数过滤类型；类型字段与全部成员字段共同参与匹配。</summary>
        private static HelpApiType[] Rank(IEnumerable<HelpApiType> types, string[] terms)
        {
            var fields = new List<HelpSearchField>();
            return types.Select((type, index) => (type, index, score: TypeScore(type, terms, fields)))
                .Where(item => item.score >= 0)
                .OrderByDescending(item => item.score).ThenBy(item => item.index)
                .Select(item => item.type).ToArray();
        }

        private static int TypeScore(HelpApiType type, string[] terms, List<HelpSearchField> fields)
        {
            fields.Clear(); type.AppendSearchFields(fields, true); return HelpSearch.Score(fields, terms);
        }

        /// <summary>收集每个可见类型中命中剩余查询词的成员，供导航列出成员级结果。</summary>
        private void BuildVisibleMembers(string[] terms)
        {
            mVisibleMembers.Clear();
            var fields = new List<HelpSearchField>();
            var context = new List<HelpSearchField>();
            var single = new string[1];
            foreach (var type in mVisibleTypes)
            {
                // 类型名、限定名与声明能覆盖的词不再作为成员结果，避免纯类型查询刷出全部成员。
                context.Clear();
                type.AppendSearchFields(context, false, false);
                var memberTerms = new List<string>();
                foreach (var term in terms)
                {
                    single[0] = term;
                    if (HelpSearch.Score(context, single) < 0) memberTerms.Add(term);
                }
                if (memberTerms.Count == 0) continue;
                var remaining = memberTerms.ToArray();
                var matched = new List<(HelpApiMember Entry, int Score, int Index)>();
                for (var index = 0; index < type.Entries.Length; index++)
                {
                    fields.Clear();
                    type.Entries[index].AppendSearchFields(fields, type.Name);
                    var score = HelpSearch.Score(fields, remaining);
                    if (score >= 0) matched.Add((type.Entries[index], score, index));
                }
                if (matched.Count == 0) continue;
                mVisibleMembers[type.Id] = matched.OrderByDescending(item => item.Score).ThenBy(item => item.Index)
                    .Select(item => item.Entry).ToArray();
            }
        }

        private void DrawNavigation(Rect rect)
        {
            if (rect.width < 24 || rect.height < 24) return;
            mNavViewportHeight = Mathf.Max(1, rect.height - 40);
            EditorGUI.DrawRect(rect, mPalette.NavBg);
            GUILayout.BeginArea(new Rect(rect.x + 4, rect.y + 5, rect.width - 8, rect.height - 10));
            mNavScroll = EditorGUILayout.BeginScrollView(mNavScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            var count = mTab == 0 ? mVisibleTopics.Length : mTab == 1 ? mVisibleTypes.Length : mVisibleAbout.Length;
            var searching = !string.IsNullOrWhiteSpace(mSearch);
            var memberCount = 0;
            if (searching && mTab == 1)
                foreach (var pair in mVisibleMembers) memberCount += pair.Value.Length;
            GUILayout.Label(!searching ? $"{count} 项" :
                memberCount > 0 ? $"找到 {count} 个类型、{memberCount} 个成员匹配" : $"找到 {count} 项匹配结果", mSmall);
            GUILayout.Space(2);
            HelpDraw.Rule(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), mPalette.NavRule);
            GUILayout.Space(5);
            if (mTab == 0)
            {
                foreach (var group in mVisibleTopics.Select(topic => topic.Group).Distinct())
                {
                    var topics = mVisibleTopics.Where(topic => topic.Group == group).ToArray();
                    if (topics.Length == 0 || !DrawGroup("guide:" + group, group, topics.Length, mNavGroup)) continue;
                    var stripe = 0;
                    foreach (var topic in topics)
                        DrawNavItem(topic.Id, topic.Title, topic.Summary, mGuideId, stripe++, searching ? "指南" : null);
                }
            }
            else if (mTab == 1)
            {
                foreach (var group in mVisibleTypes.Select(type => type.Group).Distinct())
                {
                    var types = mVisibleTypes.Where(type => type.Group == group).ToArray();
                    if (!DrawGroup("api:" + group, group, types.Length, mNavGroup)) continue;
                    var stripe = 0;
                    foreach (var type in types)
                    {
                        DrawNavItem(type.Id, type.Name, type.Summary, mApiId, stripe++, searching ? "类型" : null);
                        if (!searching || !mVisibleMembers.TryGetValue(type.Id, out var members)) continue;
                        foreach (var member in members)
                        {
                            var memberTitle = member.Name == ".ctor" ? type.Name + "()" : type.Name + "." + member.Name;
                            var memberType = type;
                            var memberEntry = member;
                            DrawNavItem(type.Id + "::" + member.Signature, memberTitle, member.Signature, "", stripe++, member.Group, 14f,
                                () => JumpToMember(memberType, memberEntry));
                        }
                    }
                }
            }
            else
            {
                var stripe = 0;
                foreach (var page in mVisibleAbout)
                    DrawNavItem(page.Id, page.Title, page.Summary, mAboutId, stripe++, searching ? "关于" : null);
            }
            if (count == 0) GUILayout.Label("没有找到结果。可尝试类型名、方法名或中文功能词；清空搜索可恢复全部条目。", mBody);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private bool DrawGroup(string key, string title, int count, GUIStyle style)
        {
            // 搜索期间使用独立的折叠集合：搜索结果中的展开/折叠不会覆盖用户手动设置的组状态。
            var searching = !string.IsNullOrWhiteSpace(mSearch);
            var open = searching ? !mSearchCollapsed.Contains(key) : !mCollapsedGroups.Contains(key);
            GUILayout.Space(style == mApiGroup ? 14 : 6);
            var rect = GUILayoutUtility.GetRect(0, Mathf.Max(28, style.lineHeight + 6), GUILayout.ExpandWidth(true));
            if (style == mNavGroup) EditorGUI.DrawRect(rect, mPalette.NavGroupBg);
            var label = new GUIContent(title + "  " + count, title);
            if (style == mApiGroup) DrawHighlight(EditorGUI.IndentedRect(rect), label.text, style);
            var next = EditorGUI.Foldout(rect, open, label, true, style);
            if (style == mNavGroup) HelpDraw.Rule(new Rect(rect.x, rect.yMax - 1, rect.width, 1), mPalette.NavGroupRule);
            else HelpDraw.Rule(new Rect(rect.x, rect.yMax - 1, rect.width, 1), mPalette.Separator);
            if (next != open)
            {
                if (searching) { if (next) mSearchCollapsed.Remove(key); else mSearchCollapsed.Add(key); }
                else { if (next) mCollapsedGroups.Remove(key); else mCollapsedGroups.Add(key); }
                Repaint(); GUIUtility.ExitGUI();
            }
            return open;
        }

        private void DrawNavItem(string id, string title, string summary, string selected, int stripe,
            string tag = null, float indent = 0f, Action click = null)
        {
            var rect = GUILayoutUtility.GetRect(0, 29, GUILayout.ExpandWidth(true));
            var current = id == selected;
            mPalette.DrawStripe(rect, stripe);
            if (current) EditorGUI.DrawRect(rect, mPalette.NavSelected);
            else if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, mPalette.NavHover);
            if (current) EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3, 3, rect.height - 6), mPalette.NavSelectedAccent);
            // 导航后把选中项滚入可视区域，只做一次；之后的滚动完全交给用户。
            if (current && mRevealInNav && Event.current.type == EventType.Repaint)
            {
                if (rect.y < mNavScroll.y) mNavScroll.y = rect.y;
                else if (rect.yMax > mNavScroll.y + mNavViewportHeight) mNavScroll.y = rect.yMax - mNavViewportHeight;
                mRevealInNav = false; Repaint();
            }
            // 结果来源标签复用导航行：整行可点，标签前缀在文本层，不改变行高与配色。
            var label = tag == null ? title : "[" + tag + "] " + title;
            var tooltip = title + "\n" + summary;
            if (GUI.Button(rect, new GUIContent("", tooltip), GUIStyle.none))
            {
                if (click != null) click();
                else if (!current) Navigate(mTab, id, false);
            }
            GUI.Label(new Rect(rect.x + indent, rect.y, Mathf.Max(0, rect.width - indent), rect.height),
                new GUIContent(label, tooltip), current ? mSelectedNav : mNav);
        }

        /// <summary>从搜索结果跳到具体成员：切换类型、展开该成员并滚动到它的信息块。</summary>
        private void JumpToMember(HelpApiType type, HelpApiMember member)
        {
            mHistory.Push(new Navigation { Tab = mTab, Guide = mGuideId, Api = mApiId, About = mAboutId,
                Search = mSearch, Nav = mNavScroll, Reader = mReaderScroll,
                Collapsed = mCollapsedGroups.ToArray(), SearchCollapsed = mSearchCollapsed.ToArray() });
            mTab = 1;
            mApiId = type.Id;
            mApiStateId = type.Id;
            mMemberSearch = ""; mApiCategory = "";
            mOverloadKey = type.Id + "\n#" + member.Name;
            var key = type.Id + "\n" + member.Signature;
            mDetailExpanded.Add(key);
            mRevealMemberKey = key;
            mReaderScroll = Vector2.zero;
            mFilterKey = null; mRevealInNav = true;
            mHighlights.Clear();
            Repaint(); GUIUtility.ExitGUI();
        }

        private void DrawSplitter(Rect rect, float windowWidth)
        {
            var hovered = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y, 1, rect.height), hovered ? mPalette.TabAccent : mPalette.Separator);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            var id = GUIUtility.GetControlID("ProjectHelpSplitter".GetHashCode(), FocusType.Passive);
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            { GUIUtility.hotControl = id; evt.Use(); }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            { mSidebarWidth = ClampSidebar(evt.mousePosition.x, windowWidth); evt.Use(); Repaint(); GUIUtility.ExitGUI(); }
            else if (evt.type == EventType.MouseUp && GUIUtility.hotControl == id)
            { GUIUtility.hotControl = 0; evt.Use(); }
        }

        private void DrawReader(Rect rect)
        {
            if (rect.width < 48 || rect.height < 24) return;
            var width = Mathf.Max(80, rect.width - 44);
            // 阅读宽度变化后字符的像素位置失效，丢弃高亮几何缓存。
            if (mReadingWidth != width) { mReadingWidth = width; mHighlights.ClearGeometry(); }
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 6, rect.width - 22, rect.height - 12));
            mReaderScroll = EditorGUILayout.BeginScrollView(mReaderScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            if (mTab == 0)
            {
                var topic = mVisibleTopics.FirstOrDefault(entry => entry.Id == mGuideId);
                if (topic != null) DrawPage(topic); else DrawEmpty();
            }
            else if (mTab == 1)
            {
                var type = mVisibleTypes.FirstOrDefault(entry => entry.Id == mApiId);
                if (type != null) DrawType(type); else DrawEmpty();
            }
            else
            {
                var page = mVisibleAbout.FirstOrDefault(entry => entry.Id == mAboutId);
                if (page != null) DrawPage(page); else DrawEmpty();
            }
            GUILayout.Space(20);
            if (mTab != 2 && mModule != null && mModule.About.Length != 0 && DrawLink("关于与许可", "查看模块信息与许可", false))
                Navigate(2, mModule.About[0].Id, true);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawEmpty()
        {
            if (mModule == null)
            { DrawText("暂无已注册的帮助模块", mTitle, false); DrawText("模块提供方注册内容后即可在这里查阅。", mBody, false); return; }
            if (mTab == 2 && mModule.About.Length == 0)
            { DrawText("该模块未提供关于信息", mTitle, false); return; }
            DrawText("没有匹配内容", mTitle, false);
            DrawText("可清空搜索，或减少关键词。搜索按名称、说明与正文加权，空格分词且所有词都需要匹配。", mBody, false);
        }

        private void DrawPage(HelpPage page)
        {
            DrawText(mTab == 0 ? "使用指南  ›  " + page.Group : "关于与许可", mSmall);
            DrawText(page.Title, mTitle);
            DrawText(page.Summary, mBody);
            if (!string.IsNullOrWhiteSpace(page.Summary)) GUILayout.Space(4);
            for (var i = 0; i < page.Blocks.Length; i++)
            {
                var block = page.Blocks[i];
                if (block.Kind == HelpBlockKind.Link)
                {
                    if (DrawLink(block.Title, block.Url)) Application.OpenURL(block.Url);
                    GUILayout.Space(4);
                    continue;
                }
                DrawText(block.Title, mHeading);
                if (block.Kind == HelpBlockKind.Code)
                { DrawText(block.Context, mBody); if (block.Content.Length != 0) DrawCode(page.Id + ":" + i, block.Content); }
                else if (block.Kind == HelpBlockKind.Notice)
                    DrawNotice(block.Content);
                else
                { DrawText(block.Content, mBody); GUILayout.Space(6); }
            }
            var related = mIndex.Where(entry => page.FeaturedTypes.Contains(entry.Type)).ToArray();
            if (related.Length == 0) return;
            DrawText("相关 API", mHeading);
            foreach (var type in related) if (DrawLink(type.Name, type.Summary)) Navigate(1, type.Id, true);
        }

        private void DrawType(HelpApiType type)
        {
            // 切换类型时只重置该类型浏览器的筛选状态；展开状态按类型/成员标识保留。
            if (mApiStateId != type.Id)
            {
                mApiStateId = type.Id;
                mApiCategory = "";
                mMemberSearch = "";
                mOverloadKey = null;
            }
            DrawText("API 参考  ›  " + type.Group, mSmall);
            DrawText(type.Name, mTitle);
            DrawText("命名空间：" + (type.Type.Namespace ?? "（全局）"), mSmall);
            DrawTypeSummary(type);
            DrawCompactCode(type.Id + ":declaration", type.Declaration);
            DrawInheritance(type);
            DrawTypeNotes(type);
            DrawMemberBrowser(type);
            DrawRelatedGuides(type);
        }

        /// <summary>类型摘要：超过三行时折叠显示，展开状态按类型标识保存。</summary>
        private void DrawTypeSummary(HelpApiType type)
        {
            if (string.IsNullOrWhiteSpace(type.Summary))
            {
                DrawText("未提供摘要说明。", mSmall, false);
                return;
            }
            var limit = mBody.lineHeight * 3;
            if (mBody.CalcHeight(new GUIContent(type.Summary), mReadingWidth) <= limit + 1f)
            {
                DrawText(type.Summary, mBody);
                GUILayout.Space(2);
                return;
            }
            if (mApiSummaryExpanded.Contains(type.Id))
            {
                DrawText(type.Summary, mBody);
                if (DrawSmallLink("收起说明")) { mApiSummaryExpanded.Remove(type.Id); Repaint(); GUIUtility.ExitGUI(); }
                return;
            }
            var rect = GUILayoutUtility.GetRect(mReadingWidth, limit, GUILayout.Width(mReadingWidth));
            DrawHighlight(rect, type.Summary, mBodyClamp);
            EditorGUI.SelectableLabel(rect, type.Summary, mBodyClamp);
            if (DrawSmallLink("展开全部说明")) { mApiSummaryExpanded.Add(type.Id); Repaint(); GUIUtility.ExitGUI(); }
        }

        /// <summary>紧凑只读代码条：高度随内容换行，不嵌套滚动条，保留复制。</summary>
        private void DrawCompactCode(string key, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var inner = Mathf.Max(40, mReadingWidth - 20);
            var textHeight = Mathf.Max(mCodeWrap.lineHeight, mCodeWrap.CalcHeight(new GUIContent(text), inner));
            var block = GUILayoutUtility.GetRect(mReadingWidth, textHeight + 44, GUILayout.Width(mReadingWidth));
            EditorGUI.DrawRect(block, mPalette.CodeBg);
            HelpDraw.Outline(block, mPalette.CodeBorder);
            var header = new Rect(block.x + 1, block.y + 1, block.width - 2, 24);
            EditorGUI.DrawRect(header, mPalette.CodeHeaderBg);
            HelpDraw.Rule(new Rect(header.x, header.yMax, header.width, 1), mPalette.CodeBorder);
            var tag = new GUIContent("C# · 只读");
            var tagSize = mTag.CalcSize(tag);
            var tagRect = new Rect(block.x + 10, block.y + 3, tagSize.x + 10, 20);
            EditorGUI.DrawRect(tagRect, mPalette.CodeTagBg);
            GUI.Label(tagRect, tag, mTag);
            var copied = mCopiedKey == key && EditorApplication.timeSinceStartup < mCopyUntil;
            if (GUI.Button(new Rect(block.xMax - 66, block.y + 2, 56, 22), copied ? "已复制" : "复制", mButton))
            {
                EditorGUIUtility.systemCopyBuffer = text;
                mCopiedKey = key; mCopyUntil = EditorApplication.timeSinceStartup + 1.6;
                ShowNotification(new GUIContent("已复制到剪贴板")); Repaint();
            }
            var view = new Rect(block.x + 10, header.yMax + 8, inner, textHeight);
            DrawHighlight(view, text, mCodeWrap);
            EditorGUI.SelectableLabel(view, text, mCodeWrap);
            GUILayout.Space(10);
        }

        /// <summary>可折叠的信息区；默认折叠，展开状态按稳定键保存。</summary>
        private bool DrawSection(string key, string title, int count)
        {
            var open = mApiSections.Contains(key);
            GUILayout.Space(12);
            var rect = GUILayoutUtility.GetRect(0, Mathf.Max(26, mSectionFoldout.lineHeight + 6), GUILayout.ExpandWidth(true));
            var label = count > 0 ? title + "  " + count : title;
            var next = EditorGUI.Foldout(rect, open, new GUIContent(label, label), true, mSectionFoldout);
            HelpDraw.Rule(new Rect(rect.x, rect.yMax - 2, rect.width, 1), mPalette.Separator);
            if (next != open)
            {
                if (next) mApiSections.Add(key); else mApiSections.Remove(key);
                Repaint(); GUIUtility.ExitGUI();
            }
            return open;
        }

        private void DrawInheritance(HelpApiType type)
        {
            // 排除 object、ValueType 和 Enum：它们是类、结构体与枚举的隐式基类型，列出只会干扰阅读。
            var bases = type.Type.GetInterfaces().Concat(type.Type.BaseType == null ? Type.EmptyTypes : new[] { type.Type.BaseType })
                .Where(contract => contract != typeof(object) && contract != typeof(ValueType) && contract != typeof(Enum)).Distinct().ToArray();
            if (bases.Length == 0 || !DrawSection(type.Id + ":bases", "继承与接口", bases.Length)) return;
            DrawText("本页展示当前类型声明的成员；继承成员可从下面的基类与接口查阅。", mBody);
            foreach (var contract in bases)
            {
                var definition = contract.IsGenericType ? contract.GetGenericTypeDefinition() : contract;
                var entry = mIndex.FirstOrDefault(item => item.Type == definition);
                var name = HelpApiFormatter.FormatType(contract, mShortNamespaces);
                if (entry == null) DrawText(name, mBody);
                else if (DrawLink(name, entry.Summary)) Navigate(1, entry.Id, true);
            }
        }

        private void DrawTypeNotes(HelpApiType type)
        {
            var parameters = type.Help.TypeParameters;
            var remarks = type.Help.Remarks;
            if (parameters.Length == 0 && string.IsNullOrWhiteSpace(remarks)) return;
            if (!DrawSection(type.Id + ":notes", "类型参数与备注", parameters.Length)) return;
            if (parameters.Length != 0)
            {
                DrawText("类型参数", mSubheading);
                foreach (var parameter in parameters) DrawParameterRow(mReadingWidth, parameter.Name, "", parameter.Description);
                GUILayout.Space(4);
            }
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                DrawText("备注", mSubheading);
                DrawText(remarks, mBody);
                GUILayout.Space(6);
            }
        }

        private void DrawRelatedGuides(HelpApiType type)
        {
            if (type.RelatedPages.Length == 0 || !DrawSection(type.Id + ":related", "相关指南", type.RelatedPages.Length)) return;
            foreach (var topic in type.RelatedPages) if (DrawLink(topic.Title, topic.Summary)) Navigate(0, topic.Id, true);
        }

        private void DrawMemberBrowser(HelpApiType type)
        {
            GUILayout.Space(18);
            DrawText("成员", mSectionTitle);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("ProjectHelpMemberSearch");
            var search = GUILayout.TextField(mMemberSearch, mSearchField, GUILayout.MinWidth(60));
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(mMemberSearch)))
                if (GUILayout.Button("清空", mButton, GUILayout.Width(54), GUILayout.Height(26))) search = "";
            GUILayout.EndHorizontal();
            if (search != mMemberSearch)
            {
                // 过滤只改变可见行；已展开的成员详情按稳定键保留，重新可见时继续展开。
                mMemberSearch = search;
                Repaint(); GUIUtility.ExitGUI();
            }
            DrawText("搜索范围：当前类型的成员；名称命中优先，其次说明与签名；空格分词同时匹配（不改动全局搜索范围）。", mSmall, false);
            var groups = MemberGroups.Where(group => type.Entries.Any(entry => entry.Group == group)).ToArray();
            DrawCategoryChips(type, groups);
            if (type.Entries.Length == 0)
            {
                DrawText("没有额外声明的成员；可从类型声明和相关接口了解其契约。", mBody);
                return;
            }
            var entries = FilteredMembers(type);
            if (entries.Length == 0)
            {
                DrawText("没有匹配的成员。清空搜索或切换分类可恢复全部成员。", mBody, false);
                return;
            }
            foreach (var overloads in entries.GroupBy(entry => entry.Name))
            {
                var items = overloads.ToArray();
                if (items.Length > 1) DrawOverloadGroup(type, overloads.Key, items);
                else DrawMemberRow(type, items[0], 0f);
            }
        }

        /// <summary>当前类型的成员筛选：分类过滤后按加权分数排序；同分保持原顺序，空查询保持原顺序。</summary>
        private HelpApiMember[] FilteredMembers(HelpApiType type)
        {
            var filterKey = type.Id + "\n" + mMemberSearch + "\n" + mApiCategory;
            if (mMemberFilterKey == filterKey) return mMemberFiltered;
            mMemberFilterKey = filterKey;
            IEnumerable<HelpApiMember> candidates = type.Entries;
            if (!string.IsNullOrEmpty(mApiCategory)) candidates = candidates.Where(entry => entry.Group == mApiCategory);
            var terms = HelpSearch.Tokenize(mMemberSearch);
            if (terms.Length == 0)
            {
                mMemberFiltered = candidates.ToArray();
                return mMemberFiltered;
            }
            var fields = new List<HelpSearchField>();
            var matched = new List<(HelpApiMember Entry, int Score, int Index)>();
            var index = 0;
            foreach (var entry in candidates)
            {
                fields.Clear();
                entry.AppendSearchFields(fields, type.Name);
                var score = HelpSearch.Score(fields, terms);
                if (score >= 0) matched.Add((entry, score, index));
                index++;
            }
            mMemberFiltered = matched.OrderByDescending(item => item.Score).ThenBy(item => item.Index)
                .Select(item => item.Entry).ToArray();
            return mMemberFiltered;
        }

        /// <summary>成员分类芯片：只显示实际存在的类别与真实数量。</summary>
        private void DrawCategoryChips(HelpApiType type, string[] groups)
        {
            var labels = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("", "全部  " + type.Entries.Length) };
            foreach (var group in groups) labels.Add(new KeyValuePair<string, string>(group, group + "  " + type.Entries.Count(entry => entry.Group == group)));
            const float gap = 6f;
            var sizes = labels.Select(label => mChip.CalcSize(new GUIContent(label.Value))).ToArray();
            var chipHeight = Mathf.Max(22f, mChip.lineHeight + 6);
            var rows = 1;
            var used = 0f;
            foreach (var size in sizes)
            {
                var chipWidth = size.x + 16;
                if (used > 0 && used + gap + chipWidth > mReadingWidth) { rows++; used = 0; }
                used += (used > 0 ? gap : 0) + chipWidth;
            }
            var total = rows * chipHeight + (rows - 1) * gap;
            var area = GUILayoutUtility.GetRect(mReadingWidth, total, GUILayout.Width(mReadingWidth));
            var x = area.x;
            var y = area.y;
            used = 0;
            for (var index = 0; index < labels.Count; index++)
            {
                var chipWidth = sizes[index].x + 16;
                if (used > 0 && used + gap + chipWidth > mReadingWidth) { y += chipHeight + gap; x = area.x; used = 0; }
                var rect = new Rect(x, y, chipWidth, chipHeight);
                var current = (labels[index].Key ?? "") == (mApiCategory ?? "");
                if (current) EditorGUI.DrawRect(rect, mPalette.ChipSelectedBg);
                else if (rect.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(rect, mPalette.ChipHoverBg);
                else EditorGUI.DrawRect(rect, mPalette.ChipBg);
                HelpDraw.Outline(rect, mPalette.ChipBorder);
                if (GUI.Button(rect, new GUIContent(labels[index].Value, "只显示该分类的成员"), current ? mChipSelected : mChip) && !current)
                {
                    mApiCategory = labels[index].Key ?? "";
                    Repaint(); GUIUtility.ExitGUI();
                }
                x += chipWidth + gap;
                used += (used > 0 ? gap : 0) + chipWidth;
            }
            GUILayout.Space(10);
        }

        /// <summary>成员信息块：圆角弱轮廓 + 左侧强调线；顶部摘要可点击，展开详情仍在同一块内。</summary>
        private void DrawMemberRow(HelpApiType type, HelpApiMember entry, float indent)
        {
            var key = type.Id + "\n" + entry.Signature;
            var expanded = mDetailExpanded.Contains(key);
            var blockWidth = Mathf.Max(120f, mReadingWidth - indent);
            var style = expanded ? mMemberBoxExpanded : mHoverBlockKey == key ? mMemberBoxHover : mMemberBox;
            var inner = Mathf.Max(60f, blockWidth - style.padding.horizontal);
            var display = DisplayName(type, entry);
            var brief = DescribeBrief(entry.Member);
            var badges = MemberBadges(type, entry);
            var nameHeight = Mathf.Max(18f, mMemberName.lineHeight);
            var summaryHeight = Mathf.Max(16f, mMemberSummary.lineHeight);
            GUILayout.BeginVertical(style, GUILayout.Width(blockWidth));
            var header = GUILayoutUtility.GetRect(inner, nameHeight + summaryHeight + 2, GUILayout.Width(inner));
            if (mRevealMemberKey == key && Event.current.type == EventType.Repaint)
            {
                // 从搜索结果跳转：把目标成员滚到阅读区顶部附近，只做一次。
                mReaderScroll = new Vector2(mReaderScroll.x, Mathf.Max(0f, header.y - 10f));
                mRevealMemberKey = null;
                Repaint();
            }
            if (GUI.Button(header, new GUIContent("", entry.Signature), GUIStyle.none))
            {
                // 多个成员详情可同时展开；再次点击同一成员只收起它自己。
                if (expanded) mDetailExpanded.Remove(key);
                else mDetailExpanded.Add(key);
                Repaint(); GUIUtility.ExitGUI();
            }
            var badgesWidth = DrawBadges(header, header.y, badges);
            var lineWidth = Mathf.Max(40f, header.width - badgesWidth);
            var nameWidth = Mathf.Min(mMemberName.CalcSize(new GUIContent(display)).x, lineWidth);
            GUI.Label(new Rect(header.x, header.y, nameWidth, nameHeight), new GUIContent(display, entry.Signature), mMemberName);
            if (!string.IsNullOrEmpty(brief) && lineWidth > nameWidth + 10)
                GUI.Label(new Rect(header.x + nameWidth + 6, header.y, lineWidth - nameWidth - 6, nameHeight),
                    new GUIContent(brief, entry.Signature), mMemberBrief);
            var summary = string.IsNullOrWhiteSpace(entry.Help.Summary) ? "未提供摘要说明。" : entry.Help.Summary;
            GUI.Label(new Rect(header.x, header.y + nameHeight, inner, summaryHeight), new GUIContent(summary, summary), mMemberSummary);
            if (expanded)
            {
                // 摘要与详情之间：略强于普通边框的低对比分隔线，上下留出间距。
                GUILayout.Space(6);
                var divider = GUILayoutUtility.GetRect(inner, 1, GUILayout.Width(inner));
                EditorGUI.DrawRect(new Rect(divider.x + 2, divider.y, Mathf.Max(0, divider.width - 4), 1), mPalette.MemberDivider);
                // 详情区在块内再水平内缩 4px，与外层边框和摘要文字都拉开距离。
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                GUILayout.Space(4);
                GUILayout.BeginVertical(mMemberDetailBox, GUILayout.Width(inner - 8));
                DrawMemberDetail(type, entry, Mathf.Max(60f, inner - 8 - mMemberDetailBox.padding.horizontal));
                GUILayout.EndVertical();
                GUILayout.Space(4);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var block = GUILayoutUtility.GetLastRect();
                var hovered = block.Contains(Event.current.mousePosition);
                if (hovered && mHoverBlockKey != key) { mHoverBlockKey = key; Repaint(); }
                else if (!hovered && mHoverBlockKey == key) { mHoverBlockKey = null; Repaint(); }
            }
            GUILayout.Space(6);
        }

        /// <summary>同名重载归组信息块：显示数量，展开后按块列出具体重载。</summary>
        private void DrawOverloadGroup(HelpApiType type, string name, HelpApiMember[] items)
        {
            var key = type.Id + "\n#" + name;
            var open = mOverloadKey == key;
            var style = open ? mMemberBoxExpanded : mHoverBlockKey == key ? mMemberBoxHover : mMemberBox;
            var inner = Mathf.Max(60f, mReadingWidth - style.padding.horizontal);
            var display = name == ".ctor" ? type.Name : name;
            GUILayout.BeginVertical(style, GUILayout.Width(mReadingWidth));
            var row = GUILayoutUtility.GetRect(inner, Mathf.Max(26f, mDetailFoldout.lineHeight + 4), GUILayout.Width(inner));
            var label = new GUIContent(display + "  ·  " + items.Length + " 个重载", "展开选择具体的重载");
            var next = EditorGUI.Foldout(row, open, label, true, mDetailFoldout);
            if (next != open)
            {
                mOverloadKey = next ? key : null;
                Repaint(); GUIUtility.ExitGUI();
            }
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var block = GUILayoutUtility.GetLastRect();
                var hovered = block.Contains(Event.current.mousePosition);
                if (hovered && mHoverBlockKey != key) { mHoverBlockKey = key; Repaint(); }
                else if (!hovered && mHoverBlockKey == key) { mHoverBlockKey = null; Repaint(); }
            }
            GUILayout.Space(6);
            if (!open) return;
            foreach (var item in items) DrawMemberRow(type, item, 16f);
        }

        private void DrawMemberDetail(HelpApiType type, HelpApiMember entry, float inner)
        {
            var help = entry.Help;
            var head = GUILayoutUtility.GetRect(inner, Mathf.Max(20f, mSmall.lineHeight), GUILayout.Width(inner));
            GUI.Label(new Rect(head.x, head.y, Mathf.Max(0, head.width - 60), head.height), "C# 签名", mSmall);
            var copyKey = type.Id + "\n" + entry.Signature + ":copy";
            var copied = mCopiedKey == copyKey && EditorApplication.timeSinceStartup < mCopyUntil;
            if (GUI.Button(new Rect(head.xMax - 52, head.y, 52, Mathf.Max(20f, head.height)), copied ? "已复制" : "复制", mButton))
            {
                EditorGUIUtility.systemCopyBuffer = entry.Signature;
                mCopiedKey = copyKey; mCopyUntil = EditorApplication.timeSinceStartup + 1.6;
                ShowNotification(new GUIContent("已复制签名")); Repaint();
            }
            // 完整签名放进轻量 code 区域，和下面的 XML 正文样式区分开。
            var codeInner = Mathf.Max(40f, inner - 18);
            var signatureHeight = Mathf.Max(mCodeWrap.lineHeight, mCodeWrap.CalcHeight(new GUIContent(entry.Signature), codeInner));
            var codeArea = GUILayoutUtility.GetRect(inner, signatureHeight + 18, GUILayout.Width(inner));
            EditorGUI.DrawRect(codeArea, mPalette.CodeBg);
            HelpDraw.Outline(codeArea, mPalette.CodeBorder);
            DrawSelectable(new Rect(codeArea.x + 9, codeArea.y + 9, codeInner, signatureHeight), entry.Signature, mCodeWrap);
            GUILayout.Space(4);
            // 摘要行已经在成员块顶部显示同一段 XML summary，这里不再重复渲染。
            var hasContent = false;
            if (help.TypeParameters.Length != 0)
            {
                DrawTextAt("类型参数", mDetailHeading, inner, false);
                foreach (var parameter in help.TypeParameters) DrawParameterRow(inner, parameter.Name, "", parameter.Description);
                hasContent = true;
            }
            if (help.Parameters.Length != 0)
            {
                DrawTextAt("参数", mDetailHeading, inner, false);
                foreach (var parameter in help.Parameters)
                    DrawParameterRow(inner, parameter.Name, ParameterType(entry.Member, parameter.Name), parameter.Description);
                hasContent = true;
            }
            if (!string.IsNullOrWhiteSpace(help.Returns))
            {
                DrawTextAt(entry.Member is PropertyInfo ? "属性值" : "返回值", mDetailHeading, inner, false);
                DrawTextAt(help.Returns, mBody, inner);
                hasContent = true;
            }
            if (!string.IsNullOrWhiteSpace(help.Remarks))
            {
                DrawTextAt("备注与注意事项", mDetailHeading, inner, false);
                DrawTextAt(help.Remarks, mBody, inner);
                hasContent = true;
            }
            if (!hasContent) DrawTextAt("未提供参数、返回值或备注说明。", mSmall, inner, false);
        }

        /// <summary>名称 / 类型 / 说明的对齐布局；窄窗口改为上下排列。</summary>
        private void DrawParameterRow(float width, string name, string type, string description)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description)) return;
            const float gap = 10f;
            if (width < 340f)
            {
                DrawTextAt(string.IsNullOrWhiteSpace(type) ? name : name + "  " + type, mCode, width);
                if (!string.IsNullOrWhiteSpace(description)) DrawTextAt(description, mBody, Mathf.Max(40, width - 14));
                GUILayout.Space(3);
                return;
            }
            var nameWidth = Mathf.Clamp(width * 0.22f, 60f, 150f);
            var typeWidth = Mathf.Clamp(width * 0.30f, 80f, 220f);
            var descriptionWidth = Mathf.Max(40f, width - nameWidth - typeWidth - gap * 2);
            var nameHeight = Mathf.Max(mCode.lineHeight, mCode.CalcHeight(new GUIContent(name ?? ""), nameWidth));
            var typeHeight = Mathf.Max(mCode.lineHeight, mCode.CalcHeight(new GUIContent(type ?? ""), typeWidth));
            var descriptionHeight = Mathf.Max(mBody.lineHeight, mBody.CalcHeight(new GUIContent(description ?? ""), descriptionWidth));
            var height = Mathf.Max(nameHeight, Mathf.Max(typeHeight, descriptionHeight));
            var row = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width));
            if (!string.IsNullOrWhiteSpace(name)) DrawSelectable(new Rect(row.x, row.y, nameWidth, height), name, mCode);
            if (!string.IsNullOrWhiteSpace(type)) DrawSelectable(new Rect(row.x + nameWidth + gap, row.y, typeWidth, height), type, mCode);
            if (!string.IsNullOrWhiteSpace(description)) DrawSelectable(new Rect(row.x + nameWidth + typeWidth + gap * 2, row.y, descriptionWidth, height), description, mBody);
            GUILayout.Space(2);
        }

        private static string DisplayName(HelpApiType type, HelpApiMember entry) => entry.Name == ".ctor" ? type.Name : entry.Name;

        private string DescribeBrief(MemberInfo member)
        {
            if (member is ConstructorInfo constructor) return "(" + BriefParameters(constructor) + ")";
            if (member is MethodInfo method) return (method.IsGenericMethod
                ? "<" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + ">" : "") +
                "(" + BriefParameters(method) + ")";
            if (member is PropertyInfo property)
            {
                var propertyType = HelpApiFormatter.FormatType(property.PropertyType, mShortNamespaces);
                var index = property.GetIndexParameters();
                return index.Length == 0 ? ": " + propertyType : "[" + string.Join(", ", index.Select(parameter =>
                    HelpApiFormatter.FormatType(parameter.ParameterType, mShortNamespaces) + " " + parameter.Name)) + "] : " + propertyType;
            }
            if (member is FieldInfo field) return field.DeclaringType != null && field.DeclaringType.IsEnum
                ? "= " + field.GetRawConstantValue()
                : ": " + HelpApiFormatter.FormatType(field.FieldType, mShortNamespaces);
            if (member is EventInfo signal) return ": " + HelpApiFormatter.FormatType(signal.EventHandlerType, mShortNamespaces);
            return "";
        }

        private string BriefParameters(MethodBase method)
        {
            var extension = method.IsDefined(typeof(ExtensionAttribute), false);
            return string.Join(", ", method.GetParameters().Select((parameter, index) =>
                (extension && index == 0 ? "this " : "") +
                (parameter.IsDefined(typeof(ParamArrayAttribute), false) ? "params " :
                    parameter.ParameterType.IsByRef ? parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref " : "") +
                HelpApiFormatter.FormatType(parameter.ParameterType, mShortNamespaces) + " " + parameter.Name));
        }

        private string ParameterType(MemberInfo member, string name)
        {
            var parameters = member is ConstructorInfo constructor ? constructor.GetParameters() :
                member is MethodInfo method ? method.GetParameters() :
                member is PropertyInfo property ? property.GetIndexParameters() : Array.Empty<ParameterInfo>();
            var parameter = parameters.FirstOrDefault(item => item.Name == name);
            if (parameter == null) return "";
            var prefix = parameter.ParameterType.IsByRef ? parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref " : "";
            return prefix + HelpApiFormatter.FormatType(parameter.ParameterType, mShortNamespaces);
        }

        private string[] MemberBadges(HelpApiType type, HelpApiMember entry)
        {
            var member = entry.Member;
            var badges = new List<string>();
            var isEnumValue = member is FieldInfo enumField && enumField.DeclaringType.IsEnum;
            var isStatic = member is MethodBase callable ? callable.IsStatic :
                member is FieldInfo field ? field.IsStatic :
                member is PropertyInfo property ? (property.GetGetMethod(true) ?? property.GetSetMethod(true))?.IsStatic == true :
                member is EventInfo signal && signal.GetAddMethod(true) is MethodInfo add && add.IsStatic;
            if (isStatic && !isEnumValue) badges.Add("static");
            var accessibility = member is MethodBase method ? AccessBadge(method.IsPublic, method.IsFamily, method.IsFamilyOrAssembly) :
                member is FieldInfo declared ? AccessBadge(declared.IsPublic, declared.IsFamily, declared.IsFamilyOrAssembly) :
                member is PropertyInfo declaredProperty ? PropertyAccess(declaredProperty) :
                member is EventInfo declaredEvent && declaredEvent.GetAddMethod(true) is MethodInfo handler
                    ? AccessBadge(handler.IsPublic, handler.IsFamily, handler.IsFamilyOrAssembly) : "";
            if (accessibility.Length != 0) badges.Add(accessibility);
            if (!isEnumValue && member is FieldInfo constant && constant.IsLiteral) badges.Add("const");
            else if (!isEnumValue && member is FieldInfo readonlyField && readonlyField.IsInitOnly) badges.Add("readonly");
            if (type.MemberMatches(entry, mSearch)) badges.Add("匹配");
            return badges.ToArray();
        }

        private static string AccessBadge(bool isPublic, bool isFamily, bool isFamilyOrAssembly) => isPublic ? "" :
            isFamilyOrAssembly ? "protected internal" : isFamily ? "protected" : "";

        private static string PropertyAccess(PropertyInfo property)
        {
            var accessors = property.GetAccessors(true);
            var primary = accessors.FirstOrDefault(method => method.IsPublic) ?? accessors.FirstOrDefault();
            return primary == null ? "" : AccessBadge(primary.IsPublic, primary.IsFamily, primary.IsFamilyOrAssembly);
        }

        private float DrawBadges(Rect row, float y, string[] badges)
        {
            if (badges.Length == 0) return 0;
            var height = Mathf.Max(16f, mBadge.lineHeight + 3);
            var total = 0f;
            var x = row.xMax;
            foreach (var badge in badges)
            {
                var width = mBadge.CalcSize(new GUIContent(badge)).x + 12;
                x -= width;
                var rect = new Rect(x, y, width, height);
                EditorGUI.DrawRect(rect, mPalette.BadgeBg);
                GUI.Label(rect, new GUIContent(badge, badge), mBadge);
                x -= 5;
                total += width + 5;
            }
            return total;
        }

        private bool DrawSmallLink(string label)
        {
            var size = mSmallLink.CalcSize(new GUIContent(label));
            var rect = GUILayoutUtility.GetRect(mReadingWidth, Mathf.Max(20f, mSmallLink.lineHeight), GUILayout.Width(mReadingWidth));
            return GUI.Button(new Rect(rect.x, rect.y, Mathf.Min(size.x + 4, rect.width), rect.height),
                new GUIContent(label, label), mSmallLink);
        }

        private void DrawText(string text, GUIStyle style, bool highlight = true) => DrawTextAt(text, style, mReadingWidth, highlight);

        /// <summary>按给定宽度绘制可选择文本；正文自动换行且换行后对齐内容起点。</summary>
        private void DrawTextAt(string text, GUIStyle style, float width, bool highlight = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var height = Mathf.Max(style.lineHeight, style.CalcHeight(new GUIContent(text), width));
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width));
            DrawSelectable(rect, text, style, highlight);
        }

        private void DrawHighlight(Rect rect, string text, GUIStyle style)
        {
            mHighlights.SetQuery(mSearch);
            mHighlights.Draw(rect, text, style, mStylesDark);
        }

        private void DrawSelectable(Rect rect, string text, GUIStyle style, bool highlight = true)
        {
            if (highlight) DrawHighlight(EditorGUI.IndentedRect(rect), text, style);
            EditorGUI.SelectableLabel(rect, text, style);
        }

        private void DrawNotice(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var padding = mNotice.padding;
            var width = Mathf.Max(1, mReadingWidth - padding.horizontal - 36);
            var height = Mathf.Max(24, mBody.CalcHeight(new GUIContent(text), width));
            var rect = GUILayoutUtility.GetRect(mReadingWidth, height + padding.vertical, GUILayout.Width(mReadingWidth));
            EditorGUI.DrawRect(rect, mPalette.NoticeBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), mPalette.NoticeAccent);
            HelpDraw.Outline(new Rect(rect.x + 1, rect.y, rect.width - 1, rect.height), mPalette.NoticeBorder);
            GUI.Label(new Rect(rect.x + padding.left, rect.y + padding.top, 24, 24),
                EditorGUIUtility.IconContent("console.infoicon"), GUIStyle.none);
            DrawSelectable(new Rect(rect.x + padding.left + 34, rect.y + padding.top, width, height), text, mBody);
            GUILayout.Space(10);
        }

        private bool DrawLink(string label, string tooltip, bool highlight = true)
        {
            var height = Mathf.Max(22, mLink.CalcHeight(new GUIContent(label), mReadingWidth));
            var rect = GUILayoutUtility.GetRect(mReadingWidth, height, GUILayout.Width(mReadingWidth));
            if (highlight) DrawHighlight(rect, label, mLink);
            return GUI.Button(rect, new GUIContent(label, tooltip), mLink);
        }

        private void DrawCode(string key, string text)
        {
            var label = new GUIContent(text);
            var codeWidth = mCode.CalcSize(label).x + 16;
            var textHeight = Mathf.Max(mCode.lineHeight, mCode.CalcHeight(label, codeWidth));
            var codeHeight = textHeight + 20;
            // 高度按文本估算并限制在 96~384；横向需要滚动时额外留出滚动条的空间。
            var height = Mathf.Clamp(codeHeight + 46 + (codeWidth > mReadingWidth - 24 ? 16 : 0), 96, 384);
            var block = GUILayoutUtility.GetRect(mReadingWidth, height, GUILayout.Width(mReadingWidth));
            EditorGUI.DrawRect(block, mPalette.CodeBg);
            HelpDraw.Outline(block, mPalette.CodeBorder);
            var header = new Rect(block.x, block.y, block.width, 30);
            EditorGUI.DrawRect(header, mPalette.CodeHeaderBg);
            HelpDraw.Rule(new Rect(header.x + 1, header.yMax - 1, header.width - 2, 1), mPalette.CodeBorder);
            var tag = new GUIContent("C# · 只读");
            var tagSize = mTag.CalcSize(tag);
            var tagRect = new Rect(block.x + 10, block.y + 5, tagSize.x + 10, 20);
            EditorGUI.DrawRect(tagRect, mPalette.CodeTagBg);
            GUI.Label(tagRect, tag, mTag);
            var copied = mCopiedKey == key && EditorApplication.timeSinceStartup < mCopyUntil;
            if (GUI.Button(new Rect(block.xMax - 76, block.y + 3, 66, 24), copied ? "已复制" : "复制", mButton))
            {
                EditorGUIUtility.systemCopyBuffer = text; mCopiedKey = key; mCopyUntil = EditorApplication.timeSinceStartup + 1.6;
                ShowNotification(new GUIContent("已复制到剪贴板")); Repaint();
            }
            var view = new Rect(block.x + 10, header.yMax + 6, block.width - 20, Mathf.Max(1, block.height - header.height - 12));
            mCodeScroll.TryGetValue(key, out var scroll);
            var content = new Rect(0, 0, Mathf.Max(view.width - 16, codeWidth), Mathf.Max(view.height - 16, codeHeight));
            scroll = GUI.BeginScrollView(view, scroll, content);
            DrawSelectable(new Rect(10, 8, content.width - 20, textHeight), text, mCode);
            GUI.EndScrollView(); mCodeScroll[key] = scroll;
            GUILayout.Space(10);
        }

        private void Navigate(int tab, string id, bool clearSearch)
        {
            // 跳转前保存完整阅读状态，返回时才能还原页签、搜索、折叠与滚动位置。
            mHistory.Push(new Navigation { Tab = mTab, Guide = mGuideId, Api = mApiId, About = mAboutId,
                Search = mSearch, Nav = mNavScroll, Reader = mReaderScroll,
                Collapsed = mCollapsedGroups.ToArray(), SearchCollapsed = mSearchCollapsed.ToArray() });
            if (mTab != tab) mNavScroll = Vector2.zero;
            mTab = tab;
            if (tab == 0) mGuideId = id; else if (tab == 1) mApiId = id; else mAboutId = id;
            if (clearSearch) { mSearch = ""; mSearchCollapsed.Clear(); }
            mReaderScroll = Vector2.zero; mFilterKey = null; mRevealInNav = true;
            mHighlights.Clear();
            Repaint(); GUIUtility.ExitGUI();
        }

        private void GoBack()
        {
            var previous = mHistory.Pop();
            mTab = previous.Tab; mGuideId = previous.Guide; mApiId = previous.Api; mAboutId = previous.About;
            mSearch = previous.Search; mNavScroll = previous.Nav; mReaderScroll = previous.Reader; mFilterKey = null;
            mCollapsedGroups = previous.Collapsed.ToList();
            mSearchCollapsed.Clear(); mSearchCollapsed.UnionWith(previous.SearchCollapsed);
            // 滚动位置随历史一起还原，不再自动滚动到选中项。
            mRevealInNav = false; mRevealMemberKey = null;
            mHighlights.Clear();
            Repaint(); GUIUtility.ExitGUI();
        }

        private void RevealSelectedGroup()
        {
            var group = mTab == 0 ? mVisibleTopics.FirstOrDefault(topic => topic.Id == mGuideId)?.Group :
                mTab == 1 ? mVisibleTypes.FirstOrDefault(type => type.Id == mApiId)?.Group : null;
            if (group == null) return;
            var key = (mTab == 0 ? "guide:" : "api:") + group;
            if (string.IsNullOrWhiteSpace(mSearch)) mCollapsedGroups.Remove(key);
            else mSearchCollapsed.Remove(key);
        }
    }
}

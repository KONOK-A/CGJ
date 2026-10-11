namespace YLFramework.Core.Tests
{
    public sealed class YLCoreSmokePanel : YLFramework.Core.Panel
    {
        public static System.Action<YLCoreSmokePanel> LoadingProbe;
        public static System.Action<YLCoreSmokePanel, LoadViewArgs> ArgsProbe;
        public static System.Action<YLCoreSmokePanel, int> SortingProbe;
        public static System.Action<YLCoreSmokePanel> VisibleProbe;
        public static System.Action<YLCoreSmokePanel> InvisibleProbe;
        public static System.Action<YLCoreSmokePanel> EnabledProbe;
        public static System.Action<YLCoreSmokePanel, LoadViewArgs> UpdateProbe;
        public static PanelType ProbePanelType = PanelType.Window;
        public const string ProbeEvent = "YLCoreSmoke.PanelEvent";
        public static bool UseSharedHandler;
        public static int SharedEventCount;
        public bool KeepInFront;
        public override bool AlwaysInFront => KeepInFront;
        public bool Modal;
        public override bool IsModal => Modal;
        public PanelType? TypeOverride;
        public override PanelType PanelType => TypeOverride ?? ProbePanelType;
        public int LoadedCount { get; private set; }
        public int ArgsCount { get; private set; }
        public int RemovedCount { get; private set; }
        public int ClosedCount { get; private set; }
        public int Token { get; private set; }
        public int EventCount { get; private set; }
        public System.Action RemoveAction;

        protected override void Awake()
        {
            base.Awake();
            RegisterEvent(ProbeEvent, UseSharedHandler ? (System.Action)SharedHandler : HandleEvent);
        }

        private static void SharedHandler() => SharedEventCount++;
        private void HandleEvent() => EventCount++;

        public override void OnLoaded(params object[] param)
        {
            LoadedCount++;
            LoadingProbe?.Invoke(this);
        }

        public override void OnLoaded(LoadViewArgs args)
        {
            ArgsCount++;
            Token = args.GetInt("token");
            ArgsProbe?.Invoke(this, args);
        }

        protected override void OnSortingOrderChange(int sortingOrder) => SortingProbe?.Invoke(this, sortingOrder);
        public override void Visible() => VisibleProbe?.Invoke(this);
        public override void Invisible() => InvisibleProbe?.Invoke(this);
        protected override void OnClosed() => ClosedCount++;
        protected override void OnViewEnabled() => EnabledProbe?.Invoke(this);
        public override void OnWindowUpdated(LoadViewArgs args) => UpdateProbe?.Invoke(this, args);

        public override void OnRemove()
        {
            RemovedCount++;
            RemoveAction?.Invoke();
        }

        public static void ResetProbes()
        {
            LoadingProbe = null;
            ArgsProbe = null;
            SortingProbe = null;
            VisibleProbe = null;
            InvisibleProbe = null;
            EnabledProbe = null;
            UpdateProbe = null;
            ProbePanelType = YLFramework.Core.PanelType.Window;
            UseSharedHandler = false;
            SharedEventCount = 0;
        }
    }
}

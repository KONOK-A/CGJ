using UnityEngine;

namespace YLFramework.Core.Tests
{
    public sealed class YLCoreSmokeView : YLFramework.Core.View
    {
        public const string ProbeEvent = "YLCoreSmoke.View.Probe";
        public int LoadedCount { get; private set; }
        public int ArgsCount { get; private set; }
        public int RemovedCount { get; private set; }
        public int EnabledCount { get; private set; }
        public int DisabledCount { get; private set; }
        public int EventCount { get; private set; }
        public int Token { get; private set; }
        public object Parameter { get; private set; }
        public Model BoundModel;
        public System.Action AfterClose;
        public override Model Model => BoundModel;

        public override void Close()
        {
            base.Close();
            AfterClose?.Invoke();
        }

        private void Awake() => RegisterEvent(ProbeEvent, (XFEvent)(() => EventCount++));

        protected override void OnEnable()
        {
            base.OnEnable();
            EnabledCount++;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DisabledCount++;
        }

        public override void OnLoaded(params object[] param)
        {
            LoadedCount++;
            Parameter = param.Length > 0 ? param[0] : null;
        }

        public override void OnLoaded(LoadViewArgs args)
        {
            ArgsCount++;
            Token = args.GetInt("token");
        }

        public override void OnRemove() => RemovedCount++;
    }
}

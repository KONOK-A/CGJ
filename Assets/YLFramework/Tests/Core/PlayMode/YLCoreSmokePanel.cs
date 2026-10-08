namespace YLFramework.Core.Tests
{
    public sealed class YLCoreSmokePanel : YLFramework.Core.Panel
    {
        public int LoadedCount { get; private set; }
        public int ArgsCount { get; private set; }
        public int RemovedCount { get; private set; }
        public int Token { get; private set; }

        public override void OnLoaded(params object[] param) => LoadedCount++;

        public override void OnLoaded(LoadViewArgs args)
        {
            ArgsCount++;
            Token = args.GetInt("token");
        }

        public override void OnRemove() => RemovedCount++;
    }
}

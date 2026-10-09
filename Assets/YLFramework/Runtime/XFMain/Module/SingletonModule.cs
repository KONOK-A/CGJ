namespace YLFramework.Core
{
    public class SingletonModule<T> : Module where T : Module
    {

        /// <summary>
        /// 查询实际注册实例（包括Starting）；未注册或关闭后返回null，不创建对象。
        /// </summary>
        public static T Instance => ModuleManager.GetModule<T>();

        public override void OnStart()
        {
        }
    }

}

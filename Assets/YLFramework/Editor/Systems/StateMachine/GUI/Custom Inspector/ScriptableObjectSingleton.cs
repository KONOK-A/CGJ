using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.StateMachine
{
    public class ScriptableObjectSingleton<T> : ScriptableObject where T : ScriptableObjectSingleton<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<T>();
                }
                return _instance;
            }
        }

        private void OnDisable()
        {
            _instance = null;
        }

    }

}

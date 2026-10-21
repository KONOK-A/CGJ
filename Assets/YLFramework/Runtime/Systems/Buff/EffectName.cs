using YLFramework.Foundation;
using System;


namespace YLFramework.BuffSystem
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class EffectName : Attribute
    {

        private string name;

        public string Name => name;

        public EffectName(string name)
        {
            this.name = name;
        }
    }

}

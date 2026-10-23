using System;
using YLFramework.Foundation;


namespace YLFramework.BuffSystem
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class EffectName : Attribute
    {

        private string _name;

        public string Name => _name;

        public EffectName(string name)
        {
            this._name = name;
        }
    }

}

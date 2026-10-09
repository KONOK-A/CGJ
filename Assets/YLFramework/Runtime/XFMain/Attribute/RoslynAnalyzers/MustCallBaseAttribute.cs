using System;
 
namespace YLFramework.Core.RoslynAnalyzers
{

    /// <summary>
    /// 标记某个虚方法,当重写虚方法时必须调用父类的方法
    /// </summary>
    public class MustCallBaseAttribute : Attribute
    {
        
        public MustCallBaseAttribute() { }

        public MustCallBaseAttribute(string message) { }

    }
    
}


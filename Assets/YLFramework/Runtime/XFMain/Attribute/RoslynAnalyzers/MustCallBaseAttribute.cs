using System;
 
namespace YLFramework.Core.RoslynAnalyzers
{

    /// <summary>
    /// 标记虚方法的 base 调用契约；重写链上的有效标记由 YL0001 检查并默认报告 Warning。
    /// 当前方法本体需直接调用同一被覆写槽；Lambda、局部函数、方法组及同名 overload 不满足契约。
    /// 仅证明调用存在，不证明所有控制流执行；抽象基类层级冲突需调整标记或覆写结构。
    /// </summary>
    public class MustCallBaseAttribute : Attribute
    {
        
        public MustCallBaseAttribute() { }

        public MustCallBaseAttribute(string message) { }

    }
    
}

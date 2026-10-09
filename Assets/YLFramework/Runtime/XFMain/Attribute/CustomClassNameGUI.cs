using System;

#if UNITY_EDITOR
namespace YLFramework.Core
{

/// <summary>
/// 用于编辑器的类名自定义显示
/// </summary>
public class CustomClassNameGUI : Attribute
{
    private string name;
    public CustomClassNameGUI(string name) 
    {
        this.name = name;
    } 

    public string GetName() { 
        return name;
    } 
}

}
#endif

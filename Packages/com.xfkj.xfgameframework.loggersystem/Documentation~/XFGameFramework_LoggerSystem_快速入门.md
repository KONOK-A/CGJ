## XFGameFramework.LoggerSystem快速入门


### 功能介绍

[XFGameFramework游戏框架](https://gitee.com/xianfengkeji/XFGameFramework.git)日志系统拓展包,通过该拓展包能够控制日志开关,监听日志事件!

### 日志输出

日志输出不需要做任何调整,直接使用原生Debug输出即可,代码如下:

> ```none
>
>public class Test : MonoBehaviour
>{
>  
>
>    // Start is called before the first frame update
>    void Start()
>    {  
>        Debug.Log("Log");  
>        Debug.LogWarning("Warning"); 
>        Debug.LogError("Error");  
>    }
>
>
>}
> ```


### 日志开关

通过Debug输出日志后,如果我们想关掉某个类型的日志,可以通过 LoggerManager.FilterLogType 来实现,代码如下:


> ```none
>
>public class Test : MonoBehaviour
>{
>
>    // Start is called before the first frame update
>    void Start()
>    {
>        // 默认所有类型的日志都是开启状态
>        LoggerManager.FilterLogType[LogType.Log] = false; // 关闭Log类型日志(开启设置为true即可!)
>        LoggerManager.FilterLogType[LogType.Warning] = false; // 关闭Warning类型日志(开启设置为true即可!)
>        LoggerManager.FilterLogType[LogType.Error] = false; // 关闭Error类型日志(开启设置为true即可!)
>    }
>
>
>}
> ```


### 写入文件开关

默认所有的日志都会写入到文件中,如果不想写入文件,可通过下面的代码来关闭:

> ```none
>
>public class Test : MonoBehaviour
>{
>
>    // Start is called before the first frame update
>    void Start()
>    {
>        // 默认所有类型的日志都是开启状态
>        LoggerManager.FilterLogFileType[LogType.Log] = false; // 禁止Log类型日志写入文件(开启设置为true即可!)
>        LoggerManager.FilterLogFileType[LogType.Warning] = false; // 禁止Warning类型日志写入文件(开启设置为true即可!)
>        LoggerManager.FilterLogFileType[LogType.Error] = false; // 禁止Error类型日志写入文件(开启设置为true即可!)
>    }
>
>
>}
> ```


### 获取日志文件

可以通过以下代码来获取日志文件:

> ```none
>
>public class Test : MonoBehaviour
>{
>
>    // Start is called before the first frame update
>    void Start()
>    {
>        Debug.Log(LoggerManager.LogFileDirectory);  // 日志文件所在的目录
>        Debug.Log(LoggerManager.LogFilePath);       // 日志文件存放路径
>    }
>
>
>}
> ```

### 日志事件监听

在输出日志时,会触发事件,如果大家想监听日志事件,可以通过以下代码实现:

> ```none
>
>public class Test : MonoBehaviour
>{
>
>    void Start()
>    {
>        LoggerManager.OnLogEvent += OnLog; // 注册日志事件
>    }
>
>
>    private void OnLog(LogType logType,string message) 
>    {
>        // 监听到日志输出 TODO
>    }
>
>
>}
> ```

**注:上面的日志事件,不仅会在输出日志时触发，在触发异常时也会触发, 如果我们想监听有没有异常，代码有没有报错,也可以通过监听日志事件来实现!**

还有一点需要注意的是, 只要调用Debug输出日志,日志事件就会触发, 即使通过 LoggerManager.FilterLogType 关闭所有日志输出, 该事件也会触发! 如果不希望日志事件触发，不监听 或者 置为空 即可!(默认为空,默认不会执行触发日志事件逻辑!)




### 如有疑问 或 遗漏请及时联系群主,qq交流群:946441033
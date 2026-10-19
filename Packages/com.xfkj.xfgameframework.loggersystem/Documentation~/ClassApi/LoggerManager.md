# LoggerManager
class in XFGameFramework.LoggerSystem

### 说明:

日志系统业务逻辑


### 属性

| 名称      | 说明 |
| ----------- | ----------- |
| [FilterLogType](../XFGameFramework_LoggerSystem_快速入门.md#日志开关) | 需要输出的日志类型(默认所有的日志都会输出) | 
| [FilterLogFileType](../XFGameFramework_LoggerSystem_快速入门.md#写入文件开关) | 需要输出到文件的日志类型(默认所有的日志都会输出) | 
| [OnLogEvent](../XFGameFramework_LoggerSystem_快速入门.md#日志事件监听) | 日志输出事件(抛出异常时也会触发),即使把所有类型的日志输出都关闭,只要监听该事件,当输出日志时就会触发,如果不希望事件触发,置为空即可!(默认为空)| 
| [LogFileDirectory](../XFGameFramework_LoggerSystem_快速入门.md#获取日志文件) | 日志文件所在的目录 | 
| [LogFilePath](../XFGameFramework_LoggerSystem_快速入门.md#获取日志文件) | 日志文件路径 | 
## 项目内同版本修复

- 来源版本 1.0.1，Git revision `0ebbebeb4b961d6fb67670d22cad819856969c3a`；保留上游 MIT 许可证与原程序集/资源身份。
- 修复真实 Unity 禁用 Domain Reload 时的日志接管生命周期：停用/销毁归还自身接管的 handler，重新启用仅重装自身 handler，销毁后清除实例与已释放的静态 writer。不关闭日志、修改过滤规则或绕过日志事件。

## version 1.0.1 更新内容

1. 优化日志系统初始化

## version 1.0.0 更新内容

1. 完成日志系统功能开发

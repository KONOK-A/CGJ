# Behavior 组件与主 Unity 验收入口

本组为独立 YL 能力，原游戏继续使用 XF/XFFSM/Overlap，不重绑原 Scene/Prefab。
以下 Unity 门尚未执行，不能把夹具编译或离线 NUnit 结果当作引擎验收。

## Overlap

- Namespace：`YLFramework.Overlap`。保留 Point/Circle/Box/Capsule/Area 2D、Box/Sphere 3D、`Results`、`OverlapHit<T>`、enter/exit、`Running`、检测频率和 scaled/unscaled 模式。
- 新建自有对象，添加对应形状，设置 `LayerMask`、形状参数和事件。查询继续排除 Trigger；`Results` 是本组件维护的可读快照，不应由调用方在回调内改写。`OverlapHit<T>.point` 保留原 `Vector2` 契约，包括 3D 查询。
- 手动 `CheckOverlap()` 可独立使用；同组件事件内递归查询被拒绝，其他组件可查询。禁用不补发 exit，立即清空结果，重新启用后的首次查询重新 enter，`Running` 的显式设置保留。
- 容量保持 100，不偷偷扩容或改变时序。Editor/Development 达到容量时每次启用周期最多提示一次“可能截断”，不声称一定丢失命中；Release 无此日志路径。
- Inspector 保留 Results、区域列表/Scene 节点、Box/Capsule 手柄，绘制移到 Editor，不把 Handles 留在 Runtime。
- 主 Unity：执行 `YLFramework.Tests.Extensions.Behavior.OverlapComponentTests`；对每个形状检查触发/实体、多 Collider、负缩放/旋转、enter/exit、容量100/101、回调中双实例/同实例查询及禁用/销毁。
- 另验 scaled/unscaled 在 `timeScale=0/0.5/1` 与检测频率 0.01/10/100 的时序；3D/2D 层矩阵刻意设为不同后 Reset，确认各用各矩阵并恢复设置。检查正常帧 GC，不以无分配源码推断 Profiler 结果。
- 创建自有 Prefab，绑定 enter/exit 持久回调，保存/重新导入/关闭重开，确认字段、事件与新 GUID 正确；旧8个 Prefab/hash保持不变。

## 离线工具

从本 worktree 根运行 `pwsh -NoLogo -NoProfile -NonInteractive -File Tools/Behavior/Invoke-BehaviorOffline.ps1 -Mode All -Evidence <唯一名称>`。
`Pure` 只执行真实托管逻辑/静态契约的 NUnit，不模拟 Unity；`Compile` 使用已安装 Unity SDK 的真实引用辅助编译 Runtime、Editor profile、EditorTools 及本组测试源码，不启动 Unity。

运行 `Tools/Behavior/Test-BehaviorBoundary.ps1 -Evidence <唯一名称>` 产生 `Temp/Behavior.Offline/<名称>/pending-meta.txt` 与逐文件 SHA/来源/保护区报告。来源和 SDK 引用留在临时证据，不提交二进制/缓存。所有新增脚本、目录和文本资产的 meta 由主 Unity 生成。

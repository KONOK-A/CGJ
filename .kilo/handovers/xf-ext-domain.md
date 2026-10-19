# C组领域融合检查点

唯一正式Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`。
本轮用户明确授权五项连续源码融合、必要修复、离线验证及中文本地候选提交；覆盖Plan历史未授权/分批等待状态，不覆盖共享区域、许可和验证边界。

## 基线与边界

- 实际工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-domain-1c975de3e2f7676b`。
- 分支：`fusion/xf-ext-domain`；开工HEAD及共同B0：`c88500aef6651f5b13ac933dd1f209ecb314e92d`，祖先检查退出0，工作区/索引干净。
- 只改C组Systems、对应Editor、Domain测试/夹具、专属工具和本说明。主Plan、Foundation/Core/Resource/Save、主体asmdef、Packages、ProjectSettings只读。
- 不调用Unity MCP、不启动Editor/batchmode、不Push/合并/Rebase/Reset/Clean/Stash，不写其他worktree。
- Shell实际为Windows PowerShell 5.1，脚本按5.1语法编写；项目Unity为2022.3.62f3c1。不存在本工作区`.editorconfig`或YL子树补充AGENTS。
- 不手造Unity行为stub或meta；新资产/目录meta统一待集成owner用Unity生成，未执行引擎门不计Passed。

## 进度

| 模块 | 状态 | 候选提交/证据 |
|---|---|---|
| Reddot | 源码候选完成，引擎待验 | `5c04f2b`；9项托管红→绿，Runtime辅助编译退出0/41个既有warning |
| Logger | 源码候选完成，引擎待验 | 6项Logger+9项Reddot托管通过；默认关闭，原embedded只读 |
| Skill | 待实施 | 保留释放/中断/冷却及普通作者流程 |
| Buff | 待实施 | 保留叠加/效果及普通作者流程 |
| Mission | 待实施 | 保留条件/进度/奖励及普通作者流程，不改Save |
| Excel接线 | 待核对B真实API | 开工时B交接文件不存在，不空等、不造接口 |
| 整组Review | 待五项候选完成 | 仅一次独立Review及直接修复回归 |

## 来源与许可交接

只读来源为主工程`Library/PackageCache`中Plan所列精确revision；Logger用本工作区embedded源码。Reddot/Logger为MIT，Copyright (c) 2025 弦风课堂；Skill/Buff/Mission为MIT，Copyright (c) 2024 弦风课堂。新文件保留适用声明，各模块附完整许可；中央LICENSE/Notices由集成owner汇总。不承接logo、安装器、Demo、旧Analyzer或未核实DLL。

## 实施裁定

- 沿用原Manager领域职责与入口，不引入统一Service/领域工厂平台。红点独立token只管本次来源，Reset先清状态再通知；旧token不能撤销新登记。
- 离线执行只覆盖无需Unity原生调用的真实生产代码，真实Unity引用用于辅助编译；UI/资源/Play/Player不能用离线结果替代。

## 引擎待验

全部模块的Unity编译、EditMode/PlayMode实际运行、作者工具保存重开/Undo、资源Lease和宿主生命周期、关闭Domain Reload连续两Play、无XF独立Mono/IL2CPP Low构建运行、正式导出/首导入及原游戏非干扰均未执行。后续按模块补齐具体用例和入口。

## Reddot使用与验证

- namespace为`YLFramework.ReddotSystem`。保留`AddPath`/`RemovePath`/`Reset`和路径通知，`ReddotPath`改为只读快照，防止绕过通知；新YL身份不提供XF命名空间兼容。
- 推荐`var registration = ReddotManager.RegisterPath("Mail/Gift");`，owner退出`registration.Dispose()`；普通Remove不夺取独立来源。Reset清空后通知且使所有旧token失效，按Ordinal整段匹配，规范空白/斜杠，null安全。
- `Reddot`挂在持续存活的UI节点，显式设置`Path`和`Indicator`，只切换该目标，拒绝本物体/祖先；停用与销毁精确退订，不清业务路径。不要再依赖“所有子节点即指示器”的旧隐式行为。
- `Tools/Domain/Verify-Domain.ps1 -Mode Runtime`使用真实Unity引用辅助编译，退出0、41个既有warning；`-Run reddot-red`实跑9失败（YL能力缺失）；`-Run reddot-green`实跑9通过、0失败/跳过，证据为各自`Temp/Domain.Offline/<Run>/results.xml`。
- `ReddotLifecycleTests.OnlyExplicitIndicatorChangesAndDisabledHostUnsubscribes`已准备，未运行。覆盖嵌套UI业务子物体不变、停用退订、重新启用刷新、自指示器拒绝；后续补实际UGUI/缓存View/场景与双Play消费者。
- 原缺陷由Plan及只读原源码确认，未运行原XF测试，不把YL缺能力红例冒称原XF引擎Bug复现。

## Logger使用与验证

- `YLFramework.Logging.LoggerManager.Enable(new LoggerOptions { WriteToFile = true })`为唯一Unity接管入口，只在有效Play主线程显式调用；默认只读`Current/IsEnabled`不创建宿主、不访问目录、不写盘、不抢handler。Enable默认也不写盘，必须显式`WriteToFile=true`。关闭调用`Disable()`，宿主disable/destroy/quit亦释放。
- 默认目录为`Application.persistentDataPath/YLFramework/Logs`，可以配置独立目录。过滤字典在创建会话时复制，null全开、空表全关。事件始终触发，回调线程为日志原线程，不得访问非线程安全Unity API或等待会记录日志的其他线程。
- 沿用原同步writer/立即Flush、五类过滤、格式化和事件职责；加UTC高精度+GUID与CreateNew防会话碰撞、串行锁、回调逐项异常隔离。嵌套日志明确丢弃并计数，格式化ToString也在递归门内；失败通过`LastError`及实际`LogFilePath`诊断，不递归Debug报错或静默冒称成功。
- 只归还仍由自己拥有的handler；后续handler接管后不覆盖。已关闭会话若仍被后续handler串联，只透明转发原handler，不重开文件/监听。没有新后台线程、Analyzer、整Runtime AlwaysLinkAssembly或Debug批量替换。
- `logger-red`：9通过/5缺能力失败；`logger-green`：14通过；新增格式化重入直接回归`logger-format-red`为14通过/1失败（多发nested-format），修复后`logger-format-green`为15通过/0失败/跳过。并发100条完整记录、两会话文件独占读回、文件错误、过滤/事件/递归均实际执行。Runtime辅助编译退出0/41个既有warning。
- Context7没有提供精确2022.3 ILogHandler资料，补读Unity 2022.3官方Scripting API，接口/前任handler用法亦由实际程序集编译确认；不外推线程Unity行为。
- `LoggerLifecycleTests.DefaultIsPassiveAndLaterHandlerKeepsOwnershipOnClose`已准备未运行。无XF独立Mono/IL2CPP Low消费者、五类真实Debug、同秒连续双Play、disable/destroy/quit释放、原Logger共存仍待引擎门，旧Logger阶段9证据不能替代。

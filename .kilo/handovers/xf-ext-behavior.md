# A 组连续实施检查点

唯一正式 Plan：`.kilo/plans/1792420261057-xf-ecosystem-integration.md`。本文件仅记录 A 组执行事实，不修改共享 Plan。

## 授权与基线

- 用户 2026-10-19 本轮授权连续完成 Overlap、CC2D、MoveablePlatform、XFFSM，含离线验证、本地中文候选提交及一次组内定向 Review。
- 实核工作区：`G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-behavior-901d14f1ae336a53`；分支 `fusion/xf-ext-behavior`；开工 HEAD/B0 `c88500aef6651f5b13ac933dd1f209ecb314e92d`，祖先检查退出 0，开工工作区干净。
- Unity `2022.3.62f3c1`。不调用 Unity MCP，不启动 Editor/batchmode。不修改共享区域、原包、主工程或其他 worktree；不 Push/合并/Rebase/Reset/Clean/Stash。
- Ruling：本次授权覆盖 Plan 过时的未授权/首批停止状态；D1 采用双 FSM 独立共存、局部替换风险 Editor 实现，D6 将运动组件作为可选组件。唯一执行记录为本文件，不另建过程 Plan/ledger。
- 原轻量 `Runtime/Utilities/FSM` 完整只读；产品和测试 asmdef、E0 工具、Core/Foundation/Resource、Packages/ProjectSettings 只读。

## 当前进度

- 当前模块：CharacterController2D 离线候选已完成，自动推进 MoveablePlatform。
- Overlap本地候选：`51e61da`，未Push。
- 来源：主工程只读 `Library/PackageCache/com.xfkj.xfgameframework.overlap@4961d83581`，1.0.4，revision `4961d8358119485c94d6f0f74b6ca5d8b4163460`，MIT/2024 弦风课堂。逐文件 SHA 将由专属导入工具记录在 `Tools/Behavior/Overlap.source.json`。
- 静态根因：静态结果缓冲跨实例污染、回调内查询重入、3D Reset 使用 2D 矩阵、销毁 Collider 比较与禁用结果残留、Area 无效节点提前返回不发退出。
- 已保留：7种查询形状、Results/enter/exit、100容量、检测频率/时间模式、区域/Box/Capsule作者操作；Editor绘制与Runtime隔离。
- 已修复：实例缓冲+同实例重入守卫、禁用时中断并清快照、fake-null过滤、Area空路径退出、3D层矩阵；小增强为每次启用周期一次“可能截断”提示。
- 实际红例：`overlap-red` 0/4通过；承接后 `overlap-import-red` 2通过/5失败，覆盖静态缓冲及缺重入/生命周期守卫。
- 实际绿例：`pwsh -File Tools/Behavior/Invoke-BehaviorOffline.ps1 -Mode All -Evidence overlap-final`，7/7（3托管逻辑+4静态契约），0失败/跳过。Runtime/Editor profile/EditorTools辅助编译退出0，分别41/40/10警告；含既有告警和上游过时Handles API告警，不计Unity通过。本组EditMode/PlayMode夹具辅助编译成功，未执行。
- 来源/保护区：`Test-BehaviorBoundary.ps1 -Evidence overlap-final` 核对19个C#、1个上游身份、保护区diff空，26个meta待主Unity生成；完整路径见`Temp/Behavior.Offline/overlap-final/pending-meta.txt`。完整MIT在产品局部`ThirdPartyNotices.txt`，共享根LICENSE/Notices留给集成owner串行汇总。
- 下一动作：精确提交Overlap及专属离线工具，继续CC2D的碰撞缓冲、重力禁用恢复、配置诊断和Inspector适配，不等待人工审批。

## CharacterController2D

- 来源：1.1.6，revision `f0241054744ca0673ec370d5fad39c32b789d81c`，逐文件身份在`Tools/Behavior/CharacterController2D.source.json`；MIT/2024弦风课堂全文保留于模块内。
- 保留原Rigidbody2D/Collider2D、ground/wall/top检测、多段跳/长按中断、重力/旋转、五类事件及原Inspector。无新输入包/全局角色系统，无CC2D以外共享代码改动。
- 局部修复：hit/contact实例隔离；停用归还重力并清输入/接触/跳跃缓存，重新启用刷新attached Collider；状态先提交再通知，检测结果不再为了刷新强发false/true；按配置groundAngle判断下方；Circle负缩放半径；更新信息节流时间实际写回。
- 小增强：`RefreshColliders()`及缺Collider/未命名Layer定位诊断；允许运行时配置修正后显式恢复。Scene标签/法线和Inspector计数保留，Handles仅在Editor。
- 实际红例`cc2d-red`：新增9项全部因缺能力失败，Overlap7项仍绿。`cc2d-green1`辅助编译抓到本次补丁局部变量赋值错位，已定点修正，不计成功。
- 最终`cc2d-closure`：16/16（11托管逻辑+5静态契约），0失败/跳过；Runtime/Editor profile/EditorTools辅助编译退出0，41/40/9警告。本组EditMode/PlayMode夹具编译通过未执行，PlayMode测试引用另有1条NUnit3.5/3.14兼容警告，不计为引擎运行通过。
- `cc2d-final`静态身份/保护区检查：31个C#、2个上游身份、保护区diff空、42个meta待生成。后续最终审计将覆盖调试计数恢复后的最终SHA。
- 下一动作：精确本地提交CC2D，继续Moveable路径边界、接触快照和多Collider/刚体去重，不等待审批。

## 验证边界

- 本轮Passed仅为上述真实离线逻辑/静态契约。Unity/EditMode/PlayMode/GUI/资产保存/Mono Player/IL2CPP 均未运行。
- 新脚本/目录/文本资产 `.meta` 由主 Unity 集成阶段生成，不复制原 GUID、不手工伪造。精确待生成清单将由专属静态工具输出。

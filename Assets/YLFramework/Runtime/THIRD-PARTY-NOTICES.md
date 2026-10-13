# Third-Party Notices

本文档集中记录 YLFramework 运行时源码中包含的第三方开源代码及其许可信息；完整许可文本随 YLFramework 一并分发。

## QFramework（liangxiegame）

以下目录包含源自或改编自 [QFramework](https://github.com/liangxiegame/QFramework) 的代码：

- `Runtime/Action/`：ActionKit 及其执行、组合、屏幕过渡与 Observable 适配。
- `Runtime/Audio/`：AudioKit 及音频管理、配置与诊断。
- `Runtime/Foundation/Kernel/Timing/`：统一 Timer 内核、内部索引堆与句柄收集；旧 `Runtime/Tools/Timing/` 的适用来源声明继续保留。
- `Editor/Audio/`、`Editor/Action/Help/`、`Editor/Help/`：基于 ActionKit / AudioKit API 的帮助内容与窗口适配。
- `Integrations/DOTween/`：ActionKit 的 DOTween 集成适配（不随默认导出；DOTween 本身适用 Demigiant 的独立 Artistic 许可）。

版权声明：

- Copyright (c) 2015 - 2025 liangxiegame
- Copyright (c) 2017 snowcold、Copyright (c) 2017 liangxie（Timing 等部分文件）
- 旧 Timer 文件同时标注 [UnityTimer](https://github.com/akbiggs/UnityTimer) 参考来源，统一计时后继续保留其声明：Copyright (c) 2016 Alexander Biggs & Adam Robinson-Yu。内部索引堆承接的是 YL / QFramework 的 BinaryHeap，不将该堆算法归于 UnityTimer。

以上来源均采用 MIT License：

```text
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

YLFramework 承接上述代码时进行了命名空间统一、程序集隔离、API 调整与部分实现重写；源文件中已有的上游版权横幅保持原样。

## XFGameFramework（弦小风）

- `Runtime/Foundation/Kernel/Serialization/SerializedDictionary.cs`：承接并改编自 XFGameFramework 的 `Runtime/Extensions/SerializedDictionary.cs`，来源 revision 为 `2182708aec`。统一容器在 2.5-A 重写行为后，于 2.5-B 从 YL Core 迁入 Foundation；仍保留 `datas/Key/Value` 序列化条目布局。
- `Runtime/Foundation/Kernel/AssemblyTool.cs`、`LayerMaskUtility.cs`、`Attributes/`：统一原 YL 实现与 XF revision `2182708aec` 的类型全名查找、掩码成员判断和展示元数据用途；其中 `DisableEdit` 迁自自有 Core，`CustomPropertyGUI` 保留 XF 的 TreeView 刷新配置。必要 Drawer 仍在 `Editor/XFMain/CustomPropertyDrawer/DisableEditDrawer.cs`，完整 Editor 许可继续保留于 `Editor/XFMain/LICENSE.md`。
- `Runtime/Services/Http/`、`Runtime/Tools/StringTools.cs` 与 `Runtime/Tools/Extensions/` 中的 Animator / Bounds / Color / EventTrigger / LayerMask / Quaternion / Random / Ray / Rigidbody / Scene / Time / Type / Vector2 / Vector3 扩展：由自有 Core 承接的 XF revision `2182708aec` 能力迁入正式 Runtime；HTTP 和几何行为修复仍保留来源。旧 GameObject / Component 扩展的递归设层及 Unity 存活判断合入现有 `GameObjectExtension` / `ObjectExtension`，不保留双实现。LogUtil 实现退役不撤销历史来源；原包不修改，完整 XF MIT 许可仍随 `Runtime/Foundation/Kernel/LICENSE.md` 与 `Runtime/XFMain/LICENSE.md` 分发。
- `Runtime/Audio/AudioChannel.cs`、`AudioKit.Named.cs`、`AudioPlayRequest.cs`、`Config/AudioClipLibrary.cs`、`Components/AudioGroupPlayer.cs` 与 `Editor/Audio/` 对应素材组 Inspector：承接 XF revision `2182708aec` 的命名控制、素材组选曲/配置、借入 Source、变化观察和 Enable 播放用途，按唯一 YL 请求/资源内核重新实现，未整类搬移旧播放器。自有 `Runtime/XFMain/Manager/Audio*.cs` 与旧 AudioPlayerInspector 已退出；删除重复实现不撤销历史来源、署名或完整许可。原 XF Package 保持只读且独立存在。
- 适用 MIT License，版权声明为 `Copyright (c) 2021 弦小风`；完整许可见 `Runtime/Foundation/Kernel/LICENSE.md`。其余 Core 承接源码的完整许可继续保留在 `Runtime/XFMain/LICENSE.md`。

## 其他第三方组件

- `Runtime/Tools/Json/`：JSONObject（Matt Schoen / Defective Studios，MIT），完整文本见该目录 `LICENSE.md`。
- `Runtime/Tools/ZipTool/`：ICSharpCode.SharpZipLib（MIT），来源、署名与完整文本见 [`Tools/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md`](Tools/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md)。内置源码与许可随默认 Runtime 导出一并分发。
- `Plugins/` 下的第三方编辑器插件随附各自的说明材料，其许可与使用条件以插件自身提供为准。

## ColorSelector 的历史来源与替代

- 原 `Samples/ColorSelector/Readme.txt` 标注 `CodeArtist.mx 2015`、`Code: Rodrigo Fernandez Diaz`、联系地址 `http://codeartist.mx/`；其 `.meta` 的 `licenseType: Store` 不是开源再分发许可。
- 未取得覆盖原代码、Shader、图片、材质或 Prefab 布局且允许作为框架工具再分发的完整许可。2026-10-11 核查时作者站点不可读；没有将免费获取、署名、修改或其他来源的 MIT 当作该资源的授权。
- Unity [Asset Store Terms/EULA](https://unity.com/legal/as-terms) 和[官方商业使用说明](https://support.unity.com/hc/en-us/articles/205623589-Can-I-use-assets-from-the-Asset-Store-in-my-commercial-game) 也不等同于资产类产品的自由再分发授权；这些规则不证明原目录具体取得过何种许可。
- 原实现、Shader、素材及布局已从分发目录退出，未迁入 Runtime。历史作者署名仍在 Sample 说明中保留，原文件可由 Git 历史追溯，不以删署名或改名掩盖来源。
- `Runtime/XFMain/UI/Tools/` 与 `Prefabs/ColorSelector.prefab` 按既定 HSV 功能契约独立实现/重建，不复制原源码表达、Shader 或图片。图形和游标由项目代码生成，材质使用安装的标准 UGUI 路径，不将 Unity 包 Shader 源码复制到框架。不声称本次采用了严格 clean-room 隔离。

# Third-Party Notices

本文档集中记录 YLFramework 运行时源码中包含的第三方开源代码及其许可信息；完整许可文本随 YLFramework 一并分发。

## QFramework（liangxiegame）

以下目录包含源自或改编自 [QFramework](https://github.com/liangxiegame/QFramework) 的代码：

- `Runtime/Systems/Action/`：ActionKit 及其执行、组合、屏幕过渡与 Observable 适配。
- `Runtime/Systems/Audio/`：AudioKit 及音频管理、配置与诊断。
- `Runtime/Foundation/Timing/`：统一 Timer 内核、内部索引堆与句柄收集；旧 `Runtime/Tools/Timing/` 的适用来源声明继续保留。
- `Editor/Systems/Audio/`、`Editor/Systems/Action/Help/`、`Editor/Help/`：基于 ActionKit / AudioKit API 的帮助内容与窗口适配。
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

- `Runtime/Foundation/Serialization/SerializedDictionary.cs`：承接并改编自 XFGameFramework 的 `Runtime/Extensions/SerializedDictionary.cs`，来源 revision 为 `2182708aec`。统一容器在 2.5-A 重写行为后，于 2.5-B 从 YL Core 迁入 Foundation；仍保留 `datas/Key/Value` 序列化条目布局。
- `Runtime/Foundation/Utilities/{AssemblyTool,LayerMaskUtility}.cs` 与 `Foundation/Attributes/`：统一原 YL 实现与 XF revision `2182708aec` 的类型全名查找、掩码成员判断和展示元数据用途；其中 `DisableEdit` 迁自自有 Core，`CustomPropertyGUI` 保留 XF 的 TreeView 刷新配置。必要 Drawer 位于 `Editor/Foundation/Attributes/DisableEditDrawer.cs`，原类型身份与 GUID 保持。
- `Runtime/Systems/Http/`、`Runtime/Utilities/StringTools.cs` 与 `Runtime/Utilities/Extensions/` 中的 Animator / Bounds / Color / EventTrigger / LayerMask / Quaternion / Random / Ray / Rigidbody / Scene / Time / Type / Vector2 / Vector3 扩展：由自有 Core 承接的 XF revision `2182708aec` 能力迁入正式 Runtime；HTTP 和几何行为修复仍保留来源。旧 GameObject / Component 扩展的递归设层及 Unity 存活判断合入现有 `GameObjectExtension` / `ObjectExtension`，不保留双实现。LogUtil 实现退役不撤销历史来源，原包不修改。
- `Runtime/Systems/Audio/AudioChannel.cs`、`AudioKit.Named.cs`、`AudioPlayRequest.cs`、`Config/AudioClipLibrary.cs`、`Components/AudioGroupPlayer.cs` 与 `Editor/Systems/Audio/` 对应素材组 Inspector：承接 XF revision `2182708aec` 的命名控制、素材组选曲/配置、借入 Source、变化观察和 Enable 播放用途，按唯一 YL 请求/资源内核重新实现，未整类搬移旧播放器。自有 `Runtime/XFMain/Manager/Audio*.cs` 与旧 AudioPlayerInspector 已退出；删除重复实现不撤销历史来源、署名或完整许可。原 XF Package 保持只读且独立存在。
- 上述 XF 承接源码（含 Editor）适用 MIT License，版权声明为 `Copyright (c) 2021 弦小风`；完整上游许可集中于框架根目录 [`Assets/YLFramework/LICENSE.md`](../LICENSE.md)，默认导出显式携带该文件。不重复放置分区副本，也不以此覆盖其他来源的许可。

## 拓展公共准备 E0

- `Runtime/Systems/Excel/`：承接 `com.xfkj.xfgameframework.excelsystem` 1.0.7，revision `725f94f23327de190111573870146af66e9484b6` 的 `ExcelIgnore`、`ExcelScriptableObjectID`、`CustomExcelFieldImportAndExport` 与必要枚举 `ExcelFieldsType`。来源为 https://gitee.com/xianfengkeji/xfgame-framework.excel-system ，MIT，`Copyright (c) 2025 弦风课堂`；完整许可和版权行并入框架根 `LICENSE.md`。未迁入解析器内部DTO、EPPlus、任何xlsx后端或领域模块。
- `Editor/Utilities/TreeViews/ReorderableTreeView.cs`：承接 XFABManager 1.24.0 revision `c35dc4058e` 的 `Editor/Other/TreeView/DragableTreeView.cs`，MIT，`Copyright (c) 2021 弦小风`。保留平面TreeView构建/重排与派生钩子，局部适配当前列表getter、Undo/Redo、索引槽位和每实例拖拽所有权；完整许可仍集中于根 `LICENSE.md`。不将此工具承接称为再次迁移XFAB资源系统。
- 原XF包、Analyzer和游戏保持原归属，E0不表示13项系统已迁移；可选依赖与重大API/格式取舍仍须另行裁定。

## 其他第三方组件

### A 组行为运动来源

- `Runtime/Systems/Overlap/`及对应Editor：`com.xfkj.xfgameframework.overlap` 1.0.4，revision `4961d8358119485c94d6f0f74b6ca5d8b4163460`。
- `Runtime/Systems/CharacterController2D/`及对应Editor：`com.xfkj.xfgameframework.charactercontroller2d` 1.1.6，revision `f0241054744ca0673ec370d5fad39c32b789d81c`。
- `Runtime/Systems/MoveablePlatform/`及对应Editor：`com.xfkj.xfgameframework.moveableplatform` 1.0.6，revision `1e1265d8e6adb4ebab6febe8bc51288972e2b234`。
- 上述三个来源均为 MIT / `Copyright (c) 2024 弦风课堂`；完整原文保留于各模块`ThirdPartyNotices.txt`并由根`LICENSE.md`携带适用版权。精确上游文件身份见`Tools/Behavior/*.source.json`，不是原包GUID复用。
- `Runtime/Systems/StateMachine/`及对应Editor：XFFSM 1.5.4，revision `b06333c2cdc801fcc401f14c252853bf099aa1b7`，MIT / `Copyright (c) 2021 弦小风`。保留成熟资产FSM，与原轻量`YLFramework.FSM`独立共存。`FSMSelectionDragger.cs`、网格及框选实现未复制，调用Unity公共GraphView API；Unity Reference-Only来源判断与链接见模块`ThirdPartyNotices.txt`，不将该许可重标为MIT。
- 主工程接收不改变原XF包、场景或Prefab身份；本轮没有正式Exporter/G4独立交付，也不以共存工程验证代替无旧包首导入。

### B 组配置数据来源

- `Runtime/Systems/Localization/`与`Editor/Systems/Localization/`：Localization 1.0.9，revision `743876d52c5fe636effc0b63c34bd261d6cad243`，来源 https://gitee.com/xianfengkeji/xfgame-framework.localization.git ，MIT / `Copyright (c) 2024 弦风课堂`。保留语言/条目布局与作者流程，资源/事件接YL；不承接Youdao凭据、联网翻译或Steam窗口。完整MIT在模块`LICENSE.txt`及根`LICENSE.md`。
- `Runtime/Systems/DataAssets/`及对应Editor：ScriptableObjectConfigs 1.1.4，revision `d7b6c171999e05a1840f413e1f3e2080e3853163`，来源 https://gitee.com/xianfengkeji/xfgameframework.scriptableobjectconfigs.git ，MIT / `Copyright (c) 2024 弦风课堂`。保留根/子资产、类型绑定、ID/名称/hash及CRUD；Runtime与Editor目录各保留完整`LICENSE.txt`。
- `Integrations/Excel/Editor/`：ExcelSystem 1.0.7，revision `725f94f23327de190111573870146af66e9484b6`，MIT / `Copyright (c) 2025 弦风课堂`。三行表头、转换与SO/List/JSON作者用途继续保留，EPPlus未复制；完整MIT见`Licenses/XF-ExcelSystem-LICENSE.txt`。
- 可选MiniExcel 1.31.2，来源 https://github.com/mini-software/MiniExcel ，commit `f1498a2d162debca9a1e9153cf2249131a75eeb0`，Apache-2.0。未经修改的netstandard2.0 DLL SHA256为`0a49b66224bef9fdcec12800e4660267a993b1ece87727d9fe15c7dabf58ba1e`；完整Apache-2.0随`Integrations/Excel/Editor/Licenses/MiniExcel-LICENSE.txt`，具体nupkg/来源及许可指纹在`Dependencies.json`。该整目录须作为选装单元携带，不进入核心默认导出或Player，核心无需解析器引用。
- `Runtime/Systems/Save/`：吸收ArchiveSystem 1.0.7（revision `8046fe56f82983e6081a5c110559915d94df9e3e`，来源 https://gitee.com/xianfengkeji/xfgame-framework.archive-system.git ，MIT / `Copyright (c) 2025 弦风课堂`）的模型/槽位/当前项用途，统一到唯一SaveStore。没有第二ArchiveManager、DES、XF旧档扫描或迁移；删除历史迁移能力不撤销来源署名。完整MIT与2025版权见根`LICENSE.md`。

### C 组领域玩法来源

- `Runtime/Systems/Skill/`及`Editor/Systems/Skill/`：SkillSystem 1.5.8，revision `4c9d8f5d3120cdf7333a9a040a85ee9583af072f`，来源 https://gitee.com/xianfengkeji/xfgame-framework.skill-system.git 。
- `Runtime/Systems/Buff/`及`Editor/Systems/Buff/`：BuffSystem 1.7.3，revision `01d05626b01bcb01e0c27a1040119f41b534f902`，来源 https://gitee.com/xianfengkeji/xfgame-framework.buff-system.git 。
- `Runtime/Systems/Mission/`及`Editor/Systems/Mission/`：MissionSystem 1.2.4，revision `a3da5d37ae61d24becd915281f2b676cb1210749`，来源 https://gitee.com/xianfengkeji/xfgame-framework.mission-system.git 。
- 上述三项均为MIT / `Copyright (c) 2024 弦风课堂`；沿用领域算法和普通作者流程，局部接YL Pool/Update/Resource，配置与运行态隔离。各Runtime模块`LICENSE.txt`保留完整原文，覆盖相应Editor承接代码；不恢复已退役的XF旧事件、旧池接口或路径字段。
- `Runtime/Systems/Reddot/`：ReddotSystem 1.0.4，revision `8e2c23e435a4ff1eb4dbc441a6d4fb389ee58c89`，来源 https://gitee.com/xianfengkeji/xfgame-framework.reddot-system.git ，MIT / `Copyright (c) 2025 弦风课堂`。
- `Runtime/Systems/Logging/`：LoggerSystem 1.0.1，revision `0ebbebeb4b961d6fb67670d22cad819856969c3a`及主工程embedded来源的既有handler/writer所有权修复，来源 https://gitee.com/xianfengkeji/xfgame-framework.logger-system.git ，MIT / `Copyright (c) 2025 弦风课堂`。新YL日志默认不接管；没有复制旧Analyzer。上述两模块各附完整`LICENSE.txt`。
- `Integrations/DomainExcel`只消费B的真实Editor后端，需显式`YL_DOMAIN_EXCEL`，不进入核心默认包或Player；选装单元需同时携带`Integrations/Excel/Editor`和其MIT/Apache-2.0许可。Mission跨sheet原子事务与全局Buff Effect库xlsx未实现，不以普通作者工具或单sheet测试冒称支持。

### 共享依赖与排除

- `Newtonsoft.Json.dll`由外部UPM依赖`com.unity.nuget.newtonsoft-json`提供，不复制到本框架导出根。受测3.2.1包的`LICENSE.md`声明Unity Companion License；其`Third Party Notices.md`分别保留Newtonsoft.Json（James Newton-King）、Json.Net.Unity3D（SaladLab）、Newtonsoft.Json-for-Unity（Kalle Jillheden）及com.newtonsoft.json（Mike Wuetherick）的完整MIT声明。应用重新分发该二进制时须携带对应包的适用许可与通知，不能用下述JSONObject许可替代。
- `Plugins/BouncyCastle/`随框架导出批准的BouncyCastle.Cryptography 2.7.0官方netstandard2.0二进制、完整MIT `LICENSE.md`与精确来源`SOURCE.txt`。Resource仅内部使用其HKDF-SHA256，Windows AES-GCM由系统CNG提供，不分发系统密码DLL。Tink仅作为framing参照，适用Apache-2.0全文随`TINK-LICENSE.txt`携带，不宣称依赖或分发Tink SDK；详见该目录来源声明。
- `Runtime/Utilities/Json/`：JSONObject（Matt Schoen / Defective Studios，MIT），完整文本见该目录 `LICENSE.md`。
- `Runtime/Utilities/ZipTool/`：ICSharpCode.SharpZipLib（MIT），来源、署名与完整文本见 [`Utilities/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md`](Utilities/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md)。内置源码与许可随默认 Runtime 导出一并分发。
- 宿主`Plugins/Editor Enhancers Bundle/`下的商业编辑器插件未取得框架再分发授权，不随正式Exporter输出；不以购买、署名或本机安装代替再分发许可。主工程原文件及配置保持。
- 可选`com.xfkj.xfgameframework.loggersystem` 1.0.1不在本框架UnityPackage中，也不是YL依赖。阶段9独立tarball保留其自身完整MIT `LICENSE`、`Copyright (c) 2025 弦风课堂`及原身份；来源revision `0ebbebeb4b961d6fb67670d22cad819856969c3a`，项目补丁含handler/writer所有权与独立IL2CPP自动初始化根修复。该包单独许可不能被上面2021弦小风MIT替代；交付补丁精确身份见可选制品SHA256和逐文件清单，不宣称迁移其他XF扩展。

## ColorSelector 的历史来源与替代

- 原 `Samples/ColorSelector/Readme.txt` 标注 `CodeArtist.mx 2015`、`Code: Rodrigo Fernandez Diaz`、联系地址 `http://codeartist.mx/`；其 `.meta` 的 `licenseType: Store` 不是开源再分发许可。
- 未取得覆盖原代码、Shader、图片、材质或 Prefab 布局且允许作为框架工具再分发的完整许可。2026-10-11 核查时作者站点不可读；没有将免费获取、署名、修改或其他来源的 MIT 当作该资源的授权。
- Unity [Asset Store Terms/EULA](https://unity.com/legal/as-terms) 和[官方商业使用说明](https://support.unity.com/hc/en-us/articles/205623589-Can-I-use-assets-from-the-Asset-Store-in-my-commercial-game) 也不等同于资产类产品的自由再分发授权；这些规则不证明原目录具体取得过何种许可。
- 原实现、Shader、素材及布局已从分发目录退出，未迁入 Runtime。ColorSelector 样例随后退役，历史作者署名 `CodeArtist.mx 2015`、`Code: Rodrigo Fernandez Diaz` 与联系地址 `http://codeartist.mx/` 集中保留于本文；原文件及完整 Readme 可由 Git 历史追溯，不以删署名或改名掩盖来源，也不新增再分发许可。
- `Runtime/Utilities/UI/ColorSelector/` 的控件、渲染及窗口与 `Prefabs/ColorSelector.prefab` 按既定 HSV 功能契约独立实现/重建，不复制原源码表达、Shader 或图片。图形和游标由项目代码生成，材质使用安装的标准 UGUI 路径，不将 Unity 包 Shader 源码复制到框架。不声称本次采用了严格 clean-room 隔离。

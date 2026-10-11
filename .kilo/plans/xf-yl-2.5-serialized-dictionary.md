# XF × YL 2.5 SerializedDictionary 研究与 Implementation Plan

**状态：总体技术方案已批准；2.5-A 已获授权并完成实现及定向技术验证（2026-10-11，见第 14 节），待用户验收与 Git 收口。B/C 未获实施授权。**

> 后续实施代理仅在用户授权具体批次后，按 `executing-plans` 或适用的 `subagent-driven-development` 工作方式执行。第 13 节总体批准与第 14 节 A 的单独授权应区分；A 的授权不扩展到 B/C、分支操作或 Git 收口。

**目标：** 将新 YL 内的两份序列化字典收敛为可独立复用、运行时修改可保存、Inspector 可编辑的唯一通用容器。

**推荐架构：** 序列化条目列表为唯一事实源，运行时使用一份非序列化的键到行索引缓存；所有公开修改写穿列表。Unity 回调只管理缓存失效，Json.NET 使用标准 `IDictionary<TKey,TValue>` contract，不把 Unity 字段格式当作 JSON 存档格式。

**技术基线：** Unity `2022.3.62f3c1`；`YLFramework.Foundation.Runtime`；现有 Json.NET `13.0.2` / Unity 包 `com.unity.nuget.newtonsoft-json@3.2.1`；必要的 Unity 原生 PropertyDrawer。

**共同约束：** [长期融合蓝图](../../Assets/YLFramework/Documentation/IntegrationPlan.md)、[阶段 2 总规划](xf-yl-2-runtime-support.md)、根 `AGENTS.md`。本文件是 2.5 唯一专项方案。

## 1. 基线与范围

- 2026-10-11 实际核对：`integration/xf-yl`，HEAD `09e3fe0`，`milestone/2.4-timer` 指向同一提交，开始时工作区干净。接受用户已验收的阶段 1、Pool、Events、Update、Timer，不重复审查。
- 长期蓝图和阶段 2 总规划仍有 Timer 收口前的 `IN PROGRESS` / 待 Git 收口文字；本轮以用户确认及实际 Git 基线为准。受本轮单文档边界约束，不修改这两份历史状态。
- 原 XF/XFAB 及原游戏只读，原游戏保留原框架、资源、UnityEvent、Bundle 和存档路径。普通 Dictionary、Save 系统总体设计、JSON 后端、Pool/MVC/UI 均不是替换对象。
- 本轮只做源码、配置、文本资产、程序集及版本匹配资料研究；没有运行 Unity 刷新、测试、编译、PlayMode 或构建。下述运行时结果预测不是已执行的复现。

路径简称：`C` = `Assets/YLFramework/Runtime/XFMain/`，`Y` = `Assets/YLFramework/Runtime/`，`E` = `Assets/YLFramework/Editor/`，`T` = `Assets/YLFramework/Tests/`，`XF` = `Library/PackageCache/com.xfkj.xfgameframework@2182708aec/`。行号对应本轮源码。

## 2. 源码与能力对比

| 项目 | 原 XF 与新 YL Core 承接副本 | 旧 YL Save/Serialization |
|---|---|---|
| 实际类型 | `XFGameFramework.SerializedDictionary<TKey,TValue>` / `YLFramework.Core.SerializedDictionary<TKey,TValue>`；另有公开 `DictionaryItem<TKey,TValue>` | `YLFramework.Services.Save.SerializedDictionary<K,V>` |
| 所属程序集 | 原 `XFGameFramework` / 新 `YLFramework.Core.Runtime` | `YLFramework.Runtime`，不是 Foundation |
| Unity 数据 | `[SerializeField] List<DictionaryItem<TKey,TValue>> datas`，条目字段 `Key`、`Value` | `[SerializeField,HideInInspector] keyList`、`valueList`，非序列化 `dictionary` |
| 运行时机制 | 两份延迟缓存：键到值、键到条目；仅以 Count 比较判断重建 | 一个真实 Dictionary；Unity 和 .NET 序列化回调复制/重建 |
| 公开操作 | `Add/Clear/ContainsKey/ContainsValue/Remove`，索引器、`Keys/Values/Count`；无标准字典接口、无 `TryGetValue`、无字典枚举接口 | 只公开 `Dictionary`，调用者直接操作底层字典；构造时可借用外部实例 |
| 缺键读取 | 返回 `default(TValue)`，与真正存储 default 无法区分 | 底层 Dictionary 的标准契约，索引器抛 `KeyNotFoundException` |
| 重复键 | Add 接受重复条目；读取采用第一项，键值缓存重建时记录错误 | 正常运行由 Dictionary 拒绝重复；反序列化直接 Add，重复抛错 |
| Inspector | 默认 Inspector 可见 `datas/Key/Value`；没有专用 Drawer | 双列表隐藏，无已找到的编辑界面 |
| 比较器与所有权 | 默认比较器；未提供外部字典借用 | 构造函数直接引用外部 Dictionary，但反序列化新建默认比较器的 Dictionary，原 alias/comparer 不保留 |
| 有价值部分 | 单条目绑定 Key/Value、可编辑列表、序列化行顺序；这些通用能力值得保留 | 真字典的正确性与标准操作、明确回调意识；不应照搬双列表及外部可变字典出口 |

**直接依据：** `XF/Runtime/Extensions/SerializedDictionary.cs:12–150`，`C/Extensions/SerializedDictionary.cs:12–150`，`Y/Save/Serialization/SerializedDictionary.cs:24–94`。原 XF 与 Core 的 `git diff --no-index` 只显示 BOM 和 namespace 差异，不是两个不同算法。XF `Documentation~/ClassApi/SerializedDictionary.md:6–15` 只给“用法和字典一致”及 MonoBehaviour 字段示例，不证明标准 IDictionary 或 JSON 支持。

**当前身份：** Core 脚本 GUID `1418cb45915d1504e859f117b32ade29`，旧 Save 脚本 GUID `92f87fcb20da409a84c5c36308bed9fb`，原 XF GUID `4399cf79252792242b318125b35b6325`。XF/Core 的 Unity 字段结构相同，但 namespace、程序集和 GUID 不同；旧 YL 双列表格式与前两者不同。保留 GUID 不等于保证类型/字段格式兼容。

## 3. 消费者与数据证据

### 3.1 已检索范围

- 全 `Assets` C#，包含原游戏、YL Runtime/Editor/Tests/Plugins；精确搜索 `SerializedDictionary`、`DictionaryItem`、近名 `SerializableDictionary`、泛型派生、相关反射/字符串字段引用。
- 显式定向读取被 Git 忽略的 `Library/PackageCache/com.xfkj.*`，覆盖原 XF/XFAB 及已安装 XF 扩展；另检查命中的 SRP、2D Animation 等近名实现，不把默认搜索跳过 Library 当成无引用证据。
- 全 `Assets` 的 `*.unity/*.prefab/*.asset`，检查目标类型名、`datas:`、`keyList:`、`valueList:`；补查两份 YL 脚本 GUID。`ProjectSettings/EditorSettings.asset:7` 为 ForceText。两 GUID 在该范围只命中各自 meta，未找到资产引用。
- 检查实际 asmdef、测试、相关文档、Save 调用链、Json.NET 来源，以及已提交存档样例。

### 3.2 本轮发现

| 范围 | 已确认的检索结果 | 设计含义 |
|---|---|---|
| 两份 YL 目标类型 | C# 仅找到定义，没有找到字段/调用/泛型派生；`DictionaryItem` 只被自身容器使用 | 没有查实必须保留旧公开 API 的仓库内消费者 |
| 原 XF 生态 | 已检索安装源码只命中目标定义及说明，没有查实实际消费者 | 原包仍保留，不因检索结果删除或转接 |
| 场景/Prefab/ScriptableObject/UnityEvent/反射 | 未找到相关字段、类型名、脚本 GUID、动态绑定 | 没有查实待迁移的 YL 自有资产清单 |
| 持久化样例 | 未找到已提交的 `SaveSystemData.json` 或使用目标字典的存档样例 | 不可声称存在已完成的数据迁移，也不预建历史 reader |
| Editor | XF/Core 专用 Drawer 仅找到 DisableEditDrawer，没有目标字典 Drawer | 本专题确有必要新增最小字典编辑/诊断支撑 |
| 测试 | 未找到目标字典行为测试；Save.md 所列 `SaveManagerTests.cs` 实际未找到 | 只复用现有测试程序集和身份测试模式，不能沿用文档中的测试数量 |

**证据限制：** 未反编译预编译 DLL，未读取仓库外用户存档，未打开二进制资产或分析生成 Bundle；ForceText 配置也不能证明历史资产全部是文本。没有找到引用不等于不存在外部用户、未提交数据、运行时生成引用或自定义 serializer。未查实的消费者不是为了冻结新框架而虚构的兼容需求；实施前须对届时差异复查，发现真实依赖时暂停对应身份/格式退出并补充有样本的迁移方案。

SRP 自有 `UnityEngine.Rendering.SerializedDictionary`、2D Animation 内部 `SerializableDictionary`、插件 `VHierarchy.Libs` / `VFolders.Libs.SerializableDictionary` 均不是融合对象。SRP 同短名冲突通过 namespace/明确 using 解决，不改第三方类型或插件数据。

## 4. Unity 与实际 JSON 路径必须分开

### 4.1 Unity 路径

Unity 直接序列化字段，不经过公开索引器。Core 的 `datas` 可以承载符合 Unity 规则的 Key/Value；旧 YL 的双列表靠 `ISerializationCallbackReceiver` 转换。Inspector 修改、实例克隆和脚本重载均可能触发序列化；回调可能不在主线程，不能在回调内访问场景、AssetDatabase 或依赖任意 Unity 对象存活 API。

`[Serializable]` 与 `ISerializationCallbackReceiver` 不让 Unity 自动支持 Dictionary、任意复杂泛型、嵌套容器或多态引用。Key/Value 仍须符合 Unity 规则；本专题不为全部类型建立自定义序列化系统。

### 4.2 仓库中 Save 的真实实现路径

`SaveManager` → `SaveFileIO.WriteJson/ReadJson` → `JsonConvert.SerializeObject/DeserializeObject`，不是 JsonUtility：

- `Y/Save/SaveManager.cs:45–59`：用户对象设置为公开、默认空的 `JsonSerializerSettings`；索引元数据另用固定设置。未找到 Assets 源码针对这条存档路径的 converter/resolver 配置或 `JsonConvert.DefaultSettings` 赋值；运行时外部设置仍是未知边界。
- `Y/Save/SaveFileIO.cs:90–93,185–201`：直接按传入设置读写 Json.NET，失败沿现有 JSON/IO 错误与备份逻辑处理；本专题不重构它。
- `Y/Save/SaveManager.cs:40–42,64–85`：根目录默认 `Application.persistentDataPath`，也可覆盖；对象位于 `SaveData/{id}/{name}.json`，索引为 `SaveData/SaveSystemData.json`，设置位于 `Setting/{name}.json`。内存缓存命中返回原对象，因此保存后直接 Load 不能证明磁盘往返。
- `ProjectSettings/ProjectSettings.asset:15–16` 的公司/产品身份已核对；没有读取该用户目录。静态配置只能给出候选位置，不能证明某次运行的真实 StorageRoot、文件存在与内容。
- 包锁及 `Library/PackageCache/com.unity.nuget.newtonsoft-json@3.2.1/package.json:3–6` 确认 Json.NET `13.0.2`。不是新加依赖；Foundation 目前无 Newtonsoft 引用。

未找到游戏/测试直接调用上述 YL SaveManager，也未找到它消费两份目标字典。已安装的原 XF ArchiveSystem 另有 Json.NET 路径，已定向核对无目标字典引用，但未找到 `Assets/Games` 对 ArchiveManager 的调用，不能把安装包的保存能力当成游戏实际存档使用。游戏中确认的持久化例子是 `Assets/Games/MainModule/Scripts/Controller/AudioController.cs:14–17` 的 PlayerPrefs 音量旧键处理，不使用目标字典；这些路径均保持原 owner，不纳入本专题改造。

### 4.3 默认 Json.NET contract 的静态判断

依据对应 `13.0.2` 的 `DefaultContractResolver.CreateContract/GetSerializableMembers/ResolveCallbackMethods`、`CollectionUtils.IsDictionaryType`、`JsonSerializerInternalReader.PopulateDictionary/PopulateObject`：

| 类型 | 默认 JSON 写出形状 | 读回判断，尚未运行 |
|---|---|---|
| 旧 YL 包装器 | `{"Dictionary":{"a":1}}`；private `[SerializeField]` 双列表不自动成为 JSON 成员 | public 只读 Dictionary 可能被填充，但随后 .NET `[OnDeserialized]` 调 `OnAfterDeserialize`，访问未被填充的 `keyList`；预计空引用失败，或者在其他设置下覆盖已填充字典 |
| XF/Core | `{"Keys":["a"],"Values":[1],"Count":1}`；真实 `datas` 未被写出 | 没有恢复 datas 的路径；只读 KeyCollection/ValueCollection 的具体填充结果可能是跳过或异常，不能宣称可靠 round-trip |
| 推荐 IDictionary 类型 | `{"a":1}`；字典 contract 优先于 IEnumerable，附加 Count/诊断属性不写入 | public 无参构造创建容器，Json.NET 用索引器 setter 填充；setter 写穿列表，无须 Unity 回调才能恢复数据 |

这是版本匹配的源码推导，不是本轮已复现的游戏存档损坏。Unity 接口回调和 .NET `[OnSerializing]/[OnDeserialized]` 不是同一机制；旧 YL 显式桥接了二者，新实现不继续携带这组不必要的 .NET 回调桥接。

## 5. 缺陷、契约与风险分类

| 分类 | 源码依据/行为 | 处理 |
|---|---|---|
| 已确认机制缺陷 | Core setter 只改 `DatasDic`（148）；已有键值未写 datas，新键在后续 Count 不匹配重建时被丢弃 | 必须修复写入事实源，不仅补序列化回调 |
| 已确认机制缺陷 | Core 两缓存仅按 Count 失效（52/74）；同长度 key/value 编辑可保留旧缓存 | 回调明确失效，禁止 Count 作为有效性判据 |
| 已确认机制缺陷 | Core Add 无校验（106–109）；重复使缓存 unique Count 永远小于 raw Count，访问重复重建/报错；Remove 第一行后重复项可重新成为有效值 | 分离运行时合法操作与 Inspector 暂存行；Remove 删除该键全部行 |
| 已确认输入脆弱性 | Core null item/key 无处理；旧 YL OnAfterDeserialize 不校验列表 null/长度/重复（85–94），先替换字典后可能中途失败 | 新 Unity 导入保留可检查的原始行，使用确定投影，不抛预期的脏输入错误或留下半建缓存 |
| 合理现有契约 | Core 缺键返回 default；旧 YL 构造借用外部 Dictionary；重复时首项优先 | 不是仅凭“不像标准库”判缺陷；推荐经批准改变公开契约，说明无已查实消费者 |
| 设计限制/成本 | 旧 YL 每次 OnBeforeSerialize 新建两份 List，反序列化后清空列表，Inspector 隐藏；外部 alias/comparer 不跨重载保留 | 不宣称已测出瓶颈；采用一个可编辑列表和可控修改入口 |
| 高置信度未运行风险 | 两旧类型默认 Json.NET contract 不可可靠恢复真实数据 | 后续做真实版本往返，不把 Unity 测试代替 JSON 测试 |
| 通用限制 | 可变 key 的 hash/equality、Unity inline null/多态限制、JSON 复杂键及自定义设置 | 明确支持范围，不加入通用 comparer 持久化、任意 key 编码或全局 converter |

## 6. 主要备选与推荐

| 方案 | 优点 | 代价与判断 |
|---|---|---|
| 修补 Core 双缓存 | 字段布局/Inspector 接近现状 | 若只补 Count/hash 检查仍可能漏同长度编辑；两份缓存和两种写入位置没有必要，不推荐 |
| 旧 YL Dictionary 权威 + 双列表回调 | 标准库行为直接、查找高效 | 双列表错位、隐藏编辑与 JSON 桥接问题；暴露 Dictionary 让 dirty/write-through 无法控制，不推荐原样采用 |
| 继承 Dictionary + Unity 回调列表 | 很少转发代码、天然 JSON 字典 contract | Dictionary 修改入口非虚、不能可靠拦截全部路径；Inspector 列表和继承字典仍需要同步所有权，不推荐 |
| **条目列表权威 + 私有 key→index 缓存 + IDictionary** | Key/Value 同行、Inspector 可修复；运行时与 Unity 保存同一数据源；标准 JSON 接入无需第三方绑定 | Remove/重排 O(n)，需要少量标准接口转发；最适合配置/序列化容器，推荐 |

不增加第二种“运行时字典”容器、通用序列化服务、可插拔同步策略或跨系统抽象。高频大规模动态集合仍用普通 Dictionary，并由业务定义 DTO/保存边界。

## 7. 推荐正式契约

### 7.1 类型、文件与依赖

- 最终公开类型：`YLFramework.Foundation.SerializedDictionary<TKey,TValue>`，程序集 `YLFramework.Foundation.Runtime`，源码 `Y/Shared/Serialization/SerializedDictionary.cs`。
- 从现有 Core 文件移动并保留它的 meta/GUID；仍保留私有 `datas` 和条目字段 `Key/Value` 的 Unity 布局。条目实现保留在同文件，收为内部实现，不再把 `DictionaryItem` 作为独立公共 API。不为形式改名重写字段结构，不改为 SerializeReference。
- Runtime 实现 `[Serializable]`、`IDictionary<TKey,TValue>`、`IReadOnlyDictionary<TKey,TValue>`、`ISerializationCallbackReceiver`。保留可声明具体非泛型子类的能力，测试直接泛型字段与具体子类；不强迫用户为每个字典创建包装类型。
- Foundation 不依赖 Newtonsoft、旧 Runtime/Core、XF、Editor、Pool 或全局宿主；Core 和旧 Runtime 已直接引用 Foundation，不新增 Runtime asmdef。
- 最小 Drawer 位于 `E/Serialization/SerializedDictionaryDrawer.cs`，归 `YLFramework.Editor`，只额外直接引用 Foundation；不放进仍带 Core/XFAB 依赖的 Core Editor，不新增完整 Foundation Editor 系统。
- 行为正确后退出 `YLFramework.Core.SerializedDictionary`、公开 `DictionaryItem` 和 `YLFramework.Services.Save.SerializedDictionary`；不加同名转发、obsolete facade、旧 namespace alias 或自动迁移钩子。

### 7.2 公开 API

| API | 推荐行为 |
|---|---|
| 无参构造；接受 `IEnumerable<KeyValuePair<TKey,TValue>>` 的构造 | 空容器/复制条目，null source 抛 `ArgumentNullException`，重复键抛错；不借用外部 Dictionary |
| `TValue this[TKey key]` | get 缺键抛 `KeyNotFoundException`；set 更新已有首个有效行或追加新行 |
| `Add` / `TryAdd` | 重复键分别抛 `ArgumentException` / 返回 false；非法 null key 抛 `ArgumentNullException`，失败不改已有条目 |
| `TryGetValue` / `ContainsKey` / `ContainsValue` | 明确存在性；value 使用默认相等比较，可存 default/null |
| `Remove` / `Clear` | Remove 删除该 key 的全部序列化行，防止重复项复活；Clear 包括全部无效行 |
| `Count` / `Keys` / `Values` / 枚举 | 只表达有效唯一键；Keys/Values 为不可修改的 live view，不暴露底层 Dictionary 或 raw List |
| `InvalidEntryCount` | 当前 raw 行中被忽略的 null/重复行数，便于运行时诊断；读取不日志轰炸、不自动清理 |
| 标准接口成员 | 完成 IDictionary/ICollection 的 Contains/Remove(KeyValuePair)、CopyTo、枚举和 `IsReadOnly=false`；pair 操作按 key 和默认 value 相等性判断 |

使用 `EqualityComparer<TKey>.Default`，不提供需要跨 Unity 重载持久化的自定义 comparer 构造。CLR null key 不合法；空字符串、数值 0/default 并不非法。与普通泛型 Dictionary 一致，不将 UnityEngine.Object 的 fake-null 自动当 CLR null 来清除条目，不在序列化回调中查询其存活状态；不保证被销毁对象作为持久化键有跨会话意义，持久化身份宜用稳定 ID。

枚举按当前序列化有效行顺序，更新不改变位置、新增追加、删除保持剩余顺序；不承诺排序或跨 serializer 的字节级顺序。有效修改、Inspector 反序列化均使旧枚举器失效。可变 key 须在改变 hash/equality 前 Remove，再重新 Add；容器不轮询对象内部变化，也不宣称线程安全。

### 7.3 数据与算法

1. `datas` 为唯一事实源；一份 `[NonSerialized] Dictionary<TKey,int>` 缓存第一有效行索引，版本及视图同样非序列化。没有第二份 key→value 字典。
2. 首次查询/缓存失效时 O(n) 建索引；使用局部结果完成后再发布，Comparer 意外抛错不发布半建缓存。索引有效时 get/Contains/TryGet 为均摊 O(1)，值直接从对应行读取。
3. setter 修改对应行的 Value；Add 写入新行并维护索引。Remove 处理全部相等行并失效索引，O(n)；Clear 清空行与缓存。只对正常校验失败保证无修改，不声称可以从 OOM 或任意恶意 comparer 全面恢复。
4. `OnBeforeSerialize` 不从缓存复制数据、不压缩/去重、不覆盖列表；无需为了保存运行时更改再创建两份 List。
5. `OnAfterDeserialize` 仅失效索引/诊断/枚举版本，不在回调中遍历 key、调用 Unity API 或清除原始数据；下一次正常访问重建。不以构造函数一定执行为前提。
6. Inspector 通过 SerializedProperty 修改列表，由 Unity 反序列化失效缓存。必须用同 Count 改 key/value、Undo/Redo、JsonUtility overwrite 的真实用例证明该链；未通过时修复此链，不能退回 Count 检测或要求用户手动 Refresh。

### 7.4 无效 Unity 输入与编辑暂态

- 采用**保留原始行、运行时首个有效键优先**：跳过 null 行/CLR null key，后续重复行不参与 Count/查询/枚举；不抛这些预期输入错误，不自动删除行。Inspector 能显示并修复问题。
- 新增 Inspector 行常带 default key，暂时重复不应让 OnBeforeSerialize 擦除用户正在编辑的内容；修改 key 后可成为有效行。
- 运行时合法 Add 仍严格拒绝重复，不能用编辑容错把运行时错误吞掉。setter 不偷偷清理重复行；Remove 明确移除全部同键行，不会把隐藏值重新暴露。
- 缺失/null datas 作为空输入；不涉及双列表长度修复，因为最终格式只有一个列表。普通 comparer 异常不宽泛 catch 后忽略。
- Unity inline 类不保持任意 null、多态或 alias 身份；保留行仅指 Unity 可表达的字段内容，不承诺损坏文件字节级保真或自行恢复丢失引用。未修复的无效 raw 行可继续随 Unity 保存，而导出标准 JSON 只输出有效字典投影，二者不可混同。

### 7.5 JSON 契约

- 利用已核实的 Json.NET `IDictionary` contract，支持可被该后端往返的 string/int/enum 键及相应 value；根容器/记录字段输出裸 JSON 对象，不输出 datas、Dictionary、Keys、Values、Count 或诊断包装。
- JSON 填充经索引器写穿列表，因此读回后再执行 Unity 序列化也不会丢数据；不添加 Json.NET attributes、converter、resolver、.NET callback bridge 或 Foundation DLL 引用。
- 默认 Json.NET 使用字符串属性名。复杂 key 的 `ToString()` 不代表可逆编码；Unity 可序列化不等于 JSON 可保存。复杂键/Unity 对象引用、任意自定义 resolver、类型元数据与保引用设置的保证明确不在本专题。
- JSON 重复名称/不同名称转换成同一个键，沿 Json.NET setter 填充采用后值覆盖；这是 JSON 后端契约，不是 Unity 原始重复行的首项投影。损坏语法/无法转换的 key/value 沿 JSON 异常与现有 Save 错误路径处理。
- `$type/$id/$ref` 等元数据名称及用户设置可能影响标准字典读回；不承诺任意 string key 在任意 Json.NET 设置下无损，也不为了该限制全局改 Save 设置。需要此类业务键时由调用方明确 serializer 配置/DTO，并另验实际 round-trip。
- 不把 Json.NET Populate 解释为原子替换：对已有实例的填充可能合并/中途失败；本专题基线是反序列化到新容器，再由调用方提交，不扩展为通用事务反序列化。

## 8. 必要 Inspector 支撑

推荐 `CustomPropertyDrawer(typeof(SerializedDictionary<,>), true)`，覆盖直接字段和具体子类；先在当前 Unity 验证绑定，不增加全局反射 Drawer 路由或额外运行时基类框架。

- 基于原生 `datas` PropertyField 展示 Key/Value、增删、展开和重排，不另建编辑态字典或自制复杂表格。显示 raw 行数/有效数与 null/重复行诊断；提示第一有效行参与运行时，其他行仍会被 Unity 保留。
- 修改全部走 SerializedObject/SerializedProperty；IMGUI 用 BeginProperty/EndProperty 和原生高度计算，UI Toolkit 通过 PropertyField/binding 支撑默认 Inspector。Unity 2022.3 默认 Inspector 与旧 IMGUI 自定义 Inspector 两条路径都验收。
- 诊断读取当前 SerializedProperty 数据，使用实际 TKey 的默认比较器；在 Editor 内按具体闭合类型做局部反射/比较即可，不新增 Runtime→Editor 依赖、公共非泛型容器接口或 friend assembly。可利用 2022.3 `boxedValue` 的受支持字段访问；不支持的展示类型明确提示，不能用 ToString 比较 key。
- Undo/Redo、Prefab override/apply/revert、保存重开和同 Count 编辑必须更新 Runtime 投影。普通绘制不直接修改运行时缓存，不调用自动 SaveAssets，不重序列化全工程。
- 多对象编辑沿原生 mixed-value；结构/长度不一致时禁用批量增删/重排并明确提示，不把第一个对象的 raw 列表覆盖到所有目标。诊断按目标区分，不能用首个对象推断全部选中对象合法。
- 不加入搜索分页、复制导出、批量修复按钮、持久化 UI 状态、Odin 依赖或阶段 6 全面 Editor 融合。

## 9. 实际兼容影响与迁移策略

| 变化 | 当前影响与推荐措施 |
|---|---|
| Core → Foundation namespace/程序集 | 公开类型身份变化；没有查实源码/资产消费者。移动保留自有 GUID，原包不动；不把 GUID 保留当作 managed reference/type-name 的兼容证明 |
| 缺键 get、重复 Add、外部 Dictionary 出口 | 改为标准契约，退出旧 default-return 和外部借用；没有查实调用迁移，不保留猜测式适配 API |
| datas/Key/Value | 推荐保留布局以降低变更成本；类型归属变化后的 inline 读取仍需代表性 Unity 往返确认，不直接宣布旧资产兼容已通过 |
| keyList/valueList 与旧 Save 类型退出 | 没有查实双列表资产，不能用 FormerlySerializedAs 将两个列表假装转成一个列表；不新增隐藏旧字段或自动兼容 reader |
| 默认 JSON 形状改变 | 推荐新裸字典对象；两旧类型不是可靠默认 JSON 容器，没有已提交旧样本。不是 Save 索引 schema 改动，不改 CurrentDataVersion 或现有存档目录 |
| DictionaryItem 从公共出口退出 | 检索仅定义内使用；不保留独立公开 pair 容器。若实施前发现独立调用则重新评估，不机械删除 |

**当前迁移结论：** 未查实需要迁移的真实资产或存档，因此不预设数据迁移批次、批量 MovedFrom 声明、旧类型 forwarder 或全局兼容 converter。此结论只针对已说明的检索范围，不承诺外部历史用户无迁移成本。

**发现真实数据后的条件分支：** 先取得授权的样本/owner/原类型与程序集身份/serializer 设置，确认是 inline、SerializeReference、UnityEvent、类型名 JSON 还是双列表。备份并制定定向迁移，保留 GUID/fileID/Prefab 关系；双列表须显式转换并确认错位、重复、null 的处置，不静默截短或丢行。默认旧 YL JSON 的 Dictionary 对象可以在有样本时做一次性转换；Core Keys/Values 不能无条件假定包含可恢复的完整数据。只有实际需要时选择精确类型迁移 attribute 或局部工具，并单独获得授权；不修改原游戏资产或用户真实存档。

**许可：** 移动后继续保留 Shared/XFMain 的完整 MIT 与 `Copyright (c) 2021 弦小风`；实施时在现有 `Y/THIRD-PARTY-NOTICES.md` 增量标明该承接位置、来源和 revision。旧 YL 文件没有明确上游头，不凭同名猜成 QFramework 来源。新 Inspector 不复制第三方插件；不机械逐文件补版权模板，不删除仍被吸收代码适用的声明。

## 10. 少量独立实施批次

批次需逐批授权与验收；以下是建议实施顺序，不是本轮执行指令。行为和身份分开，避免一批里同时无法判断数据问题与归属问题。

### 2.5-A 容器行为与两种序列化契约

**前置：** 用户接受第 7 节行为/坏输入/JSON 形状；重新核对工作区及消费者。先保留现有 Core namespace、程序集、文件 GUID 与 datas 布局；旧 Save 未退出。

**范围：** 在 `C/Extensions/SerializedDictionary.cs` 替换双缓存为第 7 节算法，补标准接口及非破坏性 Unity 回调，禁止暴露外部可变字典。在 `T/Core/EditMode/SerializedDictionaryContractTests.cs` / `SerializedDictionaryJsonTests.cs` 补最小契约回归；同批测试 asmdef 可明确加入现有 `Newtonsoft.Json.dll`，产品容器不绑定该 DLL。

**验收：** 关键缺陷先有失败证据，再验证 Add/set/Remove/Clear 保存重载、同 Count overwrite、脏行保留/投影/Remove 不复活、标准接口和真实 Json.NET 形状/往返。Unity 与 JSON 分别报告；手动调用接口回调不是 Unity 真往返的替代。无身份退出时可独立验收行为改善。

**主要风险：** 改公开契约、泛型 Unity 字段支持、索引 invalidation、JSON setter 重入及构造。发现真实调用或数据按第 9 节处理，不静默加兼容层。

### 2.5-B 公共归属与冗余实现退出

**前置：** A 通过、用户确认 Foundation 身份及旧 API/格式退出；复查当前源码/资产/反射及新增消费者。

**范围：** 移动 A 文件/meta 至 `Y/Shared/Serialization/SerializedDictionary.cs`，归 `YLFramework.Foundation`，内部化条目类型；删除旧 `Y/Save/Serialization/SerializedDictionary.cs` 及其 meta，仅在查证无引用后执行。不动 SerializedVector/Color 或普通运行时 Dictionary。

将 A 契约测试/meta 移到已有 `T/Foundation/EditMode/`；JSON 集成测试/meta 移到已有 `T/EditMode/`，对应测试 asmdef 引用现有 DLL，撤回仅供 A 的测试 DLL 引用。复用 `CoreIdentityTests.cs` 的身份/依赖模式补充唯一类型、旧类型退出、Foundation 无 Core/Runtime/Newtonsoft/XF/Editor 依赖断言；不新建测试程序集。

**验收：** 实际 Unity 编译与 A 的受影响测试；GUID 保留、字段形状、唯一归属、Core/Runtime 均可引用新类型。若有查实迁移，必须完成相应样本重开/存档读回才允许退出旧类型；无真实迁移则如实报告“未发现待迁移数据”，不是“迁移测试通过”。

**主要风险：** namespace/程序集的动态身份、许可证归属、测试 overrideReferences 的 DLL 可见性；不能因为同工程有原包就认为容器可独立复用。

### 2.5-C Inspector 与专题收口

**前置：** B 的唯一数据/身份稳定；单独验证 generic/derived Drawer 的当前 Unity 绑定行为。

**范围：** 增加 `E/Serialization/SerializedDictionaryDrawer.cs`，在现有 `E/YLFramework.Editor.asmdef` 直接引用 Foundation；在 `T/EditMode/SerializedDictionaryEditorTests.cs` 验证必要编辑行为。复用原生 Inspector 和现有测试承载，不新增长期场景/Prefab/ScriptableObject 配置系统。

同步现有 `Documentation/Modules/Save.md` 中的旧容器入口说明及有价值的使用约束，并增量记录许可/长期蓝图最终状态；仅在这一实施批次另获授权后修改这些非专项文档，不顺手修无关历史测试数量。

**验收：** UI Toolkit/IMGUI、同 Count key/value 修改、Undo/Redo、重复暂存修复、Prefab override/apply/revert、保存重开、混合多对象安全；跑一次隔离临时目录的真实 SaveManager 写盘/清缓存/读盘验证。复查 Runtime/Editor/DLL/meta/许可差异及原游戏/原包无改动，输出已实现、已验证和未验证边界。

**主要风险：** Native Inspector 与回调时序、非泛型子类/嵌套字段绑定、Prefab/Undo 数据覆盖。未通过的关键编辑链是阻塞，不以默认绘制“看得到列表”算完成。

## 11. 最小充分验证设计

以下仅为未来验收设计，本轮一项也未执行。不要求测试数量、全量历史系统回归或新通用测试设施。

| 验证组 | 必须证明的事实 |
|---|---|
| 运行时标准契约 | duplicate Add 失败不改数据，TryAdd/TryGetValue，missing get 抛错，default/null value，pair Remove/CopyTo/只读视图；更新保持行位置，修改使枚举失效 |
| Unity 真往返 | 含字典的最小 SO/Mono fixture 经 JsonUtility + 保存重开/Instantiate 代表性路径保留 set/add/remove/clear；嵌套 Value 与 UnityEngine.Object 引用仅在 Unity 支持范围验证 |
| Count 不变 | 先构建索引，再 SerializedProperty 同长度换 key/改 value、Undo/Redo、JsonUtility.FromJsonOverwrite；旧 key 消失、新 key 和新值即时正确，不要求手动 Refresh |
| 无效原始输入 | raw 重复/null key/null item/缺失列表的预期投影和诊断；保存不自动删除可表达的坏行；修复 key 后重新有效，Remove 同键全部行且不复活；不新增旧双列表 repair 系统 |
| JSON contract | 当前 Json.NET + 默认及实际 SaveSettings，string/int/enum 代表键、标量/嵌套 POCO 值；根/嵌套字段裸对象，空对象，读取后再 Unity 保存一致；损坏输入及重复 JSON 键沿既定后端契约 |
| 真实 Save 路径 | `CreateSaveItem` → `SaveObject` → 清除缓存 → `LoadObject`；断言真正磁盘内容和新实例，不只比较同一缓存对象。使用独立 temp root，显式文件名，不进入真实用户目录；测试前后恢复原 root override/settings/索引与缓存内存状态，只清理自有临时产物 |
| Inspector/资产 | direct generic + concrete derived、列表中的字典、代表性嵌套值、两 UI 路径、Undo/Redo、Prefab override/apply/revert、多对象 mixed；临时资产仅测试所有，保留并恢复用户未保存场景 |
| 归属与隔离 | Foundation 编译依赖无原系统/JSON/Editor；两旧 YL 类型退出、原 XF 仍原身份、移动 GUID 保留、新 Editor/meta 唯一；JSON 测试引用不污染 Runtime |

Json.NET 的元数据保留名/自定义设置只做与承诺范围相关的代表性边界核对，不为了未支持场景建立 key codec 或修改 Save 后端。端到端 Save 测试若被查实的无关 Save 缺陷阻塞，应分类报告、暂停该项而不是顺带重构 Save；仅 serializer 单测通过不能冒充该项通过。

现有 `T/Core/EditMode/CoreIdentityTests.cs:61–97`、`T/Core/Editor/CoreEditorIdentityTests.cs:33–75,119–176` 可复用身份/GUID/许可断言模式；不为新 Editor 改写 Core 原有依赖白名单。`T/Foundation/EditMode`、`T/EditMode` 已有 asmdef，均 `overrideReferences=true`；实际 Newtonsoft DLL 须明确加入需要的集成测试程序集，不能依赖 IDE 的隐式引用。

纯容器不依赖帧驱动，不机械重跑 Domain Reload、Timer/Pool/Events 全套或原游戏完整流程；无新反射/AOT/Player API 时不默认追加 Player 构建。必要的程序集移动验证以实际 Unity 编译为准，最终跨平台/IL2CPP/无原 15 系统的整框架独立验收仍归既有后续范围，不能用本专题替代。

## 12. 审阅决策与停止点

需要用户确认的是三项实质决策，不是索引维护/Drawer 布局等普通内部细节：

1. **唯一中立类型及身份退出：** 推荐 Foundation 归属，旧 Core/Save 容器与公共 DictionaryItem 退出，不保留没有查实依赖的兼容 facade。
2. **公开行为与无效 Unity 数据：** 推荐标准 Dictionary 操作契约；Inspector/Unity 原始脏行保留、首个有效项参与运行时并给出诊断，不自动清理或在回调里抛预期输入错误。
3. **JSON 与迁移边界：** 推荐标准 IDictionary 裸对象，接受与两旧 POCO 形状不同；本轮未查实真实迁移，不预建旧格式兼容，不改变 Save 后端/全局设置。若后续发现真实数据，先补有证据的迁移并另获授权。

本轮完成后停止，等待方案审阅与具体批次授权；不进入 2.5-A，不 Commit/Push/Merge/Tag，不更新 Runtime、Editor、测试、资源、程序集、项目配置或其他文档。

## 13. 批准记录

- **2026-10-11：用户审阅通过 2.5 总体技术方案**，确认第 12 节三项重大决策及 A→B→C 实施路线。本节更新方案当前状态；第 12 节末尾的停止记录保留为该研究轮的历史过程记录，不再代表当前状态。
- **批准范围**：架构方向（Foundation 唯一中立类型、以序列化条目为事实源、标准 `IDictionary` 接入的设计方向）、公开行为契约（标准字典操作与无效 Unity 数据的保留/诊断方向）、兼容边界（Json.NET 裸对象、未查实迁移时不预建旧格式兼容、不改 Save 后端与全局设置）、批次路线（A 容器行为与序列化契约 → B 公共归属与冗余实现退出 → C Inspector 与专题收口）。
- **不强制具体内部实现细节**：Inspector 缓存失效机制及泛型 Drawer 绑定方式，由实施时依据 Unity 实际验证结果自主调整，不预设必须采用本文某一种实现；同一批准方向内的具体算法、字段与接口细节允许实施时调整并如实记录。
- **批次逐批推进**：A/B/C 仍需逐批实施、验收与 Git 收口，逐批另行授权；本次批准不构成任何批次的实施授权或 Git 权限，不自动启动 2.5-A。
- 既有源码研究、证据、风险分类与验证设计要求继续有效；实施中若发现真实消费者或数据，按第 9 节条件分支处理（暂停对应退出并补充有样本迁移方案）。

## 14. 2.5-A 实施与验证记录

### 14.1 授权、基线与范围

- 2026-10-11 用户单独授权正式实施 A。开始时实际分支 `fusion/2.5-serialized-dictionary`，HEAD `e6ba5e4`，工作区干净。定向复查 `Assets` C#，两份 YL 容器及 `DictionaryItem` 仍只有自身定义/内部使用，没有发现新的源码消费者。
- 产品修改仅限 `C/Extensions/SerializedDictionary.cs`；新增 `T/Core/EditMode/SerializedDictionaryContractTests.cs`、`SerializedDictionaryJsonTests.cs` 和共用的 `SerializedDictionaryTestAsset.cs`，Unity 为三份脚本生成独立 meta。只在既有 Core EditMode 测试 asmdef 中加入 `Newtonsoft.Json.dll`。
- 保留 `YLFramework.Core.SerializedDictionary<TKey,TValue>`、`YLFramework.Core.Runtime`、原脚本路径/GUID `1418cb45915d1504e859f117b32ade29`、私有 `List<DictionaryItem<TKey,TValue>> datas` 及 `Key/Value` 字段。`DictionaryItem` 本批仍公开，内部化留给 B。旧 Save 容器保持不变。
- 未移动身份、修改 Core Runtime asmdef、新增产品 JSON 依赖、修改 SaveManager/后端、修改原游戏或原包，也未进行 Commit/Push/Merge/Tag/Reset/Clean 或分支切换。

### 14.2 实际实现与取舍

- `datas` 是唯一事实源；删除两个键到值/条目缓存，改为一份非序列化 `Dictionary<TKey,int>` 首项行索引。索引按失效状态延迟重建，以局部完整结果发布，不再用 Count 判断有效性。
- 实现 `IDictionary<TKey,TValue>`、`IReadOnlyDictionary<TKey,TValue>`、`ISerializationCallbackReceiver`、复制来源构造、`TryAdd/TryGetValue`、pair 操作和 `CopyTo`。setter 更新首个有效行或追加新行，Add 严格拒绝重复；缺键 get 抛 `KeyNotFoundException`；所有键入口只拒绝 CLR null。
- Keys/Values 是不可修改的 live view，没有可变字典/原始列表出口。查询、Count、ContainsValue、视图及枚举只使用有效唯一投影；有效行顺序保留。`InvalidEntryCount` 诊断 null 行/CLR null 键/重复行，不记录错误日志或自动清理。
- setter 保留重复行；Remove 删除同键全部行，Clear 清空全部原始行。对正常重复/null 校验失败不改已有数据。缺失/null `datas` 按空输入处理；不会轮询可变 key 内部状态，也不宣称线程安全。
- `OnBeforeSerialize` 不做数据复制或清理；`OnAfterDeserialize` 只清除索引/诊断缓存并递增枚举版本，不遍历键或调用 Unity 对象 API。实际 Unity 编辑/overwrite/Undo 路径已支持此机制，无需额外 hash 扫描、Count 检测、手动 Refresh 或 Editor 回调桥接。
- 独立只读审查发现 `yield` 枚举器在结束后不再检查版本。补充 `ExhaustedEnumeratorsStillDetectSubsequentMutation`，先实际观察失败，再改为共享的显式枚举器；pair/Keys/Values 在首次移动前、移动期间和结束后均检查版本。反序列化后旧 live view 更新、旧枚举器失效也有直接覆盖。
- Json.NET 使用标准字典 contract，经 setter 写穿列表；无 attributes/converter/resolver/.NET callback bridge。当前 Core 的其他模块（HttpManager、StartUpModuleRequest）本来已引用 Newtonsoft，因此本批不能将“整个 Core 无 Newtonsoft 依赖”作为断言；容器自身不绑定 JSON DLL，Foundation 无此依赖的程序集断言留给 B。

### 14.3 本轮真实验证

验证通过已打开的 Unity `2022.3.62f3c1` Editor/MCP 进行，没有启动第二个 Unity 实例，没有以辅助 dotnet 编译代替 Unity。所有 Unity 往返用例均调用真实序列化/编辑 API，而不是手动调用容器回调。

| 验证 | 本轮结果与证据 |
|---|---|
| 修复前失败证据 | 原实现的 `IndexerWritesSurviveRealUnityJsonRoundTrip` 恢复旧值 1 而非 2；SerializedProperty 同 Count 编辑及 Unity overwrite 均保留旧键；默认 Json.NET 写出 `Keys/Values/Count` 而非 `{"a":1}`。四项在产品修改前实际失败，修改后通过 |
| Unity 实际编译 | `assets-refresh(ForceSynchronousImport)` 最终返回 Success；新容器及测试在 Unity 中编译后执行。测试扩充过程中出现过 using 位置编译错误，已修正并重新编译/运行，不沿用失败状态 |
| 运行时与身份 | 标准操作/null/default/重复/缺失键、复制所有权、pair/CopyTo、只读 live view、有效顺序、失败操作不失效枚举、有效修改失效枚举、全部重复 Remove、全部 raw Clear，以及 namespace/assembly/GUID/序列化布局通过 |
| Unity 编辑与缓存 | 直接泛型字段和具体非泛型子类，SerializedProperty 同长度改 key/value、单改 value、Undo/Redo、JsonUtility.FromJsonOverwrite 同 Count、旧视图及枚举器行为通过；无需手动失效调用 |
| Unity 保存恢复 | 测试独占临时 SO 资产，运行时 set/add/remove/clear 后 SaveAssetIfDirty、UnloadAsset、ForceUpdate 导入并 LoadAsset，断言不是原 managed 实例；恢复的有效行、具体子类、嵌套值和 Unity 对象自引用通过。Instantiate 后独立修改以及 Clear 后 JsonUtility 恢复为空也通过；仅删除测试自己的临时资产 |
| 无效 raw 数据 | CLR null 行/键通过定向注入验证保留、计数及有效投影；Unity 可表达的重复行经真实 JSON 保存恢复仍保留，SerializedProperty 修复后即时有效，Remove 不复活。不能把 CLR 注入当成 Unity 任意 inline null 的保真证明 |
| Unity fake-null 实测 | Object key 的 CLR null 经 JsonUtility 往返恢复为 CLR 非 null、Unity `== null` 为 true 的包装对象：原三行（null/同对象重复）从有效 1、无效 2 变为有效 2、无效 1，raw 行数仍为 3。测试明确断言此实际转换；按已批准契约，fake-null 键仍合法且不会被自动清除。另验已销毁 Unity 对象键也按 CLR 非 null 处理。这不构成跨会话对象键持久化保证 |
| Json.NET 实际往返 | 当前安装 Json.NET 的默认设置及只读使用真实 `SaveManager.SerializerSettings`：根/记录字段裸对象，string/int/enum 键、null/嵌套 POCO 值、空对象、读取后修改/再次写出，再转 Unity 保存恢复全部通过；没有调用 SaveManager IO、索引或缓存路径 |
| JSON 边界 | 重复名称与 `"1"/"01"` 转换为同键均后值覆盖，无 Unity 重复行；无法转换键及截断 JSON 实际抛 `JsonSerializationException`，没有吞异常或更改后端设置 |
| 最终定向回归 | `tests-run(testMode=EditMode, testAssembly=YLFramework.Core.Tests.EditMode)` 最终 **58 passed、0 failed、0 skipped**，含新增 **23** 项容器/JSON 用例及既有 Core EditMode 回归。工具的 `TotalTests=1246` 是全工程发现数，不是本次执行数；不将其报告为全工程通过 |

### 14.4 完成边界与待验收事项

- A 的技术完成条件已达到，可作为 B 的技术前置；仍需用户验收和 Git 收口，未自动启动 B/C。
- 行为变化明确：缺键不再返回 default，Add 不再接受重复，Keys/Values 公开类型从具体 Dictionary collection 改为只读 live `ICollection`，Json.NET 从旧 POCO 包装改为裸字典对象。未发现需迁移的源码消费者，本批不为未知历史数据增加兼容层。
- 未执行 Foundation 身份迁移/旧 Save 退出、正式 Drawer/UI Toolkit/IMGUI、多对象/Prefab override 链、真实 SaveManager 写盘端到端、Player/IL2CPP 或完整游戏测试；分别留给 B/C 或既有后续范围。复杂 JSON key、自定义元数据/保引用设置、任意 inline null/多态/alias 保真仍在既有保证范围之外。
- Unity 刷新/测试期间工作区另外出现既有 `T/Core/Fixtures/YLCoreSmokeModule/YLCoreSmokeModule.asset` 三处 `filter:` 尾空白差异（无字段值变化）。该资产未由本轮手动编辑，来源未进一步断定；按保护并行/现有改动约束保留，不纳入 A 交付改动。全工作区 `git diff --check` 因这三处差异报尾空白；A 的源码/测试/专项方案定向差异检查无此问题。
- 本批没有真实历史资产/存档迁移样本，不能把测试临时资产往返称为历史数据迁移完成。原 XF/XFAB 与原游戏资源保持原 owner。

## 资料依据

- Unity [2022.3 Script Serialization](https://docs.unity3d.com/2022.3/Documentation/Manual/script-Serialization.html)、[ISerializationCallbackReceiver](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ISerializationCallbackReceiver.html)：字段规则、Inspector 反序列化、回调线程与 inline 限制。
- Unity [PropertyDrawer](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PropertyDrawer.html)、[ApplyModifiedProperties](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.ApplyModifiedProperties.html)、[boxedValue](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedProperty-boxedValue.html)：双 UI 路径、Undo/回写和访问限制；open generic Drawer 的实际绑定仍须实现阶段验证。
- Unity [FormerlySerializedAs](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Serialization.FormerlySerializedAsAttribute.html)、[FromJsonOverwrite](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/JsonUtility.FromJsonOverwrite.html)：字段改名不能代替格式/类型迁移，overwrite 应区分缺失字段保留与清空。
- Json.NET 官方 [Serialization Guide](https://www.newtonsoft.com/json/help/html/SerializationGuide.htm)、[Serialization Callbacks](https://www.newtonsoft.com/json/help/html/SerializationCallbacks.htm)，以及 [13.0.2 DefaultContractResolver](https://github.com/JamesNK/Newtonsoft.Json/blob/13.0.2/Src/Newtonsoft.Json/Serialization/DefaultContractResolver.cs)、[CollectionUtils](https://github.com/JamesNK/Newtonsoft.Json/blob/13.0.2/Src/Newtonsoft.Json/Utilities/CollectionUtils.cs)、[JsonSerializerInternalReader](https://github.com/JamesNK/Newtonsoft.Json/blob/13.0.2/Src/Newtonsoft.Json/Serialization/JsonSerializerInternalReader.cs)。通用官方说明以版本源码及当前包配置核对，不作为已运行结果。
- 当前可调用工具没有 Context7 查询能力；未声称已获得 Context7 结果，使用现有源码、已安装包元数据及上述版本匹配的一手资料。
- A 实施时 Context7 已可调用：查询 `/websites/unity3d_manual` 的序列化回调资料，但返回没有明确的 2022.3 版本限定；仅作辅助说明，以当前 Editor 的真实测试作为本批回调/缓存结论。上一条是研究轮工具能力的历史记录。

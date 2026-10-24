# ZipTool（Zip / GZip 工具）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供与 Unity 解耦的压缩解压能力：目录打包为 zip、解压 zip 到目录、提取单个条目、GZip 压缩 / 解压，并沿用现有内置 SharpZipLib 源码作为实现基础。本轮只升级 YL 自有包装身份，不改第三方子树的 namespace、源码或许可证。

## 位置

- 运行时：[`Runtime/Utilities/ZipTool/`](../../Runtime/Utilities/ZipTool/)
- 对外封装：[`Utilities/`](../../Runtime/Utilities/ZipTool/Utilities/)；内置库：[`ICSharpCode.SharpZipLib/`](../../Runtime/Utilities/ZipTool/ICSharpCode.SharpZipLib/)
- 自有包装 namespace：`YLFramework.Compression`，消费者使用 `using YLFramework.Compression;`；内置 SharpZipLib 子树仍保留现有 `YLFramework.Utilities.ZipTool.*` 身份。两者归 `YLFramework.Runtime` 主体，不新增 DLL 或旧 namespace 转发壳；工具源码保持 BCL-only，不等于整个主体 DLL 无引擎依赖。主体已解除 XFAB 编译依赖。
- 常规业务直接使用 `ZipUtility` / `GZipUtility`，多次读取同一归档使用 `using` 管理 `ZipArchiveReader`；无需 Manager、组件挂载或公共 Root。

## 主要功能

- `ZipUtility`：`ZipFolder`（可按过滤规则打包；输出位于源目录内或已存在时经临时文件打包后替换）、`UnzipToDirectory`（覆盖 / 进度回调 / 编码参数 / 配额）、`ExtractEntry`（提取单个条目）。
- `ZipArchiveReader`：会话式读取 zip（一次打开多次读取，`IDisposable`），解压前预扫描配额与 Zip Slip 路径。
- `UnzipOptions`：解压配额（总大小 / 单条目大小 / 条目数量，`ForNetworkDownloads` 预设）；0 = 不限制。
- `GZipUtility`：字节数组 / 流的 GZip 压缩与解压（永不关闭调用方流，支持 0 字节数据）。
- 内置 `ICSharpCode.SharpZipLib`：保留既有 `YLFramework.Utilities.ZipTool.*` namespace、SharpZipLib Contributors 来源声明与完整 MIT，不随自有 Compression 包装重命名。

## 主要类型

- `ZipUtility`：zip 打包 / 解压入口。
- `ZipArchiveReader`：会话式 zip 读取器。
- `UnzipOptions`：解压限制选项。
- `GZipUtility`：GZip 工具。
- `CodePageScope`（internal）：zip 条目名编码切换辅助。

## 关键语义

- **Zip Slip**：所有写文件入口都不会逃逸目标目录。`UnzipToDirectory` 在写出任何文件前对全部条目做 `Path.GetFullPath` 规范化前缀校验，越界抛 `InvalidDataException` 且不创建目标目录；绝对路径 / 盘符条目会先被内置库 `ZipEntry.CleanName` 归一为相对路径；`ExtractEntry` 的目标路径完全由调用方指定，条目名不参与拼接。
- **CRC 校验**：普通解压不校验 CRC（内置库 `GetInputStream` 行为）；需要完整性校验时使用 `ZipUtility.VerifyArchive`（`testData=true` 完整校验 CRC，损坏 deflate 数据返回 false）。
- **配额（quotas）**：`UnzipOptions` 三项限制 0 = 不限制，负值抛 `ArgumentOutOfRangeException`；先按条目元数据预扫描，拷贝期间再按实际写出字节复核，元数据说谎仍会被拦截。超限抛 `InvalidDataException` 并停止解压。
- **失败不破坏已有文件**：解压单个文件先写同目录临时文件、成功后再替换目标；中途失败（配额 / 数据损坏 / IO）不会截断已有文件，也不残留临时文件。
- **编码 / 线程安全**：`nameCodePage` 通过 `CodePageScope` 临时修改进程级静态 `ZipConstants.DefaultCodePage`，调用结束（含异常）后恢复，嵌套按 LIFO；**非线程安全**，不保证并发调用。
- **进度回调**：值为已处理字节 / 总字节，范围 0..1，存在回调时最后一次必为 1（空 zip 回调一次 1）；回调异常原样抛出，不吞。
- **GZip stream ownership**：`GZipUtility` 不 Dispose 调用方传入的输入 / 输出流。
- **输出 zip 自打包**：`ZipFolder` 的输出位于待压缩目录内部或输出已存在时，先打包到系统临时文件再复制到目标，避免把正在写出的 zip 打包进自身，也避免失败时破坏已有输出。

## 依赖

- Zip源码无框架内模块或UnityEngine/UnityEditor依赖；`ZipToolSourceTests` 独立编译全源码，仅接受Unity安装目录Mono/NetStandard下BCL参考，排除主体、Engine、Editor、测试与XF DLL。合入主体不撤销这条源码边界，也不承诺模块独立裁剪交付。
- 旧Resource的ZipFileHelper随旧栈退役，不再通过它读取旧二进制表或APK内配置；内置ZipFile等低层接口仍可供明确调用方使用，不因此改变通用解压门面的职责，也不宣称已验证所有平台的资源交付。
- `Tests/EditMode/Editor/Distribution/ExportUnityPackageTests.cs` 直接使用内置 GZip / Tar 读取 Unity 导出包。内置低层类型继续保留，避免破坏现有消费者；BCL-only 探针为 `Tests/EditMode/Utilities/ZipTool/ZipToolSourceTests.cs`。

## 分发

默认导出递归包含 `Runtime/`，因此 ZipTool 的封装、内置库及原 meta 一并归 Runtime 主体分发，不恢复已退役的独立 asmdef；来源声明与 [框架许可证](../../LICENSE.md) 一同保留。不需要额外安装 SharpZipLib Package 或供应商 DLL，不可只导出封装而漏掉内置实现。此结论仅针对 ZipTool，不表示整个框架已完成当前新身份的独立分发验收。

## 当前状态

成熟实现保持不变。当前仓库没有文档旧列出的 `Tests/EditMode/ZipToolTests.cs`，不作为可运行入口或本批验证证据；实际导出边界入口为 `Tests/EditMode/Editor/Distribution/ExportUnityPackageTests.cs`，不等同于 Zip / GZip 的全量算法验证。Android APK 的实际读取验证留对应平台 / Resource 专题。

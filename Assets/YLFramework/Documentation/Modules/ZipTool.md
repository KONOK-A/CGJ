# ZipTool（Zip / GZip 工具）

> [文档首页](../README.md) · [架构总览](../Architecture.md)

## 作用

提供与 Unity 解耦的压缩解压能力：目录打包为 zip、解压 zip 到目录、提取单个条目、GZip 压缩 / 解压，并内置改写命名空间后的 SharpZipLib 源码作为实现基础。

## 位置

- 运行时：[`Runtime/Tools/ZipTool/`](../../Runtime/Tools/ZipTool/)
- 对外封装：[`Utilities/`](../../Runtime/Tools/ZipTool/Utilities/)；内置库：[`ICSharpCode.SharpZipLib/`](../../Runtime/Tools/ZipTool/ICSharpCode.SharpZipLib/)

## 主要功能

- `ZipUtility`：`ZipFolder`（可按过滤规则打包；输出位于源目录内或已存在时经临时文件打包后替换）、`UnzipToDirectory`（覆盖 / 进度回调 / 编码参数 / 配额）、`ExtractEntry`（提取单个条目）。
- `ZipArchiveReader`：会话式读取 zip（一次打开多次读取，`IDisposable`），解压前预扫描配额与 Zip Slip 路径。
- `UnzipOptions`：解压配额（总大小 / 单条目大小 / 条目数量，`ForNetworkDownloads` 预设）；0 = 不限制。
- `GZipUtility`：字节数组 / 流的 GZip 压缩与解压（永不关闭调用方流，支持 0 字节数据）。
- 内置 `ICSharpCode.SharpZipLib`：命名空间统一为 `YLFramework.Utilities.ZipTool.*`，随源码维护；来源与 MIT 许可见 [`THIRD-PARTY-NOTICES.md`](../../Runtime/Tools/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md)。

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

- 无框架内模块依赖；asmdef 声明 `noEngineReferences: true`，不引用 UnityEngine / UnityEditor。
- 被依赖：`Resource` 模块的 `ZipFileHelper` 使用其读取 StreamingAssets 中的 zip。

## 当前状态

已完成。测试：`Tests/EditMode/ZipToolTests.cs`。

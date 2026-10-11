ColorSelector 正式工具演示

ColorSelectorSample.prefab 已绑定 Assets/YLFramework/Prefabs/ColorSelector.prefab。
在已有场景中放置 Sample；可选绑定 uiRoot Canvas。场景已有 EventSystem 时应配置匹配
输入后端的有效模块；没有有效系统时由统一 UI 入口自建，不是 Sample 的 Inspector 字段。
未绑定 uiRoot 时沿 Core 默认根。不要重复放置多个相同 Sample Module 宿主。
TestColorSelector 只创建演示 Module、打开正式工具和消费颜色回调；可选 preview Renderer
仅使用演示自己的材质实例，宿主结束后释放并恢复原引用。

正式 API：YLFramework.Core.ColorSelectorWindow.Arguments(initialColor, callback)，
WindowCatalog.Open<ColorSelectorWindow>(owner, key, parent, args)，CurrentColor 和 Close()。
RGB 为 SDR [0,1]；不新增 Alpha 编辑、数值/Hex 或确认取消流程。
完整配置与生命周期见 Documentation/Modules/UI.md。

历史来源署名（原实现已退出，仅保留来源记录，不构成许可）：
CodeArtist.mx 2015
Code: Rodrigo Fernandez Diaz
http://codeartist.mx/

未确认原代码、Shader、素材和布局的框架再分发授权，因此没有直接提升它们到 Runtime。
原文件和完整原 Readme 可由 Git 历史追溯。正式工具及 Prefab 按 HSV 功能契约独立重建，
使用标准 UGUI 网格/材质；此 Sample 不分发或重新许可 CodeArtist 原实现。
详见 Runtime/THIRD-PARTY-NOTICES.md。

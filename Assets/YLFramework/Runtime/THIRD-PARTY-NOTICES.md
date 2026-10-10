# Third-Party Notices

本文档集中记录 YLFramework 运行时源码中包含的第三方开源代码及其许可信息；完整许可文本随 YLFramework 一并分发。

## QFramework（liangxiegame）

以下目录包含源自或改编自 [QFramework](https://github.com/liangxiegame/QFramework) 的代码：

- `Runtime/Action/`：ActionKit 及其执行、组合、屏幕过渡与 Observable 适配。
- `Runtime/Audio/`：AudioKit 及音频管理、配置与诊断。
- `Runtime/Shared/Timing/`：统一 Timer 内核、内部索引堆与句柄收集；旧 `Runtime/Tools/Timing/` 的适用来源声明继续保留。
- `Editor/Audio/`、`Editor/Action/Help/`、`Editor/Help/`：基于 ActionKit / AudioKit API 的帮助内容与窗口适配。
- `Integrations/DOTween/`：ActionKit 的 DOTween 集成适配（不随默认导出；DOTween 本身适用 Demigiant 的独立 Artistic 许可）。

版权声明：

- Copyright (c) 2015 - 2025 liangxiegame
- Copyright (c) 2017 snowcold、Copyright (c) 2017 liangxie（Timing 等部分文件）
- Timer 的堆调度实现参考 [UnityTimer](https://github.com/akbiggs/UnityTimer)：Copyright (c) 2016 Alexander Biggs & Adam Robinson-Yu

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

## 其他第三方组件

- `Runtime/Tools/Json/`：JSONObject（Matt Schoen / Defective Studios，MIT），完整文本见该目录 `LICENSE.md`。
- `Runtime/Tools/ZipTool/`：ICSharpCode.SharpZipLib（MIT），完整文本见该目录 `THIRD-PARTY-NOTICES.md`。
- `Plugins/` 下的第三方编辑器插件随附各自的说明材料，其许可与使用条件以插件自身提供为准。

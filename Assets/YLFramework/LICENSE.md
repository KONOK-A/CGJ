MIT License

YLFramework 的资源构建内核参考 XFABManager 1.24.0 的显式 BuildMap 与依赖者集合分包机制，
按自有资源定义、隔离 Workspace 和实际 Unity 约束重新实现，不调用原 XFAB 构建流程。
XFABManager 来源：https://gitee.com/xianfengkeji/xfabmanager
Editor平面列表承接XFABManager revision `c35dc4058e` 的TreeView行构建及索引重排流程，
局部修复空项/重复项、Undo与窗口隔离，不引入原资源系统。
`Runtime/Systems/Excel/`的字段标记/枚举及自定义转换边界承接XF ExcelSystem 1.0.7，
revision `725f94f23327de190111573870146af66e9484b6`，来源：
https://gitee.com/xianfengkeji/xfgame-framework.excel-system
仅包含字段契约，不分发其EPPlus二进制或宣称提供xlsx后端。
相关上游声明及完整 MIT 许可保留如下。

Copyright (c) 2021 弦小风
Copyright (c) 2025 弦风课堂

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

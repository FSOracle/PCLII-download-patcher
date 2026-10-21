# PCL2 下载功能恢复补丁

- **补丁版创作者：FS**
- **适用版本：Plain Craft Launcher 2 正式版 2.99.0.0（413）**
  查看方法：PCL → 更多 → 关于与鸣谢 →"Plain Craft Launcher 2"一行的"当前版本"

PCL2 从这个版本开始关闭了 Minecraft 下载功能，这个补丁可以把它恢复回来：

- ✅ 下载 → 自动安装：版本列表恢复，可以正常下载、安装 MC（以及 Forge / Fabric / NeoForge / OptiFine 等）
- ✅ 整合包安装恢复：可以拖进窗口安装，也可以点按钮选择文件安装
- ✅ 更多 → 关于与鸣谢：增加一行"FS — 补丁版创作者"

> 仅供个人在可合法使用 Minecraft 的地区自用。游戏文件来自 Mojang 官方源或 BMCLAPI 镜像，正版登录不受影响。

---

## 压缩包里有什么

├─ 补丁版/
│  └─ Plain Craft Launcher 2.exe    ← 已经打好补丁的完整 PCL，可以直接用
└─ 补丁工具/
   ├─ 一键打补丁.bat                ← 给自己的原版 exe 打补丁用
   ├─ patch.ps1                     ← 一键脚本本体（备份 → 打补丁 → 替换）
   ├─ run.ps1                       ← 编译并运行补丁程序
   ├─ PclPatcher.cs                 ← 补丁源码
      ├─ dnlib.dll                  ← IL 读写库（MIT 许可）
      └─ dnlib-LICENSE.txt
```

**两种用法任选一种就行，效果完全一样**（工具打出来的 exe 和"补丁版"里的 exe 逐字节相同）。

---

## 方法一：直接替换（最简单）
> 把release版exe替换进去即可
> 这个 exe 只对应 2.99.0.0（413）。如果你的 PCL 是别的版本，请改用方法二。

---

## 方法二：一键打补丁工具

适合这些情况：想自己给 exe 打补丁，或者 PCL 更新后需要重新打。

1. **关闭 PCL。**
2. 把压缩包**完整解压**（不要在压缩软件里直接双击运行）。
3. 进入 `补丁工具` 文件夹，任选一种方式：
   - **拖拽**：把你 PCL 文件夹里的 **原版** `Plain Craft Launcher 2.exe` 直接拖到 `一键打补丁.bat` 图标上；
   - **双击**：双击 `一键打补丁.bat`，在弹出的窗口里把 exe 拖进去，然后按回车。
4. 等待窗口提示 **"补丁完成！"**，然后按任意键关闭窗口。

工具会自动完成这些事：
- 检查 PCL 是否还在运行（还在运行就停止，不做任何修改）；
- 把原版备份成同目录下的 `Plain Craft Launcher 2.exe.bak`（如果 `.bak` 已经存在，就带时间戳另存一份，不会覆盖旧备份）；
- 打补丁，并替换原文件。

**常见提示：**

| 提示 | 含义 | 怎么办 |
|---|---|---|
| `PCL 正在运行` | PCL 没关 | 关闭 PCL（包括任务栏托盘）后再运行 |
| `未找到预期的 IL 指令` | ① 这个 exe 已经打过补丁；② PCL 版本不是 2.99.0.0（413） | ① 不需要再打；② 这个补丁暂时不支持该版本 |
| 窗口一闪就关 / 提示脚本被禁止 | 杀毒软件或系统策略拦截了 PowerShell | 允许后重试，或改用方法一 |

**以上任何失败，原文件都不会被修改。**

运行环境：Windows 10 / 11 自带的 PowerShell 5.1 即可，**不需要安装 .NET SDK 或其他东西**。

---

## 验证补丁是否生效

1. **下载 → 自动安装**：能看到 Minecraft 版本列表，选一个版本，点"开始安装"能正常下载。
2. **整合包**：把 `.zip` / `.mrpack` 整合包拖进 PCL 窗口，能正常开始安装。
3. **更多 → 关于与鸣谢**："关于"卡片最后一行显示 **FS — 补丁版创作者**。

如果出现问题，打开 PCL 文件夹下的 `PCL\Log1.txt` 查看日志：加载版本列表时会有 `DlClientList Main` 相关记录，安装整合包时会有 `整合包种类：xxx` 记录。

---

## 还原为原版

1. 关闭 PCL。
2. 删除（或改名）补丁版 `Plain Craft Launcher 2.exe`。
3. 把 `Plain Craft Launcher 2.exe.bak` 改名回 `Plain Craft Launcher 2.exe`。

---

## 注意事项

- **PCL 自动更新会覆盖补丁版。** 更新后，用方法二对新的 exe 再打一次。如果新版本改了拦截方式，工具会报错并停止，不会把 exe 改坏。
- 补丁版 exe 的哈希和官方原版不同，属于正常现象。
  - 补丁版 SHA-256：`6d340135987bcf5f0b1895dd666780e73ef4bce47eee65f27c40ad8e389ff228`
  - 对应原版 SHA-256：`6b6e1260cb5a1d6a303855f1c60cab3d0bc1aebba7e40a964180188b271e7386`
- 补丁只改了 PCL 本体 4 个方法的代码，不会新增任何网络请求、后台程序或文件。

---

## 原理（给想了解细节的人）

### 拦截方式

PCL 本体是 VB.NET / WPF 程序，没有混淆。反编译后全局搜索提示语"为遵守相关法律法规，PCL 暂时不再提供 MC 下载功能"，可以找到 3 处拦截：

| 位置 | 拦截方式 |
|---|---|
| `PageDownloadInstall.LoaderInit()` | 版本列表加载器被换成一个名叫 `"关闭下载"` 的假任务，一运行就抛出上面那句提示 |
| `ModModpack.ModpackInstall(File, …)` | 识别出整合包类型后，不再交给安装器，而是直接 `throw` |
| `ModModpack.ModpackInstall()` | "安装整合包"按钮的函数体被清空，只弹一个红色提示 |

**下载功能本身并没有被删除**：`ModDownload.DlClientListLoader`（BMCLAPI / Mojang 双源版本列表）、`McInstallStart` 安装流程、7 个整合包安装器，以及"选择文件后安装"那个线程函数（VB 编译器生成的闭包 `_Closure$__0-0._Lambda$__0`），全都完整保留在程序里，只是入口被切断了。"版本修改"页面至今还在使用同一个 `DlClientListLoader`。

### 补丁做了什么

补丁用 [dnlib](https://github.com/0xd4d/dnlib) 直接修改 IL，写回时保留原有的元数据 token（`PreserveAll`），WPF 界面资源（BAML）不受影响。

**① 恢复版本列表**

```diff
- PageLoaderInit(…, new LoaderTask("关闭下载", _ => throw new Exception("为遵守相关法律法规…")), …)
+ PageLoaderInit(…, ModDownload.DlClientListLoader, …)
```

**② 恢复整合包分发**

```diff
-     throw new Exception("为遵守相关法律法规…");
+     switch (packType) {
+         case 0: return InstallPackCurseForge(File, archive, baseFolder, InstanceName, Logo);
+         case 1: return InstallPackHMCL(File, archive, baseFolder);
+         case 2: return InstallPackMMC(File, archive, baseFolder);
+         case 3: return InstallPackMCBBS(File, archive, baseFolder, InstanceName);
+         case 4: return InstallPackModrinth(File, archive, baseFolder, InstanceName, Logo);
+         case 9: return InstallPackLauncherPack(File, archive, baseFolder);
+         default: return InstallPackCompress(File, archive);
+     }
```

类型编号和原有的识别代码一一对应：`manifest.json` → 0，`modpack.json` → 1，`mmc-pack.json` → 2，`mcbbs.packmeta` 或带 addons 的 manifest → 3，`modrinth.index.json` → 4，内含 `modpack.zip/mrpack` → 9。

两个实现细节：
- 内层 try 的各个 `leave` 都跳到原来的 `throw` 语句，所以这条语句的第一条指令是原地改写的，原有跳转全部保持有效。
- 返回值通过 `leave` 带出 try；外层负责释放 ZipArchive 的 `finally` 照常执行。

**③ 恢复"选择整合包文件"**

```csharp
c.File = Dialogs.SelectFile("选择整合包压缩文件", false, null,
         "整合包文件(*.rar;*.zip;*.mrpack)|*.rar;*.zip;*.mrpack").FirstOrDefault();
if (string.IsNullOrEmpty(c.File)) return;
ModBase.RunInThread(c._Lambda$__0);   // 原有闭包：后台安装，出错弹窗
```

**④ 署名**

新增一个方法 `PageOtherAbout.FsAddPatchCredit()`，在"关于与鸣谢"页面首次加载时，往"关于"卡片的表格末尾追加一行 `FS — 补丁版创作者`，样式和上面几行一致。

### 安全检查

补丁每改一处之前都会先确认：指令序列、局部变量类型、跳转关系、安装器参数个数都和预期完全一致。只要有一项不符就中止，不写出任何文件。

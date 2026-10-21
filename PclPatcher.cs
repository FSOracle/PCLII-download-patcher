// 恢复 PCL2 (2.99.0.0 / 413) 被移除的 MC 下载入口。
// 仅修改 3 个方法的 IL，原始实现（DlClientListLoader、InstallPack*、整合包线程 lambda）均仍在程序集中。
// 需要 C# 5（PowerShell 5.1 Add-Type）。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

public static class PclPatcher
{
    const string BlockMessage = "为遵守相关法律法规，PCL 暂时不再提供 MC 下载功能，非常抱歉 qwq……";

    public static void Run(string input, string output)
    {
        ModuleDefMD mod = ModuleDefMD.Load(input);

        TypeDef modDownload = FindType(mod, "PCL.ModDownload");
        TypeDef pageInstall = FindType(mod, "PCL.PageDownloadInstall");
        TypeDef modModpack = FindType(mod, "PCL.ModModpack");
        TypeDef modJava = FindType(mod, "PCL.ModJava");
        TypeDef modBase = FindType(mod, "PCL.ModBase");

        PatchVersionListLoader(pageInstall, modDownload);
        PatchModpackDispatch(modModpack);
        PatchModpackSelectFile(mod, modModpack, modJava, modBase);
        PatchAboutCredit(mod, FindType(mod, "PCL.PageOtherAbout"), FindType(mod, "PCL.MyListItem"));

        ModuleWriterOptions opts = new ModuleWriterOptions(mod);
        opts.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
        mod.Write(output, opts);
        Console.WriteLine("已写出：" + output);
    }

    // 1. PageDownloadInstall.LoaderInit：把 "关闭下载" 假任务换回 ModDownload.DlClientListLoader
    static void PatchVersionListLoader(TypeDef pageInstall, TypeDef modDownload)
    {
        MethodDef m = pageInstall.FindMethod("LoaderInit");
        CilBody body = m.Body;
        body.SimplifyBranches();
        IList<Instruction> il = body.Instructions;

        int start = IndexOf(il, 0, i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "关闭下载");
        int end = IndexOf(il, start, i => i.OpCode == OpCodes.Newobj && ((IMethod)i.Operand).DeclaringType.FullName.StartsWith("PCL.ModLoader/LoaderTask`2"));
        // 确认范围外没有跳转指向范围内
        for (int k = 0; k < il.Count; k++)
        {
            if (k >= start && k <= end) continue;
            Instruction target = il[k].Operand as Instruction;
            if (target != null && il.IndexOf(target) > start && il.IndexOf(target) <= end)
                throw new Exception("LoaderInit：范围外存在指向被替换区间的跳转，放弃修改");
        }

        FieldDef loader = modDownload.FindField("DlClientListLoader");
        if (loader == null) throw new Exception("找不到 ModDownload.DlClientListLoader");
        il[start].OpCode = OpCodes.Ldsfld;
        il[start].Operand = loader;
        for (int k = start + 1; k <= end; k++)
        {
            il[k].OpCode = OpCodes.Nop;
            il[k].Operand = null;
        }
        body.OptimizeBranches();
        Console.WriteLine("[1/4] LoaderInit：已恢复 DlClientListLoader");
    }

    // 2. ModpackInstall(File, InstanceName, Logo)：把末尾的 throw 换回按整合包类型分发
    static void PatchModpackDispatch(TypeDef modModpack)
    {
        MethodDef m = modModpack.Methods.Single(x => x.Name.String == "ModpackInstall" && x.Parameters.Count == 3);
        CilBody body = m.Body;
        body.SimplifyBranches();
        IList<Instruction> il = body.Instructions;

        int idx = IndexOf(il, 0, i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == BlockMessage);
        if (il[idx + 1].OpCode != OpCodes.Newobj || il[idx + 2].OpCode != OpCodes.Throw)
            throw new Exception("ModpackInstall：拦截代码形态与预期不符");

        Local ret = body.Variables[0];
        Local archive = body.Variables[1];
        Local baseFolder = body.Variables[2];
        Local packType = body.Variables[4];
        if (!ret.Type.FullName.StartsWith("PCL.ModLoader/LoaderCombo`1") ||
            archive.Type.FullName != "System.IO.Compression.ZipArchive" ||
            baseFolder.Type.FullName != "System.String" ||
            packType.Type.FullName != "System.Int32")
            throw new Exception("ModpackInstall：局部变量布局与预期不符");

        Parameter pFile = m.Parameters[0], pName = m.Parameters[1], pLogo = m.Parameters[2];

        // 复用方法开头的 Logger.Info 调用及源文件路径字符串
        int logIdx = IndexOf(il, 0, i => i.OpCode == OpCodes.Call && ((IMethod)i.Operand).FullName.Contains("MeloongCore.Logger::Info(System.String,MeloongCore.LogBehavior,System.String)"));
        IMethod loggerInfo = (IMethod)il[logIdx].Operand;
        string logPath = (string)il[logIdx - 1].Operand;

        // 方法末尾：RET: ldloc ret; ret
        Instruction retLabel = Instruction.Create(OpCodes.Ldloc, ret);

        Instruction caseCurseForge = Instruction.Create(OpCodes.Nop);
        Instruction caseHmcl = Instruction.Create(OpCodes.Nop);
        Instruction caseMmc = Instruction.Create(OpCodes.Nop);
        Instruction caseMcbbs = Instruction.Create(OpCodes.Nop);
        Instruction caseModrinth = Instruction.Create(OpCodes.Nop);
        Instruction caseLauncherPack = Instruction.Create(OpCodes.Nop);
        Instruction caseCompress = Instruction.Create(OpCodes.Nop);

        List<Instruction> code = new List<Instruction>();
        // il[idx] 是内层 try 各分支 leave 的目标，原地改写以保留跳转
        il[idx].OpCode = OpCodes.Ldloc;
        il[idx].Operand = packType;
        code.Add(Instruction.Create(OpCodes.Switch, new[] { caseCurseForge, caseHmcl, caseMmc, caseMcbbs, caseModrinth }));
        code.Add(Instruction.Create(OpCodes.Ldloc, packType));
        code.Add(Instruction.Create(OpCodes.Ldc_I4, 9));
        code.Add(Instruction.Create(OpCodes.Beq, caseLauncherPack));
        code.Add(Instruction.Create(OpCodes.Br, caseCompress));

        AddCase(code, caseCurseForge, "CurseForge", modModpack.FindMethod("InstallPackCurseForge"), loggerInfo, logPath, ret, retLabel,
            Ld(pFile), Ld(archive), Ld(baseFolder), Ld(pName), Ld(pLogo));
        AddCase(code, caseHmcl, "HMCL", modModpack.FindMethod("InstallPackHMCL"), loggerInfo, logPath, ret, retLabel,
            Ld(pFile), Ld(archive), Ld(baseFolder));
        AddCase(code, caseMmc, "MMC", modModpack.FindMethod("InstallPackMMC"), loggerInfo, logPath, ret, retLabel,
            Ld(pFile), Ld(archive), Ld(baseFolder));
        AddCase(code, caseMcbbs, "MCBBS", modModpack.FindMethod("InstallPackMCBBS"), loggerInfo, logPath, ret, retLabel,
            Ld(pFile), Ld(archive), Ld(baseFolder), Ld(pName));
        AddCase(code, caseModrinth, "Modrinth", modModpack.FindMethod("InstallPackModrinth"), loggerInfo, logPath, ret, retLabel,
            Ld(pFile), Ld(archive), Ld(baseFolder), Ld(pName), Ld(pLogo));
        AddCase(code, caseLauncherPack, "带启动器的压缩包", modModpack.FindMethod("InstallPackLauncherPack"), loggerInfo, logPath, ret, retLabel,
            Ld(pFile), Ld(archive), Ld(baseFolder));
        AddCase(code, caseCompress, "普通压缩包", modModpack.FindMethod("InstallPackCompress"), loggerInfo, logPath, ret, retLabel,
            Ld(pFile), Ld(archive));

        il.RemoveAt(idx + 2); // throw
        il.RemoveAt(idx + 1); // newobj Exception
        for (int k = 0; k < code.Count; k++) il.Insert(idx + 1 + k, code[k]);

        // 原方法以 finally 结尾（HandlerEnd = null 表示方法末尾），追加返回块并收紧 HandlerEnd
        ExceptionHandler outerFinally = body.ExceptionHandlers.Single(h => h.HandlerType == ExceptionHandlerType.Finally && h.HandlerEnd == null);
        il.Add(retLabel);
        il.Add(Instruction.Create(OpCodes.Ret));
        outerFinally.HandlerEnd = retLabel;

        body.OptimizeBranches();
        Console.WriteLine("[2/4] ModpackInstall(File, …)：已恢复整合包类型分发");
    }

    static void AddCase(List<Instruction> code, Instruction label, string kind, MethodDef installer, IMethod loggerInfo, string logPath,
        Local ret, Instruction retLabel, params Instruction[] args)
    {
        if (installer == null) throw new Exception("找不到整合包安装方法：" + kind);
        if (installer.Parameters.Count != args.Length) throw new Exception("整合包安装方法参数数量不符：" + installer.Name);
        code.Add(label);
        code.Add(Instruction.Create(OpCodes.Ldstr, "整合包种类：" + kind));
        code.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        code.Add(Instruction.Create(OpCodes.Ldstr, logPath));
        code.Add(Instruction.Create(OpCodes.Call, loggerInfo));
        code.AddRange(args);
        code.Add(Instruction.Create(OpCodes.Call, installer));
        code.Add(Instruction.Create(OpCodes.Stloc, ret));
        code.Add(Instruction.Create(OpCodes.Leave, retLabel));
    }

    // 3. ModpackInstall()：恢复 "选择文件 → 后台线程安装"，线程体为仍保留的 _Closure$__0-0._Lambda$__0
    static void PatchModpackSelectFile(ModuleDefMD mod, TypeDef modModpack, TypeDef modJava, TypeDef modBase)
    {
        MethodDef m = modModpack.Methods.Single(x => x.Name.String == "ModpackInstall" && x.Parameters.Count == 0);
        CilBody body = m.Body;
        if (!body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == BlockMessage))
            throw new Exception("ModpackInstall()：拦截代码形态与预期不符");

        TypeDef closure = modModpack.NestedTypes.Single(t => t.Name.String == "_Closure$__0-0");
        MethodDef closureCtor = closure.FindDefaultConstructor();
        FieldDef fileField = closure.FindField("$VB$Local_File");
        MethodDef lambda = closure.FindMethod("_Lambda$__0");
        MethodDef runInThread = modBase.Methods.Single(x => x.Name.String == "RunInThread" && x.Parameters.Count == 1);
        if (closureCtor == null || fileField == null || lambda == null) throw new Exception("整合包闭包类结构与预期不符");

        // 从 ModJava.ManuallyImportJava 复用 SelectFile 相关的成员引用
        IList<Instruction> java = modJava.FindMethod("ManuallyImportJava").Body.Instructions;
        IMethod opImplicit = OperandOfCall(java, "op_Implicit");
        IMethod nullableCtor = (IMethod)java.First(i => i.OpCode == OpCodes.Newobj && ((IMethod)i.Operand).DeclaringType.FullName.StartsWith("System.Nullable`1")).Operand;
        IMethod selectFile = OperandOfCall(java, "SelectFile");
        IMethod firstOrDefault = OperandOfCall(java, "FirstOrDefault");
        IMethod isNullOrEmpty = OperandOfCall(java, "IsNullOrEmpty");

        // System.Action::.ctor(object, native int)，从模块中现有引用里取
        IMethod actionCtor = null;
        foreach (TypeDef t in mod.GetTypes())
        {
            foreach (MethodDef md in t.Methods)
            {
                if (!md.HasBody) continue;
                foreach (Instruction i in md.Body.Instructions)
                {
                    IMethod im = i.Operand as IMethod;
                    if (i.OpCode == OpCodes.Newobj && im != null && im.Name.String == ".ctor" && im.DeclaringType.FullName == "System.Action")
                    {
                        actionCtor = im;
                        break;
                    }
                }
                if (actionCtor != null) break;
            }
            if (actionCtor != null) break;
        }
        if (actionCtor == null) throw new Exception("找不到 System.Action 构造函数引用");

        Local c = body.Variables[0];
        if (c.Type.FullName != closure.FullName) throw new Exception("ModpackInstall()：局部变量布局与预期不符");

        IList<Instruction> il = body.Instructions;
        il.Clear();
        body.ExceptionHandlers.Clear();
        Instruction ret = Instruction.Create(OpCodes.Ret);

        il.Add(Instruction.Create(OpCodes.Newobj, closureCtor));
        il.Add(Instruction.Create(OpCodes.Stloc, c));
        il.Add(Instruction.Create(OpCodes.Ldloc, c));
        il.Add(Instruction.Create(OpCodes.Ldstr, "选择整合包压缩文件"));
        il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        il.Add(Instruction.Create(OpCodes.Ldnull));
        il.Add(Instruction.Create(OpCodes.Ldstr, "整合包文件(*.rar;*.zip;*.mrpack)|*.rar;*.zip;*.mrpack"));
        il.Add(Instruction.Create(OpCodes.Call, opImplicit));
        il.Add(Instruction.Create(OpCodes.Newobj, nullableCtor));
        il.Add(Instruction.Create(OpCodes.Call, selectFile));
        il.Add(Instruction.Create(OpCodes.Call, firstOrDefault));
        il.Add(Instruction.Create(OpCodes.Stfld, fileField));
        il.Add(Instruction.Create(OpCodes.Ldloc, c));
        il.Add(Instruction.Create(OpCodes.Ldfld, fileField));
        il.Add(Instruction.Create(OpCodes.Call, isNullOrEmpty));
        il.Add(Instruction.Create(OpCodes.Brtrue, ret));
        il.Add(Instruction.Create(OpCodes.Ldloc, c));
        il.Add(Instruction.Create(OpCodes.Ldftn, lambda));
        il.Add(Instruction.Create(OpCodes.Newobj, actionCtor));
        il.Add(Instruction.Create(OpCodes.Call, runInThread));
        il.Add(ret);

        body.OptimizeBranches();
        Console.WriteLine("[3/4] ModpackInstall()：已恢复手动选择整合包安装");
    }

    // 4. 更多 → 关于与鸣谢：在“关于”卡片末尾追加一行 “FS / 补丁版创作者”
    static void PatchAboutCredit(ModuleDefMD mod, TypeDef page, TypeDef listItem)
    {
        MethodDef setTitle = listItem.FindMethod("set_Title");
        MethodDef setInfo = listItem.FindMethod("set_Info");
        MethodDef itemCtor = listItem.FindDefaultConstructor();
        MethodDef getItemAboutPcl = page.FindMethod("get_ItemAboutPcl");
        MethodDef loaded = page.FindMethod("PageOtherAbout_Loaded");
        if (setTitle == null || setInfo == null || itemCtor == null || getItemAboutPcl == null || loaded == null)
            throw new Exception("PageOtherAbout / MyListItem 结构与预期不符");

        IMethod getParent = mod.Import(typeof(FrameworkElement).GetProperty("Parent").GetGetMethod());
        IMethod getRowDefs = mod.Import(typeof(Grid).GetProperty("RowDefinitions").GetGetMethod());
        IMethod rowDefsAdd = mod.Import(typeof(RowDefinitionCollection).GetMethod("Add"));
        IMethod rowDefsCount = mod.Import(typeof(RowDefinitionCollection).GetProperty("Count").GetGetMethod());
        IMethod rowDefCtor = mod.Import(typeof(RowDefinition).GetConstructor(Type.EmptyTypes));
        IMethod setHeight = mod.Import(typeof(RowDefinition).GetProperty("Height").GetSetMethod());
        IMethod gridLengthCtor = mod.Import(typeof(GridLength).GetConstructor(new[] { typeof(double) }));
        IMethod thicknessCtor = mod.Import(typeof(Thickness).GetConstructor(new[] { typeof(double), typeof(double), typeof(double), typeof(double) }));
        IMethod setMargin = mod.Import(typeof(FrameworkElement).GetProperty("Margin").GetSetMethod());
        IMethod setHitTest = mod.Import(typeof(UIElement).GetProperty("IsHitTestVisible").GetSetMethod());
        IMethod setRow = mod.Import(typeof(Grid).GetMethod("SetRow"));
        IMethod setColumn = mod.Import(typeof(Grid).GetMethod("SetColumn"));
        IMethod getChildren = mod.Import(typeof(Panel).GetProperty("Children").GetGetMethod());
        IMethod childrenAdd = mod.Import(typeof(UIElementCollection).GetMethod("Add"));
        ITypeDefOrRef gridType = mod.Import(typeof(Grid));

        MethodDef m = new MethodDefUser("FsAddPatchCredit",
            MethodSig.CreateInstance(mod.CorLibTypes.Void),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Private | MethodAttributes.HideBySig);
        CilBody body = new CilBody();
        body.InitLocals = true;
        m.Body = body;
        Local grid = body.Variables.Add(new Local(gridType.ToTypeSig()));
        Local item = body.Variables.Add(new Local(listItem.ToTypeSig()));
        IList<Instruction> il = body.Instructions;
        Instruction ret = Instruction.Create(OpCodes.Ret);

        il.Add(Instruction.Create(OpCodes.Ldarg_0));
        il.Add(Instruction.Create(OpCodes.Callvirt, getItemAboutPcl));
        il.Add(Instruction.Create(OpCodes.Callvirt, getParent));
        il.Add(Instruction.Create(OpCodes.Isinst, gridType));
        il.Add(Instruction.Create(OpCodes.Stloc, grid));
        il.Add(Instruction.Create(OpCodes.Ldloc, grid));
        il.Add(Instruction.Create(OpCodes.Brfalse, ret));
        // grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) })
        il.Add(Instruction.Create(OpCodes.Ldloc, grid));
        il.Add(Instruction.Create(OpCodes.Callvirt, getRowDefs));
        il.Add(Instruction.Create(OpCodes.Newobj, rowDefCtor));
        il.Add(Instruction.Create(OpCodes.Dup));
        il.Add(Instruction.Create(OpCodes.Ldc_R8, 45.0));
        il.Add(Instruction.Create(OpCodes.Newobj, gridLengthCtor));
        il.Add(Instruction.Create(OpCodes.Callvirt, setHeight));
        il.Add(Instruction.Create(OpCodes.Callvirt, rowDefsAdd));
        // var item = new MyListItem { Title = "FS", Info = "补丁版创作者", IsHitTestVisible = false, Margin = (-5,0,15,0) }
        il.Add(Instruction.Create(OpCodes.Newobj, itemCtor));
        il.Add(Instruction.Create(OpCodes.Stloc, item));
        il.Add(Instruction.Create(OpCodes.Ldloc, item));
        il.Add(Instruction.Create(OpCodes.Ldstr, "FS"));
        il.Add(Instruction.Create(OpCodes.Callvirt, setTitle));
        il.Add(Instruction.Create(OpCodes.Ldloc, item));
        il.Add(Instruction.Create(OpCodes.Ldstr, "补丁版创作者"));
        il.Add(Instruction.Create(OpCodes.Callvirt, setInfo));
        il.Add(Instruction.Create(OpCodes.Ldloc, item));
        il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        il.Add(Instruction.Create(OpCodes.Callvirt, setHitTest));
        il.Add(Instruction.Create(OpCodes.Ldloc, item));
        il.Add(Instruction.Create(OpCodes.Ldc_R8, -5.0));
        il.Add(Instruction.Create(OpCodes.Ldc_R8, 0.0));
        il.Add(Instruction.Create(OpCodes.Ldc_R8, 15.0));
        il.Add(Instruction.Create(OpCodes.Ldc_R8, 0.0));
        il.Add(Instruction.Create(OpCodes.Newobj, thicknessCtor));
        il.Add(Instruction.Create(OpCodes.Callvirt, setMargin));
        // Grid.SetRow(item, grid.RowDefinitions.Count - 1); Grid.SetColumn(item, 1)
        il.Add(Instruction.Create(OpCodes.Ldloc, item));
        il.Add(Instruction.Create(OpCodes.Ldloc, grid));
        il.Add(Instruction.Create(OpCodes.Callvirt, getRowDefs));
        il.Add(Instruction.Create(OpCodes.Callvirt, rowDefsCount));
        il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
        il.Add(Instruction.Create(OpCodes.Sub));
        il.Add(Instruction.Create(OpCodes.Call, setRow));
        il.Add(Instruction.Create(OpCodes.Ldloc, item));
        il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
        il.Add(Instruction.Create(OpCodes.Call, setColumn));
        // grid.Children.Add(item)
        il.Add(Instruction.Create(OpCodes.Ldloc, grid));
        il.Add(Instruction.Create(OpCodes.Callvirt, getChildren));
        il.Add(Instruction.Create(OpCodes.Ldloc, item));
        il.Add(Instruction.Create(OpCodes.Callvirt, childrenAdd));
        il.Add(Instruction.Create(OpCodes.Pop));
        il.Add(ret);
        page.Methods.Add(m);

        // 挂到 PageOtherAbout_Loaded 的首次加载分支里：紧跟在 ItemAboutPcl.Info = … 之后
        IList<Instruction> lil = loaded.Body.Instructions;
        int idx = IndexOf(lil, 0, i => i.OpCode == OpCodes.Callvirt && i.Operand == setInfo);
        lil.Insert(idx + 1, Instruction.Create(OpCodes.Ldarg_0));
        lil.Insert(idx + 2, Instruction.Create(OpCodes.Call, m));
        loaded.Body.OptimizeBranches();
        Console.WriteLine("[4/4] 关于与鸣谢：已添加 “FS / 补丁版创作者”");
    }

    static Instruction Ld(Parameter p) { return Instruction.Create(OpCodes.Ldarg, p); }
    static Instruction Ld(Local l) { return Instruction.Create(OpCodes.Ldloc, l); }

    static IMethod OperandOfCall(IList<Instruction> il, string name)
    {
        return (IMethod)il.First(i => i.OpCode == OpCodes.Call && ((IMethod)i.Operand).Name.String == name).Operand;
    }

    static TypeDef FindType(ModuleDefMD mod, string fullName)
    {
        TypeDef t = mod.Find(fullName, false);
        if (t == null) throw new Exception("找不到类型：" + fullName);
        return t;
    }

    static int IndexOf(IList<Instruction> il, int from, Func<Instruction, bool> pred)
    {
        for (int k = from; k < il.Count; k++)
            if (pred(il[k])) return k;
        throw new Exception("未找到预期的 IL 指令，可能 PCL 版本不同");
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using OnlyApplicableItems.Integration;

internal static class PrMachineRuntimeChecks
{
    private static readonly MethodInfo Picked = typeof(UIPanicReductionMachineWindowData).GetMethod("OnResourcePicked", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo Remove = typeof(UIPanicReductionMachineWindowData).GetMethod("TryRemoveLoadedItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal static void Run(string managedPath)
    {
        Metadata(managedPath);
        Action[] checks = { ColdStartAccessors, CombinedAndReentrant, StalePlan, NativeException, VanillaAndUnpatch, ForeignPatch,
            CarrierCountMismatch, DisposalDuringNativeCallback, CrossAssemblyPrivateFields };
        foreach (var check in checks) { check(); Console.WriteLine("PASS Mono/Harmony PR " + check.Method.Name); }
        Console.WriteLine("9 PR integration checks passed on shipped Mono/Harmony (cold-start and in-memory inventory/effect boundary).");
    }
    private static void ColdStartAccessors() => PrColdStartCheck.Run();
    private static PrMachineStock Stock(Inventory root)
    {
        var roots = new MultiInventory(); roots.inventoryList.Add(root);
        return new PrMachineStock(new PrItemFields(), roots, item => item.id == "hypno", new PlainItemValidator().Validate);
    }
    private static PrMachineNative Bridge() => new PrMachineNative("onlyapplicableitems.pr-probe", Picked, Remove);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void CombinedAndReentrant()
    {
        using (var bridge = Bridge())
        {
            var owner = new UIPanicReductionMachineWindowData();
            var a = new Item("hypno", 20); var b = new Item("hypno", 25);
            owner.Source.Data.Inventory.AddRange(new[] { a, b });
            var cell = new UIItemCell(new Item("newspaper", 1)) { ThrowOnRead = true };
            var plan = Stock(owner.Source).Prepare("hypno", 40);
            int notified = 0;
            owner.AfterEffect = () => owner.Load(cell, 40); // Must be rejected by the native-entry gate.
            owner.Source.OnItemsRemove += _ =>
            {
                Require(owner.Effects == 1 && b.Count == 5, "Notify only after complete consumption and one native effect."); notified++;
                bool rejected = false;
                try { bridge.Run(owner, cell, new Item("hypno", 5), Stock(owner.Source).Prepare("hypno", 5), _ => { }); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "An inventory observer cannot start a nested authorized load.");
            };
            bool notificationFailure = false;
            bridge.Run(owner, cell, new Item("hypno", 40), plan, _ => notificationFailure = true);
            Require(owner.Effects == 1 && owner.Days == 80 && owner.Expressions == 1 && owner.NativeRemovals == 0 &&
                b.Count == 5 && owner.Source.Data.Inventory.Count == 1 && notified == 1 && !notificationFailure,
                "Real Harmony bridge must bypass both the pooled-cell read and unsafe UID helper, retaining one native effect.");
        }
    }
    private static void StalePlan()
    {
        using (var bridge = Bridge())
        {
            var owner = new UIPanicReductionMachineWindowData(); var a = new Item("hypno", 20);
            owner.Source.Data.Inventory.Add(a); var plan = Stock(owner.Source).Prepare("hypno", 20); a.Count = 19;
            bridge.Run(owner, new UIItemCell(a), new Item("hypno", 20), plan, _ => { });
            Require(a.Count == 19 && owner.Effects == 0 && owner.NativeRemovals == 0, "Stale plans make zero destructive writes and award no effects.");
        }
    }
    private static void NativeException()
    {
        using (var bridge = Bridge())
        {
            var owner = new UIPanicReductionMachineWindowData(); var a = new Item("hypno", 20); owner.Source.Data.Inventory.Add(a);
            var failure = new InvalidOperationException("native post-effect failure"); owner.AfterEffect = () => throw failure;
            int notifications = 0; owner.Source.OnItemsRemove += _ => notifications++;
            bool same = false;
            try { bridge.Run(owner, new UIItemCell(a), new Item("hypno", 5), Stock(owner.Source).Prepare("hypno", 5), _ => { }); }
            catch (Exception exception) { same = ReferenceEquals(exception, failure); }
            Require(same && a.Count == 15 && owner.Effects == 1 && notifications == 1, "Preserve native exception identity and notify once; never replay or roll back effects.");
            owner.AfterEffect = null;
            bridge.Run(owner, new UIItemCell(a), new Item("hypno", 5), Stock(owner.Source).Prepare("hypno", 5), _ => { });
            Require(a.Count == 10 && owner.Effects == 2, "Exception cleanup releases the native operation scope.");
        }
    }
    private static void VanillaAndUnpatch()
    {
        var owner = new UIPanicReductionMachineWindowData(); var a = new Item("hypno", 3); owner.Source.Data.Inventory.Add(a);
        using (var bridge = Bridge()) owner.Load(new UIItemCell(a), 10);
        Require(owner.NativeRemovals == 1 && owner.Days == 6, "No authorized scope must retain native per-stack clamping/removal.");
        var b = new Item("hypno", 2); owner.Source.Data.Inventory.Add(b); owner.Load(new UIItemCell(b), 10);
        Require(owner.NativeRemovals == 2 && owner.Days == 10, "Disposal restores the completely original native body.");
    }
    private static void ForeignPatch()
    {
        using (var bridge = Bridge())
        {
            var other = new Harmony("onlyapplicableitems.foreign-pr-probe");
            try
            {
                foreach (var target in new[] { Remove, typeof(Item).GetProperty(nameof(Item.Count))!.GetSetMethod()! })
                {
                    other.Patch(target, prefix: new HarmonyMethod(typeof(PrMachineRuntimeChecks), nameof(ForeignPrefix)));
                    bool rejected = false; try { bridge.Verify(); } catch (NotSupportedException) { rejected = true; }
                    Require(rejected, "Late foreign PR or source-storage patches must reject consolidation before destructive work.");
                    other.UnpatchSelf();
                }
            }
            finally { other.UnpatchSelf(); }
        }
    }
    private static void ForeignPrefix() { }

    private static void CarrierCountMismatch()
    {
        using (var bridge = Bridge())
        {
            var owner = new UIPanicReductionMachineWindowData(); var item = new Item("hypno", 20); owner.Source.Data.Inventory.Add(item);
            bridge.Run(owner, new UIItemCell(item), new Item("hypno", 1), Stock(owner.Source).Prepare("hypno", 10), _ => { });
            Require(item.Count == 20 && owner.Effects == 0, "A reduced native clamp cannot consume a larger plan or award mismatched effects.");
        }
    }
    private static void DisposalDuringNativeCallback()
    {
        var bridge = Bridge();
        try
        {
            var owner = new UIPanicReductionMachineWindowData(); var item = new Item("hypno", 20); owner.Source.Data.Inventory.Add(item);
            owner.AfterEffect = bridge.Dispose;
            int notified = 0; owner.Source.OnItemsRemove += _ => notified++;
            bridge.Run(owner, new UIItemCell(item), new Item("hypno", 10), Stock(owner.Source).Prepare("hypno", 10), _ => { });
            Require(item.Count == 10 && owner.Effects == 1 && notified == 1, "Disposal during effects still completes notification/operation cleanup.");
            owner.AfterEffect = null; owner.Load(new UIItemCell(item), 20);
            Require(owner.NativeRemovals == 1 && owner.Days == 40, "Deferred unpatch restores native behavior after the operation exits.");
        }
        finally { bridge.Dispose(); }
    }
    private static void CrossAssemblyPrivateFields()
    {
        // The real fields live in Assembly-CSharp, not the plugin assembly. Verify
        // Mono's cross-assembly skipVisibility behavior without loading Unity.
        var external = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("PrExternalProbe"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var builder = external.DefineDynamicModule("main").DefineType("ExternalStack", System.Reflection.TypeAttributes.Public);
        builder.DefineDefaultConstructor(System.Reflection.MethodAttributes.Public);
        var field = builder.DefineField("count", typeof(int), System.Reflection.FieldAttributes.Private);
        var type = builder.CreateType();
        var resolved = type.GetField(field.Name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var setter = new System.Reflection.Emit.DynamicMethod("write_external", typeof(void), new[] { typeof(object), typeof(int) }, typeof(PrMachineRuntimeChecks).Module, true);
        var il = setter.GetILGenerator(); il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Castclass, type);
        il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1); il.Emit(System.Reflection.Emit.OpCodes.Stfld, resolved); il.Emit(System.Reflection.Emit.OpCodes.Ret);
        var write = (Action<object, int>)setter.CreateDelegate(typeof(Action<object, int>));
        var instance = Activator.CreateInstance(type)!; write(instance, 37);
        Require((int)resolved.GetValue(instance)! == 37, "Raw stores must work across assembly boundaries on the shipped Mono.");
    }

    private static void Metadata(string path)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(path, "Assembly-CSharp.dll")))
        {
            var types = assembly.MainModule.Types;
            var owner = types.Single(t => t.Name == "UIPanicReductionMachineWindowData");
            var picked = owner.Methods.Single(m => m.Name == "OnResourcePicked");
            Require(!picked.IsStatic && !picked.HasGenericParameters && picked.ReturnType.FullName == "System.Void" &&
                picked.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "UIItemCell", "System.Int32" }), "Native PR load signature.");
            string[] calls = picked.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => ((MethodReference)i.Operand).FullName).ToArray();
            Require(calls.Count(c => c == "Item UIItemCell::get_DisplayingItem()") == 1 &&
                calls.Count(c => c == "System.Int32 UnityEngine.Mathf::Min(System.Int32,System.Int32)") == 1 &&
                calls.Count(c => c == "System.Boolean UIPanicReductionMachineWindowData::TryRemoveLoadedItem(Item,System.Int32)") == 1,
                "Native PR body must retain the exact single getter/clamp/removal seams used by the production bridge.");
            var item = types.Single(t => t.Name == "Item");
            var emptyConstructor = item.Methods.Single(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
            var namedConstructor = item.Methods.Single(m => m.IsConstructor && m.Parameters.Count == 2 &&
                m.Parameters[0].ParameterType.FullName == "System.String" && m.Parameters[1].ParameterType.FullName == "System.Int32");
            bool ReadsDefinition(MethodDefinition method) => method.Body.Instructions.Any(i =>
                i.Operand is MethodReference call && call.Name == "get_Definition");
            Require(!ReadsDefinition(emptyConstructor) && ReadsDefinition(namedConstructor),
                "Cold-start fixture must match the native parameterless/named Item constructor dependency boundary.");
            var inventoryConstructor = types.Single(t => t.Name == "Inventory").Methods.Single(m =>
                m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
            var itemCreations = inventoryConstructor.Body.Instructions.Where(i => i.OpCode.Name == "newobj" &&
                i.Operand is MethodReference call && call.DeclaringType.FullName == "Item").ToArray();
            Require(itemCreations.Length == 1 && ((MethodReference)itemCreations[0].Operand).Parameters.Count == 0,
                "Native default Inventory must use the balance-independent parameterless Item constructor.");
            Console.WriteLine("PASS native Item/Inventory cold-start constructor dependencies (1.012 metadata only).");
            foreach (string field in new[] { "count", "inventoryFillSize", "inventory", "inventorySize", "properties" })
                Require(item.Fields.Any(f => f.Name == field && !f.IsStatic && !f.IsInitOnly), "Native raw field: " + field);
            Require(item.Fields.Single(f => f.Name == "inventory").FieldType.FullName == "System.Collections.Generic.List`1<Item>" &&
                item.Fields.Single(f => f.Name == "properties").FieldType.FullName == "System.Collections.Generic.Dictionary`2<System.Type,SerializedItemProperty>" &&
                new[] { "count", "inventoryFillSize", "inventorySize" }.All(name => item.Fields.Single(f => f.Name == name).FieldType.FullName == "System.Int32"),
                "Native raw field types must match the compiled commit accessors.");
            Require(owner.Fields.Any(f => f.Name == "loadSourceInventory" && f.FieldType.FullName == "MultiInventory"), "Native PR loader inventory field.");
            Require(types.Single(t => t.Name == "Inventory").Fields.Any(f => f.Name == "inventoryItem" && f.FieldType.FullName == "Item"), "Native inventory ownership field.");
        }
        Console.WriteLine("PASS 1.012 PR native signatures, selection/clamp/removal IL seams and raw inventory fields (metadata only).");
    }
}

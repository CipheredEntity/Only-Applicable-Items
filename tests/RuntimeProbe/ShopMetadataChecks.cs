using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class ShopMetadataChecks
{
    public static void Run(string managedPath)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(managedPath, "Assembly-CSharp.dll")))
        {
            var types = assembly.MainModule.Types;
            void Method(string owner, string name, string result, params string[] parameters)
            {
                var type = types.Single(t => t.FullName == owner);
                var method = type.Methods.SingleOrDefault(m => m.Name == name && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters));
                if (method == null || method.IsStatic || method.HasGenericParameters || method.ReturnType.FullName != result)
                    throw new InvalidOperationException("Native Shop Selling contract mismatch: " + owner + "." + name);
            }
            Method("Trading", "FillVendorWindowData", "System.Void", "UIVendorWindowData", "System.String", "System.Action");
            Method("Trading", "TryMoveItem", "System.Boolean", "UIItemCell", "System.Int32", "Inventory", "Inventory");
            Method("Trading", "OnPlayerItemPress1", "System.Void", "UIItemCell");
            Method("Trading", "OnPlayerItemPress2", "System.Void", "UIItemCell");
            Method("Trading", "PlayerItemsAvailableCondition", "System.Boolean", "Item");
            Method("Trading", "OpenItemCountWindow", "System.Void", "UIItemCell", "Inventory", "Inventory", "UIItemCountWindowData/PriceCalculateDelegate");
            Method("Trading", "GetSingleItemCostInPlayerInventory", "System.Int32", "System.String", "System.Int32");
            Method("InventoryWidget", "OnRedraw", "System.Void");
            Method("UIVendorWindow", "RedrawLite", "System.Void");
            Method("UIVendorWindow", "HideWindow", "System.Void");
            Method("UIItemCountWindow", "Open", "System.Void", "UIItemCountWindowData");
            Method("VendorTierData", "HasProduct", "System.Boolean", "System.String");
            Method("VendorTierData", "IsBuyingProduct", "System.Boolean", "System.String");
            void Field(string owner, string name, string value)
            {
                if (!types.Single(t => t.FullName == owner).Fields.Any(f => f.Name == name && !f.IsStatic && f.FieldType.FullName == value))
                    throw new InvalidOperationException("Native Shop Selling field mismatch: " + owner + "." + name);
            }
            Field("Trading", "cachedWindowData", "UIVendorWindowData");
            Field("Trading", "sellInventory", "Inventory");
            Field("UIVendorWindow", "playerInventoryWidget", "MultiInventoryWidget");
            Field("Item", "properties", "System.Collections.Generic.Dictionary`2<System.Type,SerializedItemProperty>");
            var item = types.Single(t => t.FullName == "Item");
            var constructor = item.Methods.Single(m => m.IsConstructor && m.Parameters.Count == 2 &&
                m.Parameters[0].ParameterType.FullName == "System.String" && m.Parameters[1].ParameterType.FullName == "System.Int32");
            if (!constructor.Body.Instructions.Any(i => i.OpCode == OpCodes.Stfld &&
                (i.Operand as FieldReference)?.Name == "inventoryFillSize" && i.Previous?.OpCode == OpCodes.Ldc_I4_M1))
                throw new InvalidOperationException("Native capacity regression fixture no longer matches the initial -1 cache.");
            var capacity = item.Methods.Single(m => m.Name == "CanAddItemCountToInventory" && m.Parameters.Count == 5 &&
                m.Parameters[0].ParameterType.FullName == "ItemDef");
            if (!capacity.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldfld && (i.Operand as FieldReference)?.Name == "inventoryFillSize") ||
                capacity.Body.Instructions.Any(i => (i.Operand as MethodReference)?.Name == "get_InventoryFillSize"))
                throw new InvalidOperationException("Native capacity regression fixture no longer matches the raw-cache calculation.");
            Method("Item", "get_InventoryFillSize", "System.Int32");
        }
        Console.WriteLine("PASS Shop Selling native hook/delegate signatures and fields (copied assembly metadata only; no game methods executed).");
        Console.WriteLine("PASS native -1 occupancy sentinel and raw-cache capacity behavior (copied assembly metadata only).");
    }
}

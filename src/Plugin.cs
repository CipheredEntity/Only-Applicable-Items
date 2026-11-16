using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "cipheredentity.gk2.onlyapplicableitems";
    public const string Name = "Only Applicable Items";
    public const string Version = "0.15.0";
    internal static Plugin? Instance { get; private set; }
    internal ActionSettings Settings { get; private set; } = null!;
    private InventoryNameSettings inventoryNames = null!;
    private ConfigurationReload configurationReload = null!;
    private FixSettings fixes = null!;
    private WindowSortingFix? windowSorting;
    private readonly List<ApplicableItemsSession> sessions = new List<ApplicableItemsSession>();
    private readonly HashSet<string> warnings = new HashSet<string>(StringComparer.Ordinal);
    private Harmony? harmony;
    private Harmony? alchemyHarmony;
    private Harmony? resurrectionHarmony;
    private Harmony? resurrectionDisplayHarmony;
    private Harmony? studyDisplayHarmony;
    private Harmony? shopHarmony;
    private Harmony? prHarmony;
    private PrMachineNative? prNative;
    private GardenPickerContract? contract;
    private BodyPartPickerContract? bodyParts;
    private EmbalmingPickerContract? embalming;
    private GraveDecorationPickerContract? graveDecorations;
    private ZombieEquipmentPickerContract? zombieEquipment;
    private SermonPickerContract? sermons;
    private StudyTablePickerContract? studyTable;
    internal ResurrectionCollarPickerContract? ResurrectionCollars { get; private set; }
    internal ResurrectionCollarDisplay? ResurrectionCollarSlot { get; private set; }
    internal StudySelectedItemDisplay? StudySelectedSlot { get; private set; }
    internal AlchemyFeatures? Alchemy { get; private set; }
    internal ShopSellingFeatures? ShopSelling { get; private set; }
    internal PrMachineFeatures? PrMachine { get; private set; }

    private void Awake()
    {
        Instance = this;
        InitializeWindowSorting();
        Settings = new ActionSettings(Config);
        inventoryNames = new InventoryNameSettings(Config);
        configurationReload = new ConfigurationReload(Config,
            exception => WarnOnce("config-reload", "Could not reload configuration; continuing with loaded settings. " + exception));
        InitializeShopSelling();
        try
        {
            contract = new GardenPickerContract();
            harmony = new Harmony(Guid);
            harmony.Patch(contract.PickerConstructor,
                postfix: new HarmonyMethod(typeof(PickerPatches), nameof(PickerPatches.AfterPickerConstructed)));
            harmony.Patch(contract.InventoryRedrawNotification,
                prefix: new HarmonyMethod(typeof(PickerPatches), nameof(PickerPatches.BeforeInventoryRedrawNotification)));
            if (!contract.SupportsFertilizers)
                WarnOnce("fertilizer-contract", "The garden fertilizer predicate was not found; fertilizer consolidation is inactive. Seeds remain supported.");
            Logger.LogInfo(Name + " " + Version + " loaded. Seeds: " + Settings.Seeds.Value +
                "; Fertilizers: " + Settings.Fertilizers.Value);
        }
        catch (Exception exception)
        {
            harmony?.UnpatchSelf();
            contract = null;
            WarnOnce("startup", "The game's picker contract is not supported; consolidation is inactive. " + exception);
        }
        if (contract != null)
        {
            InitializeAlchemy();
            InitializeBodyParts();
            InitializeEmbalming();
            InitializeGraveDecorations();
            InitializeZombieEquipment();
            InitializeResurrectionCollars();
            InitializeResurrectionCollarDisplay();
            InitializeSermons();
            InitializeStudyTable();
            InitializeStudySelectedItemDisplay();
            InitializePrMachine();
        }
    }

    private void InitializePrMachine()
    {
        try
        {
            var pr = new PrMachineContract();
            var fields = new PrItemFields();
            prNative = new PrMachineNative(Guid + ".pr-native", pr.Picked, pr.Remove);
            var bridge = prNative;
            PrMachine = new PrMachineFeatures(pr, fields, () => Settings.PrMachine.Value, contract!.ValidatePlainItem,
                (operation, exception) => WarnOnce("pr-machine-" + operation, "PR Machine fallback while " + operation + ". " + exception),
                bridge.Verify, (owner, cell, carrier, plan) => bridge.Run(owner, cell, carrier, plan,
                    exception => WarnOnce("pr-machine-notification", "PR resources were consumed; an inventory observer failed. " + exception)));
            prHarmony = new Harmony(Guid + ".pr-machine");
            var countOpen = typeof(UIItemCountWindow).GetMethod("Open", new[] { typeof(UIItemCountWindowData) })
                ?? throw new MissingMethodException("PR quantity dialog Open");
            prHarmony.Patch(countOpen, prefix: new HarmonyMethod(typeof(PrMachinePatches), nameof(PrMachinePatches.BeforeCountOpen)));
            Logger.LogInfo("PR Machine ready. Enabled: " + Settings.PrMachine.Value);
        }
        catch (Exception exception)
        {
            ShutdownPrMachine();
            WarnOnce("pr-machine-startup", "PR Machine remains native; other features are independent. " + exception);
        }
    }

    private void ShutdownPrMachine()
    {
        var feature = PrMachine; PrMachine = null;
        var bridge = prNative; prNative = null;
        var patches = prHarmony; prHarmony = null;
        try { feature?.Dispose(); }
        catch (Exception exception) { WarnOnce("pr-machine-shutdown", "PR Machine requests were disabled but cleanup failed. " + exception); }
        try { bridge?.Dispose(); }
        catch (Exception exception) { WarnOnce("pr-native-shutdown", "PR Machine native-hook cleanup failed. " + exception); }
        try { patches?.UnpatchSelf(); }
        catch (Exception exception) { WarnOnce("pr-dialog-shutdown", "PR Machine dialog hooks are inactive but could not be removed. " + exception); }
    }

    private void InitializeShopSelling()
    {
        try
        {
            var shop = new ShopSellingContract();
            ShopSelling = new ShopSellingFeatures(shop, () => Settings.ShopSelling.Value, new PlainItemValidator().Validate,
                (operation, exception) => WarnOnce("shop-selling-" + operation, "Shop Selling fallback while " + operation + ". " + exception),
                inventoryNames.GetTitle, configurationReload.Refresh);
            shopHarmony = new Harmony(Guid + ".shop-selling");
            shopHarmony.Patch(shop.Fill, postfix: new HarmonyMethod(typeof(ShopSellingPatches), nameof(ShopSellingPatches.AfterFilled)));
            shopHarmony.Patch(shop.Draw, prefix: new HarmonyMethod(typeof(ShopSellingPatches), nameof(ShopSellingPatches.BeforeDraw)));
            shopHarmony.Patch(shop.Redraw, prefix: new HarmonyMethod(typeof(ShopSellingPatches), nameof(ShopSellingPatches.BeforeRedraw)));
            shopHarmony.Patch(shop.Hide, postfix: new HarmonyMethod(typeof(ShopSellingPatches), nameof(ShopSellingPatches.AfterHidden)));
            shopHarmony.Patch(shop.CountOpen, prefix: new HarmonyMethod(typeof(ShopSellingPatches), nameof(ShopSellingPatches.BeforeCountOpen)));
            shopHarmony.Patch(shop.Move, prefix: new HarmonyMethod(typeof(ShopSellingPatches), nameof(ShopSellingPatches.BeforeMove)),
                finalizer: new HarmonyMethod(typeof(ShopSellingPatches), nameof(ShopSellingPatches.AfterMove)));
            Logger.LogInfo("Shop Selling ready. Enabled: " + Settings.ShopSelling.Value);
        }
        catch (Exception exception)
        {
            ShutdownShopSelling();
            WarnOnce("shop-selling-startup", "Shop Selling remains native; other features are independent. " + exception);
        }
    }

    private void ShutdownShopSelling()
    {
        var feature = ShopSelling; ShopSelling = null;
        try { feature?.Dispose(); }
        catch (Exception exception) { WarnOnce("shop-selling-shutdown", "Shop Selling cleanup failed. " + exception); }
        try { shopHarmony?.UnpatchSelf(); }
        catch (Exception exception) { WarnOnce("shop-selling-unpatch", "Shop Selling hooks are inactive but could not be removed. " + exception); }
        shopHarmony = null;
    }

    private void InitializeWindowSorting()
    {
        try
        {
            fixes = new FixSettings(Config);
            windowSorting = new WindowSortingFix(Guid + ".window-sorting", () => fixes.WindowSortingOrder.Value,
                (operation, exception) => WarnOnce("window-sorting-" + operation,
                    "Window sorting correction suspended while " + operation + ". " + exception));
            Logger.LogInfo("Window Sorting Order ready. Enabled: " + fixes.WindowSortingOrder.Value);
        }
        catch (Exception exception)
        {
            windowSorting = null;
            WarnOnce("window-sorting-startup", "Window sorting remains native; inventory features are independent. " + exception);
        }
    }

    private void InitializeStudySelectedItemDisplay()
    {
        try
        {
            StudySelectedSlot = new StudySelectedItemDisplay(() => Settings.StudyTable.Value,
                (operation, exception) => WarnOnce("study-selected-display-" + operation,
                    "Study selected-item quantity correction failed while " + operation + ". " + exception));
            studyDisplayHarmony = new Harmony(Guid + ".study-selected-display");
            studyDisplayHarmony.Patch(StudySelectedSlot.Redraw,
                postfix: new HarmonyMethod(typeof(StudySelectedItemPatches), nameof(StudySelectedItemPatches.AfterRedraw)));
            studyDisplayHarmony.Patch(StudySelectedSlot.Hide,
                postfix: new HarmonyMethod(typeof(StudySelectedItemPatches), nameof(StudySelectedItemPatches.AfterHidden)));
        }
        catch (Exception exception)
        {
            StudySelectedSlot?.Dispose();
            studyDisplayHarmony?.UnpatchSelf();
            StudySelectedSlot = null;
            WarnOnce("study-selected-display-startup", "The selected study counter remains native; picker features remain available. " + exception);
        }
    }

    private void InitializeStudyTable()
    {
        try
        {
            studyTable = new StudyTablePickerContract(() => Settings.StudyTable.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Study Table ready. Enabled: " + Settings.StudyTable.Value);
        }
        catch (Exception exception)
        {
            studyTable = null;
            WarnOnce("study-table-startup", "Study Table is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeSermons()
    {
        try
        {
            sermons = new SermonPickerContract(() => Settings.Sermons.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Prayers ready. Enabled: " + Settings.Sermons.Value);
        }
        catch (Exception exception)
        {
            sermons = null;
            WarnOnce("sermons-startup", "Prayers is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeResurrectionCollars()
    {
        try
        {
            ResurrectionCollars = new ResurrectionCollarPickerContract(
                () => Settings.ResurrectionCollars.Value, contract!.ValidatePlainItem);
            resurrectionHarmony = new Harmony(Guid + ".resurrection-collars");
            resurrectionHarmony.Patch(ResurrectionCollars.OpenPicker,
                prefix: new HarmonyMethod(typeof(ResurrectionCollarPatches), nameof(ResurrectionCollarPatches.BeforeOpening)),
                finalizer: new HarmonyMethod(typeof(ResurrectionCollarPatches), nameof(ResurrectionCollarPatches.AfterOpening)));
            Logger.LogInfo("Resurrection Collars ready. Enabled: " + Settings.ResurrectionCollars.Value);
        }
        catch (Exception exception)
        {
            resurrectionHarmony?.UnpatchSelf();
            ResurrectionCollars = null;
            WarnOnce("resurrection-collars-startup", "Resurrection Collars is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeResurrectionCollarDisplay()
    {
        try
        {
            ResurrectionCollarSlot = new ResurrectionCollarDisplay(() => Settings.ResurrectionCollars.Value,
                (operation, exception) => WarnOnce("resurrection-collar-display-" + operation,
                    "Resurrection collar slot correction failed while " + operation + ". " + exception));
            resurrectionDisplayHarmony = new Harmony(Guid + ".resurrection-collar-display");
            resurrectionDisplayHarmony.Patch(ResurrectionCollarSlot.Redraw,
                postfix: new HarmonyMethod(typeof(ResurrectionCollarDisplayPatches), nameof(ResurrectionCollarDisplayPatches.AfterRedraw)));
            resurrectionDisplayHarmony.Patch(ResurrectionCollarSlot.Hide,
                postfix: new HarmonyMethod(typeof(ResurrectionCollarDisplayPatches), nameof(ResurrectionCollarDisplayPatches.AfterHidden)));
        }
        catch (Exception exception)
        {
            ResurrectionCollarSlot?.Dispose();
            resurrectionDisplayHarmony?.UnpatchSelf();
            ResurrectionCollarSlot = null;
            WarnOnce("resurrection-collar-display-startup", "The selected collar counter remains native; picker features remain available. " + exception);
        }
    }

    private void InitializeZombieEquipment()
    {
        try
        {
            zombieEquipment = new ZombieEquipmentPickerContract(() => Settings.ZombieEquipment.Value, contract!.ValidatePlainItem);
            if (!zombieEquipment.SupportsEquipment)
                WarnOnce("equipment-contract", "Armor, Weapons, and Tools methods or equipped-slot getters were not found; Collars remain supported.");
            Logger.LogInfo("Zombie Equipment ready. Enabled: " + Settings.ZombieEquipment.Value +
                "; Armor/Weapons/Tools supported: " + zombieEquipment.SupportsEquipment);
        }
        catch (Exception exception)
        {
            zombieEquipment = null;
            WarnOnce("zombie-equipment-startup", "Zombie Equipment is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeGraveDecorations()
    {
        try
        {
            graveDecorations = new GraveDecorationPickerContract(() => Settings.GraveDecorations.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Grave Decorations ready. Enabled: " + Settings.GraveDecorations.Value);
        }
        catch (Exception exception)
        {
            graveDecorations = null;
            WarnOnce("grave-decorations-startup", "Grave Decorations is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeEmbalming()
    {
        try
        {
            embalming = new EmbalmingPickerContract(() => Settings.EmbalmingFluids.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Embalming Fluids ready. Enabled: " + Settings.EmbalmingFluids.Value);
        }
        catch (Exception exception)
        {
            embalming = null;
            WarnOnce("embalming-startup", "Embalming Fluids is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeBodyParts()
    {
        try
        {
            bodyParts = new BodyPartPickerContract(() => Settings.BodyParts.Value, contract!.ValidatePlainItem);
            Logger.LogInfo("Body Parts ready. Enabled: " + Settings.BodyParts.Value);
        }
        catch (Exception exception)
        {
            bodyParts = null;
            WarnOnce("body-parts-startup", "Body Parts is unavailable; existing actions remain supported. " + exception);
        }
    }

    private void InitializeAlchemy()
    {
        try
        {
            var alchemyContract = new AlchemyContract();
            Alchemy = new AlchemyFeatures(this, alchemyContract, contract!.ValidatePlainItem);
            // A separate owner lets alchemy initialization fail without removing garden patches.
            alchemyHarmony = new Harmony(Guid + ".alchemy");
            alchemyHarmony.Patch(alchemyContract.CountUpdate,
                prefix: new HarmonyMethod(typeof(AlchemyPatches), nameof(AlchemyPatches.BeforeCountUpdate)),
                finalizer: new HarmonyMethod(typeof(AlchemyPatches), nameof(AlchemyPatches.AfterCountUpdate)));
            alchemyHarmony.Patch(alchemyContract.Hide,
                postfix: new HarmonyMethod(typeof(AlchemyPatches), nameof(AlchemyPatches.AfterLaboratoryHidden)));
            Logger.LogInfo("Alchemical Ingredients ready. Enabled: " + Settings.AlchemicalIngredients.Value);
        }
        catch (Exception exception)
        {
            Alchemy?.Dispose();
            alchemyHarmony?.UnpatchSelf();
            Alchemy = null;
            WarnOnce("alchemy-startup", "Alchemical Ingredients is unavailable; Seeds and Fertilizers remain supported. " + exception);
        }
    }

    internal void TryConsolidate(UIMultiInventoryWindowData data, Action<UIItemCell> callback, Func<Item, bool> predicate)
    {
        if (contract == null)
            return;
        PickerPolicy<Item>? policy = null;
        IReadOnlyList<PickerPolicy<Item>>? policies = null;
        ApplicableItemsSession? session = null;
        try
        {
            // Discovery includes private-field reads for autopsy pickers. If it fails,
            // the constructor's original inventory sections must remain usable.
            configurationReload.Refresh();
            policy = contract.GetPolicy(callback, predicate, Settings) ?? Alchemy?.GetPolicy(callback, predicate)
                ?? bodyParts?.GetPolicy(callback, predicate) ?? embalming?.GetPolicy(callback, predicate)
                ?? graveDecorations?.GetPolicy(callback, predicate) ?? zombieEquipment?.GetPolicy(callback, predicate)
                ?? ResurrectionCollars?.GetPolicy(callback, predicate) ?? sermons?.GetPolicy(callback, predicate)
                ?? PrMachine?.GetPolicy(data, callback, predicate);
            policies = policy != null ? new[] { policy } : studyTable?.GetPolicies(callback, predicate);
            if (policies == null)
                return;
            foreach (var section in policies)
                if (!section.IsEnabled)
                    return;
            policies = inventoryNames.Apply(policies);
            session = new ApplicableItemsSession(this, policies, data, predicate);
            if (policies[0].Id == PickerId.PrMachineResources) PrMachine!.Bind(session, predicate);
            session.Commit();
        }
        catch (Exception exception)
        {
            session?.Dispose();
            policy = policy ?? (policies != null && policies.Count > 0 ? policies[0] : null);
            var title = policy?.Title ?? "item";
            WarnOnce("construction-" + (policy?.Id.ToString() ?? "item"), "Leaving the original " + title +
                " picker in place because consolidation was unsafe. " + exception);
        }
    }

    private void LateUpdate()
    {
        windowSorting?.Tick();
        ShopSelling?.Tick();
        PrMachine?.Tick();
        foreach (var session in sessions.ToArray())
        {
            try
            {
                session.Tick();
            }
            catch (Exception exception)
            {
                session.RequestFallback("updating", exception);
                session.RestoreVanilla();
            }
        }
        Alchemy?.Tick();
        ResurrectionCollarSlot?.Tick();
        StudySelectedSlot?.Tick();
    }

    internal void Register(ApplicableItemsSession session) => sessions.Add(session);
    internal void Unregister(ApplicableItemsSession session) => sessions.Remove(session);

    internal void WarnOnce(string key, string message)
    {
        if (warnings.Add(key))
            Logger.LogWarning(message);
    }

    private void OnDestroy()
    {
        ShutdownPrMachine();
        ShutdownShopSelling();
        try { windowSorting?.Dispose(); }
        catch (Exception exception)
        { WarnOnce("window-sorting-shutdown", "Window sorting cleanup failed; its hooks are inactive. " + exception); }
        finally { windowSorting = null; }
        foreach (var session in sessions.ToArray())
            session.RestoreVanilla();
        Alchemy?.Dispose();
        StudySelectedSlot?.Dispose();
        studyDisplayHarmony?.UnpatchSelf();
        StudySelectedSlot = null;
        ResurrectionCollarSlot?.Dispose();
        resurrectionDisplayHarmony?.UnpatchSelf();
        ResurrectionCollarSlot = null;
        alchemyHarmony?.UnpatchSelf();
        resurrectionHarmony?.UnpatchSelf();
        ResurrectionCollars = null;
        harmony?.UnpatchSelf();
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;

// Standalone host for the game's shipped Mono and Harmony. Does not launch Unity,
// load game scenes, install anything, or execute game assembly methods.
internal static class Program
{
    private static int calls;
    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static int Run(string[] args)
    {
        if (args.Length == 3 && args[0] == "--inside-mono") return Probe(args[1], args[2]);
        if (args.Length != 2) throw new ArgumentException("Expected game-install and Managed-copy paths.");
        string mono = Path.Combine(args[0], "MonoBleedingEdge");
        if (!SetDllDirectory(Path.Combine(mono, "EmbedRuntime"))) throw new InvalidOperationException("SetDllDirectory failed.");
        mono_set_assemblies_path(AppDomain.CurrentDomain.BaseDirectory + ";" + args[1]);
        mono_set_dirs(args[1], Path.Combine(mono, "etc"));
        mono_config_parse(null);
        IntPtr domain = mono_jit_init_version("OnlyApplicableItemsChecks", "v4.0.30319");
        if (domain == IntPtr.Zero) throw new InvalidOperationException("Mono initialization failed.");
        string executable = Assembly.GetExecutingAssembly().Location;
        IntPtr assembly = mono_domain_assembly_open(domain, executable);
        if (assembly == IntPtr.Zero) throw new InvalidOperationException("Mono could not load probe.");
        return mono_jit_exec(domain, assembly, 4, new[] { executable, "--inside-mono", args[0], args[1] });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Probe(string gamePath, string managedPath)
    {
        Console.WriteLine("Runtime: " + Type.GetType("Mono.Runtime")?.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null));
        Console.WriteLine("Harmony: " + typeof(Harmony).Assembly.GetName().Version);
        var harmony = new Harmony("onlyapplicableitems.generic-probe");
        var definition = typeof(GenericStack).GetMethod(nameof(GenericStack.Add))!;
        harmony.Patch(definition.MakeGenericMethod(typeof(Data)), prefix: new HarmonyMethod(typeof(Program), nameof(Before)));
        InvokeBoth();
        Console.WriteLine("Canonical generic patch callbacks: " + calls);
        harmony.UnpatchSelf();
        if (calls != 2) throw new InvalidOperationException("Reference-type generic sharing was not observed.");
        SortingRuntimeChecks.Run(managedPath);
        ShopMetadataChecks.Run(managedPath);
        PrMachineRuntimeChecks.Run(managedPath);
        ConfigurationRuntimeChecks.Run(gamePath);
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InvokeBoth()
    {
        GenericStack.Add(new Window<DataA>());
        GenericStack.Add(new Window<DataB>());
    }
    private static void Before(object __0) { calls++; }
    private class Data { }
    private sealed class DataA : Data { }
    private sealed class DataB : Data { }
    private class Window<T> where T : Data { public int Value; }
    private static class GenericStack
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Add<T>(Window<T> window) where T : Data { window.Value++; }
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetDllDirectory(string path);
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void mono_set_assemblies_path(string path);
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void mono_set_dirs(string assemblyDir, string configDir);
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void mono_config_parse(string? path);
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_jit_init_version(string name, string version);
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_domain_assembly_open(IntPtr domain, string path);
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int mono_jit_exec(IntPtr domain, IntPtr assembly, int argc, [In] string[] argv);
}

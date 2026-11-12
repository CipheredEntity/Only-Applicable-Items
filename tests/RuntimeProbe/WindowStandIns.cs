// Only the window and canvas boundary is simulated. The controller is linked
// unchanged from the decompiled reference, and production Harmony hooks run on Mono.
namespace UnityEngine
{
    public sealed class Canvas
    {
        private int order;
        public int Writes { get; private set; }
        public int sortingOrder { get => order; set { order = value; Writes++; } }
    }
}
namespace LazyBearTechnology
{
    public class LazyWidgetDataBase { }
    public class LazyWidgetBase { }
    public class LazyWindow<T> : LazyWidgetBase where T : LazyWidgetDataBase
    {
        public bool IsModalWindow { get; set; } = true;
        public UnityEngine.Canvas Canvas { get; } = new UnityEngine.Canvas();
    }
}

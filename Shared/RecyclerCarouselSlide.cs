namespace Zebble.Plugin
{
    public abstract class RecyclerCarouselSlide<TSource> : Stack, IRecyclerCarouselSlide<TSource>
    {
        public Bindable<TSource> Item { get; } = new Bindable<TSource>();
    }
}
namespace Zebble.Plugin
{
    using Olive;

    public abstract class RecyclerCarouselSlide<TSource> : Stack, IRecyclerCarouselSlide<TSource>
    {
        public Bindable<TSource> Item { get; } = new Bindable<TSource>();
    }
}
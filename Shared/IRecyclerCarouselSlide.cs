namespace Zebble.Plugin
{
    using Olive;

    public interface IRecyclerCarouselSlide<TSource>
    {
        Bindable<TSource> Item { get; }
    }
}
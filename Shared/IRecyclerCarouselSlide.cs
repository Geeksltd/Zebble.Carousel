namespace Zebble.Plugin
{
    public interface IRecyclerCarouselSlide<TSource>
    {
        Bindable<TSource> Item { get; }
    }
}
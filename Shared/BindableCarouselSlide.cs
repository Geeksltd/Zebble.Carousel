namespace Zebble.Plugin
{
    using Olive;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Zebble.Plugin;

    public interface IBindableCarouselSlide<TSource>
    {
        Bindable<TSource> Item { get; }
    }

    public class BindableCarouselSlide<TSource> : Stack, IBindableCarouselSlide<TSource>
    {
        public Bindable<TSource> Item { get; } = new Bindable<TSource>();
    }
}

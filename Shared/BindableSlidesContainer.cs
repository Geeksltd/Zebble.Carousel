using Olive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zebble;
using Zebble.Plugin;
using static Zebble.Plugin.Carousel;

namespace Zebble
{
    public class BindableSlidesContainer<TSource, TSlideTemplate> : Stack
         where TSlideTemplate : View, IBindableCarouselSlide<TSource>, new()
         where TSource : class
    {
        public BindableCarousel<TSource, TSlideTemplate> Carousel { get; set; }
        public BindableSlidesContainer(BindableCarousel<TSource, TSlideTemplate> carousel)
        {
            Direction = RepeatDirection.Horizontal;
            Carousel = carousel;
            this.Id("SlidesContainer")
                .Height(100.Percent());
        }

        public Task<TSlideTemplate> AddSlideAt(int index, TSlideTemplate child, bool awaitNative = false)
            => Carousel.AddSlideAt(index, child, awaitNative);

        public bool Zoomed
        {
            get
            {
                var zoomableChildren = this.CurrentChildren.OfType<ZoomableSlide>();
                if (!zoomableChildren.Any()) return false;
                return zoomableChildren.Any(x => !x.Zoom.AlmostEquals(1, 0.1f));
            }
        }
    }
}

namespace Zebble.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public partial class Carousel
    {
        public class CarouselSlides
        {
            Carousel Carousel;
            internal CarouselSlides(Carousel carousel) { Carousel = carousel; }
            public Task<Slide> Add(View slide) => Carousel.AddSlide(slide);
            public int Count => Carousel.SlidesContainer.CurrentChildren.Count();
            public bool Zoomed => Carousel.SlidesContainer.CurrentChildren.Any(x => !(x as ScrollView).Zoom.AlmostEquals(1, 0.1f));

            public virtual async Task AddRange<T>(IEnumerable<T> slides) where T : View
            {
                foreach (var s in slides) await Add(s);
            }
        }
    }
}
namespace Zebble.Plugin
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public partial class Carousel
    {
        public class CarouselSlides
        {
            Carousel Carousel;
            internal CarouselSlides(Carousel carousel) => Carousel = carousel;
            public Task<View> Add(View slide) => Carousel.AddSlide(slide);

            public virtual async Task AddRange<T>(IEnumerable<T> slides) where T : View
            {
                foreach (var s in slides) await Add(s);
            }
        }
    }
}
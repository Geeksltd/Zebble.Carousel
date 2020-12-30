namespace Zebble.Plugin
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Olive;

    public partial class Carousel
    {
        public class CarouselSlides
        {
            Carousel Carousel;
            internal CarouselSlides(Carousel carousel) { Carousel = carousel; }
            public Task<View> Add(View slide) => Carousel.AddSlide(slide);

            public bool Zoomed
            {
                get
                {
                    var zoomableChildren = Carousel.SlidesContainer.CurrentChildren.OfType<ZoomableSlide>();
                    if (!zoomableChildren.Any()) return false;
                    return zoomableChildren.Any(x => !x.Zoom.AlmostEquals(1, 0.1f));
                }
            }

            public virtual async Task AddRange<T>(IEnumerable<T> slides) where T : View
            {
                foreach (var s in slides) await Add(s);
            }
        }
    }
}
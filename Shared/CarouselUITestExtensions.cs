namespace Zebble.Plugin
{
    public static class CarouselUITestExtensions
    {
        public static void SwipeCarousel(this IBaseUITest @this, Direction direction = Direction.Left, View thisCarousel = null)
        {
            var delay = 200;

#if ANDROID
            delay = 500;
#endif

            @this.Delay(delay);

            Carousel carousel;

            if (thisCarousel == null) carousel = @this.Find<Carousel>();
            else carousel = thisCarousel as Carousel;

            if (direction == Direction.Left) carousel.Next(animate: false);
            else if (direction == Direction.Right) carousel.Previous(animate: false);
        }
    }
}
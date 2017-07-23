namespace Zebble.Plugin
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class Carousel : Stack
    {
        const int DEFAULT_HEIGHT = 300;

        int CurrentSlideIndex;
        public bool EnableZooming;
        public readonly CarouselSlides Slides;
        public readonly Stack BulletsContainer;
        readonly Stack SlidesContainer;
        AsyncLock SwipeSyncLock = new AsyncLock();

        public Carousel()
        {
            Height.Set(DEFAULT_HEIGHT);
            BulletsContainer = new Stack(RepeatDirection.Horizontal).Id("BulletsContainer").Absolute();
            SlidesContainer = new Stack(RepeatDirection.Horizontal).Id("SlidesContainer").Height(100.Percent());
            Slides = new CarouselSlides(this);

            Width.Changed.HandleWith(CarouselWidthChanged);
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            await Add(SlidesContainer);
            await CreateBulletContainer();
            await ApplySelectedBullet();

            Swiped.Handle(OnSwipped);
        }

        async Task OnSwipped(SwipedEventArgs args)
        {
            if (Slides.Zoomed) return;

            using (await SwipeSyncLock.LockAsync())
            {
                if (args.Direction == Zebble.Direction.Left) await Next();
                if (args.Direction == Zebble.Direction.Right) await Previous();
            }
        }

        async Task CreateBulletContainer()
        {
            await Add(BulletsContainer);

            foreach (var c in BulletsContainer.AllChildren<Bullet>())
                await c.SetPseudoCssState("active", set: false);

            await (BulletsContainer.AllChildren<Bullet>().Skip(CurrentSlideIndex)
                  .FirstOrDefault()?.SetPseudoCssState("active", set: true)).OrCompleted();
        }

        Task AddBullet() => BulletsContainer.Add(new Bullet());

        public override async Task OnPreRender()
        {
            await base.OnPreRender();

            PositionBullets();

            SlidesContainer.Width(SlidesContainer.CurrentChildren.Count() * ActualWidth);
        }

        void PositionBullets()
        {
            BulletsContainer.Y.BindTo(Height, BulletsContainer.Height, BulletsContainer.Margin.Bottom, (x, y, mb) => x - y - mb);
        }

        void CarouselWidthChanged() => SlidesContainer.AllChildren.Do(x => x.Width(ActualWidth));

        public async Task<Slide> AddSlide(View child)
        {
            var slide = new Slide { EnableZooming = EnableZooming }.Set(x => x.Width.BindTo(Width));

            await slide.Add(child);
            await SlidesContainer.Add(slide);
            await AddBullet();
            return slide;
        }

        public async Task Next(bool animate = true)
        {
            if (CurrentSlideIndex >= Slides.Count - 1) return;

            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex++;

            if (animate)
            {
                SlidesContainer.Animate(c => ApplySelectedBulletAnimation(oldSlideIndex, CurrentSlideIndex)).RunInParallel();
            }
            else
            {
                SlidesContainer.X(SlidesContainer.X.CurrentValue - ActualWidth);

                await ApplySelectedBullet();
            }
        }

        public async Task Previous(bool animate = true)
        {
            if (CurrentSlideIndex <= 0) return;

            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex--;

            if (animate)
            {
                SlidesContainer.Animate(t => ApplySelectedBulletAnimation(oldSlideIndex, CurrentSlideIndex)).RunInParallel();
            }
            else
            {
                SlidesContainer.X(SlidesContainer.X.CurrentValue + ActualWidth);

                await ApplySelectedBullet();
            }
        }

        void ApplySelectedBulletAnimation(int oldBulletIndex, int currentBulletIndex)
        {
            var bullets = BulletsContainer.AllChildren<Bullet>().ToList();
            var oldBullet = bullets[oldBulletIndex];
            var currentBullet = bullets[currentBulletIndex];

            if (currentBulletIndex > oldBulletIndex)
            {
                SlidesContainer.X(SlidesContainer.X.CurrentValue - ActualWidth);
            }
            else
            {
                SlidesContainer.X(SlidesContainer.X.CurrentValue + ActualWidth);
            }

            oldBullet.SetPseudoCssState("active", set: false).RunInParallel();
            currentBullet.SetPseudoCssState("active", set: true).RunInParallel();
        }

        async Task ApplySelectedBullet()
        {
            var bullets = BulletsContainer.AllChildren<Bullet>().ToList();

            var current = bullets.Skip(CurrentSlideIndex).FirstOrDefault();
            if (current == null) return;

            foreach (var c in bullets)
                await c.SetPseudoCssState("active", c == current);
        }

        public class CarouselSlides
        {
            Carousel Carousel;
            internal CarouselSlides(Carousel carousel) { Carousel = carousel; }
            public Task<Slide> Add(View slide) => Carousel.AddSlide(slide);
            public int Count => Carousel.SlidesContainer.CurrentChildren.Count();
            public bool Zoomed => Carousel.SlidesContainer.CurrentChildren.Any(x => !(x as ScrollView).Zoom.AlmostEquals(1, 0.1f));
        }

        public class Slide : ScrollView { }

        public class Bullet : Canvas { }
    }
}
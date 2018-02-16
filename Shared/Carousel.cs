namespace Zebble.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class Carousel : Stack
    {
        const int DEFAULT_HEIGHT = 300, ACCEPTED_PAN_VALUE = 30;
#if ANDROID
        const int VELOCITY_VALUE = 300;
#elif UWP
        const int VELOCITY_VALUE = 1;
#elif IOS
        const int VELOCITY_VALUE = 30;
#endif

        const float HALF_SECOUND = 0.5f;

        float? slideWidth;
        int CurrentSlideIndex;
        float CurrentXPosition;
        public readonly CarouselSlides Slides;
        public readonly Stack BulletsContainer;
        readonly Stack SlidesContainer;
        Direction? LastDirection;
        DateTime? PanningDuration;

        bool ZoomingStatus, DirectionHasChanged;

        public bool CenterAligned { get; set; } = true;

        public bool EnableZooming
        {
            get { return ZoomingStatus; }
            set
            {
                if (value == ZoomingStatus) return;
                ZoomingStatus = value;
                ZoomStatusChanged(value);
            }
        }

        public float? SlideWidth
        {
            get => slideWidth;
            set
            {
                slideWidth = value;
                SlidesContainer.CurrentChildren.Do(x => x.Width(slideWidth));
            }
        }

        public Carousel()
        {
            Height.Set(DEFAULT_HEIGHT);
            BulletsContainer = new Stack(RepeatDirection.Horizontal).Id("BulletsContainer").Absolute();
            SlidesContainer = new Stack(RepeatDirection.Horizontal).Id("SlidesContainer").Height(100.Percent());
            Slides = new CarouselSlides(this);
        }

        public override async Task<TView> Add<TView>(TView child, bool awaitNative = false)
        {
            if (child.IsAnyOf(BulletsContainer, SlidesContainer))
                await base.Add(child, awaitNative);
            else
                await AddSlide(child);

            return child;
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            await Add(SlidesContainer);
            await CreateBulletContainer();
            await ApplySelectedBullet();

            await WhenShown(() => ApplySelectedWithoutAnimation(0, 0));

            ZoomStatusChanged(ZoomingStatus);

            Panning.Handle(OnPanning);
            PanFinished.Handle(OnPanFinished);
        }

        async Task OnPanning(PannedEventArgs args)
        {
            if (Slides.Zoomed) return;

            var difference = args.From.X - args.To.X;
            SlidesContainer.X(SlidesContainer.X.CurrentValue - difference);
        }

        async Task OnPanFinished(PannedEventArgs args)
        {
            if (Slides.Zoomed) return;
                        
            var velocity = Math.Abs(args.Velocity.X);

            if (velocity >= VELOCITY_VALUE)
                await DoMove(args.Velocity);
            else 
                await StayOnBestMatch();
        }

        async Task StayOnBestMatch()
        {
            var index = -(int)Math.Round((SlidesContainer.ActualX - XPositionOffset) / InternalSlideWidth);

            if (index < 0) index = 0;
            if (index > Slides.Count - 1) index = Slides.Count - 1;

            await SlidesContainer.Animate(t => ApplySelectedBulletAnimation(CurrentSlideIndex, index));
            CurrentSlideIndex = index;
        }

        async Task DoMove(Point velocity, bool withLastMove = false)
        {
            Direction? direction;
            if (withLastMove)
            {
                direction = LastDirection;
                if (direction == Zebble.Direction.Right) await Previous();
                else if (direction == Zebble.Direction.Left) await Next();
            }
            else
            {
                direction = await CheckDirection(velocity);
                if (direction == Zebble.Direction.Right) await Previous();
                else if (direction == Zebble.Direction.Left) await Next();
            }

            await Task.CompletedTask;
        }

        Task<Direction?> CheckDirection(Point velocity)
        {
            Direction? result;
            if (velocity.X > 0) result = Zebble.Direction.Right;
            else if (velocity.X < 0) result = Zebble.Direction.Left;
            else result = null;

            return Task.FromResult(result);
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

            SetContainerWidth();
        }

        void SetContainerWidth() => SlidesContainer.Width(SlidesContainer.CurrentChildren.Count() * InternalSlideWidth);

        float InternalSlideWidth => SlideWidth ?? ActualWidth;

        void PositionBullets()
        {
            BulletsContainer.Y.BindTo(Height, BulletsContainer.Height, BulletsContainer.Margin.Bottom, (x, y, mb) => x - y - mb);
        }

        public async Task<Slide> AddSlide(View child)
        {
            var slide = new Slide().Width(InternalSlideWidth);

            await slide.Add(child);
            await SlidesContainer.Add(slide);
            await AddBullet();

            SetContainerWidth();

            return slide;
        }

        void ZoomStatusChanged(bool value)
        {
            if (SlidesContainer == null || SlidesContainer.CurrentChildren.None()) return;

            foreach (var child in SlidesContainer.CurrentChildren)
            {
                var slide = child as Slide;
                if (slide == null) continue;
                slide.EnableZooming = value;
            }
        }

        public async Task Next(bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex++;

            if (CurrentSlideIndex >= Slides.Count - 1) CurrentSlideIndex = Slides.Count - 1;

            if (animate)
            {
                SlidesContainer.Animate(c => ApplySelectedBulletAnimation(oldSlideIndex, CurrentSlideIndex)).RunInParallel();
            }
            else
            {
                await ApplySelectedWithoutAnimation(CurrentSlideIndex, oldSlideIndex);
            }
        }

        public async Task Previous(bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex--;

            if (CurrentSlideIndex <= 0) CurrentSlideIndex = 0;

            if (animate)
            {
                SlidesContainer.Animate(t => ApplySelectedBulletAnimation(oldSlideIndex, CurrentSlideIndex)).RunInParallel();
            }
            else
            {
                await ApplySelectedWithoutAnimation(CurrentSlideIndex, oldSlideIndex);
            }
        }
        
        async Task ApplySelectedWithoutAnimation(int currentSlideIndex, int oldSlideIndex)
        {
            SlidesContainer.X(XPositionOffset - currentSlideIndex * InternalSlideWidth);

            await ApplySelectedBullet();
        }

        float XPositionOffset => CenterAligned ? (ActualWidth - InternalSlideWidth) / 2 : 0;

        void ApplySelectedBulletAnimation(int oldBulletIndex, int currentBulletIndex)
        {
            var bullets = BulletsContainer.AllChildren<Bullet>().ToList();
            var oldBullet = bullets[oldBulletIndex];
            var currentBullet = bullets[currentBulletIndex];

            SlidesContainer.X(XPositionOffset - currentBulletIndex * InternalSlideWidth);

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

            public virtual async Task AddRange<T>(IEnumerable<T> slides) where T : View
            {
                foreach (var s in slides) await Add(s);
            }
        }

        public class Slide : ScrollView { }

        public class Bullet : Canvas { }
    }
}
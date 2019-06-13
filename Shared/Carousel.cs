namespace Zebble.Plugin
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public partial class Carousel : Stack
    {
        const int DEFAULT_HEIGHT = 300, ACCEPTED_PAN_VALUE = 5;

#if IOS
        const float VELOCITY_VALUE = 30;
#else
        const float VELOCITY_VALUE = 0.20f;
#endif

        bool IsAnimating;
        float? slideWidth;
        public int CurrentSlideIndex { get; private set; }
        public readonly CarouselSlides Slides;
        public readonly AsyncEvent SlideChanged = new AsyncEvent();
        public readonly AsyncEvent SlideChanging = new AsyncEvent();
        public readonly Stack SlidesContainer;
        bool enableZooming;

        public bool CenterAligned { get; set; } = true;

        public bool EnableZooming
        {
            get { return enableZooming; }
            set
            {
                if (value == enableZooming) return;
                enableZooming = value;
            }
        }

        public float? SlideWidth
        {
            get => slideWidth;
            set
            {
                slideWidth = value;
                SlidesContainer.AllChildren.Do(x => x.Width(slideWidth));
            }
        }

        public Carousel()
        {
            Height.Set(DEFAULT_HEIGHT);
            BulletsContainer = new Stack(RepeatDirection.Horizontal).Id("BulletsContainer").Absolute().Visible(value: false);
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

            Panning.Handle(OnPanning);
            PanFinished.Handle(OnPanFinished);
        }

        async Task OnPanning(PannedEventArgs args)
        {
            if (IsAnimating) return;

            if (!Slides.Zoomed)
            {
                var difference = args.From.X - args.To.X;

                SlidesContainer.X(SlidesContainer.X.CurrentValue - difference);
            }
        }

        Task OnPanFinished(PannedEventArgs args)
        {
            if (Slides.Zoomed) return Task.CompletedTask;

            var velocity = Math.Abs(args.Velocity.X);

            if (velocity >= VELOCITY_VALUE)
            {
                if (args.Velocity.X > 0) return Previous();
                else return Next();
            }

            CurrentSlideIndex = GetBestMatchIndex();

            IsAnimating = true;
            return SlidesContainer.Animate(x => SetPosition(CurrentSlideIndex))
                .ContinueWith(x => IsAnimating = false);
        }

        int GetBestMatchIndex()
        {
            var result = -(int)Math.Round((SlidesContainer.ActualX - XPositionOffset) / InternalSlideWidth);
            return result.LimitMin(0).LimitMax(CountSlides() - 1);
        }

        Task<Direction?> GetDirection(Point velocity)
        {
            Direction? result;
            if (velocity.X > 0) result = Zebble.Direction.Right;
            else if (velocity.X < 0) result = Zebble.Direction.Left;
            else result = null;

            return Task.FromResult(result);
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            PositionBullets();
            AdjustContainerWidth();
        }

        protected virtual int CountSlides() => Slides.Count;

        void AdjustContainerWidth() => SlidesContainer.Width(CountSlides() * InternalSlideWidth);

        float InternalSlideWidth => SlideWidth ?? ActualWidth;

        public async Task<View> AddSlide(View child)
        {
            View slide;
            if (EnableZooming) slide = new ZoomableSlide() { EnableZooming = true };
            else slide = new Slide();

            slide.Width(InternalSlideWidth);

            await slide.Add(child);
            await SlidesContainer.Add(slide);
            await AddBullet();

            HandleVisibility(child, slide);
            AdjustContainerWidth();

            return slide;
        }

        void HandleVisibility(View child, View slide)
        {
            slide.Ignored = child.Ignored;
            slide.Visible = child.Visible;

            child.IgnoredChanged.Handle(x =>
            {
                slide.Ignored = x.Value;
                AdjustContainerWidth();
            });

            child.VisibilityChanged.Handle(() => slide.Visible = child.Visible);
        }

        public async Task RemoveSlide(View child)
        {
            if (child.Parent == null)
            {
                Device.Log.Error("[Carousel Slide] the current child is not exist in the specefic carousel");
                return;
            }

            await SlidesContainer.Remove(child.Parent);
            await RemoveLastBullet();
            AdjustContainerWidth();
        }

        public async Task Next(bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex++;

            if (CurrentSlideIndex >= CountSlides() - 1) CurrentSlideIndex = CountSlides() - 1;
            await SlideChanging.Raise();
            await MoveSlide(animate, oldSlideIndex);
            await SlideChanged.Raise();
        }

        public async Task Previous(bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex--;

            if (CurrentSlideIndex <= 0) CurrentSlideIndex = 0;
            await SlideChanging.Raise();
            await MoveSlide(animate, oldSlideIndex);
            await SlideChanged.Raise();
        }

        public async Task ShowFirst(bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex = 0;

            await MoveSlide(animate, oldSlideIndex);
        }

        public async Task ShowLast(bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;
            CurrentSlideIndex = CountSlides() - 1;

            await MoveSlide(animate, oldSlideIndex);
        }

        async Task MoveSlide(bool animate, int oldSlideIndex)
        {
            if (animate)
            {
                BulletsContainer.Animate(c => SetHighlightedBullet(oldSlideIndex, CurrentSlideIndex)).RunInParallel();
                IsAnimating = true;
                SlidesContainer.Animate(c => SetPosition(CurrentSlideIndex))
                    .ContinueWith(x => IsAnimating = false).RunInParallel();
            }
            else
            {
                await ApplySelectedWithoutAnimation(CurrentSlideIndex, oldSlideIndex);
            }
        }

        async Task ApplySelectedWithoutAnimation(int currentSlideIndex, int oldSlideIndex)
        {
            SetPosition(currentSlideIndex);
            await ApplySelectedBullet();
        }

        protected void SetPosition(int currentIndex) => SlidesContainer.X(XPositionOffset - currentIndex * InternalSlideWidth);

        public float XPositionOffset => CenterAligned ? (ActualWidth - InternalSlideWidth) / 2 : 0;

        public class Slide : Stack { }

        public class ZoomableSlide : ScrollView { }

        public override void Dispose()
        {
            SlideChanging?.Dispose();
            SlideChanged?.Dispose();
            base.Dispose();
        }
    }
}
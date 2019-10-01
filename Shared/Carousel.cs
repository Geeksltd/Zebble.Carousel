namespace Zebble.Plugin
{
    using System;
    using System.Threading.Tasks;

    public partial class Carousel : Stack
    {
        public enum StickinessOption { High, Normal, Low }

        const int DEFAULT_HEIGHT = 300;

        public StickinessOption Stickiness { get; set; } = StickinessOption.Normal;

        bool IsAnimating;
        float? slideWidth;
        public int CurrentSlideIndex { get; private set; }
        public readonly CarouselSlides Slides;
        public readonly AsyncEvent SlideChanged = new AsyncEvent();
        public readonly AsyncEvent SlideChanging = new AsyncEvent();
        public readonly AsyncEvent SlideWidthChanged = new AsyncEvent();
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
                SlideWidthChanged?.Raise();
            }
        }

        float StickVelocity
        {
            get
            {
                var multiplier = 1f;
                if (Stickiness == StickinessOption.High) multiplier = 2;
                if (Stickiness == StickinessOption.Low) multiplier = 0.5f;
#if IOS
                return multiplier * 30;
#else
                return multiplier * 0.20f;
#endif
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

            if (velocity >= StickVelocity)
            {
                Device.Log.Message("Pan finished fast " + (args.Velocity.X > 0 ? "Back" : "Forward"));

                if (args.Velocity.X > 0)
                {
                    // Find the index before the current x.
                    var index = (int)Math.Floor(-SlidesContainer.ActualX / SlideWidth ?? ActualWidth);
                    return MoveToSlide(index);
                }
                else
                {
                    var index = (int)Math.Ceiling(-SlidesContainer.ActualX / SlideWidth ?? ActualWidth);
                    return MoveToSlide(index);
                }
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

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            PositionBullets();
            AdjustContainerWidth();
        }

        protected virtual int CountSlides() => Slides.Count;

        protected void AdjustContainerWidth() => SlidesContainer.Width(CountSlides() * InternalSlideWidth);

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

        public Task Next(bool animate = true) => MoveToSlide(CurrentSlideIndex + 1, animate);
        public Task Previous(bool animate = true) => MoveToSlide(CurrentSlideIndex - 1, animate);
        public Task ShowFirst(bool animate = true) => MoveToSlide(0, animate);
        public Task ShowLast(bool animate = true) => MoveToSlide(CountSlides() - 1, animate);

        public async Task MoveToSlide(int index, bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;

            index = index.LimitMin(0).LimitMax(CountSlides() - 1);
            if (index == -1) // No slide available!!
                return;

            CurrentSlideIndex = index;
            await SlideChanging.Raise();

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

            Device.Log.Message("Moved from slide " + oldSlideIndex + " to " + CurrentSlideIndex);

            await SlideChanged.Raise();
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
            SlideWidthChanged?.Dispose();
            base.Dispose();
        }
    }
}
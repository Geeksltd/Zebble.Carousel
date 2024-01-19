namespace Zebble.Plugin
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Olive;

    public partial class Carousel : Stack
    {
        public enum StickinessOption { High, Normal, Low }

        const int DEFAULT_HEIGHT = 300;

        public StickinessOption Stickiness { get; set; } = StickinessOption.Normal;

        bool IsAnimating;
        float? slideWidth;
        protected bool ShouldResetCurrentSlide = true;

        int currentSlideIndex;
        public int CurrentSlideIndex
        {
            get => currentSlideIndex;
            set
            {
                ShouldResetCurrentSlide = false;
                if (IsShown) MoveToSlide(value).GetAwaiter();
                else
                {
                    ApplySelectedWithoutAnimation(value).GetAwaiter();
                    WhenShown(() => CurrentSlideIndex = value).GetAwaiter();
                }
            }
        }

        public readonly CarouselSlides Slides;
        public readonly SlidesContainer SlidesContainer = new();
        public readonly AsyncEvent SlideChanged = new();
        public readonly AsyncEvent SlideChanging = new();
        public readonly AsyncEvent SlideWidthChanged = new();
        public readonly AsyncEvent SlidesEnded = new();

        public bool ShowBullets { get; set; } = true;

        public bool CenterAligned { get; set; } = true;

        public float? SlideWidth
        {
            get => slideWidth;
            set
            {
                slideWidth = value;
                SlidesContainer.ArrangeSlides(value ?? ActualWidth);
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
            Slides = new CarouselSlides(this);
        }

        public override async Task<TView> Add<TView>(TView child, bool awaitNative = false)
        {
            if (child.IsAnyOf(BulletsContainer, SlidesContainer))
                return await base.Add(child, awaitNative);

            return await AddSlide(child);
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            await Add(SlidesContainer);
            SlidesContainer.ArrangeSlides(SlideWidth ?? ActualWidth);

            await CreateBulletContainer();
            await ApplySelectedBullet();
            await WhenShown(OnShown);

            RaiseGesturesOnUIThread();
            Panning.FullEvent += OnPanning;
            PanFinished.FullEvent += OnPanFinished;
        }

        async Task OnShown()
        {
            if (ShouldResetCurrentSlide == false) return;
            await ApplySelectedWithoutAnimation(0);
            await PrepareForShiftTo(1);
        }

        void OnPanning(PannedEventArgs args)
        {
            if (IsAnimating) return;

            var horizontalDifference = args.From.X - args.To.X;
            var verticalDifference = args.From.Y - args.To.Y;
            if (Math.Abs(verticalDifference) > Math.Abs(horizontalDifference))
                return;
            SlidesContainer.X(SlidesContainer.X.CurrentValue - horizontalDifference);
            PrepareForShiftTo(GetBestMatchIndex()).RunInParallel();
        }

        public int ConcurrentlyVisibleSlides => (int)Math.Ceiling(ActualWidth / InternalSlideWidth);

        void OnPanFinished(PannedEventArgs args)
        {
            var landOn = GetBestMatchIndex();

            var fast = Math.Abs(args.Velocity.X) >= StickVelocity;

            if (fast)
            {
                var position = -SlidesContainer.ActualX / SlideWidth ?? ActualWidth;

                if (args.Velocity.X > 0) landOn = (int)Math.Floor(position);
                else landOn = (int)Math.Ceiling(position);
            }

            MoveToSlide(landOn).RunInParallel();
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

        public virtual int CountSlides() => SlidesContainer.CurrentChildren.Count();

        protected void AdjustContainerWidth() => SlidesContainer.Width(CountSlides() * InternalSlideWidth);

        protected float InternalSlideWidth => SlideWidth ?? ActualWidth;

        public async Task<TView> AddSlide<TView>(TView child) where TView : View
        {
            await SlidesContainer.Add(child);

            if (ShouldAddBulletWithSlide()) await AddBullet();

            AdjustContainerWidth();

            return child;
        }

        protected virtual bool ShouldAddBulletWithSlide() => true;

        public virtual async Task RemoveSlide(View child)
        {
            if (child == null)
            {
                Log.For(this).Error("[Carousel Slide] the current child is not exist in the specefic carousel");
                return;
            }

            await SlidesContainer.Remove(child);
            await RemoveLastBullet();
            AdjustContainerWidth();
        }

        public Task Next(bool animate = true) => MoveToSlide(currentSlideIndex + 1, animate);

        public Task Previous(bool animate = true) => MoveToSlide(currentSlideIndex - 1, animate);

        public Task ShowFirst(bool animate = true) => MoveToSlide(0, animate);

        public Task ShowLast(bool animate = true) => MoveToSlide(CountSlides() - 1, animate);

        public async Task MoveToSlide(int index, bool animate = true)
        {
            var oldSlideIndex = currentSlideIndex;

            index = index.LimitMin(0);
            if (index >= CountSlides())
            {
                await SlidesEnded.Raise();
                if (CountSlides() > 0) await ShowLast(true);
                return; // No slide available!!
            }

            var actuallyChanged = index != oldSlideIndex;

            if (actuallyChanged)
            {
                if (!ShouldResetCurrentSlide)
                    await PrepareForShiftTo(index).ConfigureAwait(false);
                currentSlideIndex = index;
                await SlideChanging.Raise();
            }

            if (animate)
            {
                if (ShowBullets)
                    BulletsContainer.Animate(c => SetHighlightedBullet(oldSlideIndex, index)).RunInParallel();

                IsAnimating = true;
                SlidesContainer.Animate(c => SetPosition(index)).ContinueWith(x => IsAnimating = false).RunInParallel();
            }
            else
            {
                await ApplySelectedWithoutAnimation(index);
            }

            if (actuallyChanged) await SlideChanged.Raise();
        }

        protected virtual Task PrepareForShiftTo(int slideIndex) => Task.CompletedTask;

        async Task ApplySelectedWithoutAnimation(int currentSlideIndex)
        {
            SetPosition(currentSlideIndex);
            await ApplySelectedBullet();
        }

        protected void SetPosition(int currentIndex)
        {
            currentIndex = currentIndex.LimitMax(CountSlides() + 1 - ConcurrentlyVisibleSlides).LimitMin(0);

            var x = XPositionOffset - currentIndex * InternalSlideWidth;

            SlidesContainer.X(x);
        }

        public float XPositionOffset => CenterAligned ? (ActualWidth - InternalSlideWidth) / 2 : 0;

        public override void Dispose()
        {
            SlideChanging?.Dispose();
            SlideChanged?.Dispose();
            SlideWidthChanged?.Dispose();
            base.Dispose();
        }
    }
}
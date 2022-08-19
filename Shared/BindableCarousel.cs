namespace Zebble.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Olive;
    using static Zebble.Plugin.Carousel;

    public class BindableCarousel<TSource, TSlideTemplate> : Stack
         where TSlideTemplate : View, IBindableCarouselSlide<TSource>, new()
         where TSource : class
    {
        const int DEFAULT_HEIGHT = 300;

        bool IsAnimating, enableZooming;
        List<TSource> dataSource = new();
        List<float> slidesActualWidth = new();

        public float XPositionOffset => CenterAligned ? (ActualWidth - InternalSlideWidth) / 2 : 0;
        protected float InternalSlideWidth
        {
            get
            {
                var i = GetMiddleSlideIndex();
                if (slidesActualWidth.Count > i)
                    return slidesActualWidth[i];
                return ActualWidth;
            }
        }

        public BindableSlidesContainer<TSource, TSlideTemplate> SlidesContainer { get; set; }
        public StickinessOption Stickiness { get; set; } = StickinessOption.Normal;
        public int CurrentSlideIndex { get; private set; }
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

        public readonly AsyncEvent SlideChanged = new();
        public readonly AsyncEvent SlideChanging = new();
        public readonly AsyncEvent SlidesEnded = new();

        public BindableCarousel()
        {
            SlidesContainer = new(this);
            Height.Set(DEFAULT_HEIGHT);
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();
            await Add(SlidesContainer);

            await WhenShown(OnShown);

            RaiseGesturesOnUIThread();
            Panning.FullEvent += OnPanning;
            PanFinished.FullEvent += OnPanFinished;
        }
        async Task OnShown()
        {
            SetPosition(0);
            await PrepareForShiftTo(1);
        }
        void OnPanning(PannedEventArgs args)
        {
            if (IsAnimating) return;
            if (SlidesContainer.Zoomed) return;

            var difference = args.From.X - args.To.X;
            SlidesContainer.X(SlidesContainer.X.CurrentValue - difference);
            PrepareForShiftTo(GetBestMatchIndex()).RunInParallel();
        }
        void OnPanFinished(PannedEventArgs args)
        {
            if (SlidesContainer.Zoomed) return;
            var landOn = GetBestMatchIndex();

            var fast = Math.Abs(args.Velocity.X) >= StickVelocity;

            if (fast)
            {
                var position = -SlidesContainer.ActualX / ActualWidth;

                if (args.Velocity.X > 0) landOn = (int)Math.Floor(position);
                else landOn = (int)Math.Ceiling(position);
            }

            MoveToSlide(landOn).RunInParallel();
        }
        int GetBestMatchIndex()
        {
            var result = -(int)Math.Round((SlidesContainer.ActualX - XPositionOffset) / ActualWidth);
            return result.LimitMin(0).LimitMax(CountSlides() - 1);
        }

        int GetMiddleSlideIndex()
        {
            var slidedWidth = Math.Abs(SlidesContainer.ActualX) + (ActualWidth / 2);
            float count = 0;
            for (int i = 0; i < slidesActualWidth.Count; i++)
            {
                var width = slidesActualWidth[i];
                count += width;
                if (count >= slidedWidth) return i;
            }
            return CountSlides() - 1;
        }

        public virtual int CountSlides() => SlidesContainer.CurrentChildren.Count();
        public Task<TSlideTemplate> AddSlide(TSlideTemplate child, bool awaitNative = false)
            => AddSlideAt(CountSlides(), child, awaitNative);

        public async Task<TSlideTemplate> AddSlideAt(int index, TSlideTemplate child, bool awaitNative = false)
        {
            dataSource.Insert(index, child.Item.Value);
            await SlidesContainer.AddAt(index, child, awaitNative);
            child.Item.Changed += UpdateSlidesContainerWidth;
            UpdateSlidesContainerWidth();

            return child;
        }

        public virtual async Task RemoveSlide(TSlideTemplate child)
        {
            if (child.Parent == null)
            {
                Log.For(this).Error("[Carousel Slide] the current child is not exist in the specefic carousel");
                return;
            }

            dataSource.Remove(child.Item.Value);
            await SlidesContainer.Remove(child);
            UpdateSlidesContainerWidth();
        }

        void UpdateSlidesContainerWidth()
        {
            var slides = SlidesContainer.CurrentChildren<TSlideTemplate>();
            var lastChild = slides.LastOrDefault();

            slides.Do(slide => slidesActualWidth.Add(slide.ActualWidth));

            SlidesContainer.Width(lastChild.ActualX + lastChild.ActualWidth);
        }

        public Task Next(bool animate = true) => MoveToSlide(CurrentSlideIndex + 1, animate);

        public Task Previous(bool animate = true) => MoveToSlide(CurrentSlideIndex - 1, animate);

        public Task ShowFirst(bool animate = true) => MoveToSlide(0, animate);

        public Task ShowLast(bool animate = true) => MoveToSlide(CountSlides() - 1, animate);

        public async Task MoveToSlide(int index, bool animate = true)
        {
            var oldSlideIndex = CurrentSlideIndex;

            index = index.LimitMin(0);
            if (index >= CountSlides())
            {
                await SlidesEnded.Raise();
                return; // No slide available!!
            }

            var actuallyChanged = index != oldSlideIndex;

            if (actuallyChanged)
            {
                await PrepareForShiftTo(index);
                CurrentSlideIndex = index;
                await SlideChanging.Raise();
            }

            if (animate)
            {
                IsAnimating = true;
                SlidesContainer.Animate(c => SetPosition(index)).ContinueWith(x => IsAnimating = false).RunInParallel();
            }
            else
            {
                SetPosition(index);
            }

            if (actuallyChanged) await SlideChanged.Raise();
        }
        protected virtual Task PrepareForShiftTo(int slideIndex) => Task.CompletedTask;
        protected void SetPosition(int currentIndex)
        {
            currentIndex = currentIndex.LimitMax(CountSlides()).LimitMin(0);

            var x = XPositionOffset - currentIndex * ActualWidth;

            SlidesContainer.X(x);
        }

        public IEnumerable<TSource> DataSource
        {
            get => dataSource;
            set => UpdateDataSource(value).RunInParallel();
        }

        public async Task UpdateDataSource(IEnumerable<TSource> data)
        {
            dataSource = data.OrEmpty().ToList();

            await UIWorkBatch.Run(async () =>
            {
                await SlidesContainer.ClearChildren();
                foreach (var item in data)
                {
                    var slide = new TSlideTemplate();
                    slide.Item.Set(item);
                    await AddSlide(slide);
                }

                await ShowFirst(animate: false);
            });
        }

        public override void Dispose()
        {
            SlideChanging?.Dispose();
            SlideChanged?.Dispose();
            base.Dispose();
        }
    }
}

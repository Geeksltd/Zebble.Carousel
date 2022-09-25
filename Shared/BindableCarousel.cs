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

        bool IsAnimating;
        List<TSource> dataSource = new();
        List<float> slidesActualWidth = new();

        protected float CurrentSlideWidth
        {
            get
            {
                var i = GetBestMatchIndex();
                if (slidesActualWidth.Count > i)
                    return slidesActualWidth[i];
                return ActualWidth;
            }
        }

        public BindableSlidesContainer<TSource, TSlideTemplate> SlidesContainer { get; set; }
        public int CurrentSlideIndex { get; private set; }

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

            var horizontalDifference = args.From.X - args.To.X;
            var verticalDifference = args.From.Y - args.To.Y;
            if (Math.Abs(verticalDifference) > Math.Abs(horizontalDifference))
                return;

            SlidesContainer.X(SlidesContainer.X.CurrentValue - horizontalDifference);
            PrepareForShiftTo(GetBestMatchIndex()).RunInParallel();
        }
        void OnPanFinished(PannedEventArgs args)
        {
            if (SlidesContainer.Zoomed) return;
            var landOn = GetBestMatchIndex();

            MoveToSlide(landOn).RunInParallel();
        }

        float GetWidthToSlide(int slideIndex) => slidesActualWidth.Take(slideIndex).Sum() - ActualWidth / 2;
        int GetIndexToSlideWidth(float width)
        {
            float widthCount = 0;
            int index = 0;
            for (; index < slidesActualWidth.Count; index++)
            {
                widthCount += slidesActualWidth[index];
                if (widthCount >= width) break;
            }

            // 'width' is way bigger than our slides container
            return index + 1;
        }

        int GetBestMatchIndex()
        {
            if (SlidesContainer.ActualX > 0) return 0;

            var slidedWidth = Math.Abs(SlidesContainer.ActualX) + (ActualWidth / 2);
            var index = GetIndexToSlideWidth(slidedWidth);

            return index.LimitMin(0).LimitMax(CountSlides());
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

            slidesActualWidth.Clear();
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
                await ShowLast();
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

            var x = (- GetWidthToSlide(currentIndex)).LimitMax(0);

            if (SlidesContainer.ActualWidth > ActualWidth)
                x = x.LimitMin(-(SlidesContainer.ActualWidth - (ActualWidth / 2)));
            else
                x = 0;

            SlidesContainer.X(x);
        }

        public IEnumerable<TSource> DataSource
        {
            get => dataSource;
            set => UpdateDataSource(value).RunInParallel();
        }

        public Task UpdateDataSource(IEnumerable<TSource> data)
            => UIWorkBatch.Run(async () =>
            {
                dataSource.Clear();
                await SlidesContainer.ClearChildren();
                foreach (var item in data.OrEmpty().ToArray())
                {
                    var slide = new TSlideTemplate();
                    slide.Item.Set(item);
                    await AddSlide(slide);
                }

                await ShowFirst(animate: false);
            });

        public override void Dispose()
        {
            SlideChanging?.Dispose();
            SlideChanged?.Dispose();
            base.Dispose();
        }
    }
}

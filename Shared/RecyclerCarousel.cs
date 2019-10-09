namespace Zebble.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class RecyclerCarousel<TSource, TSlideTemplate> : Carousel
          where TSlideTemplate : View, IRecyclerCarouselSlide<TSource>, new()
          where TSource : class
    {
        TSource[] dataSource = new TSource[0];
        bool IsInitialized;

        public RecyclerCarousel()
        {
            SlideChanging.Handle(CreateReserveSlides);
            SlideWidthChanged.Handle(OnSlideWidthChanged);
        }

        public IEnumerable<TSource> DataSource
        {
            get => dataSource;
            set
            {
                if (IsInitialized)
                    Device.Log.Error("RecyclerCarousel.DataSource should not be set once it's initialized. Call UpdateDataSource() instead.");

                UpdateDataSource(value).RunInParallel();
            }
        }

        public async Task UpdateDataSource(IEnumerable<TSource> data)
        {
            dataSource = data.OrEmpty().ToArray();

            AdjustContainerWidth();

            if (IsInitialized)
            {
                var currentSlides = OrderedSlides.ToArray();

                await UIWorkBatch.Run(async () =>
                {
                    for (var i = currentSlides.Length - 1; i >= 0; i--)
                    {
                        var slide = currentSlides[i];
                        if (dataSource.Length <= i) await RemoveSlide(slide);
                        else
                        {
                            Item(slide).Value = dataSource[i];
                            slide.X(i * SlideWidth);
                        }
                    }

                    for (var i = currentSlides.Length; i < MaxNeededSlides && i < dataSource.Length; i++)
                        await CreateSlide(dataSource[i]);

                    await ShowFirst(animate: false);
                });
            }
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            foreach (var item in DataSource.Take(MaxNeededSlides - 1))
                await CreateSlide(item);
            IsInitialized = true;
        }

        async Task<View> CreateSlide(TSource item)
        {
            var slide = new TSlideTemplate();
            slide.Item.Set(item);
            var result = await AddSlide(slide);
            return result.X(result.ActualX);
        }

        IEnumerable<View> OrderedSlides => SlidesContainer.AllChildren.OrderBy(v => v.ActualX);

        View LeftSlide => OrderedSlides.FirstOrDefault();
        View SecondSlide => OrderedSlides.ExceptFirst().FirstOrDefault();
        View RightSlide => OrderedSlides.Skip(MaxNeededSlides - 1).FirstOrDefault();

        Bindable<TSource> Item(View slide) => slide?.AllChildren.OfType<IRecyclerCarouselSlide<TSource>>().Single().Item;

        int MaxNeededSlides
        {
            get
            {
                var canFit = 1;

                if (SlideWidth > 0)
                    canFit = (int)Math.Ceiling(ActualWidth / SlideWidth.Value) + 2;

                return canFit + 2;
            }
        }

        async Task CreateReserveSlides()
        {
            if (CurrentSlideIndex <= 0) return;
            while (SlidesContainer.AllChildren.Count < MaxNeededSlides &&
                dataSource.Length > SlidesContainer.AllChildren.Count)
            {
                // Create the next slide
                foreach (var item in DataSource.Skip(SlidesContainer.AllChildren.Count).Take(1))
                    await CreateSlide(item);
            }
        }

        async Task OnSlideWidthChanged()
        {
            if (!IsInitialized) return;

            var index = 0;
            foreach (var item in OrderedSlides.ToArray())
            {
                await OnUI(() => item.X(index * SlideWidth).Width(SlideWidth));
                index++;
            }
        }

        Task OnUI(Action action) => UIWorkBatch.Run(action);

        protected override void PrepareForShiftTo(int slideIndex)
        {
            if (dataSource.None() || slideIndex == -1) return;

            if (DataSource.ElementAtOrDefault(slideIndex) == Item(SecondSlide)?.Value)
                return; // Ideal location. No reposition necessary.

            if (slideIndex < CurrentSlideIndex) PrepareForShiftBack(slideIndex);

            if (slideIndex > CurrentSlideIndex && LeftSlide.ActualX + SlideWidth < -SlidesContainer.ActualX)
                PrepareForShiftForward(slideIndex);
        }

        void PrepareForShiftBack(int slideIndex)
        {
            var leftSlideIndex = dataSource.IndexOf(Item(LeftSlide)?.Value);
            if (leftSlideIndex <= 0) return;
            if (leftSlideIndex < slideIndex) return;

            var item = DataSource.ElementAtOrDefault(slideIndex - 1);
            if (item == null) return;

            // Move right to left
            var toRecycle = RightSlide;
            if (toRecycle == null) return;
            Item(toRecycle).Set(item);
            toRecycle.X(SlideWidth * (slideIndex - 1));
        }

        void PrepareForShiftForward(int slideIndex)
        {
            // Move far-left slide to far-right position
            var item = Item(RightSlide)?.Value;
            if (item == null) return;
            item = dataSource.SkipWhile(x => x != item).Skip(1).FirstOrDefault();
            if (item == null) return;

            var toRecycle = LeftSlide;
            Item(toRecycle).Set(item);
            toRecycle.X(RightSlide.ActualRight);
        }

        protected override int CountSlides() => dataSource.Length;
    }
}
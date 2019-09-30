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
            SlideChanging.Handle(OnSlideChanging);
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
                if (dataSource.Any()) await CreateOrUpdateSlide(LeftSlide, 0);
                if (dataSource.HasMany()) await CreateOrUpdateSlide(SecondSlide, 1);
                if (dataSource.Length > 2) await CreateOrUpdateSlide(RightSlide, 2);

                if (RightSlide != null && dataSource.Length < 3) await RightSlide.RemoveSelf();
                if (SecondSlide != null && dataSource.Length < 2) await SecondSlide.RemoveSelf();
                if (LeftSlide != null && dataSource.None()) await LeftSlide.RemoveSelf();

                ShowFirst(animate: false);
            }
        }

        async Task CreateOrUpdateSlide(View view, int index)
        {
            var item = dataSource[index];
            if (view is null) view = await CreateSlide(item);
            else Item(view).Value = item;
            view.X(index * SlideWidth);
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

        View LeftSlide => SlidesContainer.AllChildren.OrderBy(v => v.ActualRight).FirstOrDefault();
        View SecondSlide => SlidesContainer.AllChildren.OrderBy(v => v.ActualRight).ExceptFirst().FirstOrDefault();
        View RightSlide => SlidesContainer.AllChildren.OrderBy(v => v.ActualRight).Skip(MaxNeededSlides - 1).FirstOrDefault();

        TSource CurrentItem => DataSource.ElementAtOrDefault(CurrentSlideIndex);

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

        async Task OnSlideChanging()
        {
            if (CurrentSlideIndex > 0 && SlidesContainer.AllChildren.Count < MaxNeededSlides)
            {
                // Create the next slide
                foreach (var item in DataSource.Skip(SlidesContainer.AllChildren.Count).Take(1))
                    await CreateSlide(item);
            }

            await EnsureMiddleSlideIsCurrent();
        }

        async Task OnSlideWidthChanged()
        {
            if (!IsInitialized) return;

            var index = 0;
            foreach (var item in SlidesContainer.AllChildren.OrderBy(v => v.ActualX).ToArray())
            {
                await OnUI(() => item.X(index * SlideWidth).Width(SlideWidth));
                index++;
            }
        }

        Task OnUI(Action action) => Zebble.UIWorkBatch.Run(action);

        async Task EnsureMiddleSlideIsCurrent()
        {
            if (Item(SecondSlide)?.Value == CurrentItem) return;

            if (CurrentItem == Item(LeftSlide)?.Value)
            {
                if (CurrentSlideIndex <= 0) return;

                var item = DataSource.ElementAtOrDefault(CurrentSlideIndex - 1);
                if (item == null) return;

                // Move right to left
                var toRecycle = RightSlide;
                if (toRecycle == null) return;
                Item(toRecycle).Set(item);
                var rightSide = LeftSlide.ActualX - SlideWidth;
                await OnUI(() => toRecycle.X(rightSide));
            }
            else
            {
                // Move far-left slide to far-right position
                var item = Item(RightSlide)?.Value;
                if (item != null) item = dataSource.SkipWhile(x => x != item).Skip(1).FirstOrDefault();
                if (item != null)
                {
                    var toRecycle = LeftSlide;
                    Item(toRecycle).Set(item);
                    await OnUI(() => toRecycle.X(RightSlide.ActualRight));
                }
            }
        }

        protected override int CountSlides() => dataSource.Length;
    }
}
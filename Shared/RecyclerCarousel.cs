namespace Zebble.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
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
                if (dataSource.HasMany()) await CreateOrUpdateSlide(MiddleSlide, 1);
                if (dataSource.Length > 2) await CreateOrUpdateSlide(RightSlide, 2);

                if (RightSlide != null && dataSource.Length < 3) await RightSlide.RemoveSelf();
                if (MiddleSlide != null && dataSource.Length < 2) await MiddleSlide.RemoveSelf();
                if (LeftSlide != null && dataSource.None()) await LeftSlide.RemoveSelf();

                ShowFirst(animate: false);
            }
        }

        async Task CreateOrUpdateSlide(View view, int index)
        {
            TSource item = dataSource[index];
            if (view is null) view = await CreateSlide(item);
            else Item(view).Value = item;
            view.X(index * SlideWidth);
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();
            foreach (var item in DataSource.Take(2)) await CreateSlide(item);
            IsInitialized = true;
        }

        async Task<View> CreateSlide(TSource item)
        {
            var slide = new TSlideTemplate();
            slide.Item.Set(item);
            var result = await AddSlide(slide);
            return result.X(result.ActualX);
        }

        float FirstSlideRight => SlidesContainer.AllChildren.MinOrDefault(v => v.ActualRight);

        View LeftSlide => SlidesContainer.AllChildren.OrderBy(v => v.ActualRight).FirstOrDefault();
        View MiddleSlide => SlidesContainer.AllChildren.OrderBy(v => v.ActualRight).ExceptFirst().FirstOrDefault();
        View RightSlide => SlidesContainer.AllChildren.OrderBy(v => v.ActualRight).Skip(2).FirstOrDefault();

        TSource CurrentItem => DataSource.ElementAtOrDefault(CurrentSlideIndex);

        Bindable<TSource> Item(View slide) => (slide?.AllChildren.Single() as IRecyclerCarouselSlide<TSource>)?.Item;

        async Task OnSlideChanging()
        {
            if (CurrentSlideIndex > 0 && SlidesContainer.AllChildren.Count < 3)
            {
                // Create the 3rd slide
                foreach (var item in DataSource.Skip(2).Take(1))
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
            if (Item(MiddleSlide)?.Value == CurrentItem) return;
            else if (CurrentItem == Item(RightSlide)?.Value)
            {
                var item = DataSource.ElementAtOrDefault(CurrentSlideIndex + 1);
                if (item == null) return;

                // Move left to right
                var toRecycle = LeftSlide;
                Item(toRecycle).Set(item);
                await OnUI(() => toRecycle.X(RightSlide.ActualRight));
            }
            else if (CurrentItem == Item(LeftSlide)?.Value && CurrentSlideIndex > 0)
            {
                var item = DataSource.ElementAtOrDefault(CurrentSlideIndex - 1);
                if (item == null) return;

                // Move right to left
                var toRecycle = RightSlide;
                if (toRecycle == null) return;
                Item(toRecycle).Set(item);
                var rightSide = LeftSlide.ActualX - SlideWidth;
                await OnUI(() => toRecycle.X(rightSide));
            }
        }

        protected override int CountSlides() => dataSource.Length;
    }

    public interface IRecyclerCarouselSlide<TSource>
    {
        Bindable<TSource> Item { get; }
    }

    public abstract class RecyclerCarouselSlide<TSource> : Stack, IRecyclerCarouselSlide<TSource>
    {
        public Bindable<TSource> Item { get; } = new Bindable<TSource>();
    }
}

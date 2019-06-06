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

        public RecyclerCarousel() => SlideChanging.Handle(OnSlideChanging);

        public IEnumerable<TSource> DataSource
        {
            get => dataSource;
            set
            {
                dataSource = value.OrEmpty().ToArray();
                
                if (IsInitialized)
                {
                    if (value.Any()) SetSlide(LeftSlide, value.First());
                    if (value.HasMany()) SetSlide(MiddleSlide, value.ExceptFirst().First());
                    if (value.ExceptFirst().HasMany()) SetSlide(RightSlide, value.Skip(2).First());               
                }                    
            }
        }

        void SetSlide(View view, TSource item)
        {
            if (view is null) CreateSlide(item);
            else Item(view).Value = item;
        }

        public override async Task OnInitializing()
        {
            await base.OnInitializing();
            foreach (var item in DataSource.Take(2)) await CreateSlide(item);
        }

        async Task CreateSlide(TSource item)
        {
            var slide = new TSlideTemplate();
            slide.Item.Set(item);
            await AddSlide(slide);
            var container = SlidesContainer.AllChildren.Last();
            container.X(container.ActualX);
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

            EnsureMiddleSlideIsCurrent();
        }

        void EnsureMiddleSlideIsCurrent()
        {
            if (Item(MiddleSlide)?.Value == CurrentItem) return;
            else if (CurrentItem == Item(RightSlide)?.Value)
            {
                var item = DataSource.ElementAtOrDefault(CurrentSlideIndex + 1);
                if (item == null) return;

                // Move left to right
                Item(LeftSlide.X(RightSlide.ActualRight)).Set(item);
            }
            else if (CurrentItem == Item(LeftSlide)?.Value && CurrentSlideIndex > 0)
            {
                var item = DataSource.ElementAtOrDefault(CurrentSlideIndex - 1);
                if (item == null) return;

                // Move right to left
                var rightSide = LeftSlide.ActualX - SlideWidth;
                Item(RightSlide.X(rightSide)).Set(item);
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

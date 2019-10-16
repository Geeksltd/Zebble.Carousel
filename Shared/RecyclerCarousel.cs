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
        List<View> SlideRecycleBin = new List<View>();
        List<View> BulletRecycleBin = new List<View>();
        int? BusyPreparingFor;

        public RecyclerCarousel() => SlideWidthChanged.Handle(OnSlideWidthChanged);

        public IEnumerable<TSource> DataSource
        {
            get => dataSource;
            set
            {
                if (IsInitialized)
                {
                    Device.Log.Error("RecyclerCarousel.DataSource should not be set once it's initialized. Call UpdateDataSource() instead.");
                    UpdateDataSource(value).RunInParallel();
                }
                else dataSource = value.ToArray();
            }
        }

        public async Task UpdateDataSource(IEnumerable<TSource> data)
        {
            dataSource = data.OrEmpty().ToArray();

            AdjustContainerWidth();

            if (!IsInitialized) return;

            var currentSlides = OrderedSlides.ToArray();
            for (var i = 0; i < currentSlides.Length; i++)
            {
                var slide = currentSlides[i];
                if (dataSource.Length <= i) await MoveToRecycleBin(slide);
                else Item(slide.X(i * SlideWidth)).Value = dataSource[i];
            }

            await CreateSufficientSlides();
            await UpdateBullets();

            await ShowFirst(animate: false);
        }

        async Task UpdateBullets()
        {
            if (!ShowBullets) return;

            var toAdd = dataSource.Length - BulletsContainer.AllChildren.Count;

            if (toAdd >= 0) for (var i = 0; i < toAdd; i++) await AddBullet();
            else for (var i = 0; i > toAdd; i--)
                {
                    var bullet = BulletsContainer.AllChildren.LastOrDefault();
                    if (bullet == null) continue;

                    await bullet.Ignored().MoveTo(Root);
                    BulletRecycleBin.Add(bullet);
                }
        }

        async Task CreateSufficientSlides()
        {
            while (true)
            {
                var created = SlidesContainer.AllChildren.Count;

                var slideX = SlideWidth.Value * created;
                if (slideX > ActualWidth + SlideWidth.Value) return;

                var nextItem = dataSource.ElementAtOrDefault(created);
                if (nextItem == null) return;

                await CreateSlide(nextItem);
            }
        }

        Task MoveToRecycleBin(View slide)
        {
            return UIWorkBatch.Run(async () =>
           {
               // Recycle it instead.
               await slide.Ignored().MoveTo(Root);
               SlideRecycleBin.Add(slide);
           });
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            await CreateSufficientSlides();
            await UpdateBullets();
            IsInitialized = true;
        }

        async Task<View> CreateSlide(TSource item)
        {
            var result = SlideRecycleBin.FirstOrDefault();

            await UIWorkBatch.Run(async () =>
             {
                 if (result != null)
                 {
                     SlideRecycleBin.Remove(result);
                     await result.Ignored(false).MoveTo(SlidesContainer);
                     Item(result).Set(item);
                     result.X(dataSource.IndexOf(item) * SlideWidth);
                 }
                 else
                 {
                     var slide = new TSlideTemplate();
                     slide.Item.Set(item);
                     result = await AddSlide(slide);
                     result.X(result.ActualX);
                 }
             });

            return result;
        }

        protected override async Task AddBullet()
        {
            if (!ShowBullets) return;

            var result = BulletRecycleBin.FirstOrDefault();

            if (result == null)
            {
                await base.AddBullet();
                return;
            }

            BulletRecycleBin.Remove(result);
            await result.Ignored(false).MoveTo(BulletsContainer);
            if (BulletsContainer.CurrentChildren.Count() > 1) BulletsContainer.Visible();
        }

        Bindable<TSource> Item(View slide) => slide?.AllChildren.OfType<IRecyclerCarouselSlide<TSource>>().Single().Item;

        IOrderedEnumerable<View> OrderedSlides => SlidesContainer.AllChildren.OrderBy(x => x.X.CurrentValue);

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

        protected override async Task PrepareForShiftTo(int slideIndex)
        {
            while (BusyPreparingFor.HasValue)
            {
                if (BusyPreparingFor == slideIndex) return;
                await Task.Delay(Animation.OneFrame);
            }

            try
            {
                BusyPreparingFor = slideIndex;
                var min = slideIndex - 1;
                var max = (slideIndex + ConcurrentlyVisibleSlides).LimitMax(dataSource.Length);

                for (var i = min; i <= max; i++)
                    await RenderSlideAt(i);
            }
            finally { BusyPreparingFor = null; }
        }

        async Task RenderSlideAt(int slideIndex)
        {
            if (dataSource.None() || slideIndex < 0) return;

            var dataItem = DataSource.ElementAtOrDefault(slideIndex);
            if (dataItem == null) return;

            var slideX = slideIndex * SlideWidth;
            var slidesAtPosition = SlidesContainer.AllChildren.Where(v => v.X.CurrentValue == slideX).ToArray();

            foreach (var extra in slidesAtPosition.ExceptFirst().ToArray())
                Device.Log.Error("More than one Slide found for " + slideX);

            var slide = slidesAtPosition.FirstOrDefault();
            if (slide != null) return;

            slide = GetRecyclableSlide(favourLeft: slideIndex > CurrentSlideIndex);
            if (slide != null) Item(slide.X(slideX)).Set(dataItem);
            else await CreateSlide(dataItem);
        }

        View GetRecyclableSlide(bool favourLeft)
        {
            View fromLeft()
            {
                var farLeft = OrderedSlides.FirstOrDefault();
                var keepDownTo = -SlidesContainer.X.CurrentValue - SlideWidth.Value;
                return farLeft?.ActualX < keepDownTo ? farLeft : null;
            }

            View fromRight()
            {
                var farRight = OrderedSlides.LastOrDefault();
                var keepUpTo = SlideWidth.Value * ConcurrentlyVisibleSlides - SlidesContainer.X.CurrentValue;
                return farRight?.ActualX > keepUpTo ? farRight : null;
            }

            if (favourLeft) return fromLeft() ?? fromRight();
            else return fromRight() ?? fromLeft();
        }

        protected override int CountSlides() => dataSource.Length;
    }
}